# DESIGN（Claude Code 引き継ぎ）: デバッグ機能 & シナジー死亡カウント維持
> **状態: ✅ 実装済み（2026-05-30〜31）** — デバッグ機能 / シナジー死亡カウント維持 / 章2静的検証 完了。

> 設計: Claude (Cowork) / 実装: **Claude Code** / 2026-05-30
> 役割分担: Arcana の反転・アニメ・必殺技と Animator/アセット系は Claude(Cowork+Unity MCP) が担当中。
> こちらの2件は**純ロジックで別ファイル**のため衝突しない。`autochess-implementation` スキルの規約に従って実装すること。
> **重要**: `BaseEntity.cs` は Cowork 側が Arcana 対応で同時編集中。可能な限り触らない（#5 は SynergyManager 中心、#4 は新規ファイル）。

---

## タスク D-1: デバッグ機能（リリース時に削除）

### ゴール
コスト5など高コストの動作確認を素早く行うためのデバッグ操作を追加する。**リリース時に1ファイル削除で消せる**よう、独立した新規ファイルにまとめる。

### 影響範囲
- 新規ファイル: `Assets/Scripts/Debug/DebugMenu.cs`（`#if UNITY_EDITOR || DEVELOPMENT_BUILD` で囲み、製品ビルドに残さない）
- 既存コードは原則変更しない（既存の public API を呼ぶだけ）。public が足りなければ最小限の public 化を `GameManager`/`PlayerData` に施す（その場合は1行コメントで「DEBUG用」と明記）。

### 必要な4機能と推奨フック（既存APIを確認して使う）
1. **お金を増やす**: `PlayerData.Instance` の所持金加算。`SpendMoney`/`CanAfford` は既存。加算APIが無ければ `AddMoney(int)` を `PlayerData` に追加（DEBUGコメント）。例: +50。
2. **任意のユニットを生成**: `Entity Database.asset`（`EntitiesDatabaseSO`）の全エントリから選び、`GameManager.Instance.OnEntityBought(EntityData)` で購入扱い生成（ベンチへ）。`OnEntityBought` は既存（UIShop が使用）。お金不要で直接呼ぶ。
3. **オーグメント選択画面を強制で開く**: GameManager のオーグメント提示フロー（`augmentSelectionPending` / `AugmentSelectionUI` / 章イベント `WaveEventType.AugmentSilver/Gold/Prism`）を確認。提示を起動する内部メソッド（例 `ShowAugmentSelection`/`TryStartEventRound` 周辺）を呼べるようにする。ティア指定（Silver/Gold/Prism）で開けると理想。
4. **任意のアイテムを獲得**: 既存のアイテム配布（`GameManager.spawnDebugItemsOnStart` / アイテムベンチ / `ItemData`）の仕組みを流用。アイテム一覧から選んでベンチへ付与する内部APIを呼ぶ。

### UI
- 軽量で良い。`OnGUI()` で画面端にボタン群（「+Gold」「Spawn Unit▼」「Augment(S/G/P)」「Give Item▼」）。
- もしくはホットキー（既存 `GameManager.debugTrainingWaveHotkey = F8` の流儀）。ユニット/アイテムは数が多いので簡易スクロール一覧か名前入力が現実的。
- シーンに1つだけ存在すればよい（`EnsureExists` パターン or 起動時に自動生成、ただし `#if` ガード内）。

### 受け入れ基準
- [ ] エディタ/開発ビルドでデバッグ操作が使える（お金+／ユニット生成／オーグメント強制表示／アイテム付与）
- [ ] **製品ビルド（`#if` 無効時）にコンパイル含めて一切残らない**
- [ ] 既存フローを壊さない（public 化した場合も挙動不変）
- [ ] `Compilation completed (Errors: False)`

### 実装ヒント
- まず `grep` で各フックの正確なメソッド名を確認（`OnEntityBought` / 所持金加算 / augment 提示 / アイテム付与）。曖昧なら `docs/QUESTIONS.md` に質問を残す。
- 全アイテム/全ユニットの一覧は Resources/AssetDatabase から取得（ランタイムは Resources.LoadAll もしくは GameManager が持つ参照）。

---

