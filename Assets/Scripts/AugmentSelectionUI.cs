using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// オーグメントを3択から選ばせるUIです。各カードに1回だけリロール権があり、被りなしで抽選します。
public class AugmentSelectionUI : MonoBehaviour
{
    public static AugmentSelectionUI Instance { get; private set; }

    private const int CardCount = 3;

    private RectTransform panelRect;
    private CanvasGroup panelGroup;
    private GameObject dimObject;
    private TextMeshProUGUI titleText;

    private readonly Image[] cardBackgrounds = new Image[CardCount]; // 表面のアーティファクト画像
    private readonly Image[] cardBorders = new Image[CardCount];
    private readonly RectTransform[] cardRoots = new RectTransform[CardCount]; // 回転する要素
    private readonly Image[] cardBacks = new Image[CardCount];               // 裏面画像
    private readonly GameObject[] cardFronts = new GameObject[CardCount];     // 表面コンテナ（180°回転済み）
    private readonly TextMeshProUGUI[] tierLabels = new TextMeshProUGUI[CardCount];
    private readonly TextMeshProUGUI[] nameLabels = new TextMeshProUGUI[CardCount];
    private readonly TextMeshProUGUI[] descLabels = new TextMeshProUGUI[CardCount];
    private readonly Button[] selectButtons = new Button[CardCount];
    private readonly TextMeshProUGUI[] selectButtonTexts = new TextMeshProUGUI[CardCount];
    private readonly Button[] rerollButtons = new Button[CardCount];
    private readonly TextMeshProUGUI[] rerollButtonTexts = new TextMeshProUGUI[CardCount];

    private readonly AugmentDefinition[] currentCards = new AugmentDefinition[CardCount];
    private readonly bool[] rerollUsed = new bool[CardCount];

    private AugmentTier currentTier;
    private HashSet<string> exclusionIds = new HashSet<string>();
    private Action<AugmentDefinition> onPicked;
    private bool isOpen;
    private bool isBuilt;
    private float previousTimeScale = 1f;

    public static AugmentSelectionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        AugmentSelectionUI existing = FindObjectOfType<AugmentSelectionUI>();
        if (existing != null)
        {
            Instance = existing;
            existing.BuildIfNeeded();
            return existing;
        }

        GameObject go = new GameObject("AugmentSelectionUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AugmentSelectionUI));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 26000; // 16bit short上限(32767)内。
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        Instance = go.GetComponent<AugmentSelectionUI>();
        Instance.BuildIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        LocalizationManager.EnsureExists();
        BuildIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        LocalizationManager.OnLanguageChanged -= RefreshLanguage;
    }

    private void BuildIfNeeded()
    {
        if (isBuilt)
            return;
        BuildUi();
        SetOpenInstant(false);
        LocalizationManager.OnLanguageChanged += RefreshLanguage;
        isBuilt = true;
    }

    // ティアと除外リストを受け取り、3枚抽選してUIを開きます。pickedCallbackには選ばれたオーグメントが渡ります。
    public void Show(AugmentTier tier, IEnumerable<string> exclusions, Action<AugmentDefinition> pickedCallback)
    {
        BuildIfNeeded();
        currentTier = tier;
        exclusionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (exclusions != null)
        {
            foreach (string id in exclusions)
                if (!string.IsNullOrEmpty(id)) exclusionIds.Add(id);
        }
        onPicked = pickedCallback;

        DrawInitialCards();
        for (int i = 0; i < CardCount; i++)
            rerollUsed[i] = false;
        RefreshCards();
        SetOpen(true);
        PlayFlipAll();
    }

    private void DrawInitialCards()
    {
        List<AugmentDefinition> pool = BuildPool();
        for (int i = 0; i < CardCount; i++)
        {
            if (pool.Count == 0) { currentCards[i] = null; continue; }
            int idx = UnityEngine.Random.Range(0, pool.Count);
            currentCards[i] = pool[idx];
            pool.RemoveAt(idx);
        }
    }

