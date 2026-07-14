# DESIGN_boss_arcana: 最終ボス「アルカナ」アート＆統合仕様
> **状態: 🟡 ゲーム実装済み／最終アートは外部発注待ち** — plist・EntityDB登録・固有スキル「終焉の書」は稼働（duelystリカラー運用）。専用スプライト発注は [ARCANA_COMMISSION_BRIEF.md](ARCANA_COMMISSION_BRIEF.md)。

> 設計: Claude / 2026-05-30 / 関連: [DESIGN_skill_overhaul.md](DESIGN_skill_overhaul.md), [DESIGN_R1-meta.md](DESIGN_R1-meta.md)
> 役割: 最終ボス兼・超遠距離キャリー（コスト5 / Apex・Abyss・Arcanist）

---

## 0. 重要: この仕様書の位置づけ（正直な前提）

**118フレームの一貫したアニメーション・スプライトシートそのものは、AI画像生成では実用品質で作れない**（フレーム間でキャラの顔/髪/ドレスがブレる、130×130グリッドへの正確なパック・モーションの振り付けが破綻する）。本プロジェクトの実アートは **duelyst スプライトを plist でスライスして使うパイプライン**で、ゼロからの生成はしていない。

そこで本書は、**絵師／専用スプライト生成ツールにそのまま渡せる作画指示書**＋**完成フレームを即ゲームに載せるためのUnity統合仕様**として書く。アルカナの「ピクセル」は外部で用意し、本書の座標規約・命名・取り込み手順に沿わせれば、Claude/Codex 側が plist 生成・Animator・Entity DB 登録・スキル実装を完了できる。

> **Claude が今すぐ実装可能な部分**（art非依存）: ①完成PNGが来たら plist/.meta を生成するスライス・スクリプト、②Entity Database へのアルカナ登録、③`終焉の書` 固有スキルの実装（§5）。
> **art として外部で用意が必要な部分**: §1–§3 の作画指示に沿った 2枚の PNG（Sheet01/Sheet02）。

---

## 1. キャラクターデザイン規約（作画バイブル）

| 項目 | 指示 |
|---|---|
| 名前 | アルカナ / Arcana |
| 身長・頭身 | 既存 Andromeda・Spelleater **と同等以上**、頭身高め。フレームは 130×130（既存ユニットは 100×100 なので 1.3倍の存在感） |
| 接地 | **常時浮遊。歩行しない。足は地面につかない。** 全フレームでフレーム下端に透明余白を残し、タイル上に浮く |
| 髪 | **長い白髪・腰下まで。全モーションで必ず残す**（崩壊中も）。揺れ・なびきで生命感を出す |
| 目 | 赤。感情変化は少なめ（無表情の威圧）。Skill時のみ発光 |
| ドレス | 黒。**赤い魔力の裂け目**が走る。裾は浮遊で常に揺れる |
| シルエット要件 | **どのフレームでも「白髪＋黒ドレス」だけで識別可能**。逆光/シルエット化しても誰か分かること（最終ボスの記号性） |
| パレット | 黒（ドレス基調）／白〜銀（髪）／赤（目・魔力裂け目・魔法陣・粒子）。差し色は赤のみに統一しノイズを減らす |
| 接地ピボット | **bottom-center (0.5, 0)** を推奨。浮遊感はフレーム下端の透明余白（目安 25〜35px）で表現し、エンジン側は既存ユニットと同じ接地規約のまま扱える |

---

## 2. シート構成（技術仕様・確定値）

- フレーム: **130×130 固定**。シート: **2048×2048**。
- 配置規約: **1アニメーション＝新しい行から開始**（行内左→右、トレーリングの空セルは透明）。これにより作画・スライス・Animator 定義が最も明快。
- 列数: 2048 ÷ 130 = **15列**（15×130=1950 ≤ 2048）。行も最大15行。
- 座標式: フレーム左上 = **( x = 130 × 列index, y = 130 × 行index )**、サイズ 130×130。列index・行index は 0 起点。

