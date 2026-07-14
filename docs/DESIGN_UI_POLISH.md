# DESIGN_UI_POLISH: HUD / 関連UIの見栄え修正
> **状態: ✅ 実装済み（2026-05-29, オーグメント可視化パス／HUD改善）**

## ゴール
**「目も当てられない」状態の HUD/UI を「見える・分かる」状態にする**。原因は (1) スプライトがスライスされていない／9-slice border 未設定、(2) HUD タイルが小さすぎ、(3) 不要なコントラストの低さ。

## 影響範囲
- 変更ファイル:
  - `Assets/Scripts/AugmentHudUI.cs`（タイル拡大・レイアウト調整）
  - `Assets/Scripts/AugmentTooltipUI.cs`（軽微・アイコンサイズ）
  - `Assets/Scripts/SynergyPanelUI.cs`（"+N" バッジ拡大）
  - `Assets/Scripts/UIShop.cs`（リロールスタックバッジ拡大）
- 変更アセット:
  - `Assets/Resources/UI/Augment/kind_stat.png.meta`（スプライトシート切り直し）
  - `Assets/Resources/UI/Augment/kind_special.png.meta`（同上）
  - `Assets/Resources/UI/Augment/card_panel.png.meta`（9-slice border 設定）
  - `Assets/Resources/UI/Augment/augment_frame.png.meta`（9-slice border 設定）

---

## 調査で判明した事実

`mcp__unity-synaptic__run_csharp` で各 sprite を `LoadAssetAtPath<Texture2D>` して確認した実寸:

| ファイル | 解像度 | spriteImportMode | spriteBorder | 問題 |
|---|---|---|---|---|
| `rarity_silver.png` | 44x44 | Single | (0,0,0,0) | OK |
| `rarity_gold.png` | 44x44 | Single | (0,0,0,0) | OK |
| `rarity_prism.png` | 50x50 | Single | (0,0,0,0) | OK |
| `card_panel.png` | 140x140 | Single | **(0,0,0,0)** | 9-slice が機能していない。`Image.Type.Sliced` 指定でも Border=0 では Simple stretch にフォールバックして、HUD 全幅 552px に引き伸ばされて滲む |
| `augment_frame.png` | 68x70 | Single | **(0,0,0,0)** | 同上 |
| `badge_counter.png` | 124x86 | Single | **(0,0,0,0)** | 非正方形でリロールスタックバッジ／シナジー +N に使われ、無理に円形扱いされてつぶれて見える |
| `kind_stat.png` | **512x256** | Single | (0,0,0,0) | **2分割スプライトシート**。1枚絵として表示すると2フレーム同時表示され、HUD タイル中央で「ぼやけた帯」になる |
| `kind_special.png` | **512x256** | Single | (0,0,0,0) | 同上 |
| `kind_synergy.png` | 256x256 | Single | (0,0,0,0) | 形状 OK だが拡大時にエッジ目立つ |
| `kind_combat.png` | 256x256 | Single | (0,0,0,0) | 同上 |
| `kind_economy.png` | 71x71 | Single | (0,0,0,0) | OK |
| `kind_item.png` | 57x63 | Single | (0,0,0,0) | OK |

加えて：
- HUD タイル: 58px × 1行8タイル → 非常に小さい
- HUD ヘッダー: 12pt フォント、目立たない
- ベース背景: `Color(0.04f, 0.06f, 0.08f, 0.82f)` の極暗 → 周囲のゲーム背景と同化

---

## バグ U1: 2 分割スプライトシートを単一スプライトとして読んでいる

### 該当ファイル
- `Assets/Resources/UI/Augment/kind_stat.png` (512x256)
- `Assets/Resources/UI/Augment/kind_special.png` (512x256)

### 修正方針

**選択肢 A（推奨）: TextureImporter で Multiple モードにし、左半分の 256x256 だけ切り出して name で取り出す**

`kind_stat.png.meta` の Importer を Multiple に変更し、Sprite Editor で 2 つの 256x256 セルにスライス。1番目のフレーム（`kind_stat_0`）を `kind_stat` として参照する Resources Subasset API か、もしくは下記の Editor スクリプトで一括設定する：

```csharp
// Editor 専用一回限りスクリプト案 (run_csharp で実行可能)
void SliceTwoFrames(string path)
{
    var ti = (TextureImporter)AssetImporter.GetAtPath(path);
    ti.spriteImportMode = SpriteImportMode.Multiple;
    var prov = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
    prov.Init();
    var dp = prov.GetSpriteEditorDataProviderFromObject(ti);
    dp.InitSpriteEditorDataProvider();
    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    int w = tex.width / 2;
    int h = tex.height;
    var rects = new[] {
        new UnityEditor.U2D.Sprites.SpriteRect { name = Path.GetFileNameWithoutExtension(path) + "_0",
            rect = new Rect(0,0,w,h), pivot = new Vector2(0.5f,0.5f), alignment = SpriteAlignment.Center },
        new UnityEditor.U2D.Sprites.SpriteRect { name = Path.GetFileNameWithoutExtension(path) + "_1",
            rect = new Rect(w,0,w,h), pivot = new Vector2(0.5f,0.5f), alignment = SpriteAlignment.Center },
    };
    dp.SetSpriteRects(rects);
    dp.Apply();
    ti.SaveAndReimport();
}
```

