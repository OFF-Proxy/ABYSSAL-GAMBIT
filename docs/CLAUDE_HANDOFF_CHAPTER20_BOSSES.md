# Claude handoff: 20章化・章ボス再設定・チャプター画像反映

作成: Codex / 2026-06-14

## 目的
ユーザー方針で、チャプターを 13章から 20章へ拡張する。
Codex はシナリオ・チャプター画像担当。Claude は Unity 実装担当。

Claude 側は以下を前提に実装へ入ってください。

---

## 1. チャプター画像の変更済み事項

`Assets/Resources/Play/Chapter/` のチャプター画像を 20章ぶんに整理済み。

- 通常版: `gate_000.jpg` 〜 `gate_019.jpg`
- 2x版: `gate_000@2x.jpg` 〜 `gate_019@2x.jpg`
- すべて通常版は `316x580`
- すべて `@2x` は `632x1160`
- `gate_000` / `gate_000@2x` はユーザー指定で第1章ボス画像として固定
- 第20章は `gate_019` / `gate_019@2x`。アルカナ戦を意識した黒い終焉ゲート画像

`LobbyUI.BuildChapterCards()` は `Resources.LoadAll<Sprite>("Play/Chapter")` を名前順ソートしているため、
`gate_000 = 第1章`、`gate_019 = 第20章` として並ぶ想定。

注意:
- 以前の欠番 `gate_007/011/012/013` は埋めた。
- 以前追加されていた `gate_020`〜`gate_023` はリネーム済みで、現在は使わない。

---

## 2. 新しい章ボス一覧

ユーザー指定の章ボスは下記。

| 章 | ボスID（ユーザー指定） | 実装メモ |
|---|---|---|
| 1 | `calibero` | 既存実装上の登録IDは `Caliber`。ユーザーの `calibero` はキャリバー・O 指定と解釈。既存ID `Caliber` を使うのが安全。 |
| 2 | `rook` | 素材は `Assets/Resources/AddUnit/neutral/neutral_rook.*` にあり。Entity DB / prefab 登録が必要か確認。 |
| 3 | `neutral_sister` | 素材は `Assets/Resources/AddUnit/neutral/neutral_sister.*` にあり。Entity DB / prefab 登録が必要か確認。 |
| 4 | `Magmarvaath` | 既存章ボス。 |
| 5 | `Magmarstarhorn` | 既存章ボス。 |
| 6 | `Magmarragnora` | 既存章ボス。 |
| 7 | `Abyssallilithe` | 既存章ボス。 |
| 8 | `Abyssalcassyva` | 既存章ボス。カガチ犬化イベント対象。 |
| 9 | `Abyssalmaehv` | 既存章ボス。 |
| 10 | `Vetruvianzirix` | 既存章ボス。 |
| 11 | `Vetruviansajj` | 既存章ボス。 |
| 12 | `Vetruvianscion` | 既存章ボス。 |
| 13 | `mechaz0rwing` | 素材は `Assets/Resources/AddUnit/neutral/neutral_mechaz0rwing.*`。実装IDは `neutral_mechaz0rwing` に寄せるか要判断。 |
| 14 | `mechaz0rsword` | 素材は `Assets/Resources/AddUnit/neutral/neutral_mechaz0rsword.*`。 |
| 15 | `mechaz0rsuper` | 素材は `Assets/Resources/AddUnit/neutral/neutral_mechaz0rsuper.*`。 |
| 16 | `mechaz0rhelm` | 素材は `Assets/Resources/AddUnit/neutral/neutral_mechaz0rhelm.*`。 |
| 17 | `mechaz0rchassis` | 素材は `Assets/Resources/AddUnit/neutral/neutral_mechaz0rchassis.*`。 |
| 18 | `mechaz0rcannon` | 素材は `Assets/Resources/AddUnit/neutral/neutral_mechaz0rcannon.*`。 |
| 19 | `neutral_hydrax` | 素材は `Assets/Resources/AddUnit/neutral/neutral_hydrax.*`。 |
| 20 | `Arcana` | 既存登録済み。最終章。 |

重要:
- `GameManager.ChapterBossRewardUnitIds` は 20章ぶんに更新する。
- `GetAllChapterBossRewardUnitIds()` は連番 `1..n` を前提に while で読むため、1〜20を欠番なしにする。
- `LobbyUI.PlayableChapterCount` は `13` から `20` に更新する。
- `BuildChapterRounds(int chapter)` に 14〜20 の分岐を追加する。
- 13章は今まで Arcana だったが、今回 Arcana は 20章へ移動。

