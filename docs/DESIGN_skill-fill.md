# skill-fill: 強撃プレースホルダ→固有スキル付与（ボス＋勧誘可能ユニット）

> 設計: Claude / 実装: Cowork(Unity MCP) / 2026-06-17
> 関連: docs/DESIGN_skill_overhaul.md, references/add-unit-skill.md（4ステップ手順）

## ゴール
既定の「強撃(PowerStrike)」に落ちている **ボス＋勧誘可能ユニット 約28体** に、
**個性に合った・既存約50種と被らない固有スキル**を付与する。雑魚(neutral_z0r等)は強撃のまま。

## 対象（強撃のまま＝BaseEntityにcase無し）
- 章ボス: caliber / neutral_rook / neutral_sister / neutral_hydrax / mechaz0r×6 / Magmar×3 / Abyssian×3 / Vetruvian×3（Arcanaは実装済）
- 勧誘可能: 中立recruit neutral_beastmaster/gnasher/rawr/rok/zukong / Songhai4 lanternfox/onyxjaguar/keshraifanblade/firewyrm

## 実装方針（add-unit-skill.md 準拠・4点セット/体）
1. `BaseEntity.TryExecuteDedicatedSkill` に case 追加 → `Execute<Unit><Skill>()` を呼ぶ。
2. `Execute<Unit><Skill>()` 本体を新設。数値は既存ヘルパから：`CalculateAreaSkillDamage()`/`CalculateSingleTargetSkillDamage()`/
   `GetSkillShieldAmount()`/`GetSkillAllyHealAmount()`/`GetSkillDuration(b,scale)`、星別ティア。
   既存プリミティブのみ使用：`DealLegendarySkillDamage` / `ApplyShield` / `HealSelf` / `ApplyAttackSpeedSlow` /
   `ApplyVulnerable` / `ApplyStun` / `ApplyTimedSynergyDamageDealtBonus` / `ApplyTimedSynergyDamageReductionBonus`。
   演出：`AttackEffectPlayer.PlaySynergyEffect / PlayAreaIndicator / PlaySkill`。
   遠距離(range>=4)は効果をキャスター周囲に限定しない。近接は自分周囲AoEが妥当。
3. 該当すれば汎用フォールバック(IsXxxSkillUnit)から除外。
4. `UnitStatusPanelUI` の skill-text ディスパッチに登録＋`Build<Unit>SkillText`（JA/EN・実数値）。

> 「被らない」は78体規模ではプリミティブ完全唯一は不可能。**名称＋効果の組合せで個性**を出す方針。数値は仮、balanceはR3へ。

## ユニット別 スキル概念表

