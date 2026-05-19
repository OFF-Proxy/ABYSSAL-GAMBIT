using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum GameLanguage
{
    English,
    Japanese
}

// 画面表示の言語切替、日本語フォント適用、よく使う文言の翻訳をまとめる管理クラスです。
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }
    public static event Action OnLanguageChanged;

    private Canvas canvas;
    private RectTransform optionPanel;
    private TextMeshProUGUI optionButtonText;
    private TextMeshProUGUI languageButtonText;
    private GameLanguage currentLanguage;

    private static TMP_FontAsset cachedJapaneseFont;
    private static readonly Dictionary<string, string> UnitNameJa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Andromeda", "アンドロメダ" },
        { "Antiswarm", "アンチスウォーム" },
        { "Borealjuggernaut", "ボリアルジャガーノート" },
        { "Chaosknight", "カオスナイト" },
        { "Christmas", "クリスマス" },
        { "vampire", "ヴァンパイア" },
        { "valiant", "ヴァリアント" },
        { "Candypanda", "キャンディパンダ" },
        { "City", "シティ" },
        { "Crystal", "クリスタル" },
        { "Cindera", "シンデラ" },
        { "Decepticle", "デセプティクル" },
        { "Umbra", "アンブラ" },
        { "Spelleater", "スペルイーター" },
        { "Serpenti", "セルペンティ" },
        { "Skindogehai", "スキンドゲハイ" },
        { "Decepticleprime", "デセプティクルプライム" },
        { "Decepticlechassis", "デセプティクルシャーシ" },
        { "Wolfpunch", "ウルフパンチ" },
        { "Shadowlord", "シャドウロード" },
        { "Tier2general", "ティアツージェネラル" },
        { "Snowchasermk", "スノーチェイサー" },
        { "Solfist", "ソルフィスト" },
        { "Maehvmk", "メーヴ" }
    };

    private static readonly Dictionary<string, string> ItemNameJa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "iron_bulwark", "鉄壁の大盾" },
        { "frostguard_plate", "霜守の装甲" },
        { "eternal_heart", "永久の心臓" },
        { "iridium_scale", "イリジウムスケイル" },
        { "phalanx_aegis", "ファランクスの盾" },
        { "spine_cleaver", "脊断ちの斧" },
        { "skywind_glaives", "天風の双刃" },
        { "godhammer", "神槌" },
        { "adamantine_claws", "アダマンティンクロー" },
        { "rage_chakram", "怒りのチャクラム" },
        { "unbounded_amulet", "無限の護符" },
        { "ykir_staff", "イキルの杖" },
        { "thunderclap_scepter", "雷鳴の王笏" },
        { "repair_staff", "修復の杖" },
        { "darkstone_ring", "暗黒石の指輪" }
    };

    public static GameLanguage CurrentLanguage => EnsureExists().currentLanguage;
    public static bool IsJapanese => CurrentLanguage == GameLanguage.Japanese;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static LocalizationManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        LocalizationManager existing = FindObjectOfType<LocalizationManager>(true);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject managerObject = new GameObject("LocalizationManager", typeof(LocalizationManager));
        DontDestroyOnLoad(managerObject);
        Instance = managerObject.GetComponent<LocalizationManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        currentLanguage = PlayerPrefs.GetString("GameLanguage", "ja") == "en" ? GameLanguage.English : GameLanguage.Japanese;
        BuildOptionUi();
        StartCoroutine(ApplyAfterSceneIsReady());
    }

    private IEnumerator ApplyAfterSceneIsReady()
    {
        yield return null;
        ApplyStaticTextTranslations();
        OnLanguageChanged?.Invoke();
    }

    private void BuildOptionUi()
    {
        GameObject canvasObject = new GameObject("LocalizationOptionsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        Button optionButton = CreateButton("OptionButton", canvasObject.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(126f, 34f), out optionButtonText);
        optionButton.onClick.AddListener(ToggleOptionPanel);

        GameObject panelObject = new GameObject("OptionPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        optionPanel = panelObject.GetComponent<RectTransform>();
        optionPanel.anchorMin = new Vector2(1f, 1f);
        optionPanel.anchorMax = new Vector2(1f, 1f);
        optionPanel.pivot = new Vector2(1f, 1f);
        optionPanel.anchoredPosition = new Vector2(-12f, -52f);
        optionPanel.sizeDelta = new Vector2(180f, 54f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.01f, 0.03f, 0.05f, 0.94f);
        panelImage.raycastTarget = true;

        Button languageButton = CreateButton("LanguageButton", panelObject.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(156f, 34f), out languageButtonText);
        languageButton.onClick.AddListener(ToggleLanguage);
        optionPanel.gameObject.SetActive(false);
        RefreshOptionTexts();
    }

    private Button CreateButton(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, out TextMeshProUGUI label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.02f, 0.11f, 0.17f, 0.92f);

        Button button = buttonObject.GetComponent<Button>();

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);

        label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.9f, 1f, 1f, 1f);
        label.raycastTarget = false;
        ApplyFont(label);
        return button;
    }

    private void ToggleOptionPanel()
    {
        if (optionPanel == null)
            return;

        optionPanel.gameObject.SetActive(!optionPanel.gameObject.activeSelf);
    }

    private void ToggleLanguage()
    {
        currentLanguage = currentLanguage == GameLanguage.Japanese ? GameLanguage.English : GameLanguage.Japanese;
        PlayerPrefs.SetString("GameLanguage", currentLanguage == GameLanguage.Japanese ? "ja" : "en");
        PlayerPrefs.Save();

        RefreshOptionTexts();
        ApplyStaticTextTranslations();
        OnLanguageChanged?.Invoke();
    }

    private void RefreshOptionTexts()
    {
        if (optionButtonText != null)
            optionButtonText.text = currentLanguage == GameLanguage.Japanese ? "オプション" : "Options";

        if (languageButtonText != null)
            languageButtonText.text = currentLanguage == GameLanguage.Japanese ? "言語: 日本語" : "Language: English";

        ApplyFont(optionButtonText);
        ApplyFont(languageButtonText);
    }

    public static void ApplyFont(TMP_Text text)
    {
        if (text == null)
            return;

        TMP_FontAsset font = GetJapaneseFont();
        if (font != null)
            text.font = font;
    }

    public static void ApplyStaticTextTranslations()
    {
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            ApplyFont(text);
            if (ShouldSkipStaticTranslation(text))
                continue;

            string translated = TranslateStaticText(text.text);
            if (!string.IsNullOrEmpty(translated))
                text.text = translated;
        }
    }

    private static bool ShouldSkipStaticTranslation(TMP_Text text)
    {
        string value = text.text ?? string.Empty;
        if (value.Contains("Exp") || value.Contains("EXP") || value.Contains("FIGHT"))
            return true;

        string objectName = text.gameObject.name ?? string.Empty;
        return objectName.IndexOf("Exp", StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Fight", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TranslateStaticText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string trimmed = value.Trim();
        if (IsJapanese)
        {
            if (Regex.IsMatch(trimmed, @"^Lv\s*(\d+)$"))
                return Regex.Replace(trimmed, @"^Lv\s*(\d+)$", "レベル $1");
            if (Regex.IsMatch(trimmed, @"^SELL\s*\+(\d+)$"))
                return Regex.Replace(trimmed, @"^SELL\s*\+(\d+)$", "売却 +$1");

            switch (trimmed)
            {
                case "Reroll":
                case "更新":
                    return "リロール";
                case "MAX":
                    return "最大";
                case "STAR 2":
                    return "★2";
                case "STAR 3":
                    return "★3";
                default:
                    return null;
            }
        }

        if (Regex.IsMatch(trimmed, @"^レベル\s*(\d+)$"))
            return Regex.Replace(trimmed, @"^レベル\s*(\d+)$", "Lv $1");
        if (Regex.IsMatch(trimmed, @"^売却\s*\+(\d+)$"))
            return Regex.Replace(trimmed, @"^売却\s*\+(\d+)$", "SELL +$1");

        switch (trimmed)
        {
            case "更新":
            case "リロール":
                return "Reroll";
            case "最大":
                return "MAX";
            case "★2":
                return "STAR 2";
            case "★3":
                return "STAR 3";
            default:
                return null;
        }
    }

    public static string FormatLevel(int level)
    {
        return IsJapanese ? $"レベル {level}" : $"Lv {level}";
    }

    public static string FormatSellValue(int value)
    {
        return IsJapanese ? $"売却 +{value}" : $"SELL +{value}";
    }

    public static string FormatUpgradeLabel(int starLevel)
    {
        if (IsJapanese)
            return starLevel >= 3 ? "★3" : "★2";

        return starLevel >= 3 ? "STAR 3" : "STAR 2";
    }

    public static string UnitName(string rawName)
    {
        string cleanName = CleanUnitName(rawName);
        if (!IsJapanese)
            return cleanName;

        return UnitNameJa.TryGetValue(cleanName, out string localizedName) ? localizedName : cleanName;
    }

    public static string CleanUnitName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
            return IsJapanese ? "ユニット" : "Unit";

        return rawName
            .Replace("(Clone)", string.Empty)
            .Replace("Star1", string.Empty)
            .Replace("Star2", string.Empty)
            .Replace("Star3", string.Empty)
            .Trim();
    }

    public static string ItemName(ItemData itemData)
    {
        if (itemData == null)
            return IsJapanese ? "アイテム" : "Item";

        if (!IsJapanese)
            return itemData.displayName;

        return ItemNameJa.TryGetValue(itemData.id, out string localizedName) ? localizedName : itemData.displayName;
    }

    public static string ItemCategoryLabel(ItemCategory category)
    {
        if (IsJapanese)
        {
            switch (category)
            {
                case ItemCategory.Defense:
                    return "防御アイテム";
                case ItemCategory.Offense:
                    return "攻撃アイテム";
                case ItemCategory.Skill:
                    return "秘力 / マナアイテム";
                default:
                    return "アイテム";
            }
        }

        switch (category)
        {
            case ItemCategory.Defense:
                return "DEFENSE ITEM";
            case ItemCategory.Offense:
                return "OFFENSE ITEM";
            case ItemCategory.Skill:
                return "FOCUS / MANA ITEM";
            default:
                return "ITEM";
        }
    }

    public static string BuildItemEffectText(ItemData itemData, bool inline)
    {
        if (itemData == null)
            return string.Empty;

        List<string> lines = new List<string>();
        AddLine(lines, itemData.healthFlat != 0, IsJapanese ? $"HP +{itemData.healthFlat}" : $"HP +{itemData.healthFlat}");
        AddLine(lines, itemData.healthPercent > 0f, IsJapanese ? $"最大HP +{FormatPercent(itemData.healthPercent)}" : $"Max HP +{FormatPercent(itemData.healthPercent)}");
        AddLine(lines, itemData.damageReductionPercent > 0f, IsJapanese ? $"被ダメージ -{FormatPercent(itemData.damageReductionPercent)}" : $"Damage Taken -{FormatPercent(itemData.damageReductionPercent)}");
        AddLine(lines, itemData.damageFlat != 0, IsJapanese ? $"攻撃力 +{itemData.damageFlat}" : $"Attack +{itemData.damageFlat}");
        AddLine(lines, itemData.damagePercent > 0f, IsJapanese ? $"攻撃力 +{FormatPercent(itemData.damagePercent)}" : $"Attack +{FormatPercent(itemData.damagePercent)}");
        AddLine(lines, itemData.attackSpeedPercent > 0f, IsJapanese ? $"攻撃速度 +{FormatPercent(itemData.attackSpeedPercent)}" : $"Attack Speed +{FormatPercent(itemData.attackSpeedPercent)}");
        AddLine(lines, itemData.skillPowerPercent > 0f, IsJapanese ? $"秘力 +{FormatPercent(itemData.skillPowerPercent)}" : $"Focus +{FormatPercent(itemData.skillPowerPercent)}");
        AddLine(lines, itemData.manaOnAttackBonus != 0, IsJapanese ? $"攻撃時マナ +{itemData.manaOnAttackBonus}" : $"Mana on Attack +{itemData.manaOnAttackBonus}");
        AddLine(lines, itemData.manaOnDamageTakenBonus != 0, IsJapanese ? $"被弾時マナ +{itemData.manaOnDamageTakenBonus}" : $"Mana on Hit +{itemData.manaOnDamageTakenBonus}");
        AddLine(lines, itemData.maxManaReduction != 0, IsJapanese ? $"必要マナ -{itemData.maxManaReduction}" : $"Max Mana -{itemData.maxManaReduction}");

        if (lines.Count == 0)
            return itemData.description;

        return string.Join(inline ? "、" : "\n", lines);
    }

    private static void AddLine(List<string> lines, bool condition, string line)
    {
        if (condition)
            lines.Add(line);
    }

    public static string SkillName(UnitSkillType skillType)
    {
        if (IsJapanese)
        {
            switch (skillType)
            {
                case UnitSkillType.SelfHeal:
                    return "自己回復";
                case UnitSkillType.AllyHeal:
                    return "味方回復";
                case UnitSkillType.Shield:
                    return "シールド";
                case UnitSkillType.AttackSpeedBoost:
                    return "加速";
                case UnitSkillType.Stun:
                    return "スタン";
                case UnitSkillType.Slow:
                    return "スロウ";
                case UnitSkillType.DamageBoost:
                    return "攻撃強化";
                case UnitSkillType.AreaDamage:
                    return "範囲攻撃";
                default:
                    return "強撃";
            }
        }

        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
                return "Self Heal";
            case UnitSkillType.AllyHeal:
                return "Ally Heal";
            case UnitSkillType.Shield:
                return "Shield";
            case UnitSkillType.AttackSpeedBoost:
                return "Haste";
            case UnitSkillType.Stun:
                return "Stun";
            case UnitSkillType.Slow:
                return "Slow";
            case UnitSkillType.DamageBoost:
                return "Damage Boost";
            case UnitSkillType.AreaDamage:
                return "Area Damage";
            default:
                return "Power Strike";
        }
    }

    public static string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(Mathf.Max(0f, value) * 100f)}%";
    }

    private static TMP_FontAsset GetJapaneseFont()
    {
        if (cachedJapaneseFont != null)
            return cachedJapaneseFont;

        cachedJapaneseFont = CreateDynamicJapaneseFontAsset();
#if UNITY_EDITOR
        if (cachedJapaneseFont == null)
            cachedJapaneseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/BIZUDPGothic-Bold SDF.asset");
#endif
        return cachedJapaneseFont;
    }

    // 既存のBIZUDPGothic-Bold SDF.assetは固定アトラスで日本語グリフが不足しているため、
    // 元TTFから動的なTMPフォントを作り、表示した文字を必要に応じて追加できるようにします。
    private static TMP_FontAsset CreateDynamicJapaneseFontAsset()
    {
        Font sourceFont = Resources.Load<Font>("Fonts/BIZUDPGothic-Bold");
#if UNITY_EDITOR
        if (sourceFont == null)
            sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/BIZUDPGothic-Bold.ttf");
#endif
        if (sourceFont == null)
            return null;

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            4096,
            4096,
            AtlasPopulationMode.Dynamic,
            true);

        fontAsset.name = "BIZUDPGothic-Bold Dynamic SDF";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        return fontAsset;
    }
}
