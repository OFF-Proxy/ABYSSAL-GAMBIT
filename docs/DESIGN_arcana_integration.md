# DESIGN_arcana_integration: アルカナのスプライト整列 & Unity 統合
> **状態: ✅ 実装済み（2026-05-30, アルカナ7シート整列＋Unity統合）**

> 設計/作業: Claude / 2026-05-30 / 関連: [DESIGN_boss_arcana.md](DESIGN_boss_arcana.md)
> 対象: ツール生成のモーション別シート7枚を、モーション遷移でズレない形に整え、Unity に組み込む。

## 1. 問題（モーション遷移でのズレ・崩れ）の原因

ツール生成のため、`Assets/Images/Units/Sprite/T5/Arcana/` の7枚は**シートごとに規格がバラバラ**だった:

| シート | 元サイズ | セル(W×H) | キャラ中心X(フラク) |
|---|---|---|---|
| Ability | 2148×2988 | 358×498 | 0.478 |
| Attack | 2976×3048 | 496×508 | **0.443** |
| Dead | 2652×3228 | 442×538 | 0.507 |
| DefaultAndMove | 1800×3144 | 300×524 | 0.497 |
| Enhance | 3444×3552 | 574×592 | 0.498 |
| SpecialMove_Activate | 3840×3840 | 640×640 | 0.483 |
| SpecialMove_Charge | 3840×3024 | 640×504 | **0.458** |

いずれも **6列×6行＝36コマ・左上→右→次段折り返し**（行優先）で共通。だが、
1. **セル寸法が違う** → そのままスライスすると PPU 一定で**世界スケールが最大1.3倍ばらつく**。
2. **キャラの水平中心がシートごとに違う**（0.44〜0.51）→ セル中心ピボットだと**遷移で横にガクッと動く**。
3. 縦中心はほぼ0.49〜0.50で一致（Deadのみ崩壊で意図的に下降）。

## 2. 対策（整列の方法）

`outputs/arcana_aligned/`（プロジェクト内レビュー用: `Assets/Images/Units/Sprite/T5/Arcana/_aligned_review/`）に再生成。各シートを:
- **スケール統一**: 各セル高を共通値(312px)へ正規化（キャラはセル高をほぼ満たすため、これで全モーションのキャラ実寸が揃う）。
- **アンカー統一**: 各シートの「フルボディ姿勢フレーム群（bbox高 ≥ 最大の0.8）」のキャラ中心の中央値を**そのシート定数オフセット**として算出し、共通キャンバス中心へ合わせる。**シート内のコマ差（＝攻撃の踏み込み等の動き）は保持**される。
- **均一セルへ再パック**: 全シート共通 **340×340 セル・6×6 → 2040×2040**（≤2048 で Unity 再縮小なし）。透明背景維持。

結果（検証済み）: 7モーションの代表コマを重ねると本体・頭・ドレスが中心十字に重なる＝**どのモーションへ遷移しても本体がズレない**。

> 注意: SpecialMove の広い AOE/オーラは、整列の都合で 340 セルの外側がわずかに切れる場合がある。
> 演出を完全に残したい場合は、原案どおり**エフェクトを本体と別レイヤー（VFX）に分離**するのが綺麗（[DESIGN_boss_arcana.md](DESIGN_boss_arcana.md) §4）。

## 3. スライス仕様（Unity 取り込み）

整列後シートは全て同一規格なので、スライスは全シート同じ設定でよい:
- **Sprite Mode: Multiple / Grid by Cell Count: Columns 6 × Rows 6**（または Cell Size 340×340）。
- **Pivot: Center (0.5, 0.5)**。全シート共通。これで遷移時に本体が動かない。
- **Pixels Per Unit: 100**（既存ユニットと同じ）。最終的な見かけの大きさは**プレハブの localScale で調整**（ボスとして通常ユニットの ~1.3〜1.5倍を目安）。
- **Max Texture Size: 2048**（2040 なので原寸維持）、`alphaIsTransparency: 1`、`filterMode: Bilinear`。
- フレーム順は**左上→右→次段**（行優先）= Animator クリップのフレーム順そのまま。

## 4. シート → ゲーム内アニメの対応

| 整列シート | コマ | 用途（Animator ステート） |
|---|---|---|
| DefaultAndMove | 36 | **Default(Idle) と Move を共有**（アルカナは歩かず常時浮遊のため、同一の浮遊ループでよい）。loop |
| Attack | 36 | **RangeAttack**（主攻撃＝遠距離）。once |
| Ability | 36 | **Skill「終焉の書」本体**。once（または SkillCharge→Skill 連結） |
| Enhance | 36 | **SkillCharge**（終焉の書の溜め）。Ability の前段に連結 |
| Dead | 36 | **Death**。once、最終コマ保持（透明） |
| SpecialMove_Charge | 36 | **SpecialAura/必殺の溜め**（ボス演出）。loop or once |
| SpecialMove_Activate | 36 | **SpecialAura/必殺の発動**。once |

> 36コマは多いので、テンポに応じて Animator のサンプルFPSで調整（例 Idle 8〜12fps）。
> 元仕様(Idle12/Move10…)と枚数は違うが、ツール出力の36コマ連番をそのまま流せる。

## 5. 残りの Unity 統合（要 Unity エディタ / unity-synaptic）

1. 整列シートを正式採用（`_aligned_review/` の7枚で元7枚を**置き換え**）。
2. 上記スライス設定で6×6スライス。
3. `Assets/Animations/Arcana/` に各クリップ＋ `Arcana.controller`（Default/Move/Attack/Ability/Dead ＋ 追加 SkillCharge/SpecialAura）。状態マッピングは §4。
4. `Assets/Prefabs/Unit/T5/Arcana.prefab`（既存ボス prefab を雛形に、localScale でサイズ調整、浮遊なので接地オフセット確認）。
5. `Assets/Resources/Entity Database.asset` に **Arcana（cost5 / Apex(9)・Abyss(13)・Arcanist(3) / range5）** 登録、icon は `Assets/Images/Units/Icon/T5/Arcana.png`。
6. 固有スキル「終焉の書」を `BaseEntity.TryExecuteDedicatedSkill` に追加（[DESIGN_boss_arcana.md](DESIGN_boss_arcana.md) §5、`references/add-unit-skill.md` の手順）。

## 6. 受け入れ基準
- [ ] Idle→Attack→Skill→Idle と遷移しても**本体の位置・大きさが飛ばない**
- [ ] 7モーション全てが 6×6=36コマで正しい順に再生
- [ ] Arcana がショップ/ボスとして出せる（cost5・3シナジー）
- [ ] 「終焉の書」が発動
- [ ] `Compilation completed (Errors: False)`

## 7. メモ
- 整列スクリプトのパラメータ（CANVAS=340 / TARGET_H=312 / アンカー=フルボディ中央値）は再実行可能。AOEを切らさず広く取りたい場合は CANVAS を上げる（ただし 6×CANVAS ≤ 2048 を維持、超えるなら maxTextureSize=4096 か別シート化）。
- 元の未整列7枚は `Arcana/` 直下に温存。置換は §5-1 で明示的に行う。
