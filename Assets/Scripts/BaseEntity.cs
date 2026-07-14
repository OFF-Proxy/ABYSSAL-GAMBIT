using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ユニットがマナ満タン時に使うスキルの種類です。
public enum UnitSkillType
{
    PowerStrike,
    SelfHeal,
    AllyHeal,
    Shield,
    AttackSpeedBoost,
    Stun,
    Slow,
    DamageBoost,
    AreaDamage
}

// スキルエフェクトの雰囲気です。ユニットごとに色や使うエフェクトを少し変えます。
public enum SkillVisualTheme
{
    Neutral,
    Fire,
    Ice,
    Nature,
    Holy,
    Shadow,
    Lightning,
    Tech,
    Void
}

// 戦闘中に浮かぶ数字の種類です。色と演出を分けるために使います。
public enum CombatNumberKind
{
    AttackDamage,
    FocusDamage,
    Healing
}

// すべてのユニットの共通処理を持つ基底クラスです。
// 移動、攻撃、被ダメージ、スキル、HP/MPバー、スターアップ表示、描画順をまとめて扱います。
public class BaseEntity : MonoBehaviour
{

    // Inspectorで設定する、見た目やアニメーションに必要な参照です。
    public HealthBar barPrefab;
    public SpriteRenderer spriteRender;
    public Animator animator;
    public Material team1OutlineMaterial;
    public Material team2OutlineMaterial;

    // ユニットの基本ステータスです。購入後にコストやスターで再調整されます。
    public int baseDamage = 1;
    public int baseHealth = 3;
    // R3-hero-scale: ヒーロー育成スケール（HP/攻撃の乗算）。ボス育成と違い毎回 set し直す方式で
    // ApplyCurrentStats 内で乗算するため、複数回適用しても複利化しない（1.0で無効）。
    public float heroScaleMultiplier = 1f;
    [Range(1, 5)]
    public int range = 1;
    public float attackSpeed = 1f; //Attacks per second
    public float movementSpeed = 1f; //Attacks per second
    [Range(0f, 0.75f)]
    public float baseDamageReduction = 0f;

    // マナ関連です。攻撃時や被ダメージ時にマナが溜まり、最大になるとスキルを使います。
    public int maxMana = 100;
    public int manaOnAttack = 25;
    public int manaOnDamageTaken = 15;

    // スキルの種類と効果量です。ユニット名から自動設定することもできます。
    public float skillDamageMultiplier = 1.8f;
    public bool autoConfigureSkillType = true;
    public UnitSkillType skillType = UnitSkillType.PowerStrike;
    public float skillHealPercent = 0.25f;
    public int skillFlatHeal = 0;
    public float skillShieldPercent = 0.35f;
    public int skillFlatShield = 0;
    public float skillShieldDuration = 4f;
    public float skillAllyHealPercent = 0.2f;
    public int skillFlatAllyHeal = 0;
    public float skillAttackSpeedBoostMultiplier = 1.45f;
    public float skillDamageBoostMultiplier = 1.55f;
    public float skillBuffDuration = 4f;
    public float skillStunDuration = 1.25f;
    [Range(0.1f, 1f)]
    public float skillSlowMultiplier = 0.55f;
    public float skillSlowDuration = 4f;
    public float skillAreaRadius = 1.65f;
    public float skillAreaDamageMultiplier = 1.35f;
    public float focusInfluence = 1f;
    public bool skillUsesFocusOnly = false;
    public int skillBasePower = 120;
    public SkillVisualTheme skillVisualTheme = SkillVisualTheme.Neutral;

    // ユニットが持つシナジーです。基本は2つ、一部ユニットだけ3つまで持てます。
    public SynergyType synergy1 = SynergyType.None;
    public SynergyType synergy2 = SynergyType.None;
    public SynergyType synergy3 = SynergyType.None;

    // 一部ユニットだけが持つ通常攻撃の追加仕様です。
    public bool normalAttackHitsAdjacentEnemies = false;
    public float adjacentAttackDamageMultiplier = 0.55f;
    public float adjacentAttackRadius = 1.2f;

    // STORY: 形態ペナルティ（攻撃力/最大HPに乗算）。章8の犬化カガチなど「弱体形態」用。既定1.0。
    public float formPenalty = 1f;

    // 攻撃アニメーション全体の何割あたりでダメージ判定を出すかです。
    [Range(0.05f, 0.95f)]
    public float attackImpactNormalizedTime = 0.55f;
    [Range(0.05f, 0.95f)]
    public float skillImpactNormalizedTime = 0.55f;

    // 戦闘中に変化する共通状態です。
    protected Team myTeam;
    protected BaseEntity currentTarget = null;
    protected Node currentNode;

    // 外部から現在のNodeを読むためのプロパティです。
    public Node CurrentNode => currentNode;

    // AI処理でよく使う状態確認用プロパティです。
    protected bool HasEnemy => currentTarget != null;
    protected bool IsInRange => IsTargetInRange(currentTarget);
    // 攻撃/スキルのモーション再生中かどうか。再生中は移動・再ターゲットを止め、必ずモーションを最後まで再生します。
    protected bool IsAttackMotionPlaying => attackCoroutine != null;
    protected bool moving;
    protected Node destination;
    // 直前に離れたNode。斜め2マスを往復し続ける「引っ掛かり」を防ぐため、原則すぐには戻らない。
    protected Node previousNode;
    // 目標とする攻撃足場。安定化のため保持し、占有マスを迂回しながらここへ向かう。
    protected Node moveGoal;
    protected HealthBar healthbar;

    protected bool dead = false;
    protected bool canAttack = true;
    protected float waitBetweenAttack;

