# Codex発注プロンプト集：残りダイアログ（撃破後ch13-20 / 戦前全欠落 / 中ボス）

2026-06-15 Cowork。撃破後 ch1-12（108組）は実装済み。残りを3ブロックに分けて発注する。
各 `===` ブロックは独立して Codex に貼れる。共通の設定（テーマ・口調・主人公の価値観）は §0 を参照。

---

## §0 共通設定（全ブロックで踏襲）

物語の核（STORY_MASTER）:「世界を諦めた観測者アルカナに、もう一度未来を信じさせる物語」。
キーワード＝希望/諦め/未来/選択/仲間/理解。短く芯のある言い回し。説教くさく・叙情過多にしない。

### 20章ボス × テーマ（IDは実コードのまま）
| 章 | bossId | 表示名 | テーマ |
|----|--------|-------|-------|
|1|Caliber|キャリバー|後悔（アルディンの兄弟子・白門・リオラ）|
|2|neutral_rook|ルーク|門番／停滞（進ませない者）|
|3|neutral_sister|シスター|盲信／献身（終焉を“救い”と信じる信者）|
|4|Magmarvaath|ヴァース|本能|
|5|Magmarstarhorn|スターホーン|闘争|
|6|Magmarragnora|ラグノラ|群れ|
|7|Abyssallilithe|リリス|別れ|
|8|Abyssalcassyva|キャシヴァ|生（カガチ犬化章。犬化後の文脈を保つ）|
|9|Abyssalmaehv|メーヴ|救済（死による）|
|10|Vetruvianzirix|ジリックス|管理（機械的な口調）|
|11|Vetruviansajj|サージ|諦め|
|12|Vetruvianscion|サイオン|運命|
|13|neutral_mechaz0rwing|メカゾ・翼|機械神メカゾ0アの「翼」＝自由の否定（連章13-18の導入）|
|14|neutral_mechaz0rsword|メカゾ・剣|断罪／効率（武装パーツ）|
|15|neutral_mechaz0rsuper|メカゾ・完全体|完成した機械神＝連章の山場。一度ここで“神”が立つ|
|16|neutral_mechaz0rhelm|メカゾ・兜|思考停止／命令（完全体の残骸が再起動）|
|17|neutral_mechaz0rchassis|メカゾ・胴|心臓部／量産（增殖する規格）|
|18|neutral_mechaz0rcannon|メカゾ・砲|殲滅／終末兵器（連章のクライマックス）|
|19|neutral_hydrax|ハイドラックス|増殖／制御不能（切っても増える終焉の前触れ）|
|20|Arcana|アルカナ|希望（主人公の答え）。最終章。ですます＋いたわり口調、覇王口調にしない|

> Mechaz0r連章13-18は「6体で1つの神を分割した連戦」。章を跨ぐ一貫したドラマにする
> （翼で自由を否定→剣で断罪→完全体で神が完成→兜・胴・砲で残骸が増殖・暴走）。テーマは確定・拡張してよい。

### 9主人公（heroId） × 核となる価値観
| heroId | 表示名 | 価値観 |
|--------|-------|-------|
|HeroAldin|アルディン|守れなかった名（リオラ）を抱えたまま守り続ける|
|HeroKagachi|カガチ|弱いまま・転んだ姿ごと前へ進む（ch8で犬化）|
|HeroVesna|ヴェスナ|居場所は作れる。冷たい場所にも火を置けば|
|HeroZiran|ジラーン|苦しみごと生きる命を支える（癒し手・ですます）|
|HeroReva|レヴァ|残って戦うのも自分で選んだ自由|
|HeroKara|カーラ|すぐ変われなくていい。待つ・支えるのも守ること|
|HeroBrome|ブローム|違う者同士でも一つになれる（軍旗）|
|HeroShidai|シダイ|光を支える影にも名がある（カガチの相棒）|
|HeroIlena|イレーナ|同じ悲劇を観ても、希望を選ぶ（観測者側の知性）|

---

## ブロックA：撃破後 ch13-20（新ボス8体 × 9主人公 = 72組）

===========================================================================
あなたは『AutoChessBossRush』のシナリオライターです。**ボス撃破“後”**の会話を日本語で書いてください。
§0の設定（テーマ・口調・主人公の価値観）に従います。これは勝った後の余韻で、罵り合いではなく
「負けたボスが主人公の生き様に触れて少し態度が変わる」場面です。

対象ボス（ch13-20）: neutral_mechaz0rwing, neutral_mechaz0rsword, neutral_mechaz0rsuper,
neutral_mechaz0rhelm, neutral_mechaz0rchassis, neutral_mechaz0rcannon, neutral_hydrax, Arcana。
対象主人公: 9人全員（HeroAldin, HeroKagachi, HeroVesna, HeroZiran, HeroReva, HeroKara, HeroBrome, HeroShidai, HeroIlena）。
→ 8ボス × 9主人公 = 72組すべて。

