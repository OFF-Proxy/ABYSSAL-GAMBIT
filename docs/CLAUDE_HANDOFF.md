# Claude → Codex 引き継ぎ書

> Codex が前回チェックポイント `0f86ed9a (Save current auto battler prototype state)` から離れている間に Claude が積んだ全変更のまとめ。
> 役割分担：**設計＝Claude / 実装＝Codex**。本書を共有のソース・オブ・トゥルースとして扱う。
>
> 関連:
> - [ROADMAP.md](ROADMAP.md) — 全体方針と進行メモ（生きたドキュメント）
> - [docs/COLLAB_PROTOCOL.md](COLLAB_PROTOCOL.md) — Claude／Codex の協業プロトコル

---

## 1. ざっくり差分（base = `0f86ed9a`）

```
13 files changed, 2530 insertions(+), 262 deletions(-)
```

主要 cs の行数増分:
| ファイル | 行数差 | 主な追加内容 |
|---|---|---|
| `Assets/Scripts/GameManager.cs` | +1290 | チャプター/ラウンド構造、augment 全体、スコア追跡、召喚 augment、ベンチ拡張 |
| `Assets/Scripts/UnitStatusPanelUI.cs` | +541 | スキル説明の実数値化（自動生成） |
| `Assets/Scripts/UIShop.cs` | +239 | リロールコスト可視化、スタック機構、augment コスト3保証 |
| `Assets/Scripts/RoundProgressUI.cs` | +212 | ステージ表示・DOTween 遷移 |
| `Assets/Scripts/SynergyPanelUI.cs` | +169 | augment ボーナス「+N」バッジ、prism_all_synergy 表示、ソート |
| `Assets/Scripts/BaseEntity.cs` | +146 | augment ステータス、被ダメ軽減、on-hit proc、復活、kill heal |
| `Assets/Scripts/PlayerData.cs` | +54 | 経済ループ（基本5＋利子）、収入プレビュー、`baseRoundIncome`/`interestCap`/`interestPerGold` |
| `Assets/Scripts/SynergyTooltipUI.cs` | +44 | シナジー説明の数値透明化 |
| `Assets/Scripts/SynergyManager.cs` | +30 | augment emblem 加算、prism_all 倍化、+1 召喚 |
| `Assets/Scripts/AttackEffectPlayer.cs` | +26 | SFX/BGM volume static 制御 |
| `Assets/Scripts/LocalizationManager.cs` | +19 | 追加文字列、ApplyFont 共通化 |
| `Assets/Scripts/GridManager.cs` | +13 | ベンチタイル team 引数対応 |
| `Assets/Scripts/Draggable.cs` | +9 | `ActiveDragCount` の race condition 対策 |

新規スクリプト（メタファイル省略）:
- `Assets/Scripts/AugmentCatalog.cs` — 80 augments のデータ
- `Assets/Scripts/AugmentSelectionUI.cs` — 3択UI（カード別リロール付き）
- `Assets/Scripts/AugmentHudUI.cs` — 右上 HUD（アイコン表示）
- `Assets/Scripts/AugmentTooltipUI.cs` — augment ホバーツールチップ
- `Assets/Scripts/OptionsPanelUI.cs` — 音量/言語/速度/ヘルプ/再挑戦
- `Assets/Scripts/ResultPanelUI.cs` — ステージ/章クリアのリザルト
- `Assets/Scripts/RangedEntity.cs` — タイポ修正版（`RengedEntity.cs` を Delete）

新規アセット:
- `Assets/Prefabs/Unit/Neutral/Zyx.prefab` — 雑魚ユニット
- `Assets/Animations/Zyx/` — 5 クリップ + Animator Controller
- `Assets/Images/Units/Neutral/` — Zyx スプライト（reference の duelyst plist からスライス）
- `Assets/Resources/UI/Augment/` — Augment HUD・ツールチップ用 UI 素材（reference からコピー）

