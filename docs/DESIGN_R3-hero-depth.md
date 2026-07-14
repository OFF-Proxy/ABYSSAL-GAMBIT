# DESIGN_R3-hero-depth: ヒーロー深掘り3要素

> 設計＋実装: Claude/Cowork（2026-06-07, MCP実装）
> 状態: ✅ 実装済み（コンパイル0）。残: 実機体感・数値調整（R3-balance）。
> 関連: [DESIGN_R3-hero-units.md] / [DESIGN_R3-hero-select.md] / [DESIGN_R3-bossfeel.md]（ヒーロー必殺の基礎）

ユーザー要望「ヒーローを面白くする要素」を3つ実装。数値は暫定（R3-balance）。

## 1. 必殺アップグレード選択（ラン中1回）
- ラン開始時に1回、ヒーロー別の2択で必殺を強化（`HeroUltUpgradeUI`、モーダル・Time.timeScale=0）。
- 選択結果 `GameManager.HeroUltUpgrade`（0/1/2、ラン中保持・非永続）で `UseHeroUltimate` を分岐。
- 選択肢:
  - Aldin 聖盾: A=シールド 30%→45% / B=効果時間 6s→10s
  - Kagachi 修羅: A=与ダメ +35%→+55% / B=攻速 ×1.25→×1.45
  - Vesna 蒼炎: A=敵威力 ×1.6 / B=敵スタン0.6s 付与
- トリガ: 章モードのラン初期化で `pendingHeroUltUpgrade=true` → Start 末尾で `HeroUltUpgradeUI.Show`。コア戦は対象外。

## 2. ヒーロー別 開始ボーナス＋オーラ
- 開始ボーナス（`ApplyHeroStartingBonus`、章モードのラン初期化）:
  - Aldin=防御アイテム1個をベンチ付与 / Kagachi=+4ゴールド / Vesna=無料リロール+1（`UIShop.GrantFreeRerollStack`、新設）
- オーラ（`ApplyHeroAura`、戦闘開始時。盤面に主人公がいる時のみ、味方全体に1ウェーブ）:
  - Aldin=被ダメ-8% / Kagachi=与ダメ+8% / Vesna=攻速×1.08

## 3. 主人公は将（復活/弱体）
- ヒーローが倒れる時、1戦1回は自動復活（HP40%）。`BaseEntity.Die` に `TryConsumeHeroReviveForUnit` フックを追加（オーグメント復活より前）。
- 復活ぶんを使い切って再度倒れると、残りウェーブ中 味方全体を弱体（与ダメ-15%・攻速×0.9、`ApplyHeroFallenWeaken`、1戦1回）。
- フラグ `heroReviveUsedThisWave` / `heroFallenWeakenApplied` は戦闘開始(DebugFight)でリセット。

## 4. 秘力（Arcane）系を「マナ促進＋スキル威力」へ
- 新規 `BaseEntity.ApplyTimedSynergyPowerBonus(amount, dur)`（時限の秘力加算。`synergyPowerBonus`→`GetItemFocusMultiplier`経由でスキル威力/効果時間を伸ばす）。
- 強化マス Arcane: 攻速/与ダメ → **マナ獲得×1.5＋秘力+35%＋即時マナ+20**（60s）。
- ヒーロー・オーラ Vesna（秘力）: 攻速 → **マナ獲得×1.18＋秘力+12%**。
- 強化マス報酬UIの秘力マス説明も更新。

## 5. 必殺の効果表示
- `GameManager.GetHeroUltimateDescription(ja)`（選択アップグレード反映の効果文）。
- `HeroUltButtonUI` にホバーツールチップ（必殺名＋効果）を追加。

## 6. 必殺カットイン演出（`HeroUltCutInUI`・新規）
- 必殺発動時に全画面カットイン（戦闘は止めない・raycast off・unscaled・約1.1s）。
- 構成: 斜めの二段カラーバンド（左右から流入）＋ヒーロー立ち絵(HeroArt)スライドイン＋必殺名＋アップグレードのタグ＋全画面フラッシュ＋カメラシェイク。
- 専用SE（他ユニット未使用のアナウンサーボイスを流用）: Aldin=lyonar / Kagachi=songhai / Vesna=vanar（`Resources/sfx`、自前AudioSource）。
- **ヒーロー別＋アップグレードで色/タグが変化**: 例 Vesna A=業火(橙)/B=追雷(電青)、Kagachi A=修羅烈(赤)/B=神速(橙)、Aldin A=鉄壁(金)/B=不屈(青金)。

## 触ったファイル
- `Assets/Scripts/GameManager.cs`（状態フィールド・ApplyHeroStartingBonus/ApplyHeroAura/IsHeroOnBoard/TryConsumeHeroReviveForUnit/ApplyHeroFallenWeaken/OnHeroUltUpgradeChosen・UseHeroUltimate のアップグレード分岐・ラン初期化/戦闘開始の配線）
- `Assets/Scripts/BaseEntity.cs`（Die にヒーロー復活フック）
- `Assets/Scripts/UIShop.cs`（`GrantFreeRerollStack` 新設）
- `Assets/Scripts/HeroUltUpgradeUI.cs`（新規・2択UI）

## 受け入れ基準
- [ ] ラン開始で必殺アップグレードの2択が出て、選んだ強化が必殺に反映される。
- [ ] 選んだヒーローで開始ボーナス（アイテム/ゴールド/無料リロール）が付く。
- [ ] 盤面に主人公がいると戦闘開始で味方に該当オーラが乗る。
- [ ] 主人公が倒れると1戦1回復活し、復活後に再度倒れると味方弱体＋告知が出る。
- [ ] Compilation 0（確認済み）＋プレイモードで上記を実機確認。

## 未決（R3-balance）
- 各数値（シールド%、与ダメ%、オーラ量、復活HP%、弱体量）は暫定。
- 将来: ヒーロー永続成長（ボスのアフィニティ流用）、相性ユニット等は別タスク。