### Sheet01（2048×2048・60フレーム）
| アニメ | 論理フレーム# | 枚数 | 行 | 配置（行index→y / 列index→x=130×col） |
|---|---|---|---|---|
| Idle | 1–12 | 12 | row0 (y=0) | col0–11（x=0…1430） |
| Move | 13–22 | 10 | row1 (y=130) | col0–9（x=0…1170） |
| RangeAttack | 23–34 | 12 | row2 (y=260) | col0–11 |
| MeleeAttack | 35–44 | 10 | row3 (y=390) | col0–9 |
| SkillCharge | 45–60 | 16 | row4–5 (y=520,650) | row4 col0–14（15枚）→ row5 col0（1枚） |

### Sheet02（2048×2048・58フレーム）
| アニメ | 論理フレーム# | 枚数 | 行 | 配置 |
|---|---|---|---|---|
| Skill（終焉の書・本体） | 61–84 | 24 | row0–1 | row0 col0–14（15枚）→ row1 col0–8（9枚） |
| Death | 85–98 | 14 | row2 (y=260) | col0–13 |
| SpecialAura（ボス常時） | 99–118 | 20 | row3–4 | row3 col0–14（15枚）→ row4 col0–4（5枚） |

> 命名規約（既存 duelyst 流儀に合わせる）: `boss_arcana_<anim>_NNN.png`（例 `boss_arcana_idle_000`〜`011`, `boss_arcana_skill_000`〜`023`）。plist の `sourceSize` は `{130,130}`。この命名なら §4 のスライス・スクリプトで自動生成可能。

---

## 3. アニメーション別・作画指示（キーフレーム）

各アニメは「キーポーズ＋中割り」で。推奨FPS/ループは Animator 設定（§4）と対応。

- **Idle（12 / 4秒ループ / 3fps・loop）**: 呼吸で胸と肩が上下、髪が左右にゆっくり揺れ、数フレームに一度の瞬き、周囲に赤い魔力粒子がふわり。足は完全に浮いたまま。
- **Move（10 / ~12fps・loop）**: **浮遊移動**。足は動かさない。体がわずかに前傾し、ドレス裾と髪が進行方向と逆へ流れる。地を蹴る動きは禁止。
- **RangeAttack（12 / ~15fps・once）**: ①手を上げる ②前方に赤黒い魔法陣を展開 ③魔弾発射（赤黒い弾） ④残光。主力モーション（超遠距離キャリー）。
- **MeleeAttack（10 / ~18fps・once）**: ①瞬間移動（残像） ②赤い魔力の爪を生成 ③横薙ぎ ④元位置へ瞬間帰還。近接の緊急反撃用。髪は瞬間移動でもブレて残す。
- **SkillCharge（16 / ~12fps≈1.3秒・once）**: 前半=両腕を広げる／中盤=足元〜前方に巨大魔法陣／後半=髪が大きく舞い、目が発光開始。終焉の書（Skill）への溜め。
- **Skill「終焉の書」（24 / ~14fps≈1.7秒・once）**: 1–8 空間が歪む／9–16 巨大魔法陣が二重三重に展開／17–24 赤い瞳が見開かれ全開放。**本体だけでなく別エフェクトシート推奨**（魔法陣・ビーム・空間歪みを本体と分離レイヤーに）。
- **Death（14 / ~12fps・once→透明保持）**: 前半=崩壊／中盤=赤い粒子化／後半=魔法陣だけ残る／最終フレームは完全透明。**髪のシルエットは粒子化直前まで残す**。
- **SpecialAura（20 / ~12fps・loop / ボス専用・常時再生）**: 髪が浮き上がり、足元の魔法陣が回転、赤粒子と黒霧が立ち上る。**戦闘中ずっと加算的に重ねる想定**（§4 で別レイヤー/別オブジェクト推奨）。

---

## 4. Unity 取り込み計画

