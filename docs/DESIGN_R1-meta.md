# R1-meta: 章をまたぐボス仲間化・永続roster（差別化の核）
> **状態: ✅ 実装済み（2026-05-30, レビュー承認）** — 残: 実機で章1クリア→再起動→編成→開始を1周確認 / R3-balance でボス仲間の初期★・減衰調整。

> 設計: Claude / 実装: Codex・Claude Code / 2026-05-29
> 依存: **R1-persist（先に実装）** / 関連: [MARKET_POSITIONING.md](MARKET_POSITIONING.md) §2①, [RELEASE_PLAN.md](RELEASE_PLAN.md)
> **本作の最大の差別化機能**。「倒した章ボスが仲間になり、次章以降へ連れて行ける」。

## ゴール
章ボスを倒すと、その章ボスユニットが**永続コレクション（roster）に加わり**、以降の章を開始するときに編成へ組み込める。これにより「過去章を回って強いボス仲間を集め、上位章へ挑む」回遊ループを成立させる。

## 現状の把握（実装済みの土台）
- `GameManager.bossRewardUnitIds = { "Snowchasermk", "Solfist", "Maehvmk" }`（3体固定）。
- `unlockedBossRewardUnitIds`（`HashSet`）は **1ラン内のみ**の解放。`SelectBossReward()` がショップ解放＋★1をベンチ付与。
- ボス撃破 → `ShowBossRewardSelection()` → `BossRewardSelectionUI.Show(options, SelectBossReward)`。
- → **この "ラン内解放" を "永続 roster" へ拡張するのが本タスク。**

## 影響範囲
- 新規ファイル:
  - `Assets/Scripts/UI/ChapterRosterUI.cs` — 章開始前の「ボス仲間 編成」画面
- 変更ファイル:
  - `GameManager.cs` — 章クリア時に `SaveManager.AddBossAlly`、章開始時に roster からシード付与、`SelectBossReward` の整理
  - `Save/SaveManager.cs`（R1-persist）— `BossAllies` アクセサ（既設計）
- 既存依存（壊さないこと）: `SelectBossReward` / `GetBossRewardOptions` / `CreateBenchEntity` / `ResolveUpgradesFor` / `OnRosterChanged` / `BossRewardSelectionUI`。

## 振る舞いの仕様

### A. 永続化（獲得）
1. **章クリア時**（`QueueStageResult` の `isChapterClear` 経路、もしくは章ボス撃破確定時）に、その章のボスユニット id を `SaveManager.AddBossAlly(unitId, starLevel)` で永続 roster に追加。
   - どのユニットを「章ボス」とするかは `BuildChapterRounds` の章定義に紐づける（章ごとに `chapterBossUnitId` を持たせる）。
   - `starLevel`: MVP は **1 固定**。将来「クリア時のそのボスの育成度を引き継ぐ」拡張余地（未決）。
2. 既存の `SelectBossReward`（ラン内ボス報酬＝コスト4ユニットの選択）はそのまま残す。**「ラン内報酬」と「章ボスの永続仲間化」は別物**として両立させる（前者＝そのランを強くする、後者＝次章へ持ち越す）。

### B. 章開始前の編成（ChapterRosterUI）
1. タイトル/章セレクト（R2-chapselect）から章を選ぶ → **編成画面**を表示。
2. `SaveManager.BossAllies` を一覧表示。プレイヤーは **最大 N 体**（MVP: `maxBossAlliesPerRun = 1`、将来増加可）を選んで「この章に連れて行く」。
3. 「開始」で選んだボス仲間を **ランのベンチに★1（or 記録 star）で初期配置**してから通常のラン開始。
   - 既存 `GrantStartingUnit()`（ランダムなコスト1付与）と共存。ボス仲間は別枠でベンチへ。
   - ベンチ満杯リスクに注意（`HasBenchSpace`。MVP で 1 体なら問題なし）。
4. 何も連れて行かない選択も可（縛りプレイ／スコア狙い）。

### C. 既存フローとの接続
- `GameManager.Start()` は現状いきなりラン開始。**章セレクト→編成→ラン**の前段が入る（R2-chapselect と統合）。MVP では「編成画面を挟んでから既存の Start ロジックへ」。
- ボス仲間として連れて行ったユニットは、そのランでは通常ユニットと同様に扱う（合成・装備・スターアップ可）。ランの結果は永続 roster の star を**下げない**（持ち帰り育成は将来）。

## データ構造（追加分）

