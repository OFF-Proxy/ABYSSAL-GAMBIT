using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class AttackEffectPlayer : MonoBehaviour
{
    private const float FrameRate = 18f;
    private const string ProjectileResourcePath = "AttackEffects/fx_f1_casterprojectile";
    private const string ImpactResourcePath = "AttackEffects/fx_explosionblueelectrical";
    private const string MeleeResourcePath = "AttackEffects/fx_crossslash";
    private const string HealResourcePath = "AttackEffects/fx_heal";
    private const string ShieldResourcePath = "AttackEffects/fx_distortion_hex_shield";
    private const string BuffResourcePath = "AttackEffects/fx_buff";
    private const string StunResourcePath = "AttackEffects/fx_f6_bbs_stun";
    private const string SlowResourcePath = "AttackEffects/fx_frozen";
    private const string PowerResourcePath = "AttackEffects/fx_slashfrenzy";
    private const string AreaResourcePath = "AttackEffects/fx_whiteexplosion";
    private const string DamageBoostResourcePath = "AttackEffects/fx_ringswirl";

    private static readonly string[] BgmCandidates =
    {
        "music/music_battlemap01",
        "music/music_battlemap02",
        "music/music_battlemap_abyssian",
        "music/music_playmode"
    };

    private static readonly Dictionary<string, AudioClip> generatedAudioClips = new Dictionary<string, AudioClip>();
    private static readonly Dictionary<string, AudioClip> loadedAudioClips = new Dictionary<string, AudioClip>();
    private static AttackEffectPlayer instance;
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

    private AudioSource sfxSource;
    private AudioSource bgmSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAudio()
    {
        EnsureInstance();
        instance.PlayBgmIfNeeded();
    }

    public static void PlayAttack(BaseEntity attacker, BaseEntity target, bool rangedAttack)
    {
        if (attacker == null || target == null)
            return;

        EnsureInstance();
        EnsureSpritesLoaded();

        Vector3 source = attacker.transform.position + new Vector3(0f, 0.12f, 0f);
        Vector3 destination = target.transform.position + new Vector3(0f, 0.18f, 0f);
        Color teamColor = GetTeamEffectColor(attacker);

        instance.PlaySfx("normal_attack", 0.42f);
        if (rangedAttack)
            instance.StartCoroutine(instance.PlayRangedAttack(source, destination, teamColor));
        else
            instance.StartCoroutine(instance.PlayMeleeAttack(source, destination, teamColor));
    }

    public static void PlaySkill(BaseEntity caster, BaseEntity target, UnitSkillType skillType)
    {
        if (caster == null)
            return;

        EnsureInstance();
        EnsureSpritesLoaded();

        BaseEntity effectTarget = GetSkillEffectTarget(caster, target, skillType);
        Vector3 position = effectTarget.transform.position + new Vector3(0f, 0.2f, 0f);
        Color color = GetSkillEffectColor(caster, skillType);
        Sprite[] sprites = GetSkillSprites(skillType);
        string resourcePath = GetSkillResourcePath(skillType);
        float scale = GetSkillEffectScale(skillType);

        instance.PlaySfx(GetSkillSfxName(skillType), GetSkillSfxVolume(skillType));
        instance.StartCoroutine(instance.PlayAnimatedSprite(resourcePath, sprites, position, scale, color, 0f, GetEffectSortingOrder(position, 70)));
    }

    public static void PlayDeath(BaseEntity entity)
    {
        if (entity == null)
            return;

        EnsureInstance();
        instance.PlaySfx("death", 0.58f);
    }

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

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AttackEffectPlayer");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AttackEffectPlayer>();
        instance.EnsureAudioSources();
    }

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

    private static Sprite[] LoadSprites(string resourcePath)
    {
        return Resources.LoadAll<Sprite>(resourcePath)
            .Where(sprite => sprite != null)
            .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
            .ThenBy(sprite => sprite.name)
            .ToArray();
    }

    private static int ExtractTrailingNumber(string value)
    {
        Match match = Regex.Match(value ?? string.Empty, @"_(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static Color GetTeamEffectColor(BaseEntity attacker)
    {
        return attacker.Team == Team.Team2
            ? new Color(1f, 0.2f, 0.14f, 1f)
            : new Color(0.2f, 0.9f, 1f, 1f);
    }

    private static Color GetSkillEffectColor(BaseEntity caster, UnitSkillType skillType)
    {
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

    private static Sprite[] GetSkillSprites(UnitSkillType skillType)
    {
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

    private static string GetSkillResourcePath(UnitSkillType skillType)
    {
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

    private static float GetSkillEffectScale(UnitSkillType skillType)
    {
        switch (skillType)
        {
            case UnitSkillType.AreaDamage:
                return 1.35f;
            case UnitSkillType.Shield:
                return 1.08f;
            case UnitSkillType.Stun:
            case UnitSkillType.Slow:
                return 0.92f;
            default:
                return 1f;
        }
    }

    private static string GetSkillSfxName(UnitSkillType skillType)
    {
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

    private static float GetSkillSfxVolume(UnitSkillType skillType)
    {
        return skillType == UnitSkillType.AreaDamage ? 0.56f : 0.48f;
    }

    private static int GetEffectSortingOrder(Vector3 position, int offset)
    {
        return BaseEntity.CalculateSortingOrder(position, offset);
    }

    private IEnumerator PlayRangedAttack(Vector3 source, Vector3 destination, Color teamColor)
    {
        GameObject projectileObject = new GameObject("RangedAttackProjectile");
        projectileObject.transform.position = source;

        SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
        projectileRenderer.sortingOrder = GetEffectSortingOrder(source, 45);
        projectileRenderer.color = Color.Lerp(Color.white, teamColor, 0.28f);
        if (projectileSprites.Length > 0)
            projectileRenderer.sprite = projectileSprites[0];

        Vector3 direction = destination - source;
        float distance = direction.magnitude;
        if (distance > 0.01f)
            projectileObject.transform.right = direction.normalized;

        float duration = Mathf.Clamp(distance / 7f, 0.16f, 0.48f);
        LineRenderer trail = CreateTrail(projectileObject, teamColor, GetEffectSortingOrder(source, 44));

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

    private void PlaySfx(string clipName, float volume)
    {
        EnsureAudioSources();
        AudioClip clip = LoadFirstAudioClip(GetSfxCandidates(clipName));
        if (clip == null)
            clip = GetGeneratedSfxClip(clipName);

        if (clip != null)
            sfxSource.PlayOneShot(clip, volume);
    }

    private static string[] GetSfxCandidates(string clipName)
    {
        switch (clipName)
        {
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