### 章ボス
| id | 異名/キャラ | スキル名(JA / EN) | 機構 |
|---|---|---|---|
| caliber | 暴走守護・黄金獅子・巨剣・「眠らせる」 | 終幕の薙ぎ / Endfall Sweep | 自分周囲AoEダメージ＋命中敵を鈍化(Slow)＋自己シールド(守護崩れ) |
| neutral_rook | 石門ゴーレム・停滞・動かぬ壁 | 停滞の城門 / Stasis Gate | 自己に特大シールド＋周囲の敵を強スロウ |
| neutral_sister | 鎮魂の修女・嘆き(ch3) | 鎮魂の哀歌 / Requiem Dirge | 敵全体AoEダメージ＋命中敵に防御減(Vulnerable) |
| neutral_hydrax | 多頭水竜(ch19) | 奔流の顎 / Torrent Maw | 最遠の敵へ単体特大ダメージ＋スロウ(遠距離想定) |
| neutral_mechaz0rwing | 翼・高速 | 滑空斉射 / Strike Glide | 敵全体に小ダメージ＋自分攻速大バフ |
| neutral_mechaz0rsword | 剣・連撃 | 斬鉄連撃 / Steel Flurry | 対象に多段ダメージ＋防御減(Vulnerable) |
| neutral_mechaz0rsuper | 超合体 | オーバードライブ / Overdrive | 自分に与ダメ＆攻速大バフ＋周囲AoE |
| neutral_mechaz0rhelm | 兜・守 | 守護フィールド / Aegis Field | 味方全体に小シールド＋自分被ダメ減 |
| neutral_mechaz0rchassis | 胴・重装 | 重装砲列 / Bulwark Cannons | 自己シールド＋対象AoEダメージ |
| neutral_mechaz0rcannon | 砲・遠距離 | 全弾斉射 / Full Salvo | 最遠基点に広範囲AoE特大ダメージ |
| Magmarvaath | 溶岩巨竜(ch4) | 溶岩噴出 / Magma Eruption | 対象地点に予兆→遅延AoE特大ダメージ |
| Magmarstarhorn | 星角・突進(ch5) | 星角突撃 / Starhorn Charge | 直線/対象へ突進ダメージ＋短スタン |
| Magmarragnora | 再生巨体(ch6) | 灼熱再生 / Searing Regen | 自己大回復＋与ダメバフ |
| Abyssallilithe | 吸命の女王(ch7) | 吸命の抱擁 / Lifebloom Drain | 対象単体大ダメージ＋自分回復(吸命) |
| Abyssalcassyva | 影群れ(ch8) | 影の群葬 / Shadow Swarm | 敵全体AoEダメージ＋与ダメ減(Weaken相当=DamageDealt-) |
| Abyssalmaehv | 深淵(ch9) | 深淵の連弧 / Abyss Arc | 敵全体スロウ＋自己シールド |
| Vetruvianzirix | 砂機械(ch10) | 砂塵の刃嵐 / Sandblade Storm | 自分周囲回転AoE多段＋スロウ |
| Vetruviansajj | 太陽女王(ch11) | 太陽の聖印 / Solar Sigil | 味方全体に与ダメバフ＋自分単体大ダメージ |
| Vetruvianscion | 継承者(ch12) | 風蝕の貫き / Erosion Pierce | 最遠へ貫通ダメージ＋防御減(Vulnerable) |

### 勧誘可能（中立recruit＋Songhai）
| id | キャラ | スキル名(JA / EN) | 機構 |
|---|---|---|---|
| neutral_beastmaster | 獣使い | 群獣の咆哮 / Beast Roar | 味方全体に攻速バフ＋自分単体ダメージ |
| neutral_gnasher | 噛み砕き | 顎砕き / Gnash | 対象単体大ダメージ＋防御減 |
| neutral_rawr | 咆哮獣 | 威圧の咆哮 / Intimidate | 敵全体を短スロウ＋与ダメ減 |
| neutral_rok | 岩塊 | 岩石投擲 / Boulder Toss | 対象地点AoEダメージ＋短スタン |
| neutral_zukong | 武僧猿 | 如意千変 / Staff Flurry | 自分周囲AoE多段＋自己小シールド |
| lanternfox | 灯狐(Songhai) | 灯火の導 / Lantern Guide | 味方最低HPへ回復＋小シールド |
| onyxjaguar | 黒豹(Songhai) | 影駆け / Onyx Pounce | 最遠へ瞬間移動斬り＋次撃強化(DamageDealt+ self) |
| keshraifanblade | 扇刃(Songhai) | 扇刃乱舞 / Fan Blade Dance | 対象前方AoE多段ダメージ |
| firewyrm | 火竜(Songhai) | 業火のブレス / Fire Breath | 直線/対象周囲AoEダメージ＋継続(再ヒット) |

## 受け入れ基準
- [ ] 各対象が戦闘で**固有挙動**のスキルを撃つ（強撃に落ちない）。
- [ ] `UnitStatusPanelUI` に実数値スキル説明（JA/EN）。
- [ ] 既存ユニットの挙動デグレ無し（共有ヘルパ/switch）。
- [ ] Compilation completed (Errors: False) / realCS=0。

## バッチ計画（数体ごとにコンパイル＆コミット）
1. 早期章ボス: caliber / neutral_rook / neutral_sister
2. 中立recruit5
3. Songhai4
4. Magmar3 / Abyssian3
5. Vetruvian3 / mechaz0r6 / hydrax

## 未決
- 数値は仮置き。最終balanceは R3-balance。新デバフ(Weaken/Burn)が要る場合は BaseEntity 汎用機構として追加（1体専用にしない）。
