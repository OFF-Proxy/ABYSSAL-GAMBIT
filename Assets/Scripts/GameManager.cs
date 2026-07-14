using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
    // R2-coremode: 各陣営のコア（拠点）。Team1=自陣, Team2=敵陣。team*Entities にも含める。
    BaseEntity playerCore;
    BaseEntity enemyCore;
    bool coreModeResolved;
    int coreWavesCleared;
    Coroutine coreAutoAdvanceRoutine;
    // コア戦の自動進行タイマー（秒）。
    const float CoreModeIntervalSeconds = 5f;
    const float CoreModeBuildSeconds = 40f;
    const int CoreModeBossMilestone = 5;   // 何波クリアごとにボスを1体解放するか。

    // ② 配置フォーメーションのライブ表示（編成中にマスを光らせて完成を知らせる）。
    private Transform formationMarkerRoot;
    private readonly List<SpriteRenderer> formationMarkers = new List<SpriteRenderer>();
    private Sprite formationMarkerSprite;
    private string lastFormationSignature = "";
    private bool formationHintShown;

    // R2-rewards: 強化マス（チャプター中永続・累積）。
    private class BuffTile { public Node Node; public BuffTileType Type; }
    private readonly List<BuffTile> buffTiles = new List<BuffTile>();
    private bool buffTileSelectMode;
    private BuffTileType pendingBuffTileType;
    private Transform buffTileMarkerRoot;
    private readonly List<SpriteRenderer> buffTileMarkers = new List<SpriteRenderer>();
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
        "Grymbeast",
        "Solfist",
        "Dissonance"
    };
    readonly HashSet<string> unlockedBossRewardUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // R1-meta: 章ボス（章クリアで永続 roster に加わるユニット）の定義。BuildChapterRounds と整合します。
    // 章1の章ボス = 4-10 で出てくる "Legion"。章を増やす際はここに追加します。
    private static readonly Dictionary<int, string> ChapterBossUnitIds = new Dictionary<int, string>
    {
        { 1, "Legion" },
        { 2, "Skyfalltyrant" },
    };

    // ロビー（章選択）からシーン再読込で開始章を渡すための受け渡し変数。0なら通常起動（既存挙動）。
    public static int PendingStartChapter = 0;

    // R2-coremode: ゲームモード。既定 Chapter（従来挙動）。ロビーからシーン再読込で受け渡す。
    public enum GameMode { Chapter, CoreAssault }
    public static GameMode PendingMode = GameMode.Chapter;
    public GameMode CurrentMode { get; private set; } = GameMode.Chapter;
    public bool IsCoreMode => CurrentMode == GameMode.CoreAssault;
    // 起動時にロビー画面を出すか。既定 false（＝従来どおり即ゲーム開始）。Claude Code がエディタ確認後に true 化する想定。
    public bool showLobbyOnBoot = false;
    // 「ロビーへ戻る」導線用。シーン再読込をまたいで一度だけ起動ロビーを出すための静的フラグ。
    // RequestReturnToLobby が立て、Start で消費する（showLobbyOnBoot が false でも有効）。
    public static bool ForceLobbyOnNextBoot = false;
    // 「ロビーへ戻る」時に、タイトルではなくメインロビー（ハブ）を出すための静的フラグ。
    // RequestReturnToLobby が立て、LobbyUI 側で一度だけ消費する。
    public static bool ReturnToMainLobbyOnBoot = false;

    public static string GetChapterBossUnitId(int chapter)
    {
        return ChapterBossUnitIds.TryGetValue(chapter, out string id) ? id : null;
    }

    // === R2-recruit: 中ボス/章ボスの仲間化・ショップ解放（DESIGN_R2-recruit.md） ===
    // (B) チャプター内のみ有効な解放（中ボス報酬）。章開始＝シーン再読込で新インスタンスになり自動リセット。
    private readonly HashSet<string> chapterUnlockedUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // 最初からショップに出るコスト3（暫定・R3-balance）。これ以外のcost3とcost4/5は既定ロック。
    private static readonly HashSet<string> Cost3StarterPlayable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "Shadowlord", "Kane", "Wolfpunch", "Paragon" };


    // 章ボス報酬（恒久解放されるユニット）。ch1-5=cost4 / ch6+=cost5。倒したボス＝もらえるユニット。
    // CLAUDE_HANDOFF_CHAPTER20_BOSSES.md: 20章構成。1-20 欠番なし。
    // 1=Caliber / 2=neutral_rook / 3=neutral_sister / 4-6=Magmar / 7-9=Abyssian / 10-12=Vetruvian /
    // 13-18=Mechaz0r連章 / 19=neutral_hydrax / 20=Arcana（最終章）。
    private static readonly Dictionary<int, string> ChapterBossRewardUnitIds = new Dictionary<int, string>
    {
        { 1, "Caliber" }, { 2, "neutral_rook" }, { 3, "neutral_sister" },
        { 4, "Magmarvaath" }, { 5, "Magmarstarhorn" }, { 6, "Magmarragnora" },
        { 7, "Abyssallilithe" }, { 8, "Abyssalcassyva" }, { 9, "Abyssalmaehv" },
        { 10, "Vetruvianzirix" }, { 11, "Vetruviansajj" }, { 12, "Vetruvianscion" },
        { 13, "neutral_mechaz0rwing" }, { 14, "neutral_mechaz0rsword" }, { 15, "neutral_mechaz0rsuper" },
        { 16, "neutral_mechaz0rhelm" }, { 17, "neutral_mechaz0rchassis" }, { 18, "neutral_mechaz0rcannon" },
        { 19, "neutral_hydrax" }, { 20, "Arcana" },
    };

    public static string GetChapterBossRewardUnitId(int chapter)
    {
        return ChapterBossRewardUnitIds.TryGetValue(chapter, out string id) ? id : null;
    }

    // R1-collection: 図鑑用。章ボス報酬ユニットID一覧を章順で返す（収集対象の永続ボス）。
    public static System.Collections.Generic.List<string> GetAllChapterBossRewardUnitIds()
    {
        var list = new System.Collections.Generic.List<string>();
        int ch = 1;
        while (ChapterBossRewardUnitIds.ContainsKey(ch))
        {
            list.Add(ChapterBossRewardUnitIds[ch]);
            ch++;
        }
        return list;
    }

    // そのユニットが恒久解放済みか（章ボス報酬としてrosterに入っているか）。
    private bool IsPermanentlyUnlocked(string unitId)
    {
        return SaveManager.Instance != null && SaveManager.Instance.HasBossAlly(unitId);
    }

    // ショップに出る最大コスト（E1）。序盤はコスト3まで。ボス撃破やイベントで段階的に解放します。
    public int baseMaxShopCost = 3;
    public int maxShopCostCap = 5;
    public int MaxAvailableShopCost { get; private set; } = 3;

    // イベントラウンドのボーナスゴールド額です。
    public int eventBonusGold = 14;

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
    // 勝利演出（祝福インターバル）中は配置復元/次戦を保留する。
    bool waveClearCelebrating;
    const float WaveClearCelebrationSeconds = 1.9f;

    // ヒーロー（主人公）：毎ラン初手に確定付与し、1戦1回の必殺（味方全体バフ）を持つ。
    public string heroUnitId = "Wolfpunch";
    private bool heroUltUsedThisWave;                      // 同一ラウンド内の二重発動防止。
    private int heroUltCooldownRemaining;                  // 必殺クールタイム（残りラウンド数）。0で使用可。主人公ごとに長さが違う。
    // R3-hero-depth: ヒーロー深掘り要素の実行時状態。
    public int HeroUltUpgrade { get; private set; }       // 0=未強化 / 1=A / 2=B（ラン中1回選択）
    private bool pendingHeroUltUpgrade;                    // ラン開始時に選択UIを出す
    private bool heroReviveUsedThisWave;                   // 主人公は将：1戦1回の自動復活
    private bool heroFallenWeakenApplied;                  // 復活後に再度倒れた時の味方弱体（1戦1回）
    // R3-hero-scale: ラン内でヒーローが育つ（撃破/ラウンド進行/盤面シナジーでLvアップ→ステ＋必殺強化、節目で自動★アップ）。
    private int heroRunXp;                                 // ラン内累積XP（撃破数＋ラウンド進行）。
    private int heroRunLevel = 1;                          // ラン内ヒーローLv（XPから算出。ラン毎にリセット）。
    public int HeroRunLevel => heroRunLevel;
    private const int HeroXpPerLevel = 8;                  // このXPごとに1Lv。
    private const int HeroMaxLevel = 20;                   // ラン内Lv上限。

    // 追加ヒーロー/将系の必殺設定。[0]=効果種別 / [1]=JA名 / [2]=EN名。基本3(Aldin/Kagachi/Vesna)は個別実装。
    // 種別: shield(守護シールド)/heal(回復)/rally(攻撃集結)/speed(攻速)/burst(範囲魔法)/drain(吸収)/weaken(弱体)/frost(氷結)。
    private static readonly Dictionary<string, string[]> HeroUltConfig = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        // Lyonar：守護・聖光・王権
        { "HeroZiran",      new[]{ "heal",   "聖癒の光",   "Sacred Light" } },
        { "HeroBrome",      new[]{ "rally",  "王の進軍",   "King's March" } },
        // Songhai：影・連撃・速攻
        { "HeroReva",       new[]{ "speed",  "連射の舞",   "Volley Dance" } },
        { "HeroShidai",     new[]{ "burst",  "影刃乱舞",   "Shadow Flurry" } },
        // Vanar：氷・呪・耐久
        { "HeroKara",       new[]{ "shield", "氷盾の護り", "Frost Aegis" } },
        { "HeroIlena",      new[]{ "frost",  "氷嵐の術",   "Blizzard" } },
        // Magmar：原始・獣・炎
        { "Magmarvaath",    new[]{ "rally",  "原始の咆哮", "Primal Roar" } },
        { "Magmarstarhorn", new[]{ "speed",  "嵐角の突進", "Storm Charge" } },
        { "Magmarragnora",  new[]{ "burst",  "溶岩の咆哮", "Magma Burst" } },
        // Abyssian：死・影・深淵
        { "Abyssallilithe", new[]{ "drain",  "影の眷属",   "Shadow Brood" } },
        { "Abyssalcassyva", new[]{ "weaken", "腐敗の呪い", "Curse of Decay" } },
        { "Abyssalmaehv",   new[]{ "weaken", "深淵の囁き", "Abyss Whisper" } },
        // Vetruvian：機巧・砂・太陽
        { "Vetruvianzirix", new[]{ "shield", "機巧の盾陣", "Aegis Array" } },
        { "Vetruviansajj",  new[]{ "burst",  "太陽の灼熱", "Solar Flare" } },
        { "Vetruvianscion", new[]{ "burst",  "砂塵刃舞",   "Sand Dance" } },
    };
    private bool bossDialogueShownThisWave;                // ボス戦前の掛け合いダイアログを表示済みか（ウェーブ毎）
    private bool enemiesPreviewedThisWave;                 // R3: 編成中に敵を事前配置（入場演出）済みか（ウェーブ毎）
    bool bossRewardSelectionPending;
    bool debugTrainingWaveActive;
    float roundStartTime;
    bool roundTimeoutResolved;
    const float RoundDamageRampStartTime = 15f;
    const float RoundDamageRampSecondStageTime = 45f;
    const float RoundHardLimitSeconds = 60f;
    // 膠着検出：両軍が残っているのに一定時間まったくHPが動かない（=射程外/到達不能で攻撃が発生しない）
    // 場合、60秒ハードタイムアウトを待たずに勝敗を確定する。「ウェーブクリアが数十秒遅れる」対策。
    const float StalemateResolveSeconds = 7f;   // HP無変化がこの秒数続いたら解決。
    const float StalemateMinElapsed = 5f;        // 戦闘開始直後（接近フェーズ）は誤判定しないよう猶予。
    float lastBattleProgressTime;                // 最後にHP合計が変化した時刻。
    float lastBattleHpSum = -1f;                 // 直近のHP合計（両軍・盤上・生存）。
    bool stalematePrevInRound;                   // 前フレームが戦闘中だったか（戦闘開始の検出用）。

    // 現在戦闘中かどうか、ベンチ空き、盤面配置数、配置上限を外部から確認できます。
    public bool IsRoundInProgress { get; private set; }
    // ベンチの実効スロット数（オーグメントによる拡張を含む）。
    public int EffectiveBenchSlotCount => Mathf.Max(1, benchSlotCount + BenchSlotBonus);
    public bool HasBenchSpace => benchEntities.Count < EffectiveBenchSlotCount;
    // R3-hero-scale: ヒーロー（基本3＋採用中ボス）は盤面の配置枠を消費しない（無料枠）。
    public int PlacedTeam1Count => team1Entities.Count(e => e != null && !e.IsCore && !IsHeroUnit(e.UnitId));
    public int PlacementLimit => PlayerData.Instance != null ? PlayerData.Instance.Level : 1;
    // 盤面が「編成準備」状態として安定しているか（戦闘中・勝利演出・各種選択・ボス登場演出・ゲームオーバー・導入演出を除く）。
    // 戦闘直後の盤面復元前に配置数HUDが死亡分の減った数を一瞬出すのを防ぐためにも使う。
    public bool IsPrepPhaseReady =>
        !IsRoundInProgress && !waveClearCelebrating && !waveTransitioning && !bossIntroInProgress
        && !bossRewardSelectionPending && !augmentSelectionPending && !gameOver && !bossCinematicActive
        && !IsStoryIntroBlocking();
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

        // ① 盤面の配置数HUD（配置 X / Y）。表示可否はHUD側でロビー/導入演出を見て制御する。
        BoardCapacityHudUI.EnsureExists();

        // R2-coremode: モードを受け取り消費（既定 Chapter）。
        CurrentMode = PendingMode;
        PendingMode = GameMode.Chapter;

        // ロビーで章が選択されていれば、その章で開始します（シーン再読込で受け渡し）。
        bool startedFromLobbySelection = PendingStartChapter > 0;
        if (startedFromLobbySelection)
        {
            currentChapter = Mathf.Max(1, PendingStartChapter);
            PendingStartChapter = 0;
        }

        // 起動時にロビーを被せる場合は、ラン初期化（初期ユニット付与・章編成・イベント消化）を遅延します。
        // 章を明示選択して来た場合（startedFromLobbySelection）はロビーを出さず、そのまま初期化します。
        bool willShowBootLobby = (showLobbyOnBoot || ForceLobbyOnNextBoot) && !startedFromLobbySelection && !IsCoreMode;

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

        // R4-chapter-background: 章ごとの動的背景（視差レイヤー＋天候パーティクル）を構築する。
        // 旧静的背景 battlemap1_middleground は見た目のみ無効化（boundsはカメラ/ベンチ計算に残す）。
        ChapterBackground.EnsureExists().ApplyChapter(currentChapter);

        // オプションUI（音量・言語・速度・再挑戦）を起動時に用意し、PlayerPrefs設定を反映します。
        OptionsPanelUI.EnsureExists();

        // 獲得したオーグメントを画面右上に常時表示する HUD を用意します（最初は0個）。
        AugmentHudUI.EnsureExists().Refresh();

        // 設定（音量/表示/品質/カーソル/UIサイズ）を GameScene に適用します。
        SettingsStore.ApplyAll();
        // HUD表示設定（synergy/coin/round/tooltip）を適用する常駐コンポーネントを用意します。
        // 起動時ロビー表示中はゲームHUDを先に作らないよう、ロビーを被せない時だけ起動します。
        if (!willShowBootLobby)
            HudSettingsApplier.EnsureExists();

        // ロビー表示中はラン初期化を遅延します（章選択でシーン再読込され、選んだ章で改めて初期化されるため）。
        if (!willShowBootLobby)
        {
            // ロビーから戦闘へ入る時はバトルBGMへ戻す（ロビーでメニュー曲に切替わっているため）。
            AttackEffectPlayer.PlayBattleBgm();

            // C案: チャプターに応じて開始リソースを底上げ（後半章を“強い状態で始める”）。
            // 章モードのみ（コア戦は対象外）。ch1=据え置き / ch2=+1Lv +3G +1体 / ch3=+2Lv +6G +2体。
            if (!IsCoreMode)
            {
                int tier = Mathf.Clamp(currentChapter - 1, 0, 2);
                PlayerData.PendingStartExtraLevels = tier;       // 配置できる数（PlacementLimit=Level）も増える
                PlayerData.PendingStartExtraMoney = tier * 3;
                if (PlayerData.Instance != null)
                    PlayerData.Instance.ResetEconomyForNewRun(); // ボーナス込みで再適用（Start順序に依存しない）

                // 主人公（ヒーロー）を確定付与し、章スケールぶんのランダム初期ユニットを追加。
                ResetHeroRunProgress(); // R3-hero-scale: ラン内ヒーローLvを初期化。
                GrantHeroUnit();
                for (int i = 0; i < tier; i++)
                    GrantStartingUnit();

                // R3-hero-depth: ヒーロー別の開始ボーナス。
                ApplyHeroStartingBonus();
                // R3-hero-mastery: 必殺バリアントは「育成画面で装備した内容（熟練度で解放）」を採用。ラン中の選択UIは廃止。
                HeroUltUpgrade = SaveManager.Instance != null ? SaveManager.Instance.GetHeroEquippedUlt(heroUnitId) : 0;
            }
            else
            {
                GrantHeroUnit();
            }

            // R2-coremode: 自陣・敵陣にコア（拠点）を1基ずつ常設する。
            if (IsCoreMode)
                SpawnCores();

            // アイテムベンチに「アイテム取り外し機」を常設する。
            EnsureRemoverToolInBench();

            // ヒーロー必殺ボタン（戦闘中のみ表示）。
            HeroUltButtonUI.EnsureExists();

            // R2-recruit: ボス仲間は「章開始でベンチ無料配置」ではなく「ショップに恒久出現」する仕様へ変更。
            // よって従来の編成画面（TryShowChapterRoster）は呼ばない（恒久解放は IsEntityUnlockedForShop が参照）。

            // 最初のラウンドがイベントなら消化します。
            TryStartEventRound();

            // STORY: 章開始の導入演出。順序は プロローグ（盤面前の全画面一枚絵＋ナレーション字幕＋専用BGM→暗転）
            //        → オープニングVN（立ち絵）→ ショップ/戦闘。各章ごとに一度だけ。
            if (!IsCoreMode)
            {
                // プロローグ（全画面一枚絵演出）もオープニングVN（立ち絵）も、
                // オプションで無効化しない限り、章開始ごとに毎回再生する。
                bool playIntro = !SettingsStore.SkipPrologue;
                bool wantPrologue = playIntro && ChapterStory.HasPrologue(currentChapter);
                bool wantOpening = playIntro && InterludeScript.HasChapterOpening(currentChapter);

                Debug.Log($"[Prologue] ch={currentChapter} skip={SettingsStore.SkipPrologue} hasPrologue={ChapterStory.HasPrologue(currentChapter)} hasOpening={InterludeScript.HasChapterOpening(currentChapter)} wantPrologue={wantPrologue} wantOpening={wantOpening}");

                // オープニングVNを開く処理（プロローグの後に呼ばれる／プロローグが無ければ直接呼ばれる）。
                System.Action showOpening = null;
                if (wantOpening)
                {
                    showOpening = () => InterludeUI.EnsureExists().Show("CHOPEN_" + currentChapter, heroUnitId, () => { });
                }

                if (wantPrologue)
                {
                    var pr = ChapterStory.GetPrologue(currentChapter);
                    if (pr.HasValue)
                    {
                        Sprite art = Resources.Load<Sprite>(pr.Value.imagePath);
                        Debug.Log($"[Prologue] showing. art={(art != null)} lines={(pr.Value.lines != null ? pr.Value.lines.Length : -1)} img={pr.Value.imagePath}");
                        // プロローグ完了後にオープニングVNへ繋ぐ（プロローグ→VN→ショップ）。
                        ChapterPrologueUI.EnsureExists().Show(art, pr.Value.lines, pr.Value.bgmPaths, showOpening);
                    }
                    else if (showOpening != null) showOpening();
                }
                else if (showOpening != null)
                {
                    showOpening();
                }
            }
        }

        Camera targetCamera = GetBoardCamera();
        EnsureCameraZoomTarget(targetCamera);
        EnsureCameraBoundsRenderer();
        ClampCameraInsideBackground(targetCamera);

        // 起動時ロビー。showLobbyOnBoot（既定OFF）または「ロビーへ戻る」フラグが立っている時に表示します。
        if (willShowBootLobby)
        {
            ForceLobbyOnNextBoot = false;
            LobbyUI.EnsureExists().ShowAsBootLobby();
        }
    }

    // ロビー（章選択）から呼ばれます。開始章を保持してシーンを再読込し、その章で新しいランを始めます。
    public void RequestStartChapter(int chapter)
    {
        PendingStartChapter = Mathf.Max(1, chapter);
        Time.timeScale = 1f;
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : 0);
    }

    // オプション等から「ロビーへ戻る」。専用ロビーシーンへ遷移します。
    // （シーンが未登録の旧構成でも動くよう、見つからなければ従来の再読込フォールバック）。
    public void RequestReturnToLobby()
    {
        PendingStartChapter = 0;
        ForceLobbyOnNextBoot = true;
        ReturnToMainLobbyOnBoot = true; // 戻り先はタイトルでなくメインロビー。
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded("LobbyScene"))
        {
            SceneManager.LoadScene("LobbyScene");
            return;
        }
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : 0);
    }

    // 毎フレーム、ゲーム全体で必要な入力処理を確認します。
    private void Update()
    {
        Camera targetCamera = GetBoardCamera();

        // ⑧ ボス登場シネマティック中はカメラを演出が占有する（ホイールズーム/パン/クランプを止める）。
        if (!bossCinematicActive)
        {
            // 毎フレーム、マウスホイールによる盤面ズームを確認します。
            HandleMouseWheelZoom(targetCamera);

            // 急に倍率を変えず、目標倍率へ滑らかに近づけます。
            ApplySmoothCameraZoom(targetCamera);

            // 毎フレーム、ホイール押し込みドラッグによる盤面カメラ移動を確認します。
            HandleMiddleMousePan(targetCamera);

            // ズームや移動後に、背景の外側が見えない位置へカメラを収めます。
            ClampCameraInsideBackground(targetCamera);
        }
        else
        {
            // 演出中もFOVズームだけは滑らかに反映（ドアップ/復帰用）。位置は演出側で直接制御。
            ApplySmoothCameraZoom(targetCamera);
        }

        // デバッグ用の耐久ダミーウェーブを、通常ウェーブ進行とは別にすぐ開始できます。
        // ホットキーはキーコンフィグで変更可能（既定は debugTrainingWaveHotkey）。
        if (enableDebugTrainingWaveHotkey && Input.GetKeyDown(SettingsStore.GetBind("debug", debugTrainingWaveHotkey)))
            StartDebugTrainingWave();

        // 耐久ユニット同士で戦闘が長引いた時、1分で必ず決着を付けます。
        HandleRoundTimeout();

        // 射程外/到達不能で攻撃が発生しない膠着は、1分を待たず数秒で決着を付けます。
        HandleStalemateResolution();

        // R3-chest-room: 制限時間（30秒）で残りの宝箱を片付けてラウンドを終える。
        HandleChestRoomTimer();

        // ② 編成中はフォーメーション成立マスをライブ表示。
        UpdateFormationPreview();

        // R2-rewards: 強化マス設置モード中はクリックでマスを確定。
        HandleBuffTilePlacementClick();

        // ④ 強化マス設置モード中、ホバー中のマスに tile_box を拡縮アニメで表示。
        HandleBuffTilePlacementHover();

        // R3: 編成フェーズ中、次の戦闘ラウンドの敵を事前配置（入場演出つき）して見えるようにする。
        PreviewWaveEnemiesIfNeeded();
    }

    // 編成中に、現在の戦闘ラウンドの敵を1回だけ事前生成して盤面に見せる（入場演出つき）。
    // フロー: (進路選択枠なら)進路選択 → ボス登場演出 → セリフ → 準備 → FIGHTで戦闘開始。
    private void PreviewWaveEnemiesIfNeeded()
    {
        if (IsCoreMode) return; // コア戦は専用フロー（対象外）
        // 戦闘中/勝利演出/遷移(章ボスならWarning含む)/各種選択中/導入処理中は起動しない。
        if (IsRoundInProgress || waveClearCelebrating || waveTransitioning || bossIntroInProgress
            || bossRewardSelectionPending || augmentSelectionPending || gameOver) return;
        if (enemiesPreviewedThisWave) return;
        InitializeWaveDefinitions();
        if (waveDefinitions == null || currentWaveIndex < 0 || currentWaveIndex >= waveDefinitions.Count) return;
        WaveDefinition d = waveDefinitions[currentWaveIndex];
        if (d == null || d.IsEventRound) return; // イベント行は敵なし

        // 進路選択枠：まず進路を選ばせる（ボスが確定する）。オートプレイはFIGHT側(DebugFight)で解決。
        // 選択完了で IsNodeChoice=false になり、次フレームで下のボス登場演出へ進む（preview は再入する）。
        if (d.IsNodeChoice)
        {
            if (AutoPlayBypassStoryBlock) return; // オートプレイは preview せず DebugFight に任せる。
            bossIntroInProgress = true;
            ShowNodeChoice(d, reenterFightAfter: false);
            return; // enemiesPreviewedThisWave はまだ立てない（選択後に演出へ）。
        }

        enemiesPreviewedThisWave = true;
        if (team2Entities.Count > 0) return; // 既に敵がいる場合は再生成しない
        ClearEnemyUnits();

        // R3-chest-room: アイテム報酬の回は、敵/シネマティックの代わりに宝箱を配置する。
        if (d.IsChestRoom)
        {
            StartChestRoom(d);
            return;
        }

        // ⑧ ボス/中ボス戦は、プレビューの代わりに登場シネマティック
        //    （登場→ドアップ→セリフ→攻撃→取り巻き登場）を再生する。オートプレイ中はスキップ。
        bool isBossKind = d.IsBossWave || d.IsMidBossWave;
        if (isBossKind && !AutoPlayBypassStoryBlock && !bossCinematicPlayedThisWave)
        {
            bossCinematicPlayedThisWave = true;
            StartCoroutine(BossEntranceCinematic(d));
            return;
        }

        SpawnWaveEnemies(d);
    }

    // ⑧ ボス登場シネマティックの状態。
    private bool bossCinematicActive;            // 演出中はカメラを占有（Updateのズーム/パン/クランプを止める）。
    private bool bossCinematicPlayedThisWave;    // このウェーブで既に登場演出を再生したか。
    private bool suppressNextEnemyEntrance;      // 次に生成する敵の通常入場を1回だけ抑止（ボス専用入場のため）。
    private bool waveTransitioning;              // 勝利演出〜次ウェーブ復元（章ボスならWarning表示含む）の遷移中。
    private bool bossIntroInProgress;            // 進路選択や登場演出の導入処理中（preview の二重起動防止）。

    // ⑧ 中ボス/章ボスの登場演出：ボス単体で登場（上ドスン/横飛び＝ボスごと固定）→カメラドアップ→
    //    セリフ→攻撃モーション→カメラ復帰→取り巻きが1マス後ろから歩いて登場、までを一本で再生する。
    //    完了後は bossDialogueShownThisWave=true なので、FIGHTを押すとセリフ無しでそのまま戦闘が始まる。
    private System.Collections.IEnumerator BossEntranceCinematic(WaveDefinition wave)
    {
        bossCinematicActive = true;
        Camera cam = GetBoardCamera();
        Vector3 camHome = cam != null ? cam.transform.position : Vector3.zero;
        float fovHome = targetCameraFieldOfView;
        float orthoHome = targetOrthographicSize;

        string bossId = GetWavePrimaryBossId(wave);

        // ボス placement を特定（解決名が bossId に一致する最初の placement。無ければ先頭）。
        int bossIdx = -1;
        for (int i = 0; i < wave.Enemies.Count; i++)
        {
            if (TryGetWaveEnemyData(wave.Enemies[i], out EntitiesDatabaseSO.EntityData ed)
                && !string.IsNullOrEmpty(bossId)
                && string.Equals(ed.name, bossId, StringComparison.OrdinalIgnoreCase))
            { bossIdx = i; break; }
        }
        if (bossIdx < 0 && wave.Enemies.Count > 0) bossIdx = 0;

        // 1) ボスを先に1体だけ生成（通常入場は抑止）。
        BaseEntity boss = null;
        if (bossIdx >= 0)
        {
            int before = team2Entities.Count;
            suppressNextEnemyEntrance = true;
            SpawnWaveEnemy(wave.Enemies[bossIdx]);
            if (team2Entities.Count > before) boss = team2Entities[team2Entities.Count - 1];
        }
        // 生成直後は着地位置に見えてしまう（登場前の一瞬チラ見え）ため、演出開始まで完全に隠す。
        if (boss != null) boss.SetVisualHidden(true);

        // 2) ボスの見た目・性質に合った専用登場（ボスIDで固定割当）。
        if (boss != null)
            yield return StartCoroutine(PlayBossSpecificEntrance(boss, bossId, cam, camHome));

        // 3) カメラをボスにドアップ。
        if (cam != null && boss != null)
        {
            Vector3 focus = new Vector3(boss.transform.position.x, boss.transform.position.y + 0.3f, camHome.z);
            targetCameraFieldOfView = Mathf.Max(minCameraFieldOfView, fovHome * 0.55f);
            targetOrthographicSize = Mathf.Max(minOrthographicSize, orthoHome * 0.55f);
            float t = 0f; Vector3 from = cam.transform.position;
            while (t < 0.4f) { t += Time.deltaTime; cam.transform.position = Vector3.Lerp(from, focus, Mathf.Clamp01(t / 0.4f)); yield return null; }
            cam.transform.position = focus;
        }

        // 4) 登場直後にボスの攻撃モーションを1回再生 → 終わってdefaultへ戻す → 0.5秒空けてからセリフへ。
        if (boss != null)
        {
            boss.PlayAttackMotionOnce();
            yield return new WaitForSeconds(boss.GetAttackMotionDuration()); // 攻撃モーションが終わってdefaultに戻るまで。
        }
        yield return new WaitForSeconds(0.5f); // default復帰後の余韻（一拍）。

        // 5) セリフ（中ボスは章/個体別の名前・台本・色を反映）。閉じるまで待つ。
        bool dialogueDone = false;
        string midNameOverride = null; string[] midScriptOverride = null; Color? midTint = null;
        if (wave.IsMidBossWave)
        {
            var mv = ChapterStory.GetMidVariantForUnit(currentChapter, bossId, MidBossOccurrenceOf(bossId));
            if (mv.HasValue) { midNameOverride = mv.Value.name; midScriptOverride = mv.Value.lines; midTint = mv.Value.tint; }
        }
        HeroBossDialogueUI.EnsureExists().Show(heroUnitId, bossId, GetEntityIconById(bossId),
            () => { dialogueDone = true; }, wave.IsMidBossWave, midNameOverride, midScriptOverride, midTint);
        while (!dialogueDone) yield return null;
        bossDialogueShownThisWave = true; // FIGHTではセリフを出さずそのまま戦闘へ。

        // 6) カメラを元に戻してから占有解除。
        if (cam != null)
        {
            targetCameraFieldOfView = fovHome;
            targetOrthographicSize = orthoHome;
            float t = 0f; Vector3 from = cam.transform.position;
            while (t < 0.4f) { t += Time.deltaTime; cam.transform.position = Vector3.Lerp(from, camHome, Mathf.Clamp01(t / 0.4f)); yield return null; }
            cam.transform.position = camHome;
        }
        bossCinematicActive = false;

        // 7) 取り巻きが1マス後ろから歩いて登場。
        for (int i = 0; i < wave.Enemies.Count; i++)
        {
            if (i == bossIdx) continue;
            SpawnWaveEnemy(wave.Enemies[i]);
            yield return new WaitForSeconds(0.12f);
        }
    }

    // ⑧ ボスの見た目・性質に合わせた登場スタイル。
    private enum BossEntranceStyle
    {
        SlamDown,        // 重量級：真上から急降下スラム＋画面揺れ。
        Pounce,          // 獣・敏捷：沈み込み(タメ)→側方から弧を描いて跳び込み。
        Swoop,           // 翼あり：上空斜めから滑空して降りる。
        RiseFromGround,  // 亡霊・深淵：地面から薄く立ち上がる（フェードイン）。
        DashIn,          // 忍・砂漠・遠隔：遠くから残像ダッシュ＋わずかにオーバーシュート。
        Erupt,           // 火炎・噴出：地から小さく噴き上がってから着地。
    }

    // ボスIDのキーワードから登場スタイルを決定的に割当（同じボスは常に同じ＝ボスごと固定）。
    private BossEntranceStyle GetBossEntranceStyle(string bossId)
    {
        string id = (bossId ?? string.Empty).ToLowerInvariant();
        bool Has(params string[] keys) { for (int i = 0; i < keys.Length; i++) if (id.Contains(keys[i])) return true; return false; }

        if (Has("rok", "golem", "rook", "caliber", "vaath", "juggernaut", "stone", "ragnora", "ironcliffe")) return BossEntranceStyle.SlamDown;
        if (Has("abyss", "gnasher", "wraith", "lilithe", "cassyva", "maehv", "gloom", "shadow", "sister", "crawler")) return BossEntranceStyle.RiseFromGround;
        if (Has("silverbeak", "wing", "starhorn", "bird", "aer", "nip", "mechaz0r")) return BossEntranceStyle.Swoop;
        if (Has("vetruvian", "zirix", "sajj", "scion", "pax", "rae", "scarab", "sand", "reva", "shidai")) return BossEntranceStyle.DashIn;
        if (Has("magmar", "fire", "ember", "inferno", "pyro", "wyrm", "silithar")) return BossEntranceStyle.Erupt;
        if (Has("beast", "zukong", "rawr", "panther", "wolf", "makantor", "kara")) return BossEntranceStyle.Pounce;
        return BossEntranceStyle.SlamDown;
    }

    // ボス1体の登場演出本体。スタイルごとに開始位置・軌道・着地・フェード・画面揺れを変える。
    private System.Collections.IEnumerator PlayBossSpecificEntrance(BaseEntity boss, string bossId, Camera cam, Vector3 camHome)
    {
        Transform bt = boss.transform;
        Vector3 landing = bt.position;
        Vector3 baseScale = bt.localScale;
        bt.DOKill();
        SpriteRenderer sr = boss.spriteRender;
        void SetAlpha(float a) { if (sr != null) { Color c = sr.color; c.a = a; sr.color = c; } }

        // 開始位置に着いてから表示する（着地位置でのチラ見え防止）。出現と同時にスプライト発光。
        void Reveal(bool fadeIn)
        {
            boss.SetVisualHidden(false);
            SetAlpha(fadeIn ? 0f : 1f);
            AttackEffectPlayer.PlayArcanaSpriteGlow(boss, 0.5f, 0.85f);
        }
        // ボスの見た目の大きさ（スプライト高さ）。エフェクトのサイズをこれに比例させ、巨大ボスほど大きく出す。
        float fxH = (sr != null && sr.sprite != null) ? Mathf.Clamp(sr.bounds.size.y, 0.8f, 9f) : Mathf.Clamp(baseScale.y * 1.6f, 0.8f, 9f);

        // スタイルに合った着地/出現エフェクト。Assets/Resources/fx の実スプライトシート(Multiple)を再生する。
        // 引数 mul はボスサイズ(fxH)への倍率。エフェクトの最終サイズ＝fxH×mul。
        void Impact() { AttackEffectPlayer.PlayUnitAttackImpact(boss, landing); }
        // 土煙：煙2スプライトを茶系にtintして広げる（スラム/跳び込み用）。
        void Dust(float mul)
        {
            Impact();
            AttackEffectPlayer.PlayAreaIndicator(boss, landing, fxH * 0.5f, 0.45f, 1.0f);
            AttackEffectPlayer.PlaySheetEffectAt("fx/fx_smoke2", landing, fxH * mul, new Color(0.80f, 0.71f, 0.55f, 1f));
        }
        // 毒沼：地面プールのスプライトを緑にtintして、地中出現に合わせ展開（毒の沼）。
        void Poison(float mul)
        {
            AttackEffectPlayer.PlayAreaIndicator(boss, landing, fxH * 0.55f, 0.6f, 1.0f);
            AttackEffectPlayer.PlaySheetEffectAt("fx/fx_bloodground", landing, fxH * mul, new Color(0.46f, 0.95f, 0.40f, 1f));
        }
        // 風/つむじ風：竜巻スプライトを白〜水色で（滑空用）。
        void Wind(float mul)
        {
            AttackEffectPlayer.PlaySheetEffectAt("fx/fx_tornadoswirl", landing, fxH * mul, new Color(0.85f, 0.95f, 1f, 1f));
        }
        // マグマ：既存の炎VFX＋炎インパクトのスプライト（炎系用）。
        void Magma(float mul)
        {
            Impact();
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, landing, fxH * 0.6f);
            AttackEffectPlayer.PlaySheetEffectAt("fx/fx_fireimpact", landing, fxH * mul, new Color(1f, 0.72f, 0.38f, 1f));
        }
        // 残像強調：着地時の小さな白い閃光（本体の残像は移動中に生成）。
        void DashFx(float mul)
        {
            Impact();
            AttackEffectPlayer.PlaySheetEffectAt("fx/fx_whiteexplosion", landing, fxH * mul, new Color(0.82f, 0.9f, 1f, 1f));
        }

        switch (GetBossEntranceStyle(bossId))
        {
            case BossEntranceStyle.SlamDown:
            {
                Vector3 start = landing + new Vector3(0f, 7.5f, 0f);
                bt.position = start; Reveal(false);
                float dur = 0.42f, t = 0f;
                while (t < dur) { t += Time.deltaTime; float n = Mathf.Clamp01(t / dur); bt.position = Vector3.Lerp(start, landing, n * n); yield return null; }
                bt.position = landing;
                Dust(0.6f); // 上からドスン＝土煙（ボスサイズ連動）。
                yield return StartCoroutine(SquashLand(bt, baseScale, 1.28f, 0.68f));
                yield return StartCoroutine(ShakeCam(cam, camHome, 0.35f, 0.28f));
                break;
            }
            case BossEntranceStyle.Pounce:
            {
                Vector3 start = landing + new Vector3(5.2f, 3.6f, 0f);
                bt.position = start; Reveal(false);
                Vector3 crouch = new Vector3(baseScale.x * 1.1f, baseScale.y * 0.8f, baseScale.z);
                float c0 = 0f; while (c0 < 0.14f) { c0 += Time.deltaTime; bt.localScale = Vector3.Lerp(baseScale, crouch, c0 / 0.14f); yield return null; }
                bt.localScale = baseScale;
                float dur = 0.4f, t = 0f;
                while (t < dur) { t += Time.deltaTime; float n = Mathf.Clamp01(t / dur); float e = Mathf.Sin(n * Mathf.PI * 0.5f); Vector3 p = Vector3.Lerp(start, landing, e); p.y += Mathf.Sin(n * Mathf.PI) * 0.8f; bt.position = p; yield return null; }
                bt.position = landing;
                Dust(0.5f); // 跳び込み着地＝土煙（ボスサイズ連動）。
                yield return StartCoroutine(SquashLand(bt, baseScale, 1.2f, 0.82f));
                break;
            }
            case BossEntranceStyle.Swoop:
            {
                Vector3 start = landing + new Vector3(6.5f, 5.2f, 0f);
                bt.position = start; Reveal(false);
                float dur = 0.6f, t = 0f;
                while (t < dur) { t += Time.deltaTime; float n = Mathf.Clamp01(t / dur); float e = 1f - (1f - n) * (1f - n); Vector3 p = Vector3.Lerp(start, landing, e); p.y += Mathf.Sin(n * Mathf.PI) * 0.6f; bt.position = p; yield return null; }
                bt.position = landing;
                Wind(0.6f); // 滑空＝風/つむじ風（ボスサイズ連動）。
                break;
            }
            case BossEntranceStyle.RiseFromGround:
            {
                Vector3 start = landing + new Vector3(0f, -2.6f, 0f);
                bt.position = start; Reveal(true);
                Poison(0.55f); // 地中から出現＝毒沼のぶくぶく（ボスサイズ連動）。
                float dur = 0.6f, t = 0f;
                while (t < dur) { t += Time.deltaTime; float n = Mathf.Clamp01(t / dur); float e = 1f - (1f - n) * (1f - n); bt.position = Vector3.Lerp(start, landing, e); SetAlpha(n); yield return null; }
                bt.position = landing; SetAlpha(1f);
                break;
            }
            case BossEntranceStyle.DashIn:
            {
                Vector3 start = landing + new Vector3(9.5f, 0f, 0f);
                bt.position = start; Reveal(false);
                float dur = 0.3f, t = 0f; float ghostTimer = 0f;
                while (t < dur) { t += Time.deltaTime; float n = Mathf.Clamp01(t / dur); float e = 1f - (1f - n) * (1f - n) * (1f - n); bt.position = Vector3.Lerp(start, landing, e); float stretch = Mathf.Lerp(1.35f, 1f, n); bt.localScale = new Vector3(baseScale.x * stretch, baseScale.y / Mathf.Max(0.6f, stretch), baseScale.z);
                    // 残像を目立たせる：移動中、本体のスプライトを薄い青白いゴーストとして連続生成。
                    ghostTimer += Time.deltaTime; if (ghostTimer >= 0.03f) { ghostTimer = 0f; SpawnAfterimageGhost(sr, 0.3f); }
                    yield return null; }
                bt.position = landing; bt.localScale = baseScale;
                DashFx(0.45f); // 残像の締め＋速度残光（ボスサイズ連動）。
                Vector3 over = landing + new Vector3(-0.4f, 0f, 0f);
                float o = 0f; while (o < 0.12f) { o += Time.deltaTime; bt.position = Vector3.Lerp(over, landing, o / 0.12f); yield return null; }
                bt.position = landing;
                break;
            }
            case BossEntranceStyle.Erupt:
            {
                Vector3 start = landing + new Vector3(0f, -1.4f, 0f);
                bt.position = start; Reveal(true); bt.localScale = baseScale * 0.6f;
                Magma(0.6f); // 噴き上がり＝マグマ（炎VFX＋火の粉、ボスサイズ連動）。
                float dur = 0.34f, t = 0f;
                while (t < dur) { t += Time.deltaTime; float n = Mathf.Clamp01(t / dur); bt.position = Vector3.Lerp(start, landing + new Vector3(0f, 0.6f, 0f), n); SetAlpha(Mathf.Clamp01(n * 1.5f)); bt.localScale = Vector3.Lerp(baseScale * 0.6f, baseScale * 1.12f, n); yield return null; }
                float d2 = 0.16f, t2 = 0f; Vector3 p0 = bt.position;
                while (t2 < d2) { t2 += Time.deltaTime; bt.position = Vector3.Lerp(p0, landing, t2 / d2); bt.localScale = Vector3.Lerp(baseScale * 1.12f, baseScale, t2 / d2); yield return null; }
                bt.position = landing; bt.localScale = baseScale; SetAlpha(1f);
                Magma(0.55f); // 着地でもう一度マグマを弾けさせる（ボスサイズ連動）。
                yield return StartCoroutine(ShakeCam(cam, camHome, 0.2f, 0.2f));
                break;
            }
        }

        boss.SetVisualHidden(false);
        bt.localScale = baseScale;
        SetAlpha(1f);
    }

    // 着地のつぶれ→元に戻すスケール演出。
    private System.Collections.IEnumerator SquashLand(Transform bt, Vector3 baseScale, float sx, float sy)
    {
        Vector3 squash = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);
        bt.localScale = squash;
        float s = 0f;
        while (s < 0.16f) { s += Time.deltaTime; bt.localScale = Vector3.Lerp(squash, baseScale, s / 0.16f); yield return null; }
        bt.localScale = baseScale;
    }

    // 着地衝撃などの画面揺れ（home を中心に減衰しながら揺らし、最後に home へ戻す）。演出中はクランプ停止中なので安全。
    private System.Collections.IEnumerator ShakeCam(Camera cam, Vector3 home, float amp, float dur)
    {
        if (cam == null) yield break;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / dur);
            float ox = (Mathf.PerlinNoise(t * 40f, 0f) - 0.5f) * 2f * amp * k;
            float oy = (Mathf.PerlinNoise(0f, t * 40f) - 0.5f) * 2f * amp * k;
            cam.transform.position = home + new Vector3(ox, oy, 0f);
            yield return null;
        }
        cam.transform.position = home;
    }

    // 残像ゴースト：本体スプライトを薄い青白いコピーとして残し、フェードして消える。
    private void SpawnAfterimageGhost(SpriteRenderer src, float life)
    {
        if (src == null || src.sprite == null) return;
        GameObject go = new GameObject("BossAfterimage");
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        go.transform.localScale = src.transform.lossyScale;
        SpriteRenderer gr = go.AddComponent<SpriteRenderer>();
        gr.sprite = src.sprite; gr.flipX = src.flipX;
        gr.sortingLayerID = src.sortingLayerID; gr.sortingOrder = src.sortingOrder - 1;
        gr.color = new Color(0.6f, 0.8f, 1f, 0.5f);
        gr.DOFade(0f, life).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() => { if (go != null) Destroy(go); });
    }

    // 戦闘時間に応じた与ダメージ倍率です。序盤は等倍、後半ほど一気に決着が付きやすくします。
    private float GetRoundDamageMultiplier()
    {
        if (!IsRoundInProgress)
            return 1f;

        // R3-chest-room: チェスト部屋は時間経過で倍率を上げない（常に等倍）。
        if (chestRoomActive)
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

        // FIGHTの見た目（Duelystの金ターンボタン＋白ラベル）をここでも適用し、再設定時にも維持する。
        UIShop.StyleFightButton(fightObject, true);

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
        // R2-coremode: コア戦は時間切れ敗北を行わない（決着はコア破壊のみ）。
        if (IsCoreMode)
            return;

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
            BeginWaveClearCelebration();
            return;
        }

        TriggerGameOver();
    }

    // 両軍が残っているのに一定時間まったくHPが動かない膠着（射程外・到達不能で攻撃が発生しない）を検出し、
    // 60秒ハードタイムアウトを待たずに勝敗を確定する。これが「ウェーブクリアが数十秒遅れる」現象の対策。
    private void HandleStalemateResolution()
    {
        if (IsCoreMode)
            return;

        // 戦闘外ならフラグを倒すだけ（次の戦闘開始で初期化される）。
        if (!IsRoundInProgress)
        {
            stalematePrevInRound = false;
            return;
        }

        // 戦闘開始フレーム：基準HP合計と基準時刻を初期化（接近フェーズの誤判定を避けるため計測開始）。
        if (!stalematePrevInRound)
        {
            stalematePrevInRound = true;
            lastBattleProgressTime = Time.time;
            lastBattleHpSum = ComputeBattleHpSum();
            return;
        }

        if (roundTimeoutResolved)
            return;

        float sum = ComputeBattleHpSum();
        if (!Mathf.Approximately(sum, lastBattleHpSum))
        {
            // HPが動いた＝戦闘が進行している。基準を更新して膠着タイマーをリセット。
            lastBattleHpSum = sum;
            lastBattleProgressTime = Time.time;
            return;
        }

        // 接近猶予を過ぎ、かつ一定時間HPが無変化なら膠着とみなし即解決。
        if (RoundElapsedTime >= StalemateMinElapsed &&
            (Time.time - lastBattleProgressTime) >= StalemateResolveSeconds)
        {
            ResolveRoundByStalemate();
        }
    }

    // 両軍・盤上・生存ユニットの現在HP合計（コアは戦闘の決着に無関係なので除外）。
    private float ComputeBattleHpSum()
    {
        float sum = 0f;
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e != null && e.IsOnBoard && !e.IsDead && !e.IsCore)
                sum += Mathf.Max(0, e.CurrentHealth);
        }
        for (int i = 0; i < team2Entities.Count; i++)
        {
            BaseEntity e = team2Entities[i];
            if (e != null && e.IsOnBoard && !e.IsDead && !e.IsCore)
                sum += Mathf.Max(0, e.CurrentHealth);
        }
        return sum;
    }

    // 膠着時の決着：HandleRoundTimeout と同じ判定（残HP割合で勝敗）だが、60秒を待たずに確定する。
    private void ResolveRoundByStalemate()
    {
        roundTimeoutResolved = true; // タイムアウトと共通の二重解決ガード。
        float playerHealthRatio = GetTeamRemainingHealthRatio(team1Entities, true);
        float enemyHealthRatio = GetTeamRemainingHealthRatio(team2Entities);
        Debug.Log($"[Stalemate] {StalemateResolveSeconds:0}s 無進展で決着。Player HP:{playerHealthRatio:0.00} Enemy HP:{enemyHealthRatio:0.00}");

        if (!HasLivingPlayerBattleUnit())
        {
            TriggerGameOver();
            return;
        }

        if (!HasLivingEnemyBattleUnit() || playerHealthRatio >= enemyHealthRatio)
        {
            BeginWaveClearCelebration();
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
            if (entity != null && entity.Team == Team.Team1 && entity.IsOnBoard && !entity.IsDead && !entity.IsSummonedUnit && !entity.IsCore)
                return true;
        }

        return false;
    }

    // 敵側は一時召喚も戦場にいる敵として扱い、全て倒れたらウェーブクリアにします。
    // R2-coremode: コアは常設拠点なので「全滅判定」から除外（コアが残っていてもウェーブはクリアになる）。
    private bool HasLivingEnemyBattleUnit()
    {
        for (int i = 0; i < team2Entities.Count; i++)
        {
            BaseEntity entity = team2Entities[i];
            if (entity != null && entity.Team == Team.Team2 && entity.IsOnBoard && !entity.IsDead && !entity.IsCore)
                return true;
        }

        return false;
    }

    // ショップでこのユニットを買えるか確認します。
    // オートプレイ実行中は導入演出ブロックを完全バイパス（演出が高速ランで残留し購入が止まる事故を防ぐ）。
    public static bool AutoPlayBypassStoryBlock = false;

    // 章導入（プロローグ全画面演出 / オープニングVN）の表示中か。
    // この間は背後のショップ操作・盤面ドラッグを一切受け付けない（レイキャスト遮断の保険）。
    public static bool IsStoryIntroBlocking()
    {
        if (AutoPlayBypassStoryBlock) return false; // オートプレイ中はブロックしない。
        if (ChapterPrologueUI.Instance != null && ChapterPrologueUI.Instance.IsShowing) return true;
        if (InterludeUI.Instance != null && InterludeUI.Instance.IsShowing) return true;
        return false;
    }

    public bool CanBuyEntity(EntitiesDatabaseSO.EntityData entityData)
    {
        // 章導入演出中は購入不可（プロローグ画像/VNの背後でショップが反応しないように）。
        if (IsStoryIntroBlocking())
            return false;

        if (IsLegionOnlySummonData(entityData))
            return false;

        // ヒーロー形態変化用の温存ユニットはショップ購入不可。
        if (IsReservedHeroFormUnit(entityData.name))
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

        // ヒーロー形態変化用の温存ユニットはショップ抽選に出さない。
        if (IsReservedHeroFormUnit(entityData.name))
            return false;

        if (HasOwnedStarThreeUnit(entityData.name))
            return false;

        // R4-collection-hub: プレイヤーがコレクションハブで「ショップに出さない」に設定したユニットは抽選から外す（恒久選抜）。
        if (SaveManager.Instance != null && !SaveManager.Instance.IsShopUnitEnabled(entityData.name))
            return false;

        // R2-recruit: コスト帯別のロック。cost1-2は常時。cost3はスターター集合のみ常時、
        // 他のcost3とcost4/5は「中ボスで章内解放」または「章ボスで恒久解放」されるまで非表示。
        if (entityData.cost <= 2)
            return true;

        if (entityData.cost == 3 && Cost3StarterPlayable.Contains(entityData.name))
            return true;

        return chapterUnlockedUnitIds.Contains(entityData.name) || IsPermanentlyUnlocked(entityData.name);
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

        // R1-collection: 所持ボス（章ボス報酬）は育成レベルに応じた永続ステータス倍率を適用。
        if (SaveManager.Instance != null && SaveManager.Instance.HasBossAlly(entityData.name))
            newEntity.ApplyBossAffinityMultiplier(SaveManager.Instance.GetBossAffinityStatMultiplier(entityData.name));

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

    // 主人公（ヒーロー）ユニットを確定付与する。
    // 選択中ヒーロー（SaveManager.GetHeroUnitId）を優先し、空/不明IDは既定ヒーロー(HeroAldin)へ、
    // それも DB に無ければ（アセット未ビルド時など）通常のランダム付与へフォールバックする。
    private void GrantHeroUnit()
    {
        if (entitiesDatabase == null || entitiesDatabase.allEntities == null) { GrantStartingUnit(); return; }

        // R3-hero-select: スロットの選択ヒーローを採用。未選択/不正は既定ヒーローへ。
        string selected = SaveManager.Instance != null ? SaveManager.Instance.GetHeroUnitId() : null;
        // 基本3人＋解放済み章ボスを採用可。未解放/不正は既定ヒーローへ。
        if (string.IsNullOrEmpty(selected) || !IsHeroCandidateUnlocked(selected))
            selected = "HeroAldin";
        // STORY-skin: カガチを選び、犬化スキンが解放済み＆ON なら skindogehai の姿（別ユニット）で出撃。
        // 章8は犬化イベント中なので、解放/トグルに関係なくカガチは犬化(Skindogehai)固定＋弱体で戦う。
        bool isKagachi = string.Equals(selected, "HeroKagachi", StringComparison.OrdinalIgnoreCase);
        bool ch8ForcedDog = !IsCoreMode && currentChapter == 8 && isKagachi;
        if ((isKagachi && IsKagachiDogSkinActive()) || ch8ForcedDog)
            selected = "Skindogehai";
        heroUnitId = selected; // 必殺(UseHeroUltimate)・ボタン名もこのIDで分岐する。ボスは default 必殺へ。

        EntitiesDatabaseSO.EntityData data = entitiesDatabase.allEntities.FirstOrDefault(d =>
            d.prefab != null && string.Equals(d.name, heroUnitId, StringComparison.OrdinalIgnoreCase));
        if (data.prefab == null) { GrantStartingUnit(); return; } // ヒーロー未登録(未ビルド)時の保険。
        BaseEntity heroEntity = CreateBenchEntity(data, 1);
        if (heroEntity != null)
        {
            // 章8の犬化カガチはステ/スキルを弱める（剥き身の生命）。攻撃力・最大HP・スキル威力を低下。
            if (ch8ForcedDog) { heroEntity.formPenalty = 0.55f; heroEntity.RefreshDerivedStats(true); }
            OnRosterChanged?.Invoke();
        }
    }

    // R3-hero-select: 編成中に主人公（ヒーロー）を変更する。既存のヒーロー実体を盤面・ベンチから取り除き、
    // 選択をスロットへ保存し、新しいヒーローを付与する。戦闘中は不可。成功で true。
    public bool ChangeHeroUnit(string newHeroId)
    {
        if (!IsHeroCandidateUnlocked(newHeroId))
            return false;
        if (IsRoundInProgress)
            return false;

        // 既存ヒーロー（基本3＋現アクティブ）を収集してから除去。装備アイテムはベンチへ返す。
        List<BaseEntity> existingHeroes = new List<BaseEntity>();
        for (int i = 0; i < team1Entities.Count; i++)
            if (team1Entities[i] != null && IsHeroUnit(team1Entities[i].UnitId))
                existingHeroes.Add(team1Entities[i]);
        for (int i = 0; i < benchEntities.Count; i++)
            if (benchEntities[i] != null && IsHeroUnit(benchEntities[i].UnitId) && !existingHeroes.Contains(benchEntities[i]))
                existingHeroes.Add(benchEntities[i]);
        for (int i = 0; i < existingHeroes.Count; i++)
            RemoveOwnedEntity(existingHeroes[i], true);

        // 選択を保存し、新ヒーローを付与（GrantHeroUnit が SaveManager から読む）。
        if (SaveManager.Instance != null)
            SaveManager.Instance.SetHeroUnitId(newHeroId);
        heroUnitId = newHeroId;
        GrantHeroUnit();
        OnRosterChanged?.Invoke();
        return true;
    }

    // 現在の主人公（ヒーロー）ID。UI（ヒーロー変更）が現在値の表示に使う。
    public string CurrentHeroUnitId => heroUnitId;

    // STORY-skin: カガチ犬化スキン（skindogehai）の解放/選択状態。
    public static bool IsKagachiDogSkinUnlocked()
        => SaveManager.Instance != null && SaveManager.Instance.GetStoryFlag("skin_kagachi_unlocked");
    public static bool IsKagachiDogSkinActive()
        => IsKagachiDogSkinUnlocked() && SaveManager.Instance.GetStoryFlag("skin_kagachi_on");
    public static void SetKagachiDogSkinEnabled(bool on)
    {
        if (SaveManager.Instance != null && IsKagachiDogSkinUnlocked())
            SaveManager.Instance.SetStoryFlag("skin_kagachi_on", on);
    }

    // === R3-hero-depth: 必殺アップグレード／開始ボーナス＋オーラ／主人公は将 ===

    // 必殺アップグレードの選択結果を受け取る（1=A / 2=B）。
    private void OnHeroUltUpgradeChosen(int choice)
    {
        HeroUltUpgrade = Mathf.Clamp(choice, 1, 2);
        pendingHeroUltUpgrade = false;
    }

    // ヒーロー別のラン開始ボーナス。Aldin=防御アイテム / Kagachi=ゴールド / Vesna=無料リロール。
    private void ApplyHeroStartingBonus()
    {
        switch ((heroUnitId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin":
                ItemData def = PickRandomItemOfCategory(ItemCategory.Defense);
                if (def != null) ReturnItemToBench(def);
                break;
            case "herokagachi":
                if (PlayerData.Instance != null) PlayerData.Instance.AddMoney(4);
                break;
            case "herovesna":
                if (UIShop.Instance != null) UIShop.Instance.GrantFreeRerollStack(1);
                else if (PlayerData.Instance != null) PlayerData.Instance.AddMoney(3); // ショップ未生成時の保険
                break;
        }
    }

    // 盤面に主人公（ヒーロー）が生存して配置されているか。
    private bool IsHeroOnBoard()
    {
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore && IsHeroUnit(e.UnitId))
                return true;
        }
        return false;
    }

    // ヒーロー・オーラ：盤面に主人公がいる時、戦闘開始時に味方全体へ小バフ（守護/攻撃/秘力）。
    private void ApplyHeroAura()
    {
        if (!IsHeroOnBoard()) return;
        const float dur = 600f; // 1ウェーブ分（戦闘終了でリセットされる）
        string hero = (heroUnitId ?? string.Empty).ToLowerInvariant();
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore) continue;
            switch (hero)
            {
                case "heroaldin": e.ApplyTimedSynergyDamageReductionBonus(0.08f, dur); break; // 被ダメ-8%
                case "herokagachi": e.ApplyTimedSynergyDamageDealtBonus(0.08f, dur); break;   // 与ダメ+8%
                case "herovesna": // 秘力オーラ：マナ獲得促進＋スキル威力強化（魔法寄り）。
                    e.ApplyTimedSynergyManaGainMultiplier(1.18f, dur);
                    e.ApplyTimedSynergyPowerBonus(0.12f, dur);
                    break;
            }
        }
    }

    // ====== R3-hero-scale: ヒーロー育成（ラン内Lv＋章進行メタ） ======

    // 盤面に出ているヒーロー実体を返す（無ければ null）。
    private BaseEntity GetHeroEntityOnBoard()
    {
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore && IsHeroUnit(e.UnitId))
                return e;
        }
        return null;
    }

    // 章進行メタ：クリア済みチャプター数に応じてヒーローの基礎を底上げ（自動上昇ぶん）。
    // 例: 5章クリア → +20%。手動育成ポイント（Phase2）は別途ここに合算予定。
    private float HeroMetaBaseMultiplier()
    {
        int cleared = 0;
        float manualMul = 1f;
        if (SaveManager.Instance != null)
        {
            for (int c = 1; c <= 13; c++)
                if (SaveManager.Instance.IsChapterUnlocked(c + 1)) cleared++;
            // R3-hero-mastery: 熟練度Lvぶんの基礎ステ倍率を合算。
            manualMul = SaveManager.Instance.GetHeroMasteryStatMultiplier(heroUnitId);
        }
        return (1f + cleared * 0.04f) * manualMul;
    }

    // 盤面の充実度ボーナス：出している味方ユニット数（ヒーロー/コア除く）に応じた小バフ。
    private float HeroBoardBonus()
    {
        int units = 0;
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore && !IsHeroUnitId(e.UnitId)) units++;
        }
        return units * 0.02f; // 1体につき+2%
    }

    // ヒーローのステータス総合倍率（HP/攻撃）。メタ底上げ × ラン内Lv × 盤面ボーナス。
    private float HeroTotalStatMultiplier()
    {
        float lvl = 1f + (heroRunLevel - 1) * 0.05f; // Lvごと+5%
        return HeroMetaBaseMultiplier() * lvl * (1f + HeroBoardBonus());
    }

    // 必殺の威力倍率（ラン内Lv連動。説明文と実値の両方で使う）。
    public float HeroUltPower => 1f + (heroRunLevel - 1) * 0.06f;

    // 必殺クールタイム（ラウンド数）。効果が強力なほど長い＝「ここぞ」で使う設計。
    //   攻撃的AoE(burst/蒼炎)・吸収=長(4) / 防御・妨害(shield/heal/weaken/frost)=中(3) / 単純バフ(rally/speed)=短(2)。
    public int GetHeroUltCooldown()
    {
        string id = (heroUnitId ?? string.Empty).ToLowerInvariant();
        switch (id)
        {
            case "heroaldin": return 3;    // 聖盾の号令：全体シールド＋被ダメ減（防御寄り）。
            case "herokagachi": return 3;  // 修羅の号令：全体与ダメ＋攻速（攻め）。
            case "herovesna": return 4;    // 蒼炎の号令：敵全体AoE＋味方攻速（強力＝長め）。
        }
        if (HeroUltConfig.TryGetValue(heroUnitId ?? string.Empty, out string[] cfg) && cfg.Length > 0)
        {
            switch (cfg[0])
            {
                case "burst": return 4;
                case "drain": return 4;
                case "frost": return 3;
                case "weaken": return 3;
                case "shield": return 3;
                case "heal": return 3;
                case "rally": return 2;
                case "speed": return 2;
            }
        }
        return 3;
    }

    // 必殺クールタイムの残りラウンド数（HUDボタン表示用）。0で使用可。
    public int HeroUltCooldownRemaining => Mathf.Max(0, heroUltCooldownRemaining);

    // Lv節目の自動スターアップ（ショップ購入以外の経路）。Lv5→★2 / Lv10→★3。
    private int HeroStarForLevel(int level)
    {
        if (level >= 10) return 3;
        if (level >= 5) return 2;
        return 1;
    }

    // ラン内XPを加算し、Lvが上がったらポップアップで通知する（戦闘外でも呼ばれ得る）。
    public void AddHeroRunXp(int amount)
    {
        if (amount <= 0) return;
        heroRunXp += amount;
        int newLevel = Mathf.Clamp(1 + heroRunXp / HeroXpPerLevel, 1, HeroMaxLevel);
        if (newLevel != heroRunLevel)
        {
            heroRunLevel = newLevel;
            bool ja = LocalizationManager.IsJapanese;
            ScorePopupUI.EnsureExists().Show(1, (ja ? "主人公 Lv " : "Hero Lv ") + heroRunLevel, new Color(1f, 0.85f, 0.4f));
            ApplyHeroScaling(); // 盤面に出ていれば即反映。
        }
    }

    // ラン開始時にヒーロー育成状態を初期化する。
    private void ResetHeroRunProgress()
    {
        heroRunXp = 0;
        heroRunLevel = 1;
        heroUltCooldownRemaining = 0; // 新ランは必殺CTゼロから。
    }

    // 盤面のヒーローへ、現在の総合倍率と節目スターを反映する（戦闘開始時・Lvアップ時に呼ぶ）。
    private void ApplyHeroScaling()
    {
        BaseEntity hero = GetHeroEntityOnBoard();
        if (hero == null) return;
        int targetStar = HeroStarForLevel(heroRunLevel);
        if (hero.StarLevel != targetStar)
            hero.ApplyStarLevel(targetStar); // ★アップ演出込み。
        hero.heroScaleMultiplier = HeroTotalStatMultiplier();
        hero.RefreshDerivedStats(true);
    }

    // 主人公は将：ヒーローが倒れる時、1戦1回は自動復活（HP40%）。復活後に再び倒れると味方を弱体（1回）。
    // BaseEntity.Die から呼ばれ、true を返すと死亡をキャンセルして復活する。
    public bool TryConsumeHeroReviveForUnit(BaseEntity entity)
    {
        if (entity == null || entity.Team != Team.Team1 || !IsHeroUnit(entity.UnitId)) return false;

        if (!heroReviveUsedThisWave)
        {
            heroReviveUsedThisWave = true;
            entity.AugmentReviveAtRatio(0.40f);
            AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, entity.transform.position, 1.1f);
            ScorePopupUI.EnsureExists().Show(1, LocalizationManager.IsJapanese ? "主人公 復活！" : "Hero Revives!", new Color(1f, 0.9f, 0.45f));
            return true;
        }

        // すでに復活済み → ここで本当に倒れる。味方を弱体（1戦1回）。
        if (!heroFallenWeakenApplied)
        {
            heroFallenWeakenApplied = true;
            ApplyHeroFallenWeaken();
        }
        return false;
    }

    // 主人公が（復活ぶんを使い切って）倒れた時の味方弱体。残りウェーブ中、与ダメ-15%・攻速×0.9。
    private void ApplyHeroFallenWeaken()
    {
        const float dur = 600f;
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore) continue;
            e.ApplyTimedSynergyDamageDealtBonus(-0.15f, dur);
            e.ApplyAttackSpeedBoostFromSynergy(0.9f, dur);
        }
        ScorePopupUI.EnsureExists().Show(1, LocalizationManager.IsJapanese ? "主人公が倒れた…！ 味方弱体" : "Hero has fallen… allies weakened", new Color(1f, 0.5f, 0.45f));
    }

    // ヒーロー必殺が使えるか（戦闘中・未使用・盤面に味方がいる）。HUDボタンが参照。
    public bool IsHeroUltVisible => IsRoundInProgress && !IsCoreMode;
    public bool CanUseHeroUltimate()
    {
        return IsRoundInProgress && !heroUltUsedThisWave && heroUltCooldownRemaining <= 0 && HasLivingPlayerBattleUnit();
    }

    // 現在のランの主人公（ヒーロー）必殺名。HUDボタンのラベルにも使う。JA/EN。
    public string GetHeroUltimateName(bool ja)
    {
        switch ((heroUnitId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin": return ja ? "聖盾の号令" : "Aegis Command";
            case "herokagachi": return ja ? "修羅の号令" : "Carnage Command";
            case "herovesna": return ja ? "蒼炎の号令" : "Azure Flame Command";
            default:
                if (HeroUltConfig.TryGetValue(heroUnitId ?? string.Empty, out string[] cfg)) return ja ? cfg[1] : cfg[2];
                return ja ? "ヒーロー必殺" : "Hero Ultimate";
        }
    }

    // 現在のヒーロー必殺の効果説明（選択中アップグレードを反映）。ボタンのツールチップ等で表示。JA/EN。
    public string GetHeroUltimateDescription(bool ja)
    {
        int up = HeroUltUpgrade;
        float ult = HeroUltPower; // R3-hero-scale: ラン内Lvで威力が伸びる。
        switch ((heroUnitId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin":
            {
                float shield = (up == 1 ? 45f : 30f) * ult; float dur = up == 2 ? 10f : 6f;
                return ja ? $"味方全体に最大HP{shield:0}%シールド＋被ダメ-20%（{dur:0}秒）"
                          : $"All allies: {shield:0}% HP shield + DR -20% ({dur:0}s)";
            }
            case "herokagachi":
            {
                float dmg = (up == 1 ? 55f : 35f) * ult; float spd = up == 2 ? 1.45f : 1.25f;
                return ja ? $"味方全体に与ダメ+{dmg:0}%＋攻撃速度×{spd:0.00}（6秒）"
                          : $"All allies: DMG +{dmg:0}% + ATKSPD x{spd:0.00} (6s)";
            }
            case "herovesna":
            {
                int dmg = Mathf.RoundToInt((80 + currentChapter * 40) * ult); if (up == 1) dmg = Mathf.RoundToInt(dmg * 1.6f);
                string extra = up == 2 ? (ja ? "＋スタン0.6秒" : " + stun 0.6s") : string.Empty;
                return ja ? $"敵全体に蒼炎{dmg}ダメージ{extra}＋味方攻撃速度×1.15（6秒）"
                          : $"All enemies: {dmg} azure damage{extra} + allies ATKSPD x1.15 (6s)";
            }
            default:
                if (HeroUltConfig.TryGetValue(heroUnitId ?? string.Empty, out string[] cfg))
                    return GetConfiguredUltDescription(cfg[0], up, ult, ja);
                return ja ? "味方全体を強化（1戦1回）" : "Empower all allies (once per battle)";
        }
    }

    // 設定駆動ヒーローの必殺説明（種別ごと）。
    private string GetConfiguredUltDescription(string kind, int up, float ult, bool ja)
    {
        switch (kind)
        {
            case "shield":
            {
                int s = Mathf.RoundToInt((up == 1 ? 40f : 28f) * ult);
                return ja ? $"味方全体に最大HP{s}%シールド＋被ダメ-15%（{(up==2?9:6)}秒）" : $"All allies: {s}% HP shield + DR -15% ({(up==2?9:6)}s)";
            }
            case "heal":
            {
                int h = Mathf.RoundToInt((up == 1 ? 45f : 30f) * ult);
                return ja ? $"味方全体を最大HP{h}%回復＋小シールド" : $"Heal all allies {h}% HP + small shield";
            }
            case "rally":
            {
                int d = Mathf.RoundToInt((up == 1 ? 50f : 32f) * ult);
                return ja ? $"味方全体に与ダメ+{d}%＋攻撃速度×{(up==2?1.45f:1.25f):0.00}（6秒）" : $"Allies: DMG +{d}% + ATKSPD x{(up==2?1.45f:1.25f):0.00} (6s)";
            }
            case "speed":
            {
                return ja ? $"味方全体に攻撃速度×{(up==1?1.5f:1.3f):0.00}＋与ダメ+15%（6秒）" : $"Allies: ATKSPD x{(up==1?1.5f:1.3f):0.00} + DMG +15% (6s)";
            }
            case "burst":
            {
                int dmg = Mathf.RoundToInt((75 + currentChapter * 35) * ult); if (up == 1) dmg = Mathf.RoundToInt(dmg * 1.6f);
                return ja ? $"敵全体に{dmg}ダメージ{(up==2?"＋スタン0.5秒":"")}" : $"All enemies: {dmg} damage{(up==2?" + stun 0.5s":"")}";
            }
            case "drain":
            {
                int dmg = Mathf.RoundToInt((55 + currentChapter * 28) * ult); if (up == 1) dmg = Mathf.RoundToInt(dmg * 1.5f);
                return ja ? $"敵全体に{dmg}ダメージ＋与ダメの一部を味方が回復" : $"All enemies: {dmg} dmg + allies drain heal";
            }
            case "weaken":
            {
                int w = Mathf.RoundToInt((up == 1 ? 35f : 25f));
                return ja ? $"敵全体の与ダメ-{w}%＋味方スキル威力+20%（6秒）" : $"Enemies DMG -{w}% + allies skill +20% (6s)";
            }
            case "frost":
            {
                int dmg = Mathf.RoundToInt((60 + currentChapter * 25) * ult); if (up == 1) dmg = Mathf.RoundToInt(dmg * 1.5f);
                return ja ? $"敵全体に氷{dmg}ダメージ＋敵攻撃速度×0.7{(up==2?"＋スタン0.5秒":"")}" : $"All enemies: {dmg} frost dmg + ATKSPD x0.7{(up==2?" + stun":"")}";
            }
            default: return ja ? "味方全体を強化（1戦1回）" : "Empower all allies (once per battle)";
        }
    }

    // ヒーロー必殺：1戦1回。選択中ヒーローのIDで内容を分岐する（DESIGN_R3-hero-units）。
    // Aldin=聖盾（味方シールド＋被ダメ減）/ Kagachi=修羅（味方与ダメ＋攻速）/ Vesna=蒼炎（敵全体に青炎＋味方攻速）。
    public void UseHeroUltimate()
    {
        if (!CanUseHeroUltimate())
            return;
        heroUltUsedThisWave = true;
        heroUltCooldownRemaining = GetHeroUltCooldown(); // 必殺クールタイム開始（威力に応じた長さ）。
        bool ja = LocalizationManager.IsJapanese;
        string hero = (heroUnitId ?? string.Empty).ToLowerInvariant();
        int up = HeroUltUpgrade; // 0=未強化 / 1=A / 2=B（R3-hero-depth）
        float ult = HeroUltPower; // R3-hero-scale: ラン内Lvで必殺の威力が伸びる。

        // R3-hero-depth: 必殺カットイン演出（ヒーロー別・アップグレードで色変化）。
        HeroUltCutInUI.EnsureExists().Play(heroUnitId, up, GetHeroUltimateName(ja));

        switch (hero)
        {
            case "heroaldin": // 聖盾の号令：味方全体にシールド＋被ダメ-20%。A=シールド増 / B=効果延長。
            {
                float dur = up == 2 ? 10f : 6f;
                float shieldPct = (up == 1 ? 0.45f : 0.30f) * ult;
                for (int i = 0; i < team1Entities.Count; i++)
                {
                    BaseEntity e = team1Entities[i];
                    if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore) continue;
                    e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * shieldPct)), dur);
                    e.ApplyTimedSynergyDamageReductionBonus(0.20f, dur);
                    AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, e.transform.position, 1.0f);
                }
                AttackEffectPlayer.PlayUiSfx("fight_start");
                ScorePopupUI.EnsureExists().Show(1, GetHeroUltimateName(ja) + "！", new Color(1f, 0.92f, 0.55f));
                break;
            }

            case "herokagachi": // 修羅の号令：味方全体に与ダメ＋攻撃速度。A=与ダメ増 / B=攻速増。
            {
                float dur = 6f;
                float dmgBonus = (up == 1 ? 0.55f : 0.35f) * ult;
                float spd = up == 2 ? 1.45f : 1.25f;
                for (int i = 0; i < team1Entities.Count; i++)
                {
                    BaseEntity e = team1Entities[i];
                    if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore) continue;
                    e.ApplyTimedSynergyDamageDealtBonus(dmgBonus, dur);
                    e.ApplyAttackSpeedBoostFromSynergy(spd, dur);
                    AttackEffectPlayer.PlaySynergyEffect(SynergyType.Shadow, e.transform.position, 1.0f);
                }
                AttackEffectPlayer.PlayUiSfx("fight_start");
                ScorePopupUI.EnsureExists().Show(1, GetHeroUltimateName(ja) + "！", new Color(0.7f, 0.45f, 0.95f));
                break;
            }

            case "herovesna": // 蒼炎の号令：敵全体に青炎ダメージ＋味方攻撃速度。A=威力増 / B=スタン付与。
            {
                float dur = 6f;
                int dmg = Mathf.RoundToInt((80 + currentChapter * 40) * ult);
                if (up == 1) dmg = Mathf.RoundToInt(dmg * 1.6f);
                bool stun = up == 2;
                for (int i = 0; i < team2Entities.Count; i++)
                {
                    BaseEntity en = team2Entities[i];
                    if (en == null || en.IsDead || !en.IsOnBoard || en.IsCore) continue;
                    // TODO(VFX): 専用の「青い炎」エフェクトに差し替え。現状は Frost（青）を蒼炎の代理として使用。
                    AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, en.transform.position, 1.1f);
                    en.TakeDamage(dmg);
                    if (stun) en.ApplyStun(0.6f);
                }
                for (int i = 0; i < team1Entities.Count; i++)
                {
                    BaseEntity e = team1Entities[i];
                    if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore) continue;
                    e.ApplyAttackSpeedBoostFromSynergy(1.15f, dur);
                }
                AttackEffectPlayer.PlayUiSfx("fight_start");
                ScorePopupUI.EnsureExists().Show(1, GetHeroUltimateName(ja) + "！", new Color(0.4f, 0.7f, 1f));
                break;
            }

            default:
            {
                // 設定駆動ヒーロー（追加6＋将系9）：得意分野の必殺を適用。
                if (HeroUltConfig.TryGetValue(heroUnitId ?? string.Empty, out string[] cfg))
                {
                    ApplyConfiguredHeroUlt(cfg[0], up, ult, ja, GetHeroUltimateName(ja));
                    break;
                }
                // 真のフォールバック（未設定）：従来のコマンドラリー。
                float dur = 6f;
                for (int i = 0; i < team1Entities.Count; i++)
                {
                    BaseEntity e = team1Entities[i];
                    if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore) continue;
                    e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.20f)), dur);
                    e.ApplyTimedSynergyDamageDealtBonus(0.25f, dur);
                    e.ApplyAttackSpeedBoostFromSynergy(1.20f, dur);
                    AttackEffectPlayer.PlayAreaIndicator(e, e.transform.position, 0.55f, 0.9f, 1.3f);
                }
                AttackEffectPlayer.PlayUiSfx("fight_start");
                ScorePopupUI.EnsureExists().Show(1, ja ? "ヒーロー必殺！" : "Hero Ultimate!", new Color(1f, 0.85f, 0.4f));
                break;
            }
        }
    }

    // 設定駆動ヒーローの必殺効果を適用（種別ごと。up=バリアント、ult=熟練度威力倍率）。
    private void ApplyConfiguredHeroUlt(string kind, int up, float ult, bool ja, string name)
    {
        float dur = up == 2 ? 9f : 6f;
        Color popup = new Color(1f, 0.86f, 0.42f);
        switch (kind)
        {
            case "shield": // 守護：味方シールド＋被ダメ減（Lyonar/Vetruvian/Vanar守護）。
            {
                float pct = (up == 1 ? 0.40f : 0.28f) * ult;
                foreach (BaseEntity e in team1Entities)
                    if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore)
                    { e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * pct)), dur); e.ApplyTimedSynergyDamageReductionBonus(0.15f, dur); AttackEffectPlayer.PlaySynergyEffect(SynergyType.Guardian, e.transform.position, 1f); }
                popup = new Color(1f, 0.85f, 0.4f); break;
            }
            case "heal": // 回復：味方回復＋小シールド（Lyonar聖光）。
            {
                float hp = (up == 1 ? 0.45f : 0.30f) * ult;
                foreach (BaseEntity e in team1Entities)
                    if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore)
                    { e.HealFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * hp))); e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.1f)), dur); AttackEffectPlayer.PlaySynergyEffect(SynergyType.Divine, e.transform.position, 1f); }
                popup = new Color(1f, 0.95f, 0.6f); break;
            }
            case "rally": // 集結：味方与ダメ＋攻速（攻め）。
            {
                float dmg = (up == 1 ? 0.50f : 0.32f) * ult; float spd = up == 2 ? 1.45f : 1.25f;
                foreach (BaseEntity e in team1Entities)
                    if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore)
                    { e.ApplyTimedSynergyDamageDealtBonus(dmg, 6f); e.ApplyAttackSpeedBoostFromSynergy(spd, 6f); AttackEffectPlayer.PlaySynergyEffect(SynergyType.Warrior, e.transform.position, 1f); }
                popup = new Color(1f, 0.6f, 0.3f); break;
            }
            case "speed": // 速攻：味方攻速大＋小与ダメ（Songhai/Magmar嵐）。
            {
                float spd = up == 1 ? 1.5f : 1.3f;
                foreach (BaseEntity e in team1Entities)
                    if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore)
                    { e.ApplyAttackSpeedBoostFromSynergy(spd, 6f); e.ApplyTimedSynergyDamageDealtBonus(0.15f, 6f); AttackEffectPlayer.PlaySynergyEffect(SynergyType.Storm, e.transform.position, 1f); }
                popup = new Color(0.6f, 0.85f, 1f); break;
            }
            case "burst": // 範囲魔法：敵全体ダメージ（＋up2スタン）。
            {
                int dmg = Mathf.RoundToInt((75 + currentChapter * 35) * ult); if (up == 1) dmg = Mathf.RoundToInt(dmg * 1.6f);
                foreach (BaseEntity en in team2Entities)
                    if (en != null && !en.IsDead && en.IsOnBoard && !en.IsCore)
                    { AttackEffectPlayer.PlaySynergyEffect(SynergyType.Inferno, en.transform.position, 1.1f); en.TakeDamage(dmg); if (up == 2) en.ApplyStun(0.5f); }
                popup = new Color(1f, 0.55f, 0.25f); break;
            }
            case "drain": // 吸収：敵全体ダメージ＋味方回復（Abyssian）。
            {
                int dmg = Mathf.RoundToInt((55 + currentChapter * 28) * ult); if (up == 1) dmg = Mathf.RoundToInt(dmg * 1.5f);
                foreach (BaseEntity en in team2Entities)
                    if (en != null && !en.IsDead && en.IsOnBoard && !en.IsCore)
                    { AttackEffectPlayer.PlaySynergyEffect(SynergyType.Abyss, en.transform.position, 1.1f); en.TakeDamage(dmg); }
                foreach (BaseEntity e in team1Entities)
                    if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore)
                        e.HealFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.12f)));
                popup = new Color(0.7f, 0.3f, 0.9f); break;
            }
            case "weaken": // 弱体：敵与ダメ減＋味方スキル威力増（Abyssian呪い）。
            {
                float w = up == 1 ? 0.35f : 0.25f;
                foreach (BaseEntity en in team2Entities)
                    if (en != null && !en.IsDead && en.IsOnBoard && !en.IsCore)
                    { en.ApplyTimedSynergyDamageDealtBonus(-w, dur); AttackEffectPlayer.PlaySynergyEffect(SynergyType.Wraith, en.transform.position, 1f); }
                foreach (BaseEntity e in team1Entities)
                    if (e != null && !e.IsDead && e.IsOnBoard && !e.IsCore)
                        e.ApplyTimedSynergyPowerBonus(0.20f, dur);
                popup = new Color(0.6f, 0.25f, 0.7f); break;
            }
            case "frost": // 氷結：敵全体に氷ダメージ＋敵攻速低下（Vanar）。
            {
                int dmg = Mathf.RoundToInt((60 + currentChapter * 25) * ult); if (up == 1) dmg = Mathf.RoundToInt(dmg * 1.5f);
                foreach (BaseEntity en in team2Entities)
                    if (en != null && !en.IsDead && en.IsOnBoard && !en.IsCore)
                    { AttackEffectPlayer.PlaySynergyEffect(SynergyType.Frost, en.transform.position, 1.1f); en.TakeDamage(dmg); en.ApplyAttackSpeedBoostFromSynergy(0.7f, dur); if (up == 2) en.ApplyStun(0.5f); }
                popup = new Color(0.5f, 0.85f, 1f); break;
            }
        }
        AttackEffectPlayer.PlayUiSfx("fight_start");
        ScorePopupUI.EnsureExists().Show(1, name + "！", popup);
    }

    // 章ボス戦の固有ギミック：一定間隔で予兆付きの範囲攻撃を味方側へ落とす（章でスケール）。
    private System.Collections.IEnumerator BossMechanicRoutine()
    {
        yield return new WaitForSeconds(3.5f);
        int strikes = 2 + currentChapter; // ch1=3 / ch2=4 / ch3=5 …
        for (int i = 0; i < strikes; i++)
        {
            if (!IsRoundInProgress) yield break;
            yield return StartCoroutine(BossAoeStrike());
            yield return new WaitForSeconds(4f);
        }
    }

    private System.Collections.IEnumerator BossAoeStrike()
    {
        // 味方の誰かを狙って予兆→着弾。プレイヤーは“散開”や“鉄壁”で対応する。
        BaseEntity target = team1Entities.FirstOrDefault(e => e != null && !e.IsDead && e.IsOnBoard && !e.IsCore && !e.IsSummonedUnit);
        if (target == null) yield break;
        Vector3 center = target.transform.position;
        BaseEntity boss = team2Entities.FirstOrDefault(e => e != null && !e.IsDead && e.IsOnBoard);
        AttackEffectPlayer.PlayAreaIndicator(boss != null ? boss : target, center, 1.6f, 1.0f, 1.3f); // 予兆
        yield return new WaitForSeconds(1.0f);
        if (!IsRoundInProgress) yield break;

        int dmg = 110 + currentChapter * 60;
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore) continue;
            if (Vector3.Distance(e.transform.position, center) <= 1.6f)
            {
                e.TakeDamage(dmg);
                e.ApplyStun(0.6f);
            }
        }
    }

    // 戦闘開始時、解放済みボス（仲間化）に育成（アフィニティ）節目の固有パッシブを付与する。
    private void ApplyRecruitedBossPassives()
    {
        if (SaveManager.Instance == null) return;
        const float dur = 60f;
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore || e.IsSummonedUnit) continue;
            if (!SaveManager.Instance.HasBossAlly(e.UnitId)) continue;
            int lv = SaveManager.Instance.GetBossAffinityLevel(e.UnitId);
            if (lv >= 3) e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.15f)), dur); // 開幕シールド
            if (lv >= 5) e.ApplyAttackSpeedBoostFromSynergy(1.12f, dur);                                      // 攻撃速度
            if (lv >= 8) e.ApplyTimedSynergyDamageDealtBonus(0.12f, dur);                                     // 与ダメ
        }
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
        // 章導入演出中はドラッグ不可。
        if (IsStoryIntroBlocking())
            return false;

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
        // R3-hero-scale: ヒーローは無料枠なので、上限に達していても盤面へ出せる。
        if (!entity.IsOnBoard && !IsHeroUnit(entity.UnitId) && PlacedTeam1Count >= PlacementLimit)
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：ベンチのアイテムを盤面の最良ユニット（星が高い/ヒーロー優先）へ自動装備。装備数を返す。
    public int DebugAutoEquipBenchItems()
    {
        if (itemBenchItems == null || itemBenchItems.Count == 0) return 0;
        int equipped = 0;
        var items = new List<ItemInstance>(itemBenchItems);
        foreach (var inst in items)
        {
            if (inst == null || inst.Data == null || inst.Data.isRemover) continue;
            BaseEntity best = null; int bestScore = int.MinValue;
            foreach (var e in team1Entities)
            {
                if (e == null || e.IsCore || !e.IsOnBoard || !e.HasItemSpace) continue;
                int score = e.StarLevel * 10 + (IsHeroUnit(e.UnitId) ? 5 : 0) - e.EquippedItems.Count;
                if (score > bestScore) { bestScore = score; best = e; }
            }
            if (best != null && TryEquipItemToEntity(inst, best)) equipped++;
        }
        return equipped;
    }
