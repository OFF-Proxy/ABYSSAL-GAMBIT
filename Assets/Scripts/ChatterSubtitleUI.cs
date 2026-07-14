using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// STORY v2: 戦闘を止めない短い字幕（節目チャッター）。章ボスが遠くから煽る声などを画面下に数秒だけ出す。
// クリック送り無し・入力を奪わない（GraphicRaycaster なし）。倍速/ポーズ非依存（SetUpdate(true)）。
public class ChatterSubtitleUI : MonoBehaviour
{
    public static ChatterSubtitleUI Instance { get; private set; }

    private bool isBuilt;
    private CanvasGroup group;
    private RectTransform card;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI bodyText;

    public static ChatterSubtitleUI EnsureExists()
    {
        if (Instance != null) return Instance;
        ChatterSubtitleUI existing = FindObjectOfType<ChatterSubtitleUI>(true);
        if (existing != null) { Instance = existing; existing.BuildIfNeeded(); return Instance; }

        GameObject root = new GameObject("ChatterSubtitleUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 23000; // 戦闘HUDより前面、警告/結果より背面。
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Instance = root.AddComponent<ChatterSubtitleUI>();
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

        GameObject c = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
        c.transform.SetParent(transform, false);
        card = c.GetComponent<RectTransform>();
        // 画面下やや上。盤面の邪魔をしない位置。
        card.anchorMin = new Vector2(0.5f, 0f); card.anchorMax = new Vector2(0.5f, 0f); card.pivot = new Vector2(0.5f, 0f);
        card.anchoredPosition = new Vector2(0f, 150f);
        card.sizeDelta = new Vector2(1280f, 150f);
        group = c.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false; group.interactable = false;

        // 半透明の暗い帯。
        GameObject band = new GameObject("Band", typeof(RectTransform), typeof(Image));
        band.transform.SetParent(card, false);
        RectTransform brt = band.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        Image bandImg = band.GetComponent<Image>();
        bandImg.color = new Color(0.04f, 0.05f, 0.08f, 0.74f);
        bandImg.raycastTarget = false;

        nameText = MakeLabel(card, "Name", new Vector2(0.04f, 0.56f), new Vector2(0.96f, 1f), 30f, FontStyles.Bold, new Color(1f, 0.82f, 0.4f), TextAlignmentOptions.Left);
        bodyText = MakeLabel(card, "Body", new Vector2(0.04f, 0f), new Vector2(0.96f, 0.58f), 34f, FontStyles.Normal, new Color(0.96f, 0.96f, 0.98f), TextAlignmentOptions.Left);

        isBuilt = true;
        gameObject.SetActive(true);
        c.SetActive(false);
    }

    private TextMeshProUGUI MakeLabel(Transform parent, string n, Vector2 aMin, Vector2 aMax, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.alignment = align; LocalizationManager.ApplyFont(t);
        t.fontSize = size; t.fontStyle = style; t.color = color; t.raycastTarget = false;
        t.outlineWidth = 0.18f; t.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        return t;
    }

    // 字幕を数秒だけ出して自動で消える（戦闘は止めない）。
    public void Show(string speaker, string line, float hold = 3.4f)
    {
        BuildIfNeeded();
        nameText.text = speaker ?? string.Empty;
        bodyText.text = line ?? string.Empty;
        LocalizationManager.ApplyFont(nameText);
        LocalizationManager.ApplyFont(bodyText);

        card.gameObject.SetActive(true);
        group.DOKill();
        group.alpha = 0f;
        DOTween.Sequence().SetUpdate(true)
            .Append(group.DOFade(1f, 0.25f))
            .AppendInterval(Mathf.Max(0.6f, hold))
            .Append(group.DOFade(0f, 0.4f))
            .OnComplete(() => { if (card != null) card.gameObject.SetActive(false); });
    }
}