呼び出し側（AugmentHudUI.LoadSprites）は、Resources.Load<Sprite>(...) を **subasset 名指定**に変更：
```csharp
// kind_stat と kind_special だけ サブスプライト名で取得
kindSprites[AugmentEffectKind.Stat] = LoadSpriteByName("UI/Augment/kind_stat", "kind_stat_0");
kindSprites[AugmentEffectKind.Special] = LoadSpriteByName("UI/Augment/kind_special", "kind_special_0");

private static Sprite LoadSpriteByName(string resourcePath, string spriteName)
{
    Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
    for (int i = 0; i < all.Length; i++)
        if (all[i].name == spriteName) return all[i];
    return all.Length > 0 ? all[0] : null;
}
```

**選択肢 B（簡単・推奨できる）: 別の reference 素材に差し替え**

reference/duelyst の `icons/icon_f1_*.png` には 256x256 単一スプライトが大量にある。下記で代替推奨：
- `kind_stat` ← `icon_f1_buffbloom`（ステータスバフのイメージに合う）
- `kind_special` ← `icon_f1_aurynnexus`（特殊効果のイメージに合う）

これなら meta 編集不要で、ただ PNG を再コピー＆既存と同じ Importer 設定で済む。**B を採用する。**

### 受け入れ基準
- [ ] `kind_stat.png` `kind_special.png` の表示が **1 枚絵としてくっきり**見える（HUD タイル中央に icon、左右に黒帯がない）
- [ ] 旧 512x256 PNG を削除（あるいはバックアップ済みなら `_old` 名にリネーム）

---

## バグ U2: 9-slice 対象スプライトの border が未設定

### 該当ファイル
- `Assets/Resources/UI/Augment/card_panel.png` (140x140)
- `Assets/Resources/UI/Augment/augment_frame.png` (68x70)

`Image.Type.Sliced` を指定しても `spriteBorder = (0,0,0,0)` だと Simple として扱われ、引き伸ばしによる滲みが発生。

### 修正方針

`mcp__unity-synaptic__run_csharp` で TextureImporter に border を設定する一回スクリプト：

```csharp
void SetBorder(string path, int l, int b, int r, int t)
{
    var ti = (TextureImporter)AssetImporter.GetAtPath(path);
    ti.spriteBorder = new Vector4(l, b, r, t);
    EditorUtility.SetDirty(ti);
    ti.SaveAndReimport();
}
SetBorder("Assets/Resources/UI/Augment/card_panel.png", 18, 18, 18, 18);
SetBorder("Assets/Resources/UI/Augment/augment_frame.png", 12, 12, 12, 12);
```

数値は元画像の縁飾りに合わせて微調整 (Sprite Editor で目視確認推奨)。

`AugmentHudUI.cs` 側は既に `background.type = Image.Type.Sliced` を指定しているので、border 設定後は自動で 9-slice 表示に切り替わる。

### 受け入れ基準
- [ ] HUD パネルの背景が、サイズが変わっても**滲まず**両端の縁飾りが保たれる
- [ ] augment HUD タイルの枠線がシャープに表示される

---

## バグ U3: HUD タイルが小さく見えない

