# Chapter01 Full Script Draft: 白門の後悔

この稿は `Chapter01` 実装用の台本です。各行に `chapter / round / speaker` が分かるIDを付けています。

## 章方針

- Chapter01の核: 「守れなかった痛みを理由に、世界を眠らせていいのか」
- キャリバーの立場: 悪役ではなく、白門を守れなかった騎士。民を苦しみから遠ざけるために、門ごと終わらせようとしている。
- 主人公側の答え: 守れなかった痛みを抱えたまま、それでも次の一人を守る。
- 門の初回理解: 門はただの入口ではなく、誰かの強い後悔や願いが形になった場所。中ではその人の考えが敵や地形になり、それを越えると次の門が開く。
- 表現方針: 難しい比喩だけで終わらせない。「何をされたら嫌なのか」「何を守りたいのか」を台詞の中で直接言う。
- 伏線の扱い: 終盤の重要人物は名前を出さず、「白い声」「優しい声」「誰かに観られていた感覚」だけで残す。
- 章クリア報酬: `Caliber` は正式な仲間ではなく、剣片契約による戦術ユニット仮加入。再クリアで強化可能。
- アイコン参照: 中ボスは各 `[midboss]` の `unit/icon` を使用。章ボスは `boss=Caliber` を会話アイコン参照キーとして扱う。

## 陣営の志

- ライオネル: 守れなかった人の名前を忘れず、今いる誰かを守る。
- ソンガイ: 迷っても足を止めない。弱さを知った上で、前へ出る。
- ヴァナー: 寒い場所にも、帰れる場所を作る。
- マグマー: 生きる力を消さない。力は世界を終わらせるためではなく、明日へ進むために使う。
- アビサル: 別れや痛みを隠さない。それでも手を伸ばした事実を残す。
- ヴェトルビアン: 未来を決めつけない。終わると分かっていても、別の道を探す。

## Opening

[interlude id=CH01_OPENING]
meta: chapter=01 round=Opening place=白門跡 participants=HeroAldin,HeroKagachi,HeroVesna,Caliber

C01-OPEN-01 S: 白門跡。昼なのに空は白く、鐘楼の鐘は鳴らない。
C01-OPEN-02 S: 広場には倒れた人々。息はある。眠っているように、誰も目を開けない。
C01-OPEN-03 N: 風が止まっている。泣き声だけが、石畳の上を小さく転がる。
C01-OPEN-04 HeroAldin: こちらへ。大丈夫、声は出さなくていい…歩ける者から、私の後ろへ！
C01-OPEN-05 S: アルディンが盾を地面に打ち、避難民の前に立つ。
C01-OPEN-06 Child: お母さんが、起きない……。
C01-OPEN-07 HeroAldin: 見捨てない。何度でも名を呼べ…眠りの向こうへ届くまで、私もここを離れない！
C01-OPEN-08 HeroKagachi: 後ろは任せた！ 前は俺が割る…邪魔する奴から順番だ！
C01-OPEN-09 S: カガチが白く濁った兵の群れへ踏み込む。刃が抜ける音だけが鋭い。
C01-OPEN-10 HeroKagachi: どけ！ 寝言みたいな剣で人を囲むな。そんな手つきで守ってるつもりか？
C01-OPEN-11 HeroVesna: ここに火を置きます…小さいけれど、寒さで足が止まらないように！
C01-OPEN-12 S: ヴェスナが崩れた門柱の陰に小さな火を灯す。炎は赤ではなく、青白く揺れる。
C01-OPEN-13 Evacuee: 火が……あたたかい。
C01-OPEN-14 HeroVesna: あなたの帰る場所までは守れない…でも今だけは、この火で震えを止めます。息をして、目をそらさないで！
C01-OPEN-15 S: 白門の奥、本来は崩れているはずの石アーチがゆがむ。内側に、もう一つの白門跡が重なって見える。
C01-OPEN-16 HeroKagachi: 何だありゃ？ 門の向こうに、また同じ広場が見えるぞ。道じゃねえのか？
C01-OPEN-17 HeroVesna: 道ではありません…誰かのつらい記憶が、そのまま場所になっています。
C01-OPEN-18 HeroAldin: 門はただの入口ではない。強い後悔や願いが、形になった場所だ。
C01-OPEN-19 HeroAldin: 中では、その人の考えが敵になる。倒すだけでは進めない。こちらの答えを示す必要がある。
C01-OPEN-20 HeroKagachi: つまり、奥で待ってる奴に「それは違う」って叩きつけるわけか？ なら分かる！
C01-OPEN-21 HeroVesna: その人の痛みも見ないと、門は閉じない…そういうことですね。
C01-OPEN-22 S: 白門の奥、折れた騎士像の前にキャリバーが立つ。鎧の傷に白い光が染みている。
C01-OPEN-23 Caliber: 白門を閉じろ。
C01-OPEN-24 S: その声で、眠った兵たちが一斉に顔を上げる。
C01-OPEN-25 Caliber: これ以上、名を増やすな。これ以上、祈りを折るな。
C01-OPEN-26 HeroAldin: キャリバー……あなたなのか？ どうして、こんな眠りを門に広げた！
C01-OPEN-27 Caliber: アルディン。まだ立つか。まだ、守れると思うのか。
C01-OPEN-28 HeroAldin: 思っているのではない。立つんだ…立たなければ、また誰かが倒れる！
C01-OPEN-29 HeroKagachi: 古い知り合いか？ ずいぶん嫌な目で見てくるな…ああいう顔、殴る前に止めた方がいいぜ！
C01-OPEN-30 HeroVesna: あの人、怒っているのに……泣いているように見えます。炎が近づくほど、冷たくなる感じです…
C01-OPEN-31 Caliber: 民を眠らせる。これ以上、苦しむ人を増やさないために。白門は、最後に閉じる盾だ。
C01-OPEN-32 HeroAldin: 違う！ 盾は人を眠らせるためのものじゃない…生きている人を守るものだ！
C01-OPEN-33 Caliber: ならば証明しろ。守れなかった者が、何を守れるのか。
C01-OPEN-34 S: 白い風が門の内側へ吸い込まれ、道が戦場へ変わる。
C01-OPEN-35 N: 誰かに観られているような感覚が、一瞬だけ背中を撫でた。

