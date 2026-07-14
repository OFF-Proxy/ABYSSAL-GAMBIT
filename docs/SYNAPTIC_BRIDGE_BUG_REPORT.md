# Synaptic Unity MCP ブリッジ無応答 — 原因特定・解決策レポート

宛先: Synaptic（Unity MCP）開発チーム
報告者: AutoChessBossRush 開発（Cowork / Claude 経由で MCP を使用）
発生日時: 2026-06（セッション中）
深刻度: High（エディタ側ツールが全滅し、自動化ワークフローが完全停止）

---

## English TL;DR (for maintainers)

- **Symptom:** All *editor-side* tool calls (`run_csharp`, `execute(unity_console)`, every tool that round-trips into the Unity Editor) fail with `MCP error -32001: Request timed out`. *Server-side/static* tools (`list_categories`, `list_tools`) respond instantly. So the MCP server process is healthy; the **server ⇆ Unity Editor link (the in-editor request pump) is stalled.**
- **Trigger:** Started right after calling, via `run_csharp`, `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()` (+ `AssetDatabase.Refresh()`). Subsequent `run_csharp` calls first returned `{"resultSet":false}` (silently dropped responses), then degraded to hard timeouts.
- **Persistence:** Survived a **full Unity Editor restart** by the user. Still timing out.
- **Most likely root causes (ranked):** (1) bridge does not re-establish its request pump / socket after a **domain reload**; (2) editor request pump runs on `EditorApplication.update`, which Unity **throttles/halts when the Editor is unfocused/background**, so queued requests never execute; (3) an in-flight request issued across the compile/domain-reload boundary deadlocks or is orphaned, wedging the queue.
- **Asks:** re-init on `AssemblyReloadEvents.afterAssemblyReload`; pump independent of focus; explicit "editor reloading/disconnected" status instead of silent drop + late timeout; a cheap editor-side health probe; shorter, clearer timeout surfacing.

---

## 1. 環境

- 利用形態: Claude (Cowork) → MCP → Synaptic Unity サーバー → Unity Editor
- サーバー名（ツール接頭辞）: `mcp__unity-synaptic__*`
- 代表ツール: `run_csharp`（C# を Editor 上で評価。`AssemblyReload` はトリガーしない設計と説明あり）, `list_tools`, `list_categories`, `execute`, `inspect`, `modify` ほか（合計 356 tools / 32 categories）
- Unity: 2022.3 系（プロジェクト: AutoChessBossRush, 2D）
- OS: Windows

---

## 2. 症状（事実）

| 呼び出し | 種別 | 結果 |
|---|---|---|
| `list_categories` | サーバー側・静的 | **即時成功**（32カテゴリ/356ツールを返却） |
| `list_tools(category="editor")` | サーバー側・静的 | **即時成功** |
| `run_csharp("return 1+1;")` 等 | エディタ往復 | `MCP error -32001: Request timed out` |
| `execute("unity_console", {})` | エディタ往復 | `MCP error -32001: Request timed out` |

- つまり **静的（サーバープロセス内で完結）ツールは生存、Editor へ往復するツールは全滅**。
- `run_csharp` だけの問題ではなく、**Editor 側リクエスト処理経路そのもの**が停止していると判断。

### 劣化の時系列（重要）

1. 正常稼働中、`run_csharp` で以下を実行:
   ```csharp
   UnityEditor.AssetDatabase.Refresh();
   UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
   ```
   → これは成功（`compile-requested` を返却）。**スクリプト再コンパイル＝ドメインリロードを誘発。**
2. 直後の `run_csharp`（`isCompiling` 確認など）が **`{"success":true,"output":"","result":null,"resultSet":false}`** を返し始める＝**応答が黙って落ちる（dropped response）**。本セッションでは、ドメインリロードや `RequestScriptCompilation` 直後にこの `resultSet:false` が**断続的に発生**していた（既知のフレーク）。
3. その後、`run_csharp` は `resultSet:false` ですら返らず、**完全タイムアウト（-32001）** に移行。
4. ユーザーが **Unity を再起動**。にもかかわらず Editor 往復ツールは**依然タイムアウト**（サーバー側ツールのみ応答）。

---

## 3. 原因仮説（優先度順）

### 仮説A（最有力）: ドメインリロード後にブリッジがリクエストポンプ/ソケットを再確立できていない
- `RequestScriptCompilation()` → アセンブリ再ロードで **managed 静的状態が破棄・再生成**される。
- このとき Editor 側ブリッジが、(a) TCP/IPC リスナの再オープン、(b) `EditorApplication.update` への購読再登録、(c) スレッドの再起動 を**自動で行えていない**と、以後の Editor 往復が全て無応答になる。
- 再起動後も直らない点は、起動時の初期化が `[InitializeOnLoad]` ではなく特定操作トリガー依存だと説明がつく（＝起動だけでは復帰しない）。

### 仮説B: Editor 非フォーカス時にリクエストポンプが回らない
- Editor 側の受信処理が **メインスレッド** で `EditorApplication.update` 駆動の場合、Unity は **Editor が非アクティブ/バックグラウンドだと `update` のティックを大幅に間引く/停止**する（Preferences の「Interaction Mode / Enter Play Mode」等とは別の、ウィンドウ非アクティブ時の更新抑制）。
- 結果、キューに積まれたリクエストが処理されず **タイムアウト**。Cowork からの自動操作中は Editor が前面に来ないため発生しやすい。

