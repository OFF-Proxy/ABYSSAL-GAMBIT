# Codex発注: Chapter1 セリフ回し調整（見た目→口調）

> 目的: 章1のセリフを、各キャラの**見た目（立ち絵/アイコン/ショップ画像）から推定した口調**に沿って書き直す。
> ストーリーの筋・感情の節目・分岐は**変えない**。変えるのは語り口（語彙・リズム・一人称・語尾・温度）だけ。
> 参照ワークフローの正本は `docs/Story/CHARACTER_VISUAL_GUIDE.md`。本書はその章1実行版。

## 0. Codexへの指示（プロンプトにそのまま入れる）

「添付した画像から各キャラの**外見・雰囲気を把握し、そこからペルソナ（性格・口調）を推定**して、
その口調で章1の該当セリフを書き直せ。下の『現行ベースライン』は現状の語り口メモであり、
**画像と食い違う場合は画像を優先**する。物語の意味・順番・結論は保持し、口調のみ調整する。
日本語(JA)と英語(EN)を必ず両方更新し、`LocalizationManager.ApplyFont` の呼び出しは壊さない。」

## 1. 添付する画像（この2枚＋必要なら個別ファイル）

- 一覧: `docs/Story/codex_ch1_voice_refs/_contact_heroes_boss.png`（キャリバー＋主人公9人）
- 一覧: `docs/Story/codex_ch1_voice_refs/_contact_midboss.png`（章1中ボス7体）
- 個別の高解像は同フォルダ内 `heroes/ boss/ midboss/` に格納（`__shop` `__portrait` `__icon` `__standing` `__speech`）。

## 2. 章1で喋るキャラと画像パス（正本）

### 章ボス: キャリバー / `Caliber`

| 種別 | パス |
|---|---|
| ショップ画像 | `Assets/Images/Units/Icon/T4/Caliber.png` |
| ダイアログ立ち絵 | `Assets/Resources/UI/Dialog/general_boss_1.png` |
| スピーチ立ち絵 | `Assets/Resources/UI/Dialog/speech_portrait_calibero.png` |

現行ベースライン: 低く厳粛、騎士的。怒鳴らず刃のように重い。一人称「私」。慈悲＝終焉と信じかけた後悔の英雄。暴走の奥に悲しみ。軽口・即改心は禁止。

### 主人公9人（キャリバー戦の戦前/撃破後で応答）

画像: ショップ=`Assets/Images/Units/Icon/Hero/<Name>.png`、立ち絵=`Assets/Resources/UI/Dialog/hero_<name>.png`、顔アイコン=下表。

| heroId | ショップ/立ち絵 | 顔アイコン | 現行ベースライン（画像で確認せよ） |
|---|---|---|---|
| HeroAldin | Hero/Aldin.png ・ hero_aldin.png | — | 金獅子鎧＋大剣。実直で熱いライオネル騎士。一人称「俺」、芯のある静かな熱。 |
| HeroKagachi | Hero/Kagachi.png ・ hero_kagachi.png | — | 赤黒の鬼侍＋双刃。荒く短いソンガイ。噛みつく物言い（「うるせえ」「勝手に決めるな」）。 |
| HeroVesna | Hero/Vesna.png ・ hero_vesna.png | — | 赤毛・鷹・刃付き銃・尾。野性的で凛、情がにじむヴァナー。短く鋭い。 |
| HeroZiran | Hero/Ziran.png ・ hero_ziran.png | `DialogueIcon/lyonar_ziransunforge1.jpg` | 太陽紋の白金鎧＋盾＋細剣。ですます調の癒し手、静かな祈りの落ち着き。 |
| HeroReva | Hero/Reva.png ・ hero_reva.png | `DialogueIcon/songhai_revaeventide1.jpg` | 龍角冠・赤金の龍紋・双刃。華やかで大胆な自由人。軽口とプライド。 |
| HeroKara | Hero/Kara.png ・ hero_kara.png | `DialogueIcon/vanar_karawinterblade1.jpg` | 毛皮・氷角・巨大氷鎚。寡黙で重厚、辛抱強い。低く静かで短い。 |
| HeroBrome | Hero/Brome.png ・ hero_brome.png | `DialogueIcon/lyonar_brome1.png` | 炎装飾の金鎧＋大盾、髭の旗頭。温かく力強い号令、包容力。 |
| HeroShidai | Hero/Shidai.png ・ hero_shidai.png | `DialogueIcon/songhai_shidai1.png` | 白銀髪＋双刃、俊敏。多くを語らず要点を突く、影の静かな誇り。 |
| HeroIlena | Hero/Ilena.png ・ hero_ilena.png | `DialogueIcon/vanar_ilena1.png` | 氷晶を纏う暗色鎧＋槍。冷静で理知的、観測者的な距離感、分析口調。 |

