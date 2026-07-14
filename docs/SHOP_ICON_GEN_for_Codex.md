# ショップアイコン生成レシピ（gpt-image-2 / API方式）— Codex用

内蔵画像生成は無関係画像を返す不具合のため使用しない。**OpenAI API (gpt-image-2) を CLI/SDK で呼ぶ**こと。
対象ユニット・参照素材・保存先の一覧は `docs/SHOP_ICON_TODO_for_Codex.md` を正本とする。本書はその「作り方」。

## 0. 前提
- `OPENAI_API_KEY` を環境変数に設定（ユーザーが用意）。
- Python: `pip install openai pillow`。

## 1. 仕様（厳守）
- **最終解像度: 290×130 px、29:13 の横長**（既存アイコンと一致）。RGB・**透過なし（背景あり）**。
- **画風: 既存の手描きカードイラスト**に合わせる。並べて浮かないこと。
  - 画風リファレンス（高解像・1873×840）例: `Assets/Images/Units/Icon/T5/Kron.png`, `.../Legion.png`, `.../Embergeneral.png`。
- 構図: キャラを中央〜やや左、横長に収まる全身〜上半身。背景は淡いシーン/グラデ（既存に倣う）。

## 2. キャラの見た目リファレンス（誰を描くか）
- **ダイアログアイコンがあるユニット**（中立中ボス・リカラー・各陣営非将・ヒーロー・章ボス等）
  → そのダイアログアイコン/立ち絵を `images.edit` の入力に渡し、キャラの容姿を保持したまま 29:13 カード絵に描き直す。パスは TODO 表の「参照」列。
- **Songhai 4体（Lanternfox / Onyxjaguar / Keshraifanblade / Firewyrm）はダイアログアイコン無し＝スプライトのみ**。
  → スプライト（例 `Assets/Images/Units/Sprite/T1/Lanternfox/f2_lanternfox.png`、512×1024のアトラス。`.plist`で1フレーム切出し可）を容姿参照にし、テキストプロンプト＋画風リファレンス画像で生成。

## 3. プロンプト雛形
```
A single-character fantasy trading-card illustration, painterly digital art,
landscape banner composition (about 29:13 aspect, wide). Subject: <UNIT DESC>.
Match the art style of the reference card images: hand-painted, dramatic lighting,
soft background scene. The character occupies the center, fully visible, no text,
no card frame, no UI. Cohesive with the provided style references.
```
`<UNIT DESC>` は容姿参照画像から起こす（例 Lanternfox=「two-tailed fox spirit holding glowing lanterns, Songhai eastern fantasy」）。

## 4. 呼び出し（容姿参照ありの例 / images.edit）
```python
from openai import OpenAI
client = OpenAI()
res = client.images.edit(
    model="gpt-image-2",
    image=[open(char_ref,"rb"), open(style_ref,"rb")],  # 1枚目=容姿, 2枚目=画風
    prompt=PROMPT,
    size="1536x1024",          # 最大の横長。後段でクロップ
)
```
スプライトのみ（Songhai）は `images.generate(model="gpt-image-2", prompt=PROMPT, size="1536x1024")`
＋プロンプトに画風参照の説明を厚めに。可能なら style_ref を edit で併用。

## 5. 後処理 → 290×130 で上書き保存
```python
from PIL import Image
def to_icon(src_png, dst_png):
    im = Image.open(src_png).convert("RGB")
    w,h = im.size
    target = 29/13
    # 中央クロップして 29:13 に
    if w/h > target:
        nw = int(h*target); x=(w-nw)//2; im = im.crop((x,0,x+nw,h))
    else:
        nh = int(w/target); y=(h-nh)//2; im = im.crop((0,y,w,y+nh))
    im = im.resize((290,130), Image.LANCZOS)
    im.save(dst_png)   # 既存があれば上書き
```
保存先は TODO 表の「保存先」列（`Assets/Images/Units/Icon/<Tier>/<Unit>.png`）。
**既存の Lanternfox.png / Onyxjaguar.png（前回の微妙な版）は上書きする。**

## 6. 順序
1. **Lanternfox, Onyxjaguar を最優先**（ユーザー指定）。1枚ずつ生成→ユーザー確認。
2. OKなら Keshraifanblade, Firewyrm → 残り（ヒーロー→中ボス→章ボス→雑魚）。

## 7. 反映（生成後）
- 290×130 PNG を保存後、Unity で `Tools/AutoChess/Sync Entity Database` を実行 → カード/ショップに反映。
