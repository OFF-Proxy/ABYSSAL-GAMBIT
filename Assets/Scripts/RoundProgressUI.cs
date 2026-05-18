using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 画面上部中央に、現在のウェーブ位置を表示する小さなUIです。
// シーンに手で置かれていなくても、GameManagerから呼ばれた時にCanvas上へ自動生成します。
public class RoundProgressUI : MonoBehaviour
{
    // どこからでも現在の進行UIへアクセスするための参照です。
    public static RoundProgressUI Instance { get; private set; }

    // referenceからコピーした進行アイコンです。未設定ならResourcesから自動で読み込みます。
    public Sprite currentWaveSprite;
    public Sprite clearedWaveSprite;
    public Sprite bossWaveSprite;

    // 生成したUI部品を保持して、ウェーブ数が変わっても再利用します。
    TextMeshProUGUI titleText;
    Transform iconParent;
    readonly List<Image> waveIcons = new List<Image>();

    // Unityが生成直後に呼ぶ初期化処理です。
    private void Awake()
    {
        Instance = this;
        LoadSpritesIfNeeded();
        EnsureUiParts();
    }

    // UIが存在しなければCanvas上へ作り、進行UIを返します。
    public static RoundProgressUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        RoundProgressUI existingUi = FindObjectOfType<RoundProgressUI>();
        if (existingUi != null)
        {
            Instance = existingUi;
            existingUi.LoadSpritesIfNeeded();
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

        GameObject uiObject = new GameObject("RoundProgressUI", typeof(RectTransform), typeof(Image), typeof(RoundProgressUI));
        uiObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = uiObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -18f);
        rectTransform.sizeDelta = new Vector2(340f, 82f);

        Image background = uiObject.GetComponent<Image>();
        background.color = new Color(0f, 0.04f, 0.08f, 0.62f);
        background.raycastTarget = false;

