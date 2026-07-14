using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// ボスウェーブをクリアした時に、仲間にするボスを1体選ぶためのUIです。
// シーンに手置きしなくても、GameManagerから呼ばれた時にCanvasへ自動生成されます。
public class BossRewardSelectionUI : MonoBehaviour
{
    private const int BossRewardSortingOrder = 25020; // 16bit short上限(32767)内。

    // どこからでも現在のボス報酬UIへアクセスするための参照です。
    public static BossRewardSelectionUI Instance { get; private set; }

    // 選択後にGameManagerへ結果を返すためのコールバックです。
    private Action<EntitiesDatabaseSO.EntityData> onSelected;

    // 実行時に作るUI部品です。
    private RectTransform panelRect;
    private TextMeshProUGUI titleText;
    private Transform optionParent;
    private Canvas localCanvas;
    private readonly List<GameObject> optionObjects = new List<GameObject>();
    private readonly List<EntitiesDatabaseSO.EntityData> currentOptions = new List<EntitiesDatabaseSO.EntityData>();

    // Unityが生成直後に呼ぶ初期化処理です。
    private void Awake()
    {
        LocalizationManager.EnsureExists();
        Instance = this;
        EnsureUiParts();
        gameObject.SetActive(false);
        LocalizationManager.OnLanguageChanged += RefreshLanguage;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        LocalizationManager.OnLanguageChanged -= RefreshLanguage;
    }

    // UIが存在しなければCanvas上へ作り、報酬UIを返します。
    public static BossRewardSelectionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        BossRewardSelectionUI existingUi = FindObjectOfType<BossRewardSelectionUI>(true);
        if (existingUi != null)
        {
            Instance = existingUi;
            existingUi.EnsureUiParts();
            return existingUi;
        }

        // 独立したルートの ScreenSpaceOverlay Canvas として生成する。
        // 以前は FindObjectOfType<Canvas>() の子にしていたため、プレイヤーがオーグメントをホバー中
        // （AugmentTooltipUI の Canvas が生成・active）にこのUIを出すと、たまたまそのトグルする
        // ツールチップ Canvas を親にしてしまい、ツールチップの表示/非表示に選択肢の表示が連動して
        // 点滅・選択不能（進行ハマり）になっていた。ルート化で外部 Canvas への依存を断つ。
        GameObject uiObject = new GameObject("BossRewardSelectionUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(BossRewardSelectionUI));
        Canvas rootCanvas = uiObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = BossRewardSortingOrder;
        uiObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        RectTransform rectTransform = uiObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image overlay = uiObject.GetComponent<Image>();
        overlay.color = new Color(0f, 0.02f, 0.05f, 0.72f);
        overlay.raycastTarget = true;

        Instance = uiObject.GetComponent<BossRewardSelectionUI>();
        Instance.EnsureUiParts();
        Instance.gameObject.SetActive(false);
        return Instance;
    }

