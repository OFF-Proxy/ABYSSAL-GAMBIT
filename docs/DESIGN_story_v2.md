# DESIGN_story_v2 — 章中心ダイアログ構造＋中ボス別キャラ化

> 作成: 2026-06-15 / 役割: 設計(Cowork)。実装はこの設計に沿ってCoworkが行い、台詞本文はCodexが章ごとに執筆。
> 背景: 実機レビューで「物語の目的が見えない／中ボスが毎回同じ台詞／全体に"それっぽいだけ"」と判明。
> 方針（ユーザー確定 2026-06-15）:
> - 物語の土台は **Codexがストーリーバイブルを先に執筆** → それを基に **1チャプターずつ丁寧に** 執筆。
> - 章開始に **導入演出/ダイアログ** を追加。
> - **ラウンド進行で敵セリフが節目で変化**（序盤/中盤/章ボス直前の3フェーズ）。
> - **中ボスは章ボスを立てる「かませ」**。章ごとに内容を変える。
> - **再登場/別章の中ボスは 色ティント＋表示名 で別キャラ化**（実装=Cowork、命名・台詞=Codex）。
> - 主人公セリフも物語性重視で全面見直し。

---

## 1. 全体像：1章＝1つの脚本パッケージ

従来は台詞が「bossId|heroId」単位でバラバラだった。これを **章(chapter)を主キー** にした脚本パッケージへ作り直す。
1章ぶんのパッケージは以下の要素で構成する。

| 要素 | いつ出る | 立ち絵様式 | 主キー | 担当 |
|------|---------|-----------|--------|------|
| ① 章導入（オープニング） | 章開始時（ラウンド1の前）に1回 | VN（InterludeUI流用） | chapter | 演出=Cowork / 台本=Codex |
| ② 節目チャッター | 序盤/中盤/章ボス直前の3フェーズ突入時 | 軽量バナー or コンパクト | chapter+phase | 仕組み=Cowork / 台本=Codex |
| ③ 中ボス「かませ」 | 各中ボス戦の戦前 | コンパクト（小アイコン） | chapter+slot(variant) | 仕組み・色=Cowork / 命名・台本=Codex |
| ④ 章ボス 戦前 | 章ボス戦の戦前 | 大立ち絵 | boss|hero | 既存。Codexが物語化して書き直し |
| ⑤ 章ボス 撃破後 | 章ボス勝利後 | 大立ち絵 | boss|hero | 既存。Codexが物語化して書き直し |

主人公の物語性は ①④⑤＋幕間に織り込む（heroId差し替えで主人公視点を出す）。

---

## 2. 中ボス別キャラ化（色ティント＋表示名）— Cowork実装の核

### 問題
中ボスは各章の `RecruitCandidateIds` プールから抽選されるため、**同じ素体ユニットが同一章内で再登場・別章でも再登場**する。
現状は `midBossLines[bossId]`（主人公共通・全章共通）なので、同じ素体＝必ず同じ台詞＝ナンセンス。

### 解決：バリアント
中ボスの **登場枠(encounter slot)** ごとに「バリアント」を割り当て、見た目と台詞を別キャラ化する。
ドラクエの スライム / スライムベス のように、素体は同じでも別個体として扱う。

```
MidBossVariant {
    string variantId;     // 例 "ch4_1"（章4の1体目）
    string baseUnitId;    // 抽選された素体（例 neutral_beastmaster）
    string displayName;   // 表示名（Codexが命名。例「灰毛の調教士ガルム」）
    Color  tint;          // スプライト色ティント（Coworkが章/枠ごとに決定的に割当）
}
```

- **色ティント**: `BaseEntity.spriteRender.color` に乗算。章ごと・枠ごとに決定的なパレットから割り当て（後述§2.1）。HP/ステータスは変えない（見た目だけ）。
- **表示名**: ダイアログの名前枠・HUDで baseUnit の名前ではなく `displayName` を使う。
- **ダイアログアイコン**: 今回は据え置き（共通アイコンのまま）。負荷を抑えるためアイコン差し替えはしない（ユーザー確定「色ティント＋名前のみ」）。
- **台詞**: `midBossLines[variantId]` で参照（章+枠ごとに別台本）。

### 2.1 ティントの割り当て規則（Cowork）
- 章ごとに基調色を1つ持たせ、枠(slot)で明度/色相を少しずらして個体差を出す。
- 決定的（章番号・slotから算出）にして、再ビルドや再起動で色が変わらないようにする。
- 既存の被ダメ白点滅・撃破演出と競合しないよう、ティントは「基準色」として保持し、点滅は一時的に上書き→復帰させる（`BaseEntity` の color 制御に注意）。

### 2.2 枠(slot)の決め方
- 章Nの中ボス戦に **出現順** で slot番号を振る（1体目=slot1, 2体目=slot2…）。同章で同素体が再登場しても slot が違えば別バリアント＝別色・別名・別台詞。
- バリアント表（chapter→slot→{baseUnitId, displayName, tint}）を `ChapterStory` に保持。displayName はCodexが命名するまで暫定（baseUnit名＋色名）。

---

## 3. 節目チャッター（ラウンド進行で敵セリフが変化）

