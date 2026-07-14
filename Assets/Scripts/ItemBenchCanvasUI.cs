using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// アイテムベンチを画面左側に固定表示するCanvas UIです。
// 既存のItemInstanceは所持データとして残し、見た目と入力だけをUIで扱います。
public class ItemBenchCanvasUI : MonoBehaviour
{
    private const int CanvasSortingOrder = 13040; // 16bit short上限(32767)内。
    private const float SynergySideWidth = 142f;
    private const float SlotSize = 78f;
    private const float SlotSpacing = 80f;
    private const float ColumnSpacing = 82f;
    private const float TopOffset = 40f;
    private const float ToggleButtonSize = 42f;

    private static ItemBenchCanvasUI instance;
    private static Sprite slotFrameSprite;
    private static Sprite toggleButtonSprite;
    private static Sprite highlightSprite;

    private Canvas canvas;
    private RectTransform panelRect;
    private RectTransform slotRoot;
    private RectTransform toggleRect;
    private Image toggleButtonImage;
    private Image dragIconImage;
    private RectTransform dragIconRect;
    private GameManager gameManager;
    private bool expanded;
    private readonly List<ItemBenchCanvasSlot> slots = new List<ItemBenchCanvasSlot>();

    // GameManagerから呼び、UIがなければ作って返します。
    public static ItemBenchCanvasUI EnsureExists(GameManager manager)
    {
        if (instance != null)
        {
            instance.Attach(manager);
            return instance;
        }

        ItemBenchCanvasUI existing = FindObjectOfType<ItemBenchCanvasUI>(true);
        if (existing != null)
        {
            instance = existing;
            instance.Attach(manager);
            return instance;
        }

        GameObject uiObject = new GameObject("ItemBenchCanvasUI", typeof(RectTransform));
        instance = uiObject.AddComponent<ItemBenchCanvasUI>();
        instance.Attach(manager);
        return instance;
    }

    // 他処理から、現在のアイテム一覧に合わせて表示を更新します。
    public static void RefreshNow()
    {
        if (instance != null)
            instance.Refresh();
    }

    // アイテムベンチUIの基準位置に合わせて、シナジーUIを左側へ寄せたい時のX座標です。
    public static float SynergyPanelLeftX => 8f;

    // GameManager参照を差し替え、初期UIを構築します。
    private void Attach(GameManager manager)
    {
        gameManager = manager != null ? manager : gameManager;
        if (canvas == null)
            BuildUi();

        Refresh();
    }

