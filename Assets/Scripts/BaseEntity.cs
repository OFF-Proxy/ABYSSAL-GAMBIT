using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

public class BaseEntity : MonoBehaviour
{

    public HealthBar barPrefab;
    public SpriteRenderer spriteRender;
    public Animator animator;
    public Material team1OutlineMaterial;
    public Material team2OutlineMaterial;

    public int baseDamage = 1;
    public int baseHealth = 3;
    [Range(1, 5)]
    public int range = 1;
    public float attackSpeed = 1f; //Attacks per second
    public float movementSpeed = 1f; //Attacks per second
    public int maxMana = 100;
    public int manaOnAttack = 25;
    public int manaOnDamageTaken = 15;
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
    [Range(0.05f, 0.95f)]
    public float attackImpactNormalizedTime = 0.55f;
    [Range(0.05f, 0.95f)]
    public float skillImpactNormalizedTime = 0.55f;

    protected Team myTeam;
    protected BaseEntity currentTarget = null;
    protected Node currentNode;

    public Node CurrentNode => currentNode;

    protected bool HasEnemy => currentTarget != null;
    protected bool IsInRange => IsTargetInRange(currentTarget);
    protected bool moving;
    protected Node destination;
    protected HealthBar healthbar;

    protected bool dead = false;
    protected bool canAttack = true;
    protected float waitBetweenAttack;
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
    protected bool CanAct => IsOnBoard && GameManager.Instance != null && GameManager.Instance.IsRoundInProgress && !dead && !stunned;

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
    private Vector3 originalScale;
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
    private static Material fallbackTeam1OutlineMaterial;
    private static Material fallbackTeam2OutlineMaterial;
    private const string VisualRootName = "Visual";
    private const float FootLocalY = -0.42f;
    private const float GlobalDamageMultiplier = 0.88f;
    private const float GlobalAttackSpeedMultiplier = 0.76f;
    private const int VisualSortingBaseOrder = 10000;
    private static readonly int SpriteUvMinMaxId = Shader.PropertyToID("_SpriteUVMinMax");
    private Transform visualRoot;
    private MaterialPropertyBlock spritePropertyBlock;
    private Sprite lastSpriteForOutlineBounds;

    protected void Awake()
    {
        EnsureComponentReferences();
        ConfigureAnimatorForRuntime();
        RestoreAnimatorControllerIfMissing();
        CaptureBaseStats();
    }

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

    public void ApplyStarLevel(int starLevel)
    {
        CaptureBaseStats();

        int previousStarLevel = StarLevel;
        StarLevel = Mathf.Clamp(starLevel, 1, 3);
        float damageHealthMultiplier = GetStarDamageHealthMultiplier(StarLevel);

        baseDamage = Mathf.Max(1, Mathf.RoundToInt(originalBaseDamage * damageHealthMultiplier));
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(originalBaseHealth * damageHealthMultiplier));
        baseHealth = maxHealth;
        range = Mathf.Max(1, originalRange);
        attackSpeed = originalAttackSpeed * GetStarAttackSpeedMultiplier(StarLevel);
        movementSpeed = originalMovementSpeed * GetStarMovementSpeedMultiplier(StarLevel);
        transform.localScale = originalScale;
        ApplyStarVisualScale();
        gameObject.name = $"{UnitId} Star{StarLevel}";

        if (healthbar != null)
            healthbar.Setup(transform, MaxHealth, StarLevel, spriteRender, this);

