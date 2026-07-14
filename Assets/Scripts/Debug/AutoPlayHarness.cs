// DESIGN_autoplay.md: 開発用オートプレイ・ハーネス（v1：ハーネス＋Dump）。
// ユーザーには出ません。製品ビルドではコンパイルされません（#if UNITY_EDITOR || DEVELOPMENT_BUILD）。
// このファイル1つ削除すれば機能が完全に消える設計です。
//
// 役割：標準ヒューリスティックのボットでフルランを自動周回し、
//   - 進行不能（一定時間進行しない）を検出
//   - 例外/エラーログを捕捉
//   そのとき AutoPlayDumps/ にログと状態スナップショットを書き出す（Claudeが解析・修正に使う）。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using AutoChessBossRush.Save;

namespace AutoChessBossRush.DebugTools
{
    public class AutoPlayHarness : MonoBehaviour
    {
        private static AutoPlayHarness instance;
        public static AutoPlayHarness Instance => instance;

        // ===== 設定（DebugMenuから調整可） =====
        public float speed = 6f;                  // 戦闘/進行の時間スケール。
        public float actInterval = 0.2f;          // ボット判断の間隔（実時間秒）。
        public float stuckTimeoutSec = 30f;        // この実時間秒だけ状態が変わらなければ「進行不能」。
        public int maxChapter = 3;                 // 周回する最大章（1..maxChapter）。
        public string[] heroPool = { "HeroAldin", "HeroKagachi", "HeroVesna" };

        public bool Running { get; private set; }
        public int RunsDone { get; private set; }
        public int Clears { get; private set; }
        public int GameOvers { get; private set; }
        public int StuckRuns { get; private set; }
        public int DumpsWritten { get; private set; }
        public string LastStatus { get; private set; } = "idle";

        // ===== 内部状態 =====
        private readonly Queue<(string hero, int chapter)> configQueue = new Queue<(string, int)>();
        private (string hero, int chapter) currentConfig;
        private float nextActTime;
        private string lastSignature = "";
        private float lastProgressRealtime;
        private int runStartFrame;
        private bool runActive;            // 章シーンに入ってラン進行中。
        private bool runRecorded;          // 当該ランの結果を集計済みか。
        private bool dumpedThisRun;        // 当該ランで既にDumpしたか（連発防止）。
        private int earlyLossStreak;       // 連続早期敗北数（学習：盤面強化へ寄せる）。
        public float wanderSeconds = 10f;  // 戦闘で生存数が変化しないままこの実時間秒を超えたら「ウロウロ/手詰まり」。
        public float maxRoundSeconds = 45f; // 1ラウンドがこの実時間秒を超えたら強制決着（バッチの凍結防止）。
        private int lastRoundWave = -1;
        private float roundStartRealtime;
        private string lastCombatSig = "";
        private float lastCombatChangeRealtime;
        private bool wanderReportedThisRun; // 当該ランでウロウロ報告済みか（連発防止）。
        private bool overlapReportedThisRun; // 当該ランで「同一マス重複」報告済みか。
        private readonly List<string> routePicks = new List<string>(); // 当該ランで選んだ進路（ルート記録用）。
        private int lastClearChapter = -1;  // 二重記録防止：クリア分析を書いた章。
        // 直近の「戦闘開始時」編成スナップショット（敗北時は全滅で盤面が空になるため、ここで負けた編成を残す）。
        private string lastFightRoster = "", lastFightSyn = "(none)", lastFightAug = "(none)";
        private int lastFightWave = 0, lastFightLevel = 0, lastFightItems = 0, lastFightPlaced = 0;

        // ログ捕捉。
        private readonly List<string> logRing = new List<string>();
        private const int LogRingMax = 400;
        private int pendingErrorCount;
        private string pendingErrorMsg;

