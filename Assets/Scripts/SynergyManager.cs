using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 盤面上の味方ユニットだけを数え、シナジー段階と戦闘中効果を管理します。
public class SynergyManager : MonoBehaviour
{
    public static SynergyManager Instance { get; private set; }

    // trueにすると、再計算のたびに現在のカウントと発動段階をConsoleへ出します。
    public bool debugLog = false;

    private readonly Dictionary<SynergyType, int> synergyCounts = new Dictionary<SynergyType, int>();
    private readonly Dictionary<SynergyType, int> activeTiers = new Dictionary<SynergyType, int>();
    private readonly Dictionary<SynergyType, int> enemySynergyCounts = new Dictionary<SynergyType, int>();
    private readonly Dictionary<SynergyType, int> enemyActiveTiers = new Dictionary<SynergyType, int>();
    private readonly Dictionary<BaseEntity, int> stormAttackCounters = new Dictionary<BaseEntity, int>();
    private readonly List<BaseEntity> activeSummons = new List<BaseEntity>();
    private GameManager attachedGameManager;
    private bool wraithReviveUsedThisBattle;
    private bool abyssPanicCurseUsedThisBattle;
    private bool divineTeamRescueUsedThisBattle;
    private bool frenzyRampageUsedThisBattle;
    private bool summonerLargeSummonUsedThisBattle;
    private Coroutine infernoRainCoroutine;
    private BaseEntity royalKing;

    // 戦闘開始時のシナジー編成スナップショット。死亡してもシナジーが減らないように、
    // 戦闘中の CountSynergiesForTeam は盤面実態ではなくこの記録から数えます。
    // 編成中（戦闘外）は従来どおりリアルタイムに盤面から集計します。
    private struct CombatSynergyEntry
    {
        public string UnitKey;            // LocalizationManager.CleanUnitName 後の id（同名重複の除外キー）
        public List<SynergyType> Synergies;
    }
    private bool combatSynergySnapshotActive;
    private readonly List<CombatSynergyEntry> team1CombatSnapshot = new List<CombatSynergyEntry>();
    private readonly List<CombatSynergyEntry> team2CombatSnapshot = new List<CombatSynergyEntry>();

    private static readonly SynergyType[] CountedSynergies =
    {
        SynergyType.Warrior,
        SynergyType.Ranger,
        SynergyType.Arcanist,
        SynergyType.Guardian,
        SynergyType.Beast,
        SynergyType.Shadow,
        SynergyType.Machine,
        SynergyType.Wraith,
        SynergyType.Apex,
        SynergyType.Inferno,
        SynergyType.Frost,
        SynergyType.Storm,
        SynergyType.Abyss,
        SynergyType.Divine,
        SynergyType.Frenzy,
        SynergyType.Royal,
        SynergyType.Summoner,
        SynergyType.Alchemy,
        SynergyType.Finality,
        // 陣営シナジー（実ユニットに割当済み）。辞書キー漏れによる KeyNotFoundException を防ぐため必ず含める。
        SynergyType.Lyonar,
        SynergyType.Songhai,
        SynergyType.Magmar,
        SynergyType.Vetruvian,
        SynergyType.Abyssian,
        SynergyType.Vanar
    };

    public static IReadOnlyList<SynergyType> OrderedSynergyTypes => CountedSynergies;

