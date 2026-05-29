using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AutoChessBossRush.Save;

// 章開始前に「過去章で倒して仲間化したボス」を 1 体まで連れて行く編成画面です。
// SaveManager.BossAllies が空のときは何もしません（初回プレイ）。
// 1 体選ぶか「連れて行かない」を押すと閉じ、選択結果を GameManager に返します。
public class ChapterRosterUI : MonoBehaviour
{
    private const int SortingOrder = 60010;

    public static ChapterRosterUI Instance { get; private set; }

    private Action<EntitiesDatabaseSO.EntityData> onSelected;

    private RectTransform panelRect;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subtitleText;
    private Transform optionParent;
    private Button skipButton;
    private TextMeshProUGUI skipLabel;
    private Canvas localCanvas;
    private readonly List<GameObject> optionObjects = new List<GameObject>();
    private readonly List<EntitiesDatabaseSO.EntityData> currentOptions = new List<EntitiesDatabaseSO.EntityData>();

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

    public static ChapterRosterUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        ChapterRosterUI existing = FindObjectOfType<ChapterRosterUI>(true);
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureUiParts();
            return existing;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject uiObject = new GameObject("ChapterRosterUI", typeof(RectTransform), typeof(Image), typeof(ChapterRosterUI));
        uiObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = uiObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image overlay = uiObject.GetComponent<Image>();
        overlay.color = new Color(0f, 0.02f, 0.05f, 0.78f);
        overlay.raycastTarget = true;

