using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// アイテムの大まかな役割です。
// ショップや報酬UIでカテゴリ別に並べたい時にも使えるようにしています。
public enum ItemCategory
{
    Defense,
    Offense,
    Skill
}

// 1つのアイテムが持つ効果量をまとめたデータです。
// ScriptableObjectではなく通常クラスにして、まずはコード側のカタログから手早く増やせる形にしています。
[System.Serializable]
public class ItemData
{
    public string id;
    public string displayName;
    [TextArea]
    public string description;
    public ItemCategory category;
    public string iconResourcePath;

    public int healthFlat;
    public float healthPercent;
    public float damageReductionPercent;

    public int damageFlat;
    public float damagePercent;
    public float attackSpeedPercent;

    public float skillPowerPercent;
    public int manaOnAttackBonus;
    public int manaOnDamageTakenBonus;
    public int maxManaReduction;

    private Sprite cachedIcon;
    private Sprite[] cachedIconFrames;

    // plistで分割されたアイコンフレームをResourcesから読み込みます。
    // Editor側でスライス済みなら複数Sprite、未スライスなら最後の保険として画像全体を1枚Spriteにします。
    public IReadOnlyList<Sprite> IconFrames
    {
        get
        {
            if (cachedIconFrames != null)
                return cachedIconFrames;

            cachedIconFrames = LoadIconFrames();
            cachedIcon = cachedIconFrames.Length > 0 ? cachedIconFrames[0] : null;
            return cachedIconFrames;
        }
    }

    // アイテム一覧やHPバーに表示する代表アイコンです。
    // 分割済みスプライトシートの場合は、先頭フレームだけを使います。
    public Sprite Icon
    {
        get
        {
            if (cachedIcon != null)
                return cachedIcon;

            IReadOnlyList<Sprite> frames = IconFrames;
            return frames.Count > 0 ? frames[0] : null;
        }
    }

    // Resources.LoadAllで分割済みSpriteを読み、フレーム番号順へ並べます。
    private Sprite[] LoadIconFrames()
    {
        if (string.IsNullOrEmpty(iconResourcePath))
            return new Sprite[0];

        Sprite[] slicedSprites = Resources.LoadAll<Sprite>(iconResourcePath);
        if (slicedSprites != null && slicedSprites.Length > 0)
        {
            Sprite[] sortedSprites = slicedSprites
                .Where(sprite => sprite != null)
                .OrderBy(sprite => GetFrameGroupPriority(sprite.name))
                .ThenBy(sprite => ExtractTrailingNumber(sprite.name))
                .ThenBy(sprite => sprite.name)
                .ToArray();

            // activeフレームは選択中演出用に入っていることが多いので、通常表示では基本フレームだけ使います。
            Sprite[] defaultSprites = sortedSprites
                .Where(sprite => !IsActiveFrame(sprite.name))
                .ToArray();

            return defaultSprites.Length > 0 ? defaultSprites : sortedSprites;
        }

        Sprite singleSprite = Resources.Load<Sprite>(iconResourcePath);
        if (singleSprite != null)
            return new[] { singleSprite };

        Texture2D texture = Resources.Load<Texture2D>(iconResourcePath);
        if (texture == null)
            return new Sprite[0];

        Sprite generatedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        generatedSprite.name = displayName;
        return new[] { generatedSprite };
    }

    // "artifact_xxx_012" のような名前から末尾番号を取り出し、自然なアニメ順にします。
    private static int ExtractTrailingNumber(string value)
    {
        Match match = Regex.Match(value ?? string.Empty, @"_(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    // active用アイコンフレームかどうかを見分けます。
    private static bool IsActiveFrame(string value)
    {
        return (value ?? string.Empty).Contains("_active_");
    }

    // 通常フレームを先、activeフレームを後にして、用途の違うフレームが交互に混ざらないようにします。
    private static int GetFrameGroupPriority(string value)
    {
        return IsActiveFrame(value) ? 1 : 0;
    }

    // HPを増やすアイテムを作るための補助関数です。
    public static ItemData Defense(
        string id,
        string displayName,
        string description,
        string iconResourcePath,
        int healthFlat,
        float healthPercent,
        float damageReductionPercent)
    {
        return new ItemData
        {
            id = id,
            displayName = displayName,
            description = description,
            category = ItemCategory.Defense,
            iconResourcePath = iconResourcePath,
            healthFlat = healthFlat,
            healthPercent = healthPercent,
            damageReductionPercent = damageReductionPercent
        };
    }

    // 攻撃力や攻撃速度を増やすアイテムを作るための補助関数です。
    public static ItemData Offense(
        string id,
        string displayName,
        string description,
        string iconResourcePath,
        int damageFlat,
        float damagePercent,
        float attackSpeedPercent)
    {
        return new ItemData
        {
            id = id,
            displayName = displayName,
            description = description,
            category = ItemCategory.Offense,
            iconResourcePath = iconResourcePath,
            damageFlat = damageFlat,
            damagePercent = damagePercent,
            attackSpeedPercent = attackSpeedPercent
        };
    }

    // 秘力やマナ獲得量を増やすアイテムを作るための補助関数です。
    public static ItemData Skill(
        string id,
        string displayName,
        string description,
        string iconResourcePath,
        float skillPowerPercent,
        int manaOnAttackBonus,
        int manaOnDamageTakenBonus,
        int maxManaReduction)
    {
        return new ItemData
        {
            id = id,
            displayName = displayName,
            description = description,
            category = ItemCategory.Skill,
            iconResourcePath = iconResourcePath,
            skillPowerPercent = skillPowerPercent,
            manaOnAttackBonus = manaOnAttackBonus,
            manaOnDamageTakenBonus = manaOnDamageTakenBonus,
            maxManaReduction = maxManaReduction
        };
    }
}
