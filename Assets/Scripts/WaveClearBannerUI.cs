using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ウェーブ勝利時に画面中央へ出す祝福バナー。「WAVE CLEAR!」＋称賛メッセージを
// スケールポップ＋フェードで数秒表示する。GameManager が勝利演出の間に呼ぶ。
public class WaveClearBannerUI : MonoBehaviour
{
    public static WaveClearBannerUI Instance { get; private set; }

    private bool isBuilt;
    private CanvasGroup group;
    private RectTransform card;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI praiseText;

    private static readonly string[] PraiseJa = { "お見事！", "ナイス采配！", "完璧だ！", "その調子！", "見事な勝利！", "圧巻！" };
    private static readonly string[] PraiseEn = { "Well done!", "Nice command!", "Flawless!", "Keep it up!", "Great victory!", "Superb!" };

    public static WaveClearBannerUI EnsureExists()
    {
        if (Instance != null) return Instance;
        WaveClearBannerUI existing = FindObjectOfType<WaveClearBannerUI>(true);
        if (existing != null) { Instance = existing; existing.BuildIfNeeded(); return Instance; }

        GameObject root = new GameObject("WaveClearBannerUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(WaveClearBannerUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 24000; // HUD より前面、結果/報酬パネル(25000+)より下。
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.GetComponent<GraphicRaycaster>().enabled = false; // クリックは透過（盤面操作の邪魔をしない）

        Instance = root.GetComponent<WaveClearBannerUI>();
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
        card.anchorMin = new Vector2(0.5f, 0.5f); card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(0f, 120f);
        card.sizeDelta = new Vector2(720f, 180f);
        group = c.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        titleText = MakeLabel(card, "Title", "WAVE CLEAR!", new Vector2(0f, 0.42f), new Vector2(1f, 1f), 60f, FontStyles.Bold, new Color(1f, 0.9f, 0.45f));
        titleText.outlineWidth = 0.22f; titleText.outlineColor = new Color(0.1f, 0.06f, 0f, 1f);
        praiseText = MakeLabel(card, "Praise", "", new Vector2(0f, 0f), new Vector2(1f, 0.42f), 30f, FontStyles.Bold, new Color(0.95f, 0.98f, 1f));
        praiseText.outlineWidth = 0.18f; praiseText.outlineColor = new Color(0f, 0.04f, 0.08f, 1f);

        isBuilt = true;
        gameObject.SetActive(true);
        c.SetActive(false);
    }

    private TextMeshProUGUI MakeLabel(Transform parent, string name, string text, Vector2 aMin, Vector2 aMax, float size, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.alignment = TextAlignmentOptions.Center;
        LocalizationManager.ApplyFont(t);
        t.fontSize = size; t.fontStyle = style; t.color = color; t.raycastTarget = false;
        return t;
    }

    // 勝利演出を表示。isBoss=章ボス, isMidBoss=中ボス でタイトルを少し変える。duration 秒で自動的に消える。
    public void ShowCelebration(bool isBoss, bool isMidBoss, float duration)
    {
        BuildIfNeeded();
        bool ja = LocalizationManager.IsJapanese;

        titleText.text = isBoss ? (ja ? "ボス撃破！" : "BOSS DEFEATED!")
                       : isMidBoss ? (ja ? "中ボス撃破！" : "MID-BOSS DOWN!")
                       : (ja ? "ウェーブクリア！" : "WAVE CLEAR!");
        string[] praises = ja ? PraiseJa : PraiseEn;
        praiseText.text = praises[Random.Range(0, praises.Length)];

        card.gameObject.SetActive(true);
        group.DOKill();
        card.DOKill();
        group.alpha = 0f;
        card.localScale = Vector3.one * 0.7f;
        card.anchoredPosition = new Vector2(0f, 120f);

        float hold = Mathf.Max(0.4f, duration - 0.5f);
        DOTween.Sequence().SetUpdate(true) // 倍速/ポーズに関係なくリアル時間で演出
            .Append(group.DOFade(1f, 0.18f))
            .Join(card.DOScale(1f, 0.34f).SetEase(Ease.OutBack))
            .Join(card.DOAnchorPosY(160f, 0.34f).SetEase(Ease.OutQuad))
            .AppendInterval(hold)
            .Append(group.DOFade(0f, 0.3f))
            .Join(card.DOAnchorPosY(190f, 0.3f).SetEase(Ease.InQuad))
            .OnComplete(() => { if (card != null) card.gameObject.SetActive(false); });
    }
}