1. **スライス**: PNG2枚が `Assets/Images/Units/Sprite/T5/Arcana/` に置かれたら、§2の座標規約で plist（または .meta の sprite rects）を生成。既存 `boss_andromeda.plist` と同形式（`{{x,y},{w,h}}` / `sourceSize {130,130}`）。→ **生成スクリプトは Claude/Codex 側で用意可能**（`Assets/Editor/UnitAnimationBuilder.cs` が既存の作成補助。これに 130×130・行優先・アニメ別行のスライサーを足す）。
2. **Animator**: 既存ユニットの controller（例 `Assets/Animations/Andromeda/Andromeda.controller` = Default/Move/Attack/Ability/Dead）に倣い、`Assets/Animations/Arcana/Arcana.controller` を作成。状態マッピング:
   - `Default` ← **Idle**（loop）
   - `Move` ← **Move**（loop）
   - `Attack` ← **RangeAttack**（主攻撃。超遠距離キャリーなので通常攻撃＝遠距離）
   - （追加）`MeleeAttack` ← 敵が隣接した時のみ遷移する副攻撃ステート
   - （追加）`SkillCharge` → `Ability` ← **SkillCharge → Skill(終焉の書)** を連結（チャージ→発動）
   - `Dead` ← **Death**（once、最終フレーム保持）
   - **SpecialAura** ← ボス時のみ有効化する**加算オーバーレイ**（別 SpriteRenderer/子オブジェクト or Animatorの追加レイヤー）。プレイヤーが入手したアルカナでは無効化。
3. **接地/ピボット**: bottom-center。浮遊余白は art 側の透明で表現（§1）。既存ユニットと同じ PPU でスケールが 1.3倍に見えることを確認。
4. **エフェクト分離**: Skill/SpecialAura の魔法陣・ビーム・霧は本体スプライトと別レイヤー（別 PNG or パーティクル）。`AttackEffectPlayer` の拡張で発光・残光・空間歪みを担当（ROADMAP E6 演出強化と統合）。

---

## 5. ゲーム統合（コスト5・シナジー・スキル）

### Entity Database 登録
- `name: Arcana`, `cost: 5`, range = **5（超遠距離。既存最大が4なので新値）**, prefab/icon/frame を設定。
- シナジー: `Apex(9)`, `Abyss(13)`, `Arcanist(3)`（`synergy1/2/3`）。
- 高秘力アタッカー: 攻撃をやや低めHP・高火力・高マナ寄りに（コスト5 ranged の `ApplyBaseBalance` 基準＋ボス補正）。

### 固有スキル「終焉の書 / Tome of Finality」（§ DESIGN_skill_overhaul の流儀）
最終ボス兼キャリーの切り札。既存コスト5固有スキル（Skyfall/Invader/Gol/Legion/Kron）と同格のスケール。
- 発動: SkillCharge → Skill 演出に同期。
- 効果（初期案・R3-balance で調整）: **全敵に**巨大魔法陣からの深淵ビーム `CalculateAreaSkillDamage()` 級を `★1:2 / ★2:3 / ★3:4` 波。命中敵に **Vulnerable**（被ダメ +20%、`DESIGN_skill_overhaul` の新デバフを流用）を付与。Arcanist シナジー所持時は発動後マナ回収（既存 `GainManaFromSynergy`）。
- 実装: `TryExecuteDedicatedSkill` に `case "arcana": StartCoroutine(ExecuteArcanaTomeOfFinality(target)); return true;`。コルーチン雛形は `ExecuteSkyfallDragonRampageCoroutine` / `ExecuteInvaderThunderGodCoroutine`。
- **ボス挙動との両立**: 章ボスとして出す時はHP/火力にボス倍率。プレイヤーが撃破して仲間化（[DESIGN_R1-meta](DESIGN_R1-meta.md) の `ChapterBossUnitIds` に章Nのボスとして登録）すれば、最終ボスを連れ歩ける“ご褒美”になり、メタ進行の頂点として機能する。

---

## 6. 受け入れ基準（art 完成後）
- [ ] Sheet01/Sheet02 が 2048×2048・130×130・§2の配置で用意されている
- [ ] 全フレームで「白髪＋黒ドレス＋浮遊（足が地に着かない）」が満たされる
- [ ] plist/Animator が生成され、Idle/Move/RangeAttack/MeleeAttack/SkillCharge/Skill/Death/SpecialAura が再生する
- [ ] Entity DB に Arcana（cost5 / Apex・Abyss・Arcanist / range5）が登録され、ショップ/ボスとして出せる
- [ ] `終焉の書` が発動し、全敵への多段＋Vulnerable が機能する
- [ ] `Compilation completed (Errors: False)`

