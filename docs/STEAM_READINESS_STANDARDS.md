# STEAM_READINESS_STANDARDS — Steam リリース作成基準

> 作成: 2026-06-25 / 対象: **Steam (PC) 買い切り**（[RELEASE_PLAN.md](RELEASE_PLAN.md) 準拠）
> 目的: **「Steam の審査に落ちない」** ことを最低ラインとした、毎ビルドで満たすべき品質ゲートと、現状の引っ掛かりリスクの棚卸し。
> 使い方: 新規実装・素材追加・章追加の度に §4 のチェックリストを参照し、リリース候補ビルドは §4 全項目 + §6 ゲートを通す。
> 関連: [RELEASE_PLAN.md](RELEASE_PLAN.md)（工程・タスク） / [R4_STORE_PAGE_DRAFT.md](R4_STORE_PAGE_DRAFT.md)（ストア文面） / [MARKET_POSITIONING.md](MARKET_POSITIONING.md) / [COLLAB_PROTOCOL.md](COLLAB_PROTOCOL.md)

---

## 1. 結論（先に要点）

Steam の事前審査（Valve のレビューチーム）は、想像されるほど「面白さ」を見ない。実際に確認されるのは主に **(a) ビルドがちゃんと起動するか / (b) ストアページの記載と中身が一致しているか / (c) 法的に問題ない（権利・年齢区分）か / (d) 課金は Steam Wallet 経由か** の4点。出典は §7。

本作にとっての朗報: **アート・音楽・SFX の大半は Duelyst（OpenDuelyst）由来で、全て CC0（パブリックドメイン）**。商用利用・改変・再配布が無制限で帰属表示も不要なので、**著作権リジェクトのリスクは低い**（§3）。

残る現実的なリスクは「**完成度の見せ方（Coming Soon の扱い）**」「**タイトル/陣営名の商標**」「**年齢レーティングとストア整合**」「**一部の出所不明 SFX**」に集約される（§5）。

---

## 2. Steam 審査で実際に確認される項目（出典: Steamworks 公式）

### 2.1 ビルド審査（Product Build Review）
- **全対応OSで正常に起動すること。** ストアページに記載した全OSで起動・プレイ可能であること。
- **ストアに記載した機能は、提出ビルドに全て実装済みであること。** 「将来追加予定」の機能はストアの Basic Info から外すこと。
- **ゲーム内課金は必ず Steam Wallet 経由。** 他ストアへの導線は不可。
- 提出は **near-final（ほぼ最終版）** であること。承認後の更新は自由。

### 2.2 ストアページ審査（Store Presence Review）
- **発売時に存在する機能・コンテンツのみ掲載。** 未実装のものをスクショ/トレーラー/説明に出さない（出すなら「未実装」と明記）。
- **カプセル画像に、読めるタイトル/ロゴを入れる。**
- **スクリーンショットは原則ゲーム画面のみ。** コンセプトアート、プリレンダーの静止画、受賞ロゴ、宣伝コピー入り画像は不可。
- **説明文は具体的で筋が通っていること。** 外部サイトへのリンクを説明欄に貼らない。

### 2.3 提出・運用の前提
- **Steam Direct 手数料**（1作品あたり）と、**Onboarding（税・銀行情報）** の完了。
- レビュー所要は **ストア/ビルドとも 3〜5 営業日**。希望公開日の **7 営業日前まで**に「Mark as ready for review」。
- 一度承認されれば、以後の更新で再審査は不要。

### 2.4 年齢レーティング / コンテンツサーベイ
- **Mature Content Survey（コンテンツ申告）** に正直に回答。暴力・流血・性的表現などの有無を申告する。
- **ドイツ向けは年齢レーティングが必須**、インドネシア等も地域別の扱いあり。
- 本作は「ファンタジーの戦闘・軽度の幻想的暴力（流血表現は控えめ）」想定 → 申告は正直に。性的表現は無し。

---

## 3. 本作のアセット法務（権利の現状）

### 3.1 Duelyst 由来アセット = CC0（最重要・確認済み）
- ユニット/エフェクトのスプライト、UI 素材、**音楽（`Assets/Resources/music/*`）**、**SFX/アナウンサー音声（`Assets/Resources/sfx/sfx_announcer_*` 等）** は OpenDuelyst（`github.com/open-duelyst/duelyst`）由来。
- 同リポジトリは **`LICENSE`・`COPYING` ともに CC0-1.0**（コードもアセットも丸ごとパブリックドメイン）。**商用可・改変可・帰属不要**。→ Steam の著作権リジェクトに対して堅い。
- 念のため運用ルール: **新規にアセットを足す時は、出所と CC0（または商用可ライセンス）を確認**してから取り込む。混入した「出所不明」素材が一番危ない。