    private void Update()
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].Tick(Time.deltaTime);

        if (dragIconRect != null)
            dragIconRect.position = Input.mousePosition;
    }

    // Canvas、左パネル、スロット、展開ボタンを作ります。
    private void BuildUi()
    {
        EnsureEventSystem();
        LoadSprites();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        gameObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -TopOffset);
        panelRect.sizeDelta = new Vector2(SynergySideWidth + SlotSize + 18f, SlotSpacing * 8f + 8f);

        Image panelImage = panelObject.GetComponent<Image>();
        // Duelyst流の左ドック地：明るい枠素材ではなく「暗い半透明の角丸」で統一（rgba(1,0,37,0.75)相当）。
        panelImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.02f, 0.01f, 0.16f, 0.6f);
        panelImage.raycastTarget = false;

        GameObject slotRootObject = new GameObject("Slots", typeof(RectTransform));
        slotRootObject.transform.SetParent(panelRect, false);
        slotRoot = slotRootObject.GetComponent<RectTransform>();
        slotRoot.anchorMin = new Vector2(0f, 1f);
        slotRoot.anchorMax = new Vector2(0f, 1f);
        slotRoot.pivot = new Vector2(0f, 1f);
        slotRoot.anchoredPosition = new Vector2(SynergySideWidth + 6f, -4f);
        slotRoot.sizeDelta = new Vector2(ColumnSpacing * 2f, SlotSpacing * 8f);

        CreateSlots();
        CreateToggleButton();
    }

    // 所持スロット数に合わせてUIを再描画します。
    public void Refresh()
    {
        if (gameManager == null || panelRect == null)
            return;

        int rows = Mathf.Max(1, gameManager.ItemBenchRowsForUi);
        int columns = Mathf.Max(1, gameManager.ItemBenchColumnsForUi);
        bool hasHiddenItems = gameManager.HasItemBenchItemsBeyondColumn(0);
        int visibleColumns = expanded && columns > 1 ? Mathf.Min(columns, 2) : 1;

        panelRect.sizeDelta = new Vector2(
            SynergySideWidth + visibleColumns * ColumnSpacing + 16f,
            rows * SlotSpacing + 8f);

        EnsureSlotCount(rows * Mathf.Min(columns, 2));
        for (int column = 0; column < Mathf.Min(columns, 2); column++)
        {
            for (int row = 0; row < rows; row++)
            {
                int listIndex = column * rows + row;
                bool visible = column < visibleColumns;
                ItemBenchCanvasSlot slot = slots[listIndex];
                slot.gameObject.SetActive(visible);
                slot.Rect.anchoredPosition = new Vector2(column * ColumnSpacing, -row * SlotSpacing);
                slot.Bind(this, gameManager, listIndex, gameManager.GetItemBenchItemAtSlotForUi(listIndex));
            }
        }

        for (int i = rows * Mathf.Min(columns, 2); i < slots.Count; i++)
            slots[i].gameObject.SetActive(false);

        bool showToggle = columns > 1 && (hasHiddenItems || expanded);
        toggleRect.gameObject.SetActive(showToggle);
        if (showToggle)
        {
            toggleRect.anchoredPosition = new Vector2(
                SynergySideWidth + visibleColumns * ColumnSpacing + ToggleButtonSize * 0.5f + 4f,
                -8f);

            if (toggleButtonImage != null)
                toggleButtonImage.rectTransform.localScale = expanded ? Vector3.one : new Vector3(-1f, 1f, 1f);
        }
    }

    // ドラッグ開始時に、マウスについてくるアイコンを作ります。
    public void BeginDragIcon(ItemInstance itemInstance)
    {
        if (itemInstance == null || itemInstance.Data == null)
            return;

        ItemTooltipUI.Hide();
        EnsureDragIcon();
        dragIconImage.sprite = itemInstance.Data.Icon;
        dragIconImage.enabled = dragIconImage.sprite != null;
        dragIconRect.position = Input.mousePosition;
        dragIconRect.gameObject.SetActive(true);
        AttackEffectPlayer.PlayUiSfx("drag_start");
    }

    // UIスロットかワールド上の味方ユニットへアイテムを落とします。
    public void EndDrag(ItemInstance itemInstance)
    {
        if (dragIconRect != null)
            dragIconRect.gameObject.SetActive(false);

        if (itemInstance == null || gameManager == null)
            return;

        bool released = TryDropOnCanvasSlot(itemInstance) || TryDropOnWorldEntity(itemInstance);
        if (released)
            AttackEffectPlayer.PlayUiSfx("drag_drop");

        Refresh();
    }

    // スロットクリック時に、アイテム効果を表示します。
    public void ShowItemTooltip(ItemInstance itemInstance)
    {
        if (itemInstance != null && itemInstance.Data != null)
            ItemTooltipUI.Show(itemInstance.Data, Input.mousePosition);
    }

    // 画面上の別スロットへ落とされたかを調べます。
    private bool TryDropOnCanvasSlot(ItemInstance itemInstance)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        for (int i = 0; i < results.Count; i++)
        {
            ItemBenchCanvasSlot slot = results[i].gameObject.GetComponentInParent<ItemBenchCanvasSlot>();
            if (slot == null || !slot.gameObject.activeInHierarchy)
                continue;

            return gameManager.TryPlaceItemOnBench(itemInstance, slot.SlotIndex);
        }

        return false;
    }

    // ワールド上の味方ユニットへ落とされたかを調べます。
    private bool TryDropOnWorldEntity(ItemInstance itemInstance)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = Mathf.Abs(cam.transform.position.z);
        Vector3 worldPosition = cam.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0f;

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        BaseEntity closestEntity = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            BaseEntity entity = hits[i] != null ? hits[i].GetComponentInParent<BaseEntity>() : null;
            if (entity == null || entity.Team != Team.Team1)
                continue;

            float distance = (entity.transform.position - worldPosition).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEntity = entity;
            }
        }

        return closestEntity != null && gameManager.TryEquipItemToEntity(itemInstance, closestEntity);
    }

    private void CreateSlots()
    {
        int initialSlotCount = Mathf.Max(1, gameManager != null ? gameManager.ItemBenchRowsForUi * Mathf.Min(gameManager.ItemBenchColumnsForUi, 2) : 16);
        EnsureSlotCount(initialSlotCount);
    }

    private void EnsureSlotCount(int count)
    {
        while (slots.Count < count)
        {
            GameObject slotObject = new GameObject($"ItemBenchSlot_{slots.Count}", typeof(RectTransform), typeof(Image), typeof(ItemBenchCanvasSlot));
            slotObject.transform.SetParent(slotRoot, false);

            ItemBenchCanvasSlot slot = slotObject.GetComponent<ItemBenchCanvasSlot>();
            slot.Build(slotFrameSprite, highlightSprite, SlotSize);
            slots.Add(slot);
        }
    }

    private void CreateToggleButton()
    {
        GameObject buttonObject = new GameObject("ExpandButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panelRect, false);
        toggleRect = buttonObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 1f);
        toggleRect.anchorMax = new Vector2(0f, 1f);
        toggleRect.pivot = new Vector2(0.5f, 1f);
        toggleRect.sizeDelta = new Vector2(ToggleButtonSize, ToggleButtonSize);

        toggleButtonImage = buttonObject.GetComponent<Image>();
        toggleButtonImage.sprite = toggleButtonSprite;
        toggleButtonImage.color = Color.white;
        toggleButtonImage.preserveAspect = true;
        toggleButtonImage.type = Image.Type.Simple;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            expanded = !expanded;
            AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
            Refresh();
        });
    }

    private void EnsureDragIcon()
    {
        if (dragIconRect != null)
            return;

        GameObject dragObject = new GameObject("DragIcon", typeof(RectTransform), typeof(Image));
        dragObject.transform.SetParent(transform, false);
        dragIconRect = dragObject.GetComponent<RectTransform>();
        dragIconRect.sizeDelta = new Vector2(SlotSize, SlotSize);

        dragIconImage = dragObject.GetComponent<Image>();
        dragIconImage.preserveAspect = true;
        dragIconImage.raycastTarget = false;
        dragObject.SetActive(false);
    }

    private static void LoadSprites()
    {
        slotFrameSprite = LoadUiSprite("UI/ItemBench/artifact_frame");
        toggleButtonSprite = LoadUiSprite("UI/ItemBench/item_bench_toggle_arrow");
        highlightSprite = LoadUiSprite("UI/ItemBench/highlight_white");
    }

    private static Sprite LoadUiSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}

