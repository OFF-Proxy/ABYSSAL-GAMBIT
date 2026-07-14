# docs インデックス

> AutoChessBossRush のドキュメント一覧。各ファイルの目的と現在の状態を一望するための地図。
> 最終更新: 2026-06-02 / 運用は [COLLAB_PROTOCOL.md](COLLAB_PROTOCOL.md)・[ROADMAP.md](ROADMAP.md) に従う。

状態の凡例: ✅ 実装済み / 🟡 一部実装（残あり） / 📋 計画・仕様（実装前） / 📚 参照・常時更新

---

## 1. 運用・引き継ぎ（常時参照）

| ファイル | 目的 | 状態 |
|---|---|---|
| [ROADMAP.md](ROADMAP.md) | 全体方針・エピック（E1〜E6）・進行メモ。**真実のソース** | 📚 常時更新 |
| [COLLAB_PROTOCOL.md](COLLAB_PROTOCOL.md) | Claude（設計）／Codex・Claude Code（実装）の協業ルール | 📚 参照 |
| [CLAUDE_HANDOFF.md](CLAUDE_HANDOFF.md) | 大きな変更の引き継ぎサマリ（差分・新規ファイル・主要API） | 📚 常時更新 |
| [STEAM_READINESS_STANDARDS.md](STEAM_READINESS_STANDARDS.md) | **Steam審査に落ちないための作成基準・品質ゲート・現状リスク監査** | 📚 常時参照 |
| [QUESTIONS.md](QUESTIONS.md) | 実装中の設計上の疑問キュー（Codex→Claude） | 📚 現在アクティブ質問なし |

## 2. 設計書（DESIGN_*）