## タスク D-2: シナジー — 戦闘中に死亡した味方もカウント維持

### ゴール
戦闘中に味方ユニットが死亡しても、その**ユニットのシナジーがカウントから外れない**ようにする。プレイヤーが組んだ編成のシナジーは、戦闘終了まで一定であるべき（死亡で急にバフが消えると理不尽）。

### 根本原因（特定済み）
`Assets/Scripts/SynergyManager.cs` の `CountSynergiesForTeam(...)`（~920行〜）。集計ループの条件:
```csharp
if (entity == null || entity.Team != team || !entity.IsOnBoard || entity.IsDead || entity.IsSummonedUnit)
    continue;   // ← entity.IsDead で死亡ユニットを除外しているのが原因
```
さらに死亡時に盤面リスト（`team1Entities` 等）から除外され `IsOnBoard=false` になる経路があるかも要確認（その場合 `IsDead` を外すだけでは不足）。

### 設計方針（推奨: 戦闘開始時スナップショット方式）
死亡ユニットを「生存判定で都度集計」に混ぜると、復活・召喚・盤外移動などの既存ロジックと干渉しやすい。よって:
1. **戦闘開始（FIGHT）時に、各チームの "参戦ユニット" を記録**（`team1Entities`/盤上の非召喚ユニットの UnitId とシナジーのスナップショット）。`GameManager` が戦闘開始を知っているので、そこから `SynergyManager` にスナップショットを渡すフックを1つ追加。
2. **戦闘中のシナジー集計は、生存数ではなくスナップショット（参戦時の編成）を基準**にする。死亡しても数は減らない。
3. 戦闘外（編成中）は従来どおり盤面のリアルタイム集計でよい（ショップ/配置で数が変わるのを見せたい）。
4. 召喚体は従来どおり対象外（`IsSummonedUnit`）。**augment 由来の追加カウントやオーグメント連動（`AdditionalSynergyBonusThisCombat` / emblem 等）も二重計上にならないよう注意**。

### 代替（簡易）案
スナップショットが大掛かりなら、最小変更として「**戦闘中フラグが立っている間は `IsDead` を除外条件から外す**」。ただし死亡ユニットが盤面コレクションに残り `IsOnBoard` を維持していることが前提。死亡で除外される実装なら、死亡時に "シナジー用には残す" 別リストを保持する必要がある。**まず死亡時の盤面リスト・IsOnBoard の挙動を確認**してからどちらかを選ぶ。

### 受け入れ基準
- [ ] 戦闘開始時に成立していたシナジー段階が、味方が戦闘中に死亡しても**最後まで維持**される（パネルの数字・実効バフ両方）
- [ ] 編成中（戦闘外）は従来どおり、配置/売却でシナジー数がリアルタイムに増減する
- [ ] 召喚体・敵チーム・オーグメント加算の挙動にデグレが無い
- [ ] `Compilation completed (Errors: False)`

### 未決事項（あれば QUESTIONS.md へ）
- スナップショット方式 / 簡易フラグ方式のどちらを採るかは、死亡時の盤面リスト挙動を見て Claude Code が判断してよい（上記受け入れ基準を満たすこと）。

---

## 引き継ぎ（2026-05-31）: チャプター2 ＆ ロビー画面のエディタ検証・仕上げ

> 一次実装は Claude(Cowork) 済み・Compilation OK（Errors 0）。**プレイ検証はできていない**ため、エディタでの確認と仕上げを依頼します。
> 設計: [DESIGN_chapter2.md](DESIGN_chapter2.md) / [DESIGN_lobby.md](DESIGN_lobby.md)

### A. チャプター2（`GameManager.BuildChapter2Rounds`）
- 検証: ロビー（or `PendingStartChapter=2`）で章2を開始し、4ステージ33ラウンドが進むか。全 `WaveEnemyPlacement` の名前が Entity DB に存在し、座標(列6-10/行3-7)が有効ノードか（不正なら spawn 警告ログが出る）。
- 検証: 4-10 撃破で章クリア → Skyfalltyrant が `BossAllies` 追加 → 章3以降の roster 候補に出るか。
- 調整: 難易度（★/護衛数）は体感で。最終調整は R3-balance。

