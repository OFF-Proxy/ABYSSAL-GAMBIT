# ショップアイコンがスプライト表示のままのユニット（Codex作画タスク）

## これは何か

ショップ/カードのアイコンが、**専用イラストではなく「スプライト（戦闘アニメの1フレーム）」がそのまま表示されている**ユニットの一覧。Codex はこれらに専用アイコンを描く。

判定は実データ（`Assets/Resources/Entity Database.asset` の `EntityData.icon`）で行った。`icon` が `<Unit>_Default_000` のようなアニメフレームを指している＝スプライト表示。**該当は57体**（Zyx＝アイコンnullの雑魚を除く）。

## 各ユニットの参照素材

- **スプライト** … 戦闘アニメのアトラス（`*.png` ＋ 同名 `.plist`）。見た目はここで分かる。
- **ダイアログアイコン/立ち絵** … 既にポートレート化された画像。**あればこれを参照に描くのが最適**。
- **ダイアログアイコンが無いユニットはスプライトのみが手がかり**（下表で「（スプライトのみ）」と明記）。

## アイコンの仕組みと反映

- アイコンは `Assets/Images/Units/Icon/<Tier>/<UnitName>.png`（無ければ `Icon/<UnitName>.png`、ヒーローは `Icon/Hero/<Heroを除いた名前>.png`）から読まれる。
- ここに `<UnitName>.png`（**背景あり・290×130px・透過なし**）を置き、Unity で `Tools/AutoChess/Sync Entity Database` を実行すると反映される。

---

## ★最優先：ショップ常設のプレイヤーユニット（Songhai 4体）

**ダイアログアイコンが無いのでスプライトのみ共有。** ショップに必ず並ぶので一番目立つ。

| UnitId | Tier/cost | スプライト（唯一の参照） | 保存先 |
|---|---|---|---|
| Lanternfox | T1/1 | `Assets/Images/Units/Sprite/T1/Lanternfox/f2_lanternfox.png` | `Assets/Images/Units/Icon/T1/Lanternfox.png` |
| Onyxjaguar | T2/2 | `Assets/Images/Units/Sprite/T2/Onyxjaguar/f2_onyxjaguar.png` | `Assets/Images/Units/Icon/T2/Onyxjaguar.png` |
| Keshraifanblade | T3/3 | `Assets/Images/Units/Sprite/T3/Keshraifanblade/f2_keshraifanblade.png` | `Assets/Images/Units/Icon/T3/Keshraifanblade.png` |
| Firewyrm | T3/3 | `Assets/Images/Units/Sprite/T3/Firewyrm/f2_firewyrm.png` | `Assets/Images/Units/Icon/T3/Firewyrm.png` |

---

## ヒーロー（主人公）追加6将＋温存2体

ヒーローは立ち絵が参照になる。Tier2general / Skindogehai は形態変化用の温存ユニット。保存先は `Icon/Hero/<Heroを除いた名前>.png`。

| UnitId | スプライト | 参照（立ち絵） | 保存先 |
|---|---|---|---|
| HeroZiran | `Assets/Images/Units/Sprite/Hero/HeroZiran/f1_altgeneral.png` | `Assets/Resources/UI/Dialog/hero_ziran.png` | `Assets/Images/Units/Icon/Hero/Ziran.png` |
| HeroBrome | `Assets/Images/Units/Sprite/Hero/HeroBrome/f1_3rdgeneral.png` | `Assets/Resources/UI/Dialog/hero_brome.png` | `Assets/Images/Units/Icon/Hero/Brome.png` |
| HeroReva | `Assets/Images/Units/Sprite/Hero/HeroReva/f2_altgeneral.png` | `Assets/Resources/UI/Dialog/hero_reva.png` | `Assets/Images/Units/Icon/Hero/Reva.png` |
| HeroShidai | `Assets/Images/Units/Sprite/Hero/HeroShidai/f2_3rdgeneral.png` | `Assets/Resources/UI/Dialog/hero_shidai.png` | `Assets/Images/Units/Icon/Hero/Shidai.png` |
| HeroKara | `Assets/Images/Units/Sprite/Hero/HeroKara/f6_altgeneral.png` | `Assets/Resources/UI/Dialog/hero_kara.png` | `Assets/Images/Units/Icon/Hero/Kara.png` |
| HeroIlena | `Assets/Images/Units/Sprite/Hero/HeroIlena/f6_3rdgeneral.png` | `Assets/Resources/UI/Dialog/hero_ilena.png` | `Assets/Images/Units/Icon/Hero/Ilena.png` |
| Skindogehai | `Assets/Images/Units/Sprite/Hero/Skindogehai/f2_general_skindogehai.png` | `Assets/Resources/UI/Dialog/hero_kagachi_skindogehai.png` | `Assets/Images/Units/Icon/Hero/Skindogehai.png` |
| Tier2general | `Assets/Images/Units/Sprite/Hero/Tier2general/f6_tier2general.png` | （スプライトのみ） | `Assets/Images/Units/Icon/Hero/Tier2general.png` |

