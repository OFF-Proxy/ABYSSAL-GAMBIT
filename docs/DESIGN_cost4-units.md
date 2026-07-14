# cost4-units: コスト4ユニット5体の追加

> 設計＆実装: Claude (Cowork+Unity MCP) / 2026-05-31
> 状態: ✅ 実装済み（2026-05-31）。アート/DB/シナジー＋**固有スキル5種**＋**JA/EN名**＋アイコン（ユーザー用意）。残: stat/サイズの実機調整（R3-balance）。
> 依存: [DESIGN_R2-recruit.md](DESIGN_R2-recruit.md)（中ボス候補・章ボス報酬がcost4を消費） / 関連: `Assets/Editor/UnitAnimationBuilder.cs`

## ゴール

コスト4を現状5体→**10体**にし、R2-recruit の「stage3-4中ボス候補」と「章1-5ボス報酬」が同じ5体を取り合わない状態にする。既存の自動パイプライン（`UnitAnimationBuilder`）で duelyst のfaction将官スプライトから5体を新規生成する。

## 影響範囲

- **変更ファイル**:
  - `Assets/Editor/UnitAnimationBuilder.cs`：`PlistAtlasSpecs` に5体追加／`SyncEntityDatabase` を**マージ方式**へ修正／部分ビルド用の public メソッド追加。
  - `Assets/Scripts/SynergyManager.cs`：`ResolveDefaultSynergies` に5体の case 追加。
- **自動生成物**: 各ユニットの `Assets/Images/Units/Sprite/T4/<Name>/`（plist+png コピー＋スライス）、`Assets/Prefabs/Unit/T4/<Name>.prefab`、`Assets/Animations/<Name>/`（clip×5＋controller）、`Entity Database.asset` への登録。
- **既存依存API（壊さないこと）**:
  - `SyncEntityDatabase` は現状 `database.allEntities` を **specから全置換** している。spec に無い **Arcana / Zyx が消える**ため、**マージ（spec外の既存エントリを保持）に修正**する。これが本タスク最重要の安全対策。
  - `BaseEntity` のスキル系（固有skillは未付与＝汎用フォールバックで動作）。
  - `SynergyManager.GetSynergiesForEntityData` / `ResolveDefaultSynergies`（switch追加のみ）。

## 追加する5体

**【2026-05-31 改訂】** duelyst の `*_general` 系は同一ヒーローの形態/スキン違いが多く、既存の Tier2general / Embergeneral(f3_tier2general) / Plaguegeneral(f5_tier2general) 等と**見た目が被る**ため不採用。**general系を避けた固有スプライト**5体に差し替えた（初版の Frostwarden/Duskblade/Emberlord/Suncleric/Sanddjinn は削除）。

| 名前 | 素材plist | 見た目 | 射程 | シナジー | テンプレprefab |
|---|---|---|---|---|---|
| Grymbeast | boss_grym | 影の獣 | 1 | Shadow / Beast / Abyss | T4/Solfist |
| Cinderwraith | boss_unhallowed | 炎の亡霊 | 1 | Inferno / Wraith / Frenzy | T4/Solfist |
| Draugarlord | f6_draugarlord | 氷の機械獣 | 4 | Frost / Machine / Guardian | T4/Maehvmk |
| Kingsguard | f1_kingsguard | 騎士 | 1 | Divine / Royal / Guardian | T4/Solfist |
| Dissonance | boss_dissonance | 触手の術師 | 4 | Arcanist / Storm / Summoner | T4/Maehvmk |

> 追加シナジー＝Shadow / Beast / Abyss / Wraith / Frenzy / Guardian / Royal / **Summoner**（希少）。general系と違い各々シルエットが明確に異なる。

### spec エントリ（`PlistAtlasSpecs`）

数値は既存cost4（dmg320-420 / hp2100-2850）に揃えた暫定値。baseScale は boss_* 大型=1.05-1.1 / faction minion(draugarlord,kingsguard)=0.85-0.9（R3-balanceで微調整）。

```csharp
new PlistAtlasAnimationSpec("Grymbeast","boss_grym","T4",4,1,"Assets/Prefabs/Unit/T4/Solfist.prefab",390,2300,0.95f,1.1f,1.1f),
new PlistAtlasAnimationSpec("Cinderwraith","boss_unhallowed","T4",4,1,"Assets/Prefabs/Unit/T4/Solfist.prefab",380,2400,0.9f,1.0f,1.1f),
new PlistAtlasAnimationSpec("Draugarlord","f6_draugarlord","T4",4,4,"Assets/Prefabs/Unit/T4/Maehvmk.prefab",360,2300,0.95f,0.95f,0.9f),
new PlistAtlasAnimationSpec("Kingsguard","f1_kingsguard","T4",4,1,"Assets/Prefabs/Unit/T4/Solfist.prefab",330,2750,0.88f,0.95f,0.85f),
new PlistAtlasAnimationSpec("Dissonance","boss_dissonance","T4",4,4,"Assets/Prefabs/Unit/T4/Maehvmk.prefab",410,2050,1.0f,1.0f,1.05f),
```

