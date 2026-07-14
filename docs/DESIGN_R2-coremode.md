# R2-coremode: コア破壊モード（新モード）

> 設計: Claude (Cowork) / 実装: 後続（Codex / Claude Code / Cowork+MCP） / 2026-05-31
> 状態: 🟡 Phase 1+2+3 実装済み（モード切替・コア生成/勝敗・自動ウェーブ進行・区切りボス解放・コアHP/カウントダウンHUD）。残: 実機スモークテスト・balance調整（コアHP/ウェーブ強度/区切り間隔）。
>
> Phase 3 実装メモ（2026-06-02）:
> - 区切りボス解放: `CompleteCoreWave` で `coreWavesCleared % CoreModeBossMilestone(=5) == 0` のとき `ReleaseNextCoreModeBoss()`＝`GetAllChapterBossRewardUnitIds()` 順で未解放の1体を `SaveManager.AddBossAlly(id,1)` 恒久解放＋`ScorePopupUI` 告知＋`OnRosterChanged`。
> - コア戦HUD: 新規 `CoreModeHudUI.cs`（EnsureExists/手続き生成、sortingOrder=48000、ScaleWithScreenSize 1920x1080）。上部左=自コアHPバー／上部右=敵コアHPバー（Filled Image＋現/最大HP）、中央上部=フェーズ＆ウェーブ番号。`SetCores/SetPhase/SetWaveInfo`、LateUpdate で `HealthRatio`/`CurrentHealth` を反映。GameManager から `SpawnCores`(初期表示)・`StartCoreWave`(戦闘中)・`CoreAutoAdvanceRoutine`(小休止→編成のカウントダウン)で配線。
> - 注: Phase 2 のフェーズ告知に使った `ScorePopupUI.Show(0,…)` は amount==0 で早期 return のため非表示だった。Phase 3 で HUD に置換し解消。
> - コンパイル: csErrors=0。実機の通し確認は要手動（MCP eval はシーン遷移を駆動不可）。balance は要実機調整。
>
> Phase 2 実装メモ（2026-06-02）:
> - 自動ウェーブ進行: 1波目のみ手動 FIGHT（`DebugFight` がコア戦では `StartCoreWave` へ）。波クリア（コア以外の敵を一掃）→ `CompleteCoreWave` → `CoreAutoAdvanceRoutine`（インターバル5s→編成40s→次波 `StartCoreWave`）。編成中に FIGHT で早期開始可（コルーチンは `IsRoundInProgress` で自動中断）。
> - 勝敗はコア破壊のみ: `TryEndRound` をコア戦分岐（敵全滅でクリア／味方全滅では敗北にしない）。`HasLivingEnemyBattleUnit`/`HasLivingPlayerBattleUnit` はコア除外。
> - コアHP持ち越し: `SnapshotPlayerBoardUnits` でコアを除外し、波間の全回復対象から外す。`ClearEnemyUnits` は敵コアを残す。
> - 無限ウェーブ＋スケール: `EnsureCoreWaveAvailable`/`BuildCoreWave(waveIndex)`（数 3+idx 上限10、スター 1+idx/4 上限3、idx2+でcost2混在、idx6+でcost2主体、3体に1体遠距離、列7-9でコアと非重複）。波クリアごとに収入＋経験値（章モードと同等）＋ショップ無料リロール。
> - 編成カウントダウンは暫定で `ScorePopupUI` のフェーズ告知（戦闘終了→小休止／編成タイム／残5秒からのカウントダウン）。専用HUDバーは Phase 3。
> - コンパイル: csErrors=0。実機の通し確認は要手動（MCP eval はシーン遷移を駆動不可）。
>
> Phase 1 実装メモ（2026-06-02）:
> - BaseEntity に `IsCore` / `ConfigureAsCore(int health)`（無移動・シナジー無し・高HP・1.5倍スケール。Setup 後に呼ぶ）。Melee/RangedEntity の Update 冒頭で `if (IsCore) return;`。
> - GameManager: `GameMode{Chapter,CoreAssault}` + `PendingMode`（PendingStartChapter と同方式で受け渡し）+ `CurrentMode`/`IsCoreMode`。`InitializeWaveDefinitions` で `BuildCoreModeRounds()` に分岐（敵ウェーブ1つ）。`SpawnCores()`/`SpawnCore()` が自陣(列1,行5)・敵陣(列10,行5)にコアを常設（見た目は Borealjuggernaut 流用 / HP=6000）。`UnitDead` 冒頭でコア破壊を検知し、敵コア→`HandleCoreModeVictory`（ResultPanel 流用）/ 自コア→`TriggerGameOver`。`HandleRoundTimeout` はコア戦で無効化、`ClearEnemyUnits` はコアを残す、`PlacedTeam1Count` はコアを除外。
> - LobbyUI: 「ボスラッシュ(Coming Soon)」カードを「コア戦/CORE WAR」カードに転用し、`StartCoreMode()`（PendingMode=CoreAssault → GameScene 起動）。
> - コンパイル: csErrors=0 を確認。実機の1戦通し確認はロビー「コア戦」カードから手動で要実施（MCP eval ではシーン遷移を駆動できないため自動スモーク未実施）。
> 出典: 友人フィードバック④ / 依存: E1（盤面・戦闘）, R1-persist / 関連: [DESIGN_board-gimmicks.md](DESIGN_board-gimmicks.md), RELEASE_PLAN R2-chapselect

