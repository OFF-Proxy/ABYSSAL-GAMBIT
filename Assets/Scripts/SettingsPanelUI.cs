using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// タイトル画面の「オプション」から開く、タブ式の総合設定パネル。
// タブ: 音量 / 言語 / ディスプレイ / グラフィック / カーソル / 操作 / HUD / サイズ
// 値の保存・適用は SettingsStore。HUD表示切替・キーバインドの実適用は GameScene 側フックで読む想定。
public class SettingsPanelUI : MonoBehaviour
{
    public static SettingsPanelUI Instance { get; private set; }

    private enum Tab { Audio, Language, Display, Graphics, Cursor, Controls, Hud, Size, Story }

    private Canvas canvas;
    private GameObject dim;
    private RectTransform panel;
    private CanvasGroup panelGroup;
    private RectTransform contentRoot;
    private readonly List<Button> tabButtons = new List<Button>();
    private Tab currentTab = Tab.Audio;
    private bool built;
    private bool isOpen;

    // キーバインドのキャプチャ中アクション（null=待機なし）
    private string capturingAction;
    private TextMeshProUGUI capturingValueText;

    // HUD トグル対象（表示名はローカライズ）
    private static readonly string[] HudKeys = { "synergy", "coin", "round", "tooltip" };

    public static SettingsPanelUI EnsureExists()
    {
        if (Instance != null) return Instance;
        SettingsPanelUI existing = FindObjectOfType<SettingsPanelUI>(true);
        if (existing != null) { Instance = existing; existing.BuildIfNeeded(); return existing; }

        GameObject go = new GameObject("SettingsPanelUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SettingsPanelUI));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 26000; // 16bit short上限(32767)内。
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Instance = go.GetComponent<SettingsPanelUI>();
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
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (capturingAction != null)
        {
            CaptureKeyIfAny();
            return;
        }
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    public void Show()
    {
        BuildIfNeeded();
        isOpen = true;
        if (dim != null) dim.SetActive(true);
        panel.gameObject.SetActive(true);
        panelGroup.alpha = 0f;
        panel.localScale = Vector3.one * (0.92f * SettingsStore.UiScaleFactor);
        panelGroup.DOKill(); panel.DOKill();
        panelGroup.DOFade(1f, 0.18f).SetUpdate(true);
        panel.DOScale(SettingsStore.UiScaleFactor, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
        SelectTab(currentTab);
    }

    public void Hide()
    {
        isOpen = false;
        capturingAction = null;
        panelGroup.DOKill(); panel.DOKill();
        panelGroup.DOFade(0f, 0.14f).SetUpdate(true);
        panel.DOScale(SettingsStore.UiScaleFactor * 0.94f, 0.14f).SetUpdate(true)
            .OnComplete(() => { panel.gameObject.SetActive(false); if (dim != null) dim.SetActive(false); });
    }

    private void BuildIfNeeded()
    {
        if (built) return;
        BuildChrome();
        built = true;
        SetActiveInstant(false);
    }

    private void SetActiveInstant(bool open)
    {
        isOpen = open;
        if (dim != null) dim.SetActive(open);
        if (panel != null) { panel.gameObject.SetActive(open); if (panelGroup != null) panelGroup.alpha = open ? 1f : 0f; }
    }

    private void BuildChrome()
    {
        bool ja = LocalizationManager.IsJapanese;

        dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(transform, false);
        RectTransform dimRect = dim.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one; dimRect.sizeDelta = Vector2.zero; dimRect.anchoredPosition = Vector2.zero;
        Image dimImg = dim.GetComponent<Image>(); dimImg.color = new Color(0f, 0f, 0f, 0.6f);
        Button dimBtn = dim.GetComponent<Button>(); dimBtn.transition = Selectable.Transition.None; dimBtn.targetGraphic = dimImg;
        dimBtn.onClick.AddListener(Hide);

        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObj.transform.SetParent(transform, false);
        panel = panelObj.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(1180f, 760f);
        Image panelBg = panelObj.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/Duelyst/frame_modal");
        if (frame != null) { panelBg.sprite = frame; panelBg.type = Image.Type.Sliced; panelBg.color = Color.white; }
        else panelBg.color = new Color(0.04f, 0.06f, 0.1f, 0.98f);
        panelGroup = panelObj.GetComponent<CanvasGroup>();

        TextMeshProUGUI title = CreateText("Title", panel, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(800f, 44f), 30f, FontStyles.Bold, Color.white);
        title.alignment = TextAlignmentOptions.Center;
        title.text = ja ? "設定" : "SETTINGS";

        // 閉じる（右上）
        Sprite closeSprite = Resources.Load<Sprite>("UI/Duelyst/button_close");
        GameObject close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(panel, false);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f); closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-22f, -22f); closeRect.sizeDelta = new Vector2(56f, 56f);
        Image closeImg = close.GetComponent<Image>();
        if (closeSprite != null) { closeImg.sprite = closeSprite; closeImg.preserveAspect = true; closeImg.color = Color.white; }
        else closeImg.color = new Color(0.5f, 0.2f, 0.22f, 1f);
        Button closeBtn = close.GetComponent<Button>(); closeBtn.targetGraphic = closeImg; closeBtn.onClick.AddListener(Hide);
        close.AddComponent<ButtonJuice>();

        // 左のタブ列
        GameObject tabCol = new GameObject("Tabs", typeof(RectTransform), typeof(VerticalLayoutGroup));
        tabCol.transform.SetParent(panel, false);
        RectTransform tabRect = tabCol.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 1f); tabRect.anchorMax = new Vector2(0f, 1f); tabRect.pivot = new Vector2(0f, 1f);
        tabRect.anchoredPosition = new Vector2(34f, -96f); tabRect.sizeDelta = new Vector2(280f, 600f);
        VerticalLayoutGroup vlg = tabCol.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f; vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        AddTab(tabCol.transform, Tab.Audio, ja ? "音量" : "Audio");
        AddTab(tabCol.transform, Tab.Language, ja ? "言語" : "Language");
        AddTab(tabCol.transform, Tab.Display, ja ? "ディスプレイ" : "Display");
        AddTab(tabCol.transform, Tab.Graphics, ja ? "グラフィック" : "Graphics");
        AddTab(tabCol.transform, Tab.Cursor, ja ? "マウスカーソル" : "Cursor");
        AddTab(tabCol.transform, Tab.Controls, ja ? "操作・キー" : "Controls");
        AddTab(tabCol.transform, Tab.Hud, ja ? "HUD表示" : "HUD");
        AddTab(tabCol.transform, Tab.Size, ja ? "文字・UIサイズ" : "UI Size");
        AddTab(tabCol.transform, Tab.Story, ja ? "ストーリー演出" : "Story");

        // 右の内容領域
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel, false);
        contentRoot = content.GetComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0f, 0f); contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.offsetMin = new Vector2(340f, 40f); contentRoot.offsetMax = new Vector2(-40f, -96f);
    }

    private void AddTab(Transform parent, Tab tab, string label)
    {
        GameObject go = new GameObject("Tab_" + tab, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement le = go.GetComponent<LayoutElement>(); le.preferredHeight = 64f;
        Image img = go.GetComponent<Image>(); img.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);
        Button btn = go.GetComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); SelectTab(tab); });
        go.AddComponent<ButtonJuice>();
        TextMeshProUGUI t = CreateText("Label", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 40f), 20f, FontStyles.Bold, Color.white);
        t.alignment = TextAlignmentOptions.Center; t.text = label;
        tabButtons.Add(btn);
        btn.GetComponent<RectTransform>(); // keep
        // タブ識別のため名前を保持
        go.name = "Tab_" + (int)tab;
    }

    private void SelectTab(Tab tab)
    {
        currentTab = tab;
        capturingAction = null;
        // ハイライト
        for (int i = 0; i < tabButtons.Count; i++)
        {
            bool sel = i == (int)tab;
            tabButtons[i].GetComponent<Image>().color = sel ? new Color(0.25f, 0.55f, 0.85f, 1f) : new Color(0.12f, 0.16f, 0.24f, 0.95f);
        }
        BuildContent(tab);
    }

    private void BuildContent(Tab tab)
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        switch (tab)
        {
            case Tab.Audio: BuildAudioTab(); break;
            case Tab.Language: BuildLanguageTab(); break;
            case Tab.Display: BuildDisplayTab(); break;
            case Tab.Graphics: BuildGraphicsTab(); break;
            case Tab.Cursor: BuildCursorTab(); break;
            case Tab.Controls: BuildControlsTab(); break;
            case Tab.Hud: BuildHudTab(); break;
            case Tab.Size: BuildSizeTab(); break;
            case Tab.Story: BuildStoryTab(); break;
        }
    }

    // ===== 各タブ =====
    private void BuildAudioTab()
    {
        bool ja = LocalizationManager.IsJapanese;
        float y = -10f;
        CreateSliderRow(ja ? "マスター" : "Master", SettingsStore.MasterVolume, ref y, v => { SettingsStore.MasterVolume = v; SettingsStore.Muted = false; SettingsStore.ApplyAudio(); });
        CreateSliderRow("BGM", SettingsStore.BgmVolume, ref y, v => { SettingsStore.BgmVolume = v; SettingsStore.ApplyAudio(); });
        CreateSliderRow("SFX", SettingsStore.SfxVolume, ref y, v => { SettingsStore.SfxVolume = v; SettingsStore.ApplyAudio(); });
        TextMeshProUGUI muteVal = null;
        CreateButtonRow(ja ? "ミュート" : "Mute", SettingsStore.Muted ? "ON" : "OFF", ref y, out muteVal, () =>
        {
            SettingsStore.Muted = !SettingsStore.Muted;
            SettingsStore.ApplyAudio();
            if (muteVal != null) muteVal.text = SettingsStore.Muted ? "ON" : "OFF";
        });
    }

    private void BuildLanguageTab()
    {
        float y = -10f;
        TextMeshProUGUI val = null;
        CreateButtonRow(LocalizationManager.IsJapanese ? "表示言語" : "Language", LocalizationManager.IsJapanese ? "日本語" : "English", ref y, out val, () =>
        {
            LocalizationManager.SetLanguage(LocalizationManager.IsJapanese ? GameLanguage.English : GameLanguage.Japanese);
            if (val != null) val.text = LocalizationManager.IsJapanese ? "日本語" : "English";
        });
        CreateNote(LocalizationManager.IsJapanese ? "タブ名は次回開いた時に反映されます。" : "Tab labels update next time you open.", ref y);
    }

    private void BuildDisplayTab()
    {
        bool ja = LocalizationManager.IsJapanese;
        float y = -10f;
        Resolution[] res = Screen.resolutions;
        int idx = SettingsStore.ResolutionIndex;
        if (idx < 0 || idx >= res.Length) idx = Mathf.Max(0, res.Length - 1);
        int idxLocal = idx;
        TextMeshProUGUI resVal = null;
        CreateCycleRow(ja ? "解像度" : "Resolution", ResLabel(res, idxLocal), ref y, out resVal,
            () => { idxLocal = (idxLocal - 1 + res.Length) % res.Length; SettingsStore.ResolutionIndex = idxLocal; if (resVal != null) resVal.text = ResLabel(res, idxLocal); SettingsStore.ApplyDisplay(); },
            () => { idxLocal = (idxLocal + 1) % res.Length; SettingsStore.ResolutionIndex = idxLocal; if (resVal != null) resVal.text = ResLabel(res, idxLocal); SettingsStore.ApplyDisplay(); });

        TextMeshProUGUI fsVal = null;
        CreateButtonRow(ja ? "全画面" : "Fullscreen", SettingsStore.Fullscreen ? "ON" : "OFF", ref y, out fsVal, () =>
        {
            SettingsStore.Fullscreen = !SettingsStore.Fullscreen;
            SettingsStore.ApplyDisplay();
            if (fsVal != null) fsVal.text = SettingsStore.Fullscreen ? "ON" : "OFF";
        });
    }

    private string ResLabel(Resolution[] res, int i)
    {
        if (res == null || res.Length == 0) return "-";
        i = Mathf.Clamp(i, 0, res.Length - 1);
        return res[i].width + " x " + res[i].height;
    }

    private void BuildGraphicsTab()
    {
        bool ja = LocalizationManager.IsJapanese;
        float y = -10f;
        string[] names = QualitySettings.names;
        int q = SettingsStore.QualityLevel;
        int qLocal = q;
        TextMeshProUGUI qVal = null;
        CreateCycleRow(ja ? "品質" : "Quality", names.Length > 0 ? names[Mathf.Clamp(qLocal, 0, names.Length - 1)] : "-", ref y, out qVal,
            () => { qLocal = (qLocal - 1 + names.Length) % names.Length; SettingsStore.QualityLevel = qLocal; if (qVal != null) qVal.text = names[qLocal]; SettingsStore.ApplyQuality(); },
            () => { qLocal = (qLocal + 1) % names.Length; SettingsStore.QualityLevel = qLocal; if (qVal != null) qVal.text = names[qLocal]; SettingsStore.ApplyQuality(); });
    }

    private void BuildCursorTab()
    {
        bool ja = LocalizationManager.IsJapanese;
        float y = -10f;
        string[] opts = ja
            ? new[] { "システム標準", "選択", "補助", "無効" }
            : new[] { "System", "Select", "Assist", "Disabled" };
        int count = SettingsStore.CursorCount;
        int local = Mathf.Clamp(SettingsStore.CursorStyle, 0, count - 1);
        TextMeshProUGUI val = null;
        CreateCycleRow(ja ? "カーソル" : "Cursor", opts[local], ref y, out val,
            () => { local = (local - 1 + count) % count; SettingsStore.CursorStyle = local; if (val != null) val.text = opts[local]; SettingsStore.ApplyCursor(); },
            () => { local = (local + 1) % count; SettingsStore.CursorStyle = local; if (val != null) val.text = opts[local]; SettingsStore.ApplyCursor(); });
        CreateNote(ja ? "プレビュー: 選ぶとすぐカーソルが変わります。" : "Preview: cursor changes immediately on select.", ref y);
    }

    private void BuildControlsTab()
    {
        bool ja = LocalizationManager.IsJapanese;
        float y = -6f;
        CreateNote(ja ? "ボタンを押してから、割り当てたいキーを入力" : "Click, then press the key to assign", ref y);
        CreateKeyBindRow(ja ? "オプション開閉" : "Toggle Options", "toggleOptions", KeyCode.Escape, ref y);
        CreateKeyBindRow(ja ? "デバッグ" : "Debug", "debug", KeyCode.F8, ref y);
    }

    private void BuildHudTab()
    {
        bool ja = LocalizationManager.IsJapanese;
        float y = -10f;
        string[] labels = ja
            ? new[] { "シナジーパネル", "コイン内訳", "ラウンド進行", "ツールチップ" }
            : new[] { "Synergy Panel", "Coin Breakdown", "Round Progress", "Tooltips" };
        for (int i = 0; i < HudKeys.Length; i++)
        {
            string key = HudKeys[i];
            TextMeshProUGUI val = null;
            CreateButtonRow(labels[i], SettingsStore.GetHud(key) ? (ja ? "表示" : "ON") : (ja ? "非表示" : "OFF"), ref y, out val, () =>
            {
                bool now = !SettingsStore.GetHud(key);
                SettingsStore.SetHud(key, now);
                if (val != null) val.text = now ? (ja ? "表示" : "ON") : (ja ? "非表示" : "OFF");
            });
        }
        CreateNote(ja ? "※ゲーム中のHUDに反映されます。" : "Applies to in-game HUD.", ref y);
    }

    private void BuildSizeTab()
    {
        bool ja = LocalizationManager.IsJapanese;
        float y = -10f;
        string[] opts = ja ? new[] { "小", "中", "大" } : new[] { "S", "M", "L" };
        int local = SettingsStore.UiScaleStep;
        TextMeshProUGUI val = null;
        CreateCycleRow(ja ? "文字・UIサイズ" : "UI Size", opts[local], ref y, out val,
            () => { local = (local - 1 + 3) % 3; SettingsStore.UiScaleStep = local; if (val != null) val.text = opts[local]; SettingsStore.ApplyUiScale(); ApplySelfScale(); },
            () => { local = (local + 1) % 3; SettingsStore.UiScaleStep = local; if (val != null) val.text = opts[local]; SettingsStore.ApplyUiScale(); ApplySelfScale(); });
        CreateNote(ja ? "※一部のHUD文字はゲーム中に反映されます。" : "Some in-game HUD text applies in-game.", ref y);
    }

    private void BuildStoryTab()
    {
        bool ja = LocalizationManager.IsJapanese;
        float y = -10f;
        TextMeshProUGUI val = null;
        CreateButtonRow(ja ? "章プロローグ" : "Chapter Prologue",
            SettingsStore.SkipPrologue ? (ja ? "スキップ" : "Skip") : (ja ? "再生" : "Play"),
            ref y, out val, () =>
            {
                SettingsStore.SkipPrologue = !SettingsStore.SkipPrologue;
                if (val != null) val.text = SettingsStore.SkipPrologue ? (ja ? "スキップ" : "Skip") : (ja ? "再生" : "Play");
            });
        CreateNote(ja
            ? "※章開始時の全画面プロローグ演出。「再生」なら毎回表示、「スキップ」ならショップへ直行します。"
            : "Full-screen prologue at chapter start. 'Play' shows it every time; 'Skip' goes straight to shop.", ref y);
    }

    private void ApplySelfScale()
    {
        if (panel != null) panel.localScale = Vector3.one * SettingsStore.UiScaleFactor;
    }

    // ===== キーバインドのキャプチャ =====
    private void CaptureKeyIfAny()
    {
        if (!Input.anyKeyDown) return;
        foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
        {
            if (code == KeyCode.None) continue;
            if (Input.GetKeyDown(code))
            {
                if (code == KeyCode.Mouse0 || code == KeyCode.Mouse1) continue; // UI操作と衝突回避
                SettingsStore.SetBind(capturingAction, code);
                if (capturingValueText != null) capturingValueText.text = code.ToString();
                capturingAction = null;
                capturingValueText = null;
                return;
            }
        }
    }

    // ===== 行ビルダー =====
    private void CreateSliderRow(string label, float value, ref float y, Action<float> onChange)
    {
        GameObject row = NewRow(ref y, 56f);
        CreateText("Label", row.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(220f, 34f), 20f, FontStyles.Bold, new Color(0.85f, 0.92f, 1f)).alignment = TextAlignmentOptions.MidlineLeft;
        GameObject sObj = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
        sObj.transform.SetParent(row.transform, false);
        RectTransform sRect = sObj.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0f, 0.5f); sRect.anchorMax = new Vector2(0f, 0.5f); sRect.pivot = new Vector2(0f, 0.5f);
        sRect.anchoredPosition = new Vector2(250f, 0f); sRect.sizeDelta = new Vector2(380f, 22f);
        Image bg = sObj.GetComponent<Image>(); bg.color = new Color(0.06f, 0.08f, 0.12f, 1f);
        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform)); fillArea.transform.SetParent(sRect, false);
        RectTransform faRect = fillArea.GetComponent<RectTransform>(); faRect.anchorMin = new Vector2(0f, 0.5f); faRect.anchorMax = new Vector2(1f, 0.5f); faRect.sizeDelta = new Vector2(-16f, 16f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(faRect, false);
        RectTransform fRect = fill.GetComponent<RectTransform>(); fRect.anchorMin = Vector2.zero; fRect.anchorMax = Vector2.one; fRect.sizeDelta = Vector2.zero;
        Image fImg = fill.GetComponent<Image>(); fImg.color = new Color(0.32f, 0.7f, 1f, 1f); fImg.raycastTarget = false;
        GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform)); handleArea.transform.SetParent(sRect, false);
        RectTransform haRect = handleArea.GetComponent<RectTransform>(); haRect.anchorMin = new Vector2(0f, 0.5f); haRect.anchorMax = new Vector2(1f, 0.5f); haRect.sizeDelta = new Vector2(-16f, 22f);
        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(haRect, false);
        RectTransform hRect = handle.GetComponent<RectTransform>(); hRect.sizeDelta = new Vector2(16f, 28f);
        Image hImg = handle.GetComponent<Image>(); hImg.color = new Color(0.95f, 0.98f, 1f, 1f);
        Slider slider = sObj.GetComponent<Slider>(); slider.fillRect = fRect; slider.handleRect = hRect; slider.targetGraphic = hImg;
        slider.minValue = 0f; slider.maxValue = 1f; slider.SetValueWithoutNotify(value);
        TextMeshProUGUI valText = CreateText("Value", row.transform, new Vector2(0f, 0.5f), new Vector2(648f, 0f), new Vector2(80f, 30f), 18f, FontStyles.Bold, new Color(0.9f, 1f, 1f));
        valText.alignment = TextAlignmentOptions.MidlineLeft; valText.text = Mathf.RoundToInt(value * 100f) + "%";
        slider.onValueChanged.AddListener(v => { valText.text = Mathf.RoundToInt(v * 100f) + "%"; onChange(v); });
    }

    private void CreateButtonRow(string label, string valueText, ref float y, out TextMeshProUGUI valOut, UnityEngine.Events.UnityAction onClick)
    {
        GameObject row = NewRow(ref y, 56f);
        CreateText("Label", row.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(300f, 34f), 20f, FontStyles.Bold, new Color(0.85f, 0.92f, 1f)).alignment = TextAlignmentOptions.MidlineLeft;
        GameObject b = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        b.transform.SetParent(row.transform, false);
        RectTransform bRect = b.GetComponent<RectTransform>(); bRect.anchorMin = new Vector2(0f, 0.5f); bRect.anchorMax = new Vector2(0f, 0.5f); bRect.pivot = new Vector2(0f, 0.5f);
        bRect.anchoredPosition = new Vector2(360f, 0f); bRect.sizeDelta = new Vector2(200f, 44f);
        Image bImg = b.GetComponent<Image>(); bImg.color = new Color(0.18f, 0.3f, 0.42f, 1f);
        Button btn = b.GetComponent<Button>(); btn.targetGraphic = bImg; btn.onClick.AddListener(onClick); b.AddComponent<ButtonJuice>();
        valOut = CreateText("Val", b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 40f), 18f, FontStyles.Bold, Color.white);
        valOut.alignment = TextAlignmentOptions.Center; valOut.text = valueText;
    }

    private void CreateCycleRow(string label, string valueText, ref float y, out TextMeshProUGUI valOut, UnityEngine.Events.UnityAction onPrev, UnityEngine.Events.UnityAction onNext)
    {
        GameObject row = NewRow(ref y, 56f);
        CreateText("Label", row.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(300f, 34f), 20f, FontStyles.Bold, new Color(0.85f, 0.92f, 1f)).alignment = TextAlignmentOptions.MidlineLeft;
        MakeArrow(row.transform, "<", new Vector2(360f, 0f), onPrev);
        valOut = CreateText("Val", row.transform, new Vector2(0f, 0.5f), new Vector2(410f, 0f), new Vector2(280f, 40f), 19f, FontStyles.Bold, Color.white);
        valOut.alignment = TextAlignmentOptions.Center; valOut.text = valueText;
        MakeArrow(row.transform, ">", new Vector2(700f, 0f), onNext);
    }

    private void MakeArrow(Transform parent, string sym, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject b = new GameObject("Arrow", typeof(RectTransform), typeof(Image), typeof(Button));
        b.transform.SetParent(parent, false);
        RectTransform bRect = b.GetComponent<RectTransform>(); bRect.anchorMin = new Vector2(0f, 0.5f); bRect.anchorMax = new Vector2(0f, 0.5f); bRect.pivot = new Vector2(0f, 0.5f);
        bRect.anchoredPosition = pos; bRect.sizeDelta = new Vector2(46f, 44f);
        Image img = b.GetComponent<Image>(); img.color = new Color(0.2f, 0.32f, 0.45f, 1f);
        Button btn = b.GetComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick); b.AddComponent<ButtonJuice>();
        TextMeshProUGUI t = CreateText("S", b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), 22f, FontStyles.Bold, Color.white);
        t.alignment = TextAlignmentOptions.Center; t.text = sym;
    }

    private void CreateKeyBindRow(string label, string action, KeyCode fallback, ref float y)
    {
        GameObject row = NewRow(ref y, 56f);
        CreateText("Label", row.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(300f, 34f), 20f, FontStyles.Bold, new Color(0.85f, 0.92f, 1f)).alignment = TextAlignmentOptions.MidlineLeft;
        GameObject b = new GameObject("Bind", typeof(RectTransform), typeof(Image), typeof(Button));
        b.transform.SetParent(row.transform, false);
        RectTransform bRect = b.GetComponent<RectTransform>(); bRect.anchorMin = new Vector2(0f, 0.5f); bRect.anchorMax = new Vector2(0f, 0.5f); bRect.pivot = new Vector2(0f, 0.5f);
        bRect.anchoredPosition = new Vector2(360f, 0f); bRect.sizeDelta = new Vector2(220f, 44f);
        Image bImg = b.GetComponent<Image>(); bImg.color = new Color(0.2f, 0.26f, 0.36f, 1f);
        Button btn = b.GetComponent<Button>(); btn.targetGraphic = bImg; b.AddComponent<ButtonJuice>();
        TextMeshProUGUI val = CreateText("Val", b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 40f), 18f, FontStyles.Bold, Color.white);
        val.alignment = TextAlignmentOptions.Center; val.text = SettingsStore.GetBind(action, fallback).ToString();
        btn.onClick.AddListener(() =>
        {
            capturingAction = action;
            capturingValueText = val;
            val.text = LocalizationManager.IsJapanese ? "キー入力待ち…" : "Press a key…";
        });
    }

    private GameObject NewRow(ref float y, float height)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(contentRoot, false);
        RectTransform r = row.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(0f, 0f); r.offsetMax = new Vector2(0f, 0f);
        r.anchoredPosition = new Vector2(0f, y);
        r.sizeDelta = new Vector2(0f, height);
        y -= (height + 8f);
        return row;
    }

    private void CreateNote(string text, ref float y)
    {
        GameObject row = NewRow(ref y, 40f);
        TextMeshProUGUI t = CreateText("Note", row.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(720f, 36f), 15f, FontStyles.Italic, new Color(0.75f, 0.85f, 0.95f, 0.85f));
        t.alignment = TextAlignmentOptions.MidlineLeft; t.text = text; t.enableWordWrapping = true;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor;
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(text);
        text.fontSize = fontSize; text.fontStyle = style; text.color = color; text.raycastTarget = false;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
}