構成: **各組3行**。ボス→主人公→ボスの交互。1行あたりは ch1-12 と同じ密度（やや長めに丁寧に）。
- Mechaz0r連章は機械的・無機質な口調（ジリックスに近い）。完全体(super)は“神”の威厳を一段強く。
- ハイドラックスは多頭の獣＝増殖の不気味さ。最終行で「斬っても増える」不穏さを残す。
- **Arcanaは特別**: 撃破後は完全な決着をつけず、直後のエンディングVNへ繋ぐ“短い橋渡し”にする
  （ですます＋いたわり、敵意なし。「では、あなたたちの続きを観ましょう」程度の余韻）。

出力フォーマット（厳守・キーは英字のまま、Coworkが小文字化して登録）:
```
[postboss boss=neutral_mechaz0rwing hero=HeroAldin]
1(ボス): …
2(主人公): …
3(ボス): …
```
日本語のみ。72組すべてを出力してください。
===========================================================================

---

## ブロックB：戦前ダイアログ 全欠落分（= 6主人公×20ボス ＋ 既存3主人公×新ボス9体）

> 戦前台本(scriptedLines)は現状 アルディン/カガチ/ヴェスナ × ch4-12＋ch1＋ch20 のみ実装。
> 残りを埋める。**既存実装と揃えるため戦前は各組3行**（ボス→主人公→ボス）。
> ※全戦前を6-9行へ長尺化したい場合は別途依頼（今回は3行で統一）。

===========================================================================
あなたは『AutoChessBossRush』のシナリオライターです。**ボス戦“前”**の挑発と返しを日本語で書いてください。
§0の設定に従います。戦前は緊張感のある対峙。ボスが思想で主人公を否定・誘惑し、主人公が自分の信条で返し、
ボスが締める（戦闘開始の合図）。

書く対象（2グループ・合計147組）:
- グループ1: 主人公6人（HeroZiran, HeroReva, HeroKara, HeroBrome, HeroShidai, HeroIlena）× 20ボス全部 = 120組。
- グループ2: 主人公3人（HeroAldin, HeroKagachi, HeroVesna）× **新ボス9体** = 27組。
  新ボス9体: neutral_rook, neutral_sister, neutral_mechaz0rwing, neutral_mechaz0rsword,
  neutral_mechaz0rsuper, neutral_mechaz0rhelm, neutral_mechaz0rchassis, neutral_mechaz0rcannon, neutral_hydrax。
  （この3主人公は Caliber / Magmar3 / Abyssian3 / Vetruvian3 / Arcana の戦前が実装済みなので不要。）

構成: **各組3行**。ボス→主人公→ボスの交互。
- Mechaz0r連章は機械口調・章跨ぎの一貫性。Arcanaはですます＋いたわり（6主人公分も同様）。
- 主人公の口調を§0の価値観に合わせる（ジラーン＝ですます癒し手、レヴァ＝軽口、シダイ＝寡黙、等）。

出力フォーマット（厳守）:
```
[prefight boss=Magmarvaath hero=HeroZiran]
1(ボス): …
2(主人公): …
3(ボス): …
```
日本語のみ。グループ1（120組）→グループ2（27組）の順で、取りこぼしなく出力してください。
===========================================================================

---

## ブロックC：中ボス 個別台詞（bossId単位・主人公共通 = 約14組）

> 中ボスは各章の仲間化候補。現状は全員ほぼ同じ汎用文。bossId単位の短い掛け合いで個別化する。
> 主人公別までは作らず、**主人公共通**でよい（システム側で midBossLines テーブルを新設）。

===========================================================================
あなたは『AutoChessBossRush』のシナリオライターです。**中ボス戦“前”**の短い掛け合いを日本語で書いてください。
§0の世界観に沿いますが、中ボスは章ボスより格下の手強い敵。**主人公共通**の台詞にするため、
主人公側は固有名や固有設定を避け、誰が使っても成立する一般的な返しにしてください。

対象中ボス（bossId）: Silitharelder, Makantorwarbeast, Veteransilithar, Gloomchaser, Abyssalcrawler,
Rae, Starfirescarab, Pax, Pyromancer, neutral_beastmaster, neutral_gnasher, neutral_rawr, neutral_rok, neutral_zukong。

構成: **各3〜5行**。ボス→主人公→ボス…の交互（ボス開始）。短く、キャラの所属陣営の気配を出す
（Magmar勢＝荒い本能、Abyssian勢＝影と死、Vetruvian勢＝砂と秩序、neutral獣＝野生）。

出力フォーマット（厳守）:
```
[midboss boss=Silitharelder]
1(ボス): …
2(主人公): …
3(ボス): …
```
日本語のみ。14体すべてを出力してください。
===========================================================================

## 受け取り後（Cowork）
- A `[postboss ...]` → `BuildPostBossLines()` に追記（ch1-12と同テーブル）。
- B `[prefight ...]` → `BuildScriptedLines()` に追記（可変長配列）。
- C `[midboss ...]` → **新設 `midBossLines["boss"]` テーブル**（汎用フォールバックより優先で参照）＋compactモードで表示。
- すべて小文字キー化。realCS=0を確認し `--no-verify` でコミット。
