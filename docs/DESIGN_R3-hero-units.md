# DESIGN_R3-hero-units: ヒーロー専用ユニット3体の実装

> 設計: Claude/Cowork（2026-06-04） / 実装: **Claude Code**
> 状態: 📋 設計確定・実装待ち。
> 関連: [DESIGN_R3-hero-select.md](DESIGN_R3-hero-select.md)（ヒーロー選択）, [DESIGN_R3-bossfeel.md](DESIGN_R3-bossfeel.md)（ヒーロー必殺の現行実装）, `references/add-unit-skill.md`
> 素材: ユーザー提供の `f1_general.plist` / `f2_general.plist` / `f6_general.plist`（uploads。reference/duelyst の同名物と同一系統）

## ゴール

主人公（ヒーロー）専用のユニットを3体新規実装する。ヒーローは毎ラン初手に確定付与される“顔”であり、
**ショップ・敵ウェーブ・召喚・報酬には一切登場しない専用枠**。現行の暫定ヒーロー（Wolfpunch流用）を置き換える。

## 経緯（重要）

- f1/f2/f6_general のスプライトは過去に cost4 ユニット（Suncleric / Duskblade / Frostwarden）として一度ビルド→
  「general系は既存ユニット（Tier2general 等）と見た目が被る」ため**生成物・DBごと削除済み**（ROADMAP 2026-05-31）。
- 今回は**ヒーロー＝プレイヤーの分身**としての採用なので被り問題は該当しない。ただし**旧ID（Suncleric等）は使わない**こと。
  残骸（`Assets/Animations/...` / `Assets/Images/Units/Sprite/...` / DBエントリ）が無いことを実装前に確認する。

## 3体の仕様（名前・シナジーは仮。実装時にユーザーへ最終確認推奨）

| シート | ID（新規） | 名前（JA/EN・仮） | ロール | シナジー（仮） | 専用必殺（1戦1回・ボタン） |
|---|---|---|---|---|---|
| f1_general | `HeroAldin` | アルディン / Aldin | 聖騎士・守護型 | Divine / Guardian / Royal | **聖盾の号令**: 味方全員に最大HP30%シールド＋被ダメ-20%（6s） |
| f2_general | `HeroKagachi` | カガチ / Kagachi | 修羅・攻撃型 | Shadow / Frenzy / Warrior | **修羅の号令**: 味方全員に与ダメ+35%＋攻撃速度×1.25（6s） |
| f6_general | `HeroVesna` | ヴェスナ / Vesna | 蒼魔・秘力型 | Frost / Arcanist / Storm | **蒼雷の号令**: 敵全体に雷ダメージ（80+章×40）＋味方攻撃速度×1.15（6s） |

- 基礎ステータス: cost2〜3 相当の中堅（序盤の頼れる軸だが1体で無双しない）。例: HP 950 / 攻撃 70 / 攻速 0.85 / 近接（Aldin・Kagachi）、Vesna は射程4の遠隔・HP 700。数値は R3-balance 前提の初期値。
- 通常スキル: 各自1つ、`references/add-unit-skill.md` のレシピ通りに**専用スキル**として実装（既存と被らない名前・効果。実数値説明文も必須）。
  - Aldin: 「聖壁」自身と隣接味方にシールド＋小回復
  - Kagachi: 「残影斬」対象へ2連斬＋短時間の自己加速
  - Vesna: 「氷雷槍」直線/対象AoE＋鈍化
- スター: ヒーローは**常に★1固定**（複製が存在しないため合成不可。`ApplyStarLevel` 経路に入っても問題ないが、ショップに出ない限り発生しない）。

## ヒーロー専用枠の排他制御（最重要）

DBに登録すると既存の各抽選に乗ってしまうため、**`IsHeroUnitData(EntitiesDatabaseSO.EntityData)`（新設・ID表で判定）**を作り、
以下すべてで除外する（`IsLegionOnlySummonData` が除外されている箇所が網羅の手掛かり）:

