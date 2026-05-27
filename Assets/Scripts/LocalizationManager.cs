using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        { "Maehvmk", "メーヴ" },
        { "Archdeacon", "アークディーコン" },
        { "Backlinearcher", "バックラインアーチャー" },
        { "Auroralioness", "オーロラライオネス" },
        { "Azuritelion", "アズライトライオン" },
        { "Sandpanther", "サンドパンサー" },
        { "Protector", "プロテクター" },
        { "Taskmaster", "タスクマスター" },
        { "Kane", "ケイン" },
        { "Malyk", "マリック" },
        { "Paragon", "パラゴン" },
        { "Wujin", "ウージン" },
        { "Wraith", "レイス" },
        { "Altgeneraltier2", "アルトジェネラル" },
        { "Ilenamk2", "イレーナ" },
        { "Embergeneral", "エンバージェネラル" },
        { "Plaguegeneral", "プレイグジェネラル" },
        { "Skyfalltyrant", "スカイフォールタイラント" },
        { "Kron", "クロン" },
        { "Gol", "ゴル" },
        { "Invader", "インベーダー" },
        { "Legion", "レギオン" }
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

    public static string SynergyName(SynergyType type)
    {
        if (IsJapanese)
        {
            switch (type)
            {
                case SynergyType.Warrior:
                    return "戦士";
                case SynergyType.Ranger:
                    return "射手";
                case SynergyType.Arcanist:
                    return "魔導";
                case SynergyType.Guardian:
                    return "守護";
                case SynergyType.Beast:
                    return "獣";
                case SynergyType.Shadow:
                    return "影";
                case SynergyType.Machine:
                    return "機械";
                case SynergyType.Wraith:
                    return "亡霊";
                case SynergyType.Apex:
                    return "覇者";
                case SynergyType.Inferno:
                    return "炎獄";
                case SynergyType.Frost:
                    return "氷晶";
                case SynergyType.Storm:
                    return "雷鳴";
                case SynergyType.Abyss:
                    return "深淵";
                case SynergyType.Divine:
                    return "神聖";
                case SynergyType.Frenzy:
                    return "狂乱";
                case SynergyType.Royal:
                    return "王族";
                case SynergyType.Summoner:
                    return "召喚";
                case SynergyType.Alchemy:
                    return "錬金";
                default:
                    return "なし";
            }
        }

        switch (type)
        {
            case SynergyType.Warrior:
                return "Warrior";
            case SynergyType.Ranger:
                return "Ranger";
            case SynergyType.Arcanist:
                return "Arcanist";
            case SynergyType.Guardian:
                return "Guardian";
            case SynergyType.Beast:
                return "Beast";
            case SynergyType.Shadow:
                return "Shadow";
            case SynergyType.Machine:
                return "Machine";
            case SynergyType.Wraith:
                return "Wraith";
            case SynergyType.Apex:
                return "Apex";
            case SynergyType.Inferno:
                return "Inferno";
            case SynergyType.Frost:
                return "Frost";
            case SynergyType.Storm:
                return "Storm";
            case SynergyType.Abyss:
                return "Abyss";
            case SynergyType.Divine:
                return "Divine";
            case SynergyType.Frenzy:
                return "Frenzy";
            case SynergyType.Royal:
                return "Royal";
            case SynergyType.Summoner:
                return "Summoner";
            case SynergyType.Alchemy:
                return "Alchemy";
            default:
                return "None";
        }
    }

    public static string FormatSynergyList(BaseEntity entity)
    {
        if (entity == null)
            return IsJapanese ? "シナジー: なし" : "Synergy: None";

        List<SynergyType> synergies = entity.GetSynergyTypes();
        if (synergies.Count == 0)
            return IsJapanese ? "シナジー: なし" : "Synergy: None";

        List<string> names = synergies.Select(SynergyName).ToList();
        return IsJapanese
            ? $"シナジー: {string.Join(" / ", names)}"
            : $"Synergy: {string.Join(" / ", names)}";
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
        AddLine(lines, !string.IsNullOrEmpty(GetItemSpecialEffectText(itemData)), GetItemSpecialEffectText(itemData));

        if (lines.Count == 0)
            return itemData.description;

        return string.Join(inline ? "、" : "\n", lines);
    }

    public static string GetItemSpecialEffectText(ItemData itemData)
    {
        if (itemData == null || string.IsNullOrEmpty(itemData.id))
            return string.Empty;

        if (IsJapanese)
        {
            switch (itemData.id)
            {
                case "iron_bulwark":
                    return "戦闘開始時、最大HP12%のシールドを8秒得る";
                case "frostguard_plate":
                    return "被弾時、攻撃者の攻撃速度を2秒間18%下げる";
                case "eternal_heart":
                    return "戦闘中、3秒ごとに最大HP4%を回復する";
                case "iridium_scale":
                    return "受ける回復量とシールド量が20%増える";
                case "phalanx_aegis":
                    return "周囲1マスの味方の被ダメージを6%下げる";
                case "spine_cleaver":
                    return "通常攻撃時、対象の軽減を3秒間4%下げる。最大3スタック";
                case "skywind_glaives":
                    return "通常攻撃4回ごとに、別の近い敵へ攻撃力60%の風刃を飛ばす";
                case "godhammer":
                    return "5回目の通常攻撃で、対象を0.8秒スタンさせる";
                case "adamantine_claws":
                    return "同じ敵を攻撃し続けるたび与ダメージ+4%。最大5スタック";
                case "rage_chakram":
                    return "敵を倒すたび4秒間攻撃速度+20%。最大3スタック";
                case "unbounded_amulet":
                    return "スキル発動後5秒間、通常攻撃の獲得マナ+6";
                case "ykir_staff":
                    return "戦闘中の初回スキル効果量+30%";
                case "thunderclap_scepter":
                    return "被弾4回ごとに、周囲の敵へ秘力依存の小雷撃を放つ";
                case "repair_staff":
                    return "スキル発動時、HP割合が最も低い味方1体を追加回復する";
                case "darkstone_ring":
                    return "戦闘開始時、MPを15得る";
                default:
                    return string.Empty;
            }
        }

        switch (itemData.id)
        {
            case "iron_bulwark":
                return "Combat start: gain an 8s shield equal to 12% max HP";
            case "frostguard_plate":
                return "When hit, slow the attacker's attack speed by 18% for 2s";
            case "eternal_heart":
                return "During combat, heal 4% max HP every 3s";
            case "iridium_scale":
                return "Incoming healing and shields are increased by 20%";
            case "phalanx_aegis":
                return "Allies within 1 hex take 6% less damage";
            case "spine_cleaver":
                return "Basic attacks reduce the target's reduction by 4% for 3s. Stacks 3 times";
            case "skywind_glaives":
                return "Every 4th basic attack fires a wind blade at another nearby enemy for 60% attack damage";
            case "godhammer":
                return "Every 5th basic attack stuns the target for 0.8s";
            case "adamantine_claws":
                return "Repeated attacks on the same target deal +4% damage. Stacks 5 times";
            case "rage_chakram":
                return "On takedown, gain +20% attack speed for 4s. Stacks 3 times";
            case "unbounded_amulet":
                return "After casting, basic attacks grant +6 extra mana for 5s";
            case "ykir_staff":
                return "The first skill each combat has +30% effect";
            case "thunderclap_scepter":
                return "Every 4 hits taken releases a focus-scaling thunder pulse nearby";
            case "repair_staff":
                return "On skill cast, additionally heal the lowest-health ally";
            case "darkstone_ring":
                return "Combat start: gain 15 MP";
            default:
                return string.Empty;
        }
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