### シナジー case（`ResolveDefaultSynergies` の switch）

```csharp
case "grymbeast":    first=SynergyType.Shadow;   second=SynergyType.Beast;   third=SynergyType.Abyss;   return;
case "cinderwraith": first=SynergyType.Inferno;  second=SynergyType.Wraith;  third=SynergyType.Frenzy;  return;
case "draugarlord":  first=SynergyType.Frost;    second=SynergyType.Machine; third=SynergyType.Guardian;return;
case "kingsguard":   first=SynergyType.Divine;   second=SynergyType.Royal;   third=SynergyType.Guardian;return;
case "dissonance":   first=SynergyType.Arcanist; second=SynergyType.Storm;   third=SynergyType.Summoner;return;
```

## SyncEntityDatabase マージ修正（必須）

現状：`database.allEntities = entries;`（spec由来のみで全置換）。
修正：spec名の集合に**含まれない既存エントリ（Arcana / Zyx など）を末尾に保持**してから代入する。

```csharp
HashSet<string> specNames = new HashSet<string>(PlistAtlasSpecs.Select(s => s.UnitName), StringComparer.OrdinalIgnoreCase);
foreach (var kv in existingEntries)
    if (!specNames.Contains(kv.Key))
        entries.Add(kv.Value);   // spec管理外（手動登録のArcana/Zyx等）を温存
database.allEntities = entries;
```

## 部分ビルド用 public メソッド（任意・推奨）

`BuildListedUnitAnimations` は全spec再ビルド（重い・既存に触れる）。新5体だけ作るため、名前指定でビルドする public を追加して run_csharp から呼ぶ：

```csharp
public static void BuildUnitsByNames(string[] names) {
    foreach (var spec in PlistAtlasSpecs)
        if (names.Contains(spec.UnitName, StringComparer.OrdinalIgnoreCase))
            BuildPlistAtlasAnimationsInternal(spec);
    SyncEntityDatabase();
    AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
}
```

## 振る舞い／スキル（2026-05-31 実装済み）

`add-unit-skill.md` に沿って5体に固有スキルを付与（数値は全て `CalculateAreaSkillDamage` 等の既存ヘルパ経由＝説明文と実値が一致）。

| ユニット | スキル | 概要 |
|---|---|---|
| Grymbeast | 影喰らいの牙 | 最遠の敵へ跳躍→対象周囲AOE（対象=満額/周囲0.6倍）＋与ダメ30%吸命。Assassin系+Malyk系の雛形。 |
| Cinderwraith | 業火の狂乱 | 自分中心AOE＋延焼(毎秒18%)＋自身の攻撃速度+25%〜。Wujin系雛形。 |
| Draugarlord | 氷塊斉射 | 遠距離AOE＋攻撃速度減速＋凍結、自身にシールド。Ilena系雛形。 |
| Kingsguard | 王命の盾 | 最も傷ついた味方を回復＋周囲味方へシールドと被ダメ軽減。Paragon/Archdeacon系雛形。 |
| Dissonance | 不協和音の連弾 | 複数の敵へ秘力雷弾（★で3/4/5発）＋近接へ0.45倍連鎖。Kane系雛形。 |

- `BaseEntity.TryExecuteDedicatedSkill` に5 case＋`Execute*` メソッド5本。汎用 `IsXxxSkillUnit` には未登録（新規IDのため除外作業不要）。
- `UnitStatusPanelUI` に実数値スキル文（JA/EN）5本＋ディスパッチ。
- 表示名JA: グリムビースト/シンダーレイス/ドラウガーロード/キングスガード/ディソナンス（`LocalizationManager.UnitNameJa`）。EN=英名そのまま。
- アイコンはユーザーが用意済み（`Assets/Images/Units/Icon/T4/<Name>`）。

## 受け入れ基準

- [ ] `Entity Database.asset` に5体が cost4 で登録され、**Arcana / Zyx を含む全52体**が維持される（全置換による消失なし）。
- [ ] 5体それぞれに sprite（Default/Move/Attack/Ability/Dead）、prefab、controller が生成される。
- [ ] ショップのコスト帯解放後、5体が（R2-recruit のロック制下でも）解放経由で出現可能になる。
- [ ] 各ユニットのシナジーが表どおり割り当たる（`GetSynergiesForEntityData`）。
- [ ] Compilation completed (Errors: False)。

## 未決事項 / 後続

- 固有スキル5種・JA/EN名・アイコン（`Assets/Images/Units/Icon/T4/<Name>.png` を置けば自動採用、無ければ atlas の Default フレームを流用）は後続。
- stat / baseScale は暫定。R3-balance で実機調整。
- R2-recruit 側の cost4 不足注記は本タスク完了で緩和（章1-5ボス報酬5体＋中ボス候補5体に分離可能）。

## Review (YYYY-MM-DD, Claude) — 未実施
