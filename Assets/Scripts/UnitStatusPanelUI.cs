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
        // 固有スキルは実数値を含む専用ビルダーで生成します（数値は各 Execute メソッドと同じ式）。
        // 全28固有ユニットを網羅し、未知IDは空文字を返します。
        switch (id)
        {
            case "archdeacon": return BuildArchdeaconSkillText(entity, japanese);
            case "backlinearcher": return BuildBacklineArcherSkillText(entity, japanese);
            case "auroralioness": return BuildAuroralionessSkillText(entity, japanese);
            case "azuritelion": return BuildAzuriteLionSkillText(entity, japanese);
            case "altgeneraltier2": return BuildAltgeneralSkillText(entity, japanese);
            case "sandpanther": return BuildSandpantherSkillText(entity, japanese);
            case "protector": return BuildProtectorSkillText(entity, japanese);
            case "taskmaster": return BuildTaskmasterSkillText(entity, japanese);
            case "kane": return BuildKaneSkillText(entity, japanese);
            case "malyk": return BuildMalykSkillText(entity, japanese);
            case "paragon": return BuildParagonSkillText(entity, japanese);
            case "ilenamk2": return BuildIlenaSkillText(entity, japanese);
            case "wujin": return BuildWujinSkillText(entity, japanese);
            case "wraith": return BuildWraithSkillText(entity, japanese);
            case "snowchasermk": return BuildSnowchaserSkillText(entity, japanese);
            case "solfist": return BuildSolfistSkillText(entity, japanese);
            case "maehvmk": return BuildMaehvSkillText(entity, japanese);
            case "decepticleprime": return BuildDecepticleprimeSkillText(entity, japanese);
            case "tier2general": return BuildTier2GeneralSkillText(entity, japanese);
            case "shadowlord": return BuildShadowlordSkillText(entity, japanese);
            case "skindogehai": return BuildSkindogehaiSkillText(entity, japanese);
            case "embergeneral": return BuildEmbergeneralSkillText(entity, japanese);
            case "kron": return BuildKronSkillText(entity, japanese);
            case "invader": return BuildInvaderSkillText(entity, japanese);
            case "gol": return BuildGolSkillText(entity, japanese);
            case "legion": return BuildLegionSkillText(entity, japanese);
            case "plaguegeneral": return BuildPlaguegeneralSkillText(entity, japanese);
            case "skyfalltyrant": return BuildSkyfalltyrantSkillText(entity, japanese);
            default: return string.Empty;
        }
    }

    // 固有スキルの説明で使う、★3で大きく上がることを示す補足文です。
    private string StarNote(bool japanese) => japanese
        ? "★3で範囲・効果量・ダメージが大幅に上がる。"
        : "Star 3 greatly increases range, effect, and damage.";

    // 伝説級スキルの倍率・範囲・効果時間（BaseEntity の GetLegendary* と同じ係数）。
    private float GetLegendaryMagnitude(BaseEntity e)
    {
        float starMult = e.StarLevel >= 3 ? 6.5f : e.StarLevel >= 2 ? 2.1f : 1f;
        return starMult * Mathf.Max(0.1f, 1f + GetFocusBonus(e));
    }

    private float GetLegendaryRadius(BaseEntity e, float baseRadius)
    {
        float mult = e.StarLevel >= 3 ? 3.4f : e.StarLevel >= 2 ? 1.55f : 1f;
        return baseRadius * mult;
    }

    private float GetLegendaryDuration(BaseEntity e, float baseDuration)
    {
        float mult = e.StarLevel >= 3 ? 1.75f : e.StarLevel >= 2 ? 1.25f : 1f;
        return baseDuration * mult;
    }

    // --- コスト1〜3固有スキルの説明文（数値は各 Execute メソッドと同じ式）。 ---

    // 出典: BaseEntity.ExecuteArchdeaconHolyEdict
    private string BuildArchdeaconSkillText(BaseEntity entity, bool japanese)
    {
        int heal = GetAllyHealAmount(entity);
        int heal2 = Mathf.Max(1, Mathf.RoundToInt(heal * 0.45f));
        int shield = Mathf.Max(1, Mathf.RoundToInt(entity.MaxHealth * 0.11f * GetSkillEffectMultiplier(entity, true)));
        int shield2 = Mathf.Max(1, Mathf.RoundToInt(shield * 0.55f));
        float duration = GetSkillDuration(entity, 3.8f, true);
        string dr = FormatPercent(0.06f * GetSkillEffectMultiplier(entity, false));
        float radius = Mathf.Max(2.1f, entity.skillAreaRadius);
        return japanese
            ? $"最も傷ついた味方をHP{heal}回復＋シールド{shield}付与。周囲・半径{radius:0.#}マスの味方も{heal2}回復・シールド{shield2}を得て、{duration:0.#}秒間 被ダメージ軽減+{dr}。"
            : $"Heals the most damaged ally for {heal} HP and shields {shield}. Allies within {radius:0.#} cells also heal {heal2}, shield {shield2}, and gain +{dr} damage reduction for {duration:0.#}s.";
    }

    // 出典: BaseEntity.ExecuteBacklineArcherPiercingVolley
    private string BuildBacklineArcherSkillText(BaseEntity entity, bool japanese)
    {
        int main = Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * 1.65f * GetSkillEffectMultiplier(entity, true)));
        int splash = Mathf.Max(1, Mathf.RoundToInt(main * 0.62f));
        float slowDuration = GetSkillDuration(entity, 1.8f, false);
        return japanese
            ? $"対象と同じ横列を貫く矢を放つ。主対象に{main}、巻き込んだ敵に{splash}の攻撃ダメージを与え、{slowDuration:0.#}秒間 移動速度-12%。"
            : $"Fires a piercing arrow down the target's row, dealing {main} to the main target and {splash} attack damage to others, slowing their move speed by 12% for {slowDuration:0.#}s.";
    }

    // 出典: BaseEntity.ExecuteAuroralionessGuard
    private string BuildAuroralionessSkillText(BaseEntity entity, bool japanese)
    {
        int selfShield = GetShieldAmount(entity);
        int allyShield = Mathf.Max(1, Mathf.RoundToInt(selfShield * 0.62f));
        float duration = GetSkillDuration(entity, 4.2f, true);
        string asBoost = FormatPercent(0.10f * GetSkillEffectMultiplier(entity, false));
        float radius = Mathf.Max(1.8f, entity.skillAreaRadius);
        return japanese
            ? $"自分中心・半径{radius:0.#}マスの味方へシールド付与（自身{selfShield}／味方{allyShield}）し、{duration:0.#}秒間 攻撃速度+{asBoost}。"
            : $"Shields allies within {radius:0.#} cells (self {selfShield} / allies {allyShield}) and grants +{asBoost} attack speed for {duration:0.#}s.";
    }

    // 出典: BaseEntity.ExecuteAzuriteLionFrostPounce
    private string BuildAzuriteLionSkillText(BaseEntity entity, bool japanese)
    {
        int dmg = GetPowerStrikeDamage(entity);
        int splash = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.52f));
        string slow = FormatPercent(GetSlowAmount(entity));
        float slowDuration = GetSkillDuration(entity, 2.4f, true);
        float radius = Mathf.Max(1.35f, entity.skillAreaRadius);
        return japanese
            ? $"対象付近へ飛び込み、主対象に{dmg}・周囲半径{radius:0.#}マスの敵に{splash}の氷ダメージ。命中した敵を{slowDuration:0.#}秒間 攻撃速度-{slow}＋凍結蓄積。"
            : $"Pounces in, dealing {dmg} to the target and {splash} frost damage within {radius:0.#} cells, applying -{slow} attack speed for {slowDuration:0.#}s plus frost stacks.";
    }

    // 出典: BaseEntity.ExecuteAltgeneralTwinElement
    private string BuildAltgeneralSkillText(BaseEntity entity, bool japanese)
    {
        int area = GetAreaDamage(entity);
        int burn = Mathf.Max(1, Mathf.RoundToInt(area * 0.18f));
        float burnDuration = GetSkillDuration(entity, 3f, true);
        string slow = FormatPercent(GetSlowAmount(entity));
        float slowDuration = GetSkillDuration(entity, 2.2f, true);
        float radius = Mathf.Max(1.75f, entity.skillAreaRadius);
        return japanese
            ? $"遠距離から火と氷を放ち、対象周囲・半径{radius:0.#}マスの敵へ{area}の秘力ダメージ。燃焼{burn}（{burnDuration:0.#}秒）と攻撃速度-{slow}（{slowDuration:0.#}秒）。"
            : $"Fires flame and frost, dealing {area} focus damage within {radius:0.#} cells, plus {burn} burn over {burnDuration:0.#}s and -{slow} attack speed for {slowDuration:0.#}s.";
    }

    // 出典: BaseEntity.ExecuteSandpantherAmbush
    private string BuildSandpantherSkillText(BaseEntity entity, bool japanese)
    {
        int dmg = Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * 1.8f * GetSkillEffectMultiplier(entity, true)));
        int execDmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 1.35f));
        return japanese
            ? $"HPの低い敵（いなければ最も遠い敵）へ飛び込み{dmg}の攻撃ダメージ。HP45%以下の敵には{execDmg}。発動後しばらく狙われにくくなる。"
            : $"Leaps to the lowest-HP enemy (or the farthest), dealing {dmg} attack damage, or {execDmg} to targets below 45% HP. Becomes briefly untargetable after.";
    }

    // 出典: BaseEntity.ExecuteProtectorBulwarkLink
    private string BuildProtectorSkillText(BaseEntity entity, bool japanese)
    {
        int shield = GetShieldAmount(entity);
        float duration = GetSkillDuration(entity, entity.skillShieldDuration, true);
        string dr = FormatPercent(0.12f * GetSkillEffectMultiplier(entity, false));
        float radius = Mathf.Max(1.9f, entity.skillAreaRadius);
        return japanese
            ? $"自身と最も傷ついた味方へ{shield}のシールドを{duration:0.#}秒付与。対象周囲・半径{radius:0.#}マスの味方は被ダメージ軽減+{dr}。"
            : $"Shields itself and the most damaged ally for {shield} ({duration:0.#}s). Allies within {radius:0.#} cells gain +{dr} damage reduction.";
    }

    // 出典: BaseEntity.ExecuteTaskmasterWhipCommand
    private string BuildTaskmasterSkillText(BaseEntity entity, bool japanese)
    {
        int dmg = Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * 1.35f * GetSkillEffectMultiplier(entity, true)));
        float stun = GetSkillDuration(entity, entity.skillStunDuration, false);
        float duration = GetSkillDuration(entity, 3f, false);
        string asBoost = FormatPercent(Mathf.Max(0f, (1.18f + 0.04f * GetSkillEffectMultiplier(entity, false)) - 1f));
        return japanese
            ? $"対象に{dmg}の攻撃ダメージ＋{stun:0.#}秒スタン。周囲2.2マスの味方は{duration:0.#}秒間 攻撃速度+{asBoost}。"
            : $"Whips the target for {dmg} attack damage and a {stun:0.#}s stun. Allies within 2.2 cells gain +{asBoost} attack speed for {duration:0.#}s.";
    }

    // 出典: BaseEntity.ExecuteKaneStormTurret
    private string BuildKaneSkillText(BaseEntity entity, bool japanese)
    {
        int shots = entity.StarLevel >= 3 ? 5 : entity.StarLevel >= 2 ? 4 : 3;
        int perShot = GetAreaDamage(entity);
        int chain = Mathf.Max(1, Mathf.RoundToInt(perShot * 0.45f));
        return japanese
            ? $"機械砲台から{shots}発の雷撃をランダムな敵へ放つ。各{perShot}の秘力ダメージを与え、近くの敵へ{chain}が連鎖する。"
            : $"Fires {shots} lightning shots at random enemies for {perShot} focus damage each, chaining {chain} to a nearby enemy.";
    }

    // 出典: BaseEntity.ExecuteMalykSoulDrain
    private string BuildMalykSkillText(BaseEntity entity, bool japanese)
    {
        int dmg = GetAreaDamage(entity);
        int nonTarget = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.68f));
        float duration = GetSkillDuration(entity, 3f, true);
        float radius = Mathf.Max(2f, entity.skillAreaRadius);
        return japanese
            ? $"対象周囲・半径{radius:0.#}マスの敵から魂を吸う。対象に{dmg}・周囲に{nonTarget}の秘力ダメージと与ダメージ-12%（{duration:0.#}秒）。与ダメージの32%を最も傷ついた味方へ回復。"
            : $"Drains souls within {radius:0.#} cells, dealing {dmg} to the target and {nonTarget} focus damage to others with -12% damage dealt for {duration:0.#}s. Heals the most damaged ally for 32% of the damage dealt.";
    }

    // 出典: BaseEntity.ExecuteParagonAegis
    private string BuildParagonSkillText(BaseEntity entity, bool japanese)
    {
        int shield = GetShieldAmount(entity);
        int pulse = Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * 1.2f * GetSkillEffectMultiplier(entity, true)));
        float duration = GetSkillDuration(entity, entity.skillShieldDuration, true);
        string dr = FormatPercent(0.10f * GetSkillEffectMultiplier(entity, false));
        float radius = Mathf.Max(2.25f, entity.skillAreaRadius);
        return japanese
            ? $"周囲・半径{radius:0.#}マスの味方へ{shield}のシールドと被ダメージ軽減+{dr}（{duration:0.#}秒）。近くの敵へ{pulse}の神聖反撃ダメージ。"
            : $"Grants allies within {radius:0.#} cells a {shield} shield and +{dr} damage reduction ({duration:0.#}s), then pulses {pulse} holy damage to nearby enemies.";
    }

    // 出典: BaseEntity.ExecuteIlenaCrystalLattice
    private string BuildIlenaSkillText(BaseEntity entity, bool japanese)
    {
        int area = GetAreaDamage(entity);
        int allyShield = Mathf.Max(1, Mathf.RoundToInt(GetShieldAmount(entity) * 0.45f));
        float shieldDuration = GetSkillDuration(entity, 3f, true);
        string slow = FormatPercent(GetSlowAmount(entity));
        float slowDuration = GetSkillDuration(entity, 2.8f, true);
        float radius = Mathf.Max(2.4f, entity.skillAreaRadius);
        return japanese
            ? $"対象周囲・半径{radius:0.#}マスに氷晶格子を展開。敵へ{area}の秘力ダメージ＋攻撃速度-{slow}（{slowDuration:0.#}秒）＋凍結蓄積。範囲内の味方へ{allyShield}のシールド（{shieldDuration:0.#}秒）。"
            : $"Forms a crystal lattice within {radius:0.#} cells, dealing {area} focus damage with -{slow} attack speed ({slowDuration:0.#}s) and frost stacks. Allies inside gain a {allyShield} shield ({shieldDuration:0.#}s).";
    }

    // 出典: BaseEntity.ExecuteDecepticleprimePrismBattery
    private string BuildDecepticleprimeSkillText(BaseEntity entity, bool japanese)
    {
        int shots = entity.StarLevel >= 3 ? 5 : entity.StarLevel >= 2 ? 4 : 3;
        int baseDmg = Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * 1.35f * GetSkillEffectMultiplier(entity, true)));
        return japanese
            ? $"{shots}連射の照準砲。初弾{baseDmg}の攻撃ダメージ（弾ごとに約8%減衰）。命中した敵は被ダメージ軽減-6%、砲撃は近くの敵へ照準を移す。"
            : $"Fires {shots} prism shots; the first deals {baseDmg} attack damage (each shot about 8% less). Hits lower enemy damage reduction by 6% and retarget nearby enemies.";
    }

    // 出典: BaseEntity.ExecuteTier2GeneralGlacialCommand
    private string BuildTier2GeneralSkillText(BaseEntity entity, bool japanese)
    {
        int dmg = Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * 1.55f * GetSkillEffectMultiplier(entity, true)));
        float duration = GetSkillDuration(entity, 3.2f, true);
        string slow = FormatPercent(GetSlowAmount(entity));
        string asBoost = FormatPercent(Mathf.Max(0f, (1.12f + 0.03f * GetSkillEffectMultiplier(entity, false)) - 1f));
        string dr = FormatPercent(0.06f);
        return japanese
            ? $"対象へ{dmg}の攻撃ダメージと攻撃速度-{slow}＋凍結蓄積（{duration:0.#}秒）。周囲2.2マスの味方は攻撃速度+{asBoost}・被ダメージ軽減+{dr}。"
            : $"Strikes for {dmg} attack damage with -{slow} attack speed and frost stacks ({duration:0.#}s). Allies within 2.2 cells gain +{asBoost} attack speed and +{dr} damage reduction.";
    }

    // 出典: BaseEntity.ExecuteAssassinLeapStrike (shadowlord)
    private string BuildShadowlordSkillText(BaseEntity entity, bool japanese)
    {
        int dmg = GetPowerStrikeDamage(entity);
        return japanese
            ? $"最も遠い敵へ飛び込み{dmg}の大ダメージを与える。発動後0.75秒は敵から狙われにくくなる。"
            : $"Leaps to the farthest enemy for {dmg} heavy damage, becoming untargetable for 0.75s afterward.";
    }

    // 出典: BaseEntity.ExecuteAssassinLeapStrike (skindogehai)
    private string BuildSkindogehaiSkillText(BaseEntity entity, bool japanese)
    {
        int dmg = GetPowerStrikeDamage(entity);
        float stun = GetSkillDuration(entity, entity.skillStunDuration, false);
        return japanese
            ? $"最も遠い敵へ飛び込み{dmg}の大ダメージ＋{stun:0.#}秒スタン。発動後0.55秒は狙われにくくなる。"
            : $"Leaps to the farthest enemy for {dmg} damage and a {stun:0.#}s stun, becoming untargetable for 0.55s afterward.";
    }

    // --- コスト5（伝説級）固有スキルの説明文。 ---

    // 出典: BaseEntity.ExecuteEmbergeneralRoyalCommand
    private string BuildEmbergeneralSkillText(BaseEntity entity, bool japanese)
    {
        float mag = GetLegendaryMagnitude(entity);
        float duration = GetLegendaryDuration(entity, 5f);
        string asBoost = FormatPercent(entity.StarLevel >= 3 ? 1.25f : 0.20f * mag);
        string manaBoost = FormatPercent(entity.StarLevel >= 3 ? 1.50f : 0.20f * mag);
        string guardDr = FormatPercent(entity.StarLevel >= 3 ? 0.60f : 0.15f * mag);
        float guardRadius = GetLegendaryRadius(entity, 1.15f);
        int selfShield = Mathf.Max(1, Mathf.RoundToInt(entity.MaxHealth * (entity.StarLevel >= 3 ? 1.35f : 0.22f * mag)));
        return japanese
            ? $"味方全体へ{duration:0.#}秒の号令。攻撃速度+{asBoost}・MP獲得+{manaBoost}。半径{guardRadius:0.#}マスの味方はさらに被ダメージ軽減+{guardDr}。自身に{selfShield}のシールド。\n{StarNote(true)}"
            : $"Commands all allies for {duration:0.#}s: +{asBoost} attack speed and +{manaBoost} MP gain. Allies within {guardRadius:0.#} cells also gain +{guardDr} damage reduction. Shields itself for {selfShield}.\n{StarNote(false)}";
    }

    // 出典: BaseEntity.ExecuteKronJudgementScale
    private string BuildKronSkillText(BaseEntity entity, bool japanese)
    {
        float mag = GetLegendaryMagnitude(entity);
        int healAmount = Mathf.Max(1, Mathf.RoundToInt(entity.MaxHealth * (entity.StarLevel >= 3 ? 0.85f : 0.18f * mag)));
        int healShield = Mathf.Max(1, Mathf.RoundToInt(entity.MaxHealth * (entity.StarLevel >= 3 ? 0.45f : 0.08f * mag)));
        string execPct = FormatPercent(entity.StarLevel >= 3 ? 0.95f : 0.18f * mag);
        int balanceShield = Mathf.Max(1, Mathf.RoundToInt(entity.MaxHealth * (entity.StarLevel >= 3 ? 0.65f : 0.11f * mag)));
        return japanese
            ? $"敵味方のHP状況で効果が変化。味方劣勢→全体回復{healAmount}＋シールド{healShield}。敵が瀕死→敵全体へ最大HPの{execPct}の処刑ダメージ。それ以外→味方へシールド{balanceShield}＋敵に小ダメージ。\n{StarNote(true)}"
            : $"Effect shifts with team HP. Allies losing → heal all for {healAmount} + {healShield} shield. Enemies low → execute all for {execPct} of their max HP. Otherwise → shield allies for {balanceShield} + light enemy damage.\n{StarNote(false)}";
    }

    // 出典: BaseEntity.ExecuteInvaderThunderGodCoroutine
    private string BuildInvaderSkillText(BaseEntity entity, bool japanese)
    {
        float mag = GetLegendaryMagnitude(entity);
        float starMult = entity.StarLevel >= 3 ? 3.0f : entity.StarLevel >= 2 ? 1.45f : 1f;
        int strikes = Mathf.Max(1, Mathf.RoundToInt((10f + Mathf.Max(0f, GetFocusBonus(entity)) * 4f) * starMult));
        int strikeDmg = Mathf.Max(1, Mathf.RoundToInt((entity.SkillBasePower * 1.45f + entity.baseDamage * 0.5f) * mag));
        return japanese
            ? $"ランダムな敵へ落雷を約{strikes}回。1撃{strikeDmg}の秘力ダメージを与え、近くの敵へ連鎖し、撃破すると追加落雷が発生する。\n{StarNote(true)}"
            : $"Strikes random enemies about {strikes} times for {strikeDmg} focus damage each, chaining to nearby enemies and triggering an extra bolt on kills.\n{StarNote(false)}";
    }

    // 出典: BaseEntity.ExecuteGolBlackHoleCoroutine
    private string BuildGolSkillText(BaseEntity entity, bool japanese)
    {
        float mag = GetLegendaryMagnitude(entity);
        float duration = entity.StarLevel >= 3 ? 6f : 4f;
        float tick = entity.StarLevel >= 3 ? 0.28f : 0.5f;
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration / tick));
        int tickDmg = Mathf.Max(1, Mathf.RoundToInt((entity.SkillBasePower * 0.9f + entity.baseDamage * 0.35f) * mag));
        int explosion = Mathf.Max(1, Mathf.RoundToInt((entity.SkillBasePower * 2.2f + entity.baseDamage) * mag));
        float radius = GetLegendaryRadius(entity, 1.75f);
        return japanese
            ? $"対象付近・半径{radius:0.#}マスに{duration:0.#}秒の黒穴を展開。敵を吸い寄せ、1ティック{tickDmg}の継続ダメージ（約{ticks}回）。終了時に{explosion}の爆発を起こし、与ダメージの一部をシールド化。\n{StarNote(true)}"
            : $"Opens a black hole within {radius:0.#} cells for {duration:0.#}s, pulling enemies in and dealing {tickDmg} per tick (about {ticks} ticks). Ends with a {explosion} explosion and converts part of the damage into a shield.\n{StarNote(false)}";
    }

    // 出典: BaseEntity.ExecuteLegionDeadMarch
    private string BuildLegionSkillText(BaseEntity entity, bool japanese)
    {
        string summons = entity.StarLevel >= 3 ? "8" : entity.StarLevel >= 2 ? "5" : (japanese ? "2〜4" : "2-4");
        float lifetime = entity.StarLevel >= 3 ? 16f : 8f;
        return japanese
            ? $"Taskmasterの亡霊を{summons}体召喚（{lifetime:0.#}秒で消滅）。亡霊は死亡時に周囲の敵へ攻撃速度-28%・移動速度低下のスロウを与える。召喚体はシナジー対象外。\n{StarNote(true)}"
            : $"Summons {summons} Taskmaster ghosts (expire after {lifetime:0.#}s). On death they slow nearby enemies (-28% attack speed and reduced move speed). Summons don't count for synergies.\n{StarNote(false)}";
    }

    // 出典: BaseEntity.ExecutePlaguegeneralRoar
    private string BuildPlaguegeneralSkillText(BaseEntity entity, bool japanese)
    {
        float mag = GetLegendaryMagnitude(entity);
        float duration = GetLegendaryDuration(entity, 2f);
        int dmg = Mathf.Max(1, Mathf.RoundToInt((entity.SkillBasePower * 1.15f + entity.baseDamage * 0.5f) * mag));
        string dmgDown = FormatPercent(entity.StarLevel >= 3 ? 0.55f : 0.20f * mag);
        string moveDown = FormatPercent(entity.StarLevel >= 3 ? 0.75f : 0.40f);
        string asDown = FormatPercent(1f - (entity.StarLevel >= 3 ? 0.45f : 0.78f));
        float stun = entity.StarLevel >= 3 ? 2.2f : 0.75f;
        float stunRadius = GetLegendaryRadius(entity, 1.65f);
        return japanese
            ? $"咆哮で敵全体へ{dmg}のダメージ。{duration:0.#}秒間 与ダメージ-{dmgDown}・移動速度-{moveDown}・攻撃速度-{asDown}。半径{stunRadius:0.#}マスの敵は{stun:0.#}秒スタン。\n{StarNote(true)}"
            : $"Roars at all enemies for {dmg} damage. For {duration:0.#}s they suffer -{dmgDown} damage dealt, -{moveDown} move speed, -{asDown} attack speed. Enemies within {stunRadius:0.#} cells are stunned {stun:0.#}s.\n{StarNote(false)}";
    }

    // 出典: BaseEntity.ExecuteSkyfallDragonRampageCoroutine
    private string BuildSkyfalltyrantSkillText(BaseEntity entity, bool japanese)
    {
        float mag = GetLegendaryMagnitude(entity);
        float duration = entity.StarLevel >= 3 ? 10f : 6f;
        float tick = entity.StarLevel >= 3 ? 0.25f : 0.45f;
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration / tick));
        int tickDmg = Mathf.Max(1, Mathf.RoundToInt((entity.baseDamage * 1.25f + entity.SkillBasePower * 0.75f) * mag));
        int burn = Mathf.Max(1, Mathf.RoundToInt(tickDmg * 0.18f));
        float coneRange = GetLegendaryRadius(entity, 3.2f);
        return japanese
            ? $"{duration:0.#}秒間暴走し、通常攻撃を止めて前方・射程{coneRange:0.#}マスへ火炎を吐き続ける。1ティック{tickDmg}＋燃焼{burn}（約{ticks}回）。\n{StarNote(true)}"
            : $"Rampages for {duration:0.#}s, stopping normal attacks to breathe fire up to {coneRange:0.#} cells ahead. {tickDmg} per tick plus {burn} burn (about {ticks} ticks).\n{StarNote(false)}";
    }

    // --- コスト4ボス固有スキルの説明文。数値は BaseEntity の各 Execute メソッドと同じ式で算出する。 ---

    // 出典: BaseEntity.ExecuteWujinImperialPyre
    private string BuildWujinSkillText(BaseEntity entity, bool japanese)
    {
        int aoe = GetAreaDamage(entity);
        int burn = Mathf.Max(1, Mathf.RoundToInt(aoe * 0.16f));
        float duration = GetSkillDuration(entity, 3.5f, true);
        float radius = Mathf.Max(2.3f, entity.skillAreaRadius);
        string asBoost = FormatPercent(Mathf.Max(0f, (1.16f + 0.04f * GetSkillEffectMultiplier(entity, false)) - 1f));
        return japanese
            ? $"自分中心・半径{radius:0.#}マスの敵へ{aoe}の範囲ダメージ。命中した敵は{duration:0.#}秒かけて燃焼{burn}ダメージを受ける。範囲内の味方は攻撃速度+{asBoost}（{duration:0.#}秒）。"
            : $"Deals {aoe} area damage to enemies within {radius:0.#} cells of itself. Hit enemies burn for {burn} over {duration:0.#}s. Allies inside gain +{asBoost} attack speed for {duration:0.#}s.";
    }

    // 出典: BaseEntity.ExecuteWraithGraveBlizzard
    private string BuildWraithSkillText(BaseEntity entity, bool japanese)
    {
        int aoe = GetAreaDamage(entity);
        float radius = Mathf.Max(2.5f, entity.skillAreaRadius);
        float duration = GetSkillDuration(entity, 3.2f, true);
        string slow = FormatPercent(GetSlowAmount(entity));
        return japanese
            ? $"対象周囲・半径{radius:0.#}マスの敵へ{aoe}の秘力ダメージ。{duration:0.#}秒間、攻撃速度-{slow}・与ダメージ-10%を付与する。"
            : $"Deals {aoe} focus damage to enemies within {radius:0.#} cells of the target. For {duration:0.#}s they suffer -{slow} attack speed and deal 10% less damage.";
    }

    // 出典: BaseEntity.ExecuteSnowchaserRelay
    private string BuildSnowchaserSkillText(BaseEntity entity, bool japanese)
    {
        int heal = GetAllyHealAmount(entity);
        int shield = Mathf.Max(1, Mathf.RoundToInt(GetShieldAmount(entity) * 0.42f));
        int heal2 = Mathf.Max(1, Mathf.RoundToInt(heal * 0.65f));
        int shield2 = Mathf.Max(1, Mathf.RoundToInt(shield * 0.7f));
        float radius = Mathf.Max(2.6f, entity.skillAreaRadius);
        float duration = GetSkillDuration(entity, 2.5f, true);
        string slow = FormatPercent(GetSlowAmount(entity));
        return japanese
            ? $"最も傷ついた味方をHP{heal}回復＋シールド{shield}付与。もう1体の味方にも{heal2}回復・シールド{shield2}を中継する。回復対象の周囲・半径{radius:0.#}マスの敵を{duration:0.#}秒間 攻撃速度-{slow}。"
            : $"Heals the most damaged ally for {heal} HP and grants a {shield} shield. Relays {heal2} heal and a {shield2} shield to another ally. Enemies within {radius:0.#} cells of the healed ally lose {slow} attack speed for {duration:0.#}s.";
    }

    // 出典: BaseEntity.ExecuteSolfistSolarCombo
    private string BuildSolfistSkillText(BaseEntity entity, bool japanese)
    {
        int main = Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * 1.55f * GetSkillEffectMultiplier(entity, true)));
        int area = GetAreaDamage(entity);
        int burn = Mathf.Max(1, Mathf.RoundToInt(area * 0.18f));
        float radius = Mathf.Max(2.1f, entity.skillAreaRadius);
        float stun = GetSkillDuration(entity, 0.55f, false);
        float burnDur = GetSkillDuration(entity, 3f, true);
        return japanese
            ? $"対象に{main}の攻撃ダメージ＋{stun:0.#}秒スタン。対象周囲・半径{radius:0.#}マスの敵へ{area}ダメージと燃焼{burn}（{burnDur:0.#}秒）。"
            : $"Strikes the target for {main} attack damage and a {stun:0.#}s stun. Enemies within {radius:0.#} cells take {area} damage plus {burn} burn over {burnDur:0.#}s.";
    }

    // 出典: BaseEntity.ExecuteMaehvRailArc
    private string BuildMaehvSkillText(BaseEntity entity, bool japanese)
    {
        int dmg = GetAreaDamage(entity);
        int chain = entity.StarLevel >= 3 ? 4 : entity.StarLevel >= 2 ? 3 : 2;
        float stun = GetSkillDuration(entity, 0.55f, false);
        return japanese
            ? $"最大{chain}体に連鎖する雷撃。初撃{dmg}の秘力ダメージ（連鎖ごとに72%へ減衰）＋{stun:0.#}秒スタンを与える。"
            : $"A lightning arc chaining to up to {chain} enemies. The first hit deals {dmg} focus damage (each chain drops to 72%) and a {stun:0.#}s stun.";
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
