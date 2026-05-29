# AutoChessBossRush 開発ロードマップ

> Codex から引き継いだプロトタイプを、ローグライク・オートチェスとして完成させるための計画。
> このファイルは Claude と共有する生きた設計メモ。自由に編集・追記してOK。

## ビジョン

チャプター制で進行するローグライク・オートチェス（ボスラッシュ）。
序盤はコスト3以下のユニットしか出ず、ボス戦やイベントラウンドでコスト4ユニット・アイテム・オーグメントを集めながら強化していき、最終局面でコスト5ユニットに挑む。
チャプターごとにクリアタイム/スコアを記録してランキング化する。

---

## エピック（実装フェーズ）

### E1. チャプター & ラウンド構造（基盤）
- 全ラウンドを ~30 に拡張し、チャプターに分割（提案: 3章 × 約10ラウンド）。
- ラウンド種別を導入: 通常戦 / ボス戦 / イベント（アイテム選択・ユニット獲得・オーグメント選択・ショップ強化など）。
- チャプターごとの最終ボス（章1=◯◯, 章2=△△ …）。
- ショップのコスト解放ゲート: 序盤コスト3まで → ボス/イベントでコスト4解放 → 終盤コスト5。
- **依存: 現状の flat な waveDefinitions を一般化。多くの機能の土台。**

### E2. 経済: 利子（TFT式）
- 所持金に応じた利子（10%ごと、上限 +5/ラウンド 等）、勝敗ゴールド、連勝/連敗ボーナス。
- お金管理の駆け引きを体験させる。比較的独立して実装可能。

### E3. オーグメント（TFT式）
- 特定ラウンドで3択提示。レアリティ3段階（シルバー/ゴールド/プリズム等）。
- 効果適用フック（戦闘開始・経済・シナジー・特定ユニット強化など）。中〜大規模の新システム。

### E4. スコアリング & ライブ表示 & 記録/ランキング
- スコア要素: スターアップ、アイテム不使用でのウェーブ突破、クリアタイム、連勝、ノーデス 等。
- プレイ中に「スコア加算の瞬間」をポップアップで可視化。
- チャプターごとにタイム/スコアを記録 → ランキング表示。
- **依存: E1。永続化レイヤーが新規に必要（当初スコープ外だったセーブが、ランキングのため必須化）。まずローカル保存を想定。**

### E5. スキル / シナジー説明の透明化
- スキル説明にダメージ量・回復量・シールド量の実数値を明記（未記載のものを洗い出す）。
- シナジー説明も同様に数値を明記。
- できればステータス連動の数値から説明文を自動生成し、数値とテキストがズレないようにする。

### E6. 演出強化（VFX / SFX）
- コスト5未満、特に **コスト4ボスユニット** のスキル演出を満足度の高い迫力ある演出に。
- シナジー発動の演出強化。
- AttackEffectPlayer の拡張。反復的・最後の磨き込みフェーズ向き。

---

## 主要な決定事項（2026-05-28 確定）
- **チャプター構成: 1チャプター = 30ラウンドの長期戦**。複数チャプターを用意し、章ごとに最終ボスを変える（章1=◯◯, 章2=△△ …）。長く遊べることを重視。
- **ランキング: オンラインも視野に**。まずローカル保存で実装しつつ、永続化層を抽象化して後からバックエンド（リーダーボード）へ差し替え可能な設計にする。
- 着手順: **E5（説明の透明化）を最初に**着手。以降は順次相談。
- オーグメントの規模・点数は E3 着手時に決定。

