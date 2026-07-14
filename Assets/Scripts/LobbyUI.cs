using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using AutoChessBossRush.Save;

// タイトル/ロビー画面です。プログラム生成（シーンは LobbyScene）。Duelyst 風の3画面構成：
//  ①タイトル画面 : 左寄せのテキストメニュー（PLAY / オプション / 終了）
//  ②ロビー選択   : 大型モードカード（チャプター / コア戦 / ユニット編成＝コレクション・ショップ選抜ハブ）
//  ③チャプター選択: Assets/Resources/Play/Chapter の gate 画像でチャプターを選ぶ
// 章は全20章を進行度で順次解放（N-1クリアで解放）。ユニット編成は CollectionHubUI を開く（R4-collection-hub）。
// 章を選ぶと GameManager.PendingStartChapter をセットして GameScene へ遷移します。
public class LobbyUI : MonoBehaviour
{
    private const int SortingOrder = 25050; // 16bit short上限(32767)内。
    private const int PlayableChapterCount = 20; // 20章（1=Caliber/2=rook/3=sister/4-6=Magmar/7-9=Abyssian/10-12=Vetruvian/13-18=Mechaz0r/19=Hydrax/20=Arcana）
    private const string LobbySceneName = "LobbyScene";
    private const string GameSceneName = "GameScene";

    public static LobbyUI Instance { get; private set; }

    private Canvas localCanvas;
    private bool pausedByLobby;
    private bool uiBuilt;
    private GameObject titleView;
    private GameObject slotSelectView;
    private Transform slotRow;
    private GameObject slotConfirmOverlay;
    // タイトルの「はじめから/つづきから」でスロット選択画面の挙動を切り替える。
    private enum SlotSelectMode { NewGame, Continue }
    private SlotSelectMode slotSelectMode = SlotSelectMode.Continue;
    private TextMeshProUGUI slotSelectTitle;
    private TextMeshProUGUI slotConfirmMsg;
    private TextMeshProUGUI slotConfirmYesLabel;
    private System.Action slotConfirmAction;
    private GameObject collectionView;
    private Transform collectionGrid;
    // R1-collection: 図鑑専用の全画面詳細ビュー。
    private GameObject collectionDetailView;
    // R3-hero-select: セーブ作成時の主人公（ヒーロー）選択ビュー。
    private GameObject heroSelectView;
    private static readonly string[] HeroChoiceIds = { "HeroAldin", "HeroKagachi", "HeroVesna" };
    private Image cdSpriteImage;
    private Image cdIconImage;
    private bool cdAnimMode = true;       // true=スプライト(アニメ) / false=ショップアイコン
    private bool cdShowAbilityNext;       // クリックで attack→ability を交互再生
    private Sprite cdCurrentIcon;
    private Image cdAnimTab, cdIconTab;
    private TextMeshProUGUI cdName, cdNameEn, cdCostTag, cdChapterTag, cdActionHint;
    private readonly List<string> cdMotions = new List<string>();
    private int cdMotionIndex;
    private GameObject cdIconOverlay;
    private Image cdIconBig;
    private Transform cdSynergyRow;
    private TextMeshProUGUI cdAffLv, cdAffBonus, cdAffCount;
    private TextMeshProUGUI cdHp, cdAtk, cdAtkSpd, cdRange, cdDr, cdMana;
    private TextMeshProUGUI cdSkillTitle, cdSkillBody, cdFlavor;
    private GameObject cdPreviewEntityObj;
    private CollectionBossAnimator cdAnimator;
    private readonly List<string> cdOwnedIds = new List<string>();
    private int cdIndex;
    // 図鑑グリッドの各セル用に盤外で生成したボス実体（待機アニメをセルへミラー）。再構築・離脱時に破棄。
    private readonly List<GameObject> collectionPreviewEntities = new List<GameObject>();
    // R3-hero-scale Phase2/3: ヒーロー育成（下にキャラ列＋上に詳細パネル）。
    private GameObject heroUpgradeView;
    private Transform heroRosterRow;           // 下部のキャラ選択列（5枚カルーセル・毎回再構築）。
    private Image heroDetailArt;               // 詳細：大ポートレート（未解放はシルエット）。
    private TextMeshProUGUI heroDetailName;     // 詳細：名前。
    private TextMeshProUGUI heroDetailInfo;     // 詳細：ロール/シナジー/熟練度 or 解放条件。
    private TextMeshProUGUI heroDetailUseText;  // 詳細：使用ボタンのラベル。
    private Button heroDetailUseBtn;            // 詳細：この主人公にするボタン。
    private int heroCarouselIndex;              // カルーセルの中心＝閲覧中ヒーローの roster インデックス。
    private readonly Button[] heroUltVarBtn = new Button[3];           // 必殺バリアント(基本/A/B)選択ボタン。
    private readonly TextMeshProUGUI[] heroUltVarText = new TextMeshProUGUI[3];
    private RectTransform heroMasteryFill;     // 熟練度ゲージの中身（次Lvまでの進捗）。
    private TextMeshProUGUI heroMasteryGaugeText;
    private GameObject heroMasteryGaugeRoot;   // 未解放時に隠すためのゲージ親。
    private bool heroUpgradeFromPrep;          // 育成画面を準備画面から開いたか（戻り先の分岐）。
    private GameObject mainLobbyView; // R3: スロット選択後のメインロビー（セーブ別ハブ：プレイ/ヒーロー育成/図鑑）。
    private TextMeshProUGUI mainLobbyHeroText;
    private Image mainLobbyHeroArt; // 主人公の立ち絵（案A）。
    private GameObject lobbyView;
    private GameObject chapterView;
    // R3: チャプター選択後の出撃準備画面（将来＝ショップ編成の入口）。
    private GameObject chapterPrepView;
    private int chapterPrepChapter = 1;
    private Image chapterPrepHeroArt;
    private TextMeshProUGUI chapterPrepTitle;
    private TextMeshProUGUI chapterPrepHeroText;
    private Transform chapterCardsParent;
    private Transform modeRow;
    private readonly List<Transform> titleItems = new List<Transform>();
    private bool chapterCardsBuilt;

    private void Awake()
    {
        LocalizationManager.EnsureExists();
        Instance = this;
        EnsureUi();

        if (gameObject.scene.name == LobbySceneName)
        {
            SaveManager.EnsureExists();
            SettingsStore.ApplyAll();
            Time.timeScale = 1f;
            gameObject.SetActive(true);
            ShowInitialView();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // AttackEffectPlayer の自動BGM(AfterSceneLoad)は Awake より後に走り、ロビーBGMを上書きしてしまう。
        // そのため Start（bootstrap後）でロビー表示中なら確実にロビーBGMを再生し直す。
        if (gameObject.activeInHierarchy)
            PlayLobbyBgm();
    }

    public static LobbyUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        LobbyUI existing = FindObjectOfType<LobbyUI>(true);
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureUi();
            return existing;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject uiObject = new GameObject("LobbyUI", typeof(RectTransform), typeof(Image), typeof(LobbyUI));
        uiObject.transform.SetParent(canvas.transform, false);
        RectTransform rectTransform = uiObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Instance = uiObject.GetComponent<LobbyUI>();
        Instance.EnsureUi();
        Instance.gameObject.SetActive(false);
        return Instance;
    }

    public void ShowAsBootLobby()
    {
        pausedByLobby = true;
        Time.timeScale = 0f;
        Show();
    }

    public void Show()
    {
        EnsureUi();
        gameObject.SetActive(true);
        ShowInitialView();
    }

    // 起動/復帰時の初期ビュー。「ロビーへ戻る」で来た場合はメインロビー（ハブ）、それ以外はタイトル。
    private void ShowInitialView()
    {
        if (GameManager.ReturnToMainLobbyOnBoot)
        {
            GameManager.ReturnToMainLobbyOnBoot = false;
            // アクティブスロットの保存データを確実に読み込んでからハブを表示。
            SaveManager.EnsureExists();
            ShowMainLobby();
        }
        else
        {
            ShowTitle();
        }
    }

    public void Hide()
    {
        if (pausedByLobby)
        {
            Time.timeScale = 1f;
            pausedByLobby = false;
        }
        gameObject.SetActive(false);
    }

    // ================= ビュー切替 =================
    private void ShowTitle()
    {
        EnsureUi();
        PlayLobbyBgm();
        SetView(titleView);
    }

    // ロビー用BGM（メニュー曲）に切り替え。複数候補から最初に見つかったものをループ再生。
    private void PlayLobbyBgm()
    {
        AttackEffectPlayer.PlayBgm("music/mainmenu_v2c_looping", "music/music_mainmenu_lyonar", "music/music_collection");
    }

    private void ShowSlotSelect(SlotSelectMode mode)
    {
        EnsureUi();
        slotSelectMode = mode;
        bool ja = LocalizationManager.IsJapanese;
        if (slotSelectTitle != null)
            slotSelectTitle.text = mode == SlotSelectMode.NewGame
                ? (ja ? "はじめから — スロットを選択" : "NEW GAME — SELECT SLOT")
                : (ja ? "つづきから — スロットを選択" : "CONTINUE — SELECT SLOT");
        RefreshSlotCards();
        if (slotConfirmOverlay != null) slotConfirmOverlay.SetActive(false);
        SetView(slotSelectView);
    }

    // いずれかのスロットにセーブが存在するか。
    private bool HasAnySave()
    {
        SaveManager.EnsureExists();
        for (int slot = 0; slot < SaveManager.SlotCount; slot++)
            if (SaveManager.Instance.GetSlotInfo(slot).exists)
                return true;
        return false;
    }

    private void ShowLobby()
    {
        EnsureUi();
        SetView(lobbyView);
    }

    private void ShowChapters()
    {
        EnsureUi();
        BuildChapterCards();
        SetView(chapterView);
    }

    private void SetView(GameObject view)
    {
        if (titleView != null) titleView.SetActive(view == titleView);
        if (slotSelectView != null) slotSelectView.SetActive(view == slotSelectView);
        if (collectionView != null) collectionView.SetActive(view == collectionView);
        if (collectionDetailView != null) collectionDetailView.SetActive(view == collectionDetailView);
        if (heroSelectView != null) heroSelectView.SetActive(view == heroSelectView);
        if (heroUpgradeView != null) heroUpgradeView.SetActive(view == heroUpgradeView);
        if (mainLobbyView != null) mainLobbyView.SetActive(view == mainLobbyView);
        if (chapterPrepView != null) chapterPrepView.SetActive(view == chapterPrepView);
        if (lobbyView != null) lobbyView.SetActive(view == lobbyView);
        if (chapterView != null) chapterView.SetActive(view == chapterView);
        // 詳細ビューから離れたら、生成していたボス実体を片付ける。
        if (view != collectionDetailView)
            CleanupCollectionDetailEntity();
        // 図鑑グリッド／詳細のどちらでもなくなったら、グリッド用のプレビュー実体も破棄する。
        if (view != collectionView && view != collectionDetailView)
            CleanupCollectionPreviews();
        PlayEntrance(view);
    }

    // ビュー入場演出：フェード＋軽い拡大、対象コンテナの子をスタッガーでポップ。
    private void PlayEntrance(GameObject view)
    {
        if (view == null) return;
        CanvasGroup cg = view.GetComponent<CanvasGroup>();
        if (cg == null) cg = view.AddComponent<CanvasGroup>();
        cg.DOKill();
        cg.alpha = 0f;
        cg.DOFade(1f, 0.22f).SetUpdate(true);
        view.transform.localScale = Vector3.one * 0.985f;
        view.transform.DOScale(1f, 0.26f).SetEase(Ease.OutQuad).SetUpdate(true);

        if (view == titleView) StaggerPop(titleItems);
        else if (view == lobbyView && modeRow != null) StaggerPopChildren(modeRow);
    }

    private void StaggerPop(List<Transform> items)
    {
        for (int i = 0; i < items.Count; i++)
            PopOne(items[i], i);
    }

    private void StaggerPopChildren(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
            PopOne(parent.GetChild(i), i);
    }

    private void PopOne(Transform t, int index)
    {
        if (t == null) return;
        t.DOKill();
        t.localScale = Vector3.one * 0.82f;
        t.DOScale(1f, 0.32f).SetEase(Ease.OutBack).SetDelay(index * 0.06f).SetUpdate(true);
    }

    // ================= UI構築 =================
    private void EnsureUi()
    {
        EnsureInputCanvas();
        EnsureLobbyAudioAndCamera();
        EnsureBackground();
        if (uiBuilt)
            return;
        BuildTitleView();
        BuildSlotSelectView();
        BuildCollectionView();
        BuildCollectionDetailView();
        BuildHeroSelectView();
        BuildHeroUpgradeView();
        BuildMainLobbyView();
        BuildLobbyView();
        BuildChapterPrepView();
        BuildChapterView();
        uiBuilt = true;
    }

