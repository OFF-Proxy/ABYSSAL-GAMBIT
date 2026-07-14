# DESIGN_autoplay — オートプレイ・ハーネス（開発デバッグ専用）

状態: v1 実装済み（ハーネス＋Dump）。統計集計は次フェーズ。

## 目的
テストプレイ／通しプレイを自動で周回し、以下を発見する開発専用ツール。ユーザーには出さない。
- 進行不能箇所（一定時間進行しないラン）
- クラッシュ／例外
- （次フェーズ）極端な難易度スパイク、勝率が高すぎるユニット/アイテム/オーグメント/シナジー

進行不能・例外が起きたら `AutoPlayDumps/<日時>_<理由>/` にレポートとログを書き出し、Claude が解析→修正に使う。

## 配置・ガード
- `Assets/Scripts/Debug/AutoPlayHarness.cs`（`#if UNITY_EDITOR || DEVELOPMENT_BUILD`、`DontDestroyOnLoad`、製品ビルド非搭載）。
- 起動・停止・速度・最大章は F8 デバッグメニュー（`DebugMenu.DrawAutoPlay`）から操作。
- このファイル群を消せば機能が完全に消える設計（既存デバッグ方針に準拠）。

## ボットの方針（標準ヒューリスティック）
1. ポップアップ自動解決（優先）：オーグメント/アイテム/ボス報酬/必殺強化＝先頭候補、強化マス＝先頭種別を選び空きマスへ設置。
2. 編成フェーズ：買えるショップカードを購入→ベンチの自陣ユニットを空きマスへ配置→`DebugFight()`。
3. 戦闘中：オート戦闘に任せ、署名（フェーズ/ウェーブ/配置数/所持金）の変化で進行を監視。
4. リザルト：章クリア/ゲームオーバーを集計し、続行（ロビー復帰）。

## 制御面（呼び出すAPI）
- 章開始: `GameManager.PendingStartChapter` + `SceneManager.LoadScene("GameScene")`（ロビー時）／`RequestStartChapter`（ゲーム中）。
- フェーズ: `IsRoundInProgress` / `DebugCurrentWaveNumber` / `DebugWaveCount` / `PlacedTeam1Count`。
- 戦闘開始: `DebugFight()`。
- 購入: `UIShop.Instance.allCards` → `UICard.EntityData` → `UIShop.OnCardClick(card, data)`（所持金/ベンチ判定込み）。
- 配置: `GridManager.GetFreeNode(Team.Team1)` → `GameManager.CanPlaceEntityManually` / `TryPlaceEntityManually`。
- 各選択UI: `DebugAutoResolve()`（Augment/Item/Boss/HeroUlt）、`BuffTileRewardUI.DebugChooseFirstType()` ＋ `GameManager.DebugResolveBuffTilePlacement()`。
- リザルト: `ResultPanelUI.IsResultOpen` / `LastResultWasChapterClear` / `LastResultWasGameOver` / `DebugContinue()`。
- ヒーロー/章: `SaveManager.SetHeroUnitId` / `RecordChapterResult`。

## ウォッチドッグ（進行不能検出）
- 署名が `stuckTimeoutSec`（既定30秒・実時間）変化しなければ「進行不能」。Dump→当該ランを `RequestReturnToLobby` で中断→次設定へ。

## クラッシュ／ログ捕捉
- `Application.logMessageReceived` を購読し直近400行をリングバッファ保持。Error/Exception/Assert を検出したらラン中は1回 Dump。

## Dump 仕様
- 出力先: プロジェクト直下 `AutoPlayDumps/<yyyyMMdd_HHmmss>_<reason>/`
- `report.txt`: 理由/設定(hero,chapter)/シーン/フェーズ/ウェーブ/配置数/所持金/最終署名。
- `log.txt`: 捕捉ログ。

## 既知の制限・次フェーズ
- v1の購入/配置は素朴（コスト/シナジー最適化なし）。勝率バランス精度は限定的。
- 次フェーズ: ラン毎の統計（採用ユニット/アイテム/オーグメント/シナジー、章別クリア率・所要、勝率）をCSV/JSON＋集計レポート化。複数戦略の比較。アニメ/演出の明示スキップで更に高速化。
