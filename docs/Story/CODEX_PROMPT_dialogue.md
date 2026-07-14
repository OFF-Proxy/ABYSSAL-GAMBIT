# Codexに渡すプロンプト（そのままコピペ用）

> 下の `===` で囲んだブロックを Codex（GPT）に貼る。`docs/Story/DIALOGUE_COMMISSION_BRIEF.md` と
> `HeroBossDialogueUI.cs` の既存 `BuildScriptedLines()` を一緒に渡せると精度が上がる。

===========================================================================

あなたは『AutoChessBossRush』（Unity 2D ローグライク・オートチェス）のシナリオライターです。
ゲーム内ダイアログの**日本語セリフ本文**を執筆してください。システム実装は別担当が行うので、
あなたは指定フォーマットのテキストだけを出力してください。

## 物語の核
テーマは「終焉に抗う物語」ではなく **「世界を諦めた観測者アルカナに、もう一度未来を信じさせる物語」**。
キーワード＝希望／諦め／未来／選択／仲間／理解。ラスボスはアルカナ（最終章）。
主人公はプレイヤーが選んだヒーロー1人。

## 文体ルール（厳守）
- 各ボスは1つの「思想」を体現し、その思想で主人公を否定・誘惑する。
- 各主人公は一貫した「核となる価値観」で自分の言葉で返す（下の対応表）。
- 短く、芯のある言い回し。説教くさく・叙情に流れすぎない。戦闘直前の緊張感を保つ。
- **アルカナだけは覇王口調にしない**。ですます＋いたわり（「もう休んでいい」と諭す慈愛）。
- カガチ8章の犬化は、屈辱→生きる実感→「この姿でも進む」という再起劇。ギャグにしない。
- 固有設定: キャリバー＝主人公アルディンの兄弟子。白門の陥落、守れなかった少女リオラが因縁。

## ボス対応表（章順・テーマ）
1 キャリバー(Caliber)=後悔 / 2 ルーク(neutral_rook)=門番・停滞[テーマ確定可] /
3 シスター(neutral_sister)=盲信・献身[同] / 4 ヴァース(Magmarvaath)=本能 /
5 スターホーン(Magmarstarhorn)=闘争 / 6 ラグノラ(Magmarragnora)=群れ /
7 リリス(Abyssallilithe)=別れ / 8 キャシヴァ(Abyssalcassyva)=生(カガチ犬化章) /
9 メーヴ(Abyssalmaehv)=救済(死) / 10 ジリックス(Vetruvianzirix)=管理 /
11 サージ(Vetruviansajj)=諦め / 12 サイオン(Vetruvianscion)=運命 /
13-18 メカゾ0ア(翼/剣/完全体/兜/胴/砲)=機械神・規格化への抵抗を6体で分割した連戦[テーマ確定可] /
19 ハイドラックス(neutral_hydrax)=増殖・制御不能[同] / 20 アルカナ(Arcana)=希望(主人公の答え)。

## 主人公対応表（heroId / 核となる価値観）
HeroAldin アルディン=守れなかった名を抱えたまま守り続ける /
HeroKagachi カガチ=弱いまま・転んだ姿ごと前へ進む(ch8で犬化) /
HeroVesna ヴェスナ=居場所は作れる、冷たい場所にも火を置けば /
HeroZiran ジラーン=苦しみごと生きる命を支える癒し手 /
HeroReva レヴァ=残って戦うのも自分で選んだ自由 /
HeroKara カーラ=すぐ変われなくていい、待つ・支えるのも守ること /
HeroBrome ブローム=違う者同士でも一つになれる(軍旗) /
HeroShidai シダイ=光を支える影にも名がある(カガチの相棒) /
HeroIlena イレーナ=同じ悲劇を観ても希望を選ぶ(観測者側の知性)。

## 書く対象（この優先順で段階納品OK）
1. 【最優先】撃破後の短い会話: ボスに勝った直後の余韻。**2〜4行**。まず ch1-12 の12ボス × 9主人公。
2. 戦前ダイアログ・残り6主人公(Ziran/Reva/Kara/Brome/Shidai/Ilena) × 全ボス。**6〜9行**。
3. 戦前ダイアログ・新ボス8体(rook/sister/mechaz0r×6/hydrax) × 既存3主人公(Aldin/Kagachi/Vesna)。**6〜9行**。
4. 中ボス戦前(主人公共通): 下記中ボス × 共通1本。**3〜5行**。固有名は避ける。
   中ボス: Silitharelder, Makantorwarbeast, Veteransilithar, Gloomchaser, Abyssalcrawler,
   Rae, Starfirescarab, Pax, Pyromancer, neutral_beastmaster, neutral_gnasher,
   neutral_rawr, neutral_rok, neutral_zukong。

## 出力フォーマット（厳守）
- 行は **偶数番(1,3,5…で表記する“奇数ラベル”ではなく)** ボスで開始し、ボス→主人公→ボス…と**交互**。
- 必ず「(ボス)」「(主人公)」のラベルを付け、ボスで始め交互に並べる。3行に縛らず指定行数で。
- 1組=1ブロック。ブロック見出しは下記の通り。bossId/heroIdは上表の英字表記そのまま。

戦前:
```
[prefight boss=Caliber hero=HeroAldin]
1(ボス): …
2(主人公): …
3(ボス): …
4(主人公): …
5(ボス): …
6(主人公): …
```
撃破後:
```
[postboss boss=Caliber hero=HeroAldin]
1(ボス): …
2(主人公): …
3(ボス): …
```
中ボス（主人公共通）:
```
[midboss boss=Silitharelder]
1(ボス): …
2(主人公): …
3(ボス): …
```

- 日本語のみ（英語不要）。世界観・因縁・各キャラのペルソナが伝わる密度で。
- まず「①撃破後 ch1-12（108組）」だけを完成させて出力してください。続きは次の指示で依頼します。

===========================================================================

## 受け取り後（Cowork側の作業メモ）
- ① `[prefight ...]` → `HeroBossDialogueUI.BuildScriptedLines()` の `scriptedLines["boss|hero"]`（可変長配列）。
- ③ `[postboss ...]` → `HeroBossDialogueUI.BuildPostBossLines()` の `postBossLines["boss|hero"]`。
- ② `[midboss ...]` → **新設する `midBossLines["boss"]` テーブル**（Coworkが追加し、汎用フォールバックより優先で参照）。
- すべて小文字キーに正規化して登録。JA確定稿。コンパイル0を確認し `--no-verify` でコミット。