修正された Unity アセット:
- `Assets/Scenes/GameScene.unity` — `GameManager.spawnDebugItemsOnStart = false` 保存済み
- `Assets/Resources/Entity Database.asset` — Zyx（cost=0）追加
- `Assets/Prefabs/Unit/Unit.prefab` — Animator/Renderer 設定の調整（要確認）

---

## 2. 主要システム別の追加内容

### 2.1 チャプター・ラウンド構造（E1 完了）

**[GameManager.cs](../Assets/Scripts/GameManager.cs)** 内：
- `currentChapter` フィールド
- `InitializeWaveDefinitions()` → `BuildChapterRounds(int chapter)` → `BuildChapter1Rounds()` の階層
- **Chapter 1 = 4 stages × 33 ラウンド構成**:
  - Stage 1: zako 3 ラウンド
  - Stage 2: 通常戦＋中ボス 2-5/2-10、終わりにオーグメント (Silver) 2-3
  - Stage 3: 同様＋中ボス 3-5/3-10、オーグメント (Gold) 3-3
  - Stage 4: 中ボス 4-7/4-8/4-9、章ボス 4-10、オーグメント (Prism) 4-3
- `WaveDefinition` に `StageIndex` / `RoundInStage` / `IsMidBossWave` / `IsEventRound` を追加
- `WaveEventType` 列挙に `BonusItem` / `BonusGold` / `AugmentSilver` / `AugmentGold` / `AugmentPrism`
- `MaxAvailableShopCost`：初期3、中ボス/章ボス撃破で +1（上限5）
- `chapterStageScores` / `chapterStageTimes` でステージごとのスコア/タイムを追跡

### 2.2 経済ループ（E2 完了）

**[PlayerData.cs](../Assets/Scripts/PlayerData.cs)**：
- `baseRoundIncome`（初期 5）、`interestPerGold`（初期10）、`interestCap`（初期 5）
- `PreviewNextIncome` プロパティ — ショップに `+X` 表示用
- `startingMoney = 4`（デバッグ 999 を廃止）

PvE のため**連勝/連敗ボーナスは未実装**（仕様確定）。

### 2.3 オーグメント（E3 完了）

#### データ層
**[AugmentCatalog.cs](../Assets/Scripts/AugmentCatalog.cs)**:
- `enum AugmentTier { Silver, Gold, Prism }`
- `enum AugmentEffectKind { Stat, Synergy, Economy, Item, Combat, Special }`
- `class AugmentDefinition { Id, Tier, Kind, NameJa, NameEn, DescriptionJa, DescriptionEn }`
- `AugmentCatalog.All` / `ByTier(tier)` / `FindById(id)` 静的 API
- **総数 80（Silver 30 / Gold 25 / Prism 25）**

#### 状態管理
**[GameManager.cs](../Assets/Scripts/GameManager.cs)** の augment 関連 public/internal API:

```csharp
// 所有・選択履歴
public readonly List<AugmentDefinition> OwnedAugments;
public readonly HashSet<string> ShownAugmentIds;

// チーム共通バフ
public float TeamAttackBonusPercent { get; }
public float TeamHPBonusPercent { get; }
public float TeamDamageReductionBonus { get; }
public float TeamMoveSpeedBonusPercent { get; }
public float TeamAttackSpeedBonusPercent { get; }
public int BenchSlotBonus { get; }
public int EffectiveBenchSlotCount => benchSlotCount + BenchSlotBonus;
public int AugmentSynergyBonusWarrior { get; }
public int AugmentSynergyBonusRanger { get; }
public int AugmentSynergyBonusArcanist { get; }
public bool AugmentAllCostsUnlocked { get; }
public float ScoreMultiplier { get; }
public int ExtraExpPerWaveClear { get; }

// 戦闘単位の状態
public readonly Dictionary<SynergyType, int> AdditionalSynergyBonusThisCombat;
public int AugmentSilverRevivesSpentInChapter { get; }

// 主要メソッド
public bool HasAugment(string id);
public int CountOwnedUnitsByUnitId(string unitId);
public bool IsHighestCostOnBoard(BaseEntity entity);
public int CountSameCostBoardAllies(int cost);
public bool TryConsumeAugmentReviveForUnit(BaseEntity entity);
public void NotifyEnemyKilledByPlayer(BaseEntity killed, BaseEntity killer = null);
public BaseEntity SpawnAugmentEliteSummon();
private void ApplyBattleStartAugmentEffects();
private void ApplyAugmentEffect(AugmentDefinition aug);
private void ApplyPrismWarriorKillBuffAtBattleStart();
private void ApplyAugmentSummonBonuses(BaseEntity summon);
```

