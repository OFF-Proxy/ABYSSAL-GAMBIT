# DESIGN_R4-chapter-background — 章ごとの動的バトル背景

> task-id: R4-chapter-background / 作成: 2026-06-25 / 対象: GameScene の戦闘背景＋ロビー背景
> 要望: 章ごとに背景を変える。背景＋フォアグラウンド＋パーティクルで世界観が伝わる「動的な背景」にする。
> 素材: `Assets/Resources/maps`（背景/中景/前景レイヤー）, `Assets/Resources/particles`（天候パーティクル）。
> ユーザー回答: ①ストーリー/陣営に沿って割当 ②戦闘＋ロビー両方 ③控えめ・上品。

---

## 1. 素材棚卸し（Resources/maps）

12テーマ。各テーマで利用可能なレイヤー（@2x優先・無ければ等倍を読込）:

| テーマ | background | middleground | 追加(中景) | foreground |
|---|---|---|---|---|
| battlemap0 | ○ | ○ | — | _001, _002 |
| battlemap1 | ○ | ○ | — | — |
| battlemap2 | ○ | ○ | — | _001, _002 |
| battlemap3 | ○ | ○ | — | (単一) |
| battlemap4 | ○ | ○ | — | _001, _002 |
| battlemap5 | ○(jpg) | ○ | — | _001, _002 |
| battlemap6 | — | ○ | — | — |
| battlemap7 | ○ | ○ | — | (単一) |
| abyssian | ○(jpg) | midground | midground_cracks_glow, midground_river | — |
| redrock | ○(jpg) | midground | midground_glow | foreground |
| shimzar | ○(jpg) | midground | — | foreground |
| vanar | ○(jpg) | midground | — | — |

バイオーム（サムネ確認）: 0=石/中立, 1=中立アリーナ, 2=溶岩/夕焼け, 3=青い魔法, 4=紫の毒沼, 5=森, 6=水辺, 7=星夜, abyssian=冥界, redrock=火山岩, shimzar=密林, vanar=氷雪。

## 2. 天候パーティクル（Resources/particles）

`ParticleSystem` をランタイム生成。各種は単一テクスチャ＋Legacy Particles シェーダ（Alpha/Additive）。控えめ（低Alpha・低密度・低速）。

| 種別 | texture | 動き |
|---|---|---|
| Snow | snow | ゆっくり落下＋横ゆらぎ(Noise) |
| Petals | petals_001 | 落下＋回転、淡いピンク |
| Embers | dotorb(加算/橙) | 上昇＋明滅 |
| Ash | dotorb(暗灰) | ゆっくり落下、低Alpha |
| Dust | dotorb(加算/暖) | 浮遊（Noiseで漂う） |
| BlueDust | dotorb(加算/シアン) | 浮遊 |
| Rain | rain | 高速落下（Stretch billboard） |
| Clouds | cloud_002 | 巨大・極低速で横流れ、極低Alpha |

## 3. 章→テーマ割当（ストーリー/陣営）

ch8=アビシアン固定（カガチ犬化の物語ロック）。他は世界観に沿って配置し、後半は再訪で循環。

| 章 | テーマ | 章 | テーマ |
|---|---|---|---|
| 1 | battlemap1 | 11 | vanar |
| 2 | battlemap0 | 12 | battlemap6 |
| 3 | battlemap3 | 13 | battlemap1 |
| 4 | battlemap5 | 14 | shimzar |
| 5 | shimzar | 15 | redrock |
| 6 | battlemap2 | 16 | battlemap3 |
| 7 | redrock | 17 | vanar |
| 8 | **abyssian** | 18 | battlemap2 |
| 9 | battlemap7 | 19 | abyssian |
| 10 | battlemap4 | 20 | redrock |

> 割当は `ChapterBackground` の `ChapterToTheme` 配列で一括変更可能。

## 4. レイヤー構成と描画順（盤面の視認性最優先）

すべて**盤面より後ろ**（負のsortingOrder）。盤面/ユニット（VisualSortingBaseOrder=10000系）には一切被せない。

| レイヤー | z(目安) | sortingOrder | 視差 parallax | 自律ゆらぎ |
|---|---|---|---|---|
| background | 6.0 | -200 | 0.04 | 微小 |
| 中景追加(river/cracks 等) | 4.3 | -190 | 0.07 | 微小 |
| middleground | 4.0 | -180 | 0.10 | 小 |
| 中景前glow | 3.8 | -175 | 0.11 | 小 |
| 天候パーティクル | 2.5 | -160〜-150 | — | （PS自体が動く） |
| foreground | 1.0 | -140 | 0.18 | 中 |

- サイズ: 各レイヤーのz平面でカメラ視錐台を `Camera.ViewportToWorldPoint`相当で算出し、画面を覆うよう一様スケール（×1.12マージン）。透視/正射どちらも対応。
- 視差: `LateUpdate` でカメラ移動 × parallax を反映（戦闘カメラはほぼ静止のため、主役は自律ゆらぎ）。
- 自律ゆらぎ: レイヤー毎に位相/振幅の異なる微小サイン揺れ（控えめ）。

## 5. 統合

- **GameManager**: 章初期化後（`currentChapter` 確定後）に `ChapterBackground.EnsureExists().ApplyChapter(currentChapter)`。
- 旧静的背景 `battlemap1_middleground` は**残したままSpriteRendererのみ無効化**（`EnsureCameraBoundsRenderer`/アイテムベンチ左端がそのboundsを使うため、オブジェクトとboundsは保持）。
- **ロビー**: `LobbyUI.EnsureBackground` を章テーマの合成（background＋middleground＋foreground のUI Image積層）＋控えめな視差ドリフトに。章は「次に挑む章（最高解放章）」基準。Overlay UIのため `ParticleSystem` は使わずUIのみで上品に。

## 6. 検証
- realCS=0。Playで各章テーマのレイヤー生成・パーティクル発生・**盤面とユニットが背景に隠れない**ことを確認。
- JA/EN非依存（テキストUIなし）。@2x読込失敗時は等倍へフォールバック。

## Status
- 2026-06-25 実装完了（戦闘＝ChapterBackground.cs／ロビー＝LobbyUI合成背景）。realCS=0、全20章ビルドスモーク0例外。割当/天候/数値は調整可。
