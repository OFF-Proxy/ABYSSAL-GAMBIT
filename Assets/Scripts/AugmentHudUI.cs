using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 獲得済みオーグメントを画面右上に横並びで表示する HUD です。
// 1 マスにレアリティ枠 + 効果カテゴリのアイコンを大きく出し、テキストはツールチップへ移譲します。
public class AugmentHudUI : MonoBehaviour
{
    private static AugmentHudUI instance;
    private static Sprite panelSprite;
    private static Sprite frameSprite;
    private static readonly Dictionary<AugmentTier, Sprite> tierSprites = new Dictionary<AugmentTier, Sprite>();
    private static readonly Dictionary<AugmentEffectKind, Sprite> kindSprites = new Dictionary<AugmentEffectKind, Sprite>();
    private static bool spritesLoaded;

    private RectTransform panelRect;
    private TextMeshProUGUI headerText;
    private Transform tileRoot;
    private readonly List<AugmentHudTile> tiles = new List<AugmentHudTile>();

    private const float TileSize = 78f;
    private const float TileSpacing = 8f;
    private const int TilesPerRow = 6;

    public static AugmentHudUI EnsureExists()
    {
        if (instance != null)
            return instance;

        AugmentHudUI existing = FindObjectOfType<AugmentHudUI>(true);
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject uiObject = new GameObject("AugmentHudUI", typeof(RectTransform));
        instance = uiObject.AddComponent<AugmentHudUI>();
        instance.BuildUi();
        return instance;
    }

    public void Refresh()
    {
        if (panelRect == null)
            BuildUi();

        IReadOnlyList<AugmentDefinition> owned = GameManager.Instance != null
            ? GameManager.Instance.OwnedAugments
            : (IReadOnlyList<AugmentDefinition>)new List<AugmentDefinition>();

        // 必要なタイル数を確保
        while (tiles.Count < owned.Count)
            CreateTile(tiles.Count);

        for (int i = 0; i < tiles.Count; i++)
        {
            if (i < owned.Count)
            {
                tiles[i].Bind(owned[i]);
                tiles[i].gameObject.SetActive(true);
            }
            else
            {
                tiles[i].gameObject.SetActive(false);
            }
        }

        // 0個ならパネル自体を畳む
        bool any = owned.Count > 0;
        panelRect.gameObject.SetActive(any);

        if (headerText != null)
        {
            LocalizationManager.ApplyFont(headerText);
            headerText.text = LocalizationManager.IsJapanese
                ? $"オーグメント {owned.Count}"
                : $"Augments {owned.Count}";
        }

        AdjustPanelHeight(owned.Count);
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= Refresh;
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
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-18f, -18f);
        panelRect.sizeDelta = new Vector2(TilesPerRow * TileSize + (TilesPerRow - 1) * TileSpacing + 24f, 86f);

        Image background = panelObject.GetComponent<Image>();
        background.sprite = panelSprite;
        background.color = new Color(0.08f, 0.10f, 0.14f, 0.88f);
        background.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.raycastTarget = true;

        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObject.transform.SetParent(panelObject.transform, false);
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -5f);
        headerRect.sizeDelta = new Vector2(0f, 16f);
        headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.fontSize = 14f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = new Color(0.78f, 0.92f, 1f, 1f);
        headerText.raycastTarget = false;
        LocalizationManager.ApplyFont(headerText);

        GameObject tileRootObj = new GameObject("Tiles", typeof(RectTransform));
        tileRootObj.transform.SetParent(panelObject.transform, false);
        RectTransform tileRootRect = tileRootObj.GetComponent<RectTransform>();
        tileRootRect.anchorMin = new Vector2(0f, 1f);
        tileRootRect.anchorMax = new Vector2(1f, 1f);
        tileRootRect.pivot = new Vector2(0.5f, 1f);
        tileRootRect.anchoredPosition = new Vector2(0f, -22f);
        tileRootRect.sizeDelta = new Vector2(0f, TileSize);
        tileRoot = tileRootObj.transform;
    }

    private void CreateTile(int index)
    {
        GameObject tileObject = new GameObject($"AugmentTile_{index}", typeof(RectTransform), typeof(Image), typeof(AugmentHudTile));
        tileObject.transform.SetParent(tileRoot, false);

        RectTransform rect = tileObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(TileSize, TileSize);

        int col = index % TilesPerRow;
        int row = index / TilesPerRow;
        float xStart = 12f;
        rect.anchoredPosition = new Vector2(xStart + col * (TileSize + TileSpacing), -row * (TileSize + TileSpacing));

        Image bg = tileObject.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.1f, 0.92f);
        bg.raycastTarget = true;

        AugmentHudTile tile = tileObject.GetComponent<AugmentHudTile>();
        tile.Build(bg);
        tiles.Add(tile);
    }

    private void AdjustPanelHeight(int count)
    {
        if (panelRect == null) return;
        int rows = count == 0 ? 0 : (count - 1) / TilesPerRow + 1;
        float height = 26f + rows * (TileSize + TileSpacing) + 8f;
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, Mathf.Max(60f, height));
    }

    private static void LoadSprites()
    {
        if (spritesLoaded) return;
        spritesLoaded = true;

        panelSprite = Resources.Load<Sprite>("UI/Augment/card_panel");
        frameSprite = Resources.Load<Sprite>("UI/Augment/augment_frame");

        tierSprites[AugmentTier.Silver] = Resources.Load<Sprite>("UI/Augment/rarity_silver");
        tierSprites[AugmentTier.Gold] = Resources.Load<Sprite>("UI/Augment/rarity_gold");
        tierSprites[AugmentTier.Prism] = Resources.Load<Sprite>("UI/Augment/rarity_prism");

        kindSprites[AugmentEffectKind.Stat] = Resources.Load<Sprite>("UI/Augment/kind_stat");
        kindSprites[AugmentEffectKind.Synergy] = Resources.Load<Sprite>("UI/Augment/kind_synergy");
        kindSprites[AugmentEffectKind.Economy] = Resources.Load<Sprite>("UI/Augment/kind_economy");
        kindSprites[AugmentEffectKind.Item] = Resources.Load<Sprite>("UI/Augment/kind_item");
        kindSprites[AugmentEffectKind.Combat] = Resources.Load<Sprite>("UI/Augment/kind_combat");
        kindSprites[AugmentEffectKind.Special] = Resources.Load<Sprite>("UI/Augment/kind_special");
    }

    public static Sprite GetTierSprite(AugmentTier tier)
    {
        LoadSprites();
        Sprite s;
        return tierSprites.TryGetValue(tier, out s) ? s : null;
    }

    public static Sprite GetKindSprite(AugmentEffectKind kind)
    {
        LoadSprites();
        Sprite s;
        return kindSprites.TryGetValue(kind, out s) ? s : null;
    }

    public static Sprite GetFrameSprite()
    {
        LoadSprites();
        return frameSprite;
    }

    public static Color GetTierColor(AugmentTier tier)
    {
        switch (tier)
        {
            case AugmentTier.Silver: return new Color(0.85f, 0.88f, 0.94f, 1f);
            case AugmentTier.Gold: return new Color(1f, 0.82f, 0.32f, 1f);
            case AugmentTier.Prism: return new Color(0.82f, 0.55f, 1f, 1f);
        }
        return Color.white;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}

