using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 画面下のショップUI全体を管理するクラスです。
// カード生成、購入、リロール、EXP購入、売却プレビュー、スターアップ予告を担当します。
public class UIShop : MonoBehaviour
{
    // 他のスクリプトからショップにアクセスしやすくするための簡易Singletonです。
    public static UIShop Instance { get; private set; }

    // Inspectorから紐づけるカード一覧と、所持金・レベル・EXP表示です。
    public List<UICard> allCards;
    public TextMeshProUGUI money;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI expText;
    public Button expButton;

    // ユニットをショップへドラッグした時に表示する「売却額」用UIです。
    public TextMeshProUGUI sellPreviewText;
    public Color sellPreviewColor = new Color(1f, 0.9f, 0.2f, 1f);

    // ショップで使うユニットデータベースと、各操作のコストです。
    private EntitiesDatabaseSO cachedDb;
    private int refreshCost = 2;
    private int expCost = 4;
    private int expAmount = 4;

    // ショップに出る可能性がある最大コストです。後でコスト5まで増やす想定です。
    private const int MaxShopCost = 5;

    // レベルごとのショップ排出率です。
    // 行がプレイヤーレベル、列がユニットコスト1から5を表します。
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

    // 売却判定で「マウスがショップ上にあるか」を見るためのRectTransformです。
    private RectTransform rectTransform;

    // 売却モード中に一時的に隠すUIと、その元の表示状態を覚えておく入れ物です。
    private List<GameObject> sellModeHiddenObjects = new List<GameObject>();
    private Dictionary<GameObject, bool> previousActiveStates = new Dictionary<GameObject, bool>();
    private bool sellModeActive;

    // ショップ生成直後に、Singleton登録と必要UIの補完を行います。
    private void Awake()
    {
        // ショップはシーンに1つだけある想定なので、Instanceに自分を登録します。
        LocalizationManager.EnsureExists();
        Instance = this;
        rectTransform = GetComponent<RectTransform>();

        // Inspectorで参照が抜けていても動くよう、必要なUIを探して補完します。
        EnsureExpControls();
        NormalizeShopTextLayout();
        EnsureSellPreviewText();
        CacheSellModeHiddenObjects();
    }

    // GameManagerやPlayerDataが準備された後、カード生成とイベント登録を行います。
    private void Start()
    {
        // GameManagerが持っているユニットデータベースをショップ側で使いやすいように保存します。
        cachedDb = GameManager.Instance.entitiesDatabase;

        // ゲーム開始時に最初のショップカードを並べます。
        GenerateCard();

        // 所持ユニットやプレイヤー情報が変わった時に、表示を自動更新します。
        GameManager.Instance.OnRosterChanged += UpdateUpgradeHighlights;
        PlayerData.Instance.OnUpdate += Refresh;
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        Refresh();
        LocalizationManager.ApplyStaticTextTranslations();
        NormalizeShopTextLayout();
    }

