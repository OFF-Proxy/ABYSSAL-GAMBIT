# Claude / Codex 協業プロトコル

> 役割: **Claude = 設計担当 / Codex = 実装担当**
> 共有資料: [CLAUDE_HANDOFF.md](CLAUDE_HANDOFF.md), [ROADMAP.md](ROADMAP.md)

---

## 1. それぞれの責任範囲

### Claude（設計）
- ロードマップ更新（`ROADMAP.md`）
- 各タスクの**設計提案**（影響範囲・データ構造・依存・受け入れ基準を明記）
- 既存システムの**詳細解説**（Codex が読むためのコメント挿入提案）
- バグ報告の**根本原因分析**と修正方針
- リファクタリングの方針提示

> **原則**：Claude はファイル編集を行わず、設計提案と決定事項を `docs/` 配下の Markdown としてコミットする。
> （例外：本書のような meta ドキュメント／コメント追記／TODOマーカー設置）

### Codex（実装）
- Claude の設計に従ってコード／シーン／プレハブを編集
- `Compilation completed (Errors: False)` を確認してからコミット
- 受け入れ基準のテストを実行（あれば）
- 実装中に**設計上の疑問**が出たら、コメントを `docs/QUESTIONS.md` に追記（Claude が次回回答）
- 既存スタイル（コメント・命名・1行コメント密度）を踏襲

> **原則**：Codex は設計判断を独断で行わない。仕様が曖昧なら `QUESTIONS.md` に書く。

---

## 2. タスクのライフサイクル

```
       ┌──────────────────────────┐
       │  ROADMAP.md に未着手タスク │
       └────────────┬─────────────┘
                    │
       ┌────────────▼─────────────┐
       │  Claude: 設計 → DESIGN_*.md │
       └────────────┬─────────────┘
                    │
       ┌────────────▼─────────────┐
       │  Codex: 実装 → コミット    │
       └────────────┬─────────────┘
                    │
       ┌────────────▼─────────────┐
       │  Claude: レビュー & 次設計  │
       └──────────────────────────┘
```

### 2.1 タスク選定
- Claude が `ROADMAP.md` のチェックリスト（CLAUDE_HANDOFF §5 と同期）から次の項目を選び、`docs/DESIGN_<task-id>.md` を作成。
- 例: `docs/DESIGN_C1_score_system.md` — C-1 スコアシステム定式化

### 2.2 設計フォーマット（Claude が書く）
```markdown
# <task-id>: <タイトル>

## ゴール
（1〜2文。何が達成されたら完了か）

## 影響範囲
- 新規ファイル: ...
- 変更ファイル: ...（行数感）
- 既存依存API（壊さないこと）: ...

## データ構造
（必要なら C# クラス/構造体の skeleton を疑似コードで）

## 振る舞いの仕様
（具体的なフローと、エッジケース）

## 受け入れ基準
- [ ] テストケース1
- [ ] テストケース2
- [ ] Unity でこう操作したら、こう見える

## 実装ヒント
- 既存の <Helper名> を再利用できる
- <gotcha> に注意

## 未決事項（Codex への質問）
- なし／（あれば列挙）
```

### 2.3 実装フォーマット（Codex が書く）
コミットメッセージ：
```
<task-id>: <一行サマリ>

- 変更点1
- 変更点2

Tested: Compilation OK / Manual run: <概要>
Refs: docs/DESIGN_<task-id>.md
```

### 2.4 レビュー → 次タスク
- Claude は Codex のコミットを diff で確認し、`DESIGN_*.md` の末尾に `## Review (YYYY-MM-DD)` 節を追記。
- 受け入れ基準を満たしていれば `ROADMAP.md` のチェックを埋めて次タスクへ。

---

## 3. ファイルレイアウト

```
docs/
├── ROADMAP.md                  # 生きた進行メモ（両者編集可）
├── CLAUDE_HANDOFF.md           # Claude → Codex の引き継ぎサマリ
├── COLLAB_PROTOCOL.md          # 本書
├── QUESTIONS.md                # Codex → Claude の質問キュー
├── DESIGN_A1_*.md              # 設計書ごとに1ファイル
├── DESIGN_B1_*.md
└── DESIGN_C1_*.md
```

---

## 4. コミット規約

| 種別 | プレフィックス | 例 |
|---|---|---|
| 設計（Claude） | `design:` | `design: C-1 score system spec` |
| 実装（Codex） | `feat:` / `fix:` / `refactor:` | `feat: C-1 add ScoreEntry data model` |
| ドキュメント | `docs:` | `docs: update CLAUDE_HANDOFF` |
| アセット | `asset:` | `asset: import boss VFX from reference` |

ブランチ運用：
- 小タスクは `main` 直コミット OK（プロトタイプ段階）
- 大規模タスクは `feature/<task-id>` で PR 化

---

## 5. 共通ルール

1. **reference/ を直接参照しない**（必ず Assets/ にコピーしてから利用）
2. `HasAugment("id")` を介した分岐に文字列を直書きする時は、`AugmentCatalog.FindById` で存在確認
3. シーンや prefab を編集したら `CLAUDE_HANDOFF.md §1` の修正アセット一覧を更新
4. 既知バグや gotcha を見つけたら `CLAUDE_HANDOFF.md §4` か `§7` に追記
5. 日本語コミットメッセージ・コード内コメント可（既存スタイル）

---

## 6. 通信エチケット

- **Claude**：設計書を出す時、Codex が読まなくても実装できるレベルまで噛み砕く（コードスケルトン、シグネチャ、想定エラーケース）。
- **Codex**：仕様確認が必要な時、推測で実装せず `QUESTIONS.md` に書いて Claude の判断を待つ。
- **両者**：相手のコミットを diff で読んでから自分のターンを始める。

---

最終更新: 2026-05-29 (Claude)
