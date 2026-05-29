using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// シナジー行をクリックした時に、段階ごとの効果を表示する小さな説明パネルです。
// アイテム説明と同じく、必要になったタイミングで自動生成されます。
public class SynergyTooltipUI : MonoBehaviour
{
    private const int CanvasSortingOrder = 50005;
    private const int UnitIconColumns = 7;
    private const int MaxUnitIcons = 21;
    private const float UnitIconSize = 34f;
    private const float UnitIconSpacing = 8f;
    private static readonly Vector2 PanelSize = new Vector2(360f, 560f);
    private static readonly Vector2 PointerOffset = new Vector2(18f, 18f);

    private static SynergyTooltipUI instance;
    private static Sprite frameSprite;
    private static readonly Dictionary<Sprite, Sprite> FaceSpriteCache = new Dictionary<Sprite, Sprite>();

    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private Image iconImage;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI countText;
    private TextMeshProUGUI descriptionText;
    private TextMeshProUGUI unitListTitleText;
    private RectTransform unitGridRect;
    private readonly List<GameObject> unitIconObjects = new List<GameObject>();
    private readonly List<Image> unitIconImages = new List<Image>();
    private readonly List<Image> unitIconFrames = new List<Image>();
    private SynergyType currentType = SynergyType.None;
    private Tween panelTween;

    // 指定したシナジーの説明を、マウス位置の近くに表示します。
    public static void Show(SynergyType type, Vector2 screenPosition)
    {
        if (type == SynergyType.None)
            return;

        ItemTooltipUI.Hide();
        EnsureInstance();
        instance.currentType = type;
        instance.ApplySynergy(type);
        instance.MoveNearPointer(screenPosition);
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

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject tooltipObject = new GameObject("SynergyTooltipUI", typeof(RectTransform));
        instance = tooltipObject.AddComponent<SynergyTooltipUI>();
        instance.BuildUi();
        LocalizationManager.OnLanguageChanged += instance.RefreshLanguage;
        tooltipObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    // Canvasとフレーム背景、テキスト類を実行時に作成します。
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

        iconImage = CreateImage("Icon", new Vector2(24f, -30f), new Vector2(42f, 42f));

        titleText = CreateText("Title", new Vector2(74f, -28f), new Vector2(-28f, -64f), 24f, FontStyles.Bold, Color.white);
        countText = CreateText("Count", new Vector2(24f, -78f), new Vector2(-28f, -108f), 17f, FontStyles.Bold, new Color(0.75f, 1f, 1f, 1f));
        descriptionText = CreateText("Description", new Vector2(24f, -118f), new Vector2(-28f, -382f), 16f, FontStyles.Normal, new Color(0.9f, 0.97f, 1f, 1f));
        descriptionText.lineSpacing = 4f;
        descriptionText.paragraphSpacing = 6f;

        unitListTitleText = CreateText("UnitListTitle", new Vector2(24f, -398f), new Vector2(-28f, -424f), 16f, FontStyles.Bold, new Color(0.75f, 1f, 1f, 1f));
        unitListTitleText.enableWordWrapping = false;
        unitListTitleText.overflowMode = TextOverflowModes.Ellipsis;

        GameObject unitGridObject = new GameObject("UnitIconGrid", typeof(RectTransform));
        unitGridObject.transform.SetParent(panelRect, false);
        unitGridRect = unitGridObject.GetComponent<RectTransform>();
        unitGridRect.anchorMin = new Vector2(0f, 1f);
        unitGridRect.anchorMax = new Vector2(0f, 1f);
        unitGridRect.pivot = new Vector2(0f, 1f);
        unitGridRect.anchoredPosition = new Vector2(24f, -428f);
        unitGridRect.sizeDelta = new Vector2(312f, 104f);
    }

    // シナジー詳細を開いた時だけ、DOTweenで少し拡大しながら表示します。
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
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
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
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    // 現在のカウントと、各段階の効果文を反映します。
    private void ApplySynergy(SynergyType type)
    {
        LocalizationManager.ApplyFont(titleText);
        LocalizationManager.ApplyFont(countText);
        LocalizationManager.ApplyFont(descriptionText);

        Color color = SynergyIconLibrary.GetColor(type);
        iconImage.sprite = SynergyIconLibrary.GetSprite(type);
        iconImage.color = color;

        titleText.text = LocalizationManager.SynergyName(type);

        SynergyManager manager = SynergyManager.Instance;
        int count = manager != null ? manager.GetSynergyCount(type) : 0;
        int tier = manager != null ? manager.GetSynergyTier(type) : 0;
        int next = manager != null ? manager.GetNextRequiredCountForDisplay(type) : 2;
        countText.text = LocalizationManager.IsJapanese
            ? $"現在 {count}/{next}  発動段階 {tier}"
            : $"Current {count}/{next}  Active tier {tier}";

        descriptionText.text = BuildEffectText(type, tier);
        UpdateUnitIconGrid(type);
    }