## 進行メモ
- 2026-05-27: wave 6 → 12 に暫定拡張済み。E1 のチャプター枠組みへ統合予定（1章=30ラウンドへ再編）。
- 2026-05-28: **E5 完了** — 固有スキル全28体の説明を実数値化（UnitStatusPanelUI に専用ビルダー追加）、シナジー proc の実値もツールチップへ明記（SynergyTooltipUI）。
- 2026-05-28: **E2 経済ループ実装** — ウェーブクリア収入＝基本5＋利子（10ごと+1, 上限5）＋連勝ボーナス。所持金デバッグ999→初期4（PlayerData.startingMoney で調整可）。※収入内訳のUI表示は E4 のライブ表示で実装予定。
- 2026-05-28: フィードバック反映 — 開始時にランダムなコスト1ユニットを1体付与（手詰まり防止 / GameManager.GrantStartingUnit）、PvEのため**連勝ボーナスを撤廃**（収入＝基本＋利子のみ）。
- 2026-05-28: **E1 着手①** — 進行連動のショップ・コスト解放ゲートを実装。`GameManager.MaxAvailableShopCost`（初期3、ボス撃破ごと +1、上限5）で UIShop の抽選コストを制限。`UnlockNextShopCostTier()` はイベントからも呼べる。
- 2026-05-28: **E1 着手②** — ラウンド種別を導入。`WaveDefinition` を「ラウンド定義」に一般化し、戦闘なしの**イベントラウンド**（`WaveEventType`: BonusItem / BonusGold）を追加。`TryStartEventRound()` が前ウェーブクリア後・ボス報酬選択後・開始時・FIGHT時に自動消化し、報酬を渡して次ラウンドへ進める。デモとして3イベント（ボス前アイテム/中盤ゴールド/最終ボス前アイテム）を挿入。`UnlockNextShopCostTier()` をイベントから呼べばコスト解放イベントも作れる。
- 2026-05-28: **E1 着手③（核を完成）** — チャプター制を導入し、**チャプター1=全30ラウンド**を構築（通常19・イベント8・ボス3＝R10/R20/R30）。`InitializeWaveDefinitions`→`BuildChapterRounds(chapter)`→`BuildChapter1Rounds()` でチャプター追加が容易な構造に（`currentChapter` フィールド）。コスト解放：ボス1→4、ボス2→5。ボス報酬は**解放済みを除外**するよう改善し、3ボスで報酬3体（Snowchasermk/Solfist/Maehvmk）を順に集められる。
- 2026-05-28: **雑魚機能 P1-P4 完了** — (P1) reference/duelyst の neutral_zyx.png を Assets にコピー＆plistから97フレームをスライス（Y反転）。(P2) `Assets/Prefabs/Unit/Neutral/Zyx.prefab` 作成（HP30/dmg4/range4/atkSpd0.6, maxMana9999で詠唱無効, idle静止スプライト）。Entity DatabaseにZyx(cost=0)を登録、`IsLegionOnlySummonData` にZyxを追加してショップ・召喚から除外。(P3) `EnemyDrop` 構造体＋`WaveEnemyPlacement.DropCoins/DropItem` を追加。SpawnWaveEnemy で登録、UnitDead で付与。(P4) チャプター1を 30→**33ラウンド・4ステージ構造**に刷新: Stage1=雑魚3 / Stage2-4=各10。中ボス2-5,2-10,3-5,3-10,4-7,4-8,4-9（撃破でコスト上限+1）、章ボス4-10（報酬選択）。WaveDefinition に `StageIndex`/`RoundInStage`/`IsMidBossWave` 追加。
- 2026-05-28: **雑魚機能 P5 完了** — `RoundProgressUI` をステージ単位表示に刷新。`SetStageProgress(stage, roundInStage, kinds, ...)` に切替。タイトルは「ラウンド/中ボス/章ボス/イベント X-Y」表記。**ステージ切替時にDOTweenでスライド+フェード遷移**（左に流す→中身差替→右からスライドイン、タイトルも軽く弾ませる）。アイコンは Combat=丸/Event=四角/Boss・MidBoss=ひし形、種別ごとに色分け。
- 2026-05-29: **E3 オーグメント完全実装** — 残課題だった複雑系も着地。
  - **召喚体系3種**: `gold_summon_dmg`（+30% 与ダメ）/`prism_summon_master`（+30% HP + 召喚体+1）/`gold_elite_summon`（戦闘開始時にランダムなコスト3を一時加勢）。`GameManager.ApplyAugmentSummonBonuses` で `SpawnTemporarySummonFromSynergy`/`SpawnTemporarySummonByUnitName`/`SpawnAugmentEliteSummon` に共通注入し、`BaseEntity.ApplyAugmentSummonStatMultipliers` で `originalBaseHealth`/`originalBaseDamage` を一括拡張。
  - **`prism_all_synergy`**: `SynergyManager.CountSynergiesForTeam` でプレイヤーチーム時のみ、各ユニットの所持シナジーを 2 倍カウント。
  - **`prism_warrior_kill_buff`**: 戦士の撃破者を UnitId キーで蓄積（`warriorKillBuffPendingByUnitId`）。`Die()` から `NotifyEnemyKilledByPlayer(killed, killer)` でキラーを伝搬。次戦闘開始時に `ApplyPrismWarriorKillBuffAtBattleStart` がキル数×30%（上限300%）の時限与ダメバフを各戦士へ付与。
  - **ベンチ拡張3種** (`silver/gold/prism_bench_*`): `EffectiveBenchSlotCount = benchSlotCount + BenchSlotBonus` 化。`HasBenchSpace`/`GetFreeBenchSlot`/`CanPlaceEntityOnBench`/`GetBenchSlotAtWorldPosition`/`GetBenchTileAtSlot` を実効値で参照。`EnsureExtraBenchTiles` が既存タイル間隔を推定して左右ベンチへ動的に追加生成。
