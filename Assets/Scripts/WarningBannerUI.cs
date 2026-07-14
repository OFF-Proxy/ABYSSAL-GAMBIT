using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 章ボス戦への遷移時に出す「WARNING」警告演出。赤い帯＋点滅で数秒表示し、完了で onComplete を呼ぶ。
// GameManager が勝利インターロードの後（次が章ボスウェーブのとき）に挟む。
public class WarningBannerUI : MonoBehaviour
{
    public static WarningBannerUI Instance { get; private set; }

    private bool isBuilt;
    private CanvasGroup group;
    private RectTransform card;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subText;
    private Image screenFlash;   // 画面全体の赤フラッシュ（サイレンに同期して点滅）

    // ボス戦前のサイレン効果音（Resources/sfx）。ユーザー提供。
    private const string SirenPath = "sfx/Warning-Siren05-01(Fast-Mid)";

    public static WarningBannerUI EnsureExists()
    {
        if (Instance != null) return Instance;
        WarningBannerUI existing = FindObjectOfType<WarningBannerUI>(true);
        if (existing != null) { Instance = existing; existing.BuildIfNeeded(); return Instance; }

        GameObject root = new GameObject("WarningBannerUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(WarningBannerUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 24500; // 勝利バナー(24000)より前面、結果/報酬(25000+)より下。
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.GetComponent<GraphicRaycaster>().enabled = false;

        Instance = root.GetComponent<WarningBannerUI>();
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

        // 画面全体の赤フラッシュ（card より背面）。サイレンの点滅に同期させる。
        GameObject sf = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
        sf.transform.SetParent(transform, false);
        RectTransform sfr = sf.GetComponent<RectTransform>();
        sfr.anchorMin = Vector2.zero; sfr.anchorMax = Vector2.one; sfr.offsetMin = Vector2.zero; sfr.offsetMax = Vector2.zero;
        screenFlash = sf.GetComponent<Image>();
        screenFlash.color = new Color(0.8f, 0.05f, 0.05f, 0f);
        screenFlash.raycastTarget = false;

        GameObject c = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
        c.transform.SetParent(transform, false);
        card = c.GetComponent<RectTransform>();
        card.anchorMin = new Vector2(0.5f, 0.5f); card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(1920f, 260f);
        group = c.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        // 画面横いっぱいの赤い警告帯。
        GameObject band = new GameObject("Band", typeof(RectTransform), typeof(Image));
        band.transform.SetParent(card, false);
        RectTransform brt = band.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 1f); brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        Image bandImg = band.GetComponent<Image>();
        bandImg.color = new Color(0.55f, 0.02f, 0.02f, 0.82f);
        bandImg.raycastTarget = false;

        // 上下の細い明るい赤ライン。
        MakeLine(card, new Vector2(0f, 1f), new Vector2(1f, 1f), 6f, true);
        MakeLine(card, new Vector2(0f, 0f), new Vector2(1f, 0f), 6f, false);

        titleText = MakeLabel(card, "Title", "⚠  W A R N I N G  ⚠", new Vector2(0f, 0.42f), new Vector2(1f, 1f), 78f, FontStyles.Bold, new Color(1f, 0.92f, 0.3f));
        titleText.outlineWidth = 0.28f; titleText.outlineColor = new Color(0.2f, 0f, 0f, 1f);
        subText = MakeLabel(card, "Sub", "", new Vector2(0f, 0f), new Vector2(1f, 0.44f), 34f, FontStyles.Bold, new Color(1f, 0.95f, 0.92f));
        subText.outlineWidth = 0.2f; subText.outlineColor = new Color(0.2f, 0f, 0f, 1f);

        isBuilt = true;
        gameObject.SetActive(true);
        c.SetActive(false);
    }

    private void MakeLine(Transform parent, Vector2 aMin, Vector2 aMax, float h, bool top)
    {
        GameObject go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = new Vector2(0.5f, top ? 1f : 0f);
        r.sizeDelta = new Vector2(0f, h); r.anchoredPosition = Vector2.zero;
        Image img = go.GetComponent<Image>(); img.color = new Color(1f, 0.35f, 0.2f, 0.95f); img.raycastTarget = false;
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

    // 警告演出を表示し、完了で onComplete を呼ぶ。リアル時間（倍速/ポーズに非依存）。
    // duration は目安。サイレン効果音の長さに合わせて自動調整する。
    public void Show(float duration, Action onComplete)
    {
        BuildIfNeeded();
        bool ja = LocalizationManager.IsJapanese;
        subText.text = ja ? "── 章ボス 接近 ──" : "── CHAPTER BOSS APPROACHING ──";

        // サイレンを鳴らし、その長さに演出尺を合わせる（クランプ）。
        AudioClip siren = Resources.Load<AudioClip>(SirenPath);
        float total = siren != null ? Mathf.Clamp(siren.length, 1.4f, 3.6f) : Mathf.Max(1.4f, duration);
        AttackEffectPlayer.PlayUiSfx("Warning-Siren05-01(Fast-Mid)", 1f); // 警告サイレンは大きめに。

        card.gameObject.SetActive(true);
        group.DOKill(); card.DOKill(); screenFlash.DOKill();
        group.alpha = 0f;
        card.localScale = new Vector3(1f, 0.6f, 1f);
        SetFlashAlpha(0f);

        const float fadeIn = 0.16f, fadeOut = 0.32f;
        float hold = Mathf.Max(0.4f, total - fadeIn - fadeOut);
        // サイレン（Fast-Mid）の往復に合わせた点滅周期。半周期 ~0.16s（約3回/秒）。
        int pulses = Mathf.Max(4, Mathf.RoundToInt(hold / 0.16f));

        // 画面赤フラッシュ＋バナーを同じ周期で点滅させる（サイレンと同期した警告感）。
        screenFlash.DOFade(0.32f, hold / pulses).SetUpdate(true).SetLoops(pulses, LoopType.Yoyo);
        titleText.transform.DOScale(1.06f, hold / pulses).SetUpdate(true).SetLoops(pulses, LoopType.Yoyo);

        DOTween.Sequence().SetUpdate(true)
            .Append(group.DOFade(1f, fadeIn))
            .Join(card.DOScaleY(1f, 0.22f).SetEase(Ease.OutBack))
            .Append(group.DOFade(0.5f, 0.16f).SetLoops(pulses, LoopType.Yoyo)) // バナー点滅
            .Append(group.DOFade(0f, fadeOut))
            .Join(card.DOScaleY(0.6f, fadeOut).SetEase(Ease.InQuad))
            .Join(screenFlash.DOFade(0f, fadeOut))
            .OnComplete(() =>
            {
                if (card != null) card.gameObject.SetActive(false);
                SetFlashAlpha(0f);
                onComplete?.Invoke();
            });
    }

    private void SetFlashAlpha(float a)
    {
        if (screenFlash == null) return;
        Color c = screenFlash.color; c.a = a; screenFlash.color = c;
    }
}
