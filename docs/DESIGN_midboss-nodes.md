# midboss-nodes: 章ボス直前の進路選択（逐次ノード選択）

> 設計: Claude / 実装: Cowork(Claude via Unity MCP) / 2026-06-17
> 関連: docs/DESIGN_R2-recruit.md（中ボス報酬）, ChapterStory.cs（中ボス異名/かませ）, docs/ROADMAP.md

## ゴール
章ボス直前の「中ボス3連続（例: 4-7/4-8/4-9）」を、**逐次の進路選択（Slay the Spire 風）**に置き換える。
3つのノードを提示 → 1つ選んで戦う（報酬獲得）→ 残り2つから1つ選んで戦う → 章ボス。
**実戦は3戦→2戦に短縮**し、能動的な取捨選択でテンポと緊張感・リプレイ性を上げる。
あわせて中ボスのかませセリフを**非ブロッキング字幕化**してテンポ低下を解消する。

## 確定方針（ユーザー合意済み）
- 選び方: **逐次選択**（1つ戦ってから次を選ぶ。盤面状況を見て選べる）。提示3・選択2・残り1は破棄（報酬も放棄）。
- 適用範囲: **章ボス直前の3連セグメントのみ**（散発中ボス 2-5/3-5/3-10 等は据え置き）。
- 中ボス＝モブ寄りの「かませ」。立ち絵なし。セリフは**流し見OK＝非ブロッキング字幕**。章ボスのみフル会話。

## 影響範囲
- 新規ファイル:
  - `Assets/Scripts/NodeSelectionUI.cs`（カード式の進路選択UI。`EnsureExists()`/JA-EN/DOTween。`BossRewardSelectionUI` を雛形に）
- 変更ファイル:
  - `Assets/Scripts/GameManager.cs`（〜120行: ノードプール構築・2枠への中身差し込み・逐次選択フロー・章ボスへの遷移・中ボス会話の字幕化分岐）
  - `Assets/Scripts/ChapterStory.cs`（任意: ノードのかませ短文1行を引く API。既存 GetMidVariant を流用可）
- 既存依存API（壊さないこと）:
  - `WaveDefinition` の各フィールド（`IsMidBossWave/IsBossWave/RewardKind/RewardCount/RewardCoins/RecruitCandidateIds/Enemies/StageIndex/RoundInStage`）
  - `SetMidBossReward(d,kind,count,coins)` / `ApplyMidBossRewardSchedule()` / `RecruitMidBoss(...)` / `StagedBoss(...)` / `StagedCombat(...)`
  - `InitializeWaveDefinitions()`（`Count>0` で再構築しないガードに依存）/ `CurrentMidBossSlot()` / `GetWavePrimaryBossId()`
  - `HeroBossDialogueUI.Show(...)`（章ボスは従来どおり）/ `ChatterSubtitleUI.Show(speaker,line)`（非ブロッキング）
  - `BossRewardSelectionUI` / `ItemRewardSelectionUI`（UI 雛形として参照のみ。改変しない）

## 現状の把握（実装済みの土台）
- 章ラウンドは `BuildChapterNRounds()` が `waveDefinitions`（フラットな `List<WaveDefinition>`）を構築。
  章ボス直前は `RecruitMidBoss(4,7,..) / RecruitMidBoss(4,8,..) / RecruitMidBoss(4,9,..)` の3連 → `StagedBoss(4,10,..)`。
- 報酬は `ApplyMidBossRewardSchedule()` が stage/round で上書き（4-7=アイテム3択 / 4-8=仲間化 / 4-9=大量コイン）。
- 戦闘開始は `DebugFight()`。中ボス/章ボス戦の前に `HeroBossDialogueUI.Show(..., onComplete=()=>DebugFight())` が**ブロッキング**で挟まる（GameManager 2103-2116）。
- `ChatterSubtitleUI`（非ブロッキング字幕）は既存（章チャッター 5212行で使用）。
- `InitializeWaveDefinitions()` は `if(waveDefinitions.Count>0) return;`（一度構築したら保持）。

## データ構造（疑似コード）
```csharp
// 進路ノード1つ分。WaveDefinition を生成するための素＋提示メタ。
public enum MidNodeArchetype { Elite, Standard, Supply }   // 🔴精鋭 / 🟡標準 / 🟢補給

[System.Serializable]
public struct MidBossNode {
    public MidNodeArchetype archetype;
    public string title;            // JA/EN は UI 側で archetype から引く
    public int difficultyStar;      // 敵スター（精鋭=高）
    public MidBossRewardKind reward;// Elite=良アイテム/高★仲間化, Standard=仲間化, Supply=CoinReward
    public int rewardCount, rewardCoins;
    public string[] recruitIds;     // RecruitMidBoss 用の3体（かませ＋仲間化候補）
    // 敵編成は recruitIds + difficultyStar から RecruitMidBoss 相当で生成
}

// ラン内状態（GameManager フィールド）
List<MidBossNode> nodePool;   // 章ごとに3つ
List<int> nodeRemaining;      // 未選択ノードの index（初期 {0,1,2}）
int nodePicksRemaining;       // 2 で開始
bool[] nodeSlotResolved;      // 2枠ぶん（選択済みフラグ）
```

