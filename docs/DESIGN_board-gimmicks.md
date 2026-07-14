# board-gimmicks: 盤面ギミック（配置フォーメーション ＋ 転がる巨大物）

> 設計＆実装: Claude (Cowork+Unity MCP) / 2026-05-31
> 状態: ✅ 実装済み（②フォーメーション / ③b 転がる巨大物）。残: 実機検証・R3-balance。
> 出典: 友人フィードバック①②③ / 関連: GameManager, BaseEntity, GridManager

「マスが多い盤面に意味を持たせる」ための2ギミック。①(盤面ギミック)は②③で具体化。

## ② 配置フォーメーション効果

戦闘開始時に**プレイヤー陣営の盤上配置の「形」**を検出し、時限バフを付与する。

- 実装: `GameManager.ApplyFormationBonuses()`（`StartFight` の `ApplyBattleStartAugmentEffects` 直後に呼ぶ）。
- グリッド判定: 各ユニットの `CurrentNode.worldPosition` を丸めて行(y)/列(x)に集計。
- フォーメーション（初期2種・数値暫定）:
  - **横一列「突撃」**: 同じ行に3体以上 → その行の各ユニットへ 攻撃速度+18%・与ダメ+15%（`ApplyAttackSpeedBoostFromSynergy` / `ApplyTimedSynergyDamageDealtBonus`、dur=60s）。
  - **縦一列「鉄壁」**: 同じ列に3体以上 → 被ダメ-12%＋シールド(最大HP12%)（`ApplyTimedSynergyDamageReductionBonus` / `ApplyShieldFromSynergy`）。
- 既存の時限バフ機構を流用（新規ステートなし）。発動は Debug.Log。
- **後続**: 2x2「方陣」や斜めなど形の追加、発動表示UI（シナジーパネル風）。数値はR3-balance。

## ③b 転がる巨大物

戦闘中に盤面の1行を端から端へ転がる障害物。経路上のユニットを**敵味方問わず**踏み潰す（ダメージ＋スタン）。

- 新規: `Assets/Scripts/RollingHazard.cs`（MonoBehaviour）。`Launch(startX,endX,y,speed,damageFraction,stunDuration)` で生成。毎フレーム横移動＋回転し、`GameManager.AllBoardEntities()` のうち矩形(±0.7×±0.95)内の未ヒットユニットへ `TakeDamage(最大HP×30%)`＋`ApplyStun(1.2s)`（1体1回）。端に到達で自壊。見た目は手続き生成の岩石円（外部素材不要）。
- 生成: `GameManager.LaunchRollingHazard()`（行3-7のいずれかをランダム、左右どちらかから）。
- 発生条件: **中ボス戦**中に `MidBossHazardRoutine`（開始4秒後と10秒後に各1回）。`StartFight` で `IsMidBossWave` のとき起動。
- **後続**: 専用スプライト/SE、ノックバック演出、章/イベント別の出現パターン、難度調整（R3）。

## 影響範囲 / 壊さないこと

- `GameManager`: `ApplyFormationBonuses` / `AllBoardEntities`(public) / `LaunchRollingHazard`(public) / `MidBossHazardRoutine` 追加。`StartFight` に2行のフック追加のみ。
- `BaseEntity`: 既存 public API（`TakeDamage`/`ApplyStun`/`ApplyAttackSpeedBoostFromSynergy`/`ApplyTimedSynergy*`/`ApplyShieldFromSynergy`/`IsDead`/`CurrentNode`/`MaxHealth`）のみ使用。改変なし。
- `GridManager.GetNodeAtBoardCoordinate` / `Node.worldPosition`（public）を使用。

## 受け入れ基準

- [ ] 横3連で開幕に攻撃が速く・痛くなる／縦3連で硬くなる（実機）。
- [ ] 中ボス戦中に巨大物が転がり、当たったユニット（敵味方）がダメージ＋短時間スタン。
- [ ] 通常戦では巨大物が出ない。Compilation OK（CSエラー0）。

## Review (YYYY-MM-DD) — 未実施（実機確認後）