## First Node

[node id=CH01_NODE_01]
meta: chapter=01 round=Node01 role=first_battle_intro

C01-N01-01 S: 白門前庭。眠りに落ちた兵が、盾を引きずって近づく。
C01-N01-02 HeroAldin: 斬るためではない。通すために戦う…倒れた人の上で、勝利の名を叫ばせるな！
C01-N01-03 HeroKagachi: 眠ったまま殴ってくるなら、こっちも遠慮しないぜ！ 起きた時に謝る準備だけしておけ！
C01-N01-04 HeroVesna: 火を目印にしてください！ 倒れた人を踏ませないで…ここはまだ、人が休める場所です！
C01-N01-05 SYSTEM: Tutorial Battle: 白門の眠兵を突破せよ。

## Caliber Echo

[echo id=CH01_CALIBER_ECHO_01]
meta: chapter=01 round=Echo01 trigger=after_node_01 speaker=Caliber

C01-E01-01 Caliber(遠く): 泣き声は消えない。勝った後でも、ずっと耳に残る。
C01-E01-02 Caliber(遠く): だから私が終わらせる。死者の名前が、これ以上増える前に。

[echo id=CH01_CALIBER_ECHO_02]
meta: chapter=01 round=Echo02 trigger=mid_route speaker=Caliber

C01-E02-01 Caliber(遠く): お前たちは前へ進むたび、倒れた者を後ろに残す。
C01-E02-02 Caliber(遠く): それを希望と呼ぶなら、希望は残された者に冷たい。

[echo id=CH01_CALIBER_ECHO_03]
meta: chapter=01 round=Echo03 trigger=before_midboss_04 speaker=Caliber

C01-E03-01 Caliber(遠く): リオラの名を、忘れたわけではあるまい。
C01-E03-02 Caliber(遠く): 忘れていないなら、なぜまだ剣を前へ向ける。

## Midbosses

[midboss id=CH01_MID_01 unit=neutral_beastmaster icon=neutral_beastmaster.png]
meta: chapter=01 round=Mid01 name=白門の獣使いバルガ role=門外周の番犬使い

C01-M01-01 白門の獣使いバルガ: グルル……騎士様の命令だ。門に近づく奴は、全部噛み砕く。
C01-M01-02 Hero: 飼われた獣まで眠らせる気か？ かわいそうに…牙が震えてるぞ、見えてないのか！
C01-M01-03 白門の獣使いバルガ: かわいそう？ はっ、起きて泣くよりマシだろうが！

[midboss id=CH01_MID_02 unit=neutral_gnasher icon=neutral_gnasher.png]
meta: chapter=01 round=Mid02 name=眠符の屍爪リンシェン role=眠兵の群れ