---

## 仲間化で手に入る中ボス（中立・cost3）— ダイアログアイコンあり

| UnitId | スプライト | 参照（ダイアログアイコン） | 保存先 |
|---|---|---|---|
| neutral_beastmaster | `Assets/Images/Units/Neutral/neutral_beastmaster/neutral_beastmaster.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_beastmaster.jpg` | `Assets/Images/Units/Icon/T3/neutral_beastmaster.png` |
| neutral_gnasher | `Assets/Images/Units/Neutral/neutral_gnasher/neutral_gnasher.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_gnasher.jpg` | `Assets/Images/Units/Icon/T3/neutral_gnasher.png` |
| neutral_rawr | `Assets/Images/Units/Neutral/neutral_rawr/neutral_rawr.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_rawr.jpg` | `Assets/Images/Units/Icon/T3/neutral_rawr.png` |
| neutral_rok | `Assets/Images/Units/Neutral/neutral_rok/neutral_rok.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_rok.jpg` | `Assets/Images/Units/Icon/T3/neutral_rok.png` |
| neutral_zukong | `Assets/Images/Units/Neutral/neutral_zukong/neutral_zukong.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_zukong.jpg` | `Assets/Images/Units/Icon/T3/neutral_zukong.png` |
| neutral_silverbeak | `Assets/Images/Units/Neutral/neutral_silverbeak/neutral_silverbeak.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_silverbeak.jpg` | `Assets/Images/Units/Icon/T3/neutral_silverbeak.png` |

### 色違いリカラー（cost3・専用ダイアログアイコンあり）

| UnitId | スプライト | 参照（ダイアログアイコン） | 保存先 |
|---|---|---|---|
| neutral_beastmaster_crimson | `Assets/Images/Units/Neutral/neutral_beastmaster_crimson/neutral_beastmaster_crimson.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_beastmaster_crimson.jpg` | `Assets/Images/Units/Icon/T3/neutral_beastmaster_crimson.png` |
| neutral_gnasher_ice | `Assets/Images/Units/Neutral/neutral_gnasher_ice/neutral_gnasher_ice.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_gnasher_ice.jpg` | `Assets/Images/Units/Icon/T3/neutral_gnasher_ice.png` |
| neutral_rok_steelblue | `Assets/Images/Units/Neutral/neutral_rok_steelblue/neutral_rok_steelblue.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_rok_steelblue.jpg` | `Assets/Images/Units/Icon/T3/neutral_rok_steelblue.png` |
| neutral_rok_gold | `Assets/Images/Units/Neutral/neutral_rok_gold/neutral_rok_gold.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_rok_gold.jpg` | `Assets/Images/Units/Icon/T3/neutral_rok_gold.png` |
| neutral_rok_mossgreen | `Assets/Images/Units/Neutral/neutral_rok_mossgreen/neutral_rok_mossgreen.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_rok_mossgreen.jpg` | `Assets/Images/Units/Icon/T3/neutral_rok_mossgreen.png` |

---

## 仲間化で手に入る各陣営の非将中ボス（cost4）— ダイアログアイコンあり

