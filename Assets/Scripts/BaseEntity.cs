using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
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
    [Range(1, 5)]
    public int range = 1;
    public float attackSpeed = 1f; //Attacks per second
    public float movementSpeed = 1f; //Attacks per second

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
    protected bool moving;
    protected Node destination;
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
    public bool IsDead => dead;
    public float HealthRatio => MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)baseHealth / MaxHealth);
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
    private int originalMaxMana;
    private int originalManaOnAttack;
    private int originalManaOnDamageTaken;
    private Vector3 originalScale;
    private readonly List<ItemData> equippedItems = new List<ItemData>();
    private Coroutine attackCoroutine;
    private Coroutine shieldCoroutine;
    private Coroutine attackSpeedBoostCoroutine;
    private Coroutine damageBoostCoroutine;
    private Coroutine slowCoroutine;
    private Coroutine stunCoroutine;
    private Coroutine starPopupCoroutine;
    private GameObject starPopupObject;
    private bool deathDestroyStarted;
    private bool stunned;
    private float attackSpeedBuffMultiplier = 1f;
    private float damageBuffMultiplier = 1f;
    private float attackSpeedDebuffMultiplier = 1f;

    // アウトライン用マテリアルと、見た目の親オブジェクト設定です。
    private static Material fallbackTeam1OutlineMaterial;
    private static Material fallbackTeam2OutlineMaterial;
    private const string VisualRootName = "Visual";
    private const float FootLocalY = -0.42f;

    // 全体バランス調整用の倍率です。ここを下げると全ユニットの火力や攻撃速度が落ちます。
    private const float GlobalDamageMultiplier = 0.88f;
    private const float GlobalAttackSpeedMultiplier = 0.76f;
    private const int MaxEquippedItems = 3;

    // Y座標とX座標から描画順を決めるための基準値です。
    private const int VisualSortingBaseOrder = 10000;
    private static readonly int SpriteUvMinMaxId = Shader.PropertyToID("_SpriteUVMinMax");
    private Transform visualRoot;
    private MaterialPropertyBlock spritePropertyBlock;
    private Sprite lastSpriteForOutlineBounds;

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

    // 盤面に配置された時のセットアップです。
    public void Setup(Team team, Node currentNode)
    {
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
    }

    // ラウンド開始時の処理です。派生クラス側で動き方を実装します。
    protected virtual void OnRoundStart() { }
    // ラウンド終了時に、戦闘中だけの状態を元に戻します。
    protected virtual void OnRoundEnd()
    {
        SetTarget(null);
        ClearMovementReservation();
        StopAttackAnimation();
        ClearTemporaryStatusEffects();
        ClearShield();
        ResetMana();
        canAttack = true;
        SetAnimatorBool("walking", false);
    }

    // 誰かが死んだ時、今狙っていた相手ならターゲットを外します。
    protected virtual void OnUnitDied(BaseEntity diedUnity)
    {
        if (currentTarget == diedUnity)
            SetTarget(null);
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
        if (currentTarget != null && IsInRange)
        {
            ClearMovementReservation();
            return;
        }

        SetTarget(GetNearestEnemy());
    }

    // 自分から一番近い、盤面上の敵ユニットを探します。
    private BaseEntity GetNearestEnemy()
    {
        var allEnemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        float minDistance = Mathf.Infinity;
        BaseEntity entity = null;
        foreach (BaseEntity e in allEnemies)
        {
            if (e == null || !e.IsOnBoard)
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

        this.transform.position += direction.normalized * movementSpeed * Time.deltaTime;
        return false;
    }

    // ターゲットが射程内に入る位置まで移動するための次の1マスを決めます。
    protected void GetInRange()
    {
        if (currentTarget == null)
            return;

        if(!moving)
        {
            // ターゲット周囲で、空いていて自分から近いNodeを候補にします。
            destination = null;
            List<Node> candidates = GridManager.Instance.GetNodesInRange(currentTarget.CurrentNode, range)
                .Where(node => node != currentTarget.CurrentNode && !node.IsOccupied)
                .OrderBy(node => Vector3.Distance(node.worldPosition, this.transform.position))
                .ToList();

            for(int i = 0; i < candidates.Count;i++)
            {
                var path = GridManager.Instance.GetPath(currentNode, candidates[i]);
                if (path == null || path.Count <= 1)
                    continue;

                if (path[1].IsOccupied)
                    continue;

                // 次に移動するマスを予約して、他ユニットが同じマスへ向かわないようにします。
                path[1].SetOccupied(true);
                destination = path[1];
                break;
            }
        }

        if (destination == null)
            return;

        moving = !MoveTowards(destination);
        if(!moving)
        {
            //Free previous node
            currentNode.SetOccupied(false);
            SetCurrentNode(destination);
        }
    }

    // 現在いるNodeを更新します。移動完了時に呼びます。
    public void SetCurrentNode(Node node)
    {
        currentNode = node;
    }

    // ダメージを受けた時の処理です。シールド、HP、マナ、死亡判定をまとめて行います。
    public void TakeDamage(int amount)
    {
        if (dead)
            return;

        // まずシールドで受け止め、残った分だけHPを減らします。
        int remainingDamage = Mathf.Max(0, amount);
        if (shieldHealth > 0)
        {
            int blockedDamage = Mathf.Min(shieldHealth, remainingDamage);
            shieldHealth -= blockedDamage;
            remainingDamage -= blockedDamage;

            if (healthbar != null)
                healthbar.UpdateShieldBar(shieldHealth, MaxHealth);
        }

        if (remainingDamage > 0)
            remainingDamage = Mathf.Max(1, Mathf.RoundToInt(remainingDamage * (1f - GetItemDamageReduction())));

        baseHealth -= remainingDamage;
        GainMana(manaOnDamageTaken);
        if (healthbar != null)
            healthbar.UpdateBar(baseHealth);

        if(baseHealth <= 0 && !dead)
        {
            Die();
        }
    }

    // 死亡アニメーションを待ってからGameObjectを破棄します。
    public void DestroyAfterDeathAnimation()
    {
        if (deathDestroyStarted)
            return;

        deathDestroyStarted = true;

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

        StartCoroutine(DestroyAfterDelay(deathAnimationLength));
    }

    // 味方ユニット用です。死亡アニメーション後に破棄せず非表示にして、次ウェーブで復活できるようにします。
    public void WaitForWaveReviveAfterDeathAnimation()
    {
        if (deathDestroyStarted)
            return;

        deathDestroyStarted = true;

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

        StartCoroutine(DeactivateAfterDelay(deathAnimationLength));
    }

    // ウェーブ終了後、指定Nodeに戻してHP満タンで戦闘前状態へ復帰します。
    public void RestoreForNextWave(Team team, Node restoreNode)
    {
        if (restoreNode == null)
            return;

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
        dead = true;
        AttackEffectPlayer.PlayDeath(this);
        SetTarget(null);
        ClearMovementReservation();
        StopAttackAnimation();
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

    // 通常攻撃のダメージ、エフェクト、マナ獲得を処理します。
    private void ExecuteNormalAttack(BaseEntity targetAtCast)
    {
        if (dead || targetAtCast == null || targetAtCast.dead)
            return;

        AttackEffectPlayer.PlayAttack(this, targetAtCast, range > 1);
        targetAtCast.TakeDamage(GetCurrentDamage());
        GainMana(manaOnAttack);
    }

    // スキル種類に応じて、回復・シールド・バフ・デバフ・範囲攻撃などを実行します。
    private void ExecuteSkillEffect(BaseEntity targetAtCast)
    {
        if (dead)
            return;

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
                ApplyDamageBoost(1f + GetSkillBoostAmount(skillDamageBoostMultiplier), GetSkillDuration(skillBuffDuration, true));
                break;
            case UnitSkillType.AreaDamage:
                ApplyAreaDamage(targetAtCast);
                break;
            default:
                if (targetAtCast != null && !targetAtCast.dead)
                {
                    int skillDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * skillDamageMultiplier * GetSkillEffectMultiplier(true)));
                    AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
                    targetAtCast.TakeDamage(skillDamage);
                }
                break;
        }
    }

    // 指定秒数待ってからユニットを破棄します。
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    // 指定秒数待ってから、復活待ちユニットを一時的に非表示にします。
    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }

    // Animatorが安全に使える状態か確認します。
    private bool CanUseAnimator()
    {
        EnsureComponentReferences();
        ConfigureAnimatorForRuntime();
        return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
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

        return Mathf.Max(0.05f, attackSpeed * attackSpeedBuffMultiplier * attackSpeedDebuffMultiplier);
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
        return Mathf.Max(1, Mathf.RoundToInt((skillFlatHeal + MaxHealth * Mathf.Max(0f, skillHealPercent)) * GetSkillEffectMultiplier(true)));
    }

    // 味方回復スキルの回復量を計算します。
    private int GetSkillAllyHealAmount()
    {
        return Mathf.Max(1, Mathf.RoundToInt((skillFlatAllyHeal + MaxHealth * Mathf.Max(0f, skillAllyHealPercent)) * GetSkillEffectMultiplier(true)));
    }

    // シールドスキルで付与するシールド量を計算します。
    private int GetSkillShieldAmount()
    {
        return Mathf.Max(1, Mathf.RoundToInt((skillFlatShield + MaxHealth * Mathf.Max(0f, skillShieldPercent)) * GetSkillEffectMultiplier(true)));
    }

    // 自分のHPを回復します。最大HPは超えません。
    private void HealSelf(int amount)
    {
        if (amount <= 0 || dead)
            return;

        baseHealth = Mathf.Min(MaxHealth, baseHealth + amount);
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
        return ally;
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

        shieldHealth = Mathf.Max(shieldHealth, amount);
        if (healthbar != null)
            healthbar.UpdateShieldBar(shieldHealth, MaxHealth);

        if (shieldCoroutine != null)
            StopCoroutine(shieldCoroutine);

        shieldCoroutine = StartCoroutine(ShieldDurationCoroutine(Mathf.Max(0.1f, duration)));
    }

    // 一定時間、自分の攻撃速度を上げます。
    private void ApplyAttackSpeedBoost(float multiplier, float duration)
    {
        attackSpeedBuffMultiplier = Mathf.Max(1f, multiplier);

        if (attackSpeedBoostCoroutine != null)
            StopCoroutine(attackSpeedBoostCoroutine);

        attackSpeedBoostCoroutine = StartCoroutine(ResetAttackSpeedBoostAfterDelay(Mathf.Max(0.1f, duration)));
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
        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        int areaDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * skillAreaDamageMultiplier * GetSkillEffectMultiplier(true)));

        for (int i = 0; i < enemies.Count; i++)
        {
            BaseEntity enemy = enemies[i];
            if (enemy == null || enemy.dead || !enemy.IsOnBoard)
                continue;

            if (Vector3.Distance(enemy.transform.position, targetAtCast.transform.position) > skillAreaRadius)
                continue;

            enemy.TakeDamage(areaDamage);
        }
    }

    // シールドの効果時間が終わったらシールド量を0にします。
    private IEnumerator ShieldDurationCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        shieldCoroutine = null;
        shieldHealth = 0;
        if (healthbar != null)
            healthbar.UpdateShieldBar(0, MaxHealth);
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

        attackSpeedBuffMultiplier = 1f;
        damageBuffMultiplier = 1f;
        attackSpeedDebuffMultiplier = 1f;
        stunned = false;
        SetAnimatorPlaybackSpeed(1f);
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
        SetAnimatorBool("walking", false);
    }

    // 指定ターゲットが現在の射程内にいるかを距離で判定します。
    private bool IsTargetInRange(BaseEntity target)
    {
        return target != null && Vector3.Distance(transform.position, target.transform.position) <= range + 0.05f;
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
        originalMaxMana = maxMana;
        originalManaOnAttack = manaOnAttack;
        originalManaOnDamageTaken = manaOnDamageTaken;
        originalScale = transform.localScale;
        baseStatsCaptured = true;
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

        baseDamage = Mathf.Max(1, Mathf.RoundToInt((originalBaseDamage * starDamageHealthMultiplier + itemDamageFlat) * itemDamageMultiplier));
        maxHealth = Mathf.Max(1, Mathf.RoundToInt((originalBaseHealth * starDamageHealthMultiplier + itemHealthFlat) * itemHealthMultiplier));
        range = Mathf.Max(1, originalRange);
        attackSpeed = originalAttackSpeed * GetStarAttackSpeedMultiplier(StarLevel) * itemAttackSpeedMultiplier;
        movementSpeed = originalMovementSpeed * GetStarMovementSpeedMultiplier(StarLevel);
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

    // 装備アイテムによる被ダメージ軽減率を返します。
    private float GetItemDamageReduction()
    {
        float reduction = equippedItems.Sum(item => item != null ? item.damageReductionPercent : 0f);
        return Mathf.Clamp(reduction, 0f, 0.7f);
    }

    // 装備アイテムによる秘力倍率を返します。秘力はスキル量や一部効果時間を伸ばします。
    private float GetItemFocusMultiplier()
    {
        float skillPower = equippedItems.Sum(item => item != null ? item.skillPowerPercent : 0f);
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
        return GetStarSkillEffectMultiplier() * (includeFocus ? GetItemFocusMultiplier() : 1f);
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
    private bool IsSelfHealSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "vampire", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Borealjuggernaut", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Candypanda", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Skindogehai", StringComparison.OrdinalIgnoreCase);
    }

    // 味方回復スキルを持つユニット名か判定します。
    private bool IsAllyHealSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Christmas", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "City", StringComparison.OrdinalIgnoreCase);
    }

    // シールド付与スキルを持つユニット名か判定します。
    private bool IsShieldSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Crystal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Decepticle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Umbra", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Decepticlechassis", StringComparison.OrdinalIgnoreCase);
    }

    // 攻撃速度アップスキルを持つユニット名か判定します。
    private bool IsAttackSpeedBoostSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Antiswarm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Serpenti", StringComparison.OrdinalIgnoreCase);
    }

    // スタンスキルを持つユニット名か判定します。
    private bool IsStunSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Chaosknight", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "valiant", StringComparison.OrdinalIgnoreCase);
    }

    // スロウスキルを持つユニット名か判定します。
    private bool IsSlowSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Spelleater", StringComparison.OrdinalIgnoreCase);
    }

    // ダメージアップスキルを持つユニット名か判定します。
    private bool IsDamageBoostSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Cindera", StringComparison.OrdinalIgnoreCase);
    }

    // 範囲ダメージスキルを持つユニット名か判定します。
    private bool IsAreaDamageSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Andromeda", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Decepticleprime", StringComparison.OrdinalIgnoreCase);
    }

    // コストと射程に応じて、基準HP・攻撃力・攻撃速度をゲーム用に整えます。
    private void ApplyBaseBalance()
    {
        // コスト1は基準ユニットです。遠距離はHPを低く、火力をやや高くします。
        if (BaseCost <= 1)
        {
            bool ranged = originalRange >= 4;
            originalBaseHealth = ranged ? 600 : 800;
            originalBaseDamage = BalanceDamage(ranged ? 125 : 80);
            originalAttackSpeed = BalanceAttackSpeed(ranged ? 1.15f : 1f);
            originalMovementSpeed = Mathf.Max(originalMovementSpeed, 1f);
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
            return;
        }

        bool highCostRanged = originalRange >= 4;
        originalBaseHealth = highCostRanged ? 1800 : 2200;
        originalBaseDamage = BalanceDamage(highCostRanged ? 330 : 280);
        originalAttackSpeed = BalanceAttackSpeed(highCostRanged ? 1.12f : 1f);
        originalMovementSpeed = Mathf.Max(originalMovementSpeed, 1f);
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
        float targetFootY = transform.position.y + FootLocalY;
        visualRoot.localScale = Vector3.one * GetStarScaleMultiplier(StarLevel);

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
        if (string.Equals(unitId, "Andromeda", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Antiswarm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "vampire", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Crystal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Cindera", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Spelleater", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitId, "Decepticleprime", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 1;
    }
}

// スターアップ文字を上へ動かしながらフェードアウトして消す補助クラスです。
internal sealed class StarUpgradePopupFader : MonoBehaviour
{
    // 外部からフェード開始を指示する入口です。
    public void Begin(TextMeshPro popupText, Transform popupTransform, float startScale, float endScale, float duration)
    {
        StartCoroutine(FadeAndDestroy(popupText, popupTransform, startScale, endScale, Mathf.Max(0.1f, duration)));
    }

    // 指定時間かけて文字を拡大・移動・透明化し、最後に自分自身を破棄します。
    private IEnumerator FadeAndDestroy(TextMeshPro popupText, Transform popupTransform, float startScale, float endScale, float duration)
    {
        if (popupText == null || popupTransform == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 startPosition = popupTransform.position;
        Vector3 endPosition = startPosition + new Vector3(0f, 0.62f, 0f);
        Color startColor = popupText.color;

        float elapsed = 0f;
        while (elapsed < duration && popupText != null && popupTransform != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 2f);
            float alpha = 1f - progress;

            popupTransform.position = Vector3.Lerp(startPosition, endPosition, eased);
            popupTransform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            popupText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            popupText.alpha = alpha;
            yield return null;
        }

        Destroy(gameObject);
    }
}