        Instance = uiObject.GetComponent<ChapterRosterUI>();
        Instance.EnsureUiParts();
        Instance.gameObject.SetActive(false);
        return Instance;
    }

    // 永続 roster の中身を引数として渡される EntityData リスト形式で表示します。
    // 「連れて行かない」を選んだ場合は selectedCallback に null が渡ります。
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
                if (options[i].prefab == null) continue;
                currentOptions.Add(options[i]);
                CreateOption(options[i]);
            }
        }

        if (subtitleText != null)
        {
            subtitleText.text = LocalizationManager.IsJapanese
                ? "1 体だけ章へ連れて行けます。連れて行かないことも可能です。"
                : "Bring one ally into this chapter. You can also start solo.";
        }

        gameObject.SetActive(true);
    }

    private void Hide()
    {
        UnitStatusPanelUI.Hide();
        gameObject.SetActive(false);
        onSelected = null;
    }

    private void ClearOptions()
    {
        for (int i = 0; i < optionObjects.Count; i++)
        {
            if (optionObjects[i] != null)
                Destroy(optionObjects[i]);
        }
        optionObjects.Clear();
    }

    private void CreateOption(EntitiesDatabaseSO.EntityData entityData)
    {
        GameObject optionObject = new GameObject($"{entityData.name}RosterOption", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        optionObject.transform.SetParent(optionParent, false);
        optionObjects.Add(optionObject);

        RectTransform optionRect = optionObject.GetComponent<RectTransform>();
        optionRect.sizeDelta = new Vector2(220f, 290f);

        LayoutElement layoutElement = optionObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 220f;
        layoutElement.preferredHeight = 290f;

        Image frameImage = optionObject.GetComponent<Image>();
        frameImage.sprite = entityData.frame;
        frameImage.color = new Color(0.62f, 0.85f, 1f, 1f);
        frameImage.preserveAspect = false;

        Button button = optionObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectOption(entityData));

        CreateOptionIcon(optionObject.transform, entityData);
        CreateOptionText(optionObject.transform, entityData);
        CreateInfoButton(optionObject.transform, entityData);
    }

    private void SelectOption(EntitiesDatabaseSO.EntityData entityData)
    {
        Action<EntitiesDatabaseSO.EntityData> callback = onSelected;
        Hide();
        callback?.Invoke(entityData);
    }

    private void OnSkipClicked()
    {
        Action<EntitiesDatabaseSO.EntityData> callback = onSelected;
        Hide();
        callback?.Invoke(default);
    }

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
            ? $"{LocalizationManager.UnitName(entityData.name)}\nベンチに加入"
            : $"{entityData.name}\nJOIN BENCH";
        nameText.alignment = TextAlignmentOptions.Center;
        LocalizationManager.ApplyFont(nameText);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 16f;
        nameText.fontSizeMax = 26f;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = Color.white;
        nameText.outlineWidth = 0.18f;
        nameText.outlineColor = Color.black;
        nameText.raycastTarget = false;
    }

    private void EnsureUiParts()
    {
        EnsureInputCanvas();
        if (panelRect != null && titleText != null && optionParent != null && skipButton != null)
            return;

        GameObject panelObject = new GameObject("RosterPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);

        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(820f, 480f);

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
        titleRect.sizeDelta = new Vector2(0f, 52f);

        titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.text = LocalizationManager.IsJapanese ? "章へ連れて行くボスを選択" : "CHOOSE A BOSS ALLY";
        titleText.alignment = TextAlignmentOptions.Center;
        LocalizationManager.ApplyFont(titleText);
        titleText.fontSize = 32f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.74f, 0.92f, 1f, 1f);
        titleText.outlineWidth = 0.16f;
        titleText.outlineColor = Color.black;
        titleText.raycastTarget = false;

        GameObject subtitleObject = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subtitleObject.transform.SetParent(panelObject.transform, false);

        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -72f);
        subtitleRect.sizeDelta = new Vector2(0f, 28f);

        subtitleText = subtitleObject.GetComponent<TextMeshProUGUI>();
        subtitleText.alignment = TextAlignmentOptions.Center;
        LocalizationManager.ApplyFont(subtitleText);
        subtitleText.fontSize = 16f;
        subtitleText.color = new Color(0.78f, 0.9f, 1f, 0.85f);
        subtitleText.raycastTarget = false;
        subtitleText.text = LocalizationManager.IsJapanese
            ? "1 体だけ章へ連れて行けます。連れて行かないことも可能です。"
            : "Bring one ally into this chapter. You can also start solo.";

        GameObject optionsObject = new GameObject("Options", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        optionsObject.transform.SetParent(panelObject.transform, false);

        RectTransform optionsRect = optionsObject.GetComponent<RectTransform>();
        optionsRect.anchorMin = new Vector2(0f, 0f);
        optionsRect.anchorMax = new Vector2(1f, 1f);
        optionsRect.offsetMin = new Vector2(42f, 78f);
        optionsRect.offsetMax = new Vector2(-42f, -112f);

        HorizontalLayoutGroup layoutGroup = optionsObject.GetComponent<HorizontalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.spacing = 24f;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        optionParent = optionsObject.transform;

        // Skip ボタン（連れて行かない）
        GameObject skipObject = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
        skipObject.transform.SetParent(panelObject.transform, false);
        RectTransform skipRect = skipObject.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.5f, 0f);
        skipRect.anchorMax = new Vector2(0.5f, 0f);
        skipRect.pivot = new Vector2(0.5f, 0f);
        skipRect.anchoredPosition = new Vector2(0f, 20f);
        skipRect.sizeDelta = new Vector2(280f, 40f);

        Image skipImage = skipObject.GetComponent<Image>();
        skipImage.color = new Color(0.16f, 0.22f, 0.32f, 0.95f);

        skipButton = skipObject.GetComponent<Button>();
        skipButton.targetGraphic = skipImage;
        skipButton.onClick.AddListener(OnSkipClicked);

        GameObject skipLabelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        skipLabelObject.transform.SetParent(skipObject.transform, false);
        RectTransform skipLabelRect = skipLabelObject.GetComponent<RectTransform>();
        skipLabelRect.anchorMin = Vector2.zero;
        skipLabelRect.anchorMax = Vector2.one;
        skipLabelRect.offsetMin = Vector2.zero;
        skipLabelRect.offsetMax = Vector2.zero;

        skipLabel = skipLabelObject.GetComponent<TextMeshProUGUI>();
        skipLabel.text = LocalizationManager.IsJapanese ? "連れて行かない" : "START SOLO";
        skipLabel.alignment = TextAlignmentOptions.Center;
        LocalizationManager.ApplyFont(skipLabel);
        skipLabel.fontSize = 18f;
        skipLabel.fontStyle = FontStyles.Bold;
        skipLabel.color = new Color(0.86f, 0.92f, 1f, 0.9f);
        skipLabel.raycastTarget = false;
    }

    private void EnsureInputCanvas()
    {
        if (localCanvas == null)
            localCanvas = GetComponent<Canvas>();

        if (localCanvas == null)
            localCanvas = gameObject.AddComponent<Canvas>();

        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = SortingOrder;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private void RefreshLanguage()
    {
        if (titleText != null)
        {
            LocalizationManager.ApplyFont(titleText);
            titleText.text = LocalizationManager.IsJapanese ? "章へ連れて行くボスを選択" : "CHOOSE A BOSS ALLY";
        }
        if (subtitleText != null)
        {
            LocalizationManager.ApplyFont(subtitleText);
            subtitleText.text = LocalizationManager.IsJapanese
                ? "1 体だけ章へ連れて行けます。連れて行かないことも可能です。"
                : "Bring one ally into this chapter. You can also start solo.";
        }
        if (skipLabel != null)
        {
            LocalizationManager.ApplyFont(skipLabel);
            skipLabel.text = LocalizationManager.IsJapanese ? "連れて行かない" : "START SOLO";
        }

        if (!gameObject.activeSelf || currentOptions.Count == 0)
            return;

        ClearOptions();
        for (int i = 0; i < currentOptions.Count; i++)
            CreateOption(currentOptions[i]);
    }
}
