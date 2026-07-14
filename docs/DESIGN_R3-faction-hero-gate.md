# DESIGN_R3-faction-hero-gate — 陣営シナジーの主人公ゲート＆増幅

> task-id: R3-faction-hero-gate / 作成: 2026-06-25 / 対象: バトル（陣営シナジー）
> 背景: 陣営シナジーを1体から発動にしたが、簡単に起動できてしまう。主人公（その陣営のヒーロー）を軸にした編成を促したい。
> 関連: [DESIGN_R3-boss-factions.md](DESIGN_R3-boss-factions.md) / [COLLAB_PROTOCOL.md](COLLAB_PROTOCOL.md)

---

## 1. 仕様（ユーザー要望）

陣営シナジー（Lyonar/Songhai/Magmar/Vetruvian/Abyssian/Vanar、段階 1/3/5/7/10）について:

1. **1体目（1〜2体）の発動は、主人公がその陣営の時だけ。** 主人公が別陣営なら 1〜2体では発動せず、**3体から**発動する（従来の3段階目から）。
2. **3体以上は主人公がいなくても発動。**（＝3/5/7/10 段階は従来通り。）
3. **主人公がその陣営にいると効果が高い（増幅）。**
4. 効果説明もこの仕様に合わせて更新。

## 2. 実装

### 2.1 主人公の陣営判定（GameManager）
`GameManager.ActiveHeroHasSynergy(SynergyType)`（新規）= 現在の `heroUnitId` の EntityData シナジー（`SynergyManager.GetSynergiesForEntityData`）に指定シナジーが含まれるか。
- 主人公は陣営シナジーを必ず1つ持つ（Aldin/Ziran=Lyonar, Kagachi/Reva=Songhai, Vesna/Kara=Vanar、仲間化ボス将=Magmar/Vetruvian/Abyssian 等）。

### 2.2 ゲート（CountSynergiesForTeam）
プレイヤー（Team1）の陣営シナジーで、`effective`（編成数＋オーグメント補正）が **1〜2** かつ主人公が同陣営でないなら `effective = 0` にしてから `ResolveTier`。
- 結果: 主人公非同陣営は 1〜2体で tier0（無効）、3体で tier3（＝3段階目から発動、累積効果フル）。主人公同陣営は従来通り1体から。
- 敵（Team2）はゲートしない（主人公の概念が無いため従来通り）。

### 2.3 増幅（ApplyBattleStartSynergies の陣営効果ブロック）
`FactionHeroScale(type, team)` = 主人公が同陣営（Team1）なら **`FactionHeroBonusScale`（=1.5）**、それ以外 1.0。各陣営の段階ボーナス（被ダメ減/攻速/与ダメ/スキル威力/シールド/マナ）を `× hs` で増幅。敵側は常に 1.0。
- 10段階の「決定打（全能力激増）」は元々プレイヤー専用＆十分強力なので増幅対象外。

### 2.4 説明（GetTierSummary / GetSynergySummary）
- 各陣営の **1段階目の要約**に「（主人公が同陣営のみ）／(faction hero only)」を付記。
- 陣営シナジーの要約末尾に注記:「1体目は主人公が同陣営の時のみ発動（3体以上は不問）。主人公が同陣営なら効果+50%。」（JA/EN）。

## 3. 検証
- realCS=0。主人公の陣営割当を確認（6ヒーローが各陣営を保持）。
- 手動: 主人公=Lyonar で Lyonar1体→発動／主人公=別陣営で Lyonar1〜2体→不発、3体→発動。主人公同陣営で効果が約1.5倍。説明文が更新表示。

## Status
- 2026-06-25 設計＋実装（Cowork+Unity MCP）。FactionHeroBonusScale=1.5 は調整可。