C01-M02-01 眠符の屍爪リンシェン: シィ……寝かせてやれよ。痛い朝なんか来ない方がいい。
C01-M02-02 Hero: 朝が痛くても、自分で目を開ける権利はある！ 勝手に眠らせるな！
C01-M02-03 眠符の屍爪リンシェン: きれいごとだねぇ。泣き顔を見たら、すぐ黙るくせに。

[midboss id=CH01_MID_03 unit=neutral_rawr icon=neutral_rawr.png]
meta: chapter=01 round=Mid03 name=鋼牙の機豹ラウル role=白門の突破阻止

C01-M03-01 鋼牙の機豹ラウル: ガルルッ。進入者、確認。傷の増加を止める。
C01-M03-02 Hero: 傷を増やさないために、命令で人を倒すのか？ その矛盾、機械の口でごまかせると思うな！
C01-M03-03 鋼牙の機豹ラウル: おかしいことは分かっている。だが門の命令は絶対だ。

[midboss id=CH01_MID_04 unit=neutral_rok icon=neutral_rok.png]
meta: chapter=01 round=Mid04 name=鎖門の岩顎ゴルム role=鐘楼前の封鎖

C01-M04-01 鎖門の岩顎ゴルム: 進むな。鐘は鳴らない。鳴らせば、眠った者まで起きてしまう。
C01-M04-02 Hero: 夢の中へ閉じ込めるな！ 崩れた家でも、帰ると決める人がいるんだ！
C01-M04-03 鎖門の岩顎ゴルム: 帰り道？ 崩れた家にか。そんなもの、残っていない。

[midboss id=CH01_MID_05 unit=neutral_zukong icon=neutral_zukong.png]
meta: chapter=01 round=Mid05 name=鐘楼の猿将ズーコン role=鐘楼上層の攪乱

C01-M05-01 鐘楼の猿将ズーコン: キキッ、上から見れば分かるぜ。下の連中、みんな泣いてる。
C01-M05-02 Hero: だから下へ降りるんだよ！ 泣いている人を上から数えるだけなら、そこをどけ！
C01-M05-03 鐘楼の猿将ズーコン: 偉いねぇ。なら落ちても笑ってやるよ、英雄さん。

[midboss id=CH01_MID_06 unit=neutral_gnasher_ice icon=neutral_gnasher_ice.png]
meta: chapter=01 round=Mid06 name=白符の祈り手メイリン role=章ボス直前の祈り手

C01-M06-01 白符の祈り手メイリン: 祈りはもう疲れました。届かない声なら、眠らせてあげたい。
C01-M06-02 Hero: 届かなくても、呼ぶ人がいる…声がかすれても、そこで終わりにしない！
C01-M06-03 白符の祈り手メイリン: その強さについていけない人もいるのです。

## Boss Prefight

[prefight boss=Caliber hero=HeroAldin]
meta: chapter=01 round=BossPrefight hero_name=アルディン faction=ライオネル

C01-PF-HeroAldin-01 Caliber: アルディン。お前はリオラを守れなかった。それでもまた守ると言うのか。
C01-PF-HeroAldin-02 HeroAldin: 守れなかった。だから終わらせるんじゃない…次の人を守るために立つ！
C01-PF-HeroAldin-03 Caliber: その言葉が本物か、私の剣で確かめる。

[prefight boss=Caliber hero=HeroKagachi]
meta: chapter=01 round=BossPrefight hero_name=カガチ faction=ソンガイ

C01-PF-HeroKagachi-01 Caliber: ソンガイの刃は速い。だが速くても、死んだ者は救えない。
C01-PF-HeroKagachi-02 HeroKagachi: だから止まれって？ 違うな。止まったら、生きてる奴まで死ぬだろ！
C01-PF-HeroKagachi-03 Caliber: その足を止めれば、少しは後ろを見るだろう。

[prefight boss=Caliber hero=HeroVesna]
meta: chapter=01 round=BossPrefight hero_name=ヴェスナ faction=ヴァナー

C01-PF-HeroVesna-01 Caliber: ヴェスナ。壊れた白門に、まだ帰る場所を作ると言うのか。
C01-PF-HeroVesna-02 HeroVesna: はい！ 寒い場所には火を置きます。誰かが戻ってこられるように！
C01-PF-HeroVesna-03 Caliber: その火でまた誰かが傷つく。だから私が消す。

