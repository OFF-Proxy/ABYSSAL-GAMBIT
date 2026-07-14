# 15 Hero Awakening Design

主人公覚醒イベントの正本です。  
`reference/duelyst/app/resources/units` にある `tier2`、`altgeneraltier2`、`mk2` 系スプライトを、単なる上位スキンではなく「主人公の答えが姿になる場面」として章へ組み込みます。

## 目的

覚醒は強くなった証明ではありません。  
その主人公が、自分の弱さや過去を抱えたまま、それでも世界へ返す答えを決めた瞬間です。

覚醒で達成すること:

- プレイヤーに「このキャラを選んでよかった」と感じさせる。
- 章テーマと主人公テーマを強く接続する。
- 既存の別姿スプライトを、IPとして記憶に残る見せ場にする。
- Unity実装では、見た目変更、台詞、UIバナー、保存フラグを同時に扱えるようにする。

## 基本ルール

- 覚醒は原則1キャラ1回。
- 覚醒は改心や別人格化ではなく、本人の答えの明確化。
- 覚醒後も口調、一人称、関係性の核は変えない。
- 覚醒前の弱さは消えない。弱さの扱い方が変わる。
- 章初回で該当主人公を使わなかった場合、対象章の再挑戦または後続章で回収可能にする。
- ボス将9人は、仲間化後に「かつて自分が提示した諦めを、今度は主人公として越える」ことで覚醒する。

## 表現ルール

覚醒演出は長くしすぎない。  
戦闘前、HP50%演出、撃破後のいずれかに置き、台詞は8行から16行程度に留めます。

推奨構成:

```text
1. 章側の問いが主人公の弱さを突く。
2. 主人公が一度だけ言葉を失う。
3. 関係キャラ、観測片、または敵の台詞が答えを引き出す。
4. 主人公が自分の弱さを否定せずに受け入れる。
5. スプライトが覚醒形態へ切り替わる。
6. UIで覚醒名と解放を表示する。
7. 戦闘または撃破後台詞へ戻る。
```

禁止:

- 突然、別人のような口調になる。
- 強くなったから悩みが消えたように見せる。
- 全員を同じ演出テンプレートで処理する。
- 覚醒を章テーマと無関係な報酬にする。

## 基礎主人公9人の覚醒配置

| Hero ID | 名前 | 対象章 | 覚醒名 | 通常素材 | 覚醒素材候補 | 発火位置 | 核になる答え |
| --- | --- | --- | --- | --- | --- | --- | --- |
| HeroAldin | アルディン | Chapter14 | 白門誓装 | `f1_general` | `f1_tier2general` | メカゾ0ア・剣戦前 | 守る者は、裁く剣ではなく立ち続ける盾になる。 |
| HeroKagachi | カガチ | Chapter08 | 影火の歩 | `f2_general` | `f2_tier2general` | 犬化イベント後、撃破時 | 弱い姿ごと前へ進む。 |
| HeroVesna | ヴェスナ | Chapter17 | 蒼灯の炉 | `f6_general` | `f6_tier2general` | メカゾ0ア・器HP50% | 居場所は同じ型に削ることではなく、違う火を置ける場所。 |
| HeroZiran | ジラーン | Chapter09 | 祈手の誓い | `f1_altgeneral` | `f1_altgeneraltier2` | メーヴ撃破後 | 苦しみを消すのではなく、苦しむ命の隣に立つ。 |
| HeroReva | レヴァ | Chapter13 | 風選び | `f2_altgeneral` | `f2_altgeneraltier2` | メカゾ0ア・翼戦前 | 自由とは逃げることだけでなく、残る場所を自分で選ぶこと。 |
| HeroKara | カーラ | Chapter06 | 雪盾の待火 | `f6_altgeneral` | `f6_altgeneraltier2` | ラグノラ撃破後 | 待つことは停滞ではなく、戻る場所を守る行為。 |
| HeroBrome | ブローム | Chapter18 | 旗砲の誓い | `f1_3rdgeneral` | `f1_bromemk2` | メカゾ0ア・砲発射直前 | 世界は標的ではない。違う声がまだ同じ明日を支えている。 |
| HeroShidai | シダイ | Chapter16 | 影名の刃 | `f2_3rdgeneral` | `f2_shidaimk2` | メカゾ0ア・兜戦前 | 命令の影ではなく、名を持つ影として立つ。 |
| HeroIlena | イレーナ | Chapter15 | 未完観測 | `f6_3rdgeneral` | `f6_ilenamk2` | メカゾ0ア完全体HP50% | 観測した結末を、結論ではなく追記可能な記録にする。 |