| ファイル | 目的 | 状態 |
|---|---|---|
| [DESIGN_R4-collection-hub.md](DESIGN_R4-collection-hub.md) | コレクション＋ショップ選抜ハブ（準備中要素の実装・製品版化） | ✅ 実装済み（残: 実機UI確認） |
| [DESIGN_R3-faction-hero-gate.md](DESIGN_R3-faction-hero-gate.md) | 陣営シナジー：1体目は主人公が同陣営必須／3+は不問／主人公同陣営で増幅 | ✅ 実装済み（残: 実機balance） |
| [DESIGN_R3-hero-formation.md](DESIGN_R3-hero-formation.md) | 主人公ごとの専用フォーメーション（6マス・固有バフ・ガイド/ライブ表示） | ✅ 実装済み（残: 実機balance） |
| [DESIGN_R3-midboss-synergy.md](DESIGN_R3-midboss-synergy.md) | 仲間化できる中ボス19体のシナジー個性化（switch＋DB None化） | ✅ 実装済み（残: 実機balance） |
| [DESIGN_R3-chest-room.md](DESIGN_R3-chest-room.md) | チェスト報酬ラウンド（殴って開ける宝箱・コイン/アイテム・30秒） | 🟡 実装済み（残: HUDタイマー/実機balance） |
| [DESIGN_R1-persist.md](DESIGN_R1-persist.md) | 永続化層（ISaveStore/LocalJsonSaveStore/SaveManager）の抽象化 | ✅ 実装済み（レビュー承認） |
| [DESIGN_R1-meta.md](DESIGN_R1-meta.md) | 章をまたぐボス仲間化・永続roster（**差別化の核**） | ✅ 実装済み（残: 実機1周＋balance調整） |
| [DESIGN_R1-score.md](DESIGN_R1-score.md) | スコア定式化・ベスト保存・ライブ加点表示 | ✅ 実装済み（レビュー承認） |
| [DESIGN_AUGMENT_FIXES.md](DESIGN_AUGMENT_FIXES.md) | オーグメント動作不良の修正 | ✅ 実装済み（E3で着地） |
| [DESIGN_UI_POLISH.md](DESIGN_UI_POLISH.md) | HUD／関連UIの見栄え修正 | ✅ 実装済み |
| [DESIGN_skill_overhaul.md](DESIGN_skill_overhaul.md) | 汎用スキル共有17体の固有化＋遠距離監査 | ✅ 実装済み（全45体固有化） |
| [DESIGN_boss_arcana.md](DESIGN_boss_arcana.md) | 最終ボス「アルカナ」アート＆統合仕様 | 🟡 ゲーム実装済み／最終アート発注待ち |
| [DESIGN_arcana_integration.md](DESIGN_arcana_integration.md) | アルカナのスプライト整列＆Unity統合 | ✅ 実装済み |
| [DESIGN_chapter2.md](DESIGN_chapter2.md) | チャプター2（全33R／最終ボス Skyfalltyrant） | ✅ 実装済み（静的検証済／残: 実機進行） |
| [DESIGN_chapter3.md](DESIGN_chapter3.md) | チャプター3（全33R／章ボス報酬 Maehvmk／ギミック登場） | ✅ 実装済み（コンパイル0／残: 実機1周・balance） |
| [DESIGN_lobby.md](DESIGN_lobby.md) | タイトル兼ロビー画面（章選択／ボス仲間／オプション） | ✅ 実装済み（残: showLobbyOnBoot ON化・実機検証） |
| [DESIGN_handoff_claudecode.md](DESIGN_handoff_claudecode.md) | デバッグ機能／シナジー死亡カウント維持／章2静的検証 | ✅ 実装済み |
| [DESIGN_R2-recruit.md](DESIGN_R2-recruit.md) | 中ボス／章ボスの仲間化・ショップ解放メタ進行 | ✅ 実装済み（章1-2＋FW）。残: 実機検証・R3 |
| [DESIGN_R1-saveslots.md](DESIGN_R1-saveslots.md) | セーブデータスロット（3）＋削除機能 | ✅ 実装済み。残: 実機検証 |
| [DESIGN_R1-collection.md](DESIGN_R1-collection.md) | ボス図鑑＋ボス育成（アフィニティ）＋図鑑詳細 | ✅ 実装済み。残: 実機検証・R3balance |
| [DESIGN_board-gimmicks.md](DESIGN_board-gimmicks.md) | 盤面ギミック（配置フォーメーション＋転がる巨大物） | ✅ 実装済み。残: 実機検証・R3 |
| [DESIGN_R2-coremode.md](DESIGN_R2-coremode.md) | コア破壊モード（新モード） | 🟡 Phase 1+2+3 実装済み。残: 実機スモーク・balance |
| [DESIGN_cost4-units.md](DESIGN_cost4-units.md) | コスト4ユニット5体の追加（固有スプライト） | ✅ 実装済み（アート/DB/シナジー/固有スキル/JA-EN名）。残: R3-balance |
| [DESIGN_R2-rewards.md](DESIGN_R2-rewards.md) | 中ボス報酬の刷新＋強化マス | ✅ 実装済み（コンパイル0）。残: 実機検証・balance |
| [DESIGN_R3-bossfeel.md](DESIGN_R3-bossfeel.md) | ボス固有ギミック／ヒーロー必殺／育成パッシブ | ✅ 実装済み（コンパイル0）。残: 実機検証・balance |
| [DESIGN_R3-hero-units.md](DESIGN_R3-hero-units.md) | ヒーロー専用ユニット3体（f1/f2/f6_general） | ✅ 実装済み（DB/排他/必殺）。残: 数値R3-balance |
| [DESIGN_R3-hero-select.md](DESIGN_R3-hero-select.md) | セーブ作成時のヒーロー選択 | ✅ 実装済み（選択画面+立ち絵、実機で表示確認）。残: 全フロー手動確認 |
| [DESIGN_R3-hero-depth.md](DESIGN_R3-hero-depth.md) | ヒーロー深掘り（必殺強化/開始ボーナス＋オーラ/将） | ✅ 実装済み（コンパイル0）。残: 実機体感・balance |

> 設計書を新規に書く時のテンプレは `autochess-implementation` スキルの `references/design-doc-format.md` を参照。

## 3. 事業・リリース・素材

| ファイル | 目的 | 状態 |
|---|---|---|
| [MARKET_POSITIONING.md](MARKET_POSITIONING.md) | 市場リサーチ・差別化・ポジショニング提案（Steam PC） | 📚 参照（2026-05 スナップショット） |
| [RELEASE_PLAN.md](RELEASE_PLAN.md) | Steam リリースまでの工程表・task-id 付きタスク | 📚 常時更新 |
| [R4_STORE_PAGE_DRAFT.md](R4_STORE_PAGE_DRAFT.md) | Steam ストアページ草案（JA/EN） | 🟡 ドラフト（公開前） |
| [ARCANA_COMMISSION_BRIEF.md](ARCANA_COMMISSION_BRIEF.md) | アルカナのアート発注ブリーフ（絵師へそのまま渡せる） | 📋 発注用（外部向け） |
| [DUELYST_LOBBY_ASSETS.md](DUELYST_LOBBY_ASSETS.md) | ロビー実装に必要な duelyst 素材の確定マニフェスト | 📚 参照 |

---

## 補足

- 各 DESIGN 文書の冒頭ブロックに同じ状態表記（✅/🟡 など）を記載済み。詳細はそちらを参照。
- 実装の最新進行は常に [ROADMAP.md](ROADMAP.md) の「進行メモ」末尾が最も新しい。
- このインデックスは手動更新。新しい docs を追加したら本ファイルにも1行足すこと。
