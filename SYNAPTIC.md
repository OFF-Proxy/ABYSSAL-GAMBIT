# プロジェクト概要

- **プロジェクト名**: ABYSSAL GAMBIT（リポジトリ名 = `AutoChessBossRush`。Unity の productName は暫定「AutoChess Boss Rush (Alpha)」で正式名へ改称予定）
- **ジャンル**: ローグライク・オートチェス（ボスラッシュ）。チャプター制で進行し、章ボスを倒すと**仲間になり次章以降の編成で使える**メタ進行が差別化の核
- **形態**: Steam 買い切り（シングルプレイ・PvE専用）。オンライン要素なし（ランキングは将来オンライン化を視野にローカル保存のみ）
- **対応プラットフォーム**: **PC のみ**（Windows x64。Steam向け）。モバイル対応は無い・予定もない
- **対応言語**: 日本語 / 英語（全UIが JA/EN 両対応必須）
- **開発フェーズ**: 全20章＋主要システムは実装済み。現在は「実機検証・バランス調整（R3-balance）・Steam審査準備」段階。アルファ版ビルド（`AutoChessBossRush_v0.1.0-alpha_win64.zip`）配布実績あり
- **開発体制**: 設計=Claude / 実装=Codex・Claude Code の分離運用（`docs/COLLAB_PROTOCOL.md`）。進行の真実のソースは `docs/ROADMAP.md` の進行メモ

# アーキテクチャと技術スタック

- **エンジン**: Unity（2D・C#）。ソリューション `AutoChessBossRush.sln` / `Assembly-CSharp.csproj`
- **主要ライブラリ**: DOTween（全トゥイーン。ポーズ中も動かすUIは `SetUpdate(true)`）
- **アート素材**: OpenDuelyst（CC0-1.0、商用可・帰属不要）。`reference/duelyst/` が参照元（gitignore済み・**直接参照禁止**、必ず `Assets/` へコピーして使用）。ユニットは plist をスライスしたスプライトシート
- **UI**: ほぼ全てプログラム生成（シーンに置かない）。シングルトンUIは `EnsureExists()` パターン（`Instance` + `FindObjectOfType` フォールバック）で独立ルート Canvas を自前生成
- **永続化**: `JsonUtility` + `List<T>`（Dictionary不可）。`Application.persistentDataPath/save_{0..2}.json` に原子的書き込み、破損時 `save.corrupt.json` 退避。名前空間 `AutoChessBossRush.Save`。Steamクラウドは `ISaveStore` 差し替えで対応予定（未統合）
- **ディレクトリ特記**:
  - `Assets/Scripts/` … C# 73本がフラット配置（分類方針は確定済みだが一括移動は未実施）
  - `Assets/Scripts/Save/` … 永続化層 / `Assets/Scripts/Debug/` … 開発専用（AutoPlayHarness等）
  - `Assets/Resources/Entity Database.asset` … **全ユニット定義**（84体超: name/cost/range/icon/synergy1-3）
  - `Assets/Prefabs/Unit/{T1..T5,Hero,Neutral}/` … ユニットprefab / `Assets/Animations/<Unit>/` … クリップ+AnimatorController（Default/Move/Attack/Ability/Dead）
  - `Assets/Resources/UI/`・`Assets/Resources/music`・`Assets/Resources/sfx` … UI素材・音源（Resources.Load 前提）
  - `docs/` … 生きた設計ドキュメント群（ROADMAP / DESIGN_* / QUESTIONS / STEAM_READINESS_STANDARDS 等）

# コードマップ（BOTの現地読み込み用インデックス）

症状・話題 → まず読むファイル（すべて `Assets/Scripts/` 配下）:

