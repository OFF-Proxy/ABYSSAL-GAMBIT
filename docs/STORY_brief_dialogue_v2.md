# GPT向けブリーフ：ボス戦前ダイアログの長尺化＋中ボス個別化（v2）

2026-06-13 Cowork。実機レビューで「会話が短すぎる／中ボスのセリフが全部同じ」との指摘。
システム側を拡張済み。本文（プロセ）はGPTに執筆を依頼する。

## システム仕様（実装済み・Coworkが流し込む）
- ボス戦前ダイアログは `HeroBossDialogueUI` の台本テーブル `scriptedLines["<bossId>|<heroId>"]`（小文字キー）。
- **3行固定をやめ、可変行数に対応**。配列の偶数index＝ボスのセリフ、奇数index＝主人公のセリフ（交互、ボスで開始）。
  - 例：5行なら [ボス, 主人公, ボス, 主人公, ボス]。7行・9行も可。
- 台本が無い組合せは汎用フォールバック（ボスIDで安定ランダムに振り分け＝同一文回避済み）。
- 立ち絵は味方＝左／敵＝右、画面いっぱい表示。台本側で位置指定は不要。

## 依頼1：章ボス×主人公 台本の長尺化
- 対象：13章ボス × 9主人公（既存39組＝基本3主人公は3行で実装済み。これを **5〜9行へ拡張**）。
- 1組の目安：**6〜9行**（ボス開始の交互）。世界観・キャラのペルソナ・因縁が伝わる密度に。
- 口調・固有設定は「確定設定v1」準拠（アルカナ＝ですます＋いたわり／キャリバー＝兄弟子・白門・リオラ 等）。
- 出力書式（そのままC#へ移せるよう、boss/heroのIDと交互セリフを明記）：
```
[boss=Caliber hero=HeroAldin]
1(ボス): …
2(主人公): …
3(ボス): …
4(主人公): …
5(ボス): …
（必要なら7,9行まで）
```
- bossId一覧：Caliber, Solfist, Dissonance, Magmarvaath, Magmarstarhorn, Magmarragnora,
  Abyssallilithe, Abyssalcassyva, Abyssalmaehv, Vetruvianzirix, Vetruviansajj, Vetruvianscion, Arcana。
- heroId一覧：HeroAldin, HeroKagachi, HeroVesna, HeroZiran, HeroReva, HeroKara, HeroBrome, HeroShidai, HeroIlena。

## 依頼2：中ボス用の個別台本
- 中ボスは各章の `RecruitCandidateIds`（仲間化候補＝既存ボス級ユニット）。同一文回避のため**ボス単位で短い掛け合い（3〜5行）**が欲しい。
- 主人公別まで作らず、まず **bossId単位（主人公共通）** の3〜5行で可（システムは "<bossId>|*" 風の共通キーにも拡張予定）。
- 中ボスIDの正確な一覧は `docs/STORY_brief_wavemap.md`（各章のウェーブ構成）を参照。未整備なら Cowork が章別に抽出して追記する。

## 受け取り後（Cowork）
- 上記書式の本文を `HeroBossDialogueUI.BuildScriptedLines()` に流し込み（可変長配列）。
- 中ボス共通台本は専用テーブルを追加して、台本が無い時のフォールバックより優先。
- JA確定稿。EN未対応欄は従来の汎用英語へフォールバック。