- 章を **3フェーズ** に区切る：序盤(early) / 中盤(mid) / 章ボス直前(preboss)。
- 各フェーズ突入の最初のラウンド開始時に、短い敵セリフ（1〜2行）を出す。
- "声"の主体は章ごとに選べる：**遠くから煽る章ボスの声**（黒幕感）／前線の敵／ナレーション。中ボス=かませ、章ボス=黒幕、の演出方針に合わせる。
- 実装：ウェーブ進行で現在ラウンドのフェーズを判定し、未表示フェーズなら `ChapterChatter[chapter][phase]` を軽量バナー（既存 `WarningBannerUI` 系の簡易版 or コンパクトダイアログ）で表示。1フェーズ1回。

---

## 4. データ構造（実装方針）

新規 `Assets/Scripts/ChapterStory.cs`（static）に章中心データを集約：

```
ChapterOpening   : Dictionary<int, Beat[]>                  // ① 章導入VN（heroId差し替え対応）
ChapterChatter   : Dictionary<(int chapter,Phase), string[]>// ② 節目チャッター
MidBossVariants  : Dictionary<int, MidBossVariant[]>        // 中ボス枠（章→slot配列）
midBossLines     : Dictionary<string, string[]>             // ③ variantId → かませ台本（HeroBossDialogueUIから移設 or 連携）
```

- ④⑤（章ボス戦前/撃破後）は既存 `HeroBossDialogueUI.scriptedLines / postBossLines` を継続利用。Codexが章1からストーリー版に差し替え。
- ① は `InterludeUI`（実装済みVN枠）を流用。トリガを「章開始」に追加（`storyFlag` で1回制御、スキップ可）。
- 既存の `midBossLines[bossId]`（主人公共通・全章共通）は **段階的に variant 方式へ移行**。移行中は variant 未定義の章はboss共通へフォールバック（デグレ防止）。

---

## 5. 台本フォーマット（Codex→Cowork、章ごと）

1章ぶんを以下のタグ付きで受け取り、Coworkが各テーブルへ流し込む。

```
[opening chapter=4]
S: （ト書き）…
N: （ナレーション）…
主人公: …            ← heroIdに自動差し替え
ヴァース: …          ← 章ボス等の話者名

[chatter chapter=4 phase=early]
ヴァース(遠く): …
[chatter chapter=4 phase=mid]
ヴァース(遠く): …
[chatter chapter=4 phase=preboss]
ヴァース(遠く): …

[variant chapter=4 slot=1 base=neutral_beastmaster name=灰毛の調教士ガルム]
[midboss chapter=4 slot=1]
1(ボス): …            ← 章ボス(ヴァース)を立てる「かませ」
2(主人公): …
3(ボス): …

[variant chapter=4 slot=2 base=Silitharelder name=古鱗のドルガ]
[midboss chapter=4 slot=2]
1(ボス): …
…

[prefight boss=Magmarvaath hero=HeroAldin]   ← 章ボス戦前（物語版・各主人公）
…
[postboss boss=Magmarvaath hero=HeroAldin]   ← 撃破後（物語版）
…
```

- `[variant]` の `name` はCodexが命名、`tint` はCoworkが割り当て（Codexは色を指定しなくてよい）。
- 中ボスの主人公側返しは当面 **主人公共通**（量を抑える）。ただし「かませ」内容は章ボスを立てる方向で統一。
- 章ボス戦前/撃破後は **主人公別**（9人）。物語の核に関わるため主人公視点を効かせる。

---

## 6. 作業の進め方（1章ずつ）
1. **バイブル（Codex）** … 前提・主人公の目的・20章の筋・アルカナの正体/伏線・各章テーマと中ボスのかませ方針を1本に。`docs/Story/STORY_BIBLE.md`。
2. **章1パッケージ（Codex）** … §5フォーマットで章1の全要素を執筆。
3. **章1実装（Cowork）** … バリアント色割当＋表示名＋章導入トリガ＋節目チャッター＋台本流し込み。コンパイル0→実機確認→commit。
4. 以降、章2,3,… を同じ手順で**1章ずつ**。まとめて作らない（簡略化・陳腐化を防ぐ）。

> 実装は章1で「章導入トリガ・節目チャッター・中ボスバリアント（色+名）」の3システムを一度作れば、章2以降はデータ追加が中心になる。

---

## 7. キャラの見た目を必ず伝える（外見→ペルソナ）

Codexはキャラの見た目を知らないため、外見から外れた台詞が出る（ユーザー指摘）。**章ごとの発注プロンプトには必ず以下を入れる**：

1. その章の **章ボス＋9主人公＋中ボス素体の画像を添付**するようユーザーに促す（パスは `docs/Story/CHARACTER_VISUAL_GUIDE.md` 参照）。
2. プロンプトに明記：「**添付した立ち絵から各キャラの外見・雰囲気を把握し、そこからペルソナ（性格・口調）を推定して、それに沿った台詞を書くこと。**」
3. 文章フォールバックとして、`CHARACTER_VISUAL_GUIDE.md` の **外見メモ**（Coworkが実画像を見て記述）をプロンプトに転記する。
4. 章を発注する前に、Coworkはその章の **章ボスの立ち絵を実際に Read で確認**し、`CHARACTER_VISUAL_GUIDE.md` の「章ボス外見メモ」に追記してから発注プロンプトを作る。

> 主人公9人の外見メモは作成済み（CHARACTER_VISUAL_GUIDE §主人公）。章ボスは章ごとに追記する。
