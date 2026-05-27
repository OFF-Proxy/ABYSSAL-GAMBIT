using System.Collections;
using System.Collections.Generic;
using System;
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
    private const string InfernoResourcePath = "AttackEffects/fx_fireimpact";
    private const string InfernoRainResourcePath = "AttackEffects/fx_f2_phoenixfire";
    private const string StormResourcePath = "AttackEffects/fx_chainlightning";
    private const string LightningSkillResourcePath = "AttackEffects/Pixel Art Skill Animations - Lightning/VFX3/Sprite-sheet/Sprite-sheet";
    private const string AbyssResourcePath = "AttackEffects/fx_f4_shadownova";
    private const string AbyssCurseResourcePath = "AttackEffects/fx_f4_voidpulse";
    private const string DivineResourcePath = "AttackEffects/fx_summonlegendary";
    private const string SummonerResourcePath = "AttackEffects/fx_f4_nethersummoning";
    private const string BigFireResourcePath = "AttackEffects/fx_f3_blaststarfire";
    private const string FreeVortexResourcePath = "AttackEffects/Free2/03";
    private const string FreeProjectileBurstResourcePath = "AttackEffects/Free2/13";
    private const string FreeLotusResourcePath = "AttackEffects/Free3/655";
    private const string FreeStarburstResourcePath = "AttackEffects/Free3/652";
    private const string FreeSlashResourcePath = "AttackEffects/Free3/675";
    private const string FreeFirePillarResourcePath = "AttackEffects/Free4/464";
    private const string FreeFireWaveResourcePath = "AttackEffects/Free4/477";
    private const string FreeFlameResourcePath = "AttackEffects/Free4/476";
    private const string FreeSmokeResourcePath = "AttackEffects/Free4/484";
    private const string CircleImpactResourcePath = "AttackEffects/fx_circle_00";
    private const string CircleGroundResourcePath = "AttackEffects/fx_circle_01";
    private const int FreeEffectCellSize = 64;
    private const float BoardEffectYSquash = 0.58f;
    private const float BoardEffectTiltAngle = -6f;

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
    private static readonly Dictionary<string, Sprite[]> effectRowSpriteCache = new Dictionary<string, Sprite[]>();

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
    private static Sprite[] infernoSprites;
    private static Sprite[] infernoRainSprites;
    private static Sprite[] stormSprites;
    private static Sprite[] lightningSkillSprites;
    private static Sprite[] abyssSprites;
    private static Sprite[] abyssCurseSprites;
    private static Sprite[] divineSprites;
    private static Sprite[] summonerSprites;
    private static Sprite[] bigFireSprites;
    private static Sprite projectileOrbSprite;
    private static Sprite areaIndicatorSprite;

    // SE用とBGM用のAudioSourceです。BGMはループ再生します。
    private AudioSource sfxSource;
    private AudioSource bgmSource;
    private Coroutine cameraShakeCoroutine;
    private Camera cameraShakeTarget;
    private Vector3 cameraShakeOriginalPosition;
    private readonly List<GameObject> activeBattleVisuals = new List<GameObject>();

    // シーン読み込み後、自動でこのクラスを用意しBGMを鳴らします。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAudio()
    {
        EnsureInstance();
        instance.PlayBgmIfNeeded();
    }

    // 通常攻撃のエフェクトとSEを再生します。
    public static void PlayAttack(BaseEntity attacker, BaseEntity target, bool rangedAttack, Action onImpact = null)
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
            instance.StartCoroutine(instance.PlayRangedAttack(source, target.transform, destination, teamColor, onImpact));
        else
        {
            instance.StartCoroutine(instance.PlayMeleeAttack(source, destination, teamColor));
            onImpact?.Invoke();
        }
    }

    // HP回復が発生した対象に、共通の回復エフェクトを出します。
    public static void PlayHealPulse(BaseEntity target)
    {
        if (target == null)
            return;

        EnsureInstance();
        EnsureSpritesLoaded();

        Vector3 position = target.transform.position + new Vector3(0f, 0.2f, 0f);
        Color color = new Color(0.35f, 1f, 0.3f, 1f);
        instance.PlaySfx("skill_heal", 0.22f);
        instance.PlayLayeredFreeEffect(
            FreeLotusResourcePath,
            CircleGroundResourcePath,
            GetEffectRow(SkillVisualTheme.Nature, UnitSkillType.AllyHeal),
            position,
            1.05f,
            color,
            GetEffectSortingOrder(position, 72),
            boardAlignedGround: true);
        instance.StartCoroutine(instance.PlayAnimatedSprite(HealResourcePath, healSprites, position, 0.78f, color, 0f, GetEffectSortingOrder(position, 73)));
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
        float scale = GetSkillEffectScale(caster, skillType);

        instance.PlaySfx(GetSkillSfxName(caster, skillType), GetSkillSfxVolume(skillType));
        if (skillType == UnitSkillType.Shield)
        {
            instance.StartCoroutine(instance.PlayAnimatedSprite(
                ShieldResourcePath,
                shieldSprites,
                position,
                scale,
                color,
                0f,
                GetEffectSortingOrder(position, 70)));
            return;
        }

        instance.PlaySkillVisualBurst(caster, skillType, position, scale, color);
    }

    // シナジー効果が発動した地点に、専用の目立つエフェクトとSEを出します。
    public static void PlaySynergyEffect(SynergyType type, Vector3 position, float scale = 1f)
    {
        if (type == SynergyType.None)
            return;

        EnsureInstance();
        EnsureSpritesLoaded();

        Color color = GetSynergyEffectColor(type);
        instance.PlaySfx(GetSynergySfxName(type), GetSynergySfxVolume(type));
        instance.PlaySynergyVisualBurst(type, position, scale, color);
    }

    // Invader専用の落雷です。横長の雷シートを縦向きに回転し、対象へ上から落ちるように見せます。
    public static void PlayInvaderLightningStrike(Vector3 targetPosition, float scale = 1f, bool starThree = false)
    {
        EnsureInstance();
        EnsureSpritesLoaded();

        Sprite[] lightningSprites = stormSprites != null && stormSprites.Length > 0 ? stormSprites : impactSprites;
        if (lightningSprites == null || lightningSprites.Length == 0)
            return;

        Color lightningColor = new Color(0.35f, 0.9f, 1f, 1f);
        Vector3 groundPosition = targetPosition + new Vector3(0f, 0.02f, 0f);
        int sortingOrder = GetEffectSortingOrder(groundPosition, 86);
        float safeScale = Mathf.Max(0.75f, scale * 2f * (starThree ? 1.35f : 1f));

        instance.PlaySfx("synergy_storm", 0.58f);
        instance.StartCoroutine(instance.PlayAnimatedSprite(
            StormResourcePath,
            lightningSprites,
            groundPosition + new Vector3(0f, Mathf.Clamp(0.42f * safeScale, 0.35f, 1.25f), -0.05f),
            new Vector3(safeScale * 0.85f, safeScale * 1.8f, 1f),
            lightningColor,
            -90f,
            sortingOrder));

        instance.StartCoroutine(instance.PlayAnimatedSprite(
            StormResourcePath,
            lightningSprites,
            groundPosition + new Vector3(0f, -0.04f, -0.06f),
            GetBoardAlignedScale(safeScale * 0.85f),
            lightningColor,
            BoardEffectTiltAngle,
            sortingOrder - 1));

        if (starThree)
        {
            Sprite[] heavyLightningSprites = lightningSkillSprites != null && lightningSkillSprites.Length > 0
                ? lightningSkillSprites
                : lightningSprites;

            if (heavyLightningSprites.Length > 0)
            {
                float heavyScaleY = Mathf.Clamp(safeScale * 0.34f, 1.85f, 2.65f);
                float heavyScaleX = heavyScaleY * 0.72f;
                float lightningCenterOffsetY = heavyScaleY * 1.28f - 0.06f;
                instance.StartCoroutine(instance.PlayAnimatedSprite(
                    heavyLightningSprites == lightningSkillSprites ? LightningSkillResourcePath : StormResourcePath,
                    heavyLightningSprites,
                    groundPosition + new Vector3(0f, lightningCenterOffsetY, 0.06f),
                    new Vector3(heavyScaleX, heavyScaleY, 1f),
                    lightningColor,
                    0f,
                    sortingOrder + 220));

                instance.StartCoroutine(instance.PlayAnimatedSprite(
                    heavyLightningSprites == lightningSkillSprites ? LightningSkillResourcePath : StormResourcePath,
                    heavyLightningSprites,
                    groundPosition + new Vector3(0f, lightningCenterOffsetY + 0.06f, 0.05f),
                    new Vector3(heavyScaleX * 0.78f, heavyScaleY * 0.94f, 1f),
                    new Color(0.78f, 1f, 1f, 1f),
                    0f,
                    sortingOrder + 219));
            }
        }
    }

    // Skyfalltyrant★1/★2用です。口元から対象へ向けて、追従する火炎放射を一定時間出します。
    public static void PlaySkyfallBreathFlame(BaseEntity caster, BaseEntity target, Vector3 fallbackDirection, float duration, float maxDistance)
    {
        if (caster == null)
            return;

        EnsureInstance();
        EnsureSpritesLoaded();

        Sprite[] coreSprites = LoadGridRowSprites(FreeSmokeResourcePath, 2);
        Sprite[] edgeSprites = LoadGridRowSprites(FreeFireWaveResourcePath, 2);
        Sprite[] mouthSprites = LoadGridRowSprites(FreeFlameResourcePath, 2);
        if (coreSprites.Length == 0 && edgeSprites.Length == 0 && mouthSprites.Length == 0)
            return;

        instance.PlaySfx("synergy_inferno", 0.56f);
        instance.StartCoroutine(instance.PlaySkyfallBreathFlameCoroutine(
            caster,
            target,
            fallbackDirection,
            Mathf.Max(0.1f, duration),
            Mathf.Max(0.8f, maxDistance),
            coreSprites,
            edgeSprites,
            mouthSprites));
    }

    // 火炎放射の本体です。対象が動いたら毎フレーム角度と長さを合わせ、火を吐いているように見せます。
    private IEnumerator PlaySkyfallBreathFlameCoroutine(
        BaseEntity caster,
        BaseEntity target,
        Vector3 fallbackDirection,
        float duration,
        float maxDistance,
        Sprite[] coreSprites,
        Sprite[] edgeSprites,
        Sprite[] mouthSprites)
    {
        GameObject coreObject = new GameObject("SkyfallBreathCore");
        GameObject edgeObject = new GameObject("SkyfallBreathEdge");
        GameObject mouthObject = new GameObject("SkyfallBreathMouth");
        RegisterBattleVisual(coreObject);
        RegisterBattleVisual(edgeObject);
        RegisterBattleVisual(mouthObject);

        SpriteRenderer coreRenderer = coreObject.AddComponent<SpriteRenderer>();
        SpriteRenderer edgeRenderer = edgeObject.AddComponent<SpriteRenderer>();
        SpriteRenderer mouthRenderer = mouthObject.AddComponent<SpriteRenderer>();
        coreRenderer.color = Color.white;
        edgeRenderer.color = Color.white;
        mouthRenderer.color = Color.white;

        Vector3 lastDirection = fallbackDirection.sqrMagnitude > 0.01f ? fallbackDirection.normalized : Vector3.right;
        float elapsed = 0f;
        while (elapsed < duration && caster != null && !caster.IsDead)
        {
            elapsed += Time.deltaTime;

            Vector3 targetPosition = target != null && !target.IsDead && target.CanBeTargeted
                ? target.transform.position + new Vector3(0f, 0.2f, 0f)
                : caster.transform.position + lastDirection * maxDistance;

            Vector3 sourcePosition = GetSkyfallMouthPosition(caster, lastDirection);
            Vector3 direction = targetPosition - sourcePosition;
            if (direction.sqrMagnitude > 0.01f)
                lastDirection = direction.normalized;
            else
                direction = lastDirection;

            sourcePosition = GetSkyfallMouthPosition(caster, lastDirection);
            float distance = Mathf.Clamp(Vector3.Distance(sourcePosition, targetPosition), 0.45f, maxDistance);
            Vector3 center = sourcePosition + lastDirection * (distance * 0.5f);
            float angle = Mathf.Atan2(lastDirection.y, lastDirection.x) * Mathf.Rad2Deg;
            int frameIndex = Mathf.FloorToInt(elapsed * FrameRate);
            int sortingOrder = GetEffectSortingOrder(center, 1250);

            if (coreSprites.Length > 0)
            {
                Sprite sprite = coreSprites[frameIndex % coreSprites.Length];
                coreRenderer.sprite = sprite;
                coreObject.transform.position = center + new Vector3(0f, 0.04f, 0.04f);
                coreObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                coreObject.transform.localScale = GetFlameBeamScale(sprite, distance, 0.72f);
                coreRenderer.sortingOrder = sortingOrder;
            }

            if (edgeSprites.Length > 0)
            {
                Sprite sprite = edgeSprites[frameIndex % edgeSprites.Length];
                edgeRenderer.sprite = sprite;
                edgeObject.transform.position = center + lastDirection * 0.08f + new Vector3(0f, 0.02f, 0.05f);
                edgeObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                edgeObject.transform.localScale = GetFlameBeamScale(sprite, distance * 0.9f, 0.54f);
                edgeRenderer.sortingOrder = sortingOrder + 1;
            }

            if (mouthSprites.Length > 0)
            {
                Sprite sprite = mouthSprites[frameIndex % mouthSprites.Length];
                mouthRenderer.sprite = sprite;
                mouthObject.transform.position = sourcePosition + lastDirection * 0.16f + new Vector3(0f, 0.02f, 0.06f);
                mouthObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                mouthObject.transform.localScale = Vector3.one * 0.95f;
                mouthRenderer.sortingOrder = sortingOrder + 2;
            }

            yield return null;
        }

        UnregisterBattleVisual(coreObject);
        UnregisterBattleVisual(edgeObject);
        UnregisterBattleVisual(mouthObject);
        Destroy(coreObject);
        Destroy(edgeObject);
        Destroy(mouthObject);
    }

    // スプライト1コマを、火炎放射の距離に合わせた横長スケールへ変換します。
    private static Vector3 GetFlameBeamScale(Sprite sprite, float distance, float heightScale)
    {
        float spriteWidth = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 0.64f;
        return new Vector3(Mathf.Max(0.8f, distance / spriteWidth), Mathf.Max(0.32f, heightScale), 1f);
    }

    // 専用ボーンが無いので、ユニットの向きと足元から口元らしい位置を推定します。
    private static Vector3 GetSkyfallMouthPosition(BaseEntity caster, Vector3 direction)
    {
        if (caster == null)
            return Vector3.zero;

        Vector3 safeDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.right;
        return caster.transform.position + new Vector3(0f, 0.36f, 0.06f) + safeDirection * 0.38f;
    }

    // Skyfalltyrant専用の炎上範囲です。Multipleスライス済みの炎シートだけを使って盤面上に広く燃え広がらせます。
    public static void PlaySkyfallBoardFire(Vector3 center, float radiusInTiles, bool starThree)
    {
        PlaySkyfallBoardFire(center, radiusInTiles, starThree, Vector3.zero);
    }

    // Skyfalltyrant専用の炎上範囲です。★3未満は青い火炎放射、★3は別メソッドで盤面外周を燃やします。
    public static void PlaySkyfallBoardFire(Vector3 center, float radiusInTiles, bool starThree, Vector3 breathDirection)
    {
        PlaySkyfallBoardFire(center, radiusInTiles, starThree, breathDirection, true);
    }

    // Skyfalltyrantの継続炎上表示です。★3時はFree4炎素材だけを盤面角度に合わせて出します。
    public static void PlaySkyfallBoardFire(Vector3 center, float radiusInTiles, bool starThree, Vector3 breathDirection, bool showPhoenix)
    {
        EnsureInstance();
        EnsureSpritesLoaded();

        Color fireColor = starThree ? new Color(1f, 0.32f, 0.06f, 1f) : new Color(0.18f, 0.78f, 1f, 1f);
        float visualRadius = Mathf.Clamp(radiusInTiles, 1.1f, starThree ? 4.6f : 2.8f);
        int sortingOrder = GetEffectSortingOrder(center, 78);

        instance.PlaySfx("synergy_inferno", starThree ? 0.66f : 0.46f);

        if (!starThree)
        {
            if (bigFireSprites != null && bigFireSprites.Length > 0)
            {
                Vector3 safeDirection = breathDirection.sqrMagnitude > 0.01f ? breathDirection.normalized : Vector3.right;
                float breathAngle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
                float flameScale = Mathf.Max(1.15f, visualRadius * 0.82f);
                instance.StartCoroutine(instance.PlayAnimatedSprite(
                    BigFireResourcePath,
                    bigFireSprites,
                    center + safeDirection * 0.18f + new Vector3(0f, 0.1f, 0.02f),
                    new Vector3(flameScale * 1.28f, flameScale * 0.72f, 1f),
                    Color.white,
                    breathAngle,
                    sortingOrder + 12));
            }

            return;
        }

        if (starThree)
        {
            Sprite[] boardFlames = LoadGridRowSprites(FreeSmokeResourcePath, 2);
            if (boardFlames.Length > 0)
            {
                instance.StartCoroutine(instance.PlayAnimatedSprite(
                    FreeSmokeResourcePath,
                    boardFlames,
                    center + new Vector3(0f, 0.16f, -0.03f),
                    GetBoardAlignedScale(visualRadius * 1.35f),
                    fireColor,
                    BoardEffectTiltAngle,
                    sortingOrder + 900));
            }
        }

        if (infernoSprites != null && infernoSprites.Length > 0)
        {
            if (starThree)
            {
                instance.StartCoroutine(instance.PlayAnimatedSprite(
                    InfernoResourcePath,
                    infernoSprites,
                    center + new Vector3(0f, 0.08f, -0.01f),
                    Mathf.Max(0.9f, visualRadius * 0.72f),
                    fireColor,
                    0f,
                    sortingOrder));
            }
            else
            {
                Vector3 safeDirection = breathDirection.sqrMagnitude > 0.01f ? breathDirection.normalized : Vector3.right;
                float breathAngle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
                float step = Mathf.Clamp(visualRadius * 0.34f, 0.35f, 0.85f);
                Vector3 segmentScale = new Vector3(Mathf.Max(0.9f, visualRadius * 0.58f), Mathf.Max(0.55f, visualRadius * 0.34f), 1f);

                for (int i = -1; i <= 1; i++)
                {
                    Vector3 segmentPosition = center + safeDirection * (step * i) + new Vector3(0f, 0.05f, -0.01f);
                    instance.StartCoroutine(instance.PlayAnimatedSprite(
                        InfernoResourcePath,
                        infernoSprites,
                        segmentPosition,
                        segmentScale,
                        fireColor,
                        breathAngle,
                        sortingOrder + 1 + i));
                }
            }
        }

        if (starThree)
            ShakeCamera(0.28f, 0.08f);
    }

    // Skyfalltyrant★3用です。10x8盤面の外周と内側に青い炎を重ね、中央にフェニックスを出します。
    public static void PlaySkyfallBoardPerimeterFire(IReadOnlyList<Vector3> boardPositions, bool intense = false, float persistentPhoenixDuration = 0f)
    {
        if (boardPositions == null || boardPositions.Count == 0)
            return;

        EnsureInstance();
        EnsureSpritesLoaded();

        Sprite[] edgeFlames = LoadGridRowSprites(FreeSmokeResourcePath, 2);
        Sprite[] waveFlames = LoadGridRowSprites(FreeFireWaveResourcePath, 2);
        Sprite[] burstFlames = LoadGridRowSprites(FreeFlameResourcePath, 2);
        Sprite[] pillarFlames = LoadGridRowSprites(FreeFirePillarResourcePath, 2);
        if (edgeFlames.Length == 0 && waveFlames.Length == 0 && burstFlames.Length == 0 && pillarFlames.Length == 0)
            return;

        float minX = boardPositions.Min(position => position.x);
        float maxX = boardPositions.Max(position => position.x);
        float minY = boardPositions.Min(position => position.y);
        float maxY = boardPositions.Max(position => position.y);
        Vector3 center = Vector3.zero;
        for (int i = 0; i < boardPositions.Count; i++)
            center += boardPositions[i];

        center /= boardPositions.Count;
        float tolerance = 0.06f;
        List<float> boardColumns = BuildSortedAxisValues(boardPositions, true);
        List<float> boardRows = BuildSortedAxisValues(boardPositions, false);
        float scaleMultiplier = intense ? 1.72f : 1.14f;
        Color fireColor = intense ? new Color(0.28f, 0.95f, 1f, 1f) : new Color(0.18f, 0.78f, 1f, 1f);

        instance.PlaySfx("synergy_inferno", intense ? 0.7f : 0.5f);

        for (int i = 0; i < boardPositions.Count; i++)
        {
            Vector3 position = boardPositions[i];
            bool onLeft = Mathf.Abs(position.x - minX) <= tolerance;
            bool onRight = Mathf.Abs(position.x - maxX) <= tolerance;
            bool onBottom = Mathf.Abs(position.y - minY) <= tolerance;
            bool onTop = Mathf.Abs(position.y - maxY) <= tolerance;
            bool onEdge = onLeft || onRight || onBottom || onTop;
            int columnIndex = GetNearestAxisIndex(boardColumns, position.x);
            int rowIndex = GetNearestAxisIndex(boardRows, position.y);
            int patternHash = GetSkyfallScatterHash(columnIndex, rowIndex);
            Vector3 scatterOffset = GetSkyfallScatterOffset(patternHash, intense);

            if (onEdge && (onTop || onBottom) && edgeFlames.Length > 0)
            {
                float outward = onTop ? 0.18f : -0.18f;
                float angle = onTop ? 180f : 0f;
                Vector3 effectPosition = position + new Vector3(0f, outward, -0.05f);
                Vector3 effectScale = new Vector3(1.64f * scaleMultiplier, 0.9f * scaleMultiplier, 1f);
                instance.StartCoroutine(instance.PlayAnimatedSprite(
                    FreeSmokeResourcePath,
                    edgeFlames,
                    effectPosition,
                    effectScale,
                    fireColor,
                    angle + BoardEffectTiltAngle,
                    GetEffectSortingOrder(effectPosition, 1500)));
            }

            if (onEdge && (onLeft || onRight) && waveFlames.Length > 0)
            {
                float outward = onRight ? 0.18f : -0.18f;
                float angle = onRight ? 90f : -90f;
                Vector3 effectPosition = position + new Vector3(outward, 0f, -0.05f);
                Vector3 effectScale = new Vector3(1.45f * scaleMultiplier, 0.8f * scaleMultiplier, 1f);
                instance.StartCoroutine(instance.PlayAnimatedSprite(
                    FreeFireWaveResourcePath,
                    waveFlames,
                    effectPosition,
                    effectScale,
                    fireColor,
                    angle + BoardEffectTiltAngle,
                    GetEffectSortingOrder(effectPosition, 1500)));
            }

            if (!onEdge)
            {
                bool showBurst = intense ? patternHash % 5 != 1 : patternHash % 7 == 0;
                if (showBurst && burstFlames.Length > 0)
                {
                    Vector3 effectPosition = position + scatterOffset + new Vector3(0f, 0.03f, -0.08f);
                    instance.StartCoroutine(instance.PlayAnimatedSprite(
                        FreeFlameResourcePath,
                        burstFlames,
                        effectPosition,
                        GetBoardAlignedScale((intense ? 1.6f : 1.05f) * scaleMultiplier),
                        fireColor,
                        BoardEffectTiltAngle + GetSkyfallScatterAngle(patternHash),
                        GetEffectSortingOrder(effectPosition, 1450)));
                }

                bool showPillar = intense ? patternHash % 7 == 0 || patternHash % 11 == 3 : patternHash % 13 == 0;
                if (showPillar && pillarFlames.Length > 0)
                {
                    Vector3 effectPosition = position + scatterOffset * 0.5f + new Vector3(0f, 0.4f, 0.04f);
                    instance.StartCoroutine(instance.PlayAnimatedSprite(
                        FreeFirePillarResourcePath,
                        pillarFlames,
                        effectPosition,
                        Mathf.Max(1.35f, 1.34f * scaleMultiplier),
                        fireColor,
                        0f,
                        GetEffectSortingOrder(effectPosition, 1520)));
                }
            }
        }

        if (intense && persistentPhoenixDuration > 0f && infernoRainSprites != null && infernoRainSprites.Length > 0)
        {
            float boardWidth = maxX - minX;
            float phoenixScale = Mathf.Clamp(Mathf.Max(4.8f, boardWidth * 0.58f), 4.8f, 7.2f);
            instance.StartCoroutine(instance.PlayOneShotThenHoldAnimatedSprite(
                InfernoRainResourcePath,
                infernoRainSprites,
                center + new Vector3(0f, 0.62f, 0.12f),
                Vector3.one * phoenixScale,
                new Color(0.32f, 0.95f, 1f, 1f),
                0f,
                GetEffectSortingOrder(center, 2300),
                persistentPhoenixDuration,
                0.85f,
                0.08f));
        }

        if (intense)
            ShakeCamera(0.35f, 0.1f);
    }

    // 盤面上の座標を列・行として扱えるよう、重複をまとめた並びを作ります。
    private static List<float> BuildSortedAxisValues(IReadOnlyList<Vector3> positions, bool useX)
    {
        List<float> values = new List<float>();
        const float mergeTolerance = 0.05f;
        for (int i = 0; i < positions.Count; i++)
        {
            float value = useX ? positions[i].x : positions[i].y;
            bool found = false;
            for (int j = 0; j < values.Count; j++)
            {
                if (Mathf.Abs(values[j] - value) <= mergeTolerance)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                values.Add(value);
        }

        values.Sort();
        return values;
    }

    // 座標に一番近い列・行番号を探します。
    private static int GetNearestAxisIndex(IReadOnlyList<float> values, float value)
    {
        if (values == null || values.Count == 0)
            return 0;

        int bestIndex = 0;
        float bestDistance = Mathf.Abs(values[0] - value);
        for (int i = 1; i < values.Count; i++)
        {
            float distance = Mathf.Abs(values[i] - value);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // 行列から作る疑似ランダム値です。単純なi番目間引きで出る斜め筋を避けます。
    private static int GetSkyfallScatterHash(int columnIndex, int rowIndex)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + columnIndex * 73;
            hash = hash * 31 + rowIndex * 97;
            hash ^= columnIndex * rowIndex * 53;
            return Mathf.Abs(hash);
        }
    }

    // 同じマス中心に重なりすぎないよう、ほんの少しだけ炎の発生位置を散らします。
    private static Vector3 GetSkyfallScatterOffset(int patternHash, bool intense)
    {
        if (!intense)
            return Vector3.zero;

        float x = ((patternHash % 5) - 2) * 0.045f;
        float y = (((patternHash / 5) % 5) - 2) * 0.035f;
        return new Vector3(x, y, 0f);
    }

    // 炎の向きを少しばらけさせます。派手さを出しつつ、斜めの一本線には見せません。
    private static float GetSkyfallScatterAngle(int patternHash)
    {
        return ((patternHash % 7) - 3) * 4f;
    }

    // 範囲スキルの影響範囲を、地面に薄い円として表示します。
    public static void PlayAreaIndicator(BaseEntity caster, Vector3 center, float radiusInTiles, float duration = 0.8f, float intensity = 1f)
    {
        if (radiusInTiles <= 0.05f)
            return;

        EnsureInstance();
        UnitSkillType visualSkillType = caster != null ? caster.skillType : UnitSkillType.AreaDamage;
        Color color = GetSkillEffectColor(caster, visualSkillType);
        color.a = Mathf.Clamp(0.42f * Mathf.Max(0.35f, intensity), 0.18f, 0.72f);
        int row = GetEffectRow(caster != null ? caster.SkillTheme : SkillVisualTheme.Neutral, visualSkillType);
        instance.StartCoroutine(instance.PlayAreaIndicatorCoroutine(center, radiusInTiles, color, Mathf.Max(0.15f, duration), row));
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

    // 戦闘終了時に、残っている弾・範囲表示・一時エフェクトを即座に消します。
    public static void ClearBattleVisuals()
    {
        if (instance == null)
            return;

        instance.StopAllCoroutines();
        for (int i = instance.activeBattleVisuals.Count - 1; i >= 0; i--)
        {
            GameObject visual = instance.activeBattleVisuals[i];
            if (visual != null)
                Destroy(visual);
        }

        instance.activeBattleVisuals.Clear();
        if (instance.cameraShakeTarget != null)
            instance.cameraShakeTarget.transform.position = instance.cameraShakeOriginalPosition;

        instance.cameraShakeCoroutine = null;
        instance.cameraShakeTarget = null;
    }

    // 巨大スキル用の軽いカメラ揺れです。Plaguegeneralなどの迫力付けに使います。
    public static void ShakeCamera(float duration, float strength)
    {
        EnsureInstance();
        if (instance.cameraShakeCoroutine != null)
            instance.StopCoroutine(instance.cameraShakeCoroutine);

        instance.cameraShakeCoroutine = instance.StartCoroutine(instance.ShakeCameraCoroutine(duration, strength));
    }

    // Camera.mainを短時間だけ揺らし、終了時に元の位置へ戻します。
    private IEnumerator ShakeCameraCoroutine(float duration, float strength)
    {
        Camera targetCamera = Camera.main;
        if (targetCamera == null)
            yield break;

        cameraShakeTarget = targetCamera;
        cameraShakeOriginalPosition = targetCamera.transform.position;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            float fade = 1f - elapsed / safeDuration;
            Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, strength) * fade;
            targetCamera.transform.position = cameraShakeOriginalPosition + new Vector3(offset.x, offset.y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        targetCamera.transform.position = cameraShakeOriginalPosition;
        cameraShakeCoroutine = null;
        cameraShakeTarget = null;
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
        infernoSprites ??= LoadSprites(InfernoResourcePath);
        infernoRainSprites ??= LoadSprites(InfernoRainResourcePath);
        stormSprites ??= LoadSprites(StormResourcePath);
        lightningSkillSprites ??= LoadSprites(LightningSkillResourcePath)
            .Where(sprite => sprite.name.StartsWith("lightning_vfx3"))
            .ToArray();
        abyssSprites ??= LoadSprites(AbyssResourcePath);
        abyssCurseSprites ??= LoadSprites(AbyssCurseResourcePath);
        divineSprites ??= LoadSprites(DivineResourcePath);
        summonerSprites ??= LoadSprites(SummonerResourcePath);
        bigFireSprites ??= LoadSprites(BigFireResourcePath);
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

    // Free系素材は「64pxのコマが横に並び、色違いが縦に並ぶ」構成なので、
    // 使いたい色の行だけをランタイムで切り出して再生します。
    private static Sprite[] LoadGridRowSprites(string resourcePath, int rowFromTop, int cellSize = FreeEffectCellSize)
    {
        string cacheKey = $"{resourcePath}|{rowFromTop}|{cellSize}";
        if (effectRowSpriteCache.TryGetValue(cacheKey, out Sprite[] cachedSprites))
            return cachedSprites;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Sprite[] fallbackSprites = LoadSprites(resourcePath);
            effectRowSpriteCache[cacheKey] = fallbackSprites;
            return fallbackSprites;
        }

        int columns = Mathf.Max(1, texture.width / cellSize);
        int rows = Mathf.Max(1, texture.height / cellSize);
        int row = Mathf.Clamp(rowFromTop, 0, rows - 1);
        int y = (rows - 1 - row) * cellSize;
        string spritePrefix = resourcePath.Substring(resourcePath.LastIndexOf('/') + 1);

        Sprite[] rowSprites = new Sprite[columns];
        for (int x = 0; x < columns; x++)
        {
            Rect rect = new Rect(x * cellSize, y, cellSize, cellSize);
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"{spritePrefix}_row{row:00}_{x:000}";
            rowSprites[x] = sprite;
        }

        effectRowSpriteCache[cacheKey] = rowSprites;
        return rowSprites;
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

    // 新しいFree系素材のどの色行を使うかを、ユニットの雰囲気とスキル種別から決めます。
    private static int GetEffectRow(SkillVisualTheme theme, UnitSkillType skillType)
    {
        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.AllyHeal:
                return theme == SkillVisualTheme.Holy ? 4 : 3;
            case UnitSkillType.Stun:
                return 4;
            case UnitSkillType.Slow:
                return 2;
            case UnitSkillType.AttackSpeedBoost:
            case UnitSkillType.DamageBoost:
                return theme == SkillVisualTheme.Shadow || theme == SkillVisualTheme.Void ? 7 : 4;
        }

        switch (theme)
        {
            case SkillVisualTheme.Fire:
                return 0;
            case SkillVisualTheme.Ice:
                return 2;
            case SkillVisualTheme.Nature:
                return 3;
            case SkillVisualTheme.Holy:
                return 4;
            case SkillVisualTheme.Shadow:
                return 7;
            case SkillVisualTheme.Lightning:
                return 2;
            case SkillVisualTheme.Tech:
                return 6;
            case SkillVisualTheme.Void:
                return 8;
            default:
                return 1;
        }
    }

    // 通常攻撃のチーム色から、新素材の近い色行を選びます。
    private static int GetEffectRowForColor(Color color)
    {
        if (color.r > 0.85f && color.g < 0.45f)
            return 0;
        if (color.g > 0.7f && color.r < 0.55f)
            return 3;
        if (color.b > 0.7f && color.r < 0.55f)
            return 2;
        if (color.r > 0.65f && color.b > 0.65f)
            return 7;

        return 4;
    }

    // 盤面に貼り付く円形エフェクト用に、背景の傾きに合わせて縦を少し潰します。
    private static Vector3 GetBoardAlignedScale(float scale)
    {
        return new Vector3(scale, scale * BoardEffectYSquash, 1f);
    }

    // スキルに合わせて、主役になる新素材シートを選びます。
    private static string GetFreeSkillEffectPath(SkillVisualTheme theme, UnitSkillType skillType)
    {
        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.AllyHeal:
                return FreeLotusResourcePath;
            case UnitSkillType.AttackSpeedBoost:
            case UnitSkillType.DamageBoost:
                return FreeVortexResourcePath;
            case UnitSkillType.Stun:
                return FreeStarburstResourcePath;
            case UnitSkillType.Slow:
                return CircleGroundResourcePath;
            case UnitSkillType.AreaDamage:
                switch (theme)
                {
                    case SkillVisualTheme.Fire:
                        return FreeFlameResourcePath;
                    case SkillVisualTheme.Ice:
                        return CircleGroundResourcePath;
                    case SkillVisualTheme.Lightning:
                        return FreeStarburstResourcePath;
                    case SkillVisualTheme.Nature:
                    case SkillVisualTheme.Holy:
                        return FreeLotusResourcePath;
                    case SkillVisualTheme.Shadow:
                    case SkillVisualTheme.Void:
                        return FreeVortexResourcePath;
                    default:
                        return FreeStarburstResourcePath;
                }
            default:
                switch (theme)
                {
                    case SkillVisualTheme.Fire:
                        return FreeFlameResourcePath;
                    case SkillVisualTheme.Lightning:
                        return FreeStarburstResourcePath;
                    case SkillVisualTheme.Shadow:
                    case SkillVisualTheme.Void:
                        return FreeVortexResourcePath;
                    case SkillVisualTheme.Tech:
                        return FreeProjectileBurstResourcePath;
                    default:
                        return FreeSlashResourcePath;
                }
        }
    }

    // シナジーごとに、発動時の主役エフェクト素材を選びます。
    private static string GetFreeSynergyEffectPath(SynergyType type)
    {
        switch (type)
        {
            case SynergyType.Warrior:
            case SynergyType.Beast:
            case SynergyType.Frenzy:
                return FreeSlashResourcePath;
            case SynergyType.Ranger:
                return FreeProjectileBurstResourcePath;
            case SynergyType.Arcanist:
            case SynergyType.Shadow:
            case SynergyType.Wraith:
            case SynergyType.Abyss:
            case SynergyType.Alchemy:
                return FreeVortexResourcePath;
            case SynergyType.Guardian:
            case SynergyType.Machine:
            case SynergyType.Storm:
                return FreeStarburstResourcePath;
            case SynergyType.Inferno:
            case SynergyType.Apex:
                return FreeFlameResourcePath;
            case SynergyType.Frost:
                return CircleGroundResourcePath;
            case SynergyType.Divine:
            case SynergyType.Royal:
                return FreeLotusResourcePath;
            case SynergyType.Summoner:
                return CircleGroundResourcePath;
            default:
                return FreeVortexResourcePath;
        }
    }

    // シナジーごとの色行です。Resourcesのシート側が少ない行数の場合は読み込み側で自動的に丸めます。
    private static int GetSynergyEffectRow(SynergyType type)
    {
        switch (type)
        {
            case SynergyType.Warrior:
            case SynergyType.Royal:
            case SynergyType.Divine:
                return 4;
            case SynergyType.Ranger:
            case SynergyType.Frost:
            case SynergyType.Storm:
                return 2;
            case SynergyType.Arcanist:
            case SynergyType.Shadow:
            case SynergyType.Wraith:
            case SynergyType.Abyss:
                return 7;
            case SynergyType.Guardian:
            case SynergyType.Machine:
                return 6;
            case SynergyType.Beast:
            case SynergyType.Summoner:
            case SynergyType.Alchemy:
                return 3;
            case SynergyType.Inferno:
            case SynergyType.Frenzy:
            case SynergyType.Apex:
                return 0;
            default:
                return 1;
        }
    }

    // 地面に沿うリングと前面エフェクトを重ねて、単体効果でも見栄えが出るようにします。
    private void PlayLayeredFreeEffect(string mainPath, string groundPath, int row, Vector3 position, float scale, Color color, int sortingOrder, bool boardAlignedGround)
    {
        if (!string.IsNullOrEmpty(groundPath))
        {
            Sprite[] groundSprites = LoadGridRowSprites(groundPath, row);
            if (groundSprites.Length > 0)
            {
                Vector3 groundPosition = position + new Vector3(0f, -0.03f, -0.03f);
                Vector3 groundScale = boardAlignedGround ? GetBoardAlignedScale(scale * 1.18f) : Vector3.one * scale;
                StartCoroutine(PlayAnimatedSprite(groundPath, groundSprites, groundPosition, groundScale, color, BoardEffectTiltAngle, sortingOrder - 2));
            }
        }

        if (!string.IsNullOrEmpty(mainPath))
        {
            Sprite[] mainSprites = LoadGridRowSprites(mainPath, row);
            if (mainSprites.Length > 0)
            {
                bool mainIsGround = mainPath == CircleGroundResourcePath || mainPath == CircleImpactResourcePath;
                Vector3 mainScale = mainIsGround ? GetBoardAlignedScale(scale * 1.08f) : Vector3.one * scale;
                float angle = mainIsGround ? BoardEffectTiltAngle : 0f;
                StartCoroutine(PlayAnimatedSprite(mainPath, mainSprites, position + new Vector3(0f, 0.05f, 0f), mainScale, color, angle, sortingOrder));
            }
        }
    }

    // シールド以外のスキル演出を、新しいFree素材で派手に重ねます。
    private void PlaySkillVisualBurst(BaseEntity caster, UnitSkillType skillType, Vector3 position, float scale, Color color)
    {
        SkillVisualTheme theme = caster != null ? caster.SkillTheme : SkillVisualTheme.Neutral;
        int row = GetEffectRow(theme, skillType);
        string mainPath = GetFreeSkillEffectPath(theme, skillType);
        int sortingOrder = GetEffectSortingOrder(position, 72);
        float visualScale = Mathf.Max(0.75f, scale * 1.12f);

        PlayLayeredFreeEffect(mainPath, CircleGroundResourcePath, row, position, visualScale, color, sortingOrder, boardAlignedGround: true);

        // 範囲技は地面の影響範囲もひと目で分かるよう、追加で大きな円を重ねます。
        if (skillType == UnitSkillType.AreaDamage)
        {
            float radiusScale = caster != null ? Mathf.Clamp(caster.skillAreaRadius * 0.95f, 1.2f, 5.2f) : visualScale * 1.45f;
            Sprite[] circleSprites = LoadGridRowSprites(CircleImpactResourcePath, row);
            if (circleSprites.Length > 0)
                StartCoroutine(PlayAnimatedSprite(CircleImpactResourcePath, circleSprites, position + new Vector3(0f, -0.04f, -0.04f), GetBoardAlignedScale(radiusScale), color, BoardEffectTiltAngle, sortingOrder - 3));
        }

        // 回復・妨害・強化は既存の読みやすい補助エフェクトも薄く重ね、効果種別を判別しやすくします。
        Sprite[] accentSprites = null;
        string accentPath = null;
        switch (skillType)
        {
            case UnitSkillType.SelfHeal:
            case UnitSkillType.AllyHeal:
                accentSprites = healSprites;
                accentPath = HealResourcePath;
                break;
            case UnitSkillType.Stun:
                accentSprites = stunSprites;
                accentPath = StunResourcePath;
                break;
            case UnitSkillType.Slow:
                accentSprites = slowSprites;
                accentPath = SlowResourcePath;
                break;
            case UnitSkillType.AttackSpeedBoost:
            case UnitSkillType.DamageBoost:
                accentSprites = buffSprites;
                accentPath = BuffResourcePath;
                break;
        }

        if (accentSprites != null && accentSprites.Length > 0)
            StartCoroutine(PlayAnimatedSprite(accentPath, accentSprites, position, visualScale * 0.72f, color, 0f, sortingOrder + 1));
    }

    // シナジー発動時の演出を、発動種類と規模に合わせて大きく見せます。
    private void PlaySynergyVisualBurst(SynergyType type, Vector3 position, float scale, Color color)
    {
        int row = GetSynergyEffectRow(type);
        string mainPath = GetFreeSynergyEffectPath(type);
        int sortingOrder = GetEffectSortingOrder(position, 76);
        float visualScale = Mathf.Max(0.72f, scale * 1.22f);

        PlayLayeredFreeEffect(mainPath, CircleGroundResourcePath, row, position, visualScale, color, sortingOrder, boardAlignedGround: true);

        if (scale >= 1.35f || type == SynergyType.Inferno || type == SynergyType.Storm || type == SynergyType.Apex)
        {
            Sprite[] burstSprites = LoadGridRowSprites(CircleImpactResourcePath, row);
            if (burstSprites.Length > 0)
                StartCoroutine(PlayAnimatedSprite(CircleImpactResourcePath, burstSprites, position + new Vector3(0f, -0.02f, -0.05f), GetBoardAlignedScale(visualScale * 1.55f), color, BoardEffectTiltAngle, sortingOrder - 4));
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

    private static Sprite[] GetSynergySprites(SynergyType type)
    {
        switch (type)
        {
            case SynergyType.Inferno:
                return infernoSprites != null && infernoSprites.Length > 0 ? infernoSprites : areaSprites;
            case SynergyType.Frost:
                return slowSprites;
            case SynergyType.Storm:
                return stormSprites != null && stormSprites.Length > 0 ? stormSprites : impactSprites;
            case SynergyType.Abyss:
                return abyssSprites != null && abyssSprites.Length > 0 ? abyssSprites : damageBoostSprites;
            case SynergyType.Divine:
                return divineSprites != null && divineSprites.Length > 0 ? divineSprites : shieldSprites;
            case SynergyType.Frenzy:
                return powerSprites;
            case SynergyType.Royal:
                return damageBoostSprites;
            case SynergyType.Summoner:
                return summonerSprites != null && summonerSprites.Length > 0 ? summonerSprites : buffSprites;
            case SynergyType.Alchemy:
                return buffSprites;
            case SynergyType.Apex:
                return bigFireSprites != null && bigFireSprites.Length > 0 ? bigFireSprites : powerSprites;
            default:
                return powerSprites;
        }
    }

    private static string GetSynergyResourcePath(SynergyType type)
    {
        switch (type)
        {
            case SynergyType.Inferno:
                return InfernoResourcePath;
            case SynergyType.Frost:
                return SlowResourcePath;
            case SynergyType.Storm:
                return StormResourcePath;
            case SynergyType.Abyss:
                return AbyssResourcePath;
            case SynergyType.Divine:
                return DivineResourcePath;
            case SynergyType.Frenzy:
                return PowerResourcePath;
            case SynergyType.Royal:
                return DamageBoostResourcePath;
            case SynergyType.Summoner:
                return SummonerResourcePath;
            case SynergyType.Alchemy:
                return BuffResourcePath;
            case SynergyType.Apex:
                return BigFireResourcePath;
            default:
                return PowerResourcePath;
        }
    }

    private static Color GetSynergyEffectColor(SynergyType type)
    {
        switch (type)
        {
            case SynergyType.Inferno:
                return new Color(1f, 0.22f, 0.05f, 1f);
            case SynergyType.Frost:
                return new Color(0.35f, 0.9f, 1f, 1f);
            case SynergyType.Storm:
                return new Color(0.28f, 0.78f, 1f, 1f);
            case SynergyType.Abyss:
                return new Color(0.62f, 0.18f, 1f, 1f);
            case SynergyType.Divine:
                return new Color(1f, 0.95f, 0.35f, 1f);
            case SynergyType.Frenzy:
                return new Color(1f, 0.12f, 0.18f, 1f);
            case SynergyType.Royal:
                return new Color(1f, 0.7f, 0.18f, 1f);
            case SynergyType.Summoner:
                return new Color(0.35f, 1f, 0.58f, 1f);
            case SynergyType.Alchemy:
                return new Color(0.55f, 1f, 0.25f, 1f);
            default:
                return SynergyIconLibrary.GetColor(type);
        }
    }

    private static string GetSynergySfxName(SynergyType type)
    {
        switch (type)
        {
            case SynergyType.Inferno:
                return "synergy_inferno";
            case SynergyType.Frost:
                return "synergy_frost";
            case SynergyType.Storm:
                return "synergy_storm";
            case SynergyType.Abyss:
                return "synergy_abyss";
            case SynergyType.Divine:
                return "synergy_divine";
            case SynergyType.Frenzy:
                return "synergy_frenzy";
            case SynergyType.Royal:
                return "synergy_royal";
            case SynergyType.Summoner:
                return "synergy_summoner";
            case SynergyType.Alchemy:
                return "synergy_alchemy";
            default:
                return "skill_power";
        }
    }

    private static float GetSynergySfxVolume(SynergyType type)
    {
        return type == SynergyType.Inferno || type == SynergyType.Storm || type == SynergyType.Divine ? 0.62f : 0.52f;
    }

    // エフェクトの描画順をユニットと同じ基準で計算します。
    private static int GetEffectSortingOrder(Vector3 position, int offset)
    {
        return BaseEntity.CalculateSortingOrder(position, offset);
    }

    // 遠距離攻撃の弾と軌跡を再生します。
    private IEnumerator PlayRangedAttack(Vector3 source, Transform targetTransform, Vector3 fallbackDestination, Color teamColor, Action onImpact)
    {
        GameObject projectileObject = new GameObject("RangedAttackProjectile");
        RegisterBattleVisual(projectileObject);
        projectileObject.transform.position = source;

        SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
        projectileRenderer.sortingOrder = GetEffectSortingOrder(source, 45);
        projectileRenderer.sprite = GetProjectileOrbSprite();
        projectileRenderer.color = Color.Lerp(new Color(1f, 1f, 1f, 0.98f), teamColor, 0.4f);

        // 通常遠距離攻撃は槍状エフェクトではなく、球が飛ぶ見た目にします。
        Vector3 destination = fallbackDestination;
        Vector3 direction = destination - source;
        float distance = direction.magnitude;
        float duration = Mathf.Clamp(distance / 8.4f, 0.18f, 0.42f);
        LineRenderer trail = CreateTrail(projectileObject, teamColor, GetEffectSortingOrder(source, 44));

        // 弾を目的地まで補間移動させ、少し弧を描かせます。
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (targetTransform != null)
                destination = targetTransform.position + new Vector3(0f, 0.18f, 0f);

            Vector3 position = Vector3.Lerp(source, destination, progress);
            float arc = Mathf.Sin(progress * Mathf.PI) * 0.18f;
            position += Vector3.up * arc;
            projectileObject.transform.position = position;
            float pulse = 0.5f + Mathf.Sin(elapsed * 26f) * 0.5f;
            projectileObject.transform.localScale = Vector3.one * Mathf.Lerp(0.34f, 0.46f, pulse);
            projectileRenderer.sortingOrder = GetEffectSortingOrder(position, 45);
            if (trail != null)
                trail.sortingOrder = GetEffectSortingOrder(position, 44);

            UpdateTrail(trail, source, position, teamColor, progress);
            yield return null;
        }

        UnregisterBattleVisual(projectileObject);
        Destroy(projectileObject);
        onImpact?.Invoke();
        int row = GetEffectRowForColor(teamColor);
        StartCoroutine(PlayAnimatedSprite(FreeProjectileBurstResourcePath, LoadGridRowSprites(FreeProjectileBurstResourcePath, row), destination, 0.92f, teamColor, 0f, GetEffectSortingOrder(destination, 56)));
        StartCoroutine(PlayAnimatedSprite(CircleImpactResourcePath, LoadGridRowSprites(CircleImpactResourcePath, row), destination + new Vector3(0f, -0.04f, -0.03f), GetBoardAlignedScale(0.78f), teamColor, BoardEffectTiltAngle, GetEffectSortingOrder(destination, 55)));
    }

    // 近接攻撃の斬撃エフェクトと着弾エフェクトを再生します。
    private IEnumerator PlayMeleeAttack(Vector3 source, Vector3 destination, Color teamColor)
    {
        Vector3 direction = destination - source;
        float angle = direction.sqrMagnitude > 0.001f
            ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
            : 0f;

        int sortingOrder = GetEffectSortingOrder(destination, 55);
        int row = GetEffectRowForColor(teamColor);
        Sprite[] slashSprites = LoadGridRowSprites(FreeSlashResourcePath, row);
        yield return PlayAnimatedSprite(FreeSlashResourcePath, slashSprites.Length > 0 ? slashSprites : meleeSprites, destination, 1.12f, teamColor, angle, sortingOrder);
        StartCoroutine(PlayAnimatedSprite(CircleImpactResourcePath, LoadGridRowSprites(CircleImpactResourcePath, row), destination + new Vector3(0f, -0.04f, -0.02f), GetBoardAlignedScale(0.68f), teamColor, BoardEffectTiltAngle, sortingOrder));
        StartCoroutine(PlayAnimatedSprite(ImpactResourcePath, impactSprites, destination, 0.42f, teamColor, 0f, sortingOrder + 1));
    }

    // 遠距離攻撃の軌跡をLineRendererで作ります。
    private LineRenderer CreateTrail(GameObject parent, Color teamColor, int sortingOrder)
    {
        LineRenderer line = parent.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 0.03f;
        line.endWidth = 0.13f;
        line.sortingOrder = sortingOrder;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            line.material = new Material(shader);

        line.startColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0f);
        line.endColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.55f);
        return line;
    }

    // 遠距離攻撃の軌跡を、弾の現在位置に合わせて更新します。
    private void UpdateTrail(LineRenderer trail, Vector3 source, Vector3 head, Color teamColor, float progress)
    {
        if (trail == null)
            return;

        Vector3 tail = Vector3.Lerp(source, head, Mathf.Max(0f, progress - 0.14f));
        trail.SetPosition(0, tail);
        trail.SetPosition(1, head);

        float alpha = Mathf.Sin(progress * Mathf.PI);
        trail.startColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0f);
        trail.endColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.55f * alpha);
    }

    // ランタイムで白い発光球スプライトを作ります。元の槍状素材より、通常弾らしい見た目に使います。
    private static Sprite GetProjectileOrbSprite()
    {
        if (projectileOrbSprite != null)
            return projectileOrbSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GeneratedRangedAttackOrbTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float core = Mathf.Clamp01(1f - distance * 1.65f);
                float glow = Mathf.Clamp01(1f - distance);
                float alpha = Mathf.Clamp01(core * 1.15f + glow * glow * 0.45f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        projectileOrbSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        projectileOrbSprite.name = "GeneratedRangedAttackOrb";
        return projectileOrbSprite;
    }

    // 範囲表示用の白い円形スプライトをランタイム生成します。色はSpriteRenderer側で付けます。
    private static Sprite GetAreaIndicatorSprite()
    {
        if (areaIndicatorSprite != null)
            return areaIndicatorSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                float fill = normalized <= 1f ? 0.18f * Mathf.Clamp01(1f - normalized * 0.45f) : 0f;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(normalized - 0.94f) * 18f);
                float innerRing = Mathf.Clamp01(1f - Mathf.Abs(normalized - 0.62f) * 32f) * 0.35f;
                float alpha = Mathf.Clamp01(fill + ring * 0.85f + innerRing);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        areaIndicatorSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        areaIndicatorSprite.name = "GeneratedSkillAreaIndicator";
        return areaIndicatorSprite;
    }

    // 範囲表示を少し残してからフェードアウトさせます。
    private IEnumerator PlayAreaIndicatorCoroutine(Vector3 center, float radiusInTiles, Color color, float duration, int effectRow)
    {
        Sprite sprite = GetAreaIndicatorSprite();
        if (sprite == null)
            yield break;

        GameObject indicatorObject = new GameObject("SkillAreaIndicator");
        RegisterBattleVisual(indicatorObject);
        indicatorObject.transform.position = new Vector3(center.x, center.y, center.z - 0.02f);

        float diameter = Mathf.Max(0.65f, radiusInTiles * 2f);
        float spriteWidth = Mathf.Max(0.01f, sprite.bounds.size.x);
        float baseScale = diameter / spriteWidth;
        indicatorObject.transform.localScale = GetBoardAlignedScale(baseScale);
        indicatorObject.transform.rotation = Quaternion.Euler(0f, 0f, BoardEffectTiltAngle);

        SpriteRenderer renderer = indicatorObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 220;

        Sprite[] ringSprites = LoadGridRowSprites(CircleGroundResourcePath, effectRow);
        GameObject ringObject = null;
        SpriteRenderer ringRenderer = null;
        if (ringSprites.Length > 0)
        {
            ringObject = new GameObject("SkillAreaAnimatedRing");
            RegisterBattleVisual(ringObject);
            ringObject.transform.position = new Vector3(center.x, center.y - 0.02f, center.z - 0.04f);
            ringObject.transform.localScale = GetBoardAlignedScale(baseScale * 1.22f);
            ringObject.transform.rotation = Quaternion.Euler(0f, 0f, BoardEffectTiltAngle);
            ringRenderer = ringObject.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = ringSprites[0];
            ringRenderer.color = Color.Lerp(Color.white, color, 0.2f);
            ringRenderer.sortingOrder = 221;
        }

        float elapsed = 0f;
        float originalAlpha = color.a;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float fade = 1f - progress * progress;
            color.a = originalAlpha * fade;
            renderer.color = color;
            if (ringRenderer != null)
            {
                int frameIndex = Mathf.FloorToInt(elapsed * FrameRate) % ringSprites.Length;
                ringRenderer.sprite = ringSprites[frameIndex];
                Color ringColor = Color.Lerp(Color.white, color, 0.28f);
                ringColor.a = Mathf.Clamp01(originalAlpha * 1.25f * fade);
                ringRenderer.color = ringColor;
            }
            yield return null;
        }

        UnregisterBattleVisual(indicatorObject);
        Destroy(indicatorObject);
        if (ringObject != null)
        {
            UnregisterBattleVisual(ringObject);
            Destroy(ringObject);
        }
    }

    // Sprite配列を順番に表示して、短いアニメーションエフェクトとして再生します。
    private IEnumerator PlayAnimatedSprite(string name, Sprite[] sprites, Vector3 position, float scale, Color teamColor, float angle, int sortingOrder)
    {
        return PlayAnimatedSprite(name, sprites, position, Vector3.one * scale, teamColor, angle, sortingOrder);
    }

    // Sprite配列を任意の縦横比で表示します。地面に貼る円形演出はここで縦だけ潰して使います。
    private IEnumerator PlayAnimatedSprite(string name, Sprite[] sprites, Vector3 position, Vector3 scale, Color teamColor, float angle, int sortingOrder)
    {
        if (sprites == null || sprites.Length == 0)
            yield break;

        GameObject effectObject = new GameObject(name);
        RegisterBattleVisual(effectObject);
        effectObject.transform.position = position;
        effectObject.transform.localScale = scale;
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

        UnregisterBattleVisual(effectObject);
        Destroy(effectObject);
    }

    // 持続時間のある大技用です。出現アニメを一度だけ流し、見栄えの良い中盤コマだけで滞在感を作ります。
    private IEnumerator PlayOneShotThenHoldAnimatedSprite(
        string name,
        Sprite[] sprites,
        Vector3 position,
        Vector3 scale,
        Color teamColor,
        float angle,
        int sortingOrder,
        float duration,
        float frameRateMultiplier,
        float pulseAmount)
    {
        if (sprites == null || sprites.Length == 0)
            yield break;

        GameObject effectObject = new GameObject(name);
        RegisterBattleVisual(effectObject);
        effectObject.transform.position = position;
        effectObject.transform.localScale = scale;
        effectObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.Lerp(Color.white, teamColor, 0.18f);

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.1f, duration);
        float safeFrameRate = Mathf.Max(1f, FrameRate * Mathf.Max(0.1f, frameRateMultiplier));
        // フェニックスがはっきり表示されている26、27コマ目だけを待機ループに使います。
        int holdStart = Mathf.Clamp(25, 0, sprites.Length - 1);
        int holdEnd = Mathf.Clamp(26, holdStart, sprites.Length - 1);
        int holdFrameCount = Mathf.Max(1, holdEnd - holdStart + 1);
        bool holding = false;
        float holdElapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            int frameIndex;
            if (!holding)
            {
                frameIndex = Mathf.Min(holdEnd, Mathf.FloorToInt(elapsed * safeFrameRate));
                holding = frameIndex >= holdEnd;
            }
            else
            {
                holdElapsed += Time.deltaTime;
                frameIndex = holdStart + Mathf.FloorToInt(holdElapsed * safeFrameRate * 0.45f) % holdFrameCount;
            }

            float pulse = 1f + Mathf.Sin(elapsed * 5.4f) * Mathf.Max(0f, pulseAmount);
            renderer.sprite = sprites[frameIndex];
            renderer.sortingOrder = sortingOrder;
            effectObject.transform.localScale = scale * pulse;
            yield return null;
        }

        UnregisterBattleVisual(effectObject);
        Destroy(effectObject);
    }

    // 戦闘中だけの一時エフェクトを覚えておき、戦闘終了時にまとめて消せるようにします。
    private void RegisterBattleVisual(GameObject visual)
    {
        if (visual != null && !activeBattleVisuals.Contains(visual))
            activeBattleVisuals.Add(visual);
    }

    // 自然終了した一時エフェクトは、掃除対象リストから外します。
    private void UnregisterBattleVisual(GameObject visual)
    {
        if (visual != null)
            activeBattleVisuals.Remove(visual);
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
            case "synergy_inferno":
                return new[]
                {
                    "sfx/sfx_spell_immolation_a",
                    "sfx/sfx_spell_firefall",
                    "sfx/sfx_spell_phoenixfire"
                };
            case "synergy_frost":
                return new[]
                {
                    "sfx/sfx_spell_icepillar",
                    "sfx/sfx_f6_frostwyvern_attack_impact",
                    "sfx/sfx_f6_icebeetle_attack_impact"
                };
            case "synergy_storm":
                return new[]
                {
                    "sfx/sfx_spell_ghostlightning",
                    "sfx/sfx_neutral_stormatha_attack_impact",
                    "sfx/sfx_f2_stormkage_impact"
                };
            case "synergy_abyss":
                return new[]
                {
                    "sfx/sfx_spell_shadownova",
                    "sfx/sfx_spell_voidpulse",
                    "sfx/sfx_spell_graspofagony"
                };
            case "synergy_divine":
                return new[]
                {
                    "sfx/sfx_spell_divineblood",
                    "sfx/sfx_spell_heavenstrike",
                    "sfx/sfx_summonlegendary"
                };
            case "synergy_frenzy":
                return new[]
                {
                    "sfx/sfx_spell_diretidefrenzy",
                    "sfx/sfx_spell_warsurge",
                    "sfx/sfx_spell_attackbuff2"
                };
            case "synergy_royal":
                return new[]
                {
                    "sfx/sfx_victory_crest",
                    "sfx/sfx_division_crest_reveal",
                    "sfx/sfx_spell_lionheartblessing"
                };
            case "synergy_summoner":
                return new[]
                {
                    "sfx/sfx_spell_nethersummoning",
                    "sfx/sfx_summonlegendary",
                    "sfx/sfx_unit_deploy"
                };
            case "synergy_alchemy":
                return new[]
                {
                    "sfx/sfx_gold_reward_1",
                    "sfx/sfx_artifact_equip",
                    "sfx/sfx_spell_manavortex"
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