| UnitId | スプライト | 参照（ダイアログアイコン） | 保存先 |
|---|---|---|---|
| Silitharelder | `Assets/Images/Units/Sprite/T4/Silitharelder/f5_silitharelder.png` | `Assets/Resources/AddUnit/DialogueIcon/magmar_silitharelder.jpg` | `Assets/Images/Units/Icon/T4/Silitharelder.png` |
| Makantorwarbeast | `Assets/Images/Units/Sprite/T4/Makantorwarbeast/f5_mankatorwarbeast.png` | `Assets/Resources/AddUnit/DialogueIcon/magmar_makantorwarbeast.jpg` | `Assets/Images/Units/Icon/T4/Makantorwarbeast.png` |
| Veteransilithar | `Assets/Images/Units/Sprite/T4/Veteransilithar/f5_silitharveteran.png` | `Assets/Resources/AddUnit/DialogueIcon/magmar_veteransilithar.jpg` | `Assets/Images/Units/Icon/T4/Veteransilithar.png` |
| Gloomchaser | `Assets/Images/Units/Sprite/T4/Gloomchaser/f4_gloomchaser.png` | `Assets/Resources/AddUnit/DialogueIcon/abyssian_gloomchaser.jpg` | `Assets/Images/Units/Icon/T4/Gloomchaser.png` |
| Abyssalcrawler | `Assets/Images/Units/Sprite/T4/Abyssalcrawler/f4_crawler.png` | `Assets/Resources/AddUnit/DialogueIcon/abyssian_abyssalcrawler.jpg` | `Assets/Images/Units/Icon/T4/Abyssalcrawler.png` |
| Pax | `Assets/Images/Units/Sprite/T4/Pax/f3_pax.png` | `Assets/Resources/AddUnit/DialogueIcon/vetruvian_pax.jpg` | `Assets/Images/Units/Icon/T4/Pax.png` |
| Rae | `Assets/Images/Units/Sprite/T4/Rae/f3_rae.png` | `Assets/Resources/AddUnit/DialogueIcon/vetruvian_rae.jpg` | `Assets/Images/Units/Icon/T4/Rae.png` |
| Starfirescarab | `Assets/Images/Units/Sprite/T4/Starfirescarab/f3_starfirescarab.png` | `Assets/Resources/AddUnit/DialogueIcon/vetruvian_starfirescarab.jpg` | `Assets/Images/Units/Icon/T4/Starfirescarab.png` |
| Pyromancer | `Assets/Images/Units/Sprite/T4/Pyromancer/f3_pyromancer.png` | `Assets/Resources/AddUnit/DialogueIcon/vetruvian_pyromancer.jpg` | `Assets/Images/Units/Icon/T4/Pyromancer.png` |

---

## 章ボス（撃破で仲間化／図鑑に出る）— 立ち絵あり

| UnitId | Tier | スプライト | 参照（立ち絵/ダイアログ） | 保存先 |
|---|---|---|---|---|
| Magmarvaath | T5 | `Assets/Images/Units/Sprite/T5/Magmarvaath/f5_general.png` | `Assets/Resources/UI/Dialog/Magmarvaath.png` | `Assets/Images/Units/Icon/T5/Magmarvaath.png` |
| Magmarstarhorn | T5 | `Assets/Images/Units/Sprite/T5/Magmarstarhorn/f5_altgeneral.png` | `Assets/Resources/UI/Dialog/Magmarstarhorn.png` | `Assets/Images/Units/Icon/T5/Magmarstarhorn.png` |
| Magmarragnora | T5 | `Assets/Images/Units/Sprite/T5/Magmarragnora/f5_3rdgeneral.png` | `Assets/Resources/UI/Dialog/Magmarragnora.png` | `Assets/Images/Units/Icon/T5/Magmarragnora.png` |
| Abyssallilithe | T5 | `Assets/Images/Units/Sprite/T5/Abyssallilithe/f4_general.png` | `Assets/Resources/UI/Dialog/Abyssallilithe.png` | `Assets/Images/Units/Icon/T5/Abyssallilithe.png` |
| Abyssalcassyva | T5 | `Assets/Images/Units/Sprite/T5/Abyssalcassyva/f4_altgeneral.png` | `Assets/Resources/UI/Dialog/Abyssalcassyva.png` | `Assets/Images/Units/Icon/T5/Abyssalcassyva.png` |
| Abyssalmaehv | T5 | `Assets/Images/Units/Sprite/T5/Abyssalmaehv/f4_3rdgeneral.png` | `Assets/Resources/UI/Dialog/Abyssalmaehv.png` | `Assets/Images/Units/Icon/T5/Abyssalmaehv.png` |
| Vetruvianzirix | T5 | `Assets/Images/Units/Sprite/T5/Vetruvianzirix/f3_general.png` | `Assets/Resources/UI/Dialog/Vetruvianzirix.png` | `Assets/Images/Units/Icon/T5/Vetruvianzirix.png` |
| Vetruviansajj | T5 | `Assets/Images/Units/Sprite/T5/Vetruviansajj/f3_altgeneral.png` | `Assets/Resources/UI/Dialog/Vetruviansajj.png` | `Assets/Images/Units/Icon/T5/Vetruviansajj.png` |
| Vetruvianscion | T5 | `Assets/Images/Units/Sprite/T5/Vetruvianscion/f3_3rdgeneral.png` | `Assets/Resources/UI/Dialog/Vetruvianscion.png` | `Assets/Images/Units/Icon/T5/Vetruvianscion.png` |
| neutral_rook | T4 | `Assets/Images/Units/Neutral/neutral_rook/neutral_rook.png` | `Assets/Resources/UI/Dialog/neutral_rook.png` | `Assets/Images/Units/Icon/T4/neutral_rook.png` |
| neutral_sister | T4 | `Assets/Images/Units/Neutral/neutral_sister/neutral_sister.png` | `Assets/Resources/UI/Dialog/neutral_lkiansister.png` | `Assets/Images/Units/Icon/T4/neutral_sister.png` |
| neutral_hydrax | T5 | `Assets/Images/Units/Neutral/neutral_hydrax/neutral_hydrax.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_hydrax1.jpg` | `Assets/Images/Units/Icon/T5/neutral_hydrax.png` |
| neutral_mechaz0rwing | T5 | `Assets/Images/Units/Neutral/neutral_mechaz0rwing/neutral_mechaz0rwing.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_wingsofmechaz0r.jpg` | `Assets/Images/Units/Icon/T5/neutral_mechaz0rwing.png` |
| neutral_mechaz0rsword | T5 | `Assets/Images/Units/Neutral/neutral_mechaz0rsword/neutral_mechaz0rsword.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_swordofmechaz0r.jpg` | `Assets/Images/Units/Icon/T5/neutral_mechaz0rsword.png` |
| neutral_mechaz0rsuper | T5 | `Assets/Images/Units/Neutral/neutral_mechaz0rsuper/neutral_mechaz0rsuper.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_mechaz0r.jpg` | `Assets/Images/Units/Icon/T5/neutral_mechaz0rsuper.png` |
| neutral_mechaz0rhelm | T5 | `Assets/Images/Units/Neutral/neutral_mechaz0rhelm/neutral_mechaz0rhelm.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_mechaz0r.jpg` | `Assets/Images/Units/Icon/T5/neutral_mechaz0rhelm.png` |
| neutral_mechaz0rchassis | T5 | `Assets/Images/Units/Neutral/neutral_mechaz0rchassis/neutral_mechaz0rchassis.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_mechaz0r.jpg` | `Assets/Images/Units/Icon/T5/neutral_mechaz0rchassis.png` |
| neutral_mechaz0rcannon | T5 | `Assets/Images/Units/Neutral/neutral_mechaz0rcannon/neutral_mechaz0rcannon.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_mechaz0r.jpg` | `Assets/Images/Units/Icon/T5/neutral_mechaz0rcannon.png` |

