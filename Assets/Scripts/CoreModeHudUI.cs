using TMPro;
using UnityEngine;
using UnityEngine.UI;

// R2-coremode: コア戦の専用HUD。画面上部に自コア／敵コアのHPバーを、中央上部にフェーズ／編成カウントダウンを表示する。
// チャプターモードでは生成しない（GameManager がコア戦時のみ EnsureExists して配線する）。
public class CoreModeHudUI : MonoBehaviour
{
    public static CoreModeHudUI Instance { get; private set; }

    private bool isBuilt;
    private BaseEntity playerCore;
    private BaseEntity enemyCore;

    private Image playerFill;
    private Image enemyFill;
    private TextMeshProUGUI playerHpText;
    private TextMeshProUGUI enemyHpText;
    private TextMeshProUGUI phaseText;
    private TextMeshProUGUI waveText;

    public static CoreModeHudUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        CoreModeHudUI existing = FindObjectOfType<CoreModeHudUI>(true);
        if (existing != null)
        {
            Instance = existing;
            Instance.BuildIfNeeded();
            return Instance;
        }

        GameObject root = new GameObject("CoreModeHudUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CoreModeHudUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 13000; // 16bit short上限(32767)内。HUD層。
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        Instance = root.GetComponent<CoreModeHudUI>();
        Instance.BuildIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildIfNeeded();
    }

    private void BuildIfNeeded()
    {
        if (isBuilt) return;
        bool ja = LocalizationManager.IsJapanese;

        // 自コア（左上）。
        BuildCoreBar(new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(0f, 0.5f),
            ja ? "自コア" : "YOUR CORE", new Color(0.45f, 0.85f, 1f),
            out playerFill, out playerHpText);

        // 敵コア（右上）。
        BuildCoreBar(new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(1f, 0.5f),
            ja ? "敵コア" : "ENEMY CORE", new Color(1f, 0.5f, 0.45f),
            out enemyFill, out enemyHpText);

        // フェーズ／カウントダウン（中央上部）。
        phaseText = BuildLabel("Phase", new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(0.5f, 1f),
            new Vector2(620f, 44f), 30f, new Color(1f, 0.95f, 0.75f), TextAlignmentOptions.Center);
        waveText = BuildLabel("Wave", new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(0.5f, 1f),
            new Vector2(620f, 30f), 20f, new Color(0.85f, 0.92f, 1f, 0.9f), TextAlignmentOptions.Center);

        isBuilt = true;
    }

    private void BuildCoreBar(Vector2 anchor, Vector2 anchoredPos, Vector2 pivot, string title, Color fillColor,
        out Image fill, out TextMeshProUGUI hpText)
    {
        GameObject barObj = new GameObject("CoreBar", typeof(RectTransform));
        barObj.transform.SetParent(transform, false);
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = anchor;
        barRect.pivot = pivot;
        barRect.anchoredPosition = anchoredPos;
        barRect.sizeDelta = new Vector2(420f, 56f);

        // タイトル。
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(barRect, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, 0f);
        titleRect.sizeDelta = new Vector2(0f, 22f);
        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(titleText);
        titleText.fontSize = 18f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Midline;
        titleText.color = fillColor;
        titleText.raycastTarget = false;
        titleText.text = title;

        // バー背景。
        GameObject bgObj = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(barRect, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.anchoredPosition = new Vector2(0f, 2f);
        bgRect.sizeDelta = new Vector2(0f, 26f);
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        bgImg.raycastTarget = false;

        // バーfill（Filled・水平）。
        GameObject fillObj = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(bgRect, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        fill = fillObj.GetComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.sprite = null;
        fill.fillAmount = 1f;

        // HP数値。
        GameObject hpObj = new GameObject("HpText", typeof(RectTransform), typeof(TextMeshProUGUI));
        hpObj.transform.SetParent(bgRect, false);
        RectTransform hpRect = hpObj.GetComponent<RectTransform>();
        hpRect.anchorMin = new Vector2(0f, 0f);
        hpRect.anchorMax = new Vector2(1f, 1f);
        hpRect.offsetMin = Vector2.zero;
        hpRect.offsetMax = Vector2.zero;
        hpText = hpObj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(hpText);
        hpText.fontSize = 16f;
        hpText.fontStyle = FontStyles.Bold;
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.color = Color.white;
        hpText.raycastTarget = false;
        hpText.text = "";
    }

    private TextMeshProUGUI BuildLabel(string name, Vector2 anchor, Vector2 anchoredPos, Vector2 pivot,
        Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(transform, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(text);
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    public void SetCores(BaseEntity player, BaseEntity enemy)
    {
        BuildIfNeeded();
        playerCore = player;
        enemyCore = enemy;
        UpdateBars();
    }

    public void SetPhase(string text)
    {
        BuildIfNeeded();
        if (phaseText != null)
            phaseText.text = text ?? string.Empty;
    }

    public void SetWaveInfo(int waveNumber)
    {
        BuildIfNeeded();
        if (waveText != null)
        {
            bool ja = LocalizationManager.IsJapanese;
            waveText.text = ja ? $"ウェーブ {waveNumber}" : $"Wave {waveNumber}";
        }
    }

    private void LateUpdate()
    {
        UpdateBars();
    }

    private void UpdateBars()
    {
        UpdateOne(playerCore, playerFill, playerHpText);
        UpdateOne(enemyCore, enemyFill, enemyHpText);
    }

    private void UpdateOne(BaseEntity core, Image fill, TextMeshProUGUI hpText)
    {
        if (fill == null || hpText == null)
            return;

        if (core == null || core.IsDead)
        {
            fill.fillAmount = 0f;
            hpText.text = "0";
            return;
        }

        fill.fillAmount = core.HealthRatio;
        hpText.text = $"{Mathf.Max(0, core.CurrentHealth):N0} / {core.MaxHealth:N0}";
    }
}
