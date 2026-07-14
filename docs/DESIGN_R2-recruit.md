# R2-recruit: 中ボス／章ボスの仲間化・ショップ解放メタ進行

> 設計: Claude (Cowork) / 実装: Codex・Claude Code（または Cowork+Unity MCP） / 2026-05-31
> 状態: ✅ 実装済み（2026-05-31, 章1-2＋フレームワーク, Compilation OK）。残: 実機検証・R3-balance。実装メモは末尾「実装記録」。
> 依存: [DESIGN_R1-meta.md](DESIGN_R1-meta.md)（永続roster）, [DESIGN_chapter2.md](DESIGN_chapter2.md)（章構造）, R1-persist（SaveManager）
> 関連: [RELEASE_PLAN.md](RELEASE_PLAN.md) R2-chapters, [ROADMAP.md](ROADMAP.md) E1/差別化

## ゴール

「ボスを倒して仲間を解放し、ショップで使えるようにする」回遊ループを成立させる。具体的には:

1. **中ボスラウンド**で同コストのボスを2〜3体出現させ、撃破後に**1体を選んで解放**する（そのチャプター内のみ有効）。ステージ2＝コスト3、ステージ3・4＝コスト4。
2. **章ボス（最終ボス）**は選択なしで撃破するだけで、その章ボスを**恒久解放**（次チャプター・次ラン以降もショップに出る）。章1〜5の章ボスはコスト4、章6以降はコスト5。
3. 解放したユニットは**最初からベンチに乗らない**。**ショップにプレイアブルとして出現**するようになるだけ（無料コピーは付与しない）。
4. 既存ユニットの一部（コスト3の約6体）と**コスト4・5の全て**を既定では**非プレイアブル（ショップ非出現）**にし、上記のボス解放で初めて使えるようにする。

## 影響範囲

- **変更ファイル**:
  - `Assets/Scripts/GameManager.cs`（解放判定・中ボス候補生成・章ボス報酬・章開始時リセット。中〜大）
  - `Assets/Scripts/Save/SaveManager.cs` / `SaveData.cs`（恒久ショップ解放の保持。`bossAllies` を流用 or `unlockedShopUnitIds` を追加。小）
  - 中ボス選択UIは既存 `BossRewardSelectionUI` を流用（理想は無改修）。
  - `BuildChapter1Rounds` / `BuildChapter2Rounds` の中ボス・章ボス配置を新ルールに合わせて差し替え。
- **既存依存API（壊さないこと）**:
  - `GameManager.IsEntityUnlockedForShop(EntityData)` — ショップ抽選フィルタの単一窓口。UIShop が依存。**ここを拡張する**。
  - `GameManager.UnlockNextShopCostTier()` / `MaxAvailableShopCost` — コスト帯ゲートは現状維持（解放制と併存）。
  - `GameManager.CreateBenchEntity(EntityData, star)` — 生成ヘルパ。
  - `WaveDefinition.IsMidBossWave` / `IsBossWave` — 種別判定。`StagedMidBoss(stage, round, placements)`。
  - `SaveManager.AddBossAlly/HasBossAlly/BossAllies` — 章ボス恒久記録。ロビーのボスアイコン表示が依存。
  - `BossRewardSelectionUI.EnsureExists().Show(options, callback)` — N択UI。
  - `IsLegionOnlySummonData` / `HasOwnedStarThreeUnit` — 既存の除外条件は維持。
  - ⚠ `ShowBossRewardSelection` / `SelectBossReward` / `GetBossRewardOptions` / `bossRewardUnitIds` / `unlockedBossRewardUnitIds` / `TryShowChapterRoster` / `OnChapterRosterSelected` は**本タスクで役割が変わる/廃止される**（下記「現状の把握」参照）。

## 現状の把握（実装済みの土台）

- ショップ解放判定は `IsEntityUnlockedForShop`：`bossRewardUnitIds`(=Snowchasermk/Solfist/Maehvmk)だけを「未選択なら隠す」。それ以外のユニットは**全コスト常時出現**（コスト帯ゲート `MaxAvailableShopCost` のみ）。→ ここを「コスト3一部＋コスト4・5を既定ロック」に拡張する。
- 章ボス（4-10, `IsBossWave`）クリア → `UnlockNextShopCostTier()` ＋ `ShowBossRewardSelection()`（3択で1体をその場で解放＋無料ベンチ付与）。さらに章クリア(`isChapterClear`)で `AddBossAlly(GetChapterBossUnitId(chapter))`（恒久roster：章1=Legion, 章2=Skyfalltyrant）。章開始時 `TryShowChapterRoster()` が roster から選んで**ベンチに無料配置**。
  - → 新仕様では「章ボス＝選択なしの恒久解放」「無料ベンチ配置はしない」に変える。`ShowBossRewardSelection`(3択)は**中ボス側へ移設**し、章ボスからは撤去。`TryShowChapterRoster`(ベンチ配置)は**廃止**（解放はショップ出現で表現）。
