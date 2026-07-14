using System.Collections.Generic;
using UnityEngine;

// シナジーアイコンをResourcesから読み込み、ショップやパネルで共通利用するためのクラスです。
// PNGをSpriteとして作るので、Unity側でSprite設定が漏れていても表示できます。
public static class SynergyIconLibrary
{
    private const string IconRoot = "SynergyIcons/";
    private static readonly Dictionary<SynergyType, Sprite> SpriteCache = new Dictionary<SynergyType, Sprite>();

    // 指定したシナジーのアイコンSpriteを返します。初回だけ読み込み、以降はキャッシュします。
    public static Sprite GetSprite(SynergyType type)
    {
        if (type == SynergyType.None)
            return null;

        if (SpriteCache.TryGetValue(type, out Sprite cachedSprite))
            return cachedSprite;

        Texture2D texture = Resources.Load<Texture2D>(GetResourcePath(type));
        if (texture == null)
            return null;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        sprite.name = type.ToString();
        SpriteCache[type] = sprite;
        return sprite;
    }

    // シナジーごとの識別色です。白いルーン画像にこの色を乗せて使います。
    public static Color GetColor(SynergyType type)
    {
        switch (type)
        {
            case SynergyType.Warrior:
                return new Color(1f, 0.32f, 0.18f, 1f);
            case SynergyType.Ranger:
                return new Color(0.58f, 1f, 0.35f, 1f);
            case SynergyType.Arcanist:
                return new Color(0.45f, 0.78f, 1f, 1f);
            case SynergyType.Guardian:
                return new Color(1f, 0.82f, 0.25f, 1f);
            case SynergyType.Beast:
                return new Color(1f, 0.55f, 0.18f, 1f);
            case SynergyType.Shadow:
                return new Color(0.74f, 0.35f, 1f, 1f);
            case SynergyType.Machine:
                return new Color(0.34f, 1f, 0.92f, 1f);
            case SynergyType.Wraith:
                return new Color(0.9f, 0.9f, 1f, 1f);
            case SynergyType.Apex:
                return new Color(1f, 0.67f, 0.16f, 1f);
            case SynergyType.Inferno:
                return new Color(1f, 0.18f, 0.04f, 1f);
            case SynergyType.Frost:
                return new Color(0.35f, 0.88f, 1f, 1f);
            case SynergyType.Storm:
                return new Color(0.25f, 0.78f, 1f, 1f);
            case SynergyType.Abyss:
                return new Color(0.55f, 0.16f, 0.95f, 1f);
            case SynergyType.Divine:
                return new Color(1f, 0.95f, 0.42f, 1f);
            case SynergyType.Frenzy:
                return new Color(1f, 0.22f, 0.34f, 1f);
            case SynergyType.Royal:
                return new Color(1f, 0.73f, 0.24f, 1f);
            case SynergyType.Summoner:
                return new Color(0.35f, 1f, 0.62f, 1f);
            case SynergyType.Alchemy:
                return new Color(0.55f, 1f, 0.25f, 1f);
            case SynergyType.Finality:
                return new Color(0.85f, 0.1f, 0.18f, 1f); // 血の深紅
            case SynergyType.Lyonar:
                return new Color(1f, 0.85f, 0.3f, 1f);
            case SynergyType.Songhai:
                return new Color(1f, 0.35f, 0.3f, 1f);
            case SynergyType.Magmar:
                return new Color(1f, 0.6f, 0.2f, 1f);
            case SynergyType.Vetruvian:
                return new Color(0.95f, 0.78f, 0.35f, 1f);
            case SynergyType.Abyssian:
                return new Color(0.7f, 0.3f, 0.9f, 1f);
            case SynergyType.Vanar:
                return new Color(0.5f, 0.85f, 1f, 1f);
            default:
                return Color.white;
        }
    }

    // ショップカード内で使う短い表示名です。日本語は2文字、英語は3文字程度に抑えます。
    public static string GetShortLabel(SynergyType type)
    {
        if (LocalizationManager.IsJapanese)
            return LocalizationManager.SynergyName(type);

        switch (type)
        {
            case SynergyType.Warrior:
                return "WAR";
            case SynergyType.Ranger:
                return "RNG";
            case SynergyType.Arcanist:
                return "ARC";
            case SynergyType.Guardian:
                return "GUA";
            case SynergyType.Beast:
                return "BST";
            case SynergyType.Shadow:
                return "SHD";
            case SynergyType.Machine:
                return "MCH";
            case SynergyType.Wraith:
                return "WRA";
            case SynergyType.Apex:
                return "APX";
            case SynergyType.Inferno:
                return "INF";
            case SynergyType.Frost:
                return "FRS";
            case SynergyType.Storm:
                return "STM";
            case SynergyType.Abyss:
                return "ABY";
            case SynergyType.Divine:
                return "DIV";
            case SynergyType.Frenzy:
                return "FRZ";
            case SynergyType.Royal:
                return "RYL";
            case SynergyType.Summoner:
                return "SUM";
            case SynergyType.Alchemy:
                return "ALC";
            case SynergyType.Finality:
                return "FIN";
            case SynergyType.Lyonar:
                return "LYO";
            case SynergyType.Songhai:
                return "SON";
            case SynergyType.Magmar:
                return "MAG";
            case SynergyType.Vetruvian:
                return "VET";
            case SynergyType.Abyssian:
                return "ABS";
            case SynergyType.Vanar:
                return "VAN";
            default:
                return string.Empty;
        }
    }

    // Resources.Loadで使う拡張子なしパスを返します。
    // 規約: 各シナジーは Resources/SynergyIcons/<シナジー名を小文字>.png を1枚ずつ持つ。
    // 新シナジー追加時は同名PNGを SynergyIcons に入れるだけで自動表示される
    // （アイコンは reference/duelyst/app/resources/runes のルーンをスライスして割り当てる）。
    private static string GetResourcePath(SynergyType type)
    {
        if (type == SynergyType.None)
            return string.Empty;
        return IconRoot + type.ToString().ToLowerInvariant();
    }
}