        Instance = uiObject.GetComponent<RoundProgressUI>();
        Instance.LoadSpritesIfNeeded();
        Instance.EnsureUiParts();
        return Instance;
    }

    // 現在ウェーブと総ウェーブ数を表示します。
    public void SetProgress(int nextWaveIndex, int totalWaves, bool gameOver, bool allClear, IReadOnlyList<bool> bossWaveFlags = null)
    {
        EnsureUiParts();
        EnsureIconCount(totalWaves);

        if (totalWaves <= 0)
        {
            titleText.text = string.Empty;
            return;
        }

        int clampedWaveIndex = Mathf.Clamp(nextWaveIndex, 0, totalWaves - 1);
        string dotText = BuildDotProgressText(clampedWaveIndex, totalWaves, allClear);

        if (gameOver)
            titleText.text = "GAME OVER";
        else if (allClear)
            titleText.text = $"ALL CLEAR  {dotText}";
        else if (IsBossWaveIndex(clampedWaveIndex, bossWaveFlags))
            titleText.text = $"BOSS WAVE {clampedWaveIndex + 1}/{totalWaves}  {dotText}";
        else
            titleText.text = $"WAVE {clampedWaveIndex + 1}/{totalWaves}  {dotText}";

        for (int i = 0; i < waveIcons.Count; i++)
        {
            bool cleared = allClear || i < nextWaveIndex;
            bool current = !allClear && !gameOver && i == clampedWaveIndex;
            bool bossWave = IsBossWaveIndex(i, bossWaveFlags);

            Image icon = waveIcons[i];
            icon.sprite = GetWaveIconSprite(cleared, bossWave);
            icon.color = GetIconColor(cleared, current, gameOver, bossWave);
            SetIconSize(icon, bossWave && !cleared ? 42f : 34f);
            icon.gameObject.SetActive(i < totalWaves);
        }
    }

    // Resources内にコピーしたreference画像を読み込みます。
    private void LoadSpritesIfNeeded()
    {
        if (currentWaveSprite == null)
            currentWaveSprite = LoadSpriteResource("UI/RoundProgress/run_current_game");

        if (clearedWaveSprite == null)
            clearedWaveSprite = LoadSpriteResource("UI/RoundProgress/run_win");

        if (bossWaveSprite == null)
            bossWaveSprite = LoadSpriteResource("UI/RoundProgress/run_line");
    }

    // Sprite設定で読み込めない場合でも、Texture2DからSpriteを作って使います。
    private Sprite LoadSpriteResource(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
    }

    // タイトル文字とアイコン置き場が無ければ作ります。
    private void EnsureUiParts()
    {
        if (titleText != null && iconParent != null)
            return;

        RectTransform root = GetComponent<RectTransform>();

        GameObject titleObject = new GameObject("WaveText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(transform, false);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -6f);
        titleRect.sizeDelta = new Vector2(0f, 30f);

        titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 22f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.9f, 1f, 1f, 1f);
        titleText.raycastTarget = false;

        GameObject iconsObject = new GameObject("WaveIcons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        iconsObject.transform.SetParent(transform, false);
        RectTransform iconsRect = iconsObject.GetComponent<RectTransform>();
        iconsRect.anchorMin = new Vector2(0.5f, 0f);
        iconsRect.anchorMax = new Vector2(0.5f, 0f);
        iconsRect.pivot = new Vector2(0.5f, 0f);
        iconsRect.anchoredPosition = new Vector2(0f, 8f);
        iconsRect.sizeDelta = new Vector2(root != null ? root.sizeDelta.x : 320f, 40f);

        HorizontalLayoutGroup layoutGroup = iconsObject.GetComponent<HorizontalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.spacing = 14f;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        iconParent = iconsObject.transform;
    }

    // 必要な数だけウェーブアイコンを作ります。
    private void EnsureIconCount(int totalWaves)
    {
        while (waveIcons.Count < totalWaves)
        {
            GameObject iconObject = new GameObject($"WaveIcon{waveIcons.Count + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(iconParent, false);

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(34f, 34f);

            LayoutElement layoutElement = iconObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 34f;
            layoutElement.preferredHeight = 34f;

            Image icon = iconObject.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            waveIcons.Add(icon);
        }
    }

    // 「・〇・」のような現在位置テキストを作ります。
    private string BuildDotProgressText(int currentWaveIndex, int totalWaves, bool allClear)
    {
        string result = string.Empty;
        for (int i = 0; i < totalWaves; i++)
            result += allClear || i < currentWaveIndex ? "●" : i == currentWaveIndex ? "〇" : "・";

        return result;
    }

    // クリア済み、現在、未到達、ゲームオーバーで色を変えます。
    private Color GetIconColor(bool cleared, bool current, bool gameOver, bool bossWave)
    {
        if (gameOver)
            return new Color(1f, 0.25f, 0.25f, 0.55f);

        if (cleared)
            return new Color(0.55f, 1f, 0.45f, 1f);

        if (bossWave && current)
            return new Color(1f, 0.42f, 0.18f, 1f);

        if (bossWave)
            return new Color(1f, 0.2f, 0.45f, 0.7f);

        if (current)
            return Color.white;

        return new Color(0.55f, 0.7f, 0.8f, 0.35f);
    }

    // ボスウェーブは通常ウェーブとは別のアイコン画像を使います。
    private Sprite GetWaveIconSprite(bool cleared, bool bossWave)
    {
        if (cleared)
            return clearedWaveSprite;

        if (bossWave && bossWaveSprite != null)
            return bossWaveSprite;

        return currentWaveSprite;
    }

    // ボスアイコンだけ少し大きくして、進行UI上でも特別なラウンドだと分かるようにします。
    private void SetIconSize(Image icon, float size)
    {
        if (icon == null)
            return;

        RectTransform rectTransform = icon.GetComponent<RectTransform>();
        if (rectTransform != null)
            rectTransform.sizeDelta = new Vector2(size, size);

        LayoutElement layoutElement = icon.GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.preferredWidth = size;
            layoutElement.preferredHeight = size;
        }
    }

    // 指定インデックスがボスウェーブか確認します。
    private bool IsBossWaveIndex(int waveIndex, IReadOnlyList<bool> bossWaveFlags)
    {
        return bossWaveFlags != null
            && waveIndex >= 0
            && waveIndex < bossWaveFlags.Count
            && bossWaveFlags[waveIndex];
    }
}
