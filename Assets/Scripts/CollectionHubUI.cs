using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AutoChessBossRush.Save;

// R4-collection-hub: コレクション＋ショップ選抜ハブ。
// ロビーの「ユニット編成」カード／出撃準備の「ショップに出すユニットを選ぶ」から開く。
// ショップ抽選の母集合になりうる通常ユニットをコスト別に並べ、各ユニットを「ショップに出す/出さない」で恒久トグルする。
// 設定は SaveManager.SetShopUnitEnabled でスロットに永続化され、GameManager.IsEntityUnlockedForShop が抽選時に参照する。
public class CollectionHubUI : MonoBehaviour
{
    // ロビーUI(25050)・設定/章プロローグ(26000)より前面に出す必要がある（低いとロビーの裏に隠れて「押しても遷移しない」ように見える）。
    private const int SortingOrder = 26500; // 16bit short上限(32767)内。
    // 各コストで最低この数は「ショップに出す」に残す（枝枯れ＆簡単★3量産の防止）。index=cost(1..5)。
    // ★3は同じユニットを引き続けるほど簡単になるため、低コストほど多めに残す。母集合がこの数未満なら全部維持。
    private static readonly int[] MinEnabledByCost = { 0, 9, 8, 7, 6, 5 };

    public static CollectionHubUI Instance { get; private set; }

    private Action onClose;
    private Canvas localCanvas;
    private Transform contentParent;        // スクロール内容（コスト別セクションを縦に積む）。
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI backLabel;

    // コスト(1..5)別の母集合。再描画・最低数判定に使う。
    private readonly List<EntitiesDatabaseSO.EntityData>[] candidatesByCost = new List<EntitiesDatabaseSO.EntityData>[6];
    // カード更新クロージャ（言語切替・トグル時の再描画用）。
    private readonly List<Action> cardRefreshers = new List<Action>();

