using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// R2-rewards: 強化マス報酬UI。まず種別（攻撃/防御/秘力）を選ばせ、選んだら
// 「盤面のマスをクリック」バナーに切り替える。マス確定後 GameManager が Hide() する。
public class BuffTileRewardUI : MonoBehaviour
{
    private const int SortingOrder = 25035;

    public static BuffTileRewardUI Instance { get; private set; }

    private Action<BuffTileType> onChosen;
    private Canvas localCanvas;
    private GameObject choosePanel;
    private GameObject banner;

    private void Awake()
    {
        LocalizationManager.EnsureExists();
        Instance = this;
        EnsureUiParts();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static BuffTileRewardUI EnsureExists()
    {
        if (Instance != null) return Instance;
        BuffTileRewardUI existing = FindObjectOfType<BuffTileRewardUI>(true);
        if (existing != null) { Instance = existing; existing.EnsureUiParts(); return existing; }

        // 独立ルートの ScreenSpaceOverlay Canvas として生成（FindObjectOfType<Canvas> の子にすると
        // ホバー中の AugmentTooltipUI Canvas を親にして点滅・進行不能になるバグを回避）。
        GameObject uiObject = new GameObject("BuffTileRewardUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(BuffTileRewardUI));
        Canvas rootCanvas = uiObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = SortingOrder;
        uiObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        RectTransform rt = uiObject.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        uiObject.GetComponent<Image>().color = new Color(0f, 0.02f, 0.05f, 0.6f);

        Instance = uiObject.GetComponent<BuffTileRewardUI>();
        Instance.EnsureUiParts();
        Instance.gameObject.SetActive(false);
        return Instance;
    }

    public void Show(Action<BuffTileType> chosenCallback)
    {
        EnsureUiParts();
        onChosen = chosenCallback;
        choosePanel.SetActive(true);
        banner.SetActive(false);
        Image ov = GetComponent<Image>();
        ov.raycastTarget = true;                       // 種別選択中は背面クリックを遮る
        ov.color = new Color(0f, 0.02f, 0.05f, 0.6f);  // モーダルとして濃いめに減光
        gameObject.SetActive(true);

        // 出現ポップ（アイテム3択UIと統一）。
        if (choosePanel != null)
        {
            RectTransform pr = choosePanel.transform as RectTransform;
            if (pr != null) { pr.localScale = Vector3.one * 0.94f; pr.DOKill(); pr.DOScale(1f, 0.26f).SetEase(Ease.OutBack).SetUpdate(true); }
            for (int i = 0; i < choosePanel.transform.childCount; i++)
            {
                Transform ch = choosePanel.transform.GetChild(i);
                if (ch.name.EndsWith("Card"))
                {
                    RectTransform r = ch as RectTransform;
                    r.localScale = Vector3.one * 0.7f; r.DOKill();
                    r.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true).SetDelay(0.06f + i * 0.05f);
                }
            }
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        onChosen = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：種別選択パネルが出ていれば先頭種別を自動選択する。
    // 選択後は盤面マス設置待ちになる（GameManager.DebugResolveBuffTilePlacement で解決）。
    public bool DebugChooseFirstType()
    {
        if (!gameObject.activeSelf || choosePanel == null || !choosePanel.activeSelf) return false;
        Choose((BuffTileType)0);
        return true;
    }
#endif

    // 種別が選ばれたら、マスクリック待ちのバナーに切り替える。
    // 重要: overlay の raycast は「切らない」。盤面マスの確定は GameManager.HandleBuffTilePlacementClick が
    // raw Input＋ScreenToWorldPoint（UI非依存）で検出するため overlay を通す必要は無い。
    // 逆に切ると右上のオーグメントHUDやショップが露出し、クリックでツールチップが点滅＋誤クリックで
    // 進行不能になっていた（バグ修正）。視認性のため減光だけ弱め、盤面を見えるようにする。
    private void Choose(BuffTileType type)
    {
        Action<BuffTileType> cb = onChosen;
        onChosen = null;
        choosePanel.SetActive(false);
        banner.SetActive(true);
        Image ov = GetComponent<Image>();
        ov.raycastTarget = true;                        // HUD/ショップを遮断したまま（盤面確定はworld座標で検出）
        ov.color = new Color(0f, 0.02f, 0.05f, 0.18f);  // 盤面が見える程度に薄く
        cb?.Invoke(type);
    }

    private void EnsureUiParts()
    {
        EnsureInputCanvas();
        if (choosePanel != null && banner != null) return;
        bool ja = LocalizationManager.IsJapanese;

        // 種別選択パネル（アイテム3択UIと同じ見た目に統一：card_panel＋タイトル帯＋大きめカード絶対配置）。
        choosePanel = new GameObject("ChoosePanel", typeof(RectTransform), typeof(Image));
        choosePanel.transform.SetParent(transform, false);
        RectTransform pr = choosePanel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = Vector2.zero; pr.sizeDelta = new Vector2(1180f, 660f);
        Image cpImg = choosePanel.GetComponent<Image>();
        Sprite panelSprite = Resources.Load<Sprite>("UI/Augment/card_panel");
        if (panelSprite != null) { cpImg.sprite = panelSprite; cpImg.type = Image.Type.Sliced; cpImg.color = new Color(0.10f, 0.16f, 0.26f, 0.98f); }
        else cpImg.color = new Color(0.04f, 0.07f, 0.12f, 0.97f);

        GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(choosePanel.transform, false);
        RectTransform tbr = titleBar.GetComponent<RectTransform>();
        tbr.anchorMin = new Vector2(0f, 1f); tbr.anchorMax = new Vector2(1f, 1f); tbr.pivot = new Vector2(0.5f, 1f);
        tbr.sizeDelta = new Vector2(0f, 74f); tbr.anchoredPosition = new Vector2(0f, -12f);
        titleBar.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.2f, 0.9f);
        MakeLabel(titleBar.transform, "Title", ja ? "強化マスの種別を選択" : "CHOOSE A TILE BUFF",
            Vector2.zero, Vector2.one, 34f, FontStyles.Bold, new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Center);

        // 3カードを絶対配置（防御=シルバー枠/攻撃=ゴールド枠/秘力=プリズム枠）。
        CreateTypeCard(choosePanel.transform, BuffTileType.Defense, ja ? "防御マス" : "Defense", ja ? "被ダメ -20%／最大HP25%シールド（60s）" : "DR -20% / 25% HP shield (60s)", new Color(0.4f, 0.72f, 1f), "UI/Cards/silver_front", -370f);
        CreateTypeCard(choosePanel.transform, BuffTileType.Attack, ja ? "攻撃マス" : "Attack", ja ? "与ダメ +30%／攻撃速度 ×1.15（60s）" : "DMG +30% / ATKSPD x1.15 (60s)", new Color(1f, 0.62f, 0.28f), "UI/Cards/gold_front", 0f);
        CreateTypeCard(choosePanel.transform, BuffTileType.Arcane, ja ? "秘力マス" : "Arcane", ja ? "マナ獲得 ×1.5／秘力(スキル威力) +35%（60s）" : "Mana gain x1.5 / Skill power +35% (60s)", new Color(0.78f, 0.5f, 1f), "UI/Cards/prism_front", 370f);

        // マスクリック待ちバナー（上部中央）
        banner = new GameObject("Banner", typeof(RectTransform), typeof(Image));
        banner.transform.SetParent(transform, false);
        RectTransform br = banner.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0.5f, 1f); br.anchorMax = new Vector2(0.5f, 1f); br.pivot = new Vector2(0.5f, 1f);
        br.anchoredPosition = new Vector2(0f, -120f); br.sizeDelta = new Vector2(560f, 52f);
        banner.GetComponent<Image>().color = new Color(0.05f, 0.1f, 0.16f, 0.92f);
        MakeLabel(banner.transform, "BannerL", ja ? "強化するマスを盤面からクリック" : "Click a board tile to enhance",
            Vector2.zero, Vector2.one, 20f, FontStyles.Bold, new Color(0.9f, 0.96f, 1f), TextAlignmentOptions.Center);
        banner.SetActive(false);
    }

    private void CreateTypeCard(Transform parent, BuffTileType type, string title, string desc, Color accent, string framePath, float xOffset)
    {
        GameObject card = new GameObject(title + "Card", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.46f); cr.pivot = new Vector2(0.5f, 0.5f);
        cr.anchoredPosition = new Vector2(xOffset, 0f); cr.sizeDelta = new Vector2(320f, 460f);

        Image bg = card.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>(framePath);
        if (frame != null) { bg.sprite = frame; bg.type = Image.Type.Simple; bg.color = Color.white; }
        else bg.color = new Color(0.07f, 0.12f, 0.2f, 1f);
        card.GetComponent<Button>().transition = Selectable.Transition.None;
        card.GetComponent<Button>().onClick.AddListener(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); Choose(type); });
        card.AddComponent<TileCardHover>();

