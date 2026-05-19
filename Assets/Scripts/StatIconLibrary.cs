using System.Collections.Generic;
using UnityEngine;

// ステータス種別ごとの小アイコンをResourcesから読み込み、UIで共通利用するためのクラスです。
// PNGをSpriteとして再生成するので、Texture Importerを手でSprite設定しなくても表示できます。
public enum StatIconKind
{
    AttackPower,
    Health,
    AttackSpeed,
    DamageReduction,
    Focus
}

// 1行分の「アイコン + 効果テキスト」をまとめた軽いデータです。
public readonly struct StatIconLine
{
    public readonly StatIconKind iconKind;
    public readonly string text;

    public StatIconLine(StatIconKind iconKind, string text)
    {
        this.iconKind = iconKind;
        this.text = text;
    }
}

public static class StatIconLibrary
{
    private const string IconRoot = "UI/StatIcons/";
    private static readonly Dictionary<StatIconKind, Sprite> SpriteCache = new Dictionary<StatIconKind, Sprite>();

    // 指定したステータスのSpriteを返します。初回だけResourcesから読み込み、以降はキャッシュします。
    public static Sprite GetSprite(StatIconKind iconKind)
    {
        if (SpriteCache.TryGetValue(iconKind, out Sprite cachedSprite))
            return cachedSprite;

        Texture2D texture = Resources.Load<Texture2D>(GetResourcePath(iconKind));
        if (texture == null)
            return null;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        sprite.name = iconKind.ToString();
        SpriteCache[iconKind] = sprite;
        return sprite;
    }

    // パネル表示用に、ステータス名を現在言語へ変換します。
    public static string GetLabel(StatIconKind iconKind)
    {
        if (LocalizationManager.IsJapanese)
        {
            switch (iconKind)
            {
                case StatIconKind.AttackPower:
                    return "攻撃力";
                case StatIconKind.Health:
                    return "体力";
                case StatIconKind.AttackSpeed:
                    return "攻撃速度";
                case StatIconKind.DamageReduction:
                    return "軽減";
                case StatIconKind.Focus:
                    return "秘力";
                default:
                    return "ステータス";
            }
        }

        switch (iconKind)
        {
            case StatIconKind.AttackPower:
                return "Attack";
            case StatIconKind.Health:
                return "Health";
            case StatIconKind.AttackSpeed:
                return "Atk Speed";
            case StatIconKind.DamageReduction:
                return "Reduction";
            case StatIconKind.Focus:
                return "Focus";
            default:
                return "Stat";
        }
    }

    // アイテムが持つ数値効果を、アイコン付きで並べられる行データへ変換します。
    public static List<StatIconLine> BuildItemEffectLines(ItemData itemData)
    {
        List<StatIconLine> lines = new List<StatIconLine>();
        if (itemData == null)
            return lines;

        AddLine(lines, itemData.healthFlat != 0, StatIconKind.Health, $"{GetLabel(StatIconKind.Health)} +{itemData.healthFlat}");
        AddLine(lines, itemData.healthPercent > 0f, StatIconKind.Health, $"{GetLabel(StatIconKind.Health)} +{LocalizationManager.FormatPercent(itemData.healthPercent)}");
        AddLine(lines, itemData.damageReductionPercent > 0f, StatIconKind.DamageReduction, $"{GetLabel(StatIconKind.DamageReduction)} +{LocalizationManager.FormatPercent(itemData.damageReductionPercent)}");
        AddLine(lines, itemData.damageFlat != 0, StatIconKind.AttackPower, $"{GetLabel(StatIconKind.AttackPower)} +{itemData.damageFlat}");
        AddLine(lines, itemData.damagePercent > 0f, StatIconKind.AttackPower, $"{GetLabel(StatIconKind.AttackPower)} +{LocalizationManager.FormatPercent(itemData.damagePercent)}");
        AddLine(lines, itemData.attackSpeedPercent > 0f, StatIconKind.AttackSpeed, $"{GetLabel(StatIconKind.AttackSpeed)} +{LocalizationManager.FormatPercent(itemData.attackSpeedPercent)}");
        AddLine(lines, itemData.skillPowerPercent > 0f, StatIconKind.Focus, $"{GetLabel(StatIconKind.Focus)} +{LocalizationManager.FormatPercent(itemData.skillPowerPercent)}");
        AddLine(lines, itemData.manaOnAttackBonus != 0, StatIconKind.Focus, LocalizationManager.IsJapanese ? $"攻撃時マナ +{itemData.manaOnAttackBonus}" : $"Mana on Attack +{itemData.manaOnAttackBonus}");
        AddLine(lines, itemData.manaOnDamageTakenBonus != 0, StatIconKind.Focus, LocalizationManager.IsJapanese ? $"被弾時マナ +{itemData.manaOnDamageTakenBonus}" : $"Mana on Hit +{itemData.manaOnDamageTakenBonus}");
        AddLine(lines, itemData.maxManaReduction != 0, StatIconKind.Focus, LocalizationManager.IsJapanese ? $"必要マナ -{itemData.maxManaReduction}" : $"Max Mana -{itemData.maxManaReduction}");
        return lines;
    }

    // スキル説明欄に出す「このスキルが参照する主な値」を返します。
    public static List<StatIconKind> GetSkillScalingIcons(UnitSkillType skillType)
    {
        List<StatIconKind> icons = new List<StatIconKind>();
        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.AllyHeal:
            case UnitSkillType.Shield:
                icons.Add(StatIconKind.Health);
                icons.Add(StatIconKind.Focus);
                break;
            case UnitSkillType.AttackSpeedBoost:
            case UnitSkillType.Slow:
                icons.Add(StatIconKind.AttackSpeed);
                icons.Add(StatIconKind.Focus);
                break;
            case UnitSkillType.Stun:
                icons.Add(StatIconKind.AttackPower);
                break;
            case UnitSkillType.DamageBoost:
            case UnitSkillType.AreaDamage:
            case UnitSkillType.PowerStrike:
            default:
                icons.Add(StatIconKind.AttackPower);
                icons.Add(StatIconKind.Focus);
                break;
        }

        return icons;
    }

    // 指定条件がtrueの時だけ、効果行を追加します。
    private static void AddLine(List<StatIconLine> lines, bool condition, StatIconKind iconKind, string text)
    {
        if (condition)
            lines.Add(new StatIconLine(iconKind, text));
    }

    // Resources.Loadで使う拡張子なしのパスを返します。
    private static string GetResourcePath(StatIconKind iconKind)
    {
        switch (iconKind)
        {
            case StatIconKind.AttackPower:
                return IconRoot + "attack_power_red_sword_80";
            case StatIconKind.Health:
                return IconRoot + "health_green_heart_80";
            case StatIconKind.AttackSpeed:
                return IconRoot + "attack_speed_arrow_80";
            case StatIconKind.DamageReduction:
                return IconRoot + "damage_reduction_shield_80";
            case StatIconKind.Focus:
                return IconRoot + "hiryoku_magic_80";
            default:
                return string.Empty;
        }
    }
}
