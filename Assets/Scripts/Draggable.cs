using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour
{
    public LayerMask releaseMask;
    public Vector3 dragOffset = new Vector3(0, -0.4f, 0);
    
    private Camera cam;
    private SpriteRenderer spriteRenderer;
    
    private Vector3 oldPosition;
    private Vector3 pointerOffset;
    private int oldSortingOrder;
    private Tile previousTile = null;
    
    public bool IsDragging = false;
    
    private void Awake()
    {
        EnsureReferences();
        DisableLegacyEventTrigger();
    }

    private void Start()
    {
        EnsureReferences();
    }

    public void OnStartDrag()
    {
        if (IsDragging)
            return;

        if (!Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0))
            return;

        EnsureReferences();

        //Debug.Log(this.name + " start drag");
        BaseEntity thisEntity = GetComponent<BaseEntity>();
        if (thisEntity == null || GameManager.Instance == null || !GameManager.Instance.CanDragEntity(thisEntity) || spriteRenderer == null)
            return;

        oldPosition = this.transform.position;
        pointerOffset = transform.position - GetPointerWorldPosition();
        oldSortingOrder = spriteRenderer.sortingOrder;

        spriteRenderer.sortingOrder = 20;
        IsDragging = true;

        if (UIShop.Instance != null)
            UIShop.Instance.ShowSellPreview(thisEntity);
    }

    public void OnDragging()
    {
        if (!IsDragging)
            return;

        EnsureReferences();
        if (cam == null)
            return;
        
        //Debug.Log(this.name + " dragging");

        Vector3 newPosition = GetPointerWorldPosition() + pointerOffset;
        newPosition.z = 0;
        this.transform.position = newPosition;

        BaseEntity thisEntity = GetComponent<BaseEntity>();
        Vector3 pointerWorldPosition = GetPointerWorldPosition();
        Tile benchTileUnder = GetBenchTileUnder(thisEntity, pointerWorldPosition, out int benchSlotIndex);
        if (benchTileUnder == null)
            benchTileUnder = GetBenchTileUnder(thisEntity, transform.position, out benchSlotIndex);

        if (benchTileUnder != null)
        {
            SetHoveredTile(benchTileUnder, GameManager.Instance.CanPlaceEntityOnBench(thisEntity, benchSlotIndex));
            return;
        }

        Tile tileUnder = GetBoardTileAtWorldPosition(pointerWorldPosition);
        if (tileUnder == null)
            tileUnder = GetBoardTileAtWorldPosition(transform.position);

        if (tileUnder != null)
        {
            SetHoveredTile(tileUnder, IsValidReleaseTarget(tileUnder));
            return;
        }

        ClearHoveredTile();
    }

    public void OnEndDrag()
    {
        if (!IsDragging)
            return;
        
       // Debug.Log(this.name + " end drag");

        if (!TryRelease())
        {
            //Nothing was found, return to original position.
            this.transform.position = oldPosition;
        }

        if (previousTile != null)
        {
            previousTile.SetHighlight(false, false);
            previousTile = null;
        }

        spriteRenderer.sortingOrder = oldSortingOrder;
        if (UIShop.Instance != null)
            UIShop.Instance.HideSellPreview();

        IsDragging = false;
    }

    private void OnMouseDown()
    {
        OnStartDrag();
    }

    private void OnMouseDrag()
    {
        OnDragging();
    }

    private void OnMouseUp()
    {
        OnEndDrag();
    }

    private bool TryRelease()
    {
        //Released over something!
        BaseEntity thisEntity = GetComponent<BaseEntity>();
        if (TrySellToShop(thisEntity))
            return true;

        Tile t = GetBoardTileUnder();
        if (TryPlaceOnTile(thisEntity, t))
            return true;

        if (TryPlaceOnBench(thisEntity, GetPointerWorldPosition()))
            return true;

        t = GetBoardTileAtWorldPosition(transform.position);
        if (TryPlaceOnTile(thisEntity, t))
            return true;

        if (TryPlaceOnBench(thisEntity, transform.position))
            return true;

        return false;
    }

    private bool TrySellToShop(BaseEntity thisEntity)
    {
        if (thisEntity == null || UIShop.Instance == null || GameManager.Instance == null)
            return false;

        if (!UIShop.Instance.IsPointerOverShop(Input.mousePosition))
            return false;

        return GameManager.Instance.TrySellEntity(thisEntity);
    }

    private bool TryPlaceOnTile(BaseEntity thisEntity, Tile t)
    {
        if (thisEntity == null || t == null || GameManager.Instance == null || GridManager.Instance == null)
            return false;

        if (t != null)
        {
            //It's a tile!
            Node candidateNode = GridManager.Instance.GetNodeForTile(t);
            if (candidateNode != null && thisEntity != null)
            {
                if (GameManager.Instance.TryPlaceEntityManually(thisEntity, candidateNode))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public Tile GetTileUnder()
    {
        return GetBoardTileUnder();
    }

    private Tile GetBoardTileUnder()
    {
        EnsureReferences();
        if (cam == null)
            return null;

        Vector3 worldPosition = GetPointerWorldPosition();
        return GetBoardTileAtWorldPosition(worldPosition);
    }

    private Tile GetBoardTileAtWorldPosition(Vector3 worldPosition)
    {
        if (GridManager.Instance == null)
            return null;

        Tile closestTile = GridManager.Instance.GetTileAtWorldPosition(worldPosition);
        if (closestTile != null)
            return closestTile;

        int mask = releaseMask.value == 0 ? Physics2D.DefaultRaycastLayers : releaseMask.value;
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition, mask);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            Tile t = hit.GetComponent<Tile>();
            if (t != null && GridManager.Instance.GetNodeForTile(t) != null)
                return t;
        }

        return null;
    }

    private bool TryPlaceOnBench(BaseEntity thisEntity, Vector3 worldPosition)
    {
        if (thisEntity == null || GameManager.Instance == null)
            return false;

        int slotIndex = GameManager.Instance.GetBenchSlotAtWorldPosition(thisEntity.Team, worldPosition);
        return GameManager.Instance.TryPlaceEntityOnBench(thisEntity, slotIndex);
    }

    private Tile GetBenchTileUnder(BaseEntity thisEntity, Vector3 worldPosition, out int slotIndex)
    {
        slotIndex = -1;

        if (thisEntity == null || GameManager.Instance == null)
            return null;

        slotIndex = GameManager.Instance.GetBenchSlotAtWorldPosition(thisEntity.Team, worldPosition);
        return GameManager.Instance.GetBenchTileAtSlot(thisEntity.Team, slotIndex);
    }

    private bool IsValidReleaseTarget(Tile tile)
    {
        if (GameManager.Instance == null || GridManager.Instance == null)
            return false;

        BaseEntity thisEntity = GetComponent<BaseEntity>();
        Node candidateNode = GridManager.Instance.GetNodeForTile(tile);
        return GameManager.Instance.CanPlaceEntityManually(thisEntity, candidateNode);
    }

    private void SetHoveredTile(Tile tile, bool valid)
    {
        if (previousTile != null && previousTile != tile)
            previousTile.SetHighlight(false, false);

        if (tile != null)
            tile.SetHighlight(true, valid);

        previousTile = tile;
    }

    private void ClearHoveredTile()
    {
        if (previousTile == null)
            return;

        previousTile.SetHighlight(false, false);
        previousTile = null;
    }

    private void EnsureReferences()
    {
        if (cam == null)
            cam = Camera.main;

        if (spriteRenderer == null || !spriteRenderer.enabled)
        {
            BaseEntity entity = GetComponent<BaseEntity>();
            if (entity != null)
                spriteRenderer = entity.spriteRender;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

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

    private void DisableLegacyEventTrigger()
    {
        EventTrigger eventTrigger = GetComponent<EventTrigger>();
        if (eventTrigger != null)
            eventTrigger.enabled = false;
    }
}