| 症状・話題 | 見るべきファイル |
|---|---|
| 章/ラウンド進行、ウェーブ定義、イベント回、経済、オーグメント適用、報酬、セーブ連携、ヒーロー付与、勝敗判定 | `GameManager.cs`（**中枢・約8,000行**。章構築は `BuildChapterRounds`→`BuildChapter1Rounds`〜`BuildScaledFactionChapter`） |
| ユニットの移動/攻撃/被ダメ/スキル/スター/召喚/コア/宝箱挙動 | `BaseEntity.cs`（**約7,400行**。固有スキルは `TryExecuteDedicatedSkill` の switch） |
| 近接/遠隔の行動ループ | `MeleeEntity.cs` / `RangedEntity.cs`（`GetInRange`/ターゲット選択は BaseEntity 側） |
| スキル・ステータス説明文（実数値生成） | `UnitStatusPanelUI.cs` |
| シナジー判定・効果・陣営ゲート | `SynergyManager.cs` / `SynergyType.cs`（25種）、表示は `SynergyPanelUI.cs`・`SynergyTooltipUI.cs` |
| オーグメント（80種データ / 3択UI / HUD） | `AugmentCatalog.cs` / `AugmentSelectionUI.cs` / `AugmentHudUI.cs`・`AugmentTooltipUI.cs`（適用フックは GameManager） |
| ショップ（購入/リロール/ロック/レイアウト） | `UIShop.cs` / `UICard.cs` / `UnitCardVisual.cs`（Duelyst風レイアウトは `GameHudLayout.cs`） |
| 経済数値（基本収入+利子） | `PlayerData.cs`、内訳UIは `CoinIncomePanelUI.cs` |
| セーブ/スロット/ボス仲間/ストーリーフラグ | `Save/SaveManager.cs` / `Save/SaveData.cs` / `Save/LocalJsonSaveStore.cs` / `Save/ISaveStore.cs` |
| タイトル/ロビー/スロット選択/章選択/図鑑/ヒーロー育成・選択 | `LobbyUI.cs`（約2,400行）、ハブは `CollectionHubUI.cs`、ヒーロー変更は `HeroChangeUI.cs` |
| アイテム（データ/装備/ベンチ/ツールチップ） | `ItemCatalog.cs` / `ItemData.cs` / `ItemInstance.cs` / `ItemBenchCanvasUI.cs` / `ItemTooltipUI.cs` |
| 盤面・マス・経路探索・配置 | `GridManager.cs` / `Graph.cs` / `Tile.cs` / `Draggable.cs`（配置形状は `ChapterBackground.GetBoardCornerCut` 連動） |
| VFX/SFX/BGM 全般 | `AttackEffectPlayer.cs`（約3,000行）、Arcanaの血魔法陣は `BloodMageVisual.cs` |
| ヒーロー必殺（ボタン/カットイン/強化2択） | `HeroUltButtonUI.cs` / `HeroUltCutInUI.cs` / `HeroUltUpgradeUI.cs` |
| ボス戦前会話 / 幕間VN / 章プロローグ / 字幕 | `HeroBossDialogueUI.cs` / `InterludeUI.cs`・`InterludeScript.cs` / `ChapterPrologueUI.cs` / `ChatterSubtitleUI.cs`（立ち絵解決は `DialogArt.cs`） |
| 報酬選択UI（ボス仲間化/アイテム3択/強化マス/ノード進路） | `BossRewardSelectionUI.cs` / `ItemRewardSelectionUI.cs` / `BuffTileRewardUI.cs` / `NodeSelectionUI.cs` |
| リザルト/スコア/バナー演出 | `ResultPanelUI.cs` / `ScorePopupUI.cs` / `WaveClearBannerUI.cs` / `BossUnlockBannerUI.cs` |
| ラウンド進捗HUD / 陣形ガイド / 各種HUD | `RoundProgressUI.cs` / `FormationHintUI.cs` / `BoardCapacityHudUI.cs`・`CoreModeHudUI.cs`・`ChestRoomHudUI.cs` |
| 設定（音量/言語/速度/HUD/キー） | `OptionsPanelUI.cs` / `SettingsPanelUI.cs` / `SettingsStore.cs` / `HudSettingsApplier.cs` |
| ローカライズ（JA/EN・フォント） | `LocalizationManager.cs`（`IsJapanese` / `ApplyFont` / `UnitName`） |
| 章別動的背景・前景カーテン・盤面形状 | `ChapterBackground.cs` |
| 転がる巨大物ギミック | `RollingHazard.cs` |
| コア破壊モード | `GameManager.cs`（`IsCoreMode`/`BuildCoreModeRounds`/`CoreAutoAdvanceRoutine`）+ `CoreModeHudUI.cs` |
| 体力バー表示 | `Healthbar.cs` |
| 開発用自動テスト/デバッグ | `Debug/AutoPlayHarness.cs`（F8メニュー、`#if UNITY_EDITOR||DEVELOPMENT_BUILD`） |
| ユニットの一括ビルド（sprite/prefab/anim/DB生成） | Editor系 `UnitAnimationBuilder`（`BuildUnitsByNames`。specs にアトラス指定） |

