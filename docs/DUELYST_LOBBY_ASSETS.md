# ロビー用 duelyst 素材マニフェスト（確定リスト）

> 目的: `reference/duelyst` 全体（2.3GB / 12,909ファイル）をクロールせずに**ロビー/タイトル画面を実装するために必要な素材だけ**を確定。
> 作成: Claude (Cowork) 2026-05-31 / 関連: [DESIGN_lobby.md](DESIGN_lobby.md), `Assets/Scripts/LobbyUI.cs`
> **規約**: `reference/` は直接参照しない。下記を必ず `Assets/` にコピーしてから importer 設定して使う（COLLAB_PROTOCOL §5-1）。

## 結論（Duelystメニューの構成）
**シーン背景画像 ＋ 中央の回転エンブレム（symbol_main_menu_* の多層）＋ 左寄せのテキストメニュー（button_primary 等）**。
`LobbyUI.cs` は現状プログラム生成のグレーUIで**アート未配線**。残作業は「下記を数点コピー → importer 設定 → LobbyUI に結線」のみ。

---

## 1. 背景（いずれか1枚を選択）
パス: `reference/duelyst/app/original_resources/scenes/`

| ファイル | 雰囲気 | 推奨 |
|---|---|---|
| `frostfire/background.jpg` | 雪原・寒色 | |
| `obsidian_woods/obsidian_woods_background.jpg` | 暗い森・紫黒 | ★ボスラッシュの重さに合う |
| `magaari_ember_highlands/magaari_ember_highlands_background.jpg` | 溶岩・赤 | アルカナの赤系と親和 |
| `load/scene_load_background.jpg` | ロード画面用・汎用 | 無難 |

→ コピー先: `Assets/Resources/UI/Lobby/lobby_bg.png`（jpg→png 変換 or jpg のまま取込可）。Importer: Sprite(2D and UI), PPU=100, 全画面なので Full Rect。

## 2. 中央の回転エンブレム（多層で重ねて回す）
パス: `reference/duelyst/app/original_resources/ui/`（`@2x` が高解像度版。Unity には @2x を推奨）

| ファイル | 役割 |
|---|---|
| `symbol_main_menu_center@2x.png` | 中央の核（静止） |
| `symbol_main_menu_ring_outer@2x.png` | 外リング（ゆっくり回転） |
| `symbol_main_menu_ring_inner@2x.png` | 内リング（逆回転） |
| `symbol_main_menu_diamond@2x.png` | 装飾ダイヤ |
| `symbol_main_menu_icon@2x.png` | 中央アイコン |
| `symbol_main_menu_triangle_small@2x.png` | 小三角の装飾 |

→ コピー先: `Assets/Resources/UI/Lobby/emblem/`。各 Image を中央に重ね、ring_outer/inner を `RectTransform` 回転 or DOTween で常時回転。

## 3. ボタン（左寄せメニュー用）
パス: `reference/duelyst/app/original_resources/ui/`

| ファイル | 用途 |
|---|---|
| `button_primary@2x.png` / `button_primary_glow@2x.png` | 主ボタン（章選択/PLAY）。glow=ホバー |
| `button_secondary@2x.png` / `_glow@2x.png` | 副ボタン（オプション等） |
| `button_back@2x.png` / `button_close@2x.png` (+ `_glow`) | 戻る/閉じる |

→ コピー先: `Assets/Resources/UI/Lobby/`。9-slice 設定（端の余白を Border に）。
> 注意: `button_primary.png` は既に `Assets/Resources/UI/ItemBench/` にある（別用途）。ロビー用は別フォルダに改めてコピーし、用途を混ぜない。

## 4. 枠・コンテナ（任意）
パス: `reference/duelyst/app/original_resources/ui/`
- `gold_main_menu_container@2x.png` — メニュー枠（金縁）
- `frame_modal@2x.png` — モーダル枠（オプション/章選択パネル）
- `bottom_bar_background@2x.png` — 下部バー
- `card_background@2x.png` — 既に Augment で流用中（参考）

## 5. フォント（未取込）
パス: `reference/duelyst/app/resources/fonts/`
- タイトル/見出し: `averta-bold-webfont.ttf` / `averta-black-webfont.ttf`
- 本文: `averta-regular-webfont.ttf` / `averta-semibold-webfont.ttf`
- 予備: `Lato-Bold.ttf` / `Lato-Regular.ttf`

→ コピー先: `Assets/Fonts/`。日本語は別途（Averta は英字のみ）。JA は既存の日本語フォント（`LocalizationManager.ApplyFont`）を流用し、Averta は EN タイトル装飾用に限定するのが安全。

---

## 実装チェックリスト（実装セッション向け）
- [ ] §1 から背景1枚を選び `Assets/Resources/UI/Lobby/lobby_bg` にコピー＋importer
- [ ] §2 の symbol_main_menu_* 6枚を `…/Lobby/emblem/` にコピー＋importer
- [ ] §3 のボタン（最低 primary + glow）をコピー＋9-slice
- [ ] §5 の Averta-Bold をコピー（EN タイトル用）
- [ ] `LobbyUI.cs`: 背景 Image / 中央エンブレム（多層＋回転）/ 左寄せボタン列にスプライトを結線
- [ ] `GameManager.showLobbyOnBoot = true` で起動 → 表示→章選択→開始の往復を確認
- [ ] Compilation OK（Errors 0）

## やらないこと（スコープ外＝時間を溶かさない）
- `reference/duelyst` の app/sdk・server・test・vendor 等は**ロビーに無関係**。見ない。
- カード/盤面/エフェクト系素材はロビーに不要。