### B. ロビー（`LobbyUI.cs` ＋ `GameManager` の起動フック）
- まず `GameManager.showLobbyOnBoot = true`（インスペクタ or コード）で起動し、ロビー表示→章選択→開始の往復を確認。
- 既定OFFのままなら**挙動は従来どおり**（回帰リスク無し）。ONにした時の以下を確認・仕上げ:
  1. 起動時のラン初期化順（`Start` がロビー表示前に `GrantStartingUnit`/`TryShowChapterRoster`/`TryStartEventRound` を実行する点）。章選択で再読込され破棄されるので機能はするが、綺麗にするなら「ロビー表示中は初期化を遅延」分岐を追加。
  2. 「ロビーへ戻る」導線（リザルト or オプションにボタン）を追加。`PendingStartChapter=0` で再読込すれば boot ロビーに戻る。
  3. レイアウト/フォント/解像度の崩れ確認（`LobbyUI` はプログラム生成・参照解像度依存）。
- 注意: `BaseEntity.cs` / `AttackEffectPlayer.cs` は Cowork 側が Arcana 関連で頻繁に編集中。ロビー作業は `LobbyUI.cs` と `GameManager.cs`（章まわり）に限定すると衝突が少ない。

### C. 任意（次の差別化）: Arcana を最終章ボスに
- Arcana は現状 **Entity DB 未登録**（プレハブ/アニメ/スキル/専用シナジー Finality は実装済み）。
- 章2 or 章3 の最終ボス化には Entity DB へ登録が必要: name="Arcana", prefab=`Assets/Prefabs/Unit/T5/Arcana.prefab`, cost=5, icon=`Images/Units/Icon/T5/Arcana`, frame=cost5枠流用, synergy1=Finality(19)（+ 任意で Abyss(13)/Arcanist(3)）。登録後 `ChapterBossUnitIds` の該当章を "Arcana" に差し替え。エディタで安全に（ScriptableObject 編集）。

---

## 引き継ぎ（2026-05-31 追加）: ロビーの duelyst アート結線の **プレイ検証・仕上げ**

> Cowork が duelyst 素材の取込と `LobbyUI.cs` への結線まで実施済み・**Compilation OK（Errors 0）**。
> ただし `showLobbyOnBoot` が既定 false のため**実機での見た目は未確認**。確認と微調整を依頼します。
> 素材の確定リスト＝[DUELYST_LOBBY_ASSETS.md](DUELYST_LOBBY_ASSETS.md)。**`reference/duelyst` の再クロールは不要**（2.3GB・無関係ファイル多数。見ないこと）。

### Cowork が済ませたこと（再実行不要）
- 取込（`Assets/Resources/UI/Lobby/`、importer=Sprite/PPU100、ボタンは9-slice border 設定済み）:
  - 背景 `lobby_bg.jpg`（duelyst `scenes/obsidian_woods/...background.jpg`）
  - エンブレム6枚 `emblem/sm_{center,ring_outer,ring_inner,diamond,icon,triangle_small}.png`（`symbol_main_menu_*@2x`）
  - ボタン `button_primary.png` / `button_primary_glow.png`
  - 全9枚 `Resources.Load<Sprite>` 成功を確認済み。
- `LobbyUI.cs` 結線（**素材が無い場合は従来のグレーUIへ自動フォールバック**するので回帰リスク低）:
  - ルート Image を背景スプライト化（減光 `color=(0.5,0.55,0.62)`、全画面 stretch=`preserveAspect:false`）
  - 中央に多層回転エンブレム（`Emblem`、パネル背面 siblingIndex0）。`Update()` で **unscaledDeltaTime** 回転（外輪+8°/s・内輪-12°/s・diamond+4・triangle-6）＝`timeScale=0` のロビーでも回る。
  - パネルを半透明ガラス調に（alpha 0.96→**0.72**）＝背景とエンブレムが透ける。
  - チャプター/フッターのボタンを `button_primary` の9-sliceに（フッターの青/赤 tint は維持、ロック時は減光）。