- 中ボス（`IsMidBossWave`：2-5,2-10,3-5,3-10,4-7,4-8,4-9）クリア → 現状 `UnlockNextShopCostTier()` のみ。→ ここに**候補選択による解放**を足す。
- 登録ユニット（コスト別、2026-05-31 時点 全47体）:
  - cost1(12): Andromeda, Antiswarm, Borealjuggernaut, Chaosknight, Christmas, vampire, valiant, Archdeacon, Backlinearcher, Auroralioness, Azuritelion, Altgeneraltier2
  - cost2(11): Candypanda, City, Crystal, Cindera, Decepticle, Umbra, Spelleater, Serpenti, Sandpanther, Protector, Taskmaster
  - cost3(10): Skindogehai, Decepticleprime, Decepticlechassis, Wolfpunch, Shadowlord, Tier2general, Kane, Malyk, Paragon, Ilenamk2
  - cost4(5): Snowchasermk, Solfist, Maehvmk, Wujin, Wraith
  - cost5(8): Embergeneral, Plaguegeneral, Skyfalltyrant, Kron, Gol, Invader, Legion, Arcana
  - cost0: Zyx（中立雑魚、ショップ除外済み）

## データ構造

### 解放の2層モデル

```csharp
// (A) 恒久解放（ラン/チャプターをまたいで保持）= 章ボス報酬。
//     既存 SaveData.bossAllies を「恒久ショップ解放リスト」として流用する。
//     SaveManager.HasBossAlly(id) == true なら恒久解放済みとみなす。
//     ※ロビーのボスアイコン表示も bossAllies を見ているので二重利用でOK。

// (B) チャプター内解放（runtime, GameManager 保持）= 中ボス報酬。
//     章開始ごとにクリアし、その章の間だけ有効。
private readonly HashSet<string> chapterUnlockedUnitIds =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase);
```

### 既定プレイアブル/ロックの定義（コスト+スターター集合で導出。アセット改変なし）

```csharp
// 最初からショップに出るコスト3（残りのcost3とcost4/5は既定ロック）。※暫定・R3-balanceで確定
private static readonly HashSet<string> Cost3StarterPlayable =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "Tier2general", "Kane", "Wolfpunch", "Paragon" };
// 上記以外のcost3（6体）= Skindogehai, Decepticleprime, Decepticlechassis, Shadowlord, Malyk, Ilenamk2 → ロック
```

### 章ボス報酬（恒久解放されるユニット。暫定割当・R2-chaptersで確定）

```csharp
// 章1〜5 = コスト4、章6以降 = コスト5。章ボスの「敵」も同コストにする（後述）。
private static readonly Dictionary<int,string> ChapterBossRewardUnitIds = new()
{
    {1,"Snowchasermk"}, {2,"Solfist"}, {3,"Maehvmk"}, {4,"Wujin"}, {5,"Wraith"},   // cost4
    {6,"Legion"}, {7,"Kron"}, {8,"Gol"}, {9,"Invader"},
    {10,"Embergeneral"}, {11,"Plaguegeneral"}, {12,"Skyfalltyrant"}, {13,"Arcana"}, // cost5
};
```

## 振る舞いの仕様

### 1. ショップ解放判定（`IsEntityUnlockedForShop` 拡張）

優先順に評価（既存の除外は維持）:
1. `IsLegionOnlySummonData` → false（従来）。`HasOwnedStarThreeUnit` → false（従来）。
2. コスト ≤ 2 → **true**（常時プレイアブル）。
3. コスト == 3：`Cost3StarterPlayable` に含む → true。含まない → `chapterUnlockedUnitIds` か `HasBossAlly` にあれば true、なければ **false**。
4. コスト ≥ 4：`chapterUnlockedUnitIds`（中ボス解放）か `SaveManager.HasBossAlly`（章ボス恒久解放）にあれば true、なければ **false**。
5. 併せて従来どおり `cost <= MaxAvailableShopCost` のコスト帯ゲートを通す（UIShop 側の既存抽選で担保。なければ本メソッドにも加える）。

> 効果：未解放のcost3(6体)・cost4・cost5はショップに出ない。中ボスで章内解放、章ボスで恒久解放されて初めて出現する。

### 2. 中ボスラウンド（候補出現→撃破→1体選択で章内解放）

