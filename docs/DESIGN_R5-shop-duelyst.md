# DESIGN_R5-shop-duelyst — ショップUIをDuelyst寄りに作り直し（脱AIイラスト）

> task-id: R5-shop-duelyst / 作成: 2026-07-03 / 対象: ショップUI（UIShop/UICard）＋通貨表示
> 背景: 既存キャラのAI生成イラストはDuelystファン/Steam的にリスク。Duelystは全アセットCC0で公開済み。
> ユーザー決定:
> - (a) カードの顔は **AIイラスト→ユニットのDuelystドット絵（スプライト）** を表示。
> - スプライトは **盤面上の同一ユニットとアニメが連動しない**こと。
> - 通貨は **コイン→マナジェム**（`Assets/Images/UI/Shop/Coin/Coin.png` は出所不明なので Duelyst のマナジェムへ）。
> - 立ち絵(非ドット)を持つのは**限定ユニットのみ**（将/ヒーロー＋一部ボス）。把握して使う。
> 関連: STEAM_READINESS_STANDARDS.md（AI/権利）, [DESIGN_R4-collection-hub.md](DESIGN_R4-collection-hub.md)

---

## 1. 現状（脱AI対象の棚卸し）
- AI生成の顔絵: `Assets/Images/Units/Icon/**`（Hero/Neutral/T1〜）約88枚 → `EntityData.icon`。
- AI枠: `Assets/Images/UI/Frame/T1〜T5`（5枚）→ `EntityData.frame` / UICard.frame。
- AIショップUI: `Assets/Images/UI/Shop/ShopBoard/*`（盤・Lv/リロールボタン）。
- コイン: `Assets/Images/UI/Shop/Coin/Coin.png`（guid 657a6adeb5208084da8990bbdcc1b357）→ Slot.prefab / T1_*.prefab / GameScene.unity から参照。
- 立ち絵(CC0 Duelyst公式): `Assets/Resources/UI/Dialog/**`（93枚。将/ヒーロー＋一部ボスのみ）。会話用途は現状維持。

## 2. 方針
UI構造（枠＋差込口）は流用し、**中身をDuelyst公式CC0＋プログラム描画＋ドット絵**へ置換する。UIの作り直しではなく素材/描画の置換が主。

### 2.1 カードの顔＝ユニットスプライト（独立アニメ）
- 各カードは `EntityData.prefab`（BaseEntity prefab, Animator+SpriteRenderer付き）の**見た目のみを別インスタンス**として使う → 盤面ユニットと**別Animator**なのでアニメは自然に非連動。
- 描画方式: **共有プレビューカメラ＋RenderTexture**。
  - 画面外のワールド座標に「ShopPreviewStage」を置き、ショップ5枠ぶんのユニット見た目を1列に生成（各自Animatorでidle再生）。
  - 専用Orthographicカメラで RenderTexture(1枚) に描画。
  - 各カードは `RawImage` で RT の該当ユニット領域(UV sub-rect)を表示。
  - BaseEntity のゲームロジック（移動/攻撃/HP等）は無効化し、見た目(SpriteRenderer/Animator)だけ動かす（`ConfigureAsPreview()` 的な最小構成）。
- フォールバック: RT/prefab不可時は idle 1コマの静止スプライト。
- 立ち絵を持つユニットでも**カードはスプライトで統一**（立ち絵は会話専用のまま）。将来オプションで切替可能に。

### 2.2 カード枠＝プログラム描画（Duelyst風）
- Unity UIで描画（画像素材不要）: 角丸枠＋陣営色の縁＋名前プレート＋★段階＋マナコスト・ジェム。
- AI枠(`UI/Frame/T1〜T5`)・`EntityData.frame` 依存を撤去。

### 2.3 通貨＝マナジェム
- **経済ロジックは不変**（`PlayerData` の残高/AddMoney/SpendMoney はそのまま）。表示とアイコンだけマナジェム化。
- アイコン差し替え: `Coin.png` を**同一パス/GUIDのままマナジェム画像へ差し替え**れば、Slot/T1_*/GameScene の全参照が一括で切替（プレハブ改変不要）。
  - 素材入手（未決）: **(i) Duelyst公式マナジェム(CC0)をユーザーが追加** / **(ii) Codexにマナジェム生成を指示**（Coworkは画像生成しない方針）。※procedural描画案もあるが方針に合わせ要相談。
- 文言: ユーザー可視の「コイン/ゴールド」表記を「マナ」へ（`LocalizationManager`/該当UI）。内部変数名は据え置き可。

