# DESIGN_R3-boss-factions: チャプター4〜13のボス陣営計画

> 設計: Claude/Cowork（2026-06-07） / 実装: Cowork（MCP）。状態: 📋 計画確定・実装段階分け。
> 関連: [DESIGN_chapter3.md], [DESIGN_R3-hero-units.md], [DESIGN_R3-hero-depth.md]（カットインSE=陣営ボイス）

## ゴール（ユーザー方針 2026-06-07）
残り3陣営（f3/f4/f5）をボス敵陣営にし、章ごとに陣営をまとめる。

| チャプター | ボス陣営 | f番号 | 属性 | 陣営SE |
|---|---|---|---|---|
| 4〜6 | Magmar | f5 | 原始・溶岩・獣（緑）| magmar 1st/2nd |
| 7〜9 | Abyssian | f4 | 死・影（紫）| abyssian 1st/2nd |
| 10〜12 | Vetruvian | f3 | 砂漠・機巧・金 | vetruvian 1st/2nd |
| 13 | Arcana（最終ボス・既存）| — | — | （既存）|

- ヒーロー陣営は f1(Lyonar)/f2(Songhai)/f6(Vanar)＝6陣営をヒーロー3・ボス3で分担。
- 各陣営に general が3体（base/alt/third）＝「同胞/形態」。reference/duelyst に画像＋plist（f3_general.plist 等）が揃い、ヒーロー同様のパイプラインでビルド可能。

## 各章の将（general）割り当て（提案・base→alt→third）
| 章 | 章ボス(general) | 中ボス候補（同陣営の同胞） |
|---|---|---|
| 4 | f5 base | f5alt, f5third |
| 5 | f5alt | f5base, f5third |
| 6 | f5third | f5base, f5alt |
| 7 | f4 base | f4alt, f4third |
| 8 | f4alt | f4base, f4third |
| 9 | f4third | f4base, f4alt |
| 10 | f3 base | f3alt, f3third |
| 11 | f3alt | f3base, f3third |
| 12 | f3third | f3base, f3alt |
| 13 | Arcana | （最終章・専用構成）|

- これにより「章ごとに同じ種族の同胞が出る」感を出す。base/alt/third を通常→強化形態として扱う案も可。

## 実装フェーズ
- **Phase A: ボス陣営ユニット9体のビルド**（f3/f4/f5 × base/alt/third）。
  - plist を reference→作業場所へコピー → `UnitAnimationBuilder` の specs に追加（cost4-5体追加と同手順）→ スプライト/prefab/Animator/DB登録。
  - 命名（仮）: `Magmar1/2/3`, `Abyssian1/2/3`, `Vetruvian1/2/3`（または固有名）。cost=4〜5 表示。シナジー/固有スキル/アイコンを付与。
- **Phase B: チャプター4〜12 のラウンド構築**（`BuildChapterN Rounds` + `BuildChapterRounds` case 追加）。章3を雛形に難度スケール。各章の中ボス/章ボスを上表の将で構成。
- **Phase C: 13章 Arcana 仕上げ＋配線**（`PlayableChapterCount` 拡張、`ChapterBossRewardUnitIds` を新ボスへ更新、章ボス戦SE＝陣営ボイス、難易度`RoundHardLimit`等）。

## 既存との差分（要更新）
- `ChapterBossRewardUnitIds`（現 ch4=Wujin…ch12=Skyfalltyrant）を新ボス将へ置換。ch1-3・ch13(Arcana)は維持。
- `LobbyUI.PlayableChapterCount` 3→13（段階的に解放）。

## 確定事項（2026-06-07 ユーザー回答）
- 着手順：**陣営ごとに縦スライス**。まず Magmar(f5)＝3将ビルド→章4-6→動作確認、次に Abyssian(f4)→章7-9、Vetruvian(f3)→章10-12、最後に Arcana章13。
- 将割り当て：**提案どおり base→alt→third**（章4=f5base/章5=f5alt/章6=f5third…）。
- 命名/シナジー/固有スキル：**仮で進めて後調整**。

### アニメ atlas（reference/duelyst/app/resources/units、ビルダーが自動コピー）
- Magmar: `f5_general`(base) / `f5_altgeneral`(alt) / `f5_3rdgeneral`(third)
- Abyssian: `f4_general` / `f4_altgeneral` / `f4_3rdgeneral`（予定）
- Vetruvian: `f3_general` / `f3_altgeneral` / `f3_3rdgeneral`（予定）

### 仮命名（後調整可）
- Magmar: `Magmarvaath`(章4) / `Magmarstarhorn`(章5) / `Magmarragnora`(章6)
- Abyssian: 未定 / Vetruvian: 未定（着手時に命名）

## 受け入れ基準（各フェーズ）
- Phase A: 9体がDBに存在し待機/移動/攻撃/スキル/死亡が再生（図鑑詳細で確認可）。
- Phase B: 章4〜12が選択・進行でき、中ボス/章ボスに該当陣営の将が出る。
- Phase C: 13章まで通しで遊べ、章ボス戦で陣営ボイス、章クリアで該当ボス解放演出。
- 各フェーズ Compilation 0 ＋ プレイモード確認。