    // このシナジーを持つ全ユニットを、説明パネル下部に正方形アイコンで並べます。
    private void UpdateUnitIconGrid(SynergyType type)
    {
        LocalizationManager.ApplyFont(unitListTitleText);
        List<EntitiesDatabaseSO.EntityData> units = GetUnitsForSynergy(type);
        unitListTitleText.text = LocalizationManager.IsJapanese
            ? $"該当ユニット {units.Count}"
            : $"Units {units.Count}";

        int visibleCount = Mathf.Min(units.Count, MaxUnitIcons);
        EnsureUnitIconSlots(visibleCount);

        for (int i = 0; i < unitIconObjects.Count; i++)
        {
            bool visible = i < visibleCount;
            unitIconObjects[i].SetActive(visible);
            if (!visible)
                continue;

            EntitiesDatabaseSO.EntityData entityData = units[i];
            Sprite faceSprite = GetFaceSprite(entityData.icon);
            unitIconImages[i].sprite = faceSprite;
            unitIconImages[i].color = faceSprite != null ? Color.white : new Color(0f, 0f, 0f, 0f);

            Color costColor = GetCostFrameColor(entityData.cost);
            unitIconFrames[i].color = new Color(costColor.r, costColor.g, costColor.b, 0.95f);
        }
    }

    private void EnsureUnitIconSlots(int count)
    {
        while (unitIconObjects.Count < count)
            CreateUnitIconSlot(unitIconObjects.Count);
    }

    private void CreateUnitIconSlot(int index)
    {
        GameObject slotObject = new GameObject($"UnitIcon_{index + 1}", typeof(RectTransform), typeof(Image), typeof(Outline));
        slotObject.transform.SetParent(unitGridRect, false);

        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0f, 1f);
        slotRect.anchorMax = new Vector2(0f, 1f);
        slotRect.pivot = new Vector2(0f, 1f);
        slotRect.sizeDelta = new Vector2(UnitIconSize, UnitIconSize);

        int column = index % UnitIconColumns;
        int row = index / UnitIconColumns;
        slotRect.anchoredPosition = new Vector2(column * (UnitIconSize + UnitIconSpacing), -row * (UnitIconSize + UnitIconSpacing));

        Image frame = slotObject.GetComponent<Image>();
        frame.color = new Color(0.02f, 0.04f, 0.055f, 0.92f);
        frame.raycastTarget = false;

        Outline outline = slotObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.78f, 1f, 0.82f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        GameObject iconObject = new GameObject("Face", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(2f, 2f);
        iconRect.offsetMax = new Vector2(-2f, -2f);

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = false;
        icon.raycastTarget = false;

        unitIconObjects.Add(slotObject);
        unitIconFrames.Add(frame);
        unitIconImages.Add(icon);
    }

    private List<EntitiesDatabaseSO.EntityData> GetUnitsForSynergy(SynergyType type)
    {
        List<EntitiesDatabaseSO.EntityData> result = new List<EntitiesDatabaseSO.EntityData>();
        EntitiesDatabaseSO database = GetEntityDatabase();
        if (database == null || database.allEntities == null)
            return result;

        HashSet<string> addedUnitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < database.allEntities.Count; i++)
        {
            EntitiesDatabaseSO.EntityData entityData = database.allEntities[i];
            if (string.IsNullOrEmpty(entityData.name) || !addedUnitNames.Add(entityData.name))
                continue;

            List<SynergyType> synergies = SynergyManager.GetSynergiesForEntityData(entityData);
            if (synergies.Contains(type))
                result.Add(entityData);
        }

