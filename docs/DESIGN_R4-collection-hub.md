# DESIGN_R4-collection-hub — コレクション＋ショップ選抜ハブ

> task-id: R4-collection-hub / 作成: 2026-06-25 / 対象: 製品版（買い切り）
> 目的: ロビーの「準備中」要素2つ（①プレイモードの「ユニット編成」カード ②出撃準備の「ショップに出すユニットを選ぶ」）を**実機能**にし、製品版として完成させる。
> 方針決定（ユーザー）: ①=**コレクション＋ショップ選抜ハブ**に統合 / ②=**解放済みからコスト別に恒久ON/OFF（コスト毎に最低数保証）**。
> 関連: [STEAM_READINESS_STANDARDS.md](STEAM_READINESS_STANDARDS.md)（リスク①の解消）/ [RELEASE_PLAN.md](RELEASE_PLAN.md) / [COLLAB_PROTOCOL.md](COLLAB_PROTOCOL.md)

---

## 1. 概要

「ユニット編成」カード → **コレクション＆ショップ選抜ハブ（`CollectionHubUI`）** を開く。
ハブでは、ショップ抽選の母集合になりうる全ユニットをコスト別(1〜5)に並べ、各ユニットを **「ショップに出す/出さない」** で恒久トグルできる。設定は**スロットのセーブに永続化**され、以降の全ランのショップ抽選に反映される。

出撃準備画面の「ショップに出すユニットを選ぶ」ボタンも、同じハブを開く（機能を一本化）。

## 2. 編成ハブに表示・管理するユニット（恒久プールのみ）

2段階で絞る:

1. `GameManager.IsShopRosterCandidate(EntityData)`（静的）= ショップに出うる通常ユニットの土台。`prefab != null`・`cost` 1〜5、除外: `IsHeroUnitId` / 名前 "Hero" 始まり / `IsReservedHeroFormUnit` / `Taskmaster` / `Zyx`。
2. `GameManager.IsHubManageableUnit(EntityData)`（静的・**ハブが使うのはこちら**）= 上記に加え「**恒久的に自分のショップ母集合になりうる**」ものだけ:
   - `cost <= 2`（常時開放のベース）／`Cost3StarterPlayable`（cost3スターターは常時）／`SaveManager.HasBossAlly`（恒久仲間化したボス）。
   - **チャプター内でだけ仲間化される一時解放ユニット（cost3-5の中ボス勧誘候補など）は除外**＝ハブに表示しない（ユーザー要望）。永続的に自分の物になったボスだけがそのコスト欄に増える。

> 進行解放ゲート・★3所持・採用中ヒーローはラン依存なので別レイヤ。抽選時は従来通り `IsEntityUnlockedForShop` が重ねて判定する。

## 3. 永続化（セーブ）

`SaveData` に1フィールド追加（`storyFlags` と同じ `List<string>` 方式。Dictionary不可の規約順守）:

- `public List<string> shopDisabledUnitIds = new List<string>();` — **無効化（ショップに出さない）ユニットID集合**。空＝全部出す（既定）。後方互換: 旧セーブは未知フィールドが既定値（空）で埋まるため移行不要。

`SaveManager` API:

- `bool IsShopUnitEnabled(string unitId)` — `!shopDisabledUnitIds.Contains(id)`（既定 true）。
- `void SetShopUnitEnabled(string unitId, bool enabled)` — 集合へ add/remove して `Save()`。
- （任意）`int ShopDisabledCount` — デバッグ/表示用。

## 4. ショップ統合（1箇所）

`GameManager.IsEntityUnlockedForShop(EntityData)` に**選抜チェックを1行追加**（reserved/legion/★3 判定の後、コスト分岐の前）:

```csharp
if (SaveManager.Instance != null && !SaveManager.Instance.IsShopUnitEnabled(entityData.name))
    return false;
```

これで `UIShop` の全候補取得（`GetEntitiesByCost`/フォールバック/`TryGetRandomAvailableEntity`）が自動的に選抜へ追従する（`UIShop` 改変不要）。

## 5. 最低数保証（枝枯れ防止）

ハブUIでトグルOFFする時に、そのコストの「有効数」が **コスト別の最低数 `MinEnabledByCost`** を下回るなら**OFFを拒否**し、注意表示を出す。

- `MinEnabledByCost`（index=cost）= **{ cost1:9, cost2:8, cost3:7, cost4:6, cost5:5 }**（調整可）。**簡単な★3量産の防止**が目的で、引き続き同じユニットを引けてしまわないよう低コストほど多めに残す。
- 実効最低数 = `min(MinEnabledByCost[cost], そのコストの表示総数)`。表示総数がこの値未満（恒久解放が少ない序盤など）なら全部維持（＝そのコストは外せない）。
- 表示総数・有効数はハブが Entity DB＋`IsHubManageableUnit` から算出。これによりショップが特定ユニットに偏らず、★3が簡単に量産できなくなる。

## 6. UI（`CollectionHubUI`・新規）

- 既存の `EnsureExists()` シングルトン規約に倣う（`Instance`＋`FindObjectOfType` フォールバック）。全画面・`SortingOrder` は他ロビーUIと衝突しない値。
- レイアウト: 上部タイトル「コレクション / ショップ選抜」、コスト1〜5の見出し＋各ユニットのカード（アイコン＋名前＋ON/OFFトグル）。スクロール可。
- トグルON=明るく枠、OFF=暗く。OFFが最低数で拒否された時はカードを軽く赤フラッシュ＋注意文。
- 「すべてON」「このコストを全ON」等のショートカットは任意（v1は個別トグル＋戻るで可）。
- 多言語 JA/EN（`LocalizationManager.IsJapanese`＋`ApplyFont`）。アイコンは既存のユニットアイコン解決を流用。
- 閉じる＝ロビー（呼び出し元のビュー）へ戻る。

## 7. 起動導線

- `LobbyUI` プレイモード行の「ユニット編成」カード: `comingSoon=false, clickable=true` にし、`CollectionHubUI.EnsureExists().Open(onClose)` を呼ぶ。
- 出撃準備の「ショップに出すユニットを選ぶ（準備中）」: ラベルから「（準備中）」を外し、同じく `CollectionHubUI` を開く（戻り先＝出撃準備）。

## 8. 検証

- コンパイル `realCS=0`。
- 手動: ハブで cost1 のユニットを最低数まで OFF → それ以上は拒否される。ON/OFF がセーブされ再起動後も保持。実戦のショップに OFF ユニットが出ない／ON だけ出る。
- デグレ: 既存セーブ（`shopDisabledUnitIds` 無し）が正常ロードされ、全ユニット既定ON。

## Status
- 2026-06-25 設計。実装着手（Cowork+Unity MCP）。
