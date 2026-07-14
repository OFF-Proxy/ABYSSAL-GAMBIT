using TMPro;
using UnityEngine;
using UnityEngine.UI;

// R3-chest-room: チェスト報酬ラウンドの残り時間バナー。画面上部中央に「宝箱を開けろ！ 残り NN 秒」を表示。
// チェスト部屋がアクティブな間だけ出す（GameManager.IsChestRoomActive を見て自動表示/非表示）。
public class ChestRoomHudUI : MonoBehaviour
{
    public static ChestRoomHudUI Instance { get; private set; }

    private bool isBuilt;
    private GameObject panel;
    private TextMeshProUGUI label;

    public static ChestRoomHudUI EnsureExists()
    {
        if (Instance != null) return Instance;
        ChestRoomHudUI existing = FindObjectOfType<ChestRoomHudUI>(true);
        if (existing != null) { Instance = existing; Instance.BuildIfNeeded(); return Instance; }

        GameObject root = new GameObject("ChestRoomHudUI",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ChestRoomHudUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 13500; // HUD層（コアHUD13000より少し上、結果/報酬25000+より下）。
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.GetComponent<GraphicRaycaster>().enabled = false;

        Instance = root.GetComponent<ChestRoomHudUI>();
        Instance.BuildIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildIfNeeded();
    }

    private void BuildIfNeeded()
    {
        if (isBuilt) return;

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 1f); pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 1f);
        pr.anchoredPosition = new Vector2(0f, -86f);
        pr.sizeDelta = new Vector2(520f, 60f);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.9f);

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(panel.transform, false);
        RectTransform lr = labelObj.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        label = labelObj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(label);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 28f;
        label.color = new Color(1f, 0.88f, 0.45f);
        label.outlineWidth = 0.18f; label.outlineColor = Color.black;
        label.raycastTarget = false;

        isBuilt = true;
        panel.SetActive(false);
    }

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        bool show = gm != null && gm.IsChestRoomActive;
        if (panel != null && panel.activeSelf != show) panel.SetActive(show);
        if (!show || label == null) return;

        int sec = Mathf.CeilToInt(gm.ChestRoomSecondsLeft);
        bool ja = LocalizationManager.IsJapanese;
        LocalizationManager.ApplyFont(label);
        label.text = ja ? $"宝箱を開けろ！  残り {sec} 秒" : $"Crack the chests!  {sec}s left";
        // 残り少ないと赤く。
        label.color = sec <= 5 ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.88f, 0.45f);
    }
}
