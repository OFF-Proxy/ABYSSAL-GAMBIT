# R1-score: スコアシステムの定式化 & ベスト保存 & ライブ表示
> **状態: ✅ 実装済み（2026-05-30, レビュー承認）** — スコア保存・加点ポップアップ・リザルト併記まで稼働。

> 設計: Claude / 実装: Codex・Claude Code / 2026-05-29
> 依存: **R1-persist（ベスト保存に必要）** / 関連: ROADMAP E4, [RELEASE_PLAN.md](RELEASE_PLAN.md)

## ゴール
既に動いているスコア計算（`QueueStageResult`）を**正式な仕様として明文化**し、(1) 章ごとのベストスコア/タイムを永続保存、(2) 加点の瞬間をプレイ中にポップアップ表示、(3) リザルトでベスト/自己新を表示する。

## 現状の把握（実装済み）
`GameManager.QueueStageResult(stageNumber, isChapterClear)` が既に以下を計算済み:

| 加点要素 | 現在の式 |
|---|---|
| 通常戦突破 | `combatClears × 100` |
| 中ボス撃破 | `midBossClears × 300` |
| 章ボス撃破 | `bossClears × 1000` |
| スターアップ | `star2 × 30 + star3 × 100` |
| スピードボーナス | `max(0, round((refTime - elapsed) × 5))` |
| 全体倍率 | 上記合計 × `ScoreMultiplier`（オーグメント由来、実装済み） |

→ **計算ロジックは流用**。本タスクは「明文化＋保存＋可視化」が主眼で、式の微調整は R3-balance で行う。

## 影響範囲
- 新規ファイル: `Assets/Scripts/UI/ScorePopupUI.cs` — 加点ポップアップ（"+300 中ボス撃破!" 等）
- 変更ファイル:
  - `GameManager.cs` — 加点発生箇所で `ScorePopupUI` を呼ぶ / 章クリア時に `SaveManager.RecordChapterResult`
  - `ResultPanelUI.cs` — `ShowStageResult` にベスト/自己新表示を追加（引数 or SaveManager 参照）
- 既存依存（壊さないこと）: `QueueStageResult` / `BuildStageBreakdown` / `BuildChapterBreakdown` / `ResultPanelUI.ShowStageResult` / `ScoreMultiplier`。

## 振る舞いの仕様

### A. 加点要素の正式仕様（明文化）
現行式を**正**とする。将来の調整は本表を更新してから実装する（テキストと実装のズレ防止）。
- ノーデス/アイテム不使用突破（ROADMAP E4 に記載あるが現状未計上）は **将来拡張**としてマーク。MVP は現行5要素＋倍率のみ。

### B. ベスト保存
- 章クリア時（`QueueStageResult` の `isChapterClear` 分岐、`chapterTotalScore`/`chapterTotalTime` 確定後）に:
  ```csharp
  SaveManager.Instance.RecordChapterResult(currentChapter, chapterTotalScore, chapterTotalTime, cleared:true);
  ```
- `RecordChapterResult` 内でベスト更新判定（高スコア優先、同点ならタイム短い方）。**自己新かどうかのフラグを返す**ようにし、リザルト表示に渡す（C で使用）。

### C. リザルト表示（ResultPanelUI 拡張）
- `ShowStageResult(...)` に「今回スコア」に加え **「ベストスコア」「NEW RECORD!」表示**を追加。
- 実装案: `ShowStageResult` の引数に `int bestScore, bool isNewRecord` を足す（呼び出しは GameManager 1 箇所なので影響小）。または ResultPanelUI 内で `SaveManager` を参照。**引数追加を推奨**（UI が SaveManager に依存しない方が疎結合）。

### D. ライブ加点ポップアップ（ScorePopupUI）
- 加点が発生した瞬間（敵撃破・スターアップ・スピードボーナス確定時）に、画面に小さく `+N 理由` をフロート表示してフェードアウト。
- MVP は **戦闘クリア/中ボス/章ボス撃破の3トリガー**で十分（スターアップは頻度が高いので任意）。
- DOTween は既に使用実績あり（RoundProgressUI）。同様にフロート＋フェードを実装。
- 演出過多を避け、0.8〜1.2 秒で消える程度に。

## 受け入れ基準
- [ ] 章をクリアすると、その章のベストスコア/タイムが保存され、再挑戦で前回より高いと「NEW RECORD!」が出る（R1-persist 連携）
- [ ] 章ボス撃破時に `+1000` 等の加点ポップアップが表示される
- [ ] リザルト画面に「今回スコア」と「ベスト」が併記される
- [ ] 既存のステージ/章リザルトの内訳テキスト（`BuildStageBreakdown`/`BuildChapterBreakdown`）が従来どおり出る
- [ ] `Compilation completed (Errors: False)`

## 実装ヒント
- 加点トリガーは既に `stageScoreCombatClears++` 等を増やしている箇所があるはず（`QueueStageResult` が参照するカウンタの increment 地点）。そこに `ScorePopupUI.Show("+100", reason)` を併置するのが最小変更。
- `GetStageReferenceTime(stageNumber)` が speedBonus の基準時間。R3-balance での調整対象。

## 未決事項（Codex への質問）
- ランキング表示（章ごと自己ベスト一覧）は R1-rank で別 UI とする。本タスクはリザルト内のベスト併記まで。
- ノーデス/ノーアイテム加点を MVP に入れるか（現行未計上）。→ R3-balance 時に判断を推奨。

---

## Review (2026-05-29, Claude) — ✅ 承認

実装コミット `7a46d430 feat(R1-score)`。

**良い点**
- `TrackStageProgress` のクリア種別カウント地点に `ScorePopupUI.Show(100/300/1000, 理由)` を併置 → 加点トリガー3種（戦闘/中ボス/章ボス）を JA-EN・色分けで表示。受け入れ基準②OK。
- `ScorePopupUI` はスタック表示（連続加点が重ならない `nextSlotOffset`）・`SetUpdate(true)`（ポーズ中でも動く）・自動 Destroy と丁寧。既存 DOTween 慣習踏襲。
- 章クリア時に `RecordChapterResult` でベスト永続化、戻り値の `isNewRecord` を `pendingResultBestScore`/リザルトへ伝搬。`previousBest` を**更新前に**取得して `Mathf.Max` 表示 → 正しい。
- `ResultPanelUI.ShowStageResult` に `bestScore`/`isNewRecord` をオプション引数で追加（既存呼び出し互換）。章クリア時のみベスト行と「★ NEW RECORD!」表示。受け入れ基準①③OK。

**気づき（軽微・将来）**
- ポップアップの数値は素の +100/300/1000 で、最終集計の `ScoreMultiplier`・speedBonus は反映されない（ポップアップは演出、リザルトが正値）。意図的でOK。気になるなら将来「×倍率」を末尾表示。
- スターアップ加点のポップアップは未実装（設計で任意とした通り）。
- ノーデス/ノーアイテム加点は未計上（現行式どおり）。R3-balance で判断。

**受け入れ基準**: ①〜④ 充足（コードレビュー上）。実機で「章クリア→NEW RECORD→再挑戦で更新」を1度確認すると完全。