- 2026-05-29: **オーグメント可視化パス完了** — 効果を「数字や HUD で見える化」する一斉対応。
  - **AugmentHudUI**: 画面右上に獲得済みオーグメントをティアカラー（Silver=銀/Gold=金/Prism=紫）で並べる常時表示の HUD。ホバー/クリックで AugmentTooltipUI が説明を表示。Reference duelyst の `collection_card_rarity_*` を `Assets/Resources/UI/Augment/rarity_*` にコピーして使用。
  - **AugmentTooltipUI**: ティアバー付きの説明ポップアップ（reference の `card_background` を panel sprite に流用）。
  - **リロールボタン**: `RefreshRerollButtonCostText()` が augment 状況に応じて数字を「2/1/0/無料/FREE」に動的書き換え。`NormalizeShopTextLayout` が "FREE"/"無料" を cost text と判定するよう更新し、ラベル化されないようにケア。
  - **無料リロールのスタック化**: `gold_free_reroll` を消費しなかった場合、ラウンドクリア時に `goldFreeRerollStacks++`。リロールボタン右上に reference の `icon_cooldown_counter`（`badge_counter`）でストック数バッジ `x N` を表示。消費順は「prism 永続 → スタック → 今ラウンドの gold_free_reroll」。
  - **シナジーパネル**: 各シナジー行に「+N」バッジを追加（emblem 系 + 戦闘中ランダム上乗せ分）。`prism_all_synergy` が有効なときはヘッダーに「★ 全シナジー +1 重ね掛け」を紫表示。
  - **デバッグアイテム配布の停止**: シーン上 `GameManager.spawnDebugItemsOnStart` を `False` に保存 → ゲーム開始時のアイテムベンチは空。
- 2026-05-29: **ビジョン確定 & リリース計画策定**（Claude）。プラットフォーム＝**Steam 買い切り**、ソーシャル＝**ランキング中心の軽い繋がり**、形態＝**アドベンチャー進行型（数十章＋ボス仲間化メタ進行＋東方風ボス掛け合い）**を正面から作ると確定。市場調査と差別化方針は [MARKET_POSITIONING.md](MARKET_POSITIONING.md)、リリース工程と task-id は [RELEASE_PLAN.md](RELEASE_PLAN.md) に集約。
- 2026-05-29: **クリティカルパス設計書3本を起票**（Claude）。実装順は R1-persist → R1-meta → R1-score。
  - [DESIGN_R1-persist.md](DESIGN_R1-persist.md) — 永続化層（ISaveStore/LocalJsonSaveStore/SaveManager）。章進捗・所持ボス仲間・ベストスコアを保存、Steam差し替え可能に。**最初に実装**。
  - [DESIGN_R1-meta.md](DESIGN_R1-meta.md) — ボス仲間化の永続roster＋章前編成UI。既存 `SelectBossReward`/`unlockedBossRewardUnitIds`（ラン内）を永続へ拡張。**差別化の核**。
  - [DESIGN_R1-score.md](DESIGN_R1-score.md) — 既存 `QueueStageResult` を明文化＋ベスト保存＋ライブ加点ポップアップ＋リザルトのベスト併記。
  - 注: 「複数チャプター」は単なる量産でなく、**ボス仲間化メタ進行＋物語**として再定義（RELEASE_PLAN の R1-meta / R2-chapters）。
- 2026-05-30: **R1 クリティカルパス3本を実装**（Claude）。Save 抽象化 → 章ベスト保存＋ポップアップ → ボス仲間 roster までを一気通貫で繋いだ。
  - `Assets/Scripts/Save/`（`ISaveStore` / `LocalJsonSaveStore` / `SaveManager` / `SaveData`）追加。`Application.persistentDataPath/save.json` に原子的書き込み、破損時は `save.corrupt.json` へ退避。`SaveManager.EnsureExists()` を `GameManager.Start` の最上段で起動。
  - `ScorePopupUI` 新規。`TrackStageProgress` 内で戦闘/中ボス/章ボスのクリア時に「+100 / +300 / +1000」を即時フロート表示（JA/EN、stacking）。
  - `QueueStageResult` の `isChapterClear` 分岐で `SaveManager.RecordChapterResult(currentChapter, totalScore, totalTime, true)` を呼び、戻り値の `isNewRecord` と `previousBest` を `pendingResult*` 経由で `ResultPanelUI.ShowStageResult` へ伝搬。`ResultPanelUI` を再レイアウト（panel 高さ 520→580）し、章クリア時のみベスト行と「★ NEW RECORD!」バッジを表示。
  - `GameManager.ChapterBossUnitIds`（chapter→unitId map、章1=`Legion`）を導入し、章クリアで `SaveManager.AddBossAlly` を呼ぶように。
  - `ChapterRosterUI` 新規（`BossRewardSelectionUI` ベースに「連れて行かない」スキップボタンを追加）。`GameManager.Start` で `TryShowChapterRoster` を `GrantStartingUnit` 直後に呼び、`BossAllies` 非空のときのみ展開→選択時 `CreateBenchEntity(data, 1)` でベンチに★1配置。
  - 永続化テスト観点: 章1クリア→再起動で `Legion` が roster に残り、開始時に選択するとベンチに加わる。ベスト/タイムも保持。
- E1 残（ポリッシュ）: ①複数チャプター、②イベントの**選択UI化**、③旧定性スキル文フォールバックの剪定。雑魚機能 P6（難易度調整）はプレイテスト後の反復作業。
- 残課題メモ: UnitStatusPanelUI の旧定性スキル文（JA/EN switch）は現状フォールバックとして残置（到達しない）。後で剪定する。
