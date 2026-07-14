using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ステージクリア時に「クリアタイム」と「スコア」をまとめて表示するリザルト画面です。
// チャプター最終ステージのクリア時は称号テキストも切り替えます。
public class ResultPanelUI : MonoBehaviour
{
    public static ResultPanelUI Instance { get; private set; }

    private RectTransform panelRect;
    private CanvasGroup panelGroup;
    private GameObject dimObject;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI timeLabel;
    private TextMeshProUGUI timeValue;
    private TextMeshProUGUI scoreLabel;
    private TextMeshProUGUI scoreValue;
    private TextMeshProUGUI bestLabel;
    private TextMeshProUGUI bestValue;
    private TextMeshProUGUI newRecordBadge;
    private TextMeshProUGUI breakdownText;
    private Button continueButton;
    private TextMeshProUGUI continueText;

    private bool isBuilt;
    private bool isOpen;
    private float previousTimeScale = 1f;

    public static ResultPanelUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        ResultPanelUI existing = FindObjectOfType<ResultPanelUI>();
        if (existing != null)
        {
            Instance = existing;
            existing.BuildIfNeeded();
            return existing;
        }

        GameObject go = new GameObject("ResultPanelUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ResultPanelUI));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25500; // 16bit short上限(32767)内。
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        Instance = go.GetComponent<ResultPanelUI>();
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

    // ステージクリアのリザルトを表示します。chapter最終ステージなら isChapterClear=true。
    // bestScore/isNewRecord は章クリア時のみ意味があります（章未クリアではゼロ/false）。
    public void ShowStageResult(int stageNumber, float seconds, int score, string breakdown, bool isChapterClear, int bestScore = 0, bool isNewRecord = false)
    {
        BuildIfNeeded();
        resultIsChapterClear = isChapterClear;
        bool ja = LocalizationManager.IsJapanese;
        LocalizationManager.ApplyFont(titleText);
        LocalizationManager.ApplyFont(subtitleText);
        LocalizationManager.ApplyFont(timeLabel);
        LocalizationManager.ApplyFont(timeValue);
        LocalizationManager.ApplyFont(scoreLabel);
        LocalizationManager.ApplyFont(scoreValue);
        LocalizationManager.ApplyFont(bestLabel);
        LocalizationManager.ApplyFont(bestValue);
        LocalizationManager.ApplyFont(newRecordBadge);
        LocalizationManager.ApplyFont(breakdownText);
        LocalizationManager.ApplyFont(continueText);

        resultIsGameOver = false;
        titleText.color = new Color(1f, 0.92f, 0.55f); // クリアは金（ゲームオーバーで赤に変えた後の復帰用）
        titleText.text = isChapterClear
            ? (ja ? "チャプタークリア！" : "CHAPTER CLEAR!")
            : (ja ? $"ステージ {stageNumber} クリア！" : $"STAGE {stageNumber} CLEAR!");
        subtitleText.text = isChapterClear
            ? (ja ? "全ステージ突破おめでとう" : "All stages cleared")
            : (ja ? $"ステージ {stageNumber} を制覇" : $"Stage {stageNumber} complete");
        timeLabel.text = ja ? "クリアタイム" : "Clear Time";
        timeValue.text = FormatTime(seconds);
        scoreLabel.text = ja ? "スコア" : "Score";
        scoreValue.text = score.ToString("N0");

        // 章クリア時のみベスト行と NEW RECORD バッジを出します。ステージ単位のクリアでは隠します。
        bool showBest = isChapterClear;
        if (bestLabel != null) bestLabel.gameObject.SetActive(showBest);
        if (bestValue != null) bestValue.gameObject.SetActive(showBest);
        if (showBest)
        {
            bestLabel.text = ja ? "ベスト" : "Best";
            bestValue.text = bestScore > 0 ? bestScore.ToString("N0") : "—";
        }

        if (newRecordBadge != null)
        {
            bool showBadge = isChapterClear && isNewRecord;
            newRecordBadge.gameObject.SetActive(showBadge);
            if (showBadge)
                newRecordBadge.text = ja ? "★ 自己ベスト更新!" : "★ NEW RECORD!";
        }

        breakdownText.text = breakdown ?? string.Empty;
        continueText.text = isChapterClear
            ? (ja ? "ロビーへ戻る" : "Return to Lobby")
            : (ja ? "次へ" : "Continue");

        SetOpen(true);
    }

    // ゲームオーバー時のリザルト。閉じるとロビーへ戻る。
    public void ShowGameOver(int score, float seconds, string subtitle)
    {
        BuildIfNeeded();
        resultIsChapterClear = false;
        resultIsGameOver = true;
        bool ja = LocalizationManager.IsJapanese;
        LocalizationManager.ApplyFont(titleText);
        LocalizationManager.ApplyFont(subtitleText);
        LocalizationManager.ApplyFont(timeLabel);
        LocalizationManager.ApplyFont(timeValue);
        LocalizationManager.ApplyFont(scoreLabel);
        LocalizationManager.ApplyFont(scoreValue);
        LocalizationManager.ApplyFont(breakdownText);
        LocalizationManager.ApplyFont(continueText);

        titleText.color = new Color(1f, 0.42f, 0.38f); // ゲームオーバーは赤
        titleText.text = ja ? "ゲームオーバー" : "GAME OVER";
        subtitleText.text = string.IsNullOrEmpty(subtitle) ? (ja ? "味方が全滅した" : "Your team was defeated") : subtitle;
        timeLabel.text = ja ? "到達タイム" : "Time";
        timeValue.text = FormatTime(seconds);
        scoreLabel.text = ja ? "スコア" : "Score";
        scoreValue.text = score.ToString("N0");

        if (bestLabel != null) bestLabel.gameObject.SetActive(false);
        if (bestValue != null) bestValue.gameObject.SetActive(false);
        if (newRecordBadge != null) newRecordBadge.gameObject.SetActive(false);
        breakdownText.text = string.Empty;
        continueText.text = ja ? "ロビーへ戻る" : "Return to Lobby";

        SetOpen(true);
    }

    public void Hide() { SetOpen(false); }

    private void SetOpenInstant(bool open)
    {
        isOpen = open;
        if (dimObject != null)
            dimObject.SetActive(open);
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(open);
            if (panelGroup != null)
                panelGroup.alpha = open ? 1f : 0f;
            panelRect.localScale = Vector3.one;
        }
        if (!open)
            Time.timeScale = OptionsPanelUI.DesiredGameSpeed; // チャプター全体で倍速を維持
    }