### 仮説C: コンパイル/リロード境界をまたいだ in-flight リクエストのデッドロック
- `RequestScriptCompilation` を**含む** `run_csharp` のレスポンス待ち中にドメインリロードが走り、応答チャネルが破棄 → サーバーは応答を受け取れず **silent drop（resultSet:false）**。
- そのリクエストやロックが解放されず、以後のキューを**ウェッジ**させている可能性。

---

## 4. 切り分け済みの事実 / 開発側で再現してほしい手順

**確定事実**
- サーバー静的ツールは生存（プロセス・stdio/HTTP は健全）。
- Editor 往復ツールは**種類を問わず**全滅（`run_csharp` 固有ではない）。
- Unity 再起動で復帰しない。

**再現手順（推定）**
1. `run_csharp` で `CompilationPipeline.RequestScriptCompilation()`（+ `AssetDatabase.Refresh()`）を実行。
2. 直後に `run_csharp` を連打（`isCompiling` ポーリング等）。
3. `resultSet:false`（応答ドロップ）が出始める → やがて `-32001` タイムアウトへ。
4. Editor を非フォーカスのまま放置 / もしくは Editor 再起動。→ 復帰可否を観測。

---

## 5. 解決策の提案（開発チーム向け）

### 5.1 ドメインリロード耐性（最重要）
- ブリッジの初期化を `[InitializeOnLoad]` / `[InitializeOnLoadMethod]` で**起動時必ず**走らせる。
- `AssemblyReloadEvents.beforeAssemblyReload` で受信スレッド/ソケットを**正しくクローズ**、`afterAssemblyReload` で**再オープン＋`EditorApplication.update` 購読を再登録**。
- リロード前後で **in-flight リクエストを検知**し、サーバーへ「editor_reloading」ステータスを返す（後述）。

### 5.2 フォーカス非依存のポンプ
- 受信は**バックグラウンドスレッド**で行い、メインスレッドへは**スレッドセーフキュー**で受け渡し、`EditorApplication.update` で drain。
- Editor 非アクティブでもティックさせるため、ハートビート時に `EditorApplication.QueuePlayerLoopUpdate()`（Play中）や定期 `Repaint`、最低限 `EditorApplication.update` の生存監視を実装。
- ドキュメントに「Cowork/自動操作中は Editor を前面に保つ必要があるか」を明記、もしくは不要にする。

### 5.3 サイレントドロップ（`resultSet:false`）の撲滅
- 現状、ドメインリロードや評価失敗時に **`success:true` のまま `result:null, resultSet:false`** を返している。これは呼び出し側で「成功なのに値なし」となり**判別不能**。
- 代わりに **明示的なエラー/ステータス**（例: `{ "status": "editor_reloading" }` / `{ "status": "evaluator_dropped", "retryable": true }`）を返す。
- リロード直後のリクエストは **サーバー側で自動リトライ**（指数バックオフ、上限付き）。

### 5.4 ヘルスプローブ & 早期失敗
- **超軽量な editor-side ping**（main-thread ティック到達のみ確認、評価器を介さない）を用意。`run_csharp` 前にこれで生死判定できる。
- サーバー↔Editor の**ハートビート**を実装し、N 秒無応答なら**即座に "bridge disconnected" を返す**（30s 等のフルタイムアウト待ちを避ける）。
- タイムアウト時のエラーに**原因区分**を載せる（"editor not pumping" / "compiling" / "no heartbeat"）。

### 5.5 復旧手段（ユーザー向け）
- メニューコマンド `Synaptic > Reconnect Bridge` / `Restart Listener` を追加（再起動なしで復帰可能に）。
- ブリッジ状態を示す Editor ウィンドウ（接続中/リロード中/停止）。

### 5.6 利用側ガイド（暫定回避策）
- `RequestScriptCompilation()` / `AssetDatabase.Refresh()` は **単独の `run_csharp`** で実行し、その**戻り値を待たない**（リロードで応答が消えるため）。
- 再コンパイル完了は**別の軽量呼び出し**でポーリングし、`resultSet:false` は「リロード中」とみなして**リトライ**する。
- 長時間の自動操作中は Editor を前面・アクティブに保つ。

---

## 6. 影響

- C# 編集後の **コンパイル検証（realCS=0 確認）** と **実機 Play スモーク** が実行不能。
- 本件発生時、`GameManager` のプロローグ/VN トリガー改修（毎回再生化・スキップ設定連動・診断ログ追加）が**ソース反映済みだがコンパイル/コミット/動作確認が未完**で停止。

---

## 7. 添付してほしいログ（開発チームが取得すると有用）

- Unity Editor の `Editor.log`（特に `AssemblyReload` 前後、ブリッジ初期化・ソケット bind/accept 行）。
- Synaptic サーバープロセスのログ（リクエスト送信〜タイムアウトの相関、editor 応答の有無）。
- ブリッジの受信スレッド/`EditorApplication.update` 購読の生存ログ（ドメインリロード後に再登録されているか）。
