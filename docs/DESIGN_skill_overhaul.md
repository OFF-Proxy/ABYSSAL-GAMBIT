# DESIGN_skill_overhaul: シンプルスキル17体の固有化 & 遠距離スキル監査
> **状態: ✅ 実装済み（2026-05-30, 17体に固有スキル新設）** — 全45体が固有スキルを保持。汎用文に到達するのは実質 Zyx のみ。

> 設計: Claude / 実装: Codex・Claude Code / 2026-05-30
> 関連: [ROADMAP.md](ROADMAP.md) E5（固有スキル実数値化）の続き / [RELEASE_PLAN.md](RELEASE_PLAN.md) R3-balance
> 方針（確定）: **既存の固有スキル28体並みに凝った設計**（召喚・多段AOE・デバフ・実数値・スター倍率）。**シナジーに紐づける**。遠距離ユニットは射程を活かす対象設計にする。

## ゴール
9種の汎用スキル（PowerStrike/Shield/AllyHeal/AttackSpeedBoost/AreaDamage/SelfHeal/Stun/Slow/DamageBoost）を共有している**17体に、被りのない固有スキルを新設**する。これにより全45体が固有スキルを持ち、ユニット格差・没個性を解消する。

## 現状の構造（把握済み）
- `BaseEntity.ExecuteSkillEffect()` は最初に `TryExecuteDedicatedSkill(target)` を試し、true なら return。false の場合のみ汎用 `switch (skillType)` に落ちる。
- 汎用割り当ては `ConfigureDefaultSkillType(unitId)` → `IsAreaDamageSkillUnit` 等の `IsXxxSkillUnit` ヘルパ群。
- **固有化の手順は3点**:
  1. `TryExecuteDedicatedSkill` の switch に `case "<id>": Execute<Unit><Skill>(target); return true;` を追加。
  2. 対応する `Execute<Unit><Skill>(...)` 本体を新設（既存の dedicated メソッド群を雛形に）。
  3. その unitId を `IsXxxSkillUnit` ヘルパから**削除**（汎用フォールバックから外す）。
  4. `UnitStatusPanelUI` の固有スキル文（`BuildXxxSkillText`）を追加し、`case "<id>":` をディスパッチに登録（E5 と同じ流儀。実数値を明記）。

## 数値の基準（既存ヘルパを流用）
新スキルは既存 dedicated 同様、以下のヘルパで数値を出す（テキストと実値のズレ防止 = E5 の方針）:
- `CalculateAreaSkillDamage()` / `CalculateSingleTargetSkillDamage()` — コスト連動のスキルダメージ。
- `GetSkillShieldAmount()` / `GetSkillAllyHealAmount()` / `GetSkillHealAmount()` — シールド/回復量。
- `GetSkillEffectMultiplier(true)` — アイテム/シナジー倍率込み。
- `GetSkillDuration(base, scalesWithStar)` — 効果時間。
- スター倍率: `StarLevel >= 3 ? A : StarLevel >= 2 ? B : C`（多段/対象数に使う、Kane/Maehv と同様）。
- ベースステータス参考（`ApplyBaseBalance`）: c1 攻撃 ranged125/melee80・c2 180/150・c3 270/220。

---

## 遠距離スキル監査（固有28体 + 汎用）結果

**結論: 固有28体に射程ミスマッチは無い。** 全 dedicated スキルは AOE/効果中心を `GetValidSkillTarget(target)`（＝攻撃対象＝遠方の敵）か `GetLowestHealthAlly()`（全体から選抜）に置いており、キャスター周囲に限定していない。例: Backline/Altgeneral/Malyk/Ilena/Wraith は **target 位置中心の AOE**、Kane/Maehv/Decepticleprime は **複数/連鎖ターゲット**、Archdeacon/Snowchaser は **最も傷ついた味方中心**。遠距離10体（Archdeacon/Backlinearcher/Altgeneraltier2/Decepticleprime/Kane/Malyk/Ilenamk2/Wraith/Snowchasermk/Maehvmk）すべて射程を活用済み。