        if (StarLevel > previousStarLevel && StarLevel > 1)
            ShowStarUpgradePopup(StarLevel);
    }

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

        this.currentNode = currentNode;
        transform.position = currentNode.worldPosition;
        currentNode.SetOccupied(true);

        EnsureHealthBar();
        if (healthbar != null)
        {
            healthbar.gameObject.SetActive(true);
            healthbar.Setup(transform, MaxHealth, StarLevel, spriteRender, this);
        }
        ApplyStarVisualScale();
    }

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

    protected void Start()
    {
        GameManager.Instance.OnRoundStart += OnRoundStart;
        GameManager.Instance.OnRoundEnd += OnRoundEnd;
        GameManager.Instance.OnUnitDied += OnUnitDied;
    }

    private void LateUpdate()
    {
        UpdateSpriteUvBounds();
        UpdateVisualSortingOrder();
    }

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

    protected virtual void OnRoundStart() { }
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

    protected virtual void OnUnitDied(BaseEntity diedUnity)
    {
        if (currentTarget == diedUnity)
            SetTarget(null);
    }

    protected void FindTarget()
    {
        if (GameManager.Instance == null)
            return;

        SetTarget(GetNearestEnemy());
    }

    protected void RefreshTargetForCombat()
    {
        if (currentTarget != null && IsInRange)
        {
            ClearMovementReservation();
            return;
        }

        SetTarget(GetNearestEnemy());
    }

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

    protected void GetInRange()
    {
        if (currentTarget == null)
            return;

        if(!moving)
        {
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

    public void SetCurrentNode(Node node)
    {
        currentNode = node;
    }

    public void TakeDamage(int amount)
    {
        if (dead)
            return;

        int remainingDamage = Mathf.Max(0, amount);
        if (shieldHealth > 0)
        {
            int blockedDamage = Mathf.Min(shieldHealth, remainingDamage);
            shieldHealth -= blockedDamage;
            remainingDamage -= blockedDamage;

            if (healthbar != null)
                healthbar.UpdateShieldBar(shieldHealth, MaxHealth);
        }

        baseHealth -= remainingDamage;
        GainMana(manaOnDamageTaken);
        if (healthbar != null)
            healthbar.UpdateBar(baseHealth);

        if(baseHealth <= 0 && !dead)
        {
            Die();
        }
    }

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

    protected virtual void Attack()
    {
        if (!canAttack || dead)
            return;

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackSequenceCoroutine(currentTarget, false));
    }

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

    private bool CanCastSkill()
    {
        return maxMana > 0 && currentMana >= maxMana;
    }

    private void CastSkill()
    {
        if (currentTarget == null)
            return;

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackSequenceCoroutine(currentTarget, true));
    }

    private IEnumerator AttackSequenceCoroutine(BaseEntity targetAtCast, bool skillAttack)
    {
        canAttack = false;
        ClearMovementReservation();
        FaceTarget(targetAtCast);

        if (skillAttack)
            ResetMana();

        waitBetweenAttack = GetAttackDuration();
        bool useAbilityAnimation = skillAttack && HasAnimationClip("Ability") && HasAnimatorParameter("ability", AnimatorControllerParameterType.Trigger);
        string clipName = useAbilityAnimation ? "Ability" : "Attack";
        float animationLength = GetAnimationClipLength(clipName);
        float actionDuration = Mathf.Max(0.05f, waitBetweenAttack);
        float playbackSpeed = animationLength > 0f ? Mathf.Clamp(animationLength / actionDuration, 0.2f, 8f) : 1f;

        BeginActionAnimation(useAbilityAnimation, playbackSpeed);

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

    private void ExecuteNormalAttack(BaseEntity targetAtCast)
    {
        if (dead || targetAtCast == null || targetAtCast.dead)
            return;

        AttackEffectPlayer.PlayAttack(this, targetAtCast, range > 1);
        targetAtCast.TakeDamage(GetCurrentDamage());
        GainMana(manaOnAttack);
    }

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
                ApplyShield(GetSkillShieldAmount(), skillShieldDuration);
                break;
            case UnitSkillType.AttackSpeedBoost:
                AttackEffectPlayer.PlaySkill(this, this, skillType);
                ApplyAttackSpeedBoost(skillAttackSpeedBoostMultiplier, skillBuffDuration);
                break;
            case UnitSkillType.Stun:
                if (targetAtCast != null && !targetAtCast.dead)
                {
                    AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
                    targetAtCast.ApplyStun(skillStunDuration);
                }
                break;
            case UnitSkillType.Slow:
                if (targetAtCast != null && !targetAtCast.dead)
                {
                    AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
                    targetAtCast.ApplyAttackSpeedSlow(skillSlowMultiplier, skillSlowDuration);
                }
                break;
            case UnitSkillType.DamageBoost:
                AttackEffectPlayer.PlaySkill(this, this, skillType);
                ApplyDamageBoost(skillDamageBoostMultiplier, skillBuffDuration);
                break;
            case UnitSkillType.AreaDamage:
                ApplyAreaDamage(targetAtCast);
                break;
            default:
                if (targetAtCast != null && !targetAtCast.dead)
                {
                    int skillDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * skillDamageMultiplier));
                    AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
                    targetAtCast.TakeDamage(skillDamage);
                }
                break;
        }
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private bool CanUseAnimator()
    {
        EnsureComponentReferences();
        ConfigureAnimatorForRuntime();
        return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameter, value);
    }

    private void SetAnimatorTrigger(string parameter)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(parameter);
    }

    private void ResetAnimatorTrigger(string parameter)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(parameter);
    }

    private void ResetActionAnimatorState()
    {
        StopAttackAnimation();
        SetAnimatorBool("walking", false);
        ResetAnimatorTrigger("dead");
    }

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

    private void ConfigureAnimatorForRuntime()
    {
        if (animator == null)
            return;

        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

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

    private string GetAnimatorControllerUnitName()
    {
        string controllerUnitName = UnitId.Replace("(Clone)", string.Empty).Trim();
        int starIndex = controllerUnitName.IndexOf(" Star", StringComparison.OrdinalIgnoreCase);
        if (starIndex >= 0)
            controllerUnitName = controllerUnitName.Substring(0, starIndex).Trim();

        return controllerUnitName;
    }

    private void RestartAnimatorPlayback()
    {
        if (!CanUseAnimator())
            return;

        animator.Rebind();
        animator.Update(0f);
    }

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

    private void ResetMana()
    {
        currentMana = 0;
        if (healthbar != null)
            healthbar.UpdateManaBar(currentMana, maxMana);
    }

    private void GainMana(int amount)
    {
        if (maxMana <= 0 || amount <= 0 || dead)
            return;

        currentMana = Mathf.Clamp(currentMana + amount, 0, maxMana);
        if (healthbar != null)
            healthbar.UpdateManaBar(currentMana, maxMana);
    }

    private float GetAttackDuration()
    {
        return 1f / Mathf.Max(0.05f, GetEffectiveAttackSpeed());
    }

    private float GetEffectiveAttackSpeed()
    {
        if (stunned)
            return 0.05f;

        return Mathf.Max(0.05f, attackSpeed * attackSpeedBuffMultiplier * attackSpeedDebuffMultiplier);
    }

    private int GetCurrentDamage()
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageBuffMultiplier));
    }

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

    private void EndActionAnimation()
    {
        SetAnimatorBool("attacking", false);
        ResetAnimatorTrigger("attack");
        SetAnimatorBool("abilitying", false);
        ResetAnimatorTrigger("ability");
        SetAnimatorPlaybackSpeed(1f);
    }

    private void SetAnimatorPlaybackSpeed(float speed)
    {
        if (!CanUseAnimator())
            return;

        animator.speed = speed <= 0f ? 0f : Mathf.Clamp(speed, 0.05f, 8f);
    }

    private int GetSkillHealAmount()
    {
        return Mathf.Max(1, skillFlatHeal + Mathf.RoundToInt(MaxHealth * Mathf.Max(0f, skillHealPercent)));
    }

    private int GetSkillAllyHealAmount()
    {
        return Mathf.Max(1, skillFlatAllyHeal + Mathf.RoundToInt(MaxHealth * Mathf.Max(0f, skillAllyHealPercent)));
    }

    private int GetSkillShieldAmount()
    {
        return Mathf.Max(1, skillFlatShield + Mathf.RoundToInt(MaxHealth * Mathf.Max(0f, skillShieldPercent)));
    }

    private void HealSelf(int amount)
    {
        if (amount <= 0 || dead)
            return;

        baseHealth = Mathf.Min(MaxHealth, baseHealth + amount);
        if (healthbar != null)
            healthbar.UpdateBar(baseHealth);
    }

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

    private void ApplyAttackSpeedBoost(float multiplier, float duration)
    {
        attackSpeedBuffMultiplier = Mathf.Max(1f, multiplier);

        if (attackSpeedBoostCoroutine != null)
            StopCoroutine(attackSpeedBoostCoroutine);

        attackSpeedBoostCoroutine = StartCoroutine(ResetAttackSpeedBoostAfterDelay(Mathf.Max(0.1f, duration)));
    }

    private IEnumerator ResetAttackSpeedBoostAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        attackSpeedBoostCoroutine = null;
        attackSpeedBuffMultiplier = 1f;
    }

    private void ApplyDamageBoost(float multiplier, float duration)
    {
        damageBuffMultiplier = Mathf.Max(1f, multiplier);

        if (damageBoostCoroutine != null)
            StopCoroutine(damageBoostCoroutine);

        damageBoostCoroutine = StartCoroutine(ResetDamageBoostAfterDelay(Mathf.Max(0.1f, duration)));
    }

    private IEnumerator ResetDamageBoostAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        damageBoostCoroutine = null;
        damageBuffMultiplier = 1f;
    }

    public void ApplyAttackSpeedSlow(float multiplier, float duration)
    {
        if (dead)
            return;

        attackSpeedDebuffMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);

        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);

        slowCoroutine = StartCoroutine(ResetAttackSpeedSlowAfterDelay(Mathf.Max(0.1f, duration)));
    }

    private IEnumerator ResetAttackSpeedSlowAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        slowCoroutine = null;
        attackSpeedDebuffMultiplier = 1f;
    }

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

    private IEnumerator ClearStunAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        stunCoroutine = null;
        stunned = false;
        SetAnimatorPlaybackSpeed(1f);
        canAttack = true;
    }

    private void ApplyAreaDamage(BaseEntity targetAtCast)
    {
        if (targetAtCast == null || GameManager.Instance == null)
            return;

        AttackEffectPlayer.PlaySkill(this, targetAtCast, skillType);
        List<BaseEntity> enemies = GameManager.Instance.GetEntitiesAgainst(myTeam);
        int areaDamage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentDamage() * skillAreaDamageMultiplier));

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

    private IEnumerator ShieldDurationCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        shieldCoroutine = null;
        shieldHealth = 0;
        if (healthbar != null)
            healthbar.UpdateShieldBar(0, MaxHealth);
    }

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

    private void SetTarget(BaseEntity target)
    {
        if (currentTarget == target)
            return;

        currentTarget = target;
        FaceTarget(currentTarget);
        ClearMovementReservation();
    }

    private void ClearMovementReservation()
    {
        if (destination != null && destination != currentNode && moving)
            destination.SetOccupied(false);

        destination = null;
        moving = false;
        SetAnimatorBool("walking", false);
    }

    private bool IsTargetInRange(BaseEntity target)
    {
        return target != null && Vector3.Distance(transform.position, target.transform.position) <= range + 0.05f;
    }

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

    private bool HasAnimationClip(string clipName)
    {
        return GetAnimationClipLength(clipName) > 0f;
    }

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

    private void FaceTarget(BaseEntity target)
    {
        if (target == null)
            return;

        FaceDirection(target.transform.position.x - transform.position.x);
    }

    private void FaceDirection(float directionX)
    {
        if (spriteRender == null || Mathf.Abs(directionX) < 0.02f)
            return;

        spriteRender.flipX = directionX < 0f;
    }

    private void EnsureHealthBar()
    {
        if (healthbar != null || barPrefab == null)
            return;

        healthbar = Instantiate(barPrefab);
        healthbar.Setup(this.transform, MaxHealth, StarLevel, spriteRender, this);
    }

    private void CaptureBaseStats()
    {
        if (baseStatsCaptured)
            return;

        originalBaseDamage = baseDamage;
        originalBaseHealth = baseHealth;
        originalRange = range;
        originalAttackSpeed = attackSpeed;
        originalMovementSpeed = movementSpeed;
        originalScale = transform.localScale;
        baseStatsCaptured = true;
    }

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

    private bool IsSelfHealSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "vampire", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Borealjuggernaut", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Candypanda", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Skindogehai", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAllyHealSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Christmas", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "City", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsShieldSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Crystal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Decepticle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Umbra", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Decepticlechassis", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAttackSpeedBoostSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Antiswarm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Serpenti", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsStunSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Chaosknight", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "valiant", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSlowSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Spelleater", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDamageBoostSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Cindera", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAreaDamageSkillUnit(string entityId)
    {
        return
            string.Equals(entityId, "Andromeda", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityId, "Decepticleprime", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyBaseBalance()
    {
        if (BaseCost <= 1)
        {
            bool ranged = originalRange >= 4;
            originalBaseHealth = ranged ? 600 : 800;
            originalBaseDamage = BalanceDamage(ranged ? 125 : 80);
            originalAttackSpeed = BalanceAttackSpeed(ranged ? 1.15f : 1f);
            originalMovementSpeed = Mathf.Max(originalMovementSpeed, 1f);
            return;
        }

        if (BaseCost == 2)
        {
            bool cost2Ranged = originalRange >= 4;
            originalBaseHealth = cost2Ranged ? 950 : 1200;
            originalBaseDamage = BalanceDamage(cost2Ranged ? 180 : 150);
            originalAttackSpeed = BalanceAttackSpeed(cost2Ranged ? 1.1f : 1f);
            originalMovementSpeed = Mathf.Max(originalMovementSpeed, 1f);
            return;
        }

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

    private int BalanceDamage(int value)
    {
        return Mathf.Max(1, Mathf.RoundToInt(value * GlobalDamageMultiplier));
    }

    private float BalanceAttackSpeed(float value)
    {
        return Mathf.Max(0.05f, value * GlobalAttackSpeedMultiplier);
    }

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

    private float GetStarAttackSpeedMultiplier(int starLevel)
    {
        if (starLevel >= 3)
            return 1.25f;

        if (starLevel == 2)
            return 1.15f;

        return 1f;
    }

    private float GetStarMovementSpeedMultiplier(int starLevel)
    {
        if (starLevel >= 3)
            return 1.1f;

        if (starLevel == 2)
            return 1.05f;

        return 1f;
    }

    private float GetStarScaleMultiplier(int starLevel)
    {
        if (starLevel >= 3)
            return 1.55f;

        if (starLevel == 2)
            return 1.28f;

        return 1f;
    }

    private void ApplyStarVisualScale()
    {
        EnsureComponentReferences();
        if (visualRoot == null || spriteRender == null || spriteRender.sprite == null)
            return;

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localScale = Vector3.one;

        float targetFootY = transform.position.y + FootLocalY;
        visualRoot.localScale = Vector3.one * GetStarScaleMultiplier(StarLevel);

        float scaledFootY = spriteRender.bounds.min.y;
        Vector3 visualPosition = visualRoot.position;
        visualPosition.y += targetFootY - scaledFootY;
        visualRoot.position = visualPosition;
    }

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
        popupText.text = $"STAR {starLevel}";
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

    private Vector3 GetStarPopupWorldPosition()
    {
        if (spriteRender != null && spriteRender.sprite != null)
        {
            Bounds bounds = spriteRender.bounds;
            return new Vector3(bounds.center.x, bounds.max.y + 0.35f, transform.position.z - 0.1f);
        }

        return transform.position + new Vector3(0f, 0.95f, -0.1f);
    }

    private int GetVisualSortingOrder()
    {
        return CalculateSortingOrder(transform.position);
    }

    public static int CalculateSortingOrder(Vector3 position, int offset = 0)
    {
        int yOrder = Mathf.RoundToInt(-position.y * 20f) * 40;
        int xOrder = Mathf.RoundToInt((position.x + 20f) * 3f);
        return VisualSortingBaseOrder + yOrder + xOrder + offset;
    }

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

internal sealed class StarUpgradePopupFader : MonoBehaviour
{
    public void Begin(TextMeshPro popupText, Transform popupTransform, float startScale, float endScale, float duration)
    {
        StartCoroutine(FadeAndDestroy(popupText, popupTransform, startScale, endScale, Mathf.Max(0.1f, duration)));
    }

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