#endif

    public bool TryEquipItemToEntity(ItemInstance itemInstance, BaseEntity targetEntity)
    {
        if (itemInstance == null || itemInstance.Data == null || targetEntity == null)
            return false;

        // アイテム取り外し機：装備中の味方に重ねるとアイテムを外してベンチへ戻す（消費しない）。
        if (itemInstance.Data.isRemover)
        {
            bool removedAny = false;
            if (targetEntity.Team == Team.Team1)
            {
                List<ItemData> removed = targetEntity.RemoveAllItems();
                if (removed != null && removed.Count > 0)
                {
                    ReturnItemsToBench(removed);
                    AttackEffectPlayer.PlayUiSfx("item_equip");
                    OnRosterChanged?.Invoke();
                    removedAny = true;
                }
            }
            // 取り外し機自身はベンチに戻す（消費されない）。
            if (itemBenchSlotByItem.TryGetValue(itemInstance, out int removerSlot))
                TryPlaceItemOnBench(itemInstance, removerSlot);
            RefreshItemBenchCanvasUi();
            return removedAny;
        }

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

        // R2-coremode: コアが破壊されたら即勝敗確定。敵コア→勝利 / 自コア→敗北。
        if (IsCoreMode && entity.IsCore)
        {
            team1Entities.Remove(entity);
            team2Entities.Remove(entity);
            OnUnitDied?.Invoke(entity);
            entity.DestroyAfterDeathAnimation();
            if (entity.Team == Team.Team2)
                HandleCoreModeVictory();
            else
                TriggerGameOver();
            OnRosterChanged?.Invoke();
            return;
        }

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
        if (IsRoundInProgress || waveClearCelebrating)
            return;
        // 遷移中(Warning含む)・進路選択/登場演出中はFIGHTを受け付けない（演出への割り込み防止）。
        if (waveTransitioning || bossIntroInProgress || bossCinematicActive)
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

        // R2-coremode: コア戦は専用の開始処理へ（1波目の手動FIGHT＋編成中の早期開始）。
        if (IsCoreMode)
        {
            StartCoreWave();
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

        // R3-boss-factions: ボス戦（中ボス/章ボス）の開始前に、ヒーロー将 vs ボス将の掛け合いを1回挟む。
        WaveDefinition curWave = waveDefinitions[currentWaveIndex];
        // STORY: 戦闘前の幕間（合流 INT_01 / 犬化 INT_08）を1回だけ挟む。完了後に DebugFight を再呼び出し。
        if (TryPlayPrefightStory(curWave)) return;
        // midboss-nodes: 進路選択枠なら、戦闘前に残りノードから1つ選ばせ、選択内容を枠へ流し込む。
        if (!IsCoreMode && curWave != null && curWave.IsNodeChoice)
        {
            ShowNodeChoice(curWave);
            return; // 選択完了後に DebugFight 再入。
        }
        if (!bossDialogueShownThisWave && curWave != null && (curWave.IsMidBossWave || curWave.IsBossWave))
        {
            bossDialogueShownThisWave = true;
            string bossId = GetWavePrimaryBossId(curWave);
            // STORY v2: 中ボスは章・登場枠(slot)ごとの別キャラ（異名＋固有かませ台本）。
            // ※非ブロッキング字幕は「読む前に戦闘が始まる」ためユーザー要望でブロッキング表示へ戻した。
            //   進路ノード先の中ボスも IsMidBossWave なのでここを通り、会話が表示される。
            string midNameOverride = null; string[] midScriptOverride = null;
            Color? midTint = null;
            if (!IsCoreMode && curWave.IsMidBossWave)
            {
                // ルート選択で実際に出る素体(bossId)に合わせて名前/台本を選ぶ（登場順ではなく素体IDで引く）。
                // 同素体が複数回出る場合は occurrence で別個体（ロウガ/メイリン等）へ切替。
                var mv = ChapterStory.GetMidVariantForUnit(currentChapter, bossId, MidBossOccurrenceOf(bossId));
                if (mv.HasValue) { midNameOverride = mv.Value.name; midScriptOverride = mv.Value.lines; midTint = mv.Value.tint; }
            }
            // 中ボス＝コンパクト（小アイコン）、章ボス＝大立ち絵。完了後に DebugFight 再入で実戦闘へ。
            // midTint＝中ボス別個体の色味（盤面スプライトと同じ色を会話アイコンへも乗算し、同素体を見分けやすく）。
            HeroBossDialogueUI.EnsureExists().Show(heroUnitId, bossId, GetEntityIconById(bossId), () => DebugFight(), curWave.IsMidBossWave, midNameOverride, midScriptOverride, midTint);
            return;
        }

        SnapshotPlayerBoardUnits();
        debugTrainingWaveActive = false;
        // R3: 編成中に事前配置（プレビュー）済みなら、その敵をそのまま使い再生成しない（入場演出の二重化を防ぐ）。
        if (!(enemiesPreviewedThisWave && team2Entities.Count > 0))
        {
            ClearEnemyUnits();
            SpawnWaveEnemies(waveDefinitions[currentWaveIndex]);
        }

        if (team2Entities.Count == 0)
        {
            Debug.LogWarning($"Wave {currentWaveIndex + 1} could not spawn any enemies.");
            return;
        }

        IsRoundInProgress = true;
        roundStartTime = Time.time;
        roundTimeoutResolved = false;
        heroUltUsedThisWave = false; // 同一ラウンド内の二重発動防止フラグ。
        if (heroUltCooldownRemaining > 0) heroUltCooldownRemaining--; // 必殺CTはラウンド経過で回復（0で再使用可）。
        heroReviveUsedThisWave = false;   // 主人公は将：自動復活は毎戦1回。
        heroFallenWeakenApplied = false;  // 弱体も毎戦リセット。
        SynergyManager.EnsureExists().ApplyBattleStartSynergies();
        ApplyBattleStartAugmentEffects();
        ApplyFormationBonuses();
        ApplyBuffTileBonuses();
        ApplyRecruitedBossPassives();
        ApplyHeroAura(); // R3-hero-depth: 盤面に主人公がいれば味方へオーラ。
        ApplyHeroScaling(); // R3-hero-scale: 盤面のヒーローへ現在の育成倍率＋節目スターを反映。
        AttackEffectPlayer.PlayUiSfx("fight_start");
        UpdateRoundProgressUi();
        OnRoundStart?.Invoke();
        // STORY v2: 節目の遠隔チャッターは「読む前に消える」ためユーザー要望で無効化（再有効化はこの行を戻す）。
        // MaybeShowChapterChatter();

        // ③b 中ボス戦では戦闘中に「転がる巨大物」ギミックを出す。
        // ただし盤面ギミックはチャプター3以降から。チャプター1・2は素のユニット戦のみ。
        if (!IsCoreMode && currentChapter >= 3
            && currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count && waveDefinitions[currentWaveIndex].IsMidBossWave)
            StartCoroutine(MidBossHazardRoutine());

        // 章ボス戦：固有ギミック（予兆付き範囲攻撃）を発動。
        if (!IsCoreMode
            && currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count && waveDefinitions[currentWaveIndex].IsBossWave)
            StartCoroutine(BossMechanicRoutine());
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
    // ===== midboss-nodes: 章ボス直前の進路選択（逐次ノード選択） =====
    // 1ノード分の素データ（選択時に枠 WaveDefinition へ流し込む）。
    private struct MidBossNode
    {
        public MidNodeArchetype archetype;
        public int star;                 // 代表中ボスの敵スター（精鋭=高）。
        public MidBossRewardKind reward;
        public int rewardCount, rewardCoins;
        public string a, b, c;           // 代表中ボス(a)＋仲間化候補(a/b/c)。
    }

    private readonly List<MidBossNode> nodePool = new List<MidBossNode>(); // 章ごと3ノード。
    private readonly List<int> nodeRemaining = new List<int>();            // 未選択ノードの index。
    private int nodePicksRemaining;                                        // 残り選択回数（2で開始）。

    private MidBossNode MakeNode(MidNodeArchetype arch, int star, MidBossRewardKind reward, int count, int coins, string a, string b, string c)
    {
        return new MidBossNode { archetype = arch, star = star, reward = reward, rewardCount = count, rewardCoins = coins, a = a, b = b, c = c };
    }

    // 章ボス直前セグメントを「進路選択」に置換する：3ノードを登録し、waveDefinitions にはノード枠を2つ追加する。
    private void AddNodeSegment(int stage, MidBossNode n0, MidBossNode n1, MidBossNode n2)
    {
        nodePool.Clear(); nodePool.Add(n0); nodePool.Add(n1); nodePool.Add(n2);
        nodeRemaining.Clear(); nodeRemaining.Add(0); nodeRemaining.Add(1); nodeRemaining.Add(2);
        nodePicksRemaining = 2;
        waveDefinitions.Add(NodeChoiceWave(stage, 7));
        waveDefinitions.Add(NodeChoiceWave(stage, 8));
    }

    private WaveDefinition NodeChoiceWave(int stage, int roundInStage)
    {
        var d = new WaveDefinition(); // 敵は選択時に流し込む。
        d.StageIndex = stage; d.RoundInStage = roundInStage;
        d.IsMidBossWave = true; d.IsNodeChoice = true;
        return d;
    }

    // 選ばれたノードの内容を、現在のノード枠 WaveDefinition へ流し込む（敵編成は RecruitMidBoss と同配置）。
    private void ApplyNodeToWave(WaveDefinition d, MidBossNode node)
    {
        int mobStar = Mathf.Max(1, node.star - 1);
        d.Enemies.Clear();
        d.Enemies.Add(new WaveEnemyPlacement(node.a, node.star, 7, 5));
        d.Enemies.Add(new WaveEnemyPlacement("Zyx", mobStar, 6, 4));
        d.Enemies.Add(new WaveEnemyPlacement("Zyx", mobStar, 6, 6));
        d.Enemies.Add(new WaveEnemyPlacement("Zyx", mobStar, 9, 5));
        d.RecruitCandidateIds.Clear();
        d.RecruitCandidateIds.Add(node.a); d.RecruitCandidateIds.Add(node.b); d.RecruitCandidateIds.Add(node.c);
        SetMidBossReward(d, node.reward, node.rewardCount, node.rewardCoins);
        d.IsNodeChoice = false; // 選択完了＝通常の中ボス戦へ。
    }

    // 進路選択UIを開き、選択結果を枠へ反映してから戦闘を再開する（逐次選択）。
    // reenterFightAfter=true: 選択後そのまま DebugFight 再入（オートプレイ等の従来フロー）。
    // reenterFightAfter=false: 選択後は preview に任せ、ボス登場演出→セリフ→準備→FIGHT のフローへ。
    private void ShowNodeChoice(WaveDefinition d, bool reenterFightAfter = true)
    {
        var options = new List<NodeSelectionUI.NodeOption>();
        foreach (int idx in nodeRemaining)
        {
            MidBossNode n = nodePool[idx];
            options.Add(BuildNodeOption(idx, n));
        }
        NodeSelectionUI.EnsureExists().Show(options, (pickedIndex) =>
        {
            if (pickedIndex >= 0 && pickedIndex < nodePool.Count)
            {
                ApplyNodeToWave(d, nodePool[pickedIndex]);
                nodeRemaining.Remove(pickedIndex);
                nodePicksRemaining = Mathf.Max(0, nodePicksRemaining - 1);
            }
            else
            {
                d.IsNodeChoice = false; // フォールバック：選べなければ素通り。
            }
            bossIntroInProgress = false; // 進路選択の導入処理おわり。
            if (reenterFightAfter)
                DebugFight();            // 枠が確定したので戦闘準備/開始へ再入。
            // reenterFightAfter=false の場合は何もしない：preview が次フレームでボス登場演出を再生する。
        });
    }

    private NodeSelectionUI.NodeOption BuildNodeOption(int poolIndex, MidBossNode n)
    {
        bool ja = LocalizationManager.IsJapanese;
        string tJa, tEn; Color tint;
        switch (n.archetype)
        {
            case MidNodeArchetype.Elite:   tJa = "精鋭"; tEn = "Elite";    tint = new Color(0.86f, 0.32f, 0.34f); break;
            case MidNodeArchetype.Supply:  tJa = "補給"; tEn = "Supply";   tint = new Color(0.46f, 0.80f, 0.52f); break;
            default:                       tJa = "標準"; tEn = "Standard"; tint = new Color(0.92f, 0.78f, 0.36f); break;
        }
        string dJa, dEn;
        switch (n.reward)
        {
            case MidBossRewardKind.ItemChoice3: dJa = "報酬：アイテム3択"; dEn = "Reward: pick 1 of 3 items"; break;
            case MidBossRewardKind.CoinReward:  dJa = $"報酬：大量コイン (+{n.rewardCoins})"; dEn = $"Reward: bonus coins (+{n.rewardCoins})"; break;
            case MidBossRewardKind.BuffTile:    dJa = "報酬：強化マス"; dEn = "Reward: buff tile"; break;
            default:                            dJa = $"報酬：仲間化 ({Mathf.Max(1, n.rewardCount)}体)"; dEn = $"Reward: recruit ({Mathf.Max(1, n.rewardCount)})"; break;
        }
        return new NodeSelectionUI.NodeOption
        {
            poolIndex = poolIndex,
            titleJa = tJa, titleEn = tEn,
            descJa = dJa, descEn = dEn,
            tint = tint,
            difficultyStar = Mathf.Clamp(n.star, 1, 3),
            icon = GetEntityIconById(n.a)
        };
    }

    private void InitializeWaveDefinitions()
    {
        if (waveDefinitions.Count > 0)
            return;

        if (IsCoreMode)
        {
            BuildCoreModeRounds();
            return;
        }

        BuildChapterRounds(currentChapter);
        ApplyMidBossRewardSchedule();
    }

    // R2-rewards: 中ボス枠の報酬を全章共通スケジュールに上書きする。
    // 2-5=アイテム3択 / 2-10=ボス選択 / 3-5=強化マス / 3-10=ボス選択 / 4-5=ボス選択x2 / 4-7,4-8=アイテム3択 / 4-9=大量コイン。
    private void ApplyMidBossRewardSchedule()
    {
        int coin = 70 + Mathf.Max(0, currentChapter - 1) * 25; // 大量コイン（章で増加）。
        for (int i = 0; i < waveDefinitions.Count; i++)
        {
            WaveDefinition d = waveDefinitions[i];
            if (d == null || d.IsBossWave || d.IsDebugWave || d.IsEventRound) continue;
            if (d.IsNodeChoice) continue; // 進路ノード枠は選択時に報酬を確定するためスケジュール対象外。
            int s = d.StageIndex, r = d.RoundInStage;
            // 仲間化(Recruit)は必ず「ボス3体が出現する RecruitMidBoss ウェーブ」に割り当てる
            //（＝戦ったボスから1体選んで仲間化＝ユーザー想定）。常に1体選択。
            // アイテム/強化マス/コインは非ボス戦 or 護衛戦に割り当てる。
            if (s == 2 && r == 5) SetMidBossReward(d, MidBossRewardKind.ItemChoice3);
            else if (s == 2 && r == 10) SetMidBossReward(d, MidBossRewardKind.Recruit, 1);   // ボス戦：1体選択
            else if (s == 3 && r == 5) SetMidBossReward(d, MidBossRewardKind.BuffTile);
            else if (s == 3 && r == 10) SetMidBossReward(d, MidBossRewardKind.Recruit, 1);  // ボス戦：1体選択
            // 4-5 は普通の戦闘（偽ボス選択を廃止）。stage4 の仲間化は実ボス戦 4-8 に置く。
            else if (s == 4 && r == 7) SetMidBossReward(d, MidBossRewardKind.ItemChoice3);
            else if (s == 4 && r == 8) SetMidBossReward(d, MidBossRewardKind.Recruit, 1);   // ボス戦：1体選択
            else if (s == 4 && r == 9) SetMidBossReward(d, MidBossRewardKind.CoinReward, 1, coin);
        }
    }

    private void SetMidBossReward(WaveDefinition d, MidBossRewardKind kind, int count = 1, int coins = 0)
    {
        d.IsMidBossWave = true;
        d.RewardKind = kind;
        d.RewardCount = Mathf.Max(1, count);
        d.RewardCoins = coins;
    }

    // R2-coremode: 最初の敵ウェーブだけ用意する。以降は EnsureCoreWaveAvailable が必要に応じて生成する。
    private void BuildCoreModeRounds()
    {
        waveDefinitions.Add(BuildCoreWave(0));
    }

    // R2-coremode: currentWaveIndex のウェーブが未生成なら、難易度をスケールさせて生成・追加する（無限ウェーブ）。
    private void EnsureCoreWaveAvailable()
    {
        while (currentWaveIndex >= waveDefinitions.Count)
            waveDefinitions.Add(BuildCoreWave(waveDefinitions.Count));
    }

    // R2-coremode: ウェーブ番号に応じた敵編成を作る。番号が上がるほど数・スター・コストが増える。
    private WaveDefinition BuildCoreWave(int waveIndex)
    {
        int star = Mathf.Clamp(1 + waveIndex / 4, 1, 3);
        int count = Mathf.Clamp(3 + waveIndex, 3, 10);
        int[] rows = { 5, 4, 6, 3, 7 };
        int[] cols = { 8, 7, 9 };
        List<WaveEnemyPlacement> placements = new List<WaveEnemyPlacement>();
        int placed = 0;
        for (int r = 0; r < rows.Length && placed < count; r++)
        {
            for (int c = 0; c < cols.Length && placed < count; c++)
            {
                // 敵コア(列10,行5)とは重ならない（列は7-9のみ）。
                placements.Add(new WaveEnemyPlacement(PickCoreEnemyKind(waveIndex, placed), star, cols[c], rows[r]));
                placed++;
            }
        }
        return StagedCombat(1, waveIndex + 1, placements.ToArray());
    }

    private WaveEnemyKind PickCoreEnemyKind(int waveIndex, int slot)
    {
        bool ranged = (slot % 3 == 2); // 3体に1体は遠距離。
        if (waveIndex >= 6)
            return ranged ? WaveEnemyKind.Cost2Ranged : WaveEnemyKind.Cost2Melee;
        if (waveIndex >= 2)
            return ranged ? WaveEnemyKind.Cost2Ranged : WaveEnemyKind.Cost1Melee;
        return ranged ? WaveEnemyKind.Cost1Ranged : WaveEnemyKind.Cost1Melee;
    }

    // R2-coremode: コア戦のウェーブ開始（1波目の手動FIGHT／編成中の早期開始／自動進行の共通入口）。
    private void StartCoreWave()
    {
        if (gameOver || coreModeResolved || IsRoundInProgress)
            return;

        InitializeWaveDefinitions();
        EnsureCoreWaveAvailable();

        ClearEnemyUnits();           // コアは残す（HasLivingEnemyBattleUnit/ClearEnemyUnits でコア除外済み）。
        SnapshotPlayerBoardUnits();  // コアは除外（HP持ち越し）。
        debugTrainingWaveActive = false;
        SpawnWaveEnemies(waveDefinitions[currentWaveIndex]);

        IsRoundInProgress = true;
        roundStartTime = Time.time;
        roundTimeoutResolved = false;
        SynergyManager.EnsureExists().ApplyBattleStartSynergies();
        ApplyBattleStartAugmentEffects();
        ApplyFormationBonuses();
        AttackEffectPlayer.PlayUiSfx("fight_start");
        UpdateRoundProgressUi();
        OnRoundStart?.Invoke();

        CoreModeHudUI hud = CoreModeHudUI.EnsureExists();
        hud.SetWaveInfo(coreWavesCleared + 1);
        hud.SetPhase(LocalizationManager.IsJapanese ? "戦闘中" : "In Battle");
    }

    // R2-coremode: コア戦のウェーブクリア処理（章モードの CompleteCurrentWave を流用せず軽量版）。
    private void CompleteCoreWave()
    {
        SynergyManager.Instance?.NotifyWaveCleared(false);
        IsRoundInProgress = false;
        debugTrainingWaveActive = false;
        ResetAllBattleRuntimeStates();
        AttackEffectPlayer.ClearBattleVisuals();
        OnRoundEnd?.Invoke();
        SynergyManager.Instance?.ClearBattleSynergyState();

        ClearTemporarySummons();
        ClearEnemyUnits();               // 敵ウェーブを片付ける（敵コアは残る）。
        RestorePlayerUnitsAfterWave();   // 味方を編成位置へ戻し全回復（コアは Snapshot 除外で不変）。
        ResolveAllAvailableUpgrades(UpgradeScope.AllOwned);

        currentWaveIndex++;
        coreWavesCleared++;
        EnsureCoreWaveAvailable(); // 次ウェーブを先に用意（進捗UIが「全クリア」表示にならないように）。
        Debug.Log($"[CoreMode] Wave {coreWavesCleared} cleared.");

        // 区切り（CoreModeBossMilestone 波ごと）にボスを1体恒久解放（R1-collection 連携）。
        if (coreWavesCleared % CoreModeBossMilestone == 0)
            ReleaseNextCoreModeBoss();

        // ウェーブクリア収入＋経験値（章モードと同等）。
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.GrantWaveClearIncome();
            PlayerData.Instance.AddExp(2 + ExtraExpPerWaveClear);
        }
        if (UIShop.Instance != null)
            UIShop.Instance.RequestFreeRerollOrPending();

        UpdateRoundProgressUi();
        OnRosterChanged?.Invoke();

        // 自動進行：インターバル→編成→次ウェーブ。
        if (coreAutoAdvanceRoutine != null)
            StopCoroutine(coreAutoAdvanceRoutine);
        coreAutoAdvanceRoutine = StartCoroutine(CoreAutoAdvanceRoutine());
    }

    // R2-coremode: 戦闘終了 → インターバル5秒 → 編成40秒 → 次ウェーブ自動開始。
    // 編成中にプレイヤーが FIGHT を押すと StartCoreWave が走り IsRoundInProgress=true になるので、ここは中断する。
    private System.Collections.IEnumerator CoreAutoAdvanceRoutine()
    {
        bool ja = LocalizationManager.IsJapanese;
        CoreModeHudUI hud = CoreModeHudUI.EnsureExists();

        float t = CoreModeIntervalSeconds;
        int lastInterval = -1;
        while (t > 0f)
        {
            if (gameOver || coreModeResolved || IsRoundInProgress) yield break;
            t -= Time.deltaTime;
            int whole = Mathf.CeilToInt(t);
            if (whole != lastInterval)
            {
                lastInterval = whole;
                hud.SetPhase((ja ? "小休止 " : "Rest ") + Mathf.Max(0, whole));
            }
            yield return null;
        }

        float b = CoreModeBuildSeconds;
        int lastWhole = -1;
        while (b > 0f)
        {
            if (gameOver || coreModeResolved || IsRoundInProgress) yield break;
            b -= Time.deltaTime;
            int whole = Mathf.CeilToInt(b);
            if (whole != lastWhole)
            {
                lastWhole = whole;
                hud.SetPhase((ja ? "編成タイム " : "Build ") + Mathf.Max(0, whole) + (ja ? " 秒（FIGHTで開戦）" : "s (FIGHT to start)"));
            }
            yield return null;
        }

        coreAutoAdvanceRoutine = null;
        StartCoreWave();
    }

    // R2-coremode: 区切り到達で、章ボス報酬の並び順にまだ未解放のボスを1体だけ恒久解放する。
    private void ReleaseNextCoreModeBoss()
    {
        if (SaveManager.Instance == null)
            return;

        List<string> ids = GetAllChapterBossRewardUnitIds();
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (string.IsNullOrEmpty(id) || SaveManager.Instance.HasBossAlly(id))
                continue;

            SaveManager.Instance.AddBossAlly(id, 1);
            OnRosterChanged?.Invoke();

            bool ja = LocalizationManager.IsJapanese;
            string display = LocalizationManager.UnitName(id);
            ScorePopupUI.EnsureExists().Show(500,
                (ja ? "ボス解放: " : "Boss unlocked: ") + display,
                new Color(1f, 0.78f, 0.42f));
            Debug.Log($"[CoreMode] Boss unlocked at wave {coreWavesCleared}: {id}");
            return;
        }

        Debug.Log("[CoreMode] All bosses already unlocked.");
    }

    // R2-coremode: 自陣（列1・行5）と敵陣（列10・行5）にコアを1基ずつ生成する。
    private void SpawnCores()
    {
        if (GridManager.Instance == null || entitiesDatabase == null || entitiesDatabase.allEntities == null)
            return;

        playerCore = SpawnCore(Team.Team1, 1, 5);
        enemyCore = SpawnCore(Team.Team2, 10, 5);

        CoreModeHudUI hud = CoreModeHudUI.EnsureExists();
        hud.SetCores(playerCore, enemyCore);
        hud.SetWaveInfo(1);
        hud.SetPhase(LocalizationManager.IsJapanese ? "FIGHTで開戦" : "Press FIGHT to begin");
    }

    private BaseEntity SpawnCore(Team team, int column, int row)
    {
        Node node = GridManager.Instance.GetNodeAtBoardCoordinate(column, row);
        if (node == null || node.IsOccupied)
        {
            Debug.LogWarning($"Core spawn node unavailable. Team:{team} Column:{column} Row:{row}");
            return null;
        }

        // コアの見た目は手頃なタンク系プレハブを流用する（挙動は ConfigureAsCore で停止拠点化）。
        EntitiesDatabaseSO.EntityData data = entitiesDatabase.allEntities
            .FirstOrDefault(d => d.prefab != null && string.Equals(d.name, "Borealjuggernaut", StringComparison.OrdinalIgnoreCase));
        if (data.prefab == null)
            data = entitiesDatabase.allEntities.FirstOrDefault(d => d.prefab != null);
        if (data.prefab == null)
            return null;

        Transform parent = team == Team.Team1 ? team1Parent : team2Parent;
        BaseEntity core = Instantiate(data.prefab, parent != null ? parent : transform);
        core.InitializeIdentity(data.name, data.cost, 1);
        if (team == Team.Team1)
            team1Entities.Add(core);
        else
            team2Entities.Add(core);
        core.Setup(team, node);
        // ConfigureAsCore は Setup 後に呼ぶ（CaptureBaseStats/ApplyStarVisualScale の後でHP・スケールを上書き）。
        core.ConfigureAsCore(6000);
        return core;
    }

    // ====== R3-chest-room: 宝箱を殴って開ける報酬部屋 ======
    private bool chestRoomActive;
    private const float ChestRoomTimeLimit = 30f;          // この秒数で強制終了（獲得済みだけ持ち越し）。
    private const float ChestClearTargetSeconds = 22f;     // 平均的な盤面がこの秒数で全開封できるようHPを決める。
    private bool chestRoomForceEnding;

    // チェスト部屋を生成する（アイテム3択の回。敵/シネマティックの代わりに宝箱を配置）。
    private void StartChestRoom(WaveDefinition d)
    {
        chestRoomActive = true;
        chestRoomForceEnding = false;
        ChestRoomHudUI.EnsureExists(); // 残り時間バナー。
        d.Enemies.Clear(); // 通常敵は出さない（宝箱だけ）。

        int coinChestCount = Mathf.Clamp(3 + (currentChapter - 1) / 3, 3, 6);
        float estDps = EstimatePlayerBoardDps();
        float pool = Mathf.Max(800f, estDps * ChestClearTargetSeconds);
        int coinHp = Mathf.Clamp(Mathf.RoundToInt(pool * 0.6f / coinChestCount), 250, 40000);
        int itemHp = Mathf.Clamp(Mathf.RoundToInt(pool * 0.4f), 400, 60000);

        // コイン箱：敵側の前〜中列(col 6-8)に行分散。アイテム箱：最奥(col 10)中央。
        int[] coinCols = { 6, 7, 8 };
        int[] coinRows = { 3, 5, 7, 2, 6, 4 };
        int placed = 0;
        for (int r = 0; r < coinRows.Length && placed < coinChestCount; r++)
        {
            int col = coinCols[placed % coinCols.Length];
            if (SpawnChest(col, coinRows[r], coinHp, false) != null) placed++;
        }
        SpawnChest(10, 5, itemHp, true); // アイテム箱（最奥・中央）。

        Debug.Log($"[ChestRoom] start: coinChests={placed}/{coinChestCount} coinHp={coinHp} itemHp={itemHp} estDps={estDps:F0}");
    }

    // 1個の宝箱を team2 に生成する。見た目は宝箱スプライト、挙動はサンドバッグ。
    private BaseEntity SpawnChest(int column, int row, int hp, bool isItemChest)
    {
        if (GridManager.Instance == null || entitiesDatabase == null) return null;
        Node node = GridManager.Instance.GetNodeAtBoardCoordinate(column, row);
        if (node == null || node.IsOccupied) return null;

        EntitiesDatabaseSO.EntityData data = entitiesDatabase.allEntities
            .FirstOrDefault(x => x.prefab != null && string.Equals(x.name, "Borealjuggernaut", StringComparison.OrdinalIgnoreCase));
        if (data.prefab == null) data = entitiesDatabase.allEntities.FirstOrDefault(x => x.prefab != null);
        if (data.prefab == null) return null;

        BaseEntity chest = Instantiate(data.prefab, team2Parent != null ? team2Parent : transform);
        chest.InitializeIdentity(data.name, data.cost, 1);
        team2Entities.Add(chest);
        chest.Setup(Team.Team2, node);

        System.Action coinDrop = () => { PlayerData.Instance?.AddMoney(1); };
        System.Action opened = isItemChest
            ? (System.Action)(() => { ShowItemChoice3Reward(); })
            : (System.Action)(() => { PlayerData.Instance?.AddMoney(5); });
        chest.ConfigureAsChest(hp, isItemChest, coinDrop, opened);

        // 宝箱スプライト（アイテム=通常宝箱 / コイン=festive）。idle=閉じてキラめき、open=開封アニメに分ける。
        Sprite[] all = Resources.LoadAll<Sprite>(isItemChest ? "fx/item_treasurechest" : "fx/item_treasurechest_festive");
        Sprite[] idle = all.Where(s => s != null && s.name.Contains("_idle_")).OrderBy(s => s.name).ToArray();
        Sprite[] open = all.Where(s => s != null && s.name.Contains("_open_")).OrderBy(s => s.name).ToArray();
        chest.SetChestFrames(idle, open);
        return chest;
    }

    // R3-chest-room: HUD（残り秒数）用の公開状態。
    public bool IsChestRoomActive => chestRoomActive;
    public float ChestRoomSecondsLeft => Mathf.Max(0f, ChestRoomTimeLimit - (IsRoundInProgress ? RoundElapsedTime : 0f));

    // 盤面の質を推定（配置数×章のプロキシ）。実DPSの厳密値は使わず、堅牢に概算する。
    private float EstimatePlayerBoardDps()
    {
        int placed = Mathf.Max(1, PlacedTeam1Count);
        float perUnit = 40f + currentChapter * 18f; // 章が進むほど1体あたりの推定DPSを上げる。
        return placed * perUnit;
    }

    // 制限時間の監視（Updateから毎フレーム）。30秒で残りの宝箱を片付けてラウンドを終える。
    private void HandleChestRoomTimer()
    {
        if (!chestRoomActive || !IsRoundInProgress || chestRoomForceEnding) return;
        if (RoundElapsedTime < ChestRoomTimeLimit) return;

        chestRoomForceEnding = true;
        // 残っている宝箱を「開封せず」に撤去（未開封＝報酬なし。落とした分は獲得済み）。
        for (int i = team2Entities.Count - 1; i >= 0; i--)
        {
            BaseEntity e = team2Entities[i];
            if (e == null) { team2Entities.RemoveAt(i); continue; }
            if (!e.IsChest) continue;
            if (e.CurrentNode != null) e.CurrentNode.SetOccupied(false);
            team2Entities.RemoveAt(i);
            Destroy(e.gameObject);
        }
        Debug.Log("[ChestRoom] time up: remaining chests removed (kept earned rewards).");
    }

    // ラウンド終了/離脱時にチェスト部屋状態を解除。
    private void EndChestRoom()
    {
        chestRoomActive = false;
        chestRoomForceEnding = false;
    }

    // R2-coremode: 敵コア破壊で勝利。1回だけ処理する。
    private void HandleCoreModeVictory()
    {
        if (coreModeResolved)
            return;
        coreModeResolved = true;
        gameOver = true;
        IsRoundInProgress = false;
        ResetAllBattleRuntimeStates();
        AttackEffectPlayer.ClearBattleVisuals();
        OnRoundEnd?.Invoke();
        Debug.Log("[CoreMode] Enemy core destroyed. Victory!");

        bool ja = LocalizationManager.IsJapanese;
        float elapsed = Time.unscaledTime - chapterStartTime;
        ResultPanelUI.EnsureExists().ShowStageResult(
            1,
            elapsed,
            0,
            ja ? "敵コアを破壊した！" : "Enemy core destroyed!",
            true,
            0,
            false);
    }

    // 指定チャプターのラウンド列を組み立てます。チャプターを増やす時はここに分岐を足します。
    private void BuildChapterRounds(int chapter)
    {
        switch (chapter)
        {
            // R3-boss-factions: ch4-6 = Magmar(f5) 陣営。章ボス＝base/alt/third、同胞がエリート敵で登場。
            case 4: BuildScaledFactionChapter(4, "Magmarvaath", MagmarElites, MagmarRecruit3, MagmarRecruit4, MagmarEscorts); break;
            case 5: BuildScaledFactionChapter(5, "Magmarstarhorn", MagmarElites, MagmarRecruit3, MagmarRecruit4, MagmarEscorts); break;
            case 6: BuildScaledFactionChapter(6, "Magmarragnora", MagmarElites, MagmarRecruit3, MagmarRecruit4, MagmarEscorts); break;
            // ch7-9 = Abyssian(f4) 陣営。
            case 7: BuildScaledFactionChapter(7, "Abyssallilithe", AbyssianElites, AbyssianRecruit3, AbyssianRecruit4, AbyssianEscorts); break;
            case 8: BuildScaledFactionChapter(8, "Abyssalcassyva", AbyssianElites, AbyssianRecruit3, AbyssianRecruit4, AbyssianEscorts); break;
            case 9: BuildScaledFactionChapter(9, "Abyssalmaehv", AbyssianElites, AbyssianRecruit3, AbyssianRecruit4, AbyssianEscorts); break;
            // ch10-12 = Vetruvian(f3) 陣営。
            case 10: BuildScaledFactionChapter(10, "Vetruvianzirix", VetruvianElites, VetruvianRecruit3, VetruvianRecruit4, VetruvianEscorts); break;
            case 11: BuildScaledFactionChapter(11, "Vetruviansajj", VetruvianElites, VetruvianRecruit3, VetruvianRecruit4, VetruvianEscorts); break;
            case 12: BuildScaledFactionChapter(12, "Vetruvianscion", VetruvianElites, VetruvianRecruit3, VetruvianRecruit4, VetruvianEscorts); break;
            // ch13-18 = Mechaz0r連章（中立メカ）。ch19 = Hydrax。中立プールで量産。
            case 13: BuildScaledFactionChapter(13, "neutral_mechaz0rwing", MechaElites, MechaRecruit3, MechaRecruit4, MechaEscorts); break;
            case 14: BuildScaledFactionChapter(14, "neutral_mechaz0rsword", MechaElites, MechaRecruit3, MechaRecruit4, MechaEscorts); break;
            case 15: BuildScaledFactionChapter(15, "neutral_mechaz0rsuper", MechaElites, MechaRecruit3, MechaRecruit4, MechaEscorts); break;
            case 16: BuildScaledFactionChapter(16, "neutral_mechaz0rhelm", MechaElites, MechaRecruit3, MechaRecruit4, MechaEscorts); break;
            case 17: BuildScaledFactionChapter(17, "neutral_mechaz0rchassis", MechaElites, MechaRecruit3, MechaRecruit4, MechaEscorts); break;
            case 18: BuildScaledFactionChapter(18, "neutral_mechaz0rcannon", MechaElites, MechaRecruit3, MechaRecruit4, MechaEscorts); break;
            case 19: BuildScaledFactionChapter(19, "neutral_hydrax", MechaElites, MechaRecruit3, MechaRecruit4, MechaEscorts); break;
            // ch20 = 最終章 Arcana。全陣営の将がエリートとして集結する総力戦。
            case 20: BuildScaledFactionChapter(20, "Arcana", FinaleElites, FinaleRecruit3, FinaleRecruit4, FinaleEscorts); break;
            case 3:
                BuildChapter3Rounds();
                break;
            case 2:
                BuildChapter2Rounds();
                break;
            default:
                BuildChapter1Rounds();
                break;
        }
    }

    // R3-boss-factions: Magmar陣営のプール（仮）。エリート＝同胞3将、勧誘候補＝既存ボス級、護衛＝cost5。
    private static readonly string[] MagmarElites = { "Magmarvaath", "Magmarstarhorn", "Magmarragnora" };
    private static readonly string[] MagmarRecruit3 = { "Silitharelder", "neutral_beastmaster", "neutral_gnasher" };
    private static readonly string[] MagmarRecruit4 = { "Makantorwarbeast", "Veteransilithar", "neutral_rawr", "neutral_rok", "neutral_zukong" };
    private static readonly string[] MagmarEscorts = { "Gol", "Kron", "Invader" };
    // Abyssian陣営のプール（仮）。
    private static readonly string[] AbyssianElites = { "Abyssallilithe", "Abyssalcassyva", "Abyssalmaehv" };
    private static readonly string[] AbyssianRecruit3 = { "Gloomchaser", "neutral_beastmaster", "neutral_gnasher" };
    private static readonly string[] AbyssianRecruit4 = { "Abyssalcrawler", "neutral_rawr", "neutral_rok", "neutral_zukong", "Gloomchaser" };
    private static readonly string[] AbyssianEscorts = { "Legion", "Invader", "Gol" };
    // Vetruvian陣営のプール（仮）。
    private static readonly string[] VetruvianElites = { "Vetruvianzirix", "Vetruviansajj", "Vetruvianscion" };
    private static readonly string[] VetruvianRecruit3 = { "Rae", "Starfirescarab", "neutral_beastmaster" };
    private static readonly string[] VetruvianRecruit4 = { "Pax", "Pyromancer", "neutral_rok", "neutral_zukong", "neutral_rawr" };
    private static readonly string[] VetruvianEscorts = { "Kron", "Gol", "Legion" };
    // Mechaz0r連章(13-18)＆Hydrax(19) の中立プール。エリート＝メカ群、勧誘候補＝既存ボス級、護衛＝cost5。
    private static readonly string[] MechaElites = { "neutral_mechaz0rsword", "neutral_mechaz0rwing", "neutral_mechaz0rcannon" };
    private static readonly string[] MechaRecruit3 = { "neutral_beastmaster", "neutral_gnasher", "neutral_rawr" };
    private static readonly string[] MechaRecruit4 = { "neutral_rok", "neutral_zukong", "neutral_beastmaster", "neutral_gnasher", "neutral_rawr" };
    private static readonly string[] MechaEscorts = { "Gol", "Kron", "Invader" };
    // 最終章 Arcana（全陣営の将が集結する総力戦）。エリート＝各陣営の頭目、護衛＝各陣営の将＋cost5。
    private static readonly string[] FinaleElites = { "Magmarvaath", "Abyssallilithe", "Vetruvianzirix" };
    private static readonly string[] FinaleRecruit3 = { "Silitharelder", "Gloomchaser", "Pax" };
    private static readonly string[] FinaleRecruit4 = { "Makantorwarbeast", "Abyssalcrawler", "Starfirescarab", "neutral_rok", "neutral_zukong" };
    private static readonly string[] FinaleEscorts = { "Magmarragnora", "Abyssalmaehv", "Vetruvianscion" };

    // 章番号で難度スケールする汎用チャプタージェネレータ（ch4以降のボス陣営章を量産する）。
    // 4ステージ/全33ラウンド。Stage1=やさしい立ち上がり、2-4=戦闘＋イベント＋中ボス、4-10=章ボス。
    // elites=陣営の同胞（エリート敵）、recruit3/4=中ボス勧誘候補（既存ボス級）、escorts=cost5大護衛。
    private void BuildScaledFactionChapter(int chapter, string chapterBoss, string[] elites, string[] recruit3, string[] recruit4, string[] escorts)
    {
        // 難度ブースト（ch4=0,ch5=1,ch6=2…）。敵スターに反映。
        int boost = Mathf.Clamp(chapter - 4, 0, 6);
        int s2 = 2 + (boost >= 2 ? 1 : 0);
        int s3 = 2 + (boost >= 1 ? 1 : 0);
        int s4 = 3 + (boost >= 2 ? 1 : 0);
        string E0 = elites.Length > 0 ? elites[0] : chapterBoss;
        string E1 = elites.Length > 1 ? elites[1] : E0;
        string E2 = elites.Length > 2 ? elites[2] : E1;
        string R3a = recruit3[0], R3b = recruit3[1], R3c = recruit3[2 % recruit3.Length];
        string R4a = recruit4[0], R4b = recruit4[1 % recruit4.Length], R4c = recruit4[2 % recruit4.Length];

        // コスト4/5の敵は★2が上限（cost4/5の★3はプレイヤー専用の強さ。章ボスも★2）。
        // 難度はユニット構成と cost2/3 のスター(s2/s3)で表現する。
        const int e4 = 2;
        WaveEnemyPlacement Kind(WaveEnemyKind k, int star, int col, int row, int coins = 0, bool item = false) => new WaveEnemyPlacement(k, star, col, row, -1, false, coins, item);
        WaveEnemyPlacement Fix(string id, int star, int col, int row, int coins = 0, bool item = false) => new WaveEnemyPlacement(id, star, col, row, coins, item);

        // === Stage 1（全章共通のやさしい立ち上がり。資源を多めに配る） ===
        string Zm(int i) { string[] m = { "neutral_z0r", "neutral_nip", "neutral_grincher", "neutral_soboro", "neutral_goldenmantella" }; return m[(chapter + i) % m.Length]; }
        string Zr(int i) { string[] r = { "neutral_ion", "neutral_aer" }; return r[(chapter + i) % r.Length]; }
        waveDefinitions.Add(StagedCombat(1, 1, Fix(Zm(0), 1, 7, 5, 5)));
        waveDefinitions.Add(StagedCombat(1, 2, Fix(Zm(1), 1, 6, 4, 0, true), Fix(Zr(0), 1, 10, 5, 4)));
        waveDefinitions.Add(StagedCombat(1, 3, Fix(Zm(2), 1, 6, 4, 0, true), Fix(Zm(3), 1, 6, 6, 3), Fix(Zr(1), 1, 10, 5, 4)));

        // === Stage 2 ===
        waveDefinitions.Add(StagedCombat(2, 1, Kind(WaveEnemyKind.Cost2Melee, s2, 6, 4, 3), Kind(WaveEnemyKind.Cost2Melee, s2, 6, 6), Kind(WaveEnemyKind.Cost2Ranged, s2, 9, 5)));
        waveDefinitions.Add(StagedCombat(2, 2, Fix(R3a, s2, 7, 4), Fix(R3b, s2, 6, 6, 0, true), Kind(WaveEnemyKind.Cost2Ranged, s2, 9, 5)));
        waveDefinitions.Add(StagedEvent(2, 3, WaveEventType.AugmentSilver));
        waveDefinitions.Add(StagedCombat(2, 4, Fix(R3c, s2, 6, 4, 4), Kind(WaveEnemyKind.Cost2Melee, s2, 6, 6), Kind(WaveEnemyKind.Cost2Ranged, s2, 10, 5)));
        waveDefinitions.Add(RecruitMidBoss(2, 5, s2, R3a, R3b, R3c));
        waveDefinitions.Add(StagedCombat(2, 6, Fix(E0, e4, 7, 5, 4), Kind(WaveEnemyKind.Cost2Melee, s2, 6, 4), Kind(WaveEnemyKind.Cost2Ranged, s2, 9, 6)));
        waveDefinitions.Add(StagedCombat(2, 7, Kind(WaveEnemyKind.Cost2Melee, s2, 6, 4), Kind(WaveEnemyKind.Cost2Melee, s2, 6, 6, 3), Kind(WaveEnemyKind.Cost2Ranged, s2, 9, 4), Kind(WaveEnemyKind.Cost2Ranged, s2, 9, 6)));
        waveDefinitions.Add(StagedChestRoom(2, 8));
        waveDefinitions.Add(StagedCombat(2, 9, Fix(R4a, e4, 6, 4, 0, true), Fix(R4b, e4, 6, 6), Kind(WaveEnemyKind.Cost2Ranged, s2, 10, 5)));
        waveDefinitions.Add(RecruitMidBoss(2, 10, s2, R3c, R3b, R3a));

        // === Stage 3 ===
        waveDefinitions.Add(StagedCombat(3, 1, Fix(R3a, s3, 6, 4), Fix(R3b, s3, 6, 6), Fix(R4a, 1, 10, 5, 4)));
        waveDefinitions.Add(StagedCombat(3, 2, Fix(R4b, e4, 7, 5), Fix(E0, e4, 6, 4), Fix(R3c, s3, 6, 6, 0, true)));
        waveDefinitions.Add(StagedEvent(3, 3, WaveEventType.AugmentGold));
        waveDefinitions.Add(StagedCombat(3, 4, Fix(R4c, e4, 7, 4), Fix(E1, e4, 6, 6), Kind(WaveEnemyKind.Cost2Ranged, s3, 9, 5, 5)));
        waveDefinitions.Add(RecruitMidBoss(3, 5, e4, R4a, R4b, R4c));
        waveDefinitions.Add(StagedCombat(3, 6, Fix(E0, e4, 10, 5), Fix(R4a, e4, 6, 4, 0, true), Fix(R3a, s3, 6, 6)));
        waveDefinitions.Add(StagedChestRoom(3, 7));
        waveDefinitions.Add(StagedCombat(3, 8, Fix(R4b, e4, 7, 4), Fix(E1, e4, 7, 6), Kind(WaveEnemyKind.Cost2Ranged, s3 + 1, 10, 5, 5)));
        waveDefinitions.Add(StagedCombat(3, 9, Fix(E2, e4, 6, 4), Fix(R4c, e4, 6, 6, 0, true), Kind(WaveEnemyKind.Cost2Ranged, s3, 9, 5)));
        waveDefinitions.Add(RecruitMidBoss(3, 10, e4, R4c, R4a, R4b));

        // === Stage 4（4-10 章ボス） ===
        waveDefinitions.Add(StagedCombat(4, 1, Fix(E0, e4, 6, 4), Fix(E1, e4, 6, 6), Fix(R4a, 1, 10, 5, 6)));
        waveDefinitions.Add(StagedCombat(4, 2, Fix(escorts[0], 1, 7, 5), Fix(escorts[1 % escorts.Length], 1, 6, 4, 0, true), Fix(E2, e4, 6, 6)));
        waveDefinitions.Add(StagedEvent(4, 3, WaveEventType.AugmentPrism));
        waveDefinitions.Add(StagedCombat(4, 4, Fix(E0, e4, 6, 4), Fix(E1, e4, 7, 5, 6), Fix(E2, e4, 6, 6)));
        waveDefinitions.Add(RecruitMidBoss(4, 5, e4, R4a, R4b, R4c));
        waveDefinitions.Add(StagedCombat(4, 6, Fix(escorts[0], e4, 7, 5), Fix(E0, e4, 6, 4, 0, true), Fix(E1, e4, 6, 6)));
        // 4-7/4-8: 進路選択（midboss-nodes）。3提示→逐次2選択→残り1破棄。
        //   cost4の★2上限を守りつつ、精鋭は陣営エリート(E0)を主役に据えて歯ごたえを出す。コインは章で増加。
        AddNodeSegment(4,
            MakeNode(MidNodeArchetype.Elite,    e4,                   MidBossRewardKind.ItemChoice3, 1, 0,                 E0,  R4a, R4b),
            MakeNode(MidNodeArchetype.Standard, e4,                   MidBossRewardKind.Recruit,     1, 0,                 R4a, R4b, R4c),
            MakeNode(MidNodeArchetype.Supply,   Mathf.Max(1, e4 - 1), MidBossRewardKind.CoinReward,  1, 70 + chapter * 6,  R4c, R4a, R4b));
        waveDefinitions.Add(StagedBoss(4, 10,
            new WaveEnemyPlacement(chapterBoss, e4, 8, 5),
            new WaveEnemyPlacement(escorts[0], 1, 6, 4),
            new WaveEnemyPlacement(escorts[1 % escorts.Length], 1, 6, 6),
            new WaveEnemyPlacement(E1, e4, 9, 5)));
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

    // R2-recruit: 中ボスウェーブ。撃破後に候補3体(a/b/c)から1体を「その章だけ」解放。
    // STORY v2: 盤面は「メインで喋る中ボス1体(=a、ダイアログアイコン持ち・色ティント・かませ台詞)＋
    //   アイコンを持たない雑魚(Zyx=neutral_z0r 系)の取り巻き」にする。b/c は盤面に出さず、解放候補としてのみ保持。
    private WaveDefinition RecruitMidBoss(int stage, int roundInStage, int bossStar, string a, string b, string c)
    {
        int mobStar = Mathf.Max(1, bossStar - 1); // 取り巻き雑魚はボスより1段やさしめ。
        var d = new WaveDefinition(
            new WaveEnemyPlacement(a, bossStar, 7, 5),                               // メイン中ボス（喋る/色違い）。
            new WaveEnemyPlacement(MidBossEscortMob(stage, roundInStage, 0), mobStar, 6, 4),
            new WaveEnemyPlacement(MidBossEscortMob(stage, roundInStage, 1), mobStar, 6, 6),
            new WaveEnemyPlacement(MidBossEscortMob(stage, roundInStage, 2), mobStar, 9, 5));
        d.StageIndex = stage;
        d.RoundInStage = roundInStage;
        d.IsMidBossWave = true;
        d.RecruitCandidateIds.Add(a);
        d.RecruitCandidateIds.Add(b);
        d.RecruitCandidateIds.Add(c);
        return d;
    }

    // 中ボスの取り巻きは「ダイアログアイコンを持たない雑魚 Zyx」で固定（ユーザー要望）。
    private string MidBossEscortMob(int stage, int roundInStage, int index) => "Zyx";

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

    // R3-chest-room: アイテムが貰えるイベント回（2-8, 3-7 等）を「宝箱を殴って開けるトレジャー戦闘」にする。
    // 敵なしの通常戦闘ウェーブ（イベント扱いにはしない＝ボス/戦闘ラウンドには影響しない）。宝箱は preview で配置。
    private WaveDefinition StagedChestRoom(int stage, int roundInStage)
    {
        var d = new WaveDefinition(); // 敵なし。チェストは StartChestRoom で生成。
        d.StageIndex = stage;
        d.RoundInStage = roundInStage;
        d.IsChestRoom = true;
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
        // 2-5: ボス戦（Shadowlord/Malyk/Skindogehai）。報酬=アイテム3択（スケジュールで割当）。コスト上限解放。
        waveDefinitions.Add(RecruitMidBoss(2, 5, 2, "neutral_beastmaster", "neutral_gnasher", "neutral_rawr"));
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
        waveDefinitions.Add(StagedChestRoom(2, 8));
        // 2-9
        waveDefinitions.Add(StagedCombat(2, 9,
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 6)));
        // 2-10: ボス戦（Decepticleprime/Decepticlechassis/Ilenamk2）。報酬=仲間化（戦った3体から1体）。コスト上限解放。
        waveDefinitions.Add(RecruitMidBoss(2, 10, 2, "neutral_gnasher", "neutral_rawr", "neutral_rok"));

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
        waveDefinitions.Add(RecruitMidBoss(3, 5, 2, "neutral_rawr", "neutral_rok", "neutral_zukong"));
        // 3-6
        waveDefinitions.Add(StagedCombat(3, 6,
            new WaveEnemyPlacement("Malyk", 2, 7, 4),
            new WaveEnemyPlacement("Shadowlord", 2, 7, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 3-7: イベント（アイテム）
        waveDefinitions.Add(StagedChestRoom(3, 7));
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
        waveDefinitions.Add(RecruitMidBoss(3, 10, 2, "neutral_rok", "neutral_zukong", "neutral_beastmaster"));

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
        // 4-7/4-8: 進路選択（midboss-nodes）。3ノード提示→逐次2選択→残り1破棄。各ノードでアーキタイプ別の難度/報酬。
        //   🔴精鋭=★3・アイテム3択 / 🟡標準=★2・仲間化1体 / 🟢補給=★1・大量コイン。
        AddNodeSegment(4,
            MakeNode(MidNodeArchetype.Elite,    3, MidBossRewardKind.ItemChoice3, 1, 0,   "neutral_zukong", "neutral_beastmaster", "neutral_gnasher"),
            MakeNode(MidNodeArchetype.Standard, 2, MidBossRewardKind.Recruit,     1, 0,   "neutral_beastmaster_crimson", "neutral_rawr", "neutral_zukong"),  // 2体目のbeastmaster=傷鬣ロウガ(深紅)
            MakeNode(MidNodeArchetype.Supply,   1, MidBossRewardKind.CoinReward,  1, 95,  "neutral_gnasher_ice", "neutral_rok", "neutral_beastmaster"));      // 2体目のgnasher=白符メイリン(氷)
        // 4-10: チャプターボス Caliber-O（章選択の表紙キャラ）。報酬 cost4 Caliber(★3)＝倒した本人を恒久解放。
        //        旧cost5ボス(Legion等)は大護衛として残し迫力を維持（cost5解放はch6+）。
        waveDefinitions.Add(StagedBoss(4, 10,
            new WaveEnemyPlacement("Caliber", 2, 8, 5),
            new WaveEnemyPlacement("Legion", 1, 9, 3),
            new WaveEnemyPlacement("Gol", 1, 9, 7),
            new WaveEnemyPlacement("Wraith", 2, 7, 4),
            new WaveEnemyPlacement("Wujin", 2, 7, 6)));
    }

    // チャプター2（全33ラウンド）。章1より一段強い構成。最終ボスは Skyfalltyrant（撃破で永続 roster に加入）。
    // 章1クリアでボス仲間（Legion）を連れて来られる前提で、序盤から圧を強めに、中盤以降はコスト4-5を多用します。
    // 中ボス: 2-5, 2-10, 3-5, 3-10, 4-7, 4-8, 4-9（撃破でコスト上限解放）。章ボス: 4-10。座標規約は章1に準拠（列6-10 / 行3-7）。
    private void BuildChapter2Rounds()
    {
        // === Stage 1: 立ち上がりは全章共通でやさしく（1体start＋確定負け回避）。数はR3で増やす。 ===
        // 1-1: 敵1体（コインドロップ）。1体配置でも勝てる導入。
        waveDefinitions.Add(StagedCombat(1, 1,
            new WaveEnemyPlacement("Zyx", 1, 7, 5, dropCoins: 3)));
        // 1-2: 敵2体（アイテム＋コイン）。
        waveDefinitions.Add(StagedCombat(1, 2,
            new WaveEnemyPlacement("Zyx", 1, 7, 4, dropItem: true),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 9, 5, dropCoins: 2)));
        // 1-3: コスト1中心の3体（アイテム1＋コイン）。やさしめ。
        waveDefinitions.Add(StagedCombat(1, 3,
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 6, 4, dropItem: true),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Ranged, 1, 10, 5, dropCoins: 2)));

        // === Stage 2: コスト2-3。2-5/2-10 が中ボス（コスト4）。 ===
        // 2-1
        waveDefinitions.Add(StagedCombat(2, 1,
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 1, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 1, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 1, 9, 5)));
        // 2-2
        waveDefinitions.Add(StagedCombat(2, 2,
            new WaveEnemyPlacement("Shadowlord", 2, 7, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 5)));
        // 2-3: イベント（シルバーオーグメント）
        waveDefinitions.Add(StagedEvent(2, 3, WaveEventType.AugmentSilver));
        // 2-4
        waveDefinitions.Add(StagedCombat(2, 4,
            new WaveEnemyPlacement("Kane", 2, 6, 4),
            new WaveEnemyPlacement("Shadowlord", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 5)));
        // 2-5: 中ボス（Wraith ＋ コスト2護衛）。コスト4解放。
        waveDefinitions.Add(RecruitMidBoss(2, 5, 2, "neutral_rok", "neutral_zukong", "neutral_gnasher"));            // 灰道の足止めゴーレム(rok基本)
        // 2-6
        waveDefinitions.Add(StagedCombat(2, 6,
            new WaveEnemyPlacement("Malyk", 2, 6, 4),
            new WaveEnemyPlacement("Kane", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 2-7
        waveDefinitions.Add(StagedCombat(2, 7,
            new WaveEnemyPlacement("Paragon", 2, 7, 4),
            new WaveEnemyPlacement("Shadowlord", 2, 7, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 6)));
        // 2-8: イベント（アイテム）
        waveDefinitions.Add(StagedChestRoom(2, 8));
        // 2-9
        waveDefinitions.Add(StagedCombat(2, 9,
            new WaveEnemyPlacement("Ilenamk2", 2, 6, 4),
            new WaveEnemyPlacement("Tier2general", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 2-10: 中ボス（Wujin ＋ コスト3護衛）。コスト5解放。
        waveDefinitions.Add(RecruitMidBoss(2, 10, 2, "neutral_rok_steelblue", "neutral_beastmaster", "neutral_rawr"));  // 停足の番人オルム(鋼青)

        // === Stage 3: コスト3-4 中盤。3-5/3-10 が中ボス。 ===
        // 3-1
        waveDefinitions.Add(StagedCombat(3, 1,
            new WaveEnemyPlacement("Shadowlord", 2, 6, 4),
            new WaveEnemyPlacement("Paragon", 2, 6, 6),
            new WaveEnemyPlacement("Decepticleprime", 2, 10, 5)));
        // 3-2
        waveDefinitions.Add(StagedCombat(3, 2,
            new WaveEnemyPlacement("Wraith", 1, 7, 5),
            new WaveEnemyPlacement("Kane", 2, 6, 4),
            new WaveEnemyPlacement("Malyk", 2, 6, 6)));
        // 3-3: イベント（ゴールドオーグメント）
        waveDefinitions.Add(StagedEvent(3, 3, WaveEventType.AugmentGold));
        // 3-4
        waveDefinitions.Add(StagedCombat(3, 4,
            new WaveEnemyPlacement("Solfist", 1, 7, 4),
            new WaveEnemyPlacement("Ilenamk2", 2, 6, 6),
            new WaveEnemyPlacement("Tier2general", 2, 7, 5)));
        // 3-5: 中ボス（Wujin ＋ Wraith）。
        waveDefinitions.Add(RecruitMidBoss(3, 5, 2, "neutral_rok_gold", "neutral_gnasher", "neutral_rok"));          // 第二門の番牙ロク(金)
        // 3-6
        waveDefinitions.Add(StagedCombat(3, 6,
            new WaveEnemyPlacement("Maehvmk", 1, 10, 5),
            new WaveEnemyPlacement("Shadowlord", 2, 6, 4),
            new WaveEnemyPlacement("Paragon", 2, 6, 6)));
        // 3-7: イベント（アイテム）
        waveDefinitions.Add(StagedChestRoom(3, 7));
        // 3-8
        waveDefinitions.Add(StagedCombat(3, 8,
            new WaveEnemyPlacement("Snowchasermk", 1, 7, 4),
            new WaveEnemyPlacement("Solfist", 1, 7, 6),
            new WaveEnemyPlacement("Decepticleprime", 2, 10, 5)));
        // 3-9
        waveDefinitions.Add(StagedCombat(3, 9,
            new WaveEnemyPlacement("Wraith", 2, 6, 4),
            new WaveEnemyPlacement("Wujin", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 3-10: 中ボス（Kron＋コスト4護衛）。
        waveDefinitions.Add(RecruitMidBoss(3, 10, 2, "neutral_rok_mossgreen", "neutral_rok", "neutral_gnasher"));    // 門石のガロ(苔緑)

        // === Stage 4: コスト4-5 終盤。4-7/4-8/4-9 が中ボス（コスト5）、4-10 が章ボス Skyfalltyrant。 ===
        // 4-1
        waveDefinitions.Add(StagedCombat(4, 1,
            new WaveEnemyPlacement("Wraith", 2, 6, 4),
            new WaveEnemyPlacement("Wujin", 2, 6, 6),
            new WaveEnemyPlacement("Maehvmk", 1, 10, 5)));
        // 4-2: コスト5 Gol 初登場（★1）
        waveDefinitions.Add(StagedCombat(4, 2,
            new WaveEnemyPlacement("Gol", 1, 7, 5),
            new WaveEnemyPlacement("Wraith", 1, 6, 4),
            new WaveEnemyPlacement("Solfist", 1, 6, 6)));
        // 4-3: イベント（プリズムオーグメント）
        waveDefinitions.Add(StagedEvent(4, 3, WaveEventType.AugmentPrism));
        // 4-4
        waveDefinitions.Add(StagedCombat(4, 4,
            new WaveEnemyPlacement("Wujin", 2, 6, 4),
            new WaveEnemyPlacement("Wraith", 2, 6, 6),
            new WaveEnemyPlacement("Snowchasermk", 1, 7, 5)));
        // 4-5
        waveDefinitions.Add(StagedCombat(4, 5,
            new WaveEnemyPlacement("Kron", 1, 6, 4),
            new WaveEnemyPlacement("Maehvmk", 1, 10, 6),
            new WaveEnemyPlacement("Solfist", 1, 7, 5)));
        // 4-6: イベント（ゴールド）
        waveDefinitions.Add(StagedEvent(4, 6, WaveEventType.BonusGold));
        // 4-7/4-8: 進路選択（midboss-nodes）。3提示→逐次2選択→残り1破棄。
        AddNodeSegment(4,
            MakeNode(MidNodeArchetype.Elite,    3, MidBossRewardKind.ItemChoice3, 1, 0,   "neutral_gnasher", "neutral_zukong", "neutral_beastmaster"),     // 眠り坂の門荒らしゼド
            MakeNode(MidNodeArchetype.Standard, 2, MidBossRewardKind.Recruit,     1, 0,   "neutral_beastmaster", "neutral_zukong", "neutral_gnasher"),     // 閉門の獣バルド
            MakeNode(MidNodeArchetype.Supply,   1, MidBossRewardKind.CoinReward,  1, 110, "neutral_zukong", "neutral_gnasher", "neutral_beastmaster"));    // 石鎖の監視者ミル
        // 4-10: チャプターボス。20章化: 章2ボス＝中立 neutral_rook（撃破で恒久解放）。大護衛つき。
        waveDefinitions.Add(StagedBoss(4, 10,
            new WaveEnemyPlacement("neutral_rook", 2, 8, 5),
            new WaveEnemyPlacement("Skyfalltyrant", 1, 9, 3),
            new WaveEnemyPlacement("Invader", 1, 9, 7),
            new WaveEnemyPlacement("Kron", 2, 7, 4),
            new WaveEnemyPlacement("Gol", 2, 7, 6)));
    }

    // チャプター3（全33ラウンド）。ch2 より一段難しく、中ボス戦で「転がる巨大物」ギミックが出る（ch3+）。
    // 章ボス報酬は Dissonance（cost4、4-10で★3出現→撃破で恒久解放）。
    private void BuildChapter3Rounds()
    {
        // === Stage 1: 立ち上がりは全章共通でやさしく（1体start＋確定負け回避）。数はR3で増やす。 ===
        // 1-1: 敵1体。
        waveDefinitions.Add(StagedCombat(1, 1,
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 7, 5, dropCoins: 3)));
        // 1-2: 敵2体。
        waveDefinitions.Add(StagedCombat(1, 2,
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 6, 4, dropItem: true),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Ranged, 1, 10, 5, dropCoins: 2)));
        // 1-3: コスト1中心の3体（やさしめ。1体だけコスト2遠）。
        waveDefinitions.Add(StagedCombat(1, 3,
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 6, 4, dropItem: true),
            new WaveEnemyPlacement(WaveEnemyKind.Cost1Melee, 1, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 1, 10, 5, dropCoins: 2)));

        // === Stage 2: cost2-3。2-5/2-10 が中ボス（cost3候補→1体解放）。 ===
        waveDefinitions.Add(StagedCombat(2, 1,
            new WaveEnemyPlacement("Shadowlord", 2, 6, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Melee, 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 5)));
        waveDefinitions.Add(StagedCombat(2, 2,
            new WaveEnemyPlacement("Kane", 2, 7, 4),
            new WaveEnemyPlacement("Malyk", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 2, 9, 5)));
        waveDefinitions.Add(StagedEvent(2, 3, WaveEventType.AugmentSilver));
        waveDefinitions.Add(StagedCombat(2, 4,
            new WaveEnemyPlacement("Paragon", 2, 6, 4),
            new WaveEnemyPlacement("Shadowlord", 2, 6, 6),
            new WaveEnemyPlacement("Decepticleprime", 2, 10, 5)));
        // 2-5: 中ボス（cost3候補3体）。
        waveDefinitions.Add(RecruitMidBoss(2, 5, 2, "neutral_zukong", "neutral_rawr", "neutral_beastmaster"));
        waveDefinitions.Add(StagedCombat(2, 6,
            new WaveEnemyPlacement("Ilenamk2", 2, 6, 4),
            new WaveEnemyPlacement("Kane", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        waveDefinitions.Add(StagedCombat(2, 7,
            new WaveEnemyPlacement("Tier2general", 2, 7, 4),
            new WaveEnemyPlacement("Shadowlord", 2, 7, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 4),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 6)));
        waveDefinitions.Add(StagedChestRoom(2, 8));
        waveDefinitions.Add(StagedCombat(2, 9,
            new WaveEnemyPlacement("Wraith", 2, 6, 4),
            new WaveEnemyPlacement("Wujin", 2, 6, 6),
            new WaveEnemyPlacement("Decepticleprime", 2, 10, 5)));
        // 2-10: 中ボス（cost3候補3体）。
        waveDefinitions.Add(RecruitMidBoss(2, 10, 3, "neutral_beastmaster", "neutral_zukong", "neutral_rok"));

        // === Stage 3: cost3-4。3-5/3-10 が中ボス（cost4候補）。 ===
        waveDefinitions.Add(StagedCombat(3, 1,
            new WaveEnemyPlacement("Paragon", 2, 6, 4),
            new WaveEnemyPlacement("Malyk", 2, 6, 6),
            new WaveEnemyPlacement("Maehvmk", 1, 10, 5)));
        waveDefinitions.Add(StagedCombat(3, 2,
            new WaveEnemyPlacement("Wraith", 2, 7, 5),
            new WaveEnemyPlacement("Kane", 2, 6, 4),
            new WaveEnemyPlacement("Ilenamk2", 2, 6, 6)));
        waveDefinitions.Add(StagedEvent(3, 3, WaveEventType.AugmentGold));
        waveDefinitions.Add(StagedCombat(3, 4,
            new WaveEnemyPlacement("Solfist", 2, 7, 4),
            new WaveEnemyPlacement("Snowchasermk", 2, 6, 6),
            new WaveEnemyPlacement("Tier2general", 2, 7, 5)));
        // 3-5: 中ボス（cost4候補3体）。
        waveDefinitions.Add(RecruitMidBoss(3, 5, 2, "neutral_zukong", "neutral_gnasher", "neutral_rok"));
        waveDefinitions.Add(StagedCombat(3, 6,
            new WaveEnemyPlacement("Maehvmk", 2, 10, 5),
            new WaveEnemyPlacement("Wujin", 2, 6, 4),
            new WaveEnemyPlacement("Paragon", 2, 6, 6)));
        waveDefinitions.Add(StagedChestRoom(3, 7));
        waveDefinitions.Add(StagedCombat(3, 8,
            new WaveEnemyPlacement("Snowchasermk", 2, 7, 4),
            new WaveEnemyPlacement("Solfist", 2, 7, 6),
            new WaveEnemyPlacement("Decepticleprime", 3, 10, 5)));
        waveDefinitions.Add(StagedCombat(3, 9,
            new WaveEnemyPlacement("Wraith", 2, 6, 4),
            new WaveEnemyPlacement("Wujin", 2, 6, 6),
            new WaveEnemyPlacement(WaveEnemyKind.Cost2Ranged, 3, 9, 5)));
        // 3-10: 中ボス（cost4候補3体）。
        waveDefinitions.Add(RecruitMidBoss(3, 10, 2, "neutral_beastmaster", "neutral_rok", "neutral_gnasher"));

        // === Stage 4: cost4-5 終盤。4-7/4-8/4-9 中ボス（cost4候補）、4-10 章ボス Dissonance★3。 ===
        waveDefinitions.Add(StagedCombat(4, 1,
            new WaveEnemyPlacement("Wraith", 2, 6, 4),
            new WaveEnemyPlacement("Wujin", 2, 6, 6),
            new WaveEnemyPlacement("Maehvmk", 2, 10, 5)));
        // 4-2: コスト5 Gol（★1）。
        waveDefinitions.Add(StagedCombat(4, 2,
            new WaveEnemyPlacement("Gol", 1, 7, 5),
            new WaveEnemyPlacement("Kron", 1, 6, 4),
            new WaveEnemyPlacement("Solfist", 2, 6, 6)));
        waveDefinitions.Add(StagedEvent(4, 3, WaveEventType.AugmentPrism));
        waveDefinitions.Add(StagedCombat(4, 4,
            new WaveEnemyPlacement("Invader", 1, 7, 5),
            new WaveEnemyPlacement("Wujin", 2, 6, 4),
            new WaveEnemyPlacement("Wraith", 2, 6, 6)));
        waveDefinitions.Add(StagedCombat(4, 5,
            new WaveEnemyPlacement("Kron", 2, 6, 4),
            new WaveEnemyPlacement("Maehvmk", 2, 10, 6),
            new WaveEnemyPlacement("Snowchasermk", 2, 7, 5)));
        waveDefinitions.Add(StagedEvent(4, 6, WaveEventType.BonusGold));
        // 4-7/4-8: 進路選択（midboss-nodes）。3提示→逐次2選択→残り1破棄。
        AddNodeSegment(4,
            MakeNode(MidNodeArchetype.Elite,    3, MidBossRewardKind.ItemChoice3, 1, 0,   "neutral_gnasher", "neutral_zukong", "neutral_rawr"),
            MakeNode(MidNodeArchetype.Standard, 2, MidBossRewardKind.Recruit,     1, 0,   "neutral_rawr", "neutral_beastmaster", "neutral_rok"),
            MakeNode(MidNodeArchetype.Supply,   1, MidBossRewardKind.CoinReward,  1, 125, "neutral_rok", "neutral_gnasher", "neutral_zukong"));
        // 4-10: チャプターボス。20章化: 章3ボス＝中立 neutral_sister（撃破で恒久解放）。cost5大護衛つき。
        waveDefinitions.Add(StagedBoss(4, 10,
            new WaveEnemyPlacement("neutral_sister", 2, 8, 5),
            new WaveEnemyPlacement("Gol", 1, 9, 3),
            new WaveEnemyPlacement("Invader", 1, 9, 7),
            new WaveEnemyPlacement("Kron", 2, 7, 4),
            new WaveEnemyPlacement("Skyfalltyrant", 1, 7, 6)));
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

            // R2-coremode: コアは戦闘後の復元・全回復対象から除外（HPをウェーブ持ち越し）。
            if (entity.IsCore)
                continue;

            roundPlayerUnits.Add(entity);
            roundStartNodeByPlayerUnit[entity] = entity.CurrentNode;
        }
    }

    // 敵スポーン先が埋まっている時の代替マス探索。同じ敵列の別行 → 敵側の空きデプロイマス の順。
    // 見つからなければ null（盤面が敵で埋まり切っている極端なケースのみ）。
    private Node FindAlternateEnemySpawnNode(int column, int row)
    {
        if (GridManager.Instance == null) return null;
        // 1) 同列(敵列)の別の行で空きを探す（本来の位置に最も近い）。
        for (int r = 1; r <= 12; r++)
        {
            if (r == row) continue;
            Node n = GridManager.Instance.GetNodeAtBoardCoordinate(column, r);
            if (n != null && !n.IsOccupied) return n;
        }
        // 2) 最後の砦：敵側(Team2)の空きデプロイマス。
        return GridManager.Instance.GetFreeNode(Team.Team2);
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
        // 指定マスが無い/既に占有（味方が敵ゾーンに残る等）でも敵を必ず出すため、空きマスへフォールバック。
        // これを入れないと「敵0体→ウェーブ開始不可」でオートプレイ等がハングする。
        if (spawnNode == null || spawnNode.IsOccupied)
        {
            Node alt = FindAlternateEnemySpawnNode(placement.Column, placement.Row);
            if (alt == null)
            {
                Debug.LogWarning($"Wave enemy spawn node unavailable and no free fallback. Column:{placement.Column} Row:{placement.Row}");
                return;
            }
            Debug.Log($"Spawn node ({placement.Column},{placement.Row}) unavailable; using fallback free node.");
            spawnNode = alt;
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

        // STORY v2: 中ボスの「顔役」素体には、敵個体限定の強化（サイズ2倍/射程2倍/ステータス2倍）＋章ごとの色ティント。
        // 入場フェードは基準色/スケールを捕捉して同値でフェードインするため、PlayEntranceAnimation の前に設定する。
        if (!IsCoreMode && currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count
            && waveDefinitions[currentWaveIndex] != null && waveDefinitions[currentWaveIndex].IsMidBossWave
            && entityData.name == GetWavePrimaryBossId(waveDefinitions[currentWaveIndex]))
        {
            // 敵としての中ボス個体のみ：サイズ・射程・ステータスに倍率（仲間化後のアライには乗らない）。
            newEntity.ApplyEnemyBossBuff(2f, 2f, 2f);
            // 色違い（同素体）は専用リカラースプライトで実装済みのため、差別化用の色ティント(カラーフィルター)は廃止。
            // 盤面スプライトはスプライト本来の色をそのまま表示する（純色）。
        }
        // ⑦ 最終ボス（章ボス）の顔役素体は、通常の3倍サイズで登場させる（中ボスの2倍より明確に大きく見せる）。
        //    サイズのみ拡大し、射程・ステータスは据え置き（難易度バランスを変えない）。
        else if (!IsCoreMode && currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count
            && waveDefinitions[currentWaveIndex] != null && waveDefinitions[currentWaveIndex].IsBossWave
            && entityData.name == GetWavePrimaryBossId(waveDefinitions[currentWaveIndex]))
        {
            newEntity.ApplyEnemyBossBuff(3f, 1f, 1f);
        }

        // R3: 敵は自陣奥（+x方向）から1マスぶんフェードイン＋歩いて登場する。
        // ⑧ 登場シネマでボスは専用入場にするため、直前にフラグが立っていれば通常入場を1回だけ抑止。
        if (suppressNextEnemyEntrance)
            suppressNextEnemyEntrance = false;
        else
            newEntity.PlayEntranceAnimation(new Vector3(1.0f, 0f, 0f), 0.6f);

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
        ItemInstance granted = ReturnItemToBench(item);
        PlayItemAcquiredFly(granted); // ⑤ 入手をはっきり伝える：インベントリ枠へ飛び込む＋発光。
        AttackEffectPlayer.PlayUiSfx("item_equip");
    }

    // ⑤ アイテム入手演出：生成されたアイテムを、最終スロット位置の少し上から飛び込ませ、
    //    着地時にスケールを弾ませ、枠を一瞬発光させて「手に入った」ことを明確にする。
    public void PlayItemAcquiredFly(ItemInstance item)
    {
        if (item == null)
            return;
        Transform tr = item.transform;
        Vector3 dest = tr.position;
        Vector3 finalScale = tr.localScale;

        // 上方から飛び込む。
        tr.position = dest + new Vector3(0f, 2.2f, 0f);
        tr.DOKill();
        Sequence seq = DOTween.Sequence().SetTarget(tr).SetUpdate(true);
        seq.Append(tr.DOMove(dest, 0.42f).SetEase(Ease.OutCubic));
        seq.Join(tr.DOScale(finalScale, 0.42f).From(finalScale * 1.7f).SetEase(Ease.OutBack));

        // 着地時に枠（スプライト）を一瞬白く光らせる。
        SpriteRenderer sr = item.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Color baseColor = sr.color;
            seq.AppendCallback(() => { if (sr != null) sr.color = Color.white; });
            seq.Append(sr.DOColor(baseColor, 0.32f).SetEase(Ease.OutQuad).SetUpdate(true));
        }
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

    // Legionのスキル専用召喚体やニュートラル雑魚、ヒーロー専用枠など、
    // ショップ・通常ウェーブ・ランダム召喚・開始抽選の候補から外すユニットです。
    private bool IsLegionOnlySummonData(EntitiesDatabaseSO.EntityData data)
    {
        if (string.IsNullOrEmpty(data.name))
            return false;
        // 主人公（ヒーロー）専用枠も全候補から除外（ショップ/ウェーブ/召喚/開始抽選）。
        // 採用中のボスヒーローも、ショップで重複購入させない（無料付与のため）。
        // "Hero"接頭辞の専用ヒーローユニット（基本3＋追加6）は常に抽選外。ボス将(Magmarvaath等)は通常ユニットのまま。
        if (IsHeroUnitId(data.name) || IsActiveHeroUnit(data.name)
            || (!string.IsNullOrEmpty(data.name) && data.name.StartsWith("Hero", StringComparison.OrdinalIgnoreCase)))
            return true;
        // 将系（ヒーロー形態/ボス将アート流用）は温存。ランダム抽選（開始/召喚/ウェーブ候補）にも乗せない。
        if (IsReservedHeroFormUnit(data.name))
            return true;
        return string.Equals(data.name, "Taskmaster", StringComparison.OrdinalIgnoreCase)
            || string.Equals(data.name, "Zyx", StringComparison.OrdinalIgnoreCase);
    }

    // 主人公（ヒーロー）専用ユニットのID判定（DESIGN_R3-hero-units）。毎ラン初手に確定付与される専用枠で、
    // ショップ/敵ウェーブ/召喚/ボス報酬/仲間化/開始抽選/売却のどれにも乗せない。
    public static bool IsHeroUnitId(string unitId)
    {
        if (string.IsNullOrEmpty(unitId))
            return false;
        return string.Equals(unitId, "HeroAldin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(unitId, "HeroKagachi", StringComparison.OrdinalIgnoreCase)
            || string.Equals(unitId, "HeroVesna", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHeroUnitData(EntitiesDatabaseSO.EntityData data)
    {
        return IsHeroUnitId(data.name);
    }

    // R4-collection-hub: ショップ選抜の母集合判定（静的・GameManager非依存）。
    // 「ショップに出うる通常ユニット」だけを true にする。コレクションハブのトグル対象 = これが true のユニット。
    // 進行解放ゲートや★3所持・採用中ヒーローはラン依存なので、ここでは見ない（IsEntityUnlockedForShop が重ねて判定）。
    public static bool IsShopRosterCandidate(EntitiesDatabaseSO.EntityData data)
    {
        if (string.IsNullOrEmpty(data.name) || data.prefab == null) return false;
        if (data.cost < 1 || data.cost > 5) return false;
        if (IsHeroUnitId(data.name)) return false;
        if (data.name.StartsWith("Hero", StringComparison.OrdinalIgnoreCase)) return false;
        if (IsReservedHeroFormUnit(data.name)) return false;
        if (string.Equals(data.name, "Taskmaster", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(data.name, "Zyx", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    // R4-collection-hub: 編成ハブで「表示・管理（ON/OFF）できる」ユニットか。
    // 恒久的に自分のショップ母集合になりうるものだけを true にする：
    //  ・cost1-2（常時開放のベース）／cost3スターター（常時）／恒久仲間化したボス（HasBossAlly）。
    // 「チャプター内でだけ仲間化される一時解放ユニット（cost3-5の中ボス勧誘候補など）」は false（ハブに出さない）。
    public static bool IsHubManageableUnit(EntitiesDatabaseSO.EntityData data)
    {
        if (!IsShopRosterCandidate(data)) return false;
        if (data.cost <= 2) return true;
        if (Cost3StarterPlayable.Contains(data.name)) return true;
        if (SaveManager.Instance != null && SaveManager.Instance.HasBossAlly(data.name)) return true;
        return false;
    }

    // R3-hero-scale: ボスもヒーローとして使える。現在アクティブな主人公IDかどうか（ボス採用時も hero 扱いにする）。
    public bool IsActiveHeroUnit(string unitId)
    {
        return !string.IsNullOrEmpty(unitId) && !string.IsNullOrEmpty(heroUnitId)
            && string.Equals(unitId, heroUnitId, StringComparison.OrdinalIgnoreCase);
    }

    // 「ヒーロー扱い」判定：基本3ヒーロー、または現在アクティブな主人公（ボス採用ぶん）。
    // 盤面枠フリー・育成スケール・自動復活・盤面検出に使う。
    public bool IsHeroUnit(string unitId)
    {
        return IsHeroUnitId(unitId) || IsActiveHeroUnit(unitId);
    }

    // R3-factions: 現在の主人公（ヒーロー）が指定シナジー（＝陣営）を持つか。
    // 陣営シナジーの「1体目は主人公が同陣営の時だけ発動」「主人公同陣営で効果増幅」の判定に使う。
    public bool ActiveHeroHasSynergy(SynergyType type)
    {
        if (type == SynergyType.None || entitiesDatabase == null || entitiesDatabase.allEntities == null
            || string.IsNullOrEmpty(heroUnitId))
            return false;
        for (int i = 0; i < entitiesDatabase.allEntities.Count; i++)
        {
            if (!string.Equals(entitiesDatabase.allEntities[i].name, heroUnitId, StringComparison.OrdinalIgnoreCase))
                continue;
            List<SynergyType> syns = SynergyManager.GetSynergiesForEntityData(entitiesDatabase.allEntities[i]);
            for (int s = 0; s < syns.Count; s++)
                if (syns[s] == type) return true;
            return false;
        }
        return false;
    }

    // 主人公として選べるキャラの厳選ロスター（順番＝育成画面の並び）。
    // 章ボスが全員ヒーローになるわけではない。ヒーローにしたいユニットだけここに足す。
    // 解放章: 0=最初から / Nなら「第N章クリアで解放」。将来ボスをヒーロー化する時は { "Xxx", N } を追加。
    public static readonly string[] HeroRosterIds =
    {
        "HeroAldin", "HeroKagachi", "HeroVesna",
        // 追加ヒーロー：各陣営(Lyonar/Songhai/Vanar)の alt/3rd 将。進行度で解放。
        "HeroZiran", "HeroReva", "HeroKara",
        "HeroBrome", "HeroShidai", "HeroIlena",
        // General系（陣営の将）。立ち絵(general_fN)があるためヒーロー化。各陣営章のクリアで解放。
        "Magmarvaath", "Magmarstarhorn", "Magmarragnora",
        "Abyssallilithe", "Abyssalcassyva", "Abyssalmaehv",
        "Vetruvianzirix", "Vetruviansajj", "Vetruvianscion",
    };
    private static readonly Dictionary<string, int> HeroUnlockChapters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        // 追加ヒーロー6体の解放章（進行度。暫定。後で調整可）。
        { "HeroZiran", 2 }, { "HeroReva", 3 }, { "HeroKara", 4 },
        { "HeroBrome", 5 }, { "HeroShidai", 6 }, { "HeroIlena", 7 },
        // General系ヒーローの解放章（その将を撃破＝該当章クリアで解放）。
        { "Magmarvaath", 4 }, { "Magmarstarhorn", 5 }, { "Magmarragnora", 6 },
        { "Abyssallilithe", 7 }, { "Abyssalcassyva", 8 }, { "Abyssalmaehv", 9 },
        { "Vetruvianzirix", 10 }, { "Vetruviansajj", 11 }, { "Vetruvianscion", 12 },
    };

    public static int GetHeroUnlockChapter(string unitId)
    {
        return (!string.IsNullOrEmpty(unitId) && HeroUnlockChapters.TryGetValue(unitId, out int c)) ? c : 0;
    }

    public static bool IsHeroRosterId(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return false;
        for (int i = 0; i < HeroRosterIds.Length; i++)
            if (string.Equals(HeroRosterIds[i], unitId, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // 主人公として選べる候補か（解放済みか）。ロスター外は不可。解放章0は常時、Nは該当章クリアで解放。
    public static bool IsHeroCandidateUnlocked(string unitId)
    {
        if (!IsHeroRosterId(unitId)) return false;
        int ch = GetHeroUnlockChapter(unitId);
        if (ch <= 0) return true;
        return SaveManager.Instance != null && SaveManager.Instance.IsChapterUnlocked(ch + 1);
    }

    // 将来のヒーロー／ボス将として温存する「将系」ユニット（DESIGN_R3-hero-units 追補）。
    // ヒーロー形態変化・ボス将のアートを流用しており、今はプレイヤーが入手（ショップ/開始抽選/召喚/
    // 勧誘/章ボス報酬）できないように外す。敵ウェーブ上の登場（伏線）としての使用のみ維持する。
    private static readonly HashSet<string> ReservedGeneralUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Skindogehai",     // f2_general_skindogehai（カガチ系）
        "Tier2general",    // f6_tier2general（ヴェスナ系）
        "Altgeneraltier2", // f6_altgeneraltier2
        "Embergeneral",    // f3_tier2general
        "Plaguegeneral",   // f5_tier2general
        "Snowchasermk",    // f6_snowchasermk2（将mk2）
        "Maehvmk",         // f4_maehvmk2（将mk2）
        "Ilenamk2",
        "neutral_z0r", "neutral_nip", "neutral_goldenmantella", "neutral_ion", "neutral_grincher", "neutral_aer", "neutral_soboro",        // f6_ilenamk2（将mk2）
    };

    public static bool IsReservedHeroFormUnit(string unitId)
    {
        if (string.IsNullOrEmpty(unitId))
            return false;
        return ReservedGeneralUnitIds.Contains(unitId);
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

            // R2-coremode: 敵コアは常設拠点なので片付けない。
            if (enemy.IsCore)
                continue;

            if (enemy.CurrentNode != null)
                enemy.CurrentNode.SetOccupied(false);

            Destroy(enemy.gameObject);
            team2Entities.RemoveAt(i);
        }

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
                GrantRandomItemFromSynergy();
                Debug.Log("Event round: granted 3 random items.");
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

    // ② 配置フォーメーションの種類。
    private enum FormationKind { Charge, Phalanx, Square, Wedge }
    private class FormationHit { public FormationKind Kind; public List<Node> Nodes; }

    // 盤上プレイヤー配置から成立しているフォーメーションを検出する（マス座標は GridManager のクラスタ列/行を使う）。
    // 横3=突撃 / 縦3=鉄壁 / 2x2=方陣 / 斜め3=楔。
    private List<FormationHit> DetectFormations(out Dictionary<Node, BaseEntity> nodeToEntity)
    {
        nodeToEntity = new Dictionary<Node, BaseEntity>();
        List<FormationHit> hits = new List<FormationHit>();
        GridManager gm = GridManager.Instance;
        if (gm == null) return hits;

        Dictionary<(int, int), Node> cellNode = new Dictionary<(int, int), Node>();
        HashSet<(int, int)> occ = new HashSet<(int, int)>();
        Dictionary<int, List<Node>> byRow = new Dictionary<int, List<Node>>();
        Dictionary<int, List<Node>> byCol = new Dictionary<int, List<Node>>();

        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsDead || !e.IsOnBoard || e.IsSummonedUnit || e.IsCore || e.CurrentNode == null) continue;
            // 自陣（配置エリア）のユニットのみ。戦闘で敵陣へ進んだ味方位置で陣形マーカーが出るのを防ぐ。
            if (!gm.IsDeploymentNode(Team.Team1, e.CurrentNode)) continue;
            int c = gm.GetBoardColumn(e.CurrentNode);
            int r = gm.GetBoardRow(e.CurrentNode);
            if (c < 0 || r < 0) continue;
            (int, int) key = (c, r);
            cellNode[key] = e.CurrentNode;
            occ.Add(key);
            nodeToEntity[e.CurrentNode] = e;
            if (!byRow.TryGetValue(r, out List<Node> rl)) { rl = new List<Node>(); byRow[r] = rl; }
            rl.Add(e.CurrentNode);
            if (!byCol.TryGetValue(c, out List<Node> cl)) { cl = new List<Node>(); byCol[c] = cl; }
            cl.Add(e.CurrentNode);
        }

        foreach (KeyValuePair<int, List<Node>> kv in byRow)
            if (kv.Value.Count >= 3) hits.Add(new FormationHit { Kind = FormationKind.Charge, Nodes = kv.Value });
        foreach (KeyValuePair<int, List<Node>> kv in byCol)
            if (kv.Value.Count >= 3) hits.Add(new FormationHit { Kind = FormationKind.Phalanx, Nodes = kv.Value });

        foreach ((int, int) key in occ)
        {
            int c = key.Item1, r = key.Item2;
            // 方陣（2x2）
            if (occ.Contains((c + 1, r)) && occ.Contains((c, r + 1)) && occ.Contains((c + 1, r + 1)))
                hits.Add(new FormationHit { Kind = FormationKind.Square, Nodes = new List<Node> { cellNode[(c, r)], cellNode[(c + 1, r)], cellNode[(c, r + 1)], cellNode[(c + 1, r + 1)] } });
            // 楔（斜め3・右上がり/右下がり）
            if (occ.Contains((c + 1, r + 1)) && occ.Contains((c + 2, r + 2)))
                hits.Add(new FormationHit { Kind = FormationKind.Wedge, Nodes = new List<Node> { cellNode[(c, r)], cellNode[(c + 1, r + 1)], cellNode[(c + 2, r + 2)] } });
            if (occ.Contains((c + 1, r - 1)) && occ.Contains((c + 2, r - 2)))
                hits.Add(new FormationHit { Kind = FormationKind.Wedge, Nodes = new List<Node> { cellNode[(c, r)], cellNode[(c + 1, r - 1)], cellNode[(c + 2, r - 2)] } });
        }
        return hits;
    }

    private Color FormationColor(FormationKind k)
    {
        switch (k)
        {
            case FormationKind.Charge: return new Color(1f, 0.55f, 0.18f, 0.5f);   // 橙
            case FormationKind.Phalanx: return new Color(0.3f, 0.62f, 1f, 0.5f);   // 青
            case FormationKind.Square: return new Color(0.3f, 0.85f, 0.55f, 0.5f); // 緑
            default: return new Color(0.9f, 0.4f, 0.85f, 0.55f);                   // 楔=マゼンタ
        }
    }

    private string FormationName(FormationKind k, bool ja)
    {
        switch (k)
        {
            case FormationKind.Charge: return ja ? "突撃（攻撃力↑・攻撃速度↑）" : "Charge (ATK & speed up)";
            case FormationKind.Phalanx: return ja ? "鉄壁（被ダメ↓・シールド）" : "Phalanx (DR & shield)";
            case FormationKind.Square: return ja ? "方陣（与ダメ↑・防御↑・シールド）" : "Square (offense & defense)";
            default: return ja ? "楔（与ダメ大↑・攻撃速度↑）" : "Wedge (high ATK & speed)";
        }
    }

    // ② 配置フォーメーション効果。戦闘開始時に成立フォーメーションへ時限バフを付与し発動エフェクトを出す。
    private void ApplyFormationBonuses()
    {
        const float dur = 60f;
        List<FormationHit> hits = DetectFormations(out Dictionary<Node, BaseEntity> nodeToEntity);
        HashSet<FormationKind> applied = new HashSet<FormationKind>();

        foreach (FormationHit hit in hits)
        {
            applied.Add(hit.Kind);
            foreach (Node n in hit.Nodes)
            {
                if (!nodeToEntity.TryGetValue(n, out BaseEntity e) || e == null) continue;
                switch (hit.Kind)
                {
                    case FormationKind.Charge:
                        e.ApplyAttackSpeedBoostFromSynergy(1.18f, dur);
                        e.ApplyTimedSynergyDamageDealtBonus(0.15f, dur);
                        break;
                    case FormationKind.Phalanx:
                        e.ApplyTimedSynergyDamageReductionBonus(0.12f, dur);
                        e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.12f)), dur);
                        break;
                    case FormationKind.Square:
                        e.ApplyTimedSynergyDamageDealtBonus(0.08f, dur);
                        e.ApplyTimedSynergyDamageReductionBonus(0.08f, dur);
                        e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.08f)), dur);
                        break;
                    case FormationKind.Wedge:
                        e.ApplyTimedSynergyDamageDealtBonus(0.25f, dur);
                        e.ApplyAttackSpeedBoostFromSynergy(1.10f, dur);
                        break;
                }
                AttackEffectPlayer.PlayAreaIndicator(e, e.transform.position, 0.55f, 0.9f, 1.1f);
            }
        }

        foreach (FormationKind k in applied)
            Debug.Log($"[Formation] {FormationName(k, true)} 発動");

        // R3-hero-formation: 主人公専用フォーメーション（6マス）が成立していれば強力な専用バフ。
        List<Node> heroNodes = DetectHeroFormation(out Dictionary<Node, BaseEntity> heroMap, out HeroFormDef hdef);
        if (heroNodes != null)
        {
            foreach (Node n in heroNodes)
            {
                if (!heroMap.TryGetValue(n, out BaseEntity e) || e == null) continue;
                ApplyHeroFormationEffect(e, hdef.effect);
                AttackEffectPlayer.PlayAreaIndicator(e, e.transform.position, 0.6f, 0.95f, 1.2f);
            }
            Debug.Log($"[HeroFormation] {hdef.ja} 発動");
        }

        ClearFormationMarkers();
        lastFormationSignature = string.Empty;
    }

    // ====== ② 配置フォーメーションのライブ表示（編成中） ======

    private void UpdateFormationPreview()
    {
        // 戦闘中・勝利演出中・選択中はガイドもマーカーも隠す。
        // （勝利演出中は IsRoundInProgress=false だが配置復元前なので、味方が敵陣に残った位置でマーカーが出るのを防ぐ）
        if (IsRoundInProgress || waveClearCelebrating || bossRewardSelectionPending || augmentSelectionPending)
        {
            if (formationMarkers.Count > 0) ClearFormationMarkers();
            lastFormationSignature = string.Empty;
            FormationHintUI.EnsureExists().SetBuildPhase(false);
            return;
        }

        Dictionary<Node, Color> cells = new Dictionary<Node, Color>();
        List<FormationKind> kinds = ComputeFormationCells(cells);

        // R3-hero-formation: 主人公専用フォーメーションのライブ表示（金色・最優先で上書き）。
        List<Node> heroCellsNow = DetectHeroFormation(out _, out HeroFormDef heroDefNow);
        if (heroCellsNow != null)
        {
            Color gold = new Color(1f, 0.84f, 0.3f, 0.62f);
            foreach (Node n in heroCellsNow) cells[n] = gold;
        }

        string sig = BuildFormationSignature(cells);
        if (sig != lastFormationSignature)
        {
            lastFormationSignature = sig;
            RebuildFormationMarkers(cells);
        }

        // 編成中はガイドパネルを表示（左上・ウェーブ進捗の左。詳細パネルとは被らない）。
        FormationHintUI ui = FormationHintUI.EnsureExists();
        ui.SetBuildPhase(true);
        ui.SetActiveFormations(
            kinds.Contains(FormationKind.Charge),
            kinds.Contains(FormationKind.Phalanx),
            kinds.Contains(FormationKind.Square),
            kinds.Contains(FormationKind.Wedge));

        // 主人公専用フォーメーションのガイド行を更新（成立時はハイライト）。
        bool jaNow = LocalizationManager.IsJapanese;
        ui.SetHeroFormation(
            HeroFormCells5x3(heroDefNow),
            jaNow ? heroDefNow.ja : heroDefNow.en,
            HeroFormEffectText(heroDefNow.effect, jaNow),
            heroCellsNow != null);
    }

    // 成立フォーメーションのマス色を求め、成立した種類リストを返す（プレビュー表示用）。
    private List<FormationKind> ComputeFormationCells(Dictionary<Node, Color> cellColor)
    {
        List<FormationHit> hits = DetectFormations(out _);
        List<FormationKind> kinds = new List<FormationKind>();
        foreach (FormationHit hit in hits)
        {
            if (!kinds.Contains(hit.Kind)) kinds.Add(hit.Kind);
            Color col = FormationColor(hit.Kind);
            foreach (Node n in hit.Nodes) cellColor[n] = col; // 特殊なフォーメーションほど後に上書き
        }
        return kinds;
    }

    // ====== R3-hero-formation: 主人公ごとの専用フォーメーション（6マス・3x3内の相対形） ======
    private enum HeroFormEffect { Bulwark, Sun, Fangs, Gale, Gate, Hook }
    private struct HeroFormDef { public (int dc, int dr)[] cells; public HeroFormEffect effect; public string ja; public string en; }

    // 現在の主人公に対応する6マス陣形を返す。基本6ヒーローは固有、ボス将ヒーローは陣営で既定形へ。
    private HeroFormDef GetActiveHeroFormation()
    {
        // 配置を離して使えるよう、各形は横方向に1マス空けたスプレッド型（最大5列×3行に収まる）。
        (int, int)[] A = { (0, 0), (2, 0), (0, 1), (2, 1), (0, 2), (2, 2) }; // 双柱（間1）
        (int, int)[] B = { (0, 0), (2, 0), (4, 0), (0, 1), (2, 1), (4, 1) }; // 横スプレッド（3点×2段）
        (int, int)[] C = { (0, 0), (4, 0), (0, 1), (4, 1), (0, 2), (4, 2) }; // 遠柱（両端）
        (int, int)[] E = { (0, 0), (2, 0), (2, 1), (4, 1), (2, 2), (4, 2) }; // 階段スプレッド
        (int, int)[] D = { (0, 0), (2, 0), (4, 0), (0, 2), (2, 2), (4, 2) }; // 格子（上下＋間1）
        (int, int)[] F = { (0, 0), (2, 0), (0, 1), (0, 2), (2, 2), (4, 2) }; // 鉤スプレッド（L）

        string h = (heroUnitId ?? string.Empty).ToLowerInvariant();
        switch (h)
        {
            case "heroaldin":   return new HeroFormDef { cells = A, effect = HeroFormEffect.Bulwark, ja = "聖盾の壁", en = "Aegis Wall" };
            case "heroziran":   return new HeroFormDef { cells = B, effect = HeroFormEffect.Sun, ja = "日輪の陣", en = "Solar Array" };
            case "herokagachi": return new HeroFormDef { cells = C, effect = HeroFormEffect.Fangs, ja = "双牙の構え", en = "Twin Fangs" };
            case "heroreva":    return new HeroFormDef { cells = E, effect = HeroFormEffect.Gale, ja = "疾風の階", en = "Gale Steps" };
            case "herovesna":   return new HeroFormDef { cells = D, effect = HeroFormEffect.Gate, ja = "氷の城門", en = "Frost Gate" };
            case "herokara":    return new HeroFormDef { cells = F, effect = HeroFormEffect.Hook, ja = "凍嶺の鉤", en = "Glacier Hook" };
        }
        // ボス将ヒーロー等：陣営で既定形へフォールバック。
        if (ActiveHeroHasSynergy(SynergyType.Lyonar))    return new HeroFormDef { cells = A, effect = HeroFormEffect.Bulwark, ja = "守護の壁", en = "Guardian Wall" };
        if (ActiveHeroHasSynergy(SynergyType.Vanar))     return new HeroFormDef { cells = D, effect = HeroFormEffect.Gate, ja = "氷の城門", en = "Frost Gate" };
        if (ActiveHeroHasSynergy(SynergyType.Songhai))   return new HeroFormDef { cells = C, effect = HeroFormEffect.Fangs, ja = "双牙の構え", en = "Twin Fangs" };
        if (ActiveHeroHasSynergy(SynergyType.Magmar))    return new HeroFormDef { cells = B, effect = HeroFormEffect.Fangs, ja = "猛攻の陣", en = "Onslaught" };
        if (ActiveHeroHasSynergy(SynergyType.Abyssian))  return new HeroFormDef { cells = E, effect = HeroFormEffect.Gale, ja = "死闘の階", en = "Deathstep" };
        if (ActiveHeroHasSynergy(SynergyType.Vetruvian)) return new HeroFormDef { cells = F, effect = HeroFormEffect.Gate, ja = "機巧の鉤", en = "Artifice Hook" };
        return new HeroFormDef { cells = A, effect = HeroFormEffect.Bulwark, ja = "主人公の陣", en = "Hero Formation" };
    }

    // 主人公専用フォーメーションが自陣に成立していれば、その6マスのNodeを返す（並進不変で全アンカーを試す）。成立しなければ null。
    private List<Node> DetectHeroFormation(out Dictionary<Node, BaseEntity> nodeToEntity, out HeroFormDef def)
    {
        def = GetActiveHeroFormation();
        nodeToEntity = new Dictionary<Node, BaseEntity>();
        GridManager grid = GridManager.Instance;
        if (grid == null || def.cells == null) return null;

        Dictionary<(int, int), Node> cellNode = new Dictionary<(int, int), Node>();
        Dictionary<(int, int), BaseEntity> cellEnt = new Dictionary<(int, int), BaseEntity>();
        HashSet<(int, int)> occ = new HashSet<(int, int)>();
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsDead || !e.IsOnBoard || e.IsSummonedUnit || e.IsCore || e.CurrentNode == null) continue;
            if (!grid.IsDeploymentNode(Team.Team1, e.CurrentNode)) continue;
            int c = grid.GetBoardColumn(e.CurrentNode);
            int r = grid.GetBoardRow(e.CurrentNode);
            if (c < 0 || r < 0) continue;
            (int, int) k = (c, r);
            occ.Add(k); cellNode[k] = e.CurrentNode; cellEnt[k] = e;
        }
        if (occ.Count < def.cells.Length) return null;

        foreach ((int, int) anchor in occ)
        {
            bool all = true;
            for (int i = 0; i < def.cells.Length; i++)
                if (!occ.Contains((anchor.Item1 + def.cells[i].dc, anchor.Item2 + def.cells[i].dr))) { all = false; break; }
            if (!all) continue;

            List<Node> nodes = new List<Node>();
            for (int i = 0; i < def.cells.Length; i++)
            {
                (int, int) key = (anchor.Item1 + def.cells[i].dc, anchor.Item2 + def.cells[i].dr);
                nodes.Add(cellNode[key]);
                nodeToEntity[cellNode[key]] = cellEnt[key];
            }
            return nodes;
        }
        return null;
    }

    // 成立中の主人公フォーメーション効果を1ユニットへ付与（戦闘開始時・全戦闘）。
    private void ApplyHeroFormationEffect(BaseEntity e, HeroFormEffect eff)
    {
        const float dur = 60f;
        switch (eff)
        {
            case HeroFormEffect.Bulwark:
                e.ApplyTimedSynergyDamageReductionBonus(0.18f, dur);
                e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.18f)), dur);
                break;
            case HeroFormEffect.Sun:
                e.ApplyTimedSynergyDamageReductionBonus(0.10f, dur);
                e.ApplyTimedSynergyDamageDealtBonus(0.10f, dur);
                e.HealFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.12f)));
                break;
            case HeroFormEffect.Fangs:
                e.ApplyAttackSpeedBoostFromSynergy(1.30f, dur);
                e.ApplyTimedSynergyDamageDealtBonus(0.20f, dur);
                break;
            case HeroFormEffect.Gale:
                e.ApplyAttackSpeedBoostFromSynergy(1.22f, dur);
                e.ApplyTimedSynergyDamageDealtBonus(0.15f, dur);
                break;
            case HeroFormEffect.Gate:
                e.ApplyTimedSynergyDamageReductionBonus(0.10f, dur);
                e.ApplyTimedSynergyDamageDealtBonus(0.25f, dur);
                break;
            default: // Hook
                e.ApplyTimedSynergyDamageReductionBonus(0.18f, dur);
                e.ApplyTimedSynergyDamageDealtBonus(0.12f, dur);
                e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.10f)), dur);
                break;
        }
    }

    private string HeroFormEffectText(HeroFormEffect eff, bool ja)
    {
        switch (eff)
        {
            case HeroFormEffect.Bulwark: return ja ? "被ダメ-18%＋大シールド" : "DR -18% + big shield";
            case HeroFormEffect.Sun: return ja ? "被ダメ-10%＋与ダメ+10%＋回復" : "DR -10% + DMG +10% + heal";
            case HeroFormEffect.Fangs: return ja ? "攻撃速度+30%＋与ダメ+20%" : "ATKSPD +30% + DMG +20%";
            case HeroFormEffect.Gale: return ja ? "攻撃速度+22%＋与ダメ+15%" : "ATKSPD +22% + DMG +15%";
            case HeroFormEffect.Gate: return ja ? "被ダメ-10%＋与ダメ+25%" : "DR -10% + DMG +25%";
            default: return ja ? "被ダメ-18%＋与ダメ+12%＋シールド" : "DR -18% + DMG +12% + shield";
        }
    }

    // 5x3ガイド表示用のセル index（index = dr*5 + dc。スプレッド形が最大5列のため幅5）。
    private int[] HeroFormCells5x3(HeroFormDef def)
    {
        if (def.cells == null) return new int[0];
        int[] arr = new int[def.cells.Length];
        for (int i = 0; i < def.cells.Length; i++)
            arr[i] = def.cells[i].dr * 5 + def.cells[i].dc;
        return arr;
    }

    private string BuildFormationSignature(Dictionary<Node, Color> cells)
    {
        if (cells.Count == 0) return "-";
        List<string> keys = new List<string>();
        foreach (KeyValuePair<Node, Color> kv in cells)
            keys.Add($"{Mathf.RoundToInt(kv.Key.worldPosition.x)},{Mathf.RoundToInt(kv.Key.worldPosition.y)}:{Mathf.RoundToInt(kv.Value.r * 10)}{Mathf.RoundToInt(kv.Value.b * 10)}");
        keys.Sort();
        return string.Join("|", keys);
    }

    private void RebuildFormationMarkers(Dictionary<Node, Color> cells)
    {
        EnsureFormationMarkerAssets();
        int idx = 0;
        foreach (KeyValuePair<Node, Color> kv in cells)
        {
            SpriteRenderer sr = idx < formationMarkers.Count ? formationMarkers[idx] : CreateFormationMarker();
            Vector3 p = kv.Key.worldPosition;
            sr.transform.position = new Vector3(p.x, p.y, p.z + 0.02f);
            sr.color = kv.Value;
            sr.gameObject.SetActive(true);
            idx++;
        }
        for (int i = idx; i < formationMarkers.Count; i++)
            formationMarkers[i].gameObject.SetActive(false);
    }

    private void ClearFormationMarkers()
    {
        for (int i = 0; i < formationMarkers.Count; i++)
            if (formationMarkers[i] != null) formationMarkers[i].gameObject.SetActive(false);
    }

    private SpriteRenderer CreateFormationMarker()
    {
        EnsureFormationMarkerAssets();
        GameObject go = new GameObject("FormationMarker", typeof(SpriteRenderer));
        go.transform.SetParent(formationMarkerRoot, false);
        go.transform.localScale = Vector3.one * 0.92f;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = formationMarkerSprite;
        sr.sortingOrder = 300;
        formationMarkers.Add(sr);
        return sr;
    }

    private void EnsureFormationMarkerAssets()
    {
        if (formationMarkerRoot == null)
        {
            formationMarkerRoot = new GameObject("FormationMarkers").transform;
            formationMarkerRoot.SetParent(transform, false);
        }
        if (formationMarkerSprite == null)
        {
            int s = 16;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Min(x, s - 1 - x) / (s * 0.5f);
                    float dy = Mathf.Min(y, s - 1 - y) / (s * 0.5f);
                    float edge = Mathf.Clamp01(Mathf.Min(dx, dy) * 2.4f);
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Lerp(0.12f, 1f, edge));
                }
            tex.SetPixels(px);
            tex.Apply();
            formationMarkerSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
        }
    }

    // ③b 盤上の全ユニット（敵味方）を返す。転がる巨大物の踏みつけ判定に使う。
    public List<BaseEntity> AllBoardEntities()
    {
        List<BaseEntity> all = new List<BaseEntity>(team1Entities.Count + team2Entities.Count);
        all.AddRange(team1Entities);
        all.AddRange(team2Entities);
        return all;
    }

    // ③b 盤面の1行を端から転がる巨大物を発生させる（敵味方問わずダメージ＋スタン）。数値は暫定（R3-balance）。
    public void LaunchRollingHazard()
    {
        GridManager gm = GridManager.Instance;
        if (gm == null) return;
        int r = UnityEngine.Random.Range(3, 8); // 行3〜7
        Node left = gm.GetNodeAtBoardCoordinate(1, r);
        Node right = gm.GetNodeAtBoardCoordinate(10, r);
        if (left == null || right == null) return;
        float y = left.worldPosition.y;
        bool fromLeft = UnityEngine.Random.value < 0.5f;
        float startX = fromLeft ? left.worldPosition.x - 2.5f : right.worldPosition.x + 2.5f;
        float endX = fromLeft ? right.worldPosition.x + 2.5f : left.worldPosition.x - 2.5f;
        RollingHazard.Launch(startX, endX, y, 6.5f, 0.30f, 1.2f);
    }

    // 中ボス戦中に、少し間を置いて転がる巨大物を数回出す。
    // チャプターが進むほど回数を増やす（ch3-4=2回 / ch5-6=3回 / ch7+=4回）。
    private System.Collections.IEnumerator MidBossHazardRoutine()
    {
        int rolls = currentChapter >= 7 ? 4 : (currentChapter >= 5 ? 3 : 2);
        yield return new WaitForSeconds(4f);
        for (int i = 0; i < rolls; i++)
        {
            if (!IsRoundInProgress) yield break;
            LaunchRollingHazard();
            yield return new WaitForSeconds(5f);
        }
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
        ShowAugmentSelectionForTier(tier);
    }

    // 指定ティアでオーグメント選択UIを開きます。イベント経由でなく、外部から直接呼ぶ用の共通入口です。
    private void ShowAugmentSelectionForTier(AugmentTier tier)
    {
        augmentSelectionPending = true;
        AugmentSelectionUI.EnsureExists().Show(tier, ShownAugmentIds, OnAugmentPicked);
    }

    // DEBUG用: デバッグメニューからオーグメント選択UIを強制表示します（製品ビルドでは DebugMenu 側で囲い込み）。
    public void DebugShowAugmentSelection(AugmentTier tier)
    {
        ShowAugmentSelectionForTier(tier);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ===== デバッグ専用API（DebugMenu から呼ぶ。製品ビルドでは丸ごと除外）=====
    // 無敵トグル（BaseEntity.TakeDamage が参照）。
    public bool DebugPlayerInvincible;
    public bool DebugEnemyInvincible;

    // 現在チャプターの総ラウンド数／現在位置（1始まり）。
    public int DebugWaveCount { get { InitializeWaveDefinitions(); return waveDefinitions.Count; } }
    public int DebugCurrentWaveNumber => Mathf.Clamp(currentWaveIndex + 1, 1, Mathf.Max(1, waveDefinitions.Count));

    // i 番目(0始まり)のラウンドのラベル（DebugMenu のリスト表示用）。
    public string DebugWaveLabel(int index)
    {
        InitializeWaveDefinitions();
        if (index < 0 || index >= waveDefinitions.Count) return "-";
        WaveDefinition d = waveDefinitions[index];
        if (d == null) return "?";
        string kind = d.IsBossWave ? "章ボス" : (d.IsMidBossWave ? "中ボス" : (d.IsEventRound ? "イベント" : "戦闘"));
        return $"{d.StageIndex}-{d.RoundInStage} [{kind}]";
    }

    // 指定ラウンド(0始まり)へジャンプ。次のFIGHTでそのラウンドが始まる。戦闘中・選択中は不可。
    public void DebugJumpToWave(int index)
    {
        if (IsRoundInProgress || waveClearCelebrating || bossRewardSelectionPending)
        {
            Debug.LogWarning("[Debug] 戦闘中／報酬選択中はラウンドジャンプできません。");
            return;
        }
        InitializeWaveDefinitions();
        if (waveDefinitions.Count == 0) return;
        currentWaveIndex = Mathf.Clamp(index, 0, waveDefinitions.Count - 1);
        bossDialogueShownThisWave = false;
        enemiesPreviewedThisWave = false;
        bossCinematicPlayedThisWave = false;
        bossIntroInProgress = false;
        ClearEnemyUnits();
        UpdateRoundProgressUi();
        Debug.Log($"[Debug] Jumped to wave {currentWaveIndex + 1} ({DebugWaveLabel(currentWaveIndex)}).");
    }

    // 報酬UIの強制表示。
    public void DebugForceItemReward() { ShowItemChoice3Reward(); }
    public void DebugForceBuffTileReward() { ShowBuffTileReward(); }

    // 現在のラウンドを開始して即勝利扱いにする（敵を全滅→通常の勝利フローへ）。
    public void DebugInstantWinRound()
    {
        if (gameOver || waveClearCelebrating || bossRewardSelectionPending)
            return;
        if (!IsRoundInProgress)
            DebugFight(); // 敵スポーン込みで通常開始（イベント行や味方0体だと開始しない）
        if (!IsRoundInProgress)
        {
            Debug.LogWarning("[Debug] ラウンドを開始できませんでした（イベント行／味方未配置など）。");
            return;
        }
        ClearEnemyUnits(); // 敵を全滅させて
        TryEndRound();     // 勝利判定→祝福インターバル→CompleteCurrentWave
    }
#endif

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

                // STORY-skin: ch8 をカガチで踏破したら、犬化(skindogehai)スキンを恒久解放。
                if (currentChapter == 8 && string.Equals(heroUnitId, "HeroKagachi", StringComparison.OrdinalIgnoreCase)
                    && !SaveManager.Instance.GetStoryFlag("skin_kagachi_unlocked"))
                {
                    SaveManager.Instance.SetStoryFlag("skin_kagachi_unlocked", true);
                    bool ja2 = LocalizationManager.IsJapanese;
                    ScorePopupUI.EnsureExists().Show(1, ja2 ? "犬化スキン 解放！" : "Dog-form skin unlocked!", new Color(0.8f, 0.95f, 1f));
                }

                // R2-recruit: 章ボス報酬ユニット（ch1-5=cost4 / ch6+=cost5）を恒久解放。
                // bossAllies に記録 → 以降のラン/章のショップに恒久出現（IsEntityUnlockedForShop が参照）。
                string chapterBossUnitId = GetChapterBossRewardUnitId(currentChapter);
                if (!string.IsNullOrEmpty(chapterBossUnitId))
                {
                    // 初回クリア=解放演出 / 2回目以降=強化（アフィニティ加算）演出を出す。
                    bool firstUnlock = !SaveManager.Instance.HasBossAlly(chapterBossUnitId);
                    SaveManager.Instance.AddBossAlly(chapterBossUnitId, 1); // 初回=recruitCount1 / 再=加算で強化
                    int affinityLevel = SaveManager.Instance.GetBossAffinityLevel(chapterBossUnitId);
                    BossUnlockBannerUI.EnsureExists().Show(GetEntityIconById(chapterBossUnitId), LocalizationManager.UnitName(chapterBossUnitId), firstUnlock, affinityLevel);
                }

                // R3-hero-mastery: 章クリアで「使用した主人公」の熟練度XPを付与（到達章＋自己新で増加）。
                if (!string.IsNullOrEmpty(heroUnitId))
                {
                    int xp = 6 + currentChapter * 3 + (isNewRecord ? 4 : 0);
                    int beforeLv = SaveManager.Instance.GetHeroMasteryLevel(heroUnitId);
                    int afterLv = SaveManager.Instance.AddHeroMasteryXp(heroUnitId, xp);
                    bool ja = LocalizationManager.IsJapanese;
                    ScorePopupUI.EnsureExists().Show(1, (ja ? "熟練度 +" : "Mastery +") + xp, new Color(0.7f, 0.9f, 1f));
                    if (afterLv > beforeLv)
                        ScorePopupUI.EnsureExists().Show(1, (ja ? "主人公 熟練度 Lv " : "Hero Mastery Lv ") + afterLv + "！", new Color(1f, 0.85f, 0.4f));
                }
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
        // ステージ1〜3（章クリア以外）の途中リザルトは表示しない。スコア集計だけ続け、最終ステージ4クリア時のみ表示する。
        // （上の chapterStageScores/Times への加算は維持される）
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

    private bool storyClearSequenceActive; // 章クリア幕間の再生中フラグ（結果表示を待たせる）。

    private void TryShowPendingStageResult()
    {
        if (!hasPendingStageResult)
            return;
        if (bossRewardSelectionPending)
            return;
        if (storyClearSequenceActive)
            return; // 幕間再生中は待機（完了後に本メソッドが再度呼ばれる）。

        // STORY: 章クリア時、結果表示の前に該当章の幕間/エンディングを挟む（ch12/ch13）。
        if (pendingResultIsChapterClear)
        {
            string interludeId = GetChapterClearInterludeId();
            if (!string.IsNullOrEmpty(interludeId))
            {
                storyClearSequenceActive = true;
                PlayChapterClearStory(interludeId, () =>
                {
                    storyClearSequenceActive = false;
                    ShowPendingStageResultNow();
                });
                return;
            }
        }
        ShowPendingStageResultNow();
    }

    private void ShowPendingStageResultNow()
    {
        if (!hasPendingStageResult) return;
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
        // R2-recruit: 撃破した中ボスウェーブを保持（currentWaveIndex++ 前に取得）。候補解放に使う。
        WaveDefinition clearedMidBossDef = clearedMidBoss ? waveDefinitions[currentWaveIndex] : null;

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
            // R3-chest-room: チェスト部屋をクリアしたら状態解除（報酬は宝箱が即時付与済み）。
            if (clearedDef != null && clearedDef.IsChestRoom) EndChestRoom();

            currentWaveIndex++;
            bossDialogueShownThisWave = false; // 次ウェーブのボス戦で掛け合いを再表示できるように。
            enemiesPreviewedThisWave = false;  // 次ウェーブの敵を改めて事前配置できるように。
            bossCinematicPlayedThisWave = false; // 次ウェーブで登場演出を再生できるように。
            bossIntroInProgress = false;
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

            // R3-hero-scale: ヒーロー育成XP（撃破した敵数＋ラウンド進行ぶん）。
            if (!clearedProgressDebugWave)
            {
                int enemyCount = clearedDef != null ? clearedDef.Enemies.Count : 0;
                AddHeroRunXp(enemyCount + 2);
            }

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

        // R2-recruit: 章ボスは選択なし。コスト上限解放のみ（恒久解放は章クリアの AddBossAlly で実施済み）。
        if (!completedDebugTrainingWave && clearedBossWave)
            UnlockNextShopCostTier();

        // R2-rewards: 中ボス撃破でコスト上限解放＋報酬種別ごとの処理。
        if (clearedMidBoss)
        {
            UnlockNextShopCostTier();
            switch (clearedMidBossDef != null ? clearedMidBossDef.RewardKind : MidBossRewardKind.Recruit)
            {
                case MidBossRewardKind.ItemChoice3:
                    ShowItemChoice3Reward();
                    break;
                case MidBossRewardKind.BuffTile:
                    ShowBuffTileReward();
                    break;
                case MidBossRewardKind.CoinReward:
                    GrantCoinReward(clearedMidBossDef != null ? clearedMidBossDef.RewardCoins : 40);
                    break;
                default:
                    ShowMidBossRecruit(clearedMidBossDef, clearedMidBossDef != null ? clearedMidBossDef.RewardCount : 1);
                    break;
            }
        }

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

    // R2-rewards: ボス選択の残り候補と残り選択回数（4-5 は count=2 で2体選べる）。
    private readonly List<EntitiesDatabaseSO.EntityData> pendingRecruitOptions = new List<EntitiesDatabaseSO.EntityData>();
    private int pendingRecruitRemaining;

    // R2-recruit/rewards: 中ボス撃破後、同コスト候補から count 体を「その章だけ」ショップ解放させる選択を出す。
    private void ShowMidBossRecruit(WaveDefinition def, int count = 1)
    {
        if (entitiesDatabase == null || entitiesDatabase.allEntities == null)
            return;

        // 候補ID：必ず「そのウェーブで戦ったボス」から取る（戦ったボス＝仲間化候補＝ユーザー想定）。
        // 1) RecruitCandidateIds があればそれ（RecruitMidBoss ウェーブ）。
        // 2) 無ければ、実際に出現した敵の UnitId（固定ユニット指定の敵＝ボス級）から導出。
        // ※以前は候補未設定時に固定プール全体へフォールバックし、戦っていない無関係ボスが並ぶ不具合があった。
        List<string> candidateIds = new List<string>();
        if (def != null && def.RecruitCandidateIds.Count > 0)
            candidateIds.AddRange(def.RecruitCandidateIds);
        else if (def != null)
        {
            for (int e = 0; e < def.Enemies.Count; e++)
            {
                string uid = def.Enemies[e].UnitId;
                if (!string.IsNullOrEmpty(uid) && !candidateIds.Contains(uid))
                    candidateIds.Add(uid);
            }
        }

        // 既に解放済み（章内 or 恒久）を除外し、DBに存在する重複なしに絞る（複数選択ぶん多めに残す）。
        pendingRecruitOptions.Clear();
        for (int i = 0; i < candidateIds.Count; i++)
        {
            string id = candidateIds[i];
            if (string.IsNullOrEmpty(id)) continue;
            // 将系（温存）は伏線として敵には出るが、仲間化候補には絶対に出さない。
            if (IsReservedHeroFormUnit(id) || IsHeroUnitId(id)) continue;
            if (chapterUnlockedUnitIds.Contains(id) || IsPermanentlyUnlocked(id)) continue;
            EntitiesDatabaseSO.EntityData data = entitiesDatabase.allEntities.FirstOrDefault(d =>
                d.prefab != null && string.Equals(d.name, id, StringComparison.OrdinalIgnoreCase));
            if (data.prefab != null && !pendingRecruitOptions.Any(o => string.Equals(o.name, data.name, StringComparison.OrdinalIgnoreCase)))
                pendingRecruitOptions.Add(data);
        }

        if (pendingRecruitOptions.Count == 0)
            return; // 候補が全て解放済みなら選択は出さない

        // 候補は常に最大3択に制限する（戦ったボスが3体を超える事はまず無いが保険）。
        const int MaxRecruitOptions = 3;
        if (pendingRecruitOptions.Count > MaxRecruitOptions)
            pendingRecruitOptions.RemoveRange(MaxRecruitOptions, pendingRecruitOptions.Count - MaxRecruitOptions);

        pendingRecruitRemaining = Mathf.Clamp(count, 1, pendingRecruitOptions.Count);
        bossRewardSelectionPending = true;
        BossRewardSelectionUI.EnsureExists().Show(pendingRecruitOptions, OnMidBossRecruit);
    }

    // 中ボス報酬：選んだユニットを「その章だけ」ショップ解放する（無料ベンチ付与つき）。複数選択にも対応。
    private void OnMidBossRecruit(EntitiesDatabaseSO.EntityData selectedData)
    {
        if (!string.IsNullOrEmpty(selectedData.name))
        {
            chapterUnlockedUnitIds.Add(selectedData.name);
            if (HasBenchSpace || CanCompleteUpgradeWithPurchase(selectedData.name))
            {
                BaseEntity recruited = CreateBenchEntity(selectedData, 1);
                if (recruited != null)
                    ResolveUpgradesFor(recruited, UpgradeScope.AllOwned);
            }
            else
            {
                Debug.LogWarning($"{selectedData.name} を章内解放しましたが、ベンチが満杯のため無料配置は行いませんでした。");
            }

            AttackEffectPlayer.PlayUiSfx("unit_buy");
            OnRosterChanged?.Invoke();
            if (UIShop.Instance != null)
                UIShop.Instance.GenerateCard();

            // 選んだ候補を除外。
            pendingRecruitOptions.RemoveAll(o => string.Equals(o.name, selectedData.name, StringComparison.OrdinalIgnoreCase));
        }

        pendingRecruitRemaining--;
        // まだ選べる＆候補が残っていれば、続けて選択UIを再表示。
        if (pendingRecruitRemaining > 0 && pendingRecruitOptions.Count > 0)
        {
            BossRewardSelectionUI.EnsureExists().Show(pendingRecruitOptions, OnMidBossRecruit);
            return;
        }

        bossRewardSelectionPending = false;
        TryStartEventRound();
        TryShowPendingStageResult();
    }

    // R2-rewards: アイテム3択（防具/攻撃/秘力カテゴリから各1体ランダム）を提示する。
    private void ShowItemChoice3Reward()
    {
        List<ItemData> options = new List<ItemData>();
        ItemData a = PickRandomItemOfCategory(ItemCategory.Defense);
        ItemData o = PickRandomItemOfCategory(ItemCategory.Offense);
        ItemData s = PickRandomItemOfCategory(ItemCategory.Skill);
        if (a != null) options.Add(a);
        if (o != null) options.Add(o);
        if (s != null) options.Add(s);

        if (options.Count == 0)
        {
            GrantRandomItemFromSynergy(); // 保険
            TryStartEventRound();
            TryShowPendingStageResult();
            return;
        }

        bossRewardSelectionPending = true;
        ItemRewardSelectionUI.EnsureExists().Show(options, OnItemRewardChosen);
    }

    private ItemData PickRandomItemOfCategory(ItemCategory category)
    {
        IReadOnlyList<ItemData> all = ItemCatalog.AllItems;
        if (all == null) return null;
        List<ItemData> pool = all.Where(it => it != null && it.category == category).ToList();
        return pool.Count == 0 ? null : pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    private void OnItemRewardChosen(ItemData item)
    {
        bossRewardSelectionPending = false;
        if (item != null)
        {
            ReturnItemToBench(item);
            AttackEffectPlayer.PlayUiSfx("item_equip");
        }
        TryStartEventRound();
        TryShowPendingStageResult();
    }

    // R2-rewards: 大量コイン報酬。
    private void GrantCoinReward(int coins)
    {
        coins = Mathf.Max(1, coins);
        PlayerData.Instance?.AddMoney(coins);
        bool ja = LocalizationManager.IsJapanese;
        ScorePopupUI.EnsureExists().Show(coins, ja ? "コイン獲得！" : "Coins!", new Color(1f, 0.86f, 0.35f));
        TryStartEventRound();
        TryShowPendingStageResult();
    }

    // ===== R2-rewards: 強化マス =====

    // 強化マス報酬：まず種別（攻撃/防御/秘力）を選ばせる。
    private void ShowBuffTileReward()
    {
        bossRewardSelectionPending = true;
        BuffTileRewardUI.EnsureExists().Show(BeginBuffTilePlacement);
    }

    // 種別選択後：盤面のマスクリック待ちへ。クリックされるまで進行は保留したまま。
    public void BeginBuffTilePlacement(BuffTileType type)
    {
        pendingBuffTileType = type;
        buffTileSelectMode = true; // bossRewardSelectionPending は保持
    }

    // ④ 強化マス設置中のホバー演出用。tile_box を対象マスに重ね、85%〜100%でゆっくり拡縮させる。
    private GameObject buffHoverPreviewObj;
    private SpriteRenderer buffHoverPreviewRenderer;
    private Sprite buffHoverBoxSprite;

    // 強化マス設置モード中、ホバー中の自陣配置マスに tile_box を拡縮アニメで表示する（Update から呼ぶ）。
    private void HandleBuffTilePlacementHover()
    {
        if (!buffTileSelectMode)
        {
            if (buffHoverPreviewObj != null && buffHoverPreviewObj.activeSelf)
                buffHoverPreviewObj.SetActive(false);
            return;
        }

        Camera cam = GetBoardCamera();
        GridManager gm = GridManager.Instance;
        if (cam == null || gm == null)
            return;

        Vector3 sp = Input.mousePosition;
        sp.z = Mathf.Abs(cam.transform.position.z);
        Vector3 wp = cam.ScreenToWorldPoint(sp);
        wp.z = 0f;
        Tile tile = gm.GetTileAtWorldPosition(wp);
        if (tile == null)
        {
            Collider2D hit = Physics2D.OverlapPoint(wp);
            if (hit != null) tile = hit.GetComponent<Tile>();
        }
        Node node = tile != null ? gm.GetNodeForTile(tile) : null;
        bool valid = node != null && gm.IsDeploymentNode(Team.Team1, node);
        if (!valid)
        {
            if (buffHoverPreviewObj != null && buffHoverPreviewObj.activeSelf)
                buffHoverPreviewObj.SetActive(false);
            return;
        }

        EnsureBuffHoverPreview();
        if (buffHoverPreviewObj == null)
            return;

        // タイルの見た目に合わせて配置・サイズ調整し、その上で 85%〜100% をゆっくり往復させる。
        SpriteRenderer tileSR = tile.GetComponentInChildren<SpriteRenderer>();
        Vector3 center = tile.transform.position;
        center.z = -0.05f;
        buffHoverPreviewObj.transform.position = center;
        if (tileSR != null)
        {
            buffHoverPreviewRenderer.sortingLayerID = tileSR.sortingLayerID;
            buffHoverPreviewRenderer.sortingOrder = 160; // タイルハイライト(150)の上に重ねる。
        }
        float cell = (tileSR != null && tileSR.sprite != null) ? Mathf.Max(tileSR.bounds.size.x, tileSR.bounds.size.y) : 1f;
        float spriteWorld = (buffHoverPreviewRenderer.sprite != null) ? buffHoverPreviewRenderer.sprite.bounds.size.x : 1f;
        float fit = spriteWorld > 0.0001f ? cell / spriteWorld : 1f;
        float pulse = Mathf.Lerp(0.85f, 1.0f, Mathf.Sin(Time.unscaledTime * 2.2f) * 0.5f + 0.5f);
        buffHoverPreviewObj.transform.localScale = Vector3.one * (fit * pulse);
        if (!buffHoverPreviewObj.activeSelf)
            buffHoverPreviewObj.SetActive(true);
    }

    private void EnsureBuffHoverPreview()
    {
        if (buffHoverPreviewObj != null)
            return;

        if (buffHoverBoxSprite == null)
        {
            buffHoverBoxSprite = Resources.Load<Sprite>("UI/Tiles/tile_box");
            if (buffHoverBoxSprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("UI/Tiles/tile_box");
                if (tex != null)
                    buffHoverBoxSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        buffHoverPreviewObj = new GameObject("BuffTileHoverPreview", typeof(SpriteRenderer));
        buffHoverPreviewRenderer = buffHoverPreviewObj.GetComponent<SpriteRenderer>();
        buffHoverPreviewRenderer.sprite = buffHoverBoxSprite;
        buffHoverPreviewRenderer.color = new Color(1f, 0.95f, 0.55f, 0.85f); // 強化マスらしい金色のハイライト。
        buffHoverPreviewObj.SetActive(false);
    }

    // 編成フェーズ中、強化マス設置モードならクリックされたマスを強化マスにする（Update から呼ぶ）。
    private void HandleBuffTilePlacementClick()
    {
        if (!buffTileSelectMode || !Input.GetMouseButtonDown(0))
            return;

        Camera cam = GetBoardCamera();
        GridManager gm = GridManager.Instance;
        if (cam == null || gm == null)
            return;

        // 盤面はパースペクティブ表示のため、Draggable と同じく「カメラから盤面(z=0)までのz距離」を
        // 与えてから ScreenToWorldPoint する（Input.mousePosition の z=0 のままだと近接面に落ちて盤面に当たらない）。
        Vector3 sp = Input.mousePosition;
        sp.z = Mathf.Abs(cam.transform.position.z); // 盤面 z=0 までの距離
        Vector3 wp = cam.ScreenToWorldPoint(sp);
        wp.z = 0f;
        Tile tile = gm.GetTileAtWorldPosition(wp);
        // 座標計算で取れない時は、Draggable と同様にコライダーでも探す（保険）。
        if (tile == null)
        {
            Collider2D hit = Physics2D.OverlapPoint(wp);
            if (hit != null) tile = hit.GetComponent<Tile>();
        }
        Node node = tile != null ? gm.GetNodeForTile(tile) : null;
        if (node == null || !gm.IsDeploymentNode(Team.Team1, node))
            return; // 自陣の配置マス以外は無視（選択継続）

        buffTiles.RemoveAll(b => b.Node == node); // 同じマスは上書き
        buffTiles.Add(new BuffTile { Node = node, Type = pendingBuffTileType });

        buffTileSelectMode = false;
        bossRewardSelectionPending = false;
        RebuildBuffTileMarkers();
        if (BuffTileRewardUI.Instance != null) BuffTileRewardUI.Instance.Hide();
        AttackEffectPlayer.PlayUiSfx("sfx_ui_select");

        TryStartEventRound();
        TryShowPendingStageResult();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：強化マス設置待ちなら、空きマス（無ければ任意の自陣配置マス）へ自動設置して進行させる。
    // 設置できなくても保留フラグは必ず解除し、進行不能を防ぐ。設置待ちでなければ false。
    public bool DebugResolveBuffTilePlacement()
    {
        if (!buffTileSelectMode)
            return false;
        Node node = GridManager.Instance != null ? GridManager.Instance.GetFreeNode(Team.Team1) : null;
        if (node != null)
        {
            buffTiles.RemoveAll(b => b.Node == node);
            buffTiles.Add(new BuffTile { Node = node, Type = pendingBuffTileType });
        }
        buffTileSelectMode = false;
        bossRewardSelectionPending = false;
        RebuildBuffTileMarkers();
        if (BuffTileRewardUI.Instance != null) BuffTileRewardUI.Instance.Hide();
        TryStartEventRound();
        TryShowPendingStageResult();
        return true;
    }
#endif

    // 戦闘開始時、各強化マス上のユニットへ時限バフを付与する。
    private void ApplyBuffTileBonuses()
    {
        if (buffTiles.Count == 0)
            return;
        const float dur = 60f;
        foreach (BuffTile bt in buffTiles)
        {
            if (bt == null || bt.Node == null) continue;
            BaseEntity e = null;
            for (int i = 0; i < team1Entities.Count; i++)
            {
                BaseEntity c = team1Entities[i];
                if (c != null && !c.IsDead && c.IsOnBoard && !c.IsSummonedUnit && !c.IsCore && c.CurrentNode == bt.Node)
                { e = c; break; }
            }
            if (e == null) continue;
            switch (bt.Type)
            {
                case BuffTileType.Attack:
                    e.ApplyTimedSynergyDamageDealtBonus(0.30f, dur);
                    e.ApplyAttackSpeedBoostFromSynergy(1.15f, dur);
                    break;
                case BuffTileType.Defense:
                    e.ApplyTimedSynergyDamageReductionBonus(0.20f, dur);
                    e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.25f)), dur);
                    break;
                case BuffTileType.Arcane:
                    // 秘力＝マナ獲得促進＋スキル威力強化（攻速/与ダメではなく“魔法寄り”の効果）。
                    e.ApplyTimedSynergyManaGainMultiplier(1.5f, dur); // マナ獲得 ×1.5
                    e.ApplyTimedSynergyPowerBonus(0.35f, dur);        // 秘力（スキル威力）+35%
                    e.GainManaFromSynergy(20);                        // 即時マナ +20（初手スキルを早める）
                    break;
            }
            AttackEffectPlayer.PlayAreaIndicator(e, e.transform.position, 0.55f, 0.9f, 1.1f);
        }
    }

    // boss-skill-progression: スキルから「caster 周囲の味方を直接強化」する（解放ボスの複雑効果）。
    //   ※戦闘中の強化マス生成は廃止（戦闘後に残る・敵陣に出る・踏むか不確実だったため）。
    //     マス/マーカーは作らず、範囲内の味方へ即時に時限バフを付与するだけ（戦闘終了で自然消滅）。
    public void EmpowerAlliesAround(BaseEntity caster, BuffTileType type, int radiusCells, float dur)
    {
        if (caster == null || caster.CurrentNode == null) return;
        GridManager grid = GridManager.Instance; if (grid == null) return;
        int nc = grid.GetBoardColumn(caster.CurrentNode), nr = grid.GetBoardRow(caster.CurrentNode);
        int r = Mathf.Max(1, radiusCells);
        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity e = team1Entities[i];
            if (e == null || e.IsDead || !e.IsOnBoard || e.IsCore || e.CurrentNode == null) continue;
            int ec = grid.GetBoardColumn(e.CurrentNode), er = grid.GetBoardRow(e.CurrentNode);
            if (Mathf.Abs(ec - nc) > r || Mathf.Abs(er - nr) > r) continue;
            ApplySkillBuffTileEffect(e, type, dur);
        }
    }

    private void ApplySkillBuffTileEffect(BaseEntity e, BuffTileType type, float dur)
    {
        switch (type)
        {
            case BuffTileType.Attack:
                e.ApplyTimedSynergyDamageDealtBonus(0.30f, dur); e.ApplyAttackSpeedBoostFromSynergy(1.15f, dur); break;
            case BuffTileType.Defense:
                e.ApplyTimedSynergyDamageReductionBonus(0.20f, dur); e.ApplyShieldFromSynergy(Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.25f)), dur); break;
            case BuffTileType.Arcane:
                e.ApplyTimedSynergyManaGainMultiplier(1.5f, dur); e.ApplyTimedSynergyPowerBonus(0.35f, dur); e.GainManaFromSynergy(20); break;
        }
        AttackEffectPlayer.PlayAreaIndicator(e, e.transform.position, 0.55f, 0.9f, 1.1f);
    }

    private Color BuffTileColor(BuffTileType type)
    {
        switch (type)
        {
            case BuffTileType.Attack: return new Color(1f, 0.5f, 0.18f, 0.55f);
            case BuffTileType.Defense: return new Color(0.3f, 0.62f, 1f, 0.55f);
            default: return new Color(0.8f, 0.45f, 1f, 0.6f); // Arcane
        }
    }

    private void RebuildBuffTileMarkers()
    {
        EnsureFormationMarkerAssets();
        if (buffTileMarkerRoot == null)
        {
            buffTileMarkerRoot = new GameObject("BuffTileMarkers").transform;
            buffTileMarkerRoot.SetParent(transform, false);
        }
        int idx = 0;
        foreach (BuffTile bt in buffTiles)
        {
            if (bt == null || bt.Node == null) continue;
            SpriteRenderer sr;
            if (idx < buffTileMarkers.Count) sr = buffTileMarkers[idx];
            else
            {
                GameObject go = new GameObject("BuffTileMarker", typeof(SpriteRenderer));
                go.transform.SetParent(buffTileMarkerRoot, false);
                go.transform.localScale = Vector3.one * 1.0f;
                sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = formationMarkerSprite;
                sr.sortingOrder = 290; // 盤面タイル上・ユニット下
                buffTileMarkers.Add(sr);
            }
            Vector3 p = bt.Node.worldPosition;
            sr.transform.position = new Vector3(p.x, p.y, p.z + 0.03f);
            sr.color = BuffTileColor(bt.Type);
            sr.gameObject.SetActive(true);
            idx++;
        }
        for (int i = idx; i < buffTileMarkers.Count; i++)
            buffTileMarkers[i].gameObject.SetActive(false);
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

    // STORY v2: 節目チャッターの既読管理（章ごとにEarly→Mid→PreBossを各1回）。
    private int chatterPhaseShown = -1;
    private int chatterChapterTracked = -1;

    // 戦闘開始時に、章の進行フェーズ（序盤/中盤/章ボス直前）が進んだら章ボスの遠隔チャッターを出す。
    private void MaybeShowChapterChatter()
    {
        if (IsCoreMode) return;
        if (currentChapter != chatterChapterTracked) { chatterChapterTracked = currentChapter; chatterPhaseShown = -1; }
        if (currentWaveIndex < 1 || currentWaveIndex >= waveDefinitions.Count) return; // ウェーブ0はオープニングと重なるので除外。
        WaveDefinition wd = waveDefinitions[currentWaveIndex];
        if (wd == null || wd.IsEventRound) return; // 戦闘ウェーブのみ。

        int bossIdx = -1;
        for (int i = 0; i < waveDefinitions.Count; i++)
            if (waveDefinitions[i] != null && waveDefinitions[i].IsBossWave) { bossIdx = i; break; }

        int phase; // 0=Early, 1=Mid, 2=PreBoss
        if (bossIdx >= 0 && currentWaveIndex >= bossIdx - 1) phase = 2;
        else { float frac = bossIdx > 0 ? (float)currentWaveIndex / bossIdx : 0f; phase = frac < 0.45f ? 0 : 1; }

        if (phase <= chatterPhaseShown) return; // 各フェーズ1回・順送り。
        if (ChapterStory.TryGetChatter(currentChapter, (ChapterStory.ChatterPhase)phase, out string speaker, out string line))
        {
            chatterPhaseShown = phase;
            ChatterSubtitleUI.EnsureExists().Show(speaker, line);
        }
        else chatterPhaseShown = phase; // データが無くてもフェーズは消化（重複表示防止）。
    }

    // STORY v2: 現在の章で「何体目の中ボス戦か」（1始まり、プレイ順）。中ボス別キャラ化のslot算出に使う。
    // currentWaveIndex 以前（含む）の IsMidBossWave 数を数える。
    private int CurrentMidBossSlot()
    {
        int slot = 0;
        for (int i = 0; i <= currentWaveIndex && i < waveDefinitions.Count; i++)
            if (waveDefinitions[i] != null && waveDefinitions[i].IsMidBossWave) slot++;
        return slot;
    }

    // STORY: 現在ウェーブより前で、同じ素体(unitId)が中ボスとして既に何回出たか（0始まり）。
    // 同素体が固定戦＋ノード等で複数回出る時、2回目を別個体（ロウガ/メイリン等）にするための occurrence。
    private int MidBossOccurrenceOf(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return 0;
        int n = 0;
        for (int i = 0; i < currentWaveIndex && i < waveDefinitions.Count; i++)
        {
            var w = waveDefinitions[i];
            if (w == null || !w.IsMidBossWave || w.IsNodeChoice) continue; // 未確定のノード枠は除外
            if (w.Enemies == null || w.Enemies.Count == 0) continue;
            if (string.Equals(GetWavePrimaryBossId(w), unitId, System.StringComparison.OrdinalIgnoreCase)) n++;
        }
        return n;
    }

    // ボス報酬候補として使う3体のEntityDataをデータベースから集めます。
    // ボス戦ウェーブの「顔役」ボスIDを返す（掛け合いダイアログ用）。
    // 中ボス＝候補IDの先頭、章ボス＝章ボス報酬ID、無ければ最初の固定ユニット敵。
    private string GetWavePrimaryBossId(WaveDefinition def)
    {
        if (def == null) return string.Empty;
        if (def.IsBossWave)
        {
            string cb = GetChapterBossRewardUnitId(currentChapter);
            if (!string.IsNullOrEmpty(cb)) return cb;
        }
        if (def.RecruitCandidateIds != null && def.RecruitCandidateIds.Count > 0)
            return def.RecruitCandidateIds[0];
        for (int i = 0; i < def.Enemies.Count; i++)
            if (!string.IsNullOrEmpty(def.Enemies[i].UnitId)) return def.Enemies[i].UnitId;
        return string.Empty;
    }

    // ===== STORY: 幕間（戦闘外VN）のトリガー =====
    // 戦闘開始前の幕間（INT_01=第一合流 / INT_08=カガチ犬化）を1回だけ再生。
    // 再生したら true（InterludeUI 完了後に DebugFight が再度呼ばれる）。
    private bool TryPlayPrefightStory(WaveDefinition curWave)
    {
        if (IsCoreMode || SaveManager.Instance == null) return false;
        if (InterludeUI.Instance != null && InterludeUI.Instance.IsShowing) return false;

        // 章開始のオープニングVN（CHOPEN）は、章開始処理（プロローグ→VN→ショップ）側で再生する。
        // ここで再度出すと FIGHT 時に二重表示になるため、この経路では出さない（重複防止）。

        // INT_01: ch4〜6（マグマー編）の最初の戦闘前に一度だけ。守護三陣営の合流。
        if (currentChapter >= 4 && currentChapter <= 6 && currentWaveIndex == 0
            && !SaveManager.Instance.GetStoryFlag("int01"))
        {
            SaveManager.Instance.SetStoryFlag("int01", true);
            InterludeUI.EnsureExists().Show("INT_01", heroUnitId, () => DebugFight());
            return true;
        }

        // INT_08: ch8 章ボス戦前、カガチ限定で一度だけ。通常の3行ダイアログの代わりに犬化幕間。
        if (curWave != null && curWave.IsBossWave && currentChapter == 8
            && string.Equals(heroUnitId, "HeroKagachi", StringComparison.OrdinalIgnoreCase)
            && !SaveManager.Instance.GetStoryFlag("int08"))
        {
            SaveManager.Instance.SetStoryFlag("int08", true);
            bossDialogueShownThisWave = true; // 通常ボスダイアログは抑止
            InterludeUI.EnsureExists().Show("INT_08", heroUnitId, () => DebugFight());
            return true;
        }
        return false;
    }

    // 章クリア時に挟む幕間ID（INT_12=観測者の正体 / INT_13＋ENDING=最終説得＆エンド）。無ければ空。
    private string GetChapterClearInterludeId()
    {
        if (IsCoreMode) return string.Empty;
        if (currentChapter == 12 && SaveManager.Instance != null && !SaveManager.Instance.GetStoryFlag("int12")) return "INT_12";
        if (currentChapter == 13) return "INT_13"; // 最終説得→共通エンディングへ連鎖。
        return string.Empty;
    }

    // 章クリア幕間を再生し、完了後に onDone を呼ぶ。ch13 は INT_13 → ENDING を連鎖。
    private void PlayChapterClearStory(string interludeId, System.Action onDone)
    {
        InterludeUI ui = InterludeUI.EnsureExists();
        if (interludeId == "INT_12") { SaveManager.Instance?.SetStoryFlag("int12", true); ui.Show("INT_12", heroUnitId, onDone); }
        else if (interludeId == "INT_13") { SaveManager.Instance?.SetStoryFlag("int13", true); ui.Show("INT_13", heroUnitId, () => ui.Show("ENDING", heroUnitId, onDone)); }
        else onDone?.Invoke();
    }

    // 指定ユニットIDのショップアイコンを返す（無ければ null）。演出用。
    public Sprite GetEntityIconById(string unitId)
    {
        if (entitiesDatabase == null || entitiesDatabase.allEntities == null || string.IsNullOrEmpty(unitId))
            return null;
        for (int i = 0; i < entitiesDatabase.allEntities.Count; i++)
            if (string.Equals(entitiesDatabase.allEntities[i].name, unitId, StringComparison.OrdinalIgnoreCase))
                return entitiesDatabase.allEntities[i].icon;
        return null;
    }

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

        // ゲームオーバーのリザルトを表示（閉じるとロビーへ戻れる）。
        int goScore = 0;
        for (int i = 0; i < chapterStageScores.Count; i++) goScore += chapterStageScores[i];
        float goTime = Time.unscaledTime - chapterStartTime;
        string roundLabel = (currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count && waveDefinitions[currentWaveIndex] != null)
            ? $"{waveDefinitions[currentWaveIndex].StageIndex}-{waveDefinitions[currentWaveIndex].RoundInStage}" : "-";
        bool ja = LocalizationManager.IsJapanese;
        string sub = IsCoreMode
            ? (ja ? "コアが破壊された" : "Your core was destroyed")
            : (ja ? $"ラウンド {roundLabel} で敗北" : $"Defeated at round {roundLabel}");
        ResultPanelUI.EnsureExists().ShowGameOver(goScore, goTime, sub);
        // R3-hero-mastery: 熟練度は「チャプタークリア」でのみ上昇（敗北では付与しない）。
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

    // アイテムベンチに「アイテム取り外し機」を常設する（無ければ1つ追加）。何度でも使える特殊ツール。
    private void EnsureRemoverToolInBench()
    {
        for (int i = 0; i < itemBenchItems.Count; i++)
            if (itemBenchItems[i] != null && itemBenchItems[i].Data != null && itemBenchItems[i].Data.isRemover)
                return; // 既にある
        ReturnItemToBench(ItemData.Remover());
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

        // 主人公（ヒーロー）は売却禁止。基本3＋採用中ヒーロー＋"Hero"接頭辞ユニットすべて対象。
        if (IsHeroUnit(entity.UnitId)
            || (!string.IsNullOrEmpty(entity.UnitId) && entity.UnitId.StartsWith("Hero", StringComparison.OrdinalIgnoreCase)))
        {
            Debug.LogWarning($"Hero unit '{entity.UnitId}' cannot be sold.");
            return false;
        }

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

        // R2-coremode: 勝敗はコア破壊のみ。敵ウェーブ（コア以外）を一掃したらウェーブクリア。
        // 味方全滅では敗北にしない（敵がコアへ攻め込み、自コア破壊で敗北になる）。
        if (IsCoreMode)
        {
            if (!HasLivingEnemyBattleUnit())
                CompleteCoreWave();
            return;
        }

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
            BeginWaveClearCelebration();
    }

    // ウェーブ勝利時：すぐ配置を戻さず、数秒の祝福インターバルを挟んでから本処理（CompleteCurrentWave）へ。
    private void BeginWaveClearCelebration()
    {
        if (waveClearCelebrating)
            return;

        // デバッグ訓練ウェーブは演出なしで即完了。
        if (debugTrainingWaveActive)
        {
            CompleteCurrentWave();
            return;
        }

        waveClearCelebrating = true;
        IsRoundInProgress = false; // 戦闘を止める（味方は待機・配置はまだ戻さない）。

        bool isBoss = currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count && waveDefinitions[currentWaveIndex].IsBossWave;
        bool isMid = currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count && waveDefinitions[currentWaveIndex].IsMidBossWave;
        AttackEffectPlayer.PlayUiSfx("sfx_ui_select", 0.22f); // ラウンド移行音は控えめに。
        WaveClearBannerUI.EnsureExists().ShowCelebration(isBoss, isMid, WaveClearCelebrationSeconds);

        StartCoroutine(WaveClearCelebrationRoutine());
    }

    private System.Collections.IEnumerator WaveClearCelebrationRoutine()
    {
        // 勝利演出〜次ウェーブ復元（章ボスならWarning表示含む）の間は遷移中フラグを立て、
        // この区間で preview がボス登場演出を先走り起動しないようにする（Warningが終わってから登場させる）。
        waveTransitioning = true;
        // クリックで早送りできる、リアル時間の祝福インターバル。
        float t = 0f;
        while (t < WaveClearCelebrationSeconds)
        {
            if (Input.GetMouseButtonDown(0)) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        waveClearCelebrating = false;

        // 章ボス撃破“後”の短い会話（インターバル後・遷移前）。セリフ本体はGPTが拡充。
        if (!IsCoreMode && currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count
            && waveDefinitions[currentWaveIndex] != null && waveDefinitions[currentWaveIndex].IsBossWave)
        {
            string pbBoss = GetChapterBossRewardUnitId(currentChapter);
            if (!string.IsNullOrEmpty(pbBoss))
            {
                bool pbDone = false;
                HeroBossDialogueUI.EnsureExists().ShowPostBoss(heroUnitId, pbBoss, GetEntityIconById(pbBoss), () => pbDone = true);
                while (!pbDone) yield return null;
            }
        }

        // 次ラウンドへの遷移演出：味方が1マス前進しながらフェードアウト→配置復元→
        // （次が章ボスなら Warning を挟む）→ 初期位置の1マス前からフェードインして出てくる。
        Vector3 fwd = OneCellForwardOffset();
        yield return StartCoroutine(PlayAllyExitAdvance(fwd, 0.32f));
        CompleteCurrentWave();
        if (!IsCoreMode && currentWaveIndex >= 0 && currentWaveIndex < waveDefinitions.Count
            && waveDefinitions[currentWaveIndex] != null && waveDefinitions[currentWaveIndex].IsBossWave)
        {
            bool warnDone = false;
            WarningBannerUI.EnsureExists().Show(1.8f, () => warnDone = true);
            while (!warnDone) yield return null;
        }
        yield return StartCoroutine(PlayAllyEnterFromFront(fwd, 0.32f));
        waveTransitioning = false; // 遷移完了。これ以降の preview でボス登場演出が走れる。
    }

    // プレイヤー前方（敵方向＝盤面の列+1）への1マスぶんのワールドオフセット。
    private Vector3 OneCellForwardOffset()
    {
        if (GridManager.Instance != null)
        {
            Node a = GridManager.Instance.GetNodeAtBoardCoordinate(1, 5);
            Node b = GridManager.Instance.GetNodeAtBoardCoordinate(2, 5);
            if (a != null && b != null) return b.worldPosition - a.worldPosition;
        }
        return new Vector3(1f, 0f, 0f);
    }

    private static void SetEntityAlpha(BaseEntity e, float a)
    {
        if (e == null || e.spriteRender == null) return;
        Color c = e.spriteRender.color; c.a = a; e.spriteRender.color = c;
    }

    // 味方を現在位置から1マス前進させつつフェードアウト（リアル時間）。
    private System.Collections.IEnumerator PlayAllyExitAdvance(Vector3 fwd, float dur)
    {
        var units = new List<BaseEntity>(roundPlayerUnits);
        var from = new Dictionary<BaseEntity, Vector3>();
        foreach (var e in units) if (e != null) from[e] = e.transform.position;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            foreach (var e in units)
            {
                if (e == null || !from.ContainsKey(e)) continue;
                e.transform.position = from[e] + fwd * k;
                SetEntityAlpha(e, 1f - k);
            }
            yield return null;
        }
    }

    // 配置復元後、homeの1マス前から home へフェードインしながら出てくる（リアル時間）。
    private System.Collections.IEnumerator PlayAllyEnterFromFront(Vector3 fwd, float dur)
    {
        var units = new List<BaseEntity>(roundPlayerUnits);
        var home = new Dictionary<BaseEntity, Vector3>();
        foreach (var e in units)
        {
            if (e == null) continue;
            home[e] = e.transform.position;            // CompleteCurrentWave で home にスナップ済み
            e.transform.position = home[e] - fwd;       // 1マス後ろ（自陣側）から前進して定位置へ
            SetEntityAlpha(e, 0f);
        }
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            foreach (var e in units)
            {
                if (e == null || !home.ContainsKey(e)) continue;
                e.transform.position = home[e] - fwd * (1f - k);
                SetEntityAlpha(e, k);
            }
            yield return null;
        }
        foreach (var e in units)
        {
            if (e == null) continue;
            if (home.ContainsKey(e)) e.transform.position = home[e];
            SetEntityAlpha(e, 1f);
        }
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
        // 章ボスではないが強敵の中ボス。撃破でショップのコスト上限を解放し、候補から1体を章内解放させます。
        public bool IsMidBossWave;
        // midboss-nodes: 進路選択の「ノード枠」。戦闘前に NodeSelectionUI で選ばせ、選んだノードの内容を
        //   この枠（Enemies/RecruitCandidateIds/Reward）へ流し込んでから戦闘へ。選択完了で false に落とす。
        public bool IsNodeChoice;
        // R2-recruit: この中ボスで仲間化候補として提示するユニットID（2〜3体）。空ならステージ帯プールから自動選出。
        public readonly List<string> RecruitCandidateIds = new List<string>();
        // R2-rewards: 中ボス撃破時の報酬種別と付随パラメータ。
        public MidBossRewardKind RewardKind = MidBossRewardKind.Recruit;
        public int RewardCount = 1;   // ボス選択で選べる数（4-5=2）。
        public int RewardCoins = 0;   // コイン報酬額（4-9）。
        // R3-chest-room: アイテム報酬(3択)の回は、敵の代わりに宝箱を殴って開ける「チェスト部屋」にする。
        public bool IsChestRoom;

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

// midboss-nodes: 進路ノードのアーキタイプ（🔴精鋭 / 🟡標準 / 🟢補給）。
public enum MidNodeArchetype
{
    Elite,     // 高難度・最良報酬
    Standard,  // 中庸・仲間化
    Supply     // 易しめ・経済報酬
}

// R2-rewards: 中ボス撃破時の報酬種別。
public enum MidBossRewardKind
{
    Recruit,      // ボス選択（RewardCount 体まで）
    ItemChoice3,  // アイテム3択（防具/攻撃/秘力から1つずつ）
    BuffTile,     // 強化マス選択
    CoinReward    // 大量コイン
}

// R2-rewards: 強化マスの種別。
public enum BuffTileType
{
    Attack,   // 与ダメ↑・攻撃速度↑
    Defense,  // 被ダメ↓・シールド
    Arcane    // 攻撃速度大↑・与ダメ↑（秘力）
}

// ユニットがどちらの陣営に属しているかを表す列挙型です。
public enum Team
{
    Team1,
    Team2
}
