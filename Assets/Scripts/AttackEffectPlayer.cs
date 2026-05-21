using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// 攻撃・スキル・死亡・UI操作の見た目と音を再生する共通プレイヤーです。
// シーンに置かれていなくても、必要になったら自動でGameObjectを作って動きます。
public class AttackEffectPlayer : MonoBehaviour
{
    // スプライトエフェクトを何fpsで再生するかです。
    private const float FrameRate = 18f;

    // Resources配下に置かれている各エフェクト画像のパスです。
    private const string ProjectileResourcePath = "AttackEffects/Generated/fx_f1_casterprojectile";
    private const string ImpactResourcePath = "AttackEffects/Generated/fx_explosionblueelectrical";
    private const string MeleeResourcePath = "AttackEffects/Generated/fx_crossslash";
    private const string HealResourcePath = "AttackEffects/Generated/fx_heal";
    private const string ShieldResourcePath = "AttackEffects/Generated/fx_distortion_hex_shield";
    private const string BuffResourcePath = "AttackEffects/Generated/fx_buff";
    private const string StunResourcePath = "AttackEffects/Generated/fx_f6_bbs_stun";
    private const string SlowResourcePath = "AttackEffects/Generated/fx_frozen";
    private const string PowerResourcePath = "AttackEffects/Generated/fx_slashfrenzy";
    private const string AreaResourcePath = "AttackEffects/Generated/fx_whiteexplosion";
    private const string DamageBoostResourcePath = "AttackEffects/Generated/fx_ringswirl";

    // BGM候補です。上から順にResources.Loadで探し、最初に見つかったものを使います。
    private static readonly string[] BgmCandidates =
    {
        "music/music_battlemap01",
        "music/music_battlemap02",
        "music/music_battlemap_abyssian",
        "music/music_playmode"
    };

    // 音声を何度もResources.Loadしないようキャッシュします。
    private static readonly Dictionary<string, AudioClip> generatedAudioClips = new Dictionary<string, AudioClip>();
    private static readonly Dictionary<string, AudioClip> loadedAudioClips = new Dictionary<string, AudioClip>();

    // このクラスの実体です。staticメソッドからでも音やエフェクトを鳴らすために使います。
    private static AttackEffectPlayer instance;

    // 各エフェクトのスプライト配列です。初回再生時にまとめて読み込みます。
    private static Sprite[] projectileSprites;
    private static Sprite[] impactSprites;
    private static Sprite[] meleeSprites;
    private static Sprite[] healSprites;
    private static Sprite[] shieldSprites;
    private static Sprite[] buffSprites;
    private static Sprite[] stunSprites;
    private static Sprite[] slowSprites;
    private static Sprite[] powerSprites;
    private static Sprite[] areaSprites;
    private static Sprite[] damageBoostSprites;

    // SE用とBGM用のAudioSourceです。BGMはループ再生します。
    private AudioSource sfxSource;
    private AudioSource bgmSource;

    // シーン読み込み後、自動でこのクラスを用意しBGMを鳴らします。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAudio()
    {
        EnsureInstance();
        instance.PlayBgmIfNeeded();
    }

    // 通常攻撃のエフェクトとSEを再生します。
    public static void PlayAttack(BaseEntity attacker, BaseEntity target, bool rangedAttack)
    {
        if (attacker == null || target == null)
            return;

        EnsureInstance();
        EnsureSpritesLoaded();

        // 発射位置と着弾位置を、ユニット中心より少し上にずらします。
        Vector3 source = attacker.transform.position + new Vector3(0f, 0.12f, 0f);
        Vector3 destination = target.transform.position + new Vector3(0f, 0.18f, 0f);
        Color teamColor = GetTeamEffectColor(attacker);

        instance.PlaySfx("normal_attack", 0.42f);
        if (rangedAttack)
            instance.StartCoroutine(instance.PlayRangedAttack(source, destination, teamColor));
        else
            instance.StartCoroutine(instance.PlayMeleeAttack(source, destination, teamColor));
    }