### カガチの特別扱い

カガチはすでに Chapter08 の犬化イベントがあります。  
ここを消さず、二段階の見せ場にします。

```text
1. ボス前: `INT_08` 犬化。強さの鎧が剥がれ、格好悪い姿になる。
2. 戦闘中: 犬のまま前へ出る。弱さを隠せない状態で戦う。
3. 撃破後: 人の姿へ戻る。ただし以前の自分ではない。
4. 解放: `f2_tier2general` を覚醒形態として解放。
```

犬化スキンはギャグではなく、弱さを隠せなくなったカガチの核心イベントです。  
覚醒は犬化を上書きするのではなく、「あの姿を経たから進める」証明にします。

## ボス将9人の共闘覚醒

ボス将は倒された章で仲間化します。  
ただし覚醒は初回撃破時ではなく、仲間化後に同じ問いへ主人公として戻った時に発火します。

| Hero ID | 名前 | 対象章 | 覚醒名 | 通常素材 | 覚醒素材候補 | 発火条件 | 核になる答え |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Magmarvaath | ヴァース | Chapter04 | 灼眠不滅 | `f5_general` | `f5_tier2general` | 仲間化後、Chapter04を主人公でクリア | 本能は眠らない。だが本能だけで世界を閉じない。 |
| Magmarstarhorn | スターホーン | Chapter05 | 角笛再火 | `f5_altgeneral` | `f5_altgeneraltier2` | 仲間化後、Chapter05を主人公でクリア | 戦う熱を、戦わない明日のためにも鳴らす。 |
| Magmarragnora | ラグノラ | Chapter06 | 母巣の灯 | `f5_3rdgeneral` | `f5_ragnoramk2` | 仲間化後、Chapter06を主人公でクリア | 群れは個を消す腹ではなく、個が帰る場所。 |
| Abyssallilithe | リリス | Chapter07 | 別れの花冠 | `f4_general` | `f4_tier2general` | 仲間化後、Chapter07を主人公でクリア | 別れるからこそ、手を伸ばした事実が残る。 |
| Abyssalcassyva | キャシヴァ | Chapter08 | 生笑の影 | `f4_altgeneral` | `f4_altgeneraltier2` | 仲間化後、Chapter08を主人公でクリア | 痛い、腹が減る、格好悪い。それでも生きる。 |
| Abyssalmaehv | メーヴ | Chapter09 | 目覚めの祈り | `f4_3rdgeneral` | `f4_maehvmk2` | 仲間化後、Chapter09を主人公でクリア | 休ませたい気持ちを持ちながら、選ぶ権利を残す。 |
| Vetruvianzirix | ジリックス | Chapter10 | 砂律の余白 | `f3_general` | `f3_tier2general` | 仲間化後、Chapter10を主人公でクリア | 規格外を排除せず、未来として管理する。 |
| Vetruviansajj | サージ | Chapter11 | 砂熱の舞 | `f3_altgeneral` | `f3_altgeneraltier2` | 仲間化後、Chapter11を主人公でクリア | 諦めた後にも熱が残る。 |
| Vetruvianscion | サイオン | Chapter12 | 運命反転舞 | `f3_3rdgeneral` | `f3_ciphyronmk2` | 仲間化後、Chapter12を主人公でクリア | 観測された結末でも、足運びは選べる。 |

## スプライト素材対応メモ

確認済み候補:

```text
reference/duelyst/app/resources/units/f1_tier2general.png
reference/duelyst/app/resources/units/f1_altgeneraltier2.png
reference/duelyst/app/resources/units/f1_bromemk2.png
reference/duelyst/app/resources/units/f2_tier2general.png
reference/duelyst/app/resources/units/f2_altgeneraltier2.png
reference/duelyst/app/resources/units/f2_shidaimk2.png
reference/duelyst/app/resources/units/f3_tier2general.png
reference/duelyst/app/resources/units/f3_altgeneraltier2.png
reference/duelyst/app/resources/units/f3_ciphyronmk2.png
reference/duelyst/app/resources/units/f4_tier2general.png
reference/duelyst/app/resources/units/f4_altgeneraltier2.png
reference/duelyst/app/resources/units/f4_maehvmk2.png
reference/duelyst/app/resources/units/f5_tier2general.png
reference/duelyst/app/resources/units/f5_altgeneraltier2.png
reference/duelyst/app/resources/units/f5_ragnoramk2.png
reference/duelyst/app/resources/units/f6_tier2general.png
reference/duelyst/app/resources/units/f6_altgeneraltier2.png
reference/duelyst/app/resources/units/f6_ilenamk2.png
```

