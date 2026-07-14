
---

## 実装ログ (Cowork)

### Phase 1 完了 (2026-07-03)
- `Assets/Scripts/GameHudLayout.cs` を新規作成。1920x1080基準で Arena/TopHud/LeftDock/RightDock/BottomShop の領域Rect＋下部ショップ操作系(マナ/レベル/経験値/Ex/リロール/FIGHT)の anchoredPosition/size 定数を集約。`ScreenCenterX`(Screen.width/2) も提供。
- realCS=0。

### Phase 2 完了 (2026-07-03)
- `UIShop.ApplyControlLayout()` / `ApplyShopArc()` を GameHudLayout 参照へ切替（散在していた固定値を撤去）。`shopArcDepth`/`shopSlotSpacing` の既定値も GameHudLayout から。
- **FIGHT を `button_cancel`(赤) → `button_end_turn_mine`(Duelystの金ターンボタン) に差替**。ラベルは濃紺＋黒縁取りで金地でも可読。button_cancel は今後キャンセル/危険用途へ。
- reference/duelyst/app/resources/ui から必要素材を `Assets/Resources/UI/Duelyst/` へコピー: button_end_turn_mine(+glow)/button_end_turn_finished(+glow)/icon_mana/icon_gold/icon_deck/frame_quest/frame_quest_challenge/bracket_friendly/bracket_enemy/artifact_frame/unit_stats_instructional_bg/button_primary(+glow)/button_secondary(+glow)。
- realCS=0、全素材の Resources.Load 確認済。

### 各UIの改修ポイント（P3-6用メモ）
- ItemBenchCanvasUI: `EnsureExists`/`BuildUi`(Panel anchor(0,1) pos(0,-40) size(220,648))＋`Refresh`でsize再計算。背景はColor.clear。
- SynergyPanelUI: `EnsureExists`/`BuildUi`(anchor(0,1) pos(8,-70) size(154,390))。背景Color.clear。
- SynergyTooltipUI: `EnsureInstance`/`BuildUi`(size360x650)＋`MoveToFixedPosition`で毎回配置。背景=synergy_tooltip_frame。
- UnitStatusPanelUI: `EnsureInstance`/`BuildUi`(anchor(1,1) scale1.1)＋`RefreshLayout`毎フレームでpos/scale再計算。背景Color.clear。
- RoundProgressUI: `EnsureExists`(Root anchor(0.5,1) pos(0,-8) size(340,70))＋`ResizeForWaveCount`。背景は暗色直指定。
- AugmentHudUI: `EnsureExists`/`BuildUi`(anchor(1,1) pos(-18,-18))＋`AdjustPanelHeight`。背景=UI/Augment/card_panel。
- 方針: 各パネルの基準pos/sizeをGameHudLayoutのDock領域に合わせる。毎フレーム再計算メソッド(RefreshLayout等)がGameHudLayout値を参照するよう改修。実画面スクショで微調整。

### Phase 2 追修正 (2026-07-03, Playスクショ反映)
- リロール/Exボタンの `coin`(ジェム) と `cost`(数字) が重なっていた（実測8px）→ `FixButtonCostLayout()` で中央アンカーに揃え横並び(ジェム40 / 数字68)に。実測28px確保。
- FIGHT文字が見えない → ラベルは存在したが濃紺色でボタン暗部に埋没。**白(1,0.98,0.9)＋濃縁(outline0.28)** に変更して可読化。
- 経済クラスタ背面に `EnsureEconomyPanel()`（`unit_stats_instructional_bg`, α0.82, raycast off, 最背面）を敷いてマナ/レベル/経験値を読みやすく。

---

## Duelyst実コード調査 (2026-07-03) — 「無理やり感」の原因と正しい流儀

参照: `reference/duelyst/app/ui/styles/layouts/game.scss`, `.../templates/layouts/game_player.hbs`, `.../view/layers/game/*`。
DuelystのゲームHUDは **Cocos(WebGL)盤面の上に DOM＋SCSS で重ねる** 構成。要点:

