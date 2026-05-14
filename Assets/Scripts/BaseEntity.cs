using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using TMPro;
using UnityEngine;

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
    protected bool CanAct => IsOnBoard && GameManager.Instance != null && GameManager.Instance.IsRoundInProgress && !dead;

    private string unitId;
    private bool baseStatsCaptured;
    private int originalBaseDamage;
    private int originalBaseHealth;
    private int originalRange;
    private float originalAttackSpeed;
    private float originalMovementSpeed;
    private Vector3 originalScale;
    private Coroutine attackCoroutine;
    private Coroutine starPopupCoroutine;
    private GameObject starPopupObject;
    private bool deathDestroyStarted;
    private static Material fallbackTeam1OutlineMaterial;
    private static Material fallbackTeam2OutlineMaterial;

    public void InitializeIdentity(string unitId, int baseCost = 1, int starLevel = 1)
    {
        CaptureBaseStats();
        this.unitId = unitId;
        BaseCost = Mathf.Max(1, baseCost);
        originalRange = GetConfiguredBaseRange(unitId);
        range = originalRange;
        ApplyStarLevel(starLevel);
    }

    public void ApplyStarLevel(int starLevel)
    {
        CaptureBaseStats();

        int previousStarLevel = StarLevel;
        StarLevel = Mathf.Clamp(starLevel, 1, 3);
        float statMultiplier = GetStarStatMultiplier(StarLevel);

        baseDamage = Mathf.Max(1, Mathf.RoundToInt(originalBaseDamage * statMultiplier));
        baseHealth = Mathf.Max(1, Mathf.RoundToInt(originalBaseHealth * statMultiplier));
        range = Mathf.Max(1, originalRange);
        attackSpeed = originalAttackSpeed * statMultiplier;
        movementSpeed = originalMovementSpeed * statMultiplier;
        transform.localScale = originalScale * GetStarScaleMultiplier(StarLevel);
        gameObject.name = $"{UnitId} Star{StarLevel}";

        if (healthbar != null)
            healthbar.Setup(transform, baseHealth);

        if (StarLevel > previousStarLevel && StarLevel > 1)
            ShowStarUpgradePopup(StarLevel);
    }

    public void Setup(Team team, Node currentNode)
    {
        CaptureBaseStats();
        myTeam = team;
        ApplyTeamVisuals();
        SetTarget(null);
        ClearMovementReservation();
        canAttack = true;
        ResetActionAnimatorState();

        this.currentNode = currentNode;
        transform.position = currentNode.worldPosition;
        currentNode.SetOccupied(true);

        EnsureHealthBar();
        if (healthbar != null)
            healthbar.gameObject.SetActive(true);
    }

    public void SetupOnBench(Team team, Vector3 benchPosition)
    {
        CaptureBaseStats();
        myTeam = team;
        ApplyTeamVisuals();
        currentNode = null;
        SetTarget(null);
        ClearMovementReservation();
        canAttack = true;
        ResetActionAnimatorState();
        transform.position = benchPosition;

        if (healthbar != null)
            healthbar.gameObject.SetActive(false);
    }

    protected void Start()
    {
        GameManager.Instance.OnRoundStart += OnRoundStart;
        GameManager.Instance.OnRoundEnd += OnRoundEnd;
        GameManager.Instance.OnUnitDied += OnUnitDied;
    }

    protected void OnDestroy()
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.OnRoundStart -= OnRoundStart;
        GameManager.Instance.OnRoundEnd -= OnRoundEnd;
        GameManager.Instance.OnUnitDied -= OnUnitDied;
    }

    protected virtual void OnRoundStart() { }
    protected virtual void OnRoundEnd()
    {
        SetTarget(null);
        ClearMovementReservation();
        StopAttackAnimation();
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

        baseHealth -= amount;
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

        waitBetweenAttack = 1f / Mathf.Max(0.01f, attackSpeed);
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(WaitCoroutine());
    }

    IEnumerator WaitCoroutine()
    {
        canAttack = false;
        SetAnimatorBool("attacking", true);
        SetAnimatorTrigger("attack");
        yield return new WaitForSeconds(waitBetweenAttack);
        SetAnimatorBool("attacking", false);
        attackCoroutine = null;

        if (!dead)
            canAttack = true;
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private bool CanUseAnimator()
    {
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

    private void StopAttackAnimation()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        SetAnimatorBool("attacking", false);
        ResetAnimatorTrigger("attack");
    }

    private void SetTarget(BaseEntity target)
    {
        if (currentTarget == target)
            return;

        currentTarget = target;
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
    }

    private void EnsureHealthBar()
    {
        if (healthbar != null || barPrefab == null)
            return;

        healthbar = Instantiate(barPrefab, this.transform);
        healthbar.Setup(this.transform, baseHealth);
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

    private float GetStarStatMultiplier(int starLevel)
    {
        if (starLevel >= 3)
            return 1.3f;

        if (starLevel == 2)
            return 1.1f;

        return 1f;
    }

    private float GetStarScaleMultiplier(int starLevel)
    {
        if (starLevel >= 3)
            return 1.5f;

        if (starLevel == 2)
            return 1.25f;

        return 1f;
    }

    private void ShowStarUpgradePopup(int starLevel)
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (starPopupCoroutine != null)
            StopCoroutine(starPopupCoroutine);

        if (starPopupObject != null)
            Destroy(starPopupObject);

        starPopupObject = new GameObject("StarUpgradePopup");
        starPopupObject.transform.SetParent(transform, false);
        starPopupObject.transform.localPosition = new Vector3(0f, 0.75f, -0.1f);
        starPopupObject.transform.localScale = Vector3.one * 0.12f;

        TextMeshPro popupText = starPopupObject.AddComponent<TextMeshPro>();
        popupText.text = $"★{starLevel}";
        popupText.alignment = TextAlignmentOptions.Center;
        popupText.enableWordWrapping = false;
        popupText.fontSize = 5.5f;
        popupText.fontStyle = FontStyles.Bold;
        popupText.color = starLevel >= 3
            ? new Color(1f, 0.45f, 1f, 1f)
            : new Color(1f, 0.92f, 0.12f, 1f);
        popupText.sortingOrder = spriteRender != null ? spriteRender.sortingOrder + 10 : 20;

        starPopupCoroutine = StartCoroutine(FadeStarUpgradePopup(popupText, starPopupObject.transform));
    }

    private IEnumerator FadeStarUpgradePopup(TextMeshPro popupText, Transform popupTransform)
    {
        const float duration = 1.25f;
        Vector3 startPosition = popupTransform.localPosition;
        Vector3 endPosition = startPosition + new Vector3(0f, 0.35f, 0f);
        Color startColor = popupText.color;

        float elapsed = 0f;
        while (elapsed < duration && popupText != null && popupTransform != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 2f);
            popupTransform.localPosition = Vector3.Lerp(startPosition, endPosition, eased);
            popupTransform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.16f, eased);
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
            string.Equals(unitId, "Cindera", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 1;
    }
}
