using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ボスウェーブをクリアした時に、仲間にするボスを1体選ぶためのUIです。
// シーンに手置きしなくても、GameManagerから呼ばれた時にCanvasへ自動生成されます。
public class BossRewardSelectionUI : MonoBehaviour
{
    // どこからでも現在のボス報酬UIへアクセスするための参照です。
    public static BossRewardSelectionUI Instance { get; private set; }

    // 選択後にGameManagerへ結果を返すためのコールバックです。
    private Action<EntitiesDatabaseSO.EntityData> onSelected;

    // 実行時に作るUI部品です。
    private RectTransform panelRect;
    private TextMeshProUGUI titleText;
    private Transform optionParent;
    private readonly List<GameObject> optionObjects = new List<GameObject>();

    // Unityが生成直後に呼ぶ初期化処理です。
    private void Awake()
    {
        Instance = this;
        EnsureUiParts();
        gameObject.SetActive(false);
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
        onSelected = selectedCallback;

        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
                CreateOption(options[i]);
        }

        gameObject.SetActive(true);
    }

    // UI全体を閉じます。
    private void Hide()
    {
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
        button.onClick.AddListener(() =>
        {
            onSelected?.Invoke(entityData);
            Hide();
        });

        CreateOptionIcon(optionObject.transform, entityData);
        CreateOptionText(optionObject.transform, entityData);
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
        nameText.text = $"{entityData.name}\nUNLOCK SHOP";
        nameText.alignment = TextAlignmentOptions.Center;
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
        titleText.text = "CHOOSE A BOSS";
        titleText.alignment = TextAlignmentOptions.Center;
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
}
