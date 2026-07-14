# 章ウェーブ構成・ダイアログ発火・中ボス編成（ChatGPT向け追補）

`STORY_brief_for_GPT.md` の補足。**各章のラウンド構成／ダイアログがどこで出るか／中ボスに誰が出るか** を実装から抜き出したもの。
（ユニットIDは実装の内部ID。和名は `STORY_brief_for_GPT.md` の表を参照）

---

## 1. 章の骨格（全章共通：33ラウンド・4ステージ）
- 1章 = **4ステージ／全33ラウンド**。
- **Stage1 = 3ラウンド**（やさしい立ち上がり・資源配布）。Stage2/3/4 = 各10ラウンド。
- ラウンド種別: **戦闘 / イベント(非戦闘) / 中ボス / 章ボス**。
- ラウンド表記は「ステージ-ラウンド」（例: 2-5 = ステージ2の5戦目）。

### イベント(非戦闘)ラウンド
- 2-3: オーグメント選択(シルバー)
- 2-8: アイテム入手
- 3-3: オーグメント選択(ゴールド)
- 3-7: アイテム入手
- 4-3: オーグメント選択(プリズム)
- （手書き章 ch1-3 のみ）4-6: ボーナスゴールド
- ※物語上は“小休止/編成の山場”。ここに幕間会話を差し込む余地あり（将来）。

---

## 2. ダイアログ発火タイミング（最重要）
- **ボス戦前ダイアログ（3行VN）は「中ボス」と「章ボス」の各ラウンド開始前に1回ずつ出る。**
- 話者(ボス側)の決定:
  - 章ボス(4-10) → **その章の報酬ボス**（= 物語の主役ボス。例: 章1=キャリバー）。
  - 中ボス → **出現候補の先頭ユニット**が話者。
- つまり1章あたりのダイアログ回数:
  - **手書き章(ch1-3)**: 中ボス7回(2-5,2-10,3-5,3-10,4-7,4-8,4-9) ＋ 章ボス1回(4-10) = **8回**
  - **生成章(ch4-13)**: 中ボス8回(2-5,2-10,3-5,3-10,4-5,4-7,4-8,4-9) ＋ 章ボス1回 = **9回**
- **物語の核は章ボス(4-10)**。中ボスの話者は“傭兵/中立級”ユニットが多く、軽め/状況説明の掛け合い向き（無理に個別化しなくてよい）。

---

## 3. リクルート（仲間化）の仕組み（物語付けに直結）
- **中ボス**: 候補3体と戦い、勝つと**その章の間だけ**1体を解放（一時仲間）。
- **章ボス(4-10)**: 章報酬ユニットを**恒久解放**（= 物語上「打ち破って力を継ぐ」）。
- 生成章(ch4-13)では、**その陣営の3将(Elite)が通常戦闘にも“エリート敵”として頻出**し、うち1人がその章のボス。→「三将が揃って立ちはだかり、毎章1人ずつ倒れて仲間になる」構図。

---

## 4. 中ボス／章ボス編成（章別・実装値）
表記: `ラウンド: 話者(先頭) ＋ 他候補` / 章ボスは `本体 ＋ 護衛`。

### 章1（ボス=キャリバー・O / Lyonar堕）
- Stage1: Zyx（雑魚）×1→2→4
- 2-5: Shadowlord ＋ Malyk, Kane
- 2-10: Decepticleprime ＋ Decepticlechassis, Wolfpunch
- 3-5: Grymbeast ＋ Draugarlord, Kingsguard
- 3-10: Cinderwraith ＋ Dissonance, Grymbeast
- 4-7: Draugarlord ＋ Kingsguard, Cinderwraith
- 4-8: Dissonance ＋ Grymbeast, Draugarlord
- 4-9: Kingsguard ＋ Cinderwraith, Dissonance
- **4-10 章ボス: Caliber ＋ 護衛 Legion, Gol, Wraith, Wujin**

### 章2（ボス=ソルフィスト / Lyonar守）
- 2-5: Malyk ＋ Decepticlechassis, Shadowlord
- 2-10: Wolfpunch ＋ Kane, Decepticleprime
- 3-5: Draugarlord ＋ Cinderwraith, Kingsguard
- 3-10: Grymbeast ＋ Dissonance, Draugarlord
- 4-7: Kingsguard ＋ Grymbeast, Cinderwraith
- 4-8: Dissonance ＋ Draugarlord, Kingsguard
- 4-9: Cinderwraith ＋ Grymbeast, Dissonance
- **4-10 章ボス: Solfist ＋ 護衛 Skyfalltyrant, Invader, Kron, Gol**