    // ショップ破棄時にイベント購読を解除します。
    private void OnDestroy()
    {
        // シーン終了やオブジェクト破棄時に、古い参照やイベント登録を残さないようにします。
        if (Instance == this)
            Instance = null;

        if (GameManager.Instance != null)
            GameManager.Instance.OnRosterChanged -= UpdateUpgradeHighlights;

        if (PlayerData.Instance != null)
            PlayerData.Instance.OnUpdate -= Refresh;

        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    // ショップカードを全て引き直します。リロール時やゲーム開始時に使います。
    public void GenerateCard()
    {
        // データベースが空ならカードを作れないので、ここで処理を止めます。
        if (cachedDb == null || cachedDb.allEntities == null || cachedDb.allEntities.Count == 0)
            return;

        for(int i = 0; i < allCards.Count; i++)
        {
            // 前回購入で非表示になったカードも、リロール時には再表示します。
            if (!allCards[i].gameObject.activeSelf)
                allCards[i].gameObject.SetActive(true);

            // 現在レベルの排出率に従って、カードにユニットを設定します。
            allCards[i].Setup(GetRandomEntityForCurrentLevel(), this);
        }

        // 引き直したカードの中にスターアップ可能なユニットがあれば光らせます。
        UpdateUpgradeHighlights();
    }

    // 現在レベルの排出率に従って、ショップに出すユニットを1体選びます。
    private EntitiesDatabaseSO.EntityData GetRandomEntityForCurrentLevel()
    {
        int targetCost = RollShopCostForCurrentLevel();
        List<EntitiesDatabaseSO.EntityData> candidates = GetEntitiesByCost(targetCost);

        // まだ該当コストのユニットが未実装なら、近いコストのユニットで代用します。
        if (candidates.Count == 0)
            candidates = GetFallbackEntitiesForCost(targetCost);

        // Prefabが入っているユニットを優先し、それもなければ全データから選びます。
        if (candidates.Count == 0)
            candidates = cachedDb.allEntities.Where(entity => entity.prefab != null && IsShopEntityUnlocked(entity)).ToList();

        if (candidates.Count == 0)
            candidates = cachedDb.allEntities.Where(IsShopEntityUnlocked).ToList();

        if (candidates.Count == 0)
            candidates = cachedDb.allEntities;

        return candidates[Random.Range(0, candidates.Count)];
    }

    // プレイヤーレベルに応じた確率表から、今回出すユニットのコストを抽選します。
    private int RollShopCostForCurrentLevel()
    {
        int level = PlayerData.Instance != null ? PlayerData.Instance.Level : 1;
        int row = Mathf.Clamp(level, 1, ShopOddsByLevel.GetLength(0)) - 1;
        int totalWeight = 0;

        // 対象レベルの全コスト確率を合計して、抽選の母数にします。
        for (int costIndex = 0; costIndex < MaxShopCost; costIndex++)
            totalWeight += ShopOddsByLevel[row, costIndex];

        if (totalWeight <= 0)
            return 1;

        // 0から合計値までの乱数を取り、どのコスト範囲に入ったかで決めます。
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

    // 指定コストかつPrefab設定済みのユニットだけを取り出します。
    private List<EntitiesDatabaseSO.EntityData> GetEntitiesByCost(int cost)
    {
        return cachedDb.allEntities
            .Where(entity => entity.prefab != null && entity.cost == cost && IsShopEntityUnlocked(entity))
            .ToList();
    }

    // 指定コストのユニットがまだ無い時に、実装済みの近いコストから代替候補を探します。
    private List<EntitiesDatabaseSO.EntityData> GetFallbackEntitiesForCost(int targetCost)
    {
        List<EntitiesDatabaseSO.EntityData> availableEntities = cachedDb.allEntities
            .Where(entity => entity.prefab != null && IsShopEntityUnlocked(entity))
            .ToList();

        if (availableEntities.Count == 0)
            return new List<EntitiesDatabaseSO.EntityData>();

        // 目標コスト以下で最も高いコストを優先し、なければ最小コストを使います。
        int fallbackCost = availableEntities
            .Select(entity => entity.cost)
            .Where(cost => cost <= targetCost)
            .DefaultIfEmpty(availableEntities.Min(entity => entity.cost))
            .Max();

        return availableEntities
            .Where(entity => entity.cost == fallbackCost)
            .ToList();
    }

    // ボス報酬ユニットなど、まだ解放されていないユニットをショップ抽選から外します。
    private bool IsShopEntityUnlocked(EntitiesDatabaseSO.EntityData entity)
    {
        return GameManager.Instance == null || GameManager.Instance.IsEntityUnlockedForShop(entity);
    }

    // ショップカードをクリックした時の購入処理です。
    public void OnCardClick(UICard card, EntitiesDatabaseSO.EntityData cardData)
    {
        // 売却プレビュー中は、誤クリックで購入やリロールが起きないようにします。
        if (sellModeActive)
            return;

        // ベンチが満杯など、ゲーム側の購入条件を満たさない場合は購入できません。
        if (!GameManager.Instance.CanBuyEntity(cardData))
            return;

        //We should check if we have the money!
        if(PlayerData.Instance.CanAfford(cardData.cost))
        {
            // お金を払ってカードを消し、GameManagerに購入ユニット生成を任せます。
            PlayerData.Instance.SpendMoney(cardData.cost);
            card.gameObject.SetActive(false);
            GameManager.Instance.OnEntityBought(cardData);
            AttackEffectPlayer.PlayUiSfx("unit_buy");
            UpdateUpgradeHighlights();
        }
    }

    // リロールボタンを押した時の処理です。
    public void OnRefreshClick()
    {
        if (sellModeActive)
            return;

        //Decrease money 
        if(PlayerData.Instance.CanAfford(refreshCost))
        {
            // リロール代を払い、ショップカードを引き直します。
            PlayerData.Instance.SpendMoney(refreshCost);
            GenerateCard();
            AttackEffectPlayer.PlayUiSfx("shop_reroll");
        }
    }

    // EXP購入ボタンを押した時の処理です。
    public void OnExpClick()
    {
        if (sellModeActive || PlayerData.Instance == null)
            return;

        // PlayerData側で所持金と最大レベルを確認し、成功したらSEを鳴らします。
        if (PlayerData.Instance.TryBuyExp(expAmount, expCost))
            AttackEffectPlayer.PlayUiSfx("exp_buy");
    }

    // 所持金、レベル、EXP、ボタン押下可否を現在のPlayerDataに合わせて更新します。
    void Refresh()
    {
        LocalizationManager.ApplyFont(money);
        LocalizationManager.ApplyFont(expText);
        money.text = PlayerData.Instance.Money.ToString();

        if (levelText != null)
        {
            levelText.enableWordWrapping = false;
            levelText.overflowMode = TextOverflowModes.Overflow;
            levelText.enableAutoSizing = true;
            levelText.fontSizeMin = 25f;
            levelText.fontSizeMax = 30f;
            LocalizationManager.ApplyFont(levelText);
            levelText.text = LocalizationManager.FormatLevel(PlayerData.Instance.Level);
        }

        if (expText != null)
        {
            expText.enableWordWrapping = false;
            expText.overflowMode = TextOverflowModes.Overflow;
            expText.enableAutoSizing = true;
            expText.fontSizeMin = 12f;
            expText.fontSizeMax = 18f;

            // 最大レベルなら次の必要EXPを出さず、MAX表示にします。
            if (PlayerData.Instance.Level >= PlayerData.Instance.MaxLevel)
                expText.text = LocalizationManager.IsJapanese ? " 最大" : " MAX";
            else
                expText.text = $" {PlayerData.Instance.Exp}/{PlayerData.Instance.NextLevelExp}";
        }

        if (expButton != null)
            expButton.interactable = PlayerData.Instance.CanBuyExp(expCost);

        NormalizeShopTextLayout();
    }

    // 現在ショップに表示されているカードが、買うとスターアップできるかを調べて強調します。
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

    // ユニットをショップへドラッグしている間、売却額表示に切り替えます。
    public void ShowSellPreview(BaseEntity entity)
    {
        if (entity == null || GameManager.Instance == null)
            return;

        EnsureSellPreviewText();
        CacheSellModeHiddenObjects();

        if (!sellModeActive)
        {
            previousActiveStates.Clear();

            // ショップカード、EXP、リロールなどを一時的に隠し、元の表示状態を覚えておきます。
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

        // 今ドラッグしているユニットを売った時の金額を大きく表示します。
        if (sellPreviewText != null)
        {
            sellPreviewText.gameObject.SetActive(true);
            LocalizationManager.ApplyFont(sellPreviewText);
            sellPreviewText.text = LocalizationManager.FormatSellValue(GameManager.Instance.GetSellValue(entity));
        }
    }

    // 売却プレビューを終え、隠していたショップUIを元に戻します。
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

    // マウス位置がショップ範囲内かを調べます。
    // ユニットをショップへドロップして売却できるかの判定に使います。
    public bool IsPointerOverShop(Vector2 screenPosition)
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, uiCamera))
            return true;

        // 売却モード中に隠しているUIの範囲も、ショップ範囲として扱います。
        for (int i = 0; i < sellModeHiddenObjects.Count; i++)
        {
            RectTransform targetRect = sellModeHiddenObjects[i] != null ? sellModeHiddenObjects[i].GetComponent<RectTransform>() : null;
            if (targetRect != null && RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPosition, uiCamera))
                return true;
        }