[prefight boss=Caliber hero=HeroZiran]
meta: chapter=01 round=BossPrefight hero_name=ジラーン faction=ライオネル

C01-PF-HeroZiran-01 Caliber: 癒し手よ。祈っても、救えない命はある。
C01-PF-HeroZiran-02 HeroZiran: あります。だから祈るだけでなく、手を伸ばします！ 勝手に眠らせないでください！
C01-PF-HeroZiran-03 Caliber: ならば、その苦しみごと抱えて立て。

[prefight boss=Caliber hero=HeroReva]
meta: chapter=01 round=BossPrefight hero_name=レヴァ faction=ソンガイ

C01-PF-HeroReva-01 Caliber: 自由な者は、苦しくなればすぐ去る。倒れた者の横には残らない。
C01-PF-HeroReva-02 HeroReva: あは、決めつけないで。去るか残るかは私が決める。今日は残るって決めたの！
C01-PF-HeroReva-03 Caliber: ならば逃げ場のない門で、その選択を試す。

[prefight boss=Caliber hero=HeroKara]
meta: chapter=01 round=BossPrefight hero_name=カーラ faction=ヴァナー

C01-PF-HeroKara-01 Caliber: 待っても、帰らない者はいる。待つほど苦しくなるだけだ。
C01-PF-HeroKara-02 HeroKara: それでも待つ。戻る場所を閉じたら、その人は本当に帰れない…
C01-PF-HeroKara-03 Caliber: その待つ心ごと砕く。門はもう、誰も待たない。

[prefight boss=Caliber hero=HeroBrome]
meta: chapter=01 round=BossPrefight hero_name=ブローム faction=ライオネル

C01-PF-HeroBrome-01 Caliber: 旗を掲げても、守れなかった人はいる。
C01-PF-HeroBrome-02 HeroBrome: だから名を呼ぶ！ 違う声でも、震えていても、共に守れると示すために！
C01-PF-HeroBrome-03 Caliber: その旗がただの飾りでないと、剣で示せ。

[prefight boss=Caliber hero=HeroShidai]
meta: chapter=01 round=BossPrefight hero_name=シダイ faction=ソンガイ

C01-PF-HeroShidai-01 Caliber: 陰で支える者は、最初に忘れられる。
C01-PF-HeroShidai-02 HeroShidai: 忘れられても支える。だが消される気はない。俺にも名がある…！
C01-PF-HeroShidai-03 Caliber: ならば、その名ごと断つ。白門に隠れる場所はない。

[prefight boss=Caliber hero=HeroIlena]
meta: chapter=01 round=BossPrefight hero_name=イレーナ faction=ヴァナー

C01-PF-HeroIlena-01 Caliber: よく見て考える者よ。悲劇を見れば、終わらせたくなるはずだ。
C01-PF-HeroIlena-02 HeroIlena: 見ました。だからこそ、終わりだけを答えにはしません！ 希望を選びます。
C01-PF-HeroIlena-03 Caliber: その希望が甘さでないか、ここで確かめる。

[prefight boss=Caliber hero=Magmarvaath]
meta: chapter=01 round=BossPrefight hero_name=ヴァース faction=マグマー

C01-PF-Magmarvaath-01 Caliber: マグマーの獣よ。力だけで救えるなら、白門は落ちなかった。
C01-PF-Magmarvaath-02 Magmarvaath: 力だけじゃねえ！ だが生きてる奴を、勝手に眠らせるなって言ってるんだ！
C01-PF-Magmarvaath-03 Caliber: その吠え声を、私の剣で黙らせる。

[prefight boss=Caliber hero=Magmarstarhorn]
meta: chapter=01 round=BossPrefight hero_name=スターホーン faction=マグマー

C01-PF-Magmarstarhorn-01 Caliber: 戦いを楽しむ者よ。疲れた者には、お前の熱も苦しい。
C01-PF-Magmarstarhorn-02 Magmarstarhorn: ハッ！ でも熱がなきゃ明日まで立てねえ！ 眠らせるより起こしてやる！
C01-PF-Magmarstarhorn-03 Caliber: その騒がしい角笛を、白門の前で折る。

[prefight boss=Caliber hero=Magmarragnora]
meta: chapter=01 round=BossPrefight hero_name=ラグノラ faction=マグマー

C01-PF-Magmarragnora-01 Caliber: 群れの母よ。守ると言って抱えすぎれば、相手は息ができない。
C01-PF-Magmarragnora-02 Magmarragnora: 我は声を消すために抱かぬ！ 戻る場所を作るために抱くのだ！
C01-PF-Magmarragnora-03 Caliber: ならばその場所ごと断つ。白門に戻る場所はない。