        return result;
    }

    private EntitiesDatabaseSO GetEntityDatabase()
    {
        if (GameManager.Instance != null && GameManager.Instance.entitiesDatabase != null)
            return GameManager.Instance.entitiesDatabase;

        return Resources.Load<EntitiesDatabaseSO>("Entity Database");
    }

    // ショップ用の横長アイコンから中央の正方形を切り出し、顔アイコンとして使いやすくします。
    private static Sprite GetFaceSprite(Sprite source)
    {
        if (source == null)
            return null;

        if (FaceSpriteCache.TryGetValue(source, out Sprite cachedSprite))
            return cachedSprite;

        try
        {
            Rect rect = source.textureRect;
            float size = Mathf.Min(rect.width, rect.height);
            Rect squareRect = new Rect(
                rect.x + (rect.width - size) * 0.5f,
                rect.y + (rect.height - size) * 0.5f,
                size,
                size);

            Sprite croppedSprite = Sprite.Create(
                source.texture,
                squareRect,
                new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);

            croppedSprite.name = source.name + "_Face";
            FaceSpriteCache[source] = croppedSprite;
            return croppedSprite;
        }
        catch
        {
            FaceSpriteCache[source] = source;
            return source;
        }
    }

    private Color GetCostFrameColor(int cost)
    {
        switch (cost)
        {
            case 1:
                return new Color(0.76f, 0.86f, 0.94f, 1f);
            case 2:
                return new Color(0.18f, 0.9f, 0.68f, 1f);
            case 3:
                return new Color(0.27f, 0.62f, 1f, 1f);
            case 4:
                return new Color(0.76f, 0.38f, 1f, 1f);
            case 5:
                return new Color(1f, 0.62f, 0.05f, 1f);
            default:
                return Color.white;
        }
    }

    private string BuildEffectText(SynergyType type, int activeTier)
    {
        List<int> tiers = GetTiers(type);
        List<string> lines = new List<string>();
        for (int i = 0; i < tiers.Count; i++)
        {
            int required = tiers[i];
            bool active = activeTier >= required;
            string state = LocalizationManager.IsJapanese
                ? (active ? "発動中" : "未発動")
                : (active ? "Active" : "Locked");
            lines.Add($"{required}: {GetTierText(type, required)}  [{state}]");
        }

        return string.Join("\n\n", lines);
    }

    private List<int> GetTiers(SynergyType type)
    {
        if (type == SynergyType.Apex)
            return new List<int> { 1, 2, 3 };

        if (type == SynergyType.Royal)
            return new List<int> { 1, 2, 4 };

        if (type == SynergyType.Shadow)
            return new List<int> { 2, 4 };

        return new List<int> { 2, 4, 6 };
    }

    private string GetTierText(SynergyType type, int required)
    {
        bool ja = LocalizationManager.IsJapanese;
        switch (type)
        {
            case SynergyType.Warrior:
                if (required == 2) return ja ? "戦士の被ダメージ軽減 +8%" : "Warriors take 8% less damage.";
                if (required == 4) return ja ? "戦士が敵を倒すと最大HPの8%回復" : "Warriors heal 8% max HP on takedown.";
                return ja ? "HP50%以下で1戦闘1回、3秒間軽減 +25%" : "Once per battle below 50% HP, gain +25% reduction for 3s.";
            case SynergyType.Ranger:
                if (required == 2) return ja ? "射手の攻撃速度 +10%" : "Rangers gain +10% attack speed.";
                if (required == 4) return ja ? "同じ敵を攻撃し続けると与ダメージ上昇（1スタック+3%, 最大+15%）" : "Repeated attacks on one target add +3% damage per stack, up to +15%.";
                return ja ? "通常攻撃が20%で追加ダメージ" : "Basic attacks have a 20% chance for bonus damage.";
            case SynergyType.Arcanist:
                if (required == 2) return ja ? "魔導の秘力 +15%" : "Arcanists gain +15% focus.";
                if (required == 4) return ja ? "スキル発動後にMPを20回復" : "Restore 20 MP after casting.";
                return ja ? "戦闘開始時、MP +40" : "Start combat with +40 MP.";
            case SynergyType.Guardian:
                if (required == 2) return ja ? "戦闘開始時、味方全体に最大HP5%のシールド" : "Combat start: all allies gain a 5% max HP shield.";
                if (required == 4) return ja ? "シールド中の味方は被ダメージ軽減 +10%" : "Shielded allies take 10% less damage.";
                return ja ? "シールド破壊時、周囲1マスの敵を0.75秒スタン" : "When a shield breaks, stun nearby enemies for 0.75s.";
            case SynergyType.Beast:
                if (required == 2) return ja ? "獣は通常攻撃ごとに攻撃速度 +1%、最大10%" : "Beasts gain +1% attack speed per basic attack, up to 10%.";
                if (required == 4) return ja ? "最大スタック時、通常攻撃に攻撃力20%の追加ダメージ" : "At max stacks, basic attacks deal +20% attack damage.";
                return ja ? "最大スタック時、通常攻撃ごとに最大HP2%回復" : "At max stacks, basic attacks heal 2% max HP.";
            case SynergyType.Shadow:
                if (required == 2) return ja ? "HP40%以下の敵への与ダメージ +20%" : "Deal +20% damage to enemies below 40% HP.";
                return ja ? "敵撃破後、2秒間攻撃速度と移動速度 +30%" : "After takedown, gain +30% attack and move speed for 2s.";
            case SynergyType.Machine:
                if (required == 2) return ja ? "戦闘開始後5秒間、機械の被ダメージ軽減 +10%" : "Machines take 10% less damage for the first 5s.";
                if (required == 4) return ja ? "HP30%以下で1戦闘1回、最大HP15%回復" : "Once per battle below 30% HP, heal 15% max HP.";
                return ja ? "死亡時、周囲1マスの味方に最大HP10%のシールド" : "On death, shield nearby allies for 10% of their max HP.";
            case SynergyType.Wraith:
                if (required == 2) return ja ? "死亡時、周囲1マスの敵を2秒間 攻撃速度-35%スロウ" : "On death, slow nearby enemies' attack speed by 35% for 2s.";
                if (required == 4) return ja ? "死亡時、近くの味方1体のMPを20回復" : "On death, restore 20 MP to a nearby ally.";
                return ja ? "最初に倒れた亡霊が1戦闘1回、HP30%で復活" : "The first fallen Wraith revives once at 30% HP.";
            case SynergyType.Apex:
                if (required == 1) return ja ? "覇者の与ダメージ +10%" : "Apex units deal +10% damage.";
                if (required == 2) return ja ? "覇者の秘力 +20%、被ダメージ軽減 +8%" : "Apex units gain +20% focus and 8% damage reduction.";
                return ja ? "戦闘開始時、覇者に最大HP8%のシールド" : "Combat start: Apex units gain an 8% max HP shield.";
            case SynergyType.Inferno:
                if (required == 2) return ja ? "通常攻撃かスキルでダメージを与えた敵に燃焼を付与（毎秒 与ダメージ12%+秘力2.5%、3秒間）。" : "Burns damaged enemies for 3s (per second: 12% of damage dealt + 2.5% focus).";
                if (required == 4) return ja ? "燃焼中の敵が倒れると爆発し、周囲1マスの敵へ最大HPの8%ダメージ。" : "Burning enemies explode on death, dealing 8% of max HP to nearby enemies.";
                return ja ? "戦闘開始10秒後、敵全体へ炎の雨（各 最大HPの7%ダメージ）。" : "After 10s, fire rain hits all enemies for 7% of their max HP.";
            case SynergyType.Frost:
                if (required == 2) return ja ? "攻撃時、20%の確率で敵を2秒間スロウします。" : "Attacks have a 20% chance to slow enemies for 2 seconds.";
                if (required == 4) return ja ? "スロウ中の敵を攻撃し続けると凍結が溜まり、3スタックで1秒凍結（スタン）。" : "Hits on slowed enemies build freeze; 3 stacks cause a 1s freeze.";
                return ja ? "戦闘開始時、敵全体を攻撃速度-32%で3秒スロウし、最も攻撃力の高い敵を2秒凍結。" : "Combat start: -32% attack speed to all enemies (3s); freeze the highest-attack enemy for 2s.";
            case SynergyType.Storm:
                if (required == 2) return ja ? "4回攻撃するたび、ランダムな敵1体へ雷撃（秘力55%+攻撃力40%）。" : "Every 4 attacks, strike a random enemy for 55% focus + 40% attack damage.";
                if (required == 4) return ja ? "雷撃が近くの敵1体へ連鎖（雷撃ダメージの65%）。" : "Lightning chains once to a nearby enemy for 65% of the strike.";
                return ja ? "スキル発動時、敵3体に雷撃を放ちます。" : "On skill cast, lightning strikes 3 enemies.";
            case SynergyType.Abyss:
                if (required == 2) return ja ? "戦闘開始時、敵全体の与ダメージを8秒間10%下げます。" : "Combat start: enemies deal 10% less damage for 8 seconds.";
                if (required == 4) return ja ? "敵がスキルを使うたび、その敵に最大HPの4.5%ダメージ。" : "When an enemy casts, it takes 4.5% of its max HP as damage.";
                return ja ? "チームHP28%以下で1戦闘1回、敵全体へ呪い（与ダメージ-18%・攻撃速度-38%・移動-30%・MP獲得-50%、5秒）。" : "Once per battle at ≤28% team HP, curse all enemies (-18% damage, -38% attack speed, -30% move, -50% MP gain for 5s).";
            case SynergyType.Divine:
                if (required == 2) return ja ? "戦闘開始時、HP割合が最も低い味方にHP12%で復活する加護を付与。" : "Combat start: grant the lowest-HP ally a ward that revives at 12% HP.";
                if (required == 4) return ja ? "加護の復活値が25%に強化。復活時、自身にシールド最大HP10%、周囲の味方を最大HP8%回復。" : "Ward revives at 25% HP; on revive, gain a 10% max HP shield and heal nearby allies for 8% max HP.";
                return ja ? "チームHP38%以下で1戦闘1回、味方全体を最大HP16%回復＋シールド最大HP10%（5秒）。" : "Once per battle at ≤38% team HP, heal all allies 16% max HP + a 10% max HP shield (5s).";
            case SynergyType.Frenzy:
                if (required == 2) return ja ? "味方が1体倒れるたび、狂乱ユニットの攻撃速度+10%（加算）。" : "Each allied death grants Frenzy units +10% attack speed (stacking).";
                if (required == 4) return ja ? "狂乱ユニットはHPが低いほど与ダメージ上昇（最大+35%）。" : "Frenzy units deal up to +35% damage as HP drops.";
                return ja ? "1戦闘1回、味方死亡時に狂乱ユニットが5秒暴走（攻撃速度×1.55・与ダメージ+22%・被ダメージ軽減-20%）。" : "Once per battle, an allied death sends Frenzy units rampaging for 5s (×1.55 attack speed, +22% damage, -20% damage reduction).";
            case SynergyType.Royal:
                if (required == 1) return ja ? "最もコストが高い味方を王に指定し、与ダメージ+12%＋シールド最大HP8%。" : "The highest-cost ally becomes king: +12% damage and an 8% max HP shield.";
                if (required == 2) return ja ? "王の周囲1.35マスの味方は護衛となり、与ダメージ+8%・被ダメージ軽減+6%。" : "Allies within 1.35 cells of the king gain +8% damage and +6% damage reduction.";
                return ja ? "味方全体に与ダメージ+8%・秘力+8%・被ダメージ軽減+4%。王が倒れると全体が与ダメージ-15%・移動-15%（60秒）。" : "All allies gain +8% damage, +8% focus, +4% damage reduction. If the king falls, the team suffers -15% damage and -15% move speed for 60s.";
            case SynergyType.Summoner:
                if (required == 2) return ja ? "戦闘開始時、小型召喚体を1体呼び出します。" : "Combat start: summon a small minion.";
                if (required == 4) return ja ? "召喚体が死亡すると、近くの敵へ最大HP4%ダメージ＋攻撃速度-30%（2秒）。" : "When a summon dies, nearby enemies take 4% max HP damage and -30% attack speed (2s).";
                return ja ? "戦闘中1回、召喚体が全滅した時に大型召喚体を呼びます。" : "Once per battle, if all summons die, call a large summon.";
            case SynergyType.Alchemy:
                if (required == 2) return ja ? "Waveクリア時、20%の確率で追加コインを得ます。" : "Wave clear: 20% chance to gain extra gold.";
                if (required == 4) return ja ? "Waveクリア時、18%の確率でランダムアイテムを獲得します。" : "Wave clear: 18% chance to gain a random item.";
                return ja ? "ボスWaveクリア時、追加アイテムを1つ生成します。" : "Boss wave clear: create one extra item.";
            default:
                return string.Empty;
        }
    }

    private void RefreshLanguage()
    {
        if (currentType != SynergyType.None)
            ApplySynergy(currentType);
    }

    private void MoveNearPointer(Vector2 screenPosition)
    {
        Vector2 targetPosition = screenPosition + PointerOffset;
        targetPosition.x = Mathf.Clamp(targetPosition.x, 8f, Mathf.Max(8f, Screen.width - PanelSize.x - 8f));
        targetPosition.y = Mathf.Clamp(targetPosition.y, 8f, Mathf.Max(8f, Screen.height - PanelSize.y - 8f));
        panelRect.anchoredPosition = targetPosition;
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