    // シーンに手動配置されていなくても、必要になった時点で自動生成します。
    public static SynergyManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        SynergyManager existing = FindObjectOfType<SynergyManager>(true);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject managerObject = new GameObject("SynergyManager");
        Instance = managerObject.AddComponent<SynergyManager>();
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
        InitializeDictionaries();
    }

    private void OnDestroy()
    {
        DetachGameManager();
    }

    // GameManagerの人数変更イベントを購読し、購入・売却・配置変更に合わせて再計算します。
    public void AttachGameManager(GameManager gameManager)
    {
        if (attachedGameManager == gameManager)
            return;

        DetachGameManager();
        attachedGameManager = gameManager;
        if (attachedGameManager == null)
            return;

        attachedGameManager.OnRosterChanged += RecalculateSynergies;
        attachedGameManager.OnRoundEnd += ClearBattleSynergyState;
        RecalculateSynergies();
    }

    private void DetachGameManager()
    {
        if (attachedGameManager == null)
            return;

        attachedGameManager.OnRosterChanged -= RecalculateSynergies;
        attachedGameManager.OnRoundEnd -= ClearBattleSynergyState;
        attachedGameManager = null;
    }

    // EntityDataで未設定なら、ユニット名・射程・見た目の役割に合わせた初期シナジーを入れます。
    public static void AssignEntitySynergies(BaseEntity entity, EntitiesDatabaseSO.EntityData entityData)
    {
        if (entity == null)
            return;

        List<SynergyType> synergies = GetSynergiesForEntityData(entityData);
        SynergyType first = synergies.Count > 0 ? synergies[0] : SynergyType.None;
        SynergyType second = synergies.Count > 1 ? synergies[1] : SynergyType.None;
        SynergyType third = synergies.Count > 2 ? synergies[2] : SynergyType.None;

        entity.SetSynergies(first, second, third);
    }

    // ショップカードなど、まだBaseEntityが生成されていない場所でもEntityDataから表示用シナジーを取得します。
    public static List<SynergyType> GetSynergiesForEntityData(EntitiesDatabaseSO.EntityData entityData)
    {
        SynergyType first = entityData.synergy1;
        SynergyType second = entityData.synergy2;
        SynergyType third = entityData.synergy3;
        if (first == SynergyType.None && second == SynergyType.None && third == SynergyType.None)
            ResolveDefaultSynergies(entityData.name, entityData.prefab, out first, out second, out third);

        List<SynergyType> synergies = new List<SynergyType>(3);
        AddUniqueSynergy(synergies, first);
        AddUniqueSynergy(synergies, second);
        AddUniqueSynergy(synergies, third);
        ApplySpecificSynergyOverrides(entityData.name, synergies);
        return synergies;
    }

    // 現在盤面にいる味方ユニットを重複名なしで数え直します。
    public void RecalculateSynergies()
    {
        InitializeDictionaries();
        if (GameManager.Instance == null)
        {
            SynergyPanelUI.EnsureExists().Refresh(this);
            return;
        }

        CountSynergiesForTeam(Team.Team1, synergyCounts, activeTiers);
        CountSynergiesForTeam(Team.Team2, enemySynergyCounts, enemyActiveTiers);

        if (debugLog)
            LogDebugState();

        SynergyPanelUI.EnsureExists().Refresh(this);
    }

    public int GetSynergyCount(SynergyType type)
    {
        int count = synergyCounts.TryGetValue(type, out int c) ? c : 0;
        return count + PlayerSynergyAugmentBonus(type);
    }

    // プレイヤーチームのシナジーカウントへ加算されるオーグメント由来のボーナス。
    // エンブレム（戦士/狙撃/秘術の真髄など）＋戦闘中限定のランダム+1（silver_extra_synergy_count / gold_duplicate_synergy）。
    // 表示用カウント(GetSynergyCount)と発動段階の計算(CountSynergiesForTeam)で“同じ値”を使うために共通化する。
    // ※以前は発動段階が生カウントのみで計算され、オーグメントで表示が増えても効果が発動しない不具合があった。
    private int PlayerSynergyAugmentBonus(SynergyType type)
    {
        if (GameManager.Instance == null) return 0;
        int bonus = 0;
        if (type == SynergyType.Warrior) bonus += GameManager.Instance.AugmentSynergyBonusWarrior;
        else if (type == SynergyType.Ranger) bonus += GameManager.Instance.AugmentSynergyBonusRanger;
        else if (type == SynergyType.Arcanist) bonus += GameManager.Instance.AugmentSynergyBonusArcanist;
        if (GameManager.Instance.AdditionalSynergyBonusThisCombat.TryGetValue(type, out int rnd))
            bonus += rnd;
        return bonus;
    }

    public int GetSynergyCountForTeam(SynergyType type, Team team)
    {
        Dictionary<SynergyType, int> source = team == Team.Team1 ? synergyCounts : enemySynergyCounts;
        return source.TryGetValue(type, out int count) ? count : 0;
    }

    public int GetSynergyTier(SynergyType type)
    {
        return activeTiers.TryGetValue(type, out int tier) ? tier : 0;
    }

    public int GetSynergyTierForTeam(SynergyType type, Team team)
    {
        Dictionary<SynergyType, int> source = team == Team.Team1 ? activeTiers : enemyActiveTiers;
        return source.TryGetValue(type, out int tier) ? tier : 0;
    }

    public bool IsSynergyActive(SynergyType type, int requiredCount)
    {
        return GetSynergyTier(type) >= requiredCount;
    }

    public bool IsSynergyActiveForTeam(SynergyType type, Team team, int requiredCount)
    {
        return GetSynergyTierForTeam(type, team) >= requiredCount;
    }

    public Dictionary<SynergyType, int> GetActiveSynergies()
    {
        return activeTiers
            .Where(pair => pair.Value > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    // 戦闘開始直前に、開幕シールドや基礎シナジー補正を盤面味方へ付与します。
    public void ApplyBattleStartSynergies()
    {
        RecalculateSynergies();
        ClearBattleSynergyState();
        wraithReviveUsedThisBattle = false;
        abyssPanicCurseUsedThisBattle = false;
        divineTeamRescueUsedThisBattle = false;
        frenzyRampageUsedThisBattle = false;
        summonerLargeSummonUsedThisBattle = false;

        // D-2: 戦闘開始時の編成をスナップショットし、戦闘中の死亡でカウントが落ちないように固定します。
        // ClearBattleSynergyState() より後ろで取らないと、戦闘間の繰越状態と紛れる可能性があるためここで実施します。
        BeginCombatSynergySnapshot();
        RecalculateSynergies();

        if (GameManager.Instance == null)
            return;

        List<BaseEntity> allies = GameManager.Instance.GetPlayerBoardEntitiesForSynergy();
        ApplyBattleStartSynergiesForTeam(allies, Team.Team1, true);

        List<BaseEntity> enemies = GameManager.Instance.GetBoardEntitiesForSynergy(Team.Team2);
        ApplyBattleStartSynergiesForTeam(enemies, Team.Team2, false);
    }

    // チームごとに、戦闘開始時に必要な基礎シナジー補正を付与します。
    private void ApplyBattleStartSynergiesForTeam(List<BaseEntity> allies, Team team, bool applyPlayerOnlyBattleSynergies)
    {
        if (allies == null)
            return;

        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (ally == null || ally.IsDead || !ally.IsOnBoard)
                continue;

            if (ally.HasSynergy(SynergyType.Warrior) && IsSynergyActiveForTeam(SynergyType.Warrior, team, 2))
                ally.AddSynergyDamageReductionBonus(0.08f);

            if (ally.HasSynergy(SynergyType.Ranger) && IsSynergyActiveForTeam(SynergyType.Ranger, team, 2))
                ally.AddSynergyAttackSpeedBonus(0.10f);

            if (ally.HasSynergy(SynergyType.Arcanist) && IsSynergyActiveForTeam(SynergyType.Arcanist, team, 2))
                ally.AddSynergyPowerBonus(0.15f);

            if (ally.HasSynergy(SynergyType.Arcanist) && IsSynergyActiveForTeam(SynergyType.Arcanist, team, 6))
                ally.GainManaFromSynergy(40);

            if (ally.HasSynergy(SynergyType.Machine) && IsSynergyActiveForTeam(SynergyType.Machine, team, 2))
                ally.ApplyTimedSynergyDamageReductionBonus(0.10f, 5f);

            if (ally.HasSynergy(SynergyType.Apex) && IsSynergyActiveForTeam(SynergyType.Apex, team, 1))
                ally.synergyDamageDealtBonus += 0.10f;

            if (ally.HasSynergy(SynergyType.Apex) && IsSynergyActiveForTeam(SynergyType.Apex, team, 2))
            {
                ally.AddSynergyPowerBonus(0.20f);
                ally.AddSynergyDamageReductionBonus(0.08f);
            }

            if (ally.HasSynergy(SynergyType.Apex) && IsSynergyActiveForTeam(SynergyType.Apex, team, 3))
                ally.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.08f)), 8f);

            if (applyPlayerOnlyBattleSynergies && ally.HasSynergy(SynergyType.Royal) && IsSynergyActive(SynergyType.Royal, 1))
                ApplyRoyalKingIfNeeded(allies);

            // === 陣営シナジー（発動段階 1/3/5/7/10。加算式で段階的に強化、10は決定打） ===
            // 各陣営：主人公が同陣営なら効果を hs 倍に増幅（FactionHeroScale, Team1のみ）。
            // Lyonar＝守護の聖光：被ダメ減＋シールド。
            if (ally.HasSynergy(SynergyType.Lyonar))
            {
                float hs = FactionHeroScale(SynergyType.Lyonar, team);
                if (IsSynergyActiveForTeam(SynergyType.Lyonar, team, 1)) ally.AddSynergyDamageReductionBonus(0.08f * hs);
                if (IsSynergyActiveForTeam(SynergyType.Lyonar, team, 3)) { ally.AddSynergyDamageReductionBonus(0.08f * hs); ally.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.08f * hs)), 600f); }
                if (IsSynergyActiveForTeam(SynergyType.Lyonar, team, 5)) { ally.AddSynergyDamageReductionBonus(0.08f * hs); ally.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.12f * hs)), 600f); }
                if (IsSynergyActiveForTeam(SynergyType.Lyonar, team, 7)) { ally.AddSynergyDamageReductionBonus(0.10f * hs); ally.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.2f * hs)), 600f); }
            }
            // Songhai＝影の速攻：攻撃速度特化。
            if (ally.HasSynergy(SynergyType.Songhai))
            {
                float hs = FactionHeroScale(SynergyType.Songhai, team);
                if (IsSynergyActiveForTeam(SynergyType.Songhai, team, 1)) ally.AddSynergyAttackSpeedBonus(0.10f * hs);
                if (IsSynergyActiveForTeam(SynergyType.Songhai, team, 3)) ally.AddSynergyAttackSpeedBonus(0.14f * hs);
                if (IsSynergyActiveForTeam(SynergyType.Songhai, team, 5)) { ally.AddSynergyAttackSpeedBonus(0.16f * hs); ally.synergyDamageDealtBonus += 0.15f * hs; }
                if (IsSynergyActiveForTeam(SynergyType.Songhai, team, 7)) { ally.AddSynergyAttackSpeedBonus(0.25f * hs); ally.synergyDamageDealtBonus += 0.25f * hs; }
            }
            // Magmar＝原始の猛攻：与ダメ特化。
            if (ally.HasSynergy(SynergyType.Magmar))
            {
                float hs = FactionHeroScale(SynergyType.Magmar, team);
                if (IsSynergyActiveForTeam(SynergyType.Magmar, team, 1)) ally.synergyDamageDealtBonus += 0.12f * hs;
                if (IsSynergyActiveForTeam(SynergyType.Magmar, team, 3)) ally.synergyDamageDealtBonus += 0.14f * hs;
                if (IsSynergyActiveForTeam(SynergyType.Magmar, team, 5)) { ally.synergyDamageDealtBonus += 0.18f * hs; ally.AddSynergyAttackSpeedBonus(0.12f * hs); }
                if (IsSynergyActiveForTeam(SynergyType.Magmar, team, 7)) { ally.synergyDamageDealtBonus += 0.3f * hs; ally.AddSynergyAttackSpeedBonus(0.15f * hs); }
            }
            // Vetruvian＝機巧と太陽：スキル威力＋マナ。
            if (ally.HasSynergy(SynergyType.Vetruvian))
            {
                float hs = FactionHeroScale(SynergyType.Vetruvian, team);
                if (IsSynergyActiveForTeam(SynergyType.Vetruvian, team, 1)) ally.AddSynergyPowerBonus(0.15f * hs);
                if (IsSynergyActiveForTeam(SynergyType.Vetruvian, team, 3)) { ally.AddSynergyPowerBonus(0.15f * hs); ally.GainManaFromSynergy(Mathf.RoundToInt(30 * hs)); }
                if (IsSynergyActiveForTeam(SynergyType.Vetruvian, team, 5)) { ally.AddSynergyPowerBonus(0.2f * hs); ally.GainManaFromSynergy(Mathf.RoundToInt(30 * hs)); }
                if (IsSynergyActiveForTeam(SynergyType.Vetruvian, team, 7)) { ally.AddSynergyPowerBonus(0.35f * hs); ally.GainManaFromSynergy(Mathf.RoundToInt(50 * hs)); }
            }
            // Abyssian＝死闘：与ダメ＋被ダメ減。
            if (ally.HasSynergy(SynergyType.Abyssian))
            {
                float hs = FactionHeroScale(SynergyType.Abyssian, team);
                if (IsSynergyActiveForTeam(SynergyType.Abyssian, team, 1)) ally.synergyDamageDealtBonus += 0.12f * hs;
                if (IsSynergyActiveForTeam(SynergyType.Abyssian, team, 3)) { ally.synergyDamageDealtBonus += 0.1f * hs; ally.AddSynergyDamageReductionBonus(0.06f * hs); }
                if (IsSynergyActiveForTeam(SynergyType.Abyssian, team, 5)) { ally.synergyDamageDealtBonus += 0.14f * hs; ally.AddSynergyDamageReductionBonus(0.08f * hs); }
                if (IsSynergyActiveForTeam(SynergyType.Abyssian, team, 7)) { ally.synergyDamageDealtBonus += 0.24f * hs; ally.AddSynergyDamageReductionBonus(0.12f * hs); }
            }
            // Vanar＝氷の堅守：被ダメ減＋スキル威力。
            if (ally.HasSynergy(SynergyType.Vanar))
            {
                float hs = FactionHeroScale(SynergyType.Vanar, team);
                if (IsSynergyActiveForTeam(SynergyType.Vanar, team, 1)) ally.AddSynergyDamageReductionBonus(0.10f * hs);
                if (IsSynergyActiveForTeam(SynergyType.Vanar, team, 3)) { ally.AddSynergyDamageReductionBonus(0.08f * hs); ally.AddSynergyPowerBonus(0.15f * hs); }
                if (IsSynergyActiveForTeam(SynergyType.Vanar, team, 5)) { ally.AddSynergyDamageReductionBonus(0.1f * hs); ally.AddSynergyPowerBonus(0.2f * hs); }
                if (IsSynergyActiveForTeam(SynergyType.Vanar, team, 7)) { ally.AddSynergyDamageReductionBonus(0.12f * hs); ally.AddSynergyPowerBonus(0.3f * hs); }
            }
            // 全陣営共通：発動段階10＝決定打。全能力を爆発的に強化（ほぼ確定クリア）。
            // 敵が10到達で理不尽にならないよう、この超強化はプレイヤー側のみ。
            foreach (SynergyType ft in applyPlayerOnlyBattleSynergies ? new[] { SynergyType.Lyonar, SynergyType.Songhai, SynergyType.Magmar, SynergyType.Vetruvian, SynergyType.Abyssian, SynergyType.Vanar } : System.Array.Empty<SynergyType>())
            {
                if (ally.HasSynergy(ft) && IsSynergyActiveForTeam(ft, team, 10))
                {
                    ally.synergyDamageDealtBonus += 2.0f;          // 与ダメ+200%
                    ally.AddSynergyDamageReductionBonus(0.5f);     // 被ダメ-50%
                    ally.AddSynergyAttackSpeedBonus(1.0f);         // 攻撃速度+100%
                    ally.AddSynergyPowerBonus(1.0f);               // スキル威力+100%
                    ally.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 1.0f)), 600f); // 最大HP相当シールド
                    break; // 複数陣営10でも1回ぶんで十分強力。
                }
            }
        }

        if (IsSynergyActiveForTeam(SynergyType.Guardian, team, 2))
        {
            for (int i = 0; i < allies.Count; i++)
            {
                BaseEntity ally = allies[i];
                if (ally != null && !ally.IsDead && ally.IsOnBoard)
                    ally.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.05f)), 8f);
            }
        }

        if (applyPlayerOnlyBattleSynergies)
            ApplyNewBattleStartSynergies(allies);
    }

    // 戦闘終了や配置変更時に、一時効果・スタック・復活回数をリセットします。
    public void ClearBattleSynergyState()
    {
        wraithReviveUsedThisBattle = false;
        abyssPanicCurseUsedThisBattle = false;
        divineTeamRescueUsedThisBattle = false;
        frenzyRampageUsedThisBattle = false;
        summonerLargeSummonUsedThisBattle = false;
        stormAttackCounters.Clear();
        royalKing = null;
        if (infernoRainCoroutine != null)
        {
            StopCoroutine(infernoRainCoroutine);
            infernoRainCoroutine = null;
        }

        ClearActiveSummons();
        // D-2: 戦闘間に持ち越さないようにシナジースナップショットも解除します。
        // 戦闘外（編成）では従来どおり盤面実態からのリアルタイム集計に戻ります。
        EndCombatSynergySnapshot();
        BaseEntity[] entities = FindObjectsOfType<BaseEntity>(true);
        for (int i = 0; i < entities.Length; i++)
        {
            if (entities[i] != null)
                entities[i].ClearSynergyBattleState();
        }
    }

    // 死亡直前にWraith6の復活条件を確認します。復活できた場合は死亡処理を止めます。
    public bool TryReviveWraith(BaseEntity entity)
    {
        if (entity == null || wraithReviveUsedThisBattle)
            return false;

        if (!entity.HasSynergy(SynergyType.Wraith) || !IsSynergyActiveForTeam(SynergyType.Wraith, entity.Team, 6))
            return false;

        wraithReviveUsedThisBattle = true;
        entity.ReviveFromSynergy(0.30f);
        return true;
    }

    // ユニット死亡時の、撃破・死亡時シナジー効果をまとめて処理します。
    public void NotifyUnitDeath(BaseEntity deadEntity, BaseEntity killer)
    {
        if (deadEntity == null || GameManager.Instance == null)
            return;

        if (killer != null && killer.IsOnBoard && !killer.IsDead)
            ApplyKillSynergies(killer, deadEntity);

        if (deadEntity.Team != Team.Team1)
        {
            TryTriggerInfernoExplosion(deadEntity, killer);
            return;
        }

        if (deadEntity.IsSummonedUnit)
        {
            NotifySummonDeath(deadEntity);
            return;
        }

        ApplyFrenzyAllyDeathSynergies(deadEntity);

        if (deadEntity == royalKing)
            ApplyRoyalKingDeathPenalty();

        if (deadEntity.HasSynergy(SynergyType.Machine) && IsSynergyActive(SynergyType.Machine, 6))
            ShieldNearbyAlliesOnMachineDeath(deadEntity);

        if (deadEntity.HasSynergy(SynergyType.Wraith))
        {
            if (IsSynergyActive(SynergyType.Wraith, 2))
                SlowNearbyEnemiesOnWraithDeath(deadEntity);

            if (IsSynergyActive(SynergyType.Wraith, 4))
                RestoreNearestAllyMana(deadEntity, 20);
        }
    }

    // Guardian6用です。シールドが割れた時、周囲の敵を短く止めます。
    public void NotifyShieldBroken(BaseEntity shieldOwner)
    {
        if (shieldOwner == null || !IsSynergyActiveForTeam(SynergyType.Guardian, shieldOwner.Team, 6))
            return;

        List<BaseEntity> enemies = GameManager.Instance != null ? GameManager.Instance.GetEntitiesAgainst(shieldOwner.Team) : null;
        if (enemies == null)
            return;

        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.IsOnBoard)
                continue;

            if (Vector3.Distance(enemy.transform.position, shieldOwner.transform.position) <= 1.25f)
                enemy.ApplyStun(0.75f);
        }
    }

    // 通常攻撃が命中した時、発火・凍結・雷撃などの攻撃反応シナジーを処理します。
    public void NotifyNormalAttackHit(BaseEntity attacker, BaseEntity target, int dealtDamage)
    {
        if (attacker == null || target == null || attacker.Team == target.Team || attacker.IsDead || target.IsDead)
            return;

        if (attacker.HasSynergy(SynergyType.Inferno) && IsSynergyActiveForTeam(SynergyType.Inferno, attacker.Team, 2))
            ApplyInfernoBurn(attacker, target, dealtDamage);

        if (attacker.HasSynergy(SynergyType.Frost) && IsSynergyActiveForTeam(SynergyType.Frost, attacker.Team, 2) && UnityEngine.Random.value < 0.20f)
        {
            target.ApplyAttackSpeedSlow(0.68f, 2f);
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position, 0.8f);
            if (IsSynergyActiveForTeam(SynergyType.Frost, attacker.Team, 4))
                target.ApplyFrostStackFromSynergy(1f);
        }

        if (attacker.HasSynergy(SynergyType.Storm) && IsSynergyActiveForTeam(SynergyType.Storm, attacker.Team, 2))
            TickStormAttack(attacker);
    }

    // スキルダメージ命中時は、炎獄の燃焼など「通常攻撃/スキル両対応」の効果だけを処理します。
    public void NotifySkillDamageHit(BaseEntity caster, BaseEntity target, int dealtDamage)
    {
        if (caster == null || target == null || caster.Team == target.Team || caster.IsDead || target.IsDead)
            return;

        if (caster.HasSynergy(SynergyType.Inferno) && IsSynergyActiveForTeam(SynergyType.Inferno, caster.Team, 2))
            ApplyInfernoBurn(caster, target, dealtDamage);
    }

    // スキル発動時に発生する追加効果です。味方は雷鳴、敵は深淵の反撃対象になります。
    public void NotifySkillCast(BaseEntity caster)
    {
        if (caster == null || caster.IsDead)
            return;

        if (caster.HasSynergy(SynergyType.Storm) && IsSynergyActiveForTeam(SynergyType.Storm, caster.Team, 6))
            StrikeRandomEnemiesWithStorm(caster, 3, true);

        Team opposingTeam = caster.Team == Team.Team1 ? Team.Team2 : Team.Team1;
        if (IsSynergyActiveForTeam(SynergyType.Abyss, opposingTeam, 4))
        {
            int curseDamage = Mathf.Max(1, Mathf.RoundToInt(caster.MaxHealth * 0.045f));
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, caster.transform.position, 1.05f);
            caster.TakeDamage(curseDamage, null, CombatNumberKind.FocusDamage);
        }
    }

    // 味方が被弾した後、深淵6や神聖6のチーム全体トリガーを確認します。
    public void NotifyAllyDamaged(BaseEntity damagedAlly)
    {
        if (damagedAlly == null || damagedAlly.Team != Team.Team1 || GameManager.Instance == null)
            return;

        List<BaseEntity> allies = GetLivingPlayerBoardUnits();
        if (allies.Count == 0)
            return;

        float teamHealthRatio = allies.Sum(ally => Mathf.Max(0, ally.CurrentHealth)) / allies.Sum(ally => Mathf.Max(1, ally.MaxHealth));
        if (!divineTeamRescueUsedThisBattle && IsSynergyActive(SynergyType.Divine, 6) && teamHealthRatio <= 0.38f)
        {
            divineTeamRescueUsedThisBattle = true;
            for (int i = 0; i < allies.Count; i++)
            {
                BaseEntity ally = allies[i];
                ally.HealFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.16f)));
                ally.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.10f)), 5f);
            }

            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, damagedAlly.transform.position, 1.45f);
        }

        if (!abyssPanicCurseUsedThisBattle && IsSynergyActive(SynergyType.Abyss, 6) && teamHealthRatio <= 0.28f)
        {
            abyssPanicCurseUsedThisBattle = true;
            List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(Team.Team1);
            for (int i = 0; i < enemies.Count; i++)
            {
                BaseEntity enemy = enemies[i];
                if (enemy == null || enemy.IsDead || !enemy.IsOnBoard)
                    continue;

                enemy.ApplyTimedSynergyDamageDealtBonus(-0.18f, 5f);
                enemy.ApplyAttackSpeedSlow(0.62f, 5f);
                enemy.ApplyTimedSynergyMoveSpeedBonus(-0.30f, 5f);
                enemy.ApplyTimedSynergyManaGainMultiplier(0.50f, 5f);
                AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, enemy.transform.position, 1.1f);
            }
        }
    }

    // ウェーブ終了時の報酬型シナジーです。戦闘後処理で呼ばれます。
    public void NotifyWaveCleared(bool clearedBossWave)
    {
        if (GameManager.Instance == null)
            return;

        if (IsSynergyActive(SynergyType.Alchemy, 2) && UnityEngine.Random.value < 0.20f)
        {
            PlayerData.Instance?.AddMoney(1);
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Alchemy, Vector3.zero, 1f);
        }

        if (IsSynergyActive(SynergyType.Alchemy, 4) && UnityEngine.Random.value < 0.18f)
        {
            GameManager.Instance.GrantRandomItemFromSynergy();
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Alchemy, Vector3.zero, 1.1f);
        }

        if (clearedBossWave && IsSynergyActive(SynergyType.Alchemy, 6))
        {
            GameManager.Instance.GrantRandomItemFromSynergy();
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Alchemy, Vector3.zero, 1.25f);
        }
    }

    private void ApplyNewBattleStartSynergies(List<BaseEntity> allies)
    {
        if (GameManager.Instance == null)
            return;

        if (IsSynergyActive(SynergyType.Frost, 6))
        {
            List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(Team.Team1);
            BaseEntity strongestEnemy = null;
            int strongestDamage = -1;
            for (int i = 0; i < enemies.Count; i++)
            {
                BaseEntity enemy = enemies[i];
                if (enemy == null || enemy.IsDead || !enemy.IsOnBoard)
                    continue;

                enemy.ApplyAttackSpeedSlow(0.68f, 3f);
                AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, enemy.transform.position, 0.9f);
                if (enemy.baseDamage > strongestDamage)
                {
                    strongestDamage = enemy.baseDamage;
                    strongestEnemy = enemy;
                }
            }

            if (strongestEnemy != null)
                strongestEnemy.ApplyStun(2f);
        }

        if (IsSynergyActive(SynergyType.Abyss, 2))
        {
            List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(Team.Team1);
            for (int i = 0; i < enemies.Count; i++)
            {
                BaseEntity enemy = enemies[i];
                if (enemy == null || enemy.IsDead || !enemy.IsOnBoard)
                    continue;

                enemy.ApplyTimedSynergyDamageDealtBonus(-0.10f, 8f);
                AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, enemy.transform.position, 0.8f);
            }
        }

        if (IsSynergyActive(SynergyType.Divine, 2))
        {
            BaseEntity protectedAlly = allies
                .Where(ally => ally != null && !ally.IsDead && ally.IsOnBoard)
                .OrderBy(ally => ally.HealthRatio)
                .ThenBy(ally => ally.MaxHealth)
                .FirstOrDefault();

            if (protectedAlly != null)
            {
                bool tier4 = IsSynergyActive(SynergyType.Divine, 4);
                protectedAlly.ApplyDivineProtectionFromSynergy(tier4 ? 0.25f : 0.12f, tier4);
            }
        }

        ApplyRoyalKingIfNeeded(allies);

        if (IsSynergyActive(SynergyType.Inferno, 6) && infernoRainCoroutine == null)
            infernoRainCoroutine = StartCoroutine(InfernoFireRainAfterDelay(10f));

        if (IsSynergyActive(SynergyType.Summoner, 2))
            SpawnSummon(false);
    }

    private void ApplyRoyalKingIfNeeded(List<BaseEntity> allies)
    {
        if (!IsSynergyActive(SynergyType.Royal, 1) || royalKing != null)
            return;

        royalKing = allies
            .Where(ally => ally != null && !ally.IsDead && ally.IsOnBoard && !ally.IsSummonedUnit)
            .OrderByDescending(ally => ally.BaseCost)
            .ThenByDescending(ally => ally.StarLevel)
            .ThenByDescending(ally => ally.MaxHealth)
            .FirstOrDefault();

        if (royalKing == null)
            return;

        royalKing.synergyDamageDealtBonus += 0.12f;
        royalKing.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(royalKing.MaxHealth * 0.08f)), 8f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Royal, royalKing.transform.position, 1.2f);

        if (IsSynergyActive(SynergyType.Royal, 2))
        {
            for (int i = 0; i < allies.Count; i++)
            {
                BaseEntity ally = allies[i];
                if (ally == null || ally == royalKing || ally.IsDead || !ally.IsOnBoard)
                    continue;

                if (Vector3.Distance(ally.transform.position, royalKing.transform.position) <= 1.35f)
                {
                    ally.synergyDamageDealtBonus += 0.08f;
                    ally.AddSynergyDamageReductionBonus(0.06f);
                }
            }
        }

        if (IsSynergyActive(SynergyType.Royal, 4))
        {
            for (int i = 0; i < allies.Count; i++)
            {
                BaseEntity ally = allies[i];
                if (ally == null || ally.IsDead || !ally.IsOnBoard)
                    continue;

                ally.synergyDamageDealtBonus += 0.08f;
                ally.AddSynergyPowerBonus(0.08f);
                ally.AddSynergyDamageReductionBonus(0.04f);
            }
        }
    }

    private IEnumerator InfernoFireRainAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!IsSynergyActive(SynergyType.Inferno, 6) || GameManager.Instance == null)
        {
            infernoRainCoroutine = null;
            yield break;
        }

        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(Team.Team1);
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.IsOnBoard)
                continue;

            int damage = Mathf.Max(1, Mathf.RoundToInt(enemy.MaxHealth * 0.07f));
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, enemy.transform.position, 1.35f);
            enemy.TakeDamage(damage, null, CombatNumberKind.FocusDamage);
        }

        infernoRainCoroutine = null;
    }

    private void ApplyInfernoBurn(BaseEntity source, BaseEntity target, int dealtDamage)
    {
        int tickDamage = Mathf.Max(1, Mathf.RoundToInt(dealtDamage * 0.12f + source.SkillBasePower * 0.025f));
        target.ApplyInfernoBurnFromSynergy(source, tickDamage, 3f);
    }

    private void TryTriggerInfernoExplosion(BaseEntity deadEnemy, BaseEntity killer)
    {
        BaseEntity source = deadEnemy != null && deadEnemy.InfernoBurnSource != null ? deadEnemy.InfernoBurnSource : killer;
        Team sourceTeam = source != null ? source.Team : Team.Team1;
        if (deadEnemy == null || !deadEnemy.HasActiveInfernoBurn || !IsSynergyActiveForTeam(SynergyType.Inferno, sourceTeam, 4) || GameManager.Instance == null)
            return;

        int explosionDamage = Mathf.Max(1, Mathf.RoundToInt(deadEnemy.MaxHealth * 0.08f));
        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(sourceTeam);
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.IsOnBoard)
                continue;

            if (Vector3.Distance(enemy.transform.position, deadEnemy.transform.position) <= 1.25f)
                enemy.TakeDamage(explosionDamage, source, CombatNumberKind.FocusDamage);
        }

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, deadEnemy.transform.position, 1.4f);
    }

    private void TickStormAttack(BaseEntity attacker)
    {
        if (!stormAttackCounters.ContainsKey(attacker))
            stormAttackCounters[attacker] = 0;

        stormAttackCounters[attacker]++;
        if (stormAttackCounters[attacker] < 4)
            return;

        stormAttackCounters[attacker] = 0;
        StrikeRandomEnemiesWithStorm(attacker, 1, IsSynergyActiveForTeam(SynergyType.Storm, attacker.Team, 4));
    }

    private void StrikeRandomEnemiesWithStorm(BaseEntity source, int strikeCount, bool allowChain)
    {
        if (source == null || GameManager.Instance == null)
            return;

        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(source.Team)
            .Where(enemy => enemy != null && !enemy.IsDead && enemy.IsOnBoard)
            .ToList();
        if (enemies.Count == 0)
            return;

        for (int i = 0; i < strikeCount && enemies.Count > 0; i++)
        {
            BaseEntity target = enemies[UnityEngine.Random.Range(0, enemies.Count)];
            StrikeStormTarget(source, target, allowChain);
            enemies.Remove(target);
        }
    }

    private void StrikeStormTarget(BaseEntity source, BaseEntity target, bool allowChain)
    {
        if (source == null || target == null || target.IsDead)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(source.SkillBasePower * 0.55f + source.baseDamage * 0.40f));
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, target.transform.position, 1f);
        target.TakeDamage(damage, source, CombatNumberKind.FocusDamage);

        if (!allowChain || GameManager.Instance == null)
            return;

        BaseEntity chained = GameManager.Instance.GetEntitiesAgainst(source.Team)
            .Where(enemy => enemy != null && enemy != target && !enemy.IsDead && enemy.IsOnBoard)
            .OrderBy(enemy => Vector3.Distance(enemy.transform.position, target.transform.position))
            .FirstOrDefault(enemy => Vector3.Distance(enemy.transform.position, target.transform.position) <= 2.25f);

        if (chained != null)
        {
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, chained.transform.position, 0.85f);
            chained.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * 0.65f)), source, CombatNumberKind.FocusDamage);
        }
    }

    private void ApplyFrenzyAllyDeathSynergies(BaseEntity deadAlly)
    {
        if (deadAlly == null || GameManager.Instance == null || !IsSynergyActive(SynergyType.Frenzy, 2))
            return;

        List<BaseEntity> allies = GetLivingPlayerBoardUnits();
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (ally.HasSynergy(SynergyType.Frenzy))
            {
                ally.AddSynergyAttackSpeedBonus(0.10f);
                AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frenzy, ally.transform.position, 0.85f);
            }
        }

        if (!frenzyRampageUsedThisBattle && IsSynergyActive(SynergyType.Frenzy, 6))
        {
            frenzyRampageUsedThisBattle = true;
            for (int i = 0; i < allies.Count; i++)
            {
                BaseEntity ally = allies[i];
                if (!ally.HasSynergy(SynergyType.Frenzy))
                    continue;

                ally.ApplyAttackSpeedBoostFromSynergy(1.55f, 5f);
                ally.ApplyTimedSynergyDamageDealtBonus(0.22f, 5f);
                ally.ApplyTimedSynergyDamageReductionBonus(-0.20f, 5f);
                AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frenzy, ally.transform.position, 1.15f);
            }
        }
    }

    private void ApplyRoyalKingDeathPenalty()
    {
        if (!IsSynergyActive(SynergyType.Royal, 4) || GameManager.Instance == null)
            return;

        List<BaseEntity> allies = GetLivingPlayerBoardUnits();
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            ally.ApplyTimedSynergyDamageDealtBonus(-0.15f, 60f);
            ally.ApplyTimedSynergyMoveSpeedBonus(-0.15f, 60f);
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Royal, ally.transform.position, 0.75f);
        }
    }

    private void SpawnSummon(bool large)
    {
        if (GameManager.Instance == null)
            return;

        BaseEntity summon = GameManager.Instance.SpawnTemporarySummonFromSynergy(large);
        if (summon == null)
            return;

        activeSummons.Add(summon);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Summoner, summon.transform.position, large ? 1.35f : 1f);

        // prism_summon_master: 召喚体を +1（追加分は小サイズ）
        if (GameManager.Instance.HasAugment("prism_summon_master"))
        {
            BaseEntity extra = GameManager.Instance.SpawnTemporarySummonFromSynergy(false);
            if (extra != null)
            {
                activeSummons.Add(extra);
                AttackEffectPlayer.PlaySynergyEffect(SynergyType.Summoner, extra.transform.position, 1f);
            }
        }
    }

    private void NotifySummonDeath(BaseEntity summon)
    {
        activeSummons.Remove(summon);
        if (IsSynergyActive(SynergyType.Summoner, 4) && GameManager.Instance != null)
        {
            List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(Team.Team1);
            for (int i = 0; i < enemies.Count; i++)
            {
                BaseEntity enemy = enemies[i];
                if (enemy == null || enemy.IsDead || !enemy.IsOnBoard)
                    continue;

                if (Vector3.Distance(enemy.transform.position, summon.transform.position) <= 1.35f)
                {
                    enemy.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(enemy.MaxHealth * 0.04f)), null, CombatNumberKind.FocusDamage);
                    enemy.ApplyAttackSpeedSlow(0.70f, 2f);
                }
            }

            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Summoner, summon.transform.position, 1.1f);
        }

        activeSummons.RemoveAll(entity => entity == null || entity.IsDead);
        if (!summonerLargeSummonUsedThisBattle && IsSynergyActive(SynergyType.Summoner, 6) && activeSummons.Count == 0)
        {
            summonerLargeSummonUsedThisBattle = true;
            SpawnSummon(true);
        }
    }

    private void ClearActiveSummons()
    {
        if (GameManager.Instance == null)
        {
            activeSummons.Clear();
            return;
        }

        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            BaseEntity summon = activeSummons[i];
            if (summon != null)
                GameManager.Instance.RemoveTemporarySummonFromSynergy(summon);
        }

        activeSummons.Clear();
    }

    private List<BaseEntity> GetLivingPlayerBoardUnits()
    {
        return GameManager.Instance != null
            ? GameManager.Instance.GetPlayerBoardEntitiesForSynergy()
                .Where(entity => entity != null && !entity.IsDead && entity.IsOnBoard && !entity.IsSummonedUnit)
                .ToList()
            : new List<BaseEntity>();
    }

    // 表示用に、シナジーの現在状態を1つのテキストへまとめます。
    public string BuildDisplayText()
    {
        List<string> lines = new List<string>();
        for (int i = 0; i < CountedSynergies.Length; i++)
        {
            SynergyType type = CountedSynergies[i];
            int count = GetSynergyCount(type);
            if (count <= 0)
                continue;

            int tier = GetSynergyTier(type);
            int next = GetNextRequiredCount(type, tier);
            lines.Add(BuildDisplayLine(type, count, tier, next));
        }

        if (lines.Count == 0)
            return LocalizationManager.IsJapanese ? "シナジーなし" : "No synergies";

        return string.Join("\n", lines);
    }

    // アイコン付きUIの1行テキストとして使える、現在のシナジー状態を返します。
    public string BuildDisplayLine(SynergyType type)
    {
        int count = GetSynergyCount(type);
        int tier = GetSynergyTier(type);
        int next = GetNextRequiredCount(type, tier);
        return BuildDisplayLine(type, count, tier, next);
    }

    // アイコン中心の小さなUIから、次に目指す必要数を確認するための表示用値です。
    public int GetNextRequiredCountForDisplay(SynergyType type)
    {
        return GetNextRequiredCount(type, GetSynergyTier(type));
    }

    private string BuildDisplayLine(SynergyType type, int count, int tier, int next)
    {
        string name = LocalizationManager.SynergyName(type);
        string effect = GetSynergySummary(type, tier, next);
        return $"{name} {count}/{next}  {effect}";
    }

    private void InitializeDictionaries()
    {
        for (int i = 0; i < CountedSynergies.Length; i++)
        {
            synergyCounts[CountedSynergies[i]] = 0;
            activeTiers[CountedSynergies[i]] = 0;
            enemySynergyCounts[CountedSynergies[i]] = 0;
            enemyActiveTiers[CountedSynergies[i]] = 0;
        }
    }

    // 指定チームの盤面ユニットだけを、同名重複なしでシナジーカウントします。
    // 戦闘中（combatSynergySnapshotActive）は、戦闘開始時に取った編成スナップショットを使い、
    // 死亡しても数が減らないようにします。戦闘外は従来どおり盤面からリアルタイムに数えます。
    private void CountSynergiesForTeam(Team team, Dictionary<SynergyType, int> counts, Dictionary<SynergyType, int> tiers)
    {
        if (counts == null || tiers == null || GameManager.Instance == null)
            return;

        // prism_all_synergy: プレイヤーチームのみ、各ユニットの所持シナジーに +1 重ね掛けします。
        bool perUnitDouble = team == Team.Team1 && GameManager.Instance.HasAugment("prism_all_synergy");

        if (combatSynergySnapshotActive)
        {
            List<CombatSynergyEntry> snapshot = team == Team.Team1 ? team1CombatSnapshot : team2CombatSnapshot;
            for (int i = 0; i < snapshot.Count; i++)
            {
                List<SynergyType> unitSynergies = snapshot[i].Synergies;
                if (unitSynergies == null)
                    continue;
                for (int synergyIndex = 0; synergyIndex < unitSynergies.Count; synergyIndex++)
                {
                    SynergyType type = unitSynergies[synergyIndex];
                    if (type == SynergyType.None)
                        continue;
                    counts[type] += perUnitDouble ? 2 : 1;
                }
            }
        }
        else
        {
            HashSet<string> countedUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<BaseEntity> boardUnits = GameManager.Instance.GetBoardEntitiesForSynergy(team);
            for (int i = 0; i < boardUnits.Count; i++)
            {
                BaseEntity entity = boardUnits[i];
                if (entity == null || entity.Team != team || !entity.IsOnBoard || entity.IsDead || entity.IsSummonedUnit)
                    continue;

                string unitKey = LocalizationManager.CleanUnitName(entity.UnitId);
                if (!countedUnitIds.Add(unitKey))
                    continue;

                List<SynergyType> unitSynergies = entity.GetSynergyTypes();
                for (int synergyIndex = 0; synergyIndex < unitSynergies.Count; synergyIndex++)
                {
                    SynergyType type = unitSynergies[synergyIndex];
                    if (type == SynergyType.None)
                        continue;

                    counts[type] += perUnitDouble ? 2 : 1;
                }
            }
        }

        for (int i = 0; i < CountedSynergies.Length; i++)
        {
            SynergyType type = CountedSynergies[i];
            // プレイヤーチームは、表示カウント(GetSynergyCount)と同じくオーグメント加算込みで発動段階を決める
            //（エンブレム＋戦闘中ランダム+1）。これで「表示は4なのに効果が未発動」を解消。
            int effective = counts[type] + (team == Team.Team1 ? PlayerSynergyAugmentBonus(type) : 0);
            // R3-factions: 陣営シナジーの「1体目(1〜2体)」は主人公が同陣営の時だけ発動させる。
            // 主人公が同陣営でないなら 1〜2体を 0 扱いにし、3体から（=従来の3段階目から）発動する。Team1のみ。
            if (team == Team.Team1 && IsFactionSynergy(type) && effective > 0 && effective <= 2
                && (GameManager.Instance == null || !GameManager.Instance.ActiveHeroHasSynergy(type)))
                effective = 0;
            tiers[type] = ResolveTier(type, effective);
        }
    }

    // 戦闘開始直前に、両チームの盤面ユニットからシナジー編成をスナップショットして固定します。
    // 召喚体・死亡ユニットは対象外（戦闘前の盤面状態のみ）。同名重複は除外。
    private void BeginCombatSynergySnapshot()
    {
        team1CombatSnapshot.Clear();
        team2CombatSnapshot.Clear();
        if (GameManager.Instance != null)
        {
            SnapshotTeamInto(Team.Team1, team1CombatSnapshot);
            SnapshotTeamInto(Team.Team2, team2CombatSnapshot);
        }
        combatSynergySnapshotActive = true;
    }

    private void SnapshotTeamInto(Team team, List<CombatSynergyEntry> dest)
    {
        HashSet<string> countedUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<BaseEntity> boardUnits = GameManager.Instance.GetBoardEntitiesForSynergy(team);
        for (int i = 0; i < boardUnits.Count; i++)
        {
            BaseEntity entity = boardUnits[i];
            if (entity == null || entity.Team != team || !entity.IsOnBoard || entity.IsDead || entity.IsSummonedUnit)
                continue;

            string unitKey = LocalizationManager.CleanUnitName(entity.UnitId);
            if (!countedUnitIds.Add(unitKey))
                continue;

            // GetSynergyTypes() を毎フレーム呼ぶ実装でも、スナップショット時点のコピーを保持します。
            dest.Add(new CombatSynergyEntry
            {
                UnitKey = unitKey,
                Synergies = new List<SynergyType>(entity.GetSynergyTypes())
            });
        }
    }

    private void EndCombatSynergySnapshot()
    {
        combatSynergySnapshotActive = false;
        team1CombatSnapshot.Clear();
        team2CombatSnapshot.Clear();
    }

    // 陣営シナジーか（発動段階 1/3/5/7/10 の特別カーブ）。
    public static bool IsFactionSynergy(SynergyType type)
    {
        return type == SynergyType.Lyonar || type == SynergyType.Songhai || type == SynergyType.Magmar
            || type == SynergyType.Vetruvian || type == SynergyType.Abyssian || type == SynergyType.Vanar;
    }

    // R3-factions: 主人公が同陣営の時、その陣営シナジー効果を強化する倍率。Team1のみ。
    public const float FactionHeroBonusScale = 1.5f;
    private float FactionHeroScale(SynergyType type, Team team)
    {
        return (team == Team.Team1 && GameManager.Instance != null && GameManager.Instance.ActiveHeroHasSynergy(type))
            ? FactionHeroBonusScale : 1f;
    }

    private int ResolveTier(SynergyType type, int count)
    {
        // 陣営シナジーは 1/3/5/7/10 で段階発動。10は決定打。
        if (IsFactionSynergy(type))
        {
            if (count >= 10) return 10;
            if (count >= 7) return 7;
            if (count >= 5) return 5;
            if (count >= 3) return 3;
            if (count >= 1) return 1;
            return 0;
        }

        if (type == SynergyType.Apex)
        {
            if (count >= 3)
                return 3;
            if (count >= 2)
                return 2;
            if (count >= 1)
                return 1;
            return 0;
        }

        if (type == SynergyType.Royal)
        {
            if (count >= 4)
                return 4;
            if (count >= 2)
                return 2;
            if (count >= 1)
                return 1;
            return 0;
        }

        if (type == SynergyType.Shadow)
            return count >= 4 ? 4 : count >= 2 ? 2 : 0;

        // 終焉はアルカナ専用。1体で常時発動。
        if (type == SynergyType.Finality)
            return count >= 1 ? 1 : 0;

        if (count >= 6)
            return 6;
        if (count >= 4)
            return 4;
        if (count >= 2)
            return 2;
        return 0;
    }

    private int GetNextRequiredCount(SynergyType type, int tier)
    {
        if (IsFactionSynergy(type))
        {
            if (tier >= 10) return 10;
            if (tier >= 7) return 10;
            if (tier >= 5) return 7;
            if (tier >= 3) return 5;
            if (tier >= 1) return 3;
            return 1;
        }

        if (type == SynergyType.Apex)
        {
            if (tier >= 3)
                return 3;
            if (tier >= 2)
                return 3;
            if (tier >= 1)
                return 2;
            return 1;
        }

        if (type == SynergyType.Royal)
        {
            if (tier >= 4)
                return 4;
            if (tier >= 2)
                return 4;
            if (tier >= 1)
                return 2;
            return 1;
        }

        if (type == SynergyType.Shadow)
            return tier >= 4 ? 4 : tier >= 2 ? 4 : 2;

        // 終焉は1体で発動済み。次の閾値も1のまま（実質1/1表示）。
        if (type == SynergyType.Finality)
            return 1;

        if (tier >= 6)
            return 6;
        if (tier >= 4)
            return 6;
        if (tier >= 2)
            return 4;
        return 2;
    }

    private string GetSynergySummary(SynergyType type, int tier, int next)
    {
        bool ja = LocalizationManager.IsJapanese;
        if (tier <= 0)
            return ja ? $"次: {GetTierSummary(type, next)}" : $"Next: {GetTierSummary(type, next)}";

        List<string> active = new List<string>();
        if (type == SynergyType.Apex)
        {
            if (tier >= 1)
                active.Add(GetTierSummary(type, 1));
            if (tier >= 2)
                active.Add(GetTierSummary(type, 2));
            if (tier >= 3)
                active.Add(GetTierSummary(type, 3));
        }
        else if (type == SynergyType.Royal)
        {
            if (tier >= 1)
                active.Add(GetTierSummary(type, 1));
            if (tier >= 2)
                active.Add(GetTierSummary(type, 2));
            if (tier >= 4)
                active.Add(GetTierSummary(type, 4));
        }
        else if (IsFactionSynergy(type))
        {
            int[] th = { 1, 3, 5, 7, 10 };
            for (int i = 0; i < th.Length; i++)
                if (tier >= th[i]) active.Add(GetTierSummary(type, th[i]));
        }
        else
        {
            if (tier >= 2)
                active.Add(GetTierSummary(type, 2));
            if (tier >= 4)
                active.Add(GetTierSummary(type, 4));
            if (tier >= 6)
                active.Add(GetTierSummary(type, 6));
        }

        string body = ja ? $"発動中: {string.Join(" / ", active)}" : $"Active: {string.Join(" / ", active)}";
        if (IsFactionSynergy(type))
            body += ja
                ? "\n※1体目は主人公が同陣営の時のみ発動（3体以上は不問）。主人公が同陣営なら効果+50%。"
                : "\n* 1st tier needs a same-faction hero (3+ always works). Same-faction hero: +50% effect.";
        return body;
    }

    private string GetTierSummary(SynergyType type, int requiredCount)
    {
        bool ja = LocalizationManager.IsJapanese;
        switch (type)
        {
            case SynergyType.Warrior:
                if (requiredCount == 2) return ja ? "軽減+8%" : "Damage taken -8%";
                if (requiredCount == 4) return ja ? "撃破時回復" : "Heal on kill";
                return ja ? "瀕死時防御" : "Low HP guard";
            case SynergyType.Ranger:
                if (requiredCount == 2) return ja ? "攻撃速度+10%" : "Attack speed +10%";
                if (requiredCount == 4) return ja ? "同対象へ火力上昇" : "Focus fire damage";
                return ja ? "追加弾" : "Extra shot";
            case SynergyType.Arcanist:
                if (requiredCount == 2) return ja ? "秘力+15%" : "Focus +15%";
                if (requiredCount == 4) return ja ? "スキル後MP+20" : "MP after skill";
                return ja ? "開幕MP+40" : "Start MP +40";
            case SynergyType.Guardian:
                if (requiredCount == 2) return ja ? "味方全体シールド" : "Team shield";
                if (requiredCount == 4) return ja ? "シールド中軽減" : "Shielded reduction";
                return ja ? "シールド破壊時スタン" : "Shield break stun";
            case SynergyType.Beast:
                if (requiredCount == 2) return ja ? "攻撃速度スタック" : "Attack speed stacks";
                if (requiredCount == 4) return ja ? "最大時追加ダメージ" : "Max stack damage";
                return ja ? "最大時自己回復" : "Max stack heal";
            case SynergyType.Shadow:
                if (requiredCount == 2) return ja ? "弱った敵へ火力" : "Execute damage";
                return ja ? "撃破時加速" : "Kill haste";
            case SynergyType.Machine:
                if (requiredCount == 2) return ja ? "開幕軽減" : "Start armor";
                if (requiredCount == 4) return ja ? "瀕死時回復" : "Low HP repair";
                return ja ? "死亡時味方シールド" : "Death shields";
            case SynergyType.Wraith:
                if (requiredCount == 2) return ja ? "死亡時スロウ" : "Death slow";
                if (requiredCount == 4) return ja ? "死亡時味方MP" : "Death MP";
                return ja ? "一度だけ復活" : "One revive";
            case SynergyType.Apex:
                if (requiredCount == 1) return ja ? "与ダメージ+10%" : "Damage +10%";
                if (requiredCount == 2) return ja ? "秘力+20%/軽減+8%" : "Focus +20% / Guard";
                return ja ? "開幕シールド" : "Start shield";
            case SynergyType.Inferno:
                if (requiredCount == 2) return ja ? "攻撃/スキルで燃焼" : "Burn on hit";
                if (requiredCount == 4) return ja ? "燃焼敵が爆発" : "Burn death explosion";
                return ja ? "開幕10秒後に火雨" : "Fire rain after 10s";
            case SynergyType.Frost:
                if (requiredCount == 2) return ja ? "攻撃時スロウ" : "Slow on hit";
                if (requiredCount == 4) return ja ? "スロウ敵を凍結" : "Freeze slowed targets";
                return ja ? "開幕全体スロウ" : "Opening team slow";
            case SynergyType.Storm:
                if (requiredCount == 2) return ja ? "4回ごと雷撃" : "Lightning every 4 attacks";
                if (requiredCount == 4) return ja ? "雷撃が連鎖" : "Lightning chains";
                return ja ? "スキル時3体雷撃" : "Skill casts strike 3";
            case SynergyType.Abyss:
                if (requiredCount == 2) return ja ? "敵全体攻撃低下" : "Enemy damage down";
                if (requiredCount == 4) return ja ? "敵スキルに反撃" : "Punish enemy casts";
                return ja ? "劣勢時に全体呪い" : "Panic team curse";
            case SynergyType.Divine:
                if (requiredCount == 2) return ja ? "低HP味方へ復活加護" : "Revive ward lowest ally";
                if (requiredCount == 4) return ja ? "復活時周囲回復" : "Revive heals nearby";
                return ja ? "劣勢時全体回復" : "Team rescue heal";
            case SynergyType.Frenzy:
                if (requiredCount == 2) return ja ? "味方死亡で加速" : "Ally deaths grant haste";
                if (requiredCount == 4) return ja ? "低HPほど火力" : "Low HP damage";
                return ja ? "一度だけ暴走" : "One rampage";
            case SynergyType.Royal:
                if (requiredCount == 1) return ja ? "王を強化" : "Empower a king";
                if (requiredCount == 2) return ja ? "王の周囲を強化" : "Buff king's guard";
                return ja ? "王生存中全体強化" : "Team buff while king lives";
            case SynergyType.Summoner:
                if (requiredCount == 2) return ja ? "開幕小型召喚" : "Summon a minion";
                if (requiredCount == 4) return ja ? "召喚死亡時爆発" : "Summon death blast";
                return ja ? "全滅時大型召喚" : "Call a large summon";
            case SynergyType.Alchemy:
                if (requiredCount == 2) return ja ? "Wave報酬コイン" : "Wave coin chance";
                if (requiredCount == 4) return ja ? "Wave報酬アイテム抽選" : "Wave item chance";
                return ja ? "ボス後追加アイテム" : "Boss reward item";
            case SynergyType.Finality:
                return ja ? "BloodMage1体ごとにスキル+15%＆マナ回収増" : "+15% skill power & mana per BloodMage";
            // 陣営シナジー（発動段階 1/3/5/7/10。10は決定打）。
            case SynergyType.Lyonar: // 守護の聖光（被ダメ減＋シールド）
                if (requiredCount == 1) return ja ? "被ダメ-8%（主人公が同陣営のみ）" : "DR -8% (faction hero only)";
                if (requiredCount == 3) return ja ? "被ダメ-16%＋開幕シールド" : "DR -16% + shield";
                if (requiredCount == 5) return ja ? "被ダメ-24%＋シールド大" : "DR -24% + big shield";
                if (requiredCount == 7) return ja ? "被ダメ-34%＋シールド特大" : "DR -34% + huge shield";
                return ja ? "聖光の加護：全能力激増＝ほぼ無敵" : "Divine blessing: massive all-stats";
            case SynergyType.Songhai: // 影の速攻（攻撃速度）
                if (requiredCount == 1) return ja ? "攻撃速度+10%（主人公が同陣営のみ）" : "ATKSPD +10% (faction hero only)";
                if (requiredCount == 3) return ja ? "攻撃速度+24%" : "ATKSPD +24%";
                if (requiredCount == 5) return ja ? "攻速+与ダメ強化" : "ATKSPD + DMG";
                if (requiredCount == 7) return ja ? "圧倒的手数" : "Overwhelming flurry";
                return ja ? "神速：全能力激増＝ほぼ確定" : "Godspeed: massive all-stats";
            case SynergyType.Magmar: // 原始の猛攻（与ダメ）
                if (requiredCount == 1) return ja ? "与ダメ+12%（主人公が同陣営のみ）" : "DMG +12% (faction hero only)";
                if (requiredCount == 3) return ja ? "与ダメ+26%" : "DMG +26%";
                if (requiredCount == 5) return ja ? "与ダメ＋攻速" : "DMG + ATKSPD";
                if (requiredCount == 7) return ja ? "獣性解放" : "Unleashed fury";
                return ja ? "原初の覇王：全能力激増＝ほぼ確定" : "Primal apex: massive all-stats";
            case SynergyType.Vetruvian: // 機巧と太陽（スキル威力＋マナ）
                if (requiredCount == 1) return ja ? "スキル威力+15%（主人公が同陣営のみ）" : "Skill +15% (faction hero only)";
                if (requiredCount == 3) return ja ? "スキル威力+28%＋開幕マナ" : "Skill +28% + mana";
                if (requiredCount == 5) return ja ? "スキル威力大＋マナ" : "Big skill + mana";
                if (requiredCount == 7) return ja ? "灼熱の術式" : "Blazing artifice";
                return ja ? "太陽の威光：全能力激増＝ほぼ確定" : "Solar majesty: massive all-stats";
            case SynergyType.Abyssian: // 死闘（与ダメ＋被ダメ減）
                if (requiredCount == 1) return ja ? "与ダメ+12%（主人公が同陣営のみ）" : "DMG +12% (faction hero only)";
                if (requiredCount == 3) return ja ? "与ダメ+22%＋被ダメ-6%" : "DMG +22% + DR -6%";
                if (requiredCount == 5) return ja ? "与ダメ＋耐久" : "DMG + bulk";
                if (requiredCount == 7) return ja ? "不死の死闘" : "Undying struggle";
                return ja ? "深淵の支配：全能力激増＝ほぼ確定" : "Abyssal dominion: massive all-stats";
            case SynergyType.Vanar: // 氷の堅守（被ダメ減＋スキル威力）
                if (requiredCount == 1) return ja ? "被ダメ-10%（主人公が同陣営のみ）" : "DR -10% (faction hero only)";
                if (requiredCount == 3) return ja ? "被ダメ-18%＋スキル威力" : "DR -18% + skill";
                if (requiredCount == 5) return ja ? "耐久＋スキル威力大" : "Bulk + big skill";
                if (requiredCount == 7) return ja ? "凍てつく要塞" : "Frozen fortress";
                return ja ? "永久氷河：全能力激増＝ほぼ確定" : "Eternal glacier: massive all-stats";
            default:
                return string.Empty;
        }
    }

    private void ApplyKillSynergies(BaseEntity killer, BaseEntity defeated)
    {
        if (killer.HasSynergy(SynergyType.Warrior) && IsSynergyActiveForTeam(SynergyType.Warrior, killer.Team, 4))
            killer.HealFromSynergy(Mathf.Max(1, Mathf.RoundToInt(killer.MaxHealth * 0.08f)));

        if (killer.HasSynergy(SynergyType.Shadow) && IsSynergyActiveForTeam(SynergyType.Shadow, killer.Team, 4))
        {
            killer.ApplyAttackSpeedBoostFromSynergy(1.30f, 2f);
            killer.ApplyTimedSynergyMoveSpeedBonus(0.30f, 2f);
        }
    }

    private void ShieldNearbyAlliesOnMachineDeath(BaseEntity deadEntity)
    {
        List<BaseEntity> allies = GameManager.Instance.GetBoardEntitiesForSynergy(deadEntity.Team);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (ally == null || ally == deadEntity || ally.IsDead || !ally.IsOnBoard)
                continue;

            if (Vector3.Distance(ally.transform.position, deadEntity.transform.position) <= 1.25f)
                ally.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.10f)), 5f);
        }
    }

    private void SlowNearbyEnemiesOnWraithDeath(BaseEntity deadEntity)
    {
        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(deadEntity.Team);
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.IsOnBoard)
                continue;

            if (Vector3.Distance(enemy.transform.position, deadEntity.transform.position) <= 1.25f)
                enemy.ApplyAttackSpeedSlow(0.65f, 2f);
        }
    }

    private void RestoreNearestAllyMana(BaseEntity deadEntity, int amount)
    {
        List<BaseEntity> allies = GameManager.Instance.GetBoardEntitiesForSynergy(deadEntity.Team);
        BaseEntity nearest = null;
        float nearestDistance = Mathf.Infinity;
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (ally == null || ally == deadEntity || ally.IsDead || !ally.IsOnBoard)
                continue;

            float distance = Vector3.Distance(ally.transform.position, deadEntity.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = ally;
            }
        }

        if (nearest != null)
            nearest.GainManaFromSynergy(amount);
    }

    // Editor拡張から、現在の推奨割り当てをEntity Databaseへ反映するための入口です。
    public static List<SynergyType> GetDefaultSynergiesForUnit(string unitName, BaseEntity prefab)
    {
        ResolveDefaultSynergies(unitName, prefab, out SynergyType first, out SynergyType second, out SynergyType third);
        List<SynergyType> synergies = new List<SynergyType>(3);
        AddUniqueSynergy(synergies, first);
        AddUniqueSynergy(synergies, second);
        AddUniqueSynergy(synergies, third);
        return synergies;
    }

    private void LogDebugState()
    {
        List<string> activeLines = new List<string>();
        for (int i = 0; i < CountedSynergies.Length; i++)
        {
            SynergyType type = CountedSynergies[i];
            Debug.Log($"[Synergy] {type} count = {GetSynergyCount(type)}, tier = {GetSynergyTier(type)}");
            if (GetSynergyTier(type) > 0)
                activeLines.Add($"{type} {GetSynergyTier(type)}");
        }

        Debug.Log($"[Synergy] Active: {string.Join(", ", activeLines)}");
    }

    private static void ResolveDefaultSynergies(string unitName, BaseEntity prefab, out SynergyType first, out SynergyType second, out SynergyType third)
    {
        first = SynergyType.None;
        second = SynergyType.None;
        third = SynergyType.None;

        string id = NormalizeUnitId(unitName);
        switch (id)
        {
            case "andromeda": first = SynergyType.Ranger; second = SynergyType.Storm; return;
            case "antiswarm": first = SynergyType.Beast; second = SynergyType.Summoner; return;
            case "borealjuggernaut": first = SynergyType.Guardian; second = SynergyType.Frost; return;
            case "chaosknight": first = SynergyType.Shadow; second = SynergyType.Abyss; return;
            case "christmas": first = SynergyType.Warrior; second = SynergyType.Frenzy; return;
            case "vampire": first = SynergyType.Arcanist; second = SynergyType.Wraith; third = SynergyType.Abyss; return;
            case "valiant": first = SynergyType.Guardian; second = SynergyType.Divine; return;
            case "candypanda": first = SynergyType.Beast; second = SynergyType.Divine; return;
            case "city": first = SynergyType.Machine; second = SynergyType.Alchemy; return;
            case "crystal": first = SynergyType.Arcanist; second = SynergyType.Frost; return;
            case "cindera": first = SynergyType.Arcanist; second = SynergyType.Inferno; third = SynergyType.Storm; return;
            case "decepticle": first = SynergyType.Machine; second = SynergyType.Alchemy; return;
            case "umbra": first = SynergyType.Beast; second = SynergyType.Wraith; third = SynergyType.Abyss; return;
            case "spelleater": first = SynergyType.Arcanist; second = SynergyType.Abyss; return;
            case "serpenti": first = SynergyType.Beast; second = SynergyType.Shadow; third = SynergyType.Frenzy; return;
            case "skindogehai": first = SynergyType.Shadow; second = SynergyType.Beast; third = SynergyType.Frenzy; return;
            case "decepticleprime": first = SynergyType.Machine; second = SynergyType.Ranger; third = SynergyType.Storm; return;
            case "decepticlechassis": first = SynergyType.Machine; second = SynergyType.Guardian; third = SynergyType.Alchemy; return;
            case "wolfpunch": first = SynergyType.Beast; second = SynergyType.Guardian; return;
            case "shadowlord": first = SynergyType.Shadow; second = SynergyType.Wraith; third = SynergyType.Abyss; return;
            case "tier2general": first = SynergyType.Warrior; second = SynergyType.Royal; return;
            case "snowchasermk": first = SynergyType.Frost; second = SynergyType.Divine; return;
            case "solfist": first = SynergyType.Inferno; second = SynergyType.Warrior; third = SynergyType.Divine; return;
            case "maehvmk": first = SynergyType.Machine; second = SynergyType.Arcanist; third = SynergyType.Storm; return;
            case "archdeacon": first = SynergyType.Divine; second = SynergyType.Arcanist; third = SynergyType.Royal; return;
            case "backlinearcher": first = SynergyType.Ranger; second = SynergyType.Shadow; return;
            case "auroralioness": first = SynergyType.Divine; second = SynergyType.Royal; return;
            case "azuritelion": first = SynergyType.Beast; second = SynergyType.Frost; third = SynergyType.Guardian; return;
            case "sandpanther": first = SynergyType.Shadow; second = SynergyType.Beast; return;
            case "protector": first = SynergyType.Guardian; second = SynergyType.Royal; return;
            case "taskmaster": first = SynergyType.Warrior; second = SynergyType.Shadow; third = SynergyType.Frenzy; return;
            case "kane": first = SynergyType.Machine; second = SynergyType.Storm; return;
            case "malyk": first = SynergyType.Arcanist; second = SynergyType.Wraith; third = SynergyType.Abyss; return;
            case "paragon": first = SynergyType.Guardian; second = SynergyType.Divine; third = SynergyType.Royal; return;
            case "wujin": first = SynergyType.Warrior; second = SynergyType.Inferno; third = SynergyType.Royal; return;
            case "wraith": first = SynergyType.Wraith; second = SynergyType.Abyss; third = SynergyType.Frost; return;
            // Magmar(f5) 将3体。陣営シナジー Magmar ＋テーマ2種。
            case "magmarvaath": first = SynergyType.Magmar; second = SynergyType.Frenzy; third = SynergyType.Inferno; return;
            case "magmarstarhorn": first = SynergyType.Magmar; second = SynergyType.Storm; third = SynergyType.Warrior; return;
            case "magmarragnora": first = SynergyType.Magmar; second = SynergyType.Inferno; third = SynergyType.Summoner; return;
            // Abyssian(f4) 将3体。陣営シナジー Abyssian ＋テーマ2種。
            case "abyssallilithe": first = SynergyType.Abyssian; second = SynergyType.Abyss; third = SynergyType.Summoner; return;
            case "abyssalcassyva": first = SynergyType.Abyssian; second = SynergyType.Wraith; third = SynergyType.Abyss; return;
            case "abyssalmaehv": first = SynergyType.Abyssian; second = SynergyType.Abyss; third = SynergyType.Frenzy; return;
            // Vetruvian(f3) 将3体。陣営シナジー Vetruvian ＋テーマ2種。
            case "vetruvianzirix": first = SynergyType.Vetruvian; second = SynergyType.Machine; third = SynergyType.Royal; return;
            case "vetruviansajj": first = SynergyType.Vetruvian; second = SynergyType.Arcanist; third = SynergyType.Storm; return;
            case "vetruvianscion": first = SynergyType.Vetruvian; second = SynergyType.Divine; third = SynergyType.Arcanist; return;
            case "altgeneraltier2": first = SynergyType.Frost; second = SynergyType.Inferno; return;
            case "ilenamk2": first = SynergyType.Frost; second = SynergyType.Arcanist; third = SynergyType.Divine; return;
            case "embergeneral": first = SynergyType.Inferno; second = SynergyType.Warrior; third = SynergyType.Royal; return;
            case "plaguegeneral": first = SynergyType.Alchemy; second = SynergyType.Frenzy; third = SynergyType.Inferno; return;
            case "skyfalltyrant": first = SynergyType.Apex; second = SynergyType.Inferno; return;
            case "kron": first = SynergyType.Apex; second = SynergyType.Machine; third = SynergyType.Storm; return;
            case "gol": first = SynergyType.Guardian; second = SynergyType.Alchemy; return;
            case "invader": first = SynergyType.Machine; second = SynergyType.Abyss; third = SynergyType.Alchemy; return;
            case "legion": first = SynergyType.Apex; second = SynergyType.Warrior; third = SynergyType.Frenzy; return;
            // cost4 追加5体（DESIGN_cost4-units.md）
            case "grymbeast": first = SynergyType.Shadow; second = SynergyType.Beast; third = SynergyType.Abyss; return;
            case "cinderwraith": first = SynergyType.Inferno; second = SynergyType.Wraith; third = SynergyType.Frenzy; return;
            case "draugarlord": first = SynergyType.Frost; second = SynergyType.Machine; third = SynergyType.Guardian; return;
            case "kingsguard": first = SynergyType.Divine; second = SynergyType.Royal; third = SynergyType.Guardian; return;
            case "dissonance": first = SynergyType.Arcanist; second = SynergyType.Storm; third = SynergyType.Summoner; return;
            // 章1ボス Caliber-O（Lyonar 聖騎士将）。
            case "caliber": first = SynergyType.Divine; second = SynergyType.Royal; third = SynergyType.Warrior; return;
            // Songhai 追加4体（陣営を7体に拡充）。陣営=Songhai＋テーマ2種。
            case "lanternfox": first = SynergyType.Songhai; second = SynergyType.Ranger; third = SynergyType.Arcanist; return;
            case "onyxjaguar": first = SynergyType.Songhai; second = SynergyType.Beast; third = SynergyType.Shadow; return;
            case "keshraifanblade": first = SynergyType.Songhai; second = SynergyType.Warrior; third = SynergyType.Shadow; return;
            case "firewyrm": first = SynergyType.Songhai; second = SynergyType.Inferno; third = SynergyType.Beast; return;
            // ヒーロー専用3体（DESIGN_R3-hero-units）。Aldin=聖騎士守護 / Kagachi=アサシン / Vesna=蒼炎。
            // 基本3ヒーローにも陣営シナジーを付与（既存1つと入替）。Aldin:Royal→Lyonar / Kagachi:Wraith→Songhai / Vesna:Arcanist→Vanar。
            case "heroaldin": first = SynergyType.Lyonar; second = SynergyType.Divine; third = SynergyType.Guardian; return;
            case "herokagachi": first = SynergyType.Songhai; second = SynergyType.Shadow; third = SynergyType.Frenzy; return;
            case "herovesna": first = SynergyType.Vanar; second = SynergyType.Inferno; third = SynergyType.Frost; return;
            // 追加ヒーロー6体（各陣営 alt/3rd 将）。陣営シナジー＋テーマ2種。
            case "heroziran": first = SynergyType.Lyonar; second = SynergyType.Divine; third = SynergyType.Guardian; return;
            case "herobrome": first = SynergyType.Lyonar; second = SynergyType.Warrior; third = SynergyType.Royal; return;
            case "heroreva": first = SynergyType.Songhai; second = SynergyType.Ranger; third = SynergyType.Shadow; return;
            case "heroshidai": first = SynergyType.Songhai; second = SynergyType.Shadow; third = SynergyType.Frenzy; return;
            case "herokara": first = SynergyType.Vanar; second = SynergyType.Frost; third = SynergyType.Guardian; return;
            case "heroilena": first = SynergyType.Vanar; second = SynergyType.Frost; third = SynergyType.Arcanist; return;
            // === 仲間化できる中ボス（勧誘候補）の個性付け（R3-midboss-synergy）。各体ユニークな組み合わせ。===
            // 中立コア5（cost3）。
            case "neutral_beastmaster": first = SynergyType.Beast; second = SynergyType.Summoner; third = SynergyType.Guardian; return;
            case "neutral_gnasher":     first = SynergyType.Beast; second = SynergyType.Frenzy; third = SynergyType.Shadow; return;
            case "neutral_rawr":        first = SynergyType.Beast; second = SynergyType.Warrior; third = SynergyType.Royal; return;
            case "neutral_rok":         first = SynergyType.Guardian; second = SynergyType.Storm; third = SynergyType.Machine; return;
            case "neutral_zukong":      first = SynergyType.Warrior; second = SynergyType.Storm; third = SynergyType.Frenzy; return;
            // 中立リカラー変種5（色＝追加テーマ）。
            case "neutral_beastmaster_crimson": first = SynergyType.Beast; second = SynergyType.Inferno; third = SynergyType.Frenzy; return;
            case "neutral_gnasher_ice":         first = SynergyType.Beast; second = SynergyType.Frost; third = SynergyType.Shadow; return;
            case "neutral_rok_steelblue":       first = SynergyType.Guardian; second = SynergyType.Frost; third = SynergyType.Machine; return;
            case "neutral_rok_gold":            first = SynergyType.Guardian; second = SynergyType.Divine; third = SynergyType.Royal; return;
            case "neutral_rok_mossgreen":       first = SynergyType.Guardian; second = SynergyType.Beast; third = SynergyType.Alchemy; return;
            // 陣営勧誘候補（cost4）。陣営シナジー＋テーマ2種で個性化。
            case "silitharelder":    first = SynergyType.Magmar; second = SynergyType.Beast; third = SynergyType.Guardian; return;
            case "veteransilithar":  first = SynergyType.Magmar; second = SynergyType.Beast; third = SynergyType.Warrior; return;
            case "makantorwarbeast": first = SynergyType.Magmar; second = SynergyType.Beast; third = SynergyType.Frenzy; return;
            case "gloomchaser":      first = SynergyType.Abyssian; second = SynergyType.Shadow; third = SynergyType.Ranger; return;
            case "abyssalcrawler":   first = SynergyType.Abyssian; second = SynergyType.Shadow; third = SynergyType.Wraith; return;
            case "rae":              first = SynergyType.Vetruvian; second = SynergyType.Arcanist; third = SynergyType.Ranger; return;
            case "starfirescarab":   first = SynergyType.Vetruvian; second = SynergyType.Machine; third = SynergyType.Arcanist; return;
            case "pax":              first = SynergyType.Vetruvian; second = SynergyType.Machine; third = SynergyType.Guardian; return;
            case "pyromancer":       first = SynergyType.Inferno; second = SynergyType.Arcanist; third = SynergyType.Storm; return;
            default:
                bool ranged = prefab != null && prefab.range >= 4;
                first = ranged ? SynergyType.Ranger : SynergyType.Warrior;
                second = ranged ? SynergyType.Arcanist : SynergyType.Guardian;
                return;
        }
    }

    private static string NormalizeUnitId(string rawName)
    {
        return LocalizationManager.CleanUnitName(rawName)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }

    // Noneや重複を除いて、1体につき最大3つまでのシナジーを安全に並べます。
    private static void AddUniqueSynergy(List<SynergyType> synergies, SynergyType type)
    {
        if (type == SynergyType.None || synergies.Contains(type) || synergies.Count >= 3)
            return;

        synergies.Add(type);
    }

    // 既存アセットに古いシナジー値が残っている場合でも、特に調整したいユニットだけ補正します。
    private static void ApplySpecificSynergyOverrides(string unitName, List<SynergyType> synergies)
    {
        if (synergies == null)
            return;

        string id = NormalizeUnitId(unitName);
        if (id == "backlinearcher")
            AddUniqueSynergy(synergies, SynergyType.Shadow);
    }
}