### 1. パネル背景は「画像素材」ではなく「暗い半透明の角丸」
- プレイヤー枠の背景は `background: rgba(1, 0, 37, 0.75)`（＝濃紺 75%）＋border-radius。**枠画像は貼っていない**。
- → 我々が status_panel/frame等の明るい枠を貼っていたのが「無理やり感」の主因。**内蔵角丸を濃紺72%で敷く方式に変更**（Duelyst同等）。EnsureEconomyPanel を修正済。
- 今後の全ドック（左/右/上）も同じ「暗い半透明角丸」で地を作り、素材はアイコン/ボタン/肖像など"部品"だけに使う。

### 2. マナ = クリスタルicon列 ＋ シアン数字
- `.mana-icon`（`icon_mana.png` active / `icon_mana_inactive.png`）を **3.0rem×3.6rem(≈30×36px)** で横並び。数字 `.mana-count-current` は **#00E2FF（シアン）2.2rem**。
- → マナ残高数字をシアン寄りに。将来はクリスタルicon表示も検討。

### 3. エンドターン(=FIGHT)ボタン
- `button_end_turn_mine.png` を background(cover)。**中に白文字 1.9rem＋text-shadow**、line-height≈10.2rem(102px高)。hoverで `_mine_glow`。完了で `_finished`。
- → FIGHT文字は白＋影で正しい方向。サイズは控えめ(24–38px)＋影に調整。

### 4. プレイヤー枠(隅)の構成 (game_player.hbs)
- user-name / hand(icon+count) / deck(icon+count) / mana(icons+count) / general-portrait(hex 145×148px) を縦に。位置は上部・中央から ±63rem(≈630px) 左右。
- → 上部HUD(P5)は「名前＋章＋（将来）肖像hex」をこの流儀で。

