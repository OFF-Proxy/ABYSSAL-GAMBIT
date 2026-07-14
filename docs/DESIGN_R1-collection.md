# R1-collection: ボス図鑑 ＆ ボス育成（アフィニティ）

> 設計＆実装: Claude (Cowork+Unity MCP) / 2026-05-31
> 状態: ✅ 実装済み（図鑑＋育成倍率）。残: 実機検証・R3-balance（倍率調整）。
> 依存: [DESIGN_R1-meta.md](DESIGN_R1-meta.md) / [DESIGN_R2-recruit.md](DESIGN_R2-recruit.md)（章ボス恒久解放＝収集対象） / R1-persist

## ゴール

メタ進行の核を強化する2点:
1. **ボス図鑑**: ロビーに「章ボス（恒久収集対象）」一覧を表示。解放済み＝アイコン＋名前＋育成Lv、未解放＝シルエット＋???。収集の可視化。
2. **ボス育成（アフィニティ）**: 同じ章ボスを再度獲得（章再クリア）するたびに育成Lvが上がり、そのユニットを編成した時の永続ステータスが強化される。買い切りでも「集めて育てる」動機を作る。

## 影響範囲

- **変更ファイル**:
  - `Save/SaveData.cs` — `BossAllyRecord.recruitCount`（育成Lvの基礎）。
  - `Save/SaveManager.cs` — `AddBossAlly` で recruitCount 加算、`GetBossAffinityLevel/GetBossAffinityStatMultiplier`。
  - `BaseEntity.cs` — `ApplyBossAffinityMultiplier(float)`（HP・攻撃力を同率で底上げ）。
  - `GameManager.cs` — `CreateBenchEntity` で所持ボスに育成倍率を適用／`GetAllChapterBossRewardUnitIds()`（図鑑用・static）。
  - `LobbyUI.cs` — タイトルに「図鑑」メニュー＋コレクションビュー（グリッド）。
- **既存依存API（壊さないこと）**: `SaveManager.AddBossAlly/HasBossAlly/BossAllies` の既存呼び出し互換（recruitCount 追加は後方互換、旧セーブは Lv1 扱い）。`IsEntityUnlockedForShop`（恒久解放＝HasBossAlly）は不変。

## データ構造 / 仕様

- `BossAllyRecord.recruitCount`（既定1）。章ボス撃破＝`AddBossAlly` ごとに +1。
- 育成Lv = recruitCount（未所持=0）。
- 育成倍率 = `Lv<=1 ? 1.0 : 1 + 0.06×min(Lv-1,10)`（Lv11で +60% 上限）。**暫定値・R3-balance**。
- 適用: `GameManager.CreateBenchEntity` で `HasBossAlly(unitId)` の時のみ `ApplyBossAffinityMultiplier` を呼ぶ。プレイヤー生成経路のみ＝敵には無影響。R2-recruit でボス仲間はショップ出現→購入もこの経路を通るので全コピーに乗る。
- 収集対象 = `ChapterBossRewardUnitIds`（ch1-5=cost4 / ch6+=cost5、現状13定義）。図鑑はこれを章順で表示。

## 振る舞い（UI）

- タイトル: **PLAY / 図鑑 / オプション / 終了**。「図鑑」→ コレクションビュー。
- コレクション: 5列グリッド。各セル＝パネル素材(result_panel)＋ユニットアイコン＋名前＋「育成 Lv N (+X%)」。未解放はアイコンを黒シルエット化＋「未解放」。
- Entity DB は `Resources.Load<EntitiesDatabaseSO>("Entity Database")` で取得（LobbyScene でも動く）。戻るでタイトル。JA/EN対応。

## 受け入れ基準

- [ ] タイトルに「図鑑」が出て、章ボス一覧が表示される（解放=情報表示／未解放=シルエット）。
- [ ] 章クリアで該当ボスの育成Lvが上がり、図鑑の Lv と +% が増える。
- [ ] 育成済みボスをショップで買って編成すると、HP/攻撃力が +% 強化されている（敵は不変）。
- [ ] 旧セーブのボス仲間は Lv1（+0%）として表示・動作（後方互換）。
- [ ] Compilation OK（CSエラー0）。

## 未決 / 後続

- 倍率カーブ・上限は暫定（R3-balance）。Lv閾値で★アップさせる案もある。
- 中ボス（章内解放）は現状 recruitCount に積まない（恒久ではないため）。将来「中ボスも図鑑/育成対象に」する場合は別途設計。
- 図鑑セルにスキル説明やロア表示を足すのは任意ポリッシュ。

## 追補（2026-05-31）: 図鑑からボス詳細

解放済みの図鑑セルを**クリック**すると `UnitStatusPanelUI.ShowPreview(entityData, 1)` を開き、**ポートレート（スプライト）＋実数値スキル＋ステータス**を表示（`LobbyUI.CreateCollectionCell` に Button＋ButtonJuice を付与）。未解放セルはクリック不可。

## Review (YYYY-MM-DD) — 未実施（実機確認後）