[prefight boss=Caliber hero=Abyssallilithe]
meta: chapter=01 round=BossPrefight hero_name=リリス faction=アビサル

C01-PF-Abyssallilithe-01 Caliber: アビサルの花よ。どうせ別れるなら、最初から手を伸ばさない方が優しい。
C01-PF-Abyssallilithe-02 Abyssallilithe: ふふ、それは優しさじゃないわ。出会ったことまで消すつもり？
C01-PF-Abyssallilithe-03 Caliber: 痛みが止まるなら、私はそれを選ぶ。

[prefight boss=Caliber hero=Abyssalcassyva]
meta: chapter=01 round=BossPrefight hero_name=キャシヴァ faction=アビサル

C01-PF-Abyssalcassyva-01 Caliber: 笑っても、倒れた者は戻らない。
C01-PF-Abyssalcassyva-02 Abyssalcassyva: あは、戻らないよ！ でも生きてる私は笑うし、腹も減る。それが悪い？
C01-PF-Abyssalcassyva-03 Caliber: その騒がしい命を、ここで静める。

[prefight boss=Caliber hero=Abyssalmaehv]
meta: chapter=01 round=BossPrefight hero_name=メーヴ faction=アビサル

C01-PF-Abyssalmaehv-01 Caliber: 救いを知る者よ。苦しみを終わらせることも、救いだと分かるはずだ。
C01-PF-Abyssalmaehv-02 Abyssalmaehv: 分かります。でも、目を閉じるかは本人が選ぶことです！
C01-PF-Abyssalmaehv-03 Caliber: その迷いが、また誰かを泣かせる。

[prefight boss=Caliber hero=Vetruvianzirix]
meta: chapter=01 round=BossPrefight hero_name=ジリックス faction=ヴェトルビアン

C01-PF-Vetruvianzirix-01 Caliber: 砂の管理者よ。小さなミスを消せば、悲劇は減る。
C01-PF-Vetruvianzirix-02 Vetruvianzirix: 却下。ミスの中にも、生き残る道がある。消せば未来も消える…
C01-PF-Vetruvianzirix-03 Caliber: その危険な未来を、私が終わらせる。

[prefight boss=Caliber hero=Vetruviansajj]
meta: chapter=01 round=BossPrefight hero_name=サージ faction=ヴェトルビアン

C01-PF-Vetruviansajj-01 Caliber: 砂の剣士よ。大切なものは、いつか失う。
C01-PF-Vetruviansajj-02 Vetruviansajj: ええ。でも失った後も、胸に残るものがあります…それまで消さないでください。
C01-PF-Vetruviansajj-03 Caliber: 残る思いは痛みを呼ぶ。白門で消す。

[prefight boss=Caliber hero=Vetruvianscion]
meta: chapter=01 round=BossPrefight hero_name=サイオン faction=ヴェトルビアン

C01-PF-Vetruvianscion-01 Caliber: 舞う者よ。終わりは、静かに受け入れるべきだ。
C01-PF-Vetruvianscion-02 Vetruvianscion: 終わりが来ても、一歩は選べます！ 最後まで、私は私の足で進みます！
C01-PF-Vetruvianscion-03 Caliber: ならば、その足を止める。白門に踊りは要らぬ。

## Boss Postfight

[postboss boss=Caliber hero=HeroAldin]
meta: chapter=01 round=BossPostfight hero_name=アルディン reward=CaliberShard

C01-PO-HeroAldin-01 Caliber: まだ盾を握るか。
C01-PO-HeroAldin-02 HeroAldin: 折れても拾う…何度でも。リオラも、白門も、私の罪も忘れない！
C01-PO-HeroAldin-03 Caliber: お前を認めたわけではない。ただ、どこまで守れるか見届ける。
C01-PO-HeroAldin-04 Caliber: 剣片を持て。私はお前の近くで見る。

[postboss boss=Caliber hero=HeroKagachi]
meta: chapter=01 round=BossPostfight hero_name=カガチ reward=CaliberShard

C01-PO-HeroKagachi-01 Caliber: 足を止めたつもりだったが、まだ進むか。
C01-PO-HeroKagachi-02 HeroKagachi: 当たり前だろ！ 痛いから止まるなら、とっくに寝てる。転んでも前に倒れる！
C01-PO-HeroKagachi-03 Caliber: その強がりが本物か、私が見届ける。
C01-PO-HeroKagachi-04 Caliber: 剣片を持て。無茶をしすぎれば、私が止める。