    // 報酬候補を表示し、選択完了時に呼ぶ処理を受け取ります。
    public void Show(IReadOnlyList<EntitiesDatabaseSO.EntityData> options, Action<EntitiesDatabaseSO.EntityData> selectedCallback)
    {
        EnsureUiParts();
        ClearOptions();
        currentOptions.Clear();
        onSelected = selectedCallback;

        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                currentOptions.Add(options[i]);
                CreateOption(options[i]);
            }
        }

        gameObject.SetActive(true);
        PlayAppear();
    }

    // パネル＋カードの出現ポップ（アイテム3択UIと統一）。ポーズ非依存。
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
            r.localScale = Vector3.one * 0.7f;
            r.DOKill();
            r.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true).SetDelay(0.06f + i * 0.06f);
        }
    }

    // カードのホバー拡大。
    private class BossCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData e) { var r = transform as RectTransform; if (r != null) { r.DOKill(); r.DOScale(1.05f, 0.12f).SetUpdate(true); } }
        public void OnPointerExit(PointerEventData e) { var r = transform as RectTransform; if (r != null) { r.DOKill(); r.DOScale(1f, 0.12f).SetUpdate(true); } }
    }

    // UI全体を閉じます。
    private void Hide()
    {
        UnitStatusPanelUI.Hide();
        gameObject.SetActive(false);
        onSelected = null;
    }

    // 既存の候補カードを消します。
    // Destroy はフレーム末まで遅延するため、複数選択での再表示時に「破棄待ちの旧カード＋新カード」が
    // 同フレームに並んで枠をはみ出し（例: 3+2=5枚）、さらに破棄待ちカードがクリックを奪って進行不能に
    // なり得た。そこで破棄前に即座に非アクティブ化＋レイアウトから親子解除して、表示も当たり判定も即除外する。
    private void ClearOptions()
    {
        for (int i = 0; i < optionObjects.Count; i++)
        {
            if (optionObjects[i] != null)
            {
                optionObjects[i].SetActive(false);
                optionObjects[i].transform.SetParent(null, false);
                Destroy(optionObjects[i]);
            }
        }

        optionObjects.Clear();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：開いていれば先頭候補を自動選択する。開いていなければfalse。
    public bool DebugAutoResolve()
    {
        if (!gameObject.activeSelf || optionObjects.Count == 0) return false;
        UnityEngine.UI.Button b = optionObjects[0] != null ? optionObjects[0].GetComponent<UnityEngine.UI.Button>() : null;
        if (b == null) return false;
        b.onClick.Invoke();
        return true;
    }
#endif

    // 1体分の選択カードを作ります。
    private void CreateOption(EntitiesDatabaseSO.EntityData entityData)
    {
        GameObject optionObject = new GameObject($"{entityData.name}RewardOption", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        optionObject.transform.SetParent(optionParent, false);
        optionObjects.Add(optionObject);
        optionObject.AddComponent<BossCardHover>(); // ホバー拡大（アイテム3択UIと統一）

        RectTransform optionRect = optionObject.GetComponent<RectTransform>();
        optionRect.sizeDelta = new Vector2(240f, 314f);

        LayoutElement layoutElement = optionObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 240f;
        layoutElement.preferredHeight = 314f;

        // カード見た目（reference の craftable_unit を UI/Cards/unit_card として使用）。無ければ従来のティア枠にフォールバック。
        Image frameImage = optionObject.GetComponent<Image>();
        Sprite cardSprite = Resources.Load<Sprite>("UI/Cards/unit_card");
        if (cardSprite != null)
        {
            frameImage.sprite = cardSprite;
            frameImage.color = Color.white;
            frameImage.preserveAspect = true;
        }
        else
        {
            UnitCardVisual.ApplyProceduralFrame(frameImage, entityData.cost); // R5: AI枠撤去→プログラム枠。
            frameImage.color = new Color(1f, 0.86f, 0.35f, 1f);
            frameImage.preserveAspect = false;
        }

        Button button = optionObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectOption(entityData));

        CreateOptionIcon(optionObject.transform, entityData);
        CreateOptionText(optionObject.transform, entityData);
        CreateInfoButton(optionObject.transform, entityData);
    }

    // 候補カードを選択し、GameManagerへ報酬決定を返します。
    // 重要: 先に Hide() してからコールバックを呼ぶ。複数選択(4-5のボス×2)では
    // コールバック内で次の候補を再表示(Show=SetActive true)するため、呼んだ後に Hide() すると
    // その再表示を即閉じてしまい、bossRewardSelectionPending だけ残って進行不能になっていた。
    private void SelectOption(EntitiesDatabaseSO.EntityData entityData)
    {
        if (onSelected == null)
            return;

        System.Action<EntitiesDatabaseSO.EntityData> callback = onSelected;
        Hide();                       // 先に閉じる（onSelected も null 化される）
        callback.Invoke(entityData);  // 必要ならコールバックが再表示する
    }

    // 報酬候補の性能を右側のユニット詳細パネルで確認するための小さなボタンを作ります。
    private void CreateInfoButton(Transform parent, EntitiesDatabaseSO.EntityData entityData)
    {
        GameObject infoObject = new GameObject("InfoButton", typeof(RectTransform), typeof(Image), typeof(Button));
        infoObject.transform.SetParent(parent, false);

        RectTransform infoRect = infoObject.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(1f, 1f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.pivot = new Vector2(1f, 1f);
        infoRect.anchoredPosition = new Vector2(-10f, -10f);
        infoRect.sizeDelta = new Vector2(78f, 32f);

        Image infoImage = infoObject.GetComponent<Image>();
        infoImage.color = new Color(0.02f, 0.14f, 0.18f, 0.94f);
        infoImage.raycastTarget = true;

        Button infoButton = infoObject.GetComponent<Button>();
        infoButton.onClick.AddListener(() =>
        {
            UnitStatusPanelUI.ShowPreview(entityData, 1);
            AttackEffectPlayer.PlayUiSfx("sfx_ui_select");
        });

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(infoObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = LocalizationManager.IsJapanese ? "性能" : "INFO";
        label.alignment = TextAlignmentOptions.Center;
        LocalizationManager.ApplyFont(label);
        label.fontSize = 15f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.86f, 1f, 1f, 1f);
        label.raycastTarget = false;
    }

    // 候補カードのアイコンを作ります。
    private void CreateOptionIcon(Transform parent, EntitiesDatabaseSO.EntityData entityData)
    {
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(parent, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        // カードの肖像窓（craftable_unit の中央上部）に合わせて配置。
        iconRect.anchorMin = new Vector2(0.18f, 0.46f);
        iconRect.anchorMax = new Vector2(0.82f, 0.86f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.color = Color.white;
        iconImage.raycastTarget = false;
        // R5: AIキャラ絵をやめ、ユニットのドット絵（盤外実体ミラー・盤面と非連動）を表示。
        iconObject.AddComponent<UnitCardPreview>().Bind(entityData);
    }

    // 候補カードの名前と説明文を作ります。
    private void CreateOptionText(Transform parent, EntitiesDatabaseSO.EntityData entityData)
    {
        GameObject nameObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObject.transform.SetParent(parent, false);

        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.08f, 0.25f);
        nameRect.anchorMax = new Vector2(0.92f, 0.43f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        TextMeshProUGUI nameText = nameObject.GetComponent<TextMeshProUGUI>();
        nameText.text = LocalizationManager.IsJapanese
            ? $"{LocalizationManager.UnitName(entityData.name)}\n仲間にする"
            : $"{LocalizationManager.UnitName(entityData.name)}\nRECRUIT";
        nameText.alignment = TextAlignmentOptions.Center;
        LocalizationManager.ApplyFont(nameText);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 16f;
        nameText.fontSizeMax = 28f;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = Color.white;
        nameText.outlineWidth = 0.18f;
        nameText.outlineColor = Color.black;
        nameText.raycastTarget = false;
    }

    // タイトルとカード置き場が無ければ作ります。
    private void EnsureUiParts()
    {
        EnsureInputCanvas();
        if (panelRect != null && titleText != null && optionParent != null)
            return;

        GameObject panelObject = new GameObject("RewardPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);

        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        // 3カード(240×3＋spacing28×2＝776)＋左右余白84＝860 を収めるため横900に（旧820ははみ出し気味）。
        panelRect.sizeDelta = new Vector2(900f, 430f);

        Image panelImage = panelObject.GetComponent<Image>();
        // アイテム3択UIと統一：card_panel を9-sliceで背景に。
        Sprite panelSprite = Resources.Load<Sprite>("UI/Augment/card_panel");
        if (panelSprite != null) { panelImage.sprite = panelSprite; panelImage.type = Image.Type.Sliced; panelImage.color = new Color(0.10f, 0.16f, 0.26f, 0.98f); }
        else panelImage.color = new Color(0.04f, 0.07f, 0.12f, 0.97f);
        panelImage.raycastTarget = true;

        // タイトル帯（上部）。
        GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(panelObject.transform, false);
        RectTransform tbarRect = titleBar.GetComponent<RectTransform>();
        tbarRect.anchorMin = new Vector2(0f, 1f); tbarRect.anchorMax = new Vector2(1f, 1f); tbarRect.pivot = new Vector2(0.5f, 1f);
        tbarRect.sizeDelta = new Vector2(0f, 74f); tbarRect.anchoredPosition = new Vector2(0f, -10f);
        titleBar.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.2f, 0.9f);

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(panelObject.transform, false);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -18f);
        titleRect.sizeDelta = new Vector2(0f, 62f);

        titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.text = LocalizationManager.IsJapanese ? "仲間にするボスを選択" : "CHOOSE A BOSS";
        titleText.alignment = TextAlignmentOptions.Center;
        LocalizationManager.ApplyFont(titleText);
        titleText.fontSize = 36f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(1f, 0.86f, 0.35f, 1f);
        titleText.outlineWidth = 0.16f;
        titleText.outlineColor = Color.black;
        titleText.raycastTarget = false;

        GameObject optionsObject = new GameObject("Options", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        optionsObject.transform.SetParent(panelObject.transform, false);

        RectTransform optionsRect = optionsObject.GetComponent<RectTransform>();
        optionsRect.anchorMin = new Vector2(0f, 0f);
        optionsRect.anchorMax = new Vector2(1f, 1f);
        optionsRect.offsetMin = new Vector2(42f, 36f);
        optionsRect.offsetMax = new Vector2(-42f, -96f);

        HorizontalLayoutGroup layoutGroup = optionsObject.GetComponent<HorizontalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.spacing = 28f;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        optionParent = optionsObject.transform;
    }

    // ボス選択は他の常時表示UIより上に出し、クリックもここで確実に受け取れるようにします。
    private void EnsureInputCanvas()
    {
        if (localCanvas == null)
            localCanvas = GetComponent<Canvas>();

        if (localCanvas == null)
            localCanvas = gameObject.AddComponent<Canvas>();

        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = BossRewardSortingOrder;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    // 言語切替時に、表示中の報酬候補を現在言語へ書き直します。
    private void RefreshLanguage()
    {
        if (titleText != null)
        {
            LocalizationManager.ApplyFont(titleText);
            titleText.text = LocalizationManager.IsJapanese ? "仲間にするボスを選択" : "CHOOSE A BOSS";
        }

        if (!gameObject.activeSelf || currentOptions.Count == 0)
            return;

        ClearOptions();
        for (int i = 0; i < currentOptions.Count; i++)
            CreateOption(currentOptions[i]);
    }
}
