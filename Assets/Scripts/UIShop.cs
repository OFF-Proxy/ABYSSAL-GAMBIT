using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIShop : MonoBehaviour
{
    public static UIShop Instance { get; private set; }

    public List<UICard> allCards;
    public TextMeshProUGUI money;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI expText;
    public Button expButton;
    public TextMeshProUGUI sellPreviewText;
    public Color sellPreviewColor = new Color(1f, 0.9f, 0.2f, 1f);

    private EntitiesDatabaseSO cachedDb;
    private int refreshCost = 2;
    private int expCost = 4;
    private int expAmount = 4;
    private const int MaxShopCost = 5;
    private static readonly int[,] ShopOddsByLevel =
    {
        { 100, 0, 0, 0, 0 },
        { 100, 0, 0, 0, 0 },
        { 75, 25, 0, 0, 0 },
        { 55, 30, 15, 0, 0 },
        { 45, 33, 20, 2, 0 },
        { 30, 40, 25, 5, 0 },
        { 19, 30, 40, 10, 1 },
        { 15, 20, 32, 30, 3 },
        { 10, 17, 25, 33, 15 },
        { 5, 10, 20, 40, 25 }
    };

    private RectTransform rectTransform;
    private List<GameObject> sellModeHiddenObjects = new List<GameObject>();
    private Dictionary<GameObject, bool> previousActiveStates = new Dictionary<GameObject, bool>();
    private bool sellModeActive;

    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        EnsureExpControls();
        EnsureSellPreviewText();
        CacheSellModeHiddenObjects();
    }

    private void Start()
    {
        cachedDb = GameManager.Instance.entitiesDatabase;
        GenerateCard();
        GameManager.Instance.OnRosterChanged += UpdateUpgradeHighlights;
        PlayerData.Instance.OnUpdate += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (GameManager.Instance != null)
            GameManager.Instance.OnRosterChanged -= UpdateUpgradeHighlights;

        if (PlayerData.Instance != null)
            PlayerData.Instance.OnUpdate -= Refresh;
    }

    public void GenerateCard()
    {
        if (cachedDb == null || cachedDb.allEntities == null || cachedDb.allEntities.Count == 0)
            return;

        for(int i = 0; i < allCards.Count; i++)
        {
            if (!allCards[i].gameObject.activeSelf)
                allCards[i].gameObject.SetActive(true);

            allCards[i].Setup(GetRandomEntityForCurrentLevel(), this);
        }

        UpdateUpgradeHighlights();
    }

    private EntitiesDatabaseSO.EntityData GetRandomEntityForCurrentLevel()
    {
        int targetCost = RollShopCostForCurrentLevel();
        List<EntitiesDatabaseSO.EntityData> candidates = GetEntitiesByCost(targetCost);
        if (candidates.Count == 0)
            candidates = GetFallbackEntitiesForCost(targetCost);

        if (candidates.Count == 0)
            candidates = cachedDb.allEntities.Where(entity => entity.prefab != null).ToList();

        if (candidates.Count == 0)
            candidates = cachedDb.allEntities;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private int RollShopCostForCurrentLevel()
    {
        int level = PlayerData.Instance != null ? PlayerData.Instance.Level : 1;
        int row = Mathf.Clamp(level, 1, ShopOddsByLevel.GetLength(0)) - 1;
        int totalWeight = 0;

        for (int costIndex = 0; costIndex < MaxShopCost; costIndex++)
            totalWeight += ShopOddsByLevel[row, costIndex];

        if (totalWeight <= 0)
            return 1;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        for (int costIndex = 0; costIndex < MaxShopCost; costIndex++)
        {
            cumulative += ShopOddsByLevel[row, costIndex];
            if (roll < cumulative)
                return costIndex + 1;
        }

        return 1;
    }

    private List<EntitiesDatabaseSO.EntityData> GetEntitiesByCost(int cost)
    {
        return cachedDb.allEntities
            .Where(entity => entity.prefab != null && entity.cost == cost)
            .ToList();
    }

    private List<EntitiesDatabaseSO.EntityData> GetFallbackEntitiesForCost(int targetCost)
    {
        List<EntitiesDatabaseSO.EntityData> availableEntities = cachedDb.allEntities
            .Where(entity => entity.prefab != null)
            .ToList();

        if (availableEntities.Count == 0)
            return new List<EntitiesDatabaseSO.EntityData>();

        int fallbackCost = availableEntities
            .Select(entity => entity.cost)
            .Where(cost => cost <= targetCost)
            .DefaultIfEmpty(availableEntities.Min(entity => entity.cost))
            .Max();

        return availableEntities
            .Where(entity => entity.cost == fallbackCost)
            .ToList();
    }

    public void OnCardClick(UICard card, EntitiesDatabaseSO.EntityData cardData)
    {
        if (sellModeActive)
            return;

        if (!GameManager.Instance.CanBuyEntity(cardData))
            return;

        //We should check if we have the money!
        if(PlayerData.Instance.CanAfford(cardData.cost))
        {
            PlayerData.Instance.SpendMoney(cardData.cost);
            card.gameObject.SetActive(false);
            GameManager.Instance.OnEntityBought(cardData);
            UpdateUpgradeHighlights();
        }
    }

    public void OnRefreshClick()
    {
        if (sellModeActive)
            return;

        //Decrease money 
        if(PlayerData.Instance.CanAfford(refreshCost))
        {
            PlayerData.Instance.SpendMoney(refreshCost);
            GenerateCard();
        }
    }

    public void OnExpClick()
    {
        if (sellModeActive || PlayerData.Instance == null)
            return;

        PlayerData.Instance.TryBuyExp(expAmount, expCost);
    }

    void Refresh()
    {
        money.text = PlayerData.Instance.Money.ToString();

        if (levelText != null)
        {
            levelText.enableWordWrapping = false;
            levelText.overflowMode = TextOverflowModes.Overflow;
            levelText.text = $"Lv {PlayerData.Instance.Level}";
        }

        if (expText != null)
        {
            if (PlayerData.Instance.Level >= PlayerData.Instance.MaxLevel)
                expText.text = "MAX";
            else
                expText.text = $"{PlayerData.Instance.Exp}/{PlayerData.Instance.NextLevelExp}";
        }

        if (expButton != null)
            expButton.interactable = PlayerData.Instance.CanBuyExp(expCost);
    }

    private void UpdateUpgradeHighlights()
    {
        for (int i = 0; i < allCards.Count; i++)
        {
            UICard card = allCards[i];
            if (card == null || !card.gameObject.activeSelf || !card.HasData)
                continue;

            card.SetUpgradeReady(GameManager.Instance.GetUpgradePreviewStarWithPurchase(card.EntityName));
        }
    }

    public void ShowSellPreview(BaseEntity entity)
    {
        if (entity == null || GameManager.Instance == null)
            return;

        EnsureSellPreviewText();
        CacheSellModeHiddenObjects();

        if (!sellModeActive)
        {
            previousActiveStates.Clear();
            for (int i = 0; i < sellModeHiddenObjects.Count; i++)
            {
                GameObject target = sellModeHiddenObjects[i];
                if (target == null || previousActiveStates.ContainsKey(target))
                    continue;

                previousActiveStates[target] = target.activeSelf;
                target.SetActive(false);
            }

            sellModeActive = true;
        }

        if (sellPreviewText != null)
        {
            sellPreviewText.gameObject.SetActive(true);
            sellPreviewText.text = $"SELL +{GameManager.Instance.GetSellValue(entity)}";
        }
    }

    public void HideSellPreview()
    {
        if (!sellModeActive)
            return;

        foreach (KeyValuePair<GameObject, bool> state in previousActiveStates)
        {
            if (state.Key != null)
                state.Key.SetActive(state.Value);
        }

        previousActiveStates.Clear();
        sellModeActive = false;

        if (sellPreviewText != null)
            sellPreviewText.gameObject.SetActive(false);

        UpdateUpgradeHighlights();
    }

    public bool IsPointerOverShop(Vector2 screenPosition)
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, uiCamera))
            return true;

        for (int i = 0; i < sellModeHiddenObjects.Count; i++)
        {
            RectTransform targetRect = sellModeHiddenObjects[i] != null ? sellModeHiddenObjects[i].GetComponent<RectTransform>() : null;
            if (targetRect != null && RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPosition, uiCamera))
                return true;
        }

        return false;
    }

    private void EnsureSellPreviewText()
    {
        if (sellPreviewText != null)
            return;

        GameObject preview = new GameObject("SellPreview", typeof(RectTransform));
        preview.transform.SetParent(transform, false);

        RectTransform previewRect = preview.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = Vector2.zero;

        sellPreviewText = preview.AddComponent<TextMeshProUGUI>();
        sellPreviewText.alignment = TextAlignmentOptions.Center;
        sellPreviewText.fontSize = 48f;
        sellPreviewText.fontStyle = FontStyles.Bold;
        sellPreviewText.color = sellPreviewColor;
        sellPreviewText.raycastTarget = false;
        sellPreviewText.text = "SELL +0";
        sellPreviewText.gameObject.SetActive(false);
    }

    private void EnsureExpControls()
    {
        if (expButton == null)
        {
            GameObject buttonObject = GameObject.Find("LevelUpButton");
            if (buttonObject != null)
                expButton = buttonObject.GetComponent<Button>();
        }

        if (expButton != null)
        {
            expButton.onClick.RemoveListener(OnExpClick);
            expButton.onClick.AddListener(OnExpClick);
        }

        if (expText == null)
        {
            GameObject expObject = GameObject.Find("Exp");
            if (expObject != null)
                expText = expObject.GetComponent<TextMeshProUGUI>();
        }

        if (levelText == null)
        {
            GameObject levelObject = GameObject.Find("LV");
            if (levelObject != null)
                levelText = levelObject.GetComponent<TextMeshProUGUI>();

            if (levelText == null)
                levelText = FindTextMeshProByNameOrText("PlayerLevel", "Lv", "LV", "Level");
        }
    }

    private TextMeshProUGUI FindTextMeshProByNameOrText(params string[] namesOrTexts)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        TextMeshProUGUI[] texts = canvas != null
            ? canvas.GetComponentsInChildren<TextMeshProUGUI>(true)
            : FindObjectsOfType<TextMeshProUGUI>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
                continue;

            for (int j = 0; j < namesOrTexts.Length; j++)
            {
                string value = namesOrTexts[j];
                if (text.gameObject.name == value || text.text == value)
                    return text;
            }
        }

        return null;
    }

    private void CacheSellModeHiddenObjects()
    {
        sellModeHiddenObjects.Clear();

        if (allCards != null && allCards.Count > 0 && allCards[0] != null && allCards[0].transform.parent != null)
            AddSellModeHiddenObject(allCards[0].transform.parent.gameObject);

        AddSellModeHiddenObject(GameObject.Find("Exp"));
        AddSellModeHiddenObject(GameObject.Find("LevelUpButton"));
        AddSellModeHiddenObject(GameObject.Find("RerollButton"));
    }

    private void AddSellModeHiddenObject(GameObject target)
    {
        if (target != null && !sellModeHiddenObjects.Contains(target))
            sellModeHiddenObjects.Add(target);
    }
}