> mechaz0r の super/helm/chassis/cannon は共通アイコン `neutral_mechaz0r.jpg` を参照に、パーツ違いとして描き分け推奨。

---

## 雑魚敵（cost1）— ショップ非対象・優先度低（後回し可）

ダイアログアイコンが揃っているのでポートレート化は容易だが、プレイヤーはほぼ入手しない。

| UnitId | スプライト | 参照（ダイアログアイコン） | 保存先 |
|---|---|---|---|
| neutral_z0r | `Assets/Images/Units/Neutral/neutral_z0r/neutral_z0r.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_z0r.jpg` | `Assets/Images/Units/Icon/T1/neutral_z0r.png` |
| neutral_nip | `Assets/Images/Units/Neutral/neutral_nip/neutral_nip.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_nip.jpg` | `Assets/Images/Units/Icon/T1/neutral_nip.png` |
| neutral_goldenmantella | `Assets/Images/Units/Neutral/neutral_goldenmantella/neutral_goldenmantella.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_goldenmantella.jpg` | `Assets/Images/Units/Icon/T1/neutral_goldenmantella.png` |
| neutral_ion | `Assets/Images/Units/Neutral/neutral_ion/neutral_ion.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_ion.jpg` | `Assets/Images/Units/Icon/T1/neutral_ion.png` |
| neutral_grincher | `Assets/Images/Units/Neutral/neutral_grincher/neutral_grincher.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_grincher.jpg` | `Assets/Images/Units/Icon/T1/neutral_grincher.png` |
| neutral_aer | `Assets/Images/Units/Neutral/neutral_aer/neutral_aer.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_aer.jpg` | `Assets/Images/Units/Icon/T1/neutral_aer.png` |
| neutral_soboro | `Assets/Images/Units/Neutral/neutral_soboro/neutral_soboro.png` | `Assets/Resources/AddUnit/DialogueIcon/neutral_soboro.jpg` | `Assets/Images/Units/Icon/T1/neutral_soboro.png` |

---

## 反映手順

1. 各「保存先」に `<UnitName>.png`（背景あり・290×130px・透過なし）を配置。
2. Unity メニュー `Tools/AutoChess/Sync Entity Database` を実行（該当ユニット再ビルドでも可）。
3. `Entity Database.asset` の `EntityData.icon` に反映され、ショップ/図鑑/カードに表示。

## 優先度

1. **Songhai 4体**（ショップ常設・ダイアログアイコン無し＝完全新規作画）
2. ヒーロー8体、仲間化中ボス（中立＋リカラー＋陣営非将）
3. 章ボス（図鑑映え）
4. 雑魚敵（後回し可）
