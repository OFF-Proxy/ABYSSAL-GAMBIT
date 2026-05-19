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
        Sprite[] sprites = GetSkillSprites(skillType);
        string resourcePath = GetSkillResourcePath(skillType);
        float scale = GetSkillEffectScale(skillType);

        instance.PlaySfx(GetSkillSfxName(skillType), GetSkillSfxVolume(skillType));
        instance.StartCoroutine(instance.PlayAnimatedSprite(resourcePath, sprites, position, scale, color, 0f, GetEffectSortingOrder(position, 70)));
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

    // スキル種類に合ったSprite配列を返します。
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

    // スキル種類に合ったResourcesパスを返します。
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

    // スキル種類ごとのエフェクトサイズです。
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

    // スキル種類から再生したいSE名を決めます。
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
