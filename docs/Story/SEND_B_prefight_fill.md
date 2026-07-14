あなたは『AutoChessBossRush』（Unity 2Dローグライク・オートチェス）のシナリオライターです。
ゲーム内の**ボス戦“前”**の挑発と返しを日本語で書いてください。本文だけを指定フォーマットで出力してください。

## 物語の核
「世界を諦めた観測者アルカナに、もう一度未来を信じさせる物語」。
キーワード＝希望/諦め/未来/選択/仲間/理解。ラスボスはアルカナ。主人公はプレイヤーが選んだヒーロー1人。

## このシーンの性質
戦闘開始直前の対峙。ボスが自分の思想で主人公を否定・誘惑し、主人公が自分の信条で返し、ボスが締める
（締めの行が戦闘開始の合図になる）。短く芯のある言い回し。説教くさく・叙情過多にしない。

## 20章ボス × テーマ（IDは英字のまま使用）
1 Caliber（キャリバー／後悔。主人公アルディンの兄弟子・白門の陥落・守れなかった少女リオラが因縁）
2 neutral_rook（ルーク／門番・停滞）
3 neutral_sister（シスター／盲信・献身。終焉を“救い”と信じる信者）
4 Magmarvaath（ヴァース／本能）
5 Magmarstarhorn（スターホーン／闘争）
6 Magmarragnora（ラグノラ／群れ）
7 Abyssallilithe（リリス／別れ）
8 Abyssalcassyva（キャシヴァ／生。カガチ犬化章）
9 Abyssalmaehv（メーヴ／救済・死による）
10 Vetruvianzirix（ジリックス／管理。機械的な口調）
11 Vetruviansajj（サージ／諦め）
12 Vetruvianscion（サイオン／運命）
13 neutral_mechaz0rwing（メカゾ・翼／自由の否定。機械口調）
14 neutral_mechaz0rsword（メカゾ・剣／断罪・効率）
15 neutral_mechaz0rsuper（メカゾ・完全体／機械神の山場。威厳を強く）
16 neutral_mechaz0rhelm（メカゾ・兜／思考停止・命令）
17 neutral_mechaz0rchassis（メカゾ・胴／心臓部・量産）
18 neutral_mechaz0rcannon（メカゾ・砲／殲滅・終末兵器）
19 neutral_hydrax（ハイドラックス／増殖・制御不能。多頭の獣）
20 Arcana（アルカナ／希望。覇王口調にせず、ですます＋いたわり）
（Mechaz0r連章13-18は6体で1つの機械神を分割した連戦。章を跨ぐ一貫したドラマに。テーマは確定・拡張してよい）

## 9主人公（heroId）と核となる価値観・口調
- HeroAldin（アルディン／守れなかった名・リオラを抱えたまま守り続ける。実直）
- HeroKagachi（カガチ／弱いまま・転んだ姿ごと前へ進む。荒い口調）
- HeroVesna（ヴェスナ／居場所は作れる、冷たい場所にも火を置けば）
- HeroZiran（ジラーン／苦しみごと生きる命を支える癒し手。ですます）
- HeroReva（レヴァ／残って戦うのも自分で選んだ自由。軽口）
- HeroKara（カーラ／すぐ変われなくていい、待つ・支えるのも守ること）
- HeroBrome（ブローム／違う者同士でも一つになれる。軍人口調）
- HeroShidai（シダイ／光を支える影にも名がある。寡黙・カガチの相棒）
- HeroIlena（イレーナ／同じ悲劇を観ても希望を選ぶ。観測者側の知性）

## 書く量（合計147組。既に実装済みの組は不要）
- グループ1: 主人公6人（HeroZiran, HeroReva, HeroKara, HeroBrome, HeroShidai, HeroIlena）× 20ボス全部 = 120組。
- グループ2: 主人公3人（HeroAldin, HeroKagachi, HeroVesna）× **新ボス9体** = 27組。
  新ボス9体: neutral_rook, neutral_sister, neutral_mechaz0rwing, neutral_mechaz0rsword,
  neutral_mechaz0rsuper, neutral_mechaz0rhelm, neutral_mechaz0rchassis, neutral_mechaz0rcannon, neutral_hydrax。
  （この3主人公は Caliber / Magmar3 / Abyssian3 / Vetruvian3 / Arcana の戦前が実装済みなので不要。）

各組**3行**（ボス→主人公→ボスの交互）。

## 出力フォーマット（厳守）
```
[prefight boss=Magmarvaath hero=HeroZiran]
1(ボス): …
2(主人公): …
3(ボス): …
```
日本語のみ。グループ1（120組）→グループ2（27組）の順で取りこぼしなく出力してください。
量が多い場合は陣営ごと（Caliber〜sister / Magmar / Abyssian / Vetruvian / Mechaz0r〜hydrax / Arcana）に区切って提出してOK。
