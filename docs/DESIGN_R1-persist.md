# R1-persist: 永続化層（セーブ/ロード）の抽象化

> 設計: Claude / 実装: Codex・Claude Code / 2026-05-29
> 関連: [RELEASE_PLAN.md](RELEASE_PLAN.md) §2 M1, [MARKET_POSITIONING.md](MARKET_POSITIONING.md) §2
> **このタスクは R1-meta / R1-score / R1-settings の土台。最初に実装する。**

## ゴール
章進捗・所持ボス仲間・章ごとのベストスコア/タイムを**アプリ再起動後も保持**する。実装はローカル(JSON)から始め、**後で Steam クラウド/リーダーボードへ差し替え可能**なよう抽象化する（ROADMAP の確定事項: 「永続化層を抽象化して後からバックエンドへ差し替え可能に」と一致）。

## 影響範囲
- 新規ファイル:
  - `Assets/Scripts/Save/ISaveStore.cs` — 保存先の抽象インターフェース
  - `Assets/Scripts/Save/LocalJsonSaveStore.cs` — `Application.persistentDataPath` への JSON 実装
  - `Assets/Scripts/Save/SaveData.cs` — シリアライズ用 DTO（`[System.Serializable]`）
  - `Assets/Scripts/Save/SaveManager.cs` — シングルトン窓口（Load/Save/アクセサ）
- 変更ファイル:
  - `GameManager.cs` — 起動時に `SaveManager` から読み込み、章クリア/ボス仲間獲得時に保存呼び出し（R1-meta/score と連携。本タスクでは**フックポイントの用意**まで）
- 既存依存（壊さないこと）: `OptionsPanelUI` / `LocalizationManager` の **PlayerPrefs は現状維持**（設定系は R1-settings で別途吸収可。本タスクでは触らない）。

## データ構造

```csharp
// SaveData.cs — JSON 化される唯一のルート DTO。フィールド追加は後方互換を意識（既存JSONに無くてもデフォルト値で動く）。
[System.Serializable]
public class SaveData
{
    public int version = 1;               // スキーマ版。マイグレーション判定に使う
    public List<ChapterRecord> chapters = new List<ChapterRecord>();   // 章ごとの進捗/ベスト
    public List<BossAllyRecord> bossAllies = new List<BossAllyRecord>(); // 所持ボス仲間（R1-meta が使用）
    public long lastSavedUnixSec = 0;
}

[System.Serializable]
public class ChapterRecord
{
    public int chapter;          // 章番号（currentChapter と対応）
    public bool cleared;         // 一度でもクリアしたか（章解放判定に使用）
    public int bestScore;        // ベストスコア（0=未記録）
    public float bestTimeSec;    // ベストタイム（クリア時のみ。0=未記録）
    public int clearCount;       // クリア回数（回遊動機の可視化に使える）
}

[System.Serializable]
public class BossAllyRecord
{
    public string unitId;        // EntityData.name（例: "Snowchasermk"）
    public int starLevel = 1;    // 引き継ぐ★（R1-meta で定義。MVPは常に1でも可）
    public long acquiredUnixSec; // 取得日時（図鑑/並び順用）
}
```

```csharp
// ISaveStore.cs — 保存先の差し替え点。Steam 実装はこれを実装するだけ。
public interface ISaveStore
{
    SaveData Load();             // 無ければ新規 SaveData を返す（null は返さない）
    void Save(SaveData data);    // 同期保存（小さなデータなので可。失敗は内部でログ）
    bool Exists();
}
```

```csharp
// SaveManager.cs — ゲーム側が触る唯一の窓口。
public class SaveManager
{
    public static SaveManager Instance { get; }   // 遅延生成シングルトン（DontDestroyOnLoad）
    public SaveData Data { get; private set; }     // メモリ上の現在値

    public static SaveManager EnsureExists();      // 他UIの EnsureExists 慣習に合わせる
    public void Save();                            // Data を store へ書き込み（lastSaved更新）

    // --- 高レベルアクセサ（GameManager はこれだけ呼ぶ） ---
    public ChapterRecord GetChapter(int chapter);          // 無ければ生成して返す
    public bool IsChapterUnlocked(int chapter);            // chapter==1 は常にtrue / それ以外は (chapter-1) が cleared
    public void RecordChapterResult(int chapter, int score, float timeSec, bool cleared); // ベスト更新＋clearCount++＋Save
    public IReadOnlyList<BossAllyRecord> BossAllies { get; }
    public void AddBossAlly(string unitId, int starLevel);  // 重複は starLevel を max 更新 / 新規追加 → Save
    public bool HasBossAlly(string unitId);
}
```

## 振る舞いの仕様

1. **起動時**: `GameManager.Start()` の早い段階で `SaveManager.EnsureExists()`。`SaveManager` は生成時に `store.Load()` で `Data` を満たす（ファイル無ければ新規・version=1）。
2. **保存タイミング（明示Save）**: 「章クリア」「ボス仲間獲得」のみ即 `Save()`。毎フレーム保存はしない。小さなJSONなので同期保存でフレーム落ちしない想定。
3. **保存先**: `Application.persistentDataPath/save.json`。書き込みは一時ファイル→`File.Replace`/`Move` で原子的に（破損防止）。
4. **後方互換**: 読み込み時 `version` を見て、将来スキーマ変更時に `SaveData` のデフォルト値で吸収。不明フィールドは無視。
5. **エラー耐性**: JSON パース失敗時は破損ファイルを `save.corrupt.json` に退避し、新規 `SaveData` で続行（進捗消失より起動継続を優先。ログは Warning）。
6. **Steam 差し替え**: 将来 `SteamCloudSaveStore : ISaveStore`（クラウド同期）/ リーダーボードは別 API。`SaveManager` の生成時に注入する store を切り替えるだけで本体コードは無変更。

## 受け入れ基準
- [ ] ゲームを起動→章クリア→アプリ終了→再起動で、その章の `cleared=true`・`bestScore`・`bestTimeSec` が残っている
- [ ] ボス仲間を獲得→再起動で `SaveManager.BossAllies` に残っている
- [ ] `save.json` を手で壊して起動してもクラッシュせず、新規セーブで起動する（`save.corrupt.json` が残る）
- [ ] `IsChapterUnlocked(2)` は 章1クリア前 false / クリア後 true
- [ ] `Compilation completed (Errors: False)`

## 実装ヒント
- 既存の `EnsureExists()` パターン（OptionsPanelUI / AugmentHudUI 等）に合わせるとレビューが楽。
- JSON は Unity 標準 `JsonUtility`（既に `[System.Serializable]` を多用＝実績あり）。`List<T>` はそのままシリアライズ可。`Dictionary` は不可なので **List + 線形検索 or 起動時に Dictionary へ展開**。
- `GameManager` 側の保存呼び出しは R1-meta / R1-score で実際に埋める。本タスクでは `SaveManager` の API と「呼ぶべき場所」のコメント TODO を置くまででよい。

## 未決事項（Codex への質問）
- セーブスロットは1つでよいか（複数プロフィール不要か）。MVP は**単一スロット**前提で設計。要否あれば QUESTIONS.md へ。