- 過去の不具合の経緯・設計判断は `docs/ROADMAP.md` の進行メモ（日付順・最下部が最新）を grep するのが最速
- Steam審査リスク一覧は `docs/STEAM_READINESS_STANDARDS.md`

# 主要機能と使用ツール

- **チャプターモード（全20章）**: 各章 4ステージ×33ラウンド（Stage1=肩慣らし3R、以降10Rずつ）。通常戦/中ボス/章ボス/イベント回（アイテム・ゴールド・オーグメント）で構成。章N-1クリアで章N解放。章1=Caliber、2=rook、3=sister、4-6=Magmar将、7-9=Abyssian将、10-12=Vetruvian将、13-18=Mechaz0r連章、19=hydrax、20=Arcana（最終）
- **セーブスロット**: 3枠。「はじめから/つづきから」、上書き・削除は確認ダイアログ付き
- **ヒーロー（主人公）**: 基本3体（アルディン/カガチ/ヴェスナ）+追加6体=9体から選択。章クリアで解放が増える。盤面配置枠を消費しない・ラン内Lv/自動★アップ・1戦1回自動復活・専用必殺（ラン開始時に強化2択）・専用6マス陣形・熟練度（Lv50）と手動強化ポイントの二重育成
- **ショップ/経済**: コスト1-5のユニット購入・売却・リロール（ロックトグルあり）・XP購入。収入=基本5+利子（所持10ごと+1、上限5）。通貨表示は「マナ」（ジェムアイコン）。コスト解放は中ボス/章ボス撃破で+1（初期3→最大5）
- **オーグメント**: 80種（Silver30/Gold25/Prism25）。特定ラウンドで3択。右上HUD+ツールチップで確認
- **シナジー**: 25種（テーマ19+陣営6）。陣営シナジーは「1〜2体は主人公が同陣営のときのみ発動、3体以上で誰でも、同陣営主人公なら効果1.5倍」
- **仲間化メタ進行**: 中ボス=戦った3体から1体選び章内解放+1体ベンチ配置。章ボス=撃破で恒久解放（`AddBossAlly`）→図鑑登録・再クリアでアフィニティLv上昇（ステ強化+節目パッシブ）・主人公としても採用可
- **ノード進路選択**: 章ボス直前は3ノード提示→2選択1破棄（精鋭/標準/補給）
- **チェスト部屋**: アイテムイベント回が「30秒で宝箱を殴って開ける」部屋に置換（コイン箱+アイテム箱）
- **強化マス**: 3-5報酬。種別（攻/防/秘力）を選び自陣マスに設置、章内永続
- **陣形**: 汎用4種（突撃/鉄壁/方陣/楔）+主人公専用形。編成中に成立マスが光る+ガイドポップアップ
- **アイテム**: 装備3枠+取り外し機（消費されないツール）。中ボス報酬の3択等で入手
- **コア破壊モード**: 別モード。敵味方コア拠点、波自動進行（インターバル5s→編成40s）、5波ごとにボス恒久解放
- **ストーリー**: 章プロローグ→オープニングVN、ボス戦前掛け合い（VN風）、中ボスは非ブロッキング字幕、幕間（INT_01/08/12/13）+エンディング。カガチはch8踏破で犬化スキン解放
- **図鑑/コレクションハブ**: ボス図鑑（待機アニメ表示・詳細ビュー・モーション巡回・ズーム）+ショップ選抜（出すユニットをON/OFF、コスト別最低数保証）
- **リザルト/スコア**: ライブ加点ポップアップ、章クリアでベストスコア/タイム保存・NEW RECORD表示。敗北時もリザルト→ロビー復帰
- **設定**: 音量/言語(JA-EN)/ゲーム速度/HUD表示切替/キーコンフィグ。倍速は章をまたいで維持
- **開発専用（製品ビルド非搭載）**: F8デバッグメニュー（ラウンドジャンプ/即勝利/無敵/報酬強制/XP付与）、AutoPlayHarness（自動周回・敗因分析・`AutoPlayDumps/` へレポート）

