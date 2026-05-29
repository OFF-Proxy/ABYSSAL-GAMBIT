using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AutoChessBossRush.Save;

// ゲーム全体の進行を管理する中心クラスです。
// 購入ユニットの生成、ベンチ管理、盤面配置、入れ替え、売却、スターアップ、戦闘開始/終了を担当します。
public class GameManager : Manager<GameManager>
{
    // ショップや敵生成で使うユニット一覧データです。
    public EntitiesDatabaseSO entitiesDatabase;

    // 味方、敵、ベンチ上ユニットをHierarchy上で整理するための親Transformです。
    public Transform team1Parent;
    public Transform team2Parent;
    public Transform benchParent;

    // 左右のベンチタイルをまとめた親Transformです。
    public Transform team1BenchTilesParent;
    public Transform team2BenchTilesParent;
    public Transform itemBenchTilesParent;
    public Transform itemBenchParent;

    // ベンチのスロット数、位置計算、ドラッグ判定用の設定です。
    public int benchSlotCount = 8;
    public Vector3 benchStartPosition = new Vector3(-6.5f, -3.5f, 0f);
    public float benchSlotSpacing = 1f;
    public float benchPickRadius = 0.9f;

    // アイテムベンチは自分ベンチのさらに左側へ、2列 x 8枠で作ります。
    public int itemBenchColumns = 2;
    public int itemBenchRows = 8;
    public float itemBenchColumnSpacing = 0.96f;
    public float itemBenchRowSpacing = 0.96f;
    public float itemBenchGapFromUnitBench = 1.45f;
    public float itemBenchLeftEdgeMargin = 0.75f;
    public float itemBenchPickRadius = 0.68f;
    public Vector3 itemIconScale = new Vector3(1.25f, 1.25f, 1f);
    public bool useCanvasItemBench = true;
    public bool spawnDebugItemsOnStart = false;

    // マウスホイールで盤面を見る時のカメラ設定です。
    public Camera boardCamera;
    public bool enableMouseWheelZoom = true;
    public float mouseWheelZoomSpeed = 4f;
    public float minCameraFieldOfView = 28f;
    public float maxCameraFieldOfView = 60f;
    public float minOrthographicSize = 3.5f;
    public float maxOrthographicSize = 8f;
    public float cameraZoomSmoothSpeed = 12f;
    public bool clampCameraToBackground = true;
    public Renderer cameraBoundsRenderer;
    public string cameraBoundsObjectName = "battlemap1_middleground";
    public Vector2 cameraBoundsPadding = new Vector2(0.05f, 0.05f);
    public bool enableMiddleMousePan = true;
    public float middleMousePanSpeed = 1f;

    // 他のクラスがゲーム状態の変化を受け取るためのイベントです。
    public Action OnRoundStart;
    public Action OnRoundEnd;
    public Action<BaseEntity> OnUnitDied;
    public Action OnRosterChanged;

    // 現在存在している味方、敵、ベンチ上ユニットのリストです。
    List<BaseEntity> team1Entities = new List<BaseEntity>();
    List<BaseEntity> team2Entities = new List<BaseEntity>();
    List<BaseEntity> benchEntities = new List<BaseEntity>();
    List<ItemInstance> itemBenchItems = new List<ItemInstance>();

    // どのベンチユニットが何番スロットにいるかを記録します。
    Dictionary<BaseEntity, int> benchSlotByEntity = new Dictionary<BaseEntity, int>();
    Dictionary<ItemInstance, int> itemBenchSlotByItem = new Dictionary<ItemInstance, int>();

    // 敵が死亡した時に渡すドロップ（コイン・アイテム）を、生成時に登録しておきます。
    Dictionary<BaseEntity, EnemyDrop> enemyDrops = new Dictionary<BaseEntity, EnemyDrop>();

    // ウェーブ開始時点で盤面にいた味方と、その初期Nodeを記録します。
    readonly List<BaseEntity> roundPlayerUnits = new List<BaseEntity>();
    readonly Dictionary<BaseEntity, Node> roundStartNodeByPlayerUnit = new Dictionary<BaseEntity, Node>();

    // 検証用ウェーブです。通常進行では序盤ウェーブの後ろに挟み、F8でも直接開始できます。
    public bool includeDebugTrainingWave = false;
    public bool enableDebugTrainingWaveHotkey = true;
    public KeyCode debugTrainingWaveHotkey = KeyCode.F8;
    public int debugTrainingDummyHealth = 300000;
    public float debugTrainingDummyMoveSpeed = 0.95f;

    // ウェーブ定義と、次に開始するウェーブ番号です。
    readonly List<WaveDefinition> waveDefinitions = new List<WaveDefinition>();
    readonly string[] bossRewardUnitIds =
    {
        "Snowchasermk",
        "Solfist",
        "Maehvmk"
    };
    readonly HashSet<string> unlockedBossRewardUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // R1-meta: 章ボス（章クリアで永続 roster に加わるユニット）の定義。BuildChapterRounds と整合します。
    // 章1の章ボス = 4-10 で出てくる "Legion"。章を増やす際はここに追加します。
    private static readonly Dictionary<int, string> ChapterBossUnitIds = new Dictionary<int, string>
    {
        { 1, "Legion" },
    };

    public static string GetChapterBossUnitId(int chapter)
    {
        return ChapterBossUnitIds.TryGetValue(chapter, out string id) ? id : null;
    }

    // ショップに出る最大コスト（E1）。序盤はコスト3まで。ボス撃破やイベントで段階的に解放します。
    public int baseMaxShopCost = 3;
    public int maxShopCostCap = 5;
    public int MaxAvailableShopCost { get; private set; } = 3;

    // イベントラウンドのボーナスゴールド額です。
    public int eventBonusGold = 8;

    // 現在のチャプター（E1）。今はチャプター1のみ。増やす時は BuildChapterRounds に分岐を足します。
    public int currentChapter = 1;

    // ステージリザルト用のスコア集計（雑魚機能ステージ制と連動）。
    private float currentStageStartTime;
    private int currentStageTrackedIndex = 1;
    private int stageScoreCombatClears;
    private int stageScoreMidBossClears;
    private int stageScoreBossClears;
    private bool hasPendingStageResult;
    private int pendingResultStage;
    private float pendingResultTime;
    private int pendingResultScore;
    private string pendingResultBreakdown = string.Empty;
    private bool pendingResultIsChapterClear;
    private int pendingResultBestScore;
    private bool pendingResultIsNewRecord;
    // チャプター総括用：各ステージのスコアとタイムを蓄積します。
    private readonly List<int> chapterStageScores = new List<int>();
    private readonly List<float> chapterStageTimes = new List<float>();
    private float chapterStartTime;

    // === オーグメント（E3） ===
    public readonly List<AugmentDefinition> OwnedAugments = new List<AugmentDefinition>();
    public readonly HashSet<string> ShownAugmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool augmentSelectionPending;

    // チーム共通バフ（オーグメント由来）。後段の戦闘コードが参照します。
    public float TeamAttackBonusPercent { get; private set; }
    public float TeamHPBonusPercent { get; private set; }
    public float TeamDamageReductionBonus { get; private set; }
    public float TeamMoveSpeedBonusPercent { get; private set; }
    public float TeamAttackSpeedBonusPercent { get; private set; }
    public int BenchSlotBonus { get; private set; }
    public int AugmentSynergyBonusWarrior { get; private set; }
    public int AugmentSynergyBonusRanger { get; private set; }
    public int AugmentSynergyBonusArcanist { get; private set; }
    public bool AugmentAllCostsUnlocked { get; private set; }
    public float ScoreMultiplier { get; private set; } = 1f;
    public int ExtraExpPerWaveClear { get; private set; }

    // 戦闘中のみ有効な augment 由来の追加シナジーカウント（毎戦闘開始時にランダムで再抽選）。
    public readonly Dictionary<SynergyType, int> AdditionalSynergyBonusThisCombat = new Dictionary<SynergyType, int>();
    // prism_one_revive を1戦闘1回までに制限するフラグ。
    private bool augmentPrismReviveUsedThisCombat;
    // silver_revive_3 はチャプター中に計3体まで。
    public int AugmentSilverRevivesSpentInChapter { get; private set; }

    int currentWaveIndex;
    bool gameOver;
    bool bossRewardSelectionPending;
    bool debugTrainingWaveActive;
    float roundStartTime;
    bool roundTimeoutResolved;
    const float RoundDamageRampStartTime = 15f;
    const float RoundDamageRampSecondStageTime = 45f;
    const float RoundHardLimitSeconds = 60f;

    // 現在戦闘中かどうか、ベンチ空き、盤面配置数、配置上限を外部から確認できます。
    public bool IsRoundInProgress { get; private set; }
    // ベンチの実効スロット数（オーグメントによる拡張を含む）。
    public int EffectiveBenchSlotCount => Mathf.Max(1, benchSlotCount + BenchSlotBonus);
    public bool HasBenchSpace => benchEntities.Count < EffectiveBenchSlotCount;
    public int PlacedTeam1Count => team1Entities.Count;
    public int PlacementLimit => PlayerData.Instance != null ? PlayerData.Instance.Level : 1;
    public float RoundElapsedTime => IsRoundInProgress ? Mathf.Max(0f, Time.time - roundStartTime) : 0f;
    public float RoundDamageMultiplier => GetRoundDamageMultiplier();

    // ホイール押し込みドラッグ中かどうかと、前フレームのマウス位置です。
    bool isMiddleMousePanning;
    Vector3 lastMiddleMousePosition;
    float targetCameraFieldOfView;
    float targetOrthographicSize;
    bool hasCameraZoomTarget;

    // シーン開始時に、カメラの現在倍率をズーム目標値として保存します。
    private void Start()
    {
        // 永続化層を最優先で起動し、章進捗・所持ボス仲間・ベストスコアにアクセス可能にします。
        // R1-meta / R1-score の保存呼び出し（章クリア時の RecordChapterResult / AddBossAlly）は
        // 各タスクで該当箇所に埋め込みます。
        SaveManager.EnsureExists();
        InitializeWaveDefinitions();
        MaxAvailableShopCost = Mathf.Clamp(baseMaxShopCost, 1, maxShopCostCap);
        // ステージ追跡を初期化します（最初のラウンドのステージ番号で開始）。
        currentStageStartTime = Time.unscaledTime;
        chapterStartTime = Time.unscaledTime;
        chapterStageScores.Clear();
        chapterStageTimes.Clear();
        currentStageTrackedIndex = (waveDefinitions.Count > 0 && waveDefinitions[0] != null) ? Mathf.Max(1, waveDefinitions[0].StageIndex) : 1;
        UpdateRoundProgressUi();
        EnsureItemBenchParents();
        SpawnDebugItemsIfNeeded();
        RepositionItemBenchItems();
        RefreshItemBenchCanvasUi();
        SynergyManager.EnsureExists().AttachGameManager(this);
        EnsureFightButtonPresentation();

        // オプションUI（音量・言語・速度・再挑戦）を起動時に用意し、PlayerPrefs設定を反映します。
        OptionsPanelUI.EnsureExists();

        // 獲得したオーグメントを画面右上に常時表示する HUD を用意します（最初は0個）。
        AugmentHudUI.EnsureExists().Refresh();

        // 手詰まり防止に、開始時にランダムなコスト1ユニットを1体付与します。
        GrantStartingUnit();

        // R1-meta: 過去章で倒したボス仲間がいれば、章開始時に編成画面を出します。
        TryShowChapterRoster();

        // 最初のラウンドがイベントなら消化します。
        TryStartEventRound();

        Camera targetCamera = GetBoardCamera();
        EnsureCameraZoomTarget(targetCamera);
        EnsureCameraBoundsRenderer();
        ClampCameraInsideBackground(targetCamera);
    }

    // 毎フレーム、ゲーム全体で必要な入力処理を確認します。
    private void Update()
    {
        Camera targetCamera = GetBoardCamera();

        // 毎フレーム、マウスホイールによる盤面ズームを確認します。
        HandleMouseWheelZoom(targetCamera);

        // 急に倍率を変えず、目標倍率へ滑らかに近づけます。
        ApplySmoothCameraZoom(targetCamera);

        // 毎フレーム、ホイール押し込みドラッグによる盤面カメラ移動を確認します。
        HandleMiddleMousePan(targetCamera);

        // ズームや移動後に、背景の外側が見えない位置へカメラを収めます。
        ClampCameraInsideBackground(targetCamera);

        // デバッグ用の耐久ダミーウェーブを、通常ウェーブ進行とは別にすぐ開始できます。
        if (enableDebugTrainingWaveHotkey && Input.GetKeyDown(debugTrainingWaveHotkey))
            StartDebugTrainingWave();

        // 耐久ユニット同士で戦闘が長引いた時、1分で必ず決着を付けます。
        HandleRoundTimeout();
    }

    // 戦闘時間に応じた与ダメージ倍率です。序盤は等倍、後半ほど一気に決着が付きやすくします。
    private float GetRoundDamageMultiplier()
    {
        if (!IsRoundInProgress)
            return 1f;

        float elapsed = RoundElapsedTime;
        if (elapsed <= RoundDamageRampStartTime)
            return 1f;

        if (elapsed < RoundDamageRampSecondStageTime)
        {
            float normalized = Mathf.InverseLerp(RoundDamageRampStartTime, RoundDamageRampSecondStageTime, elapsed);
            return Mathf.Lerp(1f, 4f, normalized * normalized);
        }

        if (elapsed < RoundHardLimitSeconds)
        {
            float normalized = Mathf.InverseLerp(RoundDamageRampSecondStageTime, RoundHardLimitSeconds, elapsed);
            return Mathf.Lerp(4f, 25f, normalized * normalized);
        }

        return 100f;
    }

    // FIGHTボタンの見た目アニメと、少し広めの透明クリック範囲を実行時に足します。
    private void EnsureFightButtonPresentation()
    {
        GameObject fightObject = GameObject.Find("FIGHT");
        if (fightObject == null)
            return;

        if (fightObject.GetComponent<TweenButtonFeedback>() == null)
        {
            TweenButtonFeedback feedback = fightObject.AddComponent<TweenButtonFeedback>();
            feedback.PlayAppear(0.1f);
        }

        RectTransform fightRect = fightObject.GetComponent<RectTransform>();
        if (fightRect == null || fightObject.transform.Find("ExpandedHitArea") != null)
            return;

        GameObject hitAreaObject = new GameObject("ExpandedHitArea", typeof(RectTransform), typeof(Image), typeof(Button));
        hitAreaObject.transform.SetParent(fightObject.transform, false);
        hitAreaObject.transform.SetAsFirstSibling();

        RectTransform hitAreaRect = hitAreaObject.GetComponent<RectTransform>();
        hitAreaRect.anchorMin = Vector2.zero;
        hitAreaRect.anchorMax = Vector2.one;
        hitAreaRect.offsetMin = new Vector2(-24f, -18f);
        hitAreaRect.offsetMax = new Vector2(24f, 18f);

        Image hitImage = hitAreaObject.GetComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitImage.raycastTarget = true;

        Button hitButton = hitAreaObject.GetComponent<Button>();
        hitButton.targetGraphic = hitImage;
        hitButton.onClick.AddListener(DebugFight);
    }

    // 60秒を超えても両軍が残っている場合は、残HP割合が高い側を勝ちにして戦闘を終了します。
    private void HandleRoundTimeout()
    {
        if (!IsRoundInProgress || roundTimeoutResolved || RoundElapsedTime < RoundHardLimitSeconds)
            return;

        roundTimeoutResolved = true;
        float playerHealthRatio = GetTeamRemainingHealthRatio(team1Entities, true);
        float enemyHealthRatio = GetTeamRemainingHealthRatio(team2Entities);
        Debug.Log($"Round time limit reached. Player HP ratio:{playerHealthRatio:0.00} Enemy HP ratio:{enemyHealthRatio:0.00}");

        if (!HasLivingPlayerBattleUnit())
        {
            TriggerGameOver();
            return;
        }

        if (!HasLivingEnemyBattleUnit() || playerHealthRatio >= enemyHealthRatio)
        {
            CompleteCurrentWave();
            return;
        }

        TriggerGameOver();
    }

    // 生存している盤面ユニットの「現在HP合計 / 最大HP合計」を返します。
    private float GetTeamRemainingHealthRatio(List<BaseEntity> entities, bool excludeSummons = false)
    {
        float totalCurrentHealth = 0f;
        float totalMaxHealth = 0f;
        for (int i = 0; i < entities.Count; i++)
        {
            BaseEntity entity = entities[i];
            if (entity == null || entity.IsDead || !entity.IsOnBoard || (excludeSummons && entity.IsSummonedUnit))
                continue;

            totalCurrentHealth += Mathf.Max(0, entity.CurrentHealth);
            totalMaxHealth += Mathf.Max(1, entity.MaxHealth);
        }

        return totalMaxHealth <= 0f ? 0f : totalCurrentHealth / totalMaxHealth;
    }

    // プレイヤー本体の生存判定です。Legionの亡霊などの召喚体だけが残っても敗北扱いにします。
    private bool HasLivingPlayerBattleUnit()
    {
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity entity = team1Entities[i];
            if (entity != null && entity.Team == Team.Team1 && entity.IsOnBoard && !entity.IsDead && !entity.IsSummonedUnit)
                return true;
        }