## 3. フェーズ（細分化）
1. **P1: プレビュー基盤** … ShopPreviewStage＋プレビューカメラ＋RT＋`UnitPreviewRenderer`。UICardをRawImage表示へ。1枠で独立アニメ確認。
2. **P2: 5枠連動** … ショップ生成/リロールでプレビュー内容を同期。UV sub-rect 割当。
3. **P3: 枠のプログラム描画** … 陣営色/★/名前/マナジェム。AI枠依存撤去。
4. **P4: 通貨マナジェム化** … アイコン差し替え＋文言。素材は入手方針決定後。
5. **P5: 図鑑(コレクション)も同方式へ**（別DESIGN or 本書追補）。

## 4. 検証
- realCS=0。Playでショップ5枚がユニットスプライトで表示され、**盤面の同ユニットとアニメが非連動**（片方だけ攻撃/移動しても崩れない）を確認。
- リロール/購入でプレビューが正しく更新・解放。
- 通貨表示がマナジェムに、購入で残高が正しく減る。
- パフォーマンス: RT/カメラ1枚に抑える。

## 6. ショップバー レイアウト設計（ShopBoard.png 撤去 / Duelyst素材化）
> 決定: `Assets/Images/UI/Shop/ShopBoard/ShopBoard.png`（AI生成の盤）は使用停止。
> 画面下部の全幅バーへ作り直し、下地は Duelyst CC0 の `bottom_bar_background.png`（暗いグラデ帯）に置換。

### 6.1 全体
- ルート `ShopBar`（画面下 全幅アンカー: anchorMin(0,0)/anchorMax(1,0)、pivot(0.5,0)）。高さ ≈ 画面の 22%（1080基準で約 240px）。
- 下地 = `bottom_bar_background.png`（Resources/UI/Duelyst）。横は Sliced/Stretch で全幅、縦アンカー下。上端が透明→下端が濃色なので、盤面へ自然に溶ける（=フレームを持たない）。
- **盤面はバー上端のフェードより上で終える**（GridManager の盤中心/スケールは現状維持で問題ないが、最下段ユニットがバーに食われないか実機で確認）。
- 既存 `UIShop`（`allCards`/`money`/`levelText`/`expText`/`expButton`/`expModeButton`/reroll/interest）の**参照はそのまま流用**。GameScene 上で各要素を下記3ゾーンへ再配置し、ShopBoard の Image を bottom_bar へ差し替え（または非表示）。

### 6.2 3ゾーン
左（幅≈18%）— レベル/経験値
- 下地 `status_panel.png`（紺のヘックス端プレート）。
- `levelText` = 「Lv N」大。右に細いEXPバー＋`expText`「EXP cur/next」。
- `expButton` = `button_confirm.png`（緑ヘックス枠）に「経験値 +4」＋小ジェム＋コスト数。
- `expModeButton`（単発/MAX）= その下の小ピル。

中央（幅≈58%）— ユニット5枠
- `allCards`(5) を等間隔で横一列、バー中央に。各カードは既に脱AI（コスト帯プログラム枠＋ドット絵＋名前プレート＋マナコストジェム＋シナジーバッジ）。
- `card_background.png` はほぼ白/空 → **カード地には使わない**（枠はプログラム描画のまま）。任意で各枠背後に procedural の凹み影のみ。

右（幅≈24%）— 経済/操作
- `money` = 大きめ `ManaGem.png` ＋ 数値。下地 `status_panel.png`。
- リロールボタン = `button_confirm.png`＋⟳＋コスト(ジェム＋数/無料時「無料」)。無料ストック数は右上バッジ（`GoldFreeRerollStacks`）。
- ロックトグル（ラウンド間キープ）= 小さめの錠前ボタン（Duelystボタン小 or procedural）。※ロック機能が未実装なら本レイアウトでは枠のみ確保し実装は別タスク。
- 利子ゲージ（`EnsureInterestGauge`）= マナ残高の上に小ジェムのピル列（最大5）。

その他: 売却プレビュー(`sellPreviewText`)はドラッグ中にバー中央へ従来通り表示。

### 6.3 素材割当まとめ
| 要素 | 素材 |
|---|---|
| バー地 | `UI/Duelyst/bottom_bar_background.png`（ShopBoard.png 置換） |
| Lv/マナのプレート | `UI/Duelyst/status_panel.png` |
| 経験値購入/リロール | `UI/Duelyst/button_confirm.png`(+_glow) |
| 通貨/コストアイコン | `UI/Shop/Coin/ManaGem.png`（既存GUID） |
| カード枠 | プログラム描画（`UnitCardVisual.ApplyProceduralFrame`） |
| カードの顔 | ユニットのドット絵（`UnitCardPreview`ミラー） |

