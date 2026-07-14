using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// midboss-nodes: 章ボス直前の「進路選択」カードUI（Slay the Spire 風）。
// 残りノード(2〜3枚)をカードで提示し、1枚クリックで onPick(poolIndex) を返す。
// 逐次選択：選ぶたびにこのUIを開き、戦闘→次の選択、を2回繰り返す（残り1ノードは破棄）。
// 既存UIシングルトンの流儀（EnsureExists / ScreenSpaceOverlay / DOTween / JA-EN）に準拠。
public class NodeSelectionUI : MonoBehaviour
{
    public static NodeSelectionUI Instance { get; private set; }

    // GameManager から渡す1ノードの表示データ。
    public struct NodeOption
    {
        public int poolIndex;      // nodePool 内の実インデックス（onPick で返す）。
        public string titleJa, titleEn;
        public string descJa, descEn;   // 報酬種別などの説明。
        public Color tint;             // アーキタイプ色（精鋭=赤/標準=金/補給=緑）。
        public int difficultyStar;     // 1〜3。
        public Sprite icon;            // 代表中ボスのアイコン（任意）。
    }

    private Canvas canvas;
    private GameObject dim;
    private RectTransform panel;
    private CanvasGroup panelGroup;
    private RectTransform cardRow;
    private bool built;
    private Action<int> onPick;
    private int autoPickIndex = -1;   // オートプレイの自動選択用（表示中の先頭ノードのpoolIndex）。
    private string autoPickLabel = "";
    public string LastPickedLabel { get; private set; } = ""; // 直近に選んだルートの表示名（オートプレイのルート記録用）。

    // 表示中か（オートプレイの自動解決判定に使う）。
    public bool IsShowing => built && panel != null && panel.gameObject.activeSelf && onPick != null;

    // オートプレイ用：表示中なら先頭ノードを選んで進める。解決したら true。
    public bool DebugAutoResolve()
    {
        if (!IsShowing) return false;
        LastPickedLabel = autoPickLabel; // ルート記録用に確定。
        var cb = onPick; onPick = null;
        Hide();
        cb?.Invoke(autoPickIndex);
        return true;
    }

    public static NodeSelectionUI EnsureExists()
    {
        if (Instance != null) return Instance;
        NodeSelectionUI ex = FindObjectOfType<NodeSelectionUI>(true);
        if (ex != null) { Instance = ex; ex.BuildIfNeeded(); return Instance; }

        GameObject go = new GameObject("NodeSelectionUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(NodeSelectionUI));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 25000; // ダイアログ類より前面、プロローグ(26000)より後ろ。
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Instance = go.GetComponent<NodeSelectionUI>();
        Instance.BuildIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; BuildIfNeeded();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void BuildIfNeeded()
    {
        if (built) return;
        bool ja = LocalizationManager.IsJapanese;

        dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(transform, false);
        RectTransform dr = dim.GetComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup));
        panelObj.transform.SetParent(transform, false);
        panel = panelObj.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f); panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(1280f, 620f);
        panelGroup = panelObj.GetComponent<CanvasGroup>();

