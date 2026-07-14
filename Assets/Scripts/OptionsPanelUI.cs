using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 画面中央に出る、ゲーム全体のオプションパネルです。
// マスター/BGM/SFXの音量、ミュート、言語、ゲーム速度、ヘルプ、再挑戦をまとめます。
// ESCで開閉、開いている間は時間を停止します。設定は PlayerPrefs で永続化します。
public class OptionsPanelUI : MonoBehaviour
{
    public static OptionsPanelUI Instance { get; private set; }

    private const string PrefKeyMasterVolume = "options.masterVolume";
    private const string PrefKeyBgmVolume = "options.bgmVolume";
    private const string PrefKeySfxVolume = "options.sfxVolume";
    private const string PrefKeyMuted = "options.muted";
    private const string PrefKeyGameSpeed = "options.gameSpeed";

    // ルートUI
    private RectTransform panelRect;
    private CanvasGroup panelGroup;
    private GameObject dimObject;

    // ヘッダ
    private TextMeshProUGUI titleText;

    // 音量
    private TextMeshProUGUI audioSectionLabel;
    private TextMeshProUGUI masterRowLabel;
    private TextMeshProUGUI bgmRowLabel;
    private TextMeshProUGUI sfxRowLabel;
    private Slider masterSlider;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private TextMeshProUGUI masterValueText;
    private TextMeshProUGUI bgmValueText;
    private TextMeshProUGUI sfxValueText;
    private Button muteButton;
    private TextMeshProUGUI muteButtonText;

    // 言語
    private TextMeshProUGUI languageLabelText;
    private Button langJpButton;
    private Button langEnButton;
    private TextMeshProUGUI langJpText;
    private TextMeshProUGUI langEnText;

    // ゲーム速度
    private TextMeshProUGUI speedLabelText;
    private Button speed1Button;
    private Button speed15Button;
    private Button speed2Button;
    private TextMeshProUGUI speed1Text;
    private TextMeshProUGUI speed15Text;
    private TextMeshProUGUI speed2Text;

    // アクション
    private Button helpButton;
    private TextMeshProUGUI helpButtonText;
    private Button heroChangeButton;
    private TextMeshProUGUI heroChangeText;
    private Button lobbyButton;
    private TextMeshProUGUI lobbyText;
    private Button restartButton;
    private TextMeshProUGUI restartText;
    private Button closeButton;
    private TextMeshProUGUI closeText;
    private TextMeshProUGUI hintText;

    // ヘルプ画面
    private RectTransform helpPanelRect;
    private CanvasGroup helpPanelGroup;
    private TextMeshProUGUI helpTitleText;
    private TextMeshProUGUI helpBodyText;
    private Button helpCloseButton;
    private TextMeshProUGUI helpCloseText;

    // 状態
    private float currentSpeed = 1f;

