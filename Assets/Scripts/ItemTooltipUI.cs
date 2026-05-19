using System.Text;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// アイテムをクリックした時に、効果説明を画面上に出す小さなUIです。
// シーンに置かなくても、最初に呼ばれたタイミングで自動生成されます。
public class ItemTooltipUI : MonoBehaviour
{
    private static ItemTooltipUI instance;

    private Canvas canvas;
    private RectTransform panelRect;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI categoryText;
    private TextMeshProUGUI descriptionText;
    private readonly List<Image> effectIcons = new List<Image>();
    private readonly List<TextMeshProUGUI> effectTexts = new List<TextMeshProUGUI>();
    private ItemData currentItem;

    private readonly Vector2 panelSize = new Vector2(360f, 206f);
    private readonly Vector2 pointerOffset = new Vector2(18f, 18f);

    // アイテム情報を表示します。マウス位置の少し右上に出し、画面外にはみ出さないようにします。
    public static void Show(ItemData itemData, Vector2 screenPosition)
    {
        if (itemData == null)
            return;

        EnsureInstance();
        instance.currentItem = itemData;
        instance.ApplyItem(itemData);
        instance.MoveNearPointer(screenPosition);
        instance.gameObject.SetActive(true);
    }

    // 表示中の説明を閉じます。
    public static void Hide()
    {
        if (instance != null)
            instance.gameObject.SetActive(false);
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

    // 右クリックかEscで説明を閉じます。
    private void Update()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    // Canvas、背景、テキストを実行時に組み立てます。
    private void BuildUi()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50000;

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

        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0.015f, 0.02f, 0.035f, 0.94f);
        background.raycastTarget = false;

        titleText = CreateText("Title", new Vector2(18f, -14f), new Vector2(-18f, -42f), 24f, FontStyles.Bold, Color.white);
        categoryText = CreateText("Category", new Vector2(18f, -44f), new Vector2(-18f, -66f), 16f, FontStyles.Bold, new Color(0.3f, 0.95f, 1f, 1f));
        descriptionText = CreateText("Description", new Vector2(18f, -72f), new Vector2(-18f, -194f), 18f, FontStyles.Normal, new Color(0.88f, 0.94f, 1f, 1f));

        for (int i = 0; i < 5; i++)
            CreateEffectRow(i);
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
        float y = -75f - index * 24f;

        Image icon = CreateImage($"EffectIcon{index + 1}", new Vector2(18f, y), new Vector2(21f, 21f));
        TextMeshProUGUI text = CreateText($"EffectText{index + 1}", new Vector2(46f, y - 1f), new Vector2(-18f, y - 23f), 17f, FontStyles.Bold, new Color(0.9f, 0.98f, 1f, 1f));
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

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

    // マウス付近に置きつつ、右端や上端からはみ出さない位置へ補正します。
    private void MoveNearPointer(Vector2 screenPosition)
    {
        Vector2 targetPosition = screenPosition + pointerOffset;
        targetPosition.x = Mathf.Clamp(targetPosition.x, 8f, Mathf.Max(8f, Screen.width - panelSize.x - 8f));
        targetPosition.y = Mathf.Clamp(targetPosition.y, 8f, Mathf.Max(8f, Screen.height - panelSize.y - 8f));
        panelRect.anchoredPosition = targetPosition;
    }
}