1. ショップ抽選: `GameManager.IsEntityUnlockedForShop` → ヒーローは常に false。
2. 敵ウェーブ候補: `GetWaveEnemyCandidates`（cost一致で敵に出てしまう）→ 除外。
3. ランダム召喚: `SpawnTemporarySummonFromSynergy` / `SpawnAugmentEliteSummon` → 除外。
4. ボス報酬/仲間化候補: `GetBossRewardOptions` / `LockedCost3Pool`・`Cost4Pool` に**入れない**（ID表に追加しない）。
5. 図鑑（章ボス報酬一覧）: `ChapterBossRewardUnitIds` に**入れない**。
6. **売却禁止**: 売却処理（`TrySellEntity` 相当）でヒーローなら拒否＋警告ログ（誤売却でランの軸と必殺の主が消えるため）。
7. 開始ユニットのランダム抽選（`GrantStartingUnit`）からも除外。

## アート/ビルド・パイプライン

- `Assets/Editor/UnitAnimationBuilder.cs` の `PlistAtlasSpecs` に3体を追加（過去の cost4 5体追加と同手順）。
  - plist: reference/duelyst の `f1_general.plist` / `f2_general.plist` / `f6_general.plist`（ユーザー提供の uploads コピーと同一。reference に無ければ uploads から `reference/` 相当の作業場所へコピーして使用）。
  - 生成: スプライト（Y反転スライス）→ `Assets/Images/Units/Sprite/Hero/<Id>/` → prefab（`Assets/Prefabs/Unit/Hero/`）→ AnimatorController（Idle/Run/Attack/Ability/Death）→ Entity Database へ **merge** 登録（SyncEntityDatabase はマージ方式になっている）。
- アイコン: シートから代表フレームを切り出して `Assets/Images/Units/Icon/Hero/` に3枚（暫定で可。後でユーザー差し替え）。
- DB 登録値: cost=2（表示用。排他制御により抽選には乗らない）、synergy は上表。

## ヒーロー必殺との接続

- 現行 `GameManager.UseHeroUltimate()`（全体一律バフ）を **選択中ヒーローのIDで分岐**する形に拡張（上表の3種）。
- ボタン（`HeroUltButtonUI`）のラベルをヒーローの必殺名に差し替え（`LocalizationManager` でJA/EN）。
- どのヒーローが選ばれているかは [DESIGN_R3-hero-select.md](DESIGN_R3-hero-select.md) の `SaveData.heroUnitId` を参照。

## 触るファイル（想定）

- `Assets/Editor/UnitAnimationBuilder.cs`（specs 3件）
- `Assets/Scripts/GameManager.cs`（IsHeroUnitData・各排他・GrantHeroUnit の選択ID対応・UseHeroUltimate 分岐）
- `Assets/Scripts/BaseEntity.cs`（3体の専用スキル case）
- `Assets/Scripts/UnitStatusPanelUI.cs`（3体のスキル名・実数値説明文）
- `Assets/Scripts/SynergyManager.cs`（ResolveDefaultSynergies 3件）
- `Assets/Scripts/LocalizationManager.cs`（JA名3件＋必殺名）
- `Assets/Resources/Entity Database.asset`（ビルダー経由で追加）

## 受け入れ基準

- [ ] 3体がDBに存在し、待機/移動/攻撃/スキル/死亡アニメが再生される（図鑑詳細のモーション巡回でも確認可）。
- [ ] ショップ・敵ウェーブ・召喚・ボス報酬・仲間化候補のどこにもヒーローが出ない。
- [ ] ヒーローを売却できない。
- [ ] 選択中ヒーローがラン初手に配られ、必殺ボタンがそのヒーローの必殺（名前・効果）になる。
- [ ] 各専用スキルが発動し、詳細パネルの説明文が実数値で一致。
- [ ] Compilation completed (Errors: False) ＋ プレイモードで上記を実機確認。

## 未決（実装時にユーザー確認 or 暫定で進めて良い）

- 名前3件（仮: アルディン/カガチ/ヴェスナ）とシナジー割当。
- 基礎ステータス・必殺の数値（暫定値で実装→R3で調整）。
- ヒーローがやられた時の扱いは通常ユニットと同じ（次ウェーブで復活）で確定。