#### 効果フック箇所一覧
| 効果カテゴリ | 主な実装場所 |
|---|---|
| **戦闘開始バフ** | `GameManager.ApplyBattleStartAugmentEffects()` |
| **ステータス倍率** | `BaseEntity.ApplyCurrentStats()` 内のチーム倍率ブロック |
| **被ダメ軽減** | `BaseEntity.GetTotalDamageReduction()` |
| **on-hit proc**（slow/burn/zap/stun） | `BaseEntity.TryApplyAugmentOnHitProcs()` |
| **復活** | `BaseEntity.Die()` 前段 → `GameManager.TryConsumeAugmentReviveForUnit()` |
| **撃破フック**（kill heal / 戦士キル蓄積） | `BaseEntity.Die()` → `GameManager.NotifyEnemyKilledByPlayer(killed, killer)` |
| **シナジー上乗せ** | `SynergyManager.GetSynergyCount()` + `CountSynergiesForTeam()` |
| **召喚体強化** | `GameManager.ApplyAugmentSummonBonuses()` → `BaseEntity.ApplyAugmentSummonStatMultipliers()` |
| **ショップ・リロール** | `UIShop.GetEffectiveRefreshCost()` / `TryGetForcedHighCostEntity()` / `GenerateCard` 末尾分岐 |
| **ウェーブクリア時** | `GameManager.CompleteCurrentWave()` 内の augment ブロック（item drop, alchemy, star2 bonus 等） |
| **ボス報酬選択** | `GameManager.GetBossRewardOptions()` の prism_boss_reward_extra 分岐 |

#### 全 augment 効果の網羅状況
**80 種全て**実装済み。残課題ゼロ。詳細は `AugmentCatalog.cs` の switch‐case／`ApplyAugmentEffect` を参照。

### 2.4 UI 群

#### AugmentHudUI（右上常時表示）
- `Assets/Resources/UI/Augment/` 配下の `rarity_*` / `kind_*` スプライトを使用
- 1 マスは「ティアグロー + レアリティ枠 + 効果カテゴリの中央アイコン」、テキストはツールチップへ
- ホバー/クリックで `AugmentTooltipUI.Show()`

#### AugmentSelectionUI（イベントラウンド時の3択）
- カード別リロール（1 回まで）対応
- 取得済み augment と現在表示中 augment を除いた重複なしプール
- DOTween フェード、ティアカラー縁取り

#### AugmentTooltipUI
- タイトル左に kind icon、上部にティアバー
- preferredHeight に応じてパネル高さ自動調整

#### OptionsPanelUI
- Master/BGM/SFX 音量スライダー、ミュート、言語、速度、ヘルプ、再挑戦
- ESC 開閉、DOTween アニメ、PlayerPrefs 永続化

#### ResultPanelUI
- `ShowStageResult(stage, seconds, score, breakdown, isChapterClear)`
- `Time.timeScale = 0` 中、フォーマットは `mm:ss`

#### RoundProgressUI
- `RoundKind { Combat, Event, MidBoss, Boss }`
- `SetStageProgress(stage, roundInStage, kinds, gameOver, allClear)`
- ステージ切替で左へスライド→中身差替→右からスライドイン（OutBack）
- アイコン：丸＝Combat / 四角＝Event / ひし形＝Boss・MidBoss

