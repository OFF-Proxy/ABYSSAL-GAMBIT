using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 現在発動しているシナジーを、アイテムベンチの右側に小さな縦リストで表示するUIです。
public class SynergyPanelUI : MonoBehaviour
{
    private static SynergyPanelUI instance;
    private static Sprite rowBackgroundSprite;
    private static Sprite iconCardBackgroundSprite;
    private static Sprite iconMaskSprite;

    private RectTransform panelRect;
    private TextMeshProUGUI noSynergyText;
    private SynergyManager lastManager;
    private readonly List<GameObject> rowObjects = new List<GameObject>();
    private readonly List<Image> rowBackgrounds = new List<Image>();
    private readonly List<Image> rowIcons = new List<Image>();
    private readonly List<Image> rowCountBackgrounds = new List<Image>();
    private readonly List<TextMeshProUGUI> rowCountTexts = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> rowTexts = new List<TextMeshProUGUI>();
    private readonly List<SynergyPanelRowClickTarget> rowClickTargets = new List<SynergyPanelRowClickTarget>();
    private readonly List<GameObject> rowAugmentBadges = new List<GameObject>();
    private readonly List<TextMeshProUGUI> rowAugmentBadgeTexts = new List<TextMeshProUGUI>();
    private static Sprite augmentBadgeSprite;

    public static SynergyPanelUI EnsureExists()
    {
        if (instance != null)
            return instance;

        SynergyPanelUI existing = FindObjectOfType<SynergyPanelUI>(true);
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject uiObject = new GameObject("SynergyPanelUI", typeof(RectTransform));
        instance = uiObject.AddComponent<SynergyPanelUI>();
        instance.BuildUi();
        return instance;
    }

    public void Refresh(SynergyManager manager)
    {
        if (noSynergyText == null)
            BuildUi();

        lastManager = manager;
        LocalizationManager.ApplyFont(noSynergyText);
        RefreshRows(manager);
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshLanguage;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLanguage;
    }

    private void RefreshLanguage()
    {
        Refresh(lastManager != null ? lastManager : SynergyManager.Instance);
    }

