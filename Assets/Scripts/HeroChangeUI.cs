using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// R3-hero-select: 編成中に主人公（ヒーロー）を変更するための簡易オーバーレイ。
// オプション（または編成画面）から開き、3体から選んで GameManager.ChangeHeroUnit を呼ぶ。
// 戦闘中は変更不可（その旨を表示）。独立したルートの ScreenSpaceOverlay Canvas として生成する
// （報酬UIと同様、外部 Canvas に親子化しない＝ホバー連動の点滅バグを避ける）。
public class HeroChangeUI : MonoBehaviour
{
    private const int SortingOrder = 26050; // オプション(60000)より下・他モーダル域。
    private static readonly string[] HeroIds = { "HeroAldin", "HeroKagachi", "HeroVesna" };

    public static HeroChangeUI Instance { get; private set; }

    private Canvas localCanvas;
    private GameObject panel;
    private TextMeshProUGUI noteText;
    private readonly Button[] heroButtons = new Button[3];
    private readonly Image[] heroButtonBg = new Image[3];

    private void Awake()
    {
        LocalizationManager.EnsureExists();
        Instance = this;
        EnsureUi();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static HeroChangeUI EnsureExists()
    {
        if (Instance != null) return Instance;
        HeroChangeUI existing = FindObjectOfType<HeroChangeUI>(true);
        if (existing != null) { Instance = existing; existing.EnsureUi(); return existing; }

        GameObject go = new GameObject("HeroChangeUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(HeroChangeUI));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0f, 0.02f, 0.05f, 0.72f);

        Instance = go.GetComponent<HeroChangeUI>();
        Instance.EnsureUi();
        Instance.gameObject.SetActive(false);
        return Instance;
    }

    public void Show()
    {
        EnsureUi();
        RefreshState();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void EnsureUi()
    {
        EnsureCanvas();
        if (panel != null) return;
        bool ja = LocalizationManager.IsJapanese;

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = Vector2.zero; pr.sizeDelta = new Vector2(760f, 360f);
        panel.GetComponent<Image>().color = new Color(0.01f, 0.04f, 0.08f, 0.96f);

        MakeLabel(panel.transform, "Title", ja ? "主人公（ヒーロー）を変更" : "CHANGE YOUR HERO",
            new Vector2(0f, 1f), new Vector2(1f, 1f), 28f, FontStyles.Bold, new Color(0.96f, 0.9f, 0.55f), TextAlignmentOptions.Center)
            .rectTransform.anchoredPosition = new Vector2(0f, -34f);

        // 3体ボタン（横並び）。
        for (int i = 0; i < HeroIds.Length; i++)
        {
            int idx = i;
            float cx = 0.5f + (i - 1) * 0.30f;
            GameObject b = new GameObject("Hero_" + HeroIds[i], typeof(RectTransform), typeof(Image), typeof(Button));
            b.transform.SetParent(panel.transform, false);
            RectTransform brt = b.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(cx, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, 10f); brt.sizeDelta = new Vector2(200f, 150f);
            heroButtonBg[i] = b.GetComponent<Image>();
            heroButtonBg[i].color = new Color(0.10f, 0.16f, 0.24f, 1f);
            heroButtons[i] = b.GetComponent<Button>();
            heroButtons[i].onClick.AddListener(() => OnPick(HeroIds[idx]));

            MakeLabel(b.transform, "N", LocalizationManager.UnitName(HeroIds[i]),
                new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f), 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            MakeLabel(b.transform, "R", RoleText(HeroIds[i], ja),
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.5f), 14f, FontStyles.Normal, new Color(0.8f, 0.88f, 1f), TextAlignmentOptions.Center);
        }

        noteText = MakeLabel(panel.transform, "Note", string.Empty,
            new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), 14f, FontStyles.Italic, new Color(1f, 0.7f, 0.6f), TextAlignmentOptions.Center);
        noteText.rectTransform.anchoredPosition = new Vector2(0f, 78f);
        noteText.rectTransform.sizeDelta = new Vector2(680f, 24f);

        // 閉じる。
        GameObject close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(panel.transform, false);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f); cr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(0f, 18f); cr.sizeDelta = new Vector2(200f, 44f);
        close.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.28f, 0.96f);
        close.GetComponent<Button>().onClick.AddListener(Hide);
        MakeLabel(close.transform, "L", ja ? "閉じる" : "CLOSE", Vector2.zero, Vector2.one, 18f, FontStyles.Bold, new Color(0.9f, 0.94f, 1f), TextAlignmentOptions.Center);
    }

    private void OnPick(string heroId)
    {
        if (GameManager.Instance == null) return;
        // 戦闘中は不可。
        if (!GameManager.Instance.ChangeHeroUnit(heroId))
        {
            bool ja = LocalizationManager.IsJapanese;
            if (noteText != null)
            {
                LocalizationManager.ApplyFont(noteText);
                noteText.text = ja ? "戦闘中は変更できません（編成フェーズで変更してください）" : "Cannot change during combat. Change during the build phase.";
            }
            return;
        }
        AttackEffectPlayer.PlayUiSfx("unit_buy");
        RefreshState();
        Hide();
    }

    // 現在のヒーローをハイライトし、戦闘中なら注意文を出す。
    private void RefreshState()
    {
        string current = GameManager.Instance != null ? GameManager.Instance.CurrentHeroUnitId : null;
        bool inCombat = GameManager.Instance != null && GameManager.Instance.IsRoundInProgress;
        bool ja = LocalizationManager.IsJapanese;
        for (int i = 0; i < HeroIds.Length; i++)
        {
            if (heroButtonBg[i] == null) continue;
            bool isCurrent = !string.IsNullOrEmpty(current) && string.Equals(current, HeroIds[i], StringComparison.OrdinalIgnoreCase);
            heroButtonBg[i].color = isCurrent ? new Color(0.22f, 0.5f, 0.7f, 1f) : new Color(0.10f, 0.16f, 0.24f, 1f);
            if (heroButtons[i] != null) heroButtons[i].interactable = !inCombat;
        }
        if (noteText != null)
        {
            LocalizationManager.ApplyFont(noteText);
            noteText.text = inCombat
                ? (ja ? "戦闘中は変更できません" : "Cannot change during combat")
                : (ja ? "選ぶと即座に入れ替わります（装備はベンチへ戻ります）" : "Picking swaps immediately (equipped items return to the bench)");
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

    private string RoleText(string heroId, bool ja)
    {
        switch ((heroId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin": return ja ? "聖騎士・守護\n聖盾の号令" : "Paladin\nAegis Command";
            case "herokagachi": return ja ? "アサシン・攻撃\n修羅の号令" : "Assassin\nCarnage Command";
            case "herovesna": return ja ? "蒼魔・蒼炎\n蒼炎の号令" : "Azure Mage\nAzure Flame Command";
            default: return string.Empty;
        }
    }

    private void EnsureCanvas()
    {
        if (localCanvas == null) localCanvas = GetComponent<Canvas>();
        if (localCanvas == null) localCanvas = gameObject.AddComponent<Canvas>();
        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = SortingOrder;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }
}
