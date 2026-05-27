using System.Collections.Generic;
using UnityEngine;

// 盤面上に見えている「1個のアイテム」を表します。
// アイテムベンチに置かれている間だけGameObjectとして存在し、ユニットに装備されたらデータだけがユニット側に残ります。
public class ItemInstance : MonoBehaviour
{
    public ItemData Data { get; private set; }
    public int SlotIndex { get; private set; } = -1;

    private SpriteRenderer spriteRenderer;
    private const int SortingBaseOrder = 12000;
    private float animationTimer;
    private int animationFrame;
    private const float IconAnimationFps = 12f;

    // アイテムアイコンが複数フレームを持つ場合、ベンチ上で軽くアニメーションさせます。
    private void Update()
    {
        if (Data == null || spriteRenderer == null)
            return;

        IReadOnlyList<Sprite> frames = Data.IconFrames;
        if (frames == null || frames.Count <= 1)
            return;

        animationTimer += Time.deltaTime;
        float frameInterval = 1f / IconAnimationFps;

        // フレーム落ちしても一定テンポで進むよう、たまった時間分だけ進めます。
        while (animationTimer >= frameInterval)
        {
            animationTimer -= frameInterval;
            animationFrame = (animationFrame + 1) % frames.Count;
            spriteRenderer.sprite = frames[animationFrame];
        }
    }

    // アイテムデータとベンチスロットを反映します。
    public void Setup(ItemData data, int slotIndex)
    {
        Data = data;
        SlotIndex = slotIndex;
        animationTimer = 0f;
        animationFrame = 0;
        name = data != null ? $"Item_{data.displayName}" : "Item";
        EnsureRenderer();

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = data != null ? data.Icon : null;
            spriteRenderer.sortingOrder = SortingBaseOrder + slotIndex;
            spriteRenderer.color = Color.white;
        }

        FitColliderToIcon();
    }

    // ベンチ内で移動した時、スロット番号だけ更新します。
    public void SetSlotIndex(int slotIndex)
    {
        SlotIndex = slotIndex;

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = SortingBaseOrder + slotIndex;
    }

    // ドラッグ中に前面へ出すため、外部から描画順を変えます。
    public void SetSortingOrder(int sortingOrder)
    {
        EnsureRenderer();

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = sortingOrder;
    }

    // Canvas版アイテムベンチを使う時は、ワールド上の見た目とクリック判定だけを隠します。
    public void SetWorldVisible(bool visible)
    {
        EnsureRenderer();

        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = visible;

        DraggableItem draggableItem = GetComponent<DraggableItem>();
        if (draggableItem != null)
            draggableItem.enabled = visible;
    }

    // アイコンを表示するSpriteRendererを用意します。
    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    // マウスで掴みやすいよう、アイコンサイズに合わせたColliderを用意します。
    private void FitColliderToIcon()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds bounds = spriteRenderer.sprite.bounds;
            box.size = bounds.size;
            box.offset = bounds.center;
            return;
        }

        box.size = Vector2.one * 0.6f;
        box.offset = Vector2.zero;
    }
}