**唯一の注意点（バグではなくガイドライン）**: 汎用バフオーラ `ApplyAttackSpeedBoostToNearbyAllies` / `ApplyDamageBoostToNearbyAllies` は**キャスター中心（半径~2）**。現状これらは melee 寄りの city/candypanda/snowchasermk のみに適用されるため実害なし。→ **新設計の原則: 遠距離ユニットの効果は「対象（遠方）」または「全体から選抜した味方」に中心化し、キャスター周囲に依存させない。**

本タスクの遠距離6体（Andromeda/Antiswarm/vampire/Crystal/Cindera/Spelleater）は、この原則で対象設計する。

---

## 17体の新固有スキル一覧

| # | ユニット | C | 射 | 旧 | 新スキル名(JA / EN) | 一行 |
|---|---|---|---|---|---|---|
| 1 | Andromeda | 1 | 遠 | 範囲攻撃 | 星屑の斉射 / Stardust Barrage | 敵が最も密集する位置へ長射程AOE＋Stormで短スロウ |
| 2 | Antiswarm | 1 | 遠 | 攻撃加速 | 群れ招来 / Swarm Call | 一時的なBeast小型召喚を呼び前線に投入 |
| 3 | Borealjuggernaut | 1 | 近 | 自己回復 | 凍て付く守勢 / Frostguard Bastion | 自己シールド＋隣接敵を凍結スロウ（前線堅持） |
| 4 | Chaosknight | 1 | 近 | スタン | 混沌の刻印 / Chaos Brand | 対象に大打撃＋Abyss刻印（被ダメ増）を付与 |
| 5 | Christmas | 1 | 近 | 味方回復 | 祝祭の鼓舞 / Festive Rally | 最も傷ついた味方を回復＋全戦士に攻撃バフ |
| 6 | vampire | 1 | 遠 | 自己回復 | 吸魂弾 / Soulpiercer | 遠距離の魂弾、与ダメの一部を自己回復 |
| 7 | valiant | 1 | 近 | スタン | 聖盾強襲 / Aegis Smite | 対象スタン＋自身にDivineシールド |
| 8 | Candypanda | 2 | 近 | 自己回復 | 甘味の供物 / Sugar Feast | 周囲の味方を回復＋小攻撃バフ（近接オーラ） |
| 9 | City | 2 | 近 | 味方回復 | 補給ドローン / Supply Drone | Machine召喚: 継続回復＋シールドを撒くドローン |
| 10 | Crystal | 2 | 遠 | シールド | 氷晶障壁 / Crystal Ward | 対象へ氷晶AOE(スロウ)＋最弱味方にシールド |
| 11 | Cindera | 2 | 遠 | ダメージUP | 業火の照準 / Ember Sight | 対象へ火炎弾＋継続炎上(Inferno DoT) |
| 12 | Decepticle | 2 | 近 | シールド | 変形突撃 / Assault Mode | 攻撃形態へ変形: 攻速UP＋被弾反射(Machine) |
| 13 | Umbra | 2 | 近 | シールド | 影喰らい / Umbral Devour | 隣接敵をHP吸収、吸収量ぶん自己シールド |
| 14 | Spelleater | 2 | 遠 | スロウ | 呪詛吸収 / Spell Eater | 対象を沈黙気味スロウ＋与ダメ減、自身マナ回収 |
| 15 | Serpenti | 2 | 近 | 攻撃加速 | 毒牙連撃 / Venom Frenzy | 自己攻速UP＋通常攻撃に毒スタック(Frenzy) |
| 16 | Decepticlechassis | 3 | 近 | シールド | 装甲再構築 / Armor Reassembly | 大型自己シールド＋周囲味方へ分配(Guardian) |
| 17 | Wolfpunch | 3 | 近 | 強撃 | 獣王の一撃 / Alpha Strike | 単体大打撃＋撃破時に攻撃バフ獲得(Beast) |

