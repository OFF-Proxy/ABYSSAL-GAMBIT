# 09 Event Flags

Unity実装時のイベントフラグ正本です。

## 既存 / 提案フラグ

| キー | 型 | 発火条件 | 用途 | 状態 |
| --- | --- | --- | --- | --- |
| int01 | bool | Chapter4-6最初の戦闘前 | 第一合流。既読なら再生しない。 | 実装済み |
| int08 | bool | Chapter8ボス前 + HeroKagachi | カガチ犬化。通常3行ダイアログを抑止。 | 実装済み |
| int12 | bool | Chapter12クリア後 | 観測者の正体。 | 実装済み |
| int13 | bool | 本来はChapter20クリア後 | 最終説得。現行コードはChapter13なので移行必要。 | 要修正 |
| skin_kagachi_unlocked | bool | Chapter8をHeroKagachiでクリア | 犬化スキン解放。 | 実装済み |
| skin_kagachi_on | bool | ロビー切替 | 犬化スキン使用状態。 | 実装済み |
| obs_chXX_unlocked | bool | 各章クリア | 観測片ライブラリ解放。現行コードに追加推奨。 | 提案 |
| arcana_memory_neri | bool | Fragment01閲覧後またはChapter20共鳴 | ネリの記憶解放。アルカナ個人史の第一キー。 | 提案 |
| arcana_memory_olha | bool | Fragment11閲覧後またはChapter20共鳴 | オルハの記憶解放。アルカナ個人史の第二キー。 | 提案 |
| arcana_memory_blank_war | bool | Fragment18閲覧後またはChapter20共鳴 | 白紙戦争の記憶解放。アルカナ個人史の第三キー。 | 提案 |
| mech_focus_chXX_seen | bool | Chapter13-18担当主人公専用台詞再生後 | メカゾ0ア連章の担当主人公イベント既読。 | 提案 |
| hero_awakened_<heroId> | bool | 主人公の覚醒イベント完了 | 覚醒形態の恒久解放。 | 提案 |
| hero_awakening_scene_<heroId>_seen | bool | 覚醒台詞再生後 | 覚醒イベントの既読管理。 | 提案 |
| hero_form_<heroId> | string | ロビーまたは戦闘開始時 | `base`、`awakened`、`special` の現在フォーム。 | 提案 |
| hero_awaken_pending_<heroId> | bool | 対象章到達後、未覚醒 | ロビーで覚醒の気配を表示。 | 提案 |
| caliber_contract_unlocked | bool | Chapter1初回クリア | キャリバー剣片契約。ユニット使用は可能だが物語上は仮加入。 | 提案 |
| caliber_blade_shard | bool | Chapter1初回クリア | キャリバーの剣片獲得。Chapter14正式共闘への伏線。 | 提案 |
| caliber_bond_rank | int | Chapter1再クリア | キャリバー強化段階。Chapter1周回報酬。 | 提案 |
| caliber_formal_pact | bool | Chapter14クリア後 | キャリバー正式共闘 / 誓剣解放。 | 提案 |
| chapter_boss_ally:<unitId> | bossAllies | 章クリア | 章ボス恒久解放。SaveManager.AddBossAlly。 | 実装済み |
| hero_mastery:<heroId> | int/xp | 章クリア | 使用主人公の熟練度XP。 | 実装済み |

## 章ボス仲間化ID

| 章 | Unit ID | 表示名 |
| --- | --- | --- |
| 1 | Caliber | キャリバー |
| 2 | neutral_rook | ルーク |
| 3 | neutral_sister | 終幕のシスター |
| 4 | Magmarvaath | ヴァース |
| 5 | Magmarstarhorn | スターホーン |
| 6 | Magmarragnora | ラグノラ |
| 7 | Abyssallilithe | リリス |
| 8 | Abyssalcassyva | キャシヴァ |
| 9 | Abyssalmaehv | メーヴ |
| 10 | Vetruvianzirix | ジリックス |
| 11 | Vetruviansajj | サージ |
| 12 | Vetruvianscion | サイオン |
| 13 | neutral_mechaz0rwing | メカゾ0ア・翼 |
| 14 | neutral_mechaz0rsword | メカゾ0ア・剣 |
| 15 | neutral_mechaz0rsuper | メカゾ0ア完全体 |
| 16 | neutral_mechaz0rhelm | メカゾ0ア・兜 |
| 17 | neutral_mechaz0rchassis | メカゾ0ア・器 |
| 18 | neutral_mechaz0rcannon | メカゾ0ア・砲 |
| 19 | neutral_hydrax | ハイドラックス |
| 20 | Arcana | アルカナ |

## 最終章トリガー移行メモ

現行コードでは `GetChapterClearInterludeId()` が `currentChapter == 13` で `INT_13` を返します。  
20章構成では以下へ変更してください。

```csharp
if (currentChapter == 20) return "INT_13";
```

