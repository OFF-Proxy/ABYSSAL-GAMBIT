using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// ユニットをマウスでドラッグ&ドロップできるようにするクラスです。
// 盤面への配置、ベンチへの移動、ショップへの売却をここからGameManagerへ依頼します。
public class Draggable : MonoBehaviour
{
    // ドロップ先をRaycastで探す時に使うレイヤーです。0ならUnity標準の判定レイヤーを使います。
    public LayerMask releaseMask;

    // ドラッグ中、マウス位置とユニット表示を少しずらしたい時の予備設定です。
    public Vector3 dragOffset = new Vector3(0, -0.4f, 0);

    // マウス座標をワールド座標に変換するためのカメラと、描画順を変えるためのSpriteRendererです。
    private Camera cam;
    private SpriteRenderer spriteRenderer;

    // ドラッグ開始前の位置・マウスとのずれ・描画順を保存し、失敗時に戻せるようにします。
    private Vector3 oldPosition;
    private Vector3 pointerOffset;
    private int oldSortingOrder;

    // 直前にハイライトしたタイルを覚え、次のタイルへ移った時に元へ戻します。
    private Tile previousTile = null;

    // 売却成功などで専用SEを鳴らした後、通常のドロップSEを重ねないためのフラグです。
    private bool releasePlayedSound;

    // クリックとドラッグを分けるための状態です。少し動かした時だけドラッグとして扱います。
    private bool pointerDown;
    private bool pointerMovedBeyondClick;
    private Vector3 mouseDownScreenPosition;
    private const float ClickDragThreshold = 8f;

    // 他のスクリプトから、今このユニットがドラッグ中かを確認できます。
    public bool IsDragging = false;

    // 現在ドラッグ中のユニット数（保留リロール等の判定に使います）。
    public static int ActiveDragCount;

    // オブジェクト生成直後に、必要な参照と旧イベント設定を整えます。
    private void Awake()
    {
        // 起動直後に必要な参照を取り、古いEventTrigger方式と重複しないよう無効化します。
        EnsureReferences();
        DisableLegacyEventTrigger();
    }

    // シーン開始後にも参照を取り直し、Camera.mainなどの遅い初期化に備えます。
    private void Start()
    {
        // Prefab生成直後はCamera.mainが取れない場合もあるため、Startでも再確認します。
        EnsureReferences();
    }

    // マウスを押してドラッグを開始する処理です。
    public void OnStartDrag()
    {
        if (IsDragging)
            return;

        // マウス左ボタン以外から呼ばれた時は何もしません。
        if (!Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0))
            return;

        EnsureReferences();

        //Debug.Log(this.name + " start drag");
        BaseEntity thisEntity = GetComponent<BaseEntity>();

        // ユニットではない、戦闘中で動かせない、描画情報がない場合はドラッグを開始しません。
        if (thisEntity == null || GameManager.Instance == null || !GameManager.Instance.CanDragEntity(thisEntity) || spriteRenderer == null)
            return;

        // 失敗した時に戻す位置と、マウスとの相対位置を保存します。
        oldPosition = this.transform.position;
        pointerOffset = transform.position - GetPointerWorldPosition();
        oldSortingOrder = spriteRenderer.sortingOrder;

        // ドラッグ中は他のユニットやタイルより前面に表示します。
        spriteRenderer.sortingOrder = 20;
        IsDragging = true;
        ActiveDragCount++;
        releasePlayedSound = false;
        ItemTooltipUI.Hide();
        UnitStatusPanelUI.Hide();
        AttackEffectPlayer.PlayUiSfx("drag_start");