## 振る舞いの仕様
1. **構築**: 各 `BuildChapterNRounds()` の章ボス直前を、`RecruitMidBoss×3` から
   - `nodePool`（3ノード）を登録する新ヘルパ `RegisterMidBossNodes(stage, nodes)`
   - `waveDefinitions` には**ノード枠を2つ**だけ追加（`IsMidBossWave=true` かつ新フラグ `IsNodeChoice=true` のプレースホルダ）
   へ置換。`ApplyMidBossRewardSchedule` はノード枠を**素通り**（中身は選択時に確定するため、s/r 一致分岐から除外）。
2. **逐次選択**: `DebugFight()` でカレントが `IsNodeChoice` のとき、戦闘開始前に
   `NodeSelectionUI.Show(remainingNodes, onPick)` を表示（**ブロッキング選択**＝ここはプレイヤー操作なのでOK）。
   - onPick(node): 選ばれた node の内容を**現在の枠 WaveDefinition に流し込む**
     （`d.RecruitCandidateIds = node.recruitIds; SetMidBossReward(d, node.reward, node.rewardCount, node.rewardCoins);`
      敵編成は `RecruitMidBoss` と同じ配置ロジックで `node.difficultyStar` を反映）。
     `nodeRemaining` から除去、`IsNodeChoice=false` にして通常戦闘へ（`DebugFight()` 再入）。
3. **かませ会話（非ブロッキング）**: 中ボス枠では `HeroBossDialogueUI`（ブロッキング）を使わず、
   `ChatterSubtitleUI.Show(midName, midLine)` を**ショップ/編成中**（onPick 直後＝戦闘開始前の準備フェーズ）に流す。
   セリフは `ChapterStory.GetMidVariant(...).lines` の先頭1行（最も“かませ”が立つ行）。**戦闘開始は待たない**。
4. **2戦終了→章ボス**: `nodePicksRemaining` が 0 になったら、未選択ノードの枠は**飛ばす**。
   実装は「枠は最初から2つだけ」なので、2枠消化＝次は自然に `StagedBoss`（章ボス）へ進む（特別な skip 不要）。
5. **章ボス**: 従来どおり `HeroBossDialogueUI`（フル会話・ブロッキング）。変更なし。

### エッジケース
- 同一ノードが2枠で重複しないこと（選択済みは `nodeRemaining` から除去）。
- セーブは不要（ラン内一時状態）。ただし `InitializeWaveDefinitions` の再構築ガードに守られている前提。
  途中でリスタート/章再入時は `nodePool/nodeRemaining/nodePicksRemaining` を章構築時に初期化する。
- AutoPlay（`AutoPlayHarness`）: `IsNodeChoice` 枠では先頭の remaining ノードを自動選択するフォールバックを足す（テンポ計測継続のため）。
- JA/EN: ノード名・難度・報酬種別ラベルを両言語。`LocalizationManager.ApplyFont`。

## 受け入れ基準
- [ ] 章ボス直前で進路選択UIが出て、3ノードから1つ選ぶと、その編成＋報酬で戦闘になる。
- [ ] 1戦クリア後にもう一度（残り2ノードから）選択が出て、2戦目を戦う。**3戦目は発生せず**章ボスへ進む。
- [ ] 選んだノードの archetype に応じて敵の強さ・報酬種別が変わる（精鋭=強い&良報酬 / 補給=易&コイン）。
- [ ] 中ボスのかませは**字幕で流れ、クリック待ちで戦闘が止まらない**。章ボスは従来どおりフル会話。
- [ ] 未選択（破棄）ノードの報酬は得られない。
- [ ] JA/EN 両方で表示崩れ・欠落なし。
- [ ] Compilation completed (Errors: False) / realCS=0。

## 実装ヒント
- UI は `BossRewardSelectionUI` を雛形にカード3枚（残り数に応じて2枚）。`EnsureExists()`/sortingOrder は既存UIに準拠。
- 敵編成生成は既存 `RecruitMidBoss(stage,round,star,a,b,c)` のロジックを共通ヘルパに切り出して node からも呼ぶと重複が減る。
- まず**章1のみ**で通し、章2/3へ横展開（章ごとの nodePool を `BuildChapterNRounds` に追記）。
- 実機テストは Unity をフォーカスして Play（バックグラウンドだとプレイヤーループが止まり検証不可）。

## 未決事項
- ノードの具体数値（精鋭の敵スター/報酬の良さ）は仮置き。最終調整は R3-balance に委ねる。
- 「補給」ノードに回復（コアHP/ヒーロー回復）を含めるかは将来拡張（まずはコイン＋Lv/利子で開始）。