> 被りなし。各スキルは2〜3のシナジー（前掲表）に意味的に紐づく。遠距離(遠)4体は効果を遠方の対象に中心化、Antiswarm/vampireも射程を活かす設計。

---

## 各スキル詳細仕様

> 表記の数値は初期案。最終調整は R3-balance（プレイテスト）で行う。`★n` はスターレベル。

### 1. Andromeda — 星屑の斉射 / Stardust Barrage（遠 / Ranger・Storm）
- 対象: **敵が最も多く半径に収まる位置**（クラスタ中心。`GetActiveEnemies` から最大被覆点を選ぶ。簡易には最も近い敵集団の中心 or 現 `targetAtCast`）。
- 効果: 中心へ `CalculateAreaSkillDamage()` のAOE。範囲内全敵に Storm スロウ（攻速 0.8倍 / `GetSkillDuration(1.8f,true)`）。
- ★: 半径 `★1:2.0 / ★2:2.3 / ★3:2.6`。
- 実装ヒント: `ExecuteAltgeneralTwinElement` が target中心AOEの良い雛形。クラスタ中心選定だけ追加。

### 2. Antiswarm — 群れ招来 / Swarm Call（遠 / Beast・Summoner）
- 効果: 戦闘中、**一時的なBeast小型召喚**を `★1:1 / ★2:2 / ★3:2＋強化` 体、前線寄りに召喚（既存の召喚API `SpawnTemporarySummon*` 系を流用）。召喚体HP/攻撃は Antiswarm 攻撃の一定割合。
- Summoner シナジー所持時は召喚数 or 持続を延長（`AugmentSynergy`/`SynergyManager` の既存フックに倣う）。
- 実装ヒント: GameManager 側の `SpawnTemporarySummonByUnitName` / `SpawnTemporarySummonFromSynergy`（オーグメントで実績あり）を再利用。雑魚 `Zyx` か既存Beast小型を召喚体に。

### 3. Borealjuggernaut — 凍て付く守勢 / Frostguard Bastion（近 / Guardian・Frost）
- 効果: 自身に `GetSkillShieldAmount()` のシールド（Guardianらしい大型, `MaxHealth*0.14`目安）＋隣接（半径1.6）の敵を Frost スロウ（0.7倍 / 2.4s）。melee前線なので近接効果が適切。
- ★: シールド量と持続が上昇。
- 実装ヒント: `ExecuteParagonAegis`（自己防御系）＋ Frost スロウの組合せ。

### 4. Chaosknight — 混沌の刻印 / Chaos Brand（近 / Shadow・Abyss）
- 効果: 対象へ `CalculateSingleTargetSkillDamage()*1.4` の打撃＋**Abyss刻印**: 以後 `GetSkillDuration(4f,true)` 間、その敵の被ダメージ +18%（★で+22/+26%）。刻印は既存のデバフ機構（スロウ/スタンと同様の時限デバフ）に「被ダメ増幅」を1種追加 or 既存があれば流用。
- 実装ヒント: 被ダメ増幅デバフが無ければ `BaseEntity` に `ApplyVulnerable(float pct, float dur)` を新設（`TakeDamage` 内で乗算）。Malyk/Wraith の時限効果が参考。

### 5. Christmas — 祝祭の鼓舞 / Festive Rally（近 / Warrior・Divine）
- 効果: 最も傷ついた味方を `GetSkillAllyHealAmount()` 回復（Divine）＋**全戦士(Warrior)味方**に攻撃 +15%（★+18/+22%）/ `GetSkillDuration(4f,true)`。
- 実装ヒント: `ApplyDamageBoostToNearbyAllies` を「Warrior条件で全体」に拡張した版。Archdeacon の全体支援ループが雛形。

