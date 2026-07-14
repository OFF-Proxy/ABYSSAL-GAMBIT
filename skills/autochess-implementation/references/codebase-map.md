# コードベース地図

「どこに何があるか」の早見表。新しい変更を入れる前に、関連箇所をここで当てる。

## ディレクトリ
- `Assets/Scripts/` — ゲームの C#（約38ファイル）。
- `Assets/Scripts/Save/` — 永続化レイヤー（`AutoChessBossRush.Save` 名前空間）。
- `Assets/Resources/Entity Database.asset` — **全ユニット定義**（name / cost / range / icon / frame / synergy1-3）。
- `Assets/Prefabs/Unit/` — ユニットのプレハブ（T1〜T5 / Neutral）。
- `Assets/Animations/<Unit>/` — クリップ＋ Animator Controller（Default/Move/Attack/Ability/Dead）。
- `Assets/Images/Units/Sprite/...` — スプライト本体（duelyst の plist をスライス）。
- `docs/` — 生きた設計・進行ドキュメント（ROADMAP / HANDOFF / COLLAB_PROTOCOL / QUESTIONS / DESIGN_*）。
- `reference/duelyst/` — アート参照元（gitignore 済み）。

## 主要スクリプトと責務
- **`GameManager.cs`（最大・中枢, ~3800行）**: 章/ラウンド構造（`BuildChapterRounds`→`BuildChapter1Rounds`、
  `WaveDefinition`）、経済（`PlayerData` 連携）、オーグメント所持・適用、スコア集計（`QueueStageResult` /
  `TrackStageProgress`）、セーブ連携（`SaveManager`）、ボス報酬（`SelectBossReward` / `unlockedBossRewardUnitIds`）、
  章ボス仲間化（`ChapterBossUnitIds` / `TryShowChapterRoster`）、ショップコスト解放（`MaxAvailableShopCost`）。
- **`BaseEntity.cs`（~4800行）**: ユニットの全挙動。スキル系は `UnitSkillType` enum、`ExecuteSkillEffect`、
  `TryExecuteDedicatedSkill`（固有スキル switch）、`ConfigureDefaultSkillType` / `IsXxxSkillUnit`（汎用割り当て）、
  ステータスは `ApplyBaseBalance`（コスト別基準値）/ `GetConfiguredBaseRange`（射程: 一覧の16体が4、他は1）。
- **`UnitStatusPanelUI.cs`**: スキル/ステータスの説明 UI。固有スキル文 `BuildXxxSkillText`（実数値・JA/EN）。
- **`SynergyType.cs`**: 18種シナジー（None,Warrior,Ranger,Arcanist,Guardian,Beast,Shadow,Machine,Wraith,Apex,
  Inferno,Frost,Storm,Abyss,Divine,Frenzy,Royal,Summoner,Alchemy）。`SynergyManager.cs` が判定・適用。
- **`AugmentCatalog.cs`**: オーグメント80種データ（Silver30/Gold25/Prism25）。適用は GameManager 側のフック。
- **`PlayerData.cs`**: 経済（基本収入＋利子）、所持金。
- **`Save/`**: `ISaveStore`（抽象）/ `LocalJsonSaveStore`（JSON実装）/ `SaveManager`（窓口・`EnsureExists`）/
  `SaveData`（DTO: chapters / bossAllies / bestScore 等）。Steam クラウドは `ISaveStore` 差し替えで対応予定。
- **UI 群**: `OptionsPanelUI` / `AugmentHudUI` / `AugmentTooltipUI` / `ResultPanelUI` / `RoundProgressUI` /
  `ScorePopupUI` / `ChapterRosterUI` / `UIShop` / `SynergyPanelUI` — いずれも `EnsureExists()` パターン。
- **`LocalizationManager.cs`**: JA/EN 切替（`IsJapanese` / `ApplyFont` / `UnitName`）。
- **`AttackEffectPlayer.cs`**: スキル/シナジー VFX・SFX・BGM。

## データの追加場所
- 新ユニット: `Entity Database.asset` に追加 ＋ プレハブ ＋ アニメ ＋ スプライト ＋（必要なら）固有スキル。
- 新シナジー: `SynergyType.cs` に enum 追加 ＋ `SynergyManager` に判定/効果。
- 新オーグメント: `AugmentCatalog.cs` に定義 ＋ GameManager に適用フック。
- 新章: `GameManager` の `BuildChapterRounds(chapter)` に分岐 ＋ `ChapterBossUnitIds` に章ボス登録。

## 既知の負債 / 注意
- `UnitStatusPanelUI` の旧定性スキル文（JA/EN switch）はフォールバックとして残置（到達しない）。剪定予定。
- 章は現状 Chapter 1 のみ実装。複数章は `BuildChapterRounds` の分岐追加で増やす設計。
- ボス仲間（cost5 Legion 等）は強力。バランスは R3-balance で調整前提。