- **敵編成**：中ボスラウンドは、その帯のロック対象から**2〜3体**を「ボス級ステータス（★2相当＋HP盛り）」で出現させる。
  - ステージ2（2-5, 2-10）＝ロック中の**cost3**から2〜3体。
  - ステージ3・4（3-5, 3-10, 4-7, 4-8, 4-9）＝**cost4**から2〜3体。
  - 候補は「系統（lineage）」でまとめると収集動機が出る（例：Decepticle系 Decepticleprime/Decepticlechassis をセットに）。同一ランの章内では既に解放済みの候補は除外し、重複提示を避ける。
- **クリア後**：出現した（＝撃破した）候補ユニットを `BossRewardSelectionUI.Show(candidates, OnMidBossRecruit)` で提示。プレイヤーが**1体選択**。
- **OnMidBossRecruit(selected)**：`chapterUnlockedUnitIds.Add(selected.name)` のみ（**無料ベンチ付与なし**）。`UIShop.GenerateCard()` で即ショップへ反映。`UnlockNextShopCostTier()` は従来どおり中ボスでも呼ぶ（コスト帯解放）。
- 候補が0体（全解放済み等）になったら選択UIは出さずスキップ。

### 3. 章ボス（最終ボス 4-10：選択なし・恒久解放）

- **敵**：章1〜5の章ボスは**コスト4ユニット**、章6以降は**コスト5ユニット**を、ボス級（★3相当・大護衛つき）で配置（`BuildChapterNRounds` の 4-10）。`ChapterBossRewardUnitIds[chapter]` と同一ユニットにする（倒したボスをそのまま獲得）。
- **クリア時**：選択UIは**出さない**。`SaveManager.AddBossAlly(ChapterBossRewardUnitIds[chapter], 1)` で**恒久解放**＋`UnlockNextShopCostTier()`。次チャプター・次ランのショップに恒久出現。**無料ベンチ配置はしない**（`TryShowChapterRoster` 廃止）。
- ロビーの「ボス仲間アイコン一覧」は `bossAllies` 表示のまま（収集の可視化）。

### 4. チャプター開始時のリセット

- `chapterUnlockedUnitIds.Clear()` を**章開始の初期化**で呼ぶ（`Start` のラン初期化、および `RequestStartChapter` 経由のシーン再読込後）。これで「中ボス解放はその章内のみ」が成立。恒久解放(`bossAllies`)はクリアしない。

## 受け入れ基準

- [ ] 既定で cost4・cost5 と「ロック対象cost3(6体)」がショップに出ない。cost1-2 と「スターターcost3(4体)」は出る。
- [ ] ステージ2の中ボスラウンドで cost3 が2〜3体ボスとして出現し、撃破後に1体選択 → そのユニットが**その章のショップに出る**ようになる（無料ベンチ追加はされない）。
- [ ] ステージ3・4の中ボスラウンドで cost4 が同様に解放できる。
- [ ] 章ボス撃破で選択UIは出ず、章ボスユニット（ch1-5=cost4 / ch6+=cost5）が恒久解放され、**次の章・再起動後のショップにも出る**。
- [ ] 次チャプターへ進む（またはランをやり直す）と、中ボスで解放した cost3/4 はショップから消える（章内のみ）。恒久解放した章ボスは残る。
- [ ] Compilation completed (Errors: False)。

## 実装ヒント

- `IsEntityUnlockedForShop` を単一の解放窓口として拡張すれば、UIShop 側は無改修で反映される（既に全抽選がここを通る）。
- 中ボス候補の生成は `StagedMidBoss(stage, round, placements)` に渡す `WaveEnemyPlacement[]` を、ロック対象プールから2〜3体抜き出して**★/HPを盛った敵**として組む。ボス級ステータスは既存の章ボス配置（Legion等）の盛り方を雛形にする。
- 中ボス選択UIは `BossRewardSelectionUI`（N択＋コールバック）をそのまま再利用。コールバックだけ `OnMidBossRecruit` に差し替える。
- 章ボス報酬を「倒したボス＝もらえるユニット」に揃えるため、`BuildChapter1Rounds`/`BuildChapter2Rounds` の 4-10 ボスを `ChapterBossRewardUnitIds[1/2]`（=cost4）へ変更する。現行の Legion/Skyfalltyrant(cost5) は ch6 以降の章ボスへ回す（`ChapterBossRewardUnitIds` 参照）。
- `unlockedBossRewardUnitIds`（旧・1ラン解放）は `chapterUnlockedUnitIds` に置き換え可能（同義に近い）。旧 `ShowBossRewardSelection`/`SelectBossReward`/`GetBossRewardOptions` は中ボス用に流用 or リネーム。

## 未決事項 / 注意（QUESTIONS.md にも要記載）

