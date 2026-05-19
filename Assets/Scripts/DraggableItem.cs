using UnityEngine;
using UnityEngine.EventSystems;

// アイテムをマウスでドラッグし、ユニットへ装備したりアイテムベンチ内で移動したりするクラスです。
public class DraggableItem : MonoBehaviour
{
    public Vector3 dragOffset = Vector3.zero;

    private Camera cam;
    private ItemInstance itemInstance;
    private SpriteRenderer spriteRenderer;
    private Vector3 oldPosition;
    private Vector3 pointerOffset;
    private int oldSortingOrder;
    private Tile previousTile;
    private Vector3 mouseDownScreenPosition;
    private bool pointerDown;
    private bool isDragging;
    private const float ClickDragThreshold = 8f;

    // 生成直後に必要な参照を取得します。
    private void Awake()
    {
        EnsureReferences();
    }

    // シーン開始後にもCamera.mainを取り直せるようにします。
    private void Start()
    {
        EnsureReferences();
    }

    // マウスを押した瞬間の位置を覚えます。
    // 少し動くまではクリック扱いにして、効果説明を開けるようにしています。
    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        EnsureReferences();
        if (itemInstance == null || spriteRenderer == null)
            return;

        oldPosition = transform.position;
        pointerOffset = transform.position - GetPointerWorldPosition() + dragOffset;
        oldSortingOrder = spriteRenderer.sortingOrder;
        mouseDownScreenPosition = Input.mousePosition;
        pointerDown = true;
        isDragging = false;
    }

    // ドラッグ中はアイテムをマウスに追従させ、アイテムベンチ上ならハイライトします。
    private void OnMouseDrag()
    {
        if (!pointerDown)
            return;

        if (!isDragging)
        {
            Vector3 screenDelta = Input.mousePosition - mouseDownScreenPosition;
            if (screenDelta.sqrMagnitude < ClickDragThreshold * ClickDragThreshold)
                return;

            BeginDrag();
        }

        if (!isDragging)
            return;

        Vector3 newPosition = GetPointerWorldPosition() + pointerOffset;
        newPosition.z = 0f;
        transform.position = newPosition;

        if (GameManager.Instance == null)
            return;

        int slotIndex = GameManager.Instance.GetItemBenchSlotAtWorldPosition(GetPointerWorldPosition());
        Tile tile = GameManager.Instance.GetItemBenchTileAtSlot(slotIndex);
        if (tile != null)
        {
            SetHoveredTile(tile, GameManager.Instance.CanPlaceItemOnBench(itemInstance, slotIndex));
            return;
        }

        ClearHoveredTile();
    }

    // マウスを離した時に、ユニット装備、ベンチ移動、元の位置へ戻す処理のどれかを行います。
    private void OnMouseUp()
    {
        if (!pointerDown)
            return;

        pointerDown = false;

        if (!isDragging)
        {
            if (itemInstance != null && itemInstance.Data != null)
                ItemTooltipUI.Show(itemInstance.Data, Input.mousePosition);

            return;
        }

        bool released = TryRelease();
        if (!released)
            transform.position = oldPosition;
        else
            AttackEffectPlayer.PlayUiSfx("drag_drop");

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = oldSortingOrder;

        ClearHoveredTile();
        isDragging = false;
    }

    // クリックではなくドラッグだと確定したタイミングで、前面表示とSEを開始します。
    private void BeginDrag()
    {
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = 22000;

        ItemTooltipUI.Hide();
        isDragging = true;
        AttackEffectPlayer.PlayUiSfx("drag_start");
    }

    // ドロップ先を順番に試します。ユニット装備を優先し、次にアイテムベンチ内移動を試します。
    private bool TryRelease()
    {
        if (GameManager.Instance == null || itemInstance == null)
            return false;

        BaseEntity targetEntity = FindEntityUnderPointer();
        if (targetEntity != null && GameManager.Instance.TryEquipItemToEntity(itemInstance, targetEntity))
            return true;

        int slotIndex = GameManager.Instance.GetItemBenchSlotAtWorldPosition(GetPointerWorldPosition());
        if (GameManager.Instance.TryPlaceItemOnBench(itemInstance, slotIndex))
            return true;

        return false;
    }

    // マウス位置にいる味方ユニットを探します。
    private BaseEntity FindEntityUnderPointer()
    {
        Vector3 pointerWorld = GetPointerWorldPosition();
        Collider2D[] hits = Physics2D.OverlapPointAll(pointerWorld);
        BaseEntity closestEntity = null;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
                continue;

            BaseEntity entity = hits[i].GetComponentInParent<BaseEntity>();
            if (entity == null || entity.Team != Team.Team1)
                continue;

            float distance = (entity.transform.position - pointerWorld).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEntity = entity;
            }
        }

        return closestEntity;
    }

    // ホバー中のタイルを切り替えます。
    private void SetHoveredTile(Tile tile, bool valid)
    {
        if (previousTile != null && previousTile != tile)
            previousTile.SetHighlight(false, false);

        if (tile != null)
            tile.SetHighlight(true, valid);

        previousTile = tile;
    }

    // 最後に光らせたタイルを元に戻します。
    private void ClearHoveredTile()
    {
        if (previousTile == null)
            return;

        previousTile.SetHighlight(false, false);
        previousTile = null;
    }

    // 必要な参照を取得します。
    private void EnsureReferences()
    {
        if (cam == null)
            cam = Camera.main;

        if (itemInstance == null)
            itemInstance = GetComponent<ItemInstance>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // マウス座標をワールド座標へ変換します。
    private Vector3 GetPointerWorldPosition()
    {
        EnsureReferences();
        if (cam == null)
            return transform.position;

        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 worldPosition = cam.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0f;
        return worldPosition;
    }
}