現行コードで既に予約または登録が見えるもの:

| 既存Unit ID | 素材 | 備考 |
| --- | --- | --- |
| Skindogehai | `f2_general_skindogehai` | カガチ犬化用。覚醒ではなく特殊姿。 |
| Tier2general | `f6_tier2general` | ヴェスナ系覚醒候補として流用可能。 |
| Altgeneraltier2 | `f6_altgeneraltier2` | カーラ系覚醒候補として流用可能。 |
| Ilenamk2 | `f6_ilenamk2` | イレーナ覚醒候補。 |
| Embergeneral | `f3_tier2general` | ジリックス覚醒候補。ただし表示名整理推奨。 |
| Plaguegeneral | `f5_tier2general` | ヴァース覚醒候補。ただし表示名整理推奨。 |
| Maehvmk | `f4_maehvmk2` | メーヴ覚醒候補。 |

未登録の候補は、後で `HeroIdAwakened` 形式またはフォーム差し替え方式で追加します。  
推奨は別Unit IDで増やしすぎず、主人公IDは維持してフォームだけ差し替える方式です。

## 章別配置

| 章 | 覚醒対象 | 位置 | 実装優先度 |
| --- | --- | --- | --- |
| Chapter04 | ヴァース | 仲間化後再挑戦、撃破後 | P2 |
| Chapter05 | スターホーン | 仲間化後再挑戦、撃破後 | P2 |
| Chapter06 | カーラ、ラグノラ | 撃破後 | P1 |
| Chapter07 | リリス | 仲間化後再挑戦、撃破後 | P2 |
| Chapter08 | カガチ、キャシヴァ | カガチは犬化後、キャシヴァは仲間化後再挑戦 | P0 |
| Chapter09 | ジラーン、メーヴ | 撃破後 | P1 |
| Chapter10 | ジリックス | 仲間化後再挑戦、撃破後 | P2 |
| Chapter11 | サージ | 仲間化後再挑戦、撃破後 | P2 |
| Chapter12 | サイオン | 仲間化後再挑戦、撃破後 | P2 |
| Chapter13 | レヴァ | 戦前 | P1 |
| Chapter14 | アルディン | 戦前 | P1 |
| Chapter15 | イレーナ | HP50%または撃破後 | P1 |
| Chapter16 | シダイ | 戦前 | P1 |
| Chapter17 | ヴェスナ | HP50%または撃破後 | P1 |
| Chapter18 | ブローム | 発射直前または撃破後 | P1 |

P0は最初に作るべき覚醒です。  
Chapter08のカガチは既存の犬化イベントと直結するため、覚醒システムの試作に向いています。

## 覚醒台詞キー

台詞キーは以下で統一します。

```text
AWAKEN_<chapter>_<heroId>
```

例:

```text
AWAKEN_CH08_HeroKagachi
AWAKEN_CH14_HeroAldin
AWAKEN_CH17_HeroVesna
```

## 覚醒台詞サンプル

### AWAKEN_CH08_HeroKagachi

```text
キャシヴァ: ほら、格好悪い。転んで、吠えて、泥だらけ。
カガチ: うるせえ。
キャシヴァ: それでも前に出るの？
カガチ: 出る。
カガチ: 弱いままでも足は出る。噛みつく歯がなくても、進む方は分かる。
カガチ: 俺は、弱かった俺ごと前へ連れていく。
SYSTEM: カガチが覚醒しました。影火の歩を解放。
```

### AWAKEN_CH14_HeroAldin

```text
メカゾ0ア・剣: JUDGE: 失敗者に守護権限なし。断罪を実行。
アルディン: その判定は正しい。私は守れなかった。
メカゾ0ア・剣: THEN: 停止せよ。
アルディン: いいえ。
アルディン: 守れなかった者が、次を守ってはいけない理由にはならない。
アルディン: 断つ剣ではなく、立ち続ける盾としてここにいる。
SYSTEM: アルディンが覚醒しました。白門誓装を解放。
```

### AWAKEN_CH15_HeroIlena

```text
メカゾ0ア完全体: COMPLETE: 未確定要素なし。記録を閉鎖。
イレーナ: 完成した記録は、美しいのでしょう。
メカゾ0ア完全体: AFFIRMATIVE.
イレーナ: けれど、追記できない記録は墓標です。
イレーナ: 私は観測しました。だからこそ、観測だけを結論にはしません。
SYSTEM: イレーナが覚醒しました。未完観測を解放。
```