[postboss boss=Caliber hero=HeroVesna]
meta: chapter=01 round=BossPostfight hero_name=ヴェスナ reward=CaliberShard

C01-PO-HeroVesna-01 Caliber: お前の小さな火で、眠っていた白門が揺れた。
C01-PO-HeroVesna-02 HeroVesna: 眠りより寒い夜があります…誰にも待たれない夜です。だから火を置くんです！
C01-PO-HeroVesna-03 Caliber: その火が人を守るか、また傷つけるか。見届けよう。
C01-PO-HeroVesna-04 Caliber: 剣片を持て。私はまだ、お前の火を信じきれない。

[postboss boss=Caliber hero=HeroZiran]
meta: chapter=01 round=BossPostfight hero_name=ジラーン reward=CaliberShard

C01-PO-HeroZiran-01 Caliber: 苦しみも命、と言ったな。重い言葉だ。
C01-PO-HeroZiran-02 HeroZiran: はい。だから勝手に眠らせてはいけません…苦しむ時間にも、誰かの手が必要です！
C01-PO-HeroZiran-03 Caliber: その祈りが逃げではないか、見届ける。
C01-PO-HeroZiran-04 Caliber: 剣片を持て。癒せない傷も、私が覚えておく。

[postboss boss=Caliber hero=HeroReva]
meta: chapter=01 round=BossPostfight hero_name=レヴァ reward=CaliberShard

C01-PO-HeroReva-01 Caliber: 逃げ場のない門で、お前は残った。
C01-PO-HeroReva-02 HeroReva: ふふ、残ると決めた私を甘く見すぎたわね。逃げない自由って、しつこいのよ？
C01-PO-HeroReva-03 Caliber: その選択が本物か、見届ける。
C01-PO-HeroReva-04 Caliber: 剣片を持て。自由を言い訳にした時、私は斬る。

[postboss boss=Caliber hero=HeroKara]
meta: chapter=01 round=BossPostfight hero_name=カーラ reward=CaliberShard

C01-PO-HeroKara-01 Caliber: 誰も待たない門を、お前は押し返した。
C01-PO-HeroKara-02 HeroKara: 待つ…何度でも。眠らせて終わりにはしない。帰るかどうかは、本人が決める…
C01-PO-HeroKara-03 Caliber: その待つ強さが本物か、見届ける。
C01-PO-HeroKara-04 Caliber: 剣片を持て。待つことが苦しみに変わる時、私が止める。

[postboss boss=Caliber hero=HeroBrome]
meta: chapter=01 round=BossPostfight hero_name=ブローム reward=CaliberShard

C01-PO-HeroBrome-01 Caliber: 旗は倒れなかった。だが旗だけでは人は救えない。
C01-PO-HeroBrome-02 HeroBrome: 分かっている！ だから違う声を集めるのだ。そろわなくても、共に立てる！
C01-PO-HeroBrome-03 Caliber: その結束が押しつけにならぬか、見届ける。
C01-PO-HeroBrome-04 Caliber: 剣片を持て。旗が人を縛るなら、私が斬る。

[postboss boss=Caliber hero=HeroShidai]
meta: chapter=01 round=BossPostfight hero_name=シダイ reward=CaliberShard

C01-PO-HeroShidai-01 Caliber: お前を消したつもりだった。
C01-PO-HeroShidai-02 HeroShidai: 消えねえよ…表に出なくても、俺は支える。俺にも名がある…！
C01-PO-HeroShidai-03 Caliber: その名が折れないか、見届ける。
C01-PO-HeroShidai-04 Caliber: 剣片を持て。見えない場所から私も見る。

[postboss boss=Caliber hero=HeroIlena]
meta: chapter=01 round=BossPostfight hero_name=イレーナ reward=CaliberShard

C01-PO-HeroIlena-01 Caliber: 悲劇を見た上で希望を選ぶ。危うい結論だ。
C01-PO-HeroIlena-02 HeroIlena: 危うさは承知しています…けれど、終わりだけを正解にはしません。別の結果を残します！
C01-PO-HeroIlena-03 Caliber: その希望が甘さに変わらぬか、見届ける。
C01-PO-HeroIlena-04 Caliber: 剣片を持て。見落とされた痛みを、私が覚えておく。