### Claude Code にお願いしたい検証・仕上げ
1. **見た目確認**: `GameManager.showLobbyOnBoot=true` で起動 → 背景・回転エンブレム・スライスボタンが出るか。エディタ Game ビューでスクショ推奨。
2. **可読性チューニング**（数値はコード定数なので調整しやすい）:
   - 背景が明るすぎ/暗すぎ → `EnsureLobbyArt` の `rootImage.color`。
   - エンブレムがパネルに埋もれて見えない → パネル alpha（`panelImage.color` の 0.72）か、エンブレム各層の alpha（`BuildEmblem`→`AddEmblemLayer` 第5引数）/サイズ（第4引数 520等）。
   - 背景が引き伸ばしで歪む → `preserveAspect:true` か、cover 用に縦横比合わせ（参照解像度依存）。
3. **回転負荷**: ロビー非表示中も `Update` は回る（軽微）。気になれば `gameObject.activeInHierarchy` ガードを追加。
4. **任意フォント**: EN タイトルを Averta 化したい場合は `reference/duelyst/app/resources/fonts/averta-bold-webfont.ttf` を `Assets/Fonts/` にコピー → **TMP_FontAsset を生成**して `titleText.font` に設定。**JA は既存の `LocalizationManager.ApplyFont` を維持**（Averta は英字のみ）。優先度低。
5. **往復確認**: 章選択→ラン開始→「ロビーへ戻る」（既存導線 §B-2）でロビーに戻れるか。
- 競合回避: Cowork が `BaseEntity.cs` / `AttackEffectPlayer.cs` を Arcana 関連で編集中。ロビー仕上げは **`LobbyUI.cs` と `GameManager.cs`（章まわり）に限定**すれば衝突しない。

---

## 引き継ぎ（2026-05-31 追加2）: 設定（SettingsStore）の GameScene 側適用

> Cowork で **専用ロビーシーン化＋3画面構成（タイトル/ロビー/チャプター選択）＋DOTween 演出＋duelyst UI 素材＋総合設定パネル**まで実装・**Compilation OK（Errors 0）**。
> 新規: `Assets/Scripts/SettingsStore.cs`（PlayerPrefs 一元管理＋適用API）、`Assets/Scripts/SettingsPanelUI.cs`（タブ式設定UI）、`Assets/Resources/UI/Duelyst/`（back/close/confirm/frame_modal 等）。
> `SettingsStore.ApplyAll()` は自己完結する項目（音量/ディスプレイ/グラフィック/カーソル/UIスケール=ConstantPixelSizeのCanvasScaler）を起動時に適用済み。
> **残り＝GameScene 固有の2系統の「実適用」**を依頼します。設定値の保存は完了しているので、読んで反映するだけ。

### A. HUD 個別表示切替
- 保存値: `SettingsStore.GetHud("synergy"|"coin"|"round"|"tooltip")`（bool, 既定 true）。
- GameScene の該当 HUD を表示/非表示に: `SynergyPanelUI` / `CoinIncomePanelUI` / `RoundProgressUI` / ツールチップ系（`SynergyTooltipUI`/`ItemTooltipUI`）。
- ライブ反映: `SettingsStore.OnChanged += ...` を購読し、トグル即時に `gameObject.SetActive` 等で反映。起動時にも一度適用。

### B. キーバインドの参照
- 保存値: `SettingsStore.GetBind("toggleOptions", KeyCode.Escape)` / `GetBind("debug", KeyCode.F8)`。
- 現状ハードコードの入力判定（`OptionsPanelUI.Update` の `KeyCode.Escape`、`GameManager.debugTrainingWaveHotkey=F8` 等）を上記参照に置換。

### C. UI/文字サイズ（任意の追加適用）
- `SettingsStore.UiScaleFactor`（小0.85/中1.0/大1.18）。`ApplyUiScale()` は **ConstantPixelSize** の CanvasScaler のみ `scaleFactor` を反映。
- GameScene HUD が **ScaleWithScreenSize** の場合は未反映。必要なら HUD ルートに `UiScaleFactor` を乗算 or 文字サイズへ反映を追加。

### 注意
- これらは値参照のみで、`LobbyUI.cs`/`SettingsPanelUI.cs`/`SettingsStore.cs` は触らなくてよい（Cowork 管理）。GameScene 側のHUDスクリプトに読み取りを足すだけ。