        return false;
    }

    // 敵側は一時召喚も戦場にいる敵として扱い、全て倒れたらウェーブクリアにします。
    private bool HasLivingEnemyBattleUnit()
    {
        for (int i = 0; i < team2Entities.Count; i++)
        {
            BaseEntity entity = team2Entities[i];
            if (entity != null && entity.Team == Team.Team2 && entity.IsOnBoard && !entity.IsDead)
                return true;
        }

        return false;
    }

    // ショップでこのユニットを買えるか確認します。
    public bool CanBuyEntity(EntitiesDatabaseSO.EntityData entityData)
    {
        if (IsLegionOnlySummonData(entityData))
            return false;

        if (HasOwnedStarThreeUnit(entityData.name))
            return false;

        // ベンチに空きがある、または購入直後にスターアップして枠が空くなら購入可能です。
        return HasBenchSpace || CanCompleteUpgradeWithPurchase(entityData.name);
    }

    // ショップ抽選にこのユニットを出してよいか返します。未選択のボス報酬ユニットは隠します。
    public bool IsEntityUnlockedForShop(EntitiesDatabaseSO.EntityData entityData)
    {
        if (string.IsNullOrEmpty(entityData.name))
            return true;

        if (IsLegionOnlySummonData(entityData))
            return false;

        if (HasOwnedStarThreeUnit(entityData.name))
            return false;

        if (!bossRewardUnitIds.Contains(entityData.name, StringComparer.OrdinalIgnoreCase))
            return true;

        return unlockedBossRewardUnitIds.Contains(entityData.name);
    }

    // 同名の★3ユニットを所有している間は、そのユニットをショップから外します。
    public bool HasOwnedStarThreeUnit(string unitId)
    {
        if (string.IsNullOrEmpty(unitId))
            return false;

        return team1Entities
            .Concat(benchEntities)
            .Any(entity => entity != null
                && entity.Team == Team.Team1
                && !entity.IsSummonedUnit
                && entity.StarLevel >= 3
                && string.Equals(entity.UnitId, unitId, StringComparison.OrdinalIgnoreCase));
    }

    // ショップ購入後に呼ばれ、ユニットをベンチへ生成します。
    public void OnEntityBought(EntitiesDatabaseSO.EntityData entityData)
    {
        if (!CanBuyEntity(entityData))
        {
            Debug.LogWarning("Bench is full. Cannot buy more units.");
            return;
        }

        BaseEntity newEntity = CreateBenchEntity(entityData, 1);
        if (newEntity == null)
            return;

        // 購入によって3体揃った場合はスターアップします。
        // 戦闘中は盤面のユニットを巻き込まず、ベンチ内だけで揃った時だけ合成します。
        ResolveUpgradesFor(newEntity, GetImmediateUpgradeScope());
        OnRosterChanged?.Invoke();
    }

    // 指定ユニットを味方ベンチへ生成します。購入とボス報酬の両方で使います。
    private BaseEntity CreateBenchEntity(EntitiesDatabaseSO.EntityData entityData, int starLevel)
    {
        if (entityData.prefab == null)
            return null;

        EnsureBenchParent();

        int slotIndex = GetFreeBenchSlot();

        // 購入・報酬ユニットは盤面ではなく、まずベンチ親の下に生成します。
        BaseEntity newEntity = Instantiate(entityData.prefab, benchParent);
        newEntity.InitializeIdentity(entityData.name, entityData.cost, starLevel);
        SynergyManager.AssignEntitySynergies(newEntity, entityData);

        benchEntities.Add(newEntity);
        if (slotIndex != -1)
            benchSlotByEntity[newEntity] = slotIndex;

        // ベンチ上ではNodeを占有せず、ベンチ座標だけを持たせます。
        newEntity.SetupOnBench(Team.Team1, slotIndex != -1 ? GetBenchPosition(slotIndex) : GetBenchPosition(0));
        return newEntity;
    }

    // ゲーム開始時に、ランダムなコスト1ユニットを1体ベンチへ付与します。所持金が尽きても戦えるようにする保険です。
    private void GrantStartingUnit()
    {
        if (entitiesDatabase == null || entitiesDatabase.allEntities == null)
            return;

        List<EntitiesDatabaseSO.EntityData> candidates = entitiesDatabase.allEntities
            .Where(data => data.prefab != null && data.cost == 1 && !IsLegionOnlySummonData(data))
            .ToList();
        if (candidates.Count == 0)
            return;

        EntitiesDatabaseSO.EntityData chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        if (CreateBenchEntity(chosen, 1) != null)
            OnRosterChanged?.Invoke();
    }

    // ショップに出る最大コストを1段階解放します（ボス撃破やイベントで呼びます）。
    public void UnlockNextShopCostTier()
    {
        int previous = MaxAvailableShopCost;
        MaxAvailableShopCost = Mathf.Min(maxShopCostCap, MaxAvailableShopCost + 1);
        if (MaxAvailableShopCost != previous)
            Debug.Log($"Shop max cost unlocked: {MaxAvailableShopCost}.");
    }

    // 指定チームから見た敵ユニットリストを返します。
    public List<BaseEntity> GetEntitiesAgainst(Team against)
    {
        if (against == Team.Team1)
            return team2Entities;
        else
            return team1Entities;
    }

    // シナジー計算用に、盤面上の味方ユニットだけを返します。ベンチと敵は含めません。
    public List<BaseEntity> GetPlayerBoardEntitiesForSynergy()
    {
        return team1Entities
            .Where(entity => entity != null && entity.Team == Team.Team1 && entity.IsOnBoard && !entity.IsSummonedUnit)
            .Distinct()
            .ToList();
    }

    // シナジー計算用に、指定チームの盤面ユニットだけを返します。召喚体はカウントしません。
    public List<BaseEntity> GetBoardEntitiesForSynergy(Team team)
    {
        List<BaseEntity> source = team == Team.Team1 ? team1Entities : team2Entities;
        return source
            .Where(entity => entity != null && entity.Team == team && entity.IsOnBoard && !entity.IsSummonedUnit)
            .Distinct()
            .ToList();
    }

    // このユニットをドラッグ可能か判定します。
    public bool CanDragEntity(BaseEntity entity)
    {
        if (entity == null || entity.Team != Team.Team1)
            return false;

        // 戦闘前なら味方ユニットを自由に動かせます。
        if (!IsRoundInProgress)
            return true;

        // 戦闘中はベンチ上のユニットだけ動かせます。
        return IsEntityOnBench(entity);
    }

    // ユニットがベンチ上にいるか確認します。
    public bool IsEntityOnBench(BaseEntity entity)
    {
        return entity != null && benchSlotByEntity.ContainsKey(entity);
    }

    // 手動で盤面Nodeへ置けるかを判定します。
    public bool CanPlaceEntityManually(BaseEntity entity, Node node)
    {
        if (entity == null || node == null)
            return false;

        // 戦闘中は盤面への投入や盤面移動は禁止です。
        if (IsRoundInProgress)
            return false;

        if (entity.Team != Team.Team1)
            return false;

        // GridManager側で、味方陣地内かどうかを確認します。
        if (!GridManager.Instance.CanManuallyPlace(entity.Team, node))
            return false;

        // 置き先に味方がいる場合、ベンチと盤面、または盤面同士なら入れ替え可能です。
        BaseEntity nodeOccupant = GetTeam1EntityAtNode(node);
        if (nodeOccupant != null && nodeOccupant != entity)
            return IsEntityOnBench(entity) || entity.IsOnBoard;

        // Nodeが他の何かに占有されていて、自分の現在Nodeでもない場合は置けません。
        if (node.IsOccupied && entity.CurrentNode != node)
            return false;

        // ベンチから盤面へ出す場合、レベルによる配置上限を確認します。
        if (!entity.IsOnBoard && PlacedTeam1Count >= PlacementLimit)
            return false;

        return true;
    }

    // 実際にユニットを盤面Nodeへ配置します。成功したらtrueです。
    public bool TryPlaceEntityManually(BaseEntity entity, Node node)
    {
        if (!CanPlaceEntityManually(entity, node))
            return false;

        // 置き先に味方がいる場合は入れ替え処理で完了します。
        if (TrySwapEntityWithBoardEntity(entity, node))
            return true;

        // 盤面上で別Nodeへ移動する時、元Nodeの占有を解除します。
        if (entity.CurrentNode != null && entity.CurrentNode != node)
            entity.CurrentNode.SetOccupied(false);

        // ベンチから出す場合はベンチ管理リストから外し、盤面親へ移します。
        if (benchEntities.Remove(entity))
        {
            benchSlotByEntity.Remove(entity);
            entity.transform.SetParent(team1Parent, true);
        }

        if (!team1Entities.Contains(entity))
            team1Entities.Add(entity);

        // BaseEntity側で位置、Node占有、HPバーなどを整えます。
        entity.Setup(entity.Team, node);
        OnRosterChanged?.Invoke();
        return true;
    }

    // 盤面Nodeにいる味方と、ドラッグ中ユニットを入れ替えられるか試します。
    private bool TrySwapEntityWithBoardEntity(BaseEntity movingEntity, Node boardNode)
    {
        if (movingEntity == null || boardNode == null)
            return false;

        BaseEntity boardEntity = GetTeam1EntityAtNode(boardNode);
        if (boardEntity == null || boardEntity == movingEntity)
            return false;

        // ドラッグ元がベンチなら「ベンチと盤面」の入れ替えです。
        if (benchSlotByEntity.TryGetValue(movingEntity, out int originalBenchSlot))
            return SwapBenchEntityWithBoardEntity(movingEntity, boardEntity, originalBenchSlot, boardNode);

        // ドラッグ元も盤面なら「盤面同士」の入れ替えです。
        return SwapBoardEntityWithBoardEntity(movingEntity, boardEntity, boardNode);
    }

    // ベンチのユニットと盤面のユニットを入れ替えます。
    private bool SwapBenchEntityWithBoardEntity(BaseEntity benchEntity, BaseEntity boardEntity, int originalBenchSlot, Node boardNode)
    {
        if (benchEntity == null || boardEntity == null || boardNode == null)
            return false;

        EnsureBenchParent();

        // 盤面側ユニットをベンチへ戻すため、現在Nodeの占有を解除します。
        if (boardEntity.CurrentNode != null)
            boardEntity.CurrentNode.SetOccupied(false);

        team1Entities.Remove(boardEntity);
        if (!benchEntities.Contains(boardEntity))
            benchEntities.Add(boardEntity);

        benchSlotByEntity[boardEntity] = originalBenchSlot;
        boardEntity.transform.SetParent(benchParent, true);
        boardEntity.SetupOnBench(boardEntity.Team, GetBenchPosition(originalBenchSlot));

        // ベンチ側ユニットを盤面へ移動します。
        benchEntities.Remove(benchEntity);
        benchSlotByEntity.Remove(benchEntity);
        benchEntity.transform.SetParent(team1Parent, true);
        if (!team1Entities.Contains(benchEntity))
            team1Entities.Add(benchEntity);

        benchEntity.Setup(benchEntity.Team, boardNode);
        OnRosterChanged?.Invoke();
        return true;
    }

    // 盤面上の味方ユニット同士の位置を入れ替えます。
    private bool SwapBoardEntityWithBoardEntity(BaseEntity movingEntity, BaseEntity targetEntity, Node targetNode)
    {
        if (movingEntity == null || targetEntity == null || targetNode == null)
            return false;

        Node originalNode = movingEntity.CurrentNode;
        if (originalNode == null)
            return false;

        // 一度両方のNodeを空にしてから、それぞれを逆のNodeへSetupします。
        originalNode.SetOccupied(false);
        targetNode.SetOccupied(false);

        targetEntity.Setup(targetEntity.Team, originalNode);
        movingEntity.Setup(movingEntity.Team, targetNode);

        OnRosterChanged?.Invoke();
        return true;
    }

    // 指定ベンチスロットにユニットを置けるか判定します。
    public bool CanPlaceEntityOnBench(BaseEntity entity, int slotIndex)
    {
        if (entity == null || entity.Team != Team.Team1)
            return false;

        if (slotIndex < 0 || slotIndex >= EffectiveBenchSlotCount)
            return false;

        // 戦闘中は盤面ユニットをベンチへ戻せません。ベンチ内移動だけ許可します。
        if (IsRoundInProgress && !IsEntityOnBench(entity))
            return false;

        BaseEntity occupant = GetBenchEntityAtSlot(slotIndex);
        if (occupant == null || occupant == entity)
            return true;

        // 置き先に誰かいる場合も、ベンチ同士または盤面とベンチなら入れ替え可能です。
        return IsEntityOnBench(entity) || entity.IsOnBoard;
    }

    // 実際にベンチへ配置します。空きなら移動、埋まっていれば入れ替えます。
    public bool TryPlaceEntityOnBench(BaseEntity entity, int slotIndex)
    {
        if (!CanPlaceEntityOnBench(entity, slotIndex))
            return false;

        EnsureBenchParent();
        EnsureBenchTileParents();

        BaseEntity occupant = GetBenchEntityAtSlot(slotIndex);
        if (occupant != null && occupant != entity && TrySwapEntityWithBenchEntity(entity, occupant, slotIndex))
            return true;

        // 盤面からベンチへ戻す場合、Node占有と盤面リストから外します。
        if (entity.CurrentNode != null)
        {
            entity.CurrentNode.SetOccupied(false);
            team1Entities.Remove(entity);
        }

        if (!benchEntities.Contains(entity))
            benchEntities.Add(entity);

        benchSlotByEntity[entity] = slotIndex;
        entity.transform.SetParent(benchParent, true);
        entity.SetupOnBench(entity.Team, GetBenchPosition(slotIndex));
        OnRosterChanged?.Invoke();
        return true;
    }

    // ベンチ上のユニットと、ドラッグ中ユニットを入れ替えられるか試します。
    private bool TrySwapEntityWithBenchEntity(BaseEntity movingEntity, BaseEntity targetBenchEntity, int targetSlot)
    {
        if (movingEntity == null || targetBenchEntity == null || movingEntity == targetBenchEntity)
            return false;

        EnsureBenchParent();
        EnsureBenchTileParents();

        // ドラッグ元がベンチならベンチ同士、盤面なら盤面とベンチの入れ替えです。
        if (benchSlotByEntity.TryGetValue(movingEntity, out int originalBenchSlot))
            return SwapBenchEntityWithBenchEntity(movingEntity, targetBenchEntity, originalBenchSlot, targetSlot);

        return SwapBoardEntityWithBenchEntity(movingEntity, targetBenchEntity, targetSlot);
    }

    // ベンチ上のユニット同士を入れ替えます。
    private bool SwapBenchEntityWithBenchEntity(BaseEntity movingEntity, BaseEntity targetEntity, int originalSlot, int targetSlot)
    {
        if (!benchSlotByEntity.ContainsKey(targetEntity))
            return false;

        benchSlotByEntity[movingEntity] = targetSlot;
        benchSlotByEntity[targetEntity] = originalSlot;

        movingEntity.SetupOnBench(movingEntity.Team, GetBenchPosition(targetSlot));
        targetEntity.SetupOnBench(targetEntity.Team, GetBenchPosition(originalSlot));

        OnRosterChanged?.Invoke();
        return true;
    }

    // 盤面ユニットとベンチユニットを入れ替えます。
    private bool SwapBoardEntityWithBenchEntity(BaseEntity movingEntity, BaseEntity targetBenchEntity, int targetSlot)
    {
        Node originalNode = movingEntity.CurrentNode;
        if (originalNode == null)
            return false;

        originalNode.SetOccupied(false);

        // 盤面側ユニットをベンチへ移します。
        team1Entities.Remove(movingEntity);
        if (!benchEntities.Contains(movingEntity))
            benchEntities.Add(movingEntity);

        benchSlotByEntity[movingEntity] = targetSlot;
        movingEntity.transform.SetParent(benchParent, true);
        movingEntity.SetupOnBench(movingEntity.Team, GetBenchPosition(targetSlot));

        // ベンチ側ユニットを元の盤面Nodeへ出します。
        benchEntities.Remove(targetBenchEntity);
        benchSlotByEntity.Remove(targetBenchEntity);
        targetBenchEntity.transform.SetParent(team1Parent, true);
        if (!team1Entities.Contains(targetBenchEntity))
            team1Entities.Add(targetBenchEntity);

        targetBenchEntity.Setup(targetBenchEntity.Team, originalNode);

        OnRosterChanged?.Invoke();
        return true;
    }

    // ワールド座標から、指定チームのベンチスロット番号を取得します。
    public int GetBenchSlotAtWorldPosition(Team team, Vector3 worldPosition)
    {
        EnsureBenchTileParents();

        Transform benchTilesParent = team == Team.Team1 ? team1BenchTilesParent : team2BenchTilesParent;
        if (benchTilesParent == null)
            return -1;

        int closestSlot = -1;
        float closestDistance = benchPickRadius * benchPickRadius;
        for (int i = 0; i < Mathf.Min(EffectiveBenchSlotCount, benchTilesParent.childCount); i++)
        {
            float distance = (benchTilesParent.GetChild(i).position - worldPosition).sqrMagnitude;
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestSlot = i;
            }
        }

        return closestSlot;
    }

    // ワールド座標からベンチタイル自体を取得します。
    public Tile GetBenchTileAtWorldPosition(Team team, Vector3 worldPosition)
    {
        int slotIndex = GetBenchSlotAtWorldPosition(team, worldPosition);
        return GetBenchTileAtSlot(team, slotIndex);
    }

    // ベンチスロット番号からTileを取得します。Tileが無ければ追加します。
    public Tile GetBenchTileAtSlot(Team team, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= EffectiveBenchSlotCount)
            return null;

        EnsureBenchTileParents();

        Transform benchTilesParent = team == Team.Team1 ? team1BenchTilesParent : team2BenchTilesParent;
        if (benchTilesParent == null || slotIndex >= benchTilesParent.childCount)
            return null;

        Tile tile = benchTilesParent.GetChild(slotIndex).GetComponent<Tile>();
        if (tile == null)
            tile = benchTilesParent.GetChild(slotIndex).gameObject.AddComponent<Tile>();

        // GridManager側で、ベンチ用の色とホバー画像を設定します。
        if (tile != null && GridManager.Instance != null)
            GridManager.Instance.ConfigureBenchTile(tile, team);

        return tile;
    }

    // アイテムを指定ユニットへ装備します。成功したらアイテムベンチ上のGameObjectは消します。
    public bool TryEquipItemToEntity(ItemInstance itemInstance, BaseEntity targetEntity)
    {
        if (itemInstance == null || itemInstance.Data == null || targetEntity == null)
            return false;

        if (targetEntity.Team != Team.Team1 || !targetEntity.HasItemSpace)
            return false;

        if (!targetEntity.TryEquipItem(itemInstance.Data))
            return false;

        itemBenchItems.Remove(itemInstance);
        itemBenchSlotByItem.Remove(itemInstance);
        Destroy(itemInstance.gameObject);
        AttackEffectPlayer.PlayUiSfx("item_equip");
        RefreshItemBenchCanvasUi();
        OnRosterChanged?.Invoke();
        return true;
    }

    // アイテムベンチ上の指定スロットへアイテムを置けるか確認します。
    public bool CanPlaceItemOnBench(ItemInstance itemInstance, int slotIndex)
    {
        if (itemInstance == null)
            return false;

        if (slotIndex < 0 || slotIndex >= ItemBenchSlotCount)
            return false;

        ItemInstance occupant = GetItemBenchItemAtSlot(slotIndex);
        return occupant == null || occupant == itemInstance;
    }

    // アイテムベンチ上の指定スロットへアイテムを移動します。
    public bool TryPlaceItemOnBench(ItemInstance itemInstance, int slotIndex)
    {
        if (!CanPlaceItemOnBench(itemInstance, slotIndex))
            return false;

        EnsureItemBenchParents();
        if (!itemBenchItems.Contains(itemInstance))
            itemBenchItems.Add(itemInstance);

        itemBenchSlotByItem[itemInstance] = slotIndex;
        itemInstance.transform.SetParent(itemBenchParent, true);
        itemInstance.transform.position = GetItemBenchPosition(slotIndex);
        itemInstance.transform.localScale = itemIconScale;
        itemInstance.SetSlotIndex(slotIndex);
        itemInstance.SetWorldVisible(!useCanvasItemBench);
        RefreshItemBenchCanvasUi();
        return true;
    }

    // ワールド座標からアイテムベンチのスロット番号を取得します。
    public int GetItemBenchSlotAtWorldPosition(Vector3 worldPosition)
    {
        EnsureItemBenchParents();
        if (itemBenchTilesParent == null)
            return -1;

        int closestSlot = -1;
        float closestDistance = itemBenchPickRadius * itemBenchPickRadius;
        for (int i = 0; i < Mathf.Min(ItemBenchSlotCount, itemBenchTilesParent.childCount); i++)
        {
            float distance = (itemBenchTilesParent.GetChild(i).position - worldPosition).sqrMagnitude;
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestSlot = i;
            }
        }

        return closestSlot;
    }

    // アイテムベンチのスロット番号からTileを返します。
    public Tile GetItemBenchTileAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= ItemBenchSlotCount)
            return null;

        EnsureItemBenchParents();
        if (itemBenchTilesParent == null || slotIndex >= itemBenchTilesParent.childCount)
            return null;

        Tile tile = itemBenchTilesParent.GetChild(slotIndex).GetComponent<Tile>();
        if (tile == null)
            tile = itemBenchTilesParent.GetChild(slotIndex).gameObject.AddComponent<Tile>();

        if (GridManager.Instance != null)
            GridManager.Instance.ConfigureItemBenchTile(tile);

        return tile;
    }

    // 外れたアイテムをベンチの空き枠へ戻します。空きがなければ近くに置きます。
    public ItemInstance ReturnItemToBench(ItemData itemData)
    {
        if (itemData == null)
            return null;

        EnsureItemBenchParents();
        int slotIndex = GetFreeItemBenchSlot();
        ItemInstance itemInstance = CreateItemInstance(itemData, slotIndex);
        if (slotIndex >= 0)
        {
            TryPlaceItemOnBench(itemInstance, slotIndex);
        }
        else
        {
            PlaceOverflowItemOnBench(itemInstance);
        }

        if (itemInstance != null)
            itemInstance.SetWorldVisible(!useCanvasItemBench || !itemBenchSlotByItem.ContainsKey(itemInstance));

        RefreshItemBenchCanvasUi();
        return itemInstance;
    }

    // Canvas版アイテムベンチが現在使う縦スロット数です。
    public int ItemBenchRowsForUi => Mathf.Max(1, itemBenchRows);

    // Canvas版アイテムベンチが扱う横列数です。1列表示から展開できるよう、最低2列を確保します。
    public int ItemBenchColumnsForUi => Mathf.Max(2, itemBenchColumns);

    // UIから、指定スロットに入っているアイテムを取得します。
    public ItemInstance GetItemBenchItemAtSlotForUi(int slotIndex)
    {
        return GetItemBenchItemAtSlot(slotIndex);
    }

    // 指定列より右側にアイテムがあるかを返します。展開ボタン表示に使います。
    public bool HasItemBenchItemsBeyondColumn(int columnIndex)
    {
        int rows = Mathf.Max(1, itemBenchRows);
        int firstHiddenSlot = (columnIndex + 1) * rows;
        foreach (KeyValuePair<ItemInstance, int> pair in itemBenchSlotByItem)
        {
            if (pair.Key != null && pair.Value >= firstHiddenSlot)
                return true;
        }

        return itemBenchItems.Count(item => item != null) > firstHiddenSlot;
    }

    // 複数アイテムをまとめてアイテムベンチへ戻します。
    private void ReturnItemsToBench(IEnumerable<ItemData> items)
    {
        if (items == null)
            return;

        foreach (ItemData item in items)
            ReturnItemToBench(item);
    }

    // ユニットが死亡した時にBaseEntityから呼ばれます。
    public void UnitDead(BaseEntity entity)
    {
        if (entity == null)
            return;

        bool playerUnitDiedDuringWave = IsRoundInProgress && entity.Team == Team.Team1 && !entity.IsSummonedUnit;

        team1Entities.Remove(entity);
        team2Entities.Remove(entity);

        // 敵ユニットのドロップ報酬（コイン・アイテム）を渡します。
        if (entity.Team == Team.Team2 && enemyDrops.TryGetValue(entity, out EnemyDrop drop))
        {
            enemyDrops.Remove(entity);
            if (drop.Coins > 0)
            {
                PlayerData.Instance?.AddMoney(drop.Coins);
                Debug.Log($"Enemy dropped {drop.Coins} gold.");
            }
            if (drop.Item)
            {
                GrantRandomItemFromSynergy();
                Debug.Log("Enemy dropped a random item.");
            }
        }

        OnUnitDied?.Invoke(entity);

        // 敵と召喚体は破棄します。味方本体は次ウェーブで復活させるため、一時退場に留めます。
        if (playerUnitDiedDuringWave)
            entity.WaitForWaveReviveAfterDeathAnimation();
        else
            entity.DestroyAfterDeathAnimation();

        TryEndRound();
        OnRosterChanged?.Invoke();
    }

    // FIGHTボタンから呼ばれ、次のウェーブを固定配置で生成して戦闘を始めます。
    public void DebugFight()
    {
        if (IsRoundInProgress)
            return;

        if (bossRewardSelectionPending)
        {
            Debug.LogWarning("Choose a boss reward before starting the next fight.");
            return;
        }

        if (gameOver)
        {
            Debug.LogWarning("Game over. Restart the scene to try again.");
            return;
        }

        InitializeWaveDefinitions();
        if (currentWaveIndex >= waveDefinitions.Count)
        {
            Debug.Log("All waves are already cleared.");
            UpdateRoundProgressUi();
            return;
        }

        // イベントラウンドは戦闘せず、報酬を消化して次のラウンドへ進めます。
        if (IsCurrentRoundEvent())
        {
            TryStartEventRound();
            return;
        }

        if (team1Entities.Count == 0)
        {
            Debug.LogWarning("Place at least one unit before starting a fight.");
            return;
        }

        ClearEnemyUnits();
        SnapshotPlayerBoardUnits();
        debugTrainingWaveActive = false;
        SpawnWaveEnemies(waveDefinitions[currentWaveIndex]);

        if (team2Entities.Count == 0)
        {
            Debug.LogWarning($"Wave {currentWaveIndex + 1} could not spawn any enemies.");
            return;
        }

        IsRoundInProgress = true;
        roundStartTime = Time.time;
        roundTimeoutResolved = false;
        SynergyManager.EnsureExists().ApplyBattleStartSynergies();
        ApplyBattleStartAugmentEffects();
        AttackEffectPlayer.PlayUiSfx("fight_start");
        UpdateRoundProgressUi();
        OnRoundStart?.Invoke();
    }

    // 検証用の耐久ダミーだけを出すウェーブを開始します。通常ウェーブ番号は進めません。
    public void StartDebugTrainingWave()
    {
        if (IsRoundInProgress)
            return;

        if (bossRewardSelectionPending)
        {
            Debug.LogWarning("Choose a boss reward before starting a debug training wave.");
            return;
        }

        if (gameOver)
        {
            Debug.LogWarning("Game over. Restart the scene to try again.");
            return;
        }

        if (team1Entities.Count == 0)
        {
            Debug.LogWarning("Place at least one unit before starting a debug training wave.");
            return;
        }

        ClearEnemyUnits();
        SnapshotPlayerBoardUnits();
        debugTrainingWaveActive = true;
        SpawnWaveEnemies(CreateDebugTrainingWaveDefinition());

        if (team2Entities.Count == 0)
        {
            debugTrainingWaveActive = false;
            Debug.LogWarning("Debug training wave could not spawn any enemies.");
            return;
        }

        IsRoundInProgress = true;
        roundStartTime = Time.time;
        roundTimeoutResolved = false;
        SynergyManager.EnsureExists().ApplyBattleStartSynergies();
        AttackEffectPlayer.PlayUiSfx("fight_start");
        UpdateRoundProgressUi();
        OnRoundStart?.Invoke();
    }

    // チャプター制（E1）。現在のチャプターのラウンド列を構築します。列は左から、行は下から数えます。
    private void InitializeWaveDefinitions()
    {
        if (waveDefinitions.Count > 0)
            return;

        BuildChapterRounds(currentChapter);
    }

    // 指定チャプターのラウンド列を組み立てます。チャプターを増やす時はここに分岐を足します。
    private void BuildChapterRounds(int chapter)
    {
        switch (chapter)
        {
            default:
                BuildChapter1Rounds();
                break;
        }
    }

    // ステージ番号とステージ内ラウンド番号を WaveDefinition に持たせる小ヘルパー群です。
    private WaveDefinition StagedCombat(int stage, int roundInStage, params WaveEnemyPlacement[] placements)
    {
        var d = new WaveDefinition(placements);
        d.StageIndex = stage;
        d.RoundInStage = roundInStage;
        return d;
    }

    private WaveDefinition StagedMidBoss(int stage, int roundInStage, params WaveEnemyPlacement[] placements)
    {
        var d = new WaveDefinition(placements);
        d.StageIndex = stage;
        d.RoundInStage = roundInStage;
        d.IsMidBossWave = true;
        return d;
    }

    private WaveDefinition StagedBoss(int stage, int roundInStage, params WaveEnemyPlacement[] placements)
    {
        var d = new WaveDefinition(true, placements);
        d.StageIndex = stage;
        d.RoundInStage = roundInStage;
        return d;
    }

    private WaveDefinition StagedEvent(int stage, int roundInStage, WaveEventType eventType)
    {
        var d = new WaveDefinition(eventType);
        d.StageIndex = stage;
        d.RoundInStage = roundInStage;
        return d;
    }

    // チャプター1（全33ラウンド）。4ステージ構成: Stage1=雑魚3 / Stage2=10 / Stage3=10 / Stage4=10(4-10が章ボス)。
    // 中ボス: 2-5, 2-10, 3-5, 3-10, 4-7, 4-8, 4-9（撃破でコスト上限が解放、報酬なし）。章ボス: 4-10（報酬選択あり）。
    private void BuildChapter1Rounds()
    {
        // === Stage 1: 雑魚戦（Zyx）3ラウンド。ドロップで序盤の資源を確保。 ===
        // 1-1: 雑魚1体（コイン3ドロップ）
        waveDefinitions.Add(StagedCombat(1, 1,
            new WaveEnemyPlacement("Zyx", 1, 7, 5, dropCoins: 3)));
        // 1-2: 雑魚2体（一体はアイテム、一体はコイン3）
        waveDefinitions.Add(StagedCombat(1, 2,
            new WaveEnemyPlacement("Zyx", 1, 7, 4, dropItem: true),
            new WaveEnemyPlacement("Zyx", 1, 7, 6, dropCoins: 3)));
        // 1-3: 雑魚4体（アイテム1＋コイン2×3）
        waveDefinitions.Add(StagedCombat(1, 3,
            new WaveEnemyPlacement("Zyx", 1, 7, 4, dropItem: true),
            new WaveEnemyPlacement("Zyx", 1, 7, 6, dropCoins: 2),
            new WaveEnemyPlacement("Zyx", 1, 9, 5, dropCoins: 2),
            new WaveEnemyPlacement("Zyx", 1, 10, 5, dropCoins: 2)));

        // === Stage 2: コスト1〜2エスカレーション。2-5/2-10が中ボス（撃破でコスト解放）。 ===
        // 2-1
        waveDefinitions.Add(StagedCombat(2, 1,
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 7, 5)));
        // 2-2
        waveDefinitions.Add(StagedCombat(2, 2,
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Ranged, 1, 10, 5)));
        // 2-3: イベント（シルバーオーグメント選択）
        waveDefinitions.Add(StagedEvent(2, 3, WaveEventType.AugmentSilver));
        // 2-4
        waveDefinitions.Add(StagedCombat(2, 4,
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 2, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Ranged, 2, 10, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Ranged, 2, 10, 6)));
        // 2-5: 中ボス（Shadowlord ＋ 護衛）。コスト4解放。
        waveDefinitions.Add(StagedMidBoss(2, 5,
            new WaveEnemyPlacement("Shadowlord", 1, 7, 5),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 2, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 2, 6, 6)));
        // 2-6
        waveDefinitions.Add(StagedCombat(2, 6,
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 1, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 1, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 1, 9, 5)));
        // 2-7
        waveDefinitions.Add(StagedCombat(2, 7,
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 7, 5),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Ranged, 3, 10, 5)));
        // 2-8: イベント（アイテム）
        waveDefinitions.Add(StagedEvent(2, 8, WaveEventType.BonusItem));
        // 2-9
        waveDefinitions.Add(StagedCombat(2, 9,
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 6)));
        // 2-10: 中ボス（Kane ＋ コスト2護衛）。コスト5解放。
        waveDefinitions.Add(StagedMidBoss(2, 10,
            new WaveEnemyPlacement("Kane", 2, 7, 5),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 5)));

        // === Stage 3: コスト2-3 中盤。3-5/3-10が中ボス。 ===
        // 3-1
        waveDefinitions.Add(StagedCombat(3, 1,
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 3-2
        waveDefinitions.Add(StagedCombat(3, 2,
            new WaveEnemyPlacement("Shadowlord", 2, 7, 5),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 6)));
        // 3-3: イベント（ゴールドオーグメント選択）
        waveDefinitions.Add(StagedEvent(3, 3, WaveEventType.AugmentGold));
        // 3-4
        waveDefinitions.Add(StagedCombat(3, 4,
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 6)));
        // 3-5: 中ボス（Kane＋Paragon）。
        waveDefinitions.Add(StagedMidBoss(3, 5,
            new WaveEnemyPlacement("Kane", 2, 6, 4),
            new WaveEnemyPlacement("Paragon", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 3-6
        waveDefinitions.Add(StagedCombat(3, 6,
            new WaveEnemyPlacement("Malyk", 2, 7, 4),
            new WaveEnemyPlacement("Shadowlord", 2, 7, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 3-7: イベント（アイテム）
        waveDefinitions.Add(StagedEvent(3, 7, WaveEventType.BonusItem));
        // 3-8
        waveDefinitions.Add(StagedCombat(3, 8,
            new WaveEnemyPlacement("Ilenamk2", 2, 7, 4),
            new WaveEnemyPlacement("Tier2general", 2, 7, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 3-9
        waveDefinitions.Add(StagedCombat(3, 9,
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 6)));
        // 3-10: 中ボス（Decepticleprime＋Kane＋Malyk）。
        waveDefinitions.Add(StagedMidBoss(3, 10,
            new WaveEnemyPlacement("Decepticleprime", 2, 10, 5),
            new WaveEnemyPlacement("Kane", 2, 6, 4),
            new WaveEnemyPlacement("Malyk", 2, 6, 6)));

        // === Stage 4: コスト3-5 終盤。4-7/4-8/4-9 が中ボス、4-10 が章ボス。 ===
        // 4-1
        waveDefinitions.Add(StagedCombat(4, 1,
            new WaveEnemyPlacement("Shadowlord", 2, 7, 4),
            new WaveEnemyPlacement("Shadowlord", 2, 7, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 4-2: コスト4 Wraith 初登場（★1で控えめ）
        waveDefinitions.Add(StagedCombat(4, 2,
            new WaveEnemyPlacement("Wraith", 1, 7, 5),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 3, 6, 6)));
        // 4-3: イベント（プリズムオーグメント選択）
        waveDefinitions.Add(StagedEvent(4, 3, WaveEventType.AugmentPrism));
        // 4-4
        waveDefinitions.Add(StagedCombat(4, 4,
            new WaveEnemyPlacement("Kane", 2, 6, 4),
            new WaveEnemyPlacement("Kane", 2, 6, 6),
            new WaveEnemyPlacement("Paragon", 2, 7, 5)));
        // 4-5
        waveDefinitions.Add(StagedCombat(4, 5,
            new WaveEnemyPlacement("Ilenamk2", 2, 6, 4),
            new WaveEnemyPlacement("Malyk", 2, 6, 6),
            new WaveEnemyPlacement("Tier2general", 2, 7, 5)));
        // 4-6: イベント（ゴールド）
        waveDefinitions.Add(StagedEvent(4, 6, WaveEventType.BonusGold));
        // 4-7: 中ボス（Wraith ＋ コスト3護衛）
        waveDefinitions.Add(StagedMidBoss(4, 7,
            new WaveEnemyPlacement("Wraith", 1, 7, 5),
            new WaveEnemyPlacement("Shadowlord", 2, 6, 4),
            new WaveEnemyPlacement("Shadowlord", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 4-8: 中ボス（Wujin ＋ コスト3護衛）
        waveDefinitions.Add(StagedMidBoss(4, 8,
            new WaveEnemyPlacement("Wujin", 1, 7, 5),
            new WaveEnemyPlacement("Kane", 2, 6, 4),
            new WaveEnemyPlacement("Decepticleprime", 2, 10, 6)));
        // 4-9: 中ボス（Snowchasermk ＋ コスト3護衛）
        waveDefinitions.Add(StagedMidBoss(4, 9,
            new WaveEnemyPlacement("Snowchasermk", 1, 7, 5),
            new WaveEnemyPlacement("Paragon", 2, 6, 4),
            new WaveEnemyPlacement("Tier2general", 2, 6, 6)));
        // 4-10: チャプターボス（コスト5 Legion ＋ コスト5/4 大護衛）。撃破で報酬選択＋章クリア。
        waveDefinitions.Add(StagedBoss(4, 10,
            new WaveEnemyPlacement("Legion", 2, 8, 5),
            new WaveEnemyPlacement("Plaguegeneral", 1, 9, 3),
            new WaveEnemyPlacement("Gol", 1, 9, 7),
            new WaveEnemyPlacement("Wraith", 1, 7, 4),
            new WaveEnemyPlacement("Wujin", 1, 7, 6)));
    }

    // 序盤3ラウンドの確認用に、高HPダミー戦を通常進行へ挟み込みます。
    private void AddOpeningDebugWaveIfNeeded()
    {
        if (!includeDebugTrainingWave)
            return;

        waveDefinitions.Add(CreateDebugTrainingWaveDefinition());
    }

    // 1分ほど耐える高HPダミーを複数置く、スキル・シナジー確認用ウェーブです。
    private WaveDefinition CreateDebugTrainingWaveDefinition()
    {
        return new WaveDefinition(false, true,
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 6, 4, -1, true),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 8, 5, -1, true),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 10, 6, -1, true));
    }

    // 戦闘開始前の味方盤面配置を保存します。
    private void SnapshotPlayerBoardUnits()
    {
        roundPlayerUnits.Clear();
        roundStartNodeByPlayerUnit.Clear();

        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity entity = team1Entities[i];
            if (entity == null || entity.CurrentNode == null)
                continue;

            roundPlayerUnits.Add(entity);
            roundStartNodeByPlayerUnit[entity] = entity.CurrentNode;
        }
    }

    // ウェーブ定義に沿って敵を生成します。
    private void SpawnWaveEnemies(WaveDefinition waveDefinition)
    {
        if (waveDefinition == null)
            return;

        for (int i = 0; i < waveDefinition.Enemies.Count; i++)
            SpawnWaveEnemy(waveDefinition.Enemies[i]);
    }

    // 1体分の敵を、指定された盤面座標に生成します。
    private void SpawnWaveEnemy(WaveEnemyPlacement placement)
    {
        if (GridManager.Instance == null)
            return;

        Node spawnNode = GridManager.Instance.GetNodeAtBoardCoordinate(placement.Column, placement.Row);
        if (spawnNode == null)
        {
            Debug.LogWarning($"Wave enemy spawn node not found. Column:{placement.Column} Row:{placement.Row}");
            return;
        }

        if (spawnNode.IsOccupied)
        {
            Debug.LogWarning($"Wave enemy spawn node is already occupied. Column:{placement.Column} Row:{placement.Row}");
            return;
        }

        if (!TryGetWaveEnemyData(placement, out EntitiesDatabaseSO.EntityData entityData))
            return;

        BaseEntity newEntity = Instantiate(entityData.prefab, team2Parent);
        newEntity.InitializeIdentity(entityData.name, entityData.cost, placement.StarLevel);
        SynergyManager.AssignEntitySynergies(newEntity, entityData);
        if (placement.IsDebugTrainingDummy)
            newEntity.ConfigureDebugTrainingDummy(debugTrainingDummyHealth, debugTrainingDummyMoveSpeed);
        team2Entities.Add(newEntity);
        newEntity.Setup(Team.Team2, spawnNode);

        // 撃破時のドロップを登録します（雑魚戦などで使用）。
        if (placement.DropCoins > 0 || placement.DropItem)
            enemyDrops[newEntity] = new EnemyDrop(placement.DropCoins, placement.DropItem);
    }

    // 召喚シナジー用の一時味方を盤面へ出します。所有ユニットではないので合成・売却・シナジーカウントから外れます。
    public BaseEntity SpawnTemporarySummonFromSynergy(bool large)
    {
        if (entitiesDatabase == null || entitiesDatabase.allEntities == null || GridManager.Instance == null)
            return null;

        Node spawnNode = GridManager.Instance.GetFreeNode(Team.Team1);
        if (spawnNode == null)
            return null;

        int maxCost = large ? 2 : 1;
        List<EntitiesDatabaseSO.EntityData> candidates = entitiesDatabase.allEntities
            .Where(data => data.prefab != null
                && data.cost <= maxCost
                && data.prefab.range <= 2
                && !IsLegionOnlySummonData(data)
                && data.synergy1 != SynergyType.Apex
                && data.synergy2 != SynergyType.Apex
                && data.synergy3 != SynergyType.Apex)
            .ToList();

        if (candidates.Count == 0)
            return null;

        EntitiesDatabaseSO.EntityData entityData = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        BaseEntity summon = Instantiate(entityData.prefab, team1Parent != null ? team1Parent : transform);
        summon.InitializeIdentity(entityData.name, entityData.cost, large ? 2 : 1);
        summon.SetSummonedUnit(true);
        team1Entities.Add(summon);
        summon.Setup(Team.Team1, spawnNode);
        ApplyAugmentSummonBonuses(summon);
        return summon;
    }

    // gold_elite_summon: 戦闘開始時、ランダムなコスト3ユニットを一時召喚体としてプレイヤー側盤面へ出します。
    public BaseEntity SpawnAugmentEliteSummon()
    {
        if (entitiesDatabase == null || entitiesDatabase.allEntities == null || GridManager.Instance == null)
            return null;

        Node spawnNode = GridManager.Instance.GetFreeNode(Team.Team1);
        if (spawnNode == null)
            return null;

        List<EntitiesDatabaseSO.EntityData> candidates = entitiesDatabase.allEntities
            .Where(data => data.prefab != null
                && data.cost == 3
                && !IsLegionOnlySummonData(data)
                && data.synergy1 != SynergyType.Apex
                && data.synergy2 != SynergyType.Apex
                && data.synergy3 != SynergyType.Apex)
            .ToList();
        if (candidates.Count == 0)
            return null;

        EntitiesDatabaseSO.EntityData entityData = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        BaseEntity summon = Instantiate(entityData.prefab, team1Parent != null ? team1Parent : transform);
        summon.InitializeIdentity(entityData.name, entityData.cost, 1);
        summon.SetSummonedUnit(true);
        team1Entities.Add(summon);
        summon.Setup(Team.Team1, spawnNode);
        ApplyAugmentSummonBonuses(summon);
        return summon;
    }

    // 召喚体への augment ステータス倍率を一括で乗せます（gold_summon_dmg / prism_summon_master）。
    private void ApplyAugmentSummonBonuses(BaseEntity summon)
    {
        if (summon == null) return;
        float hpMul = 1f;
        float dmgMul = 1f;
        if (HasAugment("prism_summon_master")) hpMul *= 1.30f;
        if (HasAugment("gold_summon_dmg")) dmgMul *= 1.30f;
        if (Mathf.Approximately(hpMul, 1f) && Mathf.Approximately(dmgMul, 1f))
            return;
        summon.ApplyAugmentSummonStatMultipliers(hpMul, dmgMul);
    }

    // 指定ユニット名の一時召喚体を、できるだけ指定Node付近へ出します。Legionなどの専用召喚で使います。
    public BaseEntity SpawnTemporarySummonByUnitName(string unitName, Team team, Node nearNode, int starLevel, float lifetime)
    {
        if (string.IsNullOrEmpty(unitName) || entitiesDatabase == null || entitiesDatabase.allEntities == null || GridManager.Instance == null)
            return null;

        EntitiesDatabaseSO.EntityData entityData = entitiesDatabase.allEntities
            .FirstOrDefault(data => data.prefab != null && string.Equals(data.name, unitName, StringComparison.OrdinalIgnoreCase));
        if (entityData.prefab == null)
            return null;

        Node spawnNode = null;
        if (nearNode != null)
        {
            spawnNode = GridManager.Instance.GetNodesInRange(nearNode, 2.5f)
                .Where(node => node != null && !node.IsOccupied)
                .OrderBy(node => Vector3.Distance(node.worldPosition, nearNode.worldPosition))
                .FirstOrDefault();
        }

        if (spawnNode == null)
            spawnNode = GridManager.Instance.GetFreeNode(team);

        if (spawnNode == null)
            return null;

        Transform parent = team == Team.Team1 ? team1Parent : team2Parent;
        BaseEntity summon = Instantiate(entityData.prefab, parent != null ? parent : transform);
        summon.InitializeIdentity(entityData.name, entityData.cost, Mathf.Max(1, starLevel));
        summon.SetSummonedUnit(true);

        if (team == Team.Team1)
            team1Entities.Add(summon);
        else
            team2Entities.Add(summon);

        summon.Setup(team, spawnNode);
        summon.BeginTemporarySummonLifetime(lifetime);
        if (team == Team.Team1) ApplyAugmentSummonBonuses(summon);
        return summon;
    }

    // 戦闘終了時などに、召喚シナジーが出した一時ユニットを安全に片付けます。
    public void RemoveTemporarySummonFromSynergy(BaseEntity summon)
    {
        if (summon == null)
            return;

        if (summon.CurrentNode != null)
            summon.CurrentNode.SetOccupied(false);

        team1Entities.Remove(summon);
        team2Entities.Remove(summon);
        benchEntities.Remove(summon);
        benchSlotByEntity.Remove(summon);
        Destroy(summon.gameObject);
        OnRosterChanged?.Invoke();
    }

    // 錬金シナジーの報酬として、ランダムなアイテムをアイテムベンチに追加します。
    public void GrantRandomItemFromSynergy()
    {
        IReadOnlyList<ItemData> items = ItemCatalog.AllItems;
        if (items == null || items.Count == 0)
            return;

        ItemData item = items[UnityEngine.Random.Range(0, items.Count)];
        ReturnItemToBench(item);
        AttackEffectPlayer.PlayUiSfx("item_equip");
    }

    // ウェーブ指定に合うユニットを選びます。固定名、候補番号、ランダム指定の順で処理します。
    private bool TryGetWaveEnemyData(WaveEnemyPlacement placement, out EntitiesDatabaseSO.EntityData entityData)
    {
        entityData = default(EntitiesDatabaseSO.EntityData);
        if (entitiesDatabase == null || entitiesDatabase.allEntities == null)
            return false;

        if (!string.IsNullOrEmpty(placement.UnitId))
        {
            List<EntitiesDatabaseSO.EntityData> fixedCandidates = entitiesDatabase.allEntities
                .Where(data => data.prefab != null && string.Equals(data.name, placement.UnitId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (fixedCandidates.Count > 0)
            {
                entityData = fixedCandidates[0];
                return true;
            }

            Debug.LogWarning($"No fixed wave enemy found for {placement.UnitId}.");
            return false;
        }

        List<EntitiesDatabaseSO.EntityData> candidates = GetWaveEnemyCandidates(placement.Kind);

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"No wave enemy candidates found for {placement.Kind}.");
            return false;
        }

        if (placement.CandidateIndex >= 0)
        {
            if (placement.CandidateIndex >= candidates.Count)
            {
                Debug.LogWarning($"Wave enemy candidate index {placement.CandidateIndex} was out of range for {placement.Kind}.");
                return false;
            }

            entityData = candidates[placement.CandidateIndex];
            return true;
        }

        entityData = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    // ウェーブ種別に合う候補を、固定配置しやすいよう名前順で返します。
    private List<EntitiesDatabaseSO.EntityData> GetWaveEnemyCandidates(WaveEnemyKind kind)
    {
        int cost = GetWaveEnemyCost(kind);

        return entitiesDatabase.allEntities
            .Where(data => data.cost == cost && data.prefab != null && !IsLegionOnlySummonData(data) && MatchesWaveEnemyKind(data, kind))
            .OrderBy(data => data.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Legionのスキル専用召喚体やニュートラル雑魚など、ショップ・通常ウェーブ・ランダム召喚候補から外すユニットです。
    private bool IsLegionOnlySummonData(EntitiesDatabaseSO.EntityData data)
    {
        if (string.IsNullOrEmpty(data.name))
            return false;
        return string.Equals(data.name, "Taskmaster", StringComparison.OrdinalIgnoreCase)
            || string.Equals(data.name, "Zyx", StringComparison.OrdinalIgnoreCase);
    }

    // ウェーブ種別から必要コストを返します。
    private int GetWaveEnemyCost(WaveEnemyKind kind)
    {
        if (kind == WaveEnemyKind.Cost2Melee || kind == WaveEnemyKind.Cost2Ranged)
            return 2;

        return 1;
    }

    // 射程から近接/遠距離を判定します。射程4以上を遠距離、それ未満を近接として扱います。
    private bool MatchesWaveEnemyKind(EntitiesDatabaseSO.EntityData entityData, WaveEnemyKind kind)
    {
        bool ranged = entityData.prefab != null && entityData.prefab.range >= 4;
        if (kind == WaveEnemyKind.Cost1Ranged || kind == WaveEnemyKind.Cost2Ranged)
            return ranged;

        if (kind == WaveEnemyKind.Cost1Melee || kind == WaveEnemyKind.Cost2Melee)
            return !ranged;

        return true;
    }

    // シーン上に残っている敵ユニットを全て片付けます。
    private void ClearEnemyUnits()
    {
        for (int i = team2Entities.Count - 1; i >= 0; i--)
        {
            BaseEntity enemy = team2Entities[i];
            if (enemy == null)
                continue;

            if (enemy.CurrentNode != null)
                enemy.CurrentNode.SetOccupied(false);

            Destroy(enemy.gameObject);
        }

        team2Entities.Clear();
        enemyDrops.Clear();
    }

    // 現在のラウンドが戦闘なしのイベントかどうかを返します。
    private bool IsCurrentRoundEvent()
    {
        return currentWaveIndex >= 0
            && currentWaveIndex < waveDefinitions.Count
            && waveDefinitions[currentWaveIndex] != null
            && waveDefinitions[currentWaveIndex].IsEventRound;
    }

    // 現在のラウンドがイベントなら報酬を渡して次へ進めます。連続イベントもまとめて消化します。
    // 戦闘中・ゲームオーバー・ボス報酬選択中・オーグメント選択中は何もしません。
    private void TryStartEventRound()
    {
        int safety = 0;
        while (IsCurrentRoundEvent() && !IsRoundInProgress && !gameOver && !bossRewardSelectionPending && !augmentSelectionPending && safety++ < 32)
        {
            WaveDefinition def = waveDefinitions[currentWaveIndex];
            WaveEventType type = def.EventType;

            // オーグメント選択イベントは UI で選ばせるため、自動消化を一旦止めます。
            if (type == WaveEventType.AugmentSilver || type == WaveEventType.AugmentGold || type == WaveEventType.AugmentPrism)
            {
                ShowAugmentSelectionForEvent(type);
                return;
            }

            ResolveEventRound(type);
            currentWaveIndex++;
            UpdateRoundProgressUi();
            OnRosterChanged?.Invoke();
        }
    }

    // イベント種別ごとの報酬を付与します（オーグメントは別経路で処理）。
    private void ResolveEventRound(WaveEventType eventType)
    {
        switch (eventType)
        {
            case WaveEventType.BonusItem:
                GrantRandomItemFromSynergy();
                GrantRandomItemFromSynergy();
                Debug.Log("Event round: granted 2 random items.");
                break;
            case WaveEventType.BonusGold:
                PlayerData.Instance?.AddMoney(eventBonusGold);
                AttackEffectPlayer.PlayUiSfx("unit_buy");
                Debug.Log($"Event round: granted {eventBonusGold} gold.");
                break;
        }
    }

    // 所持オーグメントのIDで検索します。戦闘コードや UIShop が効果を判定するときに使います。
    public bool HasAugment(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        for (int i = 0; i < OwnedAugments.Count; i++)
            if (OwnedAugments[i] != null && string.Equals(OwnedAugments[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // 同名ユニットの所持数を返します（silver_dupe_dmg などで使用）。
    public int CountOwnedUnitsByUnitId(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return 0;
        int count = 0;
        for (int i = 0; i < team1Entities.Count; i++)
            if (team1Entities[i] != null && string.Equals(team1Entities[i].UnitId, unitId, StringComparison.OrdinalIgnoreCase))
                count++;
        for (int i = 0; i < benchEntities.Count; i++)
            if (benchEntities[i] != null && string.Equals(benchEntities[i].UnitId, unitId, StringComparison.OrdinalIgnoreCase))
                count++;
        return count;
    }

    // 盤面に同じコストのプレイヤーユニットが何体いるかを返します（silver_same_cost_bond 用）。
    public int CountSameCostBoardAllies(int cost)
    {
        int count = 0;
        for (int i = 0; i < team1Entities.Count; i++)
            if (team1Entities[i] != null && team1Entities[i].BaseCost == cost)
                count++;
        return count;
    }

    // このユニットが盤面の最高コスト味方かを返します（prism_king_blessed 用）。
    public bool IsHighestCostOnBoard(BaseEntity entity)
    {
        if (entity == null || entity.Team != Team.Team1) return false;
        int myCost = entity.BaseCost;
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity other = team1Entities[i];
            if (other == null || other == entity) continue;
            if (other.BaseCost > myCost) return false;
        }
        return true;
    }

    // 戦闘開始時、オーグメントの「バトル開始系」効果を発動します。
    private void ApplyBattleStartAugmentEffects()
    {
        // prism_time_stop: 戦闘開始時、敵全体を 1.5 秒スタン。
        if (HasAugment("prism_time_stop"))
        {
            for (int i = 0; i < team2Entities.Count; i++)
            {
                BaseEntity e = team2Entities[i];
                if (e == null || e.IsDead || !e.IsOnBoard)
                    continue;

                e.ApplyStun(1.5f);
            }
        }

        // silver_team_heal: 戦闘開始時、盤面の味方全員へ最大HPの10%シールド。
        if (HasAugment("silver_team_heal"))
        {
            for (int i = 0; i < team1Entities.Count; i++)
            {
                BaseEntity e = team1Entities[i];
                if (e == null || e.IsDead || e.IsSummonedUnit || !e.IsOnBoard)
                    continue;

                int shield = Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.10f));
                e.ApplyShieldFromSynergy(shield, 30f);
            }
        }

        // silver_first_attack: 戦闘開始から3秒間、味方全体の与ダメージ+15%。
        if (HasAugment("silver_first_attack"))
        {
            for (int i = 0; i < team1Entities.Count; i++)
            {
                BaseEntity e = team1Entities[i];
                if (e == null || e.IsDead || e.IsSummonedUnit || !e.IsOnBoard)
                    continue;

                e.ApplyTimedSynergyDamageDealtBonus(0.15f, 3f);
            }
        }

        // silver_speed_boost_delayed: 戦闘開始2秒後、味方全体に5秒間 攻撃速度+10%。
        if (HasAugment("silver_speed_boost_delayed"))
            StartCoroutine(DelayedAttackSpeedBoostForAugment(2f, 0.10f, 5f));

        // gold_judgement: 戦闘開始5秒後、ランダム敵2体に最大HP15%ダメージ。
        if (HasAugment("gold_judgement"))
            StartCoroutine(DelayedJudgementBoltCoroutine(5f, 2, 0.15f));

        // gold_time_pulse: 戦闘開始10秒後、味方全体に2秒間 攻撃速度+50%。
        if (HasAugment("gold_time_pulse"))
            StartCoroutine(DelayedAttackSpeedBoostForAugment(10f, 0.50f, 2f));

        // silver_extra_synergy_count / gold_duplicate_synergy: ランダムシナジーを+1（戦闘中のみ）。
        AdditionalSynergyBonusThisCombat.Clear();
        int randomSynergyAdds = (HasAugment("silver_extra_synergy_count") ? 1 : 0) + (HasAugment("gold_duplicate_synergy") ? 1 : 0);
        if (randomSynergyAdds > 0)
        {
            var pool = SynergyManager.OrderedSynergyTypes;
            for (int n = 0; n < randomSynergyAdds; n++)
            {
                if (pool.Count == 0) break;
                SynergyType pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                int cur;
                if (AdditionalSynergyBonusThisCombat.TryGetValue(pick, out cur))
                    AdditionalSynergyBonusThisCombat[pick] = cur + 1;
                else
                    AdditionalSynergyBonusThisCombat[pick] = 1;
            }
        }

        // 1戦闘単位の augment フラグをリセットします。
        augmentPrismReviveUsedThisCombat = false;

        // gold_elite_summon: 戦闘開始時、ランダムなコスト3ユニットを召喚体として加勢させます（戦闘終了で消滅）。
        if (HasAugment("gold_elite_summon"))
        {
            BaseEntity elite = SpawnAugmentEliteSummon();
            if (elite != null)
                AttackEffectPlayer.PlaySynergyEffect(SynergyType.Summoner, elite.transform.position, 1.5f);
        }

        // prism_warrior_kill_buff: 前戦闘で蓄積した戦士の撃破回数に応じて、戦士全員に与ダメージバフを付与します。
        ApplyPrismWarriorKillBuffAtBattleStart();
    }

    // 指定秒数後、ランダムな敵を最大HP割合でダメージを与えるコルーチン（gold_judgement 用）。
    private System.Collections.IEnumerator DelayedJudgementBoltCoroutine(float delay, int targets, float maxHpPercent)
    {
        yield return new WaitForSeconds(delay);
        if (!IsRoundInProgress) yield break;
        // ランダムな敵から targets 体抽選
        List<BaseEntity> alive = new List<BaseEntity>();
        for (int i = 0; i < team2Entities.Count; i++)
        {
            BaseEntity entity = team2Entities[i];
            if (entity != null && !entity.IsDead && entity.IsOnBoard && entity.CurrentHealth > 0)
                alive.Add(entity);
        }

        for (int t = 0; t < targets && alive.Count > 0; t++)
        {
            int idx = UnityEngine.Random.Range(0, alive.Count);
            BaseEntity victim = alive[idx];
            alive.RemoveAt(idx);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(victim.MaxHealth * maxHpPercent));
            victim.TakeDamage(dmg, null, CombatNumberKind.FocusDamage);
        }
    }

    // BaseEntity の Die() から呼ばれ、オーグメントによる復活を試みます。
    public bool TryConsumeAugmentReviveForUnit(BaseEntity entity)
    {
        if (entity == null || entity.Team != Team.Team1) return false;

        // prism_one_revive: 1戦闘1回、最初に倒れた味方をHP50%で復活。
        if (!augmentPrismReviveUsedThisCombat && HasAugment("prism_one_revive"))
        {
            augmentPrismReviveUsedThisCombat = true;
            entity.AugmentReviveAtRatio(0.50f);
            return true;
        }

        // silver_revive_3: ユニットごと1度だけHP10%で復活、チャプター中3体まで。
        if (HasAugment("silver_revive_3")
            && !entity.SilverAugmentReviveUsed
            && AugmentSilverRevivesSpentInChapter < 3)
        {
            entity.SilverAugmentReviveUsed = true;
            AugmentSilverRevivesSpentInChapter++;
            entity.AugmentReviveAtRatio(0.10f);
            return true;
        }

        return false;
    }

    // 敵を撃破した瞬間に呼ばれます。prism_kill_heal / prism_warrior_kill_buff を反映します。
    public void NotifyEnemyKilledByPlayer(BaseEntity killedEnemy, BaseEntity killer = null)
    {
        if (killedEnemy == null) return;

        // prism_kill_heal: 撃破時、味方全体に分配回復（撃破対象の最大HP10%を頭割り）。
        if (HasAugment("prism_kill_heal"))
        {
            int totalHeal = Mathf.Max(1, Mathf.RoundToInt(killedEnemy.MaxHealth * 0.10f));
            int aliveAllies = 0;
            for (int i = 0; i < team1Entities.Count; i++)
                if (team1Entities[i] != null && team1Entities[i].CurrentHealth > 0)
                    aliveAllies++;
            if (aliveAllies > 0)
            {
                int perAlly = Mathf.Max(1, totalHeal / aliveAllies);
                for (int i = 0; i < team1Entities.Count; i++)
                    if (team1Entities[i] != null && team1Entities[i].CurrentHealth > 0)
                        team1Entities[i].HealFromSynergy(perAlly);
            }
        }

        // prism_warrior_kill_buff: 戦士による撃破を、撃破者のUnitId単位でカウント。次戦闘開始時に消費します。
        if (HasAugment("prism_warrior_kill_buff")
            && killer != null && !killer.IsSummonedUnit
            && killer.HasSynergy(SynergyType.Warrior)
            && !string.IsNullOrEmpty(killer.UnitId))
        {
            string key = killer.UnitId;
            int cur;
            warriorKillBuffPendingByUnitId.TryGetValue(key, out cur);
            warriorKillBuffPendingByUnitId[key] = cur + 1;
        }
    }

    // prism_warrior_kill_buff: 撃破者の UnitId をキーに、次戦闘開始まで持ち越す累積キル数。
    private readonly Dictionary<string, int> warriorKillBuffPendingByUnitId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // 戦闘開始時、保留中の戦士キルバフを盤面の該当戦士に与ダメージ%バフとして付与します。
    private void ApplyPrismWarriorKillBuffAtBattleStart()
    {
        if (!HasAugment("prism_warrior_kill_buff"))
        {
            warriorKillBuffPendingByUnitId.Clear();
            return;
        }

        if (warriorKillBuffPendingByUnitId.Count == 0)
            return;

        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity entity = team1Entities[i];
            if (entity == null || entity.IsSummonedUnit || !entity.HasSynergy(SynergyType.Warrior))
                continue;

            int kills;
            if (!warriorKillBuffPendingByUnitId.TryGetValue(entity.UnitId, out kills) || kills <= 0)
                continue;

            // 1キル毎+30%、上限 +300%。
            float bonus = Mathf.Min(3.0f, 0.30f * kills);
            entity.ApplyTimedSynergyDamageDealtBonus(bonus, 60f);
        }

        warriorKillBuffPendingByUnitId.Clear();
    }

    private System.Collections.IEnumerator DelayedAttackSpeedBoostForAugment(float delay, float bonus, float duration)
    {
        yield return new WaitForSeconds(delay);
        if (!IsRoundInProgress) yield break;
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsDead || e.IsSummonedUnit || !e.IsOnBoard)
                continue;

            e.ApplyAttackSpeedBoostFromSynergy(1f + bonus, duration);
        }
    }

    // オーグメント選択UIを呼び出し、選択完了まで進行を保留します。
    private void ShowAugmentSelectionForEvent(WaveEventType type)
    {
        AugmentTier tier = type == WaveEventType.AugmentGold ? AugmentTier.Gold
                         : type == WaveEventType.AugmentPrism ? AugmentTier.Prism
                         : AugmentTier.Silver;
        augmentSelectionPending = true;
        AugmentSelectionUI.EnsureExists().Show(tier, ShownAugmentIds, OnAugmentPicked);
    }

    private void OnAugmentPicked(AugmentDefinition aug)
    {
        if (aug != null)
        {
            OwnedAugments.Add(aug);
            ShownAugmentIds.Add(aug.Id);
            ApplyAugmentEffect(aug);
            Debug.Log($"Augment picked: {aug.Id} ({aug.Tier}).");
        }
        augmentSelectionPending = false;
        // 取得したオーグメントを HUD と関連 UI に即時反映します。
        AugmentHudUI.EnsureExists().Refresh();
        if (UIShop.Instance != null) UIShop.Instance.RefreshRerollButtonCostText();
        if (SynergyManager.Instance != null) SynergyManager.Instance.RecalculateSynergies();
        // オーグメントラウンドを消化済みとして進めます。
        currentWaveIndex++;
        UpdateRoundProgressUi();
        // 取得したステータス系オーグメントを、既に所持している盤面・ベンチユニットへ即時反映します。
        RefreshAllOwnedUnitDerivedStats();
        OnRosterChanged?.Invoke();
        // 連続イベントの可能性に備えて続行。
        TryStartEventRound();
    }

    // 盤面・ベンチの所有ユニットに、現在のチームバフやシナジー設定を反映し直します。
    private void RefreshAllOwnedUnitDerivedStats()
    {
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsSummonedUnit)
                continue;

            e.RefreshDerivedStats(false);
        }

        for (int i = 0; i < benchEntities.Count; i++)
        {
            BaseEntity e = benchEntities[i];
            if (e == null)
                continue;

            e.RefreshDerivedStats(false);
        }
    }

    // 選んだオーグメントの効果を適用します。即時付与系と永続フィールド変更系はここで反映し、
    // 戦闘中フック系（procや特殊効果）は OwnedAugments に登録のみ行い、後段で参照します。
    private void ApplyAugmentEffect(AugmentDefinition aug)
    {
        if (aug == null) return;
        switch (aug.Id)
        {
            // ----- 即時のゴールド -----
            case "silver_coins_8": PlayerData.Instance?.AddMoney(8); break;
            case "gold_coins_20": PlayerData.Instance?.AddMoney(20); break;
            case "prism_coins_50": PlayerData.Instance?.AddMoney(50); break;

            // ----- 即時のアイテム -----
            case "silver_random_item":
                GrantRandomItemFromSynergy();
                break;
            case "gold_random_items_2":
                GrantRandomItemFromSynergy();
                GrantRandomItemFromSynergy();
                break;
            case "prism_items_3":
                GrantRandomItemFromSynergy();
                GrantRandomItemFromSynergy();
                GrantRandomItemFromSynergy();
                break;

            // ----- 収入 -----
            case "silver_income_1": if (PlayerData.Instance != null) PlayerData.Instance.baseRoundIncome += 1; break;
            case "gold_income_2": if (PlayerData.Instance != null) PlayerData.Instance.baseRoundIncome += 2; break;
            case "prism_income_5": if (PlayerData.Instance != null) PlayerData.Instance.baseRoundIncome += 5; break;
            case "silver_extra_coin": if (PlayerData.Instance != null) PlayerData.Instance.baseRoundIncome += 2; break;

            // ----- 利子 -----
            case "silver_interest_cap_1": if (PlayerData.Instance != null) PlayerData.Instance.interestCap += 2; break;
            case "gold_interest_cap_2": if (PlayerData.Instance != null) PlayerData.Instance.interestCap += 3; break;
            case "gold_better_interest": if (PlayerData.Instance != null) PlayerData.Instance.interestPerGold = 8; break;
            case "prism_interest_godly":
                if (PlayerData.Instance != null) { PlayerData.Instance.interestCap += 5; PlayerData.Instance.interestPerGold = 5; }
                break;

            // ----- EXP -----
            case "silver_exp_1": ExtraExpPerWaveClear += 1; break;
            case "gold_exp_2": ExtraExpPerWaveClear += 2; break;

            // ----- ベンチスロット -----
            case "silver_bench_slot_1": BenchSlotBonus += 1; EnsureExtraBenchTiles(); break;
            case "gold_bench_slot_2": BenchSlotBonus += 2; EnsureExtraBenchTiles(); break;
            case "prism_bench_3": BenchSlotBonus += 3; EnsureExtraBenchTiles(); break;

            // ----- ステータス系 -----
            case "silver_dr_5": TeamDamageReductionBonus += 0.05f; break;
            case "gold_dr_10": TeamDamageReductionBonus += 0.10f; break;
            case "prism_dr_20": TeamDamageReductionBonus += 0.20f; break;
            case "silver_atk_6": TeamAttackBonusPercent += 0.06f; break;
            case "gold_atk_12": TeamAttackBonusPercent += 0.12f; break;
            case "prism_atk_20": TeamAttackBonusPercent += 0.20f; break;
            case "silver_move_6": TeamMoveSpeedBonusPercent += 0.06f; break;
            case "gold_move_12": TeamMoveSpeedBonusPercent += 0.12f; break;
            case "prism_speed_20":
                TeamMoveSpeedBonusPercent += 0.20f;
                TeamAttackSpeedBonusPercent += 0.20f;
                break;
            case "silver_hp_6": TeamHPBonusPercent += 0.06f; break;
            case "gold_hp_12": TeamHPBonusPercent += 0.12f; break;
            case "prism_hp_25": TeamHPBonusPercent += 0.25f; break;

            // ----- シナジーエンブレム -----
            case "silver_emblem_warrior": AugmentSynergyBonusWarrior += 1; break;
            case "silver_emblem_ranger": AugmentSynergyBonusRanger += 1; break;
            case "silver_emblem_arcanist": AugmentSynergyBonusArcanist += 1; break;
            case "gold_emblem_warrior_2": AugmentSynergyBonusWarrior += 2; break;
            case "gold_emblem_ranger_2": AugmentSynergyBonusRanger += 2; break;
            case "gold_emblem_arcanist_2": AugmentSynergyBonusArcanist += 2; break;
            case "prism_emblem_warrior_3": AugmentSynergyBonusWarrior += 3; break;
            case "prism_emblem_ranger_3": AugmentSynergyBonusRanger += 3; break;
            case "prism_emblem_arcanist_3": AugmentSynergyBonusArcanist += 3; break;

            // ----- 特殊 -----
            case "prism_unlock_all_costs":
                AugmentAllCostsUnlocked = true;
                MaxAvailableShopCost = maxShopCostCap;
                break;
            case "prism_score_multiplier":
                ScoreMultiplier = Mathf.Min(ScoreMultiplier * 1.3f, 5f);
                break;

            default:
                // proc・条件付き・召喚体強化など、戦闘中フックを必要とする augment は
                // 所持リストに保持され、戦闘コード側で OwnedAugments を参照するときに反映されます。
                Debug.Log($"Augment '{aug.Id}' is owned; combat-time effect will be picked up by later hooks.");
                break;
        }
    }

    // === ステージスコア集計 ===

    // ステージ内のクリア種別を集計し、ステージ切替を検出してリザルトを準備します。
    private void TrackStageProgress(WaveDefinition clearedDef)
    {
        if (clearedDef != null)
        {
            bool ja = LocalizationManager.IsJapanese;
            if (clearedDef.IsBossWave)
            {
                stageScoreBossClears++;
                ScorePopupUI.EnsureExists().Show(1000, ja ? "章ボス撃破!" : "Chapter Boss!", new Color(1f, 0.78f, 0.42f));
            }
            else if (clearedDef.IsMidBossWave)
            {
                stageScoreMidBossClears++;
                ScorePopupUI.EnsureExists().Show(300, ja ? "中ボス撃破!" : "Mid-Boss!", new Color(1f, 0.86f, 0.55f));
            }
            else if (!clearedDef.IsEventRound)
            {
                stageScoreCombatClears++;
                ScorePopupUI.EnsureExists().Show(100, ja ? "戦闘クリア!" : "Wave Clear!");
            }
        }

        int clearedStage = clearedDef != null && clearedDef.StageIndex > 0
            ? clearedDef.StageIndex
            : currentStageTrackedIndex;

        bool chapterCleared = currentWaveIndex >= waveDefinitions.Count;
        int nextStage = clearedStage;
        if (!chapterCleared && currentWaveIndex < waveDefinitions.Count && waveDefinitions[currentWaveIndex] != null)
            nextStage = waveDefinitions[currentWaveIndex].StageIndex;
        else if (chapterCleared)
            nextStage = clearedStage + 1; // 区別のためずらす（チャプタークリア時の番兵値）。

        if (clearedStage > 0 && nextStage != clearedStage)
        {
            QueueStageResult(clearedStage, chapterCleared);
            stageScoreCombatClears = 0;
            stageScoreMidBossClears = 0;
            stageScoreBossClears = 0;
            currentStageStartTime = Time.unscaledTime;
            currentStageTrackedIndex = chapterCleared ? clearedStage : nextStage;
        }
    }

    // 集計結果からスコアと内訳を計算して、リザルト表示のキューに積みます。
    private void QueueStageResult(int stageNumber, bool isChapterClear)
    {
        float elapsed = Time.unscaledTime - currentStageStartTime;
        int combatScore = stageScoreCombatClears * 100;
        int midBossScore = stageScoreMidBossClears * 300;
        int bossScore = stageScoreBossClears * 1000;

        int star2 = 0, star3 = 0;
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null) continue;
            if (e.StarLevel >= 3) star3++;
            else if (e.StarLevel >= 2) star2++;
        }
        for (int i = 0; i < benchEntities.Count; i++)
        {
            BaseEntity e = benchEntities[i];
            if (e == null) continue;
            if (e.StarLevel >= 3) star3++;
            else if (e.StarLevel >= 2) star2++;
        }
        int starScore = star2 * 30 + star3 * 100;

        float refTime = GetStageReferenceTime(stageNumber);
        int speedBonus = Mathf.Max(0, Mathf.RoundToInt((refTime - elapsed) * 5f));

        int stageTotalScore = Mathf.RoundToInt((combatScore + midBossScore + bossScore + starScore + speedBonus) * ScoreMultiplier);

        // 章全体の集計に追加します。
        chapterStageScores.Add(stageTotalScore);
        chapterStageTimes.Add(elapsed);

        if (isChapterClear)
        {
            // チャプタークリア時は、ステージ個別ではなく章全体の集計をリザルトとして渡します。
            int chapterTotalScore = 0;
            for (int i = 0; i < chapterStageScores.Count; i++)
                chapterTotalScore += chapterStageScores[i];
            float chapterTotalTime = Time.unscaledTime - chapterStartTime;

            // R1-score: ベストスコア/タイムを永続化し、自己新かどうかをリザルト表示へ渡します。
            int previousBest = 0;
            bool isNewRecord = false;
            if (SaveManager.Instance != null)
            {
                AutoChessBossRush.Save.ChapterRecord rec = SaveManager.Instance.GetChapter(currentChapter);
                previousBest = rec != null ? rec.bestScore : 0;
                isNewRecord = SaveManager.Instance.RecordChapterResult(currentChapter, chapterTotalScore, chapterTotalTime, true);

                // R1-meta: 章ボスを永続 roster に追加。次章以降の章開始時編成画面で連れて行けます。
                string chapterBossUnitId = GetChapterBossUnitId(currentChapter);
                if (!string.IsNullOrEmpty(chapterBossUnitId))
                    SaveManager.Instance.AddBossAlly(chapterBossUnitId, 1);
            }

            pendingResultStage = stageNumber;
            pendingResultTime = chapterTotalTime;
            pendingResultScore = chapterTotalScore;
            pendingResultBreakdown = BuildChapterBreakdown(stageNumber, stageTotalScore, elapsed);
            pendingResultIsChapterClear = true;
            pendingResultBestScore = Mathf.Max(previousBest, chapterTotalScore);
            pendingResultIsNewRecord = isNewRecord;
            hasPendingStageResult = true;
        }
        else
        {
            pendingResultStage = stageNumber;
            pendingResultTime = elapsed;
            pendingResultScore = stageTotalScore;
            pendingResultBreakdown = BuildStageBreakdown(stageScoreCombatClears, stageScoreMidBossClears, stageScoreBossClears, star2, star3, speedBonus);
            pendingResultIsChapterClear = false;
            pendingResultBestScore = 0;
            pendingResultIsNewRecord = false;
            hasPendingStageResult = true;
        }
    }

    // チャプタークリア時の総括内訳テキストを作ります。最後のステージ番号とそのスコア/タイムも引数で受け取ります。
    private string BuildChapterBreakdown(int finalStageNumber, int finalStageScore, float finalStageElapsed)
    {
        bool ja = LocalizationManager.IsJapanese;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine(ja ? "<b>各ステージの成績</b>" : "<b>Stage results</b>");
        int total = 0;
        for (int i = 0; i < chapterStageScores.Count; i++)
        {
            int score = chapterStageScores[i];
            float time = chapterStageTimes[i];
            total += score;
            string stageLabel = ja ? $"ステージ {i + 1}" : $"Stage {i + 1}";
            sb.AppendLine($"{stageLabel}    {(ja ? "スコア" : "Score")} {score:N0}    {(ja ? "時間" : "Time")} {FormatChapterTime(time)}");
        }
        sb.AppendLine(ja ? $"<b>合計    スコア {total:N0}    時間 {FormatChapterTime(Time.unscaledTime - chapterStartTime)}</b>"
                          : $"<b>Total    Score {total:N0}    Time {FormatChapterTime(Time.unscaledTime - chapterStartTime)}</b>");
        return sb.ToString().TrimEnd();
    }

    private static string FormatChapterTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }

    private float GetStageReferenceTime(int stage)
    {
        switch (stage)
        {
            case 1: return 60f;
            case 2:
            case 3: return 240f;
            case 4: return 300f;
            default: return 240f;
        }
    }

    private string BuildStageBreakdown(int combat, int midBoss, int boss, int star2, int star3, int speedBonus)
    {
        bool ja = LocalizationManager.IsJapanese;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (combat > 0) sb.AppendLine(ja ? $"・通常ラウンド突破 ×{combat}    +{combat * 100}" : $"・Combat clears ×{combat}    +{combat * 100}");
        if (midBoss > 0) sb.AppendLine(ja ? $"・中ボス撃破 ×{midBoss}    +{midBoss * 300}" : $"・Mid-bosses ×{midBoss}    +{midBoss * 300}");
        if (boss > 0) sb.AppendLine(ja ? $"・章ボス撃破 ×{boss}    +{boss * 1000}" : $"・Chapter Boss ×{boss}    +{boss * 1000}");
        if (star2 > 0) sb.AppendLine(ja ? $"・★2ユニット ×{star2}    +{star2 * 30}" : $"・★2 units ×{star2}    +{star2 * 30}");
        if (star3 > 0) sb.AppendLine(ja ? $"・★3ユニット ×{star3}    +{star3 * 100}" : $"・★3 units ×{star3}    +{star3 * 100}");
        if (speedBonus > 0) sb.AppendLine(ja ? $"・スピードボーナス    +{speedBonus}" : $"・Speed bonus    +{speedBonus}");
        return sb.ToString().TrimEnd();
    }

    private void TryShowPendingStageResult()
    {
        if (!hasPendingStageResult)
            return;
        if (bossRewardSelectionPending)
            return;

        ResultPanelUI.EnsureExists().ShowStageResult(
            pendingResultStage,
            pendingResultTime,
            pendingResultScore,
            pendingResultBreakdown,
            pendingResultIsChapterClear,
            pendingResultBestScore,
            pendingResultIsNewRecord);
        hasPendingStageResult = false;
    }

    // ウェーブクリア時、味方を戦闘前配置へ戻して全回復させます。
    private void CompleteCurrentWave()
    {
        bool completedDebugTrainingWave = debugTrainingWaveActive;
        bool clearedBossWave = currentWaveIndex >= 0
            && currentWaveIndex < waveDefinitions.Count
            && waveDefinitions[currentWaveIndex].IsBossWave;
        bool clearedProgressDebugWave = !completedDebugTrainingWave
            && currentWaveIndex >= 0
            && currentWaveIndex < waveDefinitions.Count
            && waveDefinitions[currentWaveIndex].IsDebugWave;
        bool clearedMidBoss = !completedDebugTrainingWave
            && currentWaveIndex >= 0
            && currentWaveIndex < waveDefinitions.Count
            && waveDefinitions[currentWaveIndex].IsMidBossWave;

        if (!completedDebugTrainingWave && !clearedProgressDebugWave)
            SynergyManager.Instance?.NotifyWaveCleared(clearedBossWave);

        IsRoundInProgress = false;
        debugTrainingWaveActive = false;
        ResetAllBattleRuntimeStates();
        AttackEffectPlayer.ClearBattleVisuals();
        OnRoundEnd?.Invoke();
        SynergyManager.Instance?.ClearBattleSynergyState();

        ClearTemporarySummons();
        ClearEnemyUnits();
        RestorePlayerUnitsAfterWave();
        ResolveAllAvailableUpgrades(UpgradeScope.AllOwned);

        if (!completedDebugTrainingWave)
        {
            // クリアしたラウンドの種別を、スコア集計のために覚えておきます。
            int clearedIndex = currentWaveIndex;
            WaveDefinition clearedDef = (clearedIndex >= 0 && clearedIndex < waveDefinitions.Count) ? waveDefinitions[clearedIndex] : null;

            currentWaveIndex++;
            Debug.Log($"Wave {currentWaveIndex} cleared.");

            // ウェーブクリア収入（基本＋利子）を付与します。デバッグ進行ウェーブでは付与しません。
            if (!clearedProgressDebugWave && PlayerData.Instance != null)
            {
                PlayerData.RoundIncome income = PlayerData.Instance.GrantWaveClearIncome();
                Debug.Log($"Income +{income.Total} (base {income.Base}, interest {income.Interest}).");
            }

            // ウェーブクリアごとに経験値を加算（オーグメントで増加し得る）。
            if (!clearedProgressDebugWave && PlayerData.Instance != null)
                PlayerData.Instance.AddExp(2 + ExtraExpPerWaveClear);

            // オーグメント由来のウェーブクリア報酬。
            if (!clearedProgressDebugWave)
            {
                // silver_item_drop: 10% でランダムアイテム +1
                if (HasAugment("silver_item_drop") && UnityEngine.Random.value < 0.10f)
                    GrantRandomItemFromSynergy();
                // gold_item_drop_chance: 30% でランダムアイテム +1
                if (HasAugment("gold_item_drop_chance") && UnityEngine.Random.value < 0.30f)
                    GrantRandomItemFromSynergy();
                // prism_item_alchemy: 戦闘終了時 30% でランダムアイテム
                if (HasAugment("prism_item_alchemy") && UnityEngine.Random.value < 0.30f)
                    GrantRandomItemFromSynergy();
                // gold_star2_bonus: ★2以上の所持で追加収入 +3
                if (HasAugment("gold_star2_bonus") && PlayerData.Instance != null)
                {
                    bool hasStar2 = false;
                    for (int i = 0; i < team1Entities.Count && !hasStar2; i++)
                        if (team1Entities[i] != null && team1Entities[i].StarLevel >= 2) hasStar2 = true;
                    for (int i = 0; i < benchEntities.Count && !hasStar2; i++)
                        if (benchEntities[i] != null && benchEntities[i].StarLevel >= 2) hasStar2 = true;
                    if (hasStar2)
                        PlayerData.Instance.AddMoney(3);
                }
            }

            // 次のラウンドへ移るタイミングで、ショップを1回ぶん無料リロールします。
            // ベンチユニットを掴んだ状態と被ると挙動が乱れるため、ドラッグ中は保留します。
            if (!clearedProgressDebugWave && UIShop.Instance != null)
                UIShop.Instance.RequestFreeRerollOrPending();

            // ステージスコアを更新し、ステージ切替を検出します。
            if (!clearedProgressDebugWave)
                TrackStageProgress(clearedDef);
        }
        else
        {
            Debug.Log("Debug training wave cleared.");
        }

        UpdateRoundProgressUi();
        OnRosterChanged?.Invoke();

        if (!completedDebugTrainingWave && clearedBossWave)
        {
            UnlockNextShopCostTier();
            ShowBossRewardSelection();
        }

        // 中ボス撃破でもショップのコスト上限を1段階解放します（報酬選択はなし）。
        if (clearedMidBoss)
            UnlockNextShopCostTier();

        // 次のラウンドがイベントなら自動で消化します（ボス報酬選択中は保留され、選択後に消化）。
        TryStartEventRound();

        // ステージクリアのリザルトが溜まっていれば表示します（ボス報酬選択中なら選択後に出します）。
        TryShowPendingStageResult();
    }

    // ボスウェーブクリア後、3体の中からショップ解放する仲間を選ばせます。
    private void ShowBossRewardSelection()
    {
        List<EntitiesDatabaseSO.EntityData> rewardOptions = GetBossRewardOptions();
        if (rewardOptions.Count == 0)
        {
            Debug.LogWarning("Boss reward options were not found in the entity database.");
            return;
        }

        bossRewardSelectionPending = true;
        BossRewardSelectionUI.EnsureExists().Show(rewardOptions, SelectBossReward);
    }

    // ボス報酬で選んだユニットをショップに解放し、可能なら★1としてベンチにも加入させます。
    private void SelectBossReward(EntitiesDatabaseSO.EntityData selectedData)
    {
        if (string.IsNullOrEmpty(selectedData.name))
            return;

        unlockedBossRewardUnitIds.Add(selectedData.name);
        bossRewardSelectionPending = false;

        if (HasBenchSpace || CanCompleteUpgradeWithPurchase(selectedData.name))
        {
            BaseEntity rewardEntity = CreateBenchEntity(selectedData, 1);
            if (rewardEntity != null)
                ResolveUpgradesFor(rewardEntity, UpgradeScope.AllOwned);
        }
        else
        {
            Debug.LogWarning($"{selectedData.name} was unlocked for the shop, but the bench is full so no free copy was added.");
        }

        AttackEffectPlayer.PlayUiSfx("unit_buy");
        OnRosterChanged?.Invoke();

        if (UIShop.Instance != null)
            UIShop.Instance.GenerateCard();

        // ボス報酬選択後に、次がイベントラウンドなら消化します。
        TryStartEventRound();
        // ボス報酬選択を終えたタイミングで、保留中のステージリザルトを表示します。
        TryShowPendingStageResult();
    }

    // R1-meta: 過去章で倒したボス仲間が SaveManager に居れば、章開始時の編成画面を開きます。
    // 居なければ何もしません（初回プレイ）。1 体選ぶか「連れて行かない」を押すと閉じます。
    private void TryShowChapterRoster()
    {
        if (SaveManager.Instance == null)
            return;

        IReadOnlyList<AutoChessBossRush.Save.BossAllyRecord> allies = SaveManager.Instance.BossAllies;
        if (allies == null || allies.Count == 0)
            return;

        if (entitiesDatabase == null || entitiesDatabase.allEntities == null)
            return;

        List<EntitiesDatabaseSO.EntityData> options = new List<EntitiesDatabaseSO.EntityData>();
        for (int i = 0; i < allies.Count; i++)
        {
            string unitId = allies[i].unitId;
            if (string.IsNullOrEmpty(unitId)) continue;
            EntitiesDatabaseSO.EntityData data = entitiesDatabase.allEntities.FirstOrDefault(d =>
                d.prefab != null && string.Equals(d.name, unitId, StringComparison.OrdinalIgnoreCase));
            if (data.prefab != null)
                options.Add(data);
        }

        if (options.Count == 0)
            return;

        ChapterRosterUI.EnsureExists().Show(options, OnChapterRosterSelected);
    }

    // 章編成画面で選択結果を受け取ります。引数が default（unitId 空）の場合は「連れて行かない」。
    private void OnChapterRosterSelected(EntitiesDatabaseSO.EntityData selected)
    {
        if (string.IsNullOrEmpty(selected.name))
            return;

        if (!HasBenchSpace && !CanCompleteUpgradeWithPurchase(selected.name))
        {
            Debug.LogWarning($"Chapter roster: bench is full, cannot add {selected.name}.");
            return;
        }

        BaseEntity ally = CreateBenchEntity(selected, 1);
        if (ally != null)
        {
            ResolveUpgradesFor(ally, UpgradeScope.AllOwned);
            AttackEffectPlayer.PlayUiSfx("unit_buy");
            OnRosterChanged?.Invoke();
        }
    }

    // ボス報酬候補として使う3体のEntityDataをデータベースから集めます。
    private List<EntitiesDatabaseSO.EntityData> GetBossRewardOptions()
    {
        if (entitiesDatabase == null || entitiesDatabase.allEntities == null)
            return new List<EntitiesDatabaseSO.EntityData>();

        // prism_boss_reward_extra: 報酬候補は常に全種から選ばせます（解放済みフィルタを無視）。
        bool skipUnlockedFilter = HasAugment("prism_boss_reward_extra");
        return bossRewardUnitIds
            .Where(unitId => skipUnlockedFilter || !unlockedBossRewardUnitIds.Contains(unitId))
            .Select(unitId => entitiesDatabase.allEntities.FirstOrDefault(data =>
                data.prefab != null && string.Equals(data.name, unitId, StringComparison.OrdinalIgnoreCase)))
            .Where(data => data.prefab != null)
            .ToList();
    }

    // 全味方ユニットが倒された時のゲームオーバー処理です。
    private void TriggerGameOver()
    {
        IsRoundInProgress = false;
        debugTrainingWaveActive = false;
        gameOver = true;
        ResetAllBattleRuntimeStates();
        AttackEffectPlayer.ClearBattleVisuals();
        OnRoundEnd?.Invoke();
        SynergyManager.Instance?.ClearBattleSynergyState();
        ClearTemporarySummons();
        ClearEnemyUnits();
        Debug.LogWarning("Game Over. All player units were defeated.");
        UpdateRoundProgressUi();
    }

    // 戦闘終了時に、残り時間付きのスキル・バフ・デバフを全ユニットから即時解除します。
    private void ResetAllBattleRuntimeStates()
    {
        HashSet<BaseEntity> entities = new HashSet<BaseEntity>();
        AddEntitiesToSet(team1Entities, entities);
        AddEntitiesToSet(team2Entities, entities);
        AddEntitiesToSet(roundPlayerUnits, entities);

        foreach (BaseEntity entity in entities)
        {
            if (entity != null)
                entity.ResetBattleTemporaryState();
        }
    }

    // nullを避けながらユニット一覧をSetへ入れます。
    private void AddEntitiesToSet(IEnumerable<BaseEntity> source, HashSet<BaseEntity> destination)
    {
        if (source == null || destination == null)
            return;

        foreach (BaseEntity entity in source)
        {
            if (entity != null)
                destination.Add(entity);
        }
    }

    // 戦闘終了時に残っている召喚体を片付けます。
    private void ClearTemporarySummons()
    {
        List<BaseEntity> summons = team1Entities
            .Concat(team2Entities)
            .Where(entity => entity != null && entity.IsSummonedUnit)
            .Distinct()
            .ToList();

        for (int i = 0; i < summons.Count; i++)
            RemoveTemporarySummonFromSynergy(summons[i]);
    }

    // ウェーブ開始時に保存したNodeへ味方を戻し、倒されたユニットも復活させます。
    private void RestorePlayerUnitsAfterWave()
    {
        for (int i = 0; i < roundPlayerUnits.Count; i++)
        {
            BaseEntity entity = roundPlayerUnits[i];
            if (entity != null && entity.CurrentNode != null)
                entity.CurrentNode.SetOccupied(false);
        }

        for (int i = 0; i < roundPlayerUnits.Count; i++)
        {
            BaseEntity entity = roundPlayerUnits[i];
            if (entity == null || !roundStartNodeByPlayerUnit.TryGetValue(entity, out Node restoreNode))
                continue;

            entity.RestoreForNextWave(Team.Team1, restoreNode);
            if (!team1Entities.Contains(entity))
                team1Entities.Add(entity);
        }
    }

    // ウェーブ進行UIに、次に挑むウェーブ位置を反映します。
    private void UpdateRoundProgressUi()
    {
        InitializeWaveDefinitions();
        bool allClear = waveDefinitions.Count > 0 && currentWaveIndex >= waveDefinitions.Count && !gameOver;

        // ステージ表示モードで送ります。現在のラウンドが属するステージを決め、
        // そのステージのラウンド種別だけを切り出して進捗UIへ渡します（ステージ切替時はDOTweenで遷移演出）。
        int referenceIndex = Mathf.Clamp(currentWaveIndex, 0, Mathf.Max(0, waveDefinitions.Count - 1));
        int currentStage = 1;
        int currentRoundInStage = 0;
        if (waveDefinitions.Count > 0 && referenceIndex < waveDefinitions.Count)
        {
            WaveDefinition def = waveDefinitions[referenceIndex];
            currentStage = def.StageIndex > 0 ? def.StageIndex : 1;
            currentRoundInStage = def.RoundInStage > 0 ? def.RoundInStage : referenceIndex + 1;
        }

        // 全クリア時は最後のステージを「完了」状態で表示します。
        if (allClear && waveDefinitions.Count > 0)
        {
            WaveDefinition last = waveDefinitions[waveDefinitions.Count - 1];
            currentStage = last.StageIndex > 0 ? last.StageIndex : 1;
            int lastStageCount = 0;
            for (int i = 0; i < waveDefinitions.Count; i++)
                if (waveDefinitions[i] != null && waveDefinitions[i].StageIndex == currentStage)
                    lastStageCount++;
            currentRoundInStage = lastStageCount + 1; // 範囲外で「全部クリア済み」になる位置。
        }

        List<RoundProgressUI.RoundKind> stageRounds = new List<RoundProgressUI.RoundKind>();
        for (int i = 0; i < waveDefinitions.Count; i++)
        {
            WaveDefinition def = waveDefinitions[i];
            if (def == null || def.StageIndex != currentStage)
                continue;
            RoundProgressUI.RoundKind kind = RoundProgressUI.RoundKind.Combat;
            if (def.IsBossWave) kind = RoundProgressUI.RoundKind.Boss;
            else if (def.IsMidBossWave) kind = RoundProgressUI.RoundKind.MidBoss;
            else if (def.IsEventRound) kind = RoundProgressUI.RoundKind.Event;
            stageRounds.Add(kind);
        }

        RoundProgressUI.EnsureExists().SetStageProgress(currentStage, currentRoundInStage, stageRounds, gameOver, allClear);
    }

    // ラウンドUIがボスウェーブだけ違うアイコンにできるよう、各ウェーブの種類を渡します。
    private List<bool> GetBossWaveFlags()
    {
        return waveDefinitions.Select(waveDefinition => waveDefinition != null && waveDefinition.IsBossWave).ToList();
    }

    // ベンチユニットをまとめる親が未設定なら作ります。
    private void EnsureBenchParent()
    {
        if (benchParent != null)
            return;

        GameObject bench = new GameObject("BenchUnits");
        bench.transform.SetParent(team1Parent != null ? team1Parent : transform);
        bench.transform.position = Vector3.zero;
        benchParent = bench.transform;
    }

    // ベンチスロット番号から実際のワールド座標を取得します。
    private Vector3 GetBenchPosition(int slotIndex)
    {
        EnsureBenchTileParents();

        // シーン上にベンチタイルがある場合は、その位置を正とします。
        if (team1BenchTilesParent != null && slotIndex < team1BenchTilesParent.childCount)
            return team1BenchTilesParent.GetChild(slotIndex).position;

        // ベンチタイルが無い場合の予備計算です。
        return benchStartPosition + new Vector3(0f, slotIndex * benchSlotSpacing, 0f);
    }

    // 左右ベンチタイル親の参照を、シーン名から補完します。
    private void EnsureBenchTileParents()
    {
        if (team1BenchTilesParent == null)
        {
            GameObject team1Bench = GameObject.Find("Grid/BenchLeft");
            if (team1Bench != null)
                team1BenchTilesParent = team1Bench.transform;
        }

        if (team2BenchTilesParent == null)
        {
            GameObject team2Bench = GameObject.Find("Grid/BenchRight");
            if (team2Bench != null)
                team2BenchTilesParent = team2Bench.transform;
        }

        // ベンチ拡張オーグメントが付与されている場合は、必要な分だけタイルを増やします。
        EnsureExtraBenchTiles();
    }

    // BenchSlotBonus に応じて、左右ベンチに不足分のタイルを動的に複製します。
    private void EnsureExtraBenchTiles()
    {
        int target = EffectiveBenchSlotCount;
        EnsureBenchTilesParentSize(team1BenchTilesParent, target, Team.Team1);
        EnsureBenchTilesParentSize(team2BenchTilesParent, target, Team.Team2);
    }

    // 指定ベンチ親に対し、不足しているスロット数だけ末尾タイルを複製して位置を伸ばします。
    private void EnsureBenchTilesParentSize(Transform parent, int targetCount, Team team)
    {
        if (parent == null || parent.childCount <= 0 || parent.childCount >= targetCount)
            return;

        int existingCount = parent.childCount;
        // 既存タイル2枚以上から1スロット分のオフセットを推定します。
        Vector3 step = Vector3.zero;
        if (existingCount >= 2)
            step = (parent.GetChild(existingCount - 1).position - parent.GetChild(0).position) / (existingCount - 1);
        else
            step = new Vector3(0f, benchSlotSpacing, 0f);

        Transform template = parent.GetChild(existingCount - 1);
        Vector3 lastPos = template.position;
        string prefix = team == Team.Team1 ? "BenchTile_L_" : "BenchTile_R_";

        for (int i = existingCount; i < targetCount; i++)
        {
            GameObject tileObject = Instantiate(template.gameObject, parent);
            tileObject.transform.SetParent(parent, true);
            tileObject.name = $"{prefix}{i}_bonus";
            lastPos += step;
            tileObject.transform.position = lastPos;
            tileObject.transform.localRotation = Quaternion.identity;
            Vector3 finalScale = template.localScale;
            tileObject.transform.localScale = finalScale;

            Tile tile = tileObject.GetComponent<Tile>();
            if (tile == null)
                tile = tileObject.AddComponent<Tile>();
            if (GridManager.Instance != null)
                GridManager.Instance.ConfigureBenchTile(tile, team);

            // ベンチ拡張オーグメント取得時、新タイルがふわっと弾けて生えてくる演出（DOTween）。
            // ゲーム本体は augment 選択中で Time.timeScale=0 のため、SetUpdate(true) でリアル時間で進める。
            tileObject.transform.localScale = Vector3.zero;
            tileObject.transform.DOScale(finalScale, 0.36f).SetEase(Ease.OutBack).SetUpdate(true);
            SpriteRenderer[] tileRenderers = tileObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int rIndex = 0; rIndex < tileRenderers.Length; rIndex++)
            {
                SpriteRenderer renderer = tileRenderers[rIndex];
                if (renderer == null) continue;
                Color baseColor = renderer.color;
                Color startColor = baseColor; startColor.a = 0f;
                renderer.color = startColor;
                renderer.DOFade(baseColor.a, 0.36f).SetUpdate(true);
            }
        }
    }

    // アイテムベンチの総スロット数です。
    private int ItemBenchSlotCount => Mathf.Max(1, itemBenchColumns) * Mathf.Max(1, itemBenchRows);

    // アイテムベンチの親、タイル、アイテム親を用意します。
    private void EnsureItemBenchParents()
    {
        EnsureBenchTileParents();
        NormalizeItemBenchVisualSettings();

        if (itemBenchParent == null)
        {
            GameObject itemBenchUnits = GameObject.Find("ItemBenchItems");
            if (itemBenchUnits == null)
                itemBenchUnits = new GameObject("ItemBenchItems");

            itemBenchUnits.transform.SetParent(team1Parent != null ? team1Parent : transform);
            itemBenchUnits.transform.position = Vector3.zero;
            itemBenchParent = itemBenchUnits.transform;
        }

        if (itemBenchTilesParent == null)
        {
            GameObject existing = GameObject.Find("Grid/ItemBench");
            if (existing != null)
                itemBenchTilesParent = existing.transform;
        }

        if (itemBenchTilesParent == null)
        {
            Transform gridParent = GetGridRootTransform();
            GameObject itemBenchTiles = new GameObject("ItemBench");
            itemBenchTiles.transform.SetParent(gridParent);
            itemBenchTiles.transform.localPosition = Vector3.zero;
            itemBenchTiles.transform.localRotation = Quaternion.identity;
            itemBenchTiles.transform.localScale = Vector3.one;
            itemBenchTilesParent = itemBenchTiles.transform;
        }

        EnsureItemBenchTiles();
        SetItemBenchWorldVisualsVisible(!useCanvasItemBench);
    }

    // Gridオブジェクトがあればその下に、なければGameManagerの下にアイテムベンチを作ります。
    private Transform GetGridRootTransform()
    {
        GameObject gridObject = GameObject.Find("Grid");
        if (gridObject != null)
            return gridObject.transform;

        if (GridManager.Instance != null && GridManager.Instance.terrainGrid != null && GridManager.Instance.terrainGrid.transform.parent != null)
            return GridManager.Instance.terrainGrid.transform.parent;

        return transform;
    }

    // 自分ベンチと同じ見た目のタイルを複製して、アイテムベンチを作ります。
    private void EnsureItemBenchTiles()
    {
        if (itemBenchTilesParent == null)
            return;

        itemBenchTilesParent.localRotation = Quaternion.identity;
        itemBenchTilesParent.localScale = Vector3.one;

        GameObject tileTemplate = GetBenchTileTemplate();
        int slotCount = ItemBenchSlotCount;

        for (int i = itemBenchTilesParent.childCount; i < slotCount; i++)
        {
            GameObject tileObject = tileTemplate != null
                ? Instantiate(tileTemplate, itemBenchTilesParent)
                : new GameObject($"ItemBenchTile_{i}");

            tileObject.name = $"ItemBenchTile_{i}";
            tileObject.transform.SetParent(itemBenchTilesParent);
            tileObject.transform.localRotation = Quaternion.identity;
            tileObject.transform.localScale = Vector3.one;

            if (tileObject.GetComponent<Tile>() == null)
                tileObject.AddComponent<Tile>();
        }

        for (int i = 0; i < Mathf.Min(slotCount, itemBenchTilesParent.childCount); i++)
        {
            Transform tileTransform = itemBenchTilesParent.GetChild(i);
            tileTransform.position = GetItemBenchPosition(i);
            tileTransform.rotation = Quaternion.identity;
            tileTransform.localScale = Vector3.one;

            Tile tile = tileTransform.GetComponent<Tile>();
            if (tile != null && GridManager.Instance != null)
                GridManager.Instance.ConfigureItemBenchTile(tile);
        }
    }

    // 複製元になる自分ベンチタイルを探します。
    private GameObject GetBenchTileTemplate()
    {
        EnsureBenchTileParents();

        if (team1BenchTilesParent != null && team1BenchTilesParent.childCount > 0)
            return team1BenchTilesParent.GetChild(0).gameObject;

        GameObject fallback = GameObject.Find("Grid/BenchLeft/BenchTile_L_0");
        return fallback;
    }

    // アイテムベンチのスロット番号からワールド座標を計算します。
    private Vector3 GetItemBenchPosition(int slotIndex)
    {
        EnsureBenchTileParents();

        Vector3 benchAnchor = GetTeam1BenchBottomAnchor();
        float rowSpacing = GetEffectiveItemBenchRowSpacing();
        float columnSpacing = GetEffectiveItemBenchColumnSpacing(rowSpacing);

        int row = slotIndex % Mathf.Max(1, itemBenchRows);
        int column = slotIndex / Mathf.Max(1, itemBenchRows);
        float leftMostColumnX = GetItemBenchLeftEdgeX(benchAnchor, columnSpacing);
        float x = leftMostColumnX + column * columnSpacing;
        float y = benchAnchor.y + row * rowSpacing;
        return new Vector3(x, y, 0f);
    }

    // アイテムベンチを画面/背景の左端寄りに置くためのX座標を決めます。
    private float GetItemBenchLeftEdgeX(Vector3 benchAnchor, float columnSpacing)
    {
        int columnCount = Mathf.Max(1, itemBenchColumns);
        float rightMostColumnX = benchAnchor.x - Mathf.Max(itemBenchGapFromUnitBench, columnSpacing);
        float fallbackX = rightMostColumnX - (columnCount - 1) * columnSpacing;
        float leftEdgeX = fallbackX;

        EnsureCameraBoundsRenderer();
        if (cameraBoundsRenderer != null)
            leftEdgeX = Mathf.Max(leftEdgeX, cameraBoundsRenderer.bounds.min.x + itemBenchLeftEdgeMargin);

        // アイテムベンチは盤面上に置かれたワールドオブジェクトなので、
        // 現在のカメラ表示範囲で位置を変えると、ズーム/パン後に触れた瞬間マスが動いてしまいます。
        // 背景左端は「これ以上左へ行かない」ための下限としてだけ使い、ユニットベンチに被らない位置へ固定します。
        return leftEdgeX;
    }

    // アイテムベンチが満杯の時に、一時的に置く座標です。
    private Vector3 GetItemBenchOverflowPosition(int overflowIndex = 0)
    {
        float rowSpacing = GetEffectiveItemBenchRowSpacing();
        float columnSpacing = GetEffectiveItemBenchColumnSpacing(rowSpacing);
        Vector3 basePosition = GetItemBenchPosition(ItemBenchSlotCount - 1) + new Vector3(0f, rowSpacing, 0f);
        return basePosition + new Vector3((overflowIndex % 2) * columnSpacing, (overflowIndex / 2) * rowSpacing, 0f);
    }

    // 古いシーンに保存されている小さすぎる値を、見やすい現在値へ補正します。
    private void NormalizeItemBenchVisualSettings()
    {
        itemBenchColumns = Mathf.Max(itemBenchColumns, useCanvasItemBench ? 2 : 1);
        itemBenchColumnSpacing = Mathf.Max(itemBenchColumnSpacing, 0.96f);
        itemBenchRowSpacing = Mathf.Max(itemBenchRowSpacing, 0.96f);
        itemBenchGapFromUnitBench = Mathf.Max(itemBenchGapFromUnitBench, 1.45f);
        itemBenchLeftEdgeMargin = Mathf.Clamp(itemBenchLeftEdgeMargin, 0.55f, 0.75f);
        itemBenchPickRadius = Mathf.Max(itemBenchPickRadius, 0.68f);

        if (itemIconScale.x < 1f || itemIconScale.y < 1f)
            itemIconScale = new Vector3(1.25f, 1.25f, 1f);
    }

    // アイテムベンチ上に既にあるアイテムも、補正後の位置とサイズへ揃え直します。
    private void RepositionItemBenchItems()
    {
        foreach (KeyValuePair<ItemInstance, int> pair in itemBenchSlotByItem.ToList())
        {
            ItemInstance item = pair.Key;
            if (item == null)
                continue;

            item.transform.position = GetItemBenchPosition(pair.Value);
            item.transform.localScale = itemIconScale;
            item.SetSlotIndex(pair.Value);
            item.SetWorldVisible(!useCanvasItemBench);
        }

        int overflowIndex = 0;
        for (int i = 0; i < itemBenchItems.Count; i++)
        {
            ItemInstance item = itemBenchItems[i];
            if (item == null || itemBenchSlotByItem.ContainsKey(item))
                continue;

            item.transform.position = GetItemBenchOverflowPosition(overflowIndex);
            item.transform.localScale = itemIconScale;
            item.SetSlotIndex(-1);
            overflowIndex++;
        }

        RefreshItemBenchCanvasUi();
    }

    // Canvas版ベンチ利用時は、古いワールドベンチのタイルとアイテム表示を非表示にします。
    private void SetItemBenchWorldVisualsVisible(bool visible)
    {
        if (itemBenchTilesParent != null && itemBenchTilesParent.gameObject.activeSelf != visible)
            itemBenchTilesParent.gameObject.SetActive(visible);

        for (int i = 0; i < itemBenchItems.Count; i++)
        {
            if (itemBenchItems[i] != null)
                itemBenchItems[i].SetWorldVisible(visible || !itemBenchSlotByItem.ContainsKey(itemBenchItems[i]));
        }
    }

    // アイテムベンチ満杯時もアイテムを失わないよう、ワールド上の予備位置へ保持します。
    private void PlaceOverflowItemOnBench(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return;

        EnsureItemBenchParents();
        if (!itemBenchItems.Contains(itemInstance))
            itemBenchItems.Add(itemInstance);

        itemBenchSlotByItem.Remove(itemInstance);

        int overflowIndex = itemBenchItems.Count(item => item != null && !itemBenchSlotByItem.ContainsKey(item) && item != itemInstance);
        itemInstance.transform.SetParent(itemBenchParent, true);
        itemInstance.transform.position = GetItemBenchOverflowPosition(overflowIndex);
        itemInstance.transform.localScale = itemIconScale;
        itemInstance.SetSlotIndex(-1);
        itemInstance.SetWorldVisible(true);

        Debug.LogWarning("Item bench is full. The returned item was placed in an overflow position.");
    }

    // Canvas版アイテムベンチに、現在の所持アイテムを反映します。
    private void RefreshItemBenchCanvasUi()
    {
        if (!useCanvasItemBench)
        {
            SetItemBenchWorldVisualsVisible(true);
            return;
        }

        SetItemBenchWorldVisualsVisible(false);
        ItemBenchCanvasUI.EnsureExists(this).Refresh();
    }

    // 自分ベンチタイルの一番下の中心位置を、アイテムベンチの基準にします。
    private Vector3 GetTeam1BenchBottomAnchor()
    {
        if (team1BenchTilesParent == null || team1BenchTilesParent.childCount == 0)
            return benchStartPosition;

        Vector3 anchor = team1BenchTilesParent.GetChild(0).position;
        for (int i = 1; i < team1BenchTilesParent.childCount; i++)
        {
            Vector3 position = team1BenchTilesParent.GetChild(i).position;
            if (position.y < anchor.y)
                anchor = position;
        }

        return anchor;
    }

    // 既存ベンチタイルの間隔を読める場合はそれを使い、読めない場合は設定値を使います。
    private float GetEffectiveItemBenchRowSpacing()
    {
        if (team1BenchTilesParent == null || team1BenchTilesParent.childCount < 2)
            return itemBenchRowSpacing;

        List<float> yPositions = new List<float>();
        for (int i = 0; i < team1BenchTilesParent.childCount; i++)
            yPositions.Add(team1BenchTilesParent.GetChild(i).position.y);

        yPositions.Sort();
        float totalSpacing = 0f;
        int spacingCount = 0;
        for (int i = 1; i < yPositions.Count; i++)
        {
            float spacing = Mathf.Abs(yPositions[i] - yPositions[i - 1]);
            if (spacing <= 0.05f)
                continue;

            totalSpacing += spacing;
            spacingCount++;
        }

        if (spacingCount == 0)
            return itemBenchRowSpacing;

        return Mathf.Max(itemBenchRowSpacing, totalSpacing / spacingCount);
    }

    // 横列も縦列と同じくらいの間隔にして、斜めに見えない格子へ揃えます。
    private float GetEffectiveItemBenchColumnSpacing(float rowSpacing)
    {
        return Mathf.Max(itemBenchColumnSpacing, rowSpacing);
    }

    // 空いているアイテムベンチスロットを探します。
    private int GetFreeItemBenchSlot()
    {
        for (int i = 0; i < ItemBenchSlotCount; i++)
        {
            if (!itemBenchSlotByItem.ContainsValue(i))
                return i;
        }

        return -1;
    }

    // 指定スロットにいるアイテムを返します。
    private ItemInstance GetItemBenchItemAtSlot(int slotIndex)
    {
        foreach (KeyValuePair<ItemInstance, int> pair in itemBenchSlotByItem)
        {
            if (pair.Key != null && pair.Value == slotIndex)
                return pair.Key;
        }

        return null;
    }

    // ItemDataから、ドラッグ可能なアイテムGameObjectを作ります。
    private ItemInstance CreateItemInstance(ItemData itemData, int slotIndex)
    {
        GameObject itemObject = new GameObject(itemData != null ? $"Item_{itemData.displayName}" : "Item");
        itemObject.transform.SetParent(itemBenchParent != null ? itemBenchParent : transform);
        itemObject.transform.localScale = itemIconScale;

        ItemInstance itemInstance = itemObject.AddComponent<ItemInstance>();
        itemObject.AddComponent<DraggableItem>();
        itemInstance.Setup(itemData, slotIndex);
        return itemInstance;
    }

    // 開発中にすぐ確認できるよう、初回だけ全アイテムをベンチへ並べます。
    private void SpawnDebugItemsIfNeeded()
    {
        if (!spawnDebugItemsOnStart || itemBenchItems.Count > 0)
            return;

        IReadOnlyList<ItemData> items = ItemCatalog.AllItems;
        int count = Mathf.Min(items.Count, ItemBenchSlotCount);
        for (int i = 0; i < count; i++)
            ReturnItemToBench(items[i]);
    }

    // 空いているベンチスロット番号を返します。空きがなければ-1です。
    private int GetFreeBenchSlot()
    {
        int max = EffectiveBenchSlotCount;
        for (int i = 0; i < max; i++)
        {
            if (!benchSlotByEntity.ContainsValue(i))
                return i;
        }

        return -1;
    }

    // 指定スロットにいるベンチユニットを返します。
    private BaseEntity GetBenchEntityAtSlot(int slotIndex)
    {
        foreach (KeyValuePair<BaseEntity, int> pair in benchSlotByEntity)
        {
            if (pair.Value == slotIndex)
                return pair.Key;
        }

        return null;
    }

    // 盤面操作に使うカメラを返します。未設定ならMain Cameraを使います。
    private Camera GetBoardCamera()
    {
        return boardCamera != null ? boardCamera : Camera.main;
    }

    // カメラの現在倍率を、滑らかズーム用の目標値として初期化します。
    private void EnsureCameraZoomTarget(Camera targetCamera)
    {
        if (targetCamera == null || hasCameraZoomTarget)
            return;

        targetCameraFieldOfView = targetCamera.fieldOfView;
        targetOrthographicSize = targetCamera.orthographicSize;
        hasCameraZoomTarget = true;
    }

    // マウスホイールの入力から、盤面カメラの目標倍率を更新します。
    private void HandleMouseWheelZoom(Camera targetCamera)
    {
        if (!enableMouseWheelZoom)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.01f)
            return;

        // UI上でホイールした時はショップ操作の邪魔をしないようにします。
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (targetCamera == null)
            return;

        EnsureCameraZoomTarget(targetCamera);

        if (targetCamera.orthographic)
        {
            targetOrthographicSize = Mathf.Clamp(
                targetOrthographicSize - scroll * mouseWheelZoomSpeed * 0.1f,
                minOrthographicSize,
                maxOrthographicSize);
            return;
        }

        targetCameraFieldOfView = Mathf.Clamp(
            targetCameraFieldOfView - scroll * mouseWheelZoomSpeed,
            minCameraFieldOfView,
            maxCameraFieldOfView);
    }

    // 現在倍率を目標倍率へ少しずつ近づけ、ズームの見た目を滑らかにします。
    private void ApplySmoothCameraZoom(Camera targetCamera)
    {
        if (targetCamera == null)
            return;

        EnsureCameraZoomTarget(targetCamera);

        // Time.deltaTimeに左右されすぎない補間率にして、低FPSでも自然に追従させます。
        float interpolation = 1f - Mathf.Exp(-cameraZoomSmoothSpeed * Time.deltaTime);

        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, targetOrthographicSize, interpolation);
            return;
        }

        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetCameraFieldOfView, interpolation);
    }

    // ホイールクリックを押しながらドラッグした量に合わせて盤面カメラを移動します。
    private void HandleMiddleMousePan(Camera targetCamera)
    {
        if (!enableMiddleMousePan)
            return;

        if (targetCamera == null)
            return;

        // ホイールクリックを押した瞬間に、ドラッグ移動を開始します。
        if (Input.GetMouseButtonDown(2))
        {
            // UI上で押し始めた場合は、ショップなどの操作を邪魔しないようにします。
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                isMiddleMousePanning = false;
                return;
            }

            isMiddleMousePanning = true;
            lastMiddleMousePosition = Input.mousePosition;
            return;
        }

        // ホイールクリックを離したら、ドラッグ移動を終了します。
        if (Input.GetMouseButtonUp(2))
        {
            isMiddleMousePanning = false;
            return;
        }

        if (!isMiddleMousePanning || !Input.GetMouseButton(2))
            return;

        Vector3 currentMousePosition = Input.mousePosition;
        Vector3 worldDelta = GetCameraPanWorldDelta(targetCamera, lastMiddleMousePosition, currentMousePosition);

        // マウスでつかんだ盤面がそのまま動く感覚になるよう、カメラはドラッグ方向と逆へ動かします。
        Vector3 cameraMove = -worldDelta * middleMousePanSpeed;
        cameraMove.z = 0f;
        targetCamera.transform.position += cameraMove;

        lastMiddleMousePosition = currentMousePosition;
    }

    // 背景範囲に使うRendererが未設定なら、名前から自動で探します。
    private void EnsureCameraBoundsRenderer()
    {
        if (cameraBoundsRenderer != null)
            return;

        if (string.IsNullOrEmpty(cameraBoundsObjectName))
            return;

        GameObject boundsObject = GameObject.Find(cameraBoundsObjectName);
        if (boundsObject == null)
            return;

        cameraBoundsRenderer = boundsObject.GetComponent<Renderer>();
    }

    // カメラの画面端が背景外へ出ないよう、カメラ位置を補正します。
    private void ClampCameraInsideBackground(Camera targetCamera)
    {
        if (!clampCameraToBackground || targetCamera == null)
            return;

        EnsureCameraBoundsRenderer();
        if (cameraBoundsRenderer == null)
            return;

        Vector2 correction = GetCameraBoundsCorrection(targetCamera, cameraBoundsRenderer.bounds);
        if (correction.sqrMagnitude <= 0.000001f)
            return;

        targetCamera.transform.position += new Vector3(correction.x, correction.y, 0f);
    }

    // 現在のカメラ表示範囲が背景からはみ出している分だけ、戻す移動量を計算します。
    private Vector2 GetCameraBoundsCorrection(Camera targetCamera, Bounds backgroundBounds)
    {
        Vector2 viewMin;
        Vector2 viewMax;
        if (!TryGetCameraViewBoundsOnBoardPlane(targetCamera, out viewMin, out viewMax))
            return Vector2.zero;

        float allowedMinX = backgroundBounds.min.x + cameraBoundsPadding.x;
        float allowedMaxX = backgroundBounds.max.x - cameraBoundsPadding.x;
        float allowedMinY = backgroundBounds.min.y + cameraBoundsPadding.y;
        float allowedMaxY = backgroundBounds.max.y - cameraBoundsPadding.y;

        float correctionX = GetAxisBoundsCorrection(viewMin.x, viewMax.x, allowedMinX, allowedMaxX);
        float correctionY = GetAxisBoundsCorrection(viewMin.y, viewMax.y, allowedMinY, allowedMaxY);
        return new Vector2(correctionX, correctionY);
    }

    // 1軸分の表示範囲を、許可範囲内へ戻すための移動量を返します。
    private float GetAxisBoundsCorrection(float viewMin, float viewMax, float allowedMin, float allowedMax)
    {
        float viewSize = viewMax - viewMin;
        float allowedSize = allowedMax - allowedMin;

        // ズームアウトしすぎて表示範囲の方が広い場合は、中心だけ合わせます。
        if (viewSize >= allowedSize)
            return ((allowedMin + allowedMax) * 0.5f) - ((viewMin + viewMax) * 0.5f);

        if (viewMin < allowedMin)
            return allowedMin - viewMin;

        if (viewMax > allowedMax)
            return allowedMax - viewMax;

        return 0f;
    }

    // カメラの四隅が盤面平面上でどこに見えているかを調べます。
    private bool TryGetCameraViewBoundsOnBoardPlane(Camera targetCamera, out Vector2 viewMin, out Vector2 viewMax)
    {
        viewMin = Vector2.zero;
        viewMax = Vector2.zero;

        Vector3 bottomLeft;
        Vector3 topLeft;
        Vector3 bottomRight;
        Vector3 topRight;

        if (!TryViewportPointToBoardPlane(targetCamera, new Vector3(0f, 0f, 0f), out bottomLeft)
            || !TryViewportPointToBoardPlane(targetCamera, new Vector3(0f, 1f, 0f), out topLeft)
            || !TryViewportPointToBoardPlane(targetCamera, new Vector3(1f, 0f, 0f), out bottomRight)
            || !TryViewportPointToBoardPlane(targetCamera, new Vector3(1f, 1f, 0f), out topRight))
        {
            return false;
        }

        viewMin = new Vector2(
            Mathf.Min(bottomLeft.x, topLeft.x, bottomRight.x, topRight.x),
            Mathf.Min(bottomLeft.y, topLeft.y, bottomRight.y, topRight.y));

        viewMax = new Vector2(
            Mathf.Max(bottomLeft.x, topLeft.x, bottomRight.x, topRight.x),
            Mathf.Max(bottomLeft.y, topLeft.y, bottomRight.y, topRight.y));

        return true;
    }

    // 画面上のマウス移動量を、盤面上のワールド座標移動量へ変換します。
    private Vector3 GetCameraPanWorldDelta(Camera targetCamera, Vector3 previousScreenPosition, Vector3 currentScreenPosition)
    {
        Vector3 previousWorldPosition;
        Vector3 currentWorldPosition;

        if (TryScreenPointToBoardPlane(targetCamera, previousScreenPosition, out previousWorldPosition)
            && TryScreenPointToBoardPlane(targetCamera, currentScreenPosition, out currentWorldPosition))
        {
            return currentWorldPosition - previousWorldPosition;
        }

        // 万が一盤面平面にRayが当たらない場合も、2Dカメラとして最低限動くようにします。
        float distanceFromBoard = Mathf.Abs(targetCamera.transform.position.z);
        previousScreenPosition.z = distanceFromBoard;
        currentScreenPosition.z = distanceFromBoard;
        previousWorldPosition = targetCamera.ScreenToWorldPoint(previousScreenPosition);
        currentWorldPosition = targetCamera.ScreenToWorldPoint(currentScreenPosition);
        return currentWorldPosition - previousWorldPosition;
    }

    // マウス位置から盤面のあるZ=0平面上の座標を取得します。
    private bool TryScreenPointToBoardPlane(Camera targetCamera, Vector3 screenPosition, out Vector3 worldPosition)
    {
        Plane boardPlane = new Plane(Vector3.forward, Vector3.zero);
        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        float distance;
        if (boardPlane.Raycast(ray, out distance))
        {
            worldPosition = ray.GetPoint(distance);
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    // Viewport座標から盤面のあるZ=0平面上の座標を取得します。
    private bool TryViewportPointToBoardPlane(Camera targetCamera, Vector3 viewportPosition, out Vector3 worldPosition)
    {
        Plane boardPlane = new Plane(Vector3.forward, Vector3.zero);
        Ray ray = targetCamera.ViewportPointToRay(viewportPosition);

        float distance;
        if (boardPlane.Raycast(ray, out distance))
        {
            worldPosition = ray.GetPoint(distance);
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    // 指定Nodeにいる味方ユニットを探します。
    private BaseEntity GetTeam1EntityAtNode(Node node)
    {
        if (node == null)
            return null;

        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity entity = team1Entities[i];
            if (entity != null && entity.CurrentNode == node)
                return entity;
        }

        return null;
    }

    // このユニットを今買うとスターアップが完成するかを返します。
    public bool CanCompleteUpgradeWithPurchase(string unitId)
    {
        return GetUpgradePreviewStarWithPurchase(unitId) > 0;
    }

    // このユニットを1体買った後、★2または★3が作れるかを予測します。
    public int GetUpgradePreviewStarWithPurchase(string unitId)
    {
        if (string.IsNullOrEmpty(unitId))
            return 0;

        UpgradeScope scope = GetImmediateUpgradeScope();

        // 購入予定の★1を1体足して計算します。
        int star1Count = CountOwnedUnits(unitId, 1, scope) + 1;
        int star2Count = CountOwnedUnits(unitId, 2, scope);
        int previewStarLevel = 0;

        // ★1が3体揃うなら★2が1体増える扱いにします。
        if (star1Count >= 3)
        {
            star1Count -= 3;
            star2Count++;
            previewStarLevel = 2;
        }

        // ★2が3体揃うなら★3予告です。
        if (star2Count >= 3)
            previewStarLevel = 3;

        return previewStarLevel;
    }

    // ユニットを売った時に戻るお金を計算します。
    public int GetSellValue(BaseEntity entity)
    {
        if (entity == null)
            return 0;

        int starMultiplier = 1;
        if (entity.StarLevel == 2)
            starMultiplier = 3;
        else if (entity.StarLevel >= 3)
            starMultiplier = 8;

        return Mathf.Max(1, entity.BaseCost) * starMultiplier;
    }

    // ユニットを売却し、お金を増やします。
    public bool TrySellEntity(BaseEntity entity)
    {
        if (entity == null || entity.Team != Team.Team1 || entity.IsSummonedUnit || PlayerData.Instance == null)
            return false;

        int sellValue = GetSellValue(entity);
        ReturnItemsToBench(entity.RemoveAllItems());
        RemoveOwnedEntity(entity, false);
        PlayerData.Instance.AddMoney(sellValue);
        OnRosterChanged?.Invoke();
        return true;
    }

    // 指定ユニットを起点に、同名・同スターが3体揃っていればスターアップします。
    private void ResolveUpgradesFor(BaseEntity preferredEntity, UpgradeScope scope)
    {
        if (preferredEntity == null || preferredEntity.Team != Team.Team1)
            return;

        BaseEntity current = preferredEntity;
        bool upgraded;
        do
        {
            upgraded = TryUpgradeOneGroup(current.UnitId, current.StarLevel, current, scope, out current);
        }
        while (upgraded && current != null && current.StarLevel < 3);
    }

    // 現在すでに成立しているスターアップを、可能な限りまとめて処理します。
    private void ResolveAllAvailableUpgrades(UpgradeScope scope)
    {
        bool upgraded;
        int safetyCounter = 0;

        do
        {
            upgraded = false;
            List<BaseEntity> upgradeCandidates = GetUpgradeCandidateUnits(scope);

            var readyGroup = upgradeCandidates
                .Where(entity => entity != null && entity.StarLevel < 3)
                .GroupBy(entity => new UpgradeGroupKey(entity.UnitId, entity.StarLevel))
                .FirstOrDefault(group => group.Count() >= 3);

            if (readyGroup == null)
                break;

            BaseEntity preferredEntity = ChooseUpgradeTarget(readyGroup.ToList(), null, scope);
            if (TryUpgradeOneGroup(readyGroup.Key.UnitId, readyGroup.Key.StarLevel, preferredEntity, scope, out BaseEntity upgradedEntity))
                upgraded = upgradedEntity != null;

            safetyCounter++;
        }
        while (upgraded && safetyCounter < 100);
    }

    // 1段階分のスターアップを試します。成功すると2体を消費し、1体を強化します。
    private bool TryUpgradeOneGroup(string unitId, int starLevel, BaseEntity preferredEntity, UpgradeScope scope, out BaseEntity upgradedEntity)
    {
        upgradedEntity = preferredEntity;

        if (starLevel >= 3)
            return false;

        List<BaseEntity> matches = GetOwnedUnits(unitId, starLevel, scope);
        if (matches.Count < 3)
            return false;

        // 戦闘中の購入では盤面ユニットを巻き込まないよう、もう一度ベンチ実体だけに絞ります。
        if (scope == UpgradeScope.BenchOnly)
            matches = matches.Where(IsBenchUpgradeCandidate).ToList();

        if (matches.Count < 3)
            return false;

        BaseEntity target = ChooseUpgradeTarget(matches, preferredEntity, scope);
        if (target == null)
            return false;

        // 念のため、戦闘中に盤面ユニットを残すスターアップは絶対に通さないようにします。
        if (scope == UpgradeScope.BenchOnly && target.IsOnBoard)
        {
            Debug.LogWarning($"Skipped in-combat upgrade for {unitId}: board unit was selected as target.");
            return false;
        }

        List<BaseEntity> consumed = matches.Where(entity => entity != target).Take(2).ToList();

        // 残す1体以外の2体を削除します。消える側のアイテムは、残るユニットへ移せるだけ移します。
        for (int i = 0; i < consumed.Count; i++)
        {
            MoveOrReturnItems(consumed[i], target);
            RemoveOwnedEntity(consumed[i], false);
        }

        // 残したユニットの星とステータスをBaseEntity側で更新します。
        target.ApplyStarLevel(starLevel + 1);
        upgradedEntity = target;
        return true;
    }

    // スターアップ後に残すユニットを選びます。
    private BaseEntity ChooseUpgradeTarget(List<BaseEntity> matches, BaseEntity preferredEntity, UpgradeScope scope)
    {
        if (matches == null || matches.Count == 0)
            return null;

        // 戦闘後など盤面も合成対象にしてよい時だけ、盤面ユニットを残す候補にします。
        if (scope == UpgradeScope.AllOwned)
        {
            BaseEntity boardEntity = matches.FirstOrDefault(entity => entity.IsOnBoard);
            if (boardEntity != null)
                return boardEntity;
        }

        BaseEntity slottedBenchEntity = matches.FirstOrDefault(entity => entity != preferredEntity && benchSlotByEntity.ContainsKey(entity));
        if (slottedBenchEntity != null)
            return slottedBenchEntity;

        if (preferredEntity != null && matches.Contains(preferredEntity))
            return preferredEntity;

        return matches[0];
    }

    // 味方の盤面とベンチから、指定ID・指定スターのユニットを集めます。
    private List<BaseEntity> GetOwnedUnits(string unitId, int starLevel, UpgradeScope scope)
    {
        return GetUpgradeCandidateUnits(scope)
            .Where(entity => entity != null && entity.Team == Team.Team1 && entity.UnitId == unitId && entity.StarLevel == starLevel)
            .Distinct()
            .ToList();
    }

    // 指定ID・指定スターの所有数を数えます。
    private int CountOwnedUnits(string unitId, int starLevel, UpgradeScope scope)
    {
        return GetOwnedUnits(unitId, starLevel, scope).Count;
    }

    // 今すぐ実行できるスターアップの対象範囲を返します。
    private UpgradeScope GetImmediateUpgradeScope()
    {
        return IsRoundInProgress ? UpgradeScope.BenchOnly : UpgradeScope.AllOwned;
    }

    // スターアップ候補として数えるユニット一覧を返します。
    private List<BaseEntity> GetUpgradeCandidateUnits(UpgradeScope scope)
    {
        if (scope == UpgradeScope.BenchOnly)
            return benchEntities.Where(IsBenchUpgradeCandidate).Distinct().ToList();

        return team1Entities
            .Concat(benchEntities)
            .Where(entity => entity != null && entity.Team == Team.Team1 && !entity.IsSummonedUnit)
            .Distinct()
            .ToList();
    }

    // 戦闘中に即時スターアップへ使ってよい、実際にベンチ側にいる味方ユニットだけを判定します。
    private bool IsBenchUpgradeCandidate(BaseEntity entity)
    {
        bool isAssignedToBenchSlot = entity != null && benchSlotByEntity.ContainsKey(entity);
        bool isUnderBenchParent = entity != null && benchParent != null && entity.transform.IsChildOf(benchParent);

        return entity != null
            && entity.Team == Team.Team1
            && !entity.IsSummonedUnit
            && !entity.IsOnBoard
            && (isAssignedToBenchSlot || isUnderBenchParent);
    }

    // 所有ユニットをリスト、Node、ベンチ辞書から外して破棄します。
    private void RemoveOwnedEntity(BaseEntity entity, bool returnItems = true)
    {
        if (entity == null)
            return;

        if (returnItems)
            ReturnItemsToBench(entity.RemoveAllItems());

        if (entity.CurrentNode != null)
            entity.CurrentNode.SetOccupied(false);

        team1Entities.Remove(entity);
        benchEntities.Remove(entity);
        benchSlotByEntity.Remove(entity);
        Destroy(entity.gameObject);
    }

    // スターアップで消費されるユニットの装備を、残るユニットへ移し、入らない分はベンチへ戻します。
    private void MoveOrReturnItems(BaseEntity source, BaseEntity target)
    {
        if (source == null)
            return;

        List<ItemData> removedItems = source.RemoveAllItems();
        for (int i = 0; i < removedItems.Count; i++)
        {
            ItemData item = removedItems[i];
            if (target != null && target.HasItemSpace && target.TryEquipItem(item))
                continue;

            ReturnItemToBench(item);
        }
    }

    // どちらかのチームが全滅したら戦闘終了イベントを出します。
    private void TryEndRound()
    {
        if (!IsRoundInProgress)
            return;

        bool playerHasBattleUnit = HasLivingPlayerBattleUnit();
        bool enemyHasBattleUnit = HasLivingEnemyBattleUnit();

        if (playerHasBattleUnit && enemyHasBattleUnit)
            return;

        if (!playerHasBattleUnit)
        {
            TriggerGameOver();
            return;
        }

        if (!enemyHasBattleUnit)
            CompleteCurrentWave();
    }

    // スターアップ判定で、盤面を含めるかベンチだけを見るかを表します。
    private enum UpgradeScope
    {
        AllOwned,
        BenchOnly
    }

    // 同名・同スターのグループを作るためのキーです。
    private struct UpgradeGroupKey
    {
        public readonly string UnitId;
        public readonly int StarLevel;

        public UpgradeGroupKey(string unitId, int starLevel)
        {
            UnitId = unitId;
            StarLevel = starLevel;
        }
    }

    // ウェーブで使う敵の大まかな種類です。
    private enum WaveEnemyKind
    {
        Cost1Melee,
        Cost1Ranged,
        Cost2Melee,
        Cost2Ranged,
        FixedUnit
    }

    // 敵が死亡した時に渡すドロップの内訳です。
    private readonly struct EnemyDrop
    {
        public readonly int Coins;
        public readonly bool Item;
        public EnemyDrop(int coins, bool item) { Coins = coins; Item = item; }
    }

    // ウェーブ内の敵1体分の配置データです。
    // CandidateIndexが0以上なら、条件に合う候補を名前順に並べた時の指定番号を使います。
    // DropCoins/DropItem は撃破時にプレイヤーへ渡す報酬です（雑魚戦用）。
    private struct WaveEnemyPlacement
    {
        public readonly WaveEnemyKind Kind;
        public readonly string UnitId;
        public readonly int StarLevel;
        public readonly int Column;
        public readonly int Row;
        public readonly int CandidateIndex;
        public readonly bool IsDebugTrainingDummy;
        public readonly int DropCoins;
        public readonly bool DropItem;

        public WaveEnemyPlacement(WaveEnemyKind kind, int starLevel, int column, int row, int candidateIndex = -1, bool isDebugTrainingDummy = false, int dropCoins = 0, bool dropItem = false)
        {
            Kind = kind;
            UnitId = string.Empty;
            StarLevel = starLevel;
            Column = column;
            Row = row;
            CandidateIndex = candidateIndex;
            IsDebugTrainingDummy = isDebugTrainingDummy;
            DropCoins = dropCoins;
            DropItem = dropItem;
        }

        public WaveEnemyPlacement(string unitId, int starLevel, int column, int row, int dropCoins = 0, bool dropItem = false)
        {
            Kind = WaveEnemyKind.FixedUnit;
            UnitId = unitId;
            StarLevel = starLevel;
            Column = column;
            Row = row;
            CandidateIndex = -1;
            IsDebugTrainingDummy = false;
            DropCoins = dropCoins;
            DropItem = dropItem;
        }
    }

    // 1ラウンド分の定義です。敵配置（通常/ボス）か、戦闘なしのイベントラウンドを表します。
    private class WaveDefinition
    {
        public readonly List<WaveEnemyPlacement> Enemies = new List<WaveEnemyPlacement>();
        public readonly bool IsBossWave;
        public readonly bool IsDebugWave;
        public readonly WaveEventType EventType;

        // ステージ情報（E1+雑魚機能）。0なら未指定。RoundProgressUIや章クリア表示に使います。
        public int StageIndex;
        public int RoundInStage;
        // 章ボスではないが強敵の中ボス。撃破でショップのコスト上限を解放しますが、報酬選択は出しません。
        public bool IsMidBossWave;

        // 戦闘を行わないイベントラウンドかどうか。
        public bool IsEventRound => EventType != WaveEventType.None;

        public WaveDefinition(params WaveEnemyPlacement[] enemies)
        {
            Enemies.AddRange(enemies);
        }

        public WaveDefinition(bool isBossWave, params WaveEnemyPlacement[] enemies)
        {
            IsBossWave = isBossWave;
            Enemies.AddRange(enemies);
        }

        public WaveDefinition(bool isBossWave, bool isDebugWave, params WaveEnemyPlacement[] enemies)
        {
            IsBossWave = isBossWave;
            IsDebugWave = isDebugWave;
            Enemies.AddRange(enemies);
        }

        // 戦闘なしのイベントラウンドを作ります。
        public WaveDefinition(WaveEventType eventType)
        {
            EventType = eventType;
        }
    }
}

// 戦闘を行わないイベントラウンドの種別です（E1 / E3）。
public enum WaveEventType
{
    None,
    BonusItem,
    BonusGold,
    AugmentSilver,
    AugmentGold,
    AugmentPrism
}

// ユニットがどちらの陣営に属しているかを表す列挙型です。
public enum Team
{
    Team1,
    Team2
}
