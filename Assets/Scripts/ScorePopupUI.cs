using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 戦闘クリア・中ボス撃破・章ボス撃破などの加点が発生した瞬間に、
// 「+100 戦闘クリア!」のような小さなフロート表示を画面中央上部に出します。
// DOTween で 0.9 秒ほど上昇＋フェードして自動消滅します。
public class ScorePopupUI : MonoBehaviour
{
    public static ScorePopupUI Instance { get; private set; }

    private RectTransform stackRoot;
    private bool isBuilt;
    private float nextSlotOffset;

    private const float SlotHeight = 38f;
    private const float SlotResetGap = 1.4f;
    private float lastSpawnTime;

    public static ScorePopupUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        ScorePopupUI existing = FindObjectOfType<ScorePopupUI>(true);
        if (existing != null)
        {
            Instance = existing;
            Instance.BuildIfNeeded();
            return Instance;
        }

        GameObject root = new GameObject("ScorePopupUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ScorePopupUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 49500;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        Instance = root.GetComponent<ScorePopupUI>();
        Instance.BuildIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildIfNeeded();
    }

    private void BuildIfNeeded()
    {
        if (isBuilt) return;
        GameObject stackObj = new GameObject("Stack", typeof(RectTransform));
        stackObj.transform.SetParent(transform, false);
        stackRoot = stackObj.GetComponent<RectTransform>();
        stackRoot.anchorMin = stackRoot.anchorMax = new Vector2(0.5f, 1f);
        stackRoot.pivot = new Vector2(0.5f, 1f);
        stackRoot.anchoredPosition = new Vector2(0f, -180f);
        stackRoot.sizeDelta = new Vector2(400f, 60f);
        isBuilt = true;
    }

    public void Show(int amount, string reason, Color? tint = null)
    {
        if (amount == 0) return;
        BuildIfNeeded();

        if (Time.unscaledTime - lastSpawnTime > SlotResetGap)
            nextSlotOffset = 0f;
        lastSpawnTime = Time.unscaledTime;

        GameObject popupObj = new GameObject("ScorePopup", typeof(RectTransform), typeof(CanvasGroup));
        popupObj.transform.SetParent(stackRoot, false);
        RectTransform popupRect = popupObj.GetComponent<RectTransform>();
        popupRect.anchorMin = popupRect.anchorMax = new Vector2(0.5f, 1f);
        popupRect.pivot = new Vector2(0.5f, 1f);
        popupRect.sizeDelta = new Vector2(400f, SlotHeight);
        popupRect.anchoredPosition = new Vector2(0f, -nextSlotOffset);
        CanvasGroup popupGroup = popupObj.GetComponent<CanvasGroup>();
        popupGroup.alpha = 0f;

        Color color = tint.HasValue ? tint.Value : new Color(1f, 0.92f, 0.55f);

        GameObject amountObj = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
        amountObj.transform.SetParent(popupRect, false);
        RectTransform amountRect = amountObj.GetComponent<RectTransform>();
        amountRect.anchorMin = amountRect.anchorMax = new Vector2(0.5f, 0.5f);
        amountRect.pivot = new Vector2(1f, 0.5f);
        amountRect.anchoredPosition = new Vector2(-6f, 0f);
        amountRect.sizeDelta = new Vector2(140f, SlotHeight);
        TextMeshProUGUI amountText = amountObj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(amountText);
        amountText.fontSize = 26f;
        amountText.fontStyle = FontStyles.Bold;
        amountText.alignment = TextAlignmentOptions.MidlineRight;
        amountText.color = color;
        amountText.raycastTarget = false;
        amountText.text = amount > 0 ? $"+{amount:N0}" : amount.ToString("N0");

        GameObject reasonObj = new GameObject("Reason", typeof(RectTransform), typeof(TextMeshProUGUI));
        reasonObj.transform.SetParent(popupRect, false);
        RectTransform reasonRect = reasonObj.GetComponent<RectTransform>();
        reasonRect.anchorMin = reasonRect.anchorMax = new Vector2(0.5f, 0.5f);
        reasonRect.pivot = new Vector2(0f, 0.5f);
        reasonRect.anchoredPosition = new Vector2(6f, 0f);
        reasonRect.sizeDelta = new Vector2(240f, SlotHeight);
        TextMeshProUGUI reasonText = reasonObj.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(reasonText);
        reasonText.fontSize = 16f;
        reasonText.fontStyle = FontStyles.Bold;
        reasonText.alignment = TextAlignmentOptions.MidlineLeft;
        reasonText.color = new Color(0.92f, 0.96f, 1f, 0.92f);
        reasonText.raycastTarget = false;
        reasonText.text = reason ?? string.Empty;

        nextSlotOffset += SlotHeight;

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        seq.Append(popupGroup.DOFade(1f, 0.12f));
        seq.Join(popupRect.DOAnchorPosY(popupRect.anchoredPosition.y + 6f, 0.12f).SetEase(Ease.OutQuad));
        seq.AppendInterval(0.55f);
        seq.Append(popupGroup.DOFade(0f, 0.25f));
        seq.Join(popupRect.DOAnchorPosY(popupRect.anchoredPosition.y + 22f, 0.25f).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            if (popupObj != null) Destroy(popupObj);
        });
    }
}
