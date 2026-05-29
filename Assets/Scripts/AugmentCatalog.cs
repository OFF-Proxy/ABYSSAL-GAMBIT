using System.Collections.Generic;

// オーグメントのティアです。1回目=Silver、2回目=Gold、3回目=Prism で抽選されます。
public enum AugmentTier { Silver, Gold, Prism }

// オーグメントの効果カテゴリ（後段で適用ロジックを分岐させる時に使う粗いタグ）。
// 「まずは作るところから」なので、ここではフックの目印として保持するだけです。
public enum AugmentEffectKind
{
    Stat,         // 全体・部分のステータス強化
    Synergy,      // シナジーカウント追加（エンブレム的）
    Economy,      // 収入・利子・コイン
    Item,         // アイテム入手・アイテム関連
    Combat,       // 戦闘中のproc・on-hit・特殊効果
    Special       // 特殊（コスト解放・復活・タイムストップ等）
}

// オーグメント1つの定義です。Id は一意で、後で効果適用コードのswitch分岐キーになります。
[System.Serializable]
public class AugmentDefinition
{
    public string Id;
    public AugmentTier Tier;
    public AugmentEffectKind Kind;
    public string NameJa;
    public string NameEn;
    public string DescriptionJa;
    public string DescriptionEn;

    public AugmentDefinition(string id, AugmentTier tier, AugmentEffectKind kind, string nameJa, string nameEn, string descJa, string descEn)
    {
        Id = id;
        Tier = tier;
        Kind = kind;
        NameJa = nameJa;
        NameEn = nameEn;
        DescriptionJa = descJa;
        DescriptionEn = descEn;
    }
}

// オーグメント一覧。シルバー30種類、ゴールド25種類、プリズム25種類の計80種類です。
// 効果の本実装は次フェーズ。ここではデータカタログのみを提供します。
public static class AugmentCatalog
{
    private static List<AugmentDefinition> all;

    public static IReadOnlyList<AugmentDefinition> All
    {
        get
        {
            if (all == null)
                all = BuildAll();
            return all;
        }
    }

    public static List<AugmentDefinition> ByTier(AugmentTier tier)
    {
        List<AugmentDefinition> filtered = new List<AugmentDefinition>();
        IReadOnlyList<AugmentDefinition> source = All;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].Tier == tier)
                filtered.Add(source[i]);
        }
        return filtered;
    }

    public static AugmentDefinition FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        IReadOnlyList<AugmentDefinition> source = All;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].Id == id)
                return source[i];
        }
        return null;
    }

    private static List<AugmentDefinition> BuildAll()
    {
        List<AugmentDefinition> list = new List<AugmentDefinition>(80);

        // ============================================================
        // === Silver (30) — 小〜中ぐらいの効果。最初の選択肢として安定 ===
        // ============================================================
        list.Add(new AugmentDefinition("silver_income_1", AugmentTier.Silver, AugmentEffectKind.Economy,
            "収入アップ I", "Income I",
            "毎ラウンドのクリア収入が +1 されます。",
            "Gain +1 gold every wave clear."));
        list.Add(new AugmentDefinition("silver_interest_cap_1", AugmentTier.Silver, AugmentEffectKind.Economy,
            "利子の達人 I", "Interest Cap I",
            "利子の上限が +2 されます。",
            "Increase interest cap by +2."));
        list.Add(new AugmentDefinition("silver_coins_8", AugmentTier.Silver, AugmentEffectKind.Economy,
            "小銭の祝福", "Coin Pouch",
            "選択時に 8 ゴールドを獲得します。",
            "Instantly gain 8 gold."));
        list.Add(new AugmentDefinition("silver_random_item", AugmentTier.Silver, AugmentEffectKind.Item,
            "アイテム発掘", "Item Find",
            "選択時にランダムなアイテムを 1 個入手します。",
            "Instantly gain 1 random item."));
        list.Add(new AugmentDefinition("silver_bench_slot_1", AugmentTier.Silver, AugmentEffectKind.Special,
            "ベンチ拡張 I", "Bench Slot I",
            "ベンチのスロットが +1 増えます。",
            "Add +1 bench slot."));
        list.Add(new AugmentDefinition("silver_emblem_warrior", AugmentTier.Silver, AugmentEffectKind.Synergy,
            "戦士の誇り", "Warrior Emblem",
            "戦士シナジーのカウントが +1 されます。",
            "+1 to Warrior synergy count."));
        list.Add(new AugmentDefinition("silver_emblem_ranger", AugmentTier.Silver, AugmentEffectKind.Synergy,
            "射手の集中", "Ranger Emblem",
            "射手シナジーのカウントが +1 されます。",
            "+1 to Ranger synergy count."));
        list.Add(new AugmentDefinition("silver_emblem_arcanist", AugmentTier.Silver, AugmentEffectKind.Synergy,
            "魔導の理", "Arcanist Emblem",
            "魔導シナジーのカウントが +1 されます。",
            "+1 to Arcanist synergy count."));
        list.Add(new AugmentDefinition("silver_dr_5", AugmentTier.Silver, AugmentEffectKind.Stat,
            "守護の盾 I", "Guardian Plate I",
            "味方全体の被ダメージ軽減 +5%。",
            "All allies take 5% less damage."));
        list.Add(new AugmentDefinition("silver_atk_6", AugmentTier.Silver, AugmentEffectKind.Stat,
            "剣の冴え I", "Sharpened Blade I",
            "味方全体の攻撃力 +6%。",
            "All allies gain +6% attack damage."));
        list.Add(new AugmentDefinition("silver_move_6", AugmentTier.Silver, AugmentEffectKind.Stat,
            "疾風の足 I", "Swift Step I",
            "味方全体の移動速度 +6%。",
            "All allies gain +6% movement speed."));
        list.Add(new AugmentDefinition("silver_hp_6", AugmentTier.Silver, AugmentEffectKind.Stat,
            "生命の継承", "Vitality I",
            "味方全体の最大HP +6%。",
            "All allies gain +6% max HP."));
        list.Add(new AugmentDefinition("silver_exp_1", AugmentTier.Silver, AugmentEffectKind.Economy,
            "学びの時", "Study Time",
            "ウェーブクリア時のEXP獲得 +1。",
            "+1 EXP per wave clear."));
        list.Add(new AugmentDefinition("silver_cost1_hp", AugmentTier.Silver, AugmentEffectKind.Stat,
            "若手の昇格", "Promotion",
            "戦闘開始時、所持するコスト1ユニット全員の最大HP +10%。",
            "Combat start: cost-1 units gain +10% max HP."));
        list.Add(new AugmentDefinition("silver_same_cost_bond", AugmentTier.Silver, AugmentEffectKind.Stat,
            "絆の結束", "Cost Bond",
            "盤面に同じコストのユニットが2体以上いる時、それらは +3% 与ダメージ。",
            "When 2+ units share a cost on the board, each gains +3% damage."));
        list.Add(new AugmentDefinition("silver_slow_proc", AugmentTier.Silver, AugmentEffectKind.Combat,
            "氷の囁き", "Frost Whispers",
            "通常攻撃が 5% の確率で対象を 2 秒スロウします。",
            "5% chance on hit to slow for 2s."));
        list.Add(new AugmentDefinition("silver_burn_proc", AugmentTier.Silver, AugmentEffectKind.Combat,
            "炎の祝福", "Flame Blessing",
            "通常攻撃が 5% の確率で対象を燃焼させます。",
            "5% chance on hit to burn the target."));
        list.Add(new AugmentDefinition("silver_zap_proc", AugmentTier.Silver, AugmentEffectKind.Combat,
            "雷の手", "Lightning Touch",
            "通常攻撃が 5% の確率で小さな雷撃を追加で放ちます。",
            "5% chance on hit to release a small lightning strike."));
        list.Add(new AugmentDefinition("silver_team_heal", AugmentTier.Silver, AugmentEffectKind.Combat,
            "救護の手", "First Aid",
            "戦闘開始時、最も傷ついた味方を最大HPの10%回復します。",
            "Combat start: heal the most damaged ally for 10% max HP."));
        list.Add(new AugmentDefinition("silver_reroll_cost", AugmentTier.Silver, AugmentEffectKind.Economy,
            "節約家", "Thrift",
            "リロールのコストが -1 されます。",
            "Reduce reroll cost by 1."));
        list.Add(new AugmentDefinition("silver_gold_attack", AugmentTier.Silver, AugmentEffectKind.Combat,
            "資本家の特権", "Capitalist",
            "所持金 10 ごとに 与ダメージ +1%（上限 +10%）。",
            "+1% damage per 10 gold (cap +10%)."));
        list.Add(new AugmentDefinition("silver_extra_synergy_count", AugmentTier.Silver, AugmentEffectKind.Synergy,
            "連携の証", "Bond Sign",
            "好きなシナジー1種が +1 ユニット分多くカウントされます（戦闘開始時にランダム）。",
            "One random synergy counts +1 each combat."));
        list.Add(new AugmentDefinition("silver_dupe_dmg", AugmentTier.Silver, AugmentEffectKind.Stat,
            "集めるほど強く", "Strength in Numbers",
            "同名ユニットを2体以上所持していると、それらは +5% 与ダメージ。",
            "Owning 2+ of the same unit grants those units +5% damage."));
        list.Add(new AugmentDefinition("silver_range_archer", AugmentTier.Silver, AugmentEffectKind.Stat,
            "遠投", "Far Shot",
            "射手シナジー対象ユニットの射程 +1。",
            "Ranger units gain +1 range."));
        list.Add(new AugmentDefinition("silver_speed_boost_delayed", AugmentTier.Silver, AugmentEffectKind.Combat,
            "追撃の構え", "Pursuit Stance",
            "戦闘開始 2 秒後、味方全体の攻撃速度 +10% を 5 秒間。",
            "2s into combat, allies gain +10% attack speed for 5s."));
        list.Add(new AugmentDefinition("silver_stun_proc", AugmentTier.Silver, AugmentEffectKind.Combat,
            "鎖の祝福", "Binding Chain",
            "通常攻撃が 5% の確率で対象を 1 秒スタン。",
            "5% chance on hit to stun for 1s."));
        list.Add(new AugmentDefinition("silver_revive_3", AugmentTier.Silver, AugmentEffectKind.Combat,
            "粘り強い守り", "Resilient Spirit",
            "戦闘中、味方は1度だけHP10%で復活（チャプター中 計3体まで）。",
            "Allies revive once at 10% HP (up to 3 total per chapter)."));
        list.Add(new AugmentDefinition("silver_item_drop", AugmentTier.Silver, AugmentEffectKind.Item,
            "アイテム量産", "Drop Bonus",
            "ウェーブクリア時のアイテムドロップ率 +10%。",
            "+10% wave clear item drop chance."));
        list.Add(new AugmentDefinition("silver_extra_coin", AugmentTier.Silver, AugmentEffectKind.Economy,
            "コインの輝き", "Coin Shine",
            "ウェーブクリア時に追加で +2 ゴールド。",
            "Wave clear: extra +2 gold."));
        list.Add(new AugmentDefinition("silver_first_attack", AugmentTier.Silver, AugmentEffectKind.Combat,
            "先制の達人", "First Strike",
            "戦闘開始から 3 秒間、味方全体の与ダメージ +15%。",
            "First 3s of combat: +15% damage for all allies."));

        // ============================================================
        // === Gold (25) — 中〜大の効果。戦力を一段引き上げる ===
        // ============================================================
        list.Add(new AugmentDefinition("gold_income_2", AugmentTier.Gold, AugmentEffectKind.Economy,
            "収入アップ II", "Income II",
            "毎ラウンドのクリア収入が +2 されます。",
            "Gain +2 gold every wave clear."));
        list.Add(new AugmentDefinition("gold_interest_cap_2", AugmentTier.Gold, AugmentEffectKind.Economy,
            "利子の達人 II", "Interest Cap II",
            "利子の上限が +3 されます。",
            "Increase interest cap by +3."));
        list.Add(new AugmentDefinition("gold_coins_20", AugmentTier.Gold, AugmentEffectKind.Economy,
            "金鉱発見", "Goldmine",
            "選択時に 20 ゴールドを獲得します。",
            "Instantly gain 20 gold."));
        list.Add(new AugmentDefinition("gold_random_items_2", AugmentTier.Gold, AugmentEffectKind.Item,
            "アイテム工房", "Item Workshop",
            "選択時にランダムなアイテムを 2 個入手します。",
            "Instantly gain 2 random items."));
        list.Add(new AugmentDefinition("gold_free_reroll", AugmentTier.Gold, AugmentEffectKind.Economy,
            "無料の遺産", "Free Spin",
            "毎ラウンド、リロールが 1 回ぶん無料になります。",
            "Each round, the first reroll is free."));
        list.Add(new AugmentDefinition("gold_bench_slot_2", AugmentTier.Gold, AugmentEffectKind.Special,
            "ベンチ拡張 II", "Bench Slot II",
            "ベンチのスロットが +2 増えます。",
            "Add +2 bench slots."));
        list.Add(new AugmentDefinition("gold_emblem_warrior_2", AugmentTier.Gold, AugmentEffectKind.Synergy,
            "戦士の覚醒", "Warrior Awakening",
            "戦士シナジーのカウントが +2 されます。",
            "+2 to Warrior synergy count."));
        list.Add(new AugmentDefinition("gold_emblem_ranger_2", AugmentTier.Gold, AugmentEffectKind.Synergy,
            "射手の覚醒", "Ranger Awakening",
            "射手シナジーのカウントが +2 されます。",
            "+2 to Ranger synergy count."));
        list.Add(new AugmentDefinition("gold_emblem_arcanist_2", AugmentTier.Gold, AugmentEffectKind.Synergy,
            "魔導の覚醒", "Arcanist Awakening",
            "魔導シナジーのカウントが +2 されます。",
            "+2 to Arcanist synergy count."));
        list.Add(new AugmentDefinition("gold_dr_10", AugmentTier.Gold, AugmentEffectKind.Stat,
            "守護の盾 II", "Guardian Plate II",
            "味方全体の被ダメージ軽減 +10%。",
            "All allies take 10% less damage."));
        list.Add(new AugmentDefinition("gold_atk_12", AugmentTier.Gold, AugmentEffectKind.Stat,
            "剣の冴え II", "Sharpened Blade II",
            "味方全体の攻撃力 +12%。",
            "All allies gain +12% attack damage."));
        list.Add(new AugmentDefinition("gold_move_12", AugmentTier.Gold, AugmentEffectKind.Stat,
            "疾風の足 II", "Swift Step II",
            "味方全体の移動速度 +12%。",
            "All allies gain +12% movement speed."));
        list.Add(new AugmentDefinition("gold_hp_12", AugmentTier.Gold, AugmentEffectKind.Stat,
            "生命の継承 II", "Vitality II",
            "味方全体の最大HP +12%。",
            "All allies gain +12% max HP."));
        list.Add(new AugmentDefinition("gold_duplicate_synergy", AugmentTier.Gold, AugmentEffectKind.Synergy,
            "二重シナジー", "Doubled Bond",
            "盤面のシナジーカウントが、好きな1種でさらに +1（戦闘開始時にランダム）。",
            "+1 to one random synergy each combat (chosen anew)."));
        list.Add(new AugmentDefinition("gold_low_hp_dmg", AugmentTier.Gold, AugmentEffectKind.Combat,
            "猛々しさ", "Berserker",
            "味方は HP が低いほど与ダメージ上昇（最大 +20%）。",
            "Allies deal up to +20% more damage at low HP."));
        list.Add(new AugmentDefinition("gold_elite_summon", AugmentTier.Gold, AugmentEffectKind.Combat,
            "エリート徴用", "Elite Call",
            "戦闘開始時に、ランダムなコスト3ユニット 1 体を仮配置（戦闘終了で消滅）。",
            "Combat start: temporarily summon a random cost-3 unit (despawns after fight)."));
        list.Add(new AugmentDefinition("gold_judgement", AugmentTier.Gold, AugmentEffectKind.Combat,
            "裁きの嵐", "Storm of Judgement",
            "戦闘開始 5 秒後、ランダムな敵 2 体に最大HPの 15% ダメージ。",
            "5s into combat, hit 2 random enemies for 15% max HP damage."));
        list.Add(new AugmentDefinition("gold_time_pulse", AugmentTier.Gold, AugmentEffectKind.Combat,
            "時間の流れ", "Tempo Pulse",
            "戦闘開始 10 秒後、味方全体の攻撃速度 +50% を 2 秒間。",
            "10s into combat, allies gain +50% attack speed for 2s."));
        list.Add(new AugmentDefinition("gold_low_hp_dr", AugmentTier.Gold, AugmentEffectKind.Combat,
            "不撓不屈", "Undaunted",
            "HP 30% 以下の味方は被ダメージ軽減 +15%。",
            "Allies below 30% HP take 15% less damage."));
        list.Add(new AugmentDefinition("gold_guaranteed_high_cost", AugmentTier.Gold, AugmentEffectKind.Economy,
            "黄金のリロール", "Golden Reroll",
            "リロール後、必ず 1 枚はコスト3以上のユニット。",
            "After each reroll, at least 1 card is cost 3+."));
        list.Add(new AugmentDefinition("gold_better_interest", AugmentTier.Gold, AugmentEffectKind.Economy,
            "金庫の番人", "Vault Keeper",
            "利子計算が 8 ゴールドごとになります。",
            "Interest is granted per 8 gold instead of 10."));
        list.Add(new AugmentDefinition("gold_exp_2", AugmentTier.Gold, AugmentEffectKind.Economy,
            "エクスペリエンス", "Experience",
            "ウェーブクリア時のEXP獲得 +2。",
            "+2 EXP per wave clear."));
        list.Add(new AugmentDefinition("gold_summon_dmg", AugmentTier.Gold, AugmentEffectKind.Combat,
            "召喚士の血", "Summoner's Blood",
            "全ての召喚体の与ダメージ +30%。",
            "All summons deal +30% damage."));
        list.Add(new AugmentDefinition("gold_star2_bonus", AugmentTier.Gold, AugmentEffectKind.Economy,
            "連勝の輝き", "Star Glow",
            "★2 以上のユニットを所持していると、クリア収入 +3。",
            "Owning a ★2+ unit grants +3 income per wave clear."));
        list.Add(new AugmentDefinition("gold_item_drop_chance", AugmentTier.Gold, AugmentEffectKind.Item,
            "アイテムの結晶化", "Item Crystallize",
            "ウェーブクリア時、30% の確率でアイテムが 1 個追加でドロップ。",
            "Wave clear: 30% chance for +1 random item."));

        // ============================================================
        // === Prism (25) — 強力・変革的。終盤を一気に押し上げる ===
        // ============================================================
        list.Add(new AugmentDefinition("prism_income_5", AugmentTier.Prism, AugmentEffectKind.Economy,
            "収入の女神", "Goddess of Wealth",
            "毎ラウンドのクリア収入が +5 されます。",
            "Gain +5 gold every wave clear."));
        list.Add(new AugmentDefinition("prism_interest_godly", AugmentTier.Prism, AugmentEffectKind.Economy,
            "無限利子", "Infinite Interest",
            "利子の上限 +5、かつ利子は 5 ゴールドごとに計算されます。",
            "Interest cap +5, and interest is granted per 5 gold."));
        list.Add(new AugmentDefinition("prism_coins_50", AugmentTier.Prism, AugmentEffectKind.Economy,
            "黄金時代", "Golden Age",
            "選択時に 50 ゴールドを獲得します。",
            "Instantly gain 50 gold."));
        list.Add(new AugmentDefinition("prism_items_3", AugmentTier.Prism, AugmentEffectKind.Item,
            "アーティファクトの庇護", "Artifact Protection",
            "選択時にランダムなアイテムを 3 個入手します。",
            "Instantly gain 3 random items."));
        list.Add(new AugmentDefinition("prism_free_reroll_all", AugmentTier.Prism, AugmentEffectKind.Economy,
            "無限のリロール", "Infinite Reroll",
            "リロールが無料になります。",
            "Reroll becomes free."));
        list.Add(new AugmentDefinition("prism_bench_3", AugmentTier.Prism, AugmentEffectKind.Special,
            "ベンチ拡張 III", "Bench Slot III",
            "ベンチのスロットが +3 増えます。",
            "Add +3 bench slots."));
        list.Add(new AugmentDefinition("prism_emblem_warrior_3", AugmentTier.Prism, AugmentEffectKind.Synergy,
            "戦士の真髄", "Warrior Essence",
            "戦士シナジーのカウントが +3 されます。",
            "+3 to Warrior synergy count."));
        list.Add(new AugmentDefinition("prism_emblem_ranger_3", AugmentTier.Prism, AugmentEffectKind.Synergy,
            "射手の真髄", "Ranger Essence",
            "射手シナジーのカウントが +3 されます。",
            "+3 to Ranger synergy count."));
        list.Add(new AugmentDefinition("prism_emblem_arcanist_3", AugmentTier.Prism, AugmentEffectKind.Synergy,
            "魔導の真髄", "Arcanist Essence",
            "魔導シナジーのカウントが +3 されます。",
            "+3 to Arcanist synergy count."));
        list.Add(new AugmentDefinition("prism_dr_20", AugmentTier.Prism, AugmentEffectKind.Stat,
            "絶対防御", "Absolute Defense",
            "味方全体の被ダメージ軽減 +20%。",
            "All allies take 20% less damage."));
        list.Add(new AugmentDefinition("prism_atk_20", AugmentTier.Prism, AugmentEffectKind.Stat,
            "究極の剣", "Ultimate Blade",
            "味方全体の攻撃力 +20%。",
            "All allies gain +20% attack damage."));
        list.Add(new AugmentDefinition("prism_speed_20", AugmentTier.Prism, AugmentEffectKind.Stat,
            "神速", "Divine Speed",
            "味方全体の移動・攻撃速度 +20%。",
            "All allies gain +20% move and attack speed."));
        list.Add(new AugmentDefinition("prism_hp_25", AugmentTier.Prism, AugmentEffectKind.Stat,
            "生命の根源", "Source of Life",
            "味方全体の最大HP +25%。",
            "All allies gain +25% max HP."));
        list.Add(new AugmentDefinition("prism_king_blessed", AugmentTier.Prism, AugmentEffectKind.Stat,
            "王の宣告", "King's Decree",
            "盤面でコスト最大の味方は与ダメージ +50%、被ダメージ軽減 +50%。",
            "The highest-cost ally on the board gains +50% damage and +50% damage reduction."));
        list.Add(new AugmentDefinition("prism_kill_heal", AugmentTier.Prism, AugmentEffectKind.Combat,
            "死神の凝視", "Reaper's Gaze",
            "敵を撃破するたび、その敵の最大HPの10%を味方全体に分配回復。",
            "When an enemy dies, distribute heal equal to 10% of their max HP across allies."));
        list.Add(new AugmentDefinition("prism_time_stop", AugmentTier.Prism, AugmentEffectKind.Combat,
            "タイムストップ", "Time Stop",
            "戦闘開始時、敵全体を 1.5 秒スタン。",
            "Combat start: stun all enemies for 1.5s."));
        list.Add(new AugmentDefinition("prism_one_revive", AugmentTier.Prism, AugmentEffectKind.Combat,
            "奇跡の覚醒", "Miracle Wake",
            "戦闘中 1 回、最初に倒れた味方を HP 50% で復活させます。",
            "Once per combat, revive the first fallen ally at 50% HP."));
        list.Add(new AugmentDefinition("prism_dark_pact", AugmentTier.Prism, AugmentEffectKind.Stat,
            "暗黒の祝福", "Dark Pact",
            "味方全体の与ダメージ +15%、ただし被ダメージも +15%。",
            "All allies gain +15% damage dealt and +15% damage taken."));
        list.Add(new AugmentDefinition("prism_all_synergy", AugmentTier.Prism, AugmentEffectKind.Synergy,
            "シナジー結晶", "Synergy Crystal",
            "盤面の全ユニットが、所持するシナジーそれぞれを +1 ぶん多く数えます。",
            "Each board unit counts +1 toward every synergy it owns."));
        list.Add(new AugmentDefinition("prism_summon_master", AugmentTier.Prism, AugmentEffectKind.Combat,
            "召喚マスター", "Summon Master",
            "召喚体を 1 体追加召喚し、全召喚体の HP +30%。",
            "Summon +1 extra creature; all summons gain +30% HP."));
        list.Add(new AugmentDefinition("prism_unlock_all_costs", AugmentTier.Prism, AugmentEffectKind.Economy,
            "コスト解放", "Cost Liberation",
            "このチャプター中、ショップに全コストのユニットが出現します。",
            "For this chapter, the shop can offer units of all costs."));
        list.Add(new AugmentDefinition("prism_boss_reward_extra", AugmentTier.Prism, AugmentEffectKind.Special,
            "ボスの誓い", "Boss Pledge",
            "章ボス報酬で、報酬ユニットの選択肢が常に 3 体全種類になります。",
            "Chapter boss reward always offers all 3 boss units."));
        list.Add(new AugmentDefinition("prism_warrior_kill_buff", AugmentTier.Prism, AugmentEffectKind.Combat,
            "戦士道", "Warrior's Path",
            "戦士ユニットが敵を撃破するたび、次の戦闘で +30% 与ダメージを得ます。",
            "Each enemy killed by a Warrior grants +30% damage to that Warrior next combat."));
        list.Add(new AugmentDefinition("prism_item_alchemy", AugmentTier.Prism, AugmentEffectKind.Item,
            "アイテム錬成", "Item Alchemy",
            "戦闘終了時、30% の確率でランダムなアイテムを入手。",
            "30% chance to gain a random item at combat end."));
        list.Add(new AugmentDefinition("prism_score_multiplier", AugmentTier.Prism, AugmentEffectKind.Special,
            "栄光の道", "Path of Glory",
            "ステージ/チャプタークリア時のスコア計算に +30% の倍率がかかります。",
            "Score gains are multiplied by 1.3× at stage/chapter clear."));

        return list;
    }
}