    // スキルの種類に合ったエフェクトとSEを再生します。
    public static void PlaySkill(BaseEntity caster, BaseEntity target, UnitSkillType skillType)
    {
        if (caster == null)
            return;

        EnsureInstance();
        EnsureSpritesLoaded();

        // 自己強化系は自分、攻撃系は対象の位置にエフェクトを出します。
        BaseEntity effectTarget = GetSkillEffectTarget(caster, target, skillType);
        Vector3 position = effectTarget.transform.position + new Vector3(0f, 0.2f, 0f);
        Color color = GetSkillEffectColor(caster, skillType);
        Sprite[] sprites = GetSkillSprites(caster, skillType);
        string resourcePath = GetSkillResourcePath(caster, skillType);
        float scale = GetSkillEffectScale(caster, skillType);

        instance.PlaySfx(GetSkillSfxName(caster, skillType), GetSkillSfxVolume(skillType));
        instance.StartCoroutine(instance.PlayAnimatedSprite(resourcePath, sprites, position, scale, color, 0f, GetEffectSortingOrder(position, 70)));
    }

    // シールドが残っている間、ユニットを覆う追従エフェクトを作ります。
    public static GameObject AttachShieldAura(BaseEntity owner)
    {
        if (owner == null)
            return null;

        EnsureInstance();
        EnsureSpritesLoaded();

        Sprite[] auraSprites = GetShieldAuraSprites();
        if (auraSprites.Length == 0)
            return null;

        GameObject auraObject = new GameObject("ActiveShieldAura");
        SpriteRenderer renderer = auraObject.AddComponent<SpriteRenderer>();
        renderer.sprite = auraSprites[0];
        renderer.color = GetPersistentShieldColor(owner);

        if (owner.spriteRender != null)
            renderer.sortingLayerID = owner.spriteRender.sortingLayerID;

        ShieldAuraVisual auraVisual = auraObject.AddComponent<ShieldAuraVisual>();
        auraVisual.Begin(owner, renderer, auraSprites);
        return auraObject;
    }

    // 死亡時のSEを鳴らします。
    public static void PlayDeath(BaseEntity entity)
    {
        if (entity == null)
            return;

        EnsureInstance();
        instance.PlaySfx("death", 0.58f);
    }

    // ショップやドラッグなど、UI操作SEを鳴らすための入口です。
    public static void PlayUiSfx(string cueName)
    {
        if (string.IsNullOrEmpty(cueName))
            return;

        EnsureInstance();
        instance.PlaySfx(cueName, GetUiSfxVolume(cueName));
    }