    private void Awake()
    {
        LocalizationManager.EnsureExists();
        Instance = this;
        gameObject.SetActive(false);
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    public static CollectionHubUI EnsureExists()
    {
        if (Instance != null) return Instance;

        CollectionHubUI existing = FindObjectOfType<CollectionHubUI>(true);
        if (existing != null) { Instance = existing; return existing; }

        // 独立ルートの ScreenSpaceOverlay Canvas として生成（他UIの子に入れない）。
        GameObject root = new GameObject("CollectionHubUI",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(CollectionHubUI));
        Canvas c = root.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = SortingOrder;
        root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        Image overlay = root.GetComponent<Image>();
        overlay.color = new Color(0f, 0.02f, 0.05f, 0.86f);
        overlay.raycastTarget = true;

        Instance = root.GetComponent<CollectionHubUI>();
        Instance.gameObject.SetActive(false);
        return Instance;
    }

    public void Open(Action closeCallback)
    {
        onClose = closeCallback;
        EnsureUiParts();
        Populate();
        gameObject.SetActive(true);
    }

    private void Close()
    {
        Action cb = onClose; onClose = null;
        gameObject.SetActive(false);
        cb?.Invoke();
    }

    private void OnLanguageChanged()
    {
        if (!gameObject.activeSelf) return;
        ApplyStaticText();
        Populate();
    }

    private void ApplyStaticText()
    {
        bool ja = LocalizationManager.IsJapanese;
        if (titleText != null)
        {
            LocalizationManager.ApplyFont(titleText);
            titleText.text = ja ? "コレクション ・ ショップ選抜" : "COLLECTION ・ SHOP POOL";
        }
        if (hintText != null)
        {
            LocalizationManager.ApplyFont(hintText);
            hintText.text = ja
                ? "クリックで「ショップに出す/出さない」を切替。簡単な★3量産を防ぐため、各コストは一定数以上を残す必要があります。"
                : "Click to toggle a unit in/out of the shop. Each cost must keep a minimum enabled (prevents easy 3-star spam).";
        }
        if (backLabel != null)
        {
            LocalizationManager.ApplyFont(backLabel);
            backLabel.text = ja ? "戻る" : "BACK";
        }
    }

    // ====== 構築 ======
    private void EnsureUiParts()
    {
        EnsureInputCanvas();
        if (contentParent != null) { ApplyStaticText(); return; }

        bool ja = LocalizationManager.IsJapanese;

        // 中央の大パネル。
        GameObject panel = new GameObject("HubPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.08f, 0.08f);
        pr.anchorMax = new Vector2(0.92f, 0.95f);
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.01f, 0.04f, 0.08f, 0.96f);

        // タイトル。
        titleText = MakeText(panel.transform, new Vector2(0f, 0.92f), new Vector2(1f, 1f),
            32f, FontStyles.Bold, new Color(0.74f, 0.92f, 1f, 1f), TextAlignmentOptions.Center);

        // ヒント。
        hintText = MakeText(panel.transform, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.915f),
            17f, FontStyles.Normal, new Color(0.78f, 0.9f, 1f, 0.85f), TextAlignmentOptions.Center);

        // スクロール領域。
        GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(panel.transform, false);
        RectTransform sr = scrollGo.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.02f, 0.1f);
        sr.anchorMax = new Vector2(0.98f, 0.85f);
        sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 28f;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollGo.transform, false);
        RectTransform crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = new Vector2(0f, 0f); crt.offsetMax = new Vector2(0f, 0f);
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 14f; vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = crt;
        scroll.viewport = sr;
        contentParent = content.transform;

        // 戻るボタン。
        GameObject back = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        back.transform.SetParent(panel.transform, false);
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0.5f, 0f); br.anchorMax = new Vector2(0.5f, 0f);
        br.pivot = new Vector2(0.5f, 0f); br.anchoredPosition = new Vector2(0f, 18f);
        br.sizeDelta = new Vector2(260f, 46f);
        Image backImg = back.GetComponent<Image>();
        backImg.color = new Color(0.16f, 0.22f, 0.32f, 0.95f);
        Button backBtn = back.GetComponent<Button>();
        backBtn.targetGraphic = backImg;
        backBtn.onClick.AddListener(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); Close(); });
        backLabel = MakeText(back.transform, Vector2.zero, Vector2.one, 20f, FontStyles.Bold,
            new Color(0.86f, 0.92f, 1f, 0.95f), TextAlignmentOptions.Center);

        ApplyStaticText();
    }

    private void EnsureInputCanvas()
    {
        if (localCanvas == null) localCanvas = GetComponent<Canvas>();
        if (localCanvas == null) localCanvas = gameObject.AddComponent<Canvas>();
        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = SortingOrder;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }

    // ====== 内容（コスト別） ======
    private void Populate()
    {
        if (contentParent == null) return;

        // 既存内容をクリア。
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);
        cardRefreshers.Clear();

        // 母集合をコスト別に集計。
        for (int c = 0; c <= 5; c++) candidatesByCost[c] = new List<EntitiesDatabaseSO.EntityData>();
        EntitiesDatabaseSO db = Resources.Load<EntitiesDatabaseSO>("Entity Database");
        if (db != null && db.allEntities != null)
        {
            for (int i = 0; i < db.allEntities.Count; i++)
            {
                EntitiesDatabaseSO.EntityData e = db.allEntities[i];
                // チャプター内だけ仲間化される一時解放ユニットは表示しない（恒久プールのみ管理）。
                if (!GameManager.IsHubManageableUnit(e)) continue;
                candidatesByCost[e.cost].Add(e);
            }
        }

        bool ja = LocalizationManager.IsJapanese;
        for (int cost = 1; cost <= 5; cost++)
        {
            List<EntitiesDatabaseSO.EntityData> list = candidatesByCost[cost];
            if (list.Count == 0) continue;
            BuildCostSection(cost, list, ja);
        }
    }

    private void BuildCostSection(int cost, List<EntitiesDatabaseSO.EntityData> list, bool ja)
    {
        // セクション見出し。
        GameObject header = new GameObject($"Cost{cost}Header", typeof(RectTransform), typeof(LayoutElement));
        header.transform.SetParent(contentParent, false);
        header.GetComponent<LayoutElement>().preferredHeight = 36f;
        TextMeshProUGUI h = MakeText(header.transform, Vector2.zero, Vector2.one, 22f, FontStyles.Bold,
            CostColor(cost), TextAlignmentOptions.Left);
        h.text = ja ? $"コスト {cost}" : $"COST {cost}";

        // カードを並べるグリッド。
        GameObject grid = new GameObject($"Cost{cost}Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        grid.transform.SetParent(contentParent, false);
        GridLayoutGroup g = grid.GetComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(150f, 188f);
        g.spacing = new Vector2(12f, 12f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 7;
        g.childAlignment = TextAnchor.UpperLeft;
        ContentSizeFitter gcsf = grid.GetComponent<ContentSizeFitter>();
        gcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < list.Count; i++)
            BuildUnitCard(grid.transform, list[i], cost);
    }

    private void BuildUnitCard(Transform parent, EntitiesDatabaseSO.EntityData entity, int cost)
    {
        GameObject card = new GameObject($"{entity.name}Card", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);
        Image frame = card.GetComponent<Image>();
        UnitCardVisual.ApplyProceduralFrame(frame, entity.cost); // R5: AI枠撤去→プログラム枠。
        frame.preserveAspect = false;

        // アイコン。
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(card.transform, false);
        RectTransform ir = iconGo.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.1f, 0.34f); ir.anchorMax = new Vector2(0.9f, 0.92f);
        ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        Image icon = iconGo.GetComponent<Image>();
        icon.raycastTarget = false;
        // R5: AIキャラ絵をやめ、ユニットのドット絵（盤外実体ミラー・盤面と非連動）を表示。
        iconGo.AddComponent<UnitCardPreview>().Bind(entity);

        // 名前。
        TextMeshProUGUI nameText = MakeText(card.transform, new Vector2(0.04f, 0.2f), new Vector2(0.96f, 0.34f),
            14f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 11f; nameText.fontSizeMax = 18f;
        nameText.text = LocalizationManager.IsJapanese ? LocalizationManager.UnitName(entity.name) : entity.name;
        nameText.outlineWidth = 0.18f; nameText.outlineColor = Color.black;

        // 状態ラベル（ON/OFF）。
        TextMeshProUGUI stateText = MakeText(card.transform, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.19f),
            14f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

        // カード見た目の更新。
        Action refresh = () =>
        {
            bool ja = LocalizationManager.IsJapanese;
            bool on = SaveManager.Instance == null || SaveManager.Instance.IsShopUnitEnabled(entity.name);
            frame.color = on ? new Color(0.62f, 0.85f, 1f, 1f) : new Color(0.28f, 0.3f, 0.36f, 1f);
            icon.color = on ? Color.white : new Color(0.5f, 0.5f, 0.55f, 0.7f);
            stateText.color = on ? new Color(0.6f, 1f, 0.7f, 1f) : new Color(1f, 0.55f, 0.5f, 1f);
            stateText.text = on ? (ja ? "出す" : "IN") : (ja ? "除外" : "OUT");
        };
        refresh();
        cardRefreshers.Add(refresh);

        Button btn = card.GetComponent<Button>();
        btn.targetGraphic = frame;
        RectTransform cardRt = card.GetComponent<RectTransform>();
        btn.onClick.AddListener(() => OnToggleUnit(entity, cost, refresh, cardRt));
    }

    private void OnToggleUnit(EntitiesDatabaseSO.EntityData entity, int cost, Action refresh, RectTransform cardRt)
    {
        if (SaveManager.Instance == null) return;
        bool on = SaveManager.Instance.IsShopUnitEnabled(entity.name);
        if (on)
        {
            // OFF にしようとしている：最低数を割るなら拒否。
            int total = candidatesByCost[cost] != null ? candidatesByCost[cost].Count : 0;
            int target = (cost >= 1 && cost <= 5) ? MinEnabledByCost[cost] : 2;
            int min = Mathf.Min(target, total);
            int enabled = CountEnabled(cost);
            if (enabled - 1 < min)
            {
                FlashDenied(cardRt);
                AttackEffectPlayer.PlayUiSfx("sfx_ui_error");
                if (hintText != null)
                    hintText.text = LocalizationManager.IsJapanese
                        ? $"コスト{cost}は最低{min}体必要です。これ以上は外せません。"
                        : $"Cost {cost} needs at least {min} units. Can't remove more.";
                return;
            }
            SaveManager.Instance.SetShopUnitEnabled(entity.name, false);
        }
        else
        {
            SaveManager.Instance.SetShopUnitEnabled(entity.name, true);
        }
        AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
        refresh();
    }

    private int CountEnabled(int cost)
    {
        List<EntitiesDatabaseSO.EntityData> list = candidatesByCost[cost];
        if (list == null) return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (SaveManager.Instance == null || SaveManager.Instance.IsShopUnitEnabled(list[i].name)) n++;
        return n;
    }

    private void FlashDenied(RectTransform cardRt)
    {
        if (cardRt == null) return;
        Image img = cardRt.GetComponent<Image>();
        if (img == null) return;
        img.color = new Color(1f, 0.35f, 0.3f, 1f);
        // 0.25秒後に通常色へ戻す（Populateで再構築されないよう Invoke で簡易復帰）。
        CancelInvoke(nameof(RefreshAllCards));
        Invoke(nameof(RefreshAllCards), 0.25f);
    }

    private void RefreshAllCards()
    {
        for (int i = 0; i < cardRefreshers.Count; i++) cardRefreshers[i]?.Invoke();
    }

    // ====== ヘルパ ======
    private static Color CostColor(int cost)
    {
        switch (cost)
        {
            case 1: return new Color(0.78f, 0.82f, 0.88f, 1f);
            case 2: return new Color(0.55f, 0.85f, 0.6f, 1f);
            case 3: return new Color(0.5f, 0.7f, 1f, 1f);
            case 4: return new Color(0.78f, 0.6f, 1f, 1f);
            default: return new Color(1f, 0.78f, 0.4f, 1f);
        }
    }

    private TextMeshProUGUI MakeText(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(t);
        t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = align; t.raycastTarget = false;
        return t;
    }
}
