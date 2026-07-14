# DESIGN_chapter3: チャプター3

> 設計＋実装: Claude (Cowork) / 2026-06-03
> 状態: ✅ 実装済み（静的検証=コンパイル0）。残: 実機1周・balance(R3)。
> 依存: BuildChapterRounds / RecruitMidBoss / StagedCombat・StagedBoss・StagedEvent / R2-recruit / 盤面ギミック(③b)
> 関連: [DESIGN_chapter2.md](DESIGN_chapter2.md), [DESIGN_R2-recruit.md](DESIGN_R2-recruit.md), [DESIGN_board-gimmicks.md](DESIGN_board-gimmicks.md)

## ゴール

チャプター2の続きとなる**チャプター3**を追加する。難易度はch2より一段上げ、
**盤面ギミック（転がる巨大物）がここから登場**する（ch1・2は素のユニット戦）。
章ボス報酬は `ChapterBossRewardUnitIds[3]` = **Maehvmk**（cost4、★3で出現＝撃破で恒久解放）。

## 構成（ch2 と同じ 4ステージ / 全33ラウンド）

- Stage1（1-1〜1-3）: 肩慣らし。cost1-2。ch2より数・スターをやや増。
- Stage2（2-1〜2-10）: cost2-3 主体。中ボス 2-5 / 2-10（cost3候補3体→1体解放）。イベント 2-3(silver) / 2-8(item)。
- Stage3（3-1〜3-10）: cost3-4。中ボス 3-5 / 3-10（cost4候補）。イベント 3-3(gold) / 3-7(item)。
- Stage4（4-1〜4-10）: cost4-5。中ボス 4-7 / 4-8 / 4-9（cost4候補）。**章ボス 4-10 = Maehvmk★3 ＋ cost5大護衛**。イベント 4-3(prism) / 4-6(gold)。

> 中ボス（`RecruitMidBoss`）は同コスト3体を並べ、撃破後に1体を「その章だけ」ショップ解放。
> 章ボス（`StagedBoss` 4-10）クリアで Maehvmk を恒久解放（`AddBossAlly`、`QueueStageResult` の章クリア処理）。

## ギミック

- ch3 以降、中ボスウェーブ中に「転がる巨大物」が出る（`MidBossHazardRoutine`、ch3-4=2回）。
  実装済みのゲート（`currentChapter >= 3 && IsMidBossWave`）に章3が乗るだけで自動有効。
- 「マスの変化（穴/不可侵）」は本章では未導入（後続章で追加予定）。

## 実装ポイント

- `BuildChapterRounds` の `switch` に `case 3: BuildChapter3Rounds();` を追加。
- `BuildChapter3Rounds()` を新規追加（ch2 を雛形に難易度を上げた編成）。
- `LobbyUI.PlayableChapterCount` を 2→3 にして章3を選択可能化（章2クリアで解放＝`IsChapterUnlocked` は汎用で対応済み）。
- `ChapterBossRewardUnitIds[3]`=Maehvmk は既存。`ChapterBossUnitIds` は未使用（追記不要）。

## 敵プール（既存DBから使用）

Zyx / Cost1-2 Melee・Ranged / Shadowlord / Kane / Malyk / Paragon / Ilenamk2 / Tier2general /
Wraith / Wujin / Snowchasermk / Solfist / Maehvmk / Decepticleprime / Decepticlechassis / Skindogehai /
Kron / Gol / Invader / Embergeneral / Plaguegeneral / Skyfalltyrant / cost4(Grymbeast/Cinderwraith/Draugarlord/Kingsguard/Dissonance)。

## 受け入れ基準

- [ ] ロビーで章3が選択でき（章2クリア後）、開始するとch3の編成で進行する。
- [ ] 中ボス戦で「転がる巨大物」が出る（ch1・2では出ない）。
- [ ] 章ボス(4-10)クリアで Maehvmk が恒久解放され、以降ショップに並ぶ。
- [ ] ch1・2の挙動が変わらない。
- [x] Compilation completed (Errors: False)。

## Review (2026-06-03)
- 静的検証（コンパイル0）。数値・難易度カーブ・実機1周は R3-balance で要調整。
