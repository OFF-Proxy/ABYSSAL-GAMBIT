# chapter2: チャプター2（全33ラウンド / 最終ボス Skyfalltyrant）

> 設計＆実装: Claude (Cowork+Unity MCP) / 2026-05-31 / 依存: E1（チャプター/ラウンド構造）, R1-persist（SaveManager）, R1-meta（ボス仲間 roster）
> 関連: [DESIGN_lobby.md](DESIGN_lobby.md), [DESIGN_handoff_claudecode.md](DESIGN_handoff_claudecode.md)
> 本書は **実装済み** の記録＋エディタ検証の指針。コードは `GameManager.cs` に入っており Compilation OK（Errors 0）。

## ゴール
チャプター1クリア後に挑める2つ目の章を追加する。章1より一段強い編成で、最終ボスは Skyfalltyrant。撃破で永続 roster に Skyfalltyrant が加入する。

## 影響範囲
- 変更ファイル: `Assets/Scripts/GameManager.cs`
  - `ChapterBossUnitIds` に `{ 2, "Skyfalltyrant" }` を追加（章クリアで `AddBossAlly` される）。
  - `BuildChapterRounds(int)` に `case 2: BuildChapter2Rounds();` を追加。
  - `BuildChapter2Rounds()` を新規追加（全33ラウンド・4ステージ）。
  - 章開始の受け渡し（`PendingStartChapter` / `RequestStartChapter`）は [DESIGN_lobby.md](DESIGN_lobby.md) 側で追加。
- 既存依存API（壊さないこと）: `StagedCombat/StagedMidBoss/StagedBoss/StagedEvent`、`WaveEnemyPlacement`、`WaveEnemyKind.Cost1/2Melee/Ranged`、`WaveEventType.AugmentSilver/Gold/Prism・BonusItem・BonusGold`、`UnlockNextShopCostTier()`（中ボス/ボス撃破で自動発火）、`GetChapterBossUnitId()`。

## 現状の把握（既存の土台を踏襲）
- 章1 `BuildChapter1Rounds()` と同じ「4ステージ（3/10/10/10）= 33ラウンド」構造・同じ座標規約（列6-10・行3-7）。
- cost3 以上の敵は **名前指定**（`WaveEnemyKind` は cost1/2 のみ対応）。章2も既存登録ユニットを名前指定で使用（Arcana は未登録のため不使用）。
- コスト解放ゲートは章非依存。中ボス2-5→cost4、2-10→cost5 と章1同様に `UnlockNextShopCostTier()` が発火する。

## 振る舞いの仕様（編成意図）
- Stage1(1-1〜1-3): 肩慣らし。Zyx＋cost1群れに cost2近接を早めに混在。ドロップで資源確保。
- Stage2(2-1〜2-10): cost2-3中心。中ボス 2-5=Wraith(cost4)・2-10=Wujin(cost4)。イベント 2-3=シルバー / 2-8=アイテム。
- Stage3(3-1〜3-10): cost3-4。中ボス 3-5=Wujin+Wraith・3-10=Kron(cost5)。イベント 3-3=ゴールド / 3-7=アイテム。
- Stage4(4-1〜4-10): cost4-5。中ボス 4-7=Invader・4-8=Gol+Kron・4-9=Plaguegeneral+Embergeneral。イベント 4-3=プリズム / 4-6=ゴールド。
- **章ボス 4-10**: Skyfalltyrant(★2) ＋ 大護衛（章1ボス Legion・Invader・Kron・Gol）。撃破で報酬選択＋章クリア → Skyfalltyrant が roster 加入。

## 受け入れ基準
- [x] `BuildChapterRounds(2)` が33ラウンドを構築（compile OK）。
- [x] **静的検証済み（2026-05-31 Claude Code）**：名前指定ユニット20種（Zyx/Shadowlord/Kane/Malyk/Paragon/Ilenamk2/Tier2general/Wraith/Wujin/Decepticleprime/Solfist/Maehvmk/Snowchasermk/Kron/Gol/Invader/Plaguegeneral/Embergeneral/Skyfalltyrant/Legion）が `Entity Database.asset` に各1件存在。全86配置の座標が列6-10・行3-7内（範囲外ゼロ）。ステージ構成 3/10/10/10=33。残りの「ロビーから開始→4ステージ実進行」は**実機検証が必要（Cowork）**。
- [x] 4-10 撃破で章クリア処理が走る経路を確認（`StagedBoss(4,10)` が最終ラウンド＋`ChapterBossUnitIds[2]="Skyfalltyrant"`→章クリアで `AddBossAlly("Skyfalltyrant")`。章1と同一経路）。実 roster 反映は実機検証（Cowork）。
- [x] Compilation completed (Errors: False)

## 実装ヒント / 注意
- 使用ユニット名は全て章1で実績のある登録済みユニット（Legion/Skyfalltyrant/Gol/Kron/Invader/Plaguegeneral/Embergeneral/Wujin/Wraith/Snowchasermk/Solfist/Maehvmk/Shadowlord/Kane/Malyk/Paragon/Ilenamk2/Tier2general/Decepticleprime/Zyx）。
- 数値（★・配置）は仮。難易度の最終調整は R3-balance に委ねる。プレイ検証でヌルい/理不尽なら ★ と護衛数で調整。
- **Arcana を章2（または章3）の最終ボスにしたい場合**は、先に Arcana を Entity Database へ登録する必要がある（cost5 / synergy=Finality 等 / icon=`Images/Units/Icon/T5/Arcana`）。これは別タスク（エディタで安全に実施）として [DESIGN_handoff_claudecode.md] に記載。

## 未決事項
- 章2開始時にコスト上限を3へ戻す現仕様（章1と同じ）でよいか、章2は最初から高コスト解放にするかは要プレイ判断（R3-balance）。