併せて `PlayChapterClearStory("INT_13")` の連鎖先 `ENDING` は維持します。  
Chapter13-18はメカゾ0ア連章なので、最終説得を出してはいけません。

## 観測ライブラリ実装メモ

推奨データ:

```text
ObservationEntry
  id: obs_ch01
  chapter: 1
  title: 白門の慈悲
  bodyKey: observation.ch01.body
  revealLevel: voice_only / back_view / named / full
  unlockedFlag: obs_ch01_unlocked
```

観測片は章クリア報酬として即時解放し、ロビーの観測ライブラリで再読可能にします。

## イベント再生優先順位

1. Chapter opening: `CHOPEN_X`
2. Prefight special: `INT_01`, `INT_08`
3. Awakening prefight: `AWAKEN_CHXX_<heroId>`
4. Chapter boss dialogue: `HeroBossDialogueUI`
5. Battle HP trigger awakening
6. Post boss dialogue / post boss awakening
7. Chapter clear interlude: `INT_12`, `INT_13`
8. Ending: `ENDING`
9. Result panel / unlock banner

Chapter8のカガチ犬化は、通常のボス戦前3行より優先します。

## 主人公覚醒キー

覚醒システムの正本は `15_Hero_Awakening_Design.md` です。  
保存キーは主人公IDをそのまま使い、別Unit IDを主人公扱いにしない方針を推奨します。

| Hero ID | 対象章 | 覚醒済みキー | フォームキー |
| --- | --- | --- | --- |
| HeroAldin | 14 | hero_awakened_HeroAldin | hero_form_HeroAldin |
| HeroKagachi | 8 | hero_awakened_HeroKagachi | hero_form_HeroKagachi |
| HeroVesna | 17 | hero_awakened_HeroVesna | hero_form_HeroVesna |
| HeroZiran | 9 | hero_awakened_HeroZiran | hero_form_HeroZiran |
| HeroReva | 13 | hero_awakened_HeroReva | hero_form_HeroReva |
| HeroKara | 6 | hero_awakened_HeroKara | hero_form_HeroKara |
| HeroBrome | 18 | hero_awakened_HeroBrome | hero_form_HeroBrome |
| HeroShidai | 16 | hero_awakened_HeroShidai | hero_form_HeroShidai |
| HeroIlena | 15 | hero_awakened_HeroIlena | hero_form_HeroIlena |
| Magmarvaath | 4 | hero_awakened_Magmarvaath | hero_form_Magmarvaath |
| Magmarstarhorn | 5 | hero_awakened_Magmarstarhorn | hero_form_Magmarstarhorn |
| Magmarragnora | 6 | hero_awakened_Magmarragnora | hero_form_Magmarragnora |
| Abyssallilithe | 7 | hero_awakened_Abyssallilithe | hero_form_Abyssallilithe |
| Abyssalcassyva | 8 | hero_awakened_Abyssalcassyva | hero_form_Abyssalcassyva |
| Abyssalmaehv | 9 | hero_awakened_Abyssalmaehv | hero_form_Abyssalmaehv |
| Vetruvianzirix | 10 | hero_awakened_Vetruvianzirix | hero_form_Vetruvianzirix |
| Vetruviansajj | 11 | hero_awakened_Vetruviansajj | hero_form_Vetruviansajj |
| Vetruvianscion | 12 | hero_awakened_Vetruvianscion | hero_form_Vetruvianscion |

カガチ犬化は `hero_form_HeroKagachi = special` とし、覚醒後の恒久姿は `awakened` として分けます。  
これにより、犬化スキンと覚醒姿の競合を避けられます。

## メカゾ0ア担当主人公キー

Chapter13〜18は、担当主人公で挑んだ場合だけ専用3行を優先します。

| 章 | Boss ID | 担当Hero ID | 推奨既読キー |
| --- | --- | --- | --- |
| 13 | neutral_mechaz0rwing | HeroReva | mech_focus_ch13_seen |
| 14 | neutral_mechaz0rsword | HeroAldin | mech_focus_ch14_seen |
| 15 | neutral_mechaz0rsuper | HeroIlena | mech_focus_ch15_seen |
| 16 | neutral_mechaz0rhelm | HeroShidai | mech_focus_ch16_seen |
| 17 | neutral_mechaz0rchassis | HeroVesna | mech_focus_ch17_seen |
| 18 | neutral_mechaz0rcannon | HeroBrome | mech_focus_ch18_seen |

台詞本文は `14_Implementation_Dialogue_Scripts.md` を参照。

## アルカナ個人史キー

ネリ、オルハ、白紙戦争は必須閲覧条件にしなくてもよいですが、観測ライブラリで既読管理できるとChapter20の共鳴演出が強くなります。

推奨:

- 未閲覧でもChapter20で最低限理解できる。
- 閲覧済みなら、Chapter20の該当台詞直前に観測片が強く光る。
- 全三つ閲覧済みなら、アルカナ仲間化後のロビー台詞を追加解放する。
