# DESIGN_R3-hero-formation — 主人公ごとの専用フォーメーション（6マス）

> task-id: R3-hero-formation / 作成: 2026-06-25 / 対象: バトル（配置フォーメーション）
> 要望: 主人公ごとに専用の陣形を作る。6マス以上を使って機能する陣形にする。
> 関連: 既存 ② 配置フォーメーション（突撃/鉄壁/方陣/楔＝`FormationKind`）/ [DESIGN_R3-faction-hero-gate.md](DESIGN_R3-faction-hero-gate.md)

---

## 1. 概要

既存の汎用フォーメーション（横3/縦3/2×2/斜め3）に加え、**現在の主人公専用の6マス陣形**を追加。
自陣（配置エリア＝5列×3行以上）に主人公固有の6マス形が成立すると、戦闘開始時にその6体へ強力な専用バフが入る。
形は**3×3内の相対オフセット**で定義し、並進不変（盤面のどこでも、向きは固定）で検出する。

## 2. 主人公→陣形（6種・各6マス）

基本6ヒーローは固有。ボス将ヒーロー等は陣営でフォールバック。

> 形は**配置を離して使えるスプレッド型**（横方向に1マス空け、最大5列×3行）。密着しないので置きやすい。

| 主人公 | 陣形名 | 形(5×3・スプレッド) | 効果(Effect) |
|---|---|---|---|
| Aldin (Lyonar) | 聖盾の壁 | 双柱（間1） | Bulwark: 被ダメ-18%＋大シールド |
| Ziran (Lyonar) | 日輪の陣 | 横スプレッド(3点×2段) | Sun: 被ダメ-10%＋与ダメ+10%＋回復 |
| Kagachi (Songhai) | 双牙の構え | 遠柱（両端） | Fangs: 攻撃速度+30%＋与ダメ+20% |
| Reva (Songhai) | 疾風の階 | 階段スプレッド | Gale: 攻撃速度+22%＋与ダメ+15% |
| Vesna (Vanar) | 氷の城門 | 格子（上下＋間1） | Gate: 被ダメ-10%＋与ダメ+25% |
| Kara (Vanar) | 凍嶺の鉤 | 鉤スプレッド(L) | Hook: 被ダメ-18%＋与ダメ+12%＋シールド |

フォールバック（ボス将）: Lyonar→Bulwark / Vanar→Gate / Songhai→Fangs / Magmar→Fangs(横陣) / Abyssian→Gale / Vetruvian→Gate(鉤)。

> 数値・形は調整可。`GameManager.GetActiveHeroFormation` / `ApplyHeroFormationEffect` を編集。

## 3. 実装（GameManager）

- `GetActiveHeroFormation()` → `heroUnitId` から `HeroFormDef{ cells(相対), effect, ja/en }`。
- `DetectHeroFormation(out map, out def)` → 自陣配置（`IsDeploymentNode`／`GetBoardColumn/Row`）の占有マスに対し、各占有マスをアンカーに全オフセットが埋まるか検査。成立で6 Nodeを返す。
- `ApplyHeroFormationEffect(e, effect)` → 既存の時限バフAPI（`ApplyTimedSynergyDamageReductionBonus`/`...DamageDealtBonus`/`ApplyAttackSpeedBoostFromSynergy`/`ApplyShieldFromSynergy`/`HealFromSynergy`）で付与（dur=60s＝全戦闘）。
- `ApplyFormationBonuses()` 末尾で検出→6体へ付与＋発動エフェクト。
- `UpdateFormationPreview()` で成立中の6マスを**金色**ライブ表示し、ガイドの主人公行を更新。

## 4. UI（FormationHintUI）

- 既存の4行ガイドに**主人公専用フォーメーションの5行目（動的）**を追加。`SetHeroFormation(cells3x3, name, eff, active)` で形(3×3点灯)・名前・効果・成立ハイライトを更新。ガイドパネルを5行ぶんに拡大。
- 編成中は盤面の成立マスが金色で光る（既存の `formationMarkers` を流用、最優先で上書き）。

## 5. 検証
- realCS=0。6形が各6マス・ユニークなことを確認（5×3 index集合 index=dr*5+dc: A{0,2,5,7,10,12} B{0,2,4,5,7,9} C{0,4,5,9,10,14} E{0,2,7,9,12,14} D{0,2,4,10,12,14} F{0,2,5,10,12,14}）。ガイドは5×3表示。
- 手動: 主人公ごとに該当6マスを組むと戦闘開始で専用バフ＋金マーカー＋ガイド5行目がハイライト。6体未満や形不一致では不発。

## Status
- 2026-06-25 設計＋実装（Cowork+Unity MCP）。形・効果は調整可。