    private List<AugmentDefinition> BuildPool()
    {
        List<AugmentDefinition> pool = new List<AugmentDefinition>();
        IReadOnlyList<AugmentDefinition> source = AugmentCatalog.ByTier(currentTier);
        for (int i = 0; i < source.Count; i++)
        {
            AugmentDefinition aug = source[i];
            if (aug == null) continue;
            if (exclusionIds.Contains(aug.Id)) continue;
            pool.Add(aug);
        }
        return pool;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：選択UIが開いているか。
    public bool DebugIsOpen => isOpen;

    // オートプレイ(debug)用：開いていれば先頭の有効カードを自動選択する。開いていなければfalse。
    public bool DebugAutoResolve()
    {
        if (!isOpen) return false;
        for (int i = 0; i < CardCount; i++)
            if (currentCards[i] != null) { OnSelectClicked(i); return true; }
        return false;
    }
#endif

    private void OnRerollClicked(int index)
    {
        if (rerollUsed[index])
            return;
        rerollUsed[index] = true;

        HashSet<string> currentlyShown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < CardCount; i++)
        {
            if (i == index) continue;
            if (currentCards[i] != null) currentlyShown.Add(currentCards[i].Id);
        }

        List<AugmentDefinition> pool = BuildPool();
        // 既に他のカードで表示中のものは除外。
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (currentlyShown.Contains(pool[i].Id))
                pool.RemoveAt(i);
        }

