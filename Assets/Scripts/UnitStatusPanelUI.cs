using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// ユニットをクリックした時に、ステータス・スキル・装備アイテムを画面右端に表示するUIです。
// シーンへ手動配置しなくても、最初にShowが呼ばれたタイミングで自動生成されます。
public class UnitStatusPanelUI : MonoBehaviour
{
    private static UnitStatusPanelUI instance;
    private static GameObject previewEntityObject;

    private Canvas canvas;
    private RectTransform panelRect;
    private RectTransform portraitRect;
    private RectTransform hpFillRect;
    private RectTransform manaFillRect;
    private Image portraitImage;
    private Image teamStripeImage;
    private Image innerLineImage;
    private CanvasGroup panelCanvasGroup;
    private Tween panelTween;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI starText;
    private TextMeshProUGUI teamText;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI manaText;
    private TextMeshProUGUI synergyText;
    private TextMeshProUGUI skillTitleText;
    private TextMeshProUGUI skillScalingText;
    private TextMeshProUGUI skillBodyText;
    private TextMeshProUGUI itemTitleText;
    private readonly List<Image> statRowIcons = new List<Image>();
    private readonly List<TextMeshProUGUI> statRowTexts = new List<TextMeshProUGUI>();
    private readonly List<GameObject> unitSynergyBadgeObjects = new List<GameObject>();
    private readonly List<Image> unitSynergyBadgeBackgrounds = new List<Image>();
    private readonly List<Image> unitSynergyIcons = new List<Image>();
    private readonly List<TextMeshProUGUI> unitSynergyLabels = new List<TextMeshProUGUI>();
    private readonly List<Image> skillScalingIcons = new List<Image>();
    private readonly List<Image> itemIconImages = new List<Image>();
    private readonly List<TextMeshProUGUI> itemDescriptionTexts = new List<TextMeshProUGUI>();

    private BaseEntity selectedEntity;
    private Vector2 lastPanelSize;

    private const float PanelWidth = 286f;
    private const float MaxPanelHeight = 650f;
    private const float PanelVisualScale = 1.2f;
    private const float PanelMargin = 10f;
    private const float BottomUiReserve = 124f;
    private const float BarFullWidth = 252f;
    private const float BarHeight = 18f;
    private static readonly Vector2 PortraitViewportSize = new Vector2(244f, 110f);
    private const float PortraitCoverZoom = 1.18f;

    // 指定したユニットの情報パネルを表示します。
    public static void Show(BaseEntity entity)
    {
        if (entity == null)
            return;

        EnsureInstance();
        if (previewEntityObject != null && entity.gameObject != previewEntityObject)
            DestroyPreviewEntity();

        ItemTooltipUI.Hide();
        instance.selectedEntity = entity;
        instance.gameObject.SetActive(true);
        instance.RefreshLayout();
        instance.ApplyEntity();
        instance.PlayPanelAppear();
    }

    // まだ盤面にいないユニット候補を、実体を一時生成して詳細パネルに表示します。
    public static void ShowPreview(EntitiesDatabaseSO.EntityData entityData, int starLevel = 1)
    {
        if (entityData.prefab == null)
            return;

        EnsureInstance();
        DestroyPreviewEntity();

        BaseEntity previewEntity = Instantiate(entityData.prefab);
        previewEntityObject = previewEntity.gameObject;
        previewEntityObject.name = $"{entityData.name} Preview";
        previewEntity.InitializeIdentity(entityData.name, Mathf.Max(1, entityData.cost), Mathf.Clamp(starLevel, 1, 3));
        SynergyManager.AssignEntitySynergies(previewEntity, entityData);
        previewEntityObject.SetActive(false);

        ItemTooltipUI.Hide();
        instance.selectedEntity = previewEntity;
        instance.gameObject.SetActive(true);
        instance.RefreshLayout();
        instance.ApplyEntity();
        instance.PlayPanelAppear();
    }

    // ユニット情報パネルを閉じます。
    public static void Hide()
    {
        DestroyPreviewEntity();
        if (instance != null)
        {
            instance.selectedEntity = null;
            instance.panelTween?.Kill(false);
            instance.gameObject.SetActive(false);
        }
    }

    // 性能確認用に一時生成したユニットを残さないように破棄します。
    private static void DestroyPreviewEntity()
    {
        if (previewEntityObject == null)
            return;

        Destroy(previewEntityObject);
        previewEntityObject = null;
    }

