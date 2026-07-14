using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// R2-rewards: 中ボス報酬の「アイテム3択」UI。防具/攻撃/秘力カテゴリから1つずつ提示し、1つ選ばせる。
// 2026-06-06: オーグメント選択(AugmentSelectionUI)に寄せて、全画面ディム＋大きめカードを広く絶対配置するレイアウトへ刷新。
public class ItemRewardSelectionUI : MonoBehaviour
{
    private const int SortingOrder = 25030; // ボス選択(25020)より少し前面・16bit内。

    public static ItemRewardSelectionUI Instance { get; private set; }

    // オーグメント選択に揃えたカード配置・寸法。
    private static readonly float[] CardXs = { -370f, 0f, 370f };
    private const float CardWidth = 320f;
    private const float CardHeight = 460f;

    private Action<ItemData> onSelected;
    private RectTransform panelRect;
    private Canvas localCanvas;
    private readonly List<GameObject> optionObjects = new List<GameObject>();

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

    public static ItemRewardSelectionUI EnsureExists()
    {
        if (Instance != null) return Instance;
        ItemRewardSelectionUI existing = FindObjectOfType<ItemRewardSelectionUI>(true);
        if (existing != null) { Instance = existing; existing.EnsureUiParts(); return existing; }

        // 独立ルートの ScreenSpaceOverlay Canvas として生成（FindObjectOfType<Canvas> の子にすると
        // ホバー中の AugmentTooltipUI Canvas を親にして点滅・進行不能になるバグを回避）。
        GameObject uiObject = new GameObject("ItemRewardSelectionUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(ItemRewardSelectionUI));
        Canvas rootCanvas = uiObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = SortingOrder;
        // 解像度差で寸法が破綻しないよう、参照解像度1920×1080へスケールさせる。
        CanvasScaler scaler = uiObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform rt = uiObject.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Image overlay = uiObject.GetComponent<Image>();
        overlay.color = new Color(0f, 0.02f, 0.05f, 0.82f); // 全画面ディム（背面を確実に遮断）
        overlay.raycastTarget = true;

        Instance = uiObject.GetComponent<ItemRewardSelectionUI>();
        Instance.EnsureUiParts();
        Instance.gameObject.SetActive(false);
        return Instance;
    }

    public void Show(IReadOnlyList<ItemData> options, Action<ItemData> selectedCallback)
    {
        EnsureUiParts();
        ClearOptions();
        onSelected = selectedCallback;
        if (options != null)
            for (int i = 0; i < options.Count; i++)
            {
                float x = options.Count == 3 ? CardXs[i] : (i - (options.Count - 1) * 0.5f) * (CardWidth + 50f);
                CreateOption(options[i], i, x);
            }
        gameObject.SetActive(true);
        PlayAppear();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
        onSelected = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：開いていれば先頭候補を自動選択する。開いていなければfalse。
    public bool DebugAutoResolve()
    {
        if (!gameObject.activeSelf || optionObjects.Count == 0) return false;
        Button b = optionObjects[0] != null ? optionObjects[0].GetComponent<Button>() : null;
        if (b == null) return false;
        b.onClick.Invoke();
        return true;
    }
#endif

    private void ClearOptions()
    {
        for (int i = 0; i < optionObjects.Count; i++)
            if (optionObjects[i] != null)
            {
                optionObjects[i].SetActive(false);
                optionObjects[i].transform.SetParent(null, false); // 遅延破棄でも即レイアウト/当たり判定から除外
                Destroy(optionObjects[i]);
            }
        optionObjects.Clear();
    }

    // パネル＋カードの出現演出（スケールポップ）。ポーズ非依存で動かす。
    private void PlayAppear()
    {
        if (panelRect != null)
        {
            panelRect.localScale = Vector3.one * 0.94f;
            panelRect.DOKill();
            panelRect.DOScale(1f, 0.26f).SetEase(Ease.OutBack).SetUpdate(true);
        }
        for (int i = 0; i < optionObjects.Count; i++)
        {
            RectTransform r = optionObjects[i] != null ? optionObjects[i].transform as RectTransform : null;
            if (r == null) continue;
            Vector3 baseScale = r.localScale;
            r.localScale = baseScale * 0.7f;
            r.DOKill();
            r.DOScale(baseScale, 0.32f).SetEase(Ease.OutBack).SetUpdate(true).SetDelay(0.08f + i * 0.08f);
        }
    }

    private void CreateOption(ItemData item, int index, float xOffset)
    {
        bool ja = LocalizationManager.IsJapanese;
        Color accent = CategoryColor(item.category);

        // カード根（絶対配置）。背景はカテゴリ別のカード枠スプライト。
        GameObject card = new GameObject("ItemRewardOption", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(panelRect, false);
        optionObjects.Add(card);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.46f); cr.pivot = new Vector2(0.5f, 0.5f);
        cr.anchoredPosition = new Vector2(xOffset, 0f);
        cr.sizeDelta = new Vector2(CardWidth, CardHeight);

        Image bg = card.GetComponent<Image>();
        Sprite frameSprite = CategoryFrameSprite(item.category);
        if (frameSprite != null) { bg.sprite = frameSprite; bg.type = Image.Type.Simple; bg.color = Color.white; }
        else bg.color = new Color(0.07f, 0.12f, 0.2f, 1f);

        Button btn = card.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { AttackEffectPlayer.PlayUiSfx("sfx_ui_select"); if (onSelected != null) { Action<ItemData> cb = onSelected; onSelected = null; cb.Invoke(item); Hide(); } });
        card.AddComponent<CardHover>();

        // 上部：カテゴリ帯。
        GameObject cat = new GameObject("Cat", typeof(RectTransform), typeof(Image));
        cat.transform.SetParent(card.transform, false);
        RectTransform catR = cat.GetComponent<RectTransform>();
        catR.anchorMin = new Vector2(0.1f, 0.905f); catR.anchorMax = new Vector2(0.9f, 0.965f);
        catR.offsetMin = Vector2.zero; catR.offsetMax = Vector2.zero;
        Image catImg = cat.GetComponent<Image>();
        catImg.color = new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 0.95f);
        catImg.raycastTarget = false;
        MakeLabel(cat.transform, "CatL", LocalizationManager.ItemCategoryLabel(item.category), Vector2.zero, Vector2.one, 20f, FontStyles.Bold, new Color(1f, 0.98f, 0.92f), TextAlignmentOptions.Center);

        // アイコン枠（artifact_frame・カテゴリ色）＋アイテムアイコン。大きめに、カード上寄り。
        GameObject frameGo = new GameObject("IconFrame", typeof(RectTransform), typeof(Image));
        frameGo.transform.SetParent(card.transform, false);
        RectTransform fr = frameGo.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.72f); fr.pivot = new Vector2(0.5f, 0.5f);
        fr.sizeDelta = new Vector2(168f, 168f); fr.anchoredPosition = new Vector2(10f, 0f); // 枠を10px右
        Image frameImg = frameGo.GetComponent<Image>();
        Sprite af = LoadSprite("UI/ItemBench/artifact_frame");
        // artifact_frame は 9-slice ボーダー未設定なので Simple で表示（Sliced だと「border 無し」警告＋実質Simple動作）。
        if (af != null) { frameImg.sprite = af; frameImg.type = Image.Type.Simple; frameImg.preserveAspect = true; frameImg.color = accent; }
        else frameImg.color = new Color(0f, 0f, 0f, 0.35f);
        frameImg.raycastTarget = false;

