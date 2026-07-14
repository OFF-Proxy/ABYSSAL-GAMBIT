# CODEX Prompt: Chapter01 Full Script

以下を別セッションのCodexに渡して、Chapter01「白門の後悔」の実装用台本を作成してください。

```text
あなたは `C:\Users\offof\AutoChessBossRush` のストーリー台本担当です。
Chapter01「白門の後悔」の実装用フル台本を作成してください。

まず次の資料を読んでください。

- `docs/Story/16_Chapter01_Script_Spec.md`
- `docs/Story/06_Chapter_Design/Chapter01.md`
- `docs/Story/07_Dialogue_Guide.md`
- `docs/Story/03_Character_Bible.md`
- `docs/Story/05_Bosses/Caliber.md`
- `docs/Story/04_Heroes/Aldin.md`
- `docs/Story/04_Heroes/Kagachi.md`
- `docs/Story/04_Heroes/Vesna.md`
- `docs/Story/09_Event_Flags.md`
- `docs/Story/15_Hero_Awakening_Design.md`

目的:
Chapter01を、プレイヤーが最初の30分で本作の魅力に気付ける台本にする。
重い世界観説明ではなく、次の体験を作る。

1. 白門跡と終幕の眠りの異常さが分かる。
2. アルディン、カガチ、ヴェスナの魅力が最初から見える。
3. キャリバーが悪人ではなく、後悔で壊れた英雄として見える。
4. ボス撃破後、`Caliber` がすぐ戦術ユニットとして仮加入する。
5. ただしキャリバーは完全に改心せず、Chapter14の正式共闘へ因縁を残す。

最新採用方針:

- Chapter01クリア時に `Caliber` はユニットとして使用可能にする。
- 物語上は正式な仲間ではなく「剣片契約」。
- Chapter01再クリアでキャリバーは強化される。
- Chapter14でアルディン覚醒と連動し、正式共闘 / 誓剣解放へ発展する。
- Chapter01ではアルカナの名前を出さない。
- 「白い声」「優しい声」「誰かに観られていた感覚」までに留める。

出力先:
`docs/Story/06_Chapter_Design/Chapter01_FullScript_Draft.md`

出力フォーマット:
ラベル付きの実装投入しやすい台本形式で書く。

必須セクション:

1. `[interlude id=CH01_OPENING]`
   - 20〜35行。
   - 鐘が鳴らない白門跡。
   - 眠っている人々。
   - アルディンが避難者を守る。
   - カガチが強がって先に出る。
   - ヴェスナが凍えた場所に火を置く。
   - キャリバーが遠くから「白門を閉じろ」と叫ぶ。

2. `[node id=CH01_NODE_01]`
   - 最初の戦闘前の短い導入。

3. `[echo id=CH01_CALIBER_ECHO_01]` から `[echo id=CH01_CALIBER_ECHO_03]`
   - 章中に3回入るキャリバー遠隔台詞。
   - キャリバーの思想が徐々に分かるようにする。

4. `[midboss id=CH01_MID_01 ...]` から `[midboss id=CH01_MID_06 ...]`
   - 4〜6体分。
   - 各3行。
   - キャリバーの思想を予告する前座。
   - 中ボスはキャリバーより目立たせない。
   - unitIdは既存の中立候補を使う:
     `neutral_beastmaster`, `neutral_gnasher`, `neutral_rawr`, `neutral_rok`, `neutral_zukong`, `neutral_gnasher_ice`, `neutral_rok_steelblue` など。

5. `[prefight boss=Caliber hero=<HeroId>]`
   - 18主人公分を作る。
   - 各3行。
   - 形式はボス、主人公、ボス。
   - 対象HeroId:
     `HeroAldin`, `HeroKagachi`, `HeroVesna`, `HeroZiran`, `HeroReva`, `HeroKara`, `HeroBrome`, `HeroShidai`, `HeroIlena`,
     `Magmarvaath`, `Magmarstarhorn`, `Magmarragnora`,
     `Abyssallilithe`, `Abyssalcassyva`, `Abyssalmaehv`,
     `Vetruvianzirix`, `Vetruviansajj`, `Vetruvianscion`

6. `[postboss boss=Caliber hero=<HeroId>]`
   - 18主人公分を作る。
   - 各3〜5行。
   - キャリバーは主人公の答えを完全な正解とは認めない。
   - ただし「その道が折れるか見届ける」と剣片を預ける。

7. `[observation id=OBS_CH01]`
   - 観測片「白門の慈悲」を入れる。
   - 本文は `16_Chapter01_Script_Spec.md` の正本を大きく変えない。

8. `[reward id=CH01_REWARD_CALIBER]`
   - 以下の要素を必ず含める。
   - `SYSTEM: キャリバーが戦術ユニットとして仮加入しました。`
   - `SYSTEM: キャリバーの剣片を獲得。`
   - `SYSTEM: 観測片「白門の慈悲」を記録しました。`
   - 再クリアでキャリバーを強化できることをUI文として短く示す。

9. `[next id=CH01_TO_CH02]`
   - Chapter02の「停足の番人オルム」へつながる予兆。
   - 進むことを止める次の門の気配。

文体ルール:

- 日本語で書く。
- キャリバーは厳粛。軽口は禁止。
- アルディンは静かで重い。怒るほど声量を落とす。
- カガチは荒く短いが、単なる乱暴者にしない。
- ヴェスナは火と氷の比喩を使うが、攻撃的にしすぎない。
- アルカナ名は禁止。
- 説明しすぎない。設定は台詞と演出に溶かす。
- ボス戦前は原則3行。
- 撃破後も改心させすぎない。

必須ニュアンス:

- キャリバーは白門とリオラの後悔を抱えている。
- アルディン選択時は兄弟弟子の因縁を強く出す。
- 初回の章開始では、選択主人公だけでなくアルディン、カガチ、ヴェスナの三人の魅力も見せる。
- `Caliber` はChapter01で使えるようにする。
- ただし正式共闘はChapter14まで残す。

禁止:

- Chapter01でアルカナの名前を出す。
- キャリバーを完全に洗脳された被害者だけにする。
- キャリバーを完全に改心させる。
- Chapter14での誓剣解放をChapter01で直接説明する。
- 長い設定説明をナレーションで続ける。

最後に、台本末尾に短いQAチェックリストを付けてください。
```