### 章1中ボス7体（ルート/固定で登場。各1個体）

画像: 立ち絵=`Assets/Images/Units/Icon/T3/<id>.png`、顔=`Assets/Resources/AddUnit/DialogueIcon/<id>.jpg`。
※リカラー素体は着色済み画像。色＝キャラの気質のヒント（金＝豪胆、白/氷＝儚い/幼い、鋼＝機械的 等）。

| 名前 | 素体id | 現行ベースライン（画像で確認せよ） |
|---|---|---|
| 金鬣の獣戦士バルガ | `neutral_beastmaster` | 豪快な獣戦士。「ガハハッ」系の荒い快活。キャリバーの咆哮に心酔。 |
| 傷鬣の獣戦士ロウガ | `neutral_beastmaster_crimson` | 老練で吠え飽きた獣。低く諭すような凄み。 |
| 封符の屍爪リンシェン | `neutral_gnasher` | 札に縛られた屍。「ちりん…」と鈴の反復、静かに深い。 |
| 白符の屍爪メイリン | `neutral_gnasher_ice` | 幼く儚い屍。舌足らずで怖がり（「しずか、しずか…」）。 |
| 鋼牙の機豹ラウル | `neutral_rawr` | 機械豹。計器的・命令実行口調（「捕捉完了」「ノイズとして処理」）。 |
| 白門の岩顎ゴルム | `neutral_rok` | 岩のゴーレム。単語切りの重い訥弁（「ゴルム、門を守る」）。 |
| 雲棍の猿将ズーコン | `neutral_zukong` | 軽妙な猿将。跳ねる挑発、道化めいた明るさ。 |

## 3. 編集するコード箇所（章1のみ）

- 章導入プロローグ: `Assets/Scripts/ChapterStory.cs` → `GetPrologue(1)` の `lines`
- 章導入VN: `Assets/Scripts/InterludeScript.cs` → `BuildChapterOpen1`
- 節目チャッター: `Assets/Scripts/ChapterStory.cs` → `BuildChatter()` の `chatterByChapter[1]`（キャリバー遠くの声）
- 中ボス会話: `Assets/Scripts/ChapterStory.cs` → `Build()` の `midByChapter[1]`（V(...) の3行＝出会い/主人公反論/戦闘前）
- 章ボス戦前: `Assets/Scripts/HeroBossDialogueUI.cs` → `BuildScriptedLines()` の `S("Caliber", "Hero...", ...)`
- 章ボス撃破後: `Assets/Scripts/HeroBossDialogueUI.cs` → `BuildPostBossLines()` の `postBossLines["caliber|hero..."]`

進行メモ（誰がどこで喋るか）:
- 固定中ボス: `2-5 beastmaster`, `2-10 gnasher`, `3-5 rawr`, `3-10 rok`
- 進路選択中ボス `4-7`/`4-8`: Elite=`zukong`, Standard=`beastmaster_crimson`, Supply=`gnasher_ice`（選んだノードのみ喋る）
- 章ボス: `4-10 Caliber`

## 4. 制約（厳守）

- 物語の意味・感情の節目・結論（キャリバーは改心でなく剣片を託す／アルカナ名は出さない＝「優しい声」まで）は変えない。
- JA/EN両方を更新。`LocalizationManager.IsJapanese` 分岐と `ApplyFont(text)` を壊さない。片言語のハードコードを残さない。
- 実数値・変数参照・キー文字列（`caliber|heroaldin` 等）は変更しない。
- 完了後 `Compilation completed (Errors: False)` を確認してコミット。コミット規約は `docs/COLLAB_PROTOCOL.md §2.3`。

## 5. 成果物

- 変更: 上記6箇所の章1セリフ（口調調整）。
- 追記: `docs/ROADMAP.md` に1行、`docs/Story/CHARACTER_VISUAL_GUIDE.md` の外見メモを実画像準拠で確定（気付いた範囲で）。
