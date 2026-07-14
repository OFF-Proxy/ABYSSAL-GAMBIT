using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// コイン表示をクリックした時に、収入内訳（所持金・基本収入・利子・次回収入）をまとめて表示するパネルです。
// シナジー/アイテムのツールチップと同じく、左上（シナジーパネルの右隣）へ固定表示し、必要時に自動生成されます。
public class CoinIncomePanelUI : MonoBehaviour
{
    private const int CanvasSortingOrder = 15010; // 16bit short上限(32767)内。
    private static readonly Vector2 PanelSize = new Vector2(320f, 250f);

    private static CoinIncomePanelUI instance;
    private static Sprite frameSprite;

    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private Image gaugeFill;
    private TextMeshProUGUI gaugeText;
    private Tween panelTween;
    private bool subscribed;

    // コイン内訳パネルの表示／非表示を切り替えます（同じ場所をもう一度クリックで閉じる用）。
    public static void Toggle()
    {
        if (instance != null && instance.gameObject.activeSelf)
        {
            Hide();
            return;
        }
        Show();
    }

    public static void Show()
    {
        if (PlayerData.Instance == null)
            return;
        // HUD設定でコイン内訳OFFなら開かない。
        if (!SettingsStore.GetHud("coin"))
            return;

        EnsureInstance();
        SynergyTooltipUI.Hide();
        ItemTooltipUI.Hide();
        instance.RefreshContent();
        instance.MoveToFixedPosition();
        instance.gameObject.SetActive(true);
        instance.PlayPanelAppear();
    }

    public static void Hide()
    {
        if (instance != null)
        {
            instance.panelTween?.Kill(false);
            instance.gameObject.SetActive(false);
        }
    }

    // コイン表示テキストにクリック判定を仕込み、押すと内訳パネルを開けるようにします。
    public static void AttachToCoinDisplay(TextMeshProUGUI coinText)
    {
        if (coinText == null)
            return;

        coinText.raycastTarget = true;
        if (coinText.GetComponent<CoinDisplayClickProxy>() == null)
            coinText.gameObject.AddComponent<CoinDisplayClickProxy>();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject panelObject = new GameObject("CoinIncomePanelUI", typeof(RectTransform));
        instance = panelObject.AddComponent<CoinIncomePanelUI>();
        instance.BuildUi();
        LocalizationManager.OnLanguageChanged += instance.RefreshLanguage;
        panelObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (!subscribed && PlayerData.Instance != null)
        {
            PlayerData.Instance.OnUpdate += RefreshContent;
            subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (subscribed && PlayerData.Instance != null)
        {
            PlayerData.Instance.OnUpdate -= RefreshContent;
            subscribed = false;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }

        // パネル外を左クリックしたら閉じる。コイン表示自身のクリックは再オープン扱いになるためここでは閉じない。
        if (Input.GetMouseButtonDown(0) &&
            (panelRect == null || !RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, null)))
            Hide();
    }

    private void BuildUi()
    {
        LoadSprites();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);

        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.sizeDelta = PanelSize;
        panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();

        Image background = panelObject.GetComponent<Image>();
        background.sprite = frameSprite;
        background.color = frameSprite != null ? Color.white : new Color(0.015f, 0.02f, 0.035f, 0.94f);
        background.type = Image.Type.Simple;
        background.raycastTarget = false;

        titleText = CreateText("Title", new Vector2(24f, -26f), new Vector2(-24f, -58f), 22f, FontStyles.Bold, Color.white);
        bodyText = CreateText("Body", new Vector2(24f, -66f), new Vector2(-24f, -202f), 17f, FontStyles.Normal, new Color(0.9f, 0.97f, 1f, 1f));
        bodyText.lineSpacing = 6f;

