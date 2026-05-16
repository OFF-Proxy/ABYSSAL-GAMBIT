using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    private const int TileSortingOrder = 100;
    private const int TileHighlightSortingOrder = 150;

    public SpriteRenderer highlightSprite;
    public Color validColor;
    public Color wrongColor;

    private SpriteRenderer tileRenderer;
    private Sprite baseSprite;
    private Color baseColor = Color.white;
    private Sprite hoverSprite;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    public void Configure(Sprite hoverSprite, Color baseColor, Color validColor, Color wrongColor)
    {
        Initialize();
        this.hoverSprite = hoverSprite;
        this.baseColor = baseColor;
        this.validColor = validColor;
        this.wrongColor = wrongColor;
        ResetHighlight();
    }

    public void SetHighlight(bool active, bool valid)
    {
        Initialize();

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

    private SpriteRenderer GetHighlightRenderer()
    {
        if (highlightSprite != null && highlightSprite.gameObject != gameObject)
            return highlightSprite;

        return tileRenderer;
    }

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