    private void SetOpen(bool open)
    {
        if (panelRect == null)
            return;
        if (isOpen == open)
            return;

        isOpen = open;
        if (open)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (dimObject != null) dimObject.SetActive(true);
            panelRect.gameObject.SetActive(true);
            panelGroup.alpha = 0f;
            panelRect.localScale = Vector3.one * 0.9f;
            panelGroup.DOFade(1f, 0.22f).SetUpdate(true);
            panelRect.DOScale(1f, 0.32f).SetEase(Ease.OutBack).SetUpdate(true);
            if (titleText != null)
            {
                titleText.transform.localScale = Vector3.one;
                titleText.transform.DOScale(1.08f, 0.22f).SetUpdate(true).SetEase(Ease.OutQuad)
                    .OnComplete(() => titleText.transform.DOScale(1f, 0.18f).SetUpdate(true).SetEase(Ease.InOutQuad));
            }
        }
        else
        {
            Time.timeScale = OptionsPanelUI.DesiredGameSpeed; // チャプター全体で倍速を維持
            panelGroup.DOFade(0f, 0.18f).SetUpdate(true);
            panelRect.DOScale(0.94f, 0.18f).SetUpdate(true).OnComplete(() =>
            {
                panelRect.gameObject.SetActive(false);
                if (dimObject != null) dimObject.SetActive(false);
            });
        }
    }

    private bool resultIsChapterClear;
    private bool resultIsGameOver;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：リザルトが開いているか・結果種別の参照と、続行（ロビー復帰）の自動押下。
    public bool IsResultOpen => isOpen;
    public bool LastResultWasChapterClear => resultIsChapterClear;
    public bool LastResultWasGameOver => resultIsGameOver;
    public bool DebugContinue()
    {
        if (!isOpen) return false;
        OnContinueClicked();
        return true;
    }