        BuildGauge();
    }

    // パネル下部に「次の +1 利子までの進捗」ゲージを作ります（ショップ下の小型ゲージと同じ意味）。
    private void BuildGauge()
    {
        GameObject root = new GameObject("Gauge", typeof(RectTransform));
        root.transform.SetParent(panelRect, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.offsetMin = new Vector2(24f, 20f);
        rootRect.offsetMax = new Vector2(-24f, 38f);

        GameObject bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
        bgImage.raycastTarget = false;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(bg.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);
        gaugeFill = fillObj.GetComponent<Image>();
        gaugeFill.color = new Color(0.95f, 0.78f, 0.25f, 0.96f);
        gaugeFill.type = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        gaugeFill.fillAmount = 0f;
        gaugeFill.raycastTarget = false;

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        gaugeText = label.GetComponent<TextMeshProUGUI>();
        gaugeText.fontSize = 12f;
        gaugeText.fontStyle = FontStyles.Bold;
        gaugeText.alignment = TextAlignmentOptions.Center;
        gaugeText.color = new Color(0.85f, 0.96f, 1f, 0.95f);
        gaugeText.enableWordWrapping = false;
        gaugeText.raycastTarget = false;
        LocalizationManager.ApplyFont(gaugeText);
    }

    private TextMeshProUGUI CreateText(string objectName, Vector2 topLeft, Vector2 bottomRight, float fontSize, FontStyles style, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelRect, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
        rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(text);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    // 現在の経済状態（所持金・基本収入・利子・次回収入）をテキストとゲージへ反映します。
    private void RefreshContent()
    {
        PlayerData data = PlayerData.Instance;
        if (data == null)
            return;

        LocalizationManager.ApplyFont(titleText);
        LocalizationManager.ApplyFont(bodyText);
        bool ja = LocalizationManager.IsJapanese;

        int money = data.Money;
        int baseIncome = data.baseRoundIncome;
        int interest = data.CurrentInterest;
        int cap = Mathf.Max(0, data.interestCap);
        int step = Mathf.Max(1, data.interestPerGold);
        int total = data.PreviewNextIncome;

        titleText.text = ja ? "コイン収入の内訳" : "Gold Income";

        if (ja)
        {
            bodyText.text =
                $"所持金 : {money}\n" +
                $"基本収入 : +{baseIncome}\n" +
                $"利子 : +{interest} / 上限 +{cap}\n" +
                $"<color=#9ED9FF>次の収入 : +{total}</color>";
        }
        else
        {
            bodyText.text =
                $"Gold : {money}\n" +
                $"Base income : +{baseIncome}\n" +
                $"Interest : +{interest} / cap +{cap}\n" +
                $"<color=#9ED9FF>Next income : +{total}</color>";
        }

        bool maxed = interest >= cap;
        float fill;
        string gauge;
        if (maxed)
        {
            fill = 1f;
            gauge = ja ? $"利子 MAX (+{cap})" : $"Interest MAX (+{cap})";
        }
        else
        {
            int progress = money - interest * step;
            int remaining = Mathf.Max(0, step - progress);
            fill = Mathf.Clamp01((float)progress / step);
            gauge = ja ? $"利子+1 まで {remaining}g" : $"+1 interest in {remaining}g";
        }

        if (gaugeFill != null) gaugeFill.fillAmount = fill;
        if (gaugeText != null)
        {
            LocalizationManager.ApplyFont(gaugeText);
            gaugeText.text = gauge;
            gaugeText.color = maxed ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(0.85f, 0.96f, 1f, 0.95f);
        }
    }

    private void RefreshLanguage()
    {
        if (gameObject.activeSelf)
            RefreshContent();
    }

    private void PlayPanelAppear()
    {
        if (panelRect == null || panelCanvasGroup == null || !gameObject.activeInHierarchy)
            return;

        panelTween?.Kill(false);
        Vector3 targetScale = Vector3.one;
        panelCanvasGroup.alpha = 0f;
        panelRect.localScale = targetScale * 0.96f;
        panelTween = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(true)
            .Append(panelCanvasGroup.DOFade(1f, 0.12f).SetEase(Ease.OutQuad))
            .Join(panelRect.DOScale(targetScale, 0.18f).SetEase(Ease.OutBack));
    }

    // シナジーパネルの右隣・上寄せに固定表示します（pivot=(0,0) なので左下角座標）。
    private void MoveToFixedPosition()
    {
        float x = TooltipLayout.FixedPanelX;
        float y = Mathf.Max(8f, Screen.height - TooltipLayout.FixedPanelTopMargin - PanelSize.y);
        panelRect.anchoredPosition = new Vector2(x, y);
    }

    private static void LoadSprites()
    {
        if (frameSprite != null)
            return;

        frameSprite = LoadUiSprite("UI/ItemBench/synergy_tooltip_frame");
    }

    private static Sprite LoadUiSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }
}

// コイン表示テキストに付与し、クリックで収入内訳パネルを開閉する小さな仲介コンポーネントです。
public class CoinDisplayClickProxy : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        CoinIncomePanelUI.Toggle();
    }
}
