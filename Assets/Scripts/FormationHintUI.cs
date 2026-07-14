using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ② 配置フォーメーションのガイド。常時は小さなトグルボタン（ウェーブ進捗の左）だけを置き、
// 押すと中央に陣形一覧ポップアップ（形の例＋効果）を表示する。成立中はボタンと行をハイライト。
public class FormationHintUI : MonoBehaviour
{
    public static FormationHintUI Instance { get; private set; }

    private bool isBuilt;
    private GameObject toggleBtn;
    private Image toggleBg;
    private TextMeshProUGUI toggleLabel;
    private GameObject guidePanel;
    private bool open;

    private readonly Image[] rowBg = new Image[4];
    private readonly TextMeshProUGUI[] rowTag = new TextMeshProUGUI[4];
    private bool[] active = new bool[4];

    // R3-hero-formation: 主人公専用フォーメーションの動的行（形・名前・効果は主人公で変わる）。
    private Image heroRowBg;
    private readonly Image[] heroCells = new Image[15]; // 5列×3行（スプレッド形に対応）
    private TextMeshProUGUI heroNameLbl, heroEffLbl, heroTag;
    private bool heroActive;
    private static readonly Color HeroCol = new Color(1f, 0.84f, 0.3f, 1f);

    private static readonly string[] NamesJa = { "突撃（横3）", "鉄壁（縦3）", "方陣（2×2）", "楔（斜め3）" };
    private static readonly string[] NamesEn = { "Charge (row 3)", "Phalanx (col 3)", "Square (2x2)", "Wedge (diag 3)" };
    private static readonly string[] EffJa = { "攻撃力↑・攻撃速度↑", "被ダメ↓・シールド", "与ダメ↑・被ダメ↓・シールド", "与ダメ大↑・攻撃速度↑" };
    private static readonly string[] EffEn = { "ATK & attack speed up", "Damage taken down · shield", "Offense & defense · shield", "Big damage & attack speed" };
    private static readonly int[][] Cells = { new[] { 3, 4, 5 }, new[] { 1, 4, 7 }, new[] { 0, 1, 3, 4 }, new[] { 0, 4, 8 } };
    private static readonly Color[] Cols = {
        new Color(1f, 0.55f, 0.18f), new Color(0.3f, 0.62f, 1f),
        new Color(0.3f, 0.85f, 0.55f), new Color(0.9f, 0.4f, 0.85f)
    };

    public static FormationHintUI EnsureExists()
    {
        if (Instance != null) return Instance;
        FormationHintUI existing = FindObjectOfType<FormationHintUI>(true);
        if (existing != null) { Instance = existing; Instance.BuildIfNeeded(); return Instance; }

        GameObject root = new GameObject("FormationGuide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(FormationHintUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 14200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Instance = root.GetComponent<FormationHintUI>();
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

        // 常時表示の小さなトグルボタン（ウェーブ進捗の左の隙間）。
        toggleBtn = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        toggleBtn.transform.SetParent(transform, false);
        RectTransform tbr = toggleBtn.GetComponent<RectTransform>();
        tbr.anchorMin = new Vector2(0f, 1f); tbr.anchorMax = new Vector2(0f, 1f); tbr.pivot = new Vector2(0f, 1f);
        tbr.anchoredPosition = new Vector2(232f, -8f);
        tbr.sizeDelta = new Vector2(190f, 30f);
        toggleBg = toggleBtn.GetComponent<Image>();
        toggleBg.color = new Color(0.09f, 0.16f, 0.26f, 0.95f);
        toggleBtn.GetComponent<Button>().onClick.AddListener(TogglePanel);
        toggleLabel = MakeLabel(toggleBtn.transform, "L", (ja ? "▣ 陣形ガイド ▾" : "▣ Formations ▾"),
            Vector2.zero, Vector2.one, 15f, FontStyles.Bold, new Color(0.85f, 0.92f, 1f), TextAlignmentOptions.Center, 0f);

        // 中央ポップアップ（押すと開く）。
        guidePanel = new GameObject("GuidePanel", typeof(RectTransform), typeof(Image));
        guidePanel.transform.SetParent(transform, false);
        RectTransform gpr = guidePanel.GetComponent<RectTransform>();
        gpr.anchorMin = new Vector2(0.5f, 0.5f); gpr.anchorMax = new Vector2(0.5f, 0.5f); gpr.pivot = new Vector2(0.5f, 0.5f);
        gpr.anchoredPosition = new Vector2(0f, 30f);
        gpr.sizeDelta = new Vector2(380f, 372f);
        guidePanel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.97f);

        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(guidePanel.transform, false);
        RectTransform hr = header.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(0f, 34f); hr.anchoredPosition = Vector2.zero;
        header.GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.2f, 1f);
        MakeLabel(header.transform, "Title", ja ? "陣形ガイド" : "Formations",
            new Vector2(0f, 0f), new Vector2(0.8f, 1f), 18f, FontStyles.Bold, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Left, 14f);