## 7. 未決事項 / 確認したいこと
- **art の調達方法**: ①絵師に発注（本書をそのまま渡せる）②専用のスプライト/アニメ生成ツール ③duelyst 既存スプライトのリカラー流用 — どれで進める?
- range=5 を新設してよいか（現状ゲーム最大4）。盤面バランスに影響。R3-balance と要相談。
- SpecialAura を「常時オーバーレイ」にするか「ボス時の代替Idle」にするか（実装簡易度はオーバーレイ別オブジェクトが楽）。

---

## 8. art 調達ルート調査（2026-05 / 専用ツール方針）

**前提**: Cowork から直接叩ける画像/スプライト生成 MCP コネクタは存在しない（registry 確認済み）。よって art は**外部ツールで作成 → 完成 PNG を `Assets/Images/Units/Sprite/T5/Arcana/` に配置 → Claude/Codex が plist・統合**という分業になる。

アルカナはピクセルアートではなく**アニメ調の高精細イラスト**で、かつ大半のモーションが「1体の一貫キャラに対する微動（呼吸・髪揺れ・浮遊・裾揺れ・魔法陣）」。この性質から、ルートは大きく2系統:

### ルートA（推奨・一貫性最優先）: 1枚絵 → スケルタル/メッシュ変形でアニメ化
- **Live2D Cubism** または **Spine（2D skeletal）**。アルカナの完成イラスト1枚をリグ化し、呼吸・髪揺れ・浮遊・裾揺れ・魔法陣回転・SpecialAura を**変形で生成**。フレーム間のキャラ崩れが原理的に起きない。
- Idle/Move/SpecialAura/SkillCharge のような「微動・ループ」と特に相性が良い。RangeAttack/Skill の派手な部分はエフェクトを別レイヤーで重ねる（本書 §4 のエフェクト分離と一致）。
- 出力: スケルタルのまま Unity ランタイム（Spine-Unity / Live2D Cubism SDK）を入れるか、**スプライトシートに書き出して既存の frame ベース pipeline に載せる**かを選べる。後者なら本書 §2 の 130×130/2048×2048 にそのまま整列可能。
- コスト: イラスト1枚＋リグ作業。最も「狙い通りの見た目」を担保しやすい。

### ルートB（手早い・要品質確認）: AI スプライトシート生成ツール
1枚の参照画像（アップ済みの2枚が使える）から各アニメを生成。2026時点の候補:
- **Scenario（Seedance 2.0）** — キャラ一貫性とモーション生成が比較的強い。design→sprite sheet が速い。
- **Ludo.ai Sprite Generator** — フレーム数/レイアウト/サイズを Unity 向けに指定して書き出し。
- **SpriteFlow / AISpriteSheet / PixelCut** — スタイル固定・フレーム補間（4→12枚など）。

→ **注意（正直な評価）**: これらは歩行/攻撃の単純サイクルでは実用的だが、**118フレーム・8アニメ・高精細アニメ調・独自振付（終焉の書/SpecialAura）を一括で破綻なく出すのは依然難しい**。現実的には「Idle/Move/基本Attack を生成 → 細部と Skill/Death はレタッチ or 別途」というハイブリッドになりやすい。出力後に §6 の受け入れ基準（白髪＋黒ドレス＋浮遊の一貫性）で必ず検品する。

### 推奨
**ルートA（1枚絵→Spine/Live2D）** が、最終ボスの“記号性の一貫性”という本作の要件に最も合う。まず**アルカナの決定稿イラスト1枚**（正面・浮遊・腰下まで白髪・黒ドレスの赤い裂け目）を用意し、それをリグ化 or 各ツールの image-to-sprite の種にするのが堅い。決定稿1枚は AI 画像生成・絵師どちらでも可。

> いずれのルートでも、完成フレームが揃えば Claude 側で **plist 自動生成スクリプト＋Animator＋Entity DB＋「終焉の書」スキル**を実装して接続できる（§4–§5）。
