# アルカナ 発注ブリーフ / Arcana — Art Commission Brief

> 用途: 最終ボス「アルカナ」のスプライト制作を絵師/アニメーターへ発注するための資料。
> このまま渡せます。技術詳細の元仕様は `docs/DESIGN_boss_arcana.md`。
> （For artists: English version follows the Japanese section below. / 英語版は後半にあります。）

---

# 【日本語版】

## 1. 概要
PC（Steam）向けローグライク・オートチェスの**最終ボス兼プレイアブルキャラ「アルカナ」**のスプライト一式を制作いただきたいです。Unity の2Dゲームに組み込みます。**フレームベースのスプライトシート**（後述の規格）での納品を基本としますが、Spine/Live2D での制作も歓迎です（§6 スコープ参照）。

## 2. キャラクター設定（デザイン規約）
- **名前**: アルカナ / Arcana（魔導の最終ボス、超遠距離の魔法アタッカー）
- **身長/頭身**: 高め。存在感のあるボス体型。
- **接地**: **常時浮遊。歩行モーション無し。足は地面につかない。** 全フレームで下端に透明余白を残し、宙に浮く。
- **髪**: **長い白髪（腰下まで）。すべての動作で必ず描く**（崩壊演出中も最後まで）。揺れ・なびきで生命感を。
- **目**: 赤。表情変化は少なめ（無表情の威圧感）。スキル時のみ発光。
- **衣装**: 黒のドレス。**赤い魔力の裂け目（ひび/亀裂のような発光）**が走る。裾は浮遊で常に揺れる。
- **配色**: 黒（ドレス）／白〜銀（髪）／赤（目・魔力裂け目・魔法陣・粒子）。**差し色は赤のみに統一**。
- **最重要ルール（シルエットテスト）**: **どのフレームでも「白髪＋黒ドレス＋浮遊」だけでアルカナと分かること。** 逆光やシルエット化しても識別できる記号性を最優先。
- スタイル参照: 別添の参照画像2枚（アニメ調の高精細イラスト）。この絵柄・色味・雰囲気に準拠。

## 3. 納品技術規格（フレームベースの場合）
- **1フレーム = 130×130 px 固定**、背景は**透明（PNG, アルファ付き）**。
- **シート = 2048×2048 px** を2枚。
- 配置: **1アニメーション＝新しい行から開始**（左→右、余りセルは透明でOK）。1行15フレーム（130×15=1950px）。
- ピボット/基準: 接地は bottom-center 想定。浮遊感はフレーム下端の透明余白（目安25〜35px）で表現。
- 命名: 連番のスプライトシート＋（可能なら）`boss_arcana_<アニメ名>_000.png` 形式の個別フレーム or レイヤー情報。
- 納品物: **PNGシート2枚＋編集可能なソース（.psd/.clip/Spineプロジェクト等）**。

### シート割り（全118フレーム）
**Sheet01（2048×2048・60フレーム）**

| アニメ | 枚数 | 内容 |
|---|---|---|
| Idle | 12 | 呼吸・髪揺れ・瞬き・魔力粒子。4秒ループ |
| Move | 10 | 浮遊移動（足は動かさない）。体を少し前傾、髪と裾が後ろへ流れる |
| RangeAttack | 12 | ①手を上げる ②魔法陣展開 ③赤黒い魔弾発射 ④残光 |
| MeleeAttack | 10 | ①瞬間移動 ②赤い魔力の爪生成 ③横薙ぎ ④元位置へ瞬間帰還 |
| SkillCharge | 16 | 前半:両腕を広げる／中盤:巨大魔法陣／後半:髪が舞い目が発光 |

**Sheet02（2048×2048・58フレーム）**

| アニメ | 枚数 | 内容 |
|---|---|---|
| Skill「終焉の書」 | 24 | 1-8:空間が歪む／9-16:巨大魔法陣／17-24:赤い瞳が開く。**本体と魔法陣/ビーム等のエフェクトはレイヤー分け推奨** |
| Death | 14 | 前半:崩壊／中盤:赤い粒子化／後半:魔法陣だけ残る／最終フレームは完全透明 |
| SpecialAura | 20 | ボス専用・常時ループ。髪が浮く・足元の魔法陣が回転・赤粒子・黒霧 |

## 4. アニメの優先度（分割発注したい場合）
1. **必須・最優先**: Idle / RangeAttack / Death（最低限ボスとして成立）
2. 次点: Move / SkillCharge / Skill「終焉の書」
3. 余裕があれば: MeleeAttack / SpecialAura

## 5. 用途・権利
- 用途: **商用**（Steam で販売予定のゲームに使用）。
- 希望: **著作権譲渡 or 商用無制限・改変可（スプライト化/エフェクト追加/再配置）のライセンス**。クレジット表記は相談可。
- 二次利用: ストアページ・トレーラー・SNS 宣伝にも使用。