        // 上部：種別帯
        GameObject cat = new GameObject("Cat", typeof(RectTransform), typeof(Image));
        cat.transform.SetParent(card.transform, false);
        RectTransform catR = cat.GetComponent<RectTransform>();
        catR.anchorMin = new Vector2(0.1f, 0.905f); catR.anchorMax = new Vector2(0.9f, 0.965f); catR.offsetMin = Vector2.zero; catR.offsetMax = Vector2.zero;
        Image catImg = cat.GetComponent<Image>(); catImg.color = new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 0.95f); catImg.raycastTarget = false;
        MakeLabel(cat.transform, "CatL", title, Vector2.zero, Vector2.one, 20f, FontStyles.Bold, new Color(1f, 0.98f, 0.92f), TextAlignmentOptions.Center);

        // 中央：色スウォッチ（artifact_frame でアイコン枠風に）
        GameObject frameGo = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        frameGo.transform.SetParent(card.transform, false);
        RectTransform fr = frameGo.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.72f); fr.pivot = new Vector2(0.5f, 0.5f);
        fr.sizeDelta = new Vector2(150f, 150f); fr.anchoredPosition = Vector2.zero;
        Image frImg = frameGo.GetComponent<Image>();
        Sprite af = Resources.Load<Sprite>("UI/ItemBench/artifact_frame");
        if (af != null) { frImg.sprite = af; frImg.type = Image.Type.Simple; frImg.preserveAspect = true; }
        frImg.color = accent; frImg.raycastTarget = false;

        // 名前＋効果（スクリム上）
        GameObject scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
        scrim.transform.SetParent(card.transform, false);
        RectTransform scR = scrim.GetComponent<RectTransform>();
        scR.anchorMin = new Vector2(0.085f, 0.07f); scR.anchorMax = new Vector2(0.915f, 0.555f); scR.offsetMin = Vector2.zero; scR.offsetMax = Vector2.zero;
        Image scImg = scrim.GetComponent<Image>(); scImg.color = new Color(0f, 0.01f, 0.03f, 0.66f); scImg.raycastTarget = false;

        MakeLabel(scrim.transform, "D", desc, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.96f), 16f, FontStyles.Normal, new Color(0.88f, 0.94f, 1f), TextAlignmentOptions.Center).enableWordWrapping = true;
    }

    // カードのホバー拡大（ポーズ非依存）。
    private class TileCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData e) { var r = transform as RectTransform; if (r != null) { r.DOKill(); r.DOScale(1.06f, 0.12f).SetUpdate(true); } }
        public void OnPointerExit(PointerEventData e) { var r = transform as RectTransform; if (r != null) { r.DOKill(); r.DOScale(1f, 0.12f).SetUpdate(true); } }
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

    private void EnsureInputCanvas()
    {
        if (localCanvas == null) localCanvas = GetComponent<Canvas>();
        if (localCanvas == null) localCanvas = gameObject.AddComponent<Canvas>();
        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = SortingOrder;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }
}