---

## 3. 実装で触る主な場所

### `Assets/Scripts/LobbyUI.cs`
- `private const int PlayableChapterCount = 13;` を `20` に変更。
- 冒頭コメントの「13章/Arcana」説明も更新。

### `Assets/Scripts/GameManager.cs`
- `ChapterBossRewardUnitIds` を上記20章に差し替え。
- `BuildChapterRounds(int chapter)` の switch を20章対応へ。
- 既存の `BuildScaledFactionChapter(...)` を流用できる章は流用。
- 中立/Mechaz0r章は専用プールを追加するか、既存汎用ジェネレータを使う。
- 20章 Arcana は総力戦として `FinaleElites` 等を使うのが自然。

### Entity登録/Prefab生成
以下は素材はあるが、現時点で `Assets/Resources/Entity Database.asset` に登録済みとは限らない。
Claude 側で Unity MCP / Editor 経由で確認して、未登録なら prefab / animations / Entity DB を追加する。

- `neutral_rook`
- `neutral_sister`
- `neutral_hydrax`
- `neutral_mechaz0rwing`
- `neutral_mechaz0rsword`
- `neutral_mechaz0rsuper`
- `neutral_mechaz0rhelm`
- `neutral_mechaz0rchassis`
- `neutral_mechaz0rcannon`

素材の場所:
- `Assets/Resources/AddUnit/neutral/neutral_rook.png`
- `Assets/Resources/AddUnit/neutral/neutral_rook.plist`
- `Assets/Resources/AddUnit/neutral/neutral_sister.png`
- `Assets/Resources/AddUnit/neutral/neutral_sister.plist`
- `Assets/Resources/AddUnit/neutral/neutral_hydrax.png`
- `Assets/Resources/AddUnit/neutral/neutral_hydrax.plist`
- `Assets/Resources/AddUnit/neutral/neutral_mechaz0r*.png`
- `Assets/Resources/AddUnit/neutral/neutral_mechaz0r*.plist`

表示名も追加推奨:
- `LocalizationManager.UnitNameJa` に中立ボス名を追加。
- `HeroBossDialogueUI` の台本キーは小文字化される運用なので、IDの大小文字/接頭辞を統一する。

---

## 4. 推奨ID方針

ユーザー指定は `rook` / `mechaz0rwing` のように短いが、素材名は `neutral_rook` / `neutral_mechaz0rwing`。

実装の安全性を優先するなら、Entity ID は素材名に合わせて以下へ寄せるのがよい。

| 章 | 推奨実装ID |
|---|---|
| 1 | `Caliber` |
| 2 | `neutral_rook` |
| 3 | `neutral_sister` |
| 13 | `neutral_mechaz0rwing` |
| 14 | `neutral_mechaz0rsword` |
| 15 | `neutral_mechaz0rsuper` |
| 16 | `neutral_mechaz0rhelm` |
| 17 | `neutral_mechaz0rchassis` |
| 18 | `neutral_mechaz0rcannon` |
| 19 | `neutral_hydrax` |
| 20 | `Arcana` |

ただし、ユーザー指定IDをそのまま使う方針なら、素材/Prefab/Entity DB 側も同じIDへ合わせること。
途中で `rook` と `neutral_rook` が混在すると、章ボス出現・報酬解放・図鑑・会話キーがずれる。

---

## 5. シナリオ側の扱い

Codex 側で今後対応する領域:
- 20章版の章テーマ再整理
- 新ボス向け戦前会話
- Mechaz0r連章（13〜18）の物語接続
- 19章 Hydrax、20章 Arcana の終盤シナリオ
- 必要ならチャプター画像の追加/差し替え

Claude 側では、まずゲームが20章として選べて、指定ボスが出現し、章クリア報酬として解放されるところまでを優先。

---

## 6. 受け入れ確認

- ロビーに20章カードが並ぶ。
- 第1章カードは既存 `gate_000` のまま。
- 第20章カードは `gate_019` のアルカナ/終焉ゲート。
- 1章クリアで2章、19章クリアで20章が解放される。
- `GetAllChapterBossRewardUnitIds()` が20体返す。
- 各章 4-10 の章ボスが上表のボスになる。
- 章クリア時に対応ボスが `SaveManager.AddBossAlly(...)` で恒久解放される。
- 未登録IDによる null prefab / spawn failure がない。
- Unity compile errors 0。