#### SynergyPanelUI
- **発動順ソート**：active 中→未発動、各セクション内はユニット数の多い順
- 各行に「+N」augment バッジ（emblem + ランダム combat 加算）
- `prism_all_synergy` 所持時はヘッダーに「★ 全シナジー +1 重ね掛け」を紫表示

#### UIShop（リロール周り）
```csharp
public int GetEffectiveRefreshCost();   // prism永続 → スタック → gold今回 → silver割引 → 通常
public int GoldFreeRerollStacks;        // 繰越ストック数
public void RequestFreeRerollOrPending();  // ウェーブクリア時の無料リロール
public void RefreshRerollButtonCostText(); // 「2/1/0/無料/FREE」自動切替＋スタックバッジ
```
- ボタン右上に `Resources/UI/Augment/badge_counter` でスタック数バッジ
- `NormalizeShopTextLayout` が「FREE」「無料」もコストテキスト扱い（ラベル化されない）

### 2.5 雑魚ユニット（Zyx）

- **Prefab**: `Assets/Prefabs/Unit/Neutral/Zyx.prefab`
  - HP 21, dmg 3, range 4, atkSpd 0.42, moveSpd 0.63
  - `maxMana = 9999` / `manaOnAttack = 0` / `manaOnDamageTaken = 0` → スキル詠唱なし
- **Animations**: `Assets/Animations/Zyx/` に 5 クリップ + コピー作成した Animator Controller（Andromeda ベースで motion 差替）
- **Entity Database**: cost=0 で登録、`IsLegionOnlySummonData()` に追加してショップ・召喚から除外
- **ドロップ**: `EnemyDrop` 構造体 + `WaveEnemyPlacement.DropCoins`/`DropItem` で配置単位の報酬指定可能

### 2.6 スキル/シナジー説明の透明化（E5 完了）

- **UnitStatusPanelUI**: 28 体分のスキル説明を、現在の `skillBasePower` / `skillSlowMultiplier` / `skillBoostMultiplier` 等から実数値化（自動生成）。元の JA/EN switch はフォールバックとして残置（到達しない）。
- **SynergyTooltipUI**: シナジー proc の実数値を全シナジーに記載。

### 2.7 ベンチ拡張

`benchSlotCount + BenchSlotBonus = EffectiveBenchSlotCount` を全箇所に伝播：
- `HasBenchSpace` / `GetFreeBenchSlot` / `CanPlaceEntityOnBench` / `GetBenchSlotAtWorldPosition` / `GetBenchTileAtSlot`
- `EnsureExtraBenchTiles()` が既存タイル間隔を推定して左右ベンチに動的に追加生成

### 2.8 ドラッグ race condition 対策

**[Draggable.cs](../Assets/Scripts/Draggable.cs)**：
- `public static int ActiveDragCount`
- ベンチユニットを掴んだままウェーブクリア → 無料リロールが中途半端に走る既知バグを、**ActiveDragCount > 0 中はリロールを `pendingFreeReroll` に保留** → `OnEndDrag` で `UIShop.ConsumePendingFreeReroll()` 呼び出しで解消

---

## 3. アセット由来情報（reference 由来）

> **重要**: `reference/` から素材を使う場合は必ず `Assets/` にコピーする。`reference/` を直接参照しない。

### 3.1 Zyx スプライト
- `reference/duelyst/.../neutral_zyx.png` + plist → `Assets/Images/Units/Neutral/` に 97 フレームをスライス（Y反転）

### 3.2 Augment UI 素材
`Assets/Resources/UI/Augment/` (全て Sprite Importer 設定済み、PPU=100):