        TextMeshProUGUI title = MakeText("Title", panel, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1100f, 56f), 34f, FontStyles.Bold, Color.white);
        title.alignment = TextAlignmentOptions.Center;
        title.text = ja ? "進路を選べ ―― 章ボスへの最終準備" : "Choose your path — final prep before the chapter boss";

        TextMeshProUGUI sub = MakeText("Sub", panel, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(1100f, 36f), 20f, FontStyles.Italic, new Color(0.8f, 0.88f, 1f, 0.9f));
        sub.alignment = TextAlignmentOptions.Center;
        sub.text = ja ? "選んだ道だけ戦える。選ばなかった道とその報酬は失われる。" : "You only fight the path you pick. Skipped paths and their rewards are lost.";

        GameObject row = new GameObject("CardRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(panel, false);
        cardRow = row.GetComponent<RectTransform>();
        cardRow.anchorMin = new Vector2(0.5f, 0.5f); cardRow.anchorMax = new Vector2(0.5f, 0.5f); cardRow.pivot = new Vector2(0.5f, 0.5f);
        cardRow.anchoredPosition = new Vector2(0f, -20f); cardRow.sizeDelta = new Vector2(1200f, 420f);
        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 36f; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandHeight = false; hlg.childForceExpandWidth = false;

        built = true;
        gameObject.SetActive(true);
        SetShown(false);
    }

    private void SetShown(bool on)
    {
        if (dim != null) dim.SetActive(on);
        if (panel != null) panel.gameObject.SetActive(on);
        var gr = GetComponent<GraphicRaycaster>(); if (gr != null) gr.enabled = on;
    }

    public void Show(List<NodeOption> options, Action<int> pickCallback)
    {
        BuildIfNeeded();
        onPick = pickCallback;
        autoPickIndex = (options != null && options.Count > 0) ? options[0].poolIndex : -1;
        autoPickLabel = (options != null && options.Count > 0) ? (options[0].titleEn ?? options[0].titleJa ?? "") : "";

        // 既存カードをクリア。破棄前にカードのトゥイーン(ButtonJuice等)を止めて、破棄後のDOTween警告を防ぐ。
        for (int i = cardRow.childCount - 1; i >= 0; i--)
        {
            Transform ch = cardRow.GetChild(i);
            ch.DOKill();
            Destroy(ch.gameObject);
        }
        foreach (var opt in options) BuildCard(opt);

        SetShown(true);
        panelGroup.alpha = 0f;
        panel.localScale = Vector3.one * 0.94f;
        panelGroup.DOKill(); panel.DOKill();
        // SetLink: GameObject破棄時にトゥイーンを自動Killし、破棄済みRectTransformへのアクセス警告を防ぐ。
        panelGroup.DOFade(1f, 0.18f).SetUpdate(true).SetLink(panel.gameObject);
        panel.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(panel.gameObject);
    }

    private void Hide()
    {
        panelGroup.DOKill(); panel.DOKill();
        panelGroup.DOFade(0f, 0.14f).SetUpdate(true).SetLink(panel.gameObject);
        panel.DOScale(0.96f, 0.14f).SetUpdate(true).SetLink(panel.gameObject)
            .OnComplete(() => { if (panel != null) SetShown(false); });
    }

    private void BuildCard(NodeOption opt)
    {
        bool ja = LocalizationManager.IsJapanese;

        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        card.transform.SetParent(cardRow, false);
        LayoutElement le = card.GetComponent<LayoutElement>(); le.preferredWidth = 340f; le.preferredHeight = 400f;
        Image bg = card.GetComponent<Image>();
        bg.color = new Color(0.10f, 0.12f, 0.17f, 0.98f);

        // 上部のアーキタイプ色帯。
        GameObject band = new GameObject("Band", typeof(RectTransform), typeof(Image));
        band.transform.SetParent(card.transform, false);
        RectTransform bandR = band.GetComponent<RectTransform>();
        bandR.anchorMin = new Vector2(0f, 1f); bandR.anchorMax = new Vector2(1f, 1f); bandR.pivot = new Vector2(0.5f, 1f);
        bandR.sizeDelta = new Vector2(0f, 84f); bandR.anchoredPosition = Vector2.zero;
        band.GetComponent<Image>().color = opt.tint;

        TextMeshProUGUI titleT = MakeText("CardTitle", band.transform as RectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 70f), 26f, FontStyles.Bold, new Color(0.06f, 0.06f, 0.08f));
        titleT.alignment = TextAlignmentOptions.Center;
        titleT.text = ja ? opt.titleJa : opt.titleEn;

        // アイコン（代表中ボス）。
        if (opt.icon != null)
        {
            GameObject ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            ic.transform.SetParent(card.transform, false);
            RectTransform icr = ic.GetComponent<RectTransform>();
            icr.anchorMin = icr.anchorMax = new Vector2(0.5f, 1f); icr.pivot = new Vector2(0.5f, 1f);
            icr.anchoredPosition = new Vector2(0f, -100f); icr.sizeDelta = new Vector2(150f, 150f);
            Image ici = ic.GetComponent<Image>(); ici.sprite = opt.icon; ici.preserveAspect = true;
        }

        // 難度（★）。
        TextMeshProUGUI diff = MakeText("Diff", card.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(300f, 40f), 24f, FontStyles.Bold, opt.tint);
        diff.alignment = TextAlignmentOptions.Center;
        string stars = new string('★', Mathf.Clamp(opt.difficultyStar, 1, 3)) + new string('☆', Mathf.Max(0, 3 - opt.difficultyStar));
        diff.text = (ja ? "難度 " : "Risk ") + stars;

        // 説明（報酬種別など）。
        TextMeshProUGUI desc = MakeText("Desc", card.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(300f, 90f), 19f, FontStyles.Normal, new Color(0.92f, 0.95f, 1f));
        desc.alignment = TextAlignmentOptions.Center; desc.enableWordWrapping = true;
        desc.text = ja ? opt.descJa : opt.descEn;

        Button btn = card.GetComponent<Button>(); btn.targetGraphic = bg;
        int captured = opt.poolIndex;
        btn.onClick.AddListener(() =>
        {
            AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
            var cb = onPick; onPick = null; // 二重発火防止。
            Hide();
            cb?.Invoke(captured);
        });
        card.AddComponent<ButtonJuice>();
    }

    private TextMeshProUGUI MakeText(string n, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fs, FontStyles style, Color col)
    {
        GameObject o = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
        o.transform.SetParent(parent, false);
        RectTransform r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor; r.pivot = anchor;
        r.anchoredPosition = pos; r.sizeDelta = size;
        TextMeshProUGUI t = o.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(t);
        t.fontSize = fs; t.fontStyle = style; t.color = col; t.raycastTarget = false;
        return t;
    }
}