#endif

    private void OnContinueClicked()
    {
        // 章クリア／ゲームオーバーのリザルトを閉じたらロビー（LobbyScene）へ戻る。
        if ((resultIsChapterClear || resultIsGameOver) && GameManager.Instance != null)
        {
            resultIsChapterClear = false;
            resultIsGameOver = false;
            Time.timeScale = 1f;
            GameManager.Instance.RequestReturnToLobby();
            return;
        }
        Hide();
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
        Image dimImage = dimObject.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.65f);
        dimImage.raycastTarget = true;

        // パネル本体
        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObj.transform.SetParent(transform, false);
        panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(540f, 580f);
        Image panelBg = panelObj.GetComponent<Image>();
        // 9-slice の素材パネル。素材が無ければ従来のベタ塗りにフォールバック。
        Sprite resultPanelSprite = Resources.Load<Sprite>("UI/Panels/result_panel");
        if (resultPanelSprite != null)
        {
            panelBg.sprite = resultPanelSprite;
            panelBg.type = Image.Type.Sliced;
            panelBg.color = new Color(0.72f, 0.78f, 0.92f, 1f);
        }
        else
        {
            panelBg.color = new Color(0.02f, 0.06f, 0.10f, 0.97f);
        }
        panelGroup = panelObj.GetComponent<CanvasGroup>();

        // テキストの視認性のため、枠の内側に半透明の暗幕を敷く（外周の枠デザインは残す）。
        GameObject borderObj = new GameObject("InnerFill", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(panelRect, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(-46f, -56f);
        borderRect.anchoredPosition = Vector2.zero;
        Image borderImage = borderObj.GetComponent<Image>();
        borderImage.color = new Color(0.03f, 0.06f, 0.11f, 0.72f);
        borderImage.raycastTarget = false;

        titleText = CreateText("Title", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(500f, 40f), 30f, FontStyles.Bold, new Color(1f, 0.92f, 0.55f));
        titleText.alignment = TextAlignmentOptions.Center;

        subtitleText = CreateText("Subtitle", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(500f, 24f), 14f, FontStyles.Normal, new Color(0.85f, 0.92f, 1f, 0.85f));
        subtitleText.alignment = TextAlignmentOptions.Center;

        timeLabel = CreateText("TimeLabel", panelRect, new Vector2(0f, 1f), new Vector2(36f, -140f), new Vector2(200f, 24f), 16f, FontStyles.Bold, new Color(0.78f, 0.88f, 0.98f));
        timeLabel.alignment = TextAlignmentOptions.MidlineLeft;
        timeValue = CreateText("TimeValue", panelRect, new Vector2(1f, 1f), new Vector2(-36f, -140f), new Vector2(220f, 28f), 22f, FontStyles.Bold, new Color(0.98f, 1f, 1f));
        timeValue.alignment = TextAlignmentOptions.MidlineRight;

        scoreLabel = CreateText("ScoreLabel", panelRect, new Vector2(0f, 1f), new Vector2(36f, -180f), new Vector2(200f, 24f), 16f, FontStyles.Bold, new Color(0.78f, 0.88f, 0.98f));
        scoreLabel.alignment = TextAlignmentOptions.MidlineLeft;
        scoreValue = CreateText("ScoreValue", panelRect, new Vector2(1f, 1f), new Vector2(-36f, -180f), new Vector2(280f, 34f), 28f, FontStyles.Bold, new Color(1f, 0.92f, 0.55f));
        scoreValue.alignment = TextAlignmentOptions.MidlineRight;

        // ベストスコア行（章クリア時のみ表示）
        bestLabel = CreateText("BestLabel", panelRect, new Vector2(0f, 1f), new Vector2(36f, -222f), new Vector2(200f, 22f), 14f, FontStyles.Normal, new Color(0.74f, 0.84f, 0.96f, 0.85f));
        bestLabel.alignment = TextAlignmentOptions.MidlineLeft;
        bestValue = CreateText("BestValue", panelRect, new Vector2(1f, 1f), new Vector2(-36f, -222f), new Vector2(240f, 22f), 18f, FontStyles.Bold, new Color(0.94f, 0.96f, 1f, 0.95f));
        bestValue.alignment = TextAlignmentOptions.MidlineRight;

        // NEW RECORD バッジ（章クリア+自己新時のみ表示）
        newRecordBadge = CreateText("NewRecord", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(360f, 22f), 16f, FontStyles.Bold, new Color(1f, 0.55f, 0.35f));
        newRecordBadge.alignment = TextAlignmentOptions.Center;

        // 区切り線
        GameObject sepObj = new GameObject("Separator", typeof(RectTransform), typeof(Image));
        sepObj.transform.SetParent(panelRect, false);
        RectTransform sepRect = sepObj.GetComponent<RectTransform>();
        sepRect.anchorMin = new Vector2(0f, 1f);
        sepRect.anchorMax = new Vector2(1f, 1f);
        sepRect.pivot = new Vector2(0.5f, 1f);
        sepRect.anchoredPosition = new Vector2(0f, -278f);
        sepRect.sizeDelta = new Vector2(-72f, 2f);
        sepObj.GetComponent<Image>().color = new Color(0.4f, 0.6f, 0.78f, 0.4f);

        breakdownText = CreateText("Breakdown", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(480f, 220f), 14f, FontStyles.Normal, new Color(0.88f, 0.94f, 1f));
        breakdownText.alignment = TextAlignmentOptions.TopLeft;
        breakdownText.enableWordWrapping = true;
        breakdownText.lineSpacing = 3f;
        breakdownText.paragraphSpacing = 4f;

        // 次へボタン
        continueButton = CreateButton("Continue", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(320f, 44f), out continueText);
        continueButton.GetComponent<Image>().color = new Color(0.22f, 0.6f, 0.85f, 1f);
        continueButton.onClick.AddListener(OnContinueClicked);
    }

    private void RefreshLanguage()
    {
        // ShowStageResult が再度呼ばれない限り、最後に表示した文字列が残っているため
        // フォント適用のみ実施します（文言は次回更新時に切替）。
        if (titleText != null) LocalizationManager.ApplyFont(titleText);
        if (subtitleText != null) LocalizationManager.ApplyFont(subtitleText);
        if (timeLabel != null) LocalizationManager.ApplyFont(timeLabel);
        if (timeValue != null) LocalizationManager.ApplyFont(timeValue);
        if (scoreLabel != null) LocalizationManager.ApplyFont(scoreLabel);
        if (scoreValue != null) LocalizationManager.ApplyFont(scoreValue);
        if (bestLabel != null) LocalizationManager.ApplyFont(bestLabel);
        if (bestValue != null) LocalizationManager.ApplyFont(bestValue);
        if (newRecordBadge != null) LocalizationManager.ApplyFont(newRecordBadge);
        if (breakdownText != null) LocalizationManager.ApplyFont(breakdownText);
        if (continueText != null) LocalizationManager.ApplyFont(continueText);
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = s / 60;
        int remaining = s % 60;
        return $"{minutes:00}:{remaining:00}";
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
        labelText.fontSize = 16f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        return button;
    }
}
