# DESIGN_R3-hero-scale — ヒーローのスケール育成

状態: Phase1＋Phase2 実装済み（2026-06-08, Cowork/Unity MCP）。

## 目的
ヒーロー（HeroAldin/Kagachi/Vesna）はショップの星上げ対象外で、終盤は cost4/5 ユニットに対して相対的に弱かった。
今回のバランス調整（敵 cost4/5 を★2上限）で「ヒーローを必ず出さないと厳しい」強要感も上がるため、
ヒーローがラン進行で育ち、かつ盤面の枠を圧迫しない形にする。

## 確定仕様（ユーザー合意）
- スケール軸: ①ラウンド/章の進行 ②撃破数(XP) ③盤面の強さ の全て。
- 伸びる中身: ステータス（HP/攻撃）＋必殺の威力。
- 永続性: 基礎ステータスは「章の進行度で自動上昇」＋「編成画面で手動強化」の二段。
- スターアップ: ショップ購入以外の方法（チャプターラン中に上がる）。
- ヒーローは盤面の配置枠を消費しない（無料枠）。

## Phase1（実装済み）
- **盤面枠フリー**: `GameManager.PlacedTeam1Count` がヒーローを除外。配置上限判定もヒーローはスキップ
  （`CanPlaceEntityManually`）。→ ヒーローは常に追加で出せる。
- **ラン内ヒーローLv**（`heroRunXp`/`heroRunLevel`、ラン毎に `ResetHeroRunProgress`）:
  - XP源: ウェーブクリアで「敵数＋2」加算（撃破数＝倒した敵＝そのウェーブの敵数、＋ラウンド進行ぶん2）。
  - Lv = 1 + XP/8（上限Lv20）。Lvアップ時に `ScorePopupUI` で通知。
- **ステータス倍率** `HeroTotalStatMultiplier` = メタ底上げ × ラン内Lv × 盤面ボーナス:
  - メタ底上げ `HeroMetaBaseMultiplier` = 1 + クリア済み章数 × 0.04（章進行の自動上昇ぶん）。
  - ラン内Lv = 1 + (Lv-1)×0.05（Lvごと+5%）。
  - 盤面ボーナス = 出している味方ユニット数 × 0.02（盤面の強さ連動）。
  - 適用: `BaseEntity.heroScaleMultiplier`（ApplyCurrentStats 内で HP/攻撃に乗算。非複利）。`ApplyHeroScaling` が
    戦闘開始(`StartRound`)とLvアップ時に set → `RefreshDerivedStats`。
- **必殺の威力** `HeroUltPower` = 1 + (Lv-1)×0.06。`UseHeroUltimate` と `GetHeroUltimateDescription` の両方に適用
  （表示値と実値を一致させる）。
- **自動スターアップ**（非ショップ）: `HeroStarForLevel` Lv5→★2 / Lv10→★3。`ApplyHeroScaling` で `ApplyStarLevel`
  （★アップ演出込み）。

## Phase2（実装済み）
- **手動育成ポイント**（ロビーの「ヒーロー育成」画面）:
  - セーブに per-slot の `heroPoints`（ラン終了で獲得）と per-hero の `heroMastery[].manualLevel` を追加
    （`SaveData`/`SaveManager`、JsonUtility+List<T> DTO。旧セーブは既定0で後方互換）。
  - ポイント獲得: 章クリアで `10+章×5`、敗北でも `4+章×2`（リトライの足し）。
  - `SaveManager`: `GetHeroPoints`/`AddHeroPoints`/`GetHeroManualLevel`/`GetHeroManualStatMultiplier`
    （+3%/Lv, 上限Lv15）/`GetHeroManualUpgradeCost`（20+Lv×10）/`TryUpgradeHeroManual`。
  - `HeroMetaBaseMultiplier` に手動ぶん `GetHeroManualStatMultiplier(heroUnitId)` を乗算。
  - UI: `LobbyUI` のタイトルメニューに「ヒーロー育成」追加 → 3ヒーローの手動Lv/ボーナス%/所持ポイント表示、
    ポイント消費で手動Lvアップ（`BuildHeroUpgradeView`/`RefreshHeroUpgrade`/`OnHeroUpgradeClicked`）。
- 残（任意）: 常設の「ヒーローLv」HUD表示（現状はポップアップ＋ユニットパネルのステで確認）。

## Phase3（実装済み）＝ボスをヒーローとして採用・育成、ロスター選択
- **採用機構**: `IsActiveHeroUnit`/`IsHeroUnit`（基本3＋現アクティブ）/`IsHeroCandidateUnlocked`（基本3常時、章ボスは `HasBossAlly` で解放）。盤面枠フリー・育成スケール・自動復活・盤面検出・ショップ重複除外・`GrantHeroUnit`/`ChangeHeroUnit` をアクティブヒーロー対応に。ボスの必殺は `UseHeroUltimate` の default 経路。
- **ロスターUI**: ヒーロー育成画面をグリッド化（基本3＋章ボス13）。カードで主人公を選択（`SetHeroUnitId`）、解放済は強化ボタン、未解放は**シルエット＋「第N章クリアで解放」**。使用中カードを金枠＋「◆使用中」。
- **既知の見た目残**: ボスを主人公にした時、メインロビーの立ち絵は HeroArt が無いため非表示（将来ボス立ち絵を割当可）。

## 触ったファイル（Phase1）
- `Assets/Scripts/BaseEntity.cs`: `heroScaleMultiplier` フィールド＋ApplyCurrentStats へ乗算。
- `Assets/Scripts/GameManager.cs`: 盤面枠フリー、`heroRunXp/Level`、`AddHeroRunXp`/`ResetHeroRunProgress`/
  `ApplyHeroScaling`/`HeroTotalStatMultiplier`/`HeroMetaBaseMultiplier`/`HeroBoardBonus`/`HeroUltPower`/
  `HeroStarForLevel`/`GetHeroEntityOnBoard`、StartRound・ウェーブクリアへの配線、必殺の威力スケール。

## 数値メモ（R3-balance で調整）
Lvごと+5%（ステ）/+6%（必殺）、章クリア+4%、盤面1体+2%、Lv5/10で★。体感を見て係数調整。