### 6.4 実装ステップ（着手は承認後）
1. GameScene: ShopBoard Image を bottom_bar へ差し替え（または無効化）。ルート ShopBar を全幅下アンカーに整える。
2. 左/右ゾーンに status_panel プレートを敷き、既存テキスト/ボタンを再配置。
3. exp/reroll ボタンを button_confirm スキンへ。
4. 利子ゲージ・ロック枠の位置調整。
5. realCS=0 → Playで5枚表示/リロール/購入/残高減/売却プレビューを確認。
6. 旧 `ShopBoard/*` は未使用化後に削除可（他参照が無いこと確認）。

### 6.5 実装済み（P6a: 脱ShopBoard＋盤面配慮＋ロック）2026-07-03
- **脱ShopBoard.png**: `GameScene.unity` の ShopBoard Image の `m_Sprite` を `ShopBoard.png`(guid 3303838b…)→`bottom_bar_background.png`(guid 47fb94fb…) へ差替。scene内の当該guid参照は1箇所のみで完全に未使用化。
- **盤面への配慮**: 同 Image の `m_RaycastTarget: 1→0`。下地が盤面下段のクリック/ドラッグを遮らない。bottom_bar は上端が透明のグラデ帯なので、盤面が背景越しに見える（不透明AI盤の面積を排除）。`UIShop.EnsureShopChrome()` が実行時にも sprite/raycast を保証（scene未再取り込み時のフォールバック）。※売却判定は `UIShop.IsPointerOverShop`＝自身のRectで行うため raycast 無効化の影響なし。
- **ショップロック（新規機能）**: `UIShop` に `shopLocked` ＋ `EnsureLockToggle()`。RerollButton 直上に「固定/固定中」トグル（button_confirm スキン、JA/EN）。
  - 固定中は `RequestFreeRerollOrPending()` / `ConsumePendingFreeReroll()` の**ウェーブ間自動リロールをスキップ**し品揃えを持ち越す。
  - **手動リロール(`OnRefreshClick`)は品揃えが替わるため自動でロック解除**。売却プレビュー中はトグル無効。
- **検証(2026-07-03)**: realCS=0。Scene確認で ShopBoard sprite=bottom_bar_background / raycastTarget=false、UIShop に IsShopLocked/EnsureShopChrome/ToggleShopLock を確認。Play実挙動（トグル表示・固定時のリロールスキップ・盤面クリック透過）はビルド/Playで最終確認。
- 残: レイアウトの本格3ゾーン化（status_panelプレート敷設・左右クラスタ整列・exp/rerollのbutton_confirm化）は後続 P6b。旧 `Images/UI/Shop/ShopBoard/` は未使用化済みのため削除可。

### 6.6 実装済み（P6b: ボタンスキン＋プレート＋ShopBoard削除）2026-07-03
- **exp/rerollボタンのbutton_confirm化**: GameScene の Exp/LevelUp ボタン背景(`LevelUpButton.png` guid 74748ec5…)と Reroll ボタン背景(`RerollButton.png` guid 3c1fe9fd…)の `m_Sprite` を Duelyst `button_confirm.png`(guid 78a3133…) へ差替。両者ともこの1箇所ずつのみ参照だった。
- **status_panelプレート**: `UIShop.EnsureStatusPlate()` を追加。`levelText`/`money` の背面に Duelyst `status_panel` を1枚敷く（offsetMin/Max で対象矩形を padding ぶん広げて複製→アンカー形態非依存、raycast off、alpha0.92、兄弟インデックスで背面へ）。
- **ShopBoardフォルダ削除**: `Assets/Images/UI/Shop/ShopBoard/`（ShopBoard/LevelUpButton/RerollButton の png+meta）＋フォルダmetaを削除。事前に3 guid が scene/prefab/asset から無参照であることを grep 確認済み。
- 残(任意): 本格的なゾーン整列（プレート位置の微調整や左右クラスタの再配置数値）は実機の見た目を見て調整。`Assets/Images/UI/Shop/Coin/` フォルダ名は ManaGem 実体だが名称は据え置き（guid保持のため）。