### 3.2 商標（CC0 でも残る論点）
- CC0 が放棄するのは **著作権**であって **商標** ではない。
- **`Duelyst` の名称・ロゴ（`brand_duelyst.png` 等）は使わない。** 現状リポジトリにロゴ画像は未取り込み（OK）。ただしコード内に内部フォルダ名 `Resources/UI/Duelyst/...` が残る（プレイヤーには非表示なので審査上は無害。整理は任意）。
- **陣営名 `Lyonar / Songhai / Magmar / Vetruvian / Abyssian / Vanar`** は Duelyst の世界観由来でプレイヤーに表示される（シナジー名）。登録商標は主に「Duelyst」自体と考えられ、陣営名単独の商標リスクは相対的に低いが、**完全に避けたいなら独自名へ改称**するのが安全（§5-③）。
- ゲームの**タイトルに "Auto Chess" を使わない**（Drodo の商標連想）。ジャンルはタグ/説明文の "Auto-Battler" で担保（[R4_STORE_PAGE_DRAFT.md](R4_STORE_PAGE_DRAFT.md) 既述）。

### 3.3 フォント
- `Assets/Resources/Fonts/BIZUDPGothic-Bold.ttf`（BIZ UDPゴシック, Morisawa）は **SIL Open Font License** で**埋め込み・商用配布可**。本文フォントとして安全。
- `Assets/TextMesh Pro/Examples & Extras/Fonts/*`（Anton/Bangers/Oswald/Roboto/Liberation/Electronic Highway Sign）は TMP サンプル。**実際に使っていないなら配布物から除外**するのが無難（特に "Electronic Highway Sign" はライセンス要確認）。

---

## 4. リリース作成基準チェックリスト（毎リリース候補で確認）

### A. 法務 / IP
- [ ] 新規素材は全て CC0 もしくは商用可ライセンスで、出所をメモしてある。
- [ ] `Duelyst` の名称・ロゴをプレイヤーに見せていない。タイトルに "Auto Chess" を使っていない。
- [ ] 使用フォントが商用配布・埋め込み可（OFL/Apache 等）。未使用サンプルフォントを同梱していない。
- [ ] （推奨）ゲーム内に **クレジット/謝辞画面**があり、Duelyst (Counterplay Games) 由来素材を CC0 として明記。CC0 上の義務ではないが、誠実さ・商標面の安全マージンになる。

### B. 技術 / 起動
- [ ] 記載した全OS（最低 Windows x64）で**ダブルクリックから正常起動**し、メインメニューに到達する。
- [ ] **クラッシュ無し**で 1 章を通しでクリアできる（少なくとも 30 分の連続プレイで落ちない）。
- [ ] **終了手段**がある（`Application.Quit` ＝実装済み）。Alt+F4 でも安全に終了。
- [ ] **解像度 / 全画面 / 言語 / 音量 / 品質** を設定でき、再起動後も保持（SettingsStore＝実装済み）。
- [ ] セーブが `persistentDataPath` に原子的書き込みで保存され、破損時に退避（LocalJsonSaveStore＝実装済み）。
- [ ] Console に致命的な赤エラーが出続けていない（NullReference の垂れ流し等）。

### C. ストアページ整合
- [ ] スクショ・トレーラーは**実機ゲーム画面のみ**。未実装機能を映していない。
- [ ] 説明文に載せた機能が**ビルドに全て入っている**（入っていない物は記載から外す）。
- [ ] カプセルに**読めるタイトル/ロゴ**。説明欄に**外部リンクなし**。
- [ ] 価格・対応言語（JA/EN）・対応OSの記載が実態と一致。

### D. コンテンツ完成度
- [ ] **「Coming Soon / 準備中」のデッドエンドが、買い切り体験の中心動線に出てこない**（§5-① 参照。EA で出すなら EA 説明で担保）。
- [ ] プレースホルダのテキスト/アイコン（`general_unknown` 流用など）が**主要導線に露出していない**。
- [ ] 全ユニット/スキルに固有の説明文があり、実数値とズレていない。
- [ ] JA/EN 両方でテキスト欠落・はみ出し・片言語残りが無い。

### E. UX / オプション
- [ ] 初見プレイヤーが**操作とゴールを理解できる**（最低限のオンボーディング/ヒント）。
- [ ] 主要操作にキー説明があり、設定で確認できる（Controls タブ＝実装済み）。