# コーディング規約・制約

- コメントは**日本語**で要所に1〜2行。新規コードは既存と見分けがつかないスタイルにする
- **JA/EN 必須**: `LocalizationManager.IsJapanese` で分岐し、テキスト生成後に `ApplyFont(text)`。片言語ハードコード禁止
- UIシングルトンは `EnsureExists()` パターン+**独立ルートの ScreenSpaceOverlay Canvas を自前生成**（既存 Canvas への親子化は事故のもと→落とし穴参照）
- `Canvas.sortingOrder` は **32767 以下厳守**（16bit short。全UIの帯割当は ROADMAP 2026-06-02 参照。最前面=UnitStatusPanel 32000）
- スキル説明は**実数値**（E5方針）。計算に使うヘルパ（`GetAreaDamage`/`GetShieldAmount` 等）から数値を出し、表示と実値を一致させる。定性文（「大ダメージ」等）の新規追加禁止
- 永続化は `JsonUtility` + `[System.Serializable]` DTO + `List<T>`。**Dictionary 不可**
- `reference/` 直接参照禁止（Assets/ へコピー）。`HasAugment("id")` の文字列直書き時は `AugmentCatalog.FindById` で存在確認
- **敵の cost4/5 は★2上限**（★3はプレイヤー専用の強さ。章ボス含む全敵に適用済み・変更しない）
- ヒーロー/将系/雑魚の入手除外は中央フィルタ `IsLegionOnlySummonData` に集約（個別分岐を増やさない）
- LobbyUI 等の一部ファイルは `using System.Linq;` 非導入（フル修飾方針）。LINQ を足すとコンパイル崩壊した前例あり
- デバッグ機能は必ず `#if UNITY_EDITOR || DEVELOPMENT_BUILD` でガード
- 未実装・未対応の固定事実: オンラインランキング/Steamworks統合（実績・クラウド）/ゲームパッド操作/モバイル/マルチプレイ/MOD対応は**未実装**。トレーニング等の追加モードはコア戦のみ
- コミット規約: `<task-id>: <一行サマリ>` + `Tested: Compilation OK` + `Refs: docs/DESIGN_*.md`。コンパイル（Errors: False）確認前のコミット禁止

# トリアージ基準（バグ分類の判定ロジック）

**type の切り分け**:
- **クラッシュ**: ゲームが落ちる/フリーズ/Unityエラーダイアログ/起動不能/ロード不能。無限ロード・操作を受け付けない「進行不能ハマり」も実害同等ならクラッシュ扱いでよい（例: 報酬選択が消えて FIGHT が押せない）
- **不具合**: 仕様と異なる挙動全般。例=スキル効果が説明文と違う、報酬が貰えない、セーブが残らない、シナジーが発動しない、敵/味方の挙動異常（ウロウロ・攻撃しない）、進行フラグの取り違え
- **UI**: 表示崩れ・重なり・はみ出し・文字欠け・言語切替漏れ・ツールチップ/パネルが出ない/消えない・表示順（前後関係）の異常。**機能自体は動くが見た目がおかしい**もの。UI起因で進行不能になる場合は「不具合」または「クラッシュ」へ格上げ
- **パフォーマンス**: FPS低下・カクつき・倍速時の異常な重さ・メモリ肥大・ロードが極端に長い。高速周回（×8）でのみ出る症状はまずこれを疑う

**priority の判定**:
- **優先度:高**: クラッシュ/進行不能（章が進められない・FIGHT不能・ロビーに戻れない）/セーブデータ消失・破損/複数ユーザーが同一報告/金銭・進行度が失われる
- **優先度:中**: 特定機能が使えないが回避策がある/バランス崩壊級の数値バグ/特定章・特定ユニット限定の不具合/目立つUI崩れ
- **優先度:低**: 軽微な見た目・誤字・JA/EN不一致/エフェクト・SEの違和感/バランス調整要望（R3-balance 領域）/レアな環境依存

**platform**: 本作は **PC専用**。原則すべて「PC」。「モバイル」タグは本プロジェクトでは使用しない（モバイル報告が来たら仕様説明で対応=バグではない）