        if (pool.Count > 0)
        {
            currentCards[index] = pool[UnityEngine.Random.Range(0, pool.Count)];
        }
        // pool が空の場合は現在のカードを維持。
        AttackEffectPlayer.PlayUiSfx("shop_reroll");
        RefreshCards();
        PlayFlipCard(index, 0f); // リロールしたカードを再フリップ
    }

    private void OnSelectClicked(int index)
    {
        AugmentDefinition aug = currentCards[index];
        if (aug == null) return;

        // 今回見せた3枚はすべて「このチャプターで見た」扱いに（次の選択で重複しないように）。
        if (GameManager.Instance != null)
        {
            for (int i = 0; i < CardCount; i++)
                if (currentCards[i] != null)
                    GameManager.Instance.ShownAugmentIds.Add(currentCards[i].Id);
        }

        Action<AugmentDefinition> callback = onPicked;
        onPicked = null;
        AttackEffectPlayer.PlayUiSfx("unit_buy");
        SetOpen(false);
        callback?.Invoke(aug);
    }

    private void SetOpenInstant(bool open)
    {
        isOpen = open;
        if (dimObject != null) dimObject.SetActive(open);
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(open);
            if (panelGroup != null) panelGroup.alpha = open ? 1f : 0f;
            panelRect.localScale = Vector3.one;
        }
        if (!open) Time.timeScale = OptionsPanelUI.DesiredGameSpeed; // チャプター全体で倍速を維持
    }

    private void SetOpen(bool open)
    {
        if (panelRect == null)
            return;
        if (isOpen == open)
            return;

        isOpen = open;
        // 連続イベント（例: Silver → 即 Gold）で前回の閉じる Tween の OnComplete が
        // 再表示後に発火してパネルを隠してしまう事故を防ぐため、必ず既存 Tween を kill する。
        panelGroup.DOKill();
        panelRect.DOKill();

        if (open)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (dimObject != null) dimObject.SetActive(true);
            panelRect.gameObject.SetActive(true);
            panelGroup.alpha = 0f;
            panelRect.localScale = Vector3.one * 0.92f;
            panelGroup.DOFade(1f, 0.22f).SetUpdate(true);
            panelRect.DOScale(1f, 0.30f).SetEase(Ease.OutBack).SetUpdate(true);
        }
        else
        {
            Time.timeScale = OptionsPanelUI.DesiredGameSpeed; // チャプター全体で倍速を維持
            panelGroup.DOFade(0f, 0.16f).SetUpdate(true);
            panelRect.DOScale(0.94f, 0.16f).SetUpdate(true).OnComplete(() =>
            {
                // 閉じ Tween 完了時、その間に再オープンされていたら hide しない。
                if (isOpen) return;
                panelRect.gameObject.SetActive(false);
                if (dimObject != null) dimObject.SetActive(false);
            });
        }
    }

    private void BuildUi()
    {
        // 暗幕
        dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimObject.transform.SetParent(transform, false);
        RectTransform dimRect = dimObject.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.sizeDelta = Vector2.zero;
        dimRect.anchoredPosition = Vector2.zero;
        dimObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        // パネル本体
        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObj.transform.SetParent(transform, false);
        panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(960f, 540f);
        panelObj.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.07f, 0.97f);
        panelGroup = panelObj.GetComponent<CanvasGroup>();

        titleText = CreateText("Title", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(900f, 36f), 26f, FontStyles.Bold, Color.white);
        titleText.alignment = TextAlignmentOptions.Center;

        // 3カード（中央寄せで横一列）
        float[] cardXs = new float[] { -310f, 0f, 310f };
        for (int i = 0; i < CardCount; i++)
        {
            int idx = i; // closure capture
            BuildCard(panelRect, cardXs[i], idx);
        }
    }

    private void BuildCard(RectTransform parent, float xOffset, int index)
    {
        // ルート（フリップで回転する要素）
        GameObject cardObj = new GameObject($"Card_{index}", typeof(RectTransform));
        cardObj.transform.SetParent(parent, false);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(xOffset, 0f);
        cardRect.sizeDelta = new Vector2(280f, 372f); // カード比 ~0.76
        cardRoots[index] = cardRect;

        // 裏面（ティア別 card_back）。ルートが 0° のとき正面。
        GameObject backObj = new GameObject("Back", typeof(RectTransform), typeof(Image));
        backObj.transform.SetParent(cardRect, false);
        FillRect(backObj.GetComponent<RectTransform>());
        Image backImg = backObj.GetComponent<Image>();
        backImg.color = Color.white; backImg.preserveAspect = false;
        cardBacks[index] = backImg;

        // 表面コンテナ（180°回転＝ルートが 180° でこちらを向く。内容は鏡像にならない）。
        GameObject frontObj = new GameObject("Front", typeof(RectTransform));
        frontObj.transform.SetParent(cardRect, false);
        RectTransform frontRect = frontObj.GetComponent<RectTransform>();
        FillRect(frontRect);
        frontRect.localEulerAngles = new Vector3(0f, 180f, 0f);
        cardFronts[index] = frontObj;

        // 表面のアーティファクト画像（ティア別 f*_artifact）。
        GameObject artObj = new GameObject("Art", typeof(RectTransform), typeof(Image));
        artObj.transform.SetParent(frontRect, false);
        FillRect(artObj.GetComponent<RectTransform>());
        Image artImg = artObj.GetComponent<Image>();
        artImg.color = Color.white; artImg.preserveAspect = false;
        cardBackgrounds[index] = artImg;

        // テキスト可読性スクリム
        GameObject scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
        scrim.transform.SetParent(frontRect, false);
        RectTransform scrimRect = scrim.GetComponent<RectTransform>();
        scrimRect.anchorMin = new Vector2(0.08f, 0.16f); scrimRect.anchorMax = new Vector2(0.92f, 0.78f);
        scrimRect.offsetMin = Vector2.zero; scrimRect.offsetMax = Vector2.zero;
        Image scrimImg = scrim.GetComponent<Image>(); scrimImg.color = new Color(0f, 0f, 0f, 0.5f); scrimImg.raycastTarget = false;

        // 上部の縁（ティアカラー）
        GameObject borderObj = new GameObject("TierBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(frontRect, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0f, 1f);
        borderRect.anchorMax = new Vector2(1f, 1f);
        borderRect.pivot = new Vector2(0.5f, 1f);
        borderRect.anchoredPosition = new Vector2(0f, 0f);
        borderRect.sizeDelta = new Vector2(0f, 8f);
        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        cardBorders[index] = borderImg;

        tierLabels[index] = CreateText($"Tier_{index}", frontRect, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(240f, 22f), 14f, FontStyles.Bold, new Color(0.85f, 0.85f, 0.9f));
        tierLabels[index].alignment = TextAlignmentOptions.Center;

        nameLabels[index] = CreateText($"Name_{index}", frontRect, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(244f, 52f), 20f, FontStyles.Bold, Color.white);
        nameLabels[index].alignment = TextAlignmentOptions.Center;
        nameLabels[index].enableWordWrapping = true;
        nameLabels[index].enableAutoSizing = true;
        nameLabels[index].fontSizeMin = 13f;
        nameLabels[index].fontSizeMax = 20f;

        descLabels[index] = CreateText($"Desc_{index}", frontRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(232f, 168f), 13f, FontStyles.Normal, new Color(0.9f, 0.95f, 1f));
        descLabels[index].alignment = TextAlignmentOptions.Top;
        descLabels[index].enableWordWrapping = true;
        descLabels[index].lineSpacing = 4f;

        rerollButtons[index] = CreateButton($"Reroll_{index}", frontRect, new Vector2(0.5f, 0f), new Vector2(-66f, 30f), new Vector2(110f, 38f), out rerollButtonTexts[index]);
        rerollButtons[index].GetComponent<Image>().color = new Color(0.30f, 0.32f, 0.38f, 1f);
        int capturedIdx1 = index;
        rerollButtons[index].onClick.AddListener(() => OnRerollClicked(capturedIdx1));

        selectButtons[index] = CreateButton($"Select_{index}", frontRect, new Vector2(0.5f, 0f), new Vector2(66f, 30f), new Vector2(110f, 38f), out selectButtonTexts[index]);
        selectButtons[index].GetComponent<Image>().color = new Color(0.22f, 0.6f, 0.85f, 1f);
        int capturedIdx2 = index;
        selectButtons[index].onClick.AddListener(() => OnSelectClicked(capturedIdx2));
    }

    private static void FillRect(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    // ===== フリップ演出 =====
    private void PlayFlipAll()
    {
        for (int i = 0; i < CardCount; i++)
            PlayFlipCard(i, i * 0.12f);
    }

    private void PlayFlipCard(int i, float delay)
    {
        RectTransform root = cardRoots[i];
        if (root == null) return;
        root.DOKill();
        root.localEulerAngles = Vector3.zero;
        if (cardBacks[i] != null) cardBacks[i].gameObject.SetActive(true);
        if (cardFronts[i] != null) cardFronts[i].SetActive(false);

        Sequence s = DOTween.Sequence().SetUpdate(true);
        if (delay > 0f) s.AppendInterval(delay);
        s.Append(root.DOLocalRotate(new Vector3(0f, 90f, 0f), 0.16f).SetEase(Ease.InQuad));
        s.AppendCallback(() =>
        {
            if (cardBacks[i] != null) cardBacks[i].gameObject.SetActive(false);
            if (cardFronts[i] != null) cardFronts[i].SetActive(true);
        });
        s.Append(root.DOLocalRotate(new Vector3(0f, 180f, 0f), 0.20f).SetEase(Ease.OutBack));
    }

    private Sprite TierBackSprite(AugmentTier t)
    {
        string n = t == AugmentTier.Gold ? "gold_back" : (t == AugmentTier.Prism ? "prism_back" : "silver_back");
        return Resources.Load<Sprite>("UI/Cards/" + n);
    }

    private Sprite TierFrontSprite(AugmentTier t)
    {
        string n = t == AugmentTier.Gold ? "gold_front" : (t == AugmentTier.Prism ? "prism_front" : "silver_front");
        return Resources.Load<Sprite>("UI/Cards/" + n);
    }

    private void RefreshCards()
    {
        bool ja = LocalizationManager.IsJapanese;
        LocalizationManager.ApplyFont(titleText);
        string tierName = TierLocalizedName(currentTier, ja);
        titleText.text = ja ? $"{tierName}オーグメントを選択" : $"Choose a {tierName} Augment";

        Color tierColor = GetTierColor(currentTier);
        Sprite backSprite = TierBackSprite(currentTier);
        Sprite frontSprite = TierFrontSprite(currentTier);

        for (int i = 0; i < CardCount; i++)
        {
            AugmentDefinition aug = currentCards[i];
            cardBorders[i].color = tierColor;
            if (cardBacks[i] != null) cardBacks[i].sprite = backSprite;
            cardBackgrounds[i].sprite = frontSprite;
            cardBackgrounds[i].color = Color.white;
            LocalizationManager.ApplyFont(tierLabels[i]);
            LocalizationManager.ApplyFont(nameLabels[i]);
            LocalizationManager.ApplyFont(descLabels[i]);
            LocalizationManager.ApplyFont(selectButtonTexts[i]);
            LocalizationManager.ApplyFont(rerollButtonTexts[i]);

            if (aug == null)
            {
                tierLabels[i].text = "---";
                nameLabels[i].text = ja ? "（候補なし）" : "(No augment)";
                descLabels[i].text = string.Empty;
                selectButtons[i].interactable = false;
                rerollButtons[i].interactable = false;
                rerollButtonTexts[i].text = "—";
                selectButtonTexts[i].text = ja ? "選ぶ" : "Pick";
                continue;
            }

            tierLabels[i].text = TierLocalizedName(aug.Tier, ja).ToUpper();
            tierLabels[i].color = tierColor;
            nameLabels[i].text = ja ? aug.NameJa : aug.NameEn;
            descLabels[i].text = ja ? aug.DescriptionJa : aug.DescriptionEn;
            selectButtons[i].interactable = true;
            selectButtonTexts[i].text = ja ? "選ぶ" : "Pick";
            // Reroll button
            rerollButtons[i].interactable = !rerollUsed[i];
            rerollButtons[i].GetComponent<Image>().color = rerollUsed[i] ? new Color(0.18f, 0.18f, 0.22f, 0.6f) : new Color(0.30f, 0.32f, 0.38f, 1f);
            rerollButtonTexts[i].text = rerollUsed[i] ? (ja ? "使用済" : "Used") : (ja ? "リロール" : "Reroll");
        }
    }

    private Color GetTierColor(AugmentTier tier)
    {
        switch (tier)
        {
            case AugmentTier.Gold: return new Color(1f, 0.82f, 0.28f, 1f);
            case AugmentTier.Prism: return new Color(0.82f, 0.5f, 1f, 1f);
            default: return new Color(0.85f, 0.88f, 0.95f, 1f); // silver
        }
    }

    private Color GetTierBgTint(AugmentTier tier)
    {
        switch (tier)
        {
            case AugmentTier.Gold: return new Color(0.10f, 0.08f, 0.04f, 1f);
            case AugmentTier.Prism: return new Color(0.08f, 0.05f, 0.12f, 1f);
            default: return new Color(0.08f, 0.10f, 0.13f, 1f); // silver
        }
    }

    private string TierLocalizedName(AugmentTier tier, bool ja)
    {
        switch (tier)
        {
            case AugmentTier.Gold: return ja ? "ゴールド" : "Gold";
            case AugmentTier.Prism: return ja ? "プリズム" : "Prism";
            default: return ja ? "シルバー" : "Silver";
        }
    }

    private void RefreshLanguage()
    {
        if (isBuilt && isOpen)
            RefreshCards();
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, float fontSize, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(text);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private Button CreateButton(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, out TextMeshProUGUI labelText)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.18f, 0.42f, 0.52f, 1f);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;
        labelText = labelObj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(labelText);
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 14f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        return button;
    }
}
