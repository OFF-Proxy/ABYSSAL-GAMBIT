using System.Text;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// アイテムをクリックした時に、効果説明を画面上に出す小さなUIです。
// シーンに置かなくても、最初に呼ばれたタイミングで自動生成されます。
public class ItemTooltipUI : MonoBehaviour
{
    private static ItemTooltipUI instance;
    private static Sprite frameSprite;

    private Canvas canvas;
    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI categoryText;
    private TextMeshProUGUI descriptionText;
    private readonly List<Image> effectIcons = new List<Image>();
    private readonly List<TextMeshProUGUI> effectTexts = new List<TextMeshProUGUI>();
    private ItemData currentItem;
    private Tween panelTween;

    private readonly Vector2 panelSize = new Vector2(360f, 540f);

    // アイテム情報を表示します。マウス位置の少し右上に出し、画面外にはみ出さないようにします。
    public static void Show(ItemData itemData, Vector2 screenPosition)
    {
        if (itemData == null)
            return;
        // HUD設定でツールチップOFFなら表示しない。
        if (!SettingsStore.GetHud("tooltip"))
            return;

        EnsureInstance();
        SynergyTooltipUI.Hide();
        CoinIncomePanelUI.Hide();
        instance.currentItem = itemData;
        instance.ApplyItem(itemData);
        instance.MoveToFixedPosition();
        instance.gameObject.SetActive(true);
        instance.PlayPanelAppear();
    }

    // 表示中の説明を閉じます。
    public static void Hide()
    {
        if (instance != null)
        {
            instance.panelTween?.Kill(false);
            instance.gameObject.SetActive(false);
        }
    }

    // まだTooltipが無い場合だけ、必要なCanvasと中身を作ります。
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject tooltipObject = new GameObject("ItemTooltipUI", typeof(RectTransform));
        instance = tooltipObject.AddComponent<ItemTooltipUI>();
        instance.BuildUi();
        LocalizationManager.OnLanguageChanged += instance.RefreshLanguage;
        tooltipObject.SetActive(false);
    }

    // 右クリック/Esc、またはパネル外の左クリックで説明を閉じます。
    private void Update()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }

        if (Input.GetMouseButtonDown(0) &&
            (panelRect == null || !RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, null)))
            Hide();
    }

    // Canvas、背景、テキストを実行時に組み立てます。
    private void BuildUi()
    {
        LoadSprites();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15000; // 16bit short上限(32767)内。

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        gameObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);

        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.sizeDelta = panelSize;
        panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();

        Image background = panelObject.GetComponent<Image>();
        background.sprite = frameSprite;
        background.color = frameSprite != null ? Color.white : new Color(0.015f, 0.02f, 0.035f, 0.94f);
        background.type = Image.Type.Simple;
        background.raycastTarget = false;

        titleText = CreateText("Title", new Vector2(24f, -28f), new Vector2(-24f, -60f), 24f, FontStyles.Bold, Color.white);
        categoryText = CreateText("Category", new Vector2(24f, -62f), new Vector2(-24f, -88f), 16f, FontStyles.Bold, new Color(0.3f, 0.95f, 1f, 1f));
        descriptionText = CreateText("Description", new Vector2(24f, -96f), new Vector2(-24f, -506f), 18f, FontStyles.Normal, new Color(0.88f, 0.94f, 1f, 1f));
        descriptionText.lineSpacing = 1.5f;

        for (int i = 0; i < 5; i++)
            CreateEffectRow(i);
    }

    // 説明パネルを開いた時だけ、ふわっと出るDOTween演出を入れます。
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

    // TextMeshProの子オブジェクトを作り、パネル内の指定範囲へ配置します。
    private TextMeshProUGUI CreateText(string objectName, Vector2 topLeft, Vector2 bottomRight, float fontSize, FontStyles fontStyle, Color color)
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
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    // アイテム効果を「アイコン + 数値」で表示する1行を作ります。
    private void CreateEffectRow(int index)
    {
        float y = -100f - index * 40f;

        Image icon = CreateImage($"EffectIcon{index + 1}", new Vector2(24f, y), new Vector2(24f, 24f));
        TextMeshProUGUI text = CreateText($"EffectText{index + 1}", new Vector2(56f, y - 1f), new Vector2(-24f, y - 52f), 15.5f, FontStyles.Bold, new Color(0.9f, 0.98f, 1f, 1f));
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = 1.5f;

        icon.gameObject.SetActive(false);
        text.gameObject.SetActive(false);
        effectIcons.Add(icon);
        effectTexts.Add(text);
    }

    // Tooltip内に小さな画像を作成します。
    private Image CreateImage(string objectName, Vector2 topLeft, Vector2 size)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(panelRect, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // ItemDataの表示名、カテゴリ、説明文をUIへ流し込みます。
    private void ApplyItem(ItemData itemData)
    {
        titleText.text = LocalizationManager.ItemName(itemData);
        categoryText.text = GetCategoryLabel(itemData.category);
        UpdateEffectRows(itemData);
    }

    private string GetCategoryLabel(ItemCategory category)
    {
        return LocalizationManager.ItemCategoryLabel(category);
    }

    // ItemDataの数値から、文字化けしにくい英語ステータス文を作ります。
    private string BuildEffectText(ItemData itemData)
    {
        return LocalizationManager.BuildItemEffectText(itemData, false);
    }

    // アイテム効果をアイコン行へ反映します。行が作れない時だけ文章表示へ戻します。
    private void UpdateEffectRows(ItemData itemData)
    {
        List<StatIconLine> lines = StatIconLibrary.BuildItemEffectLines(itemData);
        bool useIconRows = lines.Count > 0;
        descriptionText.gameObject.SetActive(!useIconRows);
        descriptionText.text = useIconRows ? string.Empty : BuildEffectText(itemData);

        for (int i = 0; i < effectIcons.Count; i++)
        {
            bool active = i < lines.Count;
            effectIcons[i].gameObject.SetActive(active);
            effectTexts[i].gameObject.SetActive(active);

            if (!active)
                continue;

            effectIcons[i].sprite = StatIconLibrary.GetSprite(lines[i].iconKind);
            effectTexts[i].text = lines[i].text;
        }
    }

    // 言語切替時に、表示中のアイテム説明を即座に更新します。
    private void RefreshLanguage()
    {
        LocalizationManager.ApplyFont(titleText);
        LocalizationManager.ApplyFont(categoryText);
        LocalizationManager.ApplyFont(descriptionText);
        for (int i = 0; i < effectTexts.Count; i++)
            LocalizationManager.ApplyFont(effectTexts[i]);
        if (currentItem != null)
            ApplyItem(currentItem);
    }

    // シナジーパネルの右隣・上寄せに固定表示します（pivot=(0,0) なので左下角座標）。
    private void MoveToFixedPosition()
    {
        float x = TooltipLayout.FixedPanelX;
        float y = Mathf.Max(8f, Screen.height - TooltipLayout.FixedPanelTopMargin - panelSize.y);
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
