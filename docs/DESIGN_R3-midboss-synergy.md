# DESIGN_R3-midboss-synergy — 仲間化できる中ボスのシナジー個性化

> task-id: R3-midboss-synergy / 作成: 2026-06-25 / 対象: 勧誘候補（中ボス）ユニットのシナジー
> 要望: 仲間にする中ボスのシナジーを変えて、それぞれ個性を出す。
> 関連: [DESIGN_R2-recruit.md](DESIGN_R2-recruit.md) / [DESIGN_midboss-nodes.md](DESIGN_midboss-nodes.md)

---

## 1. 背景
勧誘候補（中ボス）19体は全員、汎用デフォルト（近接=Warrior/Guardian、遠隔=Ranger/Arcanist）で個性が無かった。各体にユニークで themed なシナジーを付与する。

## 2. 仕組み
シナジーは `GetSynergiesForEntityData`：DBの `synergy1/2/3` が全て None なら `ResolveDefaultSynergies` の switch を使う。
- **対象19体の switch ケースを追加**（コード上の真実）。
- **DBの当該19体の `synergy1/2/3` を None に戻す**（→ switch が適用される）。Entity Database.asset を更新・保存。

## 3. 割当（各体ユニーク）

中立コア5（cost3）:
- neutral_beastmaster = Beast / Summoner / Guardian
- neutral_gnasher = Beast / Frenzy / Shadow
- neutral_rawr = Beast / Warrior / Royal
- neutral_rok = Guardian / Storm / Machine
- neutral_zukong = Warrior / Storm / Frenzy

中立リカラー変種5（色＝追加テーマ）:
- neutral_beastmaster_crimson = Beast / Inferno / Frenzy
- neutral_gnasher_ice = Beast / Frost / Shadow
- neutral_rok_steelblue = Guardian / Frost / Machine
- neutral_rok_gold = Guardian / Divine / Royal
- neutral_rok_mossgreen = Guardian / Beast / Alchemy

陣営勧誘候補9（cost4・陣営＋テーマ2）:
- Silitharelder = Magmar / Beast / Guardian
- Veteransilithar = Magmar / Beast / Warrior
- Makantorwarbeast = Magmar / Beast / Frenzy
- Gloomchaser = Abyssian / Shadow / Ranger
- Abyssalcrawler = Abyssian / Shadow / Wraith
- Rae = Vetruvian / Arcanist / Ranger
- Starfirescarab = Vetruvian / Machine / Arcanist
- Pax = Vetruvian / Machine / Guardian
- Pyromancer = Inferno / Arcanist / Storm

## 4. 検証
- realCS=0。19体すべて `GetSynergiesForEntityData` が上記の値を返すことを確認。
- 陣営勧誘候補は陣営シナジーを持つため、同陣営を集めると（3体以上で）陣営シナジーに寄与する（[DESIGN_R3-faction-hero-gate.md] のゲート/増幅と連動）。

## Status
- 2026-06-25 実装（Cowork+Unity MCP）。組み合わせ・数値は調整可。