## ゴール

敵・味方の両陣営に**コア（破壊可能な拠点）**を置き、**敵コアを破壊すれば勝ち／自コアを破壊されたら負け**の新モードを追加する。チャプター進行とは別の、短時間で決着する対戦的バトル。盤面の広さを「コアまで攻め込む／守る」攻防に使う。

## 影響範囲

- **新規ファイル**:
  - `Assets/Scripts/CoreEntity.cs`（または BaseEntity に `isCore` フラグ）— 移動しない・攻撃しない・被ターゲット可の拠点。
  - `Assets/Prefabs/Unit/Core/PlayerCore.prefab` / `EnemyCore.prefab`（高HP・スプライトは仮で流用可）。
  - `Assets/Scripts/CoreModeController.cs`（任意）— モード進行・勝敗判定の集約。
- **変更ファイル**:
  - `Assets/Scripts/GameManager.cs` — `GameMode` 切替、コア配置、勝敗判定の分岐（中〜大）。
  - `Assets/Scripts/LobbyUI.cs` — 「コア戦」モードカード→モード指定で開始。
  - UI: コアHPバー（`RoundProgressUI` か新規）、勝敗表示（`ResultPanelUI` 流用）。
- **既存依存API（壊さないこと）**:
  - 勝敗/進行: `TriggerGameOver`、`currentWaveIndex >= waveDefinitions.Count`（章クリア判定）、`team2Entities.Count == 0`（ウェーブクリア）。**コアモードではこれらを使わず別判定にする**ため、`GameMode` で分岐し既存チャプター挙動を温存する。
  - `SpawnWaveEnemy` / `Setup(Team, Node)` / `BaseEntity.TakeDamage` / ターゲット選択（`FindTarget`）。
  - `GameManager.PendingStartChapter` と同様の受け渡し方式を踏襲。

## 現状の把握

- 勝利＝全ウェーブ消化（`currentWaveIndex >= waveDefinitions.Count`）、ウェーブクリア＝`team2Entities.Count==0`、敗北＝自軍全滅で `TriggerGameOver`。
- モードは未分岐（実質チャプターのみ）。ロビーに Chapter/BossRush/UnitFormation のカード枠はあるが挙動分岐は未実装。
- 敵は `SpawnWaveEnemy` で `Team.Team2` として生成。ユニットの `FindTarget` は敵チームを狙う。

## データ構造

```csharp
public enum GameMode { Chapter, CoreAssault }   // 既定 Chapter（従来挙動）
public static GameMode PendingMode = GameMode.Chapter;   // ロビー→シーン再読込で受け渡し

// コア：移動も通常攻撃もしないが、敵チームから「敵ユニット」として狙われ・被ダメするBaseEntity。
//   isCore=true / movementSpeed=0 / 攻撃しない / 高HP / range=0。死亡で勝敗判定をトリガ。
```

## 振る舞いの仕様（確定）

- **開始**: チャプターと同様、開始時にコイン＋ランダムなコスト1ユニット1体を付与（`GrantStartingUnit` 流用）。
- **配置**: 自コアを自陣最奥（列1付近の中央）、敵コアを敵陣最奥（列10付近の中央）に1基ずつ配置。**コアHPはウェーブをまたいで持ち越し**（戦闘ごとにリセットしない）。
- **進行（自動ウェーブ）**:
  - **1ウェーブ目のみ**プレイヤーが任意のタイミングで戦闘開始（FIGHT）できる。
  - 2ウェーブ目以降は時間経過で自動進行：**戦闘終了 → インターバル5秒 → ボード編成時間40秒 → 次ウェーブ自動開始**。編成時間中にショップ/配置を行う。
  - 各ウェーブの戦闘はそのウェーブの敵を一掃すると終了（自コアが生存していれば）。