        return false;
    }

    // 売却額テキストがInspectorで未設定でも動くよう、実行時に作ります。
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
        LocalizationManager.ApplyFont(sellPreviewText);
        sellPreviewText.text = LocalizationManager.FormatSellValue(0);
        sellPreviewText.gameObject.SetActive(false);
    }

    // 言語切替時に、ショップ内の動的テキストをすぐ更新します。
    private void OnLanguageChanged()
    {
        Refresh();
        UpdateUpgradeHighlights();
        LocalizationManager.ApplyStaticTextTranslations();
        NormalizeShopTextLayout();
    }

    // EXPボタンやレベル/EXP表示の参照を探し、ボタンイベントを登録します。
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
            // 二重登録を避けるため、先に同じイベントを外してから登録します。
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

            // 名前が変わっていても、よくありそうな名前や表示文字から探します。
            if (levelText == null)
                levelText = FindTextMeshProByNameOrText("PlayerLevel", "Lv", "LV", "Level");
        }
    }

    // ショップ左側のレベル/EXP/リロール周りは画像に対して文字枠がタイトなので、
    // 実行時に折り返し禁止・自動縮小・位置補正をまとめてかけます。
    private void NormalizeShopTextLayout()
    {
        ConfigureHeaderText(levelText, true);
        ConfigureHeaderText(expText, false);
        ConfigureShopButtonText(GameObject.Find("LevelUpButton"), true);
        ConfigureShopButtonText(GameObject.Find("RerollButton"), false);
    }

    // 上段の「レベル」と「現在EXP」を、横一列で崩れない表示にします。
    private void ConfigureHeaderText(TextMeshProUGUI text, bool isLevelText)
    {
        if (text == null)
            return;

        LocalizationManager.ApplyFont(text);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true;
        text.fontSizeMin = isLevelText ? 10f : 11f;
        text.fontSizeMax = 18f;
        text.alignment = isLevelText ? TextAlignmentOptions.Left : TextAlignmentOptions.Left;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        // 既存ShopBoardの左上メーター内で、レベル表記とEXP/最大表記が重ならない固定枠にします。
        if (isLevelText)
        {
            rect.anchoredPosition = new Vector2(315f, 208.5f);
            rect.sizeDelta = new Vector2(122f, 25f);
        }
        else
        {
            rect.anchoredPosition = new Vector2(426f, 208.5f);
            rect.sizeDelta = new Vector2(78f, 25f);
        }
    }

    // EXP購入/リロールボタン内の文字を、アイコンやコスト表示に被らない範囲へ収めます。
    private void ConfigureShopButtonText(GameObject buttonObject, bool expButtonText)
    {
        if (buttonObject == null)
            return;

        TextMeshProUGUI[] texts = buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
                continue;

            LocalizationManager.ApplyFont(text);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.alignment = TextAlignmentOptions.Center;

            string value = (text.text ?? string.Empty).Trim();
            bool isCostText = int.TryParse(value, out _);
            if (isCostText)
            {
                ConfigureButtonCostText(text);
                continue;
            }

            ConfigureButtonLabelText(text, expButtonText);
        }
    }

    // ボタン中央の主ラベルです。リロールは日本語時に必ず「リロール」と表示します。
    private void ConfigureButtonLabelText(TextMeshProUGUI text, bool expButtonText)
    {
        if (text == null)
            return;

        text.text = expButtonText
            ? "Exp+4"
            : (LocalizationManager.IsJapanese ? "リロール" : "Reroll");
        text.fontSizeMin = expButtonText ? 18f : 16f;
        text.fontSizeMax = expButtonText ? 28f : 26f;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(36f, 6f);
        rect.offsetMax = new Vector2(-105f, -5f);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
    }

    // ボタン右側のコスト数字です。コイン付近で縦に落ちないよう専用枠へ寄せます。
    private void ConfigureButtonCostText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.fontSizeMin = 14f;
        text.fontSizeMax = 22f;
        text.alignment = TextAlignmentOptions.Center;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(42f, 30f);
        rect.anchoredPosition = new Vector2(-62f, 0f);
    }

    // Canvas内から、名前または現在の文字列が一致するTextMeshProUGUIを探します。
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

    // 売却プレビュー中に隠すUIを一覧化します。
    private void CacheSellModeHiddenObjects()
    {
        sellModeHiddenObjects.Clear();

        // ショップカードはカードの親オブジェクトごと隠す想定です。
        if (allCards != null && allCards.Count > 0 && allCards[0] != null && allCards[0].transform.parent != null)
            AddSellModeHiddenObject(allCards[0].transform.parent.gameObject);

        AddSellModeHiddenObject(GameObject.Find("Exp"));
        AddSellModeHiddenObject(GameObject.Find("LevelUpButton"));
        AddSellModeHiddenObject(GameObject.Find("RerollButton"));
    }

    // nullや重複を避けながら、売却モードで隠す候補へ追加します。
    private void AddSellModeHiddenObject(GameObject target)
    {
        if (target != null && !sellModeHiddenObjects.Contains(target))
            sellModeHiddenObjects.Add(target);
    }
}