        // ショップ側に売却額プレビューを出します。
        if (UIShop.Instance != null)
            UIShop.Instance.ShowSellPreview(thisEntity);
    }

    // マウスを動かしている間、ユニットを追従させる処理です。
    public void OnDragging()
    {
        if (!IsDragging)
            return;

        EnsureReferences();
        if (cam == null)
            return;

        //Debug.Log(this.name + " dragging");

        // マウス位置に合わせてユニットを移動します。zは2D表示なので0に固定します。
        Vector3 newPosition = GetPointerWorldPosition() + pointerOffset;
        newPosition.z = 0;
        this.transform.position = newPosition;

        BaseEntity thisEntity = GetComponent<BaseEntity>();
        Vector3 pointerWorldPosition = GetPointerWorldPosition();

        // まずベンチの上にいるかを判定します。ベンチはColliderではなく座標からスロットを探します。
        Tile benchTileUnder = GetBenchTileUnder(thisEntity, pointerWorldPosition, out int benchSlotIndex);
        if (benchTileUnder == null)
            benchTileUnder = GetBenchTileUnder(thisEntity, transform.position, out benchSlotIndex);

        if (benchTileUnder != null)
        {
            SetHoveredTile(benchTileUnder, GameManager.Instance.CanPlaceEntityOnBench(thisEntity, benchSlotIndex));
            return;
        }

        // 次に盤面タイルの上にいるかを判定します。
        Tile tileUnder = GetBoardTileAtWorldPosition(pointerWorldPosition);
        if (tileUnder == null)
            tileUnder = GetBoardTileAtWorldPosition(transform.position);

        if (tileUnder != null)
        {
            SetHoveredTile(tileUnder, IsValidReleaseTarget(tileUnder));
            return;
        }

        // ベンチでも盤面でもない場所では、前回のハイライトを消します。
        ClearHoveredTile();
    }

    // マウスを離した時に、売却・盤面配置・ベンチ配置のどれかを確定します。
    public void OnEndDrag()
    {
        if (!IsDragging)
            return;

       // Debug.Log(this.name + " end drag");

        bool released = TryRelease();
        if (!released)
        {
            //Nothing was found, return to original position.
            this.transform.position = oldPosition;
        }
        else if (!releasePlayedSound)
        {
            AttackEffectPlayer.PlayUiSfx("drag_drop");
        }

        // ドロップ後はタイルのハイライトを消します。
        if (previousTile != null)
        {
            previousTile.SetHighlight(false, false);
            previousTile = null;
        }

        // 描画順とショップの売却プレビューを元に戻します。
        spriteRenderer.sortingOrder = oldSortingOrder;
        if (UIShop.Instance != null)
            UIShop.Instance.HideSellPreview();

        IsDragging = false;
        ActiveDragCount = Mathf.Max(0, ActiveDragCount - 1);
        pointerDown = false;

        // ドラッグ中に保留されていたショップの無料リロールを、ここで安全に消化します。
        if (ActiveDragCount == 0 && UIShop.Instance != null)
            UIShop.Instance.ConsumePendingFreeReroll();
    }

    // UnityのOnMouseイベントから、クリック候補として押下位置を記録します。
    private void OnMouseDown()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        // UI（オーグメント/オプション等のモーダル）上のクリックは盤面に貫通させない。
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        pointerDown = true;
        pointerMovedBeyondClick = false;
        mouseDownScreenPosition = Input.mousePosition;
    }

    // UnityのOnMouseイベントから、一定距離以上動いた場合だけドラッグへ移行します。
    private void OnMouseDrag()
    {
        if (!IsDragging && pointerDown)
        {
            float movedPixels = Vector3.Distance(Input.mousePosition, mouseDownScreenPosition);
            if (movedPixels >= ClickDragThreshold)
            {
                pointerMovedBeyondClick = true;
                OnStartDrag();
            }
        }

        OnDragging();
    }

    // UnityのOnMouseイベントから、ドラッグ終了またはユニット選択クリックを処理します。
    private void OnMouseUp()
    {
        if (IsDragging)
        {
            OnEndDrag();
            return;
        }

        if (pointerDown && !pointerMovedBeyondClick)
        {
            // 念のため、離した瞬間にUI上ならユニット詳細を開かない。
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                pointerDown = false;
                return;
            }
            BaseEntity entity = GetComponent<BaseEntity>();
            if (entity != null)
                UnitStatusPanelUI.Show(entity);
        }

        pointerDown = false;
    }

    // ドロップ先を順番に試し、成功したらtrueを返します。
    private bool TryRelease()
    {
        //Released over something!
        BaseEntity thisEntity = GetComponent<BaseEntity>();

        // ショップ上なら売却を最優先します。
        if (TrySellToShop(thisEntity))
            return true;

        // マウス座標から盤面タイルを探して配置します。
        Tile t = GetBoardTileUnder();
        if (TryPlaceOnTile(thisEntity, t))
            return true;

        // マウス座標からベンチスロットを探して配置します。
        if (TryPlaceOnBench(thisEntity, GetPointerWorldPosition()))
            return true;

        // マウス座標で拾えない場合、ユニット本体の座標でもう一度試します。
        t = GetBoardTileAtWorldPosition(transform.position);
        if (TryPlaceOnTile(thisEntity, t))
            return true;

        if (TryPlaceOnBench(thisEntity, transform.position))
            return true;

        return false;
    }

    // ショップ上で離した場合、ユニットを売却します。
    private bool TrySellToShop(BaseEntity thisEntity)
    {
        if (thisEntity == null || UIShop.Instance == null || GameManager.Instance == null)
            return false;

        if (!UIShop.Instance.IsPointerOverShop(Input.mousePosition))
            return false;

        bool sold = GameManager.Instance.TrySellEntity(thisEntity);
        if (sold)
        {
            // 売却SEは専用音源 Assets/Resources/sfx/Sell.mp3 を使用（cue名 "Sell" → default解決で "sfx/Sell"）。
            AttackEffectPlayer.PlayUiSfx("Sell");
            releasePlayedSound = true;
        }

        return sold;
    }

    // 指定された盤面タイルへユニットを置けるか試します。
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
                // 実際の配置可否、入れ替え、盤面上限などはGameManager側でまとめて判定します。
                if (GameManager.Instance.TryPlaceEntityManually(thisEntity, candidateNode))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 外部から「今マウス下にある盤面タイル」を取りたい時の公開メソッドです。
    public Tile GetTileUnder()
    {
        return GetBoardTileUnder();
    }

    // マウス座標から盤面タイルを探します。
    private Tile GetBoardTileUnder()
    {
        EnsureReferences();
        if (cam == null)
            return null;

        Vector3 worldPosition = GetPointerWorldPosition();
        return GetBoardTileAtWorldPosition(worldPosition);
    }

    // ワールド座標から盤面タイルを探します。
    private Tile GetBoardTileAtWorldPosition(Vector3 worldPosition)
    {
        if (GridManager.Instance == null)
            return null;

        // まずGridManagerの座標計算で、最も近いタイルを探します。
        Tile closestTile = GridManager.Instance.GetTileAtWorldPosition(worldPosition);
        if (closestTile != null)
            return closestTile;

        // 座標計算で見つからない場合に備えて、Colliderでも探します。
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

    // 指定座標にあるベンチスロットへユニットを置けるか試します。
    private bool TryPlaceOnBench(BaseEntity thisEntity, Vector3 worldPosition)
    {
        if (thisEntity == null || GameManager.Instance == null)
            return false;

        int slotIndex = GameManager.Instance.GetBenchSlotAtWorldPosition(thisEntity.Team, worldPosition);
        return GameManager.Instance.TryPlaceEntityOnBench(thisEntity, slotIndex);
    }

    // 指定座標にあるベンチタイルを取得します。ホバー表示に使います。
    private Tile GetBenchTileUnder(BaseEntity thisEntity, Vector3 worldPosition, out int slotIndex)
    {
        slotIndex = -1;

        if (thisEntity == null || GameManager.Instance == null)
            return null;

        slotIndex = GameManager.Instance.GetBenchSlotAtWorldPosition(thisEntity.Team, worldPosition);
        return GameManager.Instance.GetBenchTileAtSlot(thisEntity.Team, slotIndex);
    }

    // 指定タイルが、今ドラッグ中のユニットを置ける場所か確認します。
    private bool IsValidReleaseTarget(Tile tile)
    {
        if (GameManager.Instance == null || GridManager.Instance == null)
            return false;

        BaseEntity thisEntity = GetComponent<BaseEntity>();
        Node candidateNode = GridManager.Instance.GetNodeForTile(tile);
        return GameManager.Instance.CanPlaceEntityManually(thisEntity, candidateNode);
    }

    // 今ホバーしているタイルを強調表示します。
    private void SetHoveredTile(Tile tile, bool valid)
    {
        if (previousTile != null && previousTile != tile)
            previousTile.SetHighlight(false, false);

        if (tile != null)
            tile.SetHighlight(true, valid);

        previousTile = tile;
    }

    // 以前のホバー表示を消します。
    private void ClearHoveredTile()
    {
        if (previousTile == null)
            return;

        previousTile.SetHighlight(false, false);
        previousTile = null;
    }

    // カメラやSpriteRendererなど、このスクリプトが動くために必要な参照を補完します。
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

    // マウスの画面座標を、2Dゲーム内のワールド座標へ変換します。
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

    // 以前使っていたEventTriggerが残っているとOnMouse系と二重に動くため、見つけたら無効化します。
    private void DisableLegacyEventTrigger()
    {
        EventTrigger eventTrigger = GetComponent<EventTrigger>();
        if (eventTrigger != null)
            eventTrigger.enabled = false;
    }
}
