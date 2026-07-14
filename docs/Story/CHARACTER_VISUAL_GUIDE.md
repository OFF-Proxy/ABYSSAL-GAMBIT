# キャラ見た目ガイド（セリフ発注の必読資料）

> 作成: 2026-06-15 / 目的: Codexはキャラの見た目を知らないため、外見から外れた台詞（「このキャラがこんなこと言う？」）が出る。
> セリフ発注時は **各キャラの立ち絵/アイコン画像をCodexに添付**し、**見た目からペルソナを読み取って**書かせる。
> このガイドは、Coworkが実際の立ち絵を見て書いた外見メモ＋画像パス。画像添付が難しい時の文章フォールバックにもなる。

## 使い方（発注フロー）
1. その章に登場するキャラ（章ボス＋9主人公＋中ボス素体）の**画像をCodexに添付**する（下の「画像パス」を参照）。
2. プロンプトに次を入れる：「**添付した立ち絵から各キャラの外見・雰囲気を把握し、そこからペルソナ（性格・口調）を推定して、それに沿った台詞を書くこと。** 下記の外見メモも参照。」
3. バイブルの価値観＋この外見メモ＋画像、の3点をキャラ理解の土台にさせる。

---

## 主人公9人（毎章登場・最重要）
画像: `Assets/Resources/UI/Dialog/hero_<name>.png`（立ち絵・全身）。

| heroId | 画像 | 外見（立ち絵より） | 見た目から導く口調・空気 |
|--------|------|------------------|------------------------|
| HeroAldin | hero_aldin.png | 金髪短髪の屈強な騎士。金色の獅子鱗の鎧＋青いタバード、巨大な半透明の白い大剣、左拳に白い光。ライオネル。 | 実直で熱い正統派の騎士。重い覚悟を静かに背負う。芝居がからない、芯のある言葉。 |
| HeroKagachi | hero_kagachi.png | 金の逆立つ髪。赤黒い鬼侍の鎧（鬼面モチーフ）、双の反り刃を頭上に構える。ソンガイ。 | 荒く短い口調。粗野だが真っ直ぐ。格好つけない、噛みつくような物言い。 |
| HeroVesna | hero_vesna.png | 赤茶の長髪が羽のよう、赤白の顔料、装飾鎧、長い刃付き銃、毛皮の尾。ヴァナー（鷹のモチーフ）。 | 野性的で凜とした女戦士。冷たさと温度を併せ持つ。短く鋭いが情がにじむ。 |
| HeroZiran | hero_ziran.png | 褐色肌の女性、白金の輝く鎧（太陽紋）、光る黄金の盾＋細剣、ドレッドヘア。ライオネル。 | 穏やかで気高い癒し手。ですます調。静かな祈りのような落ち着き。 |
| HeroReva | hero_reva.png | 黒髪ロング、金の龍角の冠/面、赤金の龍紋の鎧、片腕に龍の刺青、双の反り刃。ソンガイ（炎/龍）。 | 華やかで大胆、芝居気のある自由人。軽口とプライドが同居する。 |
| HeroKara | hero_kara.png | ヴァナーの戦士。濃い毛皮のマント、青く光る氷の角、鎖付きの巨大な青氷の鎚。 | 寡黙で重厚、辛抱強い。急がない、低く静かな言葉。冬の沈着。 |
| HeroBrome | hero_brome.png | 褐色肌の髭の将。金の炎装飾の鎧、金の大盾＋炎の武器、背後に炎、紫のマント。ライオネル。 | 温かく頼れる旗頭。仲間を束ねる包容力。力強く、鼓舞するような語り。 |
| HeroShidai | hero_shidai.png | 白銀の髪、赤金の装飾鎧、青緑のエネルギーを纏う双刃、回転する俊敏な構え。ソンガイ。 | 俊敏で忠実。多くを語らず要点を突く。光を支える影としての静かな誇り。 |
| HeroIlena | hero_ilena.png | 金髪、暗色の鎧、周囲に浮かぶ青い氷晶、槍/杖、冷たく整った佇まい。ヴァナー（氷）。 | 冷静で理知的、観測者的な距離感。落ち着いた分析口調。アルカナと最も近い知性。 |

> 補足: カガチ犬化（Skindogehai）は `hero_kagachi_skindogehai.png`（8章用・犬化イラスト）。

---

## 章ボスの画像パス（外見メモは章執筆時にCoworkが追記）
セリフ発注時は該当ボスの画像を添付し、Codexに外見→ペルソナを読ませる。外見メモはその章を発注する時に
Coworkが実画像を見て本ファイルに追記する（毎回全ボスを見るのは非効率なため、章ごとに該当ボスだけ）。

| 章 | bossId | 画像パス |
|----|--------|---------|
| 1 | Caliber | `UI/Dialog/general_boss_1.png` |
| 2 | neutral_rook | `AddUnit/DialogueIcon/neutral_rook.png` |
| 3 | neutral_sister | `AddUnit/DialogueIcon/neutral_sister.png` |
| 4-6 | Magmarvaath / Magmarstarhorn / Magmarragnora | `UI/Dialog/<UnitID>.png` |
| 7-9 | Abyssallilithe / Abyssalcassyva / Abyssalmaehv | `UI/Dialog/<UnitID>.png` |
| 10-12 | Vetruvianzirix / Vetruviansajj / Vetruvianscion | `UI/Dialog/<UnitID>.png` |
| 13-18 | neutral_mechaz0r 翼/剣/完全体/兜/胴/砲 | `AddUnit/DialogueIcon/neutral_<part>.png`（wing=neutral_wingsofmechaz0r 等） |
| 19 | neutral_hydrax | `AddUnit/DialogueIcon/neutral_hydrax1.png` |
| 20 | Arcana | 専用立ち絵が無ければ `UI/Dialog/general_unknown.png`（暫定） |

中ボス素体（かませ）も同様に、その章の中ボス素体の `AddUnit/DialogueIcon/<icon>.png` を添付して外見を伝える。

---

## 章ボス外見メモ（章ごとに追記）
### 1 Caliber（キャリバー）— `UI/Dialog/general_boss_1.png`
（※ Codexの章1改稿で「黄金の獅子装甲・灰鉄の巨鎧・翠色の光・巨剣」と描写された。次に画像確認時、本メモを実画像準拠で確定する。）