        GameObject ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        ic.transform.SetParent(frameGo.transform, false);
        RectTransform icr = ic.GetComponent<RectTransform>();
        icr.anchorMin = icr.anchorMax = new Vector2(0.5f, 0.5f); icr.pivot = new Vector2(0.5f, 0.5f);
        // アイコンを大きめに（枠いっぱい）。透過パディング込みでも見栄えするサイズ。
        // 枠基準で10px左（枠は10px右なので、見た目はアイコンが中央寄り・枠だけ右にずれる）。
        icr.sizeDelta = new Vector2(180f, 180f); icr.anchoredPosition = new Vector2(-10f, 0f);
        Image icimg = ic.GetComponent<Image>();
        icimg.sprite = item.Icon; icimg.preserveAspect = true; icimg.raycastTarget = false;
        icimg.color = item.Icon != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);

        // 名前＋効果テキスト用スクリム（暗幕）。カード幅いっぱい近くまで使って広く見せる。
        GameObject scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
        scrim.transform.SetParent(card.transform, false);
        RectTransform scR = scrim.GetComponent<RectTransform>();
        scR.anchorMin = new Vector2(0.085f, 0.07f); scR.anchorMax = new Vector2(0.915f, 0.555f);
        scR.offsetMin = Vector2.zero; scR.offsetMax = Vector2.zero;
        Image scImg = scrim.GetComponent<Image>(); scImg.color = new Color(0f, 0.01f, 0.03f, 0.66f); scImg.raycastTarget = false;

        // 名前（スクリム上部）。長名は自動縮小。
        TextMeshProUGUI nm = MakeLabel(scrim.transform, "Name", LocalizationManager.ItemName(item), new Vector2(0.04f, 0.8f), new Vector2(0.96f, 0.99f), 22f, FontStyles.Bold, new Color(1f, 0.98f, 0.92f), TextAlignmentOptions.Center);
        nm.enableWordWrapping = false; nm.enableAutoSizing = true; nm.fontSizeMin = 14f; nm.fontSizeMax = 22f;

        // 効果（名前の下・折返し）。
        TextMeshProUGUI eff = MakeLabel(scrim.transform, "Eff", LocalizationManager.BuildItemEffectText(item, true), new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.76f), 16f, FontStyles.Normal, new Color(0.88f, 0.94f, 1f), TextAlignmentOptions.Top);
        eff.enableWordWrapping = true; eff.lineSpacing = 8f;
    }

    private Sprite CategoryFrameSprite(ItemCategory c)
    {
        switch (c)
        {
            case ItemCategory.Defense: return LoadSprite("UI/Cards/silver_front"); // 防具＝青系
            case ItemCategory.Offense: return LoadSprite("UI/Cards/gold_front");   // 攻撃＝金系
            default: return LoadSprite("UI/Cards/prism_front");                     // 秘力＝紫系
        }
    }

    private Color CategoryColor(ItemCategory c)
    {
        switch (c)
        {
            case ItemCategory.Defense: return new Color(0.4f, 0.72f, 1f);
            case ItemCategory.Offense: return new Color(1f, 0.62f, 0.28f);
            default: return new Color(0.78f, 0.5f, 1f); // Skill=秘力
        }
    }

    private static Sprite LoadSprite(string path) => Resources.Load<Sprite>(path);

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

    private void EnsureUiParts()
    {
        EnsureInputCanvas();
        if (panelRect != null) return;

        bool ja = LocalizationManager.IsJapanese;

        // パネル（オーグメント選択に合わせた大きめ枠）。カード(-370/0/370・幅320)を余裕で収める。
        GameObject panel = new GameObject("ItemRewardPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f); panelRect.anchorMax = new Vector2(0.5f, 0.5f); panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero; panelRect.sizeDelta = new Vector2(1180f, 660f);
        Image panelImg = panel.GetComponent<Image>();
        Sprite panelSprite = LoadSprite("UI/Augment/card_panel");
        if (panelSprite != null) { panelImg.sprite = panelSprite; panelImg.type = Image.Type.Sliced; panelImg.color = new Color(0.10f, 0.16f, 0.26f, 0.98f); }
        else panelImg.color = new Color(0.04f, 0.07f, 0.12f, 0.97f);

        // タイトル帯（上部）。
        GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(panel.transform, false);
        RectTransform tbr = titleBar.GetComponent<RectTransform>();
        tbr.anchorMin = new Vector2(0f, 1f); tbr.anchorMax = new Vector2(1f, 1f); tbr.pivot = new Vector2(0.5f, 1f);
        tbr.sizeDelta = new Vector2(0f, 74f); tbr.anchoredPosition = new Vector2(0f, -12f);
        titleBar.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.2f, 0.9f);
        MakeLabel(titleBar.transform, "Title", ja ? "アイテムを1つ選択" : "CHOOSE AN ITEM",
            Vector2.zero, Vector2.one, 36f, FontStyles.Bold, new Color(0.7f, 0.9f, 1f), TextAlignmentOptions.Center);
    }

    private void EnsureInputCanvas()
    {
        if (localCanvas == null) localCanvas = GetComponent<Canvas>();
        if (localCanvas == null) localCanvas = gameObject.AddComponent<Canvas>();
        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = SortingOrder;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }

    // カードのホバー拡大演出（ポーズ非依存）。
    private class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData e)
        {
            RectTransform r = transform as RectTransform; if (r == null) return;
            r.DOKill(); r.DOScale(1.06f, 0.12f).SetUpdate(true);
        }
        public void OnPointerExit(PointerEventData e)
        {
            RectTransform r = transform as RectTransform; if (r == null) return;
            r.DOKill(); r.DOScale(1f, 0.12f).SetUpdate(true);
        }
    }
}