- **コンテンツ不足（要 R2-chapters / 新ユニット）**：cost4 は現状**5体のみ**。これだと「stage3-4 中ボス候補（2-3体×5ラウンド）」と「ch1-5 章ボス報酬（5体）」で取り合いになり、変化が乏しい。**cost4 を増やす**か、当面は候補プールの重複提示を許容する。cost5(8体)は ch6-13 を賄える。
- **章3〜5以降は未実装**：現状 `BuildChapterRounds` は章1・2のみ。本設計の「ch1-5=cost4 / ch6+=cost5」は章追加（R2-chapters）と同時に確定する。**実装可能な今のスコープは章1・2＋全体フレームワーク**。
- **R1-meta の挙動変更**：章ボスを「ベンチ無料配置」から「ショップ恒久解放」へ変える＝`TryShowChapterRoster` 廃止。R1-meta レビュー済み機能の仕様変更になるため、[DESIGN_R1-meta.md](DESIGN_R1-meta.md) にも追補し ROADMAP に記録する。
- **スターターcost3 / ロックcost3 の振り分け**と**章ボス割当**は暫定。最終確定は R3-balance / R2-chapters。
- 中ボスの「ボス級ステータス」の倍率は仮。R3-balance で調整。

## 実装記録（2026-05-31, Claude/Cowork+Unity MCP）

`GameManager.cs` 中心に実装。Compilation OK（CSエラー0）。

- **2層解放**: `chapterUnlockedUnitIds`(章内・runtime)＋`SaveManager.HasBossAlly`(恒久)。章開始＝シーン再読込で前者は自動リセット。
- **ロック判定**: `IsEntityUnlockedForShop` を書換。cost≤2=常時／cost3=`Cost3StarterPlayable`(Tier2general/Kane/Wolfpunch/Paragon)のみ常時／他cost3・cost4・cost5=`chapterUnlockedUnitIds` か `HasBossAlly` で解放時のみ。`IsLegionOnlySummonData`/`HasOwnedStarThreeUnit` の従来除外は維持。
- **中ボス**: `WaveDefinition.RecruitCandidateIds` 追加。`RecruitMidBoss(stage,round,star,a,b,c)` ヘルパで同コスト候補3体をボスとして配置。章1・2の全14中ボスを stage2=cost3(Skindogehai/Decepticleprime/Decepticlechassis/Shadowlord/Malyk/Ilenamk2)・stage3-4=cost4(新5体 Grymbeast/Cinderwraith/Draugarlord/Kingsguard/Dissonance)へ差し替え。クリア時 `ShowMidBossRecruit`→`BossRewardSelectionUI`→`OnMidBossRecruit` で1体を `chapterUnlockedUnitIds` へ（**無料ベンチ無し**）。候補が空なら選択スキップ。
- **章ボス**: 4-10 のボス本体を章報酬 cost4（ch1=Snowchasermk★3 / ch2=Solfist★3）に変更し、旧cost5ボス(Legion/Skyfalltyrant)は大護衛で残置。クリアで `ShowBossRewardSelection`(3択)を撤去し選択なし。章クリア時 `AddBossAlly(GetChapterBossRewardUnitId)` で**恒久解放**。
- **R1-meta 変更**: 章開始の自動ベンチ配置 `TryShowChapterRoster` 呼び出しを撤去（解放はショップ恒久出現で表現）。`bossAllies` は恒久解放記録＋ロビーのボスアイコン表示として継続利用。
- **【2026-05-31 追補】中ボス選択UIをカード化＋選択で1体ベンチ配置**: 当初「無料ベンチ配置なし」だったが、ユーザー要望で**選択時に1体ベンチへ配置**（`OnMidBossRecruit` に `CreateBenchEntity`、章内ショップ解放は維持）。選択UIは reference `craftable_unit.png`→`UI/Cards/unit_card` を使ったカード見た目（`BossRewardSelectionUI.CreateOption`）。
- **【cost5 ショップ除外】**: 仕様上cost5は既定ロック（HasBossAlly か章内解放のみ）。実機で出ていた原因は**旧R1-meta時代の `bossAllies` に残った cost5(Legion/Skyfalltyrant)**。セーブから cost5 boss仲間を除去して解消（save.json.bak バックアップ）。新規進行では cost5 は ch6+ 章ボス報酬でのみ解放。
- 旧 `ShowBossRewardSelection`/`SelectBossReward`/`GetBossRewardOptions`/`unlockedBossRewardUnitIds`/`TryShowChapterRoster` は未使用化（dead・将来剪定）。

**未検証（実機）**: 表示・選択UI・ロック挙動・章跨ぎの恒久解放・章内リセット。balance（中ボス3体同時の難度、章ボス★3、cost3スターター振り分け）は R3。章3-5以降・cost5章ボスは R2-chapters。

## Review (YYYY-MM-DD, Claude) — 未実施（実機確認後に追記）