// Canvas上のアイテムスロット1枠です。クリック、ドラッグ開始、ドラッグ終了を受け持ちます。
public class ItemBenchCanvasSlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int SlotIndex { get; private set; }
    public RectTransform Rect { get; private set; }

    private ItemBenchCanvasUI owner;
    private GameManager gameManager;
    private Image backgroundImage;
    private Image frameImage;
    private Image highlightImage;
    private Image iconImage;
    private ItemInstance itemInstance;
    private float animationTimer;
    private int animationFrame;
    private bool dragging;
    private const float HexIconSize = 72f;
    private static readonly Vector2 HexIconTopLeft = new Vector2(-2f, -3f);
    private const float IconAnimationFps = 12f;

    public void Build(Sprite frameSprite, Sprite highlightSprite, float slotSize)
    {
        Rect = GetComponent<RectTransform>();
        Rect.anchorMin = new Vector2(0f, 1f);
        Rect.anchorMax = new Vector2(0f, 1f);
        Rect.pivot = new Vector2(0f, 1f);
        Rect.sizeDelta = new Vector2(slotSize, slotSize);

        backgroundImage = GetComponent<Image>();
        backgroundImage.sprite = null;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = Color.clear;
        backgroundImage.raycastTarget = true;

        frameImage = CreateImage("Frame", transform, Vector2.zero, new Vector2(slotSize, slotSize));
        frameImage.sprite = frameSprite;
        frameImage.preserveAspect = true;
        frameImage.color = new Color(0.95f, 1f, 1f, 0.95f);

        this.highlightImage = CreateImage("Highlight", transform, Vector2.zero, new Vector2(slotSize, slotSize));
        this.highlightImage.sprite = highlightSprite;
        this.highlightImage.color = new Color(0.2f, 1f, 1f, 0.22f);
        this.highlightImage.gameObject.SetActive(false);

        iconImage = CreateImage("Icon", transform, HexIconTopLeft, new Vector2(HexIconSize, HexIconSize));
        iconImage.preserveAspect = true;
    }

    public void Bind(ItemBenchCanvasUI owner, GameManager gameManager, int slotIndex, ItemInstance itemInstance)
    {
        this.owner = owner;
        this.gameManager = gameManager;
        SlotIndex = slotIndex;
        this.itemInstance = itemInstance;
        animationTimer = 0f;
        animationFrame = 0;

        ItemData itemData = itemInstance != null ? itemInstance.Data : null;
        iconImage.enabled = itemData != null && itemData.Icon != null;
        iconImage.sprite = itemData != null ? itemData.Icon : null;
        backgroundImage.color = Color.clear;
    }

    public void Tick(float deltaTime)
    {
        if (itemInstance == null || itemInstance.Data == null || iconImage == null)
            return;

        IReadOnlyList<Sprite> frames = itemInstance.Data.IconFrames;
        if (frames == null || frames.Count <= 1)
            return;

        animationTimer += deltaTime;
        float frameInterval = 1f / IconAnimationFps;
        while (animationTimer >= frameInterval)
        {
            animationTimer -= frameInterval;
            animationFrame = (animationFrame + 1) % frames.Count;
            iconImage.sprite = frames[animationFrame];
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!dragging && itemInstance != null)
            owner.ShowItemTooltip(itemInstance);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemInstance == null)
            return;

        dragging = true;
        highlightImage.gameObject.SetActive(true);
        owner.BeginDragIcon(itemInstance);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;

        dragging = false;
        highlightImage.gameObject.SetActive(false);
        owner.EndDrag(itemInstance);
    }

    private Image CreateImage(string objectName, Transform parent, Vector2 topLeft, Vector2 size)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}