| ファイル | reference 由来 | 用途 |
|---|---|---|
| `rarity_silver.png` | `collection_card_rarity_common` | Silver augment 枠 |
| `rarity_gold.png` | `collection_card_rarity_epic` | Gold augment 枠 |
| `rarity_prism.png` | `collection_card_rarity_mythron` | Prism augment 枠 |
| `card_panel.png` | `card_background` | パネル背景（9-slice） |
| `augment_frame.png` | `artifact_frame` | 枠線 |
| `badge_counter.png` | `icon_cooldown_counter` | リロールスタックバッジ、シナジー +N バッジ |
| `kind_stat.png` | `icon_f1_aegisbarrier` | Stat 系 augment アイコン |
| `kind_synergy.png` | `icon_f1_blessing` | Synergy 系 |
| `kind_economy.png` | `icon_gold`（ui） | Economy 系 |
| `kind_item.png` | `icon_deck` | Item 系 |
| `kind_combat.png` | `icon_f1_truestrike` | Combat 系 |
| `kind_special.png` | `icon_f1_invincible` | Special 系 |
| `status_panel.png` | `status_panel` | 予備（未使用） |
| `icon_gold.png`/`icon_atk.png`/`icon_hp.png`/`icon_heal.png`/`icon_mana.png` | 同名 ui | 今後の流用用 |

---

## 4. Codex が知っておくべき gotcha

### 4.1 `BaseEntity.baseHealth` の二重用途
- `baseHealth` は「**基準 HP の生値**」と「**現在 HP**」を同じフィールドで持つ既存仕様。
- `originalBaseHealth` が基準値、`baseHealth` が現在値。
- ステータス更新時は `previousHealthRatio` を保存して再適用するパターン（`ApplyCurrentStats` 内）。
- 召喚体に倍率を乗せる時は `originalBaseHealth` を書き換える（`ApplyAugmentSummonStatMultipliers` 参照）。

### 4.2 シナジー数の三層構造
1. `synergyCounts[type]`（生のユニット数。prism_all_synergy で *2 されている可能性あり）
2. `GetSynergyCountForTeam(type, team)` — dict 直返し（emblem は含まれない）
3. `GetSynergyCount(type)` — プレイヤーチームに emblem + random combat を加算

UI 表示用は `GetSynergyCount(type)` を呼ぶ。デバフ・proc 計算用に**ピンポイントで dict 直接参照**しているコードもあるので、変える時は両方追随。

### 4.3 イベントラウンド進行フロー
- `TryStartEventRound()` は **複数箇所から呼ばれる**：`Start()` / ボス報酬完了 / ウェーブクリア / `FIGHT` ボタン直前
- イベントが連続する可能性があるので、消化後に再帰的に同関数を呼ぶ
- augment イベントは `augmentSelectionPending` フラグで「選択完了待ち」になる

### 4.4 召喚体（IsSummonedUnit）の扱い
- シナジー計算・合成・売却対象から外す。
- `team1Entities` には入るが、`OnRosterChanged`/upgrade scope のフィルタで除外される。
- 一時召喚体には `BeginTemporarySummonLifetime(duration)` を必ず付ける。
- 戦闘終了で `ClearTemporarySummons()` が一括撤去。

### 4.5 Unity Mono.CSharp 評価器のロック
Editor 内 `mcp__unity-synaptic__run_csharp` を多用すると、`"builder already exists"` でロックすることがある（既知）。
- 対処：Editor フォーカスでドメインリロードを発生させる、もしくは Editor 再起動。
- コンパイル結果は `~/AppData/Local/Unity/Editor/Editor.log` の `Compilation completed (Errors: ...)` を grep する。

詳細：`memory/mono-csharp-stuck-quirk.md`（Claude 側のメモリ）。

### 4.6 `Mono.CSharp` interactive の制限
- `new List<T>()` 等のジェネリックインスタンス化が静かに失敗
- 複文の戻り値が消えることがある
- 回避：配列／ArrayList／既存ジェネリックメソッド呼び出し

### 4.7 PrefabUtility の信頼性
- `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` は run_csharp 経由で**静かに失敗**することがあった。
- 重要な prefab 編集は `.prefab` YAML を直接編集して回避済み（Zyx.prefab はこのルートで作成）。

---

## 5. 残課題マップ（Claude→Codex で引き継げるもの）

[ROADMAP.md](ROADMAP.md) と [今後の設計案セクション] と一致する整理：

