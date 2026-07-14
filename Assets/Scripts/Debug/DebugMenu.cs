// DESIGN_handoff_claudecode.md D-1: 開発用デバッグメニュー。
// 製品ビルドではコンパイル自体されません（#if UNITY_EDITOR || DEVELOPMENT_BUILD）。
// このファイル1つ削除すれば全機能が完全に消える設計です。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace AutoChessBossRush.DebugTools
{
    // F8 トグル / 画面右上に小さなパネルを出すデバッグメニュー。
    // RuntimeInitializeOnLoadMethod で自動生成されるので、シーン配置不要。
    public class DebugMenu : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F8;
        private static DebugMenu instance;
        private bool open;
        private Vector2 unitScroll;
        private Vector2 itemScroll;
        private Vector2 roundScroll;
        private bool showUnitList;
        private bool showItemList;
        private bool showRoundList;
        private string unitFilter = string.Empty;
        private string itemFilter = string.Empty;
        private int debugClearChapter = 1; // 章クリア扱いにする対象章。

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
                return;
            GameObject host = new GameObject("DebugMenu", typeof(DebugMenu));
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                open = !open;
        }

        private void OnGUI()
        {
            if (!open)
            {
                GUI.Label(new Rect(Screen.width - 220f, 4f, 216f, 22f), "DEBUG [F8 to open]");
                return;
            }

            float w = 360f;
            float x = Screen.width - w - 8f;
            float y = 8f;
            GUILayout.BeginArea(new Rect(x, y, w, Mathf.Min(Screen.height - 32f, 620f)), GUI.skin.box);
            GUILayout.Label($"<b>DEBUG MENU</b> (F8 to close)");

            // --- お金 ---
            GUILayout.Space(4f);
            GUILayout.Label("Money");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10")) AddMoney(10);
            if (GUILayout.Button("+50")) AddMoney(50);
            if (GUILayout.Button("+100")) AddMoney(100);
            if (GUILayout.Button("+500")) AddMoney(500);
            GUILayout.EndHorizontal();

            // --- オーグメント ---
            GUILayout.Space(4f);
            GUILayout.Label("Force Augment Selection");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Silver")) ForceAugment(AugmentTier.Silver);
            if (GUILayout.Button("Gold")) ForceAugment(AugmentTier.Gold);
            if (GUILayout.Button("Prism")) ForceAugment(AugmentTier.Prism);
            GUILayout.EndHorizontal();

            GameManager gm = GameManager.Instance;

            // --- 無敵トグル ---
            GUILayout.Space(4f);
            if (gm != null)
            {
                GUILayout.BeginHorizontal();
                gm.DebugPlayerInvincible = GUILayout.Toggle(gm.DebugPlayerInvincible, " 味方無敵");
                gm.DebugEnemyInvincible = GUILayout.Toggle(gm.DebugEnemyInvincible, " 敵無敵");
                GUILayout.EndHorizontal();
            }

            // --- ラウンド進行 ---
            GUILayout.Space(4f);
            if (gm != null)
            {
                GUILayout.Label($"Round  (現在 {gm.DebugCurrentWaveNumber} / {gm.DebugWaveCount})");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("即勝利 (Instant Win)")) gm.DebugInstantWinRound();
                if (GUILayout.Button(showRoundList ? "Hide Jump ▲" : "Round Jump ▼")) showRoundList = !showRoundList;
                GUILayout.EndHorizontal();
                if (showRoundList) DrawRoundList(gm);
            }

            // --- 報酬UI強制表示 ---
            GUILayout.Space(4f);
            GUILayout.Label("Force Reward UI");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("アイテム選択")) { if (gm != null) gm.DebugForceItemReward(); }
            if (GUILayout.Button("強化タイル選択")) { if (gm != null) gm.DebugForceBuffTileReward(); }
            GUILayout.EndHorizontal();

            // --- オートプレイ（自動周回テスト） ---
            GUILayout.Space(4f);
            DrawAutoPlay();

            // --- ヒーロー熟練度XP ---
            GUILayout.Space(4f);
            DrawHeroMastery();

            // --- 章クリア扱い ---
            GUILayout.Space(4f);
            DrawChapterClear();

            // --- ユニット生成 ---
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(showUnitList ? "Hide Units ▲" : "Spawn Unit ▼", GUILayout.Width(150f)))
                showUnitList = !showUnitList;
            unitFilter = GUILayout.TextField(unitFilter ?? string.Empty, GUILayout.MinWidth(60f));
            GUILayout.EndHorizontal();
            if (showUnitList)
                DrawUnitList();

            // --- アイテム ---
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(showItemList ? "Hide Items ▲" : "Give Item ▼", GUILayout.Width(150f)))
                showItemList = !showItemList;
            itemFilter = GUILayout.TextField(itemFilter ?? string.Empty, GUILayout.MinWidth(60f));
            GUILayout.EndHorizontal();
            if (showItemList)
                DrawItemList();

            GUILayout.EndArea();
        }

        private static void AddMoney(int amount)
        {
            if (PlayerData.Instance != null)
                PlayerData.Instance.AddMoney(amount);
            else
                Debug.LogWarning("[DebugMenu] PlayerData.Instance is null; cannot add money.");
        }

        private static void ForceAugment(AugmentTier tier)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.DebugShowAugmentSelection(tier);
            else
                Debug.LogWarning("[DebugMenu] GameManager.Instance is null; cannot force augment.");
        }

        private void DrawRoundList(GameManager gm)
        {
            int count = gm.DebugWaveCount;
            if (count <= 0)
            {
                GUILayout.Label("(no waves in current chapter)");
                return;
            }
            int current = gm.DebugCurrentWaveNumber - 1;
            roundScroll = GUILayout.BeginScrollView(roundScroll, GUILayout.Height(180f));
            for (int i = 0; i < count; i++)
            {
                string mark = i == current ? "▶ " : "   ";
                if (GUILayout.Button($"{mark}{gm.DebugWaveLabel(i)}"))
                    gm.DebugJumpToWave(i);
            }
            GUILayout.EndScrollView();
        }

        // オートプレイ・ハーネスの操作。標準ヒューリスティックで自動周回し、進行不能/例外でDumpを残す。
        private void DrawAutoPlay()
        {
            var ap = AutoPlayHarness.EnsureExists();
            GUILayout.Label($"AutoPlay: {(ap.Running ? "RUNNING" : "idle")}  ({ap.LastStatus})");
            GUILayout.Label($"runs={ap.RunsDone} clear={ap.Clears} over={ap.GameOvers} stuck={ap.StuckRuns} dumps={ap.DumpsWritten}");
            GUILayout.BeginHorizontal();
            if (!ap.Running)
            {
                if (GUILayout.Button("▶ 1周")) ap.StartBatch(1);
                if (GUILayout.Button("▶ 5周")) ap.StartBatch(5);
                if (GUILayout.Button("▶ 20周")) ap.StartBatch(20);
            }
            else
            {
                if (GUILayout.Button("■ 停止")) ap.Stop();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("速度", GUILayout.Width(30f));
            ap.speed = Mathf.Round(GUILayout.HorizontalSlider(ap.speed, 1f, 16f));
            GUILayout.Label("x" + ap.speed.ToString("0"), GUILayout.Width(34f));
            GUILayout.Label("最大章", GUILayout.Width(44f));
            if (GUILayout.Button("-", GUILayout.Width(24f))) ap.maxChapter = Mathf.Max(1, ap.maxChapter - 1);
            GUILayout.Label(ap.maxChapter.ToString(), GUILayout.Width(20f));
            if (GUILayout.Button("+", GUILayout.Width(24f))) ap.maxChapter = Mathf.Min(20, ap.maxChapter + 1);
            GUILayout.EndHorizontal();
        }

        // ヒーロー熟練度XPを強制付与する。対象は現在の主人公。
        private void DrawHeroMastery()
        {
            var sm = AutoChessBossRush.Save.SaveManager.Instance;
            if (sm == null)
            {
                GUILayout.Label("Hero Mastery (SaveManager not ready)");
                return;
            }
            string heroId = sm.GetHeroUnitId();
            if (string.IsNullOrEmpty(heroId))
            {
                GUILayout.Label("Hero Mastery (no active hero)");
                return;
            }
            int lv = sm.GetHeroMasteryLevel(heroId);
            int toNext = sm.GetHeroMasteryXpToNext(heroId);
            GUILayout.Label($"Hero Mastery: {heroId}  Lv{lv} (next {toNext}XP)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10XP")) sm.AddHeroMasteryXp(heroId, 10);
            if (GUILayout.Button("+30XP")) sm.AddHeroMasteryXp(heroId, 30);
            if (GUILayout.Button("+100XP")) sm.AddHeroMasteryXp(heroId, 100);
            if (GUILayout.Button("MAX")) sm.AddHeroMasteryXp(heroId, AutoChessBossRush.Save.SaveManager.HeroMasteryCumulativeXp(AutoChessBossRush.Save.SaveManager.HeroMasteryMaxLevel));
            GUILayout.EndHorizontal();
        }

        // 指定章を「クリア扱い」にする（次章・解放ヒーローのアンロック確認用）。
        private void DrawChapterClear()
        {
            var sm = AutoChessBossRush.Save.SaveManager.Instance;
            if (sm == null)
            {
                GUILayout.Label("Chapter Clear (SaveManager not ready)");
                return;
            }
            bool already = sm.GetChapter(debugClearChapter).cleared;
            GUILayout.Label($"Chapter Clear  (第{debugClearChapter}章: {(already ? "済" : "未")})");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(28f))) debugClearChapter = Mathf.Max(1, debugClearChapter - 1);
            if (GUILayout.Button("▶", GUILayout.Width(28f))) debugClearChapter = Mathf.Min(20, debugClearChapter + 1);
            if (GUILayout.Button($"第{debugClearChapter}章をクリア扱い")) sm.RecordChapterResult(debugClearChapter, 0, 0f, true);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("全章クリア扱い (1〜20)"))
                for (int c = 1; c <= 20; c++) sm.RecordChapterResult(c, 0, 0f, true);
        }

        private void DrawUnitList()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.entitiesDatabase == null || gm.entitiesDatabase.allEntities == null)
            {
                GUILayout.Label("(entities database unavailable)");
                return;
            }

            List<EntitiesDatabaseSO.EntityData> all = gm.entitiesDatabase.allEntities;
            unitScroll = GUILayout.BeginScrollView(unitScroll, GUILayout.Height(160f));
            for (int i = 0; i < all.Count; i++)
            {
                EntitiesDatabaseSO.EntityData data = all[i];
                if (data.prefab == null || string.IsNullOrEmpty(data.name))
                    continue;
                if (!string.IsNullOrEmpty(unitFilter) && data.name.IndexOf(unitFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (GUILayout.Button($"cost{data.cost} - {data.name}"))
                {
                    gm.OnEntityBought(data);
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawItemList()
        {
            IReadOnlyList<ItemData> items = ItemCatalog.AllItems;
            if (items == null || items.Count == 0)
            {
                GUILayout.Label("(item catalog empty)");
                return;
            }
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                GUILayout.Label("(GameManager not ready)");
                return;
            }

            itemScroll = GUILayout.BeginScrollView(itemScroll, GUILayout.Height(160f));
            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                if (item == null || string.IsNullOrEmpty(item.id))
                    continue;
                if (!string.IsNullOrEmpty(itemFilter) && item.id.IndexOf(itemFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (GUILayout.Button(item.id))
                {
                    gm.ReturnItemToBench(item);
                }
            }
            GUILayout.EndScrollView();
        }
    }
}
#endif