### F. 年齢レーティング
- [ ] Mature Content Survey を**正直に**回答（暴力＝幻想的/軽度、性的表現＝無し、で申告）。
- [ ] ドイツ等の必須地域レーティングに対応（Steam の年齢申告フローで処理）。

### G. Steam 統合（任意だが推奨）
- [ ] Steamworks SDK 連携（最低限：起動確認）。実績/クラウド/リーダーボードは差別化に有効（[RELEASE_PLAN.md] M4）。
- [ ] 課金要素を入れる場合は **Steam Wallet 経由のみ**。

---

## 5. 現状リスク監査（2026-06-25・優先度順）

| # | リスク | 重大度 | 状態/根拠 | 対処方針 |
|---|---|---|---|---|
| ① | ~~「Coming Soon / 準備中」がロビーに複数~~ → **対処済み（実装・製品版方針）** | ✅ 済 | 章は全20章を進行解放で実装済み（コメントが古かっただけ）。準備中だった「ユニット編成」「ショップ選抜」を **コレクション＋ショップ選抜ハブ（`CollectionHubUI`）として実装**（[DESIGN_R4-collection-hub.md]）。買い切り製品版として完成形にする方針。 | 残: 実機でハブUIの見栄え・ショップ反映を確認。 |
| ② | ~~productName が `AutoChess Boss Rush (Alpha)`~~ → **対処済み** | ✅ 済 | `Abyssal Gambit` に改名（productName＋タイトルロゴ、commit e6540271）。"Auto Chess"／"(Alpha)" を除去。 | 完了。正式タイトル変更時はここを更新。 |
| ③ | **陣営名が Duelyst 由来（Lyonar 等）でプレイヤー表示** | 🟡 中 | `SynergyType.cs`。CC0 で著作権はクリアだが商標観点では独自名が安全。 | 完全に避けたいなら独自名へ一括改称（ローカライズ表だけ差し替えれば内部 enum は維持可）。優先度は ① の次。 |
| ④ | **出所不明の SFX が混在** | 🟡 中 | `sfx/notification.wav` `pointdrop.wav` `select.wav` `Sell.mp3` は Duelyst 命名規則と異なる。 | 出所・ライセンスを確認。不明なら CC0/商用可素材へ差し替え。 |
| ⑤ | **未使用サンプルフォント同梱** | 🟢 低 | `TextMesh Pro/Examples & Extras/Fonts/*`。 | 配布ビルドから除外（未使用確認の上で削除）。 |
| ⑥ | **クレジット/謝辞画面が無い** | 🟢 低 | 監査時点で未確認。 | Duelyst (CC0) と使用 OSS/フォントを記す簡易クレジットを追加（誠実さ＋商標マージン）。 |
| ⑦ | **Steamworks 未統合** | 🟢 低 | 審査必須ではない。 | 実績/クラウド/LB は発売価値を上げるので M4 で対応。 |

> ✅ **クリア済みで安心な点**: アート/音楽/SFX＝CC0、解像度・全画面・言語・音量・品質の設定、終了ボタン、セーブの原子的書き込み、JA/EN 二言語、Windows x64 ビルド実績（[RELEASE_PLAN.md]）。

---

## 6. 運用ゲート（コミット/ビルド時の最低ルール）

1. **新素材を足したら** §4-A を即チェック（出所・ライセンスを `docs` か素材近傍にメモ）。
2. **機能を足す/消すたび** §4-C「ストア整合」を意識（ストア文面に未実装を残さない）。
3. **リリース候補ビルド前**に §4 全項目 + §5 の🔴🟡を解消。残す場合は理由を [ROADMAP.md] に記録。
4. ビルドは **ユーザーが明示指示した時のみ**（既存ルール踏襲）。
5. 大きな変更は [CLAUDE_HANDOFF.md] にサマリ、進捗は [ROADMAP.md] に1行追記。

---

## 7. 出典

- Steamworks「Review Process」: https://partner.steamgames.com/doc/store/review_process
- Steamworks「Content Survey / 年齢区分（独・尼）」: https://partner.steamgames.com/doc/gettingstarted/contentsurvey
- OpenDuelyst リポジトリ（CC0-1.0, LICENSE/COPYING）: https://github.com/open-duelyst/duelyst
- Duelyst オープンソース化（CC0・公道PD相当の解説）: https://gamefromscratch.com/duelyst-open-sourced/ , https://gameranx.com/updates/id/427020/article/duelyst-is-now-open-source-and-for-all-intents-and-purposes-public-domain/
