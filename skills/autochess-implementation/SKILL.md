---
name: autochess-implementation
description: >-
  AutoChessBossRush（Unity 2D ローグライク・オートチェス）の実装規約プレイブック。
  このリポジトリで C# を編集する／ユニット・スキル・シナジー・オーグメント・章を追加する／
  バグを直す／設計書(DESIGN_*.md)を書く／Codex のコミットをレビューする — そうした実装作業を
  「毎回同じ流儀・同じ品質」で進めてブレ（drift）を防ぐために必ず参照する。
  「ユニットのスキルを変えたい」「新しいユニット/章/オーグメントを足したい」「このバグを直して」
  「DESIGN書いて」「Codexの実装をレビューして」等、AutoChessBossRush の中身に触れる依頼では
  明示的に名前が出なくてもこのスキルを使うこと。docs/COLLAB_PROTOCOL.md と ROADMAP.md の運用を体現する。
---

# AutoChessBossRush 実装プレイブック

このスキルは、AutoChessBossRush の実装作業を**一貫した流儀**で進めるための共有ルール。
目的は「実装のブレを無くす」こと。新しい人（または別セッションの Claude / Codex）が触っても、
同じ場所・同じ命名・同じ手順・同じ検証になるようにする。

## 0. まず役割を確認する（最重要）

このプロジェクトは **設計と実装を分離**している（`docs/COLLAB_PROTOCOL.md`）。

- **設計担当（Claude）**: `docs/DESIGN_<task-id>.md` を書く。原則コードは編集しない（例外: docs／コメント／TODOマーカー）。
- **実装担当（Codex / Claude Code）**: 設計書に従ってコード・シーン・プレハブを編集し、**コンパイルが通ってから**コミットする。

作業を始める前に「自分は今どちらの役割か」を確認する。設計を頼まれたら DESIGN を書く。実装を頼まれたら
対応する DESIGN があるか確認し、無ければ仕様の曖昧点を `docs/QUESTIONS.md` に積むか、設計から始める。

## 1. 生きたドキュメントを常に同期する

実装・設計のたびに、関連する「生きた docs」を更新する。これがブレ防止の本体。

- `docs/ROADMAP.md` — 進行メモに1行追記（日付＋何をしたか）。全体の真実のソース。
- `docs/CLAUDE_HANDOFF.md` — 大きな変更サマリ（差分・新規ファイル・主要API）。
- `docs/QUESTIONS.md` — 実装中に出た設計上の疑問（Codex→Claude のキュー）。
- `docs/DESIGN_<task-id>.md` — レビュー時は末尾に `## Review (YYYY-MM-DD)` 節を追記。

> 迷ったら「次に触る人が同じ判断にたどり着けるか？」を基準にメモを残す。

## 2. プロジェクト地図（どこに何があるか）

詳細は `references/codebase-map.md` を読む。要点だけ:

- ゲームロジックの中枢: `Assets/Scripts/GameManager.cs`（章/ラウンド、経済、オーグメント、スコア、セーブ連携、ボス報酬/仲間化）。
- ユニット挙動: `Assets/Scripts/BaseEntity.cs`（移動・攻撃・被ダメ・**スキル**・スター・描画順）。
- スキル説明文: `Assets/Scripts/UnitStatusPanelUI.cs`（実数値の自動生成）。
- シナジー: `Assets/Scripts/SynergyType.cs` / `SynergyManager.cs`。
- オーグメント: `Assets/Scripts/AugmentCatalog.cs`（データ80種）＋ GameManager の適用フック。
- 永続化: `Assets/Scripts/Save/`（`ISaveStore` / `LocalJsonSaveStore` / `SaveManager` / `SaveData`）。
- データ資産: `Assets/Resources/Entity Database.asset`（全ユニット定義）、`Assets/Prefabs/Unit/`、`Assets/Animations/`。
- ユニット画像: `Assets/Images/Units/Sprite/...`（duelyst スプライトを plist でスライスして使用）。

## 3. コーディング規約（既存スタイルを踏襲する）

新規コードは周囲のコードと見分けがつかないように書く。具体的には:

- **コメントは日本語**で、1〜2行の意図説明を要所に置く（既存ファイルの密度に合わせる）。
- **UI シングルトンは `EnsureExists()` パターン**（例: `OptionsPanelUI` / `AugmentHudUI` / `ScorePopupUI` / `ChapterRosterUI`）。
  `Instance` 静的プロパティ＋ `FindObjectOfType` フォールバック＋必要なら `DontDestroyOnLoad`。
- **多言語は必ず JA/EN 両方**。`LocalizationManager.IsJapanese` で分岐し、テキスト生成後に
  `LocalizationManager.ApplyFont(text)` を呼ぶ。ハードコードの片言語文字列を残さない。
- **アニメーション/トゥイーンは DOTween**（`seq.SetUpdate(true)` でポーズ中も動かす等、既存に倣う）。
- **スキル説明は実数値**（E5 方針）。表示テキストと実際の計算式がズレないよう、計算に使うのと同じ
  ヘルパ（`CalculateAreaSkillDamage` 等）から数値を出す。定性文（「大ダメージ」等）の新規追加は避ける。
- **永続化は `JsonUtility` + `List<T>`**（`Dictionary` は不可。`[System.Serializable]` DTO を使う）。
  保存先は `Application.persistentDataPath`。原子的書き込み・破損退避は `LocalJsonSaveStore` に倣う。
- 名前空間: 永続化は `AutoChessBossRush.Save`。新サブシステムも必要なら名前空間を切る。

## 4. よくある変更のレシピ（references/）

定型作業は必ず対応するレシピを読んでから着手する（手順・触る場所・検証が固定化されている）:

- **ユニットのスキルを追加/変更する** → `references/add-unit-skill.md`
  （`BaseEntity.TryExecuteDedicatedSkill` への追加 / 汎用フォールバック `IsXxxSkillUnit` からの除外 /
  `UnitStatusPanelUI` の実数値テキスト / 数値ヘルパの使い方。被りのない固有スキルにする。）
- **設計書を書く** → `references/design-doc-format.md`（DESIGN_<task-id>.md の必須項目テンプレ）

> 新しい定型作業（新ユニット追加、新オーグメント追加、新章追加 等）が出てきたら、
> その手順を `references/` に新レシピとして書き起こし、本ファイル §4 にリンクを足す。これがブレ防止の積み立て。

## 5. 検証（コミット前に必ず）

- **コンパイル**: 実装後 `Compilation completed (Errors: False)` を確認してからコミット。
- **手動確認**: 可能なら該当機能を1度動かす（例: スキルなら戦闘で発動するか、セーブなら再起動で残るか）。
- **デグレ確認**: 既存の挙動・テキストを壊していないか（特に共有ヘルパや switch に手を入れた時）。
- **JA/EN 両方**でテキスト欠落・はみ出しが無いか。

## 6. コミット規約（COLLAB_PROTOCOL §2.3）

```
<task-id>: <一行サマリ>

- 変更点1
- 変更点2

Tested: Compilation OK / Manual run: <概要>
Refs: docs/DESIGN_<task-id>.md
```

## 7. このスキル自体の更新

実装中に「次もやりそうな定型」「ハマりどころ」「新しい規約」に気づいたら、
本 SKILL.md か `references/` に追記する。スキルは育てるもの。1回ごとの学びを次の実装に効かせる。