        public static AutoPlayHarness EnsureExists()
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("AutoPlayHarness", typeof(AutoPlayHarness));
            DontDestroyOnLoad(go);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }
        private void OnDestroy() { if (instance == this) instance = null; }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            string line = $"[{Time.frameCount}] {type}: {condition}";
            logRing.Add(line);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                logRing.Add(stackTrace);
                pendingErrorCount++;
                if (pendingErrorMsg == null) pendingErrorMsg = condition;
            }
            while (logRing.Count > LogRingMax) logRing.RemoveAt(0);
        }

        // ===== 開始/停止 =====
        public void StartBatch(int iterations)
        {
            ClearOldLogs();                                  // 古いバージョンのログ/ダンプを消してから開始（混入防止）。
            GameManager.AutoPlayBypassStoryBlock = true;     // オートプレイ中は導入演出ブロックを無効化（空編成事故の防止）。
            BuildQueue(iterations);
            Running = true;
            RunsDone = Clears = GameOvers = StuckRuns = DumpsWritten = 0;
            nextActTime = 0f;
            lastSignature = "";
            lastProgressRealtime = Time.realtimeSinceStartup;
            runActive = false; runRecorded = true; dumpedThisRun = false;
            LastStatus = $"batch start: {configQueue.Count} runs";
            Debug.Log($"[AutoPlay] start batch: {configQueue.Count} runs");
            // 既にゲーム中なら一旦ロビーへ戻して、設定キューの先頭から開始する。
            if (GameManager.Instance != null)
                GameManager.Instance.RequestReturnToLobby();
        }

        public void Stop()
        {
            Running = false;
            Time.timeScale = 1f;
            GameManager.AutoPlayBypassStoryBlock = false;    // 通常プレイへ戻す。
            LastStatus = "stopped";
            Debug.Log("[AutoPlay] stopped");
        }

        // バッチ開始時に AutoPlayDumps を一掃（累積ログ＋過去スナップショット）。古いバージョンのデータ混入を防ぐ。
        private void ClearOldLogs()
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string dumpsRoot = Path.Combine(root, "AutoPlayDumps");
                if (!Directory.Exists(dumpsRoot)) return;
                foreach (var f in Directory.GetFiles(dumpsRoot, "*.log")) { try { File.Delete(f); } catch { } }
                foreach (var d in Directory.GetDirectories(dumpsRoot)) { try { Directory.Delete(d, true); } catch { } }
                Debug.Log("[AutoPlay] cleared old AutoPlayDumps before batch.");
            }
            catch (Exception e) { Debug.LogWarning("[AutoPlay] clear logs failed: " + e.Message); }
        }

        private void BuildQueue(int iterations)
        {
            configQueue.Clear();
            iterations = Mathf.Max(1, iterations);
            for (int it = 0; it < iterations; it++)
                for (int ch = 1; ch <= Mathf.Max(1, maxChapter); ch++)
                    foreach (string h in heroPool)
                        configQueue.Enqueue((h, ch));
        }

        private void Update()
        {
            if (!Running) return;

            // 速度。ポーズ系ポップアップ中は強制しない（自動解決ですぐ閉じる）。
            bool pausePopup = (AugmentSelectionUI.Instance != null && AugmentSelectionUI.Instance.DebugIsOpen)
                              || (HeroUltUpgradeUI.Instance != null && HeroUltUpgradeUI.Instance.gameObject.activeSelf);
            if (!pausePopup && Time.timeScale != speed) Time.timeScale = speed;

            if (Time.realtimeSinceStartup < nextActTime) return;
            nextActTime = Time.realtimeSinceStartup + actInterval;

            try { Tick(); }
            catch (Exception e)
            {
                pendingErrorCount++; pendingErrorMsg = "Harness Tick exception: " + e.Message;
                logRing.Add(e.ToString());
            }

            // 例外/エラーが出ていれば（ラン中・未Dumpなら）スナップショットを残す。
            if (pendingErrorCount > 0 && runActive && !dumpedThisRun)
            {
                WriteDump("exception", pendingErrorMsg ?? "error");
                dumpedThisRun = true;
            }
            pendingErrorCount = 0; pendingErrorMsg = null;
        }

        private void Tick()
        {
            GameManager gm = GameManager.Instance;

            // --- ロビー/ロード中：次のランを開始 ---
            if (gm == null)
            {
                if (runActive && !runRecorded) { /* 結果未確定のままロビーへ＝中断扱い */ runRecorded = true; }
                runActive = false;
                StartNextConfigOrFinish();
                return;
            }

            // --- 1) 各種ポップアップを自動解決 ---
            if (ResolvePopups(gm)) { MarkProgress(gm); return; }

            // --- 2) リザルト（章クリア/ゲームオーバー） ---
            var result = ResultPanelUI.Instance;
            if (result != null && result.IsResultOpen)
            {
                if (!runRecorded)
                {
                    if (result.LastResultWasChapterClear) { Clears++; earlyLossStreak = 0; AnalyzeClear(gm); }
                    else if (result.LastResultWasGameOver)
                    {
                        GameOvers++;
                        int reachedWave = gm.DebugCurrentWaveNumber;
                        if (reachedWave <= 3) earlyLossStreak = Mathf.Min(5, earlyLossStreak + 1); else earlyLossStreak = 0;
                        AnalyzeGameOver(gm); // 敗因を分析してDump＋学習ログへ。
                    }
                    RunsDone++;
                    runRecorded = true;
                    LastStatus = $"run#{RunsDone} {currentConfig.hero} ch{currentConfig.chapter} -> "
                                 + (result.LastResultWasChapterClear ? "CLEAR" : "GAMEOVER");
                    Debug.Log("[AutoPlay] " + LastStatus);
                }
                result.DebugContinue(); // ロビーへ戻る
                MarkProgress(gm);
                return;
            }

            // --- 3) 編成フェーズ：ヒーロー投入→レベル/購入→アイテム装備→配置→FIGHT ---
            if (!gm.IsRoundInProgress)
            {
                PrepPhase(gm);
                MarkProgress(gm);
                return;
            }

            // --- 4) 戦闘中：自動戦闘に任せる。進行（ウェーブ/署名変化）を監視 ---
            if (gm.CanUseHeroUltimate()) gm.UseHeroUltimate(); // 必殺はCT明け＝即使用（ここぞ判断は省略、まずは撃つ）。
            CheckRoundTimeout(gm);    // 1ラウンドの長期化は強制決着（凍結防止）。
            CheckOverlap(gm);         // 同一マスに複数ユニット（配置/移動の異常）を検出して記録。
            CheckWander(gm);          // ウロウロ/手詰まりの長期化を検出して記録。
            CheckProgressOrStuck(gm); // 完全停止（進行不能）の検出＆中断。
        }

        // 開いている選択UIを1つ自動解決。解決したらtrue。
        private bool ResolvePopups(GameManager gm)
        {
            // 章プロローグ（全画面演出）は最優先で即終了。実時間進行のため放置すると購入がブロックされ続ける（空編成→全滅）。
            if (ChapterPrologueUI.Instance != null && ChapterPrologueUI.Instance.DebugAutoResolve()) return true;
            // ボス戦前ダイアログは最優先で処理（DebugFightを二重に呼んで未処理のまま戦闘開始するのを防ぐ）。
            if (HeroBossDialogueUI.Instance != null && HeroBossDialogueUI.Instance.DebugAutoResolve()) return true;
            if (InterludeUI.Instance != null && InterludeUI.Instance.DebugAutoResolve()) return true;
            if (AugmentSelectionUI.Instance != null && AugmentSelectionUI.Instance.DebugAutoResolve()) return true;
            if (ItemRewardSelectionUI.Instance != null && ItemRewardSelectionUI.Instance.DebugAutoResolve()) return true;
            if (BossRewardSelectionUI.Instance != null && BossRewardSelectionUI.Instance.DebugAutoResolve()) return true;
            if (HeroUltUpgradeUI.Instance != null && HeroUltUpgradeUI.Instance.DebugAutoResolve()) return true;
            // 進路選択（章ボス前）：先頭ノードを自動選択。これが無いとオートプレイが進路選択でハングする。
            if (NodeSelectionUI.Instance != null && NodeSelectionUI.Instance.DebugAutoResolve())
            {
                if (!string.IsNullOrEmpty(NodeSelectionUI.Instance.LastPickedLabel))
                    routePicks.Add($"w{(GameManager.Instance != null ? GameManager.Instance.DebugCurrentWaveNumber : 0)}:{NodeSelectionUI.Instance.LastPickedLabel}");
                return true;
            }
            // 強化マス：種別選択→盤面設置（設置はGameManager側で実行）。
            if (BuffTileRewardUI.Instance != null && BuffTileRewardUI.Instance.DebugChooseFirstType()) return true;
            if (gm.DebugResolveBuffTilePlacement()) return true;
            return false;
        }

        // 編成フェーズの一連の意思決定。
        private void PrepPhase(GameManager gm)
        {
            ResyncOccupancy();      // 0) ノード占有フラグを実際のユニット位置に同期（重複配置の元凶を断つ）。
            PlaceHero(gm);          // 1) 主人公を盤面へ（無料枠）。
            BuyPhase(gm);           // 2) レベル上げ＋シナジー/星up/盤面充足を意識した購入。
            gm.DebugAutoEquipBenchItems(); // 3) アイテムを最良ユニットへ装備。
            PlaceBenchUnits(gm);    // 4) ベンチの強いユニットを前衛/後衛に配置。
            CaptureFightSnapshot(gm); // 4.5) 戦闘開始直前の編成を記録（敗北時は全滅で盤面が空になるため、ここで残す）。
            gm.DebugFight();        // 5) 戦闘開始。
        }

        // ノード占有フラグを、盤面上の実際のユニット位置に合わせて再同期する。
        // ウェーブ復帰(RestoreForNextWave)等で占有フラグが実体とズレ、空きと誤認して
        // 同一マスへ重ねて配置してしまう不具合（＝戦闘が進まない原因）を防ぐ。
        private void ResyncOccupancy()
        {
            foreach (var e in FindObjectsOfType<BaseEntity>())
                if (e != null && e.IsOnBoard && e.CurrentNode != null)
                    e.CurrentNode.SetOccupied(true);
        }

        // 指定ノードに既にチーム1ユニットが物理的に居るか（占有フラグに頼らない二重チェック）。
        private bool NodeHasTeam1Unit(Node node)
        {
            if (node == null) return false;
            foreach (var e in FindObjectsOfType<BaseEntity>())
                if (e != null && e.Team == Team.Team1 && e.IsOnBoard && e.CurrentNode == node)
                    return true;
            return false;
        }

        // 主人公（ベンチに居れば）を前寄りに配置。盤面枠を消費しない。
        private void PlaceHero(GameManager gm)
        {
            BaseEntity hero = FindObjectsOfType<BaseEntity>()
                .FirstOrDefault(e => e != null && e.Team == Team.Team1 && !e.IsCore && !e.IsOnBoard && gm.IsHeroUnit(e.UnitId));
            if (hero == null) return;
            Node n = PickNodeForRole(true);
            if (n != null && gm.CanPlaceEntityManually(hero, n))
                gm.TryPlaceEntityManually(hero, n);
        }

        // レベル上げ＋購入。利子用の確保下限を意識しつつ、重複(星up)とシナジー、盤面充足を優先。
        private void BuyPhase(GameManager gm)
        {
            var pd = PlayerData.Instance;
            UIShop shop = UIShop.Instance;
            if (pd == null) return;

            int wave = gm.DebugCurrentWaveNumber;
            int keep = Mathf.Max(0, Mathf.Min(30, wave * 5) - earlyLossStreak * 5); // 連敗時は確保を緩めて盤面強化。
            int levelTarget = Mathf.Min(8, 2 + wave);

            // レベル上げ（盤面上限＝Lv）。確保下限を割らない範囲で。
            int guard = 0;
            while (pd.Level < levelTarget && pd.Money >= keep + 4 && guard++ < 8)
                if (!pd.TryBuyExp(4, 4)) break;

            if (shop == null || shop.allCards == null) return;

            // 所有数・所有シナジー数を集計（星up/シナジー判定用）。シナジーは「何体がそれを持つか」で数える。
            var owned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var synCount = new Dictionary<SynergyType, int>();
            BaseEntity heroEnt = null;
            foreach (var e in FindObjectsOfType<BaseEntity>())
            {
                if (e == null || e.Team != Team.Team1 || e.IsCore) continue;
                if (gm.IsHeroUnit(e.UnitId)) { heroEnt = e; }
                if (e.StarLevel < 3)
                    owned[e.UnitId] = (owned.TryGetValue(e.UnitId, out int c) ? c : 0) + 1;
                AddSyn(synCount, e.synergy1); AddSyn(synCount, e.synergy2); AddSyn(synCount, e.synergy3);
            }

            // 主軸シナジー＝「最も多く所持しているシナジー」。1つに集中して最大まで伸ばす。
            // まだ何も無ければ主人公の陣営シナジーを主軸に（ヒーロー陣営を伸ばすと勝率が高い、というユーザー知見）。
            SynergyType primary = PickPrimarySynergy(synCount, heroEnt);

            int rerolls = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                // 1パスで「最良の1枚」をスコアで選んで買う。主軸>所持シナジー>星up>盤面充足の優先度。
                UICard bestCard = null; EntitiesDatabaseSO.EntityData bestData = default; int bestScore = int.MinValue;
                foreach (var card in shop.allCards)
                {
                    if (card == null || !card.gameObject.activeSelf) continue;
                    var d = card.EntityData;
                    if (string.IsNullOrEmpty(d.name) || d.prefab == null) continue;
                    if (!gm.CanBuyEntity(d) || !pd.CanAfford(d.cost)) continue;

                    bool dup = owned.TryGetValue(d.name, out int oc) && oc >= 1;            // 3体で星up
                    bool boardNotFull = gm.PlacedTeam1Count < gm.PlacementLimit;
                    bool hasPrimary = d.synergy1 == primary || d.synergy2 == primary || d.synergy3 == primary;
                    int ownedSynMatch = SynScore(synCount, d.synergy1) + SynScore(synCount, d.synergy2) + SynScore(synCount, d.synergy3);
                    bool anyOwnedSyn = ownedSynMatch > 0;
                    bool worth = dup || hasPrimary || anyOwnedSyn || boardNotFull;
                    if (!worth) continue;
                    if (!dup && pd.Money - d.cost < keep) continue; // 星up以外は確保下限を守る。

                    int score = 0;
                    if (hasPrimary) score += 1000;          // 主軸最優先
                    if (dup) score += 600;                  // 星up
                    score += ownedSynMatch * 120;           // 相性の良い（既に持つ）シナジーを伸ばす
                    if (boardNotFull) score += 80;          // 盤面が空いていれば多少加点
                    score += d.cost * 5;                    // 同条件なら高コスト寄り（盤面の格上げ）
                    if (score > bestScore) { bestScore = score; bestCard = card; bestData = d; }
                }

                if (bestCard != null)
                {
                    shop.OnCardClick(bestCard, bestData);
                    owned[bestData.name] = (owned.TryGetValue(bestData.name, out int c2) ? c2 : 0) + 1;
                    AddSyn(synCount, bestData.synergy1); AddSyn(synCount, bestData.synergy2); AddSyn(synCount, bestData.synergy3);
                    if (primary == SynergyType.None) primary = PickPrimarySynergy(synCount, heroEnt);
                    continue;
                }
                // 良い買いが無ければリロール（主軸/星up探し）。
                if (pd.Money > keep + 6 && rerolls < 3 && gm.PlacedTeam1Count >= 1)
                { shop.OnRefreshClick(); rerolls++; }
                else break;
            }
        }

        private static void AddSyn(Dictionary<SynergyType, int> map, SynergyType s)
        {
            if (s == SynergyType.None) return;
            map[s] = (map.TryGetValue(s, out int c) ? c : 0) + 1;
        }
        private static int SynScore(Dictionary<SynergyType, int> map, SynergyType s)
        {
            if (s == SynergyType.None) return 0;
            return map.TryGetValue(s, out int c) ? c : 0;
        }
        // 主軸シナジー＝最多所持。無ければ主人公の陣営/クラスシナジー。
        private SynergyType PickPrimarySynergy(Dictionary<SynergyType, int> synCount, BaseEntity hero)
        {
            SynergyType best = SynergyType.None; int bestC = 0;
            foreach (var kv in synCount) if (kv.Value > bestC) { bestC = kv.Value; best = kv.Key; }
            if (best != SynergyType.None && bestC >= 2) return best;       // 既に2体以上いるなら確定で伸ばす
            if (hero != null)
            {
                if (hero.synergy1 != SynergyType.None) return hero.synergy1;
                if (hero.synergy2 != SynergyType.None) return hero.synergy2;
                if (hero.synergy3 != SynergyType.None) return hero.synergy3;
            }
            return best;
        }

        // ベンチの自陣ユニット（星が高い順）を、前衛(近接)/後衛(遠隔)を意識して配置。
        private void PlaceBenchUnits(GameManager gm)
        {
            var benched = FindObjectsOfType<BaseEntity>()
                .Where(e => e != null && e.Team == Team.Team1 && !e.IsCore && !e.IsOnBoard && !gm.IsHeroUnit(e.UnitId))
                .OrderByDescending(e => e.StarLevel)
                .ToList();
            foreach (var u in benched)
            {
                if (gm.PlacedTeam1Count >= gm.PlacementLimit) break;
                bool melee = u.range <= 1;
                Node n = PickNodeForRole(melee);
                if (n == null) break;
                if (gm.CanPlaceEntityManually(u, n))
                    gm.TryPlaceEntityManually(u, n);
                else
                    break;
            }
        }

        // 役割に応じた空き配置マスを選ぶ。近接=前列(敵に近い=列大)、遠隔=後列(列小)。行は散らす。
        private Node PickNodeForRole(bool melee)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return null;
            Node best = null; int bestScore = int.MinValue;
            for (int col = 1; col <= 5; col++)
                for (int row = 1; row <= 12; row++)
                {
                    Node n = grid.GetNodeAtBoardCoordinate(col, row);
                    if (n == null || n.IsOccupied || !grid.IsDeploymentNode(Team.Team1, n)) continue;
                    if (NodeHasTeam1Unit(n)) continue; // 占有フラグが万一ズレていても、実体が居れば避ける。
                    int bc = grid.GetBoardColumn(n);
                    int score = (melee ? bc : -bc) * 100 - grid.GetBoardRow(n);
                    if (score > bestScore) { bestScore = score; best = n; }
                }
            return best;
        }

        private void StartNextConfigOrFinish()
        {
            // シーン遷移直後の落ち着き待ち（署名で代用）。
            if (configQueue.Count == 0)
            {
                Running = false;
                Time.timeScale = 1f;
                LastStatus = $"DONE runs={RunsDone} clear={Clears} over={GameOvers} stuck={StuckRuns} dumps={DumpsWritten}";
                Debug.Log("[AutoPlay] " + LastStatus);
                return;
            }

            // まだ前のランの後始末待ち（GameManager==null安定）→次を開始。
            currentConfig = configQueue.Dequeue();
            SaveManager.EnsureExists();
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SetHeroUnitId(currentConfig.hero);
                // 対象章を解放しておく（章開始自体には必須ではないが整合のため）。
                if (currentConfig.chapter > 1)
                    SaveManager.Instance.RecordChapterResult(currentConfig.chapter - 1, 0, 0f, true);
            }
            runRecorded = false; runActive = true; dumpedThisRun = false;
            routePicks.Clear(); lastClearChapter = -1; // ラン毎にルート記録をリセット。
            lastFightRoster = ""; lastFightSyn = "(none)"; lastFightAug = "(none)";
            lastFightWave = lastFightLevel = lastFightItems = lastFightPlaced = 0;
            wanderReportedThisRun = false; overlapReportedThisRun = false; lastCombatSig = ""; lastCombatChangeRealtime = Time.realtimeSinceStartup;
            lastRoundWave = -1; roundStartRealtime = Time.realtimeSinceStartup;
            lastSignature = ""; lastProgressRealtime = Time.realtimeSinceStartup;
            runStartFrame = Time.frameCount;
            LastStatus = $"start {currentConfig.hero} ch{currentConfig.chapter}";
            Debug.Log("[AutoPlay] " + LastStatus);

            // 章シーンへ。GameManagerが生成されたら Tick が運転を始める。
            GameManagerStartChapter(currentConfig.chapter);
        }

        // GameManagerが居れば直接、居なければ一時生成は不可なので Pending 経由で。
        private void GameManagerStartChapter(int chapter)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RequestStartChapter(chapter);
                return;
            }
            // ロビーシーンには GameManager が無い。PendingStartChapter を立てて GameScene をロードする。
            GameManager.PendingStartChapter = Mathf.Max(1, chapter);
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        // 署名が変われば進行とみなし、タイマーをリセット。
        private void MarkProgress(GameManager gm)
        {
            string sig = Signature(gm);
            if (sig != lastSignature)
            {
                lastSignature = sig;
                lastProgressRealtime = Time.realtimeSinceStartup;
            }
        }

        private void CheckProgressOrStuck(GameManager gm)
        {
            string sig = Signature(gm);
            if (sig != lastSignature)
            {
                lastSignature = sig;
                lastProgressRealtime = Time.realtimeSinceStartup;
                return;
            }
            if (Time.realtimeSinceStartup - lastProgressRealtime > stuckTimeoutSec)
            {
                StuckRuns++;
                if (!dumpedThisRun) { WriteDump("stuck", $"no progress {stuckTimeoutSec}s"); dumpedThisRun = true; }
                // ランを中断してロビーへ戻し、次の設定へ。
                if (!runRecorded) { RunsDone++; runRecorded = true; }
                LastStatus = $"STUCK {currentConfig.hero} ch{currentConfig.chapter} wave{gm.DebugCurrentWaveNumber}";
                Debug.LogWarning("[AutoPlay] " + LastStatus);
                gm.RequestReturnToLobby();
                lastProgressRealtime = Time.realtimeSinceStartup;
            }
        }

        // 1ラウンドが長引きすぎたら強制的に決着させてバッチの凍結を防ぐ（手詰まりの保険）。
        private void CheckRoundTimeout(GameManager gm)
        {
            int wave = gm.DebugCurrentWaveNumber;
            if (wave != lastRoundWave) { lastRoundWave = wave; roundStartRealtime = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - roundStartRealtime > maxRoundSeconds)
            {
                Debug.LogWarning($"[AutoPlay] round timeout (>{maxRoundSeconds}s) at wave{wave} -> force win to continue");
                gm.DebugInstantWinRound();
                roundStartRealtime = Time.realtimeSinceStartup;
            }
        }

        // 戦闘中、味方/敵の生存数が一定時間まったく変化しない＝ユニットが交戦できずウロウロ/手詰まり。
        // 完全停止(進行不能)より早く検出し、修正を促すレポートを残す（ランは継続）。
        private void CheckWander(GameManager gm)
        {
            if (!gm.IsRoundInProgress) return;
            int allies = CountAlive(Team.Team1);
            int enemies = CountAlive(Team.Team2);
            string sig = allies + "v" + enemies + "@" + gm.DebugCurrentWaveNumber;
            if (sig != lastCombatSig)
            {
                lastCombatSig = sig;
                lastCombatChangeRealtime = Time.realtimeSinceStartup;
                return;
            }
            if (enemies > 0 && allies > 0 && !wanderReportedThisRun
                && Time.realtimeSinceStartup - lastCombatChangeRealtime > wanderSeconds)
            {
                wanderReportedThisRun = true;
                WriteWanderReport(gm, allies, enemies);
            }
        }

        // 同一マスに味方が2体以上いないか（配置/移動の論理的な重なり）を検出して記録。
        // ※高速再生中のアニメ補間による「見た目の重なり」とは別に、実データ上の重複だけを拾う。
        private void CheckOverlap(GameManager gm)
        {
            if (overlapReportedThisRun) return;
            var byNode = new Dictionary<Node, List<string>>();
            foreach (var e in FindObjectsOfType<BaseEntity>())
            {
                if (e == null || e.Team != Team.Team1 || !e.IsOnBoard) continue;
                Node n = e.CurrentNode; if (n == null) continue;
                if (!byNode.TryGetValue(n, out var list)) { list = new List<string>(); byNode[n] = list; }
                list.Add(e.UnitId + "★" + e.StarLevel);
            }
            string overlaps = "";
            foreach (var kv in byNode)
                if (kv.Value.Count > 1) overlaps += "node" + kv.Key.index + "{" + string.Join("+", kv.Value) + "} ";
            if (overlaps.Length == 0) return;

            overlapReportedThisRun = true;
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string dumpsRoot = Path.Combine(root, "AutoPlayDumps");
                Directory.CreateDirectory(dumpsRoot);
                File.AppendAllText(Path.Combine(dumpsRoot, "overlap_report.log"),
                    $"{DateTime.Now:HH:mm:ss}\t{currentConfig.hero}\tch{currentConfig.chapter}\twave{gm.DebugCurrentWaveNumber}\t{overlaps}\n");
                DumpsWritten++;
                Debug.LogWarning("[AutoPlay] overlap detected: " + overlaps);
            }
            catch (Exception e) { Debug.LogError("[AutoPlay] overlap report failed: " + e.Message); }
        }

        private int CountAlive(Team team)
        {
            int c = 0;
            foreach (var e in FindObjectsOfType<BaseEntity>())
                if (e != null && e.Team == team && e.IsOnBoard && e.CanBeTargeted && !e.IsCore) c++;
            return c;
        }

        private void WriteWanderReport(GameManager gm, int allies, int enemies)
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string dumpsRoot = Path.Combine(root, "AutoPlayDumps");
                Directory.CreateDirectory(dumpsRoot);
                File.AppendAllText(Path.Combine(dumpsRoot, "wander_report.log"),
                    $"{DateTime.Now:HH:mm:ss}\t{currentConfig.hero}\tch{currentConfig.chapter}\twave{gm.DebugCurrentWaveNumber}/{gm.DebugWaveCount}"
                    + $"\tally{allies}\tenemy{enemies}\theldFor>{wanderSeconds}s\t(交戦できずウロウロ/手詰まりの疑い→経路/配置の修正を検討)\n");
                string dir = Path.Combine(dumpsRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_wander");
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine("# AutoPlay Wander/Stall Report");
                sb.AppendLine($"time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"config: hero={currentConfig.hero} chapter={currentConfig.chapter}");
                sb.AppendLine($"wave: {gm.DebugCurrentWaveNumber}/{gm.DebugWaveCount}");
                sb.AppendLine($"aliveAlly: {allies}  aliveEnemy: {enemies}  (生存数が {wanderSeconds}s 変化せず)");
                sb.AppendLine("note: ユニットが射程内へ到達できず交戦が進んでいない可能性。経路探索/配置/射程の確認を。");
                File.WriteAllText(Path.Combine(dir, "report.txt"), sb.ToString());
                File.WriteAllText(Path.Combine(dir, "log.txt"), string.Join("\n", logRing));
                DumpsWritten++;
                Debug.LogWarning($"[AutoPlay] wander/stall report: ally{allies} vs enemy{enemies} wave{gm.DebugCurrentWaveNumber}");
            }
            catch (Exception e) { Debug.LogError("[AutoPlay] wander report failed: " + e.Message); }
        }

        private string Signature(GameManager gm)
        {
            int money = PlayerData.Instance != null ? PlayerData.Instance.Money : -1;
            return string.Join("|", new string[]
            {
                gm.IsRoundInProgress ? "R" : "P",
                gm.DebugCurrentWaveNumber.ToString(),
                gm.DebugWaveCount.ToString(),
                gm.PlacedTeam1Count.ToString(),
                money.ToString(),
            });
        }

        // ===== ゲームオーバー分析（敗因の推定＋学習ログ） =====
        private void AnalyzeGameOver(GameManager gm)
        {
            try
            {
                int wave = gm.DebugCurrentWaveNumber, waves = gm.DebugWaveCount;
                int level = PlayerData.Instance != null ? PlayerData.Instance.Level : -1;
                int gold = PlayerData.Instance != null ? PlayerData.Instance.Money : -1;
                int placed = gm.PlacedTeam1Count, limit = gm.PlacementLimit;

                // 盤面ユニット集計。
                var board = FindObjectsOfType<BaseEntity>()
                    .Where(e => e != null && e.Team == Team.Team1 && !e.IsCore && e.IsOnBoard).ToList();
                int starSum = board.Sum(e => e.StarLevel);
                int items = board.Sum(e => e.EquippedItems.Count);
                float avgStar = board.Count > 0 ? (float)starSum / board.Count : 0f;
                string roster = BoardRosterStr(board);          // ユニット★＋装備アイテム名
                string augStr = OwnedAugmentsStr(gm);            // 取得オーグメント
                string routeStr = RouteStr();                   // 選んだ進路

                // アクティブシナジー（取得失敗しても分析自体は続行）。
                string synStr = "(none)";
                int synCount = 0;
                try
                {
                    if (SynergyManager.Instance != null)
                    {
                        var act = SynergyManager.Instance.GetActiveSynergies();
                        if (act != null && act.Count > 0)
                        {
                            synCount = act.Count;
                            synStr = string.Join(", ", act.Select(kv => $"{kv.Key}:T{kv.Value}"));
                        }
                    }
                }
                catch { synStr = "(synergy-read-failed)"; }

                // 敗因の推定。全滅後の空盤面ではなく「負けた編成(戦闘開始時スナップショット)」で判定する。
                var causes = new List<string>();
                if (lastFightPlaced == 0)
                {
                    // 一度も編成して戦闘開始できなかった＝ボット/導入演出側の問題（バランス対象外）。
                    causes.Add("never-fielded(編成前に敗北＝ボット/導入の問題)");
                }
                else
                {
                    if (wave <= 2) causes.Add("very-early-loss(弱い立ち上がり)");
                    if (lastFightPlaced < limit) causes.Add($"board-not-full(編成{lastFightPlaced}/{limit})");
                    if (lastFightSyn == "(none)") causes.Add("no-active-synergy(シナジー未成立)");
                    if (lastFightLevel <= wave) causes.Add($"under-leveled(Lv{lastFightLevel}/wave{wave})");
                    if (lastFightItems == 0) causes.Add("no-items-equipped");
                    if (causes.Count == 0) causes.Add("balanced-loss(ちゃんと組んで敗北＝難易度高め?)");
                }

                var sb = new StringBuilder();
                sb.AppendLine("# AutoPlay GameOver Analysis");
                sb.AppendLine($"time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"config: hero={currentConfig.hero} chapter={currentConfig.chapter}");
                sb.AppendLine($"reachedWave: {wave}/{waves}");
                sb.AppendLine($"level: {level}  gold: {gold}  placed: {placed}/{limit}");
                sb.AppendLine($"avgStar: {avgStar:0.00}  equippedItems: {items}");
                sb.AppendLine($"augments: {augStr}");
                sb.AppendLine($"route: {routeStr}");
                sb.AppendLine($"--- 敗北時の盤面（全滅後＝空のことが多い） ---");
                sb.AppendLine($"finalSynergies: {synStr}");
                sb.AppendLine($"finalRoster(+items): {roster}");
                sb.AppendLine($"--- 負けた戦闘の編成（戦闘開始時スナップショット） ---");
                sb.AppendLine($"lostCompWave: w{lastFightWave}  Lv{lastFightLevel}  placed{lastFightPlaced}  items{lastFightItems}");
                sb.AppendLine($"lostCompSynergies: {lastFightSyn}");
                sb.AppendLine($"lostCompRoster(+items): {(string.IsNullOrEmpty(lastFightRoster) ? "(編成前に敗北/未記録)" : lastFightRoster)}");
                sb.AppendLine($"likelyCauses: {string.Join(" | ", causes)}");
                sb.AppendLine($"earlyLossStreak: {earlyLossStreak}");

                string root = Directory.GetParent(Application.dataPath).FullName;
                string dumpsRoot = Path.Combine(root, "AutoPlayDumps");
                Directory.CreateDirectory(dumpsRoot);

                // まず累積学習ログ（1行/敗北）を確実に残す。Claude/開発者が傾向を見るため。
                string learnLog = Path.Combine(dumpsRoot, "gameover_analysis.log");
                // 学習ログ（1行/敗北）。負けた編成（戦闘開始時スナップショット）を主役に記録する。
                File.AppendAllText(learnLog,
                    $"{DateTime.Now:HH:mm:ss}\t{currentConfig.hero}\tch{currentConfig.chapter}\tlostAtWave{wave}/{waves}"
                    + $"\tLv{lastFightLevel}\tgold{gold}"
                    + $"\tlostComp[placed{lastFightPlaced},items{lastFightItems},syn:{lastFightSyn}]"
                    + $"\troster[{(string.IsNullOrEmpty(lastFightRoster) ? "(未記録)" : lastFightRoster)}]"
                    + $"\taug[{augStr}]\troute[{routeStr}]"
                    + $"\tcause[{string.Join(",", causes)}]\n");

                // 詳細スナップショット。
                string dir = Path.Combine(dumpsRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_gameover");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "analysis.txt"), sb.ToString());
                File.WriteAllText(Path.Combine(dir, "log.txt"), string.Join("\n", logRing));
                DumpsWritten++;
                Debug.LogWarning($"[AutoPlay] gameover analysis: wave{wave}/{waves} causes={string.Join(",", causes)}");
            }
            catch (Exception e) { Debug.LogError("[AutoPlay] analyze failed: " + e.Message); }
        }

        // 章クリア時の分析。clear_analysis.log（1行/クリア）＋詳細スナップショットを残す。Tier表作成の材料。
        private void AnalyzeClear(GameManager gm)
        {
            try
            {
                if (lastClearChapter == currentConfig.chapter) return; // 同章二重記録防止。
                lastClearChapter = currentConfig.chapter;

                int wave = gm.DebugCurrentWaveNumber, waves = gm.DebugWaveCount;
                int level = PlayerData.Instance != null ? PlayerData.Instance.Level : -1;
                int gold = PlayerData.Instance != null ? PlayerData.Instance.Money : -1;
                int placed = gm.PlacedTeam1Count, limit = gm.PlacementLimit;

                var board = FindObjectsOfType<BaseEntity>()
                    .Where(e => e != null && e.Team == Team.Team1 && !e.IsCore && e.IsOnBoard).ToList();
                int items = board.Sum(e => e.EquippedItems.Count);
                float avgStar = board.Count > 0 ? (float)board.Sum(e => e.StarLevel) / board.Count : 0f;
                string roster = BoardRosterStr(board);
                string augStr = OwnedAugmentsStr(gm);
                string routeStr = RouteStr();

                string synStr = "(none)";
                try
                {
                    if (SynergyManager.Instance != null)
                    {
                        var act = SynergyManager.Instance.GetActiveSynergies();
                        if (act != null && act.Count > 0)
                            synStr = string.Join(", ", act.Select(kv => $"{kv.Key}:T{kv.Value}"));
                    }
                }
                catch { synStr = "(synergy-read-failed)"; }

                string root = Directory.GetParent(Application.dataPath).FullName;
                string dumpsRoot = Path.Combine(root, "AutoPlayDumps");
                Directory.CreateDirectory(dumpsRoot);

                // 累積クリアログ（1行/クリア）。hero/章/勝因キー要素を集約。Tier集計しやすいTSV。
                File.AppendAllText(Path.Combine(dumpsRoot, "clear_analysis.log"),
                    $"{DateTime.Now:HH:mm:ss}\t{currentConfig.hero}\tch{currentConfig.chapter}\tCLEAR\tLv{level}\tgold{gold}"
                    + $"\tplaced{placed}/{limit}\tavg★{avgStar:0.0}\tsyn[{synStr}]\titems{items}"
                    + $"\taug[{augStr}]\troute[{routeStr}]\troster[{roster}]\n");

                var sb = new StringBuilder();
                sb.AppendLine("# AutoPlay CHAPTER CLEAR Analysis");
                sb.AppendLine($"time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"config: hero={currentConfig.hero} chapter={currentConfig.chapter}  RESULT=CLEAR");
                sb.AppendLine($"level: {level}  gold: {gold}  placed: {placed}/{limit}  avgStar: {avgStar:0.00}  items: {items}");
                sb.AppendLine($"activeSynergies: {synStr}");
                sb.AppendLine($"augments: {augStr}");
                sb.AppendLine($"route: {routeStr}");
                sb.AppendLine($"winningBoard(+items): {roster}");

                string dir = Path.Combine(dumpsRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_clear");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "clear.txt"), sb.ToString());
                Debug.Log($"[AutoPlay] CLEAR ch{currentConfig.chapter} syn={synStr}");
            }
            catch (Exception e) { Debug.LogError("[AutoPlay] analyze-clear failed: " + e.Message); }
        }

        // 盤面ロスター（ユニット★＋装備アイテム名）。
        private string BoardRosterStr(List<BaseEntity> board)
        {
            if (board == null || board.Count == 0) return "(empty)";
            return string.Join(", ", board.Select(e =>
            {
                string its = e.EquippedItems.Count > 0
                    ? "{" + string.Join("+", e.EquippedItems.Select(it => it != null ? it.displayName : "?")) + "}"
                    : "";
                return $"{e.UnitId}★{e.StarLevel}{its}";
            }));
        }
        // 取得オーグメント（名前[Tier]）。
        private string OwnedAugmentsStr(GameManager gm)
        {
            if (gm == null || gm.OwnedAugments == null || gm.OwnedAugments.Count == 0) return "(none)";
            return string.Join(", ", gm.OwnedAugments.Where(a => a != null).Select(a => $"{a.NameEn}[{a.Tier}]"));
        }
        // 選んだ進路（ウェーブ:ラベル を順に）。
        private string RouteStr() => routePicks.Count > 0 ? string.Join(" > ", routePicks) : "(none)";

        // 戦闘開始直前の編成を記録（敗北時は全滅で盤面が空になるため、ここで「負けた編成」を残す）。
        private void CaptureFightSnapshot(GameManager gm)
        {
            try
            {
                var board = FindObjectsOfType<BaseEntity>()
                    .Where(e => e != null && e.Team == Team.Team1 && !e.IsCore && e.IsOnBoard).ToList();
                if (board.Count == 0) return; // 空なら直前の有効な編成を保持（上書きしない）。
                lastFightRoster = BoardRosterStr(board);
                lastFightItems = board.Sum(e => e.EquippedItems.Count);
                lastFightPlaced = board.Count;
                lastFightLevel = PlayerData.Instance != null ? PlayerData.Instance.Level : -1;
                lastFightWave = gm.DebugCurrentWaveNumber;
                lastFightAug = OwnedAugmentsStr(gm);
                try
                {
                    if (SynergyManager.Instance != null)
                    {
                        var a = SynergyManager.Instance.GetActiveSynergies();
                        lastFightSyn = (a != null && a.Count > 0) ? string.Join(", ", a.Select(kv => $"{kv.Key}:T{kv.Value}")) : "(none)";
                    }
                }
                catch { }
            }
            catch { }
        }

        // ===== Dump =====
        private void WriteDump(string reason, string detail)
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName; // プロジェクト直下
                string dir = Path.Combine(root, "AutoPlayDumps",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + reason);
                Directory.CreateDirectory(dir);

                var gm = GameManager.Instance;
                var sb = new StringBuilder();
                sb.AppendLine("# AutoPlay Dump");
                sb.AppendLine($"time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"reason: {reason}");
                sb.AppendLine($"detail: {detail}");
                sb.AppendLine($"config: hero={currentConfig.hero} chapter={currentConfig.chapter}");
                sb.AppendLine($"runStats: runs={RunsDone} clear={Clears} over={GameOvers} stuck={StuckRuns}");
                sb.AppendLine($"scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                sb.AppendLine($"frameSinceRunStart: {Time.frameCount - runStartFrame}");
                if (gm != null)
                {
                    sb.AppendLine($"inRound: {gm.IsRoundInProgress}");
                    sb.AppendLine($"wave: {gm.DebugCurrentWaveNumber}/{gm.DebugWaveCount}");
                    sb.AppendLine($"placedTeam1: {gm.PlacedTeam1Count}");
                }
                sb.AppendLine($"money: {(PlayerData.Instance != null ? PlayerData.Instance.Money : -1)}");
                sb.AppendLine($"lastSignature: {lastSignature}");
                File.WriteAllText(Path.Combine(dir, "report.txt"), sb.ToString());

                File.WriteAllText(Path.Combine(dir, "log.txt"), string.Join("\n", logRing));
                DumpsWritten++;
                Debug.LogWarning($"[AutoPlay] dump written: {dir}");
            }
            catch (Exception e)
            {
                Debug.LogError("[AutoPlay] dump failed: " + e.Message);
            }
        }
    }
}
#endif