**バグではない可能性が高い頻出報告**（トリアージ前に確認）:
- 「1マスに複数ユニットが重なる」→ 高速再生中のアニメ補間+召喚トークン(Zyx)の見た目。論理的な重複は検出器で否定済み
- 「配置数が上限を超えている」→ ヒーローと召喚トークンは配置枠を消費しない仕様
- 「BGMが鳴らない」→ エディタなら Game ビューの Mute Audio を確認
- 「敵cost5が★3で出て来ない」→ 仕様（敵の cost4/5 は★2上限）
- 「陣営シナジーが1体で発動しない」→ 仕様（主人公ゲート）

# 既知の落とし穴【最重要】

エラー・不具合報告が来たら、まずこのチェックリストを上から当てること。

**UI が表示されない/消える/裏に回る系**:
1. `Canvas.sortingOrder` が 32767 を超えていないか（**16bit shortで負値に化けて背面へ回る**。過去に図鑑詳細が非表示になった根因。50020→-15516 の実例あり）
2. 新規UIを `FindObjectOfType<Canvas>()` の子にしていないか（**トグルする他UIのCanvasを親に拾い、親のHideと連動して点滅・消滅**した実例=報酬選択UI群。必ず独立ルートCanvasを自前生成）
3. `CanvasGroup.DOFade` がロビー系で進まず alpha=0 張り付きの前例あり。表示担保は即 alpha=1+OnKill 保証で
4. `ClearOptions` 等での `Destroy` は**フレーム末まで遅延**する。旧要素が同フレームに残り、はみ出し+クリック横取りで進行不能になった実例あり→破棄前に `SetActive(false)`+`SetParent(null)`
5. 選択UIの `SelectOption` は「Hide→コールバック」の順（逆順だとコールバックが再表示した2枚目のUIを Hide が閉じて進行不能）

**入力・座標系**:
6. `ScreenToWorldPoint` は z=0 のままだと盤面（パースペクティブ）に当たらない。カメラ距離 `Mathf.Abs(cam.transform.position.z)` を z に与えてから変換（強化マス設置が全マス無反応だった根因）
7. 距離・射程判定は**チェビシェフ距離**（`max(|dx|,|dy|)`）に統一済み。ユークリッドで書くと「斜め隣接が射程外→メレーがウロウロ」が再発する
8. UIのCanvasScalerモードが混在（ConstantPixelSize と ScaleWithScreenSize）。**異なるモード間の座標比較は不可**。レイアウト干渉の静的判断はモード確認が先

**アセット・Unity仕様**:
9. **m4a は Unity 非対応**。`Resources.Load<AudioClip>` が同名 m4a を拾って無音になった実例あり（音源は ogg/wav のみ置く）
10. 流用プレハブの **Animator がスプライトを毎フレーム上書き**する。静止画・独自アニメをさせるときは `animator.enabled=false`（宝箱が Borealjuggernaut に戻った根因）
11. `Entity Database.asset` の同期はビルダーの**マージ方式**を守る（全置換にすると spec 外の Arcana/Zyx が消える事故の前例）
12. reference の plist と png が転置（512×1024 vs 1024×512）している素材があり、フレーム欠落の原因になる（HeroVesna の実例。original_resources 側を確認）
13. GUID保持のリネーム・`AssetDatabase.MoveAsset` を使えばシーン/プレハブ参照を壊さず差し替え可能（Coin→ManaGem、T3→Hero移動の実例）。生ファイル移動は参照切れを起こす

**ゲームロジック**:
14. 倍速リセット問題: ポーズ復帰は「捕捉した previousTimeScale」でなく `OptionsPanelUI.DesiredGameSpeed` へ戻す（リザルト/オーグメント選択を挟むと1倍に戻った前例）
15. `pending*` フラグ（報酬選択中等）が解除されないと FIGHT が永久に弾かれる。進行不能報告ではまず「未解決の選択UI+pendingフラグ」を疑う
16. 章ボス・中ボスの候補/報酬は「そのウェーブで戦った敵」から導出する設計。プールからのフォールバック復活は「戦っていないボスが並ぶ」バグを再発させる
17. シナジーのオーグメント加算は表示用と発動判定用の**両方**に効かせる（`PlayerSynergyAugmentBonus` 共通ヘルパ経由。表示だけ増えて発動しない前例）
18. 戦闘一時状態は `ResetBattleTemporaryState` でラウンド跨ぎリセット（Arcana詠唱回数の持ち越し前例）。新規の戦闘中ステートを足したら必ずここに片付けを追加
19. ラウンド終了で破棄されたオブジェクトを VFX コルーチンが参照して MissingReferenceException（高速周回で顕在化）。SpriteRenderer 参照には null ガード

