using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// R3-hero-depth: ラン開始時に1回、ヒーロー必殺を2択で強化する選択UI。
// 選んだ結果(1=A / 2=B)を GameManager.HeroUltUpgrade に反映する。
public class HeroUltUpgradeUI : MonoBehaviour
{
    private const int SortingOrder = 25040; // アイテム報酬(25030)より前面・ボスバナー(25600)より後面。

    public static HeroUltUpgradeUI Instance { get; private set; }

    private Action<int> onChosen;
    private RectTransform panelRect;
    private float previousTimeScale = 1f;

    public static HeroUltUpgradeUI EnsureExists()
    {
        if (Instance != null) return Instance;
        HeroUltUpgradeUI existing = FindObjectOfType<HeroUltUpgradeUI>(true);
        if (existing != null) { Instance = existing; return existing; }

        GameObject go = new GameObject("HeroUltUpgradeUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(HeroUltUpgradeUI));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Image overlay = go.GetComponent<Image>();
        overlay.color = new Color(0f, 0.02f, 0.05f, 0.8f);
        overlay.raycastTarget = true;
        Instance = go.GetComponent<HeroUltUpgradeUI>();
        return Instance;
    }

    private void Awake()
    {
        LocalizationManager.EnsureExists();
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Show(string heroId, Action<int> chosenCallback)
    {
        onChosen = chosenCallback;
        bool ja = LocalizationManager.IsJapanese;

        // 古いパネルがあれば作り直す（ヒーロー別文言のため）。
        if (panelRect != null) Destroy(panelRect.gameObject);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f); panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero; panelRect.sizeDelta = new Vector2(900f, 560f);
        Image pImg = panel.GetComponent<Image>();
        Sprite ps = Resources.Load<Sprite>("UI/Augment/card_panel");
        if (ps != null) { pImg.sprite = ps; pImg.type = Image.Type.Sliced; pImg.color = new Color(0.10f, 0.16f, 0.26f, 0.98f); }
        else pImg.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);

        GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(panel.transform, false);
        RectTransform tbr = titleBar.GetComponent<RectTransform>();
        tbr.anchorMin = new Vector2(0f, 1f); tbr.anchorMax = new Vector2(1f, 1f); tbr.pivot = new Vector2(0.5f, 1f);
        tbr.sizeDelta = new Vector2(0f, 72f); tbr.anchoredPosition = new Vector2(0f, -12f);
        titleBar.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.2f, 0.9f);
        MakeLabel(titleBar.transform, "Title", ja ? "必殺を強化（1回のみ）" : "EMPOWER YOUR ULTIMATE (once)",
            Vector2.zero, Vector2.one, 34f, FontStyles.Bold, new Color(1f, 0.92f, 0.6f), TextAlignmentOptions.Center);

        GetOptionText(heroId, ja, out string aTitle, out string aDesc, out string bTitle, out string bDesc);
        CreateCard(-220f, 1, aTitle, aDesc, new Color(0.95f, 0.78f, 0.35f));
        CreateCard(220f, 2, bTitle, bDesc, new Color(0.45f, 0.78f, 1f));

        gameObject.SetActive(true);
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        panelRect.localScale = Vector3.one * 0.92f;
        panelRect.DOKill();
        panelRect.DOScale(1f, 0.26f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void CreateCard(float x, int choice, string title, string desc, Color accent)
    {
        GameObject card = new GameObject("UpgradeCard" + choice, typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(panelRect, false);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.45f); cr.pivot = new Vector2(0.5f, 0.5f);
        cr.anchoredPosition = new Vector2(x, 0f); cr.sizeDelta = new Vector2(380f, 380f);
        card.GetComponent<Image>().color = new Color(0.07f, 0.12f, 0.2f, 1f);
        Button btn = card.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        int captured = choice;
        btn.onClick.AddListener(() => Choose(captured));
        card.AddComponent<Hover>();

        GameObject bar = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(card.transform, false);
        RectTransform brr = bar.GetComponent<RectTransform>();
        brr.anchorMin = new Vector2(0f, 1f); brr.anchorMax = new Vector2(1f, 1f); brr.pivot = new Vector2(0.5f, 1f);
        brr.sizeDelta = new Vector2(0f, 8f); brr.anchoredPosition = Vector2.zero;
        Image barImg = bar.GetComponent<Image>(); barImg.color = accent; barImg.raycastTarget = false;

        MakeLabel(card.transform, "T", title, new Vector2(0.06f, 0.7f), new Vector2(0.94f, 0.93f), 26f, FontStyles.Bold, new Color(accent.r, accent.g, accent.b), TextAlignmentOptions.Center);
        MakeLabel(card.transform, "D", desc, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.66f), 19f, FontStyles.Normal, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Center).enableWordWrapping = true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：開いていれば強化Aを自動選択する。開いていなければfalse。
    public bool DebugAutoResolve()
    {
        if (!gameObject.activeSelf) return false;
        Choose(1);
        return true;
    }
#endif

    private void Choose(int choice)
    {
        AttackEffectPlayer.PlayUiSfx("unit_buy");
        Action<int> cb = onChosen; onChosen = null;
        Time.timeScale = previousTimeScale;
        gameObject.SetActive(false);
        cb?.Invoke(choice);
    }

    private void GetOptionText(string heroId, bool ja, out string aT, out string aD, out string bT, out string bD)
    {
        switch ((heroId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin":
                aT = ja ? "鉄壁の盾" : "Bulwark"; aD = ja ? "聖盾のシールド量を増加\n最大HP 30% → 45%" : "Aegis shield 30% → 45% max HP";
                bT = ja ? "不屈" : "Endurance"; bD = ja ? "聖盾の効果時間を延長\n6秒 → 10秒" : "Aegis duration 6s → 10s"; break;
            case "herokagachi":
                aT = ja ? "修羅烈" : "Carnage"; aD = ja ? "与ダメージ増加\n+35% → +55%" : "Damage +35% → +55%";
                bT = ja ? "神速" : "Godspeed"; bD = ja ? "攻撃速度増加\n×1.25 → ×1.45" : "Attack speed x1.25 → x1.45"; break;
            case "herovesna":
                aT = ja ? "業火" : "Inferno"; aD = ja ? "敵への威力を増加\n×1.6" : "Enemy damage x1.6";
                bT = ja ? "追雷" : "Thunderclap"; bD = ja ? "敵をスタン付与\n0.6秒" : "Stuns enemies 0.6s"; break;
            default:
                aT = ja ? "強化A" : "Upgrade A"; aD = ja ? "効果を強化" : "Stronger effect";
                bT = ja ? "強化B" : "Upgrade B"; bD = ja ? "効果を延長" : "Longer effect"; break;
        }
    }

    private TextMeshProUGUI MakeLabel(Transform parent, string name, string text, Vector2 aMin, Vector2 aMax, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.alignment = align;
        LocalizationManager.ApplyFont(t);
        t.fontSize = size; t.fontStyle = style; t.color = color; t.raycastTarget = false;
        return t;
    }

    private class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData e) { var r = transform as RectTransform; if (r != null) { r.DOKill(); r.DOScale(1.05f, 0.12f).SetUpdate(true); } }
        public void OnPointerExit(PointerEventData e) { var r = transform as RectTransform; if (r != null) { r.DOKill(); r.DOScale(1f, 0.12f).SetUpdate(true); } }
    }
}