### A. 直近の仕上げ
- [ ] **A-1** AugmentSelectionUI の状態リセット（連続イベント時のプール参照）
- [ ] **A-2** ベンチ拡張時の新タイル登場演出（DOTween スケール弾ませ）
- [ ] **A-3** UnitStatusPanelUI の旧 JA/EN switch 文（フォールバック）剪定
- [ ] **A-4** 利子ゲージ UI（10/20/30/40/50 で視覚 cue）

### B. コンテンツ拡張
- [ ] **B-1** チャプター2 構築（`BuildChapter2Rounds()` + 章固有ルール）
- [ ] **B-2** イベントラウンドの選択UI化（Ch1 の Augment 以外も多択）
- [ ] **B-3** 使われていないユニットのリトリアル

### C. スコアリング & ランキング
- [ ] **C-1** スコアシステム定式化（wave × difficulty × speed × itemPenalty + boss + starUp + augment + noDeath）
- [ ] **C-2** スコア加算の瞬間のライブ表示（CombatNumber 流用のトースト）
- [ ] **C-3** `IScoreRepository` を切ったローカル実装＋リーダーボードUI

### D. 演出
- [ ] **D-1** 4 コストボススキルの迫力強化（チャージ円・画面シェイク・着弾フラッシュ）
- [ ] **D-2** シナジー発動の上部バナー
- [ ] **D-3** SE/BGM の差分追加

### E. 完成度・公開準備
- [ ] **E-1** チュートリアル（初回のみ吹き出し3つ）
- [ ] **E-2** RunState セーブ・再開
- [ ] **E-3** メタプログレッション（任意・小規模）
- [ ] **E-4** ローカライズキー化の徹底
- [ ] **E-5** ビルド・配布形態の選定

### Zako P6（プレイテスト後）
- [ ] 雑魚ラウンドの難易度調整

---

## 6. 規約（Claude/Codex 共通）

1. **reference/ を直接参照しない**。素材は必ず `Assets/` にコピー、Sprite Importer 設定まで完了させる。
2. **チャプターごとの `BuildChapterNRounds()` パターンを踏襲**。既存形と合わせて augment イベントの章内位置を揃える。
3. **`HasAugment("id")` を介して augment 効果を分岐**。文字列キーを直書きする場合は `AugmentCatalog.FindById` で存在チェックすると安心。
4. **シーン編集（GameScene.unity / prefab）**は Codex/Claude どちらが触っても良いが、変更点を本書に追記。
5. **コミット**：日本語で要点を1行＋詳細。`docs/CLAUDE_HANDOFF.md` を更新したら一緒にコミット。

---

## 7. 既知バグ（修正済み）

| 症状 | 修正内容 | コミット予定 |
|---|---|---|
| ベンチユニットを掴んだまま無料リロールが走り、ショップ表示が壊れる | `Draggable.ActiveDragCount` 導入＋`UIShop.pendingFreeReroll` で保留 | Claude 作業中 |
| ゲーム開始時に全アイテムを所持している | `GameManager.spawnDebugItemsOnStart = false` をシーンへ保存 | 〃 |
| シナジー表示順がランダム | active 優先＋ユニット数降順に変更 | 〃 |
| Zyx の Animator が「Default」ステートを持たない | controller の Idle ステートのモーション差替で対応 | 〃 |
| Zyx の Animator m_Name が "Andromeda" のままで警告 | controller YAML を直接 `m_Name: Zyx` に変更 | 〃 |

---

## 8. 連絡用フォーマット（Codex への依頼テンプレ）

```
## 設計（Claude）
- ゴール: <一行>
- 影響範囲: <ファイル/クラス/メソッド>
- 既存依存: <壊さないでほしいAPI>
- 受け入れ基準: <デモで確認したい挙動>

## 実装（Codex）
- 入力する変更:
- 検証:
  1. Unity の Compilation completed (Errors: False) を確認
  2. (任意) シーンを開いて X を操作 → Y が表示される
- 注意点:
```

---

最終更新: 2026-05-29 (Claude)