## 7. Duelyst風・足元ひし形足場への作り直し（P7）2026-07-03
> 背景: 帯＋角丸枠＋プレート方式は「不格好」。参考画像（Duelyst実機）のように、ユニットが**足元のひし形タイルに立って下部に並び、盤面がしっかり見える**形へ。
> ユーザー決定（4択）:
> - カード表現 = **ドット絵＋ひし形足場**（枠なし。盤面のユニットのように足元のひし形に立つ。コスト帯で足場色）。
> - 素材方針 = **Claudeは生成せず**、`Assets/Resources` か `reference/duelyst/app/(original_)resources` の**既存素材のみ**使用。
> - 並び = **緩い弧（谷型）＋下地透明**。
> - 操作系 = **参考画像準拠**（マナ上部/リロール等アクション右下/デッキ・レベル左下）→ これは Phase2。

### 7.1 足場素材（既存のみ）
- Duelystの盤面タイル `reference/duelyst/app/original_resources/tiles/tile_board.png`（角丸正方形＝45°回転でひし形）と `tile_glow.png` を、`Assets/Resources/UI/Duelyst/` に **tile_platform.png / tile_platform_glow.png** として取込（metaは既存Duelyst素材のテンプレ流用・guid新規・spriteBorder0）。Resources.Load 確認済（128×128 Sprite）。

### 7.2 UICard（足元のひし形足場）
- 旧・角丸枠 `frame` は **enabled=false で撤去**。
- `EnsureFootPlatform()`：`FootSquash`（縦つぶしの親 localScaleY=platformSquashY）＋`FootDiamond`（tile_platform を45°回転＝ひし形本体）＋`FootGlow`（tile_platform_glow の下グロウ）をユニット足元に生成（icon背面, raycast off）。回転した子を親で縦つぶし＝isometric な床タイル風。
- `ApplyFootPlatform(cost)`：ひし形＆グロウを **コスト帯色**（1灰/2緑/3青/4紫/5橙）で着色。スターアップ予告時は足場を強調色に。
- 調整用 public: `platformSize`(132) / `platformSquashY`(0.55) / `platformFeetNudge`(24)。

### 7.3 UIShop（下地透明＋緩い弧）
- `ShopBoard` Image を **enabled=false**（下地透明）。status_panelプレートは撤去。
- `ApplyShopArc()`：`ShopSlotParent` の5枠を X順ソート→ Y に**谷型の弧**（中央がやや低い＝盤面の楕円下辺に沿う）を加算。X間隔は現状維持。`shopArcDepth`(26) で調整。1回のみ適用。

### 7.4 検証
- realCS=0。tile_platform/tile_platform_glow の Resources.Load 成功。ShopBoard は Play(Awake) で非表示化。
- **足場のサイズ/位置/つぶし率・弧の深さは実機（Play/スクショ）で最終調整**する前提（public値で微調整可）。

### 7.5 P8 操作系の参考画像準拠配置＋FIGHT作り直し（実装済 2026-07-03）
> スクショで現状位置を採取（Canvas基準1920x1080）＋ユーザー確認（配置=この案でOK / FIGHT=赤系Duelystボタン大）を経て実装。
- `UIShop.ApplyControlLayout()`（Awakeで1回）。座標は 1920x1080 アンカー相対px。
- **左下＝経済クラスタ**：マナ残高（アイコン`coin`(70,250)＋数字`Money`(150,250)）／`levelText`(70,190)／`expText`(195,190)／Ex購入`LevelUpButton`(160,118)。全て bottom-left(0,0) アンカーへ。
- **右下＝アクション**：`RerollButton` を bottom-right(1,0) (-500,95)。`FIGHT` を bottom-right (-175,100) / size(300,112)。
- **FIGHT作り直し**：`RestyleFightButton()` で Image.sprite を旧`notification_challenge`→Duelyst `button_cancel`（赤系）に、旧レガシーText無効化＋TMP「FIGHT」ラベル新設。
- 現在採取した現状値: levelText=PlayerLevel / expText=Exp / money=Money(＋兄弟`coin`=ManaGem) / Reroll(280x85) / Ex(LevelUpButton 280x85) / FIGHT(Canvas,300x100)。
- **検証**: realCS=0、button_cancel の Resources.Load 確認。**各座標は Play/スクショで微調整前提**（public化はしていないが定数を編集で調整可）。