        GameObject close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(header.transform, false);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(1f, 0.5f); cr.anchorMax = new Vector2(1f, 0.5f); cr.pivot = new Vector2(1f, 0.5f);
        cr.anchoredPosition = new Vector2(-8f, 0f); cr.sizeDelta = new Vector2(28f, 26f);
        close.GetComponent<Image>().color = new Color(0.16f, 0.24f, 0.35f, 1f);
        close.GetComponent<Button>().onClick.AddListener(TogglePanel);
        MakeLabel(close.transform, "X", "✕", Vector2.zero, Vector2.one, 16f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, 0f);

        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(guidePanel.transform, false);
        RectTransform bodyR = body.GetComponent<RectTransform>();
        bodyR.anchorMin = new Vector2(0f, 0f); bodyR.anchorMax = new Vector2(1f, 1f);
        bodyR.offsetMin = new Vector2(8f, 8f); bodyR.offsetMax = new Vector2(-8f, -38f);
        for (int i = 0; i < 4; i++) BuildRow(bodyR, i, ja);
        BuildHeroRow(bodyR, 4, ja);

        guidePanel.SetActive(false);
        isBuilt = true;
        gameObject.SetActive(true);
        toggleBtn.SetActive(false); // SetBuildPhase で出す
    }

    private void BuildRow(RectTransform parent, int idx, bool ja)
    {
        GameObject row = new GameObject("Row" + idx, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        RectTransform rr = row.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, 60f);
        rr.anchoredPosition = new Vector2(0f, -idx * 64f);
        rowBg[idx] = row.GetComponent<Image>();
        rowBg[idx].color = new Color(1f, 1f, 1f, 0.04f);

        float cell = 15f, gap = 2f;
        GameObject grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(row.transform, false);
        RectTransform gr = grid.GetComponent<RectTransform>();
        gr.anchorMin = new Vector2(0f, 0.5f); gr.anchorMax = new Vector2(0f, 0.5f); gr.pivot = new Vector2(0f, 0.5f);
        gr.anchoredPosition = new Vector2(8f, 0f);
        gr.sizeDelta = new Vector2(cell * 3 + gap * 2, cell * 3 + gap * 2);
        HashSet<int> on = new HashSet<int>(Cells[idx]);
        for (int c = 0; c < 9; c++)
        {
            int gx = c % 3, gy = c / 3;
            GameObject cg = new GameObject("c" + c, typeof(RectTransform), typeof(Image));
            cg.transform.SetParent(grid.transform, false);
            RectTransform cr2 = cg.GetComponent<RectTransform>();
            cr2.anchorMin = new Vector2(0f, 1f); cr2.anchorMax = new Vector2(0f, 1f); cr2.pivot = new Vector2(0f, 1f);
            cr2.anchoredPosition = new Vector2(gx * (cell + gap), -gy * (cell + gap));
            cr2.sizeDelta = new Vector2(cell, cell);
            Image ci = cg.GetComponent<Image>();
            ci.color = on.Contains(c) ? Cols[idx] : new Color(0.18f, 0.22f, 0.3f, 0.9f);
            ci.raycastTarget = false;
        }

        float textX = 8f + cell * 3 + gap * 2 + 12f;
        GameObject txt = new GameObject("Texts", typeof(RectTransform));
        txt.transform.SetParent(row.transform, false);
        RectTransform tr = txt.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(textX, 4f); tr.offsetMax = new Vector2(-6f, -4f);
        MakeLabel(tr, "Name", ja ? NamesJa[idx] : NamesEn[idx], new Vector2(0f, 0.5f), new Vector2(1f, 1f), 15f, FontStyles.Bold, Cols[idx], TextAlignmentOptions.BottomLeft, 0f);
        MakeLabel(tr, "Eff", ja ? EffJa[idx] : EffEn[idx], new Vector2(0f, 0f), new Vector2(1f, 0.5f), 12f, FontStyles.Normal, new Color(0.8f, 0.86f, 0.95f), TextAlignmentOptions.TopLeft, 0f);
        rowTag[idx] = MakeLabel(row.transform, "Tag", ja ? "● 発動中" : "● ON", new Vector2(1f, 1f), new Vector2(1f, 1f), 12f, FontStyles.Bold, new Color(1f, 0.92f, 0.5f), TextAlignmentOptions.TopRight, 0f);
        RectTransform tagR = rowTag[idx].rectTransform;
        tagR.pivot = new Vector2(1f, 1f); tagR.anchoredPosition = new Vector2(-6f, -4f); tagR.sizeDelta = new Vector2(80f, 18f);
        rowTag[idx].gameObject.SetActive(false);
    }

    // 主人公専用フォーメーションの動的行（形・名前・効果は SetHeroFormation で更新）。金色テーマ。
    private void BuildHeroRow(RectTransform parent, int idx, bool ja)
    {
        GameObject row = new GameObject("HeroRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        RectTransform rr = row.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, 60f);
        rr.anchoredPosition = new Vector2(0f, -idx * 64f);
        heroRowBg = row.GetComponent<Image>();
        heroRowBg.color = new Color(HeroCol.r, HeroCol.g, HeroCol.b, 0.06f);

        float cell = 15f, gap = 2f;
        GameObject grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(row.transform, false);
        RectTransform gr = grid.GetComponent<RectTransform>();
        gr.anchorMin = new Vector2(0f, 0.5f); gr.anchorMax = new Vector2(0f, 0.5f); gr.pivot = new Vector2(0f, 0.5f);
        gr.anchoredPosition = new Vector2(8f, 0f);
        gr.sizeDelta = new Vector2(cell * 5 + gap * 4, cell * 3 + gap * 2);
        for (int c = 0; c < 15; c++)
        {
            int gx = c % 5, gy = c / 5;
            GameObject cg = new GameObject("c" + c, typeof(RectTransform), typeof(Image));
            cg.transform.SetParent(grid.transform, false);
            RectTransform cr2 = cg.GetComponent<RectTransform>();
            cr2.anchorMin = new Vector2(0f, 1f); cr2.anchorMax = new Vector2(0f, 1f); cr2.pivot = new Vector2(0f, 1f);
            cr2.anchoredPosition = new Vector2(gx * (cell + gap), -gy * (cell + gap));
            cr2.sizeDelta = new Vector2(cell, cell);
            heroCells[c] = cg.GetComponent<Image>();
            heroCells[c].color = new Color(0.18f, 0.22f, 0.3f, 0.9f);
            heroCells[c].raycastTarget = false;
        }

        float textX = 8f + cell * 5 + gap * 4 + 12f;
        GameObject txt = new GameObject("Texts", typeof(RectTransform));
        txt.transform.SetParent(row.transform, false);
        RectTransform tr = txt.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(textX, 4f); tr.offsetMax = new Vector2(-6f, -4f);
        heroNameLbl = MakeLabel(tr, "Name", ja ? "主人公の陣" : "Hero Formation", new Vector2(0f, 0.5f), new Vector2(1f, 1f), 15f, FontStyles.Bold, HeroCol, TextAlignmentOptions.BottomLeft, 0f);
        heroEffLbl = MakeLabel(tr, "Eff", "", new Vector2(0f, 0f), new Vector2(1f, 0.5f), 12f, FontStyles.Normal, new Color(0.95f, 0.9f, 0.7f), TextAlignmentOptions.TopLeft, 0f);
        heroTag = MakeLabel(row.transform, "Tag", ja ? "● 発動中" : "● ON", new Vector2(1f, 1f), new Vector2(1f, 1f), 12f, FontStyles.Bold, new Color(1f, 0.92f, 0.5f), TextAlignmentOptions.TopRight, 0f);
        RectTransform tagR = heroTag.rectTransform;
        tagR.pivot = new Vector2(1f, 1f); tagR.anchoredPosition = new Vector2(-6f, -4f); tagR.sizeDelta = new Vector2(80f, 18f);
        heroTag.gameObject.SetActive(false);
    }

    // 主人公専用フォーメーションを反映（cells3x3=点灯セルindex 0-8、name=名、eff=効果、active=成立中）。
    public void SetHeroFormation(int[] cells3x3, string name, string eff, bool active)
    {
        BuildIfNeeded();
        heroActive = active;
        HashSet<int> on = new HashSet<int>(cells3x3 ?? new int[0]);
        for (int c = 0; c < 15; c++)
            if (heroCells[c] != null)
                heroCells[c].color = on.Contains(c) ? HeroCol : new Color(0.18f, 0.22f, 0.3f, 0.9f);
        if (heroNameLbl != null) { LocalizationManager.ApplyFont(heroNameLbl); heroNameLbl.text = name; }
        if (heroEffLbl != null) { LocalizationManager.ApplyFont(heroEffLbl); heroEffLbl.text = eff; }
        if (heroTag != null) heroTag.gameObject.SetActive(active);
        if (heroRowBg != null) heroRowBg.color = active ? new Color(HeroCol.r, HeroCol.g, HeroCol.b, 0.24f) : new Color(HeroCol.r, HeroCol.g, HeroCol.b, 0.06f);
    }

    private TextMeshProUGUI MakeLabel(Transform parent, string name, string text, Vector2 aMin, Vector2 aMax, float size, FontStyles style, Color color, TextAlignmentOptions align, float leftPad)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = new Vector2(leftPad, 0f); r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(t);
        t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = align;
        t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
        t.text = text;
        return t;
    }

    private void TogglePanel()
    {
        open = !open;
        if (guidePanel != null) guidePanel.SetActive(open);
        RefreshToggleLabel();
    }

    private void RefreshToggleLabel()
    {
        if (toggleLabel == null) return;
        bool ja = LocalizationManager.IsJapanese;
        int activeCount = 0;
        for (int i = 0; i < 4; i++) if (active[i]) activeCount++;
        string arrow = open ? "▴" : "▾";
        string baseName = ja ? "陣形ガイド" : "Formations";
        if (activeCount > 0)
        {
            toggleLabel.text = $"▣ {baseName}（{(ja ? "発動中" : "ON")} {activeCount}）{arrow}";
            toggleBg.color = new Color(0.18f, 0.30f, 0.20f, 0.97f);
            toggleLabel.color = new Color(1f, 0.92f, 0.55f);
        }
        else
        {
            toggleLabel.text = $"▣ {baseName} {arrow}";
            toggleBg.color = new Color(0.09f, 0.16f, 0.26f, 0.95f);
            toggleLabel.color = new Color(0.85f, 0.92f, 1f);
        }
    }

    // 編成中だけトグルボタンを出す。戦闘・選択中はボタンもポップアップも隠す。
    public void SetBuildPhase(bool show)
    {
        BuildIfNeeded();
        if (toggleBtn.activeSelf != show) toggleBtn.SetActive(show);
        if (!show && guidePanel.activeSelf) { guidePanel.SetActive(false); open = false; }
    }

    // 成立中の陣形を行＋ボタンに反映。
    public void SetActiveFormations(bool charge, bool phalanx, bool square, bool wedge)
    {
        BuildIfNeeded();
        active[0] = charge; active[1] = phalanx; active[2] = square; active[3] = wedge;
        for (int i = 0; i < 4; i++)
        {
            if (rowTag[i] != null) rowTag[i].gameObject.SetActive(active[i]);
            if (rowBg[i] != null) rowBg[i].color = active[i] ? new Color(Cols[i].r, Cols[i].g, Cols[i].b, 0.22f) : new Color(1f, 1f, 1f, 0.04f);
        }
        RefreshToggleLabel();
    }
}