```csharp
// 章定義に章ボスを明示（BuildChapterRounds 内 or WaveDefinition 側）
// 例: chapter 1 の章ボス = 4-10 のボスユニット id
private string GetChapterBossUnitId(int chapter) { /* 章→ボスid */ }

// GameManager フィールド
public int maxBossAlliesPerRun = 1;             // この章に連れて行ける上限（将来 augment 等で増やせる）
private readonly List<string> selectedBossAlliesForRun = new List<string>(); // 編成で選ばれた id
```

## 受け入れ基準
- [ ] 章1をクリア → `SaveManager.BossAllies` にその章ボスが追加され、再起動後も残る（R1-persist 連携）
- [ ] 章セレクト→編成画面でボス仲間が一覧表示され、1体選んで章を開始するとベンチにそのユニットが★1で配置される
- [ ] ボス仲間を「連れて行かない」で開始もできる
- [ ] ラン内のボス報酬選択（`SelectBossReward`）が従来どおり機能する（デグレなし）
- [ ] ベンチ満杯時に例外が出ない（`HasBenchSpace` チェック）
- [ ] `Compilation completed (Errors: False)`

## 実装ヒント
- 章ボスのユニットは既存の `bossRewardUnitIds`（Snowchasermk/Solfist/Maehvmk）を流用してもよいし、章ごとに専用の章ボスを定義してもよい（R2-chapters と合わせて決める）。**MVP は既存3体のうち章1ボス=1体を永続仲間化**で十分にループを示せる。
- 編成画面のユニット表示は `BossRewardSelectionUI` の見せ方（EntityData→カード）を流用するとUIコストが下がる。
- `CreateBenchEntity(data, starLevel)` でベンチ生成 → `ResolveUpgradesFor` の既存呼び出しに倣う。

## 未決事項（Codex への質問）
- 持ち帰り育成（章を跨いでボス仲間の★や装備を成長させる）は MVP に入れるか? → **MVP は star=1 固定・成長なし**を推奨（スコープ管理）。やり込み軸として将来 `DESIGN_R*-ally-growth.md` で別途。
- `maxBossAlliesPerRun` の初期値（1 で開始し、章進行やオーグメントで増やす案）。要相談。

---

## Review (2026-05-29, Claude) — ✅ 承認（balance フラグ付き）

実装コミット `bd61b787 feat(R1-meta)`。差別化の核がエンドツーエンドで通った。

**良い点 / 検証結果**
- `ChapterBossUnitIds = { 1: "Legion" }` + `GetChapterBossUnitId(chapter)` で章→章ボスを定義。`BuildChapter1Rounds` の 4-10 ボス（`new WaveEnemyPlacement("Legion", ...)`）と一致。
- **ループ成立をコードで検証**: `Legion` は Entity Database に **prefab 付き・cost 5** で存在し、`IsLegionOnlySummonData`（Taskmaster/Zyx のみ）の除外対象でもない → `TryShowChapterRoster` の `data.prefab != null` フィルタを通過し、編成画面に確実に出る。
- 章クリア時（`QueueStageResult` の `isChapterClear` 経路）に `AddBossAlly(chapterBossUnitId, 1)`。`Start()` で `GrantStartingUnit()` → `TryShowChapterRoster()` の順で前段に編成画面。
- `OnChapterRosterSelected` はベンチ満杯チェック（`HasBenchSpace`）あり → 受け入れ基準⑤OK。「連れて行かない」(`default`/空名)も処理。
- 既存 `SelectBossReward`（ラン内報酬）は無改変で共存 → デグレなし（受け入れ基準④）。
- `ChapterRosterUI` は `EnsureExists`/Localization/JA-EN/`OnLanguageChanged` 対応と既存 UI 慣習を踏襲。INFO ボタンで `UnitStatusPanelUI.ShowPreview` も。

**⚠️ balance フラグ（バグではない・R3-balance 送り）**
- 章ボス Legion は **cost 5** の強ユニット。これを★1とはいえ章開始時に無料配置できると、特に章1リプレイで明確に強すぎる可能性。→ **RELEASE_PLAN R3-balance** で「ボス仲間の初期★/ステータス減衰/コスト相当のハンデ」を検討。MVP の機能検証としては問題なし。

**気づき（軽微）**
- 現状チャプターは1章のみ。よって編成画面は「章1クリア後に章1を再挑戦」する時に Legion を提示する形でループを体験できる（想定どおり）。複数章は R2-chapters で。
- `maxBossAlliesPerRun`（上限1）はUI/ロジックとも1体運用。将来拡張時は複数選択UIへ。

**受け入れ基準**: ①〜⑥ 充足（コードレビュー上）。実機で「章1クリア→再起動→編成にLegion→連れて行く」を1度通すと完全確認。
