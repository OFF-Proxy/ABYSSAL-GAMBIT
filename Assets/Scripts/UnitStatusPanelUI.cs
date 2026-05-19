using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ユニットをクリックした時に、ステータス・スキル・装備アイテムを画面右端に表示するUIです。
// シーンへ手動配置しなくても、最初にShowが呼ばれたタイミングで自動生成されます。
public class UnitStatusPanelUI : MonoBehaviour
{
    private static UnitStatusPanelUI instance;

    private Canvas canvas;
    private RectTransform panelRect;
    private RectTransform hpFillRect;
    private RectTransform manaFillRect;
    private Image portraitImage;
    private Image teamStripeImage;
    private Image innerLineImage;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI starText;
    private TextMeshProUGUI teamText;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI manaText;
    private TextMeshProUGUI skillTitleText;
    private TextMeshProUGUI skillScalingText;
    private TextMeshProUGUI skillBodyText;
    private TextMeshProUGUI itemTitleText;
    private readonly List<Image> statRowIcons = new List<Image>();
    private readonly List<TextMeshProUGUI> statRowTexts = new List<TextMeshProUGUI>();
    private readonly List<Image> skillScalingIcons = new List<Image>();
    private readonly List<Image> itemIconImages = new List<Image>();
    private readonly List<TextMeshProUGUI> itemDescriptionTexts = new List<TextMeshProUGUI>();

    private BaseEntity selectedEntity;
    private Vector2 lastPanelSize;

    private const float PanelWidth = 286f;
    private const float MaxPanelHeight = 650f;
    private const float PanelMargin = 10f;
    private const float BottomUiReserve = 124f;
    private const float BarFullWidth = 252f;
    private const float BarHeight = 18f;

    // 指定したユニットの情報パネルを表示します。
    public static void Show(BaseEntity entity)
    {
        if (entity == null)
            return;

        EnsureInstance();
        ItemTooltipUI.Hide();
        instance.selectedEntity = entity;
        instance.gameObject.SetActive(true);
        instance.RefreshLayout();
        instance.ApplyEntity();
    }

    // ユニット情報パネルを閉じます。
    public static void Hide()
    {
        if (instance != null)
            instance.gameObject.SetActive(false);
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

        Image panelBackground = panelObject.GetComponent<Image>();
        panelBackground.color = new Color(0.012f, 0.018f, 0.028f, 0.94f);
        panelBackground.raycastTarget = false;

        teamStripeImage = CreateImage("TeamStripe", panelRect, new Vector2(0f, 0f), new Vector2(5f, MaxPanelHeight), new Color(0.2f, 1f, 0.35f, 1f));
        innerLineImage = CreateImage("InnerLine", panelRect, new Vector2(7f, -7f), new Vector2(PanelWidth - 14f, MaxPanelHeight - 14f), new Color(0.05f, 0.42f, 0.5f, 0.55f));

        Image portraitBackground = CreateImage("PortraitBackground", panelRect, new Vector2(13f, -13f), new Vector2(260f, 126f), new Color(0.02f, 0.04f, 0.075f, 1f));
        portraitBackground.type = Image.Type.Sliced;
        portraitImage = CreateImage("Portrait", portraitBackground.rectTransform, new Vector2(8f, -8f), new Vector2(244f, 110f), Color.white);
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

        CreateStatRows();
        skillTitleText = CreateText("SkillTitle", panelRect, new Vector2(20f, -334f), new Vector2(244f, 24f), 17f, FontStyles.Bold, new Color(0.35f, 0.95f, 1f, 1f));
        skillScalingText = CreateText("SkillScaling", panelRect, new Vector2(20f, -358f), new Vector2(48f, 22f), 13f, FontStyles.Bold, new Color(0.75f, 0.95f, 1f, 1f));
        skillScalingText.enableWordWrapping = false;
        CreateSkillScalingIcons();
        skillBodyText = CreateText("SkillBody", panelRect, new Vector2(20f, -382f), new Vector2(244f, 70f), 14f, FontStyles.Normal, new Color(0.86f, 0.93f, 1f, 1f));
        itemTitleText = CreateText("ItemsTitle", panelRect, new Vector2(20f, -430f), new Vector2(244f, 24f), 17f, FontStyles.Bold, new Color(1f, 0.86f, 0.35f, 1f));

        for (int i = 0; i < 3; i++)
            CreateItemRow(i);

        RefreshLayout();
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

        UpdateStatRows(selectedEntity);
        skillTitleText.text = LocalizationManager.IsJapanese
            ? $"スキル: {GetSkillName(selectedEntity.skillType)}"
            : $"Skill: {GetSkillName(selectedEntity.skillType)}";
        UpdateSkillScalingIcons(selectedEntity.skillType);
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
        CreateStatRow(StatIconKind.Health, -205f);
        CreateStatRow(StatIconKind.AttackPower, -229f);
        CreateStatRow(StatIconKind.AttackSpeed, -253f);
        CreateStatRow(StatIconKind.DamageReduction, -277f);
        CreateStatRow(StatIconKind.Focus, -301f);
    }