    // UIやGameManagerから読み取るための公開情報です。
    public Team Team => myTeam;
    public bool IsOnBoard => currentNode != null;
    public string UnitId => string.IsNullOrEmpty(unitId) ? gameObject.name : unitId;
    public int BaseCost { get; private set; } = 1;
    public int StarLevel { get; private set; } = 1;
    public int CurrentMana => currentMana;
    public int MaxMana => maxMana;
    public int MaxHealth => maxHealth > 0 ? maxHealth : baseHealth;
    public int CurrentHealth => baseHealth;
    public float DamageReduction => GetTotalDamageReduction();
    public SkillVisualTheme SkillTheme => skillVisualTheme;
    public bool SkillUsesFocusOnly => skillUsesFocusOnly;
    public int SkillBasePower => skillBasePower;
    public bool HasActiveShield => shieldHealth > 0;
    public bool CanBeTargeted => !dead && Time.time >= untargetableUntil;
    public bool IsDead => dead;
    public bool IsSummonedUnit { get; private set; }
    public bool IsDebugTrainingDummy { get; private set; }
    public float HealthRatio => MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)baseHealth / MaxHealth);
    public bool HasActiveInfernoBurn => Time.time < infernoBurnUntil;
    public BaseEntity InfernoBurnSource => infernoBurnSource;
    public IReadOnlyList<ItemData> EquippedItems => equippedItems;
    public bool HasItemSpace => equippedItems.Count < MaxEquippedItems;
    protected bool CanAct => IsOnBoard && GameManager.Instance != null && GameManager.Instance.IsRoundInProgress && !dead && !stunned;

    // 内部管理用の状態です。元ステータス、現在マナ、シールド、各種Coroutineなどを保持します。
    private string unitId;
    private int currentMana;
    private int maxHealth;
    private int shieldHealth;
    private bool baseStatsCaptured;
    private int originalBaseDamage;
    private int originalBaseHealth;
    private int originalRange;
    private float originalAttackSpeed;
    private float originalMovementSpeed;
    private float originalBaseDamageReduction;
    private int originalMaxMana;
    private int originalManaOnAttack;
    private int originalManaOnDamageTaken;
    private Vector3 originalScale;
    // 敵としての中ボス個体だけに乗せる強化倍率（射程/ステータス）。仲間化後のアライ（別個体生成）には乗らない。
    private float enemyBossStatMul = 1f;
    private float enemyBossRangeMul = 1f;
    private readonly List<ItemData> equippedItems = new List<ItemData>();
    private Coroutine attackCoroutine;
    private Coroutine shieldCoroutine;
    private Coroutine attackSpeedBoostCoroutine;
    private Coroutine damageBoostCoroutine;
    private Coroutine slowCoroutine;
    private Coroutine stunCoroutine;
    private Coroutine starPopupCoroutine;
    private Coroutine invaderThunderCoroutine;
    private Coroutine golBlackHoleCoroutine;
    private Coroutine deathStateCoroutine;
    private GameObject starPopupObject;
    private Tween starScaleTween;
    private GameObject shieldAuraObject;
    private bool deathDestroyStarted;
    private bool stunned;
    private float untargetableUntil;
    private float attackSpeedBuffMultiplier = 1f;
    private float damageBuffMultiplier = 1f;
    private float attackSpeedDebuffMultiplier = 1f;
    private BaseEntity lastDamageSource;
    private readonly List<Coroutine> synergyCoroutines = new List<Coroutine>();
    private BaseEntity rangerFocusTarget;
    private int rangerFocusStacks;
    private int beastAttackSpeedStacks;
    private bool warriorLastStandUsed;
    private bool machineEmergencyRepairUsed;
    private Coroutine infernoBurnCoroutine;
    private Coroutine skyfallRampageCoroutine;
    private Coroutine citySupplyDroneCoroutine;
    private Coroutine temporarySummonLifetimeCoroutine;
    private readonly List<Coroutine> burnDamageCoroutines = new List<Coroutine>();
    private bool movementLockedBySkill;
    private float infernoBurnUntil;
    private int infernoBurnTickDamage;
    private BaseEntity infernoBurnSource;
    private bool summonedDeathSlowEnabled;
    private float summonedDeathSlowRadius;
    private float summonedDeathSlowDuration;
    private float summonedDeathAttackSpeedMultiplier = 0.72f;
    private float summonedDeathMoveSpeedPenalty = -0.25f;
    private float nextDebugDummyWanderTime;
    private int frostFreezeStacks;
    private float frostFreezeStackUntil;
    private bool divineProtectionActive;
    private bool divineProtectionHealsNearby;
    private float divineProtectionReviveHealthPercent;
    private float synergyManaGainMultiplier = 1f;

    // シナジー由来の補正です。アイテムやスター補正とは別枠で戦闘中だけ加算します。
    public float synergyDamageReductionBonus;
    public float synergyAttackSpeedBonus;
    public float synergyPowerBonus;
    public float synergyMoveSpeedBonus;
    public float synergyDamageDealtBonus;

    // アウトライン用マテリアルと、見た目の親オブジェクト設定です。
    private static Material fallbackTeam1OutlineMaterial;
    private static Material fallbackTeam2OutlineMaterial;
    private const string VisualRootName = "Visual";
    private const float FootLocalY = -0.42f;

    // 全体バランス調整用の倍率です。ここを下げると全ユニットの火力や攻撃速度が落ちます。
    private const float GlobalDamageMultiplier = 0.88f;
    private const float GlobalAttackSpeedMultiplier = 0.76f;
    private const float GlobalHealingMultiplier = 0.78f;
    private const float GlobalShieldMultiplier = 0.70f;
    private const int MaxEquippedItems = 3;

    // 固有アイテム効果で参照するIDです。ItemCatalog側のidと必ず合わせます。
    private const string IronBulwarkItemId = "iron_bulwark";
    private const string FrostguardPlateItemId = "frostguard_plate";
    private const string EternalHeartItemId = "eternal_heart";
    private const string IridiumScaleItemId = "iridium_scale";
    private const string PhalanxAegisItemId = "phalanx_aegis";
    private const string SpineCleaverItemId = "spine_cleaver";
    private const string SkywindGlaivesItemId = "skywind_glaives";
    private const string GodhammerItemId = "godhammer";
    private const string AdamantineClawsItemId = "adamantine_claws";
    private const string RageChakramItemId = "rage_chakram";
    private const string UnboundedAmuletItemId = "unbounded_amulet";
    private const string YkirStaffItemId = "ykir_staff";
    private const string ThunderclapScepterItemId = "thunderclap_scepter";
    private const string RepairStaffItemId = "repair_staff";
    private const string DarkstoneRingItemId = "darkstone_ring";
    private const float PhalanxAuraRadius = 1.15f;

    // Y座標とX座標から描画順を決めるための基準値です。
    private const int VisualSortingBaseOrder = 10000;
    private static readonly int SpriteUvMinMaxId = Shader.PropertyToID("_SpriteUVMinMax");
    private Transform visualRoot;
    private MaterialPropertyBlock spritePropertyBlock;
    private Sprite lastSpriteForOutlineBounds;

    // アイテム固有効果の戦闘中だけの状態です。
    private Coroutine eternalHeartCoroutine;
    private Coroutine unboundedAmuletCoroutine;
    private Coroutine rageChakramResetCoroutine;
    private bool unboundedManaWindowActive;
    private bool ykirFirstSkillConsumed;
    private int skywindAttackCounter;
    private int godhammerAttackCounter;
    private int thunderclapTakenHitCounter;
    private int rageChakramStacks;
    private int adamantineStacks;
    private BaseEntity adamantineTarget;
    private int spineCleaverShredStacks;
    private float spineCleaverShredUntil;
    private float currentItemSkillEffectMultiplier = 1f;

    // Unity生成直後に呼ばれます。必要な参照と初期ステータスを確保します。
    protected void Awake()
    {
        EnsureComponentReferences();
        ConfigureAnimatorForRuntime();
        RestoreAnimatorControllerIfMissing();
        CaptureBaseStats();
    }

    // ショップ購入や敵生成時に、ユニット名・コスト・スターを初期化します。
    public void InitializeIdentity(string unitId, int baseCost = 1, int starLevel = 1)
    {
        CaptureBaseStats();
        this.unitId = unitId;
        BaseCost = Mathf.Max(1, baseCost);
        RestoreAnimatorControllerIfMissing();
        originalRange = GetConfiguredBaseRange(unitId);
        range = originalRange;
        ConfigureDefaultSkillType(unitId);
        ApplyBaseBalance();
        ApplyStarLevel(starLevel);
    }

    // ★1/★2/★3に応じて、ステータスと見た目サイズを更新します。
    public void ApplyStarLevel(int starLevel)
    {
        CaptureBaseStats();

        // 前のスターを覚えておき、上がった時だけ演出を出します。
        int previousStarLevel = StarLevel;
        StarLevel = Mathf.Clamp(starLevel, 1, 3);
        ApplyCurrentStats(true);
        transform.localScale = originalScale;
        ApplyStarVisualScale();
        gameObject.name = $"{UnitId} Star{StarLevel}";

        if (healthbar != null)
        {
            healthbar.Setup(transform, MaxHealth, StarLevel, spriteRender, this);
            UpdateHealthBarItemIcons();
        }

        if (StarLevel > previousStarLevel && StarLevel > 1)
        {
            AttackEffectPlayer.PlayUiSfx("star_up");
            PlayStarUpgradeScaleTween();
            ShowStarUpgradePopup(StarLevel);
        }
    }

    // アイテムを装備します。3枠を超える場合は失敗します。
    public bool TryEquipItem(ItemData itemData)
    {
        if (itemData == null || equippedItems.Count >= MaxEquippedItems)
            return false;

        equippedItems.Add(itemData);
        ApplyCurrentStats(false);
        UpdateHealthBarItemIcons();
        return true;
    }

    // 装備中のアイテムをすべて外し、外した一覧を返します。
    public List<ItemData> RemoveAllItems()
    {
        List<ItemData> removedItems = new List<ItemData>(equippedItems);
        equippedItems.Clear();
        ApplyCurrentStats(false);
        UpdateHealthBarItemIcons();
        return removedItems;
    }

    // 生成時に、EntityDataやデフォルト設定からシナジーをコピーします。
    public void SetSynergies(SynergyType first, SynergyType second, SynergyType third = SynergyType.None)
    {
        synergy1 = first;
        synergy2 = second;
        synergy3 = third;
    }

    // 召喚体は通常の所有ユニットとは別扱いにし、シナジー計算・合成・売却対象から外します。
    public void SetSummonedUnit(bool value)
    {
        IsSummonedUnit = value;
        if (value)
            SetSynergies(SynergyType.None, SynergyType.None, SynergyType.None);
    }

    // オーグメント由来の召喚体ステータス倍率（HP/与ダメ）を、生成直後に基準値へ乗せ直します。
    public void ApplyAugmentSummonStatMultipliers(float hpMultiplier, float damageMultiplier)
    {
        if (!IsSummonedUnit) return;
        if (hpMultiplier > 0f && !Mathf.Approximately(hpMultiplier, 1f))
            originalBaseHealth = Mathf.Max(1, Mathf.RoundToInt(originalBaseHealth * hpMultiplier));
        if (damageMultiplier > 0f && !Mathf.Approximately(damageMultiplier, 1f))
            originalBaseDamage = Mathf.Max(1, Mathf.RoundToInt(originalBaseDamage * damageMultiplier));
        ApplyCurrentStats(true);
    }

    // R1-collection: ボス育成（アフィニティ）の永続ステータス倍率を適用する。HP・攻撃力を同率で底上げ。
    public void ApplyBossAffinityMultiplier(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f)) return;
        originalBaseHealth = Mathf.Max(1, Mathf.RoundToInt(originalBaseHealth * multiplier));
        originalBaseDamage = Mathf.Max(1, Mathf.RoundToInt(originalBaseDamage * multiplier));
        ApplyCurrentStats(true);
    }

    // R2-coremode: 拠点コアか。コアは移動も通常攻撃もしないが被ターゲット・被ダメ可能。死亡で勝敗判定。
    public bool IsCore { get; private set; }

    // この実体を拠点コアとして設定する（Setup 後に呼ぶ）。高HP・無移動・シナジー無し。
    public void ConfigureAsCore(int health)
    {
        IsCore = true;
        SetSynergies(SynergyType.None, SynergyType.None, SynergyType.None);
        originalBaseHealth = Mathf.Max(1, health);
        originalBaseDamage = 1;
        originalMovementSpeed = 0f;
        ApplyCurrentStats(true);
        transform.localScale = Vector3.one * 1.5f;
    }

    // ====== R3-chest-room: 殴って開ける宝箱（サンドバッグ）。移動も攻撃もせず、被弾でコイン、HP0で開封。 ======
    public bool IsChest { get; private set; }
    private bool chestIsItem;                 // true=アイテム箱（HP0で3択）/ false=コイン箱。
    private const int ChestCoinSegments = 6;  // HPを6分割。各セグメント割れで1コイン（最大5）＋HP0開封で+5＝計10。
    private int chestCoinsDropped;
    private System.Action onChestCoinDrop;    // 1コイン分の付与（GMが+1コイン）。
    private System.Action onChestOpened;      // 開封時（GMがコイン箱=+5/アイテム箱=3択）。
    private bool chestOpened;

    public void ConfigureAsChest(int hp, bool itemChest, System.Action coinDrop, System.Action opened)
    {
        IsChest = true;
        chestIsItem = itemChest;
        onChestCoinDrop = coinDrop;
        onChestOpened = opened;
        chestCoinsDropped = 0;
        chestOpened = false;
        SetSynergies(SynergyType.None, SynergyType.None, SynergyType.None);
        originalBaseHealth = Mathf.Max(1, hp);
        originalBaseDamage = 1;
        originalMovementSpeed = 0f;
        originalAttackSpeed = 0f;
        canAttack = false;
        ApplyCurrentStats(true);
        SetTarget(null);
        transform.localScale = Vector3.one * 1.2f;
        // 流用プレハブのAnimatorがスプライトを上書きするのを止め、宝箱スプライトを表示できるようにする。
        if (animator != null) animator.enabled = false;
    }

    // 宝箱のフレーム。idle=閉じてキラめく（HP0までループ）。open=開封アニメ（HP0で1回再生）。
    private Sprite[] chestIdleFrames;
    private Sprite[] chestOpenFrames;
    public void SetChestFrames(Sprite[] idle, Sprite[] open)
    {
        chestIdleFrames = idle;
        chestOpenFrames = open;
        if (idle != null && idle.Length > 0 && spriteRender != null)
            spriteRender.sprite = idle[0];
        if (idle != null && idle.Length > 1)
            StartCoroutine(ChestIdleRoutine());
    }

    // 閉じた宝箱のキラめきをループ（開封 or 死亡まで）。
    private System.Collections.IEnumerator ChestIdleRoutine()
    {
        int i = 0;
        var wait = new WaitForSeconds(0.09f);
        while (!chestOpened && !dead && IsChest && chestIdleFrames != null && chestIdleFrames.Length > 1)
        {
            if (spriteRender != null) spriteRender.sprite = chestIdleFrames[i % chestIdleFrames.Length];
            i++;
            yield return wait;
        }
    }

    // 開封アニメを1回再生（HP0で OpenChestOnce から起動）。GameObjectが消えるまでの間だけ進む。
    private System.Collections.IEnumerator ChestOpenRoutine()
    {
        var wait = new WaitForSeconds(0.05f);
        // 即時despawnでも「開いた」絵が見えるよう、まず最終フレームを表示。
        if (chestOpenFrames != null && chestOpenFrames.Length > 0 && spriteRender != null)
            spriteRender.sprite = chestOpenFrames[chestOpenFrames.Length - 1];
        if (chestOpenFrames != null)
            for (int i = 0; i < chestOpenFrames.Length; i++)
            {
                if (spriteRender != null) spriteRender.sprite = chestOpenFrames[i];
                yield return wait;
            }
    }

    // 被弾でHPが減るたびに、跨いだセグメントぶんコインを落とす（揺れ＋SE）。コイン箱のみ。
    private void HandleChestDamage()
    {
        if (!IsChest || chestIsItem) return;
        int lostSegments = Mathf.FloorToInt((float)(MaxHealth - baseHealth) / Mathf.Max(1f, MaxHealth / (float)ChestCoinSegments));
        int target = Mathf.Clamp(lostSegments, 0, ChestCoinSegments - 1); // 最大5（6個目=HP0は開封側）。
        while (chestCoinsDropped < target)
        {
            chestCoinsDropped++;
            onChestCoinDrop?.Invoke();
            ShakeChest(0.14f, 0.05f);
            AttackEffectPlayer.PlayUiSfx("sfx_gold_reward_1");
        }
    }

    // 開封：コイン箱=+5コイン、アイテム箱=3択。1回だけ。
    private void OpenChestOnce()
    {
        if (!IsChest || chestOpened) return;
        chestOpened = true;
        ShakeChest(0.22f, 0.09f);
        AttackEffectPlayer.PlayUiSfx("sfx_gold_reward_2");
        if (animator != null) animator.enabled = false; // 開封絵がジャガーノートに上書きされないように。
        if (chestOpenFrames != null && chestOpenFrames.Length > 0)
            StartCoroutine(ChestOpenRoutine());
        onChestOpened?.Invoke();
    }

    private void ShakeChest(float dur, float strength)
    {
        transform.DOKill();
        transform.DOShakePosition(dur, new Vector3(strength, strength * 0.6f, 0f), 16, 90, false, true).SetUpdate(false);
    }

    // スキルやシナジー検証用の敵ダミーにします。敵として狙われますが、自分からは攻撃せず盤面を歩き回ります。
    public void ConfigureDebugTrainingDummy(int health, float moveSpeed)
    {
        IsDebugTrainingDummy = true;
        SetSynergies(SynergyType.None, SynergyType.None, SynergyType.None);

        originalBaseHealth = Mathf.Max(1, health);
        originalBaseDamage = 1;
        originalRange = 1;
        originalAttackSpeed = 0.05f;
        originalMovementSpeed = Mathf.Max(0.1f, moveSpeed);
        originalMaxMana = 9999;
        originalManaOnAttack = 0;
        originalManaOnDamageTaken = 0;
        originalBaseDamageReduction = 0.05f;

        skillType = UnitSkillType.PowerStrike;
        skillVisualTheme = SkillVisualTheme.Neutral;
        skillUsesFocusOnly = false;
        skillBasePower = 1;

        ApplyCurrentStats(true);
        ResetMana();
        SetTarget(null);
        canAttack = true;
        nextDebugDummyWanderTime = Time.time + UnityEngine.Random.Range(0.15f, 0.85f);
        gameObject.name = $"{UnitId} TrainingDummy";
    }

    // 召喚体を一定時間だけ盤面に残し、時間切れで安全に片付けます。
    public void BeginTemporarySummonLifetime(float duration)
    {
        if (temporarySummonLifetimeCoroutine != null)
            StopCoroutine(temporarySummonLifetimeCoroutine);

        if (duration > 0f && gameObject.activeInHierarchy)
            temporarySummonLifetimeCoroutine = StartCoroutine(TemporarySummonLifetimeCoroutine(duration));
    }

    // 召喚体が倒れた時に周囲へスロウを撒く設定です。Legionの亡霊で使います。
    public void ConfigureSummonedDeathSlow(float radius, float duration, float attackSpeedMultiplier, float moveSpeedPenalty)
    {
        summonedDeathSlowEnabled = true;
        summonedDeathSlowRadius = Mathf.Max(0.1f, radius);
        summonedDeathSlowDuration = Mathf.Max(0.1f, duration);
        summonedDeathAttackSpeedMultiplier = Mathf.Clamp(attackSpeedMultiplier, 0.1f, 1f);
        summonedDeathMoveSpeedPenalty = Mathf.Clamp(moveSpeedPenalty, -0.9f, 0f);
    }

    // 指定シナジーを持っているかを返します。Noneは常にfalseです。
    public bool HasSynergy(SynergyType type)
    {
        return type != SynergyType.None && (synergy1 == type || synergy2 == type || synergy3 == type);
    }

    // 表示やカウント用に、このユニットの有効シナジーを重複なしで返します。
    public List<SynergyType> GetSynergyTypes()
    {
        List<SynergyType> result = new List<SynergyType>();
        AddSynergyIfValid(result, synergy1);
        AddSynergyIfValid(result, synergy2);
        AddSynergyIfValid(result, synergy3);
        return result;
    }

    private void AddSynergyIfValid(List<SynergyType> list, SynergyType type)
    {
        if (type == SynergyType.None || list.Contains(type))
            return;

        list.Add(type);
    }

    // 盤面に配置された時のセットアップです。
    public void Setup(Team team, Node currentNode)
    {
        if (currentNode == null)
        {
            Debug.LogError($"{name} setup failed: currentNode is null.");
            return;
        }

        EnsureComponentReferences();
        RestoreAnimatorControllerIfMissing();
        CaptureBaseStats();
        myTeam = team;
        ApplyTeamVisuals();
        SetTarget(null);
        ClearMovementReservation();
        canAttack = true;
        ResetActionAnimatorState();
        RestartAnimatorPlayback();
        ResetMana();
        ClearShield();
        ClearTemporaryStatusEffects();

        // 盤面Nodeを占有し、そのNodeの位置へユニットを移動します。
        this.currentNode = currentNode;
        transform.position = currentNode.worldPosition;
        currentNode.SetOccupied(true);

        EnsureHealthBar();
        if (healthbar != null)
        {
            healthbar.gameObject.SetActive(true);
            healthbar.Setup(transform, MaxHealth, StarLevel, spriteRender, this);
            UpdateHealthBarItemIcons();
        }
        ApplyStarVisualScale();
    }

    // ベンチ上に置かれた時のセットアップです。戦闘には参加しないのでNodeは持ちません。
    public void SetupOnBench(Team team, Vector3 benchPosition)
    {
        EnsureComponentReferences();
        RestoreAnimatorControllerIfMissing();
        CaptureBaseStats();
        myTeam = team;
        ApplyTeamVisuals();
        currentNode = null;
        SetTarget(null);
        ClearMovementReservation();
        canAttack = true;
        ResetActionAnimatorState();
        RestartAnimatorPlayback();
        ResetMana();
        ClearShield();
        ClearTemporaryStatusEffects();
        transform.position = benchPosition;
        ApplyStarVisualScale();

        if (healthbar != null)
            healthbar.gameObject.SetActive(false);
    }

    // GameManagerのイベントを購読し、ラウンド開始/終了や死亡通知を受け取れるようにします。
    protected void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning($"{name} could not subscribe to round events because GameManager was not found.");
            return;
        }

        GameManager.Instance.OnRoundStart += OnRoundStart;
        GameManager.Instance.OnRoundEnd += OnRoundEnd;
        GameManager.Instance.OnUnitDied += OnUnitDied;
    }

    // 毎フレームの最後に、アウトライン用UVと描画順を更新します。
    private void LateUpdate()
    {
        UpdateSpriteUvBounds();
        UpdateVisualSortingOrder();
    }

    // 破棄時にイベント購読や別生成したバー/ポップアップを掃除します。
    protected void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= OnRoundStart;
            GameManager.Instance.OnRoundEnd -= OnRoundEnd;
            GameManager.Instance.OnUnitDied -= OnUnitDied;
        }

        if (healthbar != null)
            Destroy(healthbar.gameObject);

        if (starPopupObject != null)
            Destroy(starPopupObject);

        starScaleTween?.Kill();
        starScaleTween = null;

        DestroyShieldAuraVisual();
    }

    // ラウンド開始時に、装備アイテムの開幕効果と戦闘中カウンターを初期化します。
    protected virtual void OnRoundStart()
    {
        ResetItemCombatState();

        if (!IsInActiveRound())
            return;

        ApplyRoundStartItemEffects();
    }
    // ラウンド終了時に、戦闘中だけの状態を元に戻します。
    protected virtual void OnRoundEnd()
    {
        ResetBattleTemporaryState();
    }

    // GameManagerからも直接呼べる、戦闘終了時の即時リセット入口です。
    public void ResetBattleTemporaryState()
    {
        SetTarget(null);
        ClearMovementReservation();
        StopAttackAnimation();
        ClearTemporaryStatusEffects();
        ClearShield();
        ResetMana();
        movementLockedBySkill = false;
        canAttack = true;
        SetAnimatorBool("walking", false);

        // アルカナのスキル回数・持続BloodMage・フィナーレは戦闘ごとにリセットし、次ラウンドへ持ち越しません。
        ResetArcanaCombatState();
    }

    // アルカナ専用の戦闘状態リセット。詠唱カウントを0に戻し、残っているBloodMageとフィナーレを片付けます。
    private void ResetArcanaCombatState()
    {
        arcanaSkillCastCount = 0;

        if (arcanaFinaleCoroutine != null)
        {
            StopCoroutine(arcanaFinaleCoroutine);
            arcanaFinaleCoroutine = null;
        }

        for (int i = 0; i < arcanaActiveBloodMages.Count; i++)
        {
            if (arcanaActiveBloodMages[i] != null)
                AttackEffectPlayer.EndBloodMage(arcanaActiveBloodMages[i]);
        }
        arcanaActiveBloodMages.Clear();

        // フィナーレ中に戦闘が終わった場合に備え、本体スケールと再生速度を正規の値へ戻します。
        SetAnimatorPlaybackSpeed(1f);
        ApplyStarVisualScale();
    }

    // 誰かが死んだ時、今狙っていた相手ならターゲットを外します。
    protected virtual void OnUnitDied(BaseEntity diedUnity)
    {
        if (currentTarget == diedUnity)
            SetTarget(null);

        if (adamantineTarget == diedUnity)
        {
            adamantineTarget = null;
            adamantineStacks = 0;
        }

        ApplyKillTriggeredItemEffects(diedUnity);
    }

    // 最も近い敵を探してターゲットにします。
    protected void FindTarget()
    {
        if (GameManager.Instance == null)
            return;

        SetTarget(GetNearestEnemy());
    }

    // 戦闘中、現在のターゲットが射程外なら狙い直します。
    protected void RefreshTargetForCombat()
    {
        if (currentTarget != null && !currentTarget.CanBeTargeted)
            SetTarget(null);

        if (currentTarget != null && IsInRange)
        {
            ClearMovementReservation();
            return;
        }

        SetTarget(GetNearestEnemy());
    }

    // デバッグ用ダミー専用の行動です。攻撃処理には入らず、空いている隣接マスへゆっくり移動します。
    protected bool TryHandleDebugTrainingDummy()
    {
        if (!IsDebugTrainingDummy)
            return false;

        SetTarget(null);

        if (GridManager.Instance == null || currentNode == null)
        {
            SetAnimatorBool("walking", false);
            return true;
        }

        if (moving && destination != null)
        {
            moving = !MoveTowards(destination);
            if (!moving)
            {
                if (currentNode != null && currentNode != destination)
                    currentNode.SetOccupied(false);

                SetCurrentNode(destination);
                destination = null;
                nextDebugDummyWanderTime = Time.time + UnityEngine.Random.Range(0.35f, 1.15f);
            }

            return true;
        }

        if (Time.time < nextDebugDummyWanderTime)
        {
            SetAnimatorBool("walking", false);
            return true;
        }

        List<Node> candidates = GridManager.Instance.GetNodesCloseTo(currentNode)
            .Where(node => node != null && !node.IsOccupied)
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        if (candidates.Count == 0)
        {
            nextDebugDummyWanderTime = Time.time + 0.6f;
            SetAnimatorBool("walking", false);
            return true;
        }

        destination = candidates[0];
        destination.SetOccupied(true);
        moving = true;
        return true;
    }

    // 自分から一番近い、盤面上の敵ユニットを探します。
    private BaseEntity GetNearestEnemy()
    {
        var allEnemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        float minDistance = Mathf.Infinity;
        BaseEntity entity = null;
        foreach (BaseEntity e in allEnemies)
        {
            if (e == null || !e.IsOnBoard || !e.CanBeTargeted)
                continue;

            float distance = Vector3.Distance(e.transform.position, this.transform.position);
            if (distance <= minDistance)
            {
                minDistance = distance;
                entity = e;
            }
        }

        return entity;
    }

    // 指定Nodeに向かって少しずつ移動します。到着したらtrueを返します。
    protected bool MoveTowards(Node nextNode)
    {
        Vector3 direction = (nextNode.worldPosition - this.transform.position);
        if(direction.sqrMagnitude <= 0.005f)
        {
            transform.position = nextNode.worldPosition;
            SetAnimatorBool("walking", false);
            return true;
        }

        FaceDirection(direction.x);
        SetAnimatorBool("walking", true);

        this.transform.position += direction.normalized * GetEffectiveMovementSpeed() * Time.deltaTime;
        return false;
    }

    // ターゲットが射程内に入る位置まで移動するための次の1マスを決めます。
    protected void GetInRange()
    {
        if (movementLockedBySkill)
        {
            ClearMovementReservation();
            SetAnimatorBool("walking", false);
            return;
        }

        if (currentTarget == null)
            return;

        if(!moving)
        {
            destination = null;
            GridManager grid = GridManager.Instance;
            Node targetNode = currentTarget.CurrentNode;
            if (grid == null || targetNode == null)
                return;

            // 既に射程内なら移動不要。
            if (IsInRange) { ClearMovementReservation(); return; }

            // 目標足場(moveGoal)を保持して安定化させる。毎着地での再選択による「ウロウロ」を防ぐ。
            // 無効化条件：占有された／ターゲットが射程外へ離れた／到達不能になった。
            bool goalValid = moveGoal != null && !moveGoal.IsOccupied
                && Mathf.Max(Mathf.Abs(moveGoal.worldPosition.x - targetNode.worldPosition.x),
                             Mathf.Abs(moveGoal.worldPosition.y - targetNode.worldPosition.y)) <= range + 0.05f;
            if (goalValid)
            {
                List<Node> vp = grid.GetPath(currentNode, moveGoal);
                if (vp == null || vp.Count <= 1) goalValid = false;
            }
            if (!goalValid)
                moveGoal = PickAttackGoal(grid, targetNode);

            if (moveGoal == null)
            {
                // どの攻撃足場にも到達できない（敵を味方が囲み切っている等）。無駄に動かず待機する。
                SetAnimatorBool("walking", false);
                return;
            }

            // moveGoal までの経路の次の1マスへ。占有マスは GetPath が自動で迂回する（遠回りしてでも向かう）。
            List<Node> path = grid.GetPath(currentNode, moveGoal);
            if (path == null || path.Count <= 1) { moveGoal = null; SetAnimatorBool("walking", false); return; }
            Node step = path[1];
            if (step.IsOccupied) { SetAnimatorBool("walking", false); return; } // 一時的に塞がれている→待機（往復しない）

            // 次に移動するマスを予約して、他ユニットが同じマスへ向かわないようにします。
            step.SetOccupied(true);
            destination = step;
        }

        if (destination == null)
            return;

        moving = !MoveTowards(destination);
        if(!moving)
        {
            //Free previous node
            currentNode.SetOccupied(false);
            previousNode = currentNode; // 来た方向を記録して、すぐ戻らないようにする。
            SetCurrentNode(destination);
        }
    }

    // ターゲットの射程内マスのうち、現在地から最短経路で到達できる空きマスを選びます。
    // 到達できる足場が無い（敵を味方が囲み切っている等）場合は null（＝待機）。
    private Node PickAttackGoal(GridManager grid, Node targetNode)
    {
        List<Node> inRange = grid.GetNodesInRange(targetNode, range);
        Node best = null; int bestSteps = int.MaxValue; float bestDist = float.MaxValue;
        for (int i = 0; i < inRange.Count; i++)
        {
            Node n = inRange[i];
            if (n == null || n == targetNode || n.IsOccupied) continue;
            List<Node> p = grid.GetPath(currentNode, n);
            if (p == null || p.Count <= 1) continue; // 到達不能
            int steps = p.Count;
            float d = Vector3.Distance(n.worldPosition, targetNode.worldPosition);
            if (steps < bestSteps || (steps == bestSteps && d < bestDist))
            { bestSteps = steps; bestDist = d; best = n; }
        }
        return best;
    }

    // 現在いるNodeを更新します。移動完了時に呼びます。
    public void SetCurrentNode(Node node)
    {
        currentNode = node;
    }

    // 攻撃元が分からないダメージ用の入口です。
    public void TakeDamage(int amount)
    {
        TakeDamage(amount, null, CombatNumberKind.AttackDamage);
    }

    // ダメージを受けた時の処理です。シールド、HP、マナ、死亡判定をまとめて行います。
    public void TakeDamage(int amount, BaseEntity source)
    {
        TakeDamage(amount, source, CombatNumberKind.AttackDamage);
    }

    // ダメージ表示の色を指定できる入口です。通常攻撃は橙、秘力寄りスキルは青で表示します。
    public void TakeDamage(int amount, BaseEntity source, CombatNumberKind numberKind)
    {
        if (dead)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // デバッグ無敵：味方/敵ユニットの被ダメージを無効化（DebugMenu のトグル）。
        if (GameManager.Instance != null)
        {
            if (Team == Team.Team1 && GameManager.Instance.DebugPlayerInvincible) return;
            if (Team == Team.Team2 && GameManager.Instance.DebugEnemyInvincible) return;
        }
#endif

        amount = ApplyRoundDamageMultiplier(amount, source);
        if (source != null)
        {
            lastDamageSource = source;
            amount = source.ApplyOutgoingSynergyDamageModifiers(this, amount);
        }
        else
        {
            lastDamageSource = null;
        }

        ApplyIncomingHitItemEffects(source, Mathf.Max(0, amount));

        // オーグメント on-hit procs (通常攻撃ヒット時のみ、ソース側で発動)
        if (source != null && numberKind == CombatNumberKind.AttackDamage)
        {
            source.TryApplyAugmentOnHitProcs(this);
            source.TryApplyUnitOnHitProcs(this);
        }

        // まずシールドで受け止め、残った分だけHPを減らします。
        int remainingDamage = Mathf.Max(0, amount);
        int displayedDamage = 0;
        bool hadShieldBeforeHit = shieldHealth > 0;
        if (shieldHealth > 0)
        {
            int blockedDamage = Mathf.Min(shieldHealth, remainingDamage);
            shieldHealth -= blockedDamage;
            remainingDamage -= blockedDamage;
            displayedDamage += blockedDamage;

            if (healthbar != null)
                healthbar.UpdateShieldBar(shieldHealth, MaxHealth);

            if (shieldHealth <= 0)
            {
                DestroyShieldAuraVisual();
                SynergyManager.Instance?.NotifyShieldBroken(this);
            }
        }

        if (remainingDamage > 0)
            remainingDamage = Mathf.Max(1, Mathf.RoundToInt(remainingDamage * (1f - GetTotalDamageReduction())));

        displayedDamage += remainingDamage;
        baseHealth -= remainingDamage;
        ShowCombatNumber(displayedDamage, numberKind);
        if (!hadShieldBeforeHit)
            GainMana(manaOnDamageTaken);
        if (healthbar != null)
            healthbar.UpdateBar(baseHealth);

        if (IsChest) HandleChestDamage(); // 被弾でコインを落とす（コイン箱）。

        if (baseHealth > 0)
        {
            CheckHealthThresholdSynergies();
            if (remainingDamage > 0 && myTeam == Team.Team1)
                SynergyManager.Instance?.NotifyAllyDamaged(this);
        }

        if(baseHealth <= 0 && !dead)
        {
            Die();
        }
    }

    // 戦闘が長引くほど、GameManager側の時間補正で受けるダメージを増やします。
    private int ApplyRoundDamageMultiplier(int amount, BaseEntity source)
    {
        if (amount <= 0 || source == null || GameManager.Instance == null || !GameManager.Instance.IsRoundInProgress)
            return amount;

        float multiplier = Mathf.Max(1f, GameManager.Instance.RoundDamageMultiplier);
        return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
    }

    // 攻撃側のシナジーによる与ダメージ補正です。通常攻撃固有の補正は別で処理します。
    private int ApplyOutgoingSynergyDamageModifiers(BaseEntity target, int amount)
    {
        if (amount <= 0 || target == null || !IsInActiveRound() || SynergyManager.Instance == null)
            return amount;

        float multiplier = 1f + synergyDamageDealtBonus;
        if (HasSynergy(SynergyType.Shadow)
            && SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Shadow, myTeam, 2)
            && target.HealthRatio <= 0.40f)
            multiplier += 0.20f;

        if (HasSynergy(SynergyType.Frenzy)
            && SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Frenzy, myTeam, 4))
            multiplier += Mathf.Clamp01(1f - HealthRatio) * 0.35f;

        // === オーグメント由来の与ダメージ補正（プレイヤー側のみ） ===
        if (myTeam == Team.Team1 && GameManager.Instance != null)
        {
            // gold_low_hp_dmg: HPが低いほど与ダメ上昇（最大+20%）
            if (GameManager.Instance.HasAugment("gold_low_hp_dmg"))
                multiplier += Mathf.Clamp01(1f - HealthRatio) * 0.20f;
            // silver_gold_attack: 所持金10ごとに+1%（上限+10%）
            if (GameManager.Instance.HasAugment("silver_gold_attack") && PlayerData.Instance != null)
                multiplier += Mathf.Clamp(PlayerData.Instance.Money * 0.001f, 0f, 0.10f);
            // silver_dupe_dmg: 同名ユニットを2体以上所持で +5%
            if (GameManager.Instance.HasAugment("silver_dupe_dmg") && GameManager.Instance.CountOwnedUnitsByUnitId(UnitId) >= 2)
                multiplier += 0.05f;
            // prism_king_blessed: 盤面で最高コストの味方なら +50% 与ダメ
            if (GameManager.Instance.HasAugment("prism_king_blessed") && GameManager.Instance.IsHighestCostOnBoard(this))
                multiplier += 0.50f;
            // silver_same_cost_bond: 同じコストの味方が盤面に2体以上いれば +3% 与ダメ
            if (GameManager.Instance.HasAugment("silver_same_cost_bond")
                && GameManager.Instance.CountSameCostBoardAllies(BaseCost) >= 2)
                multiplier += 0.03f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
    }

    // HPが一定以下になった時に1戦闘1回だけ発動するシナジーを確認します。
    private void CheckHealthThresholdSynergies()
    {
        if (SynergyManager.Instance == null || !IsInActiveRound())
            return;

        if (!warriorLastStandUsed
            && HasSynergy(SynergyType.Warrior)
            && SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Warrior, myTeam, 6)
            && HealthRatio <= 0.50f)
        {
            warriorLastStandUsed = true;
            ApplyTimedSynergyDamageReductionBonus(0.25f, 3f);
        }

        if (!machineEmergencyRepairUsed
            && HasSynergy(SynergyType.Machine)
            && SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Machine, myTeam, 4)
            && HealthRatio <= 0.30f)
        {
            machineEmergencyRepairUsed = true;
            HealFromSynergy(Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.15f)));
        }
    }

    // オーグメント on-hit proc（ソース側）: 通常攻撃ヒット時に確率で発動します。
    private void TryApplyAugmentOnHitProcs(BaseEntity target)
    {
        if (target == null || target.dead || myTeam != Team.Team1 || GameManager.Instance == null)
            return;
        if (GameManager.Instance.HasAugment("silver_slow_proc") && UnityEngine.Random.value < 0.05f)
            target.ApplyAttackSpeedSlow(0.7f, 2f);
        if (GameManager.Instance.HasAugment("silver_burn_proc") && UnityEngine.Random.value < 0.05f)
            target.ApplyInfernoBurnFromSynergy(this, Mathf.Max(2, Mathf.RoundToInt(GetCurrentDamage() * 0.10f)), 3f);
        if (GameManager.Instance.HasAugment("silver_zap_proc") && UnityEngine.Random.value < 0.05f)
            target.TakeDamage(Mathf.Max(2, Mathf.RoundToInt(GetCurrentDamage() * 0.30f)), this, CombatNumberKind.FocusDamage);
        if (GameManager.Instance.HasAugment("silver_stun_proc") && UnityEngine.Random.value < 0.05f)
            target.ApplyStun(1f);
    }

    // 固有ユニットの on-hit 効果（ソース側）。通常攻撃ヒット時に発動します。
    // 現状は Serpenti の Venom Frenzy が発動中なら、攻撃ごとに毒（Inferno DoT を流用）を付与します。
    private float serpentiVenomFrenzyUntil;
    private int serpentiVenomTickDamage;
    private float serpentiVenomDotDuration;

    private void TryApplyUnitOnHitProcs(BaseEntity target)
    {
        if (target == null || target.dead)
            return;

        if (Time.time < serpentiVenomFrenzyUntil && serpentiVenomTickDamage > 0)
        {
            target.ApplyInfernoBurnFromSynergy(this, serpentiVenomTickDamage, Mathf.Max(0.5f, serpentiVenomDotDuration));
        }
    }

    // 一定時間、対象が受けるダメージを増幅（Vulnerable）。
    // 内部実装は synergyDamageReductionBonus に負の値を加えることで、被ダメージ式 (1 - 軽減) を実質増幅させます。
    public void ApplyVulnerable(float increasePercent, float duration)
    {
        if (Mathf.Abs(increasePercent) <= 0.001f || duration <= 0f)
            return;
        ApplyTimedSynergyDamageReductionBonus(-Mathf.Abs(increasePercent), duration);
    }

    // 一定時間、対象の与ダメージを減衰（Weaken）。
    // ApplyTimedSynergyDamageDealtBonus にマイナスを渡すラッパー。
    public void ApplyWeaken(float decreasePercent, float duration)
    {
        if (Mathf.Abs(decreasePercent) <= 0.001f || duration <= 0f)
            return;
        ApplyTimedSynergyDamageDealtBonus(-Mathf.Abs(decreasePercent), duration);
    }

    // Serpenti の Venom Frenzy を起動: 持続時間中、通常攻撃ヒットごとに対象へ毒 DoT を付与します。
    public void ActivateSerpentiVenomFrenzy(int tickDamage, float dotDuration, float frenzyDuration)
    {
        serpentiVenomTickDamage = Mathf.Max(1, tickDamage);
        serpentiVenomDotDuration = Mathf.Max(0.5f, dotDuration);
        serpentiVenomFrenzyUntil = Time.time + Mathf.Max(0.1f, frenzyDuration);
    }

    // === オーグメント用の復活 ===
    // チャプター内で silver_revive_3 を消化済みか、ユニットごとに記録します。
    public bool SilverAugmentReviveUsed { get; set; }

    // オーグメントによる復活。HP比率を指定して蘇生します。
    public void AugmentReviveAtRatio(float hpRatio)
    {
        if (!dead)
            return;
        int hp = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * Mathf.Clamp(hpRatio, 0.05f, 1f)));
        baseHealth = hp;
        dead = false;
        canAttack = true;
        if (healthbar != null)
            healthbar.UpdateBar(baseHealth);
        SetAnimatorBool("walking", false);
        AttackEffectPlayer.PlayUiSfx("unit_buy");
    }

    // 被弾した瞬間に反応するアイテム効果です。シールドで防いだ場合でも「攻撃を受けた」として扱います。
    private void ApplyIncomingHitItemEffects(BaseEntity source, int incomingAmount)
    {
        if (incomingAmount <= 0 || !IsInActiveRound())
            return;

        if (HasEquippedItem(FrostguardPlateItemId) && IsEnemyEntity(source))
            source.ApplyAttackSpeedSlow(0.82f, 2f);

        if (HasEquippedItem(ThunderclapScepterItemId))
            TriggerThunderclapScepter();
    }

    // 雷鳴の王笏は4回被弾するたび、周囲の敵へ小さな雷撃を放ちます。
    private void TriggerThunderclapScepter()
    {
        thunderclapTakenHitCounter++;
        if (thunderclapTakenHitCounter < 4 || GameManager.Instance == null)
            return;

        thunderclapTakenHitCounter = 0;
        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        int thunderDamage = Mathf.Max(1, Mathf.RoundToInt(skillBasePower * 0.7f * GetStarSkillEffectMultiplier() * GetItemFocusMultiplier()));

        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                continue;

            if (Vector3.Distance(enemy.transform.position, transform.position) > 1.8f)
                continue;

            AttackEffectPlayer.PlaySkill(this, enemy, UnitSkillType.AreaDamage);
            enemy.TakeDamage(thunderDamage, this, CombatNumberKind.FocusDamage);
        }
    }

    // ユニットが受けたダメージを、頭上に浮かぶ数字として表示します。
    private void ShowDamageNumber(int amount)
    {
        ShowCombatNumber(amount, CombatNumberKind.AttackDamage);
    }

    // ダメージと回復を、種類に応じた色で大きく表示します。
    private void ShowCombatNumber(int amount, CombatNumberKind numberKind)
    {
        if (amount <= 0)
            return;

        GameObject textObject = new GameObject("DamageNumber");
        textObject.transform.position = transform.position + new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), 0.88f, -0.08f);

        TextMeshPro damageText = textObject.AddComponent<TextMeshPro>();
        damageText.text = numberKind == CombatNumberKind.Healing ? "+" + amount : amount.ToString();
        damageText.alignment = TextAlignmentOptions.Center;
        damageText.enableWordWrapping = false;
        damageText.fontSize = numberKind == CombatNumberKind.Healing ? 5.1f : 5.35f;
        damageText.fontStyle = FontStyles.Bold;
        damageText.outlineWidth = 0.22f;
        damageText.outlineColor = new Color(0f, 0f, 0f, 0.92f);
        damageText.color = GetCombatNumberColor(numberKind);

        MeshRenderer textRenderer = damageText.GetComponent<MeshRenderer>();
        if (textRenderer != null)
        {
            textRenderer.sortingLayerName = "Default";
            textRenderer.sortingOrder = CalculateSortingOrder(transform.position, 260);
        }

        DamageNumberFader fader = textObject.AddComponent<DamageNumberFader>();
        fader.Begin(damageText, textObject.transform, 1.02f);
    }

    // 数字色を戦闘で読み取りやすい3色に分けます。
    private Color GetCombatNumberColor(CombatNumberKind numberKind)
    {
        switch (numberKind)
        {
            case CombatNumberKind.Healing:
                return new Color(0.28f, 1f, 0.36f, 1f);
            case CombatNumberKind.FocusDamage:
                return new Color(0.22f, 0.78f, 1f, 1f);
            default:
                return new Color(1f, 0.58f, 0.12f, 1f);
        }
    }

    // 死亡アニメーションを待ってからGameObjectを破棄します。
    public void DestroyAfterDeathAnimation()
    {
        if (deathDestroyStarted)
            return;

        deathDestroyStarted = true;
        CancelPendingDeathStateChange();

        if (healthbar != null)
            healthbar.gameObject.SetActive(false);

        foreach (Collider2D entityCollider in GetComponents<Collider2D>())
            entityCollider.enabled = false;

        float deathAnimationLength = GetAnimationClipLength("Dead");
        if (!HasAnimatorParameter("dead", AnimatorControllerParameterType.Trigger) || deathAnimationLength <= 0f || !gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        deathStateCoroutine = StartCoroutine(DestroyAfterDelay(deathAnimationLength));
    }

    // 味方ユニット用です。死亡アニメーション後に破棄せず非表示にして、次ウェーブで復活できるようにします。
    public void WaitForWaveReviveAfterDeathAnimation()
    {
        if (deathDestroyStarted)
            return;

        deathDestroyStarted = true;
        CancelPendingDeathStateChange();

        if (healthbar != null)
            healthbar.gameObject.SetActive(false);

        foreach (Collider2D entityCollider in GetComponents<Collider2D>())
            entityCollider.enabled = false;

        float deathAnimationLength = GetAnimationClipLength("Dead");
        if (!HasAnimatorParameter("dead", AnimatorControllerParameterType.Trigger) || deathAnimationLength <= 0f || !gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        deathStateCoroutine = StartCoroutine(DeactivateAfterDelay(deathAnimationLength));
    }

    // ウェーブ終了後、指定Nodeに戻してHP満タンで戦闘前状態へ復帰します。
    public void RestoreForNextWave(Team team, Node restoreNode)
    {
        if (restoreNode == null)
            return;

        CancelPendingDeathStateChange();
        gameObject.SetActive(true);
        EnsureComponentReferences();
        RestoreAnimatorControllerIfMissing();

        if (currentNode != null && currentNode != restoreNode)
            currentNode.SetOccupied(false);

        restoreNode.SetOccupied(false);
        myTeam = team;
        currentNode = restoreNode;
        transform.position = restoreNode.worldPosition;
        restoreNode.SetOccupied(true);

        dead = false;
        deathDestroyStarted = false;
        baseHealth = MaxHealth;
        SetTarget(null);
        ClearMovementReservation();
        canAttack = true;
        ResetActionAnimatorState();
        RestartAnimatorPlayback();
        ResetMana();
        ClearShield();
        ClearTemporaryStatusEffects();

        foreach (Collider2D entityCollider in GetComponents<Collider2D>())
            entityCollider.enabled = true;

        ApplyTeamVisuals();
        EnsureHealthBar();
        if (healthbar != null)
        {
            healthbar.gameObject.SetActive(true);
            healthbar.Setup(transform, MaxHealth, StarLevel, spriteRender, this);
            healthbar.UpdateBar(baseHealth);
            healthbar.UpdateManaBar(currentMana, maxMana);
            UpdateHealthBarItemIcons();
        }

        ApplyStarVisualScale();
    }

    // HPが0以下になった時の死亡処理です。
    private void Die()
    {
        if (TryUseDivineProtection())
            return;

        if (SynergyManager.Instance != null && SynergyManager.Instance.TryReviveWraith(this))
            return;

        // R3-hero-depth: 主人公は将。ヒーローは1戦1回まで自動復活（復活後の再死亡で味方弱体）。
        if (GameManager.Instance != null && GameManager.Instance.TryConsumeHeroReviveForUnit(this))
            return;

        // オーグメント由来の復活（silver_revive_3 / prism_one_revive）。
        if (GameManager.Instance != null && GameManager.Instance.TryConsumeAugmentReviveForUnit(this))
            return;

        dead = true;
        if (IsChest) OpenChestOnce(); // HP0で開封（コイン箱=+5/アイテム箱=3択）。
        ApplySummonedDeathSlow();
        SynergyManager.Instance?.NotifyUnitDeath(this, lastDamageSource);

        // prism_kill_heal / prism_warrior_kill_buff: 敵が倒れた瞬間に、撃破者がプレイヤー側ならフックを叩きます。
        if (myTeam == Team.Team2 && lastDamageSource != null && lastDamageSource.myTeam == Team.Team1
            && GameManager.Instance != null)
            GameManager.Instance.NotifyEnemyKilledByPlayer(this, lastDamageSource);
        AttackEffectPlayer.PlayDeath(this);
        SetTarget(null);
        ClearMovementReservation();
        StopAttackAnimation();
        ClearShield();
        canAttack = false;
        SetAnimatorBool("walking", false);
        SetAnimatorTrigger("dead");

        if (currentNode != null)
        {
            currentNode.SetOccupied(false);
            currentNode = null;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.UnitDead(this);
        else
            DestroyAfterDeathAnimation();
    }

    // 通常攻撃を開始します。派生クラスから上書きできるようvirtualにしています。
    protected virtual void Attack()
    {
        if (!canAttack || dead)
            return;

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackSequenceCoroutine(currentTarget, false));
    }

    // 攻撃できる状態なら、マナ満タン時はスキル、そうでなければ通常攻撃を行います。
    protected void TryAttackCurrentTarget()
    {
        if (!canAttack || dead || currentTarget == null || currentTarget.dead)
            return;

        if (CanCastSkill())
        {
            CastSkill();
            return;
        }

        Attack();
    }

    // マナが最大まで溜まっているか確認します。
    private bool CanCastSkill()
    {
        return maxMana > 0 && currentMana >= maxMana;
    }

    // スキル攻撃を開始します。
    private void CastSkill()
    {
        if (currentTarget == null)
            return;

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackSequenceCoroutine(currentTarget, true));
    }

    // 攻撃アニメーションを再生し、指定タイミングで実際のダメージ/スキル効果を発生させます。
    private IEnumerator AttackSequenceCoroutine(BaseEntity targetAtCast, bool skillAttack)
    {
        canAttack = false;
        ClearMovementReservation();
        FaceTarget(targetAtCast);

        if (skillAttack)
            ResetMana();

        // アルカナのスキルは専用シーケンス：Ability を最後まで再生 → 最後の数コマで溜め → 魔法陣と共にダメージ。
        if (skillAttack && HasAnimationClip("Ability") && NormalizeUnitId(UnitId) == "arcana")
        {
            yield return ArcanaChargedSkillCoroutine(targetAtCast);
            attackCoroutine = null;
            if (!dead)
                canAttack = true;
            yield break;
        }

        // 攻撃速度から1回の攻撃にかける時間を決め、アニメーション再生速度も合わせます。
        waitBetweenAttack = GetAttackDuration();
        bool useAbilityAnimation = skillAttack && HasAnimationClip("Ability") && HasAnimatorParameter("ability", AnimatorControllerParameterType.Trigger);
        string clipName = useAbilityAnimation ? "Ability" : "Attack";
        float animationLength = GetAnimationClipLength(clipName);
        float actionDuration = Mathf.Max(0.05f, waitBetweenAttack);
        float playbackSpeed = animationLength > 0f ? Mathf.Clamp(animationLength / actionDuration, 0.2f, 8f) : 1f;

        BeginActionAnimation(useAbilityAnimation, playbackSpeed);

        // ダメージ判定はアニメーション途中で発生させます。
        float impactPoint = skillAttack ? skillImpactNormalizedTime : attackImpactNormalizedTime;
        float impactDelay = Mathf.Clamp(actionDuration * impactPoint, 0.03f, actionDuration);
        yield return new WaitForSeconds(impactDelay);

        if (skillAttack)
            ExecuteSkillEffect(targetAtCast);
        else
            ExecuteNormalAttack(targetAtCast);

        float remainingDelay = Mathf.Max(0f, actionDuration - impactDelay);
        if (remainingDelay > 0f)
            yield return new WaitForSeconds(remainingDelay);

        EndActionAnimation();
        attackCoroutine = null;

        if (!dead)
            canAttack = true;
    }

    // アルカナ専用のスキル再生です。Ability を必ず最後まで再生し、最後の数コマで「溜め」を作ってからダメージを出します。
    private IEnumerator ArcanaChargedSkillCoroutine(BaseEntity targetAtCast)
    {
        FaceTarget(targetAtCast);

        // 周期: 1〜3回目=Ability(溜め)→持続BloodMage設置 / 4回目=SpecialMove発動→フィナーレ。
        int upcomingPhase = (arcanaSkillCastCount % 4) + 1;

        if (upcomingPhase >= 4)
        {
            Vector3 arcanaBaseScale = transform.localScale;

            // 4回目: 必殺モーション SpecialMove を再生してからフィナーレ効果へ。
            // normalizedTime を見て Charge(等倍)→Activate(拡大＋低速＋放射光) を制御し、Special を抜けた瞬間に等倍へ戻します。
            if (HasAnimationClip("Special") && HasAnimatorParameter("special", AnimatorControllerParameterType.Trigger))
            {
                yield return ArcanaSpecialMotionCoroutine(targetAtCast, arcanaBaseScale);
            }
            else
            {
                BeginActionAnimation(true, 1f);
                yield return new WaitForSeconds(0.5f);
                if (!dead)
                    ExecuteSkillEffect(targetAtCast);
            }

            // フィナーレ中はユニットを拘束（IsAttackMotionPlaying のまま待機し、再詠唱・移動を防ぐ）。
            while (arcanaFinaleCoroutine != null && !dead)
                yield return null;

            // 念のため等倍・通常速度へ戻してから終了します。
            transform.localScale = arcanaBaseScale;
            SetAnimatorPlaybackSpeed(1f);
            EndActionAnimation();
            yield break;
        }

        // 1〜3回目: Ability を最後まで再生 → 溜め → 効果（持続BloodMage設置）。
        float abilityLength = GetAnimationClipLength("Ability");
        if (abilityLength <= 0.05f)
            abilityLength = 1.2f;
        float targetDuration = Mathf.Clamp(GetAttackDuration(), 0.85f, 1.6f);
        float playbackSpeed = Mathf.Clamp(abilityLength / targetDuration, 0.2f, 8f);

        BeginActionAnimation(true, playbackSpeed);
        yield return new WaitForSeconds(targetDuration);

        // 最後の数コマをループさせ、約0.4秒の溜めを作ります。
        yield return ArcanaHoldChargeCoroutine(0.4f);

        if (!dead)
            ExecuteSkillEffect(targetAtCast);

        EndActionAnimation();
    }

    // 4回目フィナーレの SpecialMove 再生制御です。normalizedTime を監視して、
    // Charge(前半)=等倍・通常速度 / Activate(後半)=拡大＋低速（終盤はさらに低速＋放射光の延長）にします。
    // Special を抜けた瞬間に等倍へ戻すので、直後に一回り大きい別モーションが見える不具合を防ぎます。
    private IEnumerator ArcanaSpecialMotionCoroutine(BaseEntity targetAtCast, Vector3 baseScale)
    {
        SetAnimatorBool("walking", false);
        SetAnimatorBool("attacking", false);
        SetAnimatorBool("abilitying", false);
        SetAnimatorPlaybackSpeed(1f);
        SetAnimatorTrigger("special");

        bool effectFired = false;
        bool lightSpawned = false;
        bool everEntered = false;
        float elapsed = 0f;
        const float timeout = 14f; // 低速再生でも収まる安全上限

        while (elapsed < timeout && !dead)
        {
            elapsed += Time.deltaTime;
            if (!CanUseAnimator())
                break;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            bool inSpecial = info.IsName("Special");
            if (inSpecial)
                everEntered = true;

            // Special へ遷移するまでは待機します。
            if (!everEntered)
            {
                yield return null;
                continue;
            }

            float t = inSpecial ? Mathf.Clamp01(info.normalizedTime) : 1f;

            if (inSpecial && t < 0.5f)
            {
                // Charge: 本体は元々大きく描かれているので等倍・通常速度。
                transform.localScale = baseScale;
                SetAnimatorPlaybackSpeed(1f);
            }
            else
            {
                // Activate: リング込みで本体が小さいので拡大。全体をやや低速にして目立たせます。
                transform.localScale = baseScale * ArcanaSpecialScaleCompensation;

                if (!effectFired)
                {
                    effectFired = true;
                    if (!dead)
                        ExecuteSkillEffect(targetAtCast); // → フィナーレ起動（吸い込み・明転）
                }

                if (t >= 0.85f)
                {
                    // 最後の数コマは特にゆっくり。放射状の光をスプライト外へ延長して締めます。
                    SetAnimatorPlaybackSpeed(0.3f);
                    if (!lightSpawned)
                    {
                        lightSpawned = true;
                        AttackEffectPlayer.PlayArcanaActivateLightRays(this, 1.6f);
                    }
                }
                else
                {
                    SetAnimatorPlaybackSpeed(0.65f);
                }
            }

            // Special を抜けた（Idle 等へ遷移した）瞬間に終了し、拡大したまま他モーションを見せないようにします。
            if (everEntered && !inSpecial && effectFired)
                break;
            if (inSpecial && t >= 0.995f)
                break;

            yield return null;
        }

        transform.localScale = baseScale;
        SetAnimatorPlaybackSpeed(1f);
    }

    // Ability の終盤コマ(0.9〜1.0)を小刻みに往復させ、力を溜める「溜め」を表現します。
    private IEnumerator ArcanaHoldChargeCoroutine(float duration)
    {
        if (!CanUseAnimator())
        {
            yield return new WaitForSeconds(Mathf.Max(0f, duration));
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && !dead)
        {
            // 0→1→0 を繰り返し、最後の数コマがフルフルと震える溜めにします。
            float wobble = Mathf.PingPong(elapsed * 7f, 1f);
            float normalized = Mathf.Lerp(0.9f, 0.999f, wobble);
            animator.speed = 0f;
            animator.Play("Ability", 0, normalized);
            animator.Update(0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        animator.speed = 1f;
    }

    // 通常攻撃のダメージ、エフェクト、マナ獲得を処理します。
    private void ExecuteNormalAttack(BaseEntity targetAtCast)
    {
        if (dead || targetAtCast == null || targetAtCast.dead)
            return;

        if (range > 1)
        {
            AttackEffectPlayer.PlayAttack(this, targetAtCast, true, () => CompleteNormalAttackHit(targetAtCast));
            return;
        }

        AttackEffectPlayer.PlayAttack(this, targetAtCast, false);
        CompleteNormalAttackHit(targetAtCast);
    }

    // 実際に通常攻撃が命中した瞬間の処理です。遠距離攻撃では弾が届いた時に呼ばれます。
    private void CompleteNormalAttackHit(BaseEntity targetAtCast)
    {
        if (this == null || dead || targetAtCast == null || targetAtCast.dead)
            return;

        // 着弾の SE と属性色スパーク（アーキタイプ別）。通常攻撃にも個性と手応えを足します。
        AttackEffectPlayer.PlayUnitAttackImpact(this, targetAtCast.transform.position);

        ApplyPreDamageNormalAttackItemEffects(targetAtCast);
        int damage = ApplyNormalAttackDamageItemEffects(targetAtCast, GetCurrentDamage());
        damage = ApplyNormalAttackDamageSynergyEffects(targetAtCast, damage);
        targetAtCast.TakeDamage(damage, this, CombatNumberKind.AttackDamage);
        ApplyAdjacentNormalAttackSplash(targetAtCast, damage);
        ApplyPostDamageNormalAttackItemEffects(targetAtCast, damage);
        ApplyPostDamageNormalAttackSynergyEffects(targetAtCast, damage);
        SynergyManager.Instance?.NotifyNormalAttackHit(this, targetAtCast, damage);
        GainMana(manaOnAttack + GetNormalAttackItemManaBonus());
    }

    // Borealjuggernautなど、一部の通常攻撃が隣接した敵にも当たる処理です。
    // 複数体に当たっても、マナ獲得は通常攻撃1回分だけです。
    private void ApplyAdjacentNormalAttackSplash(BaseEntity primaryTarget, int primaryDamage)
    {
        if (!normalAttackHitsAdjacentEnemies || primaryTarget == null || GameManager.Instance == null)
            return;

        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        int splashDamage = Mathf.Max(1, Mathf.RoundToInt(primaryDamage * adjacentAttackDamageMultiplier));
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy == primaryTarget || enemy.dead || !enemy.IsOnBoard)
                continue;

            if (Vector3.Distance(enemy.transform.position, primaryTarget.transform.position) > adjacentAttackRadius)
                continue;

            enemy.TakeDamage(splashDamage, this, CombatNumberKind.AttackDamage);
        }
    }

    // 通常攻撃のダメージ計算前に発動するアイテム効果です。
    private void ApplyPreDamageNormalAttackItemEffects(BaseEntity target)
    {
        if (target == null || target.dead)
            return;

        if (HasEquippedItem(SpineCleaverItemId))
            target.ApplySpineCleaverShred();
    }

    // 通常攻撃の威力そのものを変えるアイテム効果です。
    private int ApplyNormalAttackDamageItemEffects(BaseEntity target, int damageAmount)
    {
        if (!HasEquippedItem(AdamantineClawsItemId) || target == null)
            return damageAmount;

        if (adamantineTarget == target)
            adamantineStacks = Mathf.Min(5, adamantineStacks + 1);
        else
        {
            adamantineTarget = target;
            adamantineStacks = 1;
        }

        return Mathf.Max(1, Mathf.RoundToInt(damageAmount * (1f + adamantineStacks * 0.04f)));
    }

    // Ranger4など、通常攻撃の命中対象に応じて威力が変わるシナジーです。
    private int ApplyNormalAttackDamageSynergyEffects(BaseEntity target, int damageAmount)
    {
        if (target == null || SynergyManager.Instance == null || !IsInActiveRound())
            return damageAmount;

        if (HasSynergy(SynergyType.Ranger) && SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Ranger, myTeam, 4))
        {
            if (rangerFocusTarget == target)
                rangerFocusStacks = Mathf.Min(5, rangerFocusStacks + 1);
            else
            {
                rangerFocusTarget = target;
                rangerFocusStacks = 0;
            }

            damageAmount = Mathf.Max(1, Mathf.RoundToInt(damageAmount * (1f + rangerFocusStacks * 0.03f)));
        }

        return damageAmount;
    }

    // 通常攻撃が命中した後に発動するアイテム効果です。
    private void ApplyPostDamageNormalAttackItemEffects(BaseEntity primaryTarget, int dealtDamage)
    {
        if (HasEquippedItem(SkywindGlaivesItemId))
            TriggerSkywindGlaives(primaryTarget);

        if (HasEquippedItem(GodhammerItemId))
            TriggerGodhammer(primaryTarget);
    }

    // 通常攻撃後に発動するRanger/Beast系の追加効果です。
    private void ApplyPostDamageNormalAttackSynergyEffects(BaseEntity primaryTarget, int dealtDamage)
    {
        if (primaryTarget == null || SynergyManager.Instance == null || !IsInActiveRound())
            return;

        if (HasSynergy(SynergyType.Ranger)
            && SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Ranger, myTeam, 6)
            && UnityEngine.Random.value < 0.20f)
        {
            primaryTarget.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(dealtDamage * 0.50f)), this, CombatNumberKind.AttackDamage);
        }

        if (!HasSynergy(SynergyType.Beast) || !SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Beast, myTeam, 2))
            return;

        if (beastAttackSpeedStacks < 10)
        {
            beastAttackSpeedStacks++;
            synergyAttackSpeedBonus += 0.01f;
        }

        if (beastAttackSpeedStacks < 10)
            return;

        if (SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Beast, myTeam, 4) && primaryTarget != null && !primaryTarget.dead)
            primaryTarget.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 0.20f)), this, CombatNumberKind.AttackDamage);

        if (SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Beast, myTeam, 6))
            HealFromSynergy(Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.02f)));
    }

    // スキル後の短時間だけ、通常攻撃で追加マナを得ます。
    private int GetNormalAttackItemManaBonus()
    {
        return unboundedManaWindowActive && HasEquippedItem(UnboundedAmuletItemId) ? 6 : 0;
    }

    // 脊断ちの斧による軽減低下を対象側に蓄積します。
    private void ApplySpineCleaverShred()
    {
        if (dead)
            return;

        if (Time.time > spineCleaverShredUntil)
            spineCleaverShredStacks = 0;

        spineCleaverShredStacks = Mathf.Min(3, spineCleaverShredStacks + 1);
        spineCleaverShredUntil = Time.time + 3f;
    }

    // 天風の双刃は4回ごとに近くの別敵へ追加攻撃を飛ばします。
    private void TriggerSkywindGlaives(BaseEntity primaryTarget)
    {
        skywindAttackCounter++;
        if (skywindAttackCounter < 4)
            return;

        skywindAttackCounter = 0;
        BaseEntity secondaryTarget = FindNearestEnemyNear(primaryTarget != null ? primaryTarget.transform.position : transform.position, primaryTarget, 3f);
        if (secondaryTarget == null)
            return;

        int windDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 0.6f));
        AttackEffectPlayer.PlayAttack(this, secondaryTarget, true, () =>
        {
            if (this != null && !dead && secondaryTarget != null && !secondaryTarget.dead)
                secondaryTarget.TakeDamage(windDamage, this, CombatNumberKind.AttackDamage);
        });
    }

    // 神槌は5回目の通常攻撃に短いスタンを付けます。
    private void TriggerGodhammer(BaseEntity primaryTarget)
    {
        godhammerAttackCounter++;
        if (godhammerAttackCounter < 5)
            return;

        godhammerAttackCounter = 0;
        if (primaryTarget != null && !primaryTarget.dead)
            primaryTarget.ApplyStun(0.8f);
    }

    // 指定地点に近い、主対象以外の敵を探します。
    private BaseEntity FindNearestEnemyNear(Vector3 center, BaseEntity excludedTarget, float maxDistance)
    {
        if (GameManager.Instance == null)
            return null;

        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        BaseEntity nearestEnemy = null;
        float nearestDistance = maxDistance;
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy == excludedTarget || enemy.dead || !enemy.IsOnBoard)
                continue;

            float distance = Vector3.Distance(enemy.transform.position, center);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }

    // スキル種類に応じて、回復・シールド・バフ・デバフ・範囲攻撃などを実行します。
    private void ExecuteSkillEffect(BaseEntity targetAtCast)
    {
        if (dead)
            return;

        BeginItemSkillCast();
        try
        {
            if (TryExecuteDedicatedSkill(targetAtCast))
            {
                ApplyBossSignature(targetAtCast); // boss-skill-progression: 解放ボスのシグネチャ効果（Lv1常時／育成で拡張）。
                return;
            }

            switch (skillType)
            {
                case UnitSkillType.SelfHeal:
                    AttackEffectPlayer.PlaySkill(this, this, skillType);
                    HealSelf(GetSkillHealAmount());
                    break;
                case UnitSkillType.AllyHeal:
                    BaseEntity healedAlly = HealAlly(GetSkillAllyHealAmount());
                    AttackEffectPlayer.PlaySkill(this, healedAlly, skillType);
                    break;
                case UnitSkillType.Shield:
                    AttackEffectPlayer.PlaySkill(this, this, skillType);
                    ApplyShield(GetSkillShieldAmount(), GetSkillDuration(skillShieldDuration, true));
                    break;
                case UnitSkillType.AttackSpeedBoost:
                    AttackEffectPlayer.PlaySkill(this, this, skillType);
                    if (ShouldApplySupportAura())
                        ApplyAttackSpeedBoostToNearbyAllies(1f + GetSkillBoostAmount(skillAttackSpeedBoostMultiplier), GetSkillDuration(skillBuffDuration, true));
                    else
                        ApplyAttackSpeedBoost(1f + GetSkillBoostAmount(skillAttackSpeedBoostMultiplier), GetSkillDuration(skillBuffDuration, true));
                    break;
                case UnitSkillType.Stun:
                    if (targetAtCast != null && !targetAtCast.dead)
                    {
                        AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
                        targetAtCast.ApplyStun(GetSkillDuration(skillStunDuration, false));
                    }
                    break;
                case UnitSkillType.Slow:
                    if (targetAtCast != null && !targetAtCast.dead)
                    {
                        AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
                        targetAtCast.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), GetSkillDuration(skillSlowDuration, true));
                    }
                    break;
                case UnitSkillType.DamageBoost:
                    AttackEffectPlayer.PlaySkill(this, this, skillType);
                    if (ShouldApplySupportAura())
                        ApplyDamageBoostToNearbyAllies(1f + GetSkillBoostAmount(skillDamageBoostMultiplier), GetSkillDuration(skillBuffDuration, true));
                    else
                        ApplyDamageBoost(1f + GetSkillBoostAmount(skillDamageBoostMultiplier), GetSkillDuration(skillBuffDuration, true));
                    break;
                case UnitSkillType.AreaDamage:
                    ApplyAreaDamage(targetAtCast);
                    break;
                default:
                    if (targetAtCast != null && !targetAtCast.dead)
                    {
                        int skillDamage = CalculateSingleTargetSkillDamage();
                        AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
                        targetAtCast.TakeDamage(skillDamage, this, GetSkillDamageNumberKind());
                        SynergyManager.Instance?.NotifySkillDamageHit(this, targetAtCast, skillDamage);
                    }
                    break;
            }
        }
        finally
        {
            FinishItemSkillCast();
        }
    }

    // スキル1回分にだけ乗るアイテム倍率を準備します。
    private void BeginItemSkillCast()
    {
        currentItemSkillEffectMultiplier = 1f;

        if (HasEquippedItem(YkirStaffItemId) && !ykirFirstSkillConsumed)
        {
            currentItemSkillEffectMultiplier *= 1.3f;
            ykirFirstSkillConsumed = true;
        }
    }

    // スキル発動後に発動するアイテム効果をまとめて処理します。
    private void FinishItemSkillCast()
    {
        SynergyManager.Instance?.NotifySkillCast(this);

        if (HasEquippedItem(RepairStaffItemId))
            TriggerRepairStaffHeal();

        if (HasEquippedItem(UnboundedAmuletItemId))
            StartUnboundedAmuletWindow();

        if (HasSynergy(SynergyType.Arcanist)
            && SynergyManager.Instance != null
            && SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Arcanist, myTeam, 4))
            GainManaFromSynergy(20);

        currentItemSkillEffectMultiplier = 1f;
    }

    // 修復の杖はスキルを撃つたび、最も傷ついた味方を追加で支援します。
    private void TriggerRepairStaffHeal()
    {
        BaseEntity ally = GetLowestHealthAlly();
        if (ally == null)
            return;

        int healAmount = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.055f * GetSkillEffectMultiplier(true) * GlobalHealingMultiplier));
        AttackEffectPlayer.PlaySkill(this, ally, UnitSkillType.AllyHeal);
        ally.HealSelf(healAmount);
    }

    // 無限の護符はスキル後の5秒だけ、通常攻撃マナ獲得を追加します。
    private void StartUnboundedAmuletWindow()
    {
        unboundedManaWindowActive = true;

        if (unboundedAmuletCoroutine != null)
            StopCoroutine(unboundedAmuletCoroutine);

        unboundedAmuletCoroutine = StartCoroutine(ClearUnboundedAmuletWindowAfterDelay(5f));
    }

    // 無限の護符の追加マナ時間を終了します。
    private IEnumerator ClearUnboundedAmuletWindowAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        unboundedAmuletCoroutine = null;
        unboundedManaWindowActive = false;
    }

    // 一部ユニットだけが持つ、通常のスキル種別より踏み込んだ専用挙動です。
    private bool TryExecuteDedicatedSkill(BaseEntity targetAtCast)
    {
        string id = NormalizeUnitId(UnitId);
        switch (id)
        {
            case "archdeacon":
                ExecuteArchdeaconHolyEdict();
                return true;
            case "backlinearcher":
                ExecuteBacklineArcherPiercingVolley(targetAtCast);
                return true;
            case "auroralioness":
                ExecuteAuroralionessGuard();
                return true;
            case "azuritelion":
                ExecuteAzuriteLionFrostPounce(targetAtCast);
                return true;
            case "altgeneraltier2":
                ExecuteAltgeneralTwinElement(targetAtCast);
                return true;
            case "sandpanther":
                ExecuteSandpantherAmbush(targetAtCast);
                return true;
            case "protector":
                ExecuteProtectorBulwarkLink();
                return true;
            case "taskmaster":
                ExecuteTaskmasterWhipCommand(targetAtCast);
                return true;
            case "kane":
                ExecuteKaneStormTurret(targetAtCast);
                return true;
            case "malyk":
                ExecuteMalykSoulDrain(targetAtCast);
                return true;
            case "paragon":
                ExecuteParagonAegis();
                return true;
            case "ilenamk2":
                ExecuteIlenaCrystalLattice(targetAtCast);
                return true;
            case "wujin":
                ExecuteWujinImperialPyre();
                return true;
            case "wraith":
                ExecuteWraithGraveBlizzard(targetAtCast);
                return true;
            case "snowchasermk":
                ExecuteSnowchaserRelay();
                return true;
            case "solfist":
                ExecuteSolfistSolarCombo(targetAtCast);
                return true;
            case "maehvmk":
                ExecuteMaehvRailArc(targetAtCast);
                return true;
            case "decepticleprime":
                ExecuteDecepticleprimePrismBattery(targetAtCast);
                return true;
            case "tier2general":
                ExecuteTier2GeneralGlacialCommand(targetAtCast);
                return true;
            case "shadowlord":
                ExecuteAssassinLeapStrike(GetFarthestEnemy() ?? targetAtCast, 0.75f, false);
                return true;
            case "skindogehai":
                ExecuteAssassinLeapStrike(GetFarthestEnemy() ?? targetAtCast, 0.55f, true);
                return true;
            case "embergeneral":
                ExecuteEmbergeneralRoyalCommand();
                return true;
            case "kron":
                ExecuteKronJudgementScale();
                return true;
            case "invader":
                if (invaderThunderCoroutine != null)
                    StopCoroutine(invaderThunderCoroutine);

                invaderThunderCoroutine = StartCoroutine(ExecuteInvaderThunderGodCoroutine());
                return true;
            case "gol":
                if (golBlackHoleCoroutine != null)
                    StopCoroutine(golBlackHoleCoroutine);

                golBlackHoleCoroutine = StartCoroutine(ExecuteGolBlackHoleCoroutine(targetAtCast));
                return true;
            case "legion":
                ExecuteLegionDeadMarch();
                return true;
            case "plaguegeneral":
                ExecutePlaguegeneralRoar();
                return true;
            case "skyfalltyrant":
                if (skyfallRampageCoroutine != null)
                    StopCoroutine(skyfallRampageCoroutine);

                skyfallRampageCoroutine = StartCoroutine(ExecuteSkyfallDragonRampageCoroutine(targetAtCast));
                return true;
            // --- DESIGN_skill_overhaul.md: 17体の固有スキル ---
            case "andromeda":
                ExecuteAndromedaStardustBarrage(targetAtCast);
                return true;
            case "antiswarm":
                ExecuteAntiswarmSwarmCall();
                return true;
            case "borealjuggernaut":
                ExecuteBorealjuggernautFrostguardBastion();
                return true;
            case "chaosknight":
                ExecuteChaosknightChaosBrand(targetAtCast);
                return true;
            case "christmas":
                ExecuteChristmasFestiveRally();
                return true;
            case "vampire":
                ExecuteVampireSoulpiercer(targetAtCast);
                return true;
            case "valiant":
                ExecuteValiantAegisSmite(targetAtCast);
                return true;
            case "candypanda":
                ExecuteCandypandaSugarFeast();
                return true;
            case "city":
                if (citySupplyDroneCoroutine != null)
                    StopCoroutine(citySupplyDroneCoroutine);
                citySupplyDroneCoroutine = StartCoroutine(ExecuteCitySupplyDroneCoroutine());
                return true;
            case "crystal":
                ExecuteCrystalCrystalWard(targetAtCast);
                return true;
            case "cindera":
                ExecuteCinderaEmberSight(targetAtCast);
                return true;
            case "decepticle":
                ExecuteDecepticleAssaultMode();
                return true;
            case "umbra":
                ExecuteUmbraUmbralDevour();
                return true;
            case "spelleater":
                ExecuteSpelleaterSpellEater(targetAtCast);
                return true;
            case "serpenti":
                ExecuteSerpentiVenomFrenzy();
                return true;
            case "decepticlechassis":
                ExecuteDecepticlechassisArmorReassembly();
                return true;
            case "wolfpunch":
                ExecuteWolfpunchAlphaStrike(targetAtCast);
                return true;
            case "arcana":
                ExecuteArcanaTomeOfFinality(targetAtCast);
                return true;
            // --- skill-fill: 章ボスの固有スキル（強撃→固有化, DESIGN_skill-fill.md） ---
            case "caliber":
                ExecuteCaliberEndfallSweep();
                return true;
            case "neutral_rook":
                ExecuteRookStasisGate();
                return true;
            case "neutral_sister":
                ExecuteSisterRequiemDirge();
                return true;
            // --- skill-fill: 勧誘可能な中立ユニットの固有スキル ---
            case "neutral_beastmaster":
                ExecuteBeastmasterBeastRoar();
                return true;
            case "neutral_gnasher":
                ExecuteGnasherGnash(targetAtCast);
                return true;
            case "neutral_rawr":
                ExecuteRawrIntimidate();
                return true;
            case "neutral_rok":
                ExecuteRokBoulderToss(targetAtCast);
                return true;
            case "neutral_zukong":
                ExecuteZukongStaffFlurry();
                return true;
            // --- skill-fill: Songhai新4体の固有スキル ---
            case "lanternfox":
                ExecuteLanternfoxLanternGuide();
                return true;
            case "onyxjaguar":
                ExecuteOnyxjaguarOnyxPounce(targetAtCast);
                return true;
            case "keshraifanblade":
                ExecuteKeshraiFanBladeDance(targetAtCast);
                return true;
            case "firewyrm":
                ExecuteFirewyrmFireBreath(targetAtCast);
                return true;
            // --- skill-fill: 後半章ボス（Magmar/Abyssian/Vetruvian/Mecha/Hydrax） ---
            case "magmarvaath": ExecuteMagmarvaathMagmaEruption(targetAtCast); return true;
            case "magmarstarhorn": ExecuteMagmarstarhornCharge(targetAtCast); return true;
            case "magmarragnora": ExecuteMagmarragnoraSearingRegen(); return true;
            case "abyssallilithe": ExecuteAbyssallilitheLifebloomDrain(targetAtCast); return true;
            case "abyssalcassyva": ExecuteAbyssalcassyvaShadowSwarm(); return true;
            case "abyssalmaehv": ExecuteAbyssalmaehvAbyssArc(); return true;
            case "vetruvianzirix": ExecuteVetruvianzirixSandbladeStorm(); return true;
            case "vetruviansajj": ExecuteVetruviansajjSolarSigil(targetAtCast); return true;
            case "vetruvianscion": ExecuteVetruvianscionErosionPierce(targetAtCast); return true;
            case "neutral_mechaz0rwing": ExecuteMechaWingStrikeGlide(); return true;
            case "neutral_mechaz0rsword": ExecuteMechaSwordSteelFlurry(targetAtCast); return true;
            case "neutral_mechaz0rsuper": ExecuteMechaSuperOverdrive(); return true;
            case "neutral_mechaz0rhelm": ExecuteMechaHelmAegisField(); return true;
            case "neutral_mechaz0rchassis": ExecuteMechaChassisBulwarkCannons(targetAtCast); return true;
            case "neutral_mechaz0rcannon": ExecuteMechaCannonFullSalvo(targetAtCast); return true;
            case "neutral_hydrax": ExecuteHydraxTorrentMaw(targetAtCast); return true;
            // --- cost4 追加5体の固有スキル（DESIGN_cost4-units.md） ---
            case "grymbeast":
                ExecuteGrymbeastShadowMaul(targetAtCast);
                return true;
            case "cinderwraith":
                ExecuteCinderwraithPyreFrenzy();
                return true;
            case "draugarlord":
                ExecuteDraugarlordGlacialSalvo(targetAtCast);
                return true;
            case "kingsguard":
                ExecuteKingsguardAegisDecree();
                return true;
            case "dissonance":
                ExecuteDissonanceArcCacophony(targetAtCast);
                return true;
            // --- ヒーロー専用3体（DESIGN_R3-hero-units） ---
            case "heroaldin":
                ExecuteAldinSacredWall();
                return true;
            case "herokagachi":
                ExecuteKagachiAfterimageSlash(targetAtCast);
                return true;
            case "herovesna":
                ExecuteVesnaAzureLance(targetAtCast);
                return true;
            default:
                return false;
        }
    }

    // ヒーロー Aldin（聖騎士・守護）専用スキル「聖壁」。自分と周囲の味方へシールド＋小回復＋被ダメ軽減を配る。
    private void ExecuteAldinSacredWall()
    {
        int shieldAmount = GetSkillShieldAmount();
        int healAmount = Mathf.RoundToInt(GetSkillAllyHealAmount() * 0.6f);
        float duration = GetSkillDuration(4.0f, true);
        float radius = Mathf.Max(1.7f, skillAreaRadius);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.15f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.85f, 0.9f);

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) > radius)
                continue;
            bool self = ally == this;
            ally.ApplyShield(self ? shieldAmount : Mathf.RoundToInt(shieldAmount * 0.6f), duration);
            if (healAmount > 0)
                ally.HealSelf(self ? healAmount : Mathf.RoundToInt(healAmount * 0.5f));
            ally.ApplyTimedSynergyDamageReductionBonus(0.08f * GetSkillEffectMultiplier(false), duration);
        }
    }

    // ヒーロー Kagachi（アサシン）専用スキル「残影斬」。最遠の敵へ瞬時に踏み込み2連斬（★3で3連）、直後に短時間の自己加速。
    private void ExecuteKagachiAfterimageSlash(BaseEntity targetAtCast)
    {
        BaseEntity target = GetFarthestEnemy() ?? GetValidSkillTarget(targetAtCast);
        if (target == null || target.dead)
            return;

        BlinkToTargetSide(target);
        SetTarget(target);
        FaceTarget(target);
        untargetableUntil = Time.time + GetSkillDuration(0.5f, false);

        int hitDamage = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 0.62f));
        int hits = StarLevel >= 3 ? 3 : 2;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Shadow, target.transform.position, 1.0f);
        for (int h = 0; h < hits; h++)
        {
            if (target == null || target.dead)
                break;
            DealLegendarySkillDamage(target, hitDamage, CombatNumberKind.AttackDamage);
            SynergyManager.Instance?.NotifySkillDamageHit(this, target, hitDamage);
        }
        // 自己加速（アサシンの手数）。
        ApplyAttackSpeedBoostFromSynergy(1f + 0.30f * GetSkillEffectMultiplier(false), GetSkillDuration(3f, true));
    }

    // ヒーロー Vesna（蒼魔・蒼炎）専用スキル「蒼炎槍」。対象中心に青い炎のAoE＋燃焼。遠隔なので効果はキャスター周囲でなく対象中心。
    private void ExecuteVesnaAzureLance(BaseEntity targetAtCast)
    {
        BaseEntity target = GetValidSkillTarget(targetAtCast);
        if (target == null)
            return;

        int damage = CalculateAreaSkillDamage();
        float radius = Mathf.Max(1.7f, skillAreaRadius);
        FaceTarget(target);
        // 青い炎の演出（暫定: Frost=青 と Inferno=炎 を重ねて「蒼炎」に見せる。TODO: 専用の青炎VFXへ差し替え）。
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position, 1.1f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, target.transform.position + Vector3.up * 0.1f, 0.85f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.8f, 0.9f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;
            DealLegendarySkillDamage(enemy, enemy == target ? damage : Mathf.RoundToInt(damage * 0.6f), CombatNumberKind.FocusDamage);
            enemy.ApplyInfernoBurnFromSynergy(this, Mathf.RoundToInt(damage * 0.20f), GetSkillDuration(3f, true));
        }
    }

    // アルカナのスキル詠唱回数。1〜3回目で持続BloodMageを設置し、4回目でフィナーレへ。
    private int arcanaSkillCastCount = 0;
    // 1〜3回目で設置した、まだ盤面に残っている持続BloodMageの実体です。4回目に end で消します。
    private readonly List<GameObject> arcanaActiveBloodMages = new List<GameObject>();
    // フィナーレ進行中のコルーチン。詠唱モーション側はこれが終わるまでユニットを拘束します。
    private Coroutine arcanaFinaleCoroutine;
    // SpecialMove_Activate は魔法陣リング込みで本体が一回り小さく描かれるため、その間だけ本体を拡大して見た目を揃える補正倍率。
    private const float ArcanaSpecialScaleCompensation = 1.5f;

    // アルカナ（最終ボス）専用スキル「終焉の書」です。
    // 1〜3回目: 攻撃対象の位置に持続BloodMageを設置（弱い継続ダメ＋ゆっくり吸引、戦闘終了まで残る）。
    // 4回目: 既存BloodMageを消し、SpecialMove発動＋明転＋盤面全体BloodMageで吸い込み3.5秒の大ダメージ（★3は吸込時に全滅）。
    private void ExecuteArcanaTomeOfFinality(BaseEntity targetAtCast)
    {
        arcanaSkillCastCount++;
        int phase = ((arcanaSkillCastCount - 1) % 4) + 1; // 1,2,3,4 の周期

        if (phase < 4)
        {
            SpawnArcanaPersistentBloodMage(targetAtCast);
            // Arcanist: 次の詠唱へ繋ぐための小さなマナ回収。
            GainManaFromSynergy(18);
            return;
        }

        // 4回目: フィナーレを起動（詠唱コルーチンが arcanaFinaleCoroutine の完了を待ちます）。
        // 終焉シナジーの倍率は、BloodMageを消す前のこの時点で確定させます。
        float finalityBonus = ArcanaFinalityBonus();
        if (arcanaFinaleCoroutine != null)
            StopCoroutine(arcanaFinaleCoroutine);
        arcanaFinaleCoroutine = StartCoroutine(ExecuteArcanaFinaleCoroutine(finalityBonus));
        GainManaFromSynergy(Mathf.RoundToInt(45 * finalityBonus));
    }

    // 終焉シナジー: 盤面に残るBloodMageの数に応じてアルカナのスキルを強化します（1体ごと +15%）。
    // アルカナだけが持つ専用シナジー。1体で常時発動し、BloodMageを積むほど火力とマナ回収が伸びます。
    private float ArcanaFinalityBonus()
    {
        if (!HasSynergy(SynergyType.Finality))
            return 1f;
        if (SynergyManager.Instance != null && !SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Finality, myTeam, 1))
            return 1f;
        return 1f + 0.15f * arcanaActiveBloodMages.Count;
    }

    // 1〜3回目: 攻撃対象の位置に持続BloodMageを置き、近くの敵を弱く継続ダメージ＋ゆっくり吸引します。
    private void SpawnArcanaPersistentBloodMage(BaseEntity targetAtCast)
    {
        Vector3 center = (targetAtCast != null && !targetAtCast.dead)
            ? targetAtCast.transform.position
            : (currentTarget != null && !currentTarget.dead ? currentTarget.transform.position : transform.position);
        center.z -= 0.02f;

        Color blood = new Color(0.85f, 0.07f, 0.1f, 1f);
        AttackEffectPlayer.PlayArcanaBloodCircle(center, 1.2f, false);
        AttackEffectPlayer.PlayCue("synergy_abyss", 0.5f);

        GameObject visual = AttackEffectPlayer.SpawnBloodMage(center, 1.6f, blood, 84);
        if (visual == null)
            return;

        arcanaActiveBloodMages.Add(visual);
        // 毎秒 CalculateAreaSkillDamage の約12%の弱い継続ダメ。半径2マス・ゆっくり吸引。
        // 終焉シナジーで、盤面のBloodMageが増えるほど後から置いた分も強くなります。
        int damagePerSecond = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 0.12f * ArcanaFinalityBonus()));
        StartCoroutine(ArcanaBloodMagePullDamageCoroutine(visual, center, 2f, damagePerSecond));
    }

    // 持続BloodMage本体。visualが残っていて戦闘中の間、半径内の敵をゆっくり吸引しつつ継続ダメージを与えます。
    private IEnumerator ArcanaBloodMagePullDamageCoroutine(GameObject visual, Vector3 center, float radius, int damagePerSecond)
    {
        const float tickInterval = 0.5f;
        int perTick = Mathf.Max(1, Mathf.RoundToInt(damagePerSecond * tickInterval));
        const float pullStep = 0.6f; // ゆっくり（マス/秒の目安）
        float sinceTick = 0f;

        while (visual != null && !dead && IsInActiveRound())
        {
            List<BaseEntity> enemies = GetActiveEnemies();
            for (int i = 0; i < enemies.Count; i++)
            {
                BaseEntity enemy = enemies[i];
                if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                    continue;
                if (Vector3.Distance(enemy.transform.position, center) > radius)
                    continue;

                Vector3 next = Vector3.MoveTowards(enemy.transform.position, center, pullStep * Time.deltaTime);
                enemy.transform.position = new Vector3(next.x, next.y, enemy.transform.position.z);
            }

            sinceTick += Time.deltaTime;
            if (sinceTick >= tickInterval)
            {
                sinceTick -= tickInterval;
                for (int i = 0; i < enemies.Count; i++)
                {
                    BaseEntity enemy = enemies[i];
                    if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                        continue;
                    if (Vector3.Distance(enemy.transform.position, center) > radius)
                        continue;

                    DealLegendarySkillDamage(enemy, perTick, CombatNumberKind.FocusDamage);
                }
            }

            yield return null;
        }
    }

    // 4回目フィナーレ本体。明転・BGMダック・スプライト発光・盤面全体BloodMageによる吸引大ダメージを順に行います。
    // finalityBonus は BloodMage を消す前に確定させた終焉シナジー倍率です。
    private IEnumerator ExecuteArcanaFinaleCoroutine(float finalityBonus)
    {
        bool star3 = StarLevel >= 3;

        // 既存の持続BloodMageを end で締めます。
        for (int i = 0; i < arcanaActiveBloodMages.Count; i++)
            AttackEffectPlayer.EndBloodMage(arcanaActiveBloodMages[i]);
        arcanaActiveBloodMages.Clear();

        // スプライト発光＋足元の血の魔法陣＋カメラ揺れで溜めを見せます。
        AttackEffectPlayer.PlayArcanaSpriteGlow(this, 2.4f, 0.9f);
        AttackEffectPlayer.PlayArcanaBloodCircle(transform.position, 2.6f, true);
        AttackEffectPlayer.ShakeCamera(0.5f, 0.12f);
        AttackEffectPlayer.PlayCue("synergy_abyss", 0.85f);

        // 画面明転（白）＋BGMフェードアウト。
        AttackEffectPlayer.FadeBgm(0f, 0.35f);
        AttackEffectPlayer.FlashScreen(new Color(1f, 1f, 1f, 0.92f), 0.18f, 0.14f, 0.7f);

        yield return new WaitForSeconds(0.35f);

        // 盤面全体を覆うBloodMageを中央に出現させ、全敵を吸い込みます。
        Vector3 boardCenter = GetArcanaFinaleCenter();
        Color blood = new Color(0.8f, 0.05f, 0.08f, 1f);
        GameObject boardMage = AttackEffectPlayer.SpawnBloodMage(boardCenter, 6f, blood, 120);
        AttackEffectPlayer.PlayCue("skill_area", 0.9f);

        const float duration = 3.5f;
        const float pullStep = 3.2f; // 速い吸引
        const float tickInterval = 0.35f;
        int areaDamage = CalculateAreaSkillDamage();
        int perTick = Mathf.Max(1, Mathf.RoundToInt(areaDamage * (star3 ? 0.6f : 0.32f) * finalityBonus));
        float elapsed = 0f;
        float sinceTick = 0f;
        float sinceShake = 0f;
        bool annihilated = false;

        while (elapsed < duration && !dead && IsInActiveRound())
        {
            elapsed += Time.deltaTime;

            // 画面を揺らし続けます。
            sinceShake += Time.deltaTime;
            if (sinceShake >= 0.3f)
            {
                sinceShake = 0f;
                AttackEffectPlayer.ShakeCamera(0.28f, 0.08f);
            }

            List<BaseEntity> enemies = GetActiveEnemies();
            for (int i = 0; i < enemies.Count; i++)
            {
                BaseEntity enemy = enemies[i];
                if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                    continue;
                Vector3 next = Vector3.MoveTowards(enemy.transform.position, boardCenter, pullStep * Time.deltaTime);
                enemy.transform.position = new Vector3(next.x, next.y, enemy.transform.position.z);
            }

            // ★3: 吸い込んで中央に集まったタイミングで全滅させます。
            if (star3 && !annihilated && elapsed >= 1.0f)
            {
                annihilated = true;
                AttackEffectPlayer.FlashScreen(new Color(0.9f, 0.05f, 0.08f, 0.85f), 0.1f, 0.06f, 0.5f);
                AttackEffectPlayer.ShakeCamera(0.5f, 0.2f);
                for (int i = 0; i < enemies.Count; i++)
                {
                    BaseEntity enemy = enemies[i];
                    if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                        continue;
                    // 確実に消滅させるため最大HPを大きく上回るダメージ。
                    DealLegendarySkillDamage(enemy, enemy.MaxHealth * 100 + 99999, CombatNumberKind.FocusDamage);
                }
            }

            // ★1/★2: 即死させず継続大ダメージのみ（反撃の余地を残す）。
            if (!star3)
            {
                sinceTick += Time.deltaTime;
                if (sinceTick >= tickInterval)
                {
                    sinceTick -= tickInterval;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        BaseEntity enemy = enemies[i];
                        if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                            continue;
                        DealLegendarySkillDamage(enemy, perTick, CombatNumberKind.FocusDamage);
                    }
                }
            }

            yield return null;
        }

        // 盤面BloodMageを end で締め、BGMを元の音量へ戻します。
        AttackEffectPlayer.EndBloodMage(boardMage);
        AttackEffectPlayer.FadeBgm(1f, 0.8f);
        arcanaFinaleCoroutine = null;
    }

    // フィナーレの吸引中心。生存中の敵の平均位置（いなければ自分の位置）を使います。
    private Vector3 GetArcanaFinaleCenter()
    {
        List<BaseEntity> enemies = GetActiveEnemies();
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                continue;
            sum += enemy.transform.position;
            count++;
        }

        Vector3 center = count > 0 ? sum / count : transform.position;
        center.z -= 0.02f;
        return center;
    }

    // アサシン系スキルです。後衛側の敵付近へ瞬間移動し、大ダメージを与えます。
    private void ExecuteAssassinLeapStrike(BaseEntity target, float untargetableDuration, bool stunTarget)
    {
        if (target == null || target.dead)
            return;

        BlinkToTargetSide(target);
        SetTarget(target);
        FaceTarget(target);
        untargetableUntil = Time.time + Mathf.Max(0f, untargetableDuration);

        AttackEffectPlayer.PlaySkill(this, target, skillType);
        int skillDamage = CalculateSingleTargetSkillDamage();
        target.TakeDamage(skillDamage, this, GetSkillDamageNumberKind());
        SynergyManager.Instance?.NotifySkillDamageHit(this, target, skillDamage);

        if (stunTarget)
            target.ApplyStun(GetSkillDuration(skillStunDuration, false));
    }

    // 対象の周囲に空きNodeがあれば、そこへ一瞬で移動します。
    private void BlinkToTargetSide(BaseEntity target)
    {
        if (target == null || target.CurrentNode == null || currentNode == null || GridManager.Instance == null)
            return;

        ClearMovementReservation();
        List<Node> candidates = GridManager.Instance.GetNodesCloseTo(target.CurrentNode)
            .Where(node => node != null && (!node.IsOccupied || node == currentNode))
            .OrderBy(node => Vector3.Distance(node.worldPosition, transform.position))
            .ToList();

        if (candidates.Count == 0)
            return;

        Node destinationNode = candidates[0];
        if (currentNode != null && currentNode != destinationNode)
            currentNode.SetOccupied(false);

        destinationNode.SetOccupied(true);
        currentNode = destinationNode;
        transform.position = destinationNode.worldPosition;
        moving = false;
        destination = null;
    }

    // 自分から最も遠い敵を返します。アサシンが後衛を狙うために使います。
    private BaseEntity GetFarthestEnemy()
    {
        if (GameManager.Instance == null)
            return null;

        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        BaseEntity farthestEnemy = null;
        float farthestDistance = -1f;
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || !enemy.IsOnBoard || !enemy.CanBeTargeted)
                continue;

            float distance = Vector3.Distance(enemy.transform.position, transform.position);
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthestEnemy = enemy;
            }
        }

        return farthestEnemy;
    }

    // Archdeacon専用スキルです。弱った味方へ回復と小さな守りをまとめて配ります。
    private void ExecuteArchdeaconHolyEdict()
    {
        BaseEntity mainAlly = GetLowestHealthAlly() ?? this;
        int healAmount = GetSkillAllyHealAmount();
        int shieldAmount = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.11f * GetSkillEffectMultiplier(true)));
        float duration = GetSkillDuration(3.8f, true);
        float radius = Mathf.Max(2.1f, skillAreaRadius);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, mainAlly.transform.position, 1.05f);
        AttackEffectPlayer.PlayAreaIndicator(this, mainAlly.transform.position, radius, 0.8f, 0.9f);
        mainAlly.HealSelf(healAmount);
        mainAlly.ApplyShield(shieldAmount, duration);

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, mainAlly.transform.position) > radius)
                continue;

            if (ally != mainAlly)
            {
                ally.HealSelf(Mathf.RoundToInt(healAmount * 0.45f));
                ally.ApplyShield(Mathf.RoundToInt(shieldAmount * 0.55f), duration);
            }

            ally.ApplyTimedSynergyDamageReductionBonus(0.06f * GetSkillEffectMultiplier(false), duration);
        }
    }

    // Backline Archer専用スキルです。横一列へ貫通矢を放ち、後衛らしい範囲射撃にします。
    private void ExecuteBacklineArcherPiercingVolley(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int mainDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 1.65f * GetSkillEffectMultiplier(true)));
        int splashDamage = Mathf.Max(1, Mathf.RoundToInt(mainDamage * 0.62f));
        float rowTolerance = 0.75f;
        float maxDistance = Mathf.Max(range + 1f, 4.5f);

        FaceTarget(target);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.AreaDamage);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Ranger, target.transform.position, 0.9f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, 1.25f, 0.65f, 0.75f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            float rowDistance = Mathf.Abs(enemy.transform.position.y - target.transform.position.y);
            float distanceFromCaster = Vector3.Distance(transform.position, enemy.transform.position);
            if (enemy != target && (rowDistance > rowTolerance || distanceFromCaster > maxDistance))
                continue;

            int damage = enemy == target ? mainDamage : splashDamage;
            DealLegendarySkillDamage(enemy, damage, CombatNumberKind.AttackDamage);
            enemy.ApplyTimedSynergyMoveSpeedBonus(-0.12f, GetSkillDuration(1.8f, false));
        }
    }

    // Aurora Lioness専用スキルです。前線を光で包み、周囲の味方を少し速く硬くします。
    private void ExecuteAuroralionessGuard()
    {
        int shieldAmount = GetSkillShieldAmount();
        float duration = GetSkillDuration(4.2f, true);
        float radius = Mathf.Max(1.8f, skillAreaRadius);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.15f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.9f, 0.85f);

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) > radius)
                continue;

            ally.ApplyShield(ally == this ? shieldAmount : Mathf.RoundToInt(shieldAmount * 0.62f), duration);
            ally.ApplyAttackSpeedBoostFromSynergy(1f + 0.10f * GetSkillEffectMultiplier(false), duration);
        }
    }

    // Azurite Lion専用スキルです。対象へ飛び込み、周囲を凍らせながら噛みつきます。
    private void ExecuteAzuriteLionFrostPounce(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        BlinkToTargetSide(target);
        FaceTarget(target);

        int damage = CalculateSingleTargetSkillDamage();
        float radius = Mathf.Max(1.35f, skillAreaRadius);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position, 1.15f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.75f, 0.85f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;

            DealLegendarySkillDamage(enemy, enemy == target ? damage : Mathf.RoundToInt(damage * 0.52f), GetSkillDamageNumberKind());
            enemy.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), GetSkillDuration(2.4f, true));
            enemy.ApplyFrostStackFromSynergy(GetSkillDuration(0.55f, false));
        }
    }

    // Altgeneral Tier2専用スキルです。火と氷を同時に飛ばし、燃焼と鈍化を同時に付けます。
    private void ExecuteAltgeneralTwinElement(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = CalculateAreaSkillDamage();
        float radius = Mathf.Max(1.75f, skillAreaRadius);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, target.transform.position, 1.05f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position + Vector3.right * 0.12f, 0.9f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.8f, 0.9f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;

            DealLegendarySkillDamage(enemy, damage, CombatNumberKind.FocusDamage);
            enemy.ApplyInfernoBurnFromSynergy(this, Mathf.RoundToInt(damage * 0.18f), GetSkillDuration(3f, true));
            enemy.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), GetSkillDuration(2.2f, true));
        }
    }

    // Sand Panther専用スキルです。後衛や弱った敵に飛び込む暗殺スキルです。
    private void ExecuteSandpantherAmbush(BaseEntity target)
    {
        target = GetLowestHealthEnemy() ?? GetFarthestEnemy() ?? GetValidSkillTarget(target);
        if (target == null)
            return;

        BlinkToTargetSide(target);
        FaceTarget(target);
        untargetableUntil = Time.time + GetSkillDuration(0.45f, false);

        float executeBonus = target.HealthRatio <= 0.45f ? 1.35f : 1f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 1.8f * executeBonus * GetSkillEffectMultiplier(true)));
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Shadow, target.transform.position, 1f);
        DealLegendarySkillDamage(target, damage, CombatNumberKind.AttackDamage);
        ApplyTimedSynergyMoveSpeedBonus(0.25f, GetSkillDuration(2.5f, false));
    }

    // Protector専用スキルです。自分と最も傷ついた味方を線で守るイメージの防御スキルです。
    private void ExecuteProtectorBulwarkLink()
    {
        BaseEntity ally = GetLowestHealthAlly() ?? this;
        int shieldAmount = GetSkillShieldAmount();
        float duration = GetSkillDuration(skillShieldDuration, true);
        float radius = Mathf.Max(1.9f, skillAreaRadius);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, ally.transform.position, 1.2f);
        AttackEffectPlayer.PlayAreaIndicator(this, ally.transform.position, radius, 0.9f, 0.9f);
        ApplyShield(shieldAmount, duration);
        ally.ApplyShield(shieldAmount, duration);

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity nearbyAlly = allies[i];
            if (Vector3.Distance(nearbyAlly.transform.position, ally.transform.position) <= radius)
                nearbyAlly.ApplyTimedSynergyDamageReductionBonus(0.12f * GetSkillEffectMultiplier(false), duration);
        }
    }

    // Taskmaster専用スキルです。鞭で敵を止めつつ、周囲の味方を一時的に急かします。
    private void ExecuteTaskmasterWhipCommand(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 1.35f * GetSkillEffectMultiplier(true)));
        float duration = GetSkillDuration(3f, false);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frenzy, transform.position, 1.05f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.Stun);
        DealLegendarySkillDamage(target, damage, CombatNumberKind.AttackDamage);
        target.ApplyStun(GetSkillDuration(skillStunDuration, false));

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) <= 2.2f)
                ally.ApplyAttackSpeedBoostFromSynergy(1.18f + 0.04f * GetSkillEffectMultiplier(false), duration);
        }
    }

    // Kane専用スキルです。機械砲台のように複数の雷撃をばらまきます。
    private void ExecuteKaneStormTurret(BaseEntity target)
    {
        int shotCount = StarLevel >= 3 ? 5 : StarLevel >= 2 ? 4 : 3;
        int damage = CalculateAreaSkillDamage();
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Machine, transform.position, 1f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, transform.position + Vector3.up * 0.35f, 1f);

        for (int i = 0; i < shotCount; i++)
        {
            BaseEntity lightningTarget = i == 0 ? GetValidSkillTarget(target) : GetRandomActiveEnemy();
            if (lightningTarget == null)
                break;

            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, lightningTarget.transform.position, 0.95f);
            DealLegendarySkillDamage(lightningTarget, damage, CombatNumberKind.FocusDamage);
            BaseEntity chainTarget = GetNearestEnemyNear(lightningTarget.transform.position, lightningTarget, 1.5f);
            if (chainTarget != null)
                DealLegendarySkillDamage(chainTarget, Mathf.RoundToInt(damage * 0.45f), CombatNumberKind.FocusDamage);
        }
    }

    // Malyk専用スキルです。敵の魂を削って、味方側へ回復として戻します。
    private void ExecuteMalykSoulDrain(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = CalculateAreaSkillDamage();
        int totalDamage = 0;
        float radius = Mathf.Max(2f, skillAreaRadius);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, target.transform.position, 1.2f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.85f, 1f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;

            int dealt = enemy == target ? damage : Mathf.RoundToInt(damage * 0.68f);
            DealLegendarySkillDamage(enemy, dealt, CombatNumberKind.FocusDamage);
            enemy.ApplyTimedSynergyDamageDealtBonus(-0.12f, GetSkillDuration(3f, true));
            totalDamage += dealt;
        }

        BaseEntity ally = GetLowestHealthAlly() ?? this;
        ally.HealSelf(Mathf.Max(1, Mathf.RoundToInt(totalDamage * 0.32f * GlobalHealingMultiplier)));
    }

    // Paragon専用スキルです。周囲の味方をまとめて守り、近い敵へ神聖な反撃をします。
    private void ExecuteParagonAegis()
    {
        int shieldAmount = GetSkillShieldAmount();
        int pulseDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 1.2f * GetSkillEffectMultiplier(true)));
        float duration = GetSkillDuration(skillShieldDuration, true);
        float radius = Mathf.Max(2.25f, skillAreaRadius);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.35f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 1f, 1f);

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) <= radius)
            {
                ally.ApplyShield(shieldAmount, duration);
                ally.ApplyTimedSynergyDamageReductionBonus(0.10f * GetSkillEffectMultiplier(false), duration);
            }
        }

        DamageEnemiesAround(transform.position, radius, pulseDamage, CombatNumberKind.AttackDamage);
    }

    // Ilenamk2専用スキルです。氷の格子で範囲を固定し、味方には薄い守りを張ります。
    private void ExecuteIlenaCrystalLattice(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = CalculateAreaSkillDamage();
        int shieldAmount = Mathf.RoundToInt(GetSkillShieldAmount() * 0.45f);
        float radius = Mathf.Max(2.4f, skillAreaRadius);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position, 1.35f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.95f, 1.05f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;

            DealLegendarySkillDamage(enemy, damage, CombatNumberKind.FocusDamage);
            enemy.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), GetSkillDuration(2.8f, true));
            enemy.ApplyFrostStackFromSynergy(GetSkillDuration(0.75f, false));
        }

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, target.transform.position) <= radius)
                ally.ApplyShield(shieldAmount, GetSkillDuration(3f, true));
        }
    }

    // Wujin専用スキルです。自分中心に帝炎の陣を作り、敵を焼きつつ近くの味方を鼓舞します。
    private void ExecuteWujinImperialPyre()
    {
        int damage = CalculateAreaSkillDamage();
        float duration = GetSkillDuration(3.5f, true);
        float radius = Mathf.Max(2.3f, skillAreaRadius);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, transform.position, 1.55f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Royal, transform.position, 1.05f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, duration, 1.05f);

        DamageEnemiesAround(transform.position, radius, damage, GetSkillDamageNumberKind());
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, transform.position) <= radius)
                enemy.ApplyInfernoBurnFromSynergy(this, Mathf.RoundToInt(damage * 0.16f), duration);
        }

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) <= radius)
                ally.ApplyAttackSpeedBoostFromSynergy(1.16f + 0.04f * GetSkillEffectMultiplier(false), duration);
        }
    }

    // Wraith専用スキルです。霊の吹雪を作り、範囲内の敵を削りながら弱体化します。
    private void ExecuteWraithGraveBlizzard(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = CalculateAreaSkillDamage();
        float radius = Mathf.Max(2.5f, skillAreaRadius);
        float duration = GetSkillDuration(3.2f, true);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Wraith, target.transform.position, 1.35f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position, 1.05f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, duration, 1f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;

            DealLegendarySkillDamage(enemy, damage, CombatNumberKind.FocusDamage);
            enemy.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), duration);
            enemy.ApplyTimedSynergyDamageDealtBonus(-0.10f, duration);
        }
    }

    // Snowchaser専用スキルです。回復を中継しながら、回復対象の近くの敵を凍らせます。
    private void ExecuteSnowchaserRelay()
    {
        BaseEntity firstAlly = GetLowestHealthAlly() ?? this;
        int healAmount = GetSkillAllyHealAmount();
        int shieldAmount = Mathf.RoundToInt(GetSkillShieldAmount() * 0.42f);
        float radius = Mathf.Max(2.6f, skillAreaRadius);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, firstAlly.transform.position, 1.05f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, firstAlly.transform.position, 0.85f);
        AttackEffectPlayer.PlayAreaIndicator(this, firstAlly.transform.position, radius, 0.9f, 0.95f);
        firstAlly.HealSelf(healAmount);
        firstAlly.ApplyShield(shieldAmount, GetSkillDuration(3.4f, true));

        BaseEntity secondAlly = GetLowestHealthAlly();
        if (secondAlly != null && secondAlly != firstAlly)
        {
            secondAlly.HealSelf(Mathf.RoundToInt(healAmount * 0.65f));
            secondAlly.ApplyShield(Mathf.RoundToInt(shieldAmount * 0.7f), GetSkillDuration(3.4f, true));
        }

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, firstAlly.transform.position) <= radius)
                enemy.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), GetSkillDuration(2.5f, true));
        }
    }

    // Grymbeast専用スキル（DESIGN_cost4-units.md）。影の獣が後衛へ飛び込み、対象周囲を抉って吸命する。
    private void ExecuteGrymbeastShadowMaul(BaseEntity target)
    {
        BaseEntity primary = GetFarthestEnemy() ?? GetValidSkillTarget(target);
        if (primary == null)
            return;

        BlinkToTargetSide(primary);
        FaceTarget(primary);

        int damage = CalculateAreaSkillDamage();
        int splash = Mathf.RoundToInt(damage * 0.6f);
        float radius = Mathf.Max(1.9f, skillAreaRadius);
        int totalDealt = 0;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Shadow, primary.transform.position, 1.2f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, primary.transform.position, 0.9f);
        AttackEffectPlayer.PlayAreaIndicator(this, primary.transform.position, radius, 0.8f, 1f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, primary.transform.position) > radius)
                continue;
            int dealt = enemy == primary ? damage : splash;
            DealLegendarySkillDamage(enemy, dealt, CombatNumberKind.FocusDamage);
            totalDealt += dealt;
        }
        HealSelf(Mathf.Max(1, Mathf.RoundToInt(totalDealt * 0.30f * GlobalHealingMultiplier)));
    }

    // Cinderwraith専用スキル。自分中心に業火を撒き、敵を燃やしつつ自身が高揚（攻撃速度上昇）する。
    private void ExecuteCinderwraithPyreFrenzy()
    {
        int damage = CalculateAreaSkillDamage();
        float duration = GetSkillDuration(3.2f, true);
        float radius = Mathf.Max(2.2f, skillAreaRadius);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, transform.position, 1.5f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Wraith, transform.position, 1f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, duration, 1.05f);

        DamageEnemiesAround(transform.position, radius, damage, GetSkillDamageNumberKind());
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, transform.position) <= radius)
                enemy.ApplyInfernoBurnFromSynergy(this, Mathf.RoundToInt(damage * 0.18f), duration);
        }
        ApplyAttackSpeedBoostFromSynergy(1.25f + 0.05f * GetSkillEffectMultiplier(false), duration);
    }

    // Draugarlord専用スキル。遠距離から氷塊を撃ち込み、範囲を凍結減速させて自身に守りを張る。
    private void ExecuteDraugarlordGlacialSalvo(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = CalculateAreaSkillDamage();
        float radius = Mathf.Max(2.3f, skillAreaRadius);
        float duration = GetSkillDuration(2.6f, true);
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position, 1.3f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Machine, target.transform.position, 0.9f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.9f, 1f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;
            DealLegendarySkillDamage(enemy, damage, CombatNumberKind.FocusDamage);
            enemy.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), duration);
            enemy.ApplyFrostStackFromSynergy(GetSkillDuration(0.75f, false));
        }
        ApplyShield(GetSkillShieldAmount(), GetSkillDuration(skillShieldDuration, true));
    }

    // Kingsguard専用スキル。王命の盾。最も傷ついた味方を癒し、周囲の味方へ盾と被ダメ軽減を配る。
    private void ExecuteKingsguardAegisDecree()
    {
        int shieldAmount = GetSkillShieldAmount();
        int healAmount = GetSkillAllyHealAmount();
        float duration = GetSkillDuration(skillShieldDuration, true);
        float radius = Mathf.Max(2.3f, skillAreaRadius);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.3f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Royal, transform.position, 1f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 1f, 1f);

        BaseEntity lowest = GetLowestHealthAlly() ?? this;
        lowest.HealSelf(healAmount);

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) > radius)
                continue;
            ally.ApplyShield(ally == this ? shieldAmount : Mathf.RoundToInt(shieldAmount * 0.6f), duration);
            ally.ApplyTimedSynergyDamageReductionBonus(0.10f * GetSkillEffectMultiplier(false), duration);
        }
    }

    // Dissonance専用スキル。不協和音の連弾。複数の敵へ秘力の雷弾を放ち、近接へ連鎖させる。
    private void ExecuteDissonanceArcCacophony(BaseEntity target)
    {
        int bolts = StarLevel >= 3 ? 5 : StarLevel >= 2 ? 4 : 3;
        int damage = CalculateAreaSkillDamage();
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Arcanist, transform.position, 1.1f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, transform.position + Vector3.up * 0.35f, 1f);

        for (int i = 0; i < bolts; i++)
        {
            BaseEntity boltTarget = i == 0 ? GetValidSkillTarget(target) : GetRandomActiveEnemy();
            if (boltTarget == null)
                break;
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, boltTarget.transform.position, 0.95f);
            DealLegendarySkillDamage(boltTarget, damage, CombatNumberKind.FocusDamage);
            BaseEntity chainTarget = GetNearestEnemyNear(boltTarget.transform.position, boltTarget, 1.5f);
            if (chainTarget != null)
                DealLegendarySkillDamage(chainTarget, Mathf.RoundToInt(damage * 0.45f), CombatNumberKind.FocusDamage);
        }
    }

    // Solfist専用スキルです。太陽拳で主対象を殴り、周囲へ連爆を起こします。
    private void ExecuteSolfistSolarCombo(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int mainDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 1.55f * GetSkillEffectMultiplier(true)));
        int areaDamage = CalculateAreaSkillDamage();
        float radius = Mathf.Max(2.1f, skillAreaRadius);
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, target.transform.position, 1.25f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.85f, 1f);

        DealLegendarySkillDamage(target, mainDamage, CombatNumberKind.AttackDamage);
        target.ApplyStun(GetSkillDuration(0.55f, false));

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == target || Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;

            DealLegendarySkillDamage(enemy, areaDamage, GetSkillDamageNumberKind());
            enemy.ApplyInfernoBurnFromSynergy(this, Mathf.RoundToInt(areaDamage * 0.18f), GetSkillDuration(3f, true));
        }
    }

    // Maehv skill: fires a railgun-like lightning arc that chains to nearby enemies.
    private void ExecuteMaehvRailArc(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = CalculateAreaSkillDamage();
        BaseEntity current = target;
        List<BaseEntity> hitEnemies = new List<BaseEntity>();
        int chainCount = StarLevel >= 3 ? 4 : StarLevel >= 2 ? 3 : 2;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, target.transform.position, 1.2f);
        for (int i = 0; i < chainCount && current != null; i++)
        {
            hitEnemies.Add(current);
            int chainDamage = Mathf.RoundToInt(damage * Mathf.Pow(0.72f, i));
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, current.transform.position, 0.95f);
            DealLegendarySkillDamage(current, chainDamage, CombatNumberKind.FocusDamage);
            current.ApplyStun(GetSkillDuration(i == 0 ? 0.55f : 0.3f, false));

            BaseEntity next = GetNearestEnemyNear(current.transform.position, current, 1.85f);
            if (next != null && hitEnemies.Contains(next))
                next = null;

            current = next;
        }
    }

    // 専用スキル用に、倒れていない有効な対象を取り直します。
    private BaseEntity GetValidSkillTarget(BaseEntity target)
    {
        if (target != null && !target.dead && target.CanBeTargeted)
            return target;

        return GetNearestEnemy();
    }

    // HP割合が一番低い敵を返します。暗殺や処刑系スキルの狙い先に使います。
    private BaseEntity GetLowestHealthEnemy()
    {
        List<BaseEntity> enemies = GetActiveEnemies();
        BaseEntity lowestEnemy = null;
        float lowestRatio = 1.01f;

        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.dead || !enemy.CanBeTargeted)
                continue;

            if (enemy.HealthRatio < lowestRatio)
            {
                lowestRatio = enemy.HealthRatio;
                lowestEnemy = enemy;
            }
        }

        return lowestEnemy;
    }

    // Decepticle Prime専用スキルです。遠距離から照準砲を連射し、装甲を削ります。
    private void ExecuteDecepticleprimePrismBattery(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int shotCount = StarLevel >= 3 ? 5 : StarLevel >= 2 ? 4 : 3;
        int damage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 1.35f * GetSkillEffectMultiplier(true)));
        BaseEntity currentTarget = target;

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Machine, transform.position, 1f);
        for (int i = 0; i < shotCount; i++)
        {
            if (currentTarget == null || currentTarget.dead)
                currentTarget = GetRandomActiveEnemy();

            if (currentTarget == null)
                break;

            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Ranger, currentTarget.transform.position, 0.85f);
            DealLegendarySkillDamage(currentTarget, Mathf.RoundToInt(damage * (1f - i * 0.08f)), CombatNumberKind.AttackDamage);
            currentTarget.ApplyTimedSynergyDamageReductionBonus(-0.06f, GetSkillDuration(3f, false));
            currentTarget = GetNearestEnemyNear(currentTarget.transform.position, currentTarget, 1.65f);
        }
    }

    // Tier2 General専用スキルです。氷の号令で敵を止め、味方前衛を押し上げます。
    private void ExecuteTier2GeneralGlacialCommand(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 1.55f * GetSkillEffectMultiplier(true)));
        float duration = GetSkillDuration(3.2f, true);
        float radius = Mathf.Max(1.85f, skillAreaRadius);

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position, 1.15f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.85f, 0.9f);
        DealLegendarySkillDamage(target, damage, CombatNumberKind.AttackDamage);
        target.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), duration);
        target.ApplyFrostStackFromSynergy(GetSkillDuration(0.65f, false));

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) > 2.2f)
                continue;

            ally.ApplyAttackSpeedBoostFromSynergy(1.12f + 0.03f * GetSkillEffectMultiplier(false), duration);
            ally.ApplyTimedSynergyDamageReductionBonus(0.06f, duration);
        }
    }

    // Embergeneral専用スキルです。王の号令で味方全体を強化し、近くの味方をさらに硬くします。
    private void ExecuteEmbergeneralRoyalCommand()
    {
        float magnitude = GetLegendarySkillMagnitude();
        float duration = GetLegendaryDuration(5f);
        List<BaseEntity> allies = GetActiveAllies(true);
        float attackSpeedBonus = StarLevel >= 3 ? 1.25f : 0.20f * magnitude;
        float manaGainBonus = StarLevel >= 3 ? 1.50f : 0.20f * magnitude;
        float guardBonus = StarLevel >= 3 ? 0.60f : 0.15f * magnitude;
        float guardRadius = GetLegendaryRadius(1.15f);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Royal, transform.position, StarLevel >= 3 ? 5.8f : 2.6f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, guardRadius, 1.15f, StarLevel >= 3 ? 1.5f : 1f);
        AttackEffectPlayer.PlayUiSfx("synergy_royal");

        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            ally.ApplyAttackSpeedBoostFromSynergy(1f + attackSpeedBonus, duration);
            ally.ApplyTimedSynergyManaGainMultiplier(1f + manaGainBonus, duration);
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, ally.transform.position, StarLevel >= 3 ? 2.4f : 0.8f);

            if (Vector3.Distance(ally.transform.position, transform.position) <= guardRadius)
                ally.ApplyTimedSynergyDamageReductionBonus(guardBonus, duration);
        }

        ApplyShield(Mathf.RoundToInt(MaxHealth * (StarLevel >= 3 ? 1.35f : 0.22f * magnitude)), duration);
    }

    // Kron専用スキルです。敵味方のHP状況を見て、回復・処刑・均衡効果に分岐します。
    private void ExecuteKronJudgementScale()
    {
        float magnitude = GetLegendarySkillMagnitude();
        List<BaseEntity> allies = GetActiveAllies(true);
        List<BaseEntity> enemies = GetActiveEnemies();
        float allyAverage = GetAverageHealthRatio(allies);
        float enemyAverage = GetAverageHealthRatio(enemies);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, StarLevel >= 3 ? 5.2f : 2.4f);
        Vector3 enemyCenter = GetEnemyClusterCenter(enemies, transform.position);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, enemyCenter, StarLevel >= 3 ? 5.2f : 2.2f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, GetLegendaryRadius(3.4f), 1.1f, StarLevel >= 3 ? 1.4f : 0.9f);
        AttackEffectPlayer.PlayAreaIndicator(this, enemyCenter, GetLegendaryRadius(3.4f), 1.1f, StarLevel >= 3 ? 1.4f : 0.9f);
        AttackEffectPlayer.PlayUiSfx(enemyAverage < allyAverage ? "synergy_abyss" : "synergy_divine");

        if (allyAverage <= 0.55f && allyAverage <= enemyAverage)
        {
            for (int i = 0; i < allies.Count; i++)
            {
                int healAmount = Mathf.RoundToInt(allies[i].MaxHealth * (StarLevel >= 3 ? 0.85f : 0.18f * magnitude));
                allies[i].HealSelf(healAmount);
                allies[i].ApplyShield(Mathf.RoundToInt(allies[i].MaxHealth * (StarLevel >= 3 ? 0.45f : 0.08f * magnitude)), 4.5f);
            }
            return;
        }

        if (enemyAverage <= 0.45f && enemyAverage < allyAverage)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                int damage = Mathf.RoundToInt(enemies[i].MaxHealth * (StarLevel >= 3 ? 0.95f : 0.18f * magnitude));
                DealLegendarySkillDamage(enemies[i], damage, CombatNumberKind.FocusDamage);
            }
            return;
        }

        for (int i = 0; i < allies.Count; i++)
            allies[i].ApplyShield(Mathf.RoundToInt(allies[i].MaxHealth * (StarLevel >= 3 ? 0.65f : 0.11f * magnitude)), 4.5f);

        for (int i = 0; i < enemies.Count; i++)
        {
            int damage = Mathf.RoundToInt((skillBasePower * 1.25f + enemies[i].MaxHealth * 0.035f) * magnitude);
            DealLegendarySkillDamage(enemies[i], damage, CombatNumberKind.FocusDamage);
        }
    }

    // Invader専用スキルです。一定時間ランダム落雷を起こし、近くの敵へ連鎖させます。
    private IEnumerator ExecuteInvaderThunderGodCoroutine()
    {
        float magnitude = GetLegendarySkillMagnitude();
        float interval = StarLevel >= 3 ? 0.16f : 0.5f;
        int strikeCount = Mathf.RoundToInt((10f + Mathf.Max(0f, GetItemFocusMultiplier() - 1f) * 4f) * (StarLevel >= 3 ? 3.0f : StarLevel >= 2 ? 1.45f : 1f));
        int baseStrikeDamage = Mathf.RoundToInt((skillBasePower * 1.45f + GetCurrentDamage() * 0.5f) * magnitude);
        float chainRadius = GetLegendaryRadius(1.75f);

        AttackEffectPlayer.PlayInvaderLightningStrike(transform.position, StarLevel >= 3 ? 2.4f : 1.35f, StarLevel >= 3);
        AttackEffectPlayer.PlayUiSfx("synergy_storm");

        for (int i = 0; i < strikeCount; i++)
        {
            if (!IsInActiveRound())
                break;

            BaseEntity target = GetRandomActiveEnemy();
            if (target == null)
                break;

            bool killed = StrikeInvaderLightning(target, baseStrikeDamage);
            BaseEntity chained = GetNearestEnemyNear(target.transform.position, target, chainRadius);
            if (chained != null)
                StrikeInvaderLightning(chained, Mathf.RoundToInt(baseStrikeDamage * (StarLevel >= 3 ? 0.9f : 0.55f)));

            if (killed)
            {
                BaseEntity bonusTarget = GetRandomActiveEnemy();
                if (bonusTarget != null)
                    StrikeInvaderLightning(bonusTarget, Mathf.RoundToInt(baseStrikeDamage * 0.85f));
            }

            yield return new WaitForSeconds(interval);
        }

        invaderThunderCoroutine = null;
    }

    // Invaderの落雷1発分です。倒したかどうかを返し、連鎖や追加落雷に使います。
    private bool StrikeInvaderLightning(BaseEntity target, int damage)
    {
        if (target == null || target.dead)
            return false;

        AttackEffectPlayer.PlayInvaderLightningStrike(target.transform.position, StarLevel >= 3 ? 2.2f : 1.0f, StarLevel >= 3);
        DealLegendarySkillDamage(target, damage, CombatNumberKind.FocusDamage);
        return target == null || target.dead;
    }

    // Gol専用スキルです。敵の中心に黒穴を作り、吸引しながら継続ダメージと爆発を与えます。
    private IEnumerator ExecuteGolBlackHoleCoroutine(BaseEntity targetAtCast)
    {
        float magnitude = GetLegendarySkillMagnitude();
        Vector3 center = targetAtCast != null && !targetAtCast.dead
            ? targetAtCast.transform.position
            : GetEnemyClusterCenter(GetActiveEnemies(), transform.position);
        float radius = GetLegendaryRadius(1.75f);
        float duration = StarLevel >= 3 ? 6f : 4f;
        float tickInterval = StarLevel >= 3 ? 0.28f : 0.5f;
        int tickDamage = Mathf.RoundToInt((skillBasePower * 0.9f + GetCurrentDamage() * 0.35f) * magnitude);
        int totalDamageAttempted = 0;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, center, StarLevel >= 3 ? 7.0f : 2.8f);
        AttackEffectPlayer.PlayAreaIndicator(this, center, radius, duration, StarLevel >= 3 ? 1.6f : 1.05f);
        AttackEffectPlayer.PlayUiSfx("synergy_abyss");

        float elapsed = 0f;
        while (elapsed < duration && IsInActiveRound())
        {
            List<BaseEntity> enemies = GetActiveEnemies();
            for (int i = 0; i < enemies.Count; i++)
            {
                BaseEntity enemy = enemies[i];
                if (Vector3.Distance(enemy.transform.position, center) > radius)
                    continue;

                PullEnemyToward(enemy, center, StarLevel >= 3 ? 0.55f : 0.22f);
                DealLegendarySkillDamage(enemy, tickDamage, CombatNumberKind.FocusDamage);
                totalDamageAttempted += tickDamage;
            }

            elapsed += tickInterval;
            yield return new WaitForSeconds(tickInterval);
        }

        if (!IsInActiveRound())
        {
            golBlackHoleCoroutine = null;
            yield break;
        }

        int explosionDamage = Mathf.RoundToInt((skillBasePower * 2.2f + GetCurrentDamage()) * magnitude);
        totalDamageAttempted += DamageEnemiesAround(center, radius * 1.18f, explosionDamage, CombatNumberKind.FocusDamage);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, center, StarLevel >= 3 ? 8.2f : 3.4f);
        ApplyShield(Mathf.RoundToInt(totalDamageAttempted * 0.5f), 5f);
        golBlackHoleCoroutine = null;
    }

    // Legion専用スキルです。Taskmasterの亡霊を呼び、死亡時に周囲を鈍化させます。
    private void ExecuteLegionDeadMarch()
    {
        int summonCount = StarLevel >= 3 ? 8 : StarLevel >= 2 ? 5 : UnityEngine.Random.Range(2, 5);
        float lifetime = StarLevel >= 3 ? 16f : 8f;
        float slowRadius = StarLevel >= 3 ? 2.8f : 1.35f;
        float slowDuration = StarLevel >= 3 ? 4f : 2.2f;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Summoner, transform.position, StarLevel >= 3 ? 6.0f : 2.6f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, StarLevel >= 3 ? 3.0f : 1.6f, 1.1f, StarLevel >= 3 ? 1.35f : 0.9f);
        AttackEffectPlayer.PlayUiSfx("synergy_summoner");

        for (int i = 0; i < summonCount; i++)
        {
            BaseEntity summon = GameManager.Instance != null
                ? GameManager.Instance.SpawnTemporarySummonByUnitName("Taskmaster", myTeam, currentNode, StarLevel >= 3 ? 2 : 1, lifetime)
                : null;

            if (summon == null)
                continue;

            summon.ConfigureSummonedDeathSlow(slowRadius, slowDuration, 0.72f, -0.28f);
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Wraith, summon.transform.position, StarLevel >= 3 ? 1.8f : 0.9f);
        }
    }

    // Plaguegeneral専用スキルです。全体咆哮で敵を弱らせ、近い敵は怯ませます。
    private void ExecutePlaguegeneralRoar()
    {
        float magnitude = GetLegendarySkillMagnitude();
        float duration = GetLegendaryDuration(2f);
        float stunRadius = GetLegendaryRadius(1.65f);
        int damage = Mathf.RoundToInt((skillBasePower * 1.15f + GetCurrentDamage() * 0.5f) * magnitude);
        List<BaseEntity> enemies = GetActiveEnemies();

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frenzy, transform.position, StarLevel >= 3 ? 6.5f : 2.6f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, GetEnemyClusterCenter(enemies, transform.position), StarLevel >= 3 ? 5.5f : 2.1f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, stunRadius, 1.0f, StarLevel >= 3 ? 1.45f : 1f);
        AttackEffectPlayer.PlayUiSfx("synergy_frenzy");
        AttackEffectPlayer.ShakeCamera(StarLevel >= 3 ? 0.55f : 0.28f, StarLevel >= 3 ? 0.18f : 0.07f);

        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            DealLegendarySkillDamage(enemy, damage, CombatNumberKind.AttackDamage);
            enemy.ApplyTimedSynergyDamageDealtBonus(StarLevel >= 3 ? -0.55f : -0.20f * magnitude, duration);
            enemy.ApplyTimedSynergyMoveSpeedBonus(StarLevel >= 3 ? -0.75f : -0.40f, duration);
            enemy.ApplyAttackSpeedSlow(StarLevel >= 3 ? 0.45f : 0.78f, duration);

            if (Vector3.Distance(enemy.transform.position, transform.position) <= stunRadius)
                enemy.ApplyStun(StarLevel >= 3 ? 2.2f : 0.75f);
        }
    }

    // Skyfalltyrant専用スキルです。短時間通常攻撃を止め、前方へ火炎を吐き続けます。
    private IEnumerator ExecuteSkyfallDragonRampageCoroutine(BaseEntity targetAtCast)
    {
        float magnitude = GetLegendarySkillMagnitude();
        float duration = StarLevel >= 3 ? 10f : 6f;
        float tickInterval = StarLevel >= 3 ? 0.25f : 0.45f;
        float radius = GetLegendaryRadius(1.75f);
        float rangeDistance = GetLegendaryRadius(3.2f);
        int tickDamage = Mathf.RoundToInt((GetCurrentDamage() * 1.25f + skillBasePower * 0.75f) * magnitude);
        BaseEntity directionTarget = targetAtCast != null && !targetAtCast.dead ? targetAtCast : GetNearestEnemy();
        Vector3 breathDirection = directionTarget != null
            ? (directionTarget.transform.position - transform.position).normalized
            : (myTeam == Team.Team1 ? Vector3.right : Vector3.left);
        Vector3 flameCenter = GetSkyfallFlameCenter(breathDirection, rangeDistance, radius);
        float visualRadius = StarLevel >= 3 ? Mathf.Min(radius, 4.3f) : radius;
        bool starThreeRampage = StarLevel >= 3;
        List<Vector3> boardEffectPositions = starThreeRampage ? GetBoardEffectPositions() : null;

        movementLockedBySkill = true;
        ClearMovementReservation();
        SetAnimatorBool("walking", false);
        FaceDirection(breathDirection.x);

        if (starThreeRampage)
            AttackEffectPlayer.PlaySkyfallBoardPerimeterFire(boardEffectPositions, true, duration);
        else
            AttackEffectPlayer.PlaySkyfallBreathFlame(this, directionTarget, breathDirection, duration, rangeDistance);
        AttackEffectPlayer.PlayUiSfx("synergy_inferno");

        float elapsed = 0f;
        float nextPerimeterVisualTime = starThreeRampage ? 0.85f : 0f;
        while (elapsed < duration && !dead && IsInActiveRound())
        {
            canAttack = false;
            SetAnimatorBool("walking", false);
            List<BaseEntity> enemies = GetActiveEnemies();

            directionTarget = targetAtCast != null && !targetAtCast.dead ? targetAtCast : GetNearestEnemy();
            if (directionTarget != null)
                breathDirection = (directionTarget.transform.position - transform.position).normalized;

            if (breathDirection.sqrMagnitude < 0.01f)
                breathDirection = myTeam == Team.Team1 ? Vector3.right : Vector3.left;

            FaceDirection(breathDirection.x);
            flameCenter = GetSkyfallFlameCenter(breathDirection, rangeDistance, radius);

            if (starThreeRampage)
            {
                if (elapsed >= nextPerimeterVisualTime)
                {
                    AttackEffectPlayer.PlaySkyfallBoardPerimeterFire(boardEffectPositions, true);
                    nextPerimeterVisualTime = elapsed + 0.85f;
                }
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                BaseEntity enemy = enemies[i];
                Vector3 toEnemy = enemy.transform.position - transform.position;
                bool inCone = Vector3.Dot(breathDirection, toEnemy.normalized) >= 0.25f && toEnemy.magnitude <= rangeDistance;
                bool inBlast = Vector3.Distance(enemy.transform.position, flameCenter) <= radius;
                if (!starThreeRampage && !inCone && !inBlast)
                    continue;

                DealLegendarySkillDamage(enemy, tickDamage, CombatNumberKind.FocusDamage);
                enemy.ApplyInfernoBurnFromSynergy(this, Mathf.RoundToInt(tickDamage * 0.18f), 3f);
            }

            elapsed += tickInterval;
            yield return new WaitForSeconds(tickInterval);
        }

        if (!IsInActiveRound())
        {
            skyfallRampageCoroutine = null;
            movementLockedBySkill = false;
            canAttack = true;
            yield break;
        }

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, transform.position, StarLevel >= 3 ? 3.5f : 1.2f);
        skyfallRampageCoroutine = null;
        movementLockedBySkill = false;
        if (!dead)
            canAttack = true;
    }

    // ===== DESIGN_skill_overhaul.md: 17体の固有スキル =====
    // 数値の出典は既存ヘルパ（CalculateAreaSkillDamage / GetSkillShieldAmount 等）。
    // テキスト（UnitStatusPanelUI）と必ず同じ式を共有させています。

    // Andromeda — 星屑の斉射 / Stardust Barrage（遠 / Ranger・Storm）
    private void ExecuteAndromedaStardustBarrage(BaseEntity target)
    {
        BaseEntity center = GetValidSkillTarget(target);
        if (center == null)
            return;

        int damage = CalculateAreaSkillDamage();
        float radius = StarLevel >= 3 ? 2.6f : StarLevel >= 2 ? 2.3f : 2.0f;
        float slowDur = GetSkillDuration(1.8f, true);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, center.transform.position, 1.1f);
        AttackEffectPlayer.PlayAreaIndicator(this, center.transform.position, radius, 0.85f, 0.95f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, center.transform.position) > radius)
                continue;
            DealLegendarySkillDamage(enemy, damage, GetSkillDamageNumberKind());
            enemy.ApplyAttackSpeedSlow(0.8f, slowDur);
        }
    }

    // Antiswarm — 群れ招来 / Swarm Call（遠 / Beast・Summoner）
    private void ExecuteAntiswarmSwarmCall()
    {
        if (GameManager.Instance == null)
            return;
        int summonCount = StarLevel >= 3 ? 2 : StarLevel >= 2 ? 2 : 1;
        float lifetime = StarLevel >= 3 ? 12f : 8f;
        int summonStar = StarLevel >= 3 ? 2 : 1;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Summoner, transform.position, 1.1f);
        AttackEffectPlayer.PlayUiSfx("synergy_summoner");

        for (int i = 0; i < summonCount; i++)
        {
            BaseEntity summon = GameManager.Instance.SpawnTemporarySummonByUnitName("Zyx", myTeam, currentNode, summonStar, lifetime);
            if (summon == null)
                continue;
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, summon.transform.position, 0.7f);
        }
    }

    // Borealjuggernaut — 凍て付く守勢 / Frostguard Bastion（近 / Guardian・Frost）
    private void ExecuteBorealjuggernautFrostguardBastion()
    {
        int shieldAmount = GetSkillShieldAmount();
        float shieldDur = GetSkillDuration(skillShieldDuration, true);
        float slowDur = GetSkillDuration(2.4f, true);
        float radius = 1.6f;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, transform.position, 1.2f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, transform.position, 1.0f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.85f, 0.85f);
        ApplyShield(shieldAmount, shieldDur);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, transform.position) > radius)
                continue;
            enemy.ApplyAttackSpeedSlow(0.7f, slowDur);
        }
    }

    // Chaosknight — 混沌の刻印 / Chaos Brand（近 / Shadow・Abyss）
    private void ExecuteChaosknightChaosBrand(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.4f));
        float vulnerableDuration = GetSkillDuration(4f, true);
        float vulnerablePercent = StarLevel >= 3 ? 0.26f : StarLevel >= 2 ? 0.22f : 0.18f;

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, target.transform.position, 1.0f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(target, damage, GetSkillDamageNumberKind());
        target.ApplyVulnerable(vulnerablePercent, vulnerableDuration);
    }

    // skill-fill: caliber — 終幕の薙ぎ / Endfall Sweep（近・暴走守護）。自分周囲AoE＋鈍化＋自己シールド。
    // boss-skill-progression: 育成Lvでダメージがスケール（共有計算）＋中強化で薙ぎ拡大＋複雑(Lv7+)で守護の強化マス生成。
    private void ExecuteCaliberEndfallSweep()
    {
        float radius = SkillTierEnhanced() ? 2.45f : 1.95f; // 中強化(Lv4+)で薙ぎが広がる。
        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 1.35f));
        float slowDur = GetSkillDuration(3.2f, true);
        int shield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.6f));
        float shieldDur = GetSkillDuration(4f, true);
        float slowFactor = StarLevel >= 3 ? 0.55f : StarLevel >= 2 ? 0.62f : 0.7f;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.25f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.9f, 0.8f);
        ApplyShield(shield, shieldDur);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null || Vector3.Distance(e.transform.position, transform.position) > radius) continue;
            DealLegendarySkillDamage(e, damage, GetSkillDamageNumberKind());
            e.ApplyAttackSpeedSlow(slowFactor, slowDur);
        }
        // 複雑効果(育成Lv7+)は ApplyBossComplexTier に集約。
    }

    // skill-fill: neutral_rook — 停滞の城門 / Stasis Gate（近・石門）。自己特大シールド＋周囲を強スロウ。
    private void ExecuteRookStasisGate()
    {
        float radius = 2.3f;
        int shield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 1.15f));
        float shieldDur = GetSkillDuration(5f, true);
        float slowDur = GetSkillDuration(3.6f, true);
        float slowFactor = StarLevel >= 3 ? 0.45f : StarLevel >= 2 ? 0.52f : 0.6f;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, transform.position, 1.3f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.85f, 0.85f);
        ApplyShield(shield, shieldDur);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null || Vector3.Distance(e.transform.position, transform.position) > radius) continue;
            e.ApplyAttackSpeedSlow(slowFactor, slowDur);
        }
    }

    // skill-fill: neutral_sister — 鎮魂の哀歌 / Requiem Dirge（鎮魂）。敵全体AoE＋防御減(Vulnerable)。射程非依存の全体技。
    private void ExecuteSisterRequiemDirge()
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 0.95f));
        float vulnDur = GetSkillDuration(3.6f, true);
        float vulnPct = StarLevel >= 3 ? 0.24f : StarLevel >= 2 ? 0.20f : 0.16f;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, transform.position, 1.2f);
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null) continue;
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, e.transform.position, 0.8f);
            DealLegendarySkillDamage(e, damage, GetSkillDamageNumberKind());
            e.ApplyVulnerable(vulnPct, vulnDur);
        }
    }

    // skill-fill: neutral_beastmaster — 群獣の咆哮 / Beast Roar。味方全体に攻速バフ＋自身に与ダメバフ。
    private void ExecuteBeastmasterBeastRoar()
    {
        float dur = GetSkillDuration(4f, true);
        float haste = StarLevel >= 3 ? 1.35f : StarLevel >= 2 ? 1.28f : 1.22f;
        float selfDmg = StarLevel >= 3 ? 0.30f : StarLevel >= 2 ? 0.25f : 0.20f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, transform.position, 1.25f);
        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
            if (allies[i] != null) allies[i].ApplyAttackSpeedBoostFromSynergy(haste, dur);
        ApplyTimedSynergyDamageDealtBonus(selfDmg, dur);
    }

    // skill-fill: neutral_gnasher — 顎砕き / Gnash。対象単体大ダメージ＋防御減(Vulnerable)。
    private void ExecuteGnasherGnash(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null) return;
        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.5f));
        float vulnDur = GetSkillDuration(3.5f, true);
        float vulnPct = StarLevel >= 3 ? 0.26f : StarLevel >= 2 ? 0.22f : 0.18f;
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, target.transform.position, 1.0f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(target, damage, GetSkillDamageNumberKind());
        target.ApplyVulnerable(vulnPct, vulnDur);
    }

    // skill-fill: neutral_rawr — 威圧の咆哮 / Intimidate。敵全体を短スロウ＋与ダメ減(Weaken)。
    private void ExecuteRawrIntimidate()
    {
        float slowDur = GetSkillDuration(2.8f, true);
        float weakenDur = GetSkillDuration(3.2f, true);
        float slowFactor = StarLevel >= 3 ? 0.6f : StarLevel >= 2 ? 0.66f : 0.72f;
        float weakenPct = StarLevel >= 3 ? 0.28f : StarLevel >= 2 ? 0.23f : 0.18f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, transform.position, 1.2f);
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null) continue;
            e.ApplyAttackSpeedSlow(slowFactor, slowDur);
            e.ApplyWeaken(weakenPct, weakenDur);
        }
    }

    // skill-fill: neutral_rok — 岩石投擲 / Boulder Toss。対象地点AoEダメージ＋短スタン。
    private void ExecuteRokBoulderToss(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null) return;
        float radius = 1.6f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 1.2f));
        float stunDur = StarLevel >= 3 ? 1.0f : StarLevel >= 2 ? 0.8f : 0.6f;
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, target.transform.position, 1.1f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.85f, 0.8f);
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null || Vector3.Distance(e.transform.position, target.transform.position) > radius) continue;
            DealLegendarySkillDamage(e, damage, GetSkillDamageNumberKind());
            e.ApplyStun(stunDur);
        }
    }

    // skill-fill: neutral_zukong — 如意千変 / Staff Flurry。自分周囲AoE多段＋自己小シールド。
    private void ExecuteZukongStaffFlurry()
    {
        float radius = 1.8f;
        int hits = StarLevel >= 3 ? 3 : 2;
        int perHit = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 0.55f));
        int shield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.4f));
        float shieldDur = GetSkillDuration(3.5f, true);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Warrior, transform.position, 1.2f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.9f, 0.8f);
        ApplyShield(shield, shieldDur);
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null || Vector3.Distance(e.transform.position, transform.position) > radius) continue;
            DealLegendarySkillDamage(e, perHit * hits, GetSkillDamageNumberKind());
        }
    }

    // skill-fill: lanternfox — 灯火の導 / Lantern Guide。味方最低HPへ回復＋小シールド。
    private void ExecuteLanternfoxLanternGuide()
    {
        BaseEntity ally = GetLowestHealthAlly() ?? this;
        int heal = GetSkillAllyHealAmount();
        int shield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.5f));
        float shieldDur = GetSkillDuration(4f, true);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, ally.transform.position, 1.1f);
        ally.HealSelf(heal);
        ally.ApplyShield(shield, shieldDur);
    }

    // skill-fill: onyxjaguar — 影駆け / Onyx Pounce。最遠へ瞬間移動斬り＋自身に与ダメバフ。
    private void ExecuteOnyxjaguarOnyxPounce(BaseEntity target)
    {
        ExecuteAssassinLeapStrike(GetFarthestEnemy() ?? target, 0.7f, false);
        float dur = GetSkillDuration(3f, true);
        float dmgUp = StarLevel >= 3 ? 0.32f : StarLevel >= 2 ? 0.26f : 0.20f;
        ApplyTimedSynergyDamageDealtBonus(dmgUp, dur);
    }

    // skill-fill: keshraifanblade — 扇刃乱舞 / Fan Blade Dance。対象周囲AoE多段ダメージ。
    private void ExecuteKeshraiFanBladeDance(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null) return;
        float radius = 1.7f;
        int hits = StarLevel >= 3 ? 4 : 3;
        int per = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 0.4f));
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Warrior, target.transform.position, 1.1f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.85f, 0.8f);
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null || Vector3.Distance(e.transform.position, target.transform.position) > radius) continue;
            DealLegendarySkillDamage(e, per * hits, GetSkillDamageNumberKind());
        }
    }

    // skill-fill: firewyrm — 業火のブレス / Fire Breath。対象周囲の広範囲AoE特大ダメージ。
    private void ExecuteFirewyrmFireBreath(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null) return;
        float radius = 2.1f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 1.45f));
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, target.transform.position, 1.3f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.95f, 0.7f);
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null || Vector3.Distance(e.transform.position, target.transform.position) > radius) continue;
            DealLegendarySkillDamage(e, damage, GetSkillDamageNumberKind());
        }
    }

    // skill-fill: Magmarvaath — 溶岩噴出 / Magma Eruption。対象地点の広範囲AoE特大ダメージ。
    private void ExecuteMagmarvaathMagmaEruption(BaseEntity target)
    {
        target = GetValidSkillTarget(target); if (target == null) return;
        float radius = 2.2f;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 1.5f));
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, target.transform.position, 1.35f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.95f, 0.7f);
        List<BaseEntity> en = GetActiveEnemies();
        for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null || Vector3.Distance(e.transform.position, target.transform.position) > radius) continue; DealLegendarySkillDamage(e, dmg, GetSkillDamageNumberKind()); }
    }

    // skill-fill: Magmarstarhorn — 星角突撃 / Starhorn Charge。対象へ単体大ダメージ＋短スタン。
    private void ExecuteMagmarstarhornCharge(BaseEntity target)
    {
        target = GetValidSkillTarget(target); if (target == null) return;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.6f));
        float stun = StarLevel >= 3 ? 1.0f : StarLevel >= 2 ? 0.8f : 0.6f;
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, target.transform.position, 1.1f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(target, dmg, GetSkillDamageNumberKind());
        target.ApplyStun(stun);
    }

    // skill-fill: Magmarragnora — 灼熱再生 / Searing Regen。自己大回復＋与ダメバフ。
    private void ExecuteMagmarragnoraSearingRegen()
    {
        int heal = Mathf.Max(1, Mathf.RoundToInt(GetSkillAllyHealAmount() * 1.3f));
        float dur = GetSkillDuration(4f, true);
        float dmgUp = StarLevel >= 3 ? 0.32f : StarLevel >= 2 ? 0.26f : 0.20f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, transform.position, 1.2f);
        HealSelf(heal);
        ApplyTimedSynergyDamageDealtBonus(dmgUp, dur);
    }

    // skill-fill: Abyssallilithe — 吸命の抱擁 / Lifebloom Drain。対象単体大ダメージ＋自分回復。
    private void ExecuteAbyssallilitheLifebloomDrain(BaseEntity target)
    {
        target = GetValidSkillTarget(target); if (target == null) return;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.55f));
        float lifesteal = StarLevel >= 3 ? 0.6f : StarLevel >= 2 ? 0.5f : 0.4f;
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Wraith, target.transform.position, 1.1f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(target, dmg, GetSkillDamageNumberKind());
        HealSelf(Mathf.Max(1, Mathf.RoundToInt(dmg * lifesteal * GlobalHealingMultiplier)));
    }

    // skill-fill: Abyssalcassyva — 影の群葬 / Shadow Swarm。敵全体AoE＋与ダメ減(Weaken)。
    private void ExecuteAbyssalcassyvaShadowSwarm()
    {
        int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 0.9f));
        float dur = GetSkillDuration(3.4f, true);
        float weaken = StarLevel >= 3 ? 0.28f : StarLevel >= 2 ? 0.23f : 0.18f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, transform.position, 1.2f);
        List<BaseEntity> en = GetActiveEnemies();
        for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null) continue; DealLegendarySkillDamage(e, dmg, GetSkillDamageNumberKind()); e.ApplyWeaken(weaken, dur); }
    }

    // skill-fill: Abyssalmaehv — 深淵の連弧 / Abyss Arc。敵全体スロウ＋自己シールド。
    private void ExecuteAbyssalmaehvAbyssArc()
    {
        float dur = GetSkillDuration(3.5f, true);
        float slow = StarLevel >= 3 ? 0.55f : StarLevel >= 2 ? 0.62f : 0.7f;
        int shield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.8f));
        float sdur = GetSkillDuration(4.5f, true);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, transform.position, 1.2f);
        ApplyShield(shield, sdur);
        List<BaseEntity> en = GetActiveEnemies();
        for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null) continue; e.ApplyAttackSpeedSlow(slow, dur); }
    }

    // skill-fill: Vetruvianzirix — 砂塵の刃嵐 / Sandblade Storm。自分周囲AoE多段＋スロウ。
    private void ExecuteVetruvianzirixSandbladeStorm()
    {
        float radius = 1.9f; int hits = StarLevel >= 3 ? 3 : 2;
        int per = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 0.5f));
        float dur = GetSkillDuration(3f, true); float slow = StarLevel >= 3 ? 0.62f : 0.7f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Warrior, transform.position, 1.2f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.9f, 0.8f);
        List<BaseEntity> en = GetActiveEnemies();
        for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null || Vector3.Distance(e.transform.position, transform.position) > radius) continue; DealLegendarySkillDamage(e, per * hits, GetSkillDamageNumberKind()); e.ApplyAttackSpeedSlow(slow, dur); }
    }

    // skill-fill: Vetruviansajj — 太陽の聖印 / Solar Sigil。味方全体与ダメバフ＋自分単体大ダメージ。
    private void ExecuteVetruviansajjSolarSigil(BaseEntity target)
    {
        float dur = GetSkillDuration(4f, true); float dmgUp = StarLevel >= 3 ? 0.26f : StarLevel >= 2 ? 0.22f : 0.18f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.2f);
        List<BaseEntity> al = GetActiveAllies(true);
        for (int i = 0; i < al.Count; i++) if (al[i] != null) al[i].ApplyTimedSynergyDamageDealtBonus(dmgUp, dur);
        target = GetValidSkillTarget(target);
        if (target != null) { int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.3f)); FaceTarget(target); AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike); DealLegendarySkillDamage(target, dmg, GetSkillDamageNumberKind()); }
    }

    // skill-fill: Vetruvianscion — 風蝕の貫き / Erosion Pierce。最遠へ単体大ダメージ＋防御減(Vulnerable)。
    private void ExecuteVetruvianscionErosionPierce(BaseEntity target)
    {
        BaseEntity t = GetFarthestEnemy() ?? GetValidSkillTarget(target); if (t == null) return;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.5f));
        float dur = GetSkillDuration(3.5f, true); float vuln = StarLevel >= 3 ? 0.26f : StarLevel >= 2 ? 0.22f : 0.18f;
        FaceTarget(t);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, t.transform.position, 1.0f);
        AttackEffectPlayer.PlaySkill(this, t, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(t, dmg, GetSkillDamageNumberKind()); t.ApplyVulnerable(vuln, dur);
    }

    // skill-fill: mechaz0rwing — 滑空斉射 / Strike Glide。敵全体小ダメージ＋自分攻速大バフ。
    private void ExecuteMechaWingStrikeGlide()
    {
        int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 0.6f));
        float dur = GetSkillDuration(3.5f, true); float haste = StarLevel >= 3 ? 1.5f : StarLevel >= 2 ? 1.42f : 1.35f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, transform.position, 1.2f);
        ApplyAttackSpeedBoostFromSynergy(haste, dur);
        List<BaseEntity> en = GetActiveEnemies();
        for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null) continue; DealLegendarySkillDamage(e, dmg, GetSkillDamageNumberKind()); }
    }

    // skill-fill: mechaz0rsword — 斬鉄連撃 / Steel Flurry。対象多段＋防御減。
    private void ExecuteMechaSwordSteelFlurry(BaseEntity target)
    {
        target = GetValidSkillTarget(target); if (target == null) return;
        int hits = StarLevel >= 3 ? 4 : 3; int per = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 0.5f));
        float dur = GetSkillDuration(3.5f, true); float vuln = StarLevel >= 3 ? 0.24f : 0.18f;
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Warrior, target.transform.position, 1.0f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(target, per * hits, GetSkillDamageNumberKind()); target.ApplyVulnerable(vuln, dur);
    }

    // skill-fill: mechaz0rsuper — オーバードライブ / Overdrive。自分与ダメ＆攻速大バフ＋周囲AoE。
    private void ExecuteMechaSuperOverdrive()
    {
        float radius = 1.9f; int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 1.1f));
        float dur = GetSkillDuration(4f, true); float dmgUp = StarLevel >= 3 ? 0.4f : 0.3f; float haste = StarLevel >= 3 ? 1.4f : 1.3f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, transform.position, 1.3f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.9f, 0.8f);
        ApplyTimedSynergyDamageDealtBonus(dmgUp, dur); ApplyAttackSpeedBoostFromSynergy(haste, dur);
        List<BaseEntity> en = GetActiveEnemies();
        for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null || Vector3.Distance(e.transform.position, transform.position) > radius) continue; DealLegendarySkillDamage(e, dmg, GetSkillDamageNumberKind()); }
    }

    // skill-fill: mechaz0rhelm — 守護フィールド / Aegis Field。味方全体小シールド＋自分被ダメ減。
    private void ExecuteMechaHelmAegisField()
    {
        int shield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.45f));
        float sdur = GetSkillDuration(4f, true); float dr = StarLevel >= 3 ? 0.22f : StarLevel >= 2 ? 0.18f : 0.14f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.2f);
        List<BaseEntity> al = GetActiveAllies(true);
        for (int i = 0; i < al.Count; i++) if (al[i] != null) al[i].ApplyShield(shield, sdur);
        ApplyTimedSynergyDamageReductionBonus(dr, sdur);
    }

    // skill-fill: mechaz0rchassis — 重装砲列 / Bulwark Cannons。自己シールド＋対象AoEダメージ。
    private void ExecuteMechaChassisBulwarkCannons(BaseEntity target)
    {
        int shield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.7f)); float sdur = GetSkillDuration(4.5f, true);
        ApplyShield(shield, sdur);
        target = GetValidSkillTarget(target);
        if (target == null) return;
        float radius = 1.7f; int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 1.0f));
        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, target.transform.position, 1.1f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.85f, 0.8f);
        List<BaseEntity> en = GetActiveEnemies();
        for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null || Vector3.Distance(e.transform.position, target.transform.position) > radius) continue; DealLegendarySkillDamage(e, dmg, GetSkillDamageNumberKind()); }
    }

    // skill-fill: mechaz0rcannon — 全弾斉射 / Full Salvo。最遠基点に広範囲AoE特大ダメージ。
    private void ExecuteMechaCannonFullSalvo(BaseEntity target)
    {
        BaseEntity t = GetFarthestEnemy() ?? GetValidSkillTarget(target); if (t == null) return;
        float radius = 2.3f; int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 1.4f));
        FaceTarget(t);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Warrior, t.transform.position, 1.35f);
        AttackEffectPlayer.PlayAreaIndicator(this, t.transform.position, radius, 0.95f, 0.7f);
        List<BaseEntity> en = GetActiveEnemies();
        for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null || Vector3.Distance(e.transform.position, t.transform.position) > radius) continue; DealLegendarySkillDamage(e, dmg, GetSkillDamageNumberKind()); }
    }

    // skill-fill: neutral_hydrax — 奔流の顎 / Torrent Maw。最遠へ単体特大ダメージ＋スロウ。
    private void ExecuteHydraxTorrentMaw(BaseEntity target)
    {
        BaseEntity t = GetFarthestEnemy() ?? GetValidSkillTarget(target); if (t == null) return;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.7f));
        float dur = GetSkillDuration(3.2f, true); float slow = StarLevel >= 3 ? 0.55f : 0.65f;
        FaceTarget(t);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, t.transform.position, 1.2f);
        AttackEffectPlayer.PlaySkill(this, t, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(t, dmg, GetSkillDamageNumberKind()); t.ApplyAttackSpeedSlow(slow, dur);
    }

    // Christmas — 祝祭の鼓舞 / Festive Rally（近 / Warrior・Divine）
    private void ExecuteChristmasFestiveRally()
    {
        BaseEntity mainAlly = GetLowestHealthAlly() ?? this;
        int heal = GetSkillAllyHealAmount();
        float buffDur = GetSkillDuration(4f, true);
        float buffPercent = StarLevel >= 3 ? 0.22f : StarLevel >= 2 ? 0.18f : 0.15f;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, mainAlly.transform.position, 1.1f);
        mainAlly.HealSelf(heal);

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (!ally.HasSynergy(SynergyType.Warrior))
                continue;
            ally.ApplyTimedSynergyDamageDealtBonus(buffPercent, buffDur);
        }
    }

    // vampire — 吸魂弾 / Soulpiercer（遠 / Arcanist・Wraith・Abyss）
    private void ExecuteVampireSoulpiercer(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.5f));
        float lifestealRatio = StarLevel >= 3 ? 0.60f : StarLevel >= 2 ? 0.50f : 0.40f;

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Wraith, target.transform.position, 1.0f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(target, damage, GetSkillDamageNumberKind());
        HealSelf(Mathf.Max(1, Mathf.RoundToInt(damage * lifestealRatio * GlobalHealingMultiplier)));
    }

    // valiant — 聖盾強襲 / Aegis Smite（近 / Guardian・Divine）
    private void ExecuteValiantAegisSmite(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        float stunDur = GetSkillDuration(skillStunDuration, false);
        int shieldAmount = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.10f * GetSkillEffectMultiplier(true)));
        float shieldDur = GetSkillDuration(skillShieldDuration, true);

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.05f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.Stun);
        target.ApplyStun(stunDur);
        ApplyShield(shieldAmount, shieldDur);
    }

    // Candypanda — 甘味の供物 / Sugar Feast（近 / Beast・Divine）
    private void ExecuteCandypandaSugarFeast()
    {
        int heal = Mathf.Max(1, Mathf.RoundToInt(GetSkillAllyHealAmount() * 0.8f));
        float buffDur = GetSkillDuration(4f, true);
        float buffPercent = 0.12f;
        float radius = 2.2f;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, transform.position, 1.0f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.85f, 0.85f);

        List<BaseEntity> allies = GetActiveAllies(true);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) > radius)
                continue;
            ally.HealSelf(heal);
            ally.ApplyTimedSynergyDamageDealtBonus(buffPercent, buffDur);
        }
    }

    // City — 補給ドローン / Supply Drone（近 / Machine・Alchemy）。簡易版: 設置せず時限コルーチンで継続支援。
    private IEnumerator ExecuteCitySupplyDroneCoroutine()
    {
        float duration = StarLevel >= 3 ? 5.5f : StarLevel >= 2 ? 4.5f : 3.5f;
        float tickInterval = 0.7f;
        int healPerTick = Mathf.Max(1, Mathf.RoundToInt(GetSkillAllyHealAmount() * 0.35f));
        int shieldPerTick = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.25f));

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Machine, transform.position, 1.1f);

        float elapsed = 0f;
        while (elapsed < duration && !dead && IsInActiveRound())
        {
            BaseEntity ally = GetLowestHealthAlly() ?? this;
            if (ally != null && !ally.dead)
            {
                ally.HealSelf(healPerTick);
                ally.ApplyShield(shieldPerTick, 1.8f);
                AttackEffectPlayer.PlaySynergyEffect(SynergyType.Alchemy, ally.transform.position, 0.45f);
            }
            elapsed += tickInterval;
            yield return new WaitForSeconds(tickInterval);
        }
        citySupplyDroneCoroutine = null;
    }

    // Crystal — 氷晶障壁 / Crystal Ward（遠 / Arcanist・Frost）
    private void ExecuteCrystalCrystalWard(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * 0.8f));
        float radius = Mathf.Max(1.6f, skillAreaRadius);
        float slowDur = GetSkillDuration(2.4f, true);
        int allyShield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.5f));
        float shieldDur = GetSkillDuration(skillShieldDuration, true);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, target.transform.position, 1.1f);
        AttackEffectPlayer.PlayAreaIndicator(this, target.transform.position, radius, 0.8f, 0.9f);

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, target.transform.position) > radius)
                continue;
            DealLegendarySkillDamage(enemy, damage, GetSkillDamageNumberKind());
            enemy.ApplyAttackSpeedSlow(GetSkillSlowMultiplier(), slowDur);
        }

        BaseEntity weakestAlly = GetLowestHealthAlly() ?? this;
        weakestAlly.ApplyShield(allyShield, shieldDur);
    }

    // Cindera — 業火の照準 / Ember Sight（遠 / Arcanist・Inferno・Storm）
    private void ExecuteCinderaEmberSight(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.3f));
        float burnDuration = GetSkillDuration(3f, true);
        // tick interval = 0.5s, attack の 8% / 10% / 12%。Inferno burn の duration は tick = 1秒間隔なので、
        // 0.5s tick 相当の総量を duration ぶんに均す。
        float ratio = StarLevel >= 3 ? 0.12f : StarLevel >= 2 ? 0.10f : 0.08f;
        int burnTickDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * ratio));

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, target.transform.position, 1.1f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.AreaDamage);
        DealLegendarySkillDamage(target, damage, GetSkillDamageNumberKind());
        target.ApplyInfernoBurnFromSynergy(this, burnTickDamage, burnDuration);
    }

    // Decepticle — 変形突撃 / Assault Mode（近 / Machine・Alchemy）
    private void ExecuteDecepticleAssaultMode()
    {
        float duration = GetSkillDuration(4f, true);
        float speedMult = 1f + (StarLevel >= 3 ? 0.55f : StarLevel >= 2 ? 0.45f : 0.35f);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Machine, transform.position, 1.1f);
        ApplyAttackSpeedBoostFromSynergy(speedMult, duration);
        // Machine の被弾反射効果は既存の reflect 機構が無いため、AttackSpeed バフ＋ダメージ反射代替として
        // 短いタイムドダメージリダクションで耐久を上げる（テキストの「反射」は将来 R3-balance で別途反射機構を追加）。
        ApplyTimedSynergyDamageReductionBonus(0.10f * GetSkillEffectMultiplier(false), duration);
    }

    // Umbra — 影喰らい / Umbral Devour（近 / Beast・Wraith・Abyss）
    private void ExecuteUmbraUmbralDevour()
    {
        BaseEntity target = GetNearestEnemy();
        if (target == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.2f));
        float shieldDur = GetSkillDuration(skillShieldDuration, true);

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, transform.position, 1.05f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(target, damage, GetSkillDamageNumberKind());
        ApplyShield(Mathf.Max(1, Mathf.RoundToInt(damage * 0.6f)), shieldDur);
    }

    // Spelleater — 呪詛吸収 / Spell Eater（遠 / Arcanist・Abyss）
    private void ExecuteSpelleaterSpellEater(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        float slowDur = GetSkillDuration(3f, true);
        float weakenDur = GetSkillDuration(3f, true);
        float weakenPercent = StarLevel >= 3 ? 0.30f : StarLevel >= 2 ? 0.25f : 0.20f;

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, target.transform.position, 1.0f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.Slow);
        target.ApplyAttackSpeedSlow(0.6f, slowDur);
        target.ApplyWeaken(weakenPercent, weakenDur);
        GainManaFromSynergy(15);
    }

    // Serpenti — 毒牙連撃 / Venom Frenzy（近 / Beast・Shadow・Frenzy）
    private void ExecuteSerpentiVenomFrenzy()
    {
        float duration = GetSkillDuration(4f, true);
        float speedMult = 1f + (StarLevel >= 3 ? 0.60f : StarLevel >= 2 ? 0.50f : 0.40f);

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frenzy, transform.position, 1.1f);
        ApplyAttackSpeedBoostFromSynergy(speedMult, duration);

        // 効果時間中の通常攻撃に毒（Inferno burn を流用）を載せる。
        int tickDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * 0.05f));
        ActivateSerpentiVenomFrenzy(tickDamage, 3f, duration);
    }

    // Decepticlechassis — 装甲再構築 / Armor Reassembly（近 / Machine・Guardian・Alchemy）
    private void ExecuteDecepticlechassisArmorReassembly()
    {
        int selfShield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 1.2f));
        int allyShield = Mathf.Max(1, Mathf.RoundToInt(GetSkillShieldAmount() * 0.4f));
        float shieldDur = GetSkillDuration(skillShieldDuration, true);
        float radius = 2.2f;

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, transform.position, 1.2f);
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, radius, 0.9f, 0.95f);
        ApplyShield(selfShield, shieldDur);

        List<BaseEntity> allies = GetActiveAllies(false);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (Vector3.Distance(ally.transform.position, transform.position) > radius)
                continue;
            ally.ApplyShield(allyShield, shieldDur);
        }
    }

    // Wolfpunch — 獣王の一撃 / Alpha Strike（近 / Beast・Guardian）
    private void ExecuteWolfpunchAlphaStrike(BaseEntity target)
    {
        target = GetValidSkillTarget(target);
        if (target == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(CalculateSingleTargetSkillDamage() * 1.8f));
        float stunDur = 0.6f;
        float buffDur = GetSkillDuration(5f, true);
        float buffPercent = StarLevel >= 3 ? 0.30f : StarLevel >= 2 ? 0.25f : 0.20f;
        bool willKill = target.baseHealth - damage <= 0;

        FaceTarget(target);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Beast, target.transform.position, 1.1f);
        AttackEffectPlayer.PlaySkill(this, target, UnitSkillType.PowerStrike);
        DealLegendarySkillDamage(target, damage, GetSkillDamageNumberKind());
        if (!target.dead)
            target.ApplyStun(stunDur);

        if (willKill || (target != null && target.dead))
            ApplyTimedSynergyDamageDealtBonus(buffPercent, buffDur);
    }

    // 伝説級スキル用の強化倍率です。★3は意図的に勝負を壊すほど大きくします。
    private float GetLegendarySkillMagnitude()
    {
        float starMultiplier = StarLevel >= 3 ? 6.5f : StarLevel >= 2 ? 2.1f : 1f;
        return starMultiplier * GetItemFocusMultiplier() * currentItemSkillEffectMultiplier;
    }

    // 伝説級スキルの効果時間です。★3では強化時間も長めにします。
    private float GetLegendaryDuration(float baseDuration)
    {
        return baseDuration * (StarLevel >= 3 ? 1.75f : StarLevel >= 2 ? 1.25f : 1f);
    }

    // 伝説級スキルの範囲です。★3では盤面を覆う勢いにします。
    private float GetLegendaryRadius(float baseRadius)
    {
        return baseRadius * (StarLevel >= 3 ? 3.4f : StarLevel >= 2 ? 1.55f : 1f);
    }

    // Skyfalltyrantの炎上中心を決めます。★3は盤面中央、それ以外は前方位置を盤面内の最寄りマスへ丸めます。
    private Vector3 GetSkyfallFlameCenter(Vector3 breathDirection, float rangeDistance, float radius)
    {
        if (StarLevel >= 3)
            return GetBoardCenterPosition(transform.position);

        if (breathDirection.sqrMagnitude < 0.01f)
            breathDirection = myTeam == Team.Team1 ? Vector3.right : Vector3.left;

        Vector3 projectedCenter = transform.position + breathDirection.normalized * Mathf.Min(rangeDistance, 2.4f + radius * 0.35f);
        return ClampPositionToBoard(projectedCenter, Mathf.Min(radius, 2.8f));
    }

    // 任意の位置を、盤面上の一番近いマス位置へ寄せます。
    private Vector3 ClampPositionToBoard(Vector3 position)
    {
        return ClampPositionToBoard(position, 0f);
    }

    // 半径つきの範囲演出が盤面外へ出にくいよう、盤面端から少し内側へ寄せます。
    private Vector3 ClampPositionToBoard(Vector3 position, float radiusMargin)
    {
        List<Node> boardNodes = GetBoardNodes();
        if (boardNodes.Count == 0)
            return position;

        if (radiusMargin > 0.05f)
        {
            float minX = boardNodes.Min(node => node.worldPosition.x);
            float maxX = boardNodes.Max(node => node.worldPosition.x);
            float minY = boardNodes.Min(node => node.worldPosition.y);
            float maxY = boardNodes.Max(node => node.worldPosition.y);
            float halfWidth = Mathf.Max(0.01f, (maxX - minX) * 0.5f);
            float halfHeight = Mathf.Max(0.01f, (maxY - minY) * 0.5f);
            float marginX = Mathf.Min(radiusMargin * 0.55f, halfWidth * 0.92f);
            float marginY = Mathf.Min(radiusMargin * 0.35f, halfHeight * 0.92f);
            position.x = Mathf.Clamp(position.x, minX + marginX, maxX - marginX);
            position.y = Mathf.Clamp(position.y, minY + marginY, maxY - marginY);
        }

        Node closestNode = GetClosestBoardNode(position, boardNodes);
        return closestNode != null ? closestNode.worldPosition : position;
    }

    // 盤面全体の中心を返します。ノードが取れない場合だけfallbackを使います。
    private Vector3 GetBoardCenterPosition(Vector3 fallback)
    {
        List<Node> boardNodes = GetBoardNodes();
        if (boardNodes.Count == 0)
            return fallback;

        Vector3 total = Vector3.zero;
        for (int i = 0; i < boardNodes.Count; i++)
            total += boardNodes[i].worldPosition;

        return total / boardNodes.Count;
    }

    // 指定位置に最も近い盤面Nodeを返します。
    private Node GetClosestBoardNode(Vector3 position, List<Node> cachedBoardNodes = null)
    {
        List<Node> boardNodes = cachedBoardNodes ?? GetBoardNodes();
        Node closestNode = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < boardNodes.Count; i++)
        {
            Node node = boardNodes[i];
            float distance = Vector3.SqrMagnitude(node.worldPosition - position);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closestNode = node;
        }

        return closestNode;
    }

    // GridManagerの列・行指定APIから、現在の盤面Node一覧を集めます。
    private List<Node> GetBoardNodes()
    {
        List<Node> boardNodes = new List<Node>();
        if (GridManager.Instance == null || GridManager.Instance.boardColumns <= 0)
            return boardNodes;

        for (int column = 1; column <= GridManager.Instance.boardColumns; column++)
        {
            for (int row = 1; row <= 24; row++)
            {
                Node node = GridManager.Instance.GetNodeAtBoardCoordinate(column, row);
                if (node == null)
                    break;

                boardNodes.Add(node);
            }
        }

        return boardNodes;
    }

    // 盤面全体のNode位置を抽出します。Skyfalltyrant★3の盤面炎上演出で使います。
    private List<Vector3> GetBoardEffectPositions()
    {
        List<Vector3> boardPositions = new List<Vector3>();
        if (GridManager.Instance == null || GridManager.Instance.boardColumns <= 0)
            return boardPositions;

        int boardColumns = GridManager.Instance.boardColumns;
        for (int column = 1; column <= boardColumns; column++)
        {
            List<Node> columnNodes = new List<Node>();
            for (int row = 1; row <= 24; row++)
            {
                Node node = GridManager.Instance.GetNodeAtBoardCoordinate(column, row);
                if (node == null)
                    break;

                columnNodes.Add(node);
            }

            for (int rowIndex = 0; rowIndex < columnNodes.Count; rowIndex++)
            {
                Vector3 position = columnNodes[rowIndex].worldPosition;
                position.z = 0f;
                boardPositions.Add(position);
            }
        }

        return boardPositions;
    }

    // 自分と同じチームで、盤面にいて生存している味方一覧を返します。
    private List<BaseEntity> GetActiveAllies(bool includeSelf)
    {
        if (GameManager.Instance == null)
            return new List<BaseEntity>();

        Team opposingTeam = myTeam == Team.Team1 ? Team.Team2 : Team.Team1;
        return GameManager.Instance.GetEntitiesAgainst(opposingTeam)
            .Where(entity => entity != null && !entity.dead && entity.IsOnBoard && (includeSelf || entity != this))
            .Distinct()
            .ToList();
    }

    // 自分から見た敵で、盤面にいて生存しているユニット一覧を返します。
    private List<BaseEntity> GetActiveEnemies()
    {
        if (GameManager.Instance == null)
            return new List<BaseEntity>();

        return GameManager.Instance.GetEntitiesAgainst(myTeam)
            .Where(entity => entity != null && !entity.dead && entity.IsOnBoard && entity.CanBeTargeted)
            .Distinct()
            .ToList();
    }

    // 生きている敵からランダムに1体返します。
    private BaseEntity GetRandomActiveEnemy()
    {
        List<BaseEntity> enemies = GetActiveEnemies();
        if (enemies.Count == 0)
            return null;

        return enemies[UnityEngine.Random.Range(0, enemies.Count)];
    }

    // 対象リストの平均HP割合を返します。空なら満タン扱いです。
    private float GetAverageHealthRatio(List<BaseEntity> entities)
    {
        if (entities == null || entities.Count == 0)
            return 1f;

        return entities.Average(entity => entity != null ? entity.HealthRatio : 1f);
    }

    // 敵集団の中心を返します。対象がいない場合はfallbackを使います。
    private Vector3 GetEnemyClusterCenter(List<BaseEntity> enemies, Vector3 fallback)
    {
        if (enemies == null || enemies.Count == 0)
            return fallback;

        Vector3 total = Vector3.zero;
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null || enemies[i].dead)
                continue;

            total += enemies[i].transform.position;
            count++;
        }

        return count == 0 ? fallback : total / count;
    }

    // 指定位置の近くにいる敵を1体返します。
    private BaseEntity GetNearestEnemyNear(Vector3 center, BaseEntity ignored, float radius)
    {
        List<BaseEntity> enemies = GetActiveEnemies();
        BaseEntity nearest = null;
        float nearestDistance = Mathf.Max(0f, radius);
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == ignored)
                continue;

            float distance = Vector3.Distance(center, enemy.transform.position);
            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = enemy;
        }

        return nearest;
    }

    // スキルダメージを与え、シナジー側へも命中を通知します。
    private bool DealLegendarySkillDamage(BaseEntity enemy, int damage, CombatNumberKind numberKind)
    {
        if (enemy == null || enemy.dead || damage <= 0)
            return false;

        enemy.TakeDamage(Mathf.Max(1, damage), this, numberKind);
        SynergyManager.Instance?.NotifySkillDamageHit(this, enemy, damage);
        return enemy.dead;
    }

    // 範囲内の敵へまとめてダメージを与え、試行ダメージ合計を返します。
    private int DamageEnemiesAround(Vector3 center, float radius, int damage, CombatNumberKind numberKind)
    {
        int totalDamage = 0;
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, center) > radius)
                continue;

            DealLegendarySkillDamage(enemy, damage, numberKind);
            totalDamage += damage;
        }

        return totalDamage;
    }

    // 黒穴の中心へ敵を少しずつ寄せます。高コストの敵は吸引量を弱めます。
    private void PullEnemyToward(BaseEntity enemy, Vector3 center, float pullStep)
    {
        if (enemy == null || enemy.dead)
            return;

        float costResist = enemy.BaseCost >= 5 ? 0.35f : 1f;
        Vector3 nextPosition = Vector3.MoveTowards(enemy.transform.position, center, Mathf.Max(0.02f, pullStep) * costResist);
        enemy.transform.position = new Vector3(nextPosition.x, nextPosition.y, enemy.transform.position.z);
    }

    // 召喚体が時間切れになった時に、GameManagerの管理リストからも外します。
    private IEnumerator TemporarySummonLifetimeCoroutine(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, duration));
        temporarySummonLifetimeCoroutine = null;

        if (this == null || dead || !IsSummonedUnit || GameManager.Instance == null)
            yield break;

        GameManager.Instance.RemoveTemporarySummonFromSynergy(this);
    }

    // Legionの亡霊が死亡した時の周囲スロウです。
    private void ApplySummonedDeathSlow()
    {
        if (!summonedDeathSlowEnabled || GameManager.Instance == null)
            return;

        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (Vector3.Distance(enemy.transform.position, transform.position) > summonedDeathSlowRadius)
                continue;

            enemy.ApplyAttackSpeedSlow(summonedDeathAttackSpeedMultiplier, summonedDeathSlowDuration);
            enemy.ApplyTimedSynergyMoveSpeedBonus(summonedDeathMoveSpeedPenalty, summonedDeathSlowDuration);
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Wraith, enemy.transform.position, 0.8f);
        }
    }

    // 指定秒数待ってからユニットを破棄します。
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        deathStateCoroutine = null;
        Destroy(gameObject);
    }

    // 指定秒数待ってから、復活待ちユニットを一時的に非表示にします。
    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        deathStateCoroutine = null;

        if (!deathDestroyStarted || !dead)
            yield break;

        gameObject.SetActive(false);
    }

    // 戦闘終了や復活で死亡予約が不要になった時、遅延非表示・破棄処理を止めます。
    private void CancelPendingDeathStateChange()
    {
        if (deathStateCoroutine == null)
            return;

        StopCoroutine(deathStateCoroutine);
        deathStateCoroutine = null;
    }

    // Animatorが安全に使える状態か確認します。
    private bool CanUseAnimator()
    {
        EnsureComponentReferences();
        ConfigureAnimatorForRuntime();
        return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
    }

    // R3: 敵の登場演出。最終位置(現在地)から startOffset だけ離れた地点でフェードインしつつ、
    // walk アニメで歩いて所定の位置へ入場する。ラウンド開始前（CanAct=false）に呼ぶ前提。
    public void PlayEntranceAnimation(Vector3 startOffset, float duration)
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(EntranceRoutine(startOffset, Mathf.Max(0.1f, duration)));
    }

    private System.Collections.IEnumerator EntranceRoutine(Vector3 startOffset, float duration)
    {
        EnsureComponentReferences();
        SpriteRenderer sr = spriteRender != null ? spriteRender : GetComponentInChildren<SpriteRenderer>();
        Vector3 target = transform.position;
        Vector3 from = target + startOffset;
        transform.position = from;

        Color baseColor = sr != null ? sr.color : Color.white;
        if (sr != null) { Color c = baseColor; c.a = 0f; sr.color = c; }
        SetAnimatorBool("walking", true);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.position = Vector3.Lerp(from, target, k);
            if (sr != null) { Color c = baseColor; c.a = Mathf.Clamp01(k * 1.4f); sr.color = c; }
            yield return null;
        }

        transform.position = target;
        if (sr != null) sr.color = baseColor;
        SetAnimatorBool("walking", false);
    }

    // Animatorに指定Boolパラメータがあれば値を設定します。
    private void SetAnimatorBool(string parameter, bool value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameter, value);
    }

    // Animatorに指定Triggerパラメータがあれば発火します。
    private void SetAnimatorTrigger(string parameter)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(parameter);
    }

    // Animatorに指定Triggerパラメータがあればリセットします。
    private void ResetAnimatorTrigger(string parameter)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(parameter);
    }

    // 攻撃・死亡・移動などのアニメ状態を通常状態へ戻します。
    private void ResetActionAnimatorState()
    {
        StopAttackAnimation();
        SetAnimatorBool("walking", false);
        ResetAnimatorTrigger("dead");
    }

    // SpriteRendererやAnimatorの参照を、Visual子オブジェクトから取得します。
    private void EnsureComponentReferences()
    {
        EnsureVisualRoot();

        if (spriteRender == null || spriteRender.transform == transform)
            spriteRender = visualRoot != null
                ? visualRoot.GetComponent<SpriteRenderer>()
                : GetComponentInChildren<SpriteRenderer>();

        if (animator == null || animator.transform == transform)
            animator = visualRoot != null
                ? visualRoot.GetComponent<Animator>()
                : GetComponentInChildren<Animator>();
    }

    // ユニットの見た目をVisual子オブジェクトにまとめます。
    private void EnsureVisualRoot()
    {
        if (visualRoot == null)
        {
            Transform existingVisualRoot = transform.Find(VisualRootName);
            visualRoot = existingVisualRoot != null
                ? existingVisualRoot
                : new GameObject(VisualRootName).transform;

            visualRoot.SetParent(transform, false);
        }

        // ルートにSpriteRendererが残っている場合は、Visualへコピーしてルート側を無効化します。
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null && rootRenderer.enabled)
        {
            SpriteRenderer visualRenderer = visualRoot.GetComponent<SpriteRenderer>();
            if (visualRenderer == null)
                visualRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();

            CopySpriteRenderer(rootRenderer, visualRenderer);
            rootRenderer.enabled = false;
            spriteRender = visualRenderer;
        }

        // Animatorも同様にVisual側へ移し、拡大時の足元固定や描画順調整をしやすくします。
        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null && rootAnimator.enabled)
        {
            Animator visualAnimator = visualRoot.GetComponent<Animator>();
            if (visualAnimator == null)
                visualAnimator = visualRoot.gameObject.AddComponent<Animator>();

            visualAnimator.runtimeAnimatorController = rootAnimator.runtimeAnimatorController;
            visualAnimator.applyRootMotion = rootAnimator.applyRootMotion;
            visualAnimator.updateMode = rootAnimator.updateMode;
            visualAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            rootAnimator.enabled = false;
            animator = visualAnimator;
        }
    }

    // SpriteRendererの設定を別Rendererへコピーします。
    private void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer target)
    {
        target.sprite = source.sprite;
        target.color = source.color;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.sharedMaterials = source.sharedMaterials;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.drawMode = source.drawMode;
        target.size = source.size;
        target.tileMode = source.tileMode;
        target.maskInteraction = source.maskInteraction;
        target.spriteSortPoint = source.spriteSortPoint;
    }

    // Animatorが画面外でも再生されるよう、実行時設定を整えます。
    private void ConfigureAnimatorForRuntime()
    {
        if (animator == null)
            return;

        // R3-chest-room: 宝箱はAnimatorを使わず宝箱スプライトを表示し続ける（戦闘開始でジャガーノートに戻らないように）。
        if (IsChest)
        {
            animator.enabled = false;
            return;
        }

        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    // Animator Controllerが外れている場合、Assets/Animationsから自動復旧を試します。
    private void RestoreAnimatorControllerIfMissing()
    {
        if (animator == null || animator.runtimeAnimatorController != null)
            return;

#if UNITY_EDITOR
        string controllerUnitName = GetAnimatorControllerUnitName();
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/Animations/{controllerUnitName}/{controllerUnitName}.controller");
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
            ConfigureAnimatorForRuntime();
        }
#endif
    }

    // GameObject名から、Animator Controllerのフォルダ名として使うユニット名を取り出します。
    private string GetAnimatorControllerUnitName()
    {
        string controllerUnitName = UnitId.Replace("(Clone)", string.Empty).Trim();
        int starIndex = controllerUnitName.IndexOf(" Star", StringComparison.OrdinalIgnoreCase);
        if (starIndex >= 0)
            controllerUnitName = controllerUnitName.Substring(0, starIndex).Trim();

        return controllerUnitName;
    }

    // Animatorを初期状態から再生し直します。
    private void RestartAnimatorPlayback()
    {
        if (!CanUseAnimator())
            return;

        animator.Rebind();
        animator.Update(0f);
    }

    // 攻撃やスキルのアニメーション状態を止めます。
    private void StopAttackAnimation()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        SetAnimatorBool("attacking", false);
        ResetAnimatorTrigger("attack");
        SetAnimatorBool("abilitying", false);
        ResetAnimatorTrigger("ability");
        SetAnimatorPlaybackSpeed(1f);
    }

    // マナを0に戻し、MPバーにも反映します。
    private void ResetMana()
    {
        currentMana = 0;
        if (healthbar != null)
            healthbar.UpdateManaBar(currentMana, maxMana);
    }

    // 指定量のマナを獲得し、最大値を超えないようにします。
    private void GainMana(int amount)
    {
        if (maxMana <= 0 || amount <= 0 || dead)
            return;

        amount = Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Max(0f, synergyManaGainMultiplier)));
        currentMana = Mathf.Clamp(currentMana + amount, 0, maxMana);
        if (healthbar != null)
            healthbar.UpdateManaBar(currentMana, maxMana);
    }

    // 現在の攻撃速度から、攻撃1回にかかる秒数を計算します。
    private float GetAttackDuration()
    {
        return 1f / Mathf.Max(0.05f, GetEffectiveAttackSpeed());
    }

    // バフ・デバフ・スタンを含めた実際の攻撃速度を返します。
    private float GetEffectiveAttackSpeed()
    {
        if (stunned)
            return 0.05f;

        return Mathf.Max(0.05f, attackSpeed * (1f + synergyAttackSpeedBonus) * attackSpeedBuffMultiplier * attackSpeedDebuffMultiplier);
    }

    // シナジーで移動速度が上がっている場合も含めた、実際の移動速度です。
    private float GetEffectiveMovementSpeed()
    {
        return Mathf.Max(0.05f, movementSpeed * (1f + synergyMoveSpeedBonus));
    }

    // バフ込みの現在攻撃力を返します。
    private int GetCurrentDamage()
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageBuffMultiplier));
    }

    // 攻撃またはスキル用のAnimator状態を開始します。
    private void BeginActionAnimation(bool abilityAnimation, float playbackSpeed)
    {
        SetAnimatorPlaybackSpeed(playbackSpeed);
        SetAnimatorBool("walking", false);

        if (abilityAnimation)
        {
            SetAnimatorBool("attacking", false);
            SetAnimatorBool("abilitying", true);
            SetAnimatorTrigger("ability");
            return;
        }

        SetAnimatorBool("abilitying", false);
        SetAnimatorBool("attacking", true);
        SetAnimatorTrigger("attack");
    }

    // 攻撃またはスキル用のAnimator状態を終了します。
    private void EndActionAnimation()
    {
        SetAnimatorBool("attacking", false);
        ResetAnimatorTrigger("attack");
        SetAnimatorBool("abilitying", false);
        ResetAnimatorTrigger("ability");
        SetAnimatorPlaybackSpeed(1f);
    }

    // R1-collection: 図鑑詳細でスキル/攻撃の専用アニメを持つか。
    public bool HasAbilityAnimation => HasAnimationClip("Ability");

    private Coroutine previewActionRoutine;

    // R1-collection: 図鑑プレビュー用。盤外の実体に攻撃（ability=false）/スキル（ability=true）の
    // アクションを1回だけ再生させ、クリップ長後に待機へ戻す。戦闘ロジックには影響しない。
    public void PlayPreviewAction(bool ability)
    {
        if (!CanUseAnimator())
            return;
        if (ability && !HasAnimationClip("Ability"))
            ability = false;

        EndActionAnimation();
        BeginActionAnimation(ability, 1f);

        if (previewActionRoutine != null)
            StopCoroutine(previewActionRoutine);
        if (gameObject.activeInHierarchy)
            previewActionRoutine = StartCoroutine(PreviewActionReturn(ability));
    }

    private System.Collections.IEnumerator PreviewActionReturn(bool ability)
    {
        float len = GetAnimationClipLength(ability ? "Ability" : "Attack");
        if (len <= 0.05f)
            len = 0.8f;
        yield return new WaitForSecondsRealtime(len);
        EndActionAnimation();
        previewActionRoutine = null;
    }

    // R1-collection: 図鑑プレビューで利用できるモーション判定。
    public bool HasDeathAnimation => HasAnimatorParameter("dead", AnimatorControllerParameterType.Trigger);
    public bool HasWalkAnimation => HasAnimatorParameter("walking", AnimatorControllerParameterType.Bool);

    // R1-collection: 図鑑プレビューで指定モーションを再生する（idle/walk/attack/ability/death）。
    // 毎回 Rebind で待機へ戻してから該当モーションを流すので、死亡後でも次のクリックで復帰できる。
    public void PreviewSetMotion(string motion)
    {
        if (!CanUseAnimator())
            return;

        if (previewActionRoutine != null) { StopCoroutine(previewActionRoutine); previewActionRoutine = null; }
        animator.Rebind();
        animator.Update(0f);
        SetAnimatorBool("walking", false);
        SetAnimatorBool("attacking", false);
        SetAnimatorBool("abilitying", false);

        switch (motion)
        {
            case "walk":
                SetAnimatorBool("walking", true);
                break;
            case "attack":
                BeginActionAnimation(false, 1f);
                if (gameObject.activeInHierarchy) previewActionRoutine = StartCoroutine(PreviewActionReturn(false));
                break;
            case "ability":
                BeginActionAnimation(true, 1f);
                if (gameObject.activeInHierarchy) previewActionRoutine = StartCoroutine(PreviewActionReturn(true));
                break;
            case "death":
                SetAnimatorTrigger("dead");
                break;
            default:
                break; // idle（Rebind 済み）
        }
    }

    // Animatorの再生速度を変更します。攻撃速度変化に合わせてアニメも速く/遅くします。
    private void SetAnimatorPlaybackSpeed(float speed)
    {
        if (!CanUseAnimator())
            return;

        animator.speed = speed <= 0f ? 0f : Mathf.Clamp(speed, 0.05f, 8f);
    }

    // 自己回復スキルの回復量を計算します。
    private int GetSkillHealAmount()
    {
        return Mathf.Max(1, Mathf.RoundToInt((skillFlatHeal + MaxHealth * Mathf.Max(0f, skillHealPercent)) * GetSkillEffectMultiplier(true) * GlobalHealingMultiplier));
    }

    // 味方回復スキルの回復量を計算します。
    private int GetSkillAllyHealAmount()
    {
        return Mathf.Max(1, Mathf.RoundToInt((skillFlatAllyHeal + MaxHealth * Mathf.Max(0f, skillAllyHealPercent)) * GetSkillEffectMultiplier(true) * GlobalHealingMultiplier));
    }

    // シールドスキルで付与するシールド量を計算します。
    private int GetSkillShieldAmount()
    {
        return Mathf.Max(1, Mathf.RoundToInt((skillFlatShield + MaxHealth * Mathf.Max(0f, skillShieldPercent)) * GetSkillEffectMultiplier(true)));
    }

    // 単体攻撃スキルのダメージです。秘力型は攻撃力を混ぜず、専用基礎値と秘力だけを参照します。
    // === boss-skill-progression: 解放ボスの育成Lvでスキルが強化される ===
    // 味方(Team1)として出した解放ボスのみ育成Lvを反映。敵章ボス・非ボスは常に 1（基礎）。
    private int SkillAffinityLevel()
    {
        var sm = AutoChessBossRush.Save.SaveManager.Instance;
        if (myTeam != Team.Team1 || sm == null) return 1;
        return Mathf.Clamp(sm.GetBossAffinityLevel(UnitId), 1, 11);
    }
    // 連続スケール（ダメージ等に乗算）。Lv1=1.0 … Lv11≈1.5。
    private float SkillAffinityPower() => 1f + 0.05f * (SkillAffinityLevel() - 1);
    // 段階解禁のしきい値（固有スキルの追加効果はこれで分岐）。
    private bool SkillTierEnhanced() => SkillAffinityLevel() >= 2;   // 早期強化
    private bool SkillTierComplex() => SkillAffinityLevel() >= 4;    // 追加効果（拘束/吸命 等）が解禁
    private bool SkillTierMax() => SkillAffinityLevel() >= 8;        // 最大（効果が大きく拡張）

    // boss-skill-progression: ユニットを別の盤面セルへ強制移動（敵移動スキル用）。占有/盤外は安全に無視。
    public void ForceRelocateTo(Node node)
    {
        if (node == null || currentNode == null || node == currentNode || node.IsOccupied) return;
        currentNode.SetOccupied(false);
        previousNode = currentNode;
        currentNode = node;
        node.SetOccupied(true);
        transform.position = node.worldPosition;
    }

    // 敵全体を自分中心に引き寄せ(pull)／押し出し(!pull)する。各敵は方向先の最寄り空きセルへ1回移動。
    public void DisplaceEnemiesAround(bool pull, int cells)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || currentNode == null) return;
        int cc = grid.GetBoardColumn(currentNode), cr = grid.GetBoardRow(currentNode);
        List<BaseEntity> enemies = GetActiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity e = enemies[i];
            if (e == null || e.IsDead || e.IsCore || e.currentNode == null) continue;
            int ec = grid.GetBoardColumn(e.currentNode), er = grid.GetBoardRow(e.currentNode);
            int sc = System.Math.Sign(pull ? (cc - ec) : (ec - cc));
            int sr = System.Math.Sign(pull ? (cr - er) : (er - cr));
            if (sc == 0 && sr == 0) { if (!pull) sc = 1; else continue; } // 同一列・押し出しは横へ。
            Node dest = null;
            for (int step = Mathf.Max(1, cells); step >= 1 && dest == null; step--)
            {
                Node n = grid.GetNodeAtBoardCoordinate(ec + sc * step, er + sr * step);
                if (n != null && !n.IsOccupied) dest = n;
            }
            if (dest != null) e.ForceRelocateTo(dest);
        }
    }

    // boss-skill-progression: 解放ボスの育成Lv7+で固有スキルに「踏む必要のない複雑効果」を上乗せする（全章ボス分を集約）。
    //   敵移動(引き寄せ/ノックバック)・味方直接強化・全体デバフ・AoE吸命など。最大Lv(11)で強化。
    private void ApplyBossSignature(BaseEntity target)
    {
        // シグネチャ効果は味方として出した解放ボス限定（敵章ボスは基礎スキルのまま）。
        if (myTeam != Team.Team1) return;
        // 育成Lv1から常時発動（最初から“アルト”感）。育成Lv(複雑Lv4+/最大Lv8+)で拡張。
        bool max = SkillTierMax();
        int push = max ? 2 : 1;
        GameManager gm = GameManager.Instance;
        switch (NormalizeUnitId(UnitId))
        {
            case "caliber": // 暴走守護：薙ぎ圏の敵をノックバック＋周囲の味方を守護で固める。
                DisplaceEnemiesAround(false, push);
                gm?.EmpowerAlliesAround(this, BuffTileType.Defense, 2, GetSkillDuration(8f, true));
                break;
            case "neutral_rook": // 停滞城門：敵を門へ引き寄せ、最大Lvで根固め(スタン)。
                DisplaceEnemiesAround(true, push);
                if (max) { var en = GetActiveEnemies(); for (int i = 0; i < en.Count; i++) if (en[i] != null) en[i].ApplyStun(0.7f); }
                break;
            case "neutral_sister": // 鎮魂：全敵を弱体化し、最も弱った味方を治癒。
            {
                float wd = GetSkillDuration(3.6f, true); float wp = max ? 0.28f : 0.20f;
                var en = GetActiveEnemies(); for (int i = 0; i < en.Count; i++) if (en[i] != null) en[i].ApplyWeaken(wp, wd);
                var a = GetLowestHealthAlly(); if (a != null) a.HealSelf(GetSkillAllyHealAmount());
                break;
            }
            case "neutral_hydrax": DisplaceEnemiesAround(false, push); break;            // 奔流：敵をノックバック。
            case "magmarvaath": DisplaceEnemiesAround(true, push); break;                  // 溶岩：噴出点へ引き寄せ。
            case "magmarstarhorn": DisplaceEnemiesAround(false, max ? 3 : 2); break;       // 突撃：大ノックバック。
            case "magmarragnora": gm?.EmpowerAlliesAround(this, BuffTileType.Defense, 3, GetSkillDuration(7f, true)); break; // 再生：味方を堅守化。
            case "abyssallilithe": // 吸命：全敵から吸命(AoEドレイン)。
            {
                int d = Mathf.Max(1, Mathf.RoundToInt(CalculateAreaSkillDamage() * (max ? 0.6f : 0.45f)));
                int tot = 0; var en = GetActiveEnemies();
                for (int i = 0; i < en.Count; i++) { var e = en[i]; if (e == null) continue; DealLegendarySkillDamage(e, d, GetSkillDamageNumberKind()); tot += d; }
                HealSelf(Mathf.Max(1, Mathf.RoundToInt(tot * 0.25f * GlobalHealingMultiplier)));
                break;
            }
            case "abyssalcassyva": // 影群葬：全敵を短スタン。
            { float sd = max ? 0.9f : 0.6f; var en = GetActiveEnemies(); for (int i = 0; i < en.Count; i++) if (en[i] != null) en[i].ApplyStun(sd); break; }
            case "abyssalmaehv": DisplaceEnemiesAround(true, push); break;                 // 深淵：敵を引き寄せ密集。
            case "vetruvianzirix": DisplaceEnemiesAround(false, push); break;              // 砂嵐：周囲をノックバック。
            case "vetruviansajj": gm?.EmpowerAlliesAround(this, BuffTileType.Attack, 3, GetSkillDuration(7f, true)); break; // 太陽：味方を攻撃強化。
            case "vetruvianscion": if (target != null) target.ApplyStun(max ? 0.9f : 0.6f); break; // 貫き：対象を拘束。
            case "neutral_mechaz0rwing": gm?.EmpowerAlliesAround(this, BuffTileType.Attack, 3, GetSkillDuration(6f, true)); break;
            case "neutral_mechaz0rsword": if (target != null) target.ApplyStun(max ? 0.9f : 0.6f); break;
            case "neutral_mechaz0rsuper": gm?.EmpowerAlliesAround(this, BuffTileType.Attack, 3, GetSkillDuration(6f, true)); break;
            case "neutral_mechaz0rhelm": DisplaceEnemiesAround(false, push); break;        // 守護：敵を押し出し後衛を守る。
            case "neutral_mechaz0rchassis": DisplaceEnemiesAround(false, push); break;
            case "neutral_mechaz0rcannon": // 砲撃：着弾圏の敵を短スタン。
            { float sd = max ? 0.8f : 0.5f; var en = GetActiveEnemies(); for (int i = 0; i < en.Count; i++) if (en[i] != null) en[i].ApplyStun(sd); break; }
            case "arcana": gm?.EmpowerAlliesAround(this, BuffTileType.Arcane, 3, GetSkillDuration(7f, true)); break; // 終焉：味方を秘力強化。
        }
    }

    private int CalculateSingleTargetSkillDamage()
    {
        float baseValue = skillUsesFocusOnly ? skillBasePower : GetCurrentDamage();
        return Mathf.Max(1, Mathf.RoundToInt(baseValue * skillDamageMultiplier * GetSkillEffectMultiplier(true) * SkillAffinityPower()));
    }

    // 範囲攻撃スキルのダメージです。秘力型は攻撃力ではなく専用基礎値から計算します。
    private int CalculateAreaSkillDamage()
    {
        float baseValue = skillUsesFocusOnly ? skillBasePower : GetCurrentDamage();
        return Mathf.Max(1, Mathf.RoundToInt(baseValue * skillAreaDamageMultiplier * GetSkillEffectMultiplier(true) * SkillAffinityPower()));
    }

    // 秘力型スキルは青、攻撃力型スキルは橙のダメージ表示にします。
    private CombatNumberKind GetSkillDamageNumberKind()
    {
        return skillUsesFocusOnly ? CombatNumberKind.FocusDamage : CombatNumberKind.AttackDamage;
    }

    // イリジウムスケイル装備者が受ける回復量を増やします。
    private int ModifyIncomingHealing(int amount)
    {
        if (amount <= 0)
            return amount;

        return HasEquippedItem(IridiumScaleItemId)
            ? Mathf.Max(1, Mathf.RoundToInt(amount * 1.2f))
            : amount;
    }

    // イリジウムスケイル装備者が受けるシールド量を増やします。
    private int ModifyIncomingShield(int amount)
    {
        if (amount <= 0)
            return amount;

        return HasEquippedItem(IridiumScaleItemId)
            ? Mathf.Max(1, Mathf.RoundToInt(amount * 1.2f))
            : amount;
    }

    // 自分のHPを回復します。最大HPは超えません。
    private void HealSelf(int amount)
    {
        if (amount <= 0 || dead)
            return;

        amount = ModifyIncomingHealing(amount);
        int beforeHealth = baseHealth;
        baseHealth = Mathf.Min(MaxHealth, baseHealth + amount);
        int actualHeal = Mathf.Max(0, baseHealth - beforeHealth);
        if (actualHeal <= 0)
            return;

        ShowCombatNumber(actualHeal, CombatNumberKind.Healing);
        AttackEffectPlayer.PlayHealPulse(this);
        if (healthbar != null)
            healthbar.UpdateBar(baseHealth);
    }

    // HP割合が一番低い味方を回復します。対象がいなければ自分を回復します。
    private BaseEntity HealAlly(int amount)
    {
        BaseEntity ally = GetLowestHealthAlly();
        if (ally == null)
        {
            HealSelf(amount);
            return this;
        }

        ally.HealSelf(amount);
        HealNearbyAllies(ally, Mathf.RoundToInt(amount * 0.55f));
        return ally;
    }

    // 高コストサポートなどは、主対象の周囲も少し回復します。
    private void HealNearbyAllies(BaseEntity centerAlly, int amount)
    {
        if (centerAlly == null || amount <= 0 || skillAreaRadius < 2f || GameManager.Instance == null)
            return;

        AttackEffectPlayer.PlayAreaIndicator(this, centerAlly.transform.position, skillAreaRadius, 0.85f, 0.9f);

        Team opposingTeam = myTeam == Team.Team1 ? Team.Team2 : Team.Team1;
        List<BaseEntity> allies = GameManager.Instance.GetEntitiesAgainst(opposingTeam);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (ally == null || ally == centerAlly || ally.dead || !ally.IsOnBoard || ally.baseHealth >= ally.MaxHealth)
                continue;

            if (Vector3.Distance(ally.transform.position, centerAlly.transform.position) > skillAreaRadius)
                continue;

            ally.HealSelf(amount);
        }
    }

    // 同じチーム内で、最もHP割合が低い味方を探します。
    private BaseEntity GetLowestHealthAlly()
    {
        if (GameManager.Instance == null)
            return null;

        Team opposingTeam = myTeam == Team.Team1 ? Team.Team2 : Team.Team1;
        List<BaseEntity> allies = GameManager.Instance.GetEntitiesAgainst(opposingTeam);
        BaseEntity lowestHealthAlly = null;
        float lowestHealthRatio = 1.01f;

        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (ally == null || ally.dead || !ally.IsOnBoard || ally.baseHealth >= ally.MaxHealth)
                continue;

            if (ally.HealthRatio < lowestHealthRatio)
            {
                lowestHealthRatio = ally.HealthRatio;
                lowestHealthAlly = ally;
            }
        }

        return lowestHealthAlly;
    }

    // 一定時間だけHPの前に削られるシールドを付与します。
    private void ApplyShield(int amount, float duration)
    {
        if (amount <= 0 || dead)
            return;

        amount = ModifyIncomingShield(amount);
        amount = Mathf.Max(1, Mathf.RoundToInt(amount * GlobalShieldMultiplier));
        shieldHealth = Mathf.Max(shieldHealth, amount);
        if (healthbar != null)
            healthbar.UpdateShieldBar(shieldHealth, MaxHealth);

        RefreshShieldAuraVisual();

        if (shieldCoroutine != null)
            StopCoroutine(shieldCoroutine);

        shieldCoroutine = StartCoroutine(ShieldDurationCoroutine(Mathf.Max(0.1f, duration)));
    }

    // シナジーから付与されるシールドです。アイテムと同じシールド処理を使います。
    public void ApplyShieldFromSynergy(int amount, float duration)
    {
        ApplyShield(amount, duration);
    }

    // シナジーからHPを回復します。回復量増加アイテムも自然に反映されます。
    public void HealFromSynergy(int amount)
    {
        HealSelf(amount);
    }

    // シナジーからマナを獲得します。
    public void GainManaFromSynergy(int amount)
    {
        GainMana(amount);
    }

    // シナジーから一定時間の攻撃速度バフを受けます。
    public void ApplyAttackSpeedBoostFromSynergy(float multiplier, float duration)
    {
        ApplyAttackSpeedBoost(multiplier, duration);
    }

    // シナジーから常時の被ダメージ軽減を加算します。
    public void AddSynergyDamageReductionBonus(float amount)
    {
        synergyDamageReductionBonus += Mathf.Max(0f, amount);
    }

    // シナジーから常時の攻撃速度を加算します。
    public void AddSynergyAttackSpeedBonus(float amount)
    {
        synergyAttackSpeedBonus += Mathf.Max(0f, amount);
    }

    // シナジーから秘力を加算します。
    public void AddSynergyPowerBonus(float amount)
    {
        synergyPowerBonus += Mathf.Max(0f, amount);
    }

    // シナジーから一定時間だけ被ダメージ軽減を得ます。
    public void ApplyTimedSynergyDamageReductionBonus(float amount, float duration)
    {
        if (Mathf.Abs(amount) <= 0.001f)
            return;

        synergyDamageReductionBonus += amount;
        TrackSynergyCoroutine(RemoveSynergyDamageReductionAfterDelay(amount, Mathf.Max(0.1f, duration)));
    }

    // シナジーから一定時間だけ移動速度を上げます。
    public void ApplyTimedSynergyMoveSpeedBonus(float amount, float duration)
    {
        if (Mathf.Abs(amount) <= 0.001f)
            return;

        synergyMoveSpeedBonus += amount;
        TrackSynergyCoroutine(RemoveSynergyMoveSpeedAfterDelay(amount, Mathf.Max(0.1f, duration)));
    }

    // シナジーから一定時間だけ与ダメージ補正を加減します。
    public void ApplyTimedSynergyDamageDealtBonus(float amount, float duration)
    {
        if (Mathf.Abs(amount) <= 0.001f)
            return;

        synergyDamageDealtBonus += amount;
        TrackSynergyCoroutine(RemoveSynergyDamageDealtAfterDelay(amount, Mathf.Max(0.1f, duration)));
    }

    // シナジーから一定時間だけ秘力（スキル威力）を加算します（R3: 秘力系オーラ/強化マス用）。
    // synergyPowerBonus は GetItemFocusMultiplier 経由でスキルダメージ/回復/シールド/効果時間を伸ばす。
    public void ApplyTimedSynergyPowerBonus(float amount, float duration)
    {
        if (Mathf.Abs(amount) <= 0.001f)
            return;

        synergyPowerBonus += amount;
        TrackSynergyCoroutine(RemoveSynergyPowerBonusAfterDelay(amount, Mathf.Max(0.1f, duration)));
    }

    // シナジーから一定時間だけマナ獲得倍率を変えます。
    public void ApplyTimedSynergyManaGainMultiplier(float multiplier, float duration)
    {
        multiplier = Mathf.Max(0f, multiplier);
        if (Mathf.Abs(multiplier - 1f) <= 0.001f)
            return;

        float previousMultiplier = synergyManaGainMultiplier;
        synergyManaGainMultiplier = multiplier;
        TrackSynergyCoroutine(RestoreSynergyManaGainMultiplierAfterDelay(previousMultiplier, Mathf.Max(0.1f, duration)));
    }

    // 炎獄の燃焼です。すでに燃えている場合は、効果時間とダメージを強い方へ更新します。
    public void ApplyInfernoBurnFromSynergy(BaseEntity source, int tickDamage, float duration)
    {
        if (dead || duration <= 0f)
            return;

        infernoBurnSource = source;
        infernoBurnTickDamage = Mathf.Max(infernoBurnTickDamage, Mathf.Max(1, tickDamage));
        infernoBurnUntil = Mathf.Max(infernoBurnUntil, Time.time + duration);

        if (infernoBurnCoroutine == null && gameObject.activeInHierarchy)
            infernoBurnCoroutine = StartCoroutine(InfernoBurnCoroutine());

        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, transform.position, 0.75f);
    }

    // 氷晶の凍結蓄積です。短時間に何度も攻撃されると、短いスタンへ変換します。
    public void ApplyFrostStackFromSynergy(float freezeDuration)
    {
        if (dead)
            return;

        if (Time.time > frostFreezeStackUntil)
            frostFreezeStacks = 0;

        frostFreezeStacks++;
        frostFreezeStackUntil = Time.time + 3f;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, transform.position, 0.65f);

        if (frostFreezeStacks < 3)
            return;

        frostFreezeStacks = 0;
        ApplyStun(freezeDuration);
    }

    // 神聖の復活加護を付与します。死亡寸前に一度だけ消費されます。
    public void ApplyDivineProtectionFromSynergy(float reviveHealthPercent, bool healNearby)
    {
        if (dead)
            return;

        divineProtectionActive = true;
        divineProtectionReviveHealthPercent = Mathf.Clamp(reviveHealthPercent, 0.05f, 0.75f);
        divineProtectionHealsNearby = healNearby;
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 0.95f);
    }

    // Wraith6の復活で、死亡処理に入る前にHPを戻します。
    public void ReviveFromSynergy(float healthPercent)
    {
        dead = false;
        deathDestroyStarted = false;
        baseHealth = Mathf.Clamp(Mathf.RoundToInt(MaxHealth * Mathf.Clamp01(healthPercent)), 1, MaxHealth);
        canAttack = true;
        SetAnimatorBool("walking", false);

        foreach (Collider2D entityCollider in GetComponents<Collider2D>())
            entityCollider.enabled = true;

        if (healthbar != null)
        {
            healthbar.gameObject.SetActive(true);
            healthbar.UpdateBar(baseHealth);
            healthbar.UpdateManaBar(currentMana, maxMana);
        }
    }

    // 戦闘終了時にシナジー由来の一時補正だけを消します。割り当て済みシナジーは消しません。
    public void ClearSynergyBattleState()
    {
        for (int i = 0; i < synergyCoroutines.Count; i++)
        {
            if (synergyCoroutines[i] != null)
                StopCoroutine(synergyCoroutines[i]);
        }

        synergyCoroutines.Clear();
        synergyDamageReductionBonus = 0f;
        synergyAttackSpeedBonus = 0f;
        synergyPowerBonus = 0f;
        synergyMoveSpeedBonus = 0f;
        synergyDamageDealtBonus = 0f;
        synergyManaGainMultiplier = 1f;
        rangerFocusTarget = null;
        rangerFocusStacks = 0;
        beastAttackSpeedStacks = 0;
        warriorLastStandUsed = false;
        machineEmergencyRepairUsed = false;
        infernoBurnUntil = 0f;
        infernoBurnTickDamage = 0;
        infernoBurnSource = null;
        frostFreezeStacks = 0;
        frostFreezeStackUntil = 0f;
        divineProtectionActive = false;
        divineProtectionHealsNearby = false;
        divineProtectionReviveHealthPercent = 0f;
        if (infernoBurnCoroutine != null)
        {
            StopCoroutine(infernoBurnCoroutine);
            infernoBurnCoroutine = null;
        }
        lastDamageSource = null;
    }

    private void TrackSynergyCoroutine(IEnumerator routine)
    {
        if (!gameObject.activeInHierarchy)
            return;

        synergyCoroutines.Add(StartCoroutine(routine));
    }

    private IEnumerator RemoveSynergyDamageReductionAfterDelay(float amount, float duration)
    {
        yield return new WaitForSeconds(duration);
        synergyDamageReductionBonus -= amount;
    }

    private IEnumerator RemoveSynergyMoveSpeedAfterDelay(float amount, float duration)
    {
        yield return new WaitForSeconds(duration);
        synergyMoveSpeedBonus -= amount;
    }

    private IEnumerator RemoveSynergyDamageDealtAfterDelay(float amount, float duration)
    {
        yield return new WaitForSeconds(duration);
        synergyDamageDealtBonus -= amount;
    }

    private IEnumerator RemoveSynergyPowerBonusAfterDelay(float amount, float duration)
    {
        yield return new WaitForSeconds(duration);
        synergyPowerBonus -= amount;
    }

    private IEnumerator RestoreSynergyManaGainMultiplierAfterDelay(float previousMultiplier, float duration)
    {
        yield return new WaitForSeconds(duration);
        synergyManaGainMultiplier = previousMultiplier;
    }

    private IEnumerator InfernoBurnCoroutine()
    {
        while (!dead && Time.time < infernoBurnUntil)
        {
            yield return new WaitForSeconds(1f);
            if (dead || Time.time > infernoBurnUntil)
                break;

            TakeDamage(infernoBurnTickDamage, infernoBurnSource, CombatNumberKind.FocusDamage);
        }

        infernoBurnCoroutine = null;
        if (Time.time >= infernoBurnUntil)
        {
            infernoBurnUntil = 0f;
            infernoBurnTickDamage = 0;
            infernoBurnSource = null;
        }
    }

    private bool TryUseDivineProtection()
    {
        if (!divineProtectionActive)
            return false;

        divineProtectionActive = false;
        ReviveFromSynergy(divineProtectionReviveHealthPercent);
        ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.10f)), 3f);
        AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, transform.position, 1.15f);

        if (divineProtectionHealsNearby)
            HealNearbyAllies(this, Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.08f)));

        return true;
    }

    // シールド中だけユニット本体を覆う見た目を出します。
    private void RefreshShieldAuraVisual()
    {
        if (shieldHealth <= 0 || dead || !gameObject.activeInHierarchy)
        {
            DestroyShieldAuraVisual();
            return;
        }

        if (shieldAuraObject == null)
            shieldAuraObject = AttackEffectPlayer.AttachShieldAura(this);
    }

    // シールドが切れた時や死亡/再配置時に、追従エフェクトを片付けます。
    private void DestroyShieldAuraVisual()
    {
        if (shieldAuraObject == null)
            return;

        Destroy(shieldAuraObject);
        shieldAuraObject = null;
    }

    // 一定時間、自分の攻撃速度を上げます。
    private void ApplyAttackSpeedBoost(float multiplier, float duration)
    {
        attackSpeedBuffMultiplier = Mathf.Max(1f, multiplier);

        if (attackSpeedBoostCoroutine != null)
            StopCoroutine(attackSpeedBoostCoroutine);

        attackSpeedBoostCoroutine = StartCoroutine(ResetAttackSpeedBoostAfterDelay(Mathf.Max(0.1f, duration)));
    }

    // サポート用です。自分だけでなく周囲の味方にも攻撃速度バフを配ります。
    private void ApplyAttackSpeedBoostToNearbyAllies(float multiplier, float duration)
    {
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, Mathf.Max(2f, skillAreaRadius), 0.85f, 0.9f);
        List<BaseEntity> allies = GetNearbyAlliesForAura();
        for (int i = 0; i < allies.Count; i++)
            allies[i].ApplyAttackSpeedBoost(multiplier, duration);
    }

    // 攻撃速度アップの効果時間が終わったら元に戻します。
    private IEnumerator ResetAttackSpeedBoostAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        attackSpeedBoostCoroutine = null;
        attackSpeedBuffMultiplier = 1f;
    }

    // 一定時間、自分の通常攻撃/スキルダメージを上げます。
    private void ApplyDamageBoost(float multiplier, float duration)
    {
        damageBuffMultiplier = Mathf.Max(1f, multiplier);

        if (damageBoostCoroutine != null)
            StopCoroutine(damageBoostCoroutine);

        damageBoostCoroutine = StartCoroutine(ResetDamageBoostAfterDelay(Mathf.Max(0.1f, duration)));
    }

    // サポート用です。周囲の味方へ通常攻撃ダメージバフを配ります。
    private void ApplyDamageBoostToNearbyAllies(float multiplier, float duration)
    {
        AttackEffectPlayer.PlayAreaIndicator(this, transform.position, Mathf.Max(2f, skillAreaRadius), 0.85f, 0.9f);
        List<BaseEntity> allies = GetNearbyAlliesForAura();
        for (int i = 0; i < allies.Count; i++)
            allies[i].ApplyDamageBoost(multiplier, duration);
    }

    // サポートが影響を与える周囲味方の一覧を返します。範囲が狭い場合でも最低2マスは届きます。
    private List<BaseEntity> GetNearbyAlliesForAura()
    {
        List<BaseEntity> result = new List<BaseEntity>();
        if (GameManager.Instance == null)
            return result;

        Team opposingTeam = myTeam == Team.Team1 ? Team.Team2 : Team.Team1;
        List<BaseEntity> allies = GameManager.Instance.GetEntitiesAgainst(opposingTeam);
        float auraRadius = Mathf.Max(2f, skillAreaRadius);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (ally == null || ally.dead || !ally.IsOnBoard)
                continue;

            if (Vector3.Distance(ally.transform.position, transform.position) <= auraRadius)
                result.Add(ally);
        }

        return result;
    }

    // サポート系ユニットは自己バフではなく、周囲に影響するオーラとして扱います。
    private bool ShouldApplySupportAura()
    {
        string id = NormalizeUnitId(UnitId);
        return id == "city" || id == "candypanda" || id == "snowchasermk";
    }

    // ダメージアップの効果時間が終わったら元に戻します。
    private IEnumerator ResetDamageBoostAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        damageBoostCoroutine = null;
        damageBuffMultiplier = 1f;
    }

    // 敵から受けるスロウ効果です。一定時間、攻撃速度を下げます。
    public void ApplyAttackSpeedSlow(float multiplier, float duration)
    {
        if (dead)
            return;

        attackSpeedDebuffMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);

        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);

        slowCoroutine = StartCoroutine(ResetAttackSpeedSlowAfterDelay(Mathf.Max(0.1f, duration)));
    }

    // スロウの効果時間が終わったら攻撃速度を元に戻します。
    private IEnumerator ResetAttackSpeedSlowAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        slowCoroutine = null;
        attackSpeedDebuffMultiplier = 1f;
    }

    // スタン効果です。一定時間、攻撃と移動を止めます。
    public void ApplyStun(float duration)
    {
        if (dead)
            return;

        stunned = true;
        StopAttackAnimation();
        ClearMovementReservation();
        SetAnimatorPlaybackSpeed(0f);

        if (stunCoroutine != null)
            StopCoroutine(stunCoroutine);

        stunCoroutine = StartCoroutine(ClearStunAfterDelay(Mathf.Max(0.1f, duration)));
    }

    // スタンの効果時間が終わったら行動できるように戻します。
    private IEnumerator ClearStunAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        stunCoroutine = null;
        stunned = false;
        SetAnimatorPlaybackSpeed(1f);
        canAttack = true;
    }

    // 対象周囲の敵にまとめてダメージを与えます。
    private void ApplyAreaDamage(BaseEntity targetAtCast)
    {
        if (targetAtCast == null || GameManager.Instance == null)
            return;

        // エフェクトは中心になる対象位置へ出し、範囲内の敵全員にダメージを入れます。
        AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
        AttackEffectPlayer.PlayAreaIndicator(this, targetAtCast.transform.position, skillAreaRadius, 0.85f, 1f);
        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        int areaDamage = CalculateAreaSkillDamage();

        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                continue;

            if (Vector3.Distance(enemy.transform.position, targetAtCast.transform.position) > skillAreaRadius)
                continue;

            enemy.TakeDamage(areaDamage, this, GetSkillDamageNumberKind());
            SynergyManager.Instance?.NotifySkillDamageHit(this, enemy, areaDamage);
            if (ShouldAreaSkillApplySlow())
                enemy.ApplyAttackSpeedSlow(0.72f, GetSkillDuration(2.2f, true));

            if (ShouldAreaSkillApplyBurn())
                burnDamageCoroutines.Add(StartCoroutine(ApplyBurnDamage(enemy, Mathf.Max(1, Mathf.RoundToInt(areaDamage * 0.18f)), 3, 0.65f)));
        }
    }

    // 炎系の範囲スキルは、命中後に少しだけ継続ダメージを残します。
    private IEnumerator ApplyBurnDamage(BaseEntity enemy, int tickDamage, int tickCount, float interval)
    {
        for (int i = 0; i < tickCount; i++)
        {
            yield return new WaitForSeconds(interval);
            if (!IsInActiveRound() || enemy == null || enemy.dead || !enemy.IsOnBoard)
                yield break;

            enemy.TakeDamage(tickDamage, this, CombatNumberKind.FocusDamage);
        }
    }

    // 雷や影の範囲スキルは、短時間だけ敵の攻撃速度を落とします。
    private bool ShouldAreaSkillApplySlow()
    {
        string id = NormalizeUnitId(UnitId);
        return id == "maehvmk" || id == "vampire";
    }

    // 炎系メイジや火属性ボスは、範囲攻撃に継続ダメージを持ちます。
    private bool ShouldAreaSkillApplyBurn()
    {
        string id = NormalizeUnitId(UnitId);
        return id == "cindera" || id == "solfist";
    }

    // シールドの効果時間が終わったらシールド量を0にします。
    private IEnumerator ShieldDurationCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        shieldCoroutine = null;
        shieldHealth = 0;
        if (healthbar != null)
            healthbar.UpdateShieldBar(0, MaxHealth);

        DestroyShieldAuraVisual();
    }

    // 現在のシールドを強制的に消します。ラウンド終了や再配置時に使います。
    private void ClearShield()
    {
        if (shieldCoroutine != null)
        {
            StopCoroutine(shieldCoroutine);
            shieldCoroutine = null;
        }

        shieldHealth = 0;
        if (healthbar != null)
            healthbar.UpdateShieldBar(0, MaxHealth);

        DestroyShieldAuraVisual();
    }

    // バフ、デバフ、スタンなど戦闘中だけの状態をすべて解除します。
    private void ClearTemporaryStatusEffects()
    {
        if (attackSpeedBoostCoroutine != null)
        {
            StopCoroutine(attackSpeedBoostCoroutine);
            attackSpeedBoostCoroutine = null;
        }

        if (damageBoostCoroutine != null)
        {
            StopCoroutine(damageBoostCoroutine);
            damageBoostCoroutine = null;
        }

        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }

        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
            stunCoroutine = null;
        }

        if (invaderThunderCoroutine != null)
        {
            StopCoroutine(invaderThunderCoroutine);
            invaderThunderCoroutine = null;
        }

        if (golBlackHoleCoroutine != null)
        {
            StopCoroutine(golBlackHoleCoroutine);
            golBlackHoleCoroutine = null;
        }

        if (skyfallRampageCoroutine != null)
        {
            StopCoroutine(skyfallRampageCoroutine);
            skyfallRampageCoroutine = null;
        }

        for (int i = 0; i < burnDamageCoroutines.Count; i++)
        {
            if (burnDamageCoroutines[i] != null)
                StopCoroutine(burnDamageCoroutines[i]);
        }

        burnDamageCoroutines.Clear();
        attackSpeedBuffMultiplier = 1f;
        damageBuffMultiplier = 1f;
        attackSpeedDebuffMultiplier = 1f;
        stunned = false;
        canAttack = true;
        ResetItemCombatState();
        ClearSynergyBattleState();
        SetAnimatorPlaybackSpeed(1f);
    }

    // ラウンド開始時に発動するアイテム効果です。
    private void ApplyRoundStartItemEffects()
    {
        if (HasEquippedItem(IronBulwarkItemId))
            ApplyShield(Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.12f)), 8f);

        if (HasEquippedItem(DarkstoneRingItemId))
            GainMana(15);

        if (HasEquippedItem(EternalHeartItemId))
            eternalHeartCoroutine = StartCoroutine(EternalHeartRegenerationCoroutine());
    }

    // 永久の心臓は戦闘中、ゆっくり自己回復し続けます。
    private IEnumerator EternalHeartRegenerationCoroutine()
    {
        while (IsInActiveRound() && HasEquippedItem(EternalHeartItemId))
        {
            yield return new WaitForSeconds(3f);

            if (!IsInActiveRound() || !HasEquippedItem(EternalHeartItemId))
                break;

            HealSelf(Mathf.Max(1, Mathf.RoundToInt(MaxHealth * 0.04f)));
        }

        eternalHeartCoroutine = null;
    }

    // 怒りのチャクラムは敵を倒した時に攻撃速度を一時的に上げます。
    private void ApplyKillTriggeredItemEffects(BaseEntity diedUnit)
    {
        if (diedUnit == null || !IsEnemyEntity(diedUnit) || !HasEquippedItem(RageChakramItemId) || !IsInActiveRound())
            return;

        rageChakramStacks = Mathf.Min(3, rageChakramStacks + 1);
        ApplyAttackSpeedBoost(1f + rageChakramStacks * 0.2f, 4f);

        if (rageChakramResetCoroutine != null)
            StopCoroutine(rageChakramResetCoroutine);

        rageChakramResetCoroutine = StartCoroutine(ClearRageChakramAfterDelay(4f));
    }

    // 怒りのチャクラムの撃破スタックを一定時間後に消します。
    private IEnumerator ClearRageChakramAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        rageChakramResetCoroutine = null;
        rageChakramStacks = 0;
    }

    // 戦闘中だけ使うアイテムカウンターや一時効果をまとめてリセットします。
    private void ResetItemCombatState()
    {
        if (eternalHeartCoroutine != null)
        {
            StopCoroutine(eternalHeartCoroutine);
            eternalHeartCoroutine = null;
        }

        if (unboundedAmuletCoroutine != null)
        {
            StopCoroutine(unboundedAmuletCoroutine);
            unboundedAmuletCoroutine = null;
        }

        if (rageChakramResetCoroutine != null)
        {
            StopCoroutine(rageChakramResetCoroutine);
            rageChakramResetCoroutine = null;
        }

        unboundedManaWindowActive = false;
        ykirFirstSkillConsumed = false;
        skywindAttackCounter = 0;
        godhammerAttackCounter = 0;
        thunderclapTakenHitCounter = 0;
        rageChakramStacks = 0;
        adamantineStacks = 0;
        adamantineTarget = null;
        spineCleaverShredStacks = 0;
        spineCleaverShredUntil = 0f;
        currentItemSkillEffectMultiplier = 1f;
    }

    // 新しいターゲットを設定し、向きと移動予約を更新します。
    private void SetTarget(BaseEntity target)
    {
        if (currentTarget == target)
            return;

        currentTarget = target;
        FaceTarget(currentTarget);
        ClearMovementReservation();
    }

    // 移動先として予約していたNodeを解放し、移動状態を解除します。
    private void ClearMovementReservation()
    {
        if (destination != null && destination != currentNode && moving)
            destination.SetOccupied(false);

        destination = null;
        moving = false;
        previousNode = null; // 射程内に落ち着いた等、移動を止めた時は往復防止メモリをリセット。
        moveGoal = null;      // 目標足場もリセット（次の交戦で選び直す）。
        SetAnimatorBool("walking", false);
    }

    // 指定ターゲットが現在の射程内にいるかを距離で判定します。
    private bool IsTargetInRange(BaseEntity target)
    {
        if (target == null) return false;
        // 盤面はマス間隔1.0で、移動グラフは縦横斜め1マスを隣接として繋ぐ（max(|dx|,|dy|)<=1）。
        // 射程もこのグリッド距離（チェビシェフ）で判定する。ユークリッドだと斜め隣接(≈1.41)が射程外になり、
        // メレーが敵の斜めに居着いたまま攻撃できず直交マスを探して「ウロウロ」してしまうのを防ぐ。
        Vector3 d = transform.position - target.transform.position;
        float cheby = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
        return cheby <= range + 0.05f;
    }

    // Animator Controller内から、名前にclipNameを含むAnimationClipの長さを探します。
    private float GetAnimationClipLength(string clipName)
    {
        if (!CanUseAnimator())
            return 0f;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name.IndexOf(clipName, StringComparison.OrdinalIgnoreCase) >= 0)
                return clip.length;
        }

        return 0f;
    }

    // 指定名のAnimationClipが存在するか確認します。
    private bool HasAnimationClip(string clipName)
    {
        return GetAnimationClipLength(clipName) > 0f;
    }

    // Animatorに指定名・指定型のパラメータがあるか確認します。
    private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType parameterType)
    {
        if (!CanUseAnimator())
            return false;

        foreach (AnimatorControllerParameter animatorParameter in animator.parameters)
        {
            if (animatorParameter.name == parameter && animatorParameter.type == parameterType)
                return true;
        }

        return false;
    }

    // チームに応じて左右反転とアウトライン色を設定します。
    private void ApplyTeamVisuals()
    {
        if (spriteRender == null)
            return;

        spriteRender.flipX = myTeam == Team.Team2;

        Material outlineMaterial = myTeam == Team.Team2 ? team2OutlineMaterial : team1OutlineMaterial;
        if (outlineMaterial == null)
            outlineMaterial = GetFallbackOutlineMaterial(myTeam);

        if (outlineMaterial != null)
            spriteRender.sharedMaterial = outlineMaterial;

        UpdateSpriteUvBounds(true);
    }

    // ターゲットのいる方向へ左右反転します。
    private void FaceTarget(BaseEntity target)
    {
        if (target == null)
            return;

        FaceDirection(target.transform.position.x - transform.position.x);
    }

    // x方向の差から、右向き/左向きを決めます。
    private void FaceDirection(float directionX)
    {
        if (spriteRender == null || Mathf.Abs(directionX) < 0.02f)
            return;

        spriteRender.flipX = directionX < 0f;
    }

    // HPバーがまだ無い場合、Prefabから生成します。
    private void EnsureHealthBar()
    {
        if (healthbar != null || barPrefab == null)
            return;

        healthbar = Instantiate(barPrefab);
        healthbar.Setup(this.transform, MaxHealth, StarLevel, spriteRender, this);
        UpdateHealthBarItemIcons();
    }

    // Prefabに設定されていた元ステータスを保存します。スターアップ計算の基準になります。
    private void CaptureBaseStats()
    {
        if (baseStatsCaptured)
            return;

        originalBaseDamage = baseDamage;
        originalBaseHealth = baseHealth;
        originalRange = range;
        originalAttackSpeed = attackSpeed;
        originalMovementSpeed = movementSpeed;
        originalBaseDamageReduction = baseDamageReduction;
        originalMaxMana = maxMana;
        originalManaOnAttack = manaOnAttack;
        originalManaOnDamageTaken = manaOnDamageTaken;
        originalScale = transform.localScale;
        baseStatsCaptured = true;
    }

    // オーグメント取得など、外部要因で派生ステータスを作り直す必要がある時に呼びます。
    public void RefreshDerivedStats(bool refillHealth = false)
    {
        ApplyCurrentStats(refillHealth);
    }

    // balance/STORY: 敵としての中ボス個体だけに乗せる強化（サイズ/射程/ステータス倍率）。
    // 仲間化後のアライは CreateBenchEntity で別個体として生成されるため、この強化は乗らない。
    public void ApplyEnemyBossBuff(float sizeMul, float rangeMul, float statMul)
    {
        CaptureBaseStats(); // originalScale/originalRange を確定させてから乗せる。
        enemyBossRangeMul = Mathf.Max(0.1f, rangeMul);
        enemyBossStatMul = Mathf.Max(0.1f, statMul);
        if (sizeMul > 0f && !Mathf.Approximately(sizeMul, 1f))
        {
            originalScale *= sizeMul;     // 以降の originalScale 復帰でも拡大を保つ。
            transform.localScale = originalScale;
        }
        RefreshDerivedStats(true);
    }

    // ⑥ アイテムドラッグ中のホバー対象を「枠発光」で示す。本体スプライトの背後に、同じスプライトを
    // 少し拡大した金色で重ねて縁が光って見えるようにする（毎フレーム呼ばれる前提でスプライトを追従）。
    private SpriteRenderer equipGlowRenderer;
    public void SetEquipDragHighlight(bool on)
    {
        if (spriteRender == null)
            return;

        if (!on)
        {
            if (equipGlowRenderer != null)
                equipGlowRenderer.enabled = false;
            return;
        }

        if (equipGlowRenderer == null)
        {
            GameObject g = new GameObject("EquipDragGlow", typeof(SpriteRenderer));
            g.transform.SetParent(spriteRender.transform, false);
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = Vector3.one * 1.10f;
            equipGlowRenderer = g.GetComponent<SpriteRenderer>();
        }
        // 本体の現在フレームに追従させ、背後に金色で光らせる。
        equipGlowRenderer.sprite = spriteRender.sprite;
        equipGlowRenderer.flipX = spriteRender.flipX;
        equipGlowRenderer.sortingLayerID = spriteRender.sortingLayerID;
        equipGlowRenderer.sortingOrder = spriteRender.sortingOrder - 1;
        float pulse = 0.7f + 0.3f * (Mathf.Sin(Time.unscaledTime * 6f) * 0.5f + 0.5f);
        equipGlowRenderer.color = new Color(1f, 0.92f, 0.35f, pulse);
        equipGlowRenderer.enabled = true;
    }

    // ⑥ 後ろのユニットをホバー中、手前で重なるユニットの不透明度を下げて見やすくする。
    public void SetOcclusionDim(bool on)
    {
        if (spriteRender == null)
            return;
        Color c = spriteRender.color;
        c.a = on ? 0.35f : 1f;
        spriteRender.color = c;
    }

    // ⑧ ボス登場演出用：攻撃モーションを1回だけ再生する。
    // Animatorは "attacking"/"abilitying" 等のboolで駆動されるため animator.Play では待機へ戻されてしまう。
    // 図鑑プレビューと同じ正規ルート(PlayPreviewAction=BeginActionAnimationでboolとトリガーを立て、クリップ長後に待機へ戻す)を使う。
    public void PlayAttackMotionOnce()
    {
        PlayPreviewAction(false);
    }

    // ⑧ 登場演出用：攻撃モーション（Attack）の再生時間。PlayPreviewActionと同じ算出で、
    // 「攻撃が終わって待機へ戻る」までの待ち時間に使う。
    public float GetAttackMotionDuration()
    {
        float len = GetAnimationClipLength("Attack");
        return len > 0.05f ? len : 0.8f;
    }

    // ⑧ 登場演出用：見た目（スプライト）の表示/非表示を切り替える（取り巻きを後から歩いて登場させるため）。
    public void SetVisualHidden(bool hidden)
    {
        if (spriteRender != null)
            spriteRender.enabled = !hidden;
    }

    // スターと装備アイテムを合わせた現在ステータスを作り直します。
    private void ApplyCurrentStats(bool refillHealth)
    {
        float previousHealthRatio = MaxHealth <= 0 ? 1f : Mathf.Clamp01((float)baseHealth / MaxHealth);
        float starDamageHealthMultiplier = GetStarDamageHealthMultiplier(StarLevel);

        int itemHealthFlat = equippedItems.Sum(item => item != null ? item.healthFlat : 0);
        float itemHealthMultiplier = 1f + equippedItems.Sum(item => item != null ? item.healthPercent : 0f);
        int itemDamageFlat = equippedItems.Sum(item => item != null ? item.damageFlat : 0);
        float itemDamageMultiplier = 1f + equippedItems.Sum(item => item != null ? item.damagePercent : 0f);
        float itemAttackSpeedMultiplier = 1f + equippedItems.Sum(item => item != null ? item.attackSpeedPercent : 0f);
        int manaAttackBonus = equippedItems.Sum(item => item != null ? item.manaOnAttackBonus : 0);
        int manaDamageBonus = equippedItems.Sum(item => item != null ? item.manaOnDamageTakenBonus : 0);
        int maxManaReduction = equippedItems.Sum(item => item != null ? item.maxManaReduction : 0);

        // オーグメント由来のチームバフ（プレイヤー側のみ）。乗算で乗せ、被ダメ軽減は加算。
        float teamDamageMul = 1f;
        float teamHealthMul = 1f;
        float teamAttackSpeedMul = 1f;
        float teamMoveSpeedMul = 1f;
        float teamDamageReduction = 0f;
        if (myTeam == Team.Team1 && GameManager.Instance != null)
        {
            teamDamageMul = 1f + GameManager.Instance.TeamAttackBonusPercent;
            teamHealthMul = 1f + GameManager.Instance.TeamHPBonusPercent;
            teamAttackSpeedMul = 1f + GameManager.Instance.TeamAttackSpeedBonusPercent;
            teamMoveSpeedMul = 1f + GameManager.Instance.TeamMoveSpeedBonusPercent;
            teamDamageReduction = GameManager.Instance.TeamDamageReductionBonus;
            // prism_dark_pact: 与ダメ+15%、被ダメ+15%
            if (GameManager.Instance.HasAugment("prism_dark_pact"))
            {
                teamDamageMul += 0.15f;
                teamDamageReduction -= 0.15f; // 被ダメ+15% = 軽減-15%
            }
            // silver_cost1_hp: コスト1ユニットは最大HP +10%
            if (GameManager.Instance.HasAugment("silver_cost1_hp") && BaseCost == 1)
                teamHealthMul += 0.10f;
        }

        float heroMul = heroScaleMultiplier > 0f ? heroScaleMultiplier : 1f; // R3-hero-scale: ヒーロー育成倍率（非ヒーローは1.0）。
        float fp = Mathf.Clamp(formPenalty, 0.05f, 1f); // 形態ペナルティ（弱体形態）。
        baseDamage = Mathf.Max(1, Mathf.RoundToInt(((originalBaseDamage * starDamageHealthMultiplier + itemDamageFlat) * itemDamageMultiplier) * teamDamageMul * heroMul * fp * enemyBossStatMul));
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(((originalBaseHealth * starDamageHealthMultiplier + itemHealthFlat) * itemHealthMultiplier) * teamHealthMul * heroMul * fp * enemyBossStatMul));
        range = Mathf.Max(1, Mathf.RoundToInt(originalRange * enemyBossRangeMul));
        // silver_range_archer: Ranger シナジーを持つユニットは射程 +1
        if (myTeam == Team.Team1 && GameManager.Instance != null
            && GameManager.Instance.HasAugment("silver_range_archer")
            && HasSynergy(SynergyType.Ranger))
            range += 1;
        attackSpeed = originalAttackSpeed * GetStarAttackSpeedMultiplier(StarLevel) * itemAttackSpeedMultiplier * teamAttackSpeedMul;
        movementSpeed = originalMovementSpeed * GetStarMovementSpeedMultiplier(StarLevel) * teamMoveSpeedMul;
        baseDamageReduction = Mathf.Clamp(originalBaseDamageReduction + GetStarDamageReductionBonus(StarLevel) + teamDamageReduction, -0.5f, 0.75f);
        manaOnAttack = Mathf.Max(0, originalManaOnAttack + manaAttackBonus);
        manaOnDamageTaken = Mathf.Max(0, originalManaOnDamageTaken + manaDamageBonus);
        maxMana = Mathf.Max(20, originalMaxMana - maxManaReduction);
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);

        baseHealth = refillHealth
            ? maxHealth
            : Mathf.Clamp(Mathf.RoundToInt(maxHealth * previousHealthRatio), 1, maxHealth);

        if (healthbar != null)
        {
            healthbar.Setup(transform, MaxHealth, StarLevel, spriteRender, this);
            healthbar.UpdateBar(baseHealth);
            healthbar.UpdateManaBar(currentMana, maxMana);
        }
    }

    // ユニット自身と装備アイテムを合わせた被ダメージ軽減率を返します。
    private float GetTotalDamageReduction()
    {
        float reduction = equippedItems.Sum(item => item != null ? item.damageReductionPercent : 0f);
        reduction += synergyDamageReductionBonus;
        reduction += HasNearbyPhalanxAegisAura() ? 0.06f : 0f;
        reduction += HasActiveShield
            && SynergyManager.Instance != null
            && SynergyManager.Instance.IsSynergyActiveForTeam(SynergyType.Guardian, myTeam, 4)
                ? 0.10f
                : 0f;
        reduction -= GetActiveSpineCleaverShred();

        // === オーグメント由来の被ダメ軽減（プレイヤー側のみ） ===
        if (myTeam == Team.Team1 && GameManager.Instance != null)
        {
            // gold_low_hp_dr: HP30%以下で被ダメ軽減+15%
            if (GameManager.Instance.HasAugment("gold_low_hp_dr") && HealthRatio <= 0.30f)
                reduction += 0.15f;
            // prism_king_blessed: 盤面で最高コストの味方なら被ダメ軽減+50%
            if (GameManager.Instance.HasAugment("prism_king_blessed") && GameManager.Instance.IsHighestCostOnBoard(this))
                reduction += 0.50f;
        }

        return Mathf.Clamp(baseDamageReduction + reduction, 0f, 0.85f);
    }

    // 装備中かどうかをIDで確認します。
    private bool HasEquippedItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;

        return equippedItems.Any(item => item != null && item.id == itemId);
    }

    // 戦闘中の盤面ユニットかどうかを返します。
    private bool IsInActiveRound()
    {
        return IsOnBoard && !dead && GameManager.Instance != null && GameManager.Instance.IsRoundInProgress;
    }

    // 指定ユニットが敵チームかどうかを返します。
    private bool IsEnemyEntity(BaseEntity other)
    {
        return other != null && other.myTeam != myTeam;
    }

    // ファランクスの盾を持つ周囲味方がいれば、隊列防御オーラを受けます。同名効果は重複しません。
    private bool HasNearbyPhalanxAegisAura()
    {
        if (!IsOnBoard || GameManager.Instance == null)
            return false;

        Team opposingTeam = myTeam == Team.Team1 ? Team.Team2 : Team.Team1;
        List<BaseEntity> allies = GameManager.Instance.GetEntitiesAgainst(opposingTeam);
        for (int i = 0; i < allies.Count; i++)
        {
            BaseEntity ally = allies[i];
            if (ally == null || ally == this || ally.dead || !ally.IsOnBoard)
                continue;

            if (!ally.HasEquippedItem(PhalanxAegisItemId))
                continue;

            if (Vector3.Distance(ally.transform.position, transform.position) <= PhalanxAuraRadius)
                return true;
        }

        return false;
    }

    // 脊断ちの斧で受けている軽減低下量を返します。
    private float GetActiveSpineCleaverShred()
    {
        if (spineCleaverShredStacks <= 0)
            return 0f;

        if (Time.time > spineCleaverShredUntil)
        {
            spineCleaverShredStacks = 0;
            return 0f;
        }

        return spineCleaverShredStacks * 0.04f;
    }

    // 装備アイテムによる秘力倍率を返します。秘力はスキル量や一部効果時間を伸ばします。
    private float GetItemFocusMultiplier()
    {
        float skillPower = equippedItems.Sum(item => item != null ? item.skillPowerPercent : 0f);
        skillPower += synergyPowerBonus;
        skillPower *= Mathf.Max(0.1f, focusInfluence);
        return Mathf.Max(0.1f, 1f + skillPower);
    }

    // ★によるスキル全体強化を返します。スタンのように秘力で伸びない効果も、スターアップでは強くなります。
    private float GetStarSkillEffectMultiplier()
    {
        return StarLevel >= 3 ? 1.6f : StarLevel >= 2 ? 1.25f : 1f;
    }

    // スキル効果倍率を返します。includeFocus=falseなら秘力を無視し、スター倍率だけを使います。
    private float GetSkillEffectMultiplier(bool includeFocus)
    {
        return GetStarSkillEffectMultiplier() * (includeFocus ? GetItemFocusMultiplier() : 1f) * currentItemSkillEffectMultiplier;
    }

    // 効果時間は伸びすぎると戦闘が止まりやすいので、倍率の一部だけを時間へ反映します。
    private float GetSkillDuration(float baseDuration, bool includeFocus)
    {
        float multiplier = GetSkillEffectMultiplier(includeFocus);
        return Mathf.Max(0.1f, baseDuration * (1f + (multiplier - 1f) * 0.65f));
    }

    // 攻撃速度上昇や通常攻撃強化の「増加量」だけを強化します。
    private float GetSkillBoostAmount(float baseMultiplier)
    {
        return Mathf.Max(0f, baseMultiplier - 1f) * GetSkillEffectMultiplier(true);
    }

    // スロウは強くなりすぎると敵がほぼ止まるので、弱体量に上限を置きます。
    private float GetSkillSlowMultiplier()
    {
        float slowAmount = Mathf.Clamp((1f - skillSlowMultiplier) * GetSkillEffectMultiplier(true), 0f, 0.85f);
        return 1f - slowAmount;
    }

    // HPバーの上へ、装備中アイテムのアイコンを表示します。
    private void UpdateHealthBarItemIcons()
    {
        if (healthbar == null)
            return;

        healthbar.UpdateItemIcons(equippedItems);
    }

    // ユニット名から、デフォルトのスキル種類を自動設定します。
    private void ConfigureDefaultSkillType(string entityId)
    {
        if (!autoConfigureSkillType || string.IsNullOrEmpty(entityId))
            return;

        if (IsAreaDamageSkillUnit(entityId))
        {
            skillType = UnitSkillType.AreaDamage;
            return;
        }

        if (IsAllyHealSkillUnit(entityId))
        {
            skillType = UnitSkillType.AllyHeal;
            return;
        }

        if (IsShieldSkillUnit(entityId))
        {
            skillType = UnitSkillType.Shield;
            return;
        }

        if (IsSelfHealSkillUnit(entityId))
        {
            skillType = UnitSkillType.SelfHeal;
            return;
        }

        if (IsAttackSpeedBoostSkillUnit(entityId))
        {
            skillType = UnitSkillType.AttackSpeedBoost;
            return;
        }

        if (IsStunSkillUnit(entityId))
        {
            skillType = UnitSkillType.Stun;
            return;
        }

        if (IsSlowSkillUnit(entityId))
        {
            skillType = UnitSkillType.Slow;
            return;
        }

        if (IsDamageBoostSkillUnit(entityId))
        {
            skillType = UnitSkillType.DamageBoost;
            return;
        }

        skillType = UnitSkillType.PowerStrike;
    }

    // 自己回復スキルを持つユニット名か判定します。
    // DESIGN_skill_overhaul.md で vampire / Borealjuggernaut / Candypanda は固有スキル化されたため除外。
    private bool IsSelfHealSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Skindogehai", StringComparison.OrdinalIgnoreCase);
    }

    // 味方回復スキルを持つユニット名か判定します。
    // DESIGN_skill_overhaul.md: Christmas / City を固有化したため、現在は該当無し。
    private bool IsAllyHealSkillUnit(string entityId)
    {
        return false;
    }

    // シールド付与スキルを持つユニット名か判定します。
    // DESIGN_skill_overhaul.md: Crystal / Decepticle / Umbra / Decepticlechassis を固有化したため、現在は該当無し。
    private bool IsShieldSkillUnit(string entityId)
    {
        return false;
    }

    // 攻撃速度アップスキルを持つユニット名か判定します。
    // DESIGN_skill_overhaul.md: Antiswarm / Serpenti を固有化したため、現在は該当無し。
    private bool IsAttackSpeedBoostSkillUnit(string entityId)
    {
        return false;
    }

    // スタンスキルを持つユニット名か判定します。
    // DESIGN_skill_overhaul.md: Chaosknight / valiant を固有化したため、現在は該当無し。
    private bool IsStunSkillUnit(string entityId)
    {
        return false;
    }

    // スロウスキルを持つユニット名か判定します。
    // DESIGN_skill_overhaul.md: Spelleater を固有化したため、現在は該当無し。
    private bool IsSlowSkillUnit(string entityId)
    {
        return false;
    }

    // ダメージアップスキルを持つユニット名か判定します。
    // DESIGN_skill_overhaul.md: Cindera を固有化したため、現在は該当無し。
    private bool IsDamageBoostSkillUnit(string entityId)
    {
        return false;
    }

    // 範囲ダメージスキルを持つユニット名か判定します。
    // DESIGN_skill_overhaul.md で Andromeda は固有化されたため除外。
    private bool IsAreaDamageSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Decepticleprime", StringComparison.OrdinalIgnoreCase);
    }

    // コストと射程に応じて、基準HP・攻撃力・攻撃速度をゲーム用に整えます。
    private void ApplyBaseBalance()
    {
        if (ApplyNamedUnitBalance())
            return;

        // Zyx（コスト0の中立雑魚）専用。コスト≤1基準だと耐久(800)・火力が高すぎるため、HP600＋火力を下げる。
        // 他のステータスはコスト≤1の同分岐に揃える（挙動を変えない）。
        if (NormalizeUnitId(UnitId) == "zyx")
        {
            bool zyxRanged = originalRange >= 4;
            originalBaseHealth = 600;
            originalBaseDamage = BalanceDamage(zyxRanged ? 90 : 55);
            originalAttackSpeed = BalanceAttackSpeed(zyxRanged ? 1.15f : 1f);
            originalMovementSpeed = Mathf.Max(originalMovementSpeed, 1f);
            originalMaxMana = zyxRanged ? 105 : 95;
            originalBaseDamageReduction = zyxRanged ? 0.02f : 0.08f;
            return;
        }

        // コスト1は基準ユニットです。遠距離はHPを低く、火力をやや高くします。
        if (BaseCost <= 1)
        {
            bool ranged = originalRange >= 4;
            originalBaseHealth = ranged ? 600 : 800;
            originalBaseDamage = BalanceDamage(ranged ? 125 : 80);
            originalAttackSpeed = BalanceAttackSpeed(ranged ? 1.15f : 1f);
            originalMovementSpeed = Mathf.Max(originalMovementSpeed, 1f);
            originalMaxMana = ranged ? 105 : 95;
            originalBaseDamageReduction = ranged ? 0.02f : 0.08f;
            return;
        }

        // コスト2はコスト1より一段強いステータスにします。
        if (BaseCost == 2)
        {
            bool cost2Ranged = originalRange >= 4;
            originalBaseHealth = cost2Ranged ? 950 : 1200;
            originalBaseDamage = BalanceDamage(cost2Ranged ? 180 : 150);
            originalAttackSpeed = BalanceAttackSpeed(cost2Ranged ? 1.1f : 1f);
            originalMovementSpeed = Mathf.Max(originalMovementSpeed, 1f);
            originalMaxMana = cost2Ranged ? 105 : 100;
            originalBaseDamageReduction = cost2Ranged ? 0.04f : 0.12f;
            return;
        }

        // コスト3はさらに耐久と火力を上げます。
        if (BaseCost == 3)
        {
            bool cost3Ranged = originalRange >= 4;
            originalBaseHealth = cost3Ranged ? 1450 : 1800;
            originalBaseDamage = BalanceDamage(cost3Ranged ? 270 : 220);
            originalAttackSpeed = BalanceAttackSpeed(cost3Ranged ? 1.12f : 1f);
            originalMovementSpeed = Mathf.Max(originalMovementSpeed, 0.95f);
            originalMaxMana = cost3Ranged ? 110 : 105;
            originalBaseDamageReduction = cost3Ranged ? 0.06f : 0.16f;
            return;
        }

        bool highCostRanged = originalRange >= 4;
        originalBaseHealth = highCostRanged ? 1800 : 2200;
        originalBaseDamage = BalanceDamage(highCostRanged ? 330 : 280);
        originalAttackSpeed = BalanceAttackSpeed(highCostRanged ? 1.12f : 1f);
        originalMovementSpeed = Mathf.Max(originalMovementSpeed, 1f);
        originalMaxMana = highCostRanged ? 115 : 110;
        originalBaseDamageReduction = highCostRanged ? 0.08f : 0.18f;
    }

    // ユニット名ごとの役割・マナ・基礎ステータス・スキル傾向を設定します。
    private bool ApplyNamedUnitBalance()
    {
        string id = NormalizeUnitId(UnitId);
        ResetUniqueAttackRules();

        switch (id)
        {
            case "andromeda":
                ApplyUnitTuning(620, 50, 0.98f, 4, 1.05f, 80, UnitSkillType.PowerStrike, SkillVisualTheme.Void, 0.02f, 24, 12, 2.7f, focusOnly: true, focusSkillPower: 155, skillDamage: 2.2f);
                return true;
            case "antiswarm":
                ApplyUnitTuning(780, 50, 0.94f, 2, 1.08f, 95, UnitSkillType.AttackSpeedBoost, SkillVisualTheme.Lightning, 0.08f, 25, 15, 2.2f, focusOnly: true, focusSkillPower: 120, skillAttackSpeed: 1.55f, skillDuration: 4.4f);
                return true;
            case "borealjuggernaut":
                ApplyUnitTuning(980, 58, 0.68f, 1, 0.9f, 125, UnitSkillType.Shield, SkillVisualTheme.Ice, 0.2f, 22, 18, 0.85f, shieldPercent: 0.42f, shieldDuration: 5.2f);
                normalAttackHitsAdjacentEnemies = true;
                adjacentAttackDamageMultiplier = 0.5f;
                adjacentAttackRadius = 1.25f;
                return true;
            case "chaosknight":
                ApplyUnitTuning(850, 86, 0.82f, 1, 1.0f, 90, UnitSkillType.PowerStrike, SkillVisualTheme.Fire, 0.1f, 24, 14, 0.75f, skillDamage: 2.15f);
                return true;
            case "christmas":
                ApplyUnitTuning(880, 76, 0.84f, 1, 1.0f, 85, UnitSkillType.SelfHeal, SkillVisualTheme.Nature, 0.12f, 24, 15, 0.8f, healPercent: 0.22f);
                return true;
            case "valiant":
                ApplyUnitTuning(1080, 56, 0.68f, 1, 0.88f, 120, UnitSkillType.Stun, SkillVisualTheme.Holy, 0.23f, 22, 18, 0.75f, skillStun: 1.15f);
                return true;
            case "vampire":
                ApplyUnitTuning(560, 30, 0.78f, 4, 1.0f, 110, UnitSkillType.AreaDamage, SkillVisualTheme.Shadow, 0.02f, 28, 10, 3.0f, focusOnly: true, focusSkillPower: 135, areaDamage: 2.1f, areaRadius: 1.7f);
                return true;
            case "archdeacon":
                ApplyUnitTuning(620, 28, 0.72f, 4, 0.92f, 80, UnitSkillType.AllyHeal, SkillVisualTheme.Holy, 0.03f, 30, 12, 2.2f, focusOnly: true, focusSkillPower: 130, allyHealPercent: 0.18f, shieldPercent: 0.16f, shieldDuration: 3.8f, areaRadius: 2.2f);
                return true;
            case "backlinearcher":
                ApplyUnitTuning(590, 72, 0.95f, 4, 1.02f, 85, UnitSkillType.AreaDamage, SkillVisualTheme.Nature, 0.02f, 28, 10, 0.85f, skillDamage: 1.4f, areaDamage: 1.2f, areaRadius: 1.25f);
                return true;
            case "auroralioness":
                ApplyUnitTuning(820, 62, 0.78f, 1, 1.08f, 95, UnitSkillType.Shield, SkillVisualTheme.Holy, 0.10f, 24, 15, 1.5f, shieldPercent: 0.22f, shieldDuration: 4.4f, skillAttackSpeed: 1.22f, areaRadius: 1.85f);
                return true;
            case "azuritelion":
                ApplyUnitTuning(920, 78, 0.82f, 1, 1.18f, 90, UnitSkillType.Slow, SkillVisualTheme.Ice, 0.13f, 25, 15, 1.2f, skillDamage: 1.55f, skillSlow: 0.62f, slowDuration: 3.2f, areaRadius: 1.35f);
                return true;
            case "candypanda":
                ApplyUnitTuning(800, 44, 0.86f, 1, 1.05f, 75, UnitSkillType.AllyHeal, SkillVisualTheme.Nature, 0.08f, 28, 16, 1.45f, allyHealPercent: 0.18f, areaRadius: 2.1f);
                return true;
            case "cindera":
                ApplyUnitTuning(660, 34, 0.78f, 4, 1.0f, 115, UnitSkillType.AreaDamage, SkillVisualTheme.Fire, 0.03f, 26, 10, 2.9f, focusOnly: true, focusSkillPower: 150, areaDamage: 2.05f, areaRadius: 1.95f);
                return true;
            case "city":
                ApplyUnitTuning(740, 38, 0.74f, 4, 0.95f, 70, UnitSkillType.AttackSpeedBoost, SkillVisualTheme.Tech, 0.06f, 30, 14, 1.65f, skillAttackSpeed: 1.32f, skillDuration: 5.2f, areaRadius: 2.6f);
                return true;
            case "crystal":
                ApplyUnitTuning(860, 54, 0.82f, 2, 1.05f, 95, UnitSkillType.Shield, SkillVisualTheme.Ice, 0.12f, 25, 15, 2.35f, focusOnly: true, focusSkillPower: 120, shieldPercent: 0.28f, shieldDuration: 4.6f);
                return true;
            case "decepticle":
                ApplyUnitTuning(1220, 58, 0.66f, 1, 0.85f, 130, UnitSkillType.Slow, SkillVisualTheme.Tech, 0.24f, 22, 18, 0.85f, skillSlow: 0.55f, slowDuration: 4.6f);
                return true;
            case "sandpanther":
                ApplyUnitTuning(980, 132, 0.92f, 1, 1.35f, 75, UnitSkillType.DamageBoost, SkillVisualTheme.Shadow, 0.06f, 28, 12, 0.8f, skillDamage: 1.8f, skillDamageBoost: 1.35f, skillDuration: 2.5f);
                return true;
            case "protector":
                ApplyUnitTuning(1450, 60, 0.65f, 1, 0.85f, 125, UnitSkillType.Shield, SkillVisualTheme.Holy, 0.26f, 22, 18, 1.0f, shieldPercent: 0.35f, shieldDuration: 4.8f, areaRadius: 2f);
                return true;
            case "taskmaster":
                ApplyUnitTuning(1050, 86, 0.82f, 2, 1.0f, 95, UnitSkillType.Stun, SkillVisualTheme.Shadow, 0.10f, 25, 14, 0.9f, skillDamage: 1.35f, skillStun: 0.95f, skillDuration: 3f, areaRadius: 2.2f);
                return true;
            case "serpenti":
                ApplyUnitTuning(920, 94, 0.82f, 1, 1.08f, 90, UnitSkillType.PowerStrike, SkillVisualTheme.Nature, 0.1f, 24, 14, 0.75f, skillDamage: 2.05f);
                return true;
            case "spelleater":
                ApplyUnitTuning(860, 48, 0.86f, 2, 1.05f, 100, UnitSkillType.Slow, SkillVisualTheme.Void, 0.09f, 25, 15, 2.35f, focusOnly: true, focusSkillPower: 125, skillSlow: 0.52f, slowDuration: 4.2f);
                return true;
            case "umbra":
                ApplyUnitTuning(940, 98, 0.76f, 1, 1.03f, 85, UnitSkillType.DamageBoost, SkillVisualTheme.Shadow, 0.11f, 24, 14, 0.75f, skillDamageBoost: 1.48f, skillDuration: 4.2f);
                return true;
            case "decepticlechassis":
                ApplyUnitTuning(1580, 72, 0.62f, 1, 0.82f, 145, UnitSkillType.Shield, SkillVisualTheme.Tech, 0.3f, 21, 20, 0.8f, shieldPercent: 0.46f, shieldDuration: 5.8f);
                return true;
            case "decepticleprime":
                ApplyUnitTuning(880, 112, 0.84f, 4, 0.98f, 90, UnitSkillType.PowerStrike, SkillVisualTheme.Tech, 0.07f, 25, 12, 0.9f, skillDamage: 2.55f);
                return true;
            case "shadowlord":
                ApplyUnitTuning(780, 130, 0.92f, 1, 1.3f, 80, UnitSkillType.PowerStrike, SkillVisualTheme.Shadow, 0.06f, 28, 12, 0.75f, skillDamage: 2.35f);
                return true;
            case "skindogehai":
                ApplyUnitTuning(760, 122, 0.96f, 1, 1.32f, 75, UnitSkillType.Stun, SkillVisualTheme.Fire, 0.05f, 29, 12, 0.75f, skillStun: 0.95f);
                return true;
            case "tier2general":
                ApplyUnitTuning(1080, 104, 0.84f, 1, 1.05f, 95, UnitSkillType.PowerStrike, SkillVisualTheme.Ice, 0.15f, 24, 15, 0.8f, skillDamage: 2.15f);
                return true;
            case "wolfpunch":
                ApplyUnitTuning(1420, 78, 0.7f, 1, 0.95f, 135, UnitSkillType.SelfHeal, SkillVisualTheme.Nature, 0.27f, 22, 19, 0.85f, healPercent: 0.18f);
                return true;
            case "kane":
                ApplyUnitTuning(1280, 92, 0.78f, 4, 0.9f, 100, UnitSkillType.AreaDamage, SkillVisualTheme.Lightning, 0.10f, 27, 13, 2.4f, focusOnly: true, focusSkillPower: 190, areaDamage: 1.45f, areaRadius: 2.25f);
                return true;
            case "malyk":
                ApplyUnitTuning(980, 58, 0.74f, 4, 0.94f, 115, UnitSkillType.AreaDamage, SkillVisualTheme.Void, 0.06f, 28, 12, 2.8f, focusOnly: true, focusSkillPower: 210, areaDamage: 1.9f, areaRadius: 2.05f);
                return true;
            case "paragon":
                ApplyUnitTuning(2100, 86, 0.68f, 1, 0.82f, 130, UnitSkillType.Shield, SkillVisualTheme.Holy, 0.28f, 22, 18, 1.1f, shieldPercent: 0.34f, shieldDuration: 5f, areaRadius: 2.35f);
                return true;
            case "maehvmk":
                ApplyUnitTuning(1160, 70, 0.84f, 4, 1.0f, 105, UnitSkillType.AreaDamage, SkillVisualTheme.Lightning, 0.14f, 25, 15, 2.55f, focusOnly: true, focusSkillPower: 175, areaDamage: 1.85f, areaRadius: 2.15f);
                return true;
            case "snowchasermk":
                ApplyUnitTuning(980, 52, 0.74f, 4, 0.98f, 80, UnitSkillType.AllyHeal, SkillVisualTheme.Ice, 0.08f, 29, 14, 1.7f, focusOnly: true, focusSkillPower: 165, allyHealPercent: 0.145f, shieldPercent: 0.16f, areaRadius: 2.7f);
                return true;
            case "solfist":
                ApplyUnitTuning(1500, 96, 0.72f, 1, 0.95f, 120, UnitSkillType.AreaDamage, SkillVisualTheme.Fire, 0.22f, 23, 18, 1f, areaDamage: 1.7f, areaRadius: 2.15f);
                return true;
            case "altgeneraltier2":
                ApplyUnitTuning(720, 42, 0.78f, 4, 1.02f, 90, UnitSkillType.AreaDamage, SkillVisualTheme.Fire, 0.06f, 27, 12, 2.5f, focusOnly: true, focusSkillPower: 135, skillSlow: 0.58f, slowDuration: 3f, areaDamage: 1.75f, areaRadius: 1.75f);
                return true;
            case "ilenamk2":
                ApplyUnitTuning(1500, 70, 0.76f, 4, 1.02f, 105, UnitSkillType.AreaDamage, SkillVisualTheme.Ice, 0.08f, 28, 14, 2.6f, focusOnly: true, focusSkillPower: 190, areaDamage: 1.85f, areaRadius: 2.45f);
                return true;
            case "embergeneral":
                ApplyUnitTuning(4300, 128, 0.76f, 1, 1.02f, 120, UnitSkillType.AttackSpeedBoost, SkillVisualTheme.Holy, 0.2f, 24, 18, 1.2f, skillAttackSpeed: 1.2f, skillDuration: 5f, areaRadius: 2.4f);
                return true;
            case "kron":
                ApplyUnitTuning(4500, 72, 0.6f, 4, 0.85f, 125, UnitSkillType.AllyHeal, SkillVisualTheme.Holy, 0.16f, 24, 18, 1.45f, focusOnly: true, focusSkillPower: 240, allyHealPercent: 0.2f, areaRadius: 3.0f);
                return true;
            case "invader":
                ApplyUnitTuning(3600, 92, 0.64f, 4, 1.0f, 115, UnitSkillType.AreaDamage, SkillVisualTheme.Lightning, 0.08f, 26, 14, 2.8f, focusOnly: true, focusSkillPower: 260, areaDamage: 2.1f, areaRadius: 2.8f);
                return true;
            case "gol":
                ApplyUnitTuning(5000, 82, 0.58f, 4, 0.82f, 135, UnitSkillType.AreaDamage, SkillVisualTheme.Void, 0.24f, 22, 20, 2.6f, focusOnly: true, focusSkillPower: 250, areaDamage: 2.0f, areaRadius: 2.4f);
                return true;
            case "legion":
                ApplyUnitTuning(4000, 108, 0.7f, 1, 1.0f, 130, UnitSkillType.AreaDamage, SkillVisualTheme.Shadow, 0.18f, 24, 18, 1.25f, focusOnly: true, focusSkillPower: 220, areaDamage: 1.7f, areaRadius: 2.2f);
                return true;
            case "plaguegeneral":
                ApplyUnitTuning(4600, 116, 0.72f, 1, 0.98f, 115, UnitSkillType.Slow, SkillVisualTheme.Shadow, 0.23f, 24, 18, 1.25f, skillSlow: 0.62f, slowDuration: 2f, areaRadius: 2.2f);
                return true;
            case "skyfalltyrant":
                ApplyUnitTuning(4200, 150, 0.64f, 1, 0.9f, 125, UnitSkillType.AreaDamage, SkillVisualTheme.Fire, 0.16f, 22, 18, 1.15f, areaDamage: 2.2f, areaRadius: 2.2f);
                return true;
            default:
                return false;
        }
    }

    // 1ユニット分のチューニング値を、スター計算の基準値へ流し込みます。
    private void ApplyUnitTuning(
        int health,
        int damage,
        float attacksPerSecond,
        int unitRange,
        float moveSpeed,
        int manaMax,
        UnitSkillType configuredSkillType,
        SkillVisualTheme visualTheme,
        float damageReduction = 0f,
        int attackMana = 24,
        int damageTakenMana = 14,
        float focusInfluenceValue = 1f,
        bool focusOnly = false,
        int focusSkillPower = 120,
        float skillDamage = 1.8f,
        float healPercent = 0.22f,
        float shieldPercent = 0.32f,
        float shieldDuration = 4f,
        float allyHealPercent = 0.2f,
        float skillAttackSpeed = 1.45f,
        float skillDamageBoost = 1.5f,
        float skillDuration = 4f,
        float skillStun = 1.1f,
        float skillSlow = 0.58f,
        float slowDuration = 4f,
        float areaRadius = 1.65f,
        float areaDamage = 1.35f)
    {
        originalBaseHealth = Mathf.Max(1, health);
        originalBaseDamage = BalanceDamage(damage);
        originalAttackSpeed = BalanceAttackSpeed(attacksPerSecond);
        originalRange = Mathf.Clamp(unitRange, 1, 5);
        originalMovementSpeed = Mathf.Max(0.05f, moveSpeed);
        originalMaxMana = Mathf.Max(20, manaMax);
        originalManaOnAttack = Mathf.Max(0, attackMana);
        originalManaOnDamageTaken = Mathf.Max(0, damageTakenMana);
        originalBaseDamageReduction = Mathf.Clamp(damageReduction, 0f, 0.75f);

        skillType = configuredSkillType;
        skillVisualTheme = visualTheme;
        focusInfluence = Mathf.Max(0.1f, focusInfluenceValue);
        skillUsesFocusOnly = focusOnly;
        skillBasePower = Mathf.Max(1, focusSkillPower);
        skillDamageMultiplier = skillDamage;
        skillHealPercent = healPercent;
        skillFlatHeal = 0;
        skillShieldPercent = shieldPercent;
        skillFlatShield = 0;
        skillShieldDuration = shieldDuration;
        skillAllyHealPercent = allyHealPercent;
        skillFlatAllyHeal = 0;
        skillAttackSpeedBoostMultiplier = skillAttackSpeed;
        skillDamageBoostMultiplier = skillDamageBoost;
        skillBuffDuration = skillDuration;
        skillStunDuration = skillStun;
        skillSlowMultiplier = Mathf.Clamp(skillSlow, 0.1f, 1f);
        skillSlowDuration = slowDuration;
        skillAreaRadius = areaRadius;
        skillAreaDamageMultiplier = areaDamage;
    }

    // 個別ユニットの特殊通常攻撃を初期化します。
    private void ResetUniqueAttackRules()
    {
        normalAttackHitsAdjacentEnemies = false;
        adjacentAttackDamageMultiplier = 0.55f;
        adjacentAttackRadius = 1.2f;
        skillUsesFocusOnly = false;
        skillBasePower = 120;
    }

    // CloneやStar表記を外した、小文字のユニットIDを返します。
    private static string NormalizeUnitId(string rawName)
    {
        return LocalizationManager.CleanUnitName(rawName).ToLowerInvariant();
    }

    // 全体火力調整倍率をかけた攻撃力を返します。
    private int BalanceDamage(int value)
    {
        return Mathf.Max(1, Mathf.RoundToInt(value * GlobalDamageMultiplier));
    }

    // 全体攻撃速度調整倍率をかけた攻撃速度を返します。
    private float BalanceAttackSpeed(float value)
    {
        return Mathf.Max(0.05f, value * GlobalAttackSpeedMultiplier);
    }

    // スターごとのHP/攻撃力倍率を返します。
    private float GetStarDamageHealthMultiplier(int starLevel)
    {
        if (BaseCost >= 2)
        {
            if (starLevel >= 3)
                return 3.2f;

            if (starLevel == 2)
                return 2.2f;

            return 1f;
        }

        if (starLevel >= 3)
            return 4.2f;

        if (starLevel == 2)
            return 2.2f;

        return 1f;
    }

    // スターごとの攻撃速度倍率を返します。
    private float GetStarAttackSpeedMultiplier(int starLevel)
    {
        if (starLevel >= 3)
            return 1.25f;

        if (starLevel == 2)
            return 1.15f;

        return 1f;
    }

    // スターごとの移動速度倍率を返します。
    private float GetStarMovementSpeedMultiplier(int starLevel)
    {
        if (starLevel >= 3)
            return 1.1f;

        if (starLevel == 2)
            return 1.05f;

        return 1f;
    }

    // スターアップ時に、防御面も少しだけ強くします。軽減率の主役はユニット個性とアイテムです。
    private float GetStarDamageReductionBonus(int starLevel)
    {
        if (starLevel >= 3)
            return 0.06f;

        if (starLevel == 2)
            return 0.03f;

        return 0f;
    }

    // スターごとの見た目サイズ倍率を返します。
    private float GetStarScaleMultiplier(int starLevel)
    {
        if (starLevel >= 3)
            return 1.55f;

        if (starLevel == 2)
            return 1.28f;

        return 1f;
    }

    // スターアップで見た目を大きくしつつ、足元の位置だけは固定します。
    private void ApplyStarVisualScale()
    {
        EnsureComponentReferences();
        if (visualRoot == null || spriteRender == null || spriteRender.sprite == null)
            return;

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localScale = Vector3.one;

        // 拡大後のSprite下端が足元位置に合うよう、Visualだけ上下に動かします。
        visualRoot.localScale = Vector3.one * GetStarScaleMultiplier(StarLevel);
        AlignVisualRootFootToGround();
    }

    private void PlayStarUpgradeScaleTween()
    {
        EnsureComponentReferences();
        if (!Application.isPlaying || visualRoot == null || spriteRender == null || spriteRender.sprite == null)
            return;

        starScaleTween?.Kill();

        Vector3 targetScale = Vector3.one * GetStarScaleMultiplier(StarLevel);
        visualRoot.localScale = targetScale * 0.74f;
        AlignVisualRootFootToGround();

        starScaleTween = visualRoot
            .DOScale(targetScale, 0.34f)
            .SetEase(Ease.OutBack)
            .SetTarget(visualRoot)
            .OnUpdate(AlignVisualRootFootToGround)
            .OnComplete(() =>
            {
                AlignVisualRootFootToGround();
                starScaleTween = null;
            });
    }

    private void AlignVisualRootFootToGround()
    {
        if (visualRoot == null || spriteRender == null || spriteRender.sprite == null)
            return;

        float targetFootY = transform.position.y + FootLocalY;

        float scaledFootY = spriteRender.bounds.min.y;
        Vector3 visualPosition = visualRoot.position;
        visualPosition.y += targetFootY - scaledFootY;
        visualRoot.position = visualPosition;
    }

    // アウトラインShaderが隣のスプライトを拾わないよう、現在SpriteのUV範囲を渡します。
    private void UpdateSpriteUvBounds(bool force = false)
    {
        if (spriteRender == null || spriteRender.sprite == null)
            return;

        Sprite sprite = spriteRender.sprite;
        if (!force && lastSpriteForOutlineBounds == sprite)
            return;

        Texture2D texture = sprite.texture;
        if (texture == null)
            return;

        Rect rect = sprite.textureRect;
        Vector4 uvMinMax = new Vector4(
            rect.xMin / texture.width,
            rect.yMin / texture.height,
            rect.xMax / texture.width,
            rect.yMax / texture.height);

        if (spritePropertyBlock == null)
            spritePropertyBlock = new MaterialPropertyBlock();

        spriteRender.GetPropertyBlock(spritePropertyBlock);
        spritePropertyBlock.SetVector(SpriteUvMinMaxId, uvMinMax);
        spriteRender.SetPropertyBlock(spritePropertyBlock);
        lastSpriteForOutlineBounds = sprite;
    }

    // スターアップ時にユニット上部へ「STAR 2/3」表示を出します。
    private void ShowStarUpgradePopup(int starLevel)
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (starPopupCoroutine != null)
        {
            StopCoroutine(starPopupCoroutine);
            starPopupCoroutine = null;
        }

        if (starPopupObject != null)
            Destroy(starPopupObject);

        starPopupObject = new GameObject("StarUpgradePopup");
        starPopupObject.transform.position = GetStarPopupWorldPosition();
        starPopupObject.transform.localScale = Vector3.one * 0.18f;

        TextMeshPro popupText = starPopupObject.AddComponent<TextMeshPro>();
        LocalizationManager.ApplyFont(popupText);
        popupText.text = LocalizationManager.FormatUpgradeLabel(starLevel);
        popupText.alignment = TextAlignmentOptions.Center;
        popupText.enableWordWrapping = false;
        popupText.fontSize = 9.5f;
        popupText.fontStyle = FontStyles.Bold;
        popupText.outlineWidth = 0.18f;
        popupText.outlineColor = Color.black;
        popupText.color = starLevel >= 3
            ? new Color(1f, 0.45f, 1f, 1f)
            : new Color(1f, 0.92f, 0.12f, 1f);
        popupText.sortingOrder = GetVisualSortingOrder() + 80;

        StarUpgradePopupFader fader = starPopupObject.AddComponent<StarUpgradePopupFader>();
        fader.Begin(popupText, starPopupObject.transform, 0.18f, 0.34f, 1.45f);
    }

    // スターアップ文字を表示するワールド座標を計算します。
    private Vector3 GetStarPopupWorldPosition()
    {
        if (spriteRender != null && spriteRender.sprite != null)
        {
            Bounds bounds = spriteRender.bounds;
            return new Vector3(bounds.center.x, bounds.max.y + 0.35f, transform.position.z - 0.1f);
        }

        return transform.position + new Vector3(0f, 0.95f, -0.1f);
    }

    // 現在位置に応じたユニットの描画順を返します。
    private int GetVisualSortingOrder()
    {
        return CalculateSortingOrder(transform.position);
    }

    // Y座標が低いほど前、同じYならX座標でも少し差をつける描画順計算です。
    public static int CalculateSortingOrder(Vector3 position, int offset = 0)
    {
        int yOrder = Mathf.RoundToInt(-position.y * 20f) * 40;
        int xOrder = Mathf.RoundToInt((position.x + 20f) * 3f);
        return VisualSortingBaseOrder + yOrder + xOrder + offset;
    }

    // ユニット本体の描画順を毎フレーム更新します。ドラッグ中はさらに前面に出します。
    private void UpdateVisualSortingOrder()
    {
        if (spriteRender == null)
            return;

        int orderOffset = 0;
        Draggable draggable = GetComponent<Draggable>();
        if (draggable != null && draggable.IsDragging)
            orderOffset = 900;

        spriteRender.sortingOrder = CalculateSortingOrder(transform.position, orderOffset);
    }

    // 古いスターアップ文字のフェード処理です。現在は専用Faderクラスを主に使います。
    private IEnumerator FadeStarUpgradePopup(TextMeshPro popupText, Transform popupTransform)
    {
        const float duration = 1.45f;
        Vector3 startPosition = popupTransform.position;
        Vector3 endPosition = startPosition + new Vector3(0f, 0.55f, 0f);
        Color startColor = popupText.color;

        float elapsed = 0f;
        while (elapsed < duration && popupText != null && popupTransform != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 2f);
            popupTransform.position = Vector3.Lerp(startPosition, endPosition, eased);
            popupTransform.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.26f, eased);
            popupText.color = new Color(startColor.r, startColor.g, startColor.b, 1f - progress);
            yield return null;
        }

        if (starPopupObject != null)
            Destroy(starPopupObject);

        starPopupObject = null;
        starPopupCoroutine = null;
    }

    // Inspectorでアウトラインマテリアルが未設定でも、実行時に簡易マテリアルを作ります。
    private static Material GetFallbackOutlineMaterial(Team team)
    {
        Shader shader = Shader.Find("AutoChess/SpriteOutline");
        if (shader == null)
            return null;

        if (team == Team.Team2)
        {
            if (fallbackTeam2OutlineMaterial == null)
                fallbackTeam2OutlineMaterial = CreateRuntimeOutlineMaterial(shader, new Color(1f, 0.1f, 0.08f, 1f));

            return fallbackTeam2OutlineMaterial;
        }

        if (fallbackTeam1OutlineMaterial == null)
            fallbackTeam1OutlineMaterial = CreateRuntimeOutlineMaterial(shader, new Color(0.15f, 1f, 0.2f, 1f));

        return fallbackTeam1OutlineMaterial;
    }

    // 指定色のアウトラインマテリアルを実行時に生成します。
    private static Material CreateRuntimeOutlineMaterial(Shader shader, Color outlineColor)
    {
        Material material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        material.SetColor("_OutlineColor", outlineColor);
        material.SetFloat("_OutlineSize", 1.5f);
        material.SetFloat("_AlphaThreshold", 0.05f);
        return material;
    }

    // ユニット名から基本射程を返します。ここに入っているユニットは射程4です。
    private static int GetConfiguredBaseRange(string unitId)
    {
        // アルカナ（最終ボス）は超遠距離キャリー。盤面最大の射程5。
        if (string.Equals(unitId, "Arcana", StringComparison.OrdinalIgnoreCase))
            return 5;

        if (string.Equals(unitId, "Andromeda", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Antiswarm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "vampire", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Archdeacon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Backlinearcher", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Altgeneraltier2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Crystal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Cindera", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Spelleater", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Decepticleprime", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Kane", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Malyk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Ilenamk2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Wraith", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Snowchasermk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Maehvmk", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 1;
    }
}

// スターアップ文字を上へ動かしながらフェードアウトして消す補助クラスです。
internal sealed class StarUpgradePopupFader : MonoBehaviour
{
    private Sequence sequence;

    // 外部からフェード開始を指示する入口です。
    public void Begin(TextMeshPro popupText, Transform popupTransform, float startScale, float endScale, float duration)
    {
        if (popupText == null || popupTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        sequence?.Kill();
        popupTransform.DOKill();
        popupText.DOKill();

        duration = Mathf.Max(0.1f, duration);
        Vector3 startPosition = popupTransform.position;
        Vector3 endPosition = startPosition + new Vector3(0f, 0.62f, 0f);
        Color startColor = popupText.color;
        popupTransform.position = startPosition;
        popupTransform.localScale = Vector3.one * startScale;
        popupText.color = startColor;
        popupText.alpha = 1f;

        sequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(popupTransform.DOMove(endPosition, duration).SetEase(Ease.OutQuad))
            .Join(popupTransform.DOScale(Vector3.one * endScale, duration).SetEase(Ease.OutBack))
            .Join(popupText.DOFade(0f, duration).SetEase(Ease.InQuad))
            .OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }
}

// ダメージ数字を少し上へ動かしながらフェードアウトさせる補助クラスです。
internal sealed class DamageNumberFader : MonoBehaviour
{
    private Sequence sequence;

    public void Begin(TextMeshPro damageText, Transform damageTransform, float duration)
    {
        if (damageText == null || damageTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        sequence?.Kill();
        damageTransform.DOKill();
        damageText.DOKill();

        duration = Mathf.Max(0.1f, duration);
        Vector3 startPosition = damageTransform.position;
        Vector3 endPosition = startPosition + new Vector3(0f, 0.86f, 0f);
        damageTransform.localScale = Vector3.one * 0.78f;
        damageText.alpha = 1f;

        sequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(damageTransform.DOMove(endPosition, duration).SetEase(Ease.OutCubic))
            .Join(damageTransform.DOScale(Vector3.one * 1.18f, duration).SetEase(Ease.OutBack))
            .Join(damageText.DOFade(0f, duration).SetEase(Ease.InQuad))
            .OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }
}
