# boss-skill-progression: 解放ボスの育成Lvでスキルが強化・複雑化

> 設計: Claude / 実装: Cowork(Unity MCP) / 2026-06-17
> 関連: docs/DESIGN_skill-fill.md（固有スキル基盤）, SaveManager.GetBossAffinityLevel, BuffTile系, GridManager

## ゴール
章を再クリアして**解放ボスの育成レベル（アフィニティ）**が上がるほど、そのボスを味方として使ったとき
**固有スキルが強力＝段階的に複雑**になる。最大強化では「強化マスを自分で生成」「敵の位置を大きく動かす」等の
**盤面に影響を与える派手な効果**を解禁する。敵として出る章ボスは基礎版のまま（育成は味方インスタンス限定）。

## 既存の土台
- 育成Lv: `SaveManager.Instance.GetBossAffinityLevel(unitId)`（=章再クリアの累計取得回数。Lv1〜実質11）。
- 味方/敵判定: `BaseEntity.Team`（`Team.Player` のみ育成を反映）。
- 盤面セル: `BaseEntity.CurrentNode` / `GridManager.Instance`（`GetNodesCloseTo`/`GetPath`/`SetOccupied`）。
- 強化マス: `GameManager.buffTiles`（{Node,Type}）＋ `RebuildBuffTileMarkers` / `ApplyBuffTileBonuses` / `BuffTileType`。

## データ/ヘルパ（新規）
```csharp
// BaseEntity: スキルの育成レベル。敵・非解放ボスは 1。
private int SkillAffinityLevel()
{
    if (myTeam != Team.Player || SaveManager.Instance == null) return 1;
    return Mathf.Clamp(SaveManager.Instance.GetBossAffinityLevel(UnitId), 1, 11);
}
// 連続スケール（ダメージ/シールド/効果量に乗算）。Lv1=1.0 … Lv11≈1.5。
private float SkillAffinityPower() => 1f + 0.05f * (SkillAffinityLevel() - 1);
// 段階解禁のしきい値。
private bool SkillTierEnhanced() => SkillAffinityLevel() >= 4;   // 中強化（+1ヒット/範囲拡大/デバフ追加 等）
private bool SkillTierComplex()  => SkillAffinityLevel() >= 7;   // 複雑効果（強化マス生成/敵移動 等）
private bool SkillTierMax()      => SkillAffinityLevel() >= 11;  // 最大（複雑効果の上位版）
```

## 新規プリミティブ（汎用・他でも再利用可）
1. **戦闘中の強化マス生成** `GameManager.SpawnSkillBuffTile(Vector3 worldPos, BuffTileType type, float battleSeconds)`
   - 最寄り Node に一時強化マスを追加→ `RebuildBuffTileMarkers` →その上/周囲の味方へ即時 `ApplyBuffTileBonuses` 相当を適用。
   - `battleSeconds` 後 or ラウンド終了で除去（恒久ではなくその戦闘限り）。
2. **敵の位置移動** `GameManager.DisplaceEnemies(Vector3 center, float cells, bool pull)`
   - `pull=true`：中心へ引き寄せ（門/重力）。`false`：中心から押し出し（ノックバック）。
   - 各敵 `BaseEntity` を方向先の**最寄りの空き Node** へ移送（`currentNode.SetOccupied(false)`→新Node占有→`transform.position`更新）。
   - 占有衝突・盤外は `GridManager` で安全に解決。移動は1回（毎フレーム追従しない）。
   - `BaseEntity.ForceRelocateTo(Node)` を新設して使う（移動状態/索敵をリセット）。

## 段階仕様（スキルごとに分岐を追加）
各 `Execute<Boss>...` で `SkillAffinityPower()` を数値に乗算し、`SkillTierEnhanced/Complex/Max` で追加効果を解禁。
- **基礎(Lv1-3)**: 現行スキルそのまま（×Power）。
- **中強化(Lv4-6)**: +1ヒット / 範囲+ / デバフ追加 など、既存プリミティブの増量。
- **複雑(Lv7-10)**: 盤面影響の目玉効果（boss別、下表）。
- **最大(Lv11)**: 複雑効果の上位（範囲↑・数値↑・追加移動量↑）。

### boss別 複雑効果（抜粋・まず早期章ボスから実装）
| boss | 基礎 | 複雑(Lv7+) | 最大(Lv11) |
|---|---|---|---|
| caliber 終幕の薙ぎ | 周囲薙ぎ＋鈍化＋自己シールド | 着地点に**防御の強化マス生成**＋命中敵をノックバック | ノックバック距離↑＋強化マスを攻撃にも |
| neutral_rook 停滞の城門 | 自己特大シールド＋停滞 | 敵全体を自分へ**引き寄せ**て密集スロウ | 引き寄せ＋短スタン(根固め) |
| neutral_sister 鎮魂の哀歌 | 敵全体AoE＋防御減 | 最寄りの味方足元に**秘力の強化マス生成** | 強化マス効果↑＋敵を押し出し |
| Magmarvaath 溶岩噴出 | 対象地点AoE | 噴出点に**攻撃の強化マス生成** | 敵を噴出点へ引き寄せて焼く |
| …（他ボスは別バッチ） | | | |

## 実装順（バッチ）
1. 基盤：`SkillAffinityLevel/Power/Tier*`、`SpawnSkillBuffTile`、`DisplaceEnemies`＋`ForceRelocateTo`。
2. 早期章ボス3体（caliber/rook/sister）に段階分岐を実装し、Lv1/7/11で挙動が変わるのを実機確認。
3. 残りの章ボスへ横展開。

## 受け入れ基準
- [ ] 味方の解放ボスは育成Lvでスキル数値が上がる（敵章ボスは基礎のまま）。
- [ ] Lv7+で強化マス生成 or 敵移動が発生し、盤面に見える影響が出る。
- [ ] 強化マスはその戦闘限りで除去される／敵移動は1回で破綻しない（占有・盤外OK）。
- [ ] JA/EN 説明が育成Lvの効果（解禁段階）を反映。Compilation 0 / realCS=0。

## 未決（ユーザー確認したい）
- しきい値（中強化Lv4 / 複雑Lv7 / 最大Lv11）でよいか。
- まず早期3体で挙動を見てから全章ボスへ、で進めてよいか（推奨）。
- 強化マスは「その戦闘限りの一時マス」で良いか（恒久だと盤面が埋まるため）。