**開発環境（Unity MCP / git）**:
20. コンパイル確認は **LogEntries.Clear()→再コンパイル→エラー走査**の順（クリアしないと旧エラーを拾って誤判定）。合格基準は realCS=0
21. Synaptic ブリッジは全再インポート・ドメインリロード中は無応答になる（バグではない。復帰待ち）。`docs/SYNAPTIC_BRIDGE_BUG_REPORT.md` 参照
22. MCP eval からはシーン遷移（LoadScene）を伴う動作確認ができない。ロビー→ゲーム遷移の検証は手動プレイ必須
23. bash（サンドボックス）経由の大ファイル書き込みは 128KB 切断事故の前例あり（LobbyUI.cs 切断）。大きい .cs はホスト側ツールで編集
24. git index 破損・bash git のオブジェクト書き込み不能の前例あり。コミットはホスト側で実施

# 用語集

- **章 / ステージ / ラウンド**: 1章=4ステージ=全33ラウンド。「X-Y」表記=ステージX-ラウンドY（例: 4-10=章ボス戦）
- **中ボス / 章ボス**: 中ボス=ステージ途中のボス戦（撃破でコスト解放+報酬）。章ボス=4-10、撃破で章クリア+恒久仲間化
- **仲間化 / 勧誘（recruit)**: ボスを自軍ユニットとして解放すること。章内限定（中ボス）と恒久（章ボス=BossAlly）の2層
- **アフィニティ**: 同じ章ボスを再取得するたび上がる育成Lv（ステ倍率+節目パッシブ）
- **ヒーロー / 主人公**: プレイヤーの分身ユニット。盤面枠を消費しない。ボス将も主人公に採用可能
- **必殺 / アルト**: ヒーローの1戦1回のアクティブスキル（HeroUlt）
- **オーグメント**: TFT式の3択パッシブ強化。ティア=Silver/Gold/Prism
- **シナジー / 陣営**: ユニットの系統ボーナス。陣営=Lyonar/Songhai/Vetruvian/Abyssian/Magmar/Vanar（Duelyst由来名。将来独自名へ差し替え予定→`LocalizationManager` の表示名のみで対応）
- **スター（★）**: 同一ユニット重ねで★1→3。敵cost4/5は★2上限
- **Zyx**: cost0 の中立雑魚・召喚トークン（ショップ非売品）
- **Arcana / アルカナ**: 最終章ボス（cost5）。血の魔法陣＝BloodMage が固有ギミック
- **強化マス**: 自陣マスに置く章内永続バフタイル
- **ノード選択**: 章ボス直前の Slay the Spire 風進路3択（精鋭/標準/補給）
- **チェスト部屋**: 宝箱を殴って開ける30秒のボーナスラウンド
- **コア戦 / コア破壊モード**: 拠点破壊型の別モード（CoreAssault）
- **陣形（フォーメーション）**: 配置形状ボーナス。汎用4種+ヒーロー専用形
- **取り外し機**: 装備アイテムを外す消費されないツール
- **EnsureExists**: UIシングルトンの遅延生成パターン（本プロジェクトの標準）
- **realCS**: コンパイル検証指標「実際のC#エラー数=0」のこと
- **R3-balance**: 「数値バランスは後でまとめて調整する」を意味するタスクラベル。数値への不満報告はここへ分類
- **task-id**: `R1-persist` 等の作業ID。設計書 `docs/DESIGN_<task-id>.md` と1:1対応
- **Cowork / Claude Code / Codex**: 開発に使うAIエージェントの呼び分け（Cowork=Unity MCP併用の実装/検証、Claude Code=純ロジック実装、Codex=初期実装者）。ROADMAP の署名に登場
- **Duelyst / OpenDuelyst**: アート・音源の出典（CC0）。`reference/duelyst/` が原本
