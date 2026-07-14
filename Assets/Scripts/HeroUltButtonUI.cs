using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ヒーロー必殺ボタン。戦闘中だけ表示し、1戦1回だけ押せる（使用後は無効化）。
// GameManager の状態を毎フレーム見て表示/活性を切り替える。
// ホバーで現在の必殺の効果（アップグレード反映）をツールチップ表示する。
public class HeroUltButtonUI : MonoBehaviour
{
    public static HeroUltButtonUI Instance { get; private set; }

    private bool isBuilt;
    private GameObject buttonObj;
    private Image bg;
    private Button button;
    private TextMeshProUGUI label;
    private GameObject tooltip;
    private TextMeshProUGUI tooltipText;

    public static HeroUltButtonUI EnsureExists()
    {
        if (Instance != null) return Instance;
        HeroUltButtonUI existing = FindObjectOfType<HeroUltButtonUI>(true);
        if (existing != null) { Instance = existing; existing.BuildIfNeeded(); return Instance; }

        GameObject root = new GameObject("HeroUltButtonUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HeroUltButtonUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 14500;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Instance = root.GetComponent<HeroUltButtonUI>();
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
        bool ja = LocalizationManager.IsJapanese;

        buttonObj = new GameObject("HeroUlt", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(transform, false);
        RectTransform r = buttonObj.GetComponent<RectTransform>();
        // FIGHTボタン（scene: bottom-left基準 (1580,215)/300×100 ＝ x[1430-1730]・y[165-265]）と
        // 重ならないよう、その真上に配置する。右側は1730→1920の190pxしか無く220px幅が入らないため上へ逃がす。
        // 参照解像度を FIGHT(=1920×1080想定) に合わせ、bottom-right基準で FIGHT中心(x=1580)上に揃える。
        r.anchorMin = new Vector2(1f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(1f, 0f);
        r.anchoredPosition = new Vector2(-230f, 292f); // x右端=1690(中心1580)、y下端=292(FIGHT上端265の上)
        r.sizeDelta = new Vector2(220f, 64f);
        bg = buttonObj.GetComponent<Image>();
        bg.color = new Color(0.55f, 0.32f, 0.08f, 0.96f);
        button = buttonObj.GetComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.UseHeroUltimate(); });

        label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(buttonObj.transform, false);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        LocalizationManager.ApplyFont(label);
        label.text = ja ? "ヒーロー必殺" : "HERO ULT";
        label.fontSize = 22f; label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.95f, 0.8f); label.raycastTarget = false;

        // ホバーで効果説明を出すツールチップ（ボタンの上）。
        tooltip = new GameObject("UltTooltip", typeof(RectTransform), typeof(Image));
        tooltip.transform.SetParent(buttonObj.transform, false);
        RectTransform tr = tooltip.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 1f); tr.anchorMax = new Vector2(0.5f, 1f); tr.pivot = new Vector2(0.5f, 0f);
        tr.anchoredPosition = new Vector2(0f, 12f); tr.sizeDelta = new Vector2(420f, 96f);
        tooltip.GetComponent<Image>().color = new Color(0.03f, 0.06f, 0.1f, 0.96f);
        tooltipText = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        tooltipText.transform.SetParent(tooltip.transform, false);
        RectTransform ttr = tooltipText.rectTransform;
        ttr.anchorMin = Vector2.zero; ttr.anchorMax = Vector2.one; ttr.offsetMin = new Vector2(12f, 8f); ttr.offsetMax = new Vector2(-12f, -8f);
        LocalizationManager.ApplyFont(tooltipText);
        tooltipText.fontSize = 17f; tooltipText.alignment = TextAlignmentOptions.Center; tooltipText.enableWordWrapping = true;
        tooltipText.color = new Color(0.9f, 0.95f, 1f); tooltipText.raycastTarget = false;
        tooltip.SetActive(false);

        // ホバー検出（ボタンに付与）。
        HoverProxy hover = buttonObj.AddComponent<HoverProxy>();
        hover.owner = this;

        isBuilt = true;
        gameObject.SetActive(true);
        buttonObj.SetActive(false);
    }

    private void ShowTooltip()
    {
        if (tooltip == null) return;
        bool ja = LocalizationManager.IsJapanese;
        string name = GameManager.Instance != null ? GameManager.Instance.GetHeroUltimateName(ja) : "";
        string desc = GameManager.Instance != null ? GameManager.Instance.GetHeroUltimateDescription(ja) : "";
        tooltipText.text = $"<b>{name}</b>\n{desc}";
        tooltip.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.SetActive(false);
    }

    // ボタンのホバーを拾ってツールチップを出すプロキシ。
    private class HoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public HeroUltButtonUI owner;
        public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.ShowTooltip(); }
        public void OnPointerExit(PointerEventData e) { if (owner != null) owner.HideTooltip(); }
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        bool visible = gm != null && gm.IsHeroUltVisible;
        if (buttonObj.activeSelf != visible) buttonObj.SetActive(visible);
        if (!visible) return;

        bool ready = gm.CanUseHeroUltimate();
        int cd = gm.HeroUltCooldownRemaining; // クールタイム残りラウンド数。
        button.interactable = ready;
        bg.color = ready ? new Color(0.78f, 0.46f, 0.1f, 0.98f) : new Color(0.28f, 0.24f, 0.2f, 0.85f);
        label.color = ready ? new Color(1f, 0.96f, 0.82f) : new Color(0.6f, 0.58f, 0.54f);

        // ラベルは選択中ヒーローの必殺名。クールタイム中は残りラウンド数を併記する。
        bool ja = LocalizationManager.IsJapanese;
        string ultName = gm.GetHeroUltimateName(ja);
        string shown = cd > 0
            ? ultName + (ja ? $"\n<size=68%>CT 残り{cd}</size>" : $"\n<size=68%>CD {cd}</size>")
            : ultName;
        if (label.text != shown)
        {
            LocalizationManager.ApplyFont(label);
            label.text = shown;
        }
    }
}