### 6. vampire — 吸魂弾 / Soulpiercer（遠 / Arcanist・Wraith・Abyss）
- 効果: 対象へ遠距離の魂弾 `CalculateSingleTargetSkillDamage()*1.5`、**与ダメの 40%(★50/60%) を自己回復**（旧SelfHealの吸血を“射程を活かす攻撃”に転化）。
- 実装ヒント: 単体ダメージ後に `HealSelf(round(dealt*ratio))`。Malyk SoulDrain の吸収を単体遠距離化したもの。

### 7. valiant — 聖盾強襲 / Aegis Smite（近 / Guardian・Divine）
- 効果: 対象スタン `GetSkillDuration(skillStunDuration,false)`＋自身に Divine シールド `MaxHealth*0.10`。melee前線でスタンは適切。
- 実装ヒント: 旧Stunに自己シールドを足すだけ。`ApplyStun` + `ApplyShield`。

### 8. Candypanda — 甘味の供物 / Sugar Feast（近 / Beast・Divine）
- 効果: 周囲(半径2.2)の味方を `GetSkillAllyHealAmount()*0.8` 回復＋攻撃 +12%（Beast）。**近接オーラとして妥当**（既に `ShouldApplySupportAura` 対象）。
- 実装ヒント: `HealNearbyAllies` + `ApplyDamageBoostToNearbyAllies` の合成。

### 9. City — 補給ドローン / Supply Drone（近 / Machine・Alchemy）
- 効果: **Machine召喚ドローン**を1体設置（一定時間）。ドローンは毎秒、最も傷ついた味方を小回復＋低シールド。`★` で持続/回復量UP。
- 実装ヒント: 簡易版は「設置せず、3.5s間 0.7s毎に最弱味方を回復/シールドする時限コルーチン」（Invader/Skyfall のコルーチン雛形）。見た目はドローンVFX。

### 10. Crystal — 氷晶障壁 / Crystal Ward（遠 / Arcanist・Frost）
- 効果: 対象へ氷晶AOE `CalculateAreaSkillDamage()*0.8`＋範囲内に Frost スロウ。同時に**最も傷ついた味方にシールド** `GetSkillShieldAmount()*0.5`。遠距離が射程先で攻防両立。
- 実装ヒント: Ilena CrystalLattice（AOE＋シールド）が近い。シールド先を「最弱味方」に。

### 11. Cindera — 業火の照準 / Ember Sight（遠 / Arcanist・Inferno・Storm）
- 効果: 対象へ火炎弾 `CalculateSingleTargetSkillDamage()*1.3`＋**炎上DoT**: `GetSkillDuration(3f,true)` 間、毎0.5s で attack の 8%(★10/12%) 継続ダメージ（Inferno）。旧DamageBoost（自己バフ）を“射程を活かす火力スキル”へ。
- 実装ヒント: DoT機構が無ければ `ApplyBurn(dmgPerTick, interval, dur)` を新設（コルーチン）。Slow/Stunの時限処理が参考。

### 12. Decepticle — 変形突撃 / Assault Mode（近 / Machine・Alchemy）
- 効果: `GetSkillDuration(4f,true)` 間、攻撃形態へ変形し攻速 +35%(★45/55%)＋被弾時に反射ダメージ（受けたダメの15%を攻撃者へ, Machine）。
- 実装ヒント: 攻速バフ（既存）＋ `TakeDamage` にこのユニット限定の反射フック（時限フラグ）。

### 13. Umbra — 影喰らい / Umbral Devour（近 / Beast・Wraith・Abyss）
- 効果: 隣接の最も近い敵から `CalculateSingleTargetSkillDamage()*1.2` を奪い、**与ダメの60%を自己シールド**化（旧Shieldの“受動シールド”を能動的な吸収に）。
- 実装ヒント: 単体ダメージ→`ApplyShield(round(dealt*0.6), dur)`。Malyk吸収の近接版。