### AWAKEN_CH18_HeroBrome

```text
メカゾ0ア・砲: TARGET: MITHRA. 殲滅により痛みを終了。
ブローム: 照準を下ろせ。
メカゾ0ア・砲: DENIED.
ブローム: 世界は標的ではない。違う声が、まだ同じ明日を支えている。
ブローム: 我らは同じ色ではない。だからこそ、同じ旗の下に立てる。
SYSTEM: ブロームが覚醒しました。旗砲の誓いを解放。
```

## UI演出

覚醒時:

- 画面中央に短い白フラッシュ。
- 主人公の立ち絵またはユニットを一瞬シルエット化。
- フォーム変更後、覚醒名を2秒表示。
- 覚醒後のスキルアイコンに小さな光を走らせる。
- リザルトで「Awakened Form Unlocked」相当のローカライズ済みバナーを表示。

表示文:

```text
覚醒
<覚醒名>
<主人公名>の新たな姿を解放しました
```

## BGMとカメラ

覚醒直前:

- BGMを完全停止せず、低域だけ残す。
- 敵の台詞の後に0.4秒から0.7秒の間を置く。
- 主人公の足元、武器、目元など、キャラごとに象徴カットを入れる。

覚醒瞬間:

- 共通SEは1種類でよい。ただし勢力ごとの追加音を重ねる。
- ライオネルは鐘、ソンガイは風切り、ヴァナーは氷音、マグマーは鼓動、アビサルは囁き、ヴェツルヴィアンは砂音。
- カメラはズームしすぎない。ユニットとしての読みやすさを保つ。

## Unity実装フラグ

推奨フラグ:

| キー | 型 | 用途 |
| --- | --- | --- |
| `hero_awakened_<heroId>` | bool | 覚醒済みか。 |
| `hero_awakening_scene_<heroId>_seen` | bool | 覚醒シーン既読。 |
| `hero_form_<heroId>` | string | `base`、`awakened`、`special`。 |
| `hero_awaken_chapter_<heroId>` | int | 覚醒した章。 |
| `hero_awaken_pending_<heroId>` | bool | 対象章を通過済みだが未覚醒。ロビーや再挑戦で通知する。 |

推奨フォーム:

```text
base      通常姿
awakened  覚醒姿
special   犬化など一時的な特殊姿
```

カガチのみ:

```text
hero_form_HeroKagachi = special
skin_kagachi_unlocked = true
hero_awakened_HeroKagachi = true
hero_form_HeroKagachi = awakened
```

犬化と覚醒を同時に扱う場合は、`special` を戦闘中一時フォーム、`awakened` を恒久フォームとして分けます。

## イベント再生優先順位

1. Chapter opening
2. 章固有の特殊幕間
3. 覚醒戦前シーン
4. 通常ボス戦前3行
5. 戦闘中HPトリガー覚醒
6. 撃破後覚醒
7. 観測片取得
8. Chapter clear interlude
9. Result panel

Chapter08のカガチは例外で、`INT_08` が通常ボス戦前3行を抑止します。  
その後の覚醒解放は撃破後に出します。

## ロビー導線

未覚醒の主人公を選んだ時、対象章が解放済みならロビーで小さく示唆します。

表示例:

```text
覚醒の気配: Chapter08 生は苦しみだけではない
```

ただし初回プレイではネタバレを避けるため、対象章に到達するまでは表示しません。

## 実装方針

第一段階:

- Chapter08のカガチ覚醒を実装。
- `hero_awakened_HeroKagachi`、`hero_form_HeroKagachi` を保存。
- リザルトで覚醒バナーを表示。

第二段階:

- Chapter13から18の基礎主人公6人を実装。
- メカゾ0ア担当主人公イベントと統合。
- HP50%トリガーが難しい場合は撃破後覚醒に寄せる。

第三段階:

- Chapter06のカーラ、Chapter09のジラーンを実装。
- ボス将9人の共闘覚醒を追加。
- 未登録素材をフォーム差し替え方式で接続。

## QAチェック

- 覚醒が章テーマに接続しているか。
- 覚醒前の弱さが消えたように見えないか。
- 覚醒後の口調が別人になっていないか。
- 対象章で該当主人公を使わなかった場合も、後で回収できるか。
- カガチ犬化と覚醒姿が競合していないか。
- 覚醒フォームのスプライト、攻撃、被弾、死亡アニメが全て読み込めるか。
- 既存の主人公熟練度、仲間化、ボス報酬と保存データが衝突していないか。
