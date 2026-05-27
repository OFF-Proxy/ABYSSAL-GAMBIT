using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ボスウェーブをクリアした時に、仲間にするボスを1体選ぶためのUIです。
// シーンに手置きしなくても、GameManagerから呼ばれた時にCanvasへ自動生成されます。
public class BossRewardSelectionUI : MonoBehaviour
{
    private const int BossRewardSortingOrder = 60020;

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

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject uiObject = new GameObject("BossRewardSelectionUI", typeof(RectTransform), typeof(Image), typeof(BossRewardSelectionUI));
        uiObject.transform.SetParent(canvas.transform, false);

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
    }

    // UI全体を閉じます。
    private void Hide()
    {
        UnitStatusPanelUI.Hide();
        gameObject.SetActive(false);
        onSelected = null;
    }

    // 既存の候補カードを消します。
    private void ClearOptions()
    {
        for (int i = 0; i < optionObjects.Count; i++)
        {
            if (optionObjects[i] != null)
                Destroy(optionObjects[i]);
        }

        optionObjects.Clear();
    }

    // 1体分の選択カードを作ります。
    private void CreateOption(EntitiesDatabaseSO.EntityData entityData)
    {
        GameObject optionObject = new GameObject($"{entityData.name}RewardOption", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        optionObject.transform.SetParent(optionParent, false);
        optionObjects.Add(optionObject);

        RectTransform optionRect = optionObject.GetComponent<RectTransform>();
        optionRect.sizeDelta = new Vector2(230f, 300f);

        LayoutElement layoutElement = optionObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 230f;
        layoutElement.preferredHeight = 300f;

        Image frameImage = optionObject.GetComponent<Image>();
        frameImage.sprite = entityData.frame;
        frameImage.color = new Color(1f, 0.86f, 0.35f, 1f);
        frameImage.preserveAspect = false;

        Button button = optionObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectOption(entityData));

        CreateOptionIcon(optionObject.transform, entityData);
        CreateOptionText(optionObject.transform, entityData);
        CreateInfoButton(optionObject.transform, entityData);
    }

    // 候補カードを選択し、GameManagerへ報酬決定を返します。
    private void SelectOption(EntitiesDatabaseSO.EntityData entityData)
    {
        if (onSelected == null)
            return;

        onSelected.Invoke(entityData);
        Hide();
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
        iconRect.anchorMin = new Vector2(0.08f, 0.25f);
        iconRect.anchorMax = new Vector2(0.92f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = entityData.icon;
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    // 候補カードの名前と説明文を作ります。
    private void CreateOptionText(Transform parent, EntitiesDatabaseSO.EntityData entityData)
    {
        GameObject nameObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObject.transform.SetParent(parent, false);

        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.06f, 0.04f);
        nameRect.anchorMax = new Vector2(0.94f, 0.24f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        TextMeshProUGUI nameText = nameObject.GetComponent<TextMeshProUGUI>();
        nameText.text = LocalizationManager.IsJapanese
            ? $"{LocalizationManager.UnitName(entityData.name)}\nショップに解放"
            : $"{entityData.name}\nUNLOCK SHOP";
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
        panelRect.sizeDelta = new Vector2(820f, 430f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.01f, 0.04f, 0.08f, 0.94f);
        panelImage.raycastTarget = true;

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