### 改訂方針（P3-6）
- 地=暗い半透明角丸で統一（明るい枠素材の多用をやめる）。
- 素材は"部品"（アイコン/ボタン/肖像hex/ブラケット）に限定。
- 文字色: 見出し白＋影、数値はシアン(#00E2FF)/金など意味色。
- まず EconomyPanel を暗半透明化して方向性を確認 → 左右ドック/上部へ横展開。

### FIGHTボタンの堅牢化 (2026-07-03)
- 症状: FIGHTボタンが実行時にリセットされ、私の sprite/ラベルが消える（sprite が既定の `notification_challenge`(赤) に戻り、金＋文字が消失）。
- 原因: FIGHTの sprite はSceneで `notification_challenge`(guid 0292348b…) 固定＋文字はそのスプライトに焼き込み。実行時上書きは何かの再設定で戻る。
- 対処:
  1. **Scene の FIGHT Image sprite を `notification_challenge` → `button_end_turn_mine`(金) に変更**（既定が金。戻されても金）。
  2. `UIShop.RestyleFightButton` を **public static `StyleFightButton(GameObject, reposition)`** 化。ラベルは**シーン常駐のレガシーText("Text (Legacy)")を活用**（"FIGHT" 白）＝実行時再設定で消えにくい。無ければTMPフォールバック。
  3. `GameManager.EnsureFightButtonPresentation`（ゲーム自身のFIGHT設定箇所）からも `UIShop.StyleFightButton` を呼び、再設定時にも金＋ラベルを維持。
- realCS=0。要 再Play で「金ボタン＋FIGHT文字」が維持されるか確認。

### 経済パネル（Duelyst流の暗半透明）確定
- `EnsureEconomyPanel` の地を **明るい枠素材 → 内蔵角丸を濃紺72%(0.02,0.01,0.16,0.72)** に変更（Duelystの rgba(1,0,37,0.75) 同等）。ユーザー確認で「馴染む」。以降の全ドックもこの地で統一。

### ショップカードをオリジナルDuelyst準拠に (2026-07-04)
- 経緯: ひし形足場は**Duelyst2**の素材だった。今回使えるのは**オリジナルDuelyst**なので、`app/view/nodes/cards/CardNode.js` の実装に合わせる。
- 原Duelystのカード表示（CardNode.js）:
  - 土台 = `unit_shadow.png`（横長2:1の柔らかい黒楕円影＝床に立つ表現）。
  - コスト = `icon_mana.png`（青い六角形クリスタル）＋中央に数字（`cc.LabelTTF` font24, 塗り色 `rgb(0,33,159)` 濃紺）。
- 実装（UICard）:
  - `EnsureFootPlatform` を **unit_shadow の丸影**に置換（45°ひし形・グロウ・つぶしを撤去）。`tile_platform`不使用に。
  - `EnsureCostBadge` を新規: `cost` テキスト背面に `icon_mana` を敷き、数字を中央・濃紺(0,33,159)＋薄白縁に。
  - スターアップ予告時は影を薄く強調色に（通常は影のまま）。
  - `unit_shadow.png` を reference/duelyst/app/resources/ui から Assets/Resources/UI/Duelyst へ取込。`icon_mana` は取込済。
- realCS=0。要 再Play で「丸影＋青六角形コスト」を確認。既存のコスト表示(旧ジェム)が二重なら非表示化する。

### ショップカード仕上げ修正 (2026-07-04)
- ①土台が見えない → `unit_shadow` の影を濃く(α0.5→0.72)＋大きく(1.35×0.7)し、明るい背景でも見えるように。
- ②カード上の旧コスト表示(ManaGemの"coin") → `EnsureCostBadge` で `SetActive(false)`。新しい icon_mana 六角形バッジと二重にしない。
- ③所持マナのアイコン → `UIShop.ApplyControlLayout` で `icon_mana`（数字なしの青六角形）へ差替(preserveAspect)。カードのコスト用 icon_mana とは別物（あちらは中央に数字入り）。
- realCS=0。要 再Play確認。

### 丸背景リング card_background の追加 (2026-07-04)
- Duelystコード調査（`BottomDeckCardNode.js`）での使い分け（`showCardBackground()`）:
  - `card_background`(通常)=既定。暗い円＋白い弧。
  - `card_background_disabled`=**プレイ不可時**（マナ不足/自ターンでない等）。暗い円のみ。
  - `card_background_highlight`=**ホバー/選択/強調時**。シアン発光弧。
  - `card_background_replaced`=**引き直し(mulligan)後**。
  - 原カードは z=-3 の最背面に置き、`shadowSprite(unit_shadow)`＋`manaTokenSprite(icon_mana)`＋数字＋`ptcl_ring_glow_circle`(粒子リング) を重ねる。
- 実装(UICard.EnsureFootPlatform):
  - `card_background`(140²) を **X軸回転(cardBgTiltX=58°)** で「地面に寝た円」にして最背面に敷き、その上に `unit_shadow`(影)＋ユニット。
  - スターアップ予告時は `card_background_highlight`(シアン)へ切替（`SetUpgradeReady`）。
  - 調整用 public: `cardBgTiltX`(角度) / `cardBgScale`(大きさ)。
  - 4種(normal/disabled/highlight/replaced)を Assets/Resources/UI/Duelyst へ取込済。
- realCS=0。要 再Play確認。※ScreenSpaceOverlayでX回転が意図通り描画されない場合は scaleY 圧縮方式に切替予定。

### ショップカード調整3点 (2026-07-04)
- ①コスト表示をカード中央下へ：`EnsureCostBadge` で icon_mana バッジ＋数字を anchor(0.5,0) 中央下(`cardCostBottomY`=16)に固定。数字は autosize切りの fontSize23。ConfigureCardText後に再適用されるよう Setup 末尾でも `EnsureCostBadge()` 呼び出し。
- ②ショップ枠を全体的に上へ：`GameHudLayout.ShopSlotRaise`(58)を `ApplyShopArc` の各枠Yに加算。弧で下がる中央枠のコスト表示が隠れないように。
- ③card_background の状態切替：`UICard` に `IPointerEnter/ExitHandler` を実装し `RefreshCardBackground()` を追加。ホバー/スターアップ=highlight、購入不可(`PlayerData.CanAfford`)=disabled、通常=normal。マナ増減時は `UIShop.Refresh` から全カードの `RefreshCardBackground()` を呼んで追従。原Duelyst `BottomDeckCardNode.showCardBackground` の状態遷移に対応。
- realCS=0。※ホバーが効かない場合はカードのレイキャスト対象オブジェクトへハンドラを移す。