[postboss boss=Caliber hero=Magmarvaath]
meta: chapter=01 round=BossPostfight hero_name=ヴァース reward=CaliberShard

C01-PO-Magmarvaath-01 Caliber: 獣の力が、眠りを破った。
C01-PO-Magmarvaath-02 Magmarvaath: 眠ってる肉は動かねえ！ 生きてるなら吠えろ、噛め、立て！ まだ終わってねえ！
C01-PO-Magmarvaath-03 Caliber: その力が守るためのものか、ただ壊すだけか。見届ける。
C01-PO-Magmarvaath-04 Caliber: 剣片を持て。間違えた時は、私が断つ。

[postboss boss=Caliber hero=Magmarstarhorn]
meta: chapter=01 round=BossPostfight hero_name=スターホーン reward=CaliberShard

C01-PO-Magmarstarhorn-01 Caliber: お前の声が、白門の静けさを壊した。
C01-PO-Magmarstarhorn-02 Magmarstarhorn: ハハッ！ 静かすぎる墓場なんざ、俺の角笛で起こしてやる！ まだ朝だ！
C01-PO-Magmarstarhorn-03 Caliber: その戦いが誰かを傷つけぬか、見届ける。
C01-PO-Magmarstarhorn-04 Caliber: 剣片を持て。戦いを言い訳にするなら、斬る。

[postboss boss=Caliber hero=Magmarragnora]
meta: chapter=01 round=BossPostfight hero_name=ラグノラ reward=CaliberShard

C01-PO-Magmarragnora-01 Caliber: お前は白門を飲み込まなかった。
C01-PO-Magmarragnora-02 Magmarragnora: 我は戻る場所を作る…声を消すためではない！
C01-PO-Magmarragnora-03 Caliber: その腕が守りか支配か、見届ける。
C01-PO-Magmarragnora-04 Caliber: 剣片を持て。守る名で誰かを消すなら、私は斬る。

[postboss boss=Caliber hero=Abyssallilithe]
meta: chapter=01 round=BossPostfight hero_name=リリス reward=CaliberShard

C01-PO-Abyssallilithe-01 Caliber: 出会ったことまで消すな、か。痛い言葉だ。
C01-PO-Abyssallilithe-02 Abyssallilithe: 痛いから残るの…触れなかったふりより、ずっといいでしょう？
C01-PO-Abyssallilithe-03 Caliber: その別れが逃げでないか、見届ける。
C01-PO-Abyssallilithe-04 Caliber: 剣片を持て。人を孤独へ誘うなら、私は斬る。

[postboss boss=Caliber hero=Abyssalcassyva]
meta: chapter=01 round=BossPostfight hero_name=キャシヴァ reward=CaliberShard

C01-PO-Abyssalcassyva-01 Caliber: お前の笑いは、白門に似合わない。
C01-PO-Abyssalcassyva-02 Abyssalcassyva: あは、それ最高のほめ言葉！ 似合わないくらい生きてるってこと！
C01-PO-Abyssalcassyva-03 Caliber: その笑いが痛みから逃げていないか、見届ける。
C01-PO-Abyssalcassyva-04 Caliber: 剣片を持て。生きることを軽く見た時、私が止める。

[postboss boss=Caliber hero=Abyssalmaehv]
meta: chapter=01 round=BossPostfight hero_name=メーヴ reward=CaliberShard

C01-PO-Abyssalmaehv-01 Caliber: お前は私に近い。だからこそ、私と違う道を選んだ。
C01-PO-Abyssalmaehv-02 Abyssalmaehv: 休ませたい気持ちはあります…でも、目を閉じるかは本人のものです！
C01-PO-Abyssalmaehv-03 Caliber: その優しさが弱さにならぬか、見届ける。
C01-PO-Abyssalmaehv-04 Caliber: 剣片を持て。救いの名で奪うなら、私は斬る。

[postboss boss=Caliber hero=Vetruvianzirix]
meta: chapter=01 round=BossPostfight hero_name=ジリックス reward=CaliberShard

C01-PO-Vetruvianzirix-01 Caliber: ミスを残す。危険な判断だ。
C01-PO-Vetruvianzirix-02 Vetruvianzirix: 危険は承知。だが消せば、助かる未来まで消える…
C01-PO-Vetruvianzirix-03 Caliber: その計算が人の涙を見落とさぬか、見届ける。
C01-PO-Vetruvianzirix-04 Caliber: 剣片を持て。数字で痛みを捨てるなら、斬る。