- **戦闘/ターゲット**: 両軍ユニットは交戦。**ターゲット優先は基本「最も近い敵ユニット/オブジェクト」**で、コアも被ターゲット対象（射程に入れば攻撃される）。一部ユニットには例外的なターゲット規則を許容（後で個別設定可）。敵ユニットは前進して自コアを攻撃しうる＝放置すると自コアが削られる。
- **勝敗（即時判定）**: **敵コアHP0で勝利**（`ResultPanelUI` で勝利＋スコア/タイム記録）。**自コアHP0で即敗北**（`TriggerGameOver` 相当）。`GameMode.CoreAssault` の時のみこの判定を使い、`team2Entities.Count==0` や全ウェーブ消化での終了は無効化。
- **区切りステージ＆ボス解放**: チャプターは毎クリアでボス解放だが、**本モードは「区切りのいいステージ」（例: 5ウェーブごと等のマイルストーン）到達時にボス解放**（R1-collection の `AddBossAlly` を区切りでのみ呼ぶ）。難易度も区切りで段階上昇。
- **UI**: 画面上部に自/敵コアのHPバー、編成時間/インターバルのカウントダウン表示。開戦/勝敗演出は既存流用。
- **コアの挙動**: 移動・通常攻撃なし（純拠点）。v1は無能力。将来コア固有の妨害（範囲オーラ等）を付与可能（後続）。
- **盤面ギミック併用**: 配置フォーメーション（②）・転がる巨大物（③b）も機能（発生条件はモード別に調整可）。

## 受け入れ基準

- [ ] ロビーに「コア戦」モードがあり、選ぶとコアモードで開始する（チャプターは従来どおり）。
- [ ] 自陣・敵陣にコアが1基ずつ配置され、HPバーが表示される。
- [ ] 味方ユニットが敵コアを攻撃でき、敵コアHP0で勝利／自コアHP0で敗北になる（既存の「敵全滅＝クリア」では終了しない）。
- [ ] チャプターモードの既存挙動が一切変わらない（`GameMode.Chapter` 分岐で温存）。
- [ ] Compilation completed (Errors: False)。

## 実装ヒント

- コアは `BaseEntity` を雛形に「移動しない（movementSpeed=0、Update で移動・索敵しない）」「攻撃しない」「被ターゲット可（`CanBeTargeted`）」にする。`MeleeEntity`/`RangedEntity` の Update を持たせず、純粋な被弾オブジェクトにするのが安全。
- 勝敗フックはコアの `Die`/HP0 で `GameMode.CoreAssault` のとき専用処理（勝利 or `TriggerGameOver`）。
- モード受け渡しは `PendingStartChapter` と同じ静的フラグ方式（`PendingMode`）。`GameManager.Start` で分岐し、コアモードなら `InitializeWaveDefinitions` の代わりにコアモード初期化（コア生成＋敵編成）。
- 既存のチャプター進行コードは触らず、`if (PendingMode == CoreAssault) { ... return; }` の早期分岐で隔離するとデグレ最小。

## 確定事項（2026-05-31, ユーザー決定）

1. **進行**: 開始時コイン＋コスト1付与。1ウェーブ目のみ手動FIGHT、以降は「戦闘終了→インターバル5秒→編成40秒→次ウェーブ自動開始」の時間進行。コアHPはウェーブ持ち越し。
2. **ターゲット優先**: 基本は最も近い敵ユニット/オブジェクト（コア含む）。一部ユニットに例外規則を許容。
3. **敗北**: 自コア破壊で即敗北（ライフ制なし）。勝利は敵コア破壊。
4. **メタ連携**: 本モードは「区切りのいいステージ」(マイルストーン)到達でボス解放（チャプターは毎クリア解放）。
5. **解放条件**: モードは最初から選択可。

## 未決（実装時に詰める細部・暫定で進めてよい）

- 区切りの間隔（例: 5ウェーブごと）と、解放するボスの対象。→ 暫定: 5ウェーブごとに `ChapterBossRewardUnitIds` の順で1体。
- コアの初期HP・敵ウェーブの強さカーブ。→ R3-balance。
- ターゲット例外を持つユニット（遠距離はコアを優先 等）。→ 後続の個別調整。

## Review (YYYY-MM-DD) — 未実施