    // 他UI（リザルト/オーグメント等）がポーズ解除後に復帰すべき「プレイヤー設定のゲーム速度」。
    // インスタンスがあればその値、無ければ PlayerPrefs から読む（チャプター全体で倍速を維持するため）。
    public static float DesiredGameSpeed
    {
        get
        {
            if (Instance != null) return Instance.currentSpeed;
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyGameSpeed, 1f), 0.25f, 4f);
        }
    }
    private float lastUnmutedMasterVolume = 1f;
    private bool muted;
    private bool isOpen;
    private bool isHelpOpen;
    private bool isBuilt;

    // 必要になった時に自動生成します。
    public static OptionsPanelUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        OptionsPanelUI existing = FindObjectOfType<OptionsPanelUI>();
        if (existing != null)
        {
            Instance = existing;
            existing.BuildIfNeeded();
            return existing;
        }

        GameObject go = new GameObject("OptionsPanelUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(OptionsPanelUI));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25000; // 16bit short上限(32767)内。
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        Instance = go.GetComponent<OptionsPanelUI>();
        Instance.BuildIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        LocalizationManager.EnsureExists();
        BuildIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        LocalizationManager.OnLanguageChanged -= RefreshLanguageLabels;
    }

    private void Update()
    {
        // オプション開閉キーは設定（キーコンフィグ）で変更可能。既定は Escape。
        if (Input.GetKeyDown(SettingsStore.GetBind("toggleOptions", KeyCode.Escape)))
        {
            if (isHelpOpen)
                HideHelp();
            else
                Toggle();
        }
    }

    public void Toggle() { SetOpen(!isOpen); }
    public void Show() { SetOpen(true); }
    public void Hide() { SetOpen(false); }

    private void BuildIfNeeded()
    {
        if (isBuilt)
            return;
        BuildUi();
        LoadSettings();
        ApplyAllSettings();
        RefreshLanguageLabels();
        RefreshSpeedHighlights();
        RefreshMuteButton();
        SetOpenInstant(false);
        SetHelpOpenInstant(false);
        LocalizationManager.OnLanguageChanged += RefreshLanguageLabels;
        isBuilt = true;
    }

    private void SetOpenInstant(bool open)
    {
        isOpen = open;
        if (dimObject != null)
            dimObject.SetActive(open);
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(open);
            if (panelGroup != null)
                panelGroup.alpha = open ? 1f : 0f;
            panelRect.localScale = Vector3.one;
        }
        Time.timeScale = open ? 0f : currentSpeed;
    }

    private void SetOpen(bool open)
    {
        if (panelRect == null)
            return;
        if (isOpen == open)
            return;

        isOpen = open;
        if (open)
        {
            if (dimObject != null) dimObject.SetActive(true);
            panelRect.gameObject.SetActive(true);
            panelGroup.alpha = 0f;
            panelRect.localScale = Vector3.one * 0.92f;
            panelGroup.DOFade(1f, 0.18f).SetUpdate(true);
            panelRect.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
            Time.timeScale = 0f;
        }
        else
        {
            // ヘルプも閉じる
            if (isHelpOpen)
                SetHelpOpenInstant(false);

            Time.timeScale = currentSpeed;
            panelGroup.DOFade(0f, 0.14f).SetUpdate(true);
            panelRect.DOScale(0.94f, 0.14f).SetUpdate(true).OnComplete(() =>
            {
                panelRect.gameObject.SetActive(false);
                if (dimObject != null && !isHelpOpen) dimObject.SetActive(false);
            });
        }
    }

    private void ShowHelp() { SetHelpOpen(true); }
    private void HideHelp() { SetHelpOpen(false); }

    private void SetHelpOpenInstant(bool open)
    {
        isHelpOpen = open;
        if (helpPanelRect != null)
        {
            helpPanelRect.gameObject.SetActive(open);
            if (helpPanelGroup != null)
                helpPanelGroup.alpha = open ? 1f : 0f;
            helpPanelRect.localScale = Vector3.one;
        }
    }

    private void SetHelpOpen(bool open)
    {
        if (helpPanelRect == null) return;
        if (isHelpOpen == open) return;
        isHelpOpen = open;
        if (open)
        {
            helpPanelRect.gameObject.SetActive(true);
            helpPanelGroup.alpha = 0f;
            helpPanelRect.localScale = Vector3.one * 0.94f;
            helpPanelGroup.DOFade(1f, 0.16f).SetUpdate(true);
            helpPanelRect.DOScale(1f, 0.20f).SetEase(Ease.OutBack).SetUpdate(true);
        }
        else
        {
            helpPanelGroup.DOFade(0f, 0.12f).SetUpdate(true);
            helpPanelRect.DOScale(0.94f, 0.12f).SetUpdate(true).OnComplete(() => helpPanelRect.gameObject.SetActive(false));
        }
    }

    private void BuildUi()
    {
        // 後ろの暗幕（クリックで閉じる）。
        dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dimObject.transform.SetParent(transform, false);
        RectTransform dimRect = dimObject.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.sizeDelta = Vector2.zero;
        dimRect.anchoredPosition = Vector2.zero;
        Image dimImage = dimObject.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        Button dimButton = dimObject.GetComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.targetGraphic = dimImage;
        dimButton.onClick.AddListener(Hide);

        // メインパネル本体。
        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObj.transform.SetParent(transform, false);
        panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500f, 680f);
        Image panelBg = panelObj.GetComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.08f, 0.97f);
        panelGroup = panelObj.GetComponent<CanvasGroup>();

        // 内側の枠線。
        GameObject borderObj = new GameObject("InnerBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(panelRect, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(-14f, -14f);
        borderRect.anchoredPosition = Vector2.zero;
        Image borderImage = borderObj.GetComponent<Image>();
        borderImage.color = new Color(0.08f, 0.35f, 0.45f, 0.5f);
        borderImage.raycastTarget = false;

        titleText = CreateText("Title", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(440f, 32f), 24f, FontStyles.Bold, Color.white);
        titleText.alignment = TextAlignmentOptions.Center;

        // === 音量セクション ===
        audioSectionLabel = CreateText("AudioSection", panelRect, new Vector2(0f, 1f), new Vector2(24f, -70f), new Vector2(440f, 22f), 16f, FontStyles.Bold, new Color(1f, 0.92f, 0.55f));
        audioSectionLabel.alignment = TextAlignmentOptions.MidlineLeft;

        // マスター音量
        masterRowLabel = CreateText("MasterLabel", panelRect, new Vector2(0f, 1f), new Vector2(36f, -100f), new Vector2(100f, 22f), 14f, FontStyles.Bold, new Color(0.85f, 0.92f, 1f));
        masterRowLabel.alignment = TextAlignmentOptions.MidlineLeft;
        masterSlider = BuildSlider("MasterSlider", panelRect, new Vector2(0f, 1f), new Vector2(140f, -100f), new Vector2(260f, 22f));
        masterSlider.minValue = 0f; masterSlider.maxValue = 1f;
        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        masterValueText = CreateText("MasterValue", panelRect, new Vector2(0f, 1f), new Vector2(412f, -100f), new Vector2(60f, 22f), 13f, FontStyles.Bold, new Color(0.9f, 1f, 1f));
        masterValueText.alignment = TextAlignmentOptions.MidlineRight;

        // BGM音量
        bgmRowLabel = CreateText("BgmLabel", panelRect, new Vector2(0f, 1f), new Vector2(36f, -140f), new Vector2(100f, 22f), 14f, FontStyles.Bold, new Color(0.85f, 0.92f, 1f));
        bgmRowLabel.alignment = TextAlignmentOptions.MidlineLeft;
        bgmSlider = BuildSlider("BgmSlider", panelRect, new Vector2(0f, 1f), new Vector2(140f, -140f), new Vector2(260f, 22f));
        bgmSlider.minValue = 0f; bgmSlider.maxValue = 1f;
        bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        bgmValueText = CreateText("BgmValue", panelRect, new Vector2(0f, 1f), new Vector2(412f, -140f), new Vector2(60f, 22f), 13f, FontStyles.Bold, new Color(0.9f, 1f, 1f));
        bgmValueText.alignment = TextAlignmentOptions.MidlineRight;

        // SFX音量
        sfxRowLabel = CreateText("SfxLabel", panelRect, new Vector2(0f, 1f), new Vector2(36f, -180f), new Vector2(100f, 22f), 14f, FontStyles.Bold, new Color(0.85f, 0.92f, 1f));
        sfxRowLabel.alignment = TextAlignmentOptions.MidlineLeft;
        sfxSlider = BuildSlider("SfxSlider", panelRect, new Vector2(0f, 1f), new Vector2(140f, -180f), new Vector2(260f, 22f));
        sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        sfxValueText = CreateText("SfxValue", panelRect, new Vector2(0f, 1f), new Vector2(412f, -180f), new Vector2(60f, 22f), 13f, FontStyles.Bold, new Color(0.9f, 1f, 1f));
        sfxValueText.alignment = TextAlignmentOptions.MidlineRight;

        // ミュート
        muteButton = CreateButton("Mute", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -226f), new Vector2(220f, 34f), out muteButtonText);
        muteButton.onClick.AddListener(ToggleMute);

        // === 言語セクション ===
        languageLabelText = CreateText("LanguageLabel", panelRect, new Vector2(0f, 1f), new Vector2(24f, -276f), new Vector2(120f, 24f), 16f, FontStyles.Bold, new Color(1f, 0.92f, 0.55f));
        languageLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        langJpButton = CreateButton("LangJp", panelRect, new Vector2(0f, 1f), new Vector2(176f, -278f), new Vector2(140f, 32f), out langJpText);
        langJpButton.onClick.AddListener(() => SetLanguage(GameLanguage.Japanese));
        langEnButton = CreateButton("LangEn", panelRect, new Vector2(0f, 1f), new Vector2(326f, -278f), new Vector2(140f, 32f), out langEnText);
        langEnButton.onClick.AddListener(() => SetLanguage(GameLanguage.English));

        // === ゲーム速度セクション ===
        speedLabelText = CreateText("SpeedLabel", panelRect, new Vector2(0f, 1f), new Vector2(24f, -328f), new Vector2(120f, 24f), 16f, FontStyles.Bold, new Color(1f, 0.92f, 0.55f));
        speedLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        speed1Button = CreateButton("Speed1", panelRect, new Vector2(0f, 1f), new Vector2(176f, -330f), new Vector2(92f, 32f), out speed1Text);
        speed1Button.onClick.AddListener(() => SetSpeed(1f));
        speed15Button = CreateButton("Speed15", panelRect, new Vector2(0f, 1f), new Vector2(278f, -330f), new Vector2(92f, 32f), out speed15Text);
        speed15Button.onClick.AddListener(() => SetSpeed(1.5f));
        speed2Button = CreateButton("Speed2", panelRect, new Vector2(0f, 1f), new Vector2(380f, -330f), new Vector2(92f, 32f), out speed2Text);
        speed2Button.onClick.AddListener(() => SetSpeed(2f));

        // === アクションボタン ===
        // ヒーロー変更（編成フェーズのみ有効）。速度セクションと Help の間に配置。
        heroChangeButton = CreateButton("HeroChange", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 272f), new Vector2(320f, 40f), out heroChangeText);
        heroChangeButton.GetComponent<Image>().color = new Color(0.20f, 0.42f, 0.40f, 1f);
        heroChangeButton.onClick.AddListener(OnChangeHeroClicked);

        helpButton = CreateButton("Help", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 222f), new Vector2(320f, 38f), out helpButtonText);
        helpButton.GetComponent<Image>().color = new Color(0.20f, 0.35f, 0.45f, 1f);
        helpButton.onClick.AddListener(ShowHelp);

        lobbyButton = CreateButton("Lobby", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 172f), new Vector2(320f, 42f), out lobbyText);
        lobbyButton.GetComponent<Image>().color = new Color(0.28f, 0.22f, 0.42f, 1f);
        lobbyButton.onClick.AddListener(OnReturnToLobbyClicked);

        restartButton = CreateButton("Restart", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 122f), new Vector2(320f, 42f), out restartText);
        restartButton.GetComponent<Image>().color = new Color(0.45f, 0.16f, 0.18f, 1f);
        restartButton.onClick.AddListener(OnRestartClicked);

        closeButton = CreateButton("Close", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(320f, 42f), out closeText);
        closeButton.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.52f, 1f);
        closeButton.onClick.AddListener(Hide);

        hintText = CreateText("Hint", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(440f, 20f), 12f, FontStyles.Normal, new Color(0.75f, 0.85f, 0.95f, 0.7f));
        hintText.alignment = TextAlignmentOptions.Center;

        // === ヘルプパネル ===
        BuildHelpPanel();
    }

    private void BuildHelpPanel()
    {
        GameObject panelObj = new GameObject("HelpPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObj.transform.SetParent(transform, false);
        helpPanelRect = panelObj.GetComponent<RectTransform>();
        helpPanelRect.anchorMin = helpPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        helpPanelRect.pivot = new Vector2(0.5f, 0.5f);
        helpPanelRect.sizeDelta = new Vector2(560f, 600f);
        Image panelBg = panelObj.GetComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.08f, 0.98f);
        helpPanelGroup = panelObj.GetComponent<CanvasGroup>();

        GameObject borderObj = new GameObject("InnerBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(helpPanelRect, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(-14f, -14f);
        borderRect.anchoredPosition = Vector2.zero;
        Image borderImage = borderObj.GetComponent<Image>();
        borderImage.color = new Color(0.08f, 0.35f, 0.45f, 0.5f);
        borderImage.raycastTarget = false;

        helpTitleText = CreateText("HelpTitle", helpPanelRect, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(500f, 32f), 22f, FontStyles.Bold, Color.white);
        helpTitleText.alignment = TextAlignmentOptions.Center;

        helpBodyText = CreateText("HelpBody", helpPanelRect, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(500f, 460f), 14f, FontStyles.Normal, new Color(0.88f, 0.94f, 1f));
        helpBodyText.alignment = TextAlignmentOptions.TopLeft;
        helpBodyText.enableWordWrapping = true;
        helpBodyText.lineSpacing = 4f;
        helpBodyText.paragraphSpacing = 6f;

        helpCloseButton = CreateButton("HelpClose", helpPanelRect, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(280f, 40f), out helpCloseText);
        helpCloseButton.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.52f, 1f);
        helpCloseButton.onClick.AddListener(HideHelp);
    }

    // === ハンドラ ===

    private void OnMasterChanged(float value)
    {
        value = Mathf.Clamp01(value);
        if (!muted)
            lastUnmutedMasterVolume = value;
        AudioListener.volume = value;
        if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        // ボリュームを手動で動かしたらミュート解除。
        if (muted && value > 0f)
        {
            muted = false;
            RefreshMuteButton();
            PlayerPrefs.SetInt(PrefKeyMuted, 0);
        }
        PlayerPrefs.SetFloat(PrefKeyMasterVolume, value);
        PlayerPrefs.Save();
    }

    private void OnBgmChanged(float value)
    {
        value = Mathf.Clamp01(value);
        AttackEffectPlayer.SetBgmVolume(value);
        if (bgmValueText != null) bgmValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        PlayerPrefs.SetFloat(PrefKeyBgmVolume, value);
        PlayerPrefs.Save();
    }

    private void OnSfxChanged(float value)
    {
        value = Mathf.Clamp01(value);
        AttackEffectPlayer.SetSfxVolume(value);
        if (sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        PlayerPrefs.SetFloat(PrefKeySfxVolume, value);
        PlayerPrefs.Save();
    }

    private void ToggleMute()
    {
        muted = !muted;
        if (muted)
        {
            if (masterSlider != null && masterSlider.value > 0f)
                lastUnmutedMasterVolume = masterSlider.value;
            AudioListener.volume = 0f;
        }
        else
        {
            float v = lastUnmutedMasterVolume > 0f ? lastUnmutedMasterVolume : 1f;
            AudioListener.volume = v;
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(v);
            if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(v * 100f)}%";
        }
        PlayerPrefs.SetInt(PrefKeyMuted, muted ? 1 : 0);
        PlayerPrefs.SetFloat(PrefKeyMasterVolume, lastUnmutedMasterVolume);
        PlayerPrefs.Save();
        RefreshMuteButton();
    }

    private void SetLanguage(GameLanguage lang)
    {
        LocalizationManager.SetLanguage(lang);
        RefreshLanguageLabels();
    }

    private void SetSpeed(float speed)
    {
        currentSpeed = Mathf.Clamp(speed, 0.25f, 4f);
        if (!isOpen)
            Time.timeScale = currentSpeed;
        PlayerPrefs.SetFloat(PrefKeyGameSpeed, currentSpeed);
        PlayerPrefs.Save();
        RefreshSpeedHighlights();
    }

    private void OnRestartClicked()
    {
        Time.timeScale = 1f;
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : 0);
    }

    // 「ヒーロー変更」。オプションを閉じてヒーロー変更オーバーレイを開く（編成フェーズのみ実変更可）。
    private void OnChangeHeroClicked()
    {
        Hide();
        HeroChangeUI.EnsureExists().Show();
    }

    // 「ロビーへ戻る」。GameManager に依頼して章選択をリセットし、次回起動でロビーを出して再読込します。
    private void OnReturnToLobbyClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RequestReturnToLobby();
        else
            OnRestartClicked(); // 念のためのフォールバック（GameManager 不在時は通常再読込）。
    }

    private void LoadSettings()
    {
        float master = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyMasterVolume, 1f));
        float bgm = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyBgmVolume, 1f));
        float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeySfxVolume, 1f));
        muted = PlayerPrefs.GetInt(PrefKeyMuted, 0) != 0;
        currentSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyGameSpeed, 1f), 0.25f, 4f);
        lastUnmutedMasterVolume = master > 0f ? master : 1f;

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(bgm);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfx);

        if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(master * 100f)}%";
        if (bgmValueText != null) bgmValueText.text = $"{Mathf.RoundToInt(bgm * 100f)}%";
        if (sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(sfx * 100f)}%";
    }

    private void ApplyAllSettings()
    {
        AudioListener.volume = muted ? 0f : (masterSlider != null ? masterSlider.value : 1f);
        AttackEffectPlayer.SetBgmVolume(bgmSlider != null ? bgmSlider.value : 1f);
        AttackEffectPlayer.SetSfxVolume(sfxSlider != null ? sfxSlider.value : 1f);
        if (!isOpen)
            Time.timeScale = currentSpeed;
    }

    private void RefreshLanguageLabels()
    {
        bool ja = LocalizationManager.IsJapanese;
        if (titleText != null) { LocalizationManager.ApplyFont(titleText); titleText.text = ja ? "オプション" : "OPTIONS"; }
        if (audioSectionLabel != null) { LocalizationManager.ApplyFont(audioSectionLabel); audioSectionLabel.text = ja ? "■ 音量" : "■ Audio"; }
        if (masterRowLabel != null) { LocalizationManager.ApplyFont(masterRowLabel); masterRowLabel.text = ja ? "マスター" : "Master"; }
        if (bgmRowLabel != null) { LocalizationManager.ApplyFont(bgmRowLabel); bgmRowLabel.text = "BGM"; }
        if (sfxRowLabel != null) { LocalizationManager.ApplyFont(sfxRowLabel); sfxRowLabel.text = "SFX"; }
        if (languageLabelText != null) { LocalizationManager.ApplyFont(languageLabelText); languageLabelText.text = ja ? "■ 言語" : "■ Language"; }
        if (speedLabelText != null) { LocalizationManager.ApplyFont(speedLabelText); speedLabelText.text = ja ? "■ ゲーム速度" : "■ Speed"; }
        if (langJpText != null) { LocalizationManager.ApplyFont(langJpText); langJpText.text = "日本語"; }
        if (langEnText != null) { LocalizationManager.ApplyFont(langEnText); langEnText.text = "English"; }
        if (helpButtonText != null) { LocalizationManager.ApplyFont(helpButtonText); helpButtonText.text = ja ? "ヘルプ / 操作方法" : "Help / Controls"; }
        if (heroChangeText != null) { LocalizationManager.ApplyFont(heroChangeText); heroChangeText.text = ja ? "ヒーロー変更" : "Change Hero"; }
        if (lobbyText != null) { LocalizationManager.ApplyFont(lobbyText); lobbyText.text = ja ? "ロビーへ戻る" : "Return to Lobby"; }
        if (restartText != null) { LocalizationManager.ApplyFont(restartText); restartText.text = ja ? "新しい挑戦を始める" : "Restart Run"; }
        if (closeText != null) { LocalizationManager.ApplyFont(closeText); closeText.text = ja ? "閉じる" : "Close"; }
        if (hintText != null) { LocalizationManager.ApplyFont(hintText); hintText.text = ja ? "ESCキーでも開閉できます" : "Press ESC to toggle"; }
        if (speed1Text != null) { LocalizationManager.ApplyFont(speed1Text); speed1Text.text = "1x"; }
        if (speed15Text != null) { LocalizationManager.ApplyFont(speed15Text); speed15Text.text = "1.5x"; }
        if (speed2Text != null) { LocalizationManager.ApplyFont(speed2Text); speed2Text.text = "2x"; }
        if (helpTitleText != null) { LocalizationManager.ApplyFont(helpTitleText); helpTitleText.text = ja ? "ヘルプ / 操作方法" : "Help / Controls"; }
        if (helpBodyText != null)
        {
            LocalizationManager.ApplyFont(helpBodyText);
            helpBodyText.text = ja ? GetHelpTextJa() : GetHelpTextEn();
        }
        if (helpCloseText != null) { LocalizationManager.ApplyFont(helpCloseText); helpCloseText.text = ja ? "戻る" : "Back"; }

        RefreshMuteButton();
        HighlightLanguageButtons();
    }

    private void RefreshMuteButton()
    {
        if (muteButton == null || muteButtonText == null) return;
        bool ja = LocalizationManager.IsJapanese;
        muteButtonText.text = muted ? (ja ? "ミュート解除" : "Unmute") : (ja ? "ミュート" : "Mute");
        muteButton.GetComponent<Image>().color = muted ? new Color(0.55f, 0.25f, 0.25f, 1f) : new Color(0.18f, 0.22f, 0.26f, 1f);
    }

    private void HighlightLanguageButtons()
    {
        bool ja = LocalizationManager.IsJapanese;
        if (langJpButton != null) langJpButton.GetComponent<Image>().color = ja ? new Color(0.25f, 0.6f, 0.85f, 1f) : new Color(0.15f, 0.18f, 0.22f, 1f);
        if (langEnButton != null) langEnButton.GetComponent<Image>().color = !ja ? new Color(0.25f, 0.6f, 0.85f, 1f) : new Color(0.15f, 0.18f, 0.22f, 1f);
    }

    private void RefreshSpeedHighlights()
    {
        if (speed1Button != null) speed1Button.GetComponent<Image>().color = Mathf.Approximately(currentSpeed, 1f) ? new Color(0.25f, 0.6f, 0.85f, 1f) : new Color(0.15f, 0.18f, 0.22f, 1f);
        if (speed15Button != null) speed15Button.GetComponent<Image>().color = Mathf.Approximately(currentSpeed, 1.5f) ? new Color(0.25f, 0.6f, 0.85f, 1f) : new Color(0.15f, 0.18f, 0.22f, 1f);
        if (speed2Button != null) speed2Button.GetComponent<Image>().color = Mathf.Approximately(currentSpeed, 2f) ? new Color(0.25f, 0.6f, 0.85f, 1f) : new Color(0.15f, 0.18f, 0.22f, 1f);
    }

    private string GetHelpTextJa()
    {
        return
"<b>■ 基本操作</b>\n" +
"・ユニットをドラッグ&ドロップで盤面・ベンチ間を移動\n" +
"・同じユニットを3体集めると自動で★アップ（最大★3）\n" +
"・FIGHTボタンで戦闘開始\n" +
"・ESCキーでオプションを開閉\n" +
"・右クリック / Escでユニット詳細パネルを閉じる\n" +
"\n" +
"<b>■ ショップ</b>\n" +
"・リロール: 2コイン\n" +
"・EXP購入: 4コインで4EXP\n" +
"・ウェーブクリアごとに <b>無料リロール</b> ＋ <b>+2 EXP</b>\n" +
"・ショップに出るコスト上限はボス撃破で解放（序盤コスト3まで）\n" +
"\n" +
"<b>■ 経済</b>\n" +
"・基本収入: クリアごと +5\n" +
"・利子: 所持金10ごと +1（上限+5）\n" +
"・敵がコイン・アイテムをドロップすることがあります\n" +
"\n" +
"<b>■ アイテム</b>\n" +
"・ユニット1体に最大3つ装備可能\n" +
"・ベンチからドラッグして装備\n" +
"\n" +
"<b>■ シナジー</b>\n" +
"・同じシナジー持ちユニットを盤面に並べると発動\n" +
"・画面左のシナジーパネルで現在の発動状況を確認\n" +
"\n" +
"<b>■ ステージ進行</b>\n" +
"・1チャプター = 33ラウンド（4ステージ）\n" +
"・中ボス撃破でショップのコスト上限が解放\n" +
"・章ボスを倒すとボス報酬ユニットを選択";
    }

    private string GetHelpTextEn()
    {
        return
"<b>■ Basic Controls</b>\n" +
"・Drag & drop units between board and bench\n" +
"・Combining 3 of the same unit triggers a Star-Up (max ★3)\n" +
"・Click FIGHT to start combat\n" +
"・Press ESC to toggle options\n" +
"・Right-click / Esc to close the unit detail panel\n" +
"\n" +
"<b>■ Shop</b>\n" +
"・Reroll: 2 gold\n" +
"・Buy EXP: 4 gold for 4 EXP\n" +
"・Each wave clear: <b>FREE reroll</b> + <b>+2 EXP</b>\n" +
"・Higher-cost units unlock by clearing bosses (cap is cost 3 early)\n" +
"\n" +
"<b>■ Economy</b>\n" +
"・Base income: +5 per wave clear\n" +
"・Interest: +1 per 10 gold (cap +5)\n" +
"・Enemies may drop gold and items\n" +
"\n" +
"<b>■ Items</b>\n" +
"・Up to 3 items per unit\n" +
"・Drag from item bench to equip\n" +
"\n" +
"<b>■ Synergies</b>\n" +
"・Place units sharing a synergy on the board to activate\n" +
"・Check the left synergy panel for active trait counts\n" +
"\n" +
"<b>■ Chapter Progression</b>\n" +
"・Chapter 1 = 33 rounds across 4 stages\n" +
"・Mid-bosses unlock shop cost tiers when defeated\n" +
"・Defeating the chapter boss offers a reward unit";
    }

    // === 部品ビルダー ===

    private TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, float fontSize, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(text);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private Button CreateButton(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, out TextMeshProUGUI labelText)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.15f, 0.18f, 0.22f, 1f);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;
        labelText = labelObj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(labelText);
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 14f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        return button;
    }

    private Slider BuildSlider(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        Image bg = obj.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 1f);
        bg.raycastTarget = true;

        GameObject fillAreaObj = new GameObject("FillArea", typeof(RectTransform));
        fillAreaObj.transform.SetParent(rect, false);
        RectTransform fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(-20f, size.y - 6f);
        fillAreaRect.anchoredPosition = Vector2.zero;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillAreaRect, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        Image fillImage = fillObj.GetComponent<Image>();
        fillImage.color = new Color(0.32f, 0.7f, 1f, 1f);
        fillImage.raycastTarget = false;

        GameObject handleAreaObj = new GameObject("HandleSlideArea", typeof(RectTransform));
        handleAreaObj.transform.SetParent(rect, false);
        RectTransform handleAreaRect = handleAreaObj.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
        handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
        handleAreaRect.pivot = new Vector2(0.5f, 0.5f);
        handleAreaRect.sizeDelta = new Vector2(-20f, size.y);
        handleAreaRect.anchoredPosition = Vector2.zero;

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(handleAreaRect, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, size.y + 6f);
        handleRect.anchoredPosition = Vector2.zero;
        Image handleImage = handleObj.GetComponent<Image>();
        handleImage.color = new Color(0.95f, 0.98f, 1f, 1f);

        Slider slider = obj.GetComponent<Slider>();
        slider.targetGraphic = handleImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }
}
