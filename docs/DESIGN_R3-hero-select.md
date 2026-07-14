# DESIGN_R3-hero-select: セーブ作成時のヒーロー選択

> 設計: Claude/Cowork（2026-06-04、選択画面の一枚絵化 2026-06-05） / 実装: **Claude Code**
> 状態: 📋 設計確定・実装待ち。**前提: [DESIGN_R3-hero-units.md](DESIGN_R3-hero-units.md) のヒーロー3体が先**。
> 関連: DESIGN_R1-saveslots.md（スロットUI）, DESIGN_R3-bossfeel.md（GrantHeroUnit/必殺の現行実装）

## ゴール

新しいセーブデータ（スロット）を作るとき、**3体のヒーローから1体を選択**し、そのスロットの主人公として保存する。
以降そのスロットのランでは、選んだヒーローが毎ラン初手に配られ、必殺もそのヒーローのものになる。

## フロー

```
タイトル「PLAY」→ スロット選択
  ├─ 空きスロットを選択 ─→ 【ヒーロー選択画面】→ 選択確定 → スロット作成(heroUnitId保存) → ロビーへ
  ├─ 既存スロット（hero選択済み）→ そのままロビーへ（従来どおり）
  └─ 既存スロット（heroUnitId が空＝旧セーブ）→ 【ヒーロー選択画面】を一度だけ挟む → 保存 → ロビーへ
```

- v1 では**選択後の変更は不可**（将来: ロビーに「ヒーロー変更」を追加する余地を残す。データ構造は変更可能な形にしておく）。

## データ（永続化）

- `Assets/Scripts/Save/SaveData.cs` に `public string heroUnitId = "";` を追加（JsonUtility 互換のプレーン string。Dictionary 不可の規約に適合）。
- `SaveManager` に追加:
  - `public string GetHeroUnitId()` — 現スロットの値（空なら未選択）。
  - `public void SetHeroUnitId(string id)` — 設定して `Save()`。
- 後方互換: 既存セーブは `heroUnitId == ""` → 上記フローの「旧セーブ」分岐で一度だけ選択させる。デシリアライズは JsonUtility が未知フィールドを既定値で埋めるため移行処理は不要。

## ヒーロー選択画面（LobbyUI 内の新ビュー）

- `LobbyUI` に `heroSelectView` を追加（`SetView` の切替対象に含める）。タイトル「主人公を選択 / CHOOSE YOUR HERO」。
- 3枚のカードを横並び。**カードの顔は一枚絵イラスト**（2026-06-05 ユーザー指定。アセット準備済み）:

  | ヒーロー | イラスト（透過PNG・トリミング済み） | 元画像 |
  |---|---|---|
  | HeroAldin | `Assets/Resources/UI/HeroSelect/hero_aldin.png` (1600×1133) | reference/duelyst generals/general_f1@2x.png |
  | HeroKagachi | `Assets/Resources/UI/HeroSelect/hero_kagachi.png` (1393×1600) | 同 general_f2@2x.png |
  | HeroVesna | `Assets/Resources/UI/HeroSelect/hero_vesna.png` (1482×1600) | 同 general_f6@2x.png |

  - Resources 配下なので `Resources.Load<Sprite>("UI/HeroSelect/hero_aldin")` で取得（既存 `UI/Cards` 等と同慣例）。
  - import 設定の確認: Texture Type=Sprite (2D and UI) / Mip Maps OFF / Max Size 2048 / Compression は既存UI画像に合わせる。
  - UI Image は `preserveAspect = true` で表示（3枚で縦横比が異なるため）。カード上部〜中央にイラスト、下部にテキスト。
  - イラスト主体のため `CollectionBossAnimator` のアニメ転写は**不要**（待機アニメは図鑑詳細・性能プレビューで見られる）。盤外プレハブ生成も不要になり後始末リスクが減る。
- カードの構成（上→下）: 一枚絵 → 名前（JA/EN）・ロール一行 → **必殺の名前と効果**（選択の決め手になるため必ず表示）。
  ホバー/選択中はロール色（Aldin=金、Kagachi=赤、Vesna=蒼）で枠を強調。
- 「性能」ボタン → `UnitStatusPanelUI.ShowPreview(data, 1)`（既存）。
- 選択→確認（「このヒーローにする？はい/いいえ」程度の2択 or カード2度押し）→ `SaveManager.SetHeroUnitId(id)` → ロビーへ。
- 戻るボタン: スロット選択へ戻る（**新規スロットの場合、ヒーロー未選択のままスロットを作らない**。選択確定までスロット作成を遅延するか、作成済みなら未選択のまま戻ってもロビーには進めず再度選択を出す）。

## ラン側の接続

- `GameManager.GrantHeroUnit()`: 固定 `heroUnitId`（現行 "Wolfpunch"）をやめ、
  `SaveManager.Instance.GetHeroUnitId()` を参照。フォールバック: 空/不明ID → 既定ヒーロー（HeroAldin）→ それも無ければ従来のランダム。
- `GameManager.UseHeroUltimate()` / `HeroUltButtonUI`: 選択ヒーローIDで必殺の内容・ボタン名を分岐（DESIGN_R3-hero-units の表）。
- 図鑑・ショップ等への影響なし（ヒーローは排他制御済み）。コア戦も選択ヒーローを使用。

## 触るファイル（想定）

- `Assets/Scripts/Save/SaveData.cs` / `Save/SaveManager.cs`（heroUnitId・アクセサ）
- `Assets/Scripts/LobbyUI.cs`（heroSelectView 新設、スロット選択フローへの割り込み、SetView 追加）
- `Assets/Scripts/GameManager.cs`（GrantHeroUnit / UseHeroUltimate の選択ID対応）
- `Assets/Scripts/HeroUltButtonUI.cs`（ボタン名のヒーロー別表示）
- `Assets/Scripts/LocalizationManager.cs`（画面タイトル・確認文言）

## 受け入れ基準

- [ ] 新規スロット作成時にヒーロー選択画面が出て、3体から選べる（一枚絵イラスト＋必殺説明つき。3枚とも欠けず preserveAspect で表示）。
- [ ] 選択がスロットごとに保存され、再起動後も維持される（slot0=Aldin / slot1=Kagachi のような別選択が共存できる）。
- [ ] 既存セーブ（heroUnitId 空）はロビー入場前に一度だけ選択を求められる。
- [ ] ラン開始時に選択ヒーローが初手で配られ、必殺ボタンが対応する名前・効果になる。
- [ ] 選択画面から戻ってもデータ不整合が起きない（未選択スロットでロビーへ進めない）。
- [ ] Compilation completed (Errors: False) ＋ プレイモードで全フロー実機確認（スロット3種で確認）。

## gotcha / 注意

- スロット選択 UI は「選択即 SetActiveSlot→ロビー」フローになっている。ヒーロー選択を**間に挟む**改修なので、
  SetActiveSlot のタイミング（選択画面表示前に必要）と、キャンセル時の後始末（空スロットに中途半端なデータを残さない）に注意。
- カード画像3枚は配置済み・未インポート確認。初回 Unity フォーカス時のインポートで Sprite になっているか
  （2Dプロジェクト既定）を確認し、違えば TextureImporter を Sprite (2D and UI) に直すこと。
- `LobbyUI` の Canvas sortingOrder は 16bit short 制約に注意（既存値域 13000〜26000 を踏襲）。

## 未決

- 確認ダイアログの要否（v1 は誤選択防止に「はい/いいえ」推奨）。
- 将来の「ヒーロー変更」機能（ロビーから変更可・変更時の制約）— 本設計ではデータだけ変更可能な形にし、UIは作らない。