    private void BuildUi()
    {
        EnsureEventSystem();
        LoadSprites();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 49000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(ItemBenchCanvasUI.SynergyPanelLeftX, -70f);
        panelRect.sizeDelta = new Vector2(154f, 390f);

        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("NoSynergyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = new Vector2(8f, -8f);
        textRect.sizeDelta = new Vector2(144f, 30f);

        noSynergyText = textObject.GetComponent<TextMeshProUGUI>();
        noSynergyText.fontSize = 15f;
        noSynergyText.fontStyle = FontStyles.Bold;
        noSynergyText.color = new Color(0.84f, 1f, 1f, 1f);
        noSynergyText.alignment = TextAlignmentOptions.MidlineLeft;
        noSynergyText.enableWordWrapping = false;
        noSynergyText.raycastTarget = false;
        LocalizationManager.ApplyFont(noSynergyText);

        for (int i = 0; i < SynergyManager.OrderedSynergyTypes.Count; i++)
            CreateSynergyRow(i);
    }

    // シナジー数に応じて、アイコン付きの行を表示・非表示にします。
    private void RefreshRows(SynergyManager manager)
    {
        if (manager == null)
        {
            SetNoSynergyMessage(string.Empty);
            SetAllRowsActive(false);
            return;
        }

        // 発動中→未発動 の順、それぞれの中でユニット数の多い順に並べます。
        IReadOnlyList<SynergyType> orderedTypes = SynergyManager.OrderedSynergyTypes;
        List<(SynergyType type, int count)> active = new List<(SynergyType, int)>();
        List<(SynergyType type, int count)> inactive = new List<(SynergyType, int)>();
        for (int i = 0; i < orderedTypes.Count; i++)
        {
            SynergyType type = orderedTypes[i];
            int count = manager.GetSynergyCount(type);
            if (count <= 0)
                continue;
            if (manager.GetSynergyTier(type) > 0)
                active.Add((type, count));
            else
                inactive.Add((type, count));
        }
        active.Sort((a, b) => b.count.CompareTo(a.count));
        inactive.Sort((a, b) => b.count.CompareTo(a.count));

        int visibleRow = 0;
        for (int section = 0; section < 2; section++)
        {
            List<(SynergyType type, int count)> list = section == 0 ? active : inactive;
            for (int j = 0; j < list.Count; j++)
            {
                SynergyType type = list[j].type;
                int count = list[j].count;
                EnsureRowExists(visibleRow);
                rowObjects[visibleRow].SetActive(true);
                rowClickTargets[visibleRow].Bind(type);

                Color synergyColor = SynergyIconLibrary.GetColor(type);
                rowBackgrounds[visibleRow].color = manager.GetSynergyTier(type) > 0
                    ? new Color(0.82f, 0.96f, 1f, 0.86f)
                    : new Color(0.55f, 0.7f, 0.78f, 0.58f);
                rowIcons[visibleRow].sprite = SynergyIconLibrary.GetSprite(type);
                rowIcons[visibleRow].color = synergyColor;
                rowIcons[visibleRow].preserveAspect = true;

                rowCountBackgrounds[visibleRow].color = manager.GetSynergyTier(type) > 0
                    ? new Color(synergyColor.r * 0.6f, synergyColor.g * 0.6f, synergyColor.b * 0.6f, 0.92f)
                    : new Color(0.08f, 0.09f, 0.1f, 0.9f);

                rowCountTexts[visibleRow].text = count.ToString();
                rowCountTexts[visibleRow].color = Color.white;
                LocalizationManager.ApplyFont(rowCountTexts[visibleRow]);

                LocalizationManager.ApplyFont(rowTexts[visibleRow]);
                rowTexts[visibleRow].text = BuildCompactLine(manager, type, count);
                rowTexts[visibleRow].color = manager.GetSynergyTier(type) > 0
                    ? new Color(0.95f, 1f, 0.82f, 1f)
                    : new Color(0.82f, 0.94f, 1f, 1f);

                // オーグメントで上乗せされている分を「+N」バッジで可視化します。
                int augmentBonus = GetAugmentBonusForSynergy(type);
                if (rowAugmentBadges[visibleRow] != null)
                {
                    bool showBadge = augmentBonus > 0;
                    rowAugmentBadges[visibleRow].SetActive(showBadge);
                    if (showBadge && rowAugmentBadgeTexts[visibleRow] != null)
                    {
                        LocalizationManager.ApplyFont(rowAugmentBadgeTexts[visibleRow]);
                        rowAugmentBadgeTexts[visibleRow].text = $"+{augmentBonus}";
                    }
                }

                visibleRow++;
            }
        }

        for (int i = visibleRow; i < rowObjects.Count; i++)
            rowObjects[i].SetActive(false);

        // ヘッダー行: prism_all_synergy が有効なら全シナジーが上乗せされていることを明示します。
        string headerMessage = string.Empty;
        if (visibleRow == 0)
        {
            headerMessage = LocalizationManager.IsJapanese ? "シナジーなし" : "No synergies";
        }
        else if (GameManager.Instance != null && GameManager.Instance.HasAugment("prism_all_synergy"))
        {
            headerMessage = LocalizationManager.IsJapanese ? "★ 全シナジー +1 重ね掛け" : "★ All synergies doubled";
        }
        SetNoSynergyMessage(headerMessage);
        if (noSynergyText != null)
        {
            noSynergyText.color = visibleRow == 0
                ? new Color(0.84f, 1f, 1f, 1f)
                : new Color(0.78f, 0.55f, 1f, 1f);
        }
    }

    // 表示行を1つ作ります。各行は「アイコン + 現在数 + 名前/次段階」の小さな表示です。
    private void CreateSynergyRow(int index)
    {
        GameObject rowObject = new GameObject($"SynergyRow_{index + 1}", typeof(RectTransform), typeof(Image), typeof(SynergyPanelRowClickTarget));
        rowObject.transform.SetParent(panelRect, false);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(0f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -index * 42f);
        rowRect.sizeDelta = new Vector2(150f, 38f);

        Image rowBackground = rowObject.GetComponent<Image>();
        rowBackground.sprite = rowBackgroundSprite;
        rowBackground.color = new Color(0.015f, 0.018f, 0.025f, 0.62f);
        rowBackground.type = Image.Type.Simple;
        rowBackground.raycastTarget = true;

        GameObject iconMaskObject = new GameObject("IconMask", typeof(RectTransform), typeof(Image));
        iconMaskObject.transform.SetParent(rowObject.transform, false);

        RectTransform iconMaskRect = iconMaskObject.GetComponent<RectTransform>();
        iconMaskRect.anchorMin = new Vector2(0f, 0.5f);
        iconMaskRect.anchorMax = new Vector2(0f, 0.5f);
        iconMaskRect.pivot = new Vector2(0.5f, 0.5f);
        iconMaskRect.anchoredPosition = new Vector2(19f, 0f);
        iconMaskRect.sizeDelta = new Vector2(30f, 30f);

        Image iconMask = iconMaskObject.GetComponent<Image>();
        iconMask.sprite = iconMaskSprite;
        iconMask.color = new Color(0.02f, 0.025f, 0.03f, 1f);
        iconMask.preserveAspect = true;
        iconMask.raycastTarget = false;

        GameObject iconCardObject = new GameObject("IconCardBackground", typeof(RectTransform), typeof(Image));
        iconCardObject.transform.SetParent(rowObject.transform, false);

        RectTransform iconCardRect = iconCardObject.GetComponent<RectTransform>();
        iconCardRect.anchorMin = new Vector2(0f, 0.5f);
        iconCardRect.anchorMax = new Vector2(0f, 0.5f);
        iconCardRect.pivot = new Vector2(0.5f, 0.5f);
        iconCardRect.anchoredPosition = new Vector2(19f, 0f);
        iconCardRect.sizeDelta = new Vector2(36f, 36f);

        Image iconCard = iconCardObject.GetComponent<Image>();
        iconCard.sprite = iconCardBackgroundSprite;
        iconCard.color = new Color(1f, 1f, 1f, 0.96f);
        iconCard.preserveAspect = true;
        iconCard.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(rowObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(19f, 0f);
        iconRect.sizeDelta = new Vector2(25f, 25f);

        Image icon = iconObject.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        GameObject countObject = new GameObject("Count", typeof(RectTransform), typeof(Image));
        countObject.transform.SetParent(rowObject.transform, false);
        RectTransform countRect = countObject.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0f, 0.5f);
        countRect.anchorMax = new Vector2(0f, 0.5f);
        countRect.pivot = new Vector2(0.5f, 0.5f);
        countRect.anchoredPosition = new Vector2(46f, 0f);
        countRect.sizeDelta = new Vector2(23f, 23f);

        Image countBackground = countObject.GetComponent<Image>();
        countBackground.color = new Color(0.08f, 0.09f, 0.1f, 0.9f);
        countBackground.raycastTarget = false;

        GameObject countTextObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        countTextObject.transform.SetParent(countObject.transform, false);
        RectTransform countTextRect = countTextObject.GetComponent<RectTransform>();
        countTextRect.anchorMin = Vector2.zero;
        countTextRect.anchorMax = Vector2.one;
        countTextRect.offsetMin = Vector2.zero;
        countTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI countText = countTextObject.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(countText);
        countText.fontSize = 14f;
        countText.fontStyle = FontStyles.Bold;
        countText.alignment = TextAlignmentOptions.Center;
        countText.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(rowObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(60f, 2f);
        textRect.offsetMax = new Vector2(-6f, -2f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(text);
        text.fontSize = 12f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 12f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.lineSpacing = -8f;

        // オーグメント由来の上乗せを表す小さなバッジ（+1, +2 …）。
        GameObject badgeObject = new GameObject("AugmentBonusBadge", typeof(RectTransform), typeof(Image));
        badgeObject.transform.SetParent(rowObject.transform, false);
        RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 0.5f);
        badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(54f, 14f);
        badgeRect.sizeDelta = new Vector2(26f, 18f);

        Image badgeBg = badgeObject.GetComponent<Image>();
        badgeBg.sprite = augmentBadgeSprite;
        badgeBg.color = new Color(0.85f, 0.55f, 1f, 1f);
        badgeBg.preserveAspect = true;
        badgeBg.raycastTarget = false;
        if (augmentBadgeSprite == null)
            badgeBg.type = Image.Type.Simple;

        GameObject badgeTextObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        badgeTextObject.transform.SetParent(badgeObject.transform, false);
        RectTransform badgeTextRect = badgeTextObject.GetComponent<RectTransform>();
        badgeTextRect.anchorMin = Vector2.zero;
        badgeTextRect.anchorMax = Vector2.one;
        badgeTextRect.offsetMin = Vector2.zero;
        badgeTextRect.offsetMax = Vector2.zero;
        TextMeshProUGUI badgeText = badgeTextObject.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(badgeText);
        badgeText.fontSize = 12f;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.color = Color.white;
        badgeText.raycastTarget = false;
        badgeObject.SetActive(false);

        rowObjects.Add(rowObject);
        rowBackgrounds.Add(rowBackground);
        rowIcons.Add(icon);
        rowCountBackgrounds.Add(countBackground);
        rowCountTexts.Add(countText);
        rowTexts.Add(text);
        rowAugmentBadges.Add(badgeObject);
        rowAugmentBadgeTexts.Add(badgeText);
        rowClickTargets.Add(rowObject.GetComponent<SynergyPanelRowClickTarget>());
        rowObject.SetActive(false);
    }

    // 指定シナジーに対し、オーグメントで上乗せされている離散カウントを返します。
    // emblem 系（Warrior/Ranger/Arcanist）と、戦闘中限定のランダムシナジー追加が対象です。
    // prism_all_synergy（全シナジー +1 重ね掛け）は表示中の合計値自体に既に乗っているため、ヘッダー帯で別途示します。
    private int GetAugmentBonusForSynergy(SynergyType type)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return 0;
        int bonus = 0;
        if (type == SynergyType.Warrior) bonus += gm.AugmentSynergyBonusWarrior;
        else if (type == SynergyType.Ranger) bonus += gm.AugmentSynergyBonusRanger;
        else if (type == SynergyType.Arcanist) bonus += gm.AugmentSynergyBonusArcanist;
        int random;
        if (gm.AdditionalSynergyBonusThisCombat.TryGetValue(type, out random))
            bonus += random;
        return Mathf.Max(0, bonus);
    }

    // 行の右側に出す短い表示文を作ります。長文効果は詳細パネル側へ任せます。
    private string BuildCompactLine(SynergyManager manager, SynergyType type, int count)
    {
        int next = manager.GetNextRequiredCountForDisplay(type);
        string name = LocalizationManager.SynergyName(type);
        return $"{name}\n{count}/{next}";
    }

    // 行数が足りない時の保険です。
    private void EnsureRowExists(int index)
    {
        while (rowObjects.Count <= index)
            CreateSynergyRow(rowObjects.Count);
    }

    private void SetNoSynergyMessage(string message)
    {
        if (noSynergyText == null)
            return;

        noSynergyText.text = message;
        noSynergyText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void SetAllRowsActive(bool active)
    {
        for (int i = 0; i < rowObjects.Count; i++)
            rowObjects[i].SetActive(active);
    }

    private static void LoadSprites()
    {
        if (rowBackgroundSprite == null)
            rowBackgroundSprite = LoadUiSprite("UI/ItemBench/synergy_panel_background");

        if (iconCardBackgroundSprite == null)
            iconCardBackgroundSprite = LoadUiSprite("UI/ItemBench/card_background");

        if (iconMaskSprite == null)
            iconMaskSprite = LoadUiSprite("UI/ItemBench/synergy_icon_mask");

        if (augmentBadgeSprite == null)
            augmentBadgeSprite = LoadUiSprite("UI/Augment/badge_counter");
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

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}

// シナジー行のクリックを受け、効果説明パネルを開くための小さな中継役です。
public class SynergyPanelRowClickTarget : MonoBehaviour, IPointerClickHandler
{
    private SynergyType synergyType;

    public void Bind(SynergyType type)
    {
        synergyType = type;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (synergyType == SynergyType.None)
            return;

        ItemTooltipUI.Hide();
        SynergyTooltipUI.Show(synergyType, eventData.position);
    }
}