[postboss boss=Caliber hero=Vetruviansajj]
meta: chapter=01 round=BossPostfight hero_name=サージ reward=CaliberShard

C01-PO-Vetruviansajj-01 Caliber: 失った後に残るものを、お前はまだ拾うのか。
C01-PO-Vetruviansajj-02 Vetruviansajj: 拾います…諦めた後でも、まだ持てるものはあります。
C01-PO-Vetruviansajj-03 Caliber: それが希望か、ただの未練か。見届ける。
C01-PO-Vetruviansajj-04 Caliber: 剣片を持て。諦めたふりで逃げるなら、私が斬る。

[postboss boss=Caliber hero=Vetruvianscion]
meta: chapter=01 round=BossPostfight hero_name=サイオン reward=CaliberShard

C01-PO-Vetruvianscion-01 Caliber: 終わりは来なかった。お前が足で変えた。
C01-PO-Vetruvianscion-02 Vetruvianscion: 結末は受け止めます。けれど、最後の一歩は自分で選べます！
C01-PO-Vetruvianscion-03 Caliber: その一歩が逃げでないか、見届ける。
C01-PO-Vetruvianscion-04 Caliber: 剣片を持て。終わりを飾るだけなら、私は斬る。

## Observation

[observation id=OBS_CH01]
meta: chapter=01 round=Observation title=白門の慈悲 trigger=after_boss_clear

C01-OBS-01 S: キャリバーの剣片が白く光り、空中に薄いページが開く。
C01-OBS-02 S: 文字は誰かの手書きのように震え、読み終えるまで消えない。

タイトル: 白門の慈悲

終焉は慈悲だと、誰かが言った。
倒れた子の名を呼ぶ声も、瓦礫の下で途切れた祈りも、もう増えなくて済むから。
けれど私は知っている。
増えない痛みは、癒える時間も持たない。
鐘は鳴らなかった。
それでも、誰かが鐘楼へ登ろうとしていた。

C01-OBS-03 N: ページが閉じる直前、優しい声が遠くで笑った気がした。
C01-OBS-04 N: この記録は、「苦しむ人をこれ以上増やしたくないから、世界を終わらせたい」という考えを残していた。
C01-OBS-05 N: その声は名乗らない。ただ、こちらを観ていた。

## Reward

[reward id=CH01_REWARD_CALIBER]
meta: chapter=01 round=Reward unlock=Caliber provisional=true

C01-RWD-01 SYSTEM: キャリバーが戦術ユニットとして仮加入しました。
C01-RWD-02 SYSTEM: キャリバーの剣片を獲得。
C01-RWD-03 SYSTEM: 観測片「白門の慈悲」を記録しました。
C01-RWD-04 SYSTEM: Chapter01を再クリアすると、キャリバーの剣片契約が深まり、戦術ユニット性能が強化されます。
C01-RWD-05 Caliber: 私は友ではない。お前たちが本当に人を守れるのか、近くで見届ける。
C01-RWD-06 N: 一行は剣片を受け取る。眠らせて終わらせるのではなく、明日へ進むために。

## Next

[next id=CH01_TO_CH02]
meta: chapter=01 round=NextChapter next_chapter=Chapter02 next_boss=Olm

C01-NEXT-01 S: 白門の奥で、次の門がゆっくり形を取る。
C01-NEXT-02 N: その門の前では、誰も一歩を踏み出せない。
C01-NEXT-03 UnknownVoice: 進むな。進めば、また傷つく。
C01-NEXT-04 HeroAldin: 止まれば、助けを待つ声も遠ざかる…足が重くても進む。誰かの明日まで！
C01-NEXT-05 SYSTEM: Chapter02「停足の番人オルム」が解放されました。

## QA Checklist

- [x] Chapter01で終盤の重要人物の固有名を出していない。
- [x] Openingで「門」がただの入口ではなく、強い後悔や願いが形になった場所だと説明している。
- [x] キャリバーを純粋な悪役にも完全な被害者にもしていない。
- [x] キャリバーは撃破後も改心しきらず、剣片契約で仮加入する。
- [x] 初期3主人公の見せ場をOpeningに入れている。
- [x] 中ボス6体に `Assets/Images/Units/Icon/T3` の `neutral_*` 素体名を使用している。
- [x] 18主人公ぶんのprefight / postbossを用意している。
- [x] 観測片「白門の慈悲」をChapter01報酬に含めている。
- [x] Chapter02「停足の番人オルム」への導線を入れている。
