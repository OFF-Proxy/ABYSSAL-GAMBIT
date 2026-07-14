# R1-saveslots: セーブデータスロット & 削除機能

> 設計＆実装: Claude (Cowork+Unity MCP) / 2026-05-31
> 状態: ✅ 実装済み（3スロット・タイトルPLAY→スロット選択・削除確認付き）。残: 実機検証。
> 依存: [DESIGN_R1-persist.md](DESIGN_R1-persist.md)（保存層） / 関連: LobbyUI

## ゴール

セーブを**3スロット**に分け、タイトルの PLAY からスロットを選んでプレイ。各スロットは**独立した進捗**（章クリア/ベスト/ボス仲間）を持ち、**削除**できる。

## 影響範囲

- **変更ファイル**:
  - `Assets/Scripts/Save/ISaveStore.cs` — `Delete()` 追加。
  - `Assets/Scripts/Save/LocalJsonSaveStore.cs` — ファイル名指定コンストラクタ＋`Delete()`。
  - `Assets/Scripts/Save/SaveManager.cs` — スロット管理（`SlotCount`/`ActiveSlot`/`SetActiveSlot`/`DeleteSlot`/`GetSlotInfo`）＋旧 save.json→slot0 移行。
  - `Assets/Scripts/LobbyUI.cs` — PLAY→スロット選択ビュー（3枚＋削除確認）。
- **既存依存API（壊さないこと）**: `SaveManager.EnsureExists/Save/Data/GetChapter/RecordChapterResult/BossAllies/HasBossAlly/AddBossAlly` は不変。`LocalJsonSaveStore()` 既存コンストラクタも維持。

## データ構造 / 仕様

- スロットファイル: `Application.persistentDataPath/save_{0,1,2}.json`（破損退避は `<file>.corrupt`）。
- アクティブスロットは `PlayerPrefs("save.activeSlot")` に記憶（アプリ再起動でも復元）。既定 0。
- **移行**: 旧 `save.json` が在り `save_0.json` が無ければ一度だけコピー（既存進捗をスロット0へ継承）。
- `SaveManager.SlotInfo`（UI表示用要約）: `exists / highestClearedChapter / bestScore / bossAllyCount / lastSavedUnixSec`。`GetSlotInfo(slot)` はアクティブを変えずに読み出す。
- `SetActiveSlot(slot)`: そのスロットのストアへ切替＆ロード＆PlayerPrefs保存。
- `DeleteSlot(slot)`: ファイル削除。アクティブスロットなら `Data` を空に。

## 振る舞い（UI）

- タイトル **PLAY → スロット選択ビュー**（`LobbyUI.ShowSlotSelect`）。3枚のスロットカードを横並び表示。
  - 各カード: 「スロット N」（アクティブに ★）＋要約（空き / 到達章・ベスト・仲間数・保存日時）。
  - カードクリック → `SetActiveSlot` → ロビー（章選択）へ。
  - 「削除」ボタン（データ有時のみ）→ 確認オーバーレイ（削除する / キャンセル）→ 削除後にカード再描画。
  - 戻るボタン → タイトル。
- JA/EN 両対応。

## 受け入れ基準

- [ ] PLAYで3スロットが表示され、空きスロットは「空き」、データ有スロットは到達章/ベスト/仲間/保存日時を表示。
- [ ] スロットを選ぶとそのデータでロビー→章開始でき、進捗はそのスロットに保存される。
- [ ] 別スロットを選ぶと別の進捗になる（独立）。アプリ再起動で最後に選んだスロットが既定。
- [ ] 削除→確認→実行でそのスロットが空きに戻る（他スロットは不変）。
- [ ] 旧 save.json の進捗がスロット1（slot0）に引き継がれている。
- [ ] Compilation OK（CSエラー0）。

## 未決 / 後続

- 「上書き確認（既存スロットを新規開始で潰す）」は現状なし（章選択で既存の続きから始まる設計のため不要）。必要なら追加。
- スロットのサムネ/章アートや並べ替えは任意のポリッシュ（R3）。

## Review (YYYY-MM-DD) — 未実施（実機確認後）
