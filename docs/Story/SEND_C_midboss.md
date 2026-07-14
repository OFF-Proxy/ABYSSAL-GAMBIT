あなたは『AutoChessBossRush』（Unity 2Dローグライク・オートチェス）のシナリオライターです。
ゲーム内の**中ボス戦“前”**の短い掛け合いを日本語で書いてください。本文だけを指定フォーマットで出力してください。

## 物語の核
「世界を諦めた観測者アルカナに、もう一度未来を信じさせる物語」。主人公はプレイヤーが選んだヒーロー1人。

## このシーンの性質
中ボスは各章の仲間化候補で、章ボスより格下だが手強い敵。戦闘開始直前の短い対峙。
**主人公共通の台詞**にするため、主人公側は固有名や固有設定を避け、誰が使っても成立する一般的な返しにする。
短く、ボスの所属陣営の気配を出す（Magmar勢＝荒い本能、Abyssian勢＝影と死、Vetruvian勢＝砂と秩序、neutral獣＝野生）。

## 対象中ボス（bossId・14体）
- Silitharelder, Makantorwarbeast, Veteransilithar（Magmar陣営）
- Gloomchaser, Abyssalcrawler（Abyssian陣営）
- Rae, Starfirescarab, Pax, Pyromancer（Vetruvian陣営）
- neutral_beastmaster, neutral_gnasher, neutral_rawr, neutral_rok, neutral_zukong（中立の獣）

## 書く量・構成
14体すべて。各**3〜5行**（ボス→主人公→ボス…の交互、ボス開始）。

## 出力フォーマット（厳守）
主人公は共通なので hero 指定なし。
```
[midboss boss=Silitharelder]
1(ボス): …
2(主人公): …
3(ボス): …
```
日本語のみ。14体を取りこぼしなく出力してください。
