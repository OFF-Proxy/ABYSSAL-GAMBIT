# DESIGN_R3-chest-room — チェスト報酬ラウンド（殴って開けるサンドバッグ宝箱）

> task-id: R3-chest-room / 作成: 2026-06-25 / 対象: 報酬ラウンド
> 要望: アイテム報酬の回を、盤面に宝箱（サンドバッグ）を置いて殴って開ける部屋にする。
> 決定（ユーザー回答）: ①アイテム報酬(3択)の回を置換 / ②コインチェストは章で増やす / ③アイテムチェストは3択 / ④制限時間30秒。
> 素材: `Assets/Images/Units/others/item_treasurechest`（=アイテム箱）/ `item_treasurechest_festive`（=コイン箱）。
> 関連: [DESIGN_midboss-nodes.md](DESIGN_midboss-nodes.md) / [DESIGN_R2-rewards.md](DESIGN_R2-rewards.md)

---

> 訂正(2026-06-25): 当初 `ItemChoice3`（中ボス報酬）を対象にしたが、**ボス/戦闘ラウンドに影響して不適切**だったため撤回。
> 対象は **アイテムが貰えるイベント回（`WaveEventType.BonusItem`＝2-8, 3-7 等）** のみ。`StagedChestRoom`（敵なしの通常戦闘ウェーブ・`IsChestRoom`）で置換し、既存のボス/中ボス/戦闘ラウンドには一切影響しない。

## 1. 概要

**アイテムが貰えるイベント回（2-8, 3-7 等の `BonusItem`）** を **チェスト部屋（殴って開けるトレジャー戦闘）** に置き換える。
盤面（敵側）に宝箱を配置。宝箱は **移動も攻撃もしないサンドバッグ**（`IsCore` と同型）。プレイヤーのユニットが殴って開ける。

- **コインチェスト**（複数・前〜中列）: 殴られて削れるたびにコインを落とし、開封でさらにコイン。1個で合計10コイン。
- **アイテムチェスト**（1個・最後列）: HP0で開封し、アイテム3択を1回。
- この回は **時間経過の与ダメージ倍率を固定（×1.0）**。**30秒**以内に全部開けられなければ、その時点の獲得分だけ受け取って次のラウンドへ。

## 2. コインチェスト（1個=最大10コイン）

- HP = H。**被弾でHPが 80/60/40/20% を割るたびに1コインドロップ**（＝計5コイン、減りに応じて1枚ずつ）。ドロップ毎に**箱を揺らし＋コインSE**。
- **HP0で開封 → さらに+5コイン**（開封演出＋コインSE）。
- 合計 5＋5 = **10コイン/個**。
- 個数: 章で増加。`coinChestCount = Mathf.Clamp(3 + (chapter-1)/3, 3, 6)`（ch1-3:3 / ch4-6:4 / ch7-9:5 / ch10+:6）。

## 3. アイテムチェスト（1個）

- 最後列（敵陣の最奥）に1個。HP0で開封 → **`ShowItemChoice3Reward()`（アイテム3択）を1回**。
- コインチェストより奥なので、基本はコイン箱を片付けてから到達する。

## 4. ラウンド進行

- `chestRoomActive` フラグ中:
  - `GetRoundDamageMultiplier()` は **常に1.0**（時間経過で増やさない）。
  - **30秒タイマー**。`RoundElapsedTime >= 30` で強制終了（残りチェストは未開封のまま破棄）。
  - 全チェスト撃破でも通常通りラウンド終了（team2全滅→`TryEndRound`）。
  - 報酬はチェストが**被弾/開封時に即時付与**するので、「獲得済みだけ受け取る」は自然に成立（途中終了でも落とした分は手に入っている）。
- 宝箱は敵だが**攻撃しない**ので味方は無傷。失敗条件は「時間切れで開け残し」のみ。

## 5. HPバランス（盤面の質を推定）

ラウンド開始時に**プレイヤー盤面の推定DPS**を算出し、チェストHPを決める:

- `estDPS = Σ(各配置ユニットの 有効攻撃力 × 秒間攻撃回数)`（ヒーロー含む、★・バフ反映後の実値）。
- 目標: **平均的な盤面が ~22秒で全開封**（30秒に余裕）。強い盤面は余裕、弱い盤面は一部のみ。
- 総HPプール `pool = estDPS * ChestClearTargetSeconds(=22)`。
  - コインチェスト各HP = `pool * 0.6 / coinChestCount`。
  - アイテムチェストHP = `pool * 0.4`。
- 下限・上限でクランプ（極端な編成でも 0 や過大にならないよう `Mathf.Clamp`）。
- DPS推定が取れない場合は章ベースのフォールバック `baseHP = 600 + chapter*250`。

## 6. 配置

- 敵側の配置可能行に、コインチェストを前〜中列（例 col 6〜7）に行分散、アイテムチェストを最奥（例 col 9〜10）中央。
- 既存 `SpawnCore(team, column, row)` と同様に `GridManager.GetNodeAtBoardCoordinate` でノード取得→生成。見た目プレハブは適当なタンク系を流用し、スプライトを宝箱スプライトへ差し替え（`SpriteRenderer.sprite`）。

## 7. 実装ポイント

- **`BaseEntity.ConfigureAsChest(int hp, bool isItemChest, sprite)`**: `ConfigureAsCore` と同型（無移動/無攻撃/被ターゲット）＋宝箱スプライト。被弾コールバック/開封コールバックを保持。
  - `TakeDamage` 後にHP割合を監視し、コイン箱は閾値を跨ぐ毎に `onCoinDrop()`、HP0で `onOpen()`。
  - 揺れ＝DOTween（`transform` を小さく左右）/ SE＝`AttackEffectPlayer.PlayUiSfx("coin"...)`（無ければ既存SE流用）。
- **GameManager**: `chestRoomActive`、`StartChestRoom(wave)`、`coinChestCount`、`EstimatePlayerBoardDps()`、30秒監視（Updateで `RoundElapsedTime` を見て強制終了）、コイン付与=`PlayerData.AddMoney`、アイテム=`ShowItemChoice3Reward`。
- **ウェーブ**: `ItemChoice3` スケジュールの回を `WaveDefinition.IsChestRoom=true` に。`SpawnWaveEnemy` の代わりにチェストを並べる。
- **HUD**: 残り秒数の簡易タイマー（`CoreModeHudUI` か新規の小表示）。

## 8. 検証
- realCS=0、Playスモークでチェスト生成・コインドロップ・SE・揺れ・開封(コイン/3択)・30秒終了・時間倍率固定を確認。
- バランスは実機で estDPS 係数（ChestClearTargetSeconds=22, 0.6/0.4配分）を調整。

## Status
- 2026-06-25 実装完了（part1+2＋HUDタイマー＋宝箱idleアニメ）。realCS=0。数値は調整可。