### 章3（ボス=ディソナンス / 機巧・終焉の先触れ）※中ボスで「転がる巨大物」ギミック有
- 2-5: Malyk ＋ Decepticlechassis, Paragon
- 2-10: Wolfpunch ＋ Kane, Paragon
- 3-5: Draugarlord ＋ Cinderwraith, Kingsguard
- 3-10: Grymbeast ＋ Dissonance, Draugarlord
- 4-7: Kingsguard ＋ Grymbeast, Cinderwraith
- 4-8: Dissonance ＋ Draugarlord, Kingsguard
- 4-9: Cinderwraith ＋ Grymbeast, Dissonance
- **4-10 章ボス: Dissonance ＋ 護衛 Gol, Invader, Kron, Skyfalltyrant**

### 章4-6（マグマー：ヴァース/スターホーン/ラグノラ）
- 三将(エリート敵・通常戦闘にも頻出): **Magmarvaath, Magmarstarhorn, Magmarragnora**（うち1人が各章ボス）
- 中ボス候補(コスト3級, 2-5/2-10): Malyk, Decepticlechassis, Paragon
- 中ボス候補(コスト4級, 3-5/3-10/4-5/4-7/4-8/4-9): Grymbeast, Cinderwraith, Draugarlord, Kingsguard, Dissonance
- 章ボス護衛(cost5): Gol, Kron, Invader

### 章7-9（アビサル：リリス/キャシヴァ/アビサルメーヴ）※章8でカガチ犬化イベント
- 三将(エリート): **Abyssallilithe, Abyssalcassyva, Abyssalmaehv**
- 中ボス候補(2-5/2-10): Shadowlord, Malyk, Paragon
- 中ボス候補(3-5以降): Wraith, Cinderwraith, Dissonance, Draugarlord, Grymbeast
- 護衛(cost5): Legion, Invader, Gol

### 章10-12（ヴェツルヴィアン：ジリックス/サージ/サイオン）
- 三将(エリート): **Vetruvianzirix, Vetruviansajj, Vetruvianscion**
- 中ボス候補(2-5/2-10): Decepticleprime, Kane, Paragon
- 中ボス候補(3-5以降): Kingsguard, Dissonance, Grymbeast, Draugarlord, Cinderwraith
- 護衛(cost5): Kron, Gol, Legion

### 章13（最終章：アルカナ＝終焉。全陣営総力戦）
- エリート(各陣営の頭目): **Magmarvaath, Abyssallilithe, Vetruvianzirix**
- 章ボス護衛(各陣営の将): **Magmarragnora, Abyssalmaehv, Vetruvianscion**
- 中ボス候補(2-5/2-10): Shadowlord, Kane, Paragon
- 中ボス候補(3-5以降): Wraith, Wujin, Grymbeast, Solfist, Dissonance
- **4-10 章ボス: Arcana**（前章までに倒した各陣営の頭目が再登場して立ちはだかる＝総決算）

---

## 5. シナリオ作成での使いどころ（まとめ）
- **章ボス(4-10)＝物語の山場**。ここの3行は「ボスID × 主人公ID」で個別化する価値が最も高い。
- **中ボスの話者**は中立傭兵級が多い → 軽い挑発/世界観の小出し/コメディ枠に使える（章8のキャシヴァ犬化のような“仕込み”は章ボス側で）。
- **三将の同時登場**（faction章）を活かし、「三将がかわるがわる立ちはだかり、倒すたび仲間になる」連続性をセリフで演出可能。
- **イベントラウンド**（augment/item）は将来の幕間会話の差し込み候補。
- 注意: ラウンド構成・編成は **バランス調整で変わり得る**（特にch1-3は手書き）。物語は「章ボスと三将」を軸に書けば構成変更に強い。

> GPTには本書＋`STORY_brief_for_GPT.md`＋`DESIGN_story.md` の3点を渡せば、ウェーブ/ダイアログ/編成に整合した脚本が書けます。