    // スキルエフェクトを誰の位置に出すか決めます。
    private static BaseEntity GetSkillEffectTarget(BaseEntity caster, BaseEntity target, UnitSkillType skillType)
    {
        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.Shield:
            case UnitSkillType.AttackSpeedBoost:
            case UnitSkillType.DamageBoost:
                return caster;
            default:
                return target != null ? target : caster;
        }
    }

    // AttackEffectPlayerの実体が無ければ作ります。
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AttackEffectPlayer");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AttackEffectPlayer>();
        instance.EnsureAudioSources();
    }

    // Resourcesから各エフェクトのスプライトを読み込みます。
    private static void EnsureSpritesLoaded()
    {
        projectileSprites ??= LoadSprites(ProjectileResourcePath)
            .Where(sprite => sprite.name.IndexOf("charge", System.StringComparison.OrdinalIgnoreCase) < 0)
            .ToArray();
        impactSprites ??= LoadSprites(ImpactResourcePath);
        meleeSprites ??= LoadSprites(MeleeResourcePath);
        healSprites ??= LoadSprites(HealResourcePath);
        shieldSprites ??= LoadSprites(ShieldResourcePath);
        buffSprites ??= LoadSprites(BuffResourcePath);
        stunSprites ??= LoadSprites(StunResourcePath);
        slowSprites ??= LoadSprites(SlowResourcePath);
        powerSprites ??= LoadSprites(PowerResourcePath);
        areaSprites ??= LoadSprites(AreaResourcePath);
        damageBoostSprites ??= LoadSprites(DamageBoostResourcePath);
    }

    // 指定Resourcesパスの全Spriteを読み、名前末尾の番号順に並べます。
    private static Sprite[] LoadSprites(string resourcePath)
    {
        return Resources.LoadAll<Sprite>(resourcePath)
            .Where(sprite => sprite != null)
            .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
            .ThenBy(sprite => sprite.name)
            .ToArray();
    }

    // 常時表示用のシールドは、発生途中の小さいコマを避けて大きいコマだけループします。
    private static Sprite[] GetShieldAuraSprites()
    {
        if (shieldSprites == null || shieldSprites.Length == 0)
            return new Sprite[0];

        Sprite[] loopSprites = shieldSprites
            .Where(sprite => ExtractTrailingNumber(sprite.name) >= 4)
            .ToArray();

        return loopSprites.Length > 0 ? loopSprites : shieldSprites;
    }

    // "name_001" のような名前から末尾番号を取り出します。
    private static int ExtractTrailingNumber(string value)
    {
        Match match = Regex.Match(value ?? string.Empty, @"_(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    // 通常攻撃エフェクトの色をチームで変えます。
    private static Color GetTeamEffectColor(BaseEntity attacker)
    {
        return attacker.Team == Team.Team2
            ? new Color(1f, 0.2f, 0.14f, 1f)
            : new Color(0.2f, 0.9f, 1f, 1f);
    }

    // スキル種類ごとのエフェクト色を返します。
    private static Color GetSkillEffectColor(BaseEntity caster, UnitSkillType skillType)
    {
        Color themeColor = GetThemeColor(caster != null ? caster.SkillTheme : SkillVisualTheme.Neutral);
        if ((caster != null ? caster.SkillTheme : SkillVisualTheme.Neutral) != SkillVisualTheme.Neutral)
        {
            switch (skillType)
            {
                case UnitSkillType.SelfHeal:
                case UnitSkillType.AllyHeal:
                    return Color.Lerp(new Color(0.35f, 1f, 0.28f, 1f), themeColor, 0.35f);
                case UnitSkillType.Shield:
                    return Color.Lerp(new Color(0.95f, 1f, 1f, 1f), themeColor, 0.45f);
                default:
                    return themeColor;
            }
        }

        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.AllyHeal:
                return new Color(0.35f, 1f, 0.28f, 1f);
            case UnitSkillType.Shield:
                return new Color(0.95f, 1f, 1f, 1f);
            case UnitSkillType.AttackSpeedBoost:
            case UnitSkillType.DamageBoost:
                return new Color(1f, 0.82f, 0.22f, 1f);
            case UnitSkillType.Stun:
                return new Color(1f, 0.88f, 0.18f, 1f);
            case UnitSkillType.Slow:
                return new Color(0.35f, 0.8f, 1f, 1f);
            case UnitSkillType.AreaDamage:
                return new Color(1f, 0.42f, 0.18f, 1f);
            default:
                return GetTeamEffectColor(caster);
        }
    }

    // ユニットの属性とスキル種類に合ったSprite配列を返します。
    private static Sprite[] GetSkillSprites(BaseEntity caster, UnitSkillType skillType)
    {
        if (skillType == UnitSkillType.Shield)
            return shieldSprites;

        SkillVisualTheme theme = caster != null ? caster.SkillTheme : SkillVisualTheme.Neutral;
        switch (theme)
        {
            case SkillVisualTheme.Fire:
                return skillType == UnitSkillType.Shield ? shieldSprites : areaSprites;
            case SkillVisualTheme.Ice:
                return skillType == UnitSkillType.AllyHeal ? healSprites : slowSprites;
            case SkillVisualTheme.Nature:
                return skillType == UnitSkillType.PowerStrike ? powerSprites : healSprites;
            case SkillVisualTheme.Holy:
                return skillType == UnitSkillType.Stun ? stunSprites : shieldSprites;
            case SkillVisualTheme.Shadow:
                return skillType == UnitSkillType.AreaDamage ? areaSprites : damageBoostSprites;
            case SkillVisualTheme.Lightning:
                return skillType == UnitSkillType.AttackSpeedBoost ? buffSprites : impactSprites;
            case SkillVisualTheme.Tech:
                return skillType == UnitSkillType.Shield ? shieldSprites : buffSprites;
            case SkillVisualTheme.Void:
                return skillType == UnitSkillType.Slow ? slowSprites : powerSprites;
        }

        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.AllyHeal:
                return healSprites;
            case UnitSkillType.Shield:
                return shieldSprites;
            case UnitSkillType.AttackSpeedBoost:
                return buffSprites;
            case UnitSkillType.Stun:
                return stunSprites;
            case UnitSkillType.Slow:
                return slowSprites;
            case UnitSkillType.DamageBoost:
                return damageBoostSprites;
            case UnitSkillType.AreaDamage:
                return areaSprites;
            default:
                return powerSprites;
        }
    }

    // ユニットの属性とスキル種類に合ったResourcesパスを返します。
    private static string GetSkillResourcePath(BaseEntity caster, UnitSkillType skillType)
    {
        if (skillType == UnitSkillType.Shield)
            return ShieldResourcePath;

        SkillVisualTheme theme = caster != null ? caster.SkillTheme : SkillVisualTheme.Neutral;
        switch (theme)
        {
            case SkillVisualTheme.Fire:
                return skillType == UnitSkillType.Shield ? ShieldResourcePath : AreaResourcePath;
            case SkillVisualTheme.Ice:
                return skillType == UnitSkillType.AllyHeal ? HealResourcePath : SlowResourcePath;
            case SkillVisualTheme.Nature:
                return skillType == UnitSkillType.PowerStrike ? PowerResourcePath : HealResourcePath;
            case SkillVisualTheme.Holy:
                return skillType == UnitSkillType.Stun ? StunResourcePath : ShieldResourcePath;
            case SkillVisualTheme.Shadow:
                return skillType == UnitSkillType.AreaDamage ? AreaResourcePath : DamageBoostResourcePath;
            case SkillVisualTheme.Lightning:
                return skillType == UnitSkillType.AttackSpeedBoost ? BuffResourcePath : ImpactResourcePath;
            case SkillVisualTheme.Tech:
                return skillType == UnitSkillType.Shield ? ShieldResourcePath : BuffResourcePath;
            case SkillVisualTheme.Void:
                return skillType == UnitSkillType.Slow ? SlowResourcePath : PowerResourcePath;
        }

        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.AllyHeal:
                return HealResourcePath;
            case UnitSkillType.Shield:
                return ShieldResourcePath;
            case UnitSkillType.AttackSpeedBoost:
                return BuffResourcePath;
            case UnitSkillType.Stun:
                return StunResourcePath;
            case UnitSkillType.Slow:
                return SlowResourcePath;
            case UnitSkillType.DamageBoost:
                return DamageBoostResourcePath;
            case UnitSkillType.AreaDamage:
                return AreaResourcePath;
            default:
                return PowerResourcePath;
        }
    }

    // スキル種類ごとのエフェクトサイズです。
    private static float GetSkillEffectScale(BaseEntity caster, UnitSkillType skillType)
    {
        float costScale = caster != null ? 1f + Mathf.Max(0, caster.BaseCost - 1) * 0.08f : 1f;
        float radiusScale = caster != null ? Mathf.Clamp(caster.skillAreaRadius / 1.6f, 0.9f, 2.4f) : 1f;
        switch (skillType)
        {
            case UnitSkillType.AreaDamage:
                return 1.35f * costScale * radiusScale;
            case UnitSkillType.AllyHeal:
            case UnitSkillType.AttackSpeedBoost:
            case UnitSkillType.DamageBoost:
                return Mathf.Max(1f, 0.9f * costScale * radiusScale);
            case UnitSkillType.Shield:
                return 1.08f * costScale;
            case UnitSkillType.Stun:
            case UnitSkillType.Slow:
                return 0.92f * costScale;
            default:
                return 1f * costScale;
        }
    }

    // スキル種類から再生したいSE名を決めます。
    private static string GetSkillSfxName(BaseEntity caster, UnitSkillType skillType)
    {
        if (skillType == UnitSkillType.Shield)
            return "skill_shield";

        if (caster != null)
        {
            switch (caster.SkillTheme)
            {
                case SkillVisualTheme.Fire:
                    return "skill_area";
                case SkillVisualTheme.Ice:
                    return "skill_control";
                case SkillVisualTheme.Nature:
                case SkillVisualTheme.Holy:
                    return skillType == UnitSkillType.PowerStrike ? "skill_power" : "skill_heal";
                case SkillVisualTheme.Lightning:
                    return "skill_control";
                case SkillVisualTheme.Shadow:
                case SkillVisualTheme.Void:
                    return skillType == UnitSkillType.SelfHeal || skillType == UnitSkillType.AllyHeal ? "skill_heal" : "skill_power";
                case SkillVisualTheme.Tech:
                    return "skill_buff";
            }
        }

        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.AllyHeal:
                return "skill_heal";
            case UnitSkillType.Shield:
                return "skill_shield";
            case UnitSkillType.AttackSpeedBoost:
            case UnitSkillType.DamageBoost:
                return "skill_buff";
            case UnitSkillType.Stun:
            case UnitSkillType.Slow:
                return "skill_control";
            case UnitSkillType.AreaDamage:
                return "skill_area";
            default:
                return "skill_power";
        }
    }

    // 持続シールドは半透明にして、ユニット本体を隠しすぎないようにします。
    private static Color GetPersistentShieldColor(BaseEntity owner)
    {
        Color color = GetSkillEffectColor(owner, UnitSkillType.Shield);
        color = Color.Lerp(Color.white, color, 0.42f);
        color.a = 0.58f;
        return color;
    }

    // ユニットの見た目を覆うサイズに、シールド用Spriteを拡縮します。
    private static float GetPersistentShieldScale(BaseEntity owner, Sprite[] sprites)
    {
        if (owner == null || sprites == null || sprites.Length == 0)
            return 0.65f;

        Sprite referenceSprite = sprites[sprites.Length - 1];
        Vector3 referenceSize = referenceSprite != null ? referenceSprite.bounds.size : new Vector3(2f, 2.56f, 0f);
        Vector2 shieldSize = new Vector2(referenceSize.x, referenceSize.y);
        if (shieldSize.x <= 0.01f || shieldSize.y <= 0.01f)
            return 0.65f;

        Bounds unitBounds = owner.spriteRender != null && owner.spriteRender.sprite != null
            ? owner.spriteRender.bounds
            : new Bounds(owner.transform.position, Vector3.one);
        float desiredWidth = Mathf.Clamp(unitBounds.size.x * 1.35f, 0.95f, 2.4f);
        float desiredHeight = Mathf.Clamp(unitBounds.size.y * 1.22f, 1.05f, 2.6f);
        float scale = Mathf.Max(desiredWidth / shieldSize.x, desiredHeight / shieldSize.y);
        return Mathf.Clamp(scale, 0.42f, 1.35f);
    }

    // シールド残量がある間だけ、ユニット位置へ追従してアニメーションする小さな補助コンポーネントです。
    private class ShieldAuraVisual : MonoBehaviour
    {
        private BaseEntity owner;
        private SpriteRenderer auraRenderer;
        private Sprite[] sprites;
        private int frameIndex;
        private float frameTimer;

        public void Begin(BaseEntity owner, SpriteRenderer renderer, Sprite[] sprites)
        {
            this.owner = owner;
            auraRenderer = renderer;
            this.sprites = sprites;
            frameIndex = 0;
            frameTimer = 0f;
            UpdateVisualNow();
        }

        private void LateUpdate()
        {
            if (owner == null || owner.IsDead || !owner.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            if (sprites == null || sprites.Length == 0 || auraRenderer == null)
            {
                Destroy(gameObject);
                return;
            }

            frameTimer += Time.deltaTime;
            float frameTime = 1f / Mathf.Max(1f, FrameRate * 0.72f);
            if (frameTimer >= frameTime)
            {
                frameTimer -= frameTime;
                frameIndex = (frameIndex + 1) % sprites.Length;
                auraRenderer.sprite = sprites[frameIndex];
            }

            UpdateVisualNow();
        }

        private void UpdateVisualNow()
        {
            if (owner == null || auraRenderer == null)
                return;

            Bounds unitBounds = owner.spriteRender != null && owner.spriteRender.sprite != null
                ? owner.spriteRender.bounds
                : new Bounds(owner.transform.position, Vector3.one);
            transform.position = new Vector3(unitBounds.center.x, unitBounds.center.y + 0.03f, owner.transform.position.z - 0.08f);
            transform.localScale = Vector3.one * GetPersistentShieldScale(owner, sprites);
            auraRenderer.sortingOrder = GetEffectSortingOrder(owner.transform.position, 34);
        }
    }

    // ユニットごとの属性色です。似たスキルでも、キャラの雰囲気が少し出るようにします。
    private static Color GetThemeColor(SkillVisualTheme theme)
    {
        switch (theme)
        {
            case SkillVisualTheme.Fire:
                return new Color(1f, 0.28f, 0.08f, 1f);
            case SkillVisualTheme.Ice:
                return new Color(0.35f, 0.9f, 1f, 1f);
            case SkillVisualTheme.Nature:
                return new Color(0.35f, 1f, 0.35f, 1f);
            case SkillVisualTheme.Holy:
                return new Color(1f, 0.9f, 0.3f, 1f);
            case SkillVisualTheme.Shadow:
                return new Color(0.72f, 0.15f, 1f, 1f);
            case SkillVisualTheme.Lightning:
                return new Color(0.3f, 0.85f, 1f, 1f);
            case SkillVisualTheme.Tech:
                return new Color(0.95f, 0.45f, 1f, 1f);
            case SkillVisualTheme.Void:
                return new Color(0.42f, 0.2f, 1f, 1f);
            default:
                return Color.white;
        }
    }

    // スキルSEの音量です。範囲攻撃だけ少し大きめにしています。
    private static float GetSkillSfxVolume(UnitSkillType skillType)
    {
        return skillType == UnitSkillType.AreaDamage ? 0.56f : 0.48f;
    }

    // エフェクトの描画順をユニットと同じ基準で計算します。
    private static int GetEffectSortingOrder(Vector3 position, int offset)
    {
        return BaseEntity.CalculateSortingOrder(position, offset);
    }

    // 遠距離攻撃の弾と軌跡を再生します。
    private IEnumerator PlayRangedAttack(Vector3 source, Vector3 destination, Color teamColor)
    {
        GameObject projectileObject = new GameObject("RangedAttackProjectile");
        projectileObject.transform.position = source;

        SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
        projectileRenderer.sortingOrder = GetEffectSortingOrder(source, 45);
        projectileRenderer.color = Color.Lerp(Color.white, teamColor, 0.28f);
        if (projectileSprites.Length > 0)
            projectileRenderer.sprite = projectileSprites[0];

        // 弾の向きを、攻撃元から攻撃先へ向けます。
        Vector3 direction = destination - source;
        float distance = direction.magnitude;
        if (distance > 0.01f)
            projectileObject.transform.right = direction.normalized;

        float duration = Mathf.Clamp(distance / 7f, 0.16f, 0.48f);
        LineRenderer trail = CreateTrail(projectileObject, teamColor, GetEffectSortingOrder(source, 44));

        // 弾を目的地まで補間移動させ、少し弧を描かせます。
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(source, destination, progress);
            float arc = Mathf.Sin(progress * Mathf.PI) * 0.18f;
            position += Vector3.up * arc;
            projectileObject.transform.position = position;
            projectileRenderer.sortingOrder = GetEffectSortingOrder(position, 45);
            if (trail != null)
                trail.sortingOrder = GetEffectSortingOrder(position, 44);

            if (projectileSprites.Length > 0)
                projectileRenderer.sprite = projectileSprites[Mathf.FloorToInt(elapsed * FrameRate) % projectileSprites.Length];

            UpdateTrail(trail, source, position, teamColor, progress);
            yield return null;
        }

        Destroy(projectileObject);
        StartCoroutine(PlayAnimatedSprite(ImpactResourcePath, impactSprites, destination, 0.9f, teamColor, 0f, GetEffectSortingOrder(destination, 55)));
    }

    // 近接攻撃の斬撃エフェクトと着弾エフェクトを再生します。
    private IEnumerator PlayMeleeAttack(Vector3 source, Vector3 destination, Color teamColor)
    {
        Vector3 direction = destination - source;
        float angle = direction.sqrMagnitude > 0.001f
            ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
            : 0f;

        int sortingOrder = GetEffectSortingOrder(destination, 55);
        yield return PlayAnimatedSprite(MeleeResourcePath, meleeSprites, destination, 1.05f, teamColor, angle, sortingOrder);
        StartCoroutine(PlayAnimatedSprite(ImpactResourcePath, impactSprites, destination, 0.62f, teamColor, 0f, sortingOrder + 1));
    }

    // 遠距離攻撃の軌跡をLineRendererで作ります。
    private LineRenderer CreateTrail(GameObject parent, Color teamColor, int sortingOrder)
    {
        LineRenderer line = parent.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 0.08f;
        line.endWidth = 0.22f;
        line.sortingOrder = sortingOrder;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            line.material = new Material(shader);

        line.startColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0f);
        line.endColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.75f);
        return line;
    }

    // 遠距離攻撃の軌跡を、弾の現在位置に合わせて更新します。
    private void UpdateTrail(LineRenderer trail, Vector3 source, Vector3 head, Color teamColor, float progress)
    {
        if (trail == null)
            return;

        Vector3 tail = Vector3.Lerp(source, head, Mathf.Max(0f, progress - 0.22f));
        trail.SetPosition(0, tail);
        trail.SetPosition(1, head);

        float alpha = Mathf.Sin(progress * Mathf.PI);
        trail.startColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0f);
        trail.endColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.75f * alpha);
    }

    // Sprite配列を順番に表示して、短いアニメーションエフェクトとして再生します。
    private IEnumerator PlayAnimatedSprite(string name, Sprite[] sprites, Vector3 position, float scale, Color teamColor, float angle, int sortingOrder)
    {
        if (sprites == null || sprites.Length == 0)
            yield break;

        GameObject effectObject = new GameObject(name);
        effectObject.transform.position = position;
        effectObject.transform.localScale = Vector3.one * scale;
        effectObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.Lerp(Color.white, teamColor, 0.18f);

        float frameTime = 1f / FrameRate;
        for (int i = 0; i < sprites.Length; i++)
        {
            renderer.sprite = sprites[i];
            yield return new WaitForSeconds(frameTime);
        }

        Destroy(effectObject);
    }

    // SE用とBGM用のAudioSourceを作ります。
    private void EnsureAudioSources()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = 0.75f;
        }

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.volume = 0.26f;
        }
    }

    // BGM候補から見つかったものを再生します。見つからなければ簡易生成BGMを使います。
    private void PlayBgmIfNeeded()
    {
        EnsureAudioSources();
        AudioClip clip = LoadFirstAudioClip(BgmCandidates);
        if (clip == null)
            clip = GetGeneratedBgmClip();

        if (clip == null)
            return;

        if (bgmSource.clip != clip)
        {
            bgmSource.Stop();
            bgmSource.clip = clip;
        }

        if (!bgmSource.isPlaying)
            bgmSource.Play();
    }

    // 指定名のSEを再生します。Resourcesに無ければ簡易生成音を使います。
    private void PlaySfx(string clipName, float volume)
    {
        EnsureAudioSources();
        AudioClip clip = LoadFirstAudioClip(GetSfxCandidates(clipName));
        if (clip == null)
            clip = GetGeneratedSfxClip(clipName);

        if (clip != null)
            sfxSource.PlayOneShot(clip, volume);
    }

    // SE名ごとに、Resources内で探す候補パスを返します。
    private static string[] GetSfxCandidates(string clipName)
    {
        switch (clipName)
        {
            case "shop_reroll":
                return new[]
                {
                    "sfx/sfx_ui_nextpage",
                    "sfx/sfx_ui_booster_packexplode",
                    "sfx/sfx_ui_select"
                };
            case "exp_buy":
                return new[]
                {
                    "sfx/sfx_tile_mana",
                    "sfx/sfx_gold_reward_1",
                    "sfx/pointdrop"
                };
            case "unit_buy":
                return new[]
                {
                    "sfx/sfx_unit_deploy",
                    "sfx/sfx_unit_deploy_1",
                    "sfx/sfx_ui_select"
                };
            case "unit_sell":
                return new[]
                {
                    "sfx/sfx_gold_reward_1",
                    "sfx/sfx_ui_cardburn",
                    "sfx/sfx_ui_select"
                };
            case "star_up":
                return new[]
                {
                    "sfx/sfx_summonlegendary",
                    "sfx/sfx_victory_reward",
                    "sfx/sfx_spell_starsfury"
                };
            case "drag_start":
                return new[]
                {
                    "sfx/sfx_unit_onclick",
                    "sfx/sfx_ui_in_game_hover"
                };
            case "drag_drop":
                return new[]
                {
                    "sfx/sfx_unit_deploy_1",
                    "sfx/sfx_unit_deploy",
                    "sfx/sfx_ui_select"
                };
            case "item_equip":
                return new[]
                {
                    "sfx/sfx_artifact_equip",
                    "sfx/sfx_ui_artifact",
                    "sfx/sfx_ui_select"
                };
            case "fight_start":
                return new[]
                {
                    "sfx/sfx_ui_yourturn",
                    "sfx/sfx_announcer_versus",
                    "sfx/sfx_ui_panel_swoosh_enter"
                };
            case "normal_attack":
                return new[]
                {
                    "sfx/sfx_unit_physical_1",
                    "sfx/sfx_f2melee_attack_swing_1",
                    "sfx/sfx_f1general_attack_swing"
                };
            case "skill_heal":
                return new[]
                {
                    "sfx/sfx_spell_heal",
                    "sfx/sfx_spell_fountainofyouth"
                };
            case "skill_shield":
                return new[]
                {
                    "sfx/sfx_spell_forcebarrier",
                    "sfx/sfx_spell_cosmicflesh"
                };
            case "skill_buff":
                return new[]
                {
                    "sfx/sfx_spell_attackbuff2",
                    "sfx/sfx_spell_warsurge"
                };
            case "skill_control":
                return new[]
                {
                    "sfx/sfx_spell_icepillar",
                    "sfx/sfx_spell_polymorph"
                };
            case "skill_area":
                return new[]
                {
                    "sfx/sfx_spell_firefall",
                    "sfx/sfx_spell_immolation_a"
                };
            case "skill_power":
                return new[]
                {
                    "sfx/sfx_f2melee_attack_impact_1",
                    "sfx/sfx_spell_truestrike"
                };
            case "death":
                return new[]
                {
                    "sfx/sfx_f2melee_death",
                    "sfx/sfx_f1general_death",
                    "sfx/sfx_neutral_serpenti_death"
                };
            default:
                return new[] { "sfx/" + clipName };
        }
    }

    // UI操作SEの音量を、操作の重要度ごとに調整します。
    private static float GetUiSfxVolume(string cueName)
    {
        switch (cueName)
        {
            case "fight_start":
            case "star_up":
                return 0.68f;
            case "unit_sell":
            case "unit_buy":
                return 0.56f;
            case "shop_reroll":
            case "exp_buy":
                return 0.5f;
            case "drag_start":
                return 0.34f;
            case "drag_drop":
                return 0.42f;
            default:
                return 0.45f;
        }
    }

    // 候補パスを上から順にResources.Loadし、最初に見つかったAudioClipを返します。
    private static AudioClip LoadFirstAudioClip(params string[] resourcePaths)
    {
        if (resourcePaths == null)
            return null;

        for (int i = 0; i < resourcePaths.Length; i++)
        {
            string resourcePath = resourcePaths[i];
            if (string.IsNullOrEmpty(resourcePath))
                continue;

            if (loadedAudioClips.TryGetValue(resourcePath, out AudioClip cachedClip))
                return cachedClip;

            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
                continue;

            loadedAudioClips[resourcePath] = clip;
            return clip;
        }

        return null;
    }

    // SE素材が見つからない時の保険として、短い電子音を生成します。
    private static AudioClip GetGeneratedSfxClip(string clipName)
    {
        if (generatedAudioClips.TryGetValue(clipName, out AudioClip clip))
            return clip;

        switch (clipName)
        {
            case "skill_heal":
                clip = CreateToneClip(clipName, 540f, 820f, 0.34f, 0.34f, 0.04f);
                break;
            case "skill_shield":
                clip = CreateToneClip(clipName, 220f, 360f, 0.42f, 0.38f, 0.08f);
                break;
            case "skill_buff":
                clip = CreateToneClip(clipName, 390f, 760f, 0.38f, 0.36f, 0.05f);
                break;
            case "skill_control":
                clip = CreateToneClip(clipName, 260f, 120f, 0.36f, 0.4f, 0.18f);
                break;
            case "skill_area":
                clip = CreateToneClip(clipName, 130f, 55f, 0.48f, 0.55f, 0.28f);
                break;
            case "skill_power":
                clip = CreateToneClip(clipName, 310f, 520f, 0.34f, 0.42f, 0.12f);
                break;
            default:
                clip = CreateToneClip(clipName, 250f, 330f, 0.16f, 0.28f, 0.18f);
                break;
        }

        generatedAudioClips[clipName] = clip;
        return clip;
    }

    // BGM素材が見つからない時の保険として、短いループ音を生成します。
    private static AudioClip GetGeneratedBgmClip()
    {
        const string key = "bgm_battle_generated";
        if (generatedAudioClips.TryGetValue(key, out AudioClip clip))
            return clip;

        const int sampleRate = 44100;
        const float duration = 8f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[sampleCount];
        float[] bassNotes = { 55f, 65.41f, 73.42f, 82.41f };
        float[] leadNotes = { 220f, 246.94f, 293.66f, 329.63f };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int step = Mathf.FloorToInt(t * 2f) % bassNotes.Length;
            int leadStep = Mathf.FloorToInt(t * 4f) % leadNotes.Length;
            float beatEnvelope = 0.55f + 0.45f * Mathf.Sin((t * 2f % 1f) * Mathf.PI);
            float bass = Mathf.Sin(2f * Mathf.PI * bassNotes[step] * t) * 0.08f;
            float lead = Mathf.Sin(2f * Mathf.PI * leadNotes[leadStep] * t) * 0.018f * beatEnvelope;
            data[i] = bass + lead;
        }

        clip = AudioClip.Create(key, sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        generatedAudioClips[key] = clip;
        return clip;
    }

    // 周波数を変化させながら、簡単な効果音AudioClipを作ります。
    private static AudioClip CreateToneClip(string clipName, float startFrequency, float endFrequency, float duration, float volume, float noiseAmount)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = Mathf.Clamp01(t / duration);
            float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
            tone += Mathf.Sin(2f * Mathf.PI * frequency * 2.01f * t) * 0.35f;
            float noise = Mathf.PerlinNoise(i * 0.023f, frequency * 0.001f) * 2f - 1f;
            data[i] = Mathf.Clamp((tone * (1f - noiseAmount) + noise * noiseAmount) * volume * envelope, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName + "_generated", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