    // ロビー専用シーンにはカメラも AudioListener も無く、BGMが鳴らず「No cameras rendering」も出る。
    // 無ければ最小のカメラ＋AudioListener を用意する（UIはOverlayなので描画はそのまま）。
    private void EnsureLobbyAudioAndCamera()
    {
        if (Camera.allCamerasCount == 0)
        {
            GameObject camGo = new GameObject("LobbyCamera", typeof(Camera), typeof(AudioListener));
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 1f);
            cam.cullingMask = 0; // 何も描画しない（ロビーUIは ScreenSpaceOverlay で前面に出る）
            cam.orthographic = true;
        }
        else if (FindObjectOfType<AudioListener>() == null && Camera.main != null)
        {
            Camera.main.gameObject.AddComponent<AudioListener>();
        }
    }

    private void EnsureBackground()
    {
        Image root = GetComponent<Image>();
        if (root == null) root = gameObject.AddComponent<Image>();
        // 起動ごとに背景をランダムへ切り替える（Play/LobbyBg 配下を全読込）。無ければ従来背景。
        Sprite bg = null;
        Sprite[] pool = Resources.LoadAll<Sprite>("Play/LobbyBg");
        if (pool != null && pool.Length > 0)
            bg = pool[UnityEngine.Random.Range(0, pool.Length)];
        if (bg == null)
            bg = Resources.Load<Sprite>("Play/play_background");
        if (bg != null)
        {
            root.sprite = bg;
            root.type = Image.Type.Simple;
            root.color = new Color(0.80f, 0.82f, 0.86f, 1f);
        }
        else
        {
            root.color = new Color(0.03f, 0.05f, 0.09f, 1f);
        }
        root.raycastTarget = true;
    }

    // ---------- ①タイトル画面 ----------
    private void BuildTitleView()
    {
        titleView = NewFullRect("TitleView");
        bool ja = LocalizationManager.IsJapanese;

        // ロゴ（左上）。タイトルは "Abyssal Gambit"（"Auto Chess" 商標連想を避ける／STEAM_READINESS_STANDARDS §5-②）。
        CreateLabel(titleView.transform, "Logo", "ABYSSAL\nGAMBIT",
            new Vector2(0f, 0.78f), new Vector2(0.6f, 0.96f), 54f, FontStyles.Bold,
            new Color(0.97f, 0.9f, 0.6f), TextAlignmentOptions.TopLeft, true);

        // 左寄せメニュー（はじめから / つづきから / オプション / 終了）。
        // 「つづきから」はセーブが1つでも存在する時のみ表示（初回起動では非表示）。indexは詰める。
        int mi = 0;
        CreateTitleMenuItem(titleView.transform, "NewGameItem", ja ? "はじめから" : "NEW GAME", mi++, () =>
        {
            AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
            ShowSlotSelect(SlotSelectMode.NewGame);
        });
        if (HasAnySave())
        {
            CreateTitleMenuItem(titleView.transform, "ContinueItem", ja ? "つづきから" : "CONTINUE", mi++, () =>
            {
                AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
                ShowSlotSelect(SlotSelectMode.Continue);
            });
        }
        // 図鑑・ヒーロー育成はセーブデータ別なので、タイトルではなくスロット選択後のメインロビーに置く。
        CreateTitleMenuItem(titleView.transform, "OptionItem", ja ? "オプション" : "OPTIONS", mi++, () =>
        {
            AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
            SettingsPanelUI.EnsureExists().Show();
        });
        CreateTitleMenuItem(titleView.transform, "QuitItem", ja ? "ゲーム終了" : "EXIT", mi++, () =>
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        });
    }

    private void CreateTitleMenuItem(Transform parent, string name, string label, int index, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(120f, 80f - index * 96f);
        rect.sizeDelta = new Vector2(560f, 78f);

        Image hit = go.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f); // 透明だがクリック判定用
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = hit;
        btn.onClick.AddListener(onClick);
        // ホバーで明るく
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        cb.pressedColor = new Color(1f, 1f, 1f, 0.2f);
        btn.colors = cb;

        TextMeshProUGUI text = CreateLabel(go.transform, "Label", label, Vector2.zero, Vector2.one,
            46f, FontStyles.Bold, new Color(0.95f, 0.97f, 1f), TextAlignmentOptions.Left, false);
        text.raycastTarget = false;

        go.AddComponent<ButtonJuice>();
        titleItems.Add(go.transform);
    }

    // ---------- スロット選択画面（はじめから/つづから → ここ → ロビー） ----------
    // スロットごとのヘッダ風景（Play/LobbyBg を流用）。
    private static readonly string[] SlotSceneBg =
    { "Play/LobbyBg/lobby_storm", "Play/LobbyBg/lobby_woods", "Play/LobbyBg/lobby_nightfall" };

    private void BuildSlotSelectView()
    {
        slotSelectView = NewFullRect("SlotSelectView");
        slotSelectView.SetActive(false);
        bool ja = LocalizationManager.IsJapanese;

        slotSelectTitle = CreateLabel(slotSelectView.transform, "Title", ja ? "スロットを選択" : "SELECT SLOT",
            new Vector2(0f, 0.86f), new Vector2(1f, 0.96f), 40f, FontStyles.Bold,
            new Color(0.97f, 0.9f, 0.6f), TextAlignmentOptions.Center, false);

        CreateBackButton(slotSelectView.transform, () => ShowTitle());

        GameObject rowGo = new GameObject("SlotRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(slotSelectView.transform, false);
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, -10f);
        rowRect.sizeDelta = new Vector2(980f, 420f);
        HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 28f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        slotRow = rowGo.transform;

        BuildSlotConfirmOverlay();
    }

    private void RefreshSlotCards()
    {
        if (slotRow == null) return;
        for (int i = slotRow.childCount - 1; i >= 0; i--)
            Destroy(slotRow.GetChild(i).gameObject);
        SaveManager.EnsureExists();
        for (int slot = 0; slot < SaveManager.SlotCount; slot++)
            CreateSlotCard(slot);
    }

    private void CreateSlotCard(int slot)
    {
        bool ja = LocalizationManager.IsJapanese;
        SaveManager.SlotInfo info = SaveManager.Instance.GetSlotInfo(slot);

        GameObject card = new GameObject($"Slot{slot}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        card.transform.SetParent(slotRow, false);
        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredWidth = 300f; le.preferredHeight = 400f;

        // 背景：パネル素材（frame_quest = UI/Panels/result_panel, 9-slice）。無ければ単色。
        Image bg = card.GetComponent<Image>();
        Sprite panelSprite = Resources.Load<Sprite>("UI/Panels/result_panel");
        if (panelSprite != null)
        {
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = info.exists ? new Color(0.86f, 0.91f, 1f, 1f) : new Color(0.56f, 0.59f, 0.66f, 1f);
        }
        else
        {
            bg.color = info.exists ? new Color(0.10f, 0.16f, 0.26f, 0.94f) : new Color(0.07f, 0.09f, 0.13f, 0.9f);
        }

        Button btn = card.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => SelectSlot(slot));
        card.AddComponent<ButtonJuice>();
        // つづから：データの無いスロットは選べない（淡色＋無効）。
        bool continueDisabled = slotSelectMode == SlotSelectMode.Continue && !info.exists;
        if (continueDisabled)
        {
            btn.interactable = false;
            bg.color = new Color(bg.color.r * 0.7f, bg.color.g * 0.7f, bg.color.b * 0.7f, 0.7f);
        }

        // 上部にシーン画像（スロットごとに別の風景）＋下半分に暗幕でタイトルを読みやすく。
        GameObject scene = new GameObject("Scene", typeof(RectTransform), typeof(Image));
        scene.transform.SetParent(card.transform, false);
        RectTransform scRect = scene.GetComponent<RectTransform>();
        scRect.anchorMin = new Vector2(0.085f, 0.595f); scRect.anchorMax = new Vector2(0.915f, 0.93f);
        scRect.offsetMin = Vector2.zero; scRect.offsetMax = Vector2.zero;
        Image sceneImg = scene.GetComponent<Image>();
        Sprite sceneSprite = Resources.Load<Sprite>(SlotSceneBg[slot % SlotSceneBg.Length]);
        if (sceneSprite != null) { sceneImg.sprite = sceneSprite; sceneImg.preserveAspect = false; }
        sceneImg.color = info.exists ? new Color(0.95f, 0.96f, 1f, 1f) : new Color(0.42f, 0.44f, 0.48f, 1f);
        sceneImg.raycastTarget = false;
        GameObject scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
        scrim.transform.SetParent(scene.transform, false);
        RectTransform scrimRect = scrim.GetComponent<RectTransform>();
        scrimRect.anchorMin = Vector2.zero; scrimRect.anchorMax = new Vector2(1f, 0.48f);
        scrimRect.offsetMin = Vector2.zero; scrimRect.offsetMax = Vector2.zero;
        Image scrimImg = scrim.GetComponent<Image>();
        scrimImg.color = new Color(0f, 0f, 0f, 0.5f);
        scrimImg.raycastTarget = false;

        bool active = SaveManager.Instance.ActiveSlot == slot;
        CreateLabel(card.transform, "Header", (ja ? "スロット " : "SLOT ") + (slot + 1) + (active ? "  ★" : ""),
            new Vector2(0.06f, 0.60f), new Vector2(0.94f, 0.72f), 26f, FontStyles.Bold,
            new Color(0.98f, 0.98f, 1f), TextAlignmentOptions.Center, false);

        string summary;
        if (!info.exists)
        {
            summary = slotSelectMode == SlotSelectMode.Continue
                ? (ja ? "データなし" : "NO DATA")
                : (ja ? "空き\n\nここに作成" : "EMPTY\n\nCreate here");
        }
        else
        {
            string when = info.lastSavedUnixSec > 0
                ? System.DateTimeOffset.FromUnixTimeSeconds(info.lastSavedUnixSec).LocalDateTime.ToString("yyyy/MM/dd HH:mm")
                : "-";
            summary = ja
                ? $"到達章: {info.highestClearedChapter}\nベスト: {info.bestScore}\n仲間: {info.bossAllyCount}\n保存: {when}"
                : $"Chapter: {info.highestClearedChapter}\nBest: {info.bestScore}\nAllies: {info.bossAllyCount}\nSaved: {when}";
            if (slotSelectMode == SlotSelectMode.NewGame)
                summary += ja ? "\n\n⚠ クリックで上書き" : "\n\n⚠ Click to overwrite";
        }
        CreateLabel(card.transform, "Summary", summary,
            new Vector2(0.12f, 0.2f), new Vector2(0.88f, 0.56f), 19f, FontStyles.Normal,
            new Color(0.12f, 0.16f, 0.24f), TextAlignmentOptions.Top, true);

        if (info.exists)
        {
            GameObject del = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
            del.transform.SetParent(card.transform, false);
            RectTransform delRect = del.GetComponent<RectTransform>();
            delRect.anchorMin = delRect.anchorMax = new Vector2(0.5f, 0f);
            delRect.pivot = new Vector2(0.5f, 0f);
            delRect.anchoredPosition = new Vector2(0f, 16f);
            delRect.sizeDelta = new Vector2(190f, 46f);
            Image delImg = del.GetComponent<Image>();
            delImg.color = new Color(0.45f, 0.12f, 0.14f, 0.95f);
            Button delBtn = del.GetComponent<Button>();
            delBtn.targetGraphic = delImg;
            delBtn.onClick.AddListener(() => ShowDeleteConfirm(slot));
            TextMeshProUGUI delLabel = CreateLabel(del.transform, "Label", ja ? "削除" : "DELETE",
                Vector2.zero, Vector2.one, 20f, FontStyles.Bold, new Color(1f, 0.9f, 0.9f), TextAlignmentOptions.Center, false);
            delLabel.raycastTarget = false;
        }
    }

    private void SelectSlot(int slot)
    {
        SaveManager.EnsureExists();
        AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
        bool ja = LocalizationManager.IsJapanese;
        SaveManager.SlotInfo info = SaveManager.Instance.GetSlotInfo(slot);

        if (slotSelectMode == SlotSelectMode.NewGame)
        {
            // 既存スロットは上書き確認を挟む。空きはそのまま新規開始。
            if (info.exists)
                ShowConfirm(
                    ja ? "このスロットに上書きして\n新しく始めますか？\n既存のデータは消えます。" : "Overwrite this slot and\nstart a new game?\nExisting data will be lost.",
                    ja ? "上書きして開始" : "OVERWRITE",
                    () => BeginNewGame(slot));
            else
                BeginNewGame(slot);
            return;
        }

        // つづから：データのあるスロットのみ。
        if (!info.exists) return;
        SaveManager.Instance.SetActiveSlot(slot);
        // 旧セーブで主人公が未選択なら一度だけヒーロー選択を挟む。通常はそのままメインロビーへ。
        if (string.IsNullOrEmpty(SaveManager.Instance.GetHeroUnitId()))
            ShowHeroSelect();
        else
            ShowMainLobby();
    }

    // 新規開始：選んだスロットを（既存なら消して）新規にし、ヒーロー選択からロビーへ。
    private void BeginNewGame(int slot)
    {
        SaveManager.EnsureExists();
        if (SaveManager.Instance.GetSlotInfo(slot).exists)
            SaveManager.Instance.DeleteSlot(slot);
        SaveManager.Instance.SetActiveSlot(slot);
        if (slotConfirmOverlay != null) slotConfirmOverlay.SetActive(false);
        ShowHeroSelect(); // 新規は必ずヒーロー選択 → ConfirmHeroSelection → ロビー
    }

    // ================= R3-hero-select: 主人公選択 =================
    private void ShowHeroSelect()
    {
        EnsureUi();
        SetView(heroSelectView);
    }

    // 選択中ヒーローをスロットに保存してロビーへ。
    private void ConfirmHeroSelection(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        SaveManager.EnsureExists();
        SaveManager.Instance.SetHeroUnitId(heroId);
        AttackEffectPlayer.PlayUiSfx("unit_buy");
        ShowMainLobby();
    }

    // 主人公選択ビュー：3体のヒーローカード（名前・ロール・必殺・「性能」）から1体を選ぶ。
    private void BuildHeroSelectView()
    {
        bool ja = LocalizationManager.IsJapanese;
        heroSelectView = NewFullRect("HeroSelectView");
        heroSelectView.SetActive(false);

        CreateLabel(heroSelectView.transform, "Title", ja ? "主人公を選択" : "CHOOSE YOUR HERO",
            new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.97f), 38f, FontStyles.Bold,
            new Color(0.96f, 0.9f, 0.55f), TextAlignmentOptions.Center, false);
        CreateLabel(heroSelectView.transform, "Sub", ja ? "このスロットの主人公になります（編成画面から後で変更可）" : "Becomes this slot's hero (changeable later from the board)",
            new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.875f), 17f, FontStyles.Normal,
            new Color(0.75f, 0.85f, 1f, 0.9f), TextAlignmentOptions.Center, false);

        // 3カードを横並びに配置（画面幅を3分割）。
        for (int i = 0; i < HeroChoiceIds.Length; i++)
        {
            float cx = 0.5f + (i - 1) * 0.31f; // 中央0.5、左右に0.31ずつ
            BuildHeroCard(heroSelectView.transform, HeroChoiceIds[i], cx, ja);
        }

        // 戻る（スロット選択へ）。未選択のままでもスロットにデータは作られないので安全。
        GameObject back = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
        back.transform.SetParent(heroSelectView.transform, false);
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f); br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, 24f); br.sizeDelta = new Vector2(220f, 48f);
        back.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.28f, 0.96f);
        back.GetComponent<Button>().onClick.AddListener(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); ShowSlotSelect(slotSelectMode); });
        CreateLabel(back.transform, "L", ja ? "戻る" : "BACK", Vector2.zero, Vector2.one, 20f, FontStyles.Bold,
            new Color(0.9f, 0.94f, 1f), TextAlignmentOptions.Center, false).raycastTarget = false;
    }

    private void BuildHeroCard(Transform parent, string heroId, float centerX, bool ja)
    {
        // DBからアイコン等を解決（未ビルド時は null 安全にフォールバック）。
        EntitiesDatabaseSO.EntityData data = default;
        bool hasData = false;
        if (GameManager.Instance != null && GameManager.Instance.entitiesDatabase != null && GameManager.Instance.entitiesDatabase.allEntities != null)
        {
            // このファイルは System.Linq を using していないため、手動ループで解決する（フル修飾の比較）。
            List<EntitiesDatabaseSO.EntityData> all = GameManager.Instance.entitiesDatabase.allEntities;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].prefab != null && string.Equals(all[i].name, heroId, System.StringComparison.OrdinalIgnoreCase))
                {
                    data = all[i];
                    hasData = true;
                    break;
                }
            }
        }

        GameObject card = new GameObject("HeroCard_" + heroId, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = new Vector2(centerX, 0.45f); cr.pivot = new Vector2(0.5f, 0.5f);
        cr.anchoredPosition = Vector2.zero; cr.sizeDelta = new Vector2(360f, 600f);
        Image cardBg = card.GetComponent<Image>();
        Sprite panelSprite = Resources.Load<Sprite>("UI/Panels/result_panel");
        if (panelSprite != null) { cardBg.sprite = panelSprite; cardBg.type = Image.Type.Sliced; cardBg.color = new Color(0.10f, 0.14f, 0.22f, 0.97f); }
        else cardBg.color = new Color(0.06f, 0.10f, 0.16f, 0.97f);

        // 立ち絵（ヒーローの見た目確認用）。統一素材(DialogArt)→アイコン→枠のみ の順で解決。
        Sprite art = DialogArt.Portrait(heroId);
        Sprite shown = art != null ? art : (hasData ? data.icon : null);
        GameObject ic = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        ic.transform.SetParent(card.transform, false);
        RectTransform icr = ic.GetComponent<RectTransform>();
        icr.anchorMin = new Vector2(0.06f, 0.45f); icr.anchorMax = new Vector2(0.94f, 0.965f);
        icr.offsetMin = Vector2.zero; icr.offsetMax = Vector2.zero;
        Image icImg = ic.GetComponent<Image>();
        icImg.preserveAspect = true; icImg.raycastTarget = false;
        icImg.sprite = shown;
        icImg.color = shown != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
        // 立ち絵を大きく見せる：カード上端より上＋左右いっぱいへ拡張（はみ出し許容）。preserveAspect で歪まない。
        icr.anchorMin = new Vector2(-0.04f, 0.44f); icr.anchorMax = new Vector2(1.04f, 1.26f);
        icr.offsetMin = Vector2.zero; icr.offsetMax = Vector2.zero;

        CreateLabel(card.transform, "Name", LocalizationManager.UnitName(heroId),
            new Vector2(0.05f, 0.385f), new Vector2(0.95f, 0.45f), 24f, FontStyles.Bold,
            new Color(0.96f, 0.98f, 1f), TextAlignmentOptions.Center, false);
        CreateLabel(card.transform, "Role", HeroRoleText(heroId, ja),
            new Vector2(0.05f, 0.335f), new Vector2(0.95f, 0.385f), 15f, FontStyles.Bold,
            new Color(0.7f, 0.85f, 1f), TextAlignmentOptions.Center, false);
        CreateLabel(card.transform, "Ult", HeroUltText(heroId, ja),
            new Vector2(0.07f, 0.20f), new Vector2(0.93f, 0.33f), 12.5f, FontStyles.Normal,
            new Color(0.86f, 0.92f, 1f), TextAlignmentOptions.Top, true);

        // 「性能」ボタン → ユニット詳細プレビュー。
        if (hasData)
        {
            GameObject info = new GameObject("Info", typeof(RectTransform), typeof(Image), typeof(Button));
            info.transform.SetParent(card.transform, false);
            RectTransform inr = info.GetComponent<RectTransform>();
            inr.anchorMin = new Vector2(0.5f, 0f); inr.anchorMax = new Vector2(0.5f, 0f); inr.pivot = new Vector2(0.5f, 0f);
            inr.anchoredPosition = new Vector2(0f, 66f); inr.sizeDelta = new Vector2(150f, 40f);
            info.GetComponent<Image>().color = new Color(0.02f, 0.14f, 0.18f, 0.96f);
            EntitiesDatabaseSO.EntityData captured = data;
            info.GetComponent<Button>().onClick.AddListener(() => { UnitStatusPanelUI.ShowPreview(captured, 1); AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); });
            CreateLabel(info.transform, "L", ja ? "性能" : "INFO", Vector2.zero, Vector2.one, 16f, FontStyles.Bold,
                new Color(0.86f, 1f, 1f), TextAlignmentOptions.Center, false).raycastTarget = false;
        }

        // 「このヒーローにする」＝選択確定。
        GameObject sel = new GameObject("Select", typeof(RectTransform), typeof(Image), typeof(Button));
        sel.transform.SetParent(card.transform, false);
        RectTransform sr = sel.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.5f, 0f); sr.anchorMax = new Vector2(0.5f, 0f); sr.pivot = new Vector2(0.5f, 0f);
        sr.anchoredPosition = new Vector2(0f, 16f); sr.sizeDelta = new Vector2(300f, 46f);
        sel.GetComponent<Image>().color = new Color(0.22f, 0.6f, 0.85f, 1f);
        string capturedId = heroId;
        sel.GetComponent<Button>().onClick.AddListener(() => ConfirmHeroSelection(capturedId));
        CreateLabel(sel.transform, "L", ja ? "このヒーローにする" : "CHOOSE THIS HERO", Vector2.zero, Vector2.one, 18f, FontStyles.Bold,
            Color.white, TextAlignmentOptions.Center, false).raycastTarget = false;

        // STORY-skin: カガチ＆犬化スキン解放済みなら、出撃姿（人/犬）を切り替えるトグルを表示。
        if (string.Equals(heroId, "HeroKagachi", System.StringComparison.OrdinalIgnoreCase) && GameManager.IsKagachiDogSkinUnlocked())
        {
            GameObject skin = new GameObject("SkinToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            skin.transform.SetParent(card.transform, false);
            RectTransform skr = skin.GetComponent<RectTransform>();
            skr.anchorMin = new Vector2(0.5f, 0f); skr.anchorMax = new Vector2(0.5f, 0f); skr.pivot = new Vector2(0.5f, 0f);
            skr.anchoredPosition = new Vector2(0f, 116f); skr.sizeDelta = new Vector2(300f, 40f);
            skin.GetComponent<Image>().color = new Color(0.30f, 0.22f, 0.42f, 0.96f);
            TextMeshProUGUI skinLabel = CreateLabel(skin.transform, "L", string.Empty, Vector2.zero, Vector2.one, 15f, FontStyles.Bold,
                new Color(0.92f, 0.88f, 1f), TextAlignmentOptions.Center, false);
            skinLabel.raycastTarget = false;
            System.Action refresh = () =>
            {
                bool on = GameManager.IsKagachiDogSkinActive();
                skinLabel.text = ja ? (on ? "出撃姿: 犬化 ✓" : "出撃姿: 人") : (on ? "Form: Dog ✓" : "Form: Human");
            };
            refresh();
            skin.GetComponent<Button>().onClick.AddListener(() =>
            {
                GameManager.SetKagachiDogSkinEnabled(!GameManager.IsKagachiDogSkinActive());
                AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
                refresh();
            });
        }
    }

    // ヒーローのロール一行（JA/EN）。
    private string HeroRoleText(string heroId, bool ja)
    {
        switch ((heroId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin": return ja ? "聖騎士・守護型" : "Paladin · Guardian";
            case "herokagachi": return ja ? "アサシン・攻撃型" : "Assassin · Striker";
            case "herovesna": return ja ? "蒼魔・秘術型（蒼炎）" : "Azure Mage (Blue Flame)";
            default: return string.Empty;
        }
    }

    // ヒーローの必殺名＋効果（JA/EN）。選択の決め手として表示。
    private string HeroUltText(string heroId, bool ja)
    {
        switch ((heroId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin": return ja
                ? "必殺「聖盾の号令」\n味方全体に最大HP30%シールド＋被ダメ-20%（6秒）。\nスキル「聖壁」周囲の味方をシールド＋回復。"
                : "Ult \"Aegis Command\"\nTeam gains 30% max-HP shield and -20% damage taken (6s).\nSkill \"Sacred Wall\" shields and heals nearby allies.";
            case "herokagachi": return ja
                ? "必殺「修羅の号令」\n味方全体に与ダメ+35%＋攻撃速度×1.25（6秒）。\nスキル「残影斬」最遠の敵へ踏み込み2連斬＋自己加速。"
                : "Ult \"Carnage Command\"\nTeam gains +35% damage and x1.25 attack speed (6s).\nSkill \"Afterimage Slash\" blinks in for a double strike.";
            case "herovesna": return ja
                ? "必殺「蒼炎の号令」\n敵全体に青炎ダメージ＋味方の攻撃速度×1.15（6秒）。\nスキル「蒼炎槍」対象中心に青い炎のAoE＋燃焼。"
                : "Ult \"Azure Flame Command\"\nBurns all enemies and grants allies x1.15 attack speed (6s).\nSkill \"Azure Flame Lance\" AoE blue flame + burn.";
            default: return string.Empty;
        }
    }

    private void BuildSlotConfirmOverlay()
    {
        bool ja = LocalizationManager.IsJapanese;
        slotConfirmOverlay = new GameObject("SlotConfirmOverlay", typeof(RectTransform), typeof(Image));
        slotConfirmOverlay.transform.SetParent(slotSelectView.transform, false);
        RectTransform ovRect = slotConfirmOverlay.GetComponent<RectTransform>();
        ovRect.anchorMin = Vector2.zero; ovRect.anchorMax = Vector2.one;
        ovRect.offsetMin = Vector2.zero; ovRect.offsetMax = Vector2.zero;
        slotConfirmOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(slotConfirmOverlay.transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(580f, 270f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.98f);

        slotConfirmMsg = CreateLabel(panel.transform, "Msg", "",
            new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.92f), 24f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, true);

        slotConfirmYesLabel = CreateConfirmButton(panel.transform, ja ? "OK" : "OK", new Vector2(0.30f, 0.2f), new Color(0.55f, 0.14f, 0.16f, 1f), () =>
        {
            System.Action act = slotConfirmAction;
            slotConfirmAction = null;
            slotConfirmOverlay.SetActive(false);
            act?.Invoke();
        });
        CreateConfirmButton(panel.transform, ja ? "キャンセル" : "CANCEL", new Vector2(0.70f, 0.2f), new Color(0.2f, 0.28f, 0.36f, 1f), () =>
        {
            slotConfirmAction = null;
            slotConfirmOverlay.SetActive(false);
        });

        slotConfirmOverlay.SetActive(false);
    }

    // 汎用の確認オーバーレイ。メッセージ・OKボタン名・確定時の処理を差し替えて表示する。
    private void ShowConfirm(string message, string yesLabel, System.Action onConfirm)
    {
        if (slotConfirmOverlay == null) return;
        slotConfirmAction = onConfirm;
        if (slotConfirmMsg != null) slotConfirmMsg.text = message;
        if (slotConfirmYesLabel != null) slotConfirmYesLabel.text = yesLabel;
        slotConfirmOverlay.transform.SetAsLastSibling();
        slotConfirmOverlay.SetActive(true);
    }

    private TextMeshProUGUI CreateConfirmButton(Transform parent, string label, Vector2 anchorCenter, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject b = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        b.transform.SetParent(parent, false);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchorCenter;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(210f, 62f);
        b.GetComponent<Image>().color = color;
        b.GetComponent<Button>().onClick.AddListener(onClick);
        TextMeshProUGUI t = CreateLabel(b.transform, "Label", label, Vector2.zero, Vector2.one, 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, false);
        t.raycastTarget = false;
        return t;
    }

    private void ShowDeleteConfirm(int slot)
    {
        bool ja = LocalizationManager.IsJapanese;
        ShowConfirm(
            ja ? "このスロットのデータを削除しますか？\nこの操作は元に戻せません。" : "Delete this save slot?\nThis cannot be undone.",
            ja ? "削除する" : "DELETE",
            () =>
            {
                SaveManager.EnsureExists();
                SaveManager.Instance.DeleteSlot(slot);
                RefreshSlotCards();
            });
    }

    // ---------- R3-hero-scale: ヒーロー育成（左に特大立ち絵＋右詳細／下に5枚ループ選択） ----------
    private void BuildHeroUpgradeView()
    {
        heroUpgradeView = NewFullRect("HeroUpgradeView");
        heroUpgradeView.SetActive(false);
        bool ja = LocalizationManager.IsJapanese;

        // タイトルは右上へ（左の特大立ち絵に隠れないように）。
        CreateLabel(heroUpgradeView.transform, "Title", ja ? "ヒーロー育成" : "HERO TRAINING",
            new Vector2(0.5f, 0.91f), new Vector2(0.98f, 0.99f), 38f, FontStyles.Bold,
            new Color(0.97f, 0.9f, 0.6f), TextAlignmentOptions.Right, false);

        // 左：特大ポートレート。立ち絵を画面の上下左へ大きくはみ出させ、キャラが画面いっぱいに迫る形に。
        GameObject artGo = new GameObject("DetailArt", typeof(RectTransform), typeof(Image));
        artGo.transform.SetParent(heroUpgradeView.transform, false);
        RectTransform ar = artGo.GetComponent<RectTransform>();
        ar.anchorMin = new Vector2(-0.28f, -0.35f); ar.anchorMax = new Vector2(0.78f, 1.35f);
        ar.offsetMin = Vector2.zero; ar.offsetMax = Vector2.zero;
        heroDetailArt = artGo.GetComponent<Image>();
        heroDetailArt.preserveAspect = true; heroDetailArt.raycastTarget = false;

        // 右：詳細パネル。
        RectTransform panel = CreatePanel(heroUpgradeView.transform, "Detail",
            new Vector2(0.53f, 0.36f), new Vector2(0.98f, 0.88f), new Color(0.06f, 0.1f, 0.16f, 0.94f));
        heroDetailName = CreateLabel(panel, "Name", "",
            new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.98f), 32f, FontStyles.Bold,
            new Color(0.97f, 0.92f, 0.7f), TextAlignmentOptions.Left, false);
        heroDetailInfo = CreateLabel(panel, "Info", "",
            new Vector2(0.05f, 0.61f), new Vector2(0.95f, 0.85f), 20f, FontStyles.Normal,
            new Color(0.88f, 0.93f, 1f), TextAlignmentOptions.TopLeft, true);

        // 熟練度ゲージ（次Lvまでの進捗）。
        heroMasteryGaugeRoot = new GameObject("MasteryGauge", typeof(RectTransform));
        heroMasteryGaugeRoot.transform.SetParent(panel, false);
        RectTransform gr = heroMasteryGaugeRoot.GetComponent<RectTransform>();
        gr.anchorMin = new Vector2(0.05f, 0.5f); gr.anchorMax = new Vector2(0.95f, 0.59f);
        gr.offsetMin = Vector2.zero; gr.offsetMax = Vector2.zero;
        GameObject gbg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        gbg.transform.SetParent(heroMasteryGaugeRoot.transform, false);
        RectTransform gbgr = gbg.GetComponent<RectTransform>();
        gbgr.anchorMin = new Vector2(0f, 0f); gbgr.anchorMax = new Vector2(1f, 0.6f); gbgr.offsetMin = Vector2.zero; gbgr.offsetMax = Vector2.zero;
        gbg.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.95f);
        GameObject gfill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        gfill.transform.SetParent(gbg.transform, false);
        heroMasteryFill = gfill.GetComponent<RectTransform>();
        heroMasteryFill.anchorMin = new Vector2(0f, 0f); heroMasteryFill.anchorMax = new Vector2(0.5f, 1f);
        heroMasteryFill.offsetMin = Vector2.zero; heroMasteryFill.offsetMax = Vector2.zero;
        gfill.GetComponent<Image>().color = new Color(0.4f, 0.85f, 1f, 1f);
        heroMasteryGaugeText = CreateLabel(heroMasteryGaugeRoot.transform, "GText", "",
            new Vector2(0f, 0.55f), new Vector2(1f, 1f), 14f, FontStyles.Bold,
            new Color(0.8f, 0.9f, 1f), TextAlignmentOptions.Left, false);

        // 必殺カスタム見出し。
        CreateLabel(panel, "UltHead", ja ? "必殺カスタム（熟練度で解放）" : "Ultimate (unlock by mastery)",
            new Vector2(0.05f, 0.41f), new Vector2(0.95f, 0.49f), 18f, FontStyles.Bold,
            new Color(0.8f, 0.88f, 1f), TextAlignmentOptions.Left, false);

        // 必殺バリアント3種（基本/A/B）。横並び。
        string[] varLabels = ja ? new[] { "基本", "強化A", "強化B" } : new[] { "BASE", "A", "B" };
        for (int v = 0; v < 3; v++)
        {
            GameObject vb = new GameObject("Ult" + v, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonJuice));
            vb.transform.SetParent(panel, false);
            RectTransform vr = vb.GetComponent<RectTransform>();
            float x0 = 0.05f + v * 0.305f;
            vr.anchorMin = new Vector2(x0, 0.24f); vr.anchorMax = new Vector2(x0 + 0.28f, 0.4f);
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            Image vi = vb.GetComponent<Image>(); vi.color = new Color(0.16f, 0.24f, 0.36f, 0.95f);
            Button vbtn = vb.GetComponent<Button>(); vbtn.targetGraphic = vi;
            int captured = v;
            vbtn.onClick.AddListener(() => OnHeroUltVariantClicked(captured));
            heroUltVarBtn[v] = vbtn;
            heroUltVarText[v] = CreateLabel(vb.transform, "T", varLabels[v], Vector2.zero, Vector2.one,
                16f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, false);
            heroUltVarText[v].raycastTarget = false;
        }

        // この主人公にするボタン（下部・横幅いっぱい）。
        GameObject useb = new GameObject("UseBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonJuice));
        useb.transform.SetParent(panel, false);
        RectTransform usr = useb.GetComponent<RectTransform>();
        usr.anchorMin = new Vector2(0.05f, 0.05f); usr.anchorMax = new Vector2(0.95f, 0.2f);
        usr.offsetMin = Vector2.zero; usr.offsetMax = Vector2.zero;
        Image usi = useb.GetComponent<Image>(); usi.color = new Color(0.85f, 0.66f, 0.25f, 0.98f);
        heroDetailUseBtn = useb.GetComponent<Button>(); heroDetailUseBtn.targetGraphic = usi;
        heroDetailUseBtn.onClick.AddListener(OnHeroUseClicked);
        heroDetailUseText = CreateLabel(useb.transform, "T", "", Vector2.zero, Vector2.one,
            20f, FontStyles.Bold, new Color(0.2f, 0.15f, 0.03f), TextAlignmentOptions.Center, false);
        heroDetailUseText.raycastTarget = false;

        // 下：5枚カルーセル＋左右矢印（ループ）。
        MakeCarouselArrow("ArrowL", true, new Vector2(0.02f, 0.04f), new Vector2(0.075f, 0.24f));
        MakeCarouselArrow("ArrowR", false, new Vector2(0.925f, 0.04f), new Vector2(0.98f, 0.24f));

        GameObject rowGo = new GameObject("RosterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(heroUpgradeView.transform, false);
        RectTransform rr = rowGo.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.5f, 0f); rr.anchorMax = new Vector2(0.5f, 0f); rr.pivot = new Vector2(0.5f, 0f);
        rr.anchoredPosition = new Vector2(0f, 18f); rr.sizeDelta = new Vector2(1480f, 320f);
        HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 14f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        heroRosterRow = rowGo.transform;

        CreateBackButton(heroUpgradeView.transform, () => { if (heroUpgradeFromPrep) ShowChapterPrep(chapterPrepChapter); else ShowMainLobby(); });
    }

    private void MakeCarouselArrow(string name, bool left, Vector2 aMin, Vector2 aMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonJuice));
        go.transform.SetParent(heroUpgradeView.transform, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>(); img.color = new Color(0.16f, 0.24f, 0.36f, 0.96f);
        Button b = go.GetComponent<Button>(); b.targetGraphic = img;
        int dir = left ? -1 : 1;
        b.onClick.AddListener(() => CarouselStep(dir));
        TextMeshProUGUI t = CreateLabel(go.transform, "T", left ? "◀" : "▶", Vector2.zero, Vector2.one,
            44f, FontStyles.Bold, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Center, false);
        t.raycastTarget = false;
    }

    private int WrapRoster(int i)
    {
        int n = GameManager.HeroRosterIds.Length;
        if (n <= 0) return 0;
        return ((i % n) + n) % n;
    }

    private void CarouselStep(int dir)
    {
        heroCarouselIndex = WrapRoster(heroCarouselIndex + dir);
        AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
        RefreshHeroUpgrade();
    }

    private void ShowHeroUpgrade(bool fromPrep = false)
    {
        heroUpgradeFromPrep = fromPrep;
        EnsureUi();
        SaveManager.EnsureExists();
        // 開いた時はアクティブ主人公を中心に。
        heroCarouselIndex = 0;
        string active = SaveManager.Instance != null ? SaveManager.Instance.GetHeroUnitId() : null;
        if (!string.IsNullOrEmpty(active))
            for (int i = 0; i < GameManager.HeroRosterIds.Length; i++)
                if (string.Equals(GameManager.HeroRosterIds[i], active, System.StringComparison.OrdinalIgnoreCase)) heroCarouselIndex = i;
        SetView(heroUpgradeView);
        RefreshHeroUpgrade();
    }

    private void RefreshHeroUpgrade()
    {
        if (heroRosterRow == null) return;
        SaveManager.EnsureExists();
        bool ja = LocalizationManager.IsJapanese;
        int n = GameManager.HeroRosterIds.Length;
        if (n <= 0) return;
        heroCarouselIndex = WrapRoster(heroCarouselIndex);

        EntitiesDatabaseSO db = Resources.Load<EntitiesDatabaseSO>("Entity Database");
        string shown = GameManager.HeroRosterIds[heroCarouselIndex]; // 閲覧中＝カルーセル中心。
        bool unlocked = GameManager.IsHeroCandidateUnlocked(shown);
        bool active = SaveManager.Instance != null && string.Equals(SaveManager.Instance.GetHeroUnitId(), shown, System.StringComparison.OrdinalIgnoreCase);

        // 詳細：大ポートレート。未解放はシルエット。
        if (heroDetailArt != null)
        {
            Sprite art = DialogArt.Portrait(shown) ?? FindEntityIcon(db, shown);
            heroDetailArt.sprite = art;
            heroDetailArt.color = art == null ? new Color(1f, 1f, 1f, 0f)
                : (unlocked ? Color.white : new Color(0.04f, 0.05f, 0.08f, 1f)); // 未解放＝シルエット
        }
        // 詳細：名前（未解放は ???）。
        if (heroDetailName != null) heroDetailName.text = unlocked ? LocalizationManager.UnitName(shown) : "???";
        // 詳細：解放条件 or 育成情報。
        if (heroDetailInfo != null)
        {
            if (!unlocked)
            {
                int ch = GameManager.GetHeroUnlockChapter(shown);
                heroDetailInfo.text = (ja ? "未解放\n\n解放条件： 第" + ch + "章クリア" : "Locked\n\nUnlock: clear chapter " + ch);
            }
            else
            {
                int mlv = SaveManager.Instance.GetHeroMasteryLevel(shown);
                int bonus = Mathf.RoundToInt((SaveManager.Instance.GetHeroMasteryStatMultiplier(shown) - 1f) * 100f);
                int toNext = SaveManager.Instance.GetHeroMasteryXpToNext(shown);
                string role = HeroRoleText(shown, ja);
                string syn = HeroSynergyText(db, shown);
                heroDetailInfo.text = (active ? (ja ? "◆ 使用中　" : "◆ In use　") : "")
                    + (ja ? "熟練度 Lv " : "Mastery Lv ") + mlv + "　(HP/攻撃 +" + bonus + "%)"
                    + (mlv >= SaveManager.HeroMasteryMaxLevel ? (ja ? " 最大" : " MAX") : (ja ? "　次まで " + toNext + "XP" : "  next " + toNext + "XP")) + "\n"
                    + (string.IsNullOrEmpty(role) ? "" : (ja ? "タイプ： " : "Type: ") + role + "　")
                    + (ja ? "シナジー： " : "Synergy: ") + syn;
            }
        }
        // 詳細：熟練度ゲージ（次Lvまでの進捗）。未解放時・MAX時は出し分け。
        if (heroMasteryGaugeRoot != null)
        {
            heroMasteryGaugeRoot.SetActive(unlocked);
            if (unlocked && heroMasteryFill != null)
            {
                int mlv = SaveManager.Instance.GetHeroMasteryLevel(shown);
                bool isMax = mlv >= SaveManager.HeroMasteryMaxLevel;
                int per = SaveManager.Instance.GetHeroMasteryXpForCurrentLevel(shown); // 現Lvの必要XP（Lvごとに増加）
                int into = isMax ? 1 : SaveManager.Instance.GetHeroMasteryXpIntoLevel(shown); // 現Lv内の進捗
                int toNext = SaveManager.Instance.GetHeroMasteryXpToNext(shown);
                float frac = isMax ? 1f : Mathf.Clamp01(per > 0 ? (float)into / per : 0f);
                heroMasteryFill.anchorMin = new Vector2(0f, 0f);
                heroMasteryFill.anchorMax = new Vector2(Mathf.Max(0.02f, frac), 1f);
                if (heroMasteryGaugeText != null)
                    heroMasteryGaugeText.text = isMax
                        ? (ja ? "熟練度 MAX (Lv" + mlv + ")" : "Mastery MAX (Lv" + mlv + ")")
                        : (ja ? "熟練度 Lv" + mlv + "　次のLvまで " + toNext + "XP" : "Mastery Lv" + mlv + "  next Lv in " + toNext + "XP");
            }
        }
        // 詳細：必殺バリアント3種（基本/A/B）の解放・装備状態。
        if (unlocked)
        {
            int equipped = SaveManager.Instance.GetHeroEquippedUlt(shown);
            for (int v = 0; v < 3; v++)
            {
                if (heroUltVarBtn[v] == null) continue;
                bool vunlocked = SaveManager.Instance.IsHeroUltVariantUnlocked(shown, v);
                bool isEq = equipped == v;
                heroUltVarBtn[v].interactable = vunlocked && !isEq;
                Image vi = heroUltVarBtn[v].targetGraphic as Image;
                if (vi != null) vi.color = isEq ? new Color(0.95f, 0.78f, 0.32f, 1f)
                    : vunlocked ? new Color(0.16f, 0.24f, 0.36f, 0.95f) : new Color(0.22f, 0.22f, 0.26f, 0.85f);
                if (heroUltVarText[v] != null)
                {
                    string baseL = ja ? (v == 0 ? "基本" : v == 1 ? "強化A" : "強化B") : (v == 0 ? "BASE" : v == 1 ? "A" : "B");
                    if (!vunlocked)
                    {
                        int need = v == 1 ? SaveManager.HeroUltAUnlockLevel : SaveManager.HeroUltBUnlockLevel;
                        baseL += ja ? "\nLv" + need : "\nLv" + need;
                    }
                    else if (isEq) baseL += ja ? "\n装備中" : "\nEQUIP";
                    heroUltVarText[v].text = baseL;
                    heroUltVarText[v].color = isEq ? new Color(0.2f, 0.15f, 0.03f) : (vunlocked ? Color.white : new Color(0.6f, 0.62f, 0.68f));
                }
            }
        }
        else
        {
            for (int v = 0; v < 3; v++)
                if (heroUltVarBtn[v] != null) heroUltVarBtn[v].interactable = false;
        }
        // 詳細：この主人公にするボタン。
        if (heroDetailUseBtn != null && heroDetailUseText != null)
        {
            bool can = unlocked && !active;
            heroDetailUseText.text = active ? (ja ? "使用中" : "IN USE") : (ja ? "この主人公にする" : "SET AS HERO");
            heroDetailUseBtn.interactable = can;
            Image usi = heroDetailUseBtn.targetGraphic as Image;
            if (usi != null) usi.color = can ? new Color(0.85f, 0.66f, 0.25f, 0.98f) : new Color(0.32f, 0.32f, 0.3f, 0.85f);
        }

        // 下：中心から±2の5枚をループ表示。
        for (int i = heroRosterRow.childCount - 1; i >= 0; i--)
            Destroy(heroRosterRow.GetChild(i).gameObject);
        int window = Mathf.Min(5, n);
        int half = window / 2;
        for (int off = -half; off <= half; off++)
        {
            int idx = WrapRoster(heroCarouselIndex + off);
            CreateHeroRosterTile(idx, db, off == 0);
        }
    }

    private void CreateHeroRosterTile(int rosterIndex, EntitiesDatabaseSO db, bool isCenter)
    {
        bool ja = LocalizationManager.IsJapanese;
        string heroId = GameManager.HeroRosterIds[rosterIndex];
        bool unlocked = GameManager.IsHeroCandidateUnlocked(heroId);
        bool isActive = SaveManager.Instance != null && string.Equals(SaveManager.Instance.GetHeroUnitId(), heroId, System.StringComparison.OrdinalIgnoreCase);

        GameObject tile = new GameObject(heroId + "Tile", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        tile.transform.SetParent(heroRosterRow, false);
        LayoutElement le = tile.GetComponent<LayoutElement>();
        le.preferredWidth = isCenter ? 300f : 250f; le.preferredHeight = isCenter ? 310f : 270f;
        Image bg = tile.GetComponent<Image>();
        Sprite panel = Resources.Load<Sprite>("UI/Panels/result_panel");
        if (panel != null) { bg.sprite = panel; bg.type = Image.Type.Sliced; }
        bg.color = isCenter ? new Color(0.55f, 0.95f, 0.5f, 1f)               // 中心＝選択中（緑枠）
                            : isActive ? new Color(0.95f, 0.82f, 0.4f, 1f)     // 使用中＝金
                                       : unlocked ? new Color(0.12f, 0.18f, 0.28f, 0.95f)
                                                  : new Color(0.08f, 0.1f, 0.14f, 0.9f);

        // 未解放もクリック可（中心へ寄せて閲覧）。
        int capturedIndex = rosterIndex;
        Button b = tile.AddComponent<Button>(); b.targetGraphic = bg;
        b.onClick.AddListener(() => OnHeroTileClicked(capturedIndex));
        tile.AddComponent<ButtonJuice>();

        // 立ち絵はタイル上部いっぱいにクロップ表示（顔～上半身が大きく出る）。RectMask2Dで下をカット。
        GameObject clip = new GameObject("PortraitClip", typeof(RectTransform), typeof(RectMask2D));
        clip.transform.SetParent(tile.transform, false);
        RectTransform clr = clip.GetComponent<RectTransform>();
        clr.anchorMin = new Vector2(0.05f, 0.3f); clr.anchorMax = new Vector2(0.95f, 0.99f);
        clr.offsetMin = Vector2.zero; clr.offsetMax = Vector2.zero;

        GameObject ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        ic.transform.SetParent(clip.transform, false);
        RectTransform icr = ic.GetComponent<RectTransform>();
        icr.anchorMin = new Vector2(0.5f, 1f); icr.anchorMax = new Vector2(0.5f, 1f); icr.pivot = new Vector2(0.5f, 1f);
        icr.anchoredPosition = new Vector2(0f, 0f);
        // クリップ枠より縦に大きい画像を上寄せ配置→顔～胸が大きく見え、脚側はマスクで隠れる。
        float w = isCenter ? 260f : 215f;
        icr.sizeDelta = new Vector2(w, w * 1.9f);
        Image icImg = ic.GetComponent<Image>(); icImg.preserveAspect = true; icImg.raycastTarget = false;
        icImg.sprite = DialogArt.Portrait(heroId) ?? FindEntityIcon(db, heroId);
        icImg.color = unlocked ? Color.white : new Color(0.03f, 0.04f, 0.07f, 1f); // 未解放＝シルエット

        CreateLabel(tile.transform, "Name", unlocked ? LocalizationManager.UnitName(heroId) : "???",
            new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.3f), isCenter ? 20f : 17f, FontStyles.Bold,
            (isCenter || isActive) ? new Color(0.12f, 0.14f, 0.05f) : (unlocked ? new Color(0.92f, 0.96f, 1f) : new Color(0.72f, 0.76f, 0.84f)),
            TextAlignmentOptions.Center, false);

        string sub;
        if (!unlocked) { int ch = GameManager.GetHeroUnlockChapter(heroId); sub = ja ? $"第{ch}章クリア" : $"Clear ch.{ch}"; }
        else sub = (isActive ? (ja ? "使用中 ・ " : "USE ・ ") : "") + "Lv " + SaveManager.Instance.GetHeroMasteryLevel(heroId);
        CreateLabel(tile.transform, "Sub", sub,
            new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.16f), isCenter ? 15f : 13f, FontStyles.Normal,
            (isCenter || isActive) ? new Color(0.2f, 0.22f, 0.06f) : (unlocked ? new Color(0.72f, 0.86f, 1f) : new Color(0.7f, 0.74f, 0.82f)),
            TextAlignmentOptions.Center, true);
    }

    private void OnHeroTileClicked(int rosterIndex)
    {
        heroCarouselIndex = WrapRoster(rosterIndex);
        AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
        RefreshHeroUpgrade();
    }

    private void OnHeroUseClicked()
    {
        if (SaveManager.Instance == null) return;
        string shown = GameManager.HeroRosterIds[WrapRoster(heroCarouselIndex)];
        if (!GameManager.IsHeroCandidateUnlocked(shown)) return;
        SaveManager.Instance.SetHeroUnitId(shown);
        AttackEffectPlayer.PlayUiSfx("unit_buy");
        RefreshHeroUpgrade();
    }

    private string HeroSynergyText(EntitiesDatabaseSO db, string heroId)
    {
        if (db == null || db.allEntities == null) return "-";
        for (int i = 0; i < db.allEntities.Count; i++)
        {
            if (!string.Equals(db.allEntities[i].name, heroId, System.StringComparison.OrdinalIgnoreCase)) continue;
            var d = db.allEntities[i];
            List<string> names = new List<string>();
            if (d.synergy1 != SynergyType.None) names.Add(LocalizationManager.SynergyName(d.synergy1));
            if (d.synergy2 != SynergyType.None) names.Add(LocalizationManager.SynergyName(d.synergy2));
            if (d.synergy3 != SynergyType.None) names.Add(LocalizationManager.SynergyName(d.synergy3));
            return names.Count > 0 ? string.Join(" / ", names) : "-";
        }
        return "-";
    }

    private Sprite FindEntityIcon(EntitiesDatabaseSO db, string unitId)
    {
        if (db == null || db.allEntities == null) return null;
        for (int i = 0; i < db.allEntities.Count; i++)
            if (string.Equals(db.allEntities[i].name, unitId, System.StringComparison.OrdinalIgnoreCase))
                return db.allEntities[i].icon;
        return null;
    }

    // 必殺バリアント(基本/A/B)を装備（熟練度で解放済みのみ）。
    private void OnHeroUltVariantClicked(int variant)
    {
        if (SaveManager.Instance == null) return;
        string shown = GameManager.HeroRosterIds[WrapRoster(heroCarouselIndex)];
        if (!GameManager.IsHeroCandidateUnlocked(shown)) return;
        bool ok = SaveManager.Instance.SetHeroEquippedUlt(shown, variant);
        AttackEffectPlayer.PlayUiSfx(ok ? "unit_buy" : "sfx_ui_select");
        RefreshHeroUpgrade();
    }

    // ---------- 図鑑（ボスコレクション）画面 ----------
    private void ShowCollection()
    {
        EnsureUi();
        RefreshCollection();
        SetView(collectionView);
    }

    private void BuildCollectionView()
    {
        collectionView = NewFullRect("CollectionView");
        collectionView.SetActive(false);
        bool ja = LocalizationManager.IsJapanese;

        // ヘッダー帯（上部・全幅）。タイトルをグリッドの上に独立した帯として置き、埋もれないようにする。
        GameObject headerBar = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        headerBar.transform.SetParent(collectionView.transform, false);
        RectTransform hb = headerBar.GetComponent<RectTransform>();
        hb.anchorMin = new Vector2(0f, 1f); hb.anchorMax = new Vector2(1f, 1f); hb.pivot = new Vector2(0.5f, 1f);
        hb.anchoredPosition = new Vector2(0f, 0f); hb.sizeDelta = new Vector2(0f, 150f);
        Image hbImg = headerBar.GetComponent<Image>();
        hbImg.color = new Color(0.02f, 0.04f, 0.09f, 0.78f); // 半透明の暗幕でタイトルを読みやすく
        hbImg.raycastTarget = false;

        TextMeshProUGUI collectionTitle = CreateLabel(collectionView.transform, "Title", ja ? "ボス図鑑" : "BOSS COLLECTION",
            new Vector2(0f, 0.9f), new Vector2(1f, 0.99f), 48f, FontStyles.Bold,
            new Color(0.99f, 0.86f, 0.45f), TextAlignmentOptions.Center, false);
        collectionTitle.outlineWidth = 0.22f; collectionTitle.outlineColor = Color.black;

        TextMeshProUGUI collectionHint = CreateLabel(collectionView.transform, "Hint",
            ja ? "章ボスを倒すと解放。再クリアで育成Lvが上がり強くなります。" : "Defeat chapter bosses to unlock. Re-clear to raise affinity Lv.",
            new Vector2(0f, 0.855f), new Vector2(1f, 0.9f), 18f, FontStyles.Normal,
            new Color(0.86f, 0.92f, 1f), TextAlignmentOptions.Center, false);

        CreateBackButton(collectionView.transform, () => ShowMainLobby());

        GameObject gridGo = new GameObject("CollectionGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(collectionView.transform, false);
        RectTransform gr = gridGo.GetComponent<RectTransform>();
        gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
        gr.pivot = new Vector2(0.5f, 0.5f);
        // ヘッダー帯ぶん全体を下げ、セルを少し小さくして4行が帯と被らず収まるように。
        gr.anchoredPosition = new Vector2(0f, -86f);
        gr.sizeDelta = new Vector2(1020f, 540f);
        GridLayoutGroup glg = gridGo.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(180f, 206f);
        glg.spacing = new Vector2(16f, 14f);
        glg.childAlignment = TextAnchor.MiddleCenter;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
        collectionGrid = gridGo.transform;

        // タイトル/ヒントはグリッドより後ろに作られるよう最前面へ（パネルに隠れない）。
        headerBar.transform.SetAsLastSibling();
        collectionTitle.transform.SetAsLastSibling();
        collectionHint.transform.SetAsLastSibling();
    }

    // 図鑑グリッド用に生成したボス実体を全て破棄する（再構築・画面離脱時）。
    private void CleanupCollectionPreviews()
    {
        for (int i = collectionPreviewEntities.Count - 1; i >= 0; i--)
            if (collectionPreviewEntities[i] != null) Destroy(collectionPreviewEntities[i]);
        collectionPreviewEntities.Clear();
    }

    private void RefreshCollection()
    {
        if (collectionGrid == null) return;
        CleanupCollectionPreviews();
        for (int i = collectionGrid.childCount - 1; i >= 0; i--)
            Destroy(collectionGrid.GetChild(i).gameObject);
        SaveManager.EnsureExists();
        EntitiesDatabaseSO db = Resources.Load<EntitiesDatabaseSO>("Entity Database");
        var ids = GameManager.GetAllChapterBossRewardUnitIds();
        for (int i = 0; i < ids.Count; i++)
            CreateCollectionCell(ids[i], db);
    }

    private void CreateCollectionCell(string unitId, EntitiesDatabaseSO db)
    {
        bool ja = LocalizationManager.IsJapanese;
        bool owned = SaveManager.Instance != null && SaveManager.Instance.HasBossAlly(unitId);

        EntitiesDatabaseSO.EntityData data = default;
        if (db != null && db.allEntities != null)
        {
            for (int i = 0; i < db.allEntities.Count; i++)
                if (string.Equals(db.allEntities[i].name, unitId, System.StringComparison.OrdinalIgnoreCase))
                { data = db.allEntities[i]; break; }
        }

        GameObject cell = new GameObject(unitId + "Cell", typeof(RectTransform), typeof(Image));
        cell.transform.SetParent(collectionGrid, false);
        Image cellBg = cell.GetComponent<Image>();
        Sprite panel = Resources.Load<Sprite>("UI/Panels/result_panel");
        if (panel != null)
        {
            cellBg.sprite = panel; cellBg.type = Image.Type.Sliced;
            cellBg.color = owned ? new Color(0.86f, 0.91f, 1f, 1f) : new Color(0.48f, 0.5f, 0.56f, 1f);
        }
        else cellBg.color = owned ? new Color(0.12f, 0.18f, 0.28f, 0.95f) : new Color(0.08f, 0.1f, 0.14f, 0.9f);

        // 解放済みはクリックで「全画面のボス詳細（図鑑専用）」を開く。
        if (owned && data.prefab != null)
        {
            string capturedId = unitId;
            Button cellBtn = cell.AddComponent<Button>();
            cellBtn.targetGraphic = cellBg;
            cellBtn.onClick.AddListener(() =>
            {
                AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
                ShowCollectionDetail(capturedId);
            });
            cell.AddComponent<ButtonJuice>();
        }

        GameObject ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        ic.transform.SetParent(cell.transform, false);
        RectTransform icr = ic.GetComponent<RectTransform>();
        icr.anchorMin = icr.anchorMax = new Vector2(0.5f, 0.5f);
        icr.pivot = new Vector2(0.5f, 0.5f);
        icr.anchoredPosition = new Vector2(0f, 32f);
        icr.sizeDelta = new Vector2(132f, 132f);
        Image icImg = ic.GetComponent<Image>();
        icImg.preserveAspect = true;
        icImg.raycastTarget = false;
        // ユニットのスプライト（キャラ姿）で表示。未解放はシルエット（暗色乗算）、解放後はフルカラー。
        // アイコン（長方形の絵札）ではなく、盤外で待機アニメ中の実体の SpriteRenderer をセルへミラーする。
        icImg.color = owned ? Color.white : new Color(0.03f, 0.04f, 0.07f, 1f);
        if (data.prefab != null)
        {
            BaseEntity preview = Instantiate(data.prefab);
            preview.transform.position = new Vector3(-100000f - collectionPreviewEntities.Count * 40f, -100000f, 0f);
            preview.InitializeIdentity(unitId, Mathf.Max(1, data.cost), 1);
            collectionPreviewEntities.Add(preview.gameObject);
            CollectionBossAnimator mirror = ic.AddComponent<CollectionBossAnimator>();
            mirror.source = preview.spriteRender;
            mirror.target = icImg;
            if (preview.spriteRender != null) icImg.sprite = preview.spriteRender.sprite;
        }
        else
        {
            // 実体が無い場合のみアイコンへフォールバック。
            icImg.sprite = data.icon;
        }

        CreateLabel(cell.transform, "Name", owned ? LocalizationManager.UnitName(unitId) : "???",
            new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.34f), 19f, FontStyles.Bold,
            owned ? new Color(0.12f, 0.16f, 0.24f) : new Color(0.86f, 0.89f, 0.96f), TextAlignmentOptions.Center, false);

        string sub;
        if (owned)
        {
            int lv = SaveManager.Instance.GetBossAffinityLevel(unitId);
            int bonus = Mathf.RoundToInt((SaveManager.Instance.GetBossAffinityStatMultiplier(unitId) - 1f) * 100f);
            sub = ja ? $"育成 Lv {lv}  (+{bonus}%)" : $"Affinity Lv {lv}  (+{bonus}%)";
        }
        else sub = ja ? "未解放" : "LOCKED";
        CreateLabel(cell.transform, "Sub", sub,
            new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.17f), 16f, FontStyles.Normal,
            owned ? new Color(0.2f, 0.3f, 0.5f) : new Color(0.72f, 0.74f, 0.8f), TextAlignmentOptions.Center, false);
    }

    // ---------- 図鑑：全画面ボス詳細（図鑑専用） ----------
    private RectTransform CreatePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return r;
    }

    private void CreateNavButton(Transform parent, string glyph, Vector2 aMin, Vector2 aMax, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Nav", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = new Color(0.09f, 0.14f, 0.22f, 0.95f);
        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); onClick(); });
        CreateLabel(go.transform, "G", glyph, Vector2.zero, Vector2.one, 40f, FontStyles.Bold, new Color(0.7f, 0.82f, 0.96f), TextAlignmentOptions.Center, false);
        go.AddComponent<ButtonJuice>();
    }

    private Image CreateTextButton(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = new Vector2(3f, 2f); r.offsetMax = new Vector2(-3f, -2f);
        Image img = go.GetComponent<Image>();
        img.color = new Color(0.09f, 0.14f, 0.22f, 0.95f);
        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); onClick(); });
        CreateLabel(go.transform, "L", label, Vector2.zero, Vector2.one, 13f, FontStyles.Bold, new Color(0.85f, 0.9f, 0.97f), TextAlignmentOptions.Center, false);
        go.AddComponent<ButtonJuice>();
        return img;
    }

    // アニメ表示／ショップアイコン表示の切替。
    private void SetCollectionDetailMode(bool anim)
    {
        cdAnimMode = anim;
        if (cdSpriteImage != null) cdSpriteImage.enabled = anim;
        if (cdIconImage != null) cdIconImage.enabled = !anim;
        if (cdAnimator != null) cdAnimator.enabled = anim; // アイコン表示中はミラーを止める
        if (cdActionHint != null) cdActionHint.gameObject.SetActive(anim);
        Color on = new Color(0.16f, 0.30f, 0.46f, 1f);
        Color off = new Color(0.09f, 0.14f, 0.22f, 0.95f);
        if (cdAnimTab != null) cdAnimTab.color = anim ? on : off;
        if (cdIconTab != null) cdIconTab.color = anim ? off : on;
    }

    // スプライトクリックでモーションを巡回（待機→移動→攻撃→スキル→死亡）。
    private void PlayCollectionDetailAction()
    {
        if (!cdAnimMode || cdPreviewEntityObj == null || cdMotions.Count == 0) return;
        BaseEntity ent = cdPreviewEntityObj.GetComponent<BaseEntity>();
        if (ent == null) return;
        cdMotionIndex = (cdMotionIndex + 1) % cdMotions.Count;
        string m = cdMotions[cdMotionIndex];
        ent.PreviewSetMotion(m);
        if (cdActionHint != null) cdActionHint.text = MotionLabel(m);
    }

    private string MotionLabel(string m)
    {
        bool ja = LocalizationManager.IsJapanese;
        switch (m)
        {
            case "walk": return ja ? "モーション：移動（クリックで切替）" : "Motion: Move (click to cycle)";
            case "attack": return ja ? "モーション：攻撃（クリックで切替）" : "Motion: Attack (click to cycle)";
            case "ability": return ja ? "モーション：スキル（クリックで切替）" : "Motion: Skill (click to cycle)";
            case "death": return ja ? "モーション：死亡（クリックで切替）" : "Motion: Death (click to cycle)";
            default: return ja ? "モーション：待機（クリックで切替）" : "Motion: Idle (click to cycle)";
        }
    }

    private void ShowIconFullscreen()
    {
        if (cdIconOverlay == null || cdCurrentIcon == null) return;
        if (cdIconBig != null) cdIconBig.sprite = cdCurrentIcon;
        cdIconOverlay.transform.SetAsLastSibling();
        cdIconOverlay.SetActive(true);
    }

    private void CloseIconFullscreen()
    {
        if (cdIconOverlay != null) cdIconOverlay.SetActive(false);
    }

    private void BuildCollectionDetailView()
    {
        collectionDetailView = NewFullRect("CollectionDetailView");
        collectionDetailView.SetActive(false);
        bool ja = LocalizationManager.IsJapanese;
        Transform root = collectionDetailView.transform;

        CreatePanel(root, "Backdrop", Vector2.zero, Vector2.one, new Color(0.03f, 0.05f, 0.08f, 0.96f));

        CreateBackButton(root, () => ShowCollection());

        CreateLabel(root, "Title", "BOSS FILE", new Vector2(0.30f, 0.93f), new Vector2(0.70f, 0.99f),
            26f, FontStyles.Bold, new Color(0.92f, 0.96f, 1f), TextAlignmentOptions.Center, false);
        CreateLabel(root, "Subtitle", ja ? "ボス図鑑 — 詳細" : "Boss Collection — Detail",
            new Vector2(0.30f, 0.895f), new Vector2(0.70f, 0.93f), 12f, FontStyles.Normal,
            new Color(0.44f, 0.55f, 0.69f), TextAlignmentOptions.Center, false);

        CreateNavButton(root, "‹", new Vector2(0.008f, 0.42f), new Vector2(0.052f, 0.58f), () => CollectionDetailStep(-1));
        CreateNavButton(root, "›", new Vector2(0.948f, 0.42f), new Vector2(0.992f, 0.58f), () => CollectionDetailStep(1));

        // 表示切替タブ（アニメ / アイコン）— 枠のすぐ上。
        cdAnimTab = CreateTextButton(root, "TabAnim", ja ? "アニメ" : "Sprite",
            new Vector2(0.06f, 0.865f), new Vector2(0.23f, 0.895f), () => SetCollectionDetailMode(true));
        cdIconTab = CreateTextButton(root, "TabIcon", ja ? "アイコン" : "Icon",
            new Vector2(0.23f, 0.865f), new Vector2(0.40f, 0.895f), () => SetCollectionDetailMode(false));

        // 左：大スプライト枠＋名前＋シナジー
        RectTransform frame = CreatePanel(root, "SpriteFrame", new Vector2(0.06f, 0.30f), new Vector2(0.40f, 0.855f), new Color(0.04f, 0.08f, 0.13f, 1f));
        frame.gameObject.AddComponent<RectMask2D>(); // 拡大したスプライトを枠内にクリップ。

        // ショップアイコン（大表示）。クリックで全画面拡大。
        GameObject ico = new GameObject("BossIcon", typeof(RectTransform), typeof(Image), typeof(Button));
        ico.transform.SetParent(frame, false);
        RectTransform icoR = ico.GetComponent<RectTransform>();
        icoR.anchorMin = new Vector2(0.08f, 0.10f); icoR.anchorMax = new Vector2(0.92f, 0.90f);
        icoR.offsetMin = Vector2.zero; icoR.offsetMax = Vector2.zero;
        cdIconImage = ico.GetComponent<Image>();
        cdIconImage.preserveAspect = true; cdIconImage.raycastTarget = true; cdIconImage.color = Color.white;
        cdIconImage.enabled = false;
        Button icoBtn = ico.GetComponent<Button>();
        icoBtn.targetGraphic = cdIconImage;
        icoBtn.transition = Selectable.Transition.None;
        icoBtn.onClick.AddListener(() => ShowIconFullscreen());

        // アニメスプライト。クリックでモーション切替（待機/移動/攻撃/スキル/死亡）、スクロールで拡大。
        GameObject spr = new GameObject("BossSprite", typeof(RectTransform), typeof(Image), typeof(Button));
        spr.transform.SetParent(frame, false);
        RectTransform sprR = spr.GetComponent<RectTransform>();
        sprR.anchorMin = new Vector2(0.08f, 0.10f); sprR.anchorMax = new Vector2(0.92f, 0.90f);
        sprR.offsetMin = Vector2.zero; sprR.offsetMax = Vector2.zero;
        cdSpriteImage = spr.GetComponent<Image>();
        cdSpriteImage.preserveAspect = true; cdSpriteImage.raycastTarget = true; cdSpriteImage.color = Color.white;
        Button sprBtn = spr.GetComponent<Button>();
        sprBtn.targetGraphic = cdSpriteImage;
        sprBtn.transition = Selectable.Transition.None;
        sprBtn.onClick.AddListener(() => PlayCollectionDetailAction());
        cdAnimator = spr.AddComponent<CollectionBossAnimator>();
        cdAnimator.target = cdSpriteImage;
        CollectionZoom zoom = spr.AddComponent<CollectionZoom>();
        zoom.target = sprR;

        cdCostTag = CreateLabel(frame, "CostTag", "", new Vector2(0f, 0.91f), new Vector2(0.5f, 1f), 13f, FontStyles.Bold, new Color(0.62f, 0.77f, 0.94f), TextAlignmentOptions.TopLeft, false);
        cdChapterTag = CreateLabel(frame, "ChapterTag", "", new Vector2(0.5f, 0.91f), new Vector2(1f, 1f), 13f, FontStyles.Bold, new Color(0.33f, 0.81f, 0.64f), TextAlignmentOptions.TopRight, false);
        cdActionHint = CreateLabel(frame, "ActionHint", "", new Vector2(0f, 0.005f), new Vector2(1f, 0.085f), 12f, FontStyles.Normal, new Color(0.5f, 0.62f, 0.78f), TextAlignmentOptions.Center, false);

        cdName = CreateLabel(root, "Name", "", new Vector2(0.06f, 0.225f), new Vector2(0.40f, 0.29f), 24f, FontStyles.Bold, new Color(0.95f, 0.97f, 1f), TextAlignmentOptions.Center, false);
        cdNameEn = CreateLabel(root, "NameEn", "", new Vector2(0.06f, 0.185f), new Vector2(0.40f, 0.225f), 12f, FontStyles.Normal, new Color(0.44f, 0.55f, 0.69f), TextAlignmentOptions.Center, false);

        GameObject synRow = new GameObject("SynergyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        synRow.transform.SetParent(root, false);
        RectTransform synR = synRow.GetComponent<RectTransform>();
        synR.anchorMin = new Vector2(0.06f, 0.10f); synR.anchorMax = new Vector2(0.40f, 0.175f);
        synR.offsetMin = Vector2.zero; synR.offsetMax = Vector2.zero;
        HorizontalLayoutGroup hl = synRow.GetComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 8f;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
        cdSynergyRow = synRow.transform;

        // 右：育成。3段（見出し＋ボーナス / 育成Lv / 節目）に分け、節目テキストはパネル全幅で折返しして
        // 育成Lv へ被らないようにする（旧実装は右半分1行に長文を詰めて左へあふれていた）。
        RectTransform affPanel = CreatePanel(root, "AffinityPanel", new Vector2(0.44f, 0.70f), new Vector2(0.94f, 0.86f), new Color(0.055f, 0.086f, 0.149f, 1f));
        CreateLabel(affPanel, "Head", ja ? "育成（アフィニティ）" : "Affinity",
            new Vector2(0.02f, 0.66f), new Vector2(0.55f, 0.98f), 14f, FontStyles.Bold, new Color(0.905f, 0.76f, 0.30f), TextAlignmentOptions.Left, false);
        cdAffBonus = CreateLabel(affPanel, "Bonus", "", new Vector2(0.55f, 0.66f), new Vector2(0.98f, 0.98f), 14f, FontStyles.Bold, new Color(0.5f, 0.88f, 0.75f), TextAlignmentOptions.Right, false);
        cdAffLv = CreateLabel(affPanel, "Lv", "", new Vector2(0.02f, 0.40f), new Vector2(0.98f, 0.64f), 19f, FontStyles.Bold, new Color(0.95f, 0.86f, 0.45f), TextAlignmentOptions.Left, false);
        cdAffCount = CreateLabel(affPanel, "Count", "", new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.38f), 12f, FontStyles.Normal, new Color(0.6f, 0.7f, 0.84f), TextAlignmentOptions.Left, true);

        // 右：ステータス
        RectTransform stPanel = CreatePanel(root, "StatsPanel", new Vector2(0.44f, 0.40f), new Vector2(0.94f, 0.68f), new Color(0.055f, 0.086f, 0.149f, 1f));
        CreateLabel(stPanel, "Head", ja ? "ステータス（育成補正込み）" : "Stats (with affinity)",
            new Vector2(0.02f, 0.82f), new Vector2(0.98f, 0.99f), 14f, FontStyles.Bold, new Color(0.62f, 0.77f, 0.94f), TextAlignmentOptions.Left, false);
        Color stc = new Color(0.85f, 0.90f, 0.97f);
        cdHp = CreateLabel(stPanel, "Hp", "", new Vector2(0.03f, 0.56f), new Vector2(0.50f, 0.80f), 15f, FontStyles.Normal, stc, TextAlignmentOptions.Left, false);
        cdAtk = CreateLabel(stPanel, "Atk", "", new Vector2(0.52f, 0.56f), new Vector2(0.98f, 0.80f), 15f, FontStyles.Normal, stc, TextAlignmentOptions.Left, false);
        cdAtkSpd = CreateLabel(stPanel, "AtkSpd", "", new Vector2(0.03f, 0.30f), new Vector2(0.50f, 0.54f), 15f, FontStyles.Normal, stc, TextAlignmentOptions.Left, false);
        cdRange = CreateLabel(stPanel, "Range", "", new Vector2(0.52f, 0.30f), new Vector2(0.98f, 0.54f), 15f, FontStyles.Normal, stc, TextAlignmentOptions.Left, false);
        cdDr = CreateLabel(stPanel, "Dr", "", new Vector2(0.03f, 0.04f), new Vector2(0.50f, 0.28f), 15f, FontStyles.Normal, stc, TextAlignmentOptions.Left, false);
        cdMana = CreateLabel(stPanel, "Mana", "", new Vector2(0.52f, 0.04f), new Vector2(0.98f, 0.28f), 15f, FontStyles.Normal, stc, TextAlignmentOptions.Left, false);

        // 右：スキル
        RectTransform skPanel = CreatePanel(root, "SkillPanel", new Vector2(0.44f, 0.205f), new Vector2(0.94f, 0.385f), new Color(0.055f, 0.086f, 0.149f, 1f));
        cdSkillTitle = CreateLabel(skPanel, "Title", "", new Vector2(0.02f, 0.70f), new Vector2(0.98f, 0.98f), 15f, FontStyles.Bold, new Color(0.5f, 0.82f, 0.93f), TextAlignmentOptions.Left, false);
        cdSkillBody = CreateLabel(skPanel, "Body", "", new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.68f), 13f, FontStyles.Normal, new Color(0.72f, 0.79f, 0.9f), TextAlignmentOptions.TopLeft, true);

        // 右：紹介文（ロア）
        RectTransform flPanel = CreatePanel(root, "FlavorPanel", new Vector2(0.44f, 0.06f), new Vector2(0.94f, 0.19f), new Color(0.04f, 0.065f, 0.11f, 1f));
        cdFlavor = CreateLabel(flPanel, "Flavor", "", new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.95f), 13f, FontStyles.Italic, new Color(0.66f, 0.74f, 0.86f), TextAlignmentOptions.Left, true);

        CreateLabel(root, "Hint", ja ? "‹ › で前後のボスへ / 戻るで一覧へ" : "‹ › prev/next · Back to grid",
            new Vector2(0.30f, 0.015f), new Vector2(0.70f, 0.05f), 11f, FontStyles.Normal, new Color(0.37f, 0.48f, 0.63f), TextAlignmentOptions.Center, false);

        // アイコン全画面拡大オーバーレイ（既定は非表示・最前面でクリックで閉じる）。
        cdIconOverlay = new GameObject("IconOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        cdIconOverlay.transform.SetParent(root, false);
        RectTransform ovR = cdIconOverlay.GetComponent<RectTransform>();
        ovR.anchorMin = Vector2.zero; ovR.anchorMax = Vector2.one; ovR.offsetMin = Vector2.zero; ovR.offsetMax = Vector2.zero;
        Image ovBg = cdIconOverlay.GetComponent<Image>();
        ovBg.color = new Color(0.01f, 0.02f, 0.03f, 0.94f);
        Button ovBtn = cdIconOverlay.GetComponent<Button>();
        ovBtn.targetGraphic = ovBg; ovBtn.transition = Selectable.Transition.None;
        ovBtn.onClick.AddListener(() => CloseIconFullscreen());
        GameObject big = new GameObject("IconBig", typeof(RectTransform), typeof(Image));
        big.transform.SetParent(cdIconOverlay.transform, false);
        RectTransform bigR = big.GetComponent<RectTransform>();
        bigR.anchorMin = new Vector2(0.12f, 0.12f); bigR.anchorMax = new Vector2(0.88f, 0.92f);
        bigR.offsetMin = Vector2.zero; bigR.offsetMax = Vector2.zero;
        cdIconBig = big.GetComponent<Image>();
        cdIconBig.preserveAspect = true; cdIconBig.raycastTarget = false; cdIconBig.color = Color.white;
        CreateLabel(cdIconOverlay.transform, "CloseHint", ja ? "クリックで閉じる" : "Click to close",
            new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.08f), 13f, FontStyles.Normal, new Color(0.6f, 0.7f, 0.84f), TextAlignmentOptions.Center, false);
        cdIconOverlay.SetActive(false);
    }

    private void ShowCollectionDetail(string unitId)
    {
        EnsureUi();
        cdOwnedIds.Clear();
        List<string> all = GameManager.GetAllChapterBossRewardUnitIds();
        for (int i = 0; i < all.Count; i++)
            if (SaveManager.Instance != null && SaveManager.Instance.HasBossAlly(all[i]))
                cdOwnedIds.Add(all[i]);
        cdIndex = Mathf.Max(0, cdOwnedIds.IndexOf(unitId));
        SetView(collectionDetailView);
        RefreshCollectionDetail();
    }

    private void CollectionDetailStep(int dir)
    {
        if (cdOwnedIds.Count == 0) return;
        cdIndex = (cdIndex + dir + cdOwnedIds.Count) % cdOwnedIds.Count;
        RefreshCollectionDetail();
    }

    private void CleanupCollectionDetailEntity()
    {
        if (cdAnimator != null) cdAnimator.source = null;
        if (cdPreviewEntityObj != null) { Destroy(cdPreviewEntityObj); cdPreviewEntityObj = null; }
    }

    private void BuildSynergyChips(EntitiesDatabaseSO.EntityData data)
    {
        if (cdSynergyRow == null) return;
        for (int i = cdSynergyRow.childCount - 1; i >= 0; i--)
            Destroy(cdSynergyRow.GetChild(i).gameObject);
        AddSynergyChip(data.synergy1);
        AddSynergyChip(data.synergy2);
        AddSynergyChip(data.synergy3);
    }

    private void AddSynergyChip(SynergyType type)
    {
        if (type == SynergyType.None || cdSynergyRow == null) return;
        GameObject chip = new GameObject("Chip", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        chip.transform.SetParent(cdSynergyRow, false);
        chip.GetComponent<Image>().color = new Color(0.06f, 0.11f, 0.19f, 1f);
        LayoutElement le = chip.GetComponent<LayoutElement>();
        le.preferredWidth = 118f; le.preferredHeight = 38f;
        CreateLabel(chip.transform, "T", LocalizationManager.SynergyName(type), Vector2.zero, Vector2.one,
            14f, FontStyles.Bold, new Color(0.74f, 0.84f, 0.96f), TextAlignmentOptions.Center, false);
    }

    private void RefreshCollectionDetail()
    {
        if (cdOwnedIds.Count == 0) return;
        bool ja = LocalizationManager.IsJapanese;
        string id = cdOwnedIds[cdIndex];

        List<string> all = GameManager.GetAllChapterBossRewardUnitIds();
        int chapterNo = all.IndexOf(id) + 1;

        EntitiesDatabaseSO db = Resources.Load<EntitiesDatabaseSO>("Entity Database");
        EntitiesDatabaseSO.EntityData data = default;
        if (db != null && db.allEntities != null)
            for (int i = 0; i < db.allEntities.Count; i++)
                if (string.Equals(db.allEntities[i].name, id, System.StringComparison.OrdinalIgnoreCase))
                { data = db.allEntities[i]; break; }

        CleanupCollectionDetailEntity();
        BaseEntity ent = null;
        if (data.prefab != null)
        {
            ent = Instantiate(data.prefab);
            ent.transform.position = new Vector3(-100000f, -100000f, 0f);
            ent.InitializeIdentity(id, Mathf.Max(1, data.cost), 1);
            cdPreviewEntityObj = ent.gameObject;
            if (cdAnimator != null) { cdAnimator.source = ent.spriteRender; cdAnimator.target = cdSpriteImage; }
            if (cdSpriteImage != null && ent.spriteRender != null)
            {
                cdSpriteImage.sprite = ent.spriteRender.sprite;
                cdSpriteImage.enabled = ent.spriteRender.sprite != null;
            }
        }

        // ショップアイコン（大表示用）をセットし、既定はアニメ表示に戻す。
        cdShowAbilityNext = false;
        cdCurrentIcon = data.icon;
        if (cdIconImage != null) cdIconImage.sprite = data.icon;
        if (cdIconOverlay != null) cdIconOverlay.SetActive(false);
        if (cdSpriteImage != null) cdSpriteImage.rectTransform.localScale = Vector3.one; // 拡大リセット
        SetCollectionDetailMode(true);

        // 利用できるモーションだけを巡回リストに入れる。
        cdMotions.Clear();
        cdMotions.Add("idle");
        if (ent != null && ent.HasWalkAnimation) cdMotions.Add("walk");
        cdMotions.Add("attack");
        if (ent != null && ent.HasAbilityAnimation) cdMotions.Add("ability");
        if (ent != null && ent.HasDeathAnimation) cdMotions.Add("death");
        cdMotionIndex = 0;
        if (cdActionHint != null) cdActionHint.text = MotionLabel("idle");

        cdName.text = LocalizationManager.UnitName(id);
        cdNameEn.text = LocalizationManager.CleanUnitName(id).ToUpperInvariant();
        cdCostTag.text = (ja ? "コスト " : "COST ") + Mathf.Max(1, data.cost);
        cdChapterTag.text = ja ? $"第{chapterNo}章ボス" : $"CH.{chapterNo} BOSS";

        BuildSynergyChips(data);

        int lv = SaveManager.Instance != null ? SaveManager.Instance.GetBossAffinityLevel(id) : 1;
        float mul = SaveManager.Instance != null ? SaveManager.Instance.GetBossAffinityStatMultiplier(id) : 1f;
        int bonus = Mathf.RoundToInt((mul - 1f) * 100f);
        cdAffLv.text = (ja ? "育成 Lv " : "Affinity Lv ") + Mathf.Max(1, lv);
        cdAffBonus.text = (ja ? "ステータス " : "Stats ") + "+" + bonus + "%";
        // 育成（アフィニティ）節目の固有パッシブ。達成は●、未達は○。
        string p3 = lv >= 3 ? "●" : "○";
        string p5 = lv >= 5 ? "●" : "○";
        string p8 = lv >= 8 ? "●" : "○";
        // 全幅で折返すため、区切りは「 / 」にして折返し位置を読みやすくする。
        cdAffCount.text = ja
            ? $"節目: {p3}Lv3 開幕シールド / {p5}Lv5 攻撃速度 / {p8}Lv8 与ダメ"
            : $"Milestones: {p3}Lv3 Shield / {p5}Lv5 Spd / {p8}Lv8 DMG";

        if (ent != null)
        {
            int hp = ent.MaxHealth, atk = ent.baseDamage;
            int hpB = Mathf.RoundToInt(hp * mul), atkB = Mathf.RoundToInt(atk * mul);
            cdHp.text = (ja ? "体力 " : "HP ") + hp + (mul > 1f ? " → " + hpB : "");
            cdAtk.text = (ja ? "攻撃力 " : "ATK ") + atk + (mul > 1f ? " → " + atkB : "");
            cdAtkSpd.text = (ja ? "攻撃速度 " : "ATK SPD ") + ent.attackSpeed.ToString("0.00") + "/s";
            cdRange.text = (ja ? "射程 " : "Range ") + ent.range + (ent.range >= 4 ? (ja ? "（遠）" : " (ranged)") : (ja ? "（近）" : " (melee)"));
            cdDr.text = (ja ? "被ダメ軽減 " : "DR ") + LocalizationManager.FormatPercent(ent.DamageReduction);
            cdMana.text = (ja ? "マナ " : "Mana ") + "0 / " + ent.MaxMana;
            cdSkillTitle.text = (ja ? "スキル：" : "Skill: ") + UnitStatusPanelUI.GetSkillTitleFor(ent);
            cdSkillBody.text = UnitStatusPanelUI.GetSkillBodyFor(ent);
        }

        cdFlavor.text = LocalizationManager.BossFlavor(id);
    }

    // ---------- ①.5 メインロビー（スロット選択後のセーブ別ハブ・案A：立ち絵主役＋右情報/縦ボタン） ----------
    private void ShowMainLobby()
    {
        EnsureUi();
        SaveManager.EnsureExists();
        bool ja = LocalizationManager.IsJapanese;
        string hid = SaveManager.Instance != null ? SaveManager.Instance.GetHeroUnitId() : string.Empty;

        // 主人公の立ち絵（HeroArt）。未選択時は非表示。
        if (mainLobbyHeroArt != null)
        {
            Sprite art = string.IsNullOrEmpty(hid) ? null : DialogArt.Portrait(hid);
            mainLobbyHeroArt.sprite = art;
            mainLobbyHeroArt.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        // セーブ情報（スロット/最高章/主人公＋育成Lv/ポイント）。
        if (mainLobbyHeroText != null)
        {
            int slot = SaveManager.Instance != null ? SaveManager.Instance.ActiveSlot + 1 : 1;
            int cleared = 0;
            if (SaveManager.Instance != null)
                for (int c = 1; c <= 13; c++)
                    if (SaveManager.Instance.IsChapterUnlocked(c + 1)) cleared++;
            string heroName = string.IsNullOrEmpty(hid) ? (ja ? "未選択" : "none") : LocalizationManager.UnitName(hid);
            int mlv = (SaveManager.Instance != null && !string.IsNullOrEmpty(hid)) ? SaveManager.Instance.GetHeroMasteryLevel(hid) : 1;
            mainLobbyHeroText.text = ja
                ? $"スロット{slot}　・　最高 第{cleared}章クリア\n主人公：{heroName}（熟練度 Lv {mlv}）"
                : $"Slot {slot}   ・   Cleared ch.{cleared}\nHero: {heroName}  (Mastery Lv {mlv})";
        }
        SetView(mainLobbyView);
    }

    private void BuildMainLobbyView()
    {
        mainLobbyView = NewFullRect("MainLobbyView");
        mainLobbyView.SetActive(false);
        bool ja = LocalizationManager.IsJapanese;

        // 左：主人公の立ち絵（縦長・左寄せ）。
        GameObject artGo = new GameObject("HeroArt", typeof(RectTransform), typeof(Image));
        artGo.transform.SetParent(mainLobbyView.transform, false);
        RectTransform ar = artGo.GetComponent<RectTransform>();
        ar.anchorMin = new Vector2(0f, 0f); ar.anchorMax = new Vector2(0.44f, 1f);
        ar.offsetMin = new Vector2(40f, 0f); ar.offsetMax = new Vector2(0f, 0f);
        mainLobbyHeroArt = artGo.GetComponent<Image>();
        mainLobbyHeroArt.preserveAspect = true;
        mainLobbyHeroArt.raycastTarget = false;

        // 右：タイトル「ロビー」。
        CreateLabel(mainLobbyView.transform, "Logo", ja ? "ロビー" : "LOBBY",
            new Vector2(0.46f, 0.82f), new Vector2(0.98f, 0.95f), 50f, FontStyles.Bold,
            new Color(0.97f, 0.9f, 0.6f), TextAlignmentOptions.Left, false);

        // 右：セーブ情報カード。
        RectTransform info = CreatePanel(mainLobbyView.transform, "Info",
            new Vector2(0.46f, 0.6f), new Vector2(0.98f, 0.79f), new Color(0.06f, 0.1f, 0.16f, 0.92f));
        mainLobbyHeroText = CreateLabel(info, "InfoText", "",
            new Vector2(0.05f, 0f), new Vector2(0.96f, 1f), 22f, FontStyles.Normal,
            new Color(0.86f, 0.92f, 1f), TextAlignmentOptions.Left, true);

        // 右：プレイ（大・金）。
        CreateLobbyActionButton("PlayBtn", ja ? "▶  プレイ" : "▶  PLAY",
            new Vector2(0.46f, 0.45f), new Vector2(0.98f, 0.57f),
            new Color(0.79f, 0.63f, 0.29f, 1f), new Color(0.22f, 0.17f, 0.04f), 30f,
            () => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); ShowLobby(); });

        // 右：ヒーロー育成 / 図鑑（横2分割）。
        CreateLobbyActionButton("TrainBtn", ja ? "ヒーロー育成" : "HERO TRAINING",
            new Vector2(0.46f, 0.32f), new Vector2(0.715f, 0.43f),
            new Color(0.13f, 0.2f, 0.31f, 0.95f), new Color(0.82f, 0.9f, 1f), 20f,
            () => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); ShowHeroUpgrade(); });
        CreateLobbyActionButton("CodexBtn", ja ? "図鑑" : "COLLECTION",
            new Vector2(0.725f, 0.32f), new Vector2(0.98f, 0.43f),
            new Color(0.13f, 0.2f, 0.31f, 0.95f), new Color(0.82f, 0.9f, 1f), 20f,
            () => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); ShowCollection(); });

        // 右：タイトルへ（小）。
        CreateLobbyActionButton("ToTitleBtn", ja ? "タイトルへ" : "TO TITLE",
            new Vector2(0.46f, 0.23f), new Vector2(0.98f, 0.3f),
            new Color(0.16f, 0.18f, 0.24f, 0.9f), new Color(0.78f, 0.82f, 0.9f), 18f,
            () => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); ShowTitle(); });
    }

    // メインロビーの塗りつぶしボタン（背景色＋中央ラベル＋ホバー感触）。
    private void CreateLobbyActionButton(string name, string label, Vector2 aMin, Vector2 aMax,
        Color bg, Color textColor, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonJuice));
        go.transform.SetParent(mainLobbyView.transform, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>(); img.color = bg;
        Button btn = go.GetComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        TextMeshProUGUI t = CreateLabel(go.transform, "L", label, Vector2.zero, Vector2.one,
            fontSize, FontStyles.Bold, textColor, TextAlignmentOptions.Center, false);
        t.raycastTarget = false;
    }

    // ---------- ②ロビー選択（大型モードカード） ----------
    private void BuildLobbyView()
    {
        lobbyView = NewFullRect("LobbyView");
        lobbyView.SetActive(false);
        bool ja = LocalizationManager.IsJapanese;

        CreateBackButton(lobbyView.transform, () => ShowMainLobby());

        GameObject row = new GameObject("ModeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(lobbyView.transform, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, -10f);
        rowRect.sizeDelta = new Vector2(1500f, 760f);
        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 48f;
        // childControl=true でないと LayoutElement.preferred(W/H) が無視され、カードが潰れる。
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        modeRow = row.transform;

        CreateImageCard(row.transform, "ChapterCard", Resources.Load<Sprite>("Play/play_mode_Chapter"),
            ja ? "チャプター" : "CHAPTER",
            ja ? "各章のボスを攻略して進む" : "Conquer each chapter's boss.",
            false, true, () => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); ShowChapters(); }, 400f, 720f);

        CreateImageCard(row.transform, "CoreWarCard", Resources.Load<Sprite>("Play/play_mode_BossRush"),
            ja ? "コア戦" : "CORE WAR",
            ja ? "敵のコアを破壊して勝利" : "Destroy the enemy core to win.",
            false, true, () => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); StartCoreMode(); }, 400f, 720f);

        CreateImageCard(row.transform, "UnitCard", Resources.Load<Sprite>("Play/play_mode_UnitFormation"),
            ja ? "ユニット編成" : "UNIT FORMATION",
            ja ? "コレクション・ショップ選抜" : "Collection & Shop Pool",
            false, true, () => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); CollectionHubUI.EnsureExists().Open(null); }, 400f, 720f);
    }

    // ---------- ②.5 出撃準備（チャプター選択後） ----------
    private void ShowChapterPrep(int chapter)
    {
        EnsureUi();
        SaveManager.EnsureExists();
        chapterPrepChapter = Mathf.Max(1, chapter);
        bool ja = LocalizationManager.IsJapanese;
        string hid = SaveManager.Instance != null ? SaveManager.Instance.GetHeroUnitId() : string.Empty;
        if (string.IsNullOrEmpty(hid)) hid = "HeroAldin";

        if (chapterPrepTitle != null)
            chapterPrepTitle.text = ja ? $"出撃準備 ・ 第{chapterPrepChapter}章" : $"PREPARE ・ Chapter {chapterPrepChapter}";
        if (chapterPrepHeroArt != null)
        {
            Sprite art = DialogArt.Portrait(hid);
            chapterPrepHeroArt.sprite = art;
            chapterPrepHeroArt.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
        if (chapterPrepHeroText != null)
        {
            int mlv = SaveManager.Instance != null ? SaveManager.Instance.GetHeroMasteryLevel(hid) : 1;
            chapterPrepHeroText.text = (ja ? "主人公： " : "Hero: ") + LocalizationManager.UnitName(hid)
                + "\n" + (ja ? "熟練度 Lv " : "Mastery Lv ") + mlv;
        }
        SetView(chapterPrepView);
    }

    private void BuildChapterPrepView()
    {
        chapterPrepView = NewFullRect("ChapterPrepView");
        chapterPrepView.SetActive(false);
        bool ja = LocalizationManager.IsJapanese;

        chapterPrepTitle = CreateLabel(chapterPrepView.transform, "Title", "",
            new Vector2(0f, 0.88f), new Vector2(1f, 0.98f), 40f, FontStyles.Bold,
            new Color(0.97f, 0.9f, 0.6f), TextAlignmentOptions.Center, false);

        // 左：主人公の立ち絵。
        GameObject artGo = new GameObject("HeroArt", typeof(RectTransform), typeof(Image));
        artGo.transform.SetParent(chapterPrepView.transform, false);
        RectTransform ar = artGo.GetComponent<RectTransform>();
        ar.anchorMin = new Vector2(-0.06f, 0.12f); ar.anchorMax = new Vector2(0.48f, 0.86f);
        ar.offsetMin = Vector2.zero; ar.offsetMax = Vector2.zero;
        chapterPrepHeroArt = artGo.GetComponent<Image>();
        chapterPrepHeroArt.preserveAspect = true; chapterPrepHeroArt.raycastTarget = false;

        // 右：情報＋ボタン。
        RectTransform panel = CreatePanel(chapterPrepView.transform, "Info",
            new Vector2(0.5f, 0.46f), new Vector2(0.97f, 0.82f), new Color(0.06f, 0.1f, 0.16f, 0.94f));
        chapterPrepHeroText = CreateLabel(panel, "HeroText", "",
            new Vector2(0.06f, 0.45f), new Vector2(0.94f, 0.95f), 24f, FontStyles.Normal,
            new Color(0.88f, 0.93f, 1f), TextAlignmentOptions.TopLeft, true);

        // ショップ編成（コレクション＋ショップ選抜ハブを開く）。
        CreateLobbyActionButtonOn(chapterPrepView.transform, "ShopEditBtn",
            ja ? "ショップに出すユニットを選ぶ" : "Choose shop units",
            new Vector2(0.5f, 0.37f), new Vector2(0.97f, 0.44f),
            new Color(0.2f, 0.34f, 0.5f, 0.95f), Color.white, 18f,
            () => CollectionHubUI.EnsureExists().Open(null));

        // ヒーロー変更（育成画面へ。戻り先は準備画面）。
        CreateLobbyActionButtonOn(chapterPrepView.transform, "HeroChangeBtn",
            ja ? "ヒーローを変更・育成" : "Change / Train Hero",
            new Vector2(0.5f, 0.28f), new Vector2(0.97f, 0.355f),
            new Color(0.2f, 0.34f, 0.5f, 0.95f), Color.white, 20f,
            () => ShowHeroUpgrade(true));

        // クエスト出発！
        CreateLobbyActionButtonOn(chapterPrepView.transform, "StartBtn",
            ja ? "クエスト出発！" : "START QUEST!",
            new Vector2(0.5f, 0.14f), new Vector2(0.97f, 0.27f),
            new Color(0.86f, 0.45f, 0.16f, 1f), Color.white, 30f,
            () => { AttackEffectPlayer.PlayUiSfx("fight_start"); StartChapter(chapterPrepChapter); });

        CreateBackButton(chapterPrepView.transform, () => ShowChapters());
    }

    // 任意の親に置ける塗りボタン（CreateLobbyActionButton のメインロビー固定版とは別に汎用化）。
    private void CreateLobbyActionButtonOn(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax,
        Color bg, Color textColor, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonJuice));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>(); img.color = bg;
        Button btn = go.GetComponent<Button>(); btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick); else btn.interactable = false;
        TextMeshProUGUI t = CreateLabel(go.transform, "L", label, Vector2.zero, Vector2.one,
            fontSize, FontStyles.Bold, textColor, TextAlignmentOptions.Center, false);
        t.raycastTarget = false;
    }

    // ---------- ③チャプター選択 ----------
    private void BuildChapterView()
    {
        chapterView = NewFullRect("ChapterView");
        chapterView.SetActive(false);
        bool ja = LocalizationManager.IsJapanese;

        CreateLabel(chapterView.transform, "Title", ja ? "チャプター選択" : "SELECT CHAPTER",
            new Vector2(0f, 0.88f), new Vector2(1f, 0.98f), 36f, FontStyles.Bold,
            new Color(0.92f, 0.95f, 1f), TextAlignmentOptions.Center, false);

        CreateBackButton(chapterView.transform, () => ShowLobby());

        GameObject scrollGo = new GameObject("ChapterScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(chapterView.transform, false);
        RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRect.pivot = new Vector2(0.5f, 0.5f);
        scrollRect.anchoredPosition = new Vector2(0f, -20f);
        scrollRect.sizeDelta = new Vector2(1720f, 660f);
        Image scrollBg = scrollGo.GetComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.2f);
        ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
        sr.horizontal = true; sr.vertical = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollGo.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0.5f);
        contentRect.anchorMax = new Vector2(0f, 0.5f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 620f);
        HorizontalLayoutGroup chlg = content.GetComponent<HorizontalLayoutGroup>();
        chlg.childAlignment = TextAnchor.MiddleLeft;
        chlg.spacing = 30f;
        chlg.padding = new RectOffset(34, 34, 0, 0);
        // childControl=true でないと LayoutElement.preferred(W/H) が無視され、カードが潰れる。
        chlg.childControlWidth = true; chlg.childControlHeight = true;
        chlg.childForceExpandWidth = false; chlg.childForceExpandHeight = false;
        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        sr.content = contentRect;
        chapterCardsParent = content.transform;
    }

    private void BuildChapterCards()
    {
        if (chapterCardsParent == null)
            return;
        // 解放状況（章クリア）の変化を常に反映するため、開くたびに作り直す。
        for (int i = chapterCardsParent.childCount - 1; i >= 0; i--)
            Destroy(chapterCardsParent.GetChild(i).gameObject);

        bool ja = LocalizationManager.IsJapanese;

        Sprite[] all = Resources.LoadAll<Sprite>("Play/Chapter");
        List<Sprite> gates = new List<Sprite>();
        foreach (Sprite s in all)
            if (s != null && !s.name.Contains("@2x")) gates.Add(s);
        gates.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        for (int i = 0; i < gates.Count; i++)
            CreateChapterCard(chapterCardsParent, i + 1, gates[i], ja);

        chapterCardsBuilt = true;
    }

    private void CreateChapterCard(Transform parent, int chapter, Sprite sprite, bool ja)
    {
        bool comingSoon = chapter > PlayableChapterCount;
        bool unlocked = false, cleared = false;
        int best = 0;
        if (!comingSoon)
        {
            unlocked = SaveManager.Instance == null || SaveManager.Instance.IsChapterUnlocked(chapter);
            ChapterRecord rec = SaveManager.Instance != null ? SaveManager.Instance.GetChapter(chapter) : null;
            if (rec != null) { cleared = rec.cleared; best = rec.bestScore; }
        }

        bool clickable = !comingSoon && unlocked;
        bool darken = comingSoon || !unlocked;

        string title = ja ? $"第{chapter}章" : $"Chapter {chapter}";
        string desc;
        if (comingSoon) desc = "Coming Soon";
        else if (!unlocked) desc = ja ? "前章をクリアで解放" : "Clear previous chapter";
        else if (cleared) desc = (ja ? "ベスト " : "Best ") + best;
        else desc = ja ? "挑戦可能" : "Available";

        int captured = chapter;
        UnityEngine.Events.UnityAction onClick = clickable
            ? (UnityEngine.Events.UnityAction)(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); ShowChapterPrep(captured); })
            : null;

        CreateImageCard(parent, "Chapter" + chapter + "Card", sprite, title, desc, darken, clickable, onClick, 330f, 600f);
    }

    // Duelyst風の大型カード：画像＋下部に半透明帯＋タイトル＋説明。暗転/クリック可否対応。
    private GameObject CreateImageCard(Transform parent, string name, Sprite sprite, string title, string desc,
        bool darken, bool clickable, UnityEngine.Events.UnityAction onClick, float width, float height)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        card.transform.SetParent(parent, false);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredWidth = width; le.preferredHeight = height;

        Image img = card.GetComponent<Image>();
        if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Simple; img.preserveAspect = true; }
        img.color = darken ? new Color(0.42f, 0.42f, 0.48f, 1f) : Color.white;

        Button btn = card.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = clickable;
        if (clickable)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.95f, 0.8f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.9f);
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(onClick);
            card.AddComponent<ButtonJuice>();
        }

        // 下部の半透明帯（タイトル＋説明の可読性確保）
        GameObject band = new GameObject("Band", typeof(RectTransform), typeof(Image));
        band.transform.SetParent(card.transform, false);
        RectTransform bandRect = band.GetComponent<RectTransform>();
        bandRect.anchorMin = new Vector2(0f, 0f); bandRect.anchorMax = new Vector2(1f, 0.34f);
        bandRect.offsetMin = Vector2.zero; bandRect.offsetMax = Vector2.zero;
        Image bandImg = band.GetComponent<Image>();
        bandImg.color = new Color(0f, 0f, 0f, 0.5f);
        bandImg.raycastTarget = false;

        if (!string.IsNullOrEmpty(title))
        {
            TextMeshProUGUI t = CreateLabel(card.transform, "Title", title, new Vector2(0f, 0.18f), new Vector2(1f, 0.33f),
                28f, FontStyles.Bold, new Color(0.98f, 0.98f, 1f), TextAlignmentOptions.Center, false);
            t.raycastTarget = false;
        }
        if (!string.IsNullOrEmpty(desc))
        {
            bool cs = desc == "Coming Soon";
            TextMeshProUGUI d = CreateLabel(card.transform, "Desc", desc, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.18f),
                cs ? 22f : 17f, cs ? FontStyles.Bold : FontStyles.Normal,
                cs ? new Color(1f, 0.82f, 0.4f) : new Color(0.85f, 0.9f, 1f), TextAlignmentOptions.Center, true);
            d.raycastTarget = false;
        }

        return card;
    }

    private void CreateBackButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        bool ja = LocalizationManager.IsJapanese;
        Sprite backSprite = Resources.Load<Sprite>("UI/Duelyst/button_back");

        GameObject back = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        back.transform.SetParent(parent, false);
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        Image backImg = back.GetComponent<Image>();
        Button backBtn = back.GetComponent<Button>();
        backBtn.targetGraphic = backImg;
        backBtn.onClick.AddListener(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); onClick(); });

        if (backSprite != null)
        {
            // duelyst の戻る矢印アイコンをそのまま使用。
            backRect.anchoredPosition = new Vector2(40f, -32f);
            backRect.sizeDelta = new Vector2(96f, 84f);
            backImg.sprite = backSprite;
            backImg.type = Image.Type.Simple;
            backImg.preserveAspect = true;
            backImg.color = Color.white;
            ColorBlock cb = backBtn.colors;
            cb.highlightedColor = new Color(1f, 0.96f, 0.8f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.9f);
            backBtn.colors = cb;
        }
        else
        {
            backRect.anchoredPosition = new Vector2(36f, -28f);
            backRect.sizeDelta = new Vector2(170f, 56f);
            backImg.color = new Color(0.14f, 0.18f, 0.28f, 0.94f);
            TextMeshProUGUI backLabel = CreateLabel(back.transform, "Label", ja ? "« 戻る" : "« BACK",
                Vector2.zero, Vector2.one, 22f, FontStyles.Bold, new Color(0.9f, 0.94f, 1f), TextAlignmentOptions.Center, false);
            backLabel.raycastTarget = false;
        }

        back.AddComponent<ButtonJuice>();
    }

    private void StartChapter(int chapter)
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestStartChapter(chapter);
            return;
        }
        GameManager.PendingStartChapter = Mathf.Max(1, chapter);
        SceneManager.LoadScene(GameSceneName);
    }

    // R2-coremode: コア戦モードを開始する。シーン再読込でモードを受け渡す。
    private void StartCoreMode()
    {
        Time.timeScale = 1f;
        GameManager.PendingMode = GameManager.GameMode.CoreAssault;
        GameManager.PendingStartChapter = 0;
        SceneManager.LoadScene(GameSceneName);
    }

    private GameObject NewFullRect(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        return go;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax,
        float fontSize, FontStyles style, Color color, TextAlignmentOptions align, bool wrap)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(6f, 0f);
        rect.offsetMax = new Vector2(-6f, 0f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = align;
        LocalizationManager.ApplyFont(label);
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color;
        label.raycastTarget = false;
        label.enableWordWrapping = wrap;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private void EnsureInputCanvas()
    {
        if (localCanvas == null)
            localCanvas = GetComponent<Canvas>();
        if (localCanvas == null)
            localCanvas = gameObject.AddComponent<Canvas>();

        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = SortingOrder;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }
}