    // パネルが必要になった時だけ実行時にUI一式を組み立てます。
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        LocalizationManager.EnsureExists();
        GameObject panelObject = new GameObject("UnitStatusPanelUI", typeof(RectTransform));
        instance = panelObject.AddComponent<UnitStatusPanelUI>();
        instance.BuildUi();
        panelObject.SetActive(false);
    }

    // 毎フレーム、選択中ユニットのHP/MP変化をパネルへ反映します。
    private void Update()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }

        if (selectedEntity == null)
        {
            Hide();
            return;
        }

        RefreshLayout();
        ApplyEntity();
    }

    // Canvas、背景、表示項目を画面右端固定で作成します。
    private void BuildUi()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50020;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        gameObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.localScale = Vector3.one * PanelVisualScale;

        Image panelBackground = panelObject.GetComponent<Image>();
        panelBackground.color = new Color(0.012f, 0.018f, 0.028f, 0.94f);
        panelBackground.raycastTarget = false;
        panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();

        teamStripeImage = CreateImage("TeamStripe", panelRect, new Vector2(0f, 0f), new Vector2(5f, MaxPanelHeight), new Color(0.2f, 1f, 0.35f, 1f));
        innerLineImage = CreateImage("InnerLine", panelRect, new Vector2(7f, -7f), new Vector2(PanelWidth - 14f, MaxPanelHeight - 14f), new Color(0.05f, 0.42f, 0.5f, 0.55f));

        Image portraitBackground = CreateImage("PortraitBackground", panelRect, new Vector2(13f, -13f), new Vector2(260f, 126f), new Color(0.02f, 0.04f, 0.075f, 1f));
        portraitBackground.type = Image.Type.Sliced;
        portraitBackground.gameObject.AddComponent<RectMask2D>();
        portraitImage = CreateImage("Portrait", portraitBackground.rectTransform, new Vector2(8f, -8f), PortraitViewportSize, Color.white);
        portraitRect = portraitImage.rectTransform;
        portraitImage.preserveAspect = true;

        starText = CreateText("Stars", panelRect, new Vector2(20f, -19f), new Vector2(118f, 24f), 22f, FontStyles.Bold, new Color(1f, 0.85f, 0.25f, 1f));
        teamText = CreateText("Team", panelRect, new Vector2(151f, -21f), new Vector2(112f, 22f), 16f, FontStyles.Bold, Color.white);
        teamText.alignment = TextAlignmentOptions.TopRight;
        nameText = CreateText("Name", panelRect, new Vector2(22f, -104f), new Vector2(188f, 28f), 20f, FontStyles.Bold, Color.white);
        nameText.enableWordWrapping = false;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 12f;
        nameText.fontSizeMax = 20f;

        CreateBar("HpBar", new Vector2(17f, -148f), new Color(0.01f, 0.03f, 0.018f, 1f), new Color(0.18f, 0.9f, 0.18f, 1f), out hpFillRect, out hpText);
        CreateBar("ManaBar", new Vector2(17f, -172f), new Color(0.01f, 0.02f, 0.05f, 1f), new Color(0.08f, 0.58f, 1f, 1f), out manaFillRect, out manaText);
        synergyText = CreateText("SynergyText", panelRect, new Vector2(20f, -196f), new Vector2(64f, 20f), 13f, FontStyles.Bold, new Color(0.9f, 0.98f, 1f, 1f));
        synergyText.enableWordWrapping = false;
        synergyText.overflowMode = TextOverflowModes.Ellipsis;
        CreateUnitSynergyBadges();

        CreateStatRows();
        skillTitleText = CreateText("SkillTitle", panelRect, new Vector2(20f, -370f), new Vector2(244f, 24f), 17f, FontStyles.Bold, new Color(0.35f, 0.95f, 1f, 1f));
        skillScalingText = CreateText("SkillScaling", panelRect, new Vector2(20f, -394f), new Vector2(48f, 22f), 13f, FontStyles.Bold, new Color(0.75f, 0.95f, 1f, 1f));
        skillScalingText.enableWordWrapping = false;
        CreateSkillScalingIcons();
        skillBodyText = CreateText("SkillBody", panelRect, new Vector2(20f, -418f), new Vector2(244f, 68f), 14f, FontStyles.Normal, new Color(0.86f, 0.93f, 1f, 1f));
        skillBodyText.enableAutoSizing = true;
        skillBodyText.fontSizeMin = 10f;
        skillBodyText.fontSizeMax = 14f;
        itemTitleText = CreateText("ItemsTitle", panelRect, new Vector2(20f, -484f), new Vector2(244f, 24f), 17f, FontStyles.Bold, new Color(1f, 0.86f, 0.35f, 1f));

        for (int i = 0; i < 3; i++)
            CreateItemRow(i);

        RefreshLayout();
    }

    // 詳細パネルを開く時だけ、右から軽く出るDOTween演出を入れます。
    private void PlayPanelAppear()
    {
        if (panelRect == null || panelCanvasGroup == null || !gameObject.activeInHierarchy)
            return;

        panelTween?.Kill(false);
        Vector3 targetScale = Vector3.one * PanelVisualScale;
        panelCanvasGroup.alpha = 0f;
        panelRect.localScale = targetScale * 0.94f;

        panelTween = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(true)
            .Append(panelCanvasGroup.DOFade(1f, 0.14f).SetEase(Ease.OutQuad))
            .Join(panelRect.DOScale(targetScale, 0.20f).SetEase(Ease.OutBack));
    }

    // 画面サイズに合わせて、パネルを右端中央に固定し直します。
    private void RefreshLayout()
    {
        if (panelRect == null)
            return;

        // 画面下のショップ/FIGHTボタンに被らないよう、下側に固定の余白を残します。
        float usableHeight = Screen.height - PanelMargin * 2f - BottomUiReserve;
        float targetHeight = Mathf.Clamp(usableHeight, 520f, MaxPanelHeight);
        Vector2 panelSize = new Vector2(PanelWidth, targetHeight);
        if (panelSize == lastPanelSize)
            return;

        lastPanelSize = panelSize;
        panelRect.localScale = Vector3.one * PanelVisualScale;
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = new Vector2(-PanelMargin, -PanelMargin);

        if (teamStripeImage != null)
            teamStripeImage.rectTransform.sizeDelta = new Vector2(5f, targetHeight);

        if (innerLineImage != null)
            innerLineImage.rectTransform.sizeDelta = new Vector2(PanelWidth - 14f, targetHeight - 14f);
    }

    // 選択中ユニットから、現在値をすべて読み取ってUIへ反映します。
    private void ApplyEntity()
    {
        if (selectedEntity == null)
            return;

        Color teamColor = selectedEntity.Team == Team.Team1 ? new Color(0.2f, 1f, 0.36f, 1f) : new Color(1f, 0.25f, 0.25f, 1f);
        teamStripeImage.color = teamColor;

        Sprite entitySprite = GetEntityPortraitSprite(selectedEntity);
        portraitImage.sprite = entitySprite;
        portraitImage.enabled = entitySprite != null;
        FitPortraitToViewport(entitySprite);

        nameText.text = LocalizationManager.UnitName(selectedEntity.UnitId);
        starText.text = new string('*', Mathf.Clamp(selectedEntity.StarLevel, 1, 3));
        teamText.text = LocalizationManager.IsJapanese
            ? (selectedEntity.Team == Team.Team1 ? "味方" : "敵")
            : (selectedEntity.Team == Team.Team1 ? "ALLY" : "ENEMY");
        teamText.color = teamColor;

        UpdateBar(hpFillRect, selectedEntity.MaxHealth <= 0 ? 0f : (float)selectedEntity.CurrentHealth / selectedEntity.MaxHealth);
        hpText.text = LocalizationManager.IsJapanese
            ? $"体力  {selectedEntity.CurrentHealth} / {selectedEntity.MaxHealth}"
            : $"HP  {selectedEntity.CurrentHealth} / {selectedEntity.MaxHealth}";

        UpdateBar(manaFillRect, selectedEntity.MaxMana <= 0 ? 0f : (float)selectedEntity.CurrentMana / selectedEntity.MaxMana);
        manaText.text = LocalizationManager.IsJapanese
            ? $"マナ  {selectedEntity.CurrentMana} / {selectedEntity.MaxMana}"
            : $"MP  {selectedEntity.CurrentMana} / {selectedEntity.MaxMana}";
        synergyText.text = LocalizationManager.IsJapanese ? "シナジー:" : "Synergy:";
        UpdateUnitSynergyBadges(selectedEntity);

        UpdateStatRows(selectedEntity);
        skillTitleText.text = LocalizationManager.IsJapanese
            ? $"スキル: {GetSkillName(selectedEntity)}"
            : $"Skill: {GetSkillName(selectedEntity)}";
        UpdateSkillScalingIcons(selectedEntity);
        skillBodyText.text = BuildSkillText(selectedEntity);
        UpdateItemRows(selectedEntity);
    }

    // 指定した親の中に、矩形画像を作ります。
    private Image CreateImage(string objectName, Transform parent, Vector2 topLeft, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // ショップ用アイコンの縦横比を保ったまま、詳細パネルの枠をしっかり埋めます。
    // 余った部分はPortraitBackground側のRectMask2Dで隠すので、横長画像でも小さく見えません。
    private void FitPortraitToViewport(Sprite sprite)
    {
        if (portraitRect == null)
            return;

        if (sprite == null || sprite.rect.height <= 0f)
        {
            portraitRect.anchoredPosition = new Vector2(8f, -8f);
            portraitRect.sizeDelta = PortraitViewportSize;
            return;
        }

        float targetAspect = PortraitViewportSize.x / PortraitViewportSize.y;
        float spriteAspect = sprite.rect.width / sprite.rect.height;
        float width;
        float height;

        if (spriteAspect > targetAspect)
        {
            height = PortraitViewportSize.y;
            width = height * spriteAspect;
        }
        else
        {
            width = PortraitViewportSize.x;
            height = width / spriteAspect;
        }

        width *= PortraitCoverZoom;
        height *= PortraitCoverZoom;

        portraitRect.sizeDelta = new Vector2(width, height);
        portraitRect.anchoredPosition = new Vector2(
            8f + (PortraitViewportSize.x - width) * 0.5f,
            -8f + (height - PortraitViewportSize.y) * 0.5f);
    }

    // TextMeshProのテキストを、指定範囲に作ります。
    private TextMeshProUGUI CreateText(string objectName, Transform parent, Vector2 topLeft, Vector2 size, float fontSize, FontStyles fontStyle, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;

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

    // 攻撃力や秘力など、アイコン付きで常に見たいステータス行を作成します。
    private void CreateStatRows()
    {
        CreateStatRow(StatIconKind.Health, -219f);
        CreateStatRow(StatIconKind.AttackPower, -240f);
        CreateStatRow(StatIconKind.AttackSpeed, -261f);
        CreateStatRow(StatIconKind.Range, -282f);
        CreateStatRow(StatIconKind.MovementSpeed, -303f);
        CreateStatRow(StatIconKind.DamageReduction, -324f);
        CreateStatRow(StatIconKind.Focus, -345f);
    }

    // ステータス1行分のアイコンと数値テキストを作成します。
    private void CreateStatRow(StatIconKind iconKind, float y)
    {
        Image icon = CreateImage($"{iconKind}Icon", panelRect, new Vector2(20f, y), new Vector2(18f, 18f), Color.white);
        icon.sprite = StatIconLibrary.GetSprite(iconKind);
        icon.preserveAspect = true;

        TextMeshProUGUI valueText = CreateText($"{iconKind}Value", panelRect, new Vector2(45f, y + 1f), new Vector2(223f, 20f), 13f, FontStyles.Normal, new Color(0.92f, 0.98f, 1f, 1f));
        valueText.enableWordWrapping = false;
        valueText.overflowMode = TextOverflowModes.Ellipsis;

        statRowIcons.Add(icon);
        statRowTexts.Add(valueText);
    }

    // スキル説明の中で、どのステータスが主に影響するかをアイコンで見せる枠を作成します。
    private void CreateSkillScalingIcons()
    {
        for (int i = 0; i < 3; i++)
        {
            Image icon = CreateImage($"SkillScaleIcon{i + 1}", panelRect, new Vector2(68f + i * 24f, -392f), new Vector2(19f, 19f), Color.white);
            icon.preserveAspect = true;
            icon.gameObject.SetActive(false);
            skillScalingIcons.Add(icon);
        }
    }

    // ユニットが持つシナジーを、アイコンと名前のバッジで3枠まで表示します。
    private void CreateUnitSynergyBadges()
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject badgeObject = new GameObject($"UnitSynergyBadge{i + 1}", typeof(RectTransform), typeof(Image));
            badgeObject.transform.SetParent(panelRect, false);

            RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = new Vector2(86f + i * 58f, -194f);
            badgeRect.sizeDelta = new Vector2(55f, 20f);

            Image background = badgeObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.72f);
            background.raycastTarget = false;

            Image icon = CreateImage("Icon", badgeObject.transform, new Vector2(3f, -2f), new Vector2(16f, 16f), Color.white);
            icon.preserveAspect = true;

            TextMeshProUGUI label = CreateText("Label", badgeObject.transform, new Vector2(21f, -1f), new Vector2(31f, 18f), 10f, FontStyles.Bold, Color.white);
            label.enableAutoSizing = true;
            label.fontSizeMin = 5f;
            label.fontSizeMax = 10f;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.MidlineLeft;

            unitSynergyBadgeObjects.Add(badgeObject);
            unitSynergyBadgeBackgrounds.Add(background);
            unitSynergyIcons.Add(icon);
            unitSynergyLabels.Add(label);
            badgeObject.SetActive(false);
        }
    }

    // HP/MP用の背景、残量バー、数値テキストをまとめて作ります。
    private void CreateBar(string objectName, Vector2 topLeft, Color backgroundColor, Color fillColor, out RectTransform fillRect, out TextMeshProUGUI valueText)
    {
        Image background = CreateImage(objectName, panelRect, topLeft, new Vector2(BarFullWidth, BarHeight), backgroundColor);
        Image fill = CreateImage($"{objectName}Fill", background.rectTransform, Vector2.zero, new Vector2(BarFullWidth, BarHeight), fillColor);
        fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.anchoredPosition = Vector2.zero;
        fillRect = fill.rectTransform;

        valueText = CreateText($"{objectName}Text", background.rectTransform, new Vector2(0f, 1f), new Vector2(BarFullWidth, BarHeight), 14f, FontStyles.Bold, Color.white);
        valueText.alignment = TextAlignmentOptions.Center;
    }

    // 選択中ユニットのシナジーを、現在言語の名前とアイコンで更新します。
    private void UpdateUnitSynergyBadges(BaseEntity entity)
    {
        List<SynergyType> synergies = entity != null ? entity.GetSynergyTypes() : new List<SynergyType>();

        for (int i = 0; i < unitSynergyBadgeObjects.Count; i++)
        {
            bool visible = i < synergies.Count && synergies[i] != SynergyType.None;
            unitSynergyBadgeObjects[i].SetActive(visible);
            if (!visible)
                continue;

            SynergyType type = synergies[i];
            Color synergyColor = SynergyIconLibrary.GetColor(type);

            unitSynergyBadgeBackgrounds[i].color = new Color(synergyColor.r * 0.18f, synergyColor.g * 0.18f, synergyColor.b * 0.18f, 0.82f);
            unitSynergyIcons[i].sprite = SynergyIconLibrary.GetSprite(type);
            unitSynergyIcons[i].color = synergyColor;
            unitSynergyIcons[i].preserveAspect = true;

            LocalizationManager.ApplyFont(unitSynergyLabels[i]);
            unitSynergyLabels[i].text = LocalizationManager.SynergyName(type);
            unitSynergyLabels[i].color = Color.white;
        }
    }

    // アイテム3枠のうち、1行分を作成します。
    private void CreateItemRow(int index)
    {
        float y = -510f - index * 45f;
        Image iconBack = CreateImage($"ItemSlot{index + 1}", panelRect, new Vector2(20f, y), new Vector2(42f, 42f), new Color(0.02f, 0.05f, 0.06f, 1f));
        Image icon = CreateImage($"ItemIcon{index + 1}", iconBack.rectTransform, new Vector2(4f, -4f), new Vector2(34f, 34f), Color.white);
        icon.preserveAspect = true;
        TextMeshProUGUI description = CreateText($"ItemText{index + 1}", panelRect, new Vector2(70f, y - 1f), new Vector2(195f, 48f), 13f, FontStyles.Normal, new Color(0.88f, 0.94f, 1f, 1f));
        description.enableWordWrapping = true;
        description.overflowMode = TextOverflowModes.Overflow;
        description.enableAutoSizing = true;
        description.fontSizeMin = 10f;
        description.fontSizeMax = 13f;

        itemIconImages.Add(icon);
        itemDescriptionTexts.Add(description);
    }

    // 残量バーの横幅を、0から1の割合で更新します。
    private void UpdateBar(RectTransform fillRect, float fraction)
    {
        if (fillRect == null)
            return;

        Vector2 size = fillRect.sizeDelta;
        size.x = BarFullWidth * Mathf.Clamp01(fraction);
        size.y = BarHeight;
        fillRect.sizeDelta = size;
    }

    // 基本ステータス行へ、現在値をアイコン横に表示します。
    private void UpdateStatRows(BaseEntity entity)
    {
        if (statRowTexts.Count < 7)
            return;

        string itemLabel = LocalizationManager.IsJapanese ? "アイテム" : "Items";
        statRowTexts[0].text = $"{StatIconLibrary.GetLabel(StatIconKind.Health)} {entity.CurrentHealth}/{entity.MaxHealth}";
        statRowTexts[1].text = $"{StatIconLibrary.GetLabel(StatIconKind.AttackPower)} {entity.baseDamage}  ★{entity.StarLevel}";
        statRowTexts[2].text = $"{StatIconLibrary.GetLabel(StatIconKind.AttackSpeed)} {entity.attackSpeed:0.00}/s";
        statRowTexts[3].text = $"{StatIconLibrary.GetLabel(StatIconKind.Range)} {entity.range}";
        statRowTexts[4].text = $"{StatIconLibrary.GetLabel(StatIconKind.MovementSpeed)} {entity.movementSpeed:0.00}";
        statRowTexts[5].text = $"{StatIconLibrary.GetLabel(StatIconKind.DamageReduction)} {LocalizationManager.FormatPercent(GetDamageReduction(entity))}";
        statRowTexts[6].text = $"{StatIconLibrary.GetLabel(StatIconKind.Focus)} {LocalizationManager.FormatPercent(GetFocusBonus(entity))}  {itemLabel} {entity.EquippedItems.Count}/3";
    }

    // スキルが参照する値を、説明文の直上にアイコンとして表示します。
    private void UpdateSkillScalingIcons(BaseEntity entity)
    {
        List<StatIconKind> icons = entity != null && entity.SkillUsesFocusOnly
            ? new List<StatIconKind> { StatIconKind.Focus }
            : StatIconLibrary.GetSkillScalingIcons(entity != null ? entity.skillType : UnitSkillType.PowerStrike);
        skillScalingText.text = LocalizationManager.IsJapanese ? "参照" : "Uses";

        for (int i = 0; i < skillScalingIcons.Count; i++)
        {
            Image icon = skillScalingIcons[i];
            bool active = i < icons.Count;
            icon.gameObject.SetActive(active);
            if (active)
                icon.sprite = StatIconLibrary.GetSprite(icons[i]);
        }
    }

    // ユニット専用スキルがある場合は、その名前を優先して返します。
    private string GetSkillName(BaseEntity entity)
    {
        string id = GetNormalizedUnitId(entity);
        bool japanese = LocalizationManager.IsJapanese;

        switch (id)
        {
            case "archdeacon":
                return japanese ? "聖令の祈り" : "Holy Edict";
            case "backlinearcher":
                return japanese ? "貫通斉射" : "Piercing Volley";
            case "auroralioness":
                return japanese ? "極光の加護" : "Aurora Guard";
            case "azuritelion":
                return japanese ? "凍牙の突進" : "Frost Pounce";
            case "altgeneraltier2":
                return japanese ? "双炎凍刃" : "Twin Element";
            case "sandpanther":
                return japanese ? "砂影の奇襲" : "Dune Ambush";
            case "protector":
                return japanese ? "守護結界" : "Bulwark Link";
            case "taskmaster":
                return japanese ? "鞭の号令" : "Whip Command";
            case "kane":
                return japanese ? "電磁砲台" : "Storm Turret";
            case "malyk":
                return japanese ? "魂喰い" : "Soul Drain";
            case "paragon":
                return japanese ? "典範の盾" : "Paragon Aegis";
            case "ilenamk2":
                return japanese ? "氷晶格子" : "Crystal Lattice";
            case "wujin":
                return japanese ? "帝炎陣" : "Imperial Pyre";
            case "wraith":
                return japanese ? "霊墓の吹雪" : "Grave Blizzard";
            case "snowchasermk":
                return japanese ? "雪追いの中継" : "Snow Relay";
            case "solfist":
                return japanese ? "太陽拳連爆" : "Solar Combo";
            case "maehvmk":
                return japanese ? "雷磁レール" : "Rail Arc";
            case "decepticleprime":
                return japanese ? "照準砲列" : "Prism Battery";
            case "tier2general":
                return japanese ? "氷将の号令" : "Glacial Command";
            case "shadowlord":
                return japanese ? "影断ち" : "Shadow Rend";
            case "skindogehai":
                return japanese ? "雪狐奇襲" : "Frostfang Ambush";
            case "embergeneral":
                return japanese ? "王の号令" : "Royal Command";
            case "kron":
                return japanese ? "裁きの天秤" : "Judgement Scale";
            case "invader":
                return japanese ? "雷神降臨" : "Thunder God Descends";
            case "gol":
                return japanese ? "黒穴召喚" : "Black Hole Summon";
            case "legion":
                return japanese ? "死者の大行進" : "March of the Dead";
            case "plaguegeneral":
                return japanese ? "終末の咆哮" : "Doomsday Roar";
            case "skyfalltyrant":
                return japanese ? "竜熱暴走" : "Dragonheat Rampage";
            default:
                return GetSkillName(entity != null ? entity.skillType : UnitSkillType.PowerStrike);
        }
    }

    // スキル種別ごとに、パネルへ出す名前を返します。
    private string GetSkillName(UnitSkillType skillType)
    {
        return LocalizationManager.SkillName(skillType);
    }

    // スキルの効果量を、現在ステータスと装備アイテム込みで説明します。
    private string BuildSkillText(BaseEntity entity)
    {
        string dedicatedText = BuildDedicatedLegendarySkillText(entity);
        if (!string.IsNullOrEmpty(dedicatedText))
            return dedicatedText;

        string baseText;
        if (LocalizationManager.IsJapanese)
        {
            switch (entity.skillType)
            {
                case UnitSkillType.SelfHeal:
                    baseText = $"マナ最大時、自身のHPを{GetSelfHealAmount(entity)}回復する。";
                    break;
                case UnitSkillType.AllyHeal:
                    baseText = $"最も傷ついた味方のHPを{GetAllyHealAmount(entity)}回復する。";
                    break;
                case UnitSkillType.Shield:
                    baseText = $"{GetSkillDuration(entity, entity.skillShieldDuration, true):0.#}秒間、HP{GetShieldAmount(entity)}分の白いシールドを得る。";
                    break;
                case UnitSkillType.AttackSpeedBoost:
                    baseText = $"{GetSkillDuration(entity, entity.skillBuffDuration, true):0.#}秒間、攻撃速度を{FormatPercent(GetBoostAmount(entity, entity.skillAttackSpeedBoostMultiplier))}上げる。";
                    break;
                case UnitSkillType.Stun:
                    baseText = $"対象を{GetSkillDuration(entity, entity.skillStunDuration, false):0.#}秒間スタンさせる。";
                    break;
                case UnitSkillType.Slow:
                    baseText = $"{GetSkillDuration(entity, entity.skillSlowDuration, true):0.#}秒間、対象の攻撃速度を{FormatPercent(GetSlowAmount(entity))}下げる。";
                    break;
                case UnitSkillType.DamageBoost:
                    baseText = $"{GetSkillDuration(entity, entity.skillBuffDuration, true):0.#}秒間、通常攻撃ダメージを{FormatPercent(GetBoostAmount(entity, entity.skillDamageBoostMultiplier))}上げる。";
                    break;
                case UnitSkillType.AreaDamage:
                    baseText = $"対象の周囲{entity.skillAreaRadius:0.#}マスに{GetAreaDamage(entity)}ダメージを与える。";
                    break;
                default:
                    baseText = $"次の攻撃で{GetPowerStrikeDamage(entity)}ダメージを与える。";
                    break;
            }

            return AppendExtraSkillText(entity, baseText);
        }

        switch (entity.skillType)
        {
            case UnitSkillType.SelfHeal:
                baseText = $"Restores {GetSelfHealAmount(entity)} HP to itself when MP is full.";
                break;
            case UnitSkillType.AllyHeal:
                baseText = $"Restores {GetAllyHealAmount(entity)} HP to the most damaged ally.";
                break;
            case UnitSkillType.Shield:
                baseText = $"Gains a white shield for {GetShieldAmount(entity)} HP during {GetSkillDuration(entity, entity.skillShieldDuration, true):0.#}s.";
                break;
            case UnitSkillType.AttackSpeedBoost:
                baseText = $"Increases attack speed by {FormatPercent(GetBoostAmount(entity, entity.skillAttackSpeedBoostMultiplier))} for {GetSkillDuration(entity, entity.skillBuffDuration, true):0.#}s.";
                break;
            case UnitSkillType.Stun:
                baseText = $"Stops the target for {GetSkillDuration(entity, entity.skillStunDuration, false):0.#}s.";
                break;
            case UnitSkillType.Slow:
                baseText = $"Lowers target attack speed by {FormatPercent(GetSlowAmount(entity))} for {GetSkillDuration(entity, entity.skillSlowDuration, true):0.#}s.";
                break;
            case UnitSkillType.DamageBoost:
                baseText = $"Increases normal attack damage by {FormatPercent(GetBoostAmount(entity, entity.skillDamageBoostMultiplier))} for {GetSkillDuration(entity, entity.skillBuffDuration, true):0.#}s.";
                break;
            case UnitSkillType.AreaDamage:
                baseText = $"Deals {GetAreaDamage(entity)} damage around the target within {entity.skillAreaRadius:0.#} cells.";
                break;
            default:
                baseText = $"Next attack deals {GetPowerStrikeDamage(entity)} damage.";
                break;
        }

        return AppendExtraSkillText(entity, baseText);
    }

    // 専用スキルは、汎用スキル説明ではなく固有効果を表示します。
    private string BuildDedicatedLegendarySkillText(BaseEntity entity)
    {
        if (entity == null)
            return string.Empty;

        string id = GetNormalizedUnitId(entity);
        bool japanese = LocalizationManager.IsJapanese;
        string starNote = japanese
            ? "★3時は範囲・効果量・ダメージが大幅に上がる。"
            : "At star 3, range, effect amount, and damage rise dramatically.";

        if (japanese)
        {
            switch (id)
            {
                case "archdeacon":
                    return "最も傷ついた味方を回復し、シールドを付与する。対象周囲の味方も少し回復し、短時間だけ被ダメージ軽減を得る。";
                case "backlinearcher":
                    return "対象と同じ横列の敵へ貫通矢を放つ。主対象に大きめの攻撃力ダメージを与え、巻き込んだ敵の移動速度を短時間下げる。";
                case "auroralioness":
                    return "自分中心に極光を広げ、周囲の味方へシールドと攻撃速度上昇を付与する。";
                case "azuritelion":
                    return "対象の近くへ飛び込み、周囲の敵へ氷ダメージを与える。命中した敵は攻撃速度が下がり、凍結蓄積を受ける。";
                case "altgeneraltier2":
                    return "遠距離から火と氷を同時に撃ち、対象周囲に秘力ダメージを与える。命中した敵に燃焼と攻撃速度低下を付与する。";
                case "sandpanther":
                    return "HP割合が低い敵、いなければ最も遠い敵へ飛び込む。瀕死の敵には追加ダメージを与え、発動後しばらく狙われにくくなる。";
                case "protector":
                    return "自分と最も傷ついた味方へシールドを張る。対象周囲の味方は短時間、被ダメージ軽減を得る。";
                case "taskmaster":
                    return "鞭で対象にダメージを与えてスタンさせる。周囲の味方は短時間、攻撃速度が上がる。";
                case "kane":
                    return "機械砲台を起動し、複数回の雷撃をランダムな敵へ放つ。雷撃は近くの敵へ連鎖する。";
                case "malyk":
                    return "対象周囲の敵から魂を吸い、秘力ダメージと与ダメージ低下を与える。吸い取った力で最も傷ついた味方を回復する。";
                case "paragon":
                    return "周囲の味方へシールドと被ダメージ軽減を付与する。近くの敵には神聖な反撃ダメージを与える。";
                case "ilenamk2":
                    return "対象周囲に氷晶の格子を展開し、範囲内の敵へ秘力ダメージと攻撃速度低下、凍結蓄積を与える。範囲内の味方には小シールドを付与する。";
                case "wujin":
                    return "自分中心に帝炎の陣を作る。範囲内の敵へ炎ダメージと燃焼を与え、範囲内の味方の攻撃速度を上げる。";
                case "wraith":
                    return "対象周囲に霊の吹雪を発生させ、秘力ダメージ、攻撃速度低下、与ダメージ低下を与える。";
                case "snowchasermk":
                    return "最も傷ついた味方を回復し、シールドを付与する。もう1体の味方にも回復を中継し、回復対象の近くの敵を鈍化させる。";
                case "solfist":
                    return "対象へ強烈な拳を叩き込み、短時間スタンさせる。周囲の敵には爆発ダメージと燃焼を与える。";
                case "maehvmk":
                    return "遠距離から雷磁レールを放ち、対象に秘力ダメージと短いスタンを与える。雷撃は近くの敵へ連鎖する。";
                case "decepticleprime":
                    return "遠距離から照準砲を連射する。命中した敵に攻撃力ダメージを与え、短時間だけ被ダメージ軽減を下げる。砲撃は近くの敵へ照準を移す。";
                case "tier2general":
                    return "対象へ氷の斬撃を放ち、攻撃速度低下と凍結蓄積を与える。周囲の味方は攻撃速度と被ダメージ軽減を得る。";
                case "shadowlord":
                    return "最も遠い敵へ飛び込み、大ダメージを与える。発動後、短時間だけ敵から狙われにくくなる。";
                case "skindogehai":
                    return "最も遠い敵へ飛び込み、大ダメージと短いスタンを与える。後衛を崩す暗殺スキル。";
                case "embergeneral":
                    return $"味方全体へ5秒間の号令を出し、攻撃速度とMP獲得量を上げる。周囲1マスの味方はさらに被ダメージ軽減を得る。発動時、自身にシールドを付与。\n{starNote}";
                case "kron":
                    return $"敵味方全体のHP割合を見て効果が変わる。味方が劣勢なら全体回復、敵が瀕死なら全体処刑ダメージ、それ以外なら味方にシールドを付与し敵に小ダメージ。\n{starNote}";
                case "invader":
                    return $"5秒間、ランダムな敵へ落雷を繰り返す。雷撃は近くの敵へ1回連鎖し、撃破すると追加落雷が発生する。秘力が高いほど雷撃回数が伸びる。\n{starNote}";
                case "gol":
                    return $"対象付近に黒穴を作り、周囲の敵を吸い寄せながら継続ダメージを与える。終了時に爆発し、与えたダメージの一部をシールドに変える。\n{starNote}";
                case "legion":
                    return $"Taskmasterの亡霊を複数召喚する。亡霊は一定時間で消え、死亡時に周囲の敵へスロウを与える。召喚体はシナジーカウント対象外。\n{starNote}";
                case "plaguegeneral":
                    return $"咆哮で敵全体にダメージを与え、攻撃力・移動速度・攻撃速度を下げる。近くの敵はさらに短時間スタンする。\n{starNote}";
                case "skyfalltyrant":
                    return $"6秒間暴走し、通常攻撃を止めて前方へ火炎を吐き続ける。炎に巻き込まれた敵へ範囲ダメージと燃焼を与える。\n{starNote}";
                default:
                    return string.Empty;
            }
        }

        switch (id)
        {
            case "archdeacon":
                return "Heals the most damaged ally and grants a shield. Nearby allies receive a smaller heal and brief damage reduction.";
            case "backlinearcher":
                return "Fires a piercing volley through the target row. The main target takes heavy attack damage, and clipped enemies are briefly slowed.";
            case "auroralioness":
                return "Spreads aurora light around itself, shielding nearby allies and increasing their attack speed.";
            case "azuritelion":
                return "Pounces near the target, dealing frost damage around it. Hit enemies lose attack speed and gain frost stacks.";
            case "altgeneraltier2":
                return "Fires flame and frost from range, dealing focus damage around the target. Hit enemies burn and lose attack speed.";
            case "sandpanther":
                return "Ambushes the lowest-health enemy, or the farthest enemy if none is wounded. Deals extra damage to weakened targets and becomes briefly untargetable.";
            case "protector":
                return "Links a shield to itself and the most damaged ally. Allies near the linked target gain brief damage reduction.";
            case "taskmaster":
                return "Whips the target for damage and a stun. Nearby allies gain attack speed for a short time.";
            case "kane":
                return "Deploys storm-turret fire, launching several lightning shots at random enemies. Bolts chain to nearby enemies.";
            case "malyk":
                return "Drains souls around the target, dealing focus damage and lowering enemy damage dealt. The drained force heals the most damaged ally.";
            case "paragon":
                return "Grants shields and damage reduction to nearby allies, then pulses holy retaliatory damage into nearby enemies.";
            case "ilenamk2":
                return "Creates a crystal lattice around the target, dealing focus damage, slowing attack speed, and adding frost stacks. Allies inside gain a small shield.";
            case "wujin":
                return "Creates an imperial pyre around itself. Enemies inside take fire damage and burn, while allies inside gain attack speed.";
            case "wraith":
                return "Calls a grave blizzard around the target, dealing focus damage while lowering attack speed and damage dealt.";
            case "snowchasermk":
                return "Heals and shields the most damaged ally, then relays a smaller heal to another ally. Enemies near the healed ally are slowed.";
            case "solfist":
                return "Slams the target with a solar fist, briefly stunning it. Nearby enemies take explosive damage and burn.";
            case "maehvmk":
                return "Fires a ranged rail arc, dealing focus damage and a short stun. The lightning chains to nearby enemies.";
            case "decepticleprime":
                return "Fires a ranged prism battery. Hits deal attack damage and briefly lower enemy damage reduction, then retarget nearby enemies.";
            case "tier2general":
                return "Strikes the target with glacial force, lowering attack speed and adding frost stacks. Nearby allies gain attack speed and damage reduction.";
            case "shadowlord":
                return "Leaps to the farthest enemy, dealing heavy damage. It becomes briefly harder to target after the strike.";
            case "skindogehai":
                return "Leaps to the farthest enemy, dealing heavy damage and a short stun. Built to collapse the backline.";
            case "embergeneral":
                return $"Commands all allies for 5s, increasing attack speed and MP gain. Allies within 1 cell gain extra damage reduction. Also grants itself a shield.\n{starNote}";
            case "kron":
                return $"Judges team HP ratios. If allies are losing, heals all allies. If enemies are low, executes all enemies. Otherwise shields allies and deals light damage to enemies.\n{starNote}";
            case "invader":
                return $"Calls lightning for 5s, repeatedly striking random enemies. Each bolt chains once to a nearby enemy, and kills trigger an extra bolt. Focus increases strike count.\n{starNote}";
            case "gol":
                return $"Creates a black hole near the target, pulling enemies inward while dealing repeated damage. It ends with an explosion and converts part of the damage dealt into a shield.\n{starNote}";
            case "legion":
                return $"Summons several Taskmaster ghosts. They expire after a short time and slow nearby enemies when they die. Summons do not count for synergies.\n{starNote}";
            case "plaguegeneral":
                return $"Roars at all enemies, dealing damage and lowering attack power, movement speed, and attack speed. Nearby enemies are also briefly stunned.\n{starNote}";
            case "skyfalltyrant":
                return $"Rampages for 6s, stopping normal attacks and breathing fire forward. Enemies caught in the flames take area damage and burn.\n{starNote}";
            default:
                return string.Empty;
        }
    }

    // ユニット固有の追加効果やパッシブを、基本スキル説明に追記します。
    private string AppendExtraSkillText(BaseEntity entity, string baseText)
    {
        StringBuilder builder = new StringBuilder(baseText);
        string id = GetNormalizedUnitId(entity);
        bool japanese = LocalizationManager.IsJapanese;

        if (id == "city")
            builder.Append(japanese ? $" 周囲{Mathf.Max(2f, entity.skillAreaRadius):0.#}マスの味方にも付与。" : $" Also affects allies within {Mathf.Max(2f, entity.skillAreaRadius):0.#} cells.");
        else if (id == "candypanda" || id == "snowchasermk")
            builder.Append(japanese ? $" 主対象の周囲{entity.skillAreaRadius:0.#}マスの味方も少し回復。" : $" Also lightly heals allies within {entity.skillAreaRadius:0.#} cells of the main target.");

        if (id == "vampire" || id == "maehvmk")
            builder.Append(japanese ? " 命中した敵の攻撃速度も短時間低下。" : " Hit enemies are also briefly slowed.");

        if (id == "cindera" || id == "solfist")
            builder.Append(japanese ? " 命中後、燃焼で追加ダメージ。" : " Hit enemies also take burn damage.");

        if (id == "shadowlord")
            builder.Append(japanese ? " 最も遠い敵へ飛び込み、短時間狙われにくくなる。" : " Leaps to the farthest enemy and becomes briefly untargetable.");
        else if (id == "skindogehai")
            builder.Append(japanese ? " 最も遠い敵へ飛び込み、対象を短時間スタンさせる。" : " Leaps to the farthest enemy and briefly stuns the target.");

        if (id == "borealjuggernaut")
            builder.Append(japanese ? "\nパッシブ: 通常攻撃が対象の周囲にも命中。マナ獲得は1回分。" : "\nPassive: Normal attacks also hit adjacent enemies. Mana gain counts once.");

        return builder.ToString();
    }

    // CloneやStar表記を外し、小文字の比較用IDへ変換します。
    private string GetNormalizedUnitId(BaseEntity entity)
    {
        if (entity == null)
            return string.Empty;

        string cleanName = LocalizationManager.CleanUnitName(entity.UnitId);
        return string.IsNullOrEmpty(cleanName) ? string.Empty : cleanName.ToLowerInvariant();
    }

    // 装備アイテムのアイコンと効果説明を3枠へ反映します。
    private void UpdateItemRows(BaseEntity entity)
    {
        IReadOnlyList<ItemData> items = entity.EquippedItems;
        itemTitleText.text = LocalizationManager.IsJapanese ? $"アイテム {items.Count}/3" : $"Items {items.Count}/3";

        for (int i = 0; i < itemIconImages.Count; i++)
        {
            ItemData item = i < items.Count ? items[i] : null;
            Image iconImage = itemIconImages[i];
            TextMeshProUGUI descriptionText = itemDescriptionTexts[i];

            if (item != null)
            {
                iconImage.enabled = item.Icon != null;
                iconImage.sprite = item.Icon;
                descriptionText.text = $"{LocalizationManager.ItemName(item)}\n{BuildItemEffectText(item)}";
                descriptionText.color = new Color(0.9f, 0.98f, 1f, 1f);
            }
            else
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
                descriptionText.text = LocalizationManager.IsJapanese ? "空き" : "Empty";
                descriptionText.color = new Color(0.45f, 0.55f, 0.62f, 1f);
            }
        }
    }

    // アイテムの効果量を、短い英語表記へ変換します。
    private string BuildItemEffectText(ItemData itemData)
    {
        return LocalizationManager.BuildItemEffectText(itemData, true);
    }

    // 自身回復スキルの回復量を計算します。
    private int GetSelfHealAmount(BaseEntity entity)
    {
        return Mathf.Max(1, Mathf.RoundToInt((entity.skillFlatHeal + entity.MaxHealth * Mathf.Max(0f, entity.skillHealPercent)) * GetSkillEffectMultiplier(entity, true) * 0.78f));
    }

    // 味方回復スキルの回復量を計算します。
    private int GetAllyHealAmount(BaseEntity entity)
    {
        return Mathf.Max(1, Mathf.RoundToInt((entity.skillFlatAllyHeal + entity.MaxHealth * Mathf.Max(0f, entity.skillAllyHealPercent)) * GetSkillEffectMultiplier(entity, true) * 0.78f));
    }

    // シールドスキルの付与量を計算します。
    private int GetShieldAmount(BaseEntity entity)
    {
        return Mathf.Max(1, Mathf.RoundToInt((entity.skillFlatShield + entity.MaxHealth * Mathf.Max(0f, entity.skillShieldPercent)) * GetSkillEffectMultiplier(entity, true)));
    }

    // 強力な一撃スキルのダメージ量を計算します。
    private int GetPowerStrikeDamage(BaseEntity entity)
    {
        float baseValue = entity.SkillUsesFocusOnly ? entity.SkillBasePower : entity.baseDamage;
        return Mathf.Max(1, Mathf.RoundToInt(baseValue * entity.skillDamageMultiplier * GetSkillEffectMultiplier(entity, true)));
    }

    // 範囲ダメージスキルのダメージ量を計算します。
    private int GetAreaDamage(BaseEntity entity)
    {
        float baseValue = entity.SkillUsesFocusOnly ? entity.SkillBasePower : entity.baseDamage;
        return Mathf.Max(1, Mathf.RoundToInt(baseValue * entity.skillAreaDamageMultiplier * GetSkillEffectMultiplier(entity, true)));
    }

    // 装備中アイテムによる被ダメージ軽減率を合計します。
    private float GetDamageReduction(BaseEntity entity)
    {
        return entity != null ? entity.DamageReduction : 0f;
    }

    // 秘力アイテムの合計値を、ユニットごとの秘力適性込みで返します。
    private float GetFocusBonus(BaseEntity entity)
    {
        if (entity == null)
            return 0f;

        return entity.EquippedItems.Sum(item => item != null ? item.skillPowerPercent : 0f) * Mathf.Max(0.1f, entity.focusInfluence);
    }

    // ★によるスキル全体強化を返します。スタンもこの倍率だけは受けます。
    private float GetStarSkillEffectMultiplier(BaseEntity entity)
    {
        return entity.StarLevel >= 3 ? 1.6f : entity.StarLevel >= 2 ? 1.25f : 1f;
    }

    // 秘力込み、または★だけのスキル効果倍率を返します。
    private float GetSkillEffectMultiplier(BaseEntity entity, bool includeFocus)
    {
        float focusMultiplier = includeFocus ? Mathf.Max(0.1f, 1f + GetFocusBonus(entity)) : 1f;
        return GetStarSkillEffectMultiplier(entity) * focusMultiplier;
    }

    // 効果時間は伸びすぎると読みづらいので、倍率の一部だけを時間へ反映します。
    private float GetSkillDuration(BaseEntity entity, float baseDuration, bool includeFocus)
    {
        float multiplier = GetSkillEffectMultiplier(entity, includeFocus);
        return Mathf.Max(0.1f, baseDuration * (1f + (multiplier - 1f) * 0.65f));
    }

    // 攻撃速度上昇や通常攻撃強化の上昇量を計算します。
    private float GetBoostAmount(BaseEntity entity, float baseMultiplier)
    {
        return Mathf.Max(0f, baseMultiplier - 1f) * GetSkillEffectMultiplier(entity, true);
    }

    // スロウの弱体量を計算します。強くなりすぎないよう上限を置きます。
    private float GetSlowAmount(BaseEntity entity)
    {
        return Mathf.Clamp((1f - entity.skillSlowMultiplier) * GetSkillEffectMultiplier(entity, true), 0f, 0.85f);
    }

    // 小数の倍率を18%のような表記にします。
    private string FormatPercent(float value)
    {
            return LocalizationManager.FormatPercent(value);
    }

    // ショップカードと同じアイコンを優先して、パネルの立ち絵として使います。
    private Sprite GetEntityPortraitSprite(BaseEntity entity)
    {
        Sprite shopIcon = GetShopIcon(entity);
        if (shopIcon != null)
            return shopIcon;

        return GetCurrentEntitySprite(entity);
    }

    // EntityDatabaseから、UnitIdに一致するショップアイコンを探します。
    private Sprite GetShopIcon(BaseEntity entity)
    {
        if (entity == null || GameManager.Instance == null || GameManager.Instance.entitiesDatabase == null || GameManager.Instance.entitiesDatabase.allEntities == null)
            return null;

        string unitName = LocalizationManager.CleanUnitName(entity.UnitId);
        foreach (EntitiesDatabaseSO.EntityData entityData in GameManager.Instance.entitiesDatabase.allEntities)
        {
            if (!string.Equals(entityData.name, unitName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return entityData.icon;
        }

        return null;
    }

    // ショップアイコンが見つからない時だけ、現在表示中のSpriteを保険として使います。
    private Sprite GetCurrentEntitySprite(BaseEntity entity)
    {
        if (entity.spriteRender != null)
            return entity.spriteRender.sprite;

        SpriteRenderer renderer = entity.GetComponentInChildren<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
    }

    // Unity上のCloneやStar表記を外し、パネルで読みやすい名前にします。
    private string CleanUnitName(string rawName) => LocalizationManager.UnitName(rawName);
}
