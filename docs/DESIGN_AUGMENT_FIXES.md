# DESIGN_AUGMENT_FIXES: オーグメント動作不良の修正

## ゴール
獲得したオーグメントの効果を**取得した瞬間から正しく機能させる**。特に、ステータス系オーグメントが既存ユニットに反映されない致命的バグを修正する。

## 影響範囲
- 変更ファイル: `Assets/Scripts/BaseEntity.cs`, `Assets/Scripts/GameManager.cs`
- 既存依存API（壊さないこと）: `BaseEntity.Setup`, `BaseEntity.SetupOnBench`, `BaseEntity.RestoreForNextWave`, `OnRosterChanged`, `IsRoundInProgress`

---

## バグ A1（最重要）: ステータス augment が既存ユニットに反映されない

### 現象
プレイヤーが `silver_atk_6` / `gold_hp_12` / `prism_emblem_warrior_3` 等の**ステータス・シナジー加算系 augment**を取得しても、その時点で `team1Entities`（盤面）・`benchEntities`（ベンチ）にいる既存ユニットには反映されない。新しく購入した／合成した／アイテム装備した／戦闘開始でリスポンしたユニットだけが新ボーナスを受け取る。

### 影響を受ける augment（22 個）
**Stat系**：`silver_atk_6` `gold_atk_12` `prism_atk_20` `silver_hp_6` `gold_hp_12` `prism_hp_25` `silver_move_6` `gold_move_12` `prism_speed_20` `silver_dr_5` `gold_dr_10` `prism_dr_20`
**Emblem系**：`silver_emblem_warrior/ranger/arcanist` `gold_emblem_*_2` `prism_emblem_*_3`（HP/atkに直接影響しないがシナジー閾値に効く）
**特殊**：`prism_dark_pact` `silver_cost1_hp` `silver_range_archer` `prism_king_blessed`（毎フレームの計算式に入るので動的に効く、refresh不要）

