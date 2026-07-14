using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// 章クリア時の演出バナー。
// 初回クリア=「ボス解放！」、2回目以降=「強化！ Lv.X」（アフィニティ育成）をボスアイコン付きで表示する。
// リザルトパネル(25500)より前面(25600)に出し、数秒で自動フェード or クリックで早送り。
public class BossUnlockBannerUI : MonoBehaviour
{
    private const int SortingOrder = 25600;
    private const float HoldSeconds = 2.6f;

    public static BossUnlockBannerUI Instance { get; private set; }

    private GameObject root;
    private CanvasGroup group;
    private Image dim;
    private Image glow;
    private Image icon;
    private TextMeshProUGUI headline;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI subText;
    private float closeAt;
    private bool active;

    public static BossUnlockBannerUI EnsureExists()
    {
        if (Instance != null) return Instance;
        BossUnlockBannerUI existing = FindObjectOfType<BossUnlockBannerUI>(true);
        if (existing != null) { Instance = existing; existing.Build(); return existing; }

        GameObject go = new GameObject("BossUnlockBannerUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(BossUnlockBannerUI));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Instance = go.GetComponent<BossUnlockBannerUI>();
        Instance.Build();
        return Instance;
    }

    private void Awake()
    {
        LocalizationManager.EnsureExists();
        Instance = this;
        Build();
        if (root != null) root.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Build()
    {
        if (root != null) return;

        root = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        root.transform.SetParent(transform, false);
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
        group = root.GetComponent<CanvasGroup>();
        dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0.01f, 0.03f, 0.55f);
        root.GetComponent<Button>().transition = Selectable.Transition.None;
        root.GetComponent<Button>().onClick.AddListener(Dismiss); // クリックで早送り

        // グロー（アイコン背後の光）
        glow = MakeImage("Glow", root.transform, new Vector2(0.5f, 0.56f), new Vector2(360f, 360f));
        glow.raycastTarget = false;
        Sprite glowSprite = Resources.Load<Sprite>("UI/Augment/rarity_gold");
        if (glowSprite != null) glow.sprite = glowSprite;
        glow.color = new Color(1f, 0.85f, 0.4f, 0.0f);

        // ボスアイコン
        icon = MakeImage("Icon", root.transform, new Vector2(0.5f, 0.56f), new Vector2(240f, 240f));
        icon.preserveAspect = true; icon.raycastTarget = false;

        headline = MakeText("Headline", root.transform, new Vector2(0.5f, 0.8f), new Vector2(900f, 70f), 52f, FontStyles.Bold, new Color(1f, 0.92f, 0.5f));
        nameText = MakeText("Name", root.transform, new Vector2(0.5f, 0.34f), new Vector2(900f, 48f), 34f, FontStyles.Bold, new Color(1f, 0.98f, 0.92f));
        subText = MakeText("Sub", root.transform, new Vector2(0.5f, 0.26f), new Vector2(900f, 36f), 22f, FontStyles.Normal, new Color(0.85f, 0.92f, 1f));
    }

    public void Show(Sprite bossIcon, string bossName, bool firstUnlock, int affinityLevel)
    {
        Build();
        bool ja = LocalizationManager.IsJapanese;

        icon.sprite = bossIcon;
        icon.color = bossIcon != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);

        LocalizationManager.ApplyFont(headline);
        LocalizationManager.ApplyFont(nameText);
        LocalizationManager.ApplyFont(subText);

        if (firstUnlock)
        {
            headline.text = ja ? "★ ボス解放！" : "★ BOSS UNLOCKED!";
            headline.color = new Color(1f, 0.9f, 0.45f);
            subText.text = ja ? "新たなボスが仲間に加わった！" : "A new boss has joined your roster!";
            glow.color = new Color(1f, 0.85f, 0.4f, 0f);
        }
        else
        {
            headline.text = ja ? "★ ボス強化！" : "★ BOSS EMPOWERED!";
            headline.color = new Color(0.55f, 0.85f, 1f);
            int bonusPct = Mathf.Min(Mathf.Max(0, affinityLevel - 1), 10) * 6;
            subText.text = ja ? $"アフィニティ Lv.{affinityLevel}（ステータス +{bonusPct}%）" : $"Affinity Lv.{affinityLevel} (Stats +{bonusPct}%)";
            glow.color = new Color(0.4f, 0.8f, 1f, 0f);
        }
        nameText.text = bossName ?? string.Empty;

        root.SetActive(true);
        AttackEffectPlayer.PlayUiSfx("unit_buy");
        PlayAnim();
        active = true;
        closeAt = Time.unscaledTime + HoldSeconds;
    }

    private void PlayAnim()
    {
        group.alpha = 0f;
        group.DOKill();
        group.DOFade(1f, 0.2f).SetUpdate(true);

        RectTransform ir = icon.rectTransform;
        ir.DOKill();
        ir.localScale = Vector3.one * 0.6f;
        ir.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);

        RectTransform gr = glow.rectTransform;
        gr.DOKill();
        gr.localScale = Vector3.one * 0.8f;
        gr.DOScale(1.12f, 0.9f).SetEase(Ease.OutQuad).SetUpdate(true).SetLoops(-1, LoopType.Yoyo);
        float ga = glow.color.a; Color gc = glow.color; gc.a = 0f; glow.color = gc;
        glow.DOFade(0.7f, 0.4f).SetUpdate(true);

        RectTransform hr = headline.rectTransform;
        hr.DOKill();
        Vector2 hp = hr.anchoredPosition;
        hr.anchoredPosition = hp + new Vector2(0f, 26f);
        hr.DOAnchorPos(hp, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void Update()
    {
        if (active && Time.unscaledTime >= closeAt)
            Dismiss();
    }

    private void Dismiss()
    {
        if (!active) return;
        active = false;
        if (glow != null) glow.rectTransform.DOKill();
        if (group != null)
        {
            group.DOKill();
            group.DOFade(0f, 0.25f).SetUpdate(true).OnComplete(() => { if (root != null) root.SetActive(false); });
        }
        else if (root != null) root.SetActive(false);
    }

    private Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = size;
        return go.GetComponent<Image>();
    }

    private TextMeshProUGUI MakeText(string name, Transform parent, Vector2 anchor, Vector2 size, float fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = size;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(t);
        t.fontSize = fontSize; t.fontStyle = style; t.color = color;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.outlineWidth = 0.18f; t.outlineColor = Color.black;
        return t;
    }
}