    // ステータス1行分のアイコンと数値テキストを作成します。
    private void CreateStatRow(StatIconKind iconKind, float y)
    {
        Image icon = CreateImage($"{iconKind}Icon", panelRect, new Vector2(20f, y), new Vector2(20f, 20f), Color.white);
        icon.sprite = StatIconLibrary.GetSprite(iconKind);
        icon.preserveAspect = true;

        TextMeshProUGUI valueText = CreateText($"{iconKind}Value", panelRect, new Vector2(48f, y + 1f), new Vector2(220f, 22f), 14f, FontStyles.Normal, new Color(0.92f, 0.98f, 1f, 1f));
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
            Image icon = CreateImage($"SkillScaleIcon{i + 1}", panelRect, new Vector2(68f + i * 24f, -356f), new Vector2(19f, 19f), Color.white);
            icon.preserveAspect = true;
            icon.gameObject.SetActive(false);
            skillScalingIcons.Add(icon);
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

    // アイテム3枠のうち、1行分を作成します。
    private void CreateItemRow(int index)
    {
        float y = -456f - index * 49f;
        Image iconBack = CreateImage($"ItemSlot{index + 1}", panelRect, new Vector2(20f, y), new Vector2(42f, 42f), new Color(0.02f, 0.05f, 0.06f, 1f));
        Image icon = CreateImage($"ItemIcon{index + 1}", iconBack.rectTransform, new Vector2(4f, -4f), new Vector2(34f, 34f), Color.white);
        icon.preserveAspect = true;
        TextMeshProUGUI description = CreateText($"ItemText{index + 1}", panelRect, new Vector2(70f, y - 1f), new Vector2(195f, 48f), 13f, FontStyles.Normal, new Color(0.88f, 0.94f, 1f, 1f));
        description.enableWordWrapping = false;
        description.overflowMode = TextOverflowModes.Ellipsis;
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
        if (statRowTexts.Count < 5)
            return;

        string rangeLabel = LocalizationManager.IsJapanese ? "射程" : "Range";
        string itemLabel = LocalizationManager.IsJapanese ? "アイテム" : "Items";
        statRowTexts[0].text = $"{StatIconLibrary.GetLabel(StatIconKind.Health)} {entity.CurrentHealth}/{entity.MaxHealth}";
        statRowTexts[1].text = $"{StatIconLibrary.GetLabel(StatIconKind.AttackPower)} {entity.baseDamage}  ★{entity.StarLevel}";
        statRowTexts[2].text = $"{StatIconLibrary.GetLabel(StatIconKind.AttackSpeed)} {entity.attackSpeed:0.00}/s  {rangeLabel} {entity.range}";
        statRowTexts[3].text = $"{StatIconLibrary.GetLabel(StatIconKind.DamageReduction)} {LocalizationManager.FormatPercent(GetDamageReduction(entity))}";
        statRowTexts[4].text = $"{StatIconLibrary.GetLabel(StatIconKind.Focus)} {LocalizationManager.FormatPercent(GetFocusBonus(entity))}  {itemLabel} {entity.EquippedItems.Count}/3";
    }

    // スキルが参照する値を、説明文の直上にアイコンとして表示します。
    private void UpdateSkillScalingIcons(UnitSkillType skillType)
    {
        List<StatIconKind> icons = StatIconLibrary.GetSkillScalingIcons(skillType);
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

    // スキル種別ごとに、パネルへ出す名前を返します。
    private string GetSkillName(UnitSkillType skillType)
    {
        return LocalizationManager.SkillName(skillType);
    }

    // スキルの効果量を、現在ステータスと装備アイテム込みで説明します。
    private string BuildSkillText(BaseEntity entity)
    {
        if (LocalizationManager.IsJapanese)
        {
            switch (entity.skillType)
            {
                case UnitSkillType.SelfHeal:
                    return $"マナ最大時、自身のHPを{GetSelfHealAmount(entity)}回復する。";
                case UnitSkillType.AllyHeal:
                    return $"最も傷ついた味方のHPを{GetAllyHealAmount(entity)}回復する。";
                case UnitSkillType.Shield:
                    return $"{GetSkillDuration(entity, entity.skillShieldDuration, true):0.#}秒間、HP{GetShieldAmount(entity)}分の白いシールドを得る。";
                case UnitSkillType.AttackSpeedBoost:
                    return $"{GetSkillDuration(entity, entity.skillBuffDuration, true):0.#}秒間、攻撃速度を{FormatPercent(GetBoostAmount(entity, entity.skillAttackSpeedBoostMultiplier))}上げる。";
                case UnitSkillType.Stun:
                    return $"対象を{GetSkillDuration(entity, entity.skillStunDuration, false):0.#}秒間スタンさせる。";
                case UnitSkillType.Slow:
                    return $"{GetSkillDuration(entity, entity.skillSlowDuration, true):0.#}秒間、対象の攻撃速度を{FormatPercent(GetSlowAmount(entity))}下げる。";
                case UnitSkillType.DamageBoost:
                    return $"{GetSkillDuration(entity, entity.skillBuffDuration, true):0.#}秒間、通常攻撃ダメージを{FormatPercent(GetBoostAmount(entity, entity.skillDamageBoostMultiplier))}上げる。";
                case UnitSkillType.AreaDamage:
                    return $"対象の周囲{entity.skillAreaRadius:0.#}マスに{GetAreaDamage(entity)}ダメージを与える。";
                default:
                    return $"次の攻撃で{GetPowerStrikeDamage(entity)}ダメージを与える。";
            }
        }

        switch (entity.skillType)
        {
            case UnitSkillType.SelfHeal:
                return $"Restores {GetSelfHealAmount(entity)} HP to itself when MP is full.";
            case UnitSkillType.AllyHeal:
                return $"Restores {GetAllyHealAmount(entity)} HP to the most damaged ally.";
            case UnitSkillType.Shield:
                return $"Gains a white shield for {GetShieldAmount(entity)} HP during {GetSkillDuration(entity, entity.skillShieldDuration, true):0.#}s.";
            case UnitSkillType.AttackSpeedBoost:
                return $"Increases attack speed by {FormatPercent(GetBoostAmount(entity, entity.skillAttackSpeedBoostMultiplier))} for {GetSkillDuration(entity, entity.skillBuffDuration, true):0.#}s.";
            case UnitSkillType.Stun:
                return $"Stops the target for {GetSkillDuration(entity, entity.skillStunDuration, false):0.#}s.";
            case UnitSkillType.Slow:
                return $"Lowers target attack speed by {FormatPercent(GetSlowAmount(entity))} for {GetSkillDuration(entity, entity.skillSlowDuration, true):0.#}s.";
            case UnitSkillType.DamageBoost:
                return $"Increases normal attack damage by {FormatPercent(GetBoostAmount(entity, entity.skillDamageBoostMultiplier))} for {GetSkillDuration(entity, entity.skillBuffDuration, true):0.#}s.";
            case UnitSkillType.AreaDamage:
                return $"Deals {GetAreaDamage(entity)} damage around the target within {entity.skillAreaRadius:0.#} cells.";
            default:
                return $"Next attack deals {GetPowerStrikeDamage(entity)} damage.";
        }
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
        return Mathf.Max(1, Mathf.RoundToInt((entity.skillFlatHeal + entity.MaxHealth * Mathf.Max(0f, entity.skillHealPercent)) * GetSkillEffectMultiplier(entity, true)));
    }

    // 味方回復スキルの回復量を計算します。
    private int GetAllyHealAmount(BaseEntity entity)
    {
        return Mathf.Max(1, Mathf.RoundToInt((entity.skillFlatAllyHeal + entity.MaxHealth * Mathf.Max(0f, entity.skillAllyHealPercent)) * GetSkillEffectMultiplier(entity, true)));
    }

    // シールドスキルの付与量を計算します。
    private int GetShieldAmount(BaseEntity entity)
    {
        return Mathf.Max(1, Mathf.RoundToInt((entity.skillFlatShield + entity.MaxHealth * Mathf.Max(0f, entity.skillShieldPercent)) * GetSkillEffectMultiplier(entity, true)));
    }

    // 強力な一撃スキルのダメージ量を計算します。
    private int GetPowerStrikeDamage(BaseEntity entity)
    {
        return Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * entity.skillDamageMultiplier * GetSkillEffectMultiplier(entity, true)));
    }

    // 範囲ダメージスキルのダメージ量を計算します。
    private int GetAreaDamage(BaseEntity entity)
    {
        return Mathf.Max(1, Mathf.RoundToInt(entity.baseDamage * entity.skillAreaDamageMultiplier * GetSkillEffectMultiplier(entity, true)));
    }

    // 装備中アイテムによる被ダメージ軽減率を合計します。
    private float GetDamageReduction(BaseEntity entity)
    {
        return Mathf.Clamp(entity.EquippedItems.Sum(item => item != null ? item.damageReductionPercent : 0f), 0f, 0.7f);
    }

    // 秘力アイテムの合計値を返します。表示ではこの値をそのまま%化します。
    private float GetFocusBonus(BaseEntity entity)
    {
        return entity.EquippedItems.Sum(item => item != null ? item.skillPowerPercent : 0f);
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
