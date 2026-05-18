using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 盤面やベンチの1マスを表すクラスです。配置可能/不可能のハイライトも担当します。
public class Tile : MonoBehaviour
{
    // タイル本体は背景より上、ユニットより下に表示します。
    private const int TileSortingOrder = 100;
    // ハイライトはタイル本体より上に表示します。
    private const int TileHighlightSortingOrder = 150;

    // ホバー表示専用のSpriteRendererです。未設定ならタイル本体を直接差し替えます。
    public SpriteRenderer highlightSprite;
    // 置ける場所にマウスを乗せた時の色です。
    public Color validColor;
    // 置けない場所にマウスを乗せた時の色です。
    public Color wrongColor;

    // タイル本体のSpriteRendererです。
    private SpriteRenderer tileRenderer;
    // 通常時のスプライトを覚えておき、ハイライト解除時に戻します。
    private Sprite baseSprite;
    // 通常時の色を覚えておき、ハイライト解除時に戻します。
    private Color baseColor = Color.white;
    // ホバー時に使う差し替えスプライトです。
    private Sprite hoverSprite;
    // 初期化を二重に行わないためのフラグです。
    private bool initialized;

    // Unityが生成時に呼ぶ初期化です。
    private void Awake()
    {
        Initialize();
    }

    // GridManagerから見た目の設定を渡されます。
    public void Configure(Sprite hoverSprite, Color baseColor, Color validColor, Color wrongColor)
    {
        Initialize();
        this.hoverSprite = hoverSprite;
        this.baseColor = baseColor;
        this.validColor = validColor;
        this.wrongColor = wrongColor;
        ResetHighlight();
    }

    // ドラッグ中に、このタイルを配置先候補として光らせます。
    public void SetHighlight(bool active, bool valid)
    {
        Initialize();

        // 専用Rendererがあればそこを使い、無ければタイル本体を使います。
        SpriteRenderer targetRenderer = GetHighlightRenderer();
        if (targetRenderer == null)
            return;

        if (!active)
        {
            ResetHighlight();
            return;
        }

        if (targetRenderer != tileRenderer)
            targetRenderer.gameObject.SetActive(true);

        targetRenderer.sprite = hoverSprite != null ? hoverSprite : baseSprite;
        targetRenderer.color = valid ? validColor : wrongColor;
    }

    // 必要なRenderer参照や元の見た目を一度だけ取得します。
    private void Initialize()
    {
        if (initialized)
            return;

        tileRenderer = GetComponent<SpriteRenderer>();
        if (tileRenderer != null)
        {
            baseSprite = tileRenderer.sprite;
            baseColor = tileRenderer.color;
            tileRenderer.sortingOrder = TileSortingOrder;
        }

        if (highlightSprite != null)
        {
            if (tileRenderer != null)
                highlightSprite.sortingLayerID = tileRenderer.sortingLayerID;

            highlightSprite.sortingOrder = TileHighlightSortingOrder;
        }

        initialized = true;
    }

    // ハイライト専用Rendererがある時はそれを、無い時は本体Rendererを返します。
    private SpriteRenderer GetHighlightRenderer()
    {
        if (highlightSprite != null && highlightSprite.gameObject != gameObject)
            return highlightSprite;

        return tileRenderer;
    }

    // ハイライトを消して通常のタイル表示に戻します。
    private void ResetHighlight()
    {
        SpriteRenderer targetRenderer = GetHighlightRenderer();
        if (targetRenderer == null)
            return;

        if (tileRenderer != null)
        {
            tileRenderer.sprite = baseSprite;
            tileRenderer.color = baseColor;
        }

        if (targetRenderer != tileRenderer)
            targetRenderer.gameObject.SetActive(false);
    }
}