### 根本原因
[BaseEntity.cs:4232](../Assets/Scripts/BaseEntity.cs#L4232) の `private void ApplyCurrentStats(bool refillHealth)` がチームバフを読み込んで `baseDamage / maxHealth / movementSpeed / baseDamageReduction` 等を計算する。  
このメソッドは以下の場合のみ呼ばれる：
- `Setup()` 初期化時
- `SetupOnBench()` ベンチ配置時
- `Initialize()` で `refillHealth=true`
- 装備変更時
- スターアップ時

augment 取得時には呼ばれない。

### 修正方針

#### Step 1. `BaseEntity` に public ラッパを追加
```csharp
// BaseEntity.cs, ApplyCurrentStats の直前あたり (~ 4230 付近)

// オーグメント取得など外部要因で派生ステータスを再計算する必要が生じた時に呼びます。
// HP は現在 HP の比率を保ったまま新 MaxHealth に合わせて補正します（refillHealth=false）。
// IsSummonedUnit や IsDebugTrainingDummy にも安全に呼べます。
public void RefreshDerivedStats(bool refillHealth = false)
{
    ApplyCurrentStats(refillHealth);
}
```

#### Step 2. `GameManager.OnAugmentPicked` 末尾で全ユニットを refresh
[GameManager.cs:2011](../Assets/Scripts/GameManager.cs#L2011) `OnAugmentPicked` の末尾、`OnRosterChanged?.Invoke();` の**前**で：

```csharp
// ステータス系 augment の効果を、現在所持中のユニット全員に即時反映します。
RefreshAllOwnedUnitDerivedStats();
```

その下に新メソッド:
```csharp
// 盤面・ベンチの所有ユニットに、現在のチームバフ・シナジー設定を反映し直します。
// 召喚体（IsSummonedUnit）は augment 専用倍率で独立に管理しているため除外します。
private void RefreshAllOwnedUnitDerivedStats()
{
    for (int i = 0; i < team1Entities.Count; i++)
    {
        BaseEntity e = team1Entities[i];
        if (e == null || e.IsSummonedUnit) continue;
        e.RefreshDerivedStats(false);
    }
    for (int i = 0; i < benchEntities.Count; i++)
    {
        BaseEntity e = benchEntities[i];
        if (e == null) continue;
        e.RefreshDerivedStats(false);
    }
}
```

#### Step 3. ベンチユニットも対象であることの確認
`RefreshDerivedStats(false)` → `ApplyCurrentStats(false)` 内で `previousHealthRatio` を保ったまま MaxHealth を更新するので、HP 比率が崩れない。`isOnBoard` チェックは入れていないので、**ベンチ単位にも正しく適用される**（重要：HP augment 取得時にベンチユニットの MaxHealth が変わらないと、配置した瞬間に旧 MaxHealth でセットアップされて augment が乗らない）。

### 受け入れ基準
- [ ] `silver_atk_6` を Stage 2 augment ラウンド (2-3) で取得 → 直後にベンチユニットを盤面へ置く → ユニットの実 baseDamage が `originalBaseDamage * 1.06` で計算されている（Inspector で確認 or デバッグログ）
- [ ] `gold_hp_12` を取得直後の `team1Entities[i].MaxHealth` が `originalBaseHealth * 1.12 * 他乗算` になっている
- [ ] HP 比率が augment 取得で崩れない（70% で augment → MaxHealth 増 → 現在 HP は新 MaxHealth × 0.70 に再計算）
- [ ] `silver_emblem_warrior` を取得 → SynergyPanelUI の Warrior 行の数字が +1 される（既存挙動の延長）

---

## バグ A2: `silver_team_heal` が常に無効

### 現象
戦闘開始時、最も傷ついた味方を最大HPの10%回復する augment だが、`ApplyBattleStartAugmentEffects` 内ロジックで全員 HP 満タンの状態（`RestoreForNextWave` で `baseHealth = MaxHealth` 済み）で評価されるため、`lowestRatio < 1f` の条件が**永遠に false** → 一度も発動しない。

### 影響範囲
- [GameManager.cs:1809](../Assets/Scripts/GameManager.cs#L1809) `silver_team_heal` の if ブロック

### 修正方針
仕様を**「戦闘開始時、味方全員に最大HPの10%ぶんのシールド付与」**に変更（実装が単純、TFTの Trainer Augment 感）。
ローカライズも併せて修正:
- `AugmentCatalog.cs` の `silver_team_heal` 説明
  - JA: `"戦闘開始時、味方全員に最大HPの10%ぶんのシールドを付与します。"`
  - EN: `"At combat start, grant each ally a shield equal to 10% of their max HP."`

実装差し替え:
```csharp
// silver_team_heal: 戦闘開始時、味方全員に最大HPの10%シールド（既存 ApplyShieldFromSynergy を流用）
if (HasAugment("silver_team_heal"))
{
    for (int i = 0; i < team1Entities.Count; i++)
    {
        BaseEntity e = team1Entities[i];
        if (e == null || e.IsDead || e.IsSummonedUnit || !e.IsOnBoard) continue;
        int shield = Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * 0.10f));
        e.ApplyShieldFromSynergy(shield, 30f);
    }
}
```

`ApplyShieldFromSynergy(int, float)` は既存 API（既に Royal King などで使われている）を流用。

### 受け入れ基準
- [ ] `silver_team_heal` 所持時に戦闘開始 → 盤面の全 team1 ユニットの HPバー上に Shield 表示が出る
- [ ] シールド値は概ね MaxHealth の 10%
- [ ] チャプター内で何度 augment ラウンドを通過しても効果が累積しない（毎戦闘開始で新規付与のみ）

---

## バグ A3: 戦闘開始 augment 効果が dead/summoned ユニットにも適用される

### 現象
[GameManager.cs:1798](../Assets/Scripts/GameManager.cs#L1798) `ApplyBattleStartAugmentEffects` 内のいくつかのループが `team1Entities` を生で走査しており、`IsDead` / `IsSummonedUnit` を弾いていない。`team2Entities` の `prism_time_stop` も同様。

ゲームクラッシュにはならないが、
- 召喚体に意図しないバフが乗る
- 場合によっては表示が崩れる
- 仕様の透明性が落ちる

### 修正方針
影響するブロックすべてで以下のフィルタを適用：
```csharp
if (e == null || e.IsDead || e.IsSummonedUnit || !e.IsOnBoard) continue;
```

対象箇所 (`ApplyBattleStartAugmentEffects` 内):
1. `prism_time_stop` の team2 ループ
   - フィルタは `IsDead || !IsOnBoard` のみ（敵側は summon 概念なし）
2. `silver_first_attack` の team1 ループ
3. `silver_team_heal` の team1 ループ（A2 でも触れる）
4. `DelayedJudgementBoltCoroutine` 内の `team2Entities` 抽出（CurrentHealth > 0 だけは見ているが、IsDead がより堅牢）
5. `DelayedAttackSpeedBoostForAugment` 内の team1 ループ

### 受け入れ基準
- [ ] 召喚体が盤面にいる状態で augment 系の戦闘開始バフを取得 → 召喚体に余分なバフ表示が出ない
- [ ] 既存挙動（通常ユニット）に変化がない

---

## バグ A4: `prism_warrior_kill_buff` 関連の確認事項

### 仕様
戦士ユニットが敵を撃破するたび、次の戦闘で **そのユニットID** に +30% 与ダメ（キル数 × 30%, 上限300%）。

### 既存実装の確認ポイント
- `BaseEntity.Die()` で `lastDamageSource` が確実に立っていること（既存 `TakeDamage` フロー）
- `killer.IsSummonedUnit` の場合は除外（既に対応済み: `NotifyEnemyKilledByPlayer` 内）
- `killer.HasSynergy(SynergyType.Warrior)` チェック（既に実装済み）

### 既知の懸念点（要 Codex 確認）
- 戦士ユニットを売却した後、`warriorKillBuffPendingByUnitId` にはそのユニットID が残る → 次戦闘の `ApplyPrismWarriorKillBuffAtBattleStart` で「該当 UnitId のユニットがいないので何もしない」となるが、 dict は `Clear()` されない → **次以降に同名ユニットを再購入したら過去のキル数バフが乗る**バグの種。

### 修正方針
`ApplyPrismWarriorKillBuffAtBattleStart` の末尾の `Clear()` を for ループ後ではなく**確実に毎戦闘開始で呼ぶ**。現実装でも末尾でクリアしているが、念のため `if (!HasAugment("prism_warrior_kill_buff"))` でガードして早期 return しているケースでも `warriorKillBuffPendingByUnitId.Clear()` を行う（augment を捨てた／持ってないチャプターでも残骸が残らないように）。

```csharp
private void ApplyPrismWarriorKillBuffAtBattleStart()
{
    if (!HasAugment("prism_warrior_kill_buff"))
    {
        warriorKillBuffPendingByUnitId.Clear();
        return;
    }
    if (warriorKillBuffPendingByUnitId.Count == 0) return;

    for (int i = 0; i < team1Entities.Count; i++) { /* ... 既存ループ ... */ }

    warriorKillBuffPendingByUnitId.Clear();
}
```

### 受け入れ基準
- [ ] 戦士で複数キルを取った後、次戦闘開始時にその戦士に +30%/kill のバフが乗る（ユニットの ApplyTimedSynergyDamageDealtBonus 呼び出しを Debug.Log で確認）
- [ ] そのユニットを売却して augment を持っていない章に進んでも、dict に残骸が残らない

---

## バグ A5（軽微）: `prism_score_multiplier` の重複乗算

### 現状
[GameManager.cs:2116](../Assets/Scripts/GameManager.cs#L2116) で `ScoreMultiplier *= 1.3f;` だが、`OnAugmentPicked` は同じ augment を二度引かないので**実害なし**（`ShownAugmentIds` で除外済み）。
ただし、コードが「次回 augment 選択で同じやつが現れない」前提に依存しているのは脆いので、念のため public 状態として `ScoreMultiplier = Mathf.Min(ScoreMultiplier * 1.3f, 5f);` のようなキャップを置く（拡張時の保険）。

優先度: Low。やるなら一緒に。

---

## まとめ: 実装順

1. **A1**（最重要・体感バグ）→ `BaseEntity.RefreshDerivedStats` + `GameManager.RefreshAllOwnedUnitDerivedStats`
2. **A3**（同じファイルなので一緒に） → ApplyBattleStartAugmentEffects のフィルタ強化
3. **A2** → silver_team_heal をシールド化 + AugmentCatalog の説明文修正
4. **A4** → prism_warrior_kill_buff の dict Clear ガード
5. **A5**（任意） → ScoreMultiplier キャップ

各変更は独立しているので**1コミット1修正**で構わない。コミットメッセージ規約は [COLLAB_PROTOCOL.md §4](COLLAB_PROTOCOL.md#4-コミット規約) 参照。

---

## 実装後の検証

Compilation：
- Editor.log で `Compilation completed (Errors: False)` を確認

実機検証：
1. 章開始 → 2-3 (Silver augment) で `silver_atk_6` を選択 → 2-4 戦闘開始前に Inspector でベンチユニットの `baseDamage` が `originalBaseDamage * 1.06` で表示されていることを確認
2. 章開始 → 4-3 (Prism augment) で `silver_team_heal` を選択（※ Prism スロットだが実装後は Silver スロットでも引ける、その場合は augment選択カードを別の augment に差し替えてテスト） → 戦闘開始時に全ユニット HPバーにシールド表示
3. 4-7 中ボス戦で戦士ユニットを操作 → 撃破ログを確認 → 次戦闘の戦闘開始時に該当戦士に +30% × キル数 のバフが乗る

---

## 未決事項（Codex への質問）

なし。仕様で詰まったら `QUESTIONS.md` へ。

最終更新: 2026-05-29 (Claude)
