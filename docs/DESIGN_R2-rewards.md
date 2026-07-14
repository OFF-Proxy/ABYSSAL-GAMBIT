# DESIGN_R2-rewards: 中ボス報酬の刷新＋強化マス

> 設計＋実装: Claude (Cowork) / 2026-06-03（ユーザー仕様）
> 状態: ✅ 実装済み（コンパイル0）。残: 実機1周・balance(R3)。
> 背景: ボス（キャラ）解放頻度が高すぎる（特に最終ボス前の3連続中ボス）。報酬を多様化して頻度を下げる。

## 報酬スケジュール（全章共通・中ボス枠）

`ApplyMidBossRewardSchedule()` が `BuildChapterRounds` 後に (stage,round) で上書き。

| ラウンド | 報酬 |
|---|---|
| 2-5  | アイテム3択（防具/攻撃/秘力から各1ランダム→1つ取得） |
| 2-10 | ボス選択（cost3、1体） |
| 3-5  | 強化マス選択（新規） |
| 3-10 | ボス選択（cost4、1体） |
| 4-5  | ボス選択×2（cost4、2体） |
| 4-7  | アイテム3択 |
| 4-8  | アイテム3択 |
| 4-9  | 大量コイン（40 + (章-1)*20） |

→ ボス解放は 2-10 / 3-10 / 4-5(×2) のみに減少（旧: 7枠すべて解放）。コスト上限解放は従来どおり全中ボスで実施。

## 実装

- `WaveDefinition` に `RewardKind`(Recruit/ItemChoice3/BuffTile/CoinReward) ＋ `RewardCount` ＋ `RewardCoins`。
- `CompleteCurrentWave` の中ボスクリア分岐で `RewardKind` により処理を振り分け。
- ボス選択複数: `ShowMidBossRecruit(def, count)` ＋ `OnMidBossRecruit` を連鎖（残候補から count 回選ぶ）。
- アイテム3択: `ItemRewardSelectionUI`（新規）。`PickRandomItemOfCategory` で Defense/Offense/Skill から各1。選択で `ReturnItemToBench`。
- コイン: `GrantCoinReward`（AddMoney＋ポップアップ）。

## 強化マス（新規 / ユーザー確定仕様）

- 種別を選ぶ（攻撃/防御/秘力）→ 盤面の自陣マスをクリックして設置。**そのチャプター中は永続・累積**（複数獲得でマスが増える）。
- 効果（戦闘開始時、そのマス上のユニットに時限付与・暫定値）:
  - 攻撃マス: 与ダメ +30% / 攻撃速度 ×1.15
  - 防御マス: 被ダメ -20% / 開幕シールド(最大HP25%)
  - 秘力マス: 攻撃速度 ×1.25 / 与ダメ +10%（※マナ直接操作は未公開APIのため攻撃速度で近似）
- 実装: `BuffTileRewardUI`（種別3ボタン→「マスをクリック」バナー）。`GameManager`：
  - `buffTiles`(Node＋種別) を保持。`HandleBuffTilePlacementClick`（Update）でクリック→`GridManager.GetTileAtWorldPosition`→`GetNodeForTile`→自陣判定→設置。
  - `ApplyBuffTileBonuses()` を `StartFight`(DebugFight) で適用。`RebuildBuffTileMarkers()` で色付きマーカー常設表示（攻=橙/防=青/秘=紫）。
  - Node 参照で保持（ラン中グリッド不変）。コア戦は対象外。

## 受け入れ基準

- [ ] 2-5/4-7/4-8 でアイテム3択が出て1つ取れる。
- [ ] 2-10=cost3 1体 / 3-10=cost4 1体 / 4-5=cost4 2体 のボス選択。
- [ ] 3-5 で種別選択→自陣マスをクリックして強化マスが設置され、以降その章の戦闘でそのマス上のユニットがバフを受ける（マーカー表示）。
- [ ] 4-9 で大量コイン。
- [x] Compilation completed (Errors: False)。

## Review (2026-06-03)
- 静的検証のみ（コンパイル0）。各UIの実表示・強化マスのクリック設置/適用は実機確認が必要。数値はR3-balance。
