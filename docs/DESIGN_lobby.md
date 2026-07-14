# lobby: タイトル兼ロビー画面（章選択 / ボス仲間 / オプション）
> **状態: ✅ 実装済み（2026-05-31, 一次実装＋#1#2結線）** — 残: showLobbyOnBoot の ON 化と実機（ロビー導線・アート見た目）検証。

> 設計＆一次実装: Claude (Cowork) / 2026-05-31 / 依存: R1-persist（SaveManager）, R1-meta（BossAllies）, chapter2
> 関連: [DESIGN_chapter2.md](DESIGN_chapter2.md), [DESIGN_handoff_claudecode.md](DESIGN_handoff_claudecode.md)
> `LobbyUI.cs` は実装済み（プログラム生成・Compilation OK）。**起動フローへの本結線とエディタ検証は Claude Code へ引き継ぎ**（下記）。

## ゴール
専用シーン/プレハブ無しで、起動時に出せるロビー（タイトル＋章選択＋ボス仲間一覧＋オプション/終了）を用意する。章を選ぶとその章で新しいランを開始する。

## 影響範囲
- 新規ファイル: `Assets/Scripts/LobbyUI.cs`（`ChapterRosterUI` と同じプログラム生成・`EnsureExists` 流儀。Canvas/Overlay を自前生成、sortingOrder=60050）。
- 変更ファイル: `Assets/Scripts/GameManager.cs`
  - `public static int PendingStartChapter`（ロビー→シーン再読込で開始章を受け渡し。0=従来起動）。
  - `public bool showLobbyOnBoot = false`（**既定OFF**。ONで起動時に `LobbyUI.ShowAsBootLobby()`）。
  - `Start()` 冒頭: `PendingStartChapter>0` なら `currentChapter` をそれにして消費。末尾: `showLobbyOnBoot && !startedFromLobbySelection` でロビー表示。
  - `public void RequestStartChapter(int)`: `PendingStartChapter` を設定し `Time.timeScale=1` でアクティブシーンを `SceneManager.LoadScene` 再読込（OptionsPanelUI の再挑戦と同方式）。
- 既存依存API（壊さないこと）: `OptionsPanelUI.EnsureExists().Show()`、`SaveManager.IsChapterUnlocked/GetChapter/BossAllies`、`GameManager.entitiesDatabase`、`AttackEffectPlayer.PlayUiSfx`。

## 振る舞いの仕様
- 章ボタン: 1..`ChapterCount`(=2)。`SaveManager.IsChapterUnlocked(n)`（前章クリアで解放）で活性/非活性。クリア済はベストスコア併記。押下で `GameManager.RequestStartChapter(n)`。
- ボス仲間: `SaveManager.BossAllies` を `entitiesDatabase` で EntityData 解決し、アイコンを横並び表示（未取得なら「まだいません」）。
- オプション=`OptionsPanelUI.Show()` / 終了=`Application.Quit()`(+Editorで停止)。
- `ShowAsBootLobby()` は `Time.timeScale=0` で一時停止して表示。章選択（=`RequestStartChapter`）が `timeScale=1`＋再読込で実質的な「開始」になる。

## ループ設計（章選択→プレイ→クリア→次章）
1. 起動: `showLobbyOnBoot=true` ならロビー表示（停止）。
2. 章選択 → `PendingStartChapter=n` ＋ シーン再読込。
3. 再読込後の `Start`: `PendingStartChapter>0` のため `startedFromLobbySelection=true` → **ロビーを出さずに**その章を開始。
4. 章クリア → 既存のリザルト＋ `RecordChapterResult`(前章クリアで次章解放)＋ `AddBossAlly`。
5. 再びロビーへ戻す導線は現状「Options の再挑戦で再読込→`PendingStartChapter=0`→ロビー」。**専用「ロビーへ戻る」ボタンの追加は Claude Code 推奨タスク**（下記）。

## 受け入れ基準
- [x] `LobbyUI` が compile し、`EnsureExists/Show/Hide/ShowAsBootLobby` が存在。
- [x] `showLobbyOnBoot=false`（既定）では**従来どおり**即ゲーム開始（挙動不変）。
- [ ] `showLobbyOnBoot=true` で起動時にロビーが出て、章1選択→章1開始、章2は未クリア時ロック（**要エディタ検証**）。
- [ ] 章ボタン/オプション/終了が押せてレイアウト崩れが無い（**要エディタ検証**：解像度・フォント）。
- [x] Compilation completed (Errors: False)

## Claude Code への引き継ぎ（エディタで検証・仕上げ）
> 実装状況 (2026-05-31, Claude Code): **#1 完了**（`GameManager.Start` に `willShowBootLobby` 判定を入れ、ロビー表示時はラン初期化を遅延）。**#2 完了**（`GameManager.RequestReturnToLobby()` ＋静的 `ForceLobbyOnNextBoot` フラグ、OptionsPanel に「ロビーへ戻る / Return to Lobby」ボタン追加）。**#3/#4 は実機検証が必要なため未着手（Cowork 依頼）**。`showLobbyOnBoot` 既定 false 据え置き。
1. **起動フローの精緻化**: 現状 `showLobbyOnBoot=true` だと `Start()` がランを初期化した後にロビーを被せる（`GrantStartingUnit`/`TryShowChapterRoster`/`TryStartEventRound` が先に走る）。章選択時はシーン再読込で破棄されるため機能はするが、綺麗にするなら「ロビー表示中はラン初期化を遅延」する分岐を入れる。まず `showLobbyOnBoot=true` にして体感確認 → 必要なら初期化順を調整。
2. **「ロビーへ戻る」導線**: リザルト or オプションに「ロビー」ボタンを足し、`GameManager.RequestStartChapter` を使わず「ロビー表示のみのブートへ戻す」= `PendingStartChapter=0` で再読込（`showLobbyOnBoot=true` 前提）。
3. **見た目検証**: 解像度差・日本語/英語フォント・ボス仲間アイコンの有無で崩れないか。`CanvasScaler` 参照解像度の確認。
4. **任意**: タイトルロゴ画像・背景の差し込み（現状は文字タイトルのみ）。

## 未決事項
- ロビーをデフォルトONにするか（=Steam想定では通常ON）。安全のため一次実装はOFF。Claude Code が検証後にONを既定化してよい。