// HUD 1 マス: レアリティで色付けされた枠 + 効果カテゴリの中央アイコン。テキストは出しません。
public class AugmentHudTile : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Image background;
    private Image tierGlow;
    private Image rarityFrame;
    private Image kindIcon;
    private AugmentDefinition currentAugment;

    public void Build(Image bg)
    {
        background = bg;

        // ティアカラーで光るグロー（背面）
        GameObject glowObject = new GameObject("TierGlow", typeof(RectTransform), typeof(Image));
        glowObject.transform.SetParent(transform, false);
        RectTransform glowRect = glowObject.GetComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-2f, -2f);
        glowRect.offsetMax = new Vector2(2f, 2f);
        tierGlow = glowObject.GetComponent<Image>();
        tierGlow.preserveAspect = false;
        tierGlow.raycastTarget = false;

        // レアリティ枠（前面の縁取り）
        GameObject frameObject = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frameObject.transform.SetParent(transform, false);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        rarityFrame = frameObject.GetComponent<Image>();
        rarityFrame.preserveAspect = true;
        rarityFrame.raycastTarget = false;

        // 効果カテゴリの中央アイコン
        GameObject iconObject = new GameObject("KindIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(48f, 48f);
        kindIcon = iconObject.GetComponent<Image>();
        kindIcon.preserveAspect = true;
        kindIcon.raycastTarget = false;
    }

    public void Bind(AugmentDefinition augment)
    {
        currentAugment = augment;
        if (augment == null) return;

        Color tierColor = AugmentHudUI.GetTierColor(augment.Tier);
        Sprite tierSprite = AugmentHudUI.GetTierSprite(augment.Tier);

        if (background != null)
            background.color = new Color(tierColor.r * 0.12f, tierColor.g * 0.12f, tierColor.b * 0.18f, 0.96f);

        if (tierGlow != null)
        {
            // ティア素材を背面にうっすら敷いて、レアリティを伝えます。
            tierGlow.sprite = tierSprite;
            tierGlow.color = tierSprite != null
                ? new Color(tierColor.r, tierColor.g, tierColor.b, 0.55f)
                : new Color(tierColor.r, tierColor.g, tierColor.b, 0.25f);
        }

        if (rarityFrame != null)
        {
            rarityFrame.sprite = tierSprite;
            rarityFrame.color = tierSprite != null ? new Color(1f, 1f, 1f, 0.92f) : new Color(0, 0, 0, 0);
        }

        if (kindIcon != null)
        {
            Sprite ks = AugmentHudUI.GetKindSprite(augment.Kind);
            kindIcon.sprite = ks;
            kindIcon.color = ks != null ? Color.white : new Color(0f, 0f, 0f, 0f);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ShowTooltip(eventData.position);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AugmentTooltipUI.Hide();
    }

    private void ShowTooltip(Vector2 screenPosition)
    {
        if (currentAugment == null) return;
        AugmentTooltipUI.Show(currentAugment, screenPosition);
    }
}
