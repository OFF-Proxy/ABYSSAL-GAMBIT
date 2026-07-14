using TMPro;
using UnityEngine;
using UnityEngine.UI;

// オーグメントの説明を、画面上の任意の位置に出すミニツールチップです。
public class AugmentTooltipUI : MonoBehaviour
{
    private static AugmentTooltipUI instance;
    private static Sprite cardPanelSprite;

    private RectTransform panelRect;
    private Image panelImage;
    private Image tierBarImage;
    private Image kindIconImage;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;

    public static void Show(AugmentDefinition augment, Vector2 screenPosition)
    {
        if (augment == null) return;
        // HUD設定でツールチップOFFなら表示しない。
        if (!SettingsStore.GetHud("tooltip")) return;
        EnsureExists();
        instance.gameObject.SetActive(true);
        instance.ApplyContent(augment);
        instance.PositionPanel(screenPosition);
    }

    public static void Hide()
    {
        if (instance == null) return;
        instance.gameObject.SetActive(false);
    }

    private static AugmentTooltipUI EnsureExists()
    {
        if (instance != null) return instance;

        AugmentTooltipUI existing = FindObjectOfType<AugmentTooltipUI>(true);
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject uiObject = new GameObject("AugmentTooltipUI", typeof(RectTransform));
        instance = uiObject.AddComponent<AugmentTooltipUI>();
        instance.BuildUi();
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void BuildUi()
    {
        LoadSprites();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 14500; // 16bit short上限(32767)内。

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(290f, 110f);

        panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = cardPanelSprite;
        panelImage.color = new Color(0.05f, 0.07f, 0.1f, 0.96f);
        panelImage.type = cardPanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.raycastTarget = false;

        // 上部にティアカラーの帯を入れて、レアリティを直感的に伝えます。
        GameObject tierBarObject = new GameObject("TierBar", typeof(RectTransform), typeof(Image));
        tierBarObject.transform.SetParent(panelObject.transform, false);
        RectTransform tierBarRect = tierBarObject.GetComponent<RectTransform>();
        tierBarRect.anchorMin = new Vector2(0f, 1f);
        tierBarRect.anchorMax = new Vector2(1f, 1f);
        tierBarRect.pivot = new Vector2(0.5f, 1f);
        tierBarRect.anchoredPosition = Vector2.zero;
        tierBarRect.sizeDelta = new Vector2(0f, 4f);
        tierBarImage = tierBarObject.GetComponent<Image>();
        tierBarImage.color = Color.white;
        tierBarImage.raycastTarget = false;

        // 効果カテゴリを示す小アイコン（タイトル左横）
        GameObject kindObject = new GameObject("KindIcon", typeof(RectTransform), typeof(Image));
        kindObject.transform.SetParent(panelObject.transform, false);
        RectTransform kindRect = kindObject.GetComponent<RectTransform>();
        kindRect.anchorMin = new Vector2(0f, 1f);
        kindRect.anchorMax = new Vector2(0f, 1f);
        kindRect.pivot = new Vector2(0f, 1f);
        kindRect.anchoredPosition = new Vector2(12f, -8f);
        kindRect.sizeDelta = new Vector2(28f, 28f);
        kindIconImage = kindObject.GetComponent<Image>();
        kindIconImage.preserveAspect = true;
        kindIconImage.raycastTarget = false;

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(panelObject.transform, false);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -10f);
        titleRect.sizeDelta = new Vector2(-56f, 28f);
        titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        titleText.fontSize = 15f;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        LocalizationManager.ApplyFont(titleText);

        GameObject bodyObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyObject.transform.SetParent(panelObject.transform, false);
        RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(12f, 10f);
        bodyRect.offsetMax = new Vector2(-12f, -38f);
        bodyText = bodyObject.GetComponent<TextMeshProUGUI>();
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.fontSize = 12.5f;
        bodyText.color = new Color(0.93f, 0.97f, 1f, 1f);
        bodyText.enableWordWrapping = true;
        bodyText.raycastTarget = false;
        LocalizationManager.ApplyFont(bodyText);
    }

    private void ApplyContent(AugmentDefinition augment)
    {
        if (titleText != null)
        {
            LocalizationManager.ApplyFont(titleText);
            string name = LocalizationManager.IsJapanese ? augment.NameJa : augment.NameEn;
            titleText.text = name;
            titleText.color = AugmentHudUI.GetTierColor(augment.Tier);
        }
        if (bodyText != null)
        {
            LocalizationManager.ApplyFont(bodyText);
            bodyText.text = LocalizationManager.IsJapanese ? augment.DescriptionJa : augment.DescriptionEn;
        }
        if (tierBarImage != null)
            tierBarImage.color = AugmentHudUI.GetTierColor(augment.Tier);

        if (kindIconImage != null)
        {
            Sprite ks = AugmentHudUI.GetKindSprite(augment.Kind);
            kindIconImage.sprite = ks;
            kindIconImage.color = ks != null
                ? AugmentHudUI.GetTierColor(augment.Tier)
                : new Color(0, 0, 0, 0);
        }

        ResizeForContent();
    }

    private void ResizeForContent()
    {
        if (bodyText == null || panelRect == null) return;
        bodyText.ForceMeshUpdate();
        float textHeight = bodyText.preferredHeight;
        float panelHeight = Mathf.Clamp(48f + textHeight + 14f, 90f, 320f);
        panelRect.sizeDelta = new Vector2(290f, panelHeight);
    }

    private void PositionPanel(Vector2 screenPosition)
    {
        if (panelRect == null) return;

        float screenW = Screen.width;
        float screenH = Screen.height;
        float panelW = panelRect.sizeDelta.x;
        float panelH = panelRect.sizeDelta.y;

        // 既定はクリック位置の少し上に配置
        float x = Mathf.Clamp(screenPosition.x, panelW * 0.5f + 8f, screenW - panelW * 0.5f - 8f);
        float y = Mathf.Clamp(screenPosition.y - 6f, panelH + 12f, screenH - 8f);

        panelRect.anchoredPosition = new Vector2(x, y);
    }

    private static void LoadSprites()
    {
        if (cardPanelSprite == null) cardPanelSprite = Resources.Load<Sprite>("UI/Augment/card_panel");
    }
}