### 7.6 P9 重なり解消＋通貨ジェム修正（実装済 2026-07-03, Playライブ確認）
> スクショで判明した不具合を修正。
- **通貨ジェム取り残し**: `GameObject.Find("coin")` がスロットの別"coin"を誤取得し、本物の通貨ジェム（`money`の兄弟＝ManaGemスプライト, Canvas直下(80,205)）が中央に残っていた。→ `FindCurrencyGem()`（money兄弟でManaGem）で正しく取得し左下へ。**サイズ 20→44 に拡大**。
- **ショップ枠が右寄り・広すぎ→操作系と重なる**: 枠は `ShopSlotParent` の**左端アンカー**で、私が world(canvas1920, scaleFactor1) 中心を誤算していた。→ `ApplyShopArc()` を**ワールド差分方式**に修正：`targetWorldX = Screen.width/2 + (i-中央)*spacing*scaleFactor`、`dxAnchored=(target-slot.position.x)/scaleFactor`。解像度/アンカー非依存。5枠が画面中央(470/715/960/1205/1450)に整列＝左右に操作系の余白ができる。
- **リロールとFIGHTの重なり**: リロールを FIGHT の**上に段積み**（ともに最右 wX≈1745, reroll wY210 / FIGHT wY100）。ユニット右端(≈1577)と非干渉。
- 確定座標(1920基準): gem(52,252,44²)/money(142,250)/level(52,202)/exp(150,202)/Ex(140,132,220×74)/reroll(-170,210,240×78)/FIGHT(-175,100,300×112)。spacing=245。
- Playライブで重なり解消を確認。次回Playはコードで自動適用。

## Status
- 2026-07-03 起票。
- 2026-07-03 **P1+P2実装(カードの顔=ドット絵)**: RenderTexture案は不採用。図鑑で実績のある**ミラー方式**を流用＝`UICard.Setup`で盤外(-100000)にユニットprefabを生成し`CollectionBossAnimator`でSpriteRendererを`icon`へ写す。**各カードが自前のプレビューを持つ→5枠すべて自動対応・盤面と別Animatorで非連動**。GameSceneのGameManager購読(Start)副作用は生成後にMonoBehaviour全無効化で回避(Animatorは別Componentなのでidle継続)。OnDisable/OnDestroy/Setupで盤外実体を破棄(リーク防止)。AIイラスト(EntityData.icon)は不使用に。realCS=0。残: P3枠のプログラム描画 / P4通貨文言・マナコストジェム。
- 2026-07-03 **P4先行(通貨アイコン)**: マナジェム素材確定＝`reference/duelyst/app/resources/shop/shop_premium_pack_small.png`(CC0, teal crystal 224x224)。`Coin.png`→`ManaGem.png`に**GUID保持リネーム＋中身差し替え**（旧guid 657a6a…を引継ぎ）→ Slot/T1_*/GameScene の全参照が無改変でジェムへ。文言(コイン→マナ)＋カードのマナコストジェムは後続。次はP1(プレビュー基盤)。
- 2026-07-03 **P3(カード枠)＋P4(文言)完了**: 
  - P3: `UICard.ApplyProceduralFrame(cost)` を追加。AI枠(`myData.frame`/`UI/Frame`)を撤去し、frame(icon背面) をコスト帯で色分けした角丸(内蔵UISprite 9スライス)に。色=CostTierColor(1灰/2緑/3青/4紫/5橙)。SetUpgradeReadyのdefault色にも反映。
  - P4: カードのコストアイコンは既にManaGem GUIDで済。通貨文言をマナへ＝AugmentCatalog の日本語「ゴールド/コイン」→「マナ」、英語の単独 `gold`→`mana`（ID `gold_*`/enum `AugmentTier.Gold` は正規表現の語境界で無傷を確認）。
  - 残(任意): 英語オーグメント名 "Coin Pouch"/"Goldmine"/"Coin Shine" と日本語「小銭」等の名称、P5図鑑の同方式化。realCS=0。
- 2026-07-03 **脱AIをショップ外へ横展開**: 共通部品 `UnitCardVisual`(CostTierColor/ApplyProceduralFrame) ＋ `UnitCardPreview`(盤外実体ミラー・寿命で破棄) を新規追加。**BossRewardSelectionUI / ChapterRosterUI / CollectionHubUI** の `EntityData.icon`→ユニットドット絵プレビュー、`EntityData.frame`→プログラム枠 に置換。realCS=0。
  - 残(低優先): ①LobbyUI 図鑑詳細の「アイコン表示」トグル(cdIconImage) ②UI/Prologue の物語一枚絵(3枚) ③UI/Shop/ShopBoard(盤/Lv/リロールボタン) ④英語オーグメント名 "Coin Pouch"/"Goldmine" ⑤未使用化した Images/Units/Icon(88)・UI/Frame(5) は削除可。
