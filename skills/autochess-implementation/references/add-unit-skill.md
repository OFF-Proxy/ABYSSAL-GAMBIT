# レシピ: ユニットのスキルを追加 / 変更する

ユニットに**被りのない固有スキル**を持たせる手順。これに従えば、誰がやっても同じ場所・同じ形になる。
背景と全体方針は `docs/DESIGN_skill_overhaul.md` を参照。

## スキルの2層構造（前提）

`BaseEntity.ExecuteSkillEffect()` は次の順で処理する:

1. まず `TryExecuteDedicatedSkill(target)` を試す → **固有スキルを持つユニットはここで実行して return**。
2. false の場合のみ、汎用 `switch (skillType)` に落ちる（`UnitSkillType`: PowerStrike/SelfHeal/AllyHeal/
   Shield/AttackSpeedBoost/Stun/Slow/DamageBoost/AreaDamage）。汎用割り当ては `ConfigureDefaultSkillType(unitId)`
   → `IsXxxSkillUnit(unitId)` ヘルパ群。

つまり「固有スキルを持つ＝`TryExecuteDedicatedSkill` の switch に入っていて、汎用ヘルパからは外れている」状態。

## 手順（3〜4点セット。すべて `Assets/Scripts/`）

### Step 1. `BaseEntity.TryExecuteDedicatedSkill` に分岐を追加
`NormalizeUnitId(UnitId)` の switch に、対象ユニットの case を足す:
```csharp
case "wolfpunch":
    ExecuteWolfpunchAlphaStrike(targetAtCast);
    return true;
```
コルーチンが必要な派手スキルは既存の `invader`/`gol`/`skyfalltyrant` のように
`StartCoroutine(...)` パターンで（多段・時間差はコルーチンが扱いやすい）。

### Step 2. `Execute<Unit><Skill>(...)` 本体を新設
既存の dedicated メソッドを雛形にする（用途別の良い手本）:
- 対象中心の範囲ダメージ: `ExecuteAltgeneralTwinElement` / `ExecuteMalykSoulDrain`
- 連鎖/複数ターゲット: `ExecuteKaneStormTurret` / `ExecuteMaehvRailArc`
- 味方支援（回復＋シールド）: `ExecuteArchdeaconHolyEdict` / `ExecuteSnowchaserRelay`
- 自己防御: `ExecuteParagonAegis`
- 瞬間移動斬り: `ExecuteAssassinLeapStrike`

**数値は必ず既存ヘルパから出す**（表示テキストと実値をズラさないため）:
- `CalculateAreaSkillDamage()` / `CalculateSingleTargetSkillDamage()` — コスト連動ダメージ
- `GetSkillShieldAmount()` / `GetSkillAllyHealAmount()` / `GetSkillHealAmount()`
- `GetSkillEffectMultiplier(true)` — アイテム/シナジー倍率込み
- `GetSkillDuration(baseSeconds, scalesWithStar)`
- スター倍率: `StarLevel >= 3 ? A : StarLevel >= 2 ? B : C`（多段数・対象数に使う）
- 演出: `AttackEffectPlayer.PlaySkill(...)` / `PlaySynergyEffect(SynergyType.X, pos, scale)` / `PlayAreaIndicator(...)`

**対象選択の原則**: `GetValidSkillTarget(target)`（攻撃対象＝遠方の敵）か `GetLowestHealthAlly()`（全体から選抜）を使う。
**遠距離ユニット（`GetConfiguredBaseRange` が 4 以上）の効果をキャスター周囲に限定しない**（射程が死ぬ）。
近接ユニットのみ、自分周囲の効果（隣接スロウ等）が妥当。

### Step 3. 汎用フォールバックから除外する
そのユニットが `IsAreaDamageSkillUnit` / `IsAllyHealSkillUnit` / `IsShieldSkillUnit` /
`IsSelfHealSkillUnit` / `IsAttackSpeedBoostSkillUnit` / `IsStunSkillUnit` / `IsSlowSkillUnit` /
`IsDamageBoostSkillUnit` / `IsAreaDamageSkillUnit` のいずれかに名前があれば**削除**する。
（消さないと `ConfigureDefaultSkillType` で skillType がセットされるが、Step1 が先に return するので
挙動は固有が勝つ。ただし「フォールバック残置＝負債」なので必ず外して意図を明確にする。）

### Step 4. `UnitStatusPanelUI` に実数値スキル説明を追加
ディスパッチ（`case "<id>": return BuildXxxSkillText(entity, japanese);`）に登録し、
`BuildXxxSkillText` を新設。`BuildLegionSkillText` 等が雛形。**JA/EN 両方**を書き、Step2 と同じ数値ヘルパを
使って文面の数字を出す（テキストと実値のズレ防止）。旧定性文フォールバックに落ちないようにする。

## 新しい時限デバフ等が必要な場合
被ダメ増（Vulnerable）/与ダメ減（Weaken）/継続ダメージ（Burn）/on-hit 付与 などが要るときは、
**1ユニット専用にせず `BaseEntity` の汎用機構**として実装する（後のオーグメント/アイテムでも再利用できる）。
既存の `ApplyStun` / `ApplyAttackSpeedSlow` / `ApplyShieldFromSynergy` の時限処理が実装パターンの手本。

## 検証
- [ ] 戦闘でそのユニットのスキルが**固有挙動**で発動する（汎用に落ちない）
- [ ] `UnitStatusPanelUI` の説明が新スキルの**実数値**で表示される（JA/EN）
- [ ] 共有 switch / ヘルパを触ったことで**他ユニットがデグレしていない**
- [ ] `Compilation completed (Errors: False)`

## やりがちなミス
- Step3 を忘れる（フォールバック残置）。挙動は出るが負債になる。
- 数値をベタ書きして説明文と実値がズレる（必ずヘルパ経由）。
- 遠距離ユニットなのに効果をキャスター周囲に限定してしまう（射程が死ぬ）。
- EN を書き忘れる / `ApplyFont` を呼び忘れてフォントが崩れる。