## 6. 発注スコープ（見積りの選択肢）
予算に応じて以下から選べます。お見積りをそれぞれ頂けると助かります。
- **スコープS（最小）**: アルカナの**決定稿イラスト1枚**（正面・浮遊・全身）のみ。→ こちらでツール/リグ化に使用。
- **スコープM（推奨）**: 決定稿1枚＋**Spine/Live2D リグ**で全アニメを変形生成（フレーム崩れが起きにくく一貫性◎）。スプライトシート書き出し可否も教えてください。
- **スコープL（フル）**: 上記**118フレームのスプライトシート2枚**を手描き/作画で納品。

## 7. 見積りにあたり教えてほしいこと
- 上記スコープ別の**料金・納期**。
- 分割（§4の優先度）での進行可否。
- 修正回数・ラフ確認の有無。
- 納品形式（PNGシート／個別フレーム／Spine・Live2Dプロジェクト）。
- 商用ライセンス/著作権譲渡の可否と条件。

---

# 【English version】

## 1. Overview
We are commissioning sprites for **"Arcana," the final boss and a playable character** of a roguelike auto-chess game for PC (Steam), built in Unity (2D). Default delivery is a **frame-based sprite sheet** (spec below), but **Spine/Live2D** production is very welcome (see §6 Scope).

## 2. Character Design Bible
- **Name**: Arcana — an arcane final boss; a super-long-range magic attacker.
- **Proportions**: Tall, with strong boss presence.
- **Grounding**: **Always floating. No walking animation. Feet never touch the ground.** Keep transparent padding at the bottom of every frame so she hovers.
- **Hair**: **Long white hair down past the waist. Must be drawn in every animation** (including through the death/collapse). Use sway/flow for life.
- **Eyes**: Red. Minimal emotion (cold, imposing). Glow only during the Skill.
- **Outfit**: Black dress with **glowing red magical rifts (crack-like fissures)**. The hem sways constantly from floating.
- **Palette**: Black (dress) / white–silver (hair) / red (eyes, rifts, magic circles, particles). **Red is the only accent color.**
- **Most important rule (silhouette test)**: **In every frame, Arcana must be recognizable from "white hair + black dress + floating" alone.** Prioritize this iconic readability even in backlight/silhouette.
- Style reference: see the 2 attached reference images (high-detail anime illustration). Match this style, palette, and mood.

## 3. Technical Delivery Spec (frame-based)
- **Each frame = 130×130 px fixed**, **transparent background (PNG with alpha)**.
- **Sheets = 2048×2048 px**, two of them.
- Layout: **each animation starts on a new row** (left→right; leftover cells stay transparent). 15 frames per row (130×15 = 1950 px).
- Pivot: bottom-center; express floating via transparent padding (~25–35 px) at the bottom of frames.
- Naming: sequential sprite sheet, and if possible per-frame files like `boss_arcana_<anim>_000.png`.
- Deliverables: **two PNG sheets + editable source** (.psd/.clip/Spine project, etc.).

### Sheet breakdown (118 frames total)
**Sheet01 (2048×2048, 60 frames)**

| Animation | Frames | Notes |
|---|---|---|
| Idle | 12 | Breathing, hair sway, blink, magic particles. 4-sec loop |
| Move | 10 | Floating movement (feet still). Slight forward lean; hair/hem trail behind |
| RangeAttack | 12 | 1) raise hand 2) summon magic circle 3) fire red-black bolt 4) afterglow |
| MeleeAttack | 10 | 1) teleport 2) summon red magic claws 3) horizontal slash 4) teleport back |
| SkillCharge | 16 | First: spread both arms / Mid: huge magic circle / Late: hair billows, eyes glow |

**Sheet02 (2048×2048, 58 frames)**

| Animation | Frames | Notes |
|---|---|---|
| Skill "Tome of Finality" | 24 | 1-8 space distorts / 9-16 massive magic circle / 17-24 red eyes open. **Recommend separating body vs. magic-circle/beam effects into layers** |
| Death | 14 | Collapse → red particle dissolve → only the magic circle remains → fully transparent on the last frame |
| SpecialAura | 20 | Boss-only, always looping. Hair floats up, ground magic circle rotates, red particles, black mist |

## 4. Animation Priority (if splitting the order)
1. **Must-have first**: Idle / RangeAttack / Death
2. Next: Move / SkillCharge / Skill "Tome of Finality"
3. If budget allows: MeleeAttack / SpecialAura

## 5. Usage & Rights
- Usage: **Commercial** (a game to be sold on Steam).
- Preferred: **full copyright transfer, or an unlimited commercial license with the right to modify** (turn into sprites, add effects, re-layout). Credit is negotiable.
- Secondary use: store page, trailer, social media promotion.

## 6. Commission Scope (quote options)
Please quote for each that you offer:
- **Scope S (minimal)**: a single **finalized key illustration** of Arcana (front, floating, full body) only. We rig/tool it ourselves.
- **Scope M (recommended)**: key illustration + a **Spine/Live2D rig** producing all animations via deformation (best consistency). Please note whether you can export to sprite sheets.
- **Scope L (full)**: the full **two 118-frame sprite sheets**, hand-drawn/animated.

## 7. Please tell us for the quote
- Price & lead time per scope above.
- Whether phased delivery (per §4 priority) is possible.
- Number of revisions / rough-check steps.
- Delivery format (PNG sheets / individual frames / Spine·Live2D project).
- Commercial license / copyright transfer terms.