// ボタン/カードの「感触」：ホバーで拡大、押下で縮み、離すと弾む（DOTween, timeScale非依存）。
// Selectable があり interactable=false の場合は反応しない（Coming Soon カード等）。
public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 baseScale = Vector3.one;
    private Selectable selectable;

    private void Awake()
    {
        baseScale = transform.localScale;
        selectable = GetComponent<Selectable>();
    }

    private bool Usable => selectable == null || selectable.interactable;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!Usable) return;
        transform.DOKill();
        transform.DOScale(baseScale * 1.06f, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!Usable) return;
        transform.DOKill();
        transform.DOScale(baseScale, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Usable) return;
        transform.DOKill();
        transform.DOScale(baseScale * 0.93f, 0.08f).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!Usable) return;
        transform.DOKill();
        transform.DOScale(baseScale * 1.06f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
    }
}

// R1-collection: 図鑑詳細の大スプライト用。盤外で待機アニメを再生中のボス実体の
// SpriteRenderer.sprite を、毎フレーム UI の Image へ写してアニメ表示する。
public class CollectionBossAnimator : MonoBehaviour
{
    public SpriteRenderer source;
    public Image target;

    private void LateUpdate()
    {
        if (source == null || target == null)
            return;
        Sprite s = source.sprite;
        if (s != null)
        {
            target.sprite = s;
            if (!target.enabled) target.enabled = true;
            // スプライトの向き（左右反転）も反映。
            float sx = Mathf.Abs(target.rectTransform.localScale.x);
            target.rectTransform.localScale = new Vector3(source.flipX ? -sx : sx, target.rectTransform.localScale.y, target.rectTransform.localScale.z);
        }
    }
}

// R1-collection: 図鑑詳細の大スプライトを、ホイールスクロールで枠内拡大/縮小する。
public class CollectionZoom : MonoBehaviour, IScrollHandler
{
    public RectTransform target;
    public float min = 1f, max = 3f, step = 0.18f;

    public void OnScroll(PointerEventData e)
    {
        if (target == null) return;
        float mag = Mathf.Abs(target.localScale.x);
        float s = Mathf.Clamp(mag + e.scrollDelta.y * step, min, max);
        // 左右反転（flipX）を壊さないよう、x符号は現状を維持する。
        float signedX = target.localScale.x < 0 ? -s : s;
        target.localScale = new Vector3(signedX, s, 1f);
    }
}