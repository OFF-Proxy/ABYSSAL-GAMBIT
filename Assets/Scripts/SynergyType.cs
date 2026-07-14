// ユニットが持つシナジー種別です。
// Noneは未設定として扱い、カウントや表示には使いません。
public enum SynergyType
{
    None,
    Warrior,
    Ranger,
    Arcanist,
    Guardian,
    Beast,
    Shadow,
    Machine,
    Wraith,
    Apex,
    Inferno,
    Frost,
    Storm,
    Abyss,
    Divine,
    Frenzy,
    Royal,
    Summoner,
    Alchemy,
    // アルカナ専用シナジー「終焉」。1体で常時発動し、盤面のBloodMage数に応じてアルカナ自身を強化します。
    Finality,
    // 陣営シナジー（DESIGN_R3-factions）。将来ユニットに割り当てる土台。閾値は標準(2/4/6)。
    Lyonar,
    Songhai,
    Magmar,
    Vetruvian,
    Abyssian,
    Vanar
}