### 14. Spelleater — 呪詛吸収 / Spell Eater（遠 / Arcanist・Abyss）
- 効果: 対象を強スロウ（0.6倍/3s）＋**与ダメ -20%(★-25/-30%)** デバフ（対カウンター/アンチキャリー）。さらに自身マナ +15。旧Slowを“射程を活かすアンチキャリー”へ。
- 実装ヒント: スロウ＋「敵の与ダメ減」デバフ（#4のVulnerableの逆符号 = `ApplyWeaken`）。マナは `GainManaFromSynergy` 流用。

### 15. Serpenti — 毒牙連撃 / Venom Frenzy（近 / Beast・Shadow・Frenzy）
- 効果: 自己攻速 +40%(★50/60%) / `GetSkillDuration(4f,true)`＋効果中の**通常攻撃に毒スタック**（命中ごとに attack の5%を3sのDoTとして付与、最大5スタック, Frenzy）。旧AttackSpeedBoostに毒を足して固有化。
- 実装ヒント: 攻速バフ（既存）＋ on-hit 毒付与フラグ。BaseEntity の on-hit proc 機構（augmentで実績）に倣う。

### 16. Decepticlechassis — 装甲再構築 / Armor Reassembly（近 / Machine・Guardian・Alchemy）
- 効果: 自身に大型シールド `GetSkillShieldAmount()*1.2`＋**周囲(半径2.2)の味方へシールドを分配** `GetSkillShieldAmount()*0.4`（Guardian前線リーダー）。
- 実装ヒント: 自己 `ApplyShield` ＋ 近接味方ループで `ApplyShield`。Paragon/Protector が雛形。

### 17. Wolfpunch — 獣王の一撃 / Alpha Strike（近 / Beast・Guardian）
- 効果: 対象へ `CalculateSingleTargetSkillDamage()*1.8` の重打撃＋短スタン(0.6s)。**この攻撃で敵を倒すと**自身に攻撃 +20%(★25/30%)を `GetSkillDuration(5f,true)` 付与（Beast の捕食）。旧PowerStrikeの正統進化。
- 実装ヒント: 単体大ダメージ＋kill判定（`NotifyEnemyKilledByPlayer` 周辺の既存フック）でバフ。

---

## 受け入れ基準
- [ ] 17体すべてが `TryExecuteDedicatedSkill` で固有挙動を実行し、汎用 `switch (skillType)` に落ちない
- [ ] 17体の unitId が `IsXxxSkillUnit` ヘルパから削除されている（汎用フォールバック解消）
- [ ] `UnitStatusPanelUI` に17体ぶんの固有スキル説明（実数値）が表示され、旧汎用文に落ちない
- [ ] 遠距離6体（Andromeda/Antiswarm/vampire/Crystal/Cindera/Spelleater）の効果が、キャスター周囲ではなく対象/遠方/全体選抜に作用する
- [ ] 既存28体の挙動・テキストにデグレが無い
- [ ] `Compilation completed (Errors: False)`

## 実装の進め方（推奨バッチ）
規模が大きいので3バッチに分割し、各バッチで Compile/動作確認:
1. **既存機構で完結する11体**（4,5,6,7,8,10,11※DoT除く,13,16,17,3）— ダメージ/シールド/回復/スタン/スロウの組合せのみ。
2. **新ヘルパが要る4体** — #4 `ApplyVulnerable`、#11 `ApplyBurn`(DoT)、#14 `ApplyWeaken`、#15 on-hit毒。汎用機構として実装すると後の augment/アイテムにも再利用可。
3. **召喚系2体** — #2 Antiswarm、#9 City（コルーチン/召喚）。GameManager 連携。

## 未決事項（Codex への質問）
- 新デバフ（Vulnerable/Weaken/Burn）は将来のオーグメント/アイテムでも使える**汎用機構**として `BaseEntity` に置く想定でよいか。
- Antiswarm の召喚体に使うユニット（既存Beast小型 or 専用プレハブ新規）。MVPは既存流用を推奨。
- 各スキルの初期数値は仮。R3-balance のプレイテストで詰める前提でよいか。