### 現状
[AugmentHudUI.cs:22-25](../Assets/Scripts/AugmentHudUI.cs#L22-L25)
```csharp
private const float TileSize = 58f;
private const float TileSpacing = 6f;
private const int TilesPerRow = 8;
```

58px は 1080p 表示でかなり小さい。

### 修正方針

```csharp
private const float TileSize = 78f;   // 58 → 78
private const float TileSpacing = 8f; // 6 → 8
private const int TilesPerRow = 6;    // 8 → 6 （横幅を抑える）
```

[AugmentHudUI.cs:118-119](../Assets/Scripts/AugmentHudUI.cs#L118-L119) のパネル背景色も明るく：
```csharp
background.color = new Color(0.08f, 0.10f, 0.14f, 0.88f);  // 0.04/0.06/0.08 → 明るめ
```

[AugmentHudUI.cs:233](../Assets/Scripts/AugmentHudUI.cs#L233) `KindIcon` の中央アイコンも拡大：
```csharp
iconRect.sizeDelta = new Vector2(48f, 48f);  // 36 → 48
```

[AugmentHudUI.cs:131](../Assets/Scripts/AugmentHudUI.cs#L131) ヘッダーフォント:
```csharp
headerText.fontSize = 14f;  // 12 → 14
```

### 受け入れ基準
- [ ] 1080p ディスプレイで右上 HUD が無理なく視認できる
- [ ] アイコンの色・形状がはっきり判別できる
- [ ] パネル全体の幅が画面の 1/3 以下に収まる（6×78 + 5×8 + 24 = 532px 程度）

---

## バグ U4: シナジー "+N" バッジが極小

### 現状
[SynergyPanelUI.cs:344-345](../Assets/Scripts/SynergyPanelUI.cs#L344-L345)
```csharp
badgeRect.anchoredPosition = new Vector2(46f, 12f);
badgeRect.sizeDelta = new Vector2(20f, 13f);
```

20×13 のサイズに「+1」を表示しても見えない。

### 修正方針

```csharp
badgeRect.anchoredPosition = new Vector2(54f, 14f);
badgeRect.sizeDelta = new Vector2(26f, 18f);
```

文字サイズも：
```csharp
badgeText.fontSize = 12f;  // 10 → 12
```

色も視認性向上 — 紫の濃淡を強める：
```csharp
badgeBg.color = new Color(0.85f, 0.55f, 1f, 1f);  // r, g, b 微調整
```

### 受け入れ基準
- [ ] augment で Warrior/Ranger/Arcanist を取得時、シナジー行の右側に「+1」「+2」など読めるサイズのバッジが表示される

---

## バグ U5: リロールスタックバッジ（badge_counter）が変形

### 現状
[UIShop.cs:519](../Assets/Scripts/UIShop.cs#L519) の `EnsureRerollStackBadge`:
```csharp
rect.sizeDelta = new Vector2(30f, 30f);
rerollStackBadgeIcon.sprite = badgeSprite;  // 124x86 の長方形画像
rerollStackBadgeIcon.preserveAspect = true;
```

`badge_counter.png` (124x86) を 30×30 に preserveAspect で表示 → 画像内部の縁飾りが見えない／中央に小さく圧縮される。

### 修正方針

**選択肢 A（推奨）**: badge を 30x21 (124:86 のアスペクトに合わせる) で表示。サイズも気持ち拡大：
```csharp
rect.sizeDelta = new Vector2(38f, 26f);
rerollStackBadgeIcon.preserveAspect = true;
```

そしてテキストフォントサイズも上げる：
```csharp
rerollStackBadgeText.fontSize = 16f;  // 14 → 16
```

**選択肢 B**: badge_counter を捨てて、reference の `icon_cooldown_counter` を再コピーするか、Unity の組込み UI Sprite (`UISprite`) を使って円形に作る。

A を採用。シンプルで早い。

### 受け入れ基準
- [ ] gold_free_reroll 取得状態で 1 ラウンドリロールせずに次ラウンドに進むと、リロールボタン右上に "x1" の読めるバッジが出る
- [ ] スタックが 3 まで貯まったとき "x3" が正しく表示される

---

## バグ U6（軽微）: AugmentTooltipUI のアイコンが小さい

### 現状
[AugmentTooltipUI.cs:91](../Assets/Scripts/AugmentTooltipUI.cs#L91)
```csharp
kindRect.sizeDelta = new Vector2(22f, 22f);
```

ホバー表示のタイトル横アイコンが小さい。

### 修正方針
```csharp
kindRect.sizeDelta = new Vector2(28f, 28f);
kindRect.anchoredPosition = new Vector2(12f, -8f);
// titleRect.sizeDelta も追従調整
titleRect.sizeDelta = new Vector2(-56f, 28f);
```

### 受け入れ基準
- [ ] ホバー時のツールチップでカテゴリアイコンが視認しやすくなる

---

## まとめ: 実装順

1. **U1** （差し替え路線）: `kind_stat` `kind_special` を `icon_f1_buffbloom` `icon_f1_aurynnexus` に再コピー
2. **U2**: `card_panel` `augment_frame` の border を設定（一回スクリプト）
3. **U3**: AugmentHudUI のサイズ・色をまとめて変更
4. **U4**: SynergyPanelUI の +N バッジ拡大
5. **U5**: UIShop のリロールスタックバッジ調整
6. **U6**（軽微）: AugmentTooltipUI のアイコン拡大

各変更は独立。1コミット1修正で。

---

## 実装後の検証

1. ゲーム起動 → 右上 HUD が空（augment 未所持時はパネル非表示）
2. 2-3 で Silver augment 取得 → HUD に 1 マス（78×78）でレアリティ枠 + Stat/Synergy/Combat etc. の鮮明なアイコン
3. 3-3 で Gold augment 取得 → 金色のレアリティ枠で表示
4. 6 マス埋まったら次の augment は折り返して 2 行目へ
5. リロールボタン: gold_free_reroll を取得 → ボタンが「無料」表示、消費せずに次ラウンドへ → x1 バッジ
6. シナジー Warrior augment → SynergyPanel の Warrior 行に「+1」が読めるサイズで表示

---

## 未決事項（Codex への質問）

なし。`QUESTIONS.md` を活用してください。

最終更新: 2026-05-29 (Claude)
