using System.Collections.Generic;
using System.Linq;

// ゲーム内で使えるアイテム一覧です。
// まずは固定15種類をここに置き、後で報酬テーブルやレアリティを足しやすい形にしています。
public static class ItemCatalog
{
    private static List<ItemData> allItems;

    // 全アイテムを返します。初回だけ一覧を作ります。
    public static IReadOnlyList<ItemData> AllItems
    {
        get
        {
            if (allItems == null)
                allItems = BuildCatalog();

            return allItems;
        }
    }

    // IDからアイテムを探します。保存データから復元する時にも使えます。
    public static ItemData GetById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return AllItems.FirstOrDefault(item => item.id == id);
    }

    // 防御、攻撃、秘力/マナの3カテゴリを5種類ずつ作ります。
    private static List<ItemData> BuildCatalog()
    {
        return new List<ItemData>
        {
            ItemData.Defense(
                "iron_bulwark",
                "Iron Bulwark",
                "HP +300 / 被ダメージ -8%",
                "ItemIcons/artifact_f1_bigshield",
                300,
                0f,
                0.08f),

            ItemData.Defense(
                "frostguard_plate",
                "Frostguard Plate",
                "HP +220 / 被ダメージ -12%",
                "ItemIcons/artifact_boss_frostarmor",
                220,
                0f,
                0.12f),

            ItemData.Defense(
                "eternal_heart",
                "Eternal Heart",
                "HP +500",
                "ItemIcons/artifact_f5_eternalheart",
                500,
                0f,
                0f),

            ItemData.Defense(
                "iridium_scale",
                "Iridium Scale",
                "最大HP +18% / 被ダメージ -5%",
                "ItemIcons/artifact_f5_irridiumscale",
                0,
                0.18f,
                0.05f),

            ItemData.Defense(
                "phalanx_aegis",
                "Phalanx Aegis",
                "HP +250 / 被ダメージ -10%",
                "ItemIcons/artifact_f1_shieldofphalanx",
                250,
                0f,
                0.10f),

            ItemData.Offense(
                "spine_cleaver",
                "Spine Cleaver",
                "攻撃力 +30",
                "ItemIcons/artifact_f3_spinecleaver",
                30,
                0f,
                0f),

            ItemData.Offense(
                "skywind_glaives",
                "Skywind Glaives",
                "攻撃速度 +18%",
                "ItemIcons/artifact_f1_skywindglaives",
                0,
                0f,
                0.18f),

            ItemData.Offense(
                "godhammer",
                "Godhammer",
                "攻撃力 +45 / 攻撃速度 +8%",
                "ItemIcons/artifact_f5_godhammer",
                45,
                0f,
                0.08f),

            ItemData.Offense(
                "adamantine_claws",
                "Adamantine Claws",
                "攻撃力 +25%",
                "ItemIcons/artifact_f5_adamantineclaws",
                0,
                0.25f,
                0f),

            ItemData.Offense(
                "rage_chakram",
                "Rage Chakram",
                "攻撃力 +15 / 攻撃速度 +22%",
                "ItemIcons/artifact_f4_ragechackram",
                15,
                0f,
                0.22f),

            ItemData.Skill(
                "unbounded_amulet",
                "Unbounded Amulet",
                "秘力 +15% / 通常攻撃マナ +12",
                "ItemIcons/artifact_f2_unboundedenergyamulet",
                0.15f,
                12,
                0,
                0),

            ItemData.Skill(
                "ykir_staff",
                "Ykir Staff",
                "秘力 +25%",
                "ItemIcons/artifact_f3_staffofykir",
                0.25f,
                0,
                0,
                0),

            ItemData.Skill(
                "thunderclap_scepter",
                "Thunderclap Scepter",
                "秘力 +18% / 被弾マナ +8",
                "ItemIcons/artifact_f3_thunderclap",
                0.18f,
                0,
                8,
                0),

            ItemData.Skill(
                "repair_staff",
                "Repair Staff",
                "秘力 +20% / 通常攻撃マナ +8",
                "ItemIcons/artifact_f3_repairstaff",
                0.20f,
                8,
                0,
                0),

            ItemData.Skill(
                "darkstone_ring",
                "Darkstone Ring",
                "秘力 +10% / 必要マナ -20",
                "ItemIcons/artifact_f4_darkstonering",
                0.10f,
                0,
                0,
                20)
        };
    }
}
