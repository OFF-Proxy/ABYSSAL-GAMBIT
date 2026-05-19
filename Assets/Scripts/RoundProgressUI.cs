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

    // 進行アイコンです。画像の余白ズレを避けるため、未設定なら中央揃えの簡易Spriteを実行時生成します。
    public Sprite currentWaveSprite;
    public Sprite clearedWaveSprite;
    public Sprite bossWaveSprite;

    // 生成したUI部品を保持して、ウェーブ数が変わっても再利用します。
    RectTransform rootRect;
    RectTransform iconsRect;
    TextMeshProUGUI titleText;
    Transform iconParent;
    readonly List<Image> waveIcons = new List<Image>();
    int lastNextWaveIndex;
    int lastTotalWaves;
    bool lastGameOver;
    bool lastAllClear;
    List<bool> lastBossWaveFlags = new List<bool>();

    private const float RootTopMargin = 8f;
    private const float RootMinWidth = 260f;
    private const float RootHorizontalPadding = 34f;
    private const float RootHeight = 70f;
    private const float TitleHeight = 24f;
    private const float MaxIconSize = 26f;
    private const float MinIconSize = 13f;
    private const float MaxIconSpacing = 11f;
    private const float MinIconSpacing = 4f;

    // Unityが生成直後に呼ぶ初期化処理です。
    private void Awake()
    {
        LocalizationManager.EnsureExists();
        Instance = this;
        LoadSpritesIfNeeded();
        EnsureUiParts();
        LocalizationManager.OnLanguageChanged += RefreshLanguage;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        LocalizationManager.OnLanguageChanged -= RefreshLanguage;
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
        rectTransform.anchoredPosition = new Vector2(0f, -RootTopMargin);
        rectTransform.sizeDelta = new Vector2(340f, RootHeight);

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
        CacheProgress(nextWaveIndex, totalWaves, gameOver, allClear, bossWaveFlags);

        if (totalWaves <= 0)
        {
            titleText.text = string.Empty;
            return;
        }

        int clampedWaveIndex = Mathf.Clamp(nextWaveIndex, 0, totalWaves - 1);
        ResizeForWaveCount(totalWaves);

        if (gameOver)
            titleText.text = LocalizationManager.IsJapanese ? "ゲームオーバー" : "GAME OVER";
        else if (allClear)
            titleText.text = LocalizationManager.IsJapanese ? "全ウェーブクリア" : "ALL CLEAR";
        else if (IsBossWaveIndex(clampedWaveIndex, bossWaveFlags))
            titleText.text = LocalizationManager.IsJapanese ? $"ボス {clampedWaveIndex + 1}/{totalWaves}" : $"BOSS {clampedWaveIndex + 1}/{totalWaves}";
        else
            titleText.text = LocalizationManager.IsJapanese ? $"ウェーブ {clampedWaveIndex + 1}/{totalWaves}" : $"WAVE {clampedWaveIndex + 1}/{totalWaves}";

        for (int i = 0; i < waveIcons.Count; i++)
        {
            bool cleared = allClear || i < nextWaveIndex;
            bool current = !allClear && !gameOver && i == clampedWaveIndex;
            bool bossWave = IsBossWaveIndex(i, bossWaveFlags);

            Image icon = waveIcons[i];
            icon.sprite = GetWaveIconSprite(cleared, bossWave);
            icon.color = GetIconColor(cleared, current, gameOver, bossWave);
            SetIconSize(icon, GetIconSize(totalWaves, current, bossWave));
            icon.gameObject.SetActive(i < totalWaves);
        }
    }

    // アイコンSpriteを用意します。外部画像が未設定でも、中央に揃う図形を生成して使います。
    private void LoadSpritesIfNeeded()
    {
        currentWaveSprite = CreateCircleSprite("RoundIcon_Normal");
        clearedWaveSprite = CreateCircleSprite("RoundIcon_Clear");
        bossWaveSprite = CreateDiamondSprite("RoundIcon_Boss");
    }

    // タイトル文字とアイコン置き場が無ければ作ります。
    private void EnsureUiParts()
    {
        if (titleText != null && iconParent != null)
            return;

        rootRect = GetComponent<RectTransform>();

        GameObject titleObject = new GameObject("WaveText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(transform, false);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -5f);
        titleRect.sizeDelta = new Vector2(0f, TitleHeight);

        titleText = titleObject.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(titleText);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 20f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.9f, 1f, 1f, 1f);
        titleText.raycastTarget = false;

        GameObject iconsObject = new GameObject("WaveIcons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        iconsObject.transform.SetParent(transform, false);
        iconsRect = iconsObject.GetComponent<RectTransform>();
        iconsRect.anchorMin = new Vector2(0.5f, 0f);
        iconsRect.anchorMax = new Vector2(0.5f, 0f);
        iconsRect.pivot = new Vector2(0.5f, 0.5f);
        iconsRect.anchoredPosition = new Vector2(0f, 21f);
        iconsRect.sizeDelta = new Vector2(rootRect != null ? rootRect.sizeDelta.x : 320f, 34f);

        HorizontalLayoutGroup layoutGroup = iconsObject.GetComponent<HorizontalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.spacing = MaxIconSpacing;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        iconParent = iconsObject.transform;
    }

    // ウェーブ数が増えても、上部中央に収まる幅とアイコンサイズへ調整します。
    private void ResizeForWaveCount(int totalWaves)
    {
        if (rootRect == null || iconsRect == null)
            return;

        float availableWidth = Mathf.Max(RootMinWidth, Screen.width - 120f);
        float iconSize = GetBaseIconSize(totalWaves);
        float spacing = GetIconSpacing(totalWaves);
        float iconStripWidth = Mathf.Max(iconSize, totalWaves * iconSize + Mathf.Max(0, totalWaves - 1) * spacing);
        float rootWidth = Mathf.Clamp(iconStripWidth + RootHorizontalPadding, RootMinWidth, availableWidth);

        rootRect.sizeDelta = new Vector2(rootWidth, RootHeight);
        iconsRect.sizeDelta = new Vector2(Mathf.Max(0f, rootWidth - 24f), 34f);

        HorizontalLayoutGroup layoutGroup = iconParent != null ? iconParent.GetComponent<HorizontalLayoutGroup>() : null;
        if (layoutGroup != null)
            layoutGroup.spacing = spacing;
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
            return new Color(1f, 0.24f, 0.5f, 0.75f);

        if (current)
            return new Color(0.95f, 1f, 1f, 1f);

        return new Color(0.55f, 0.7f, 0.8f, 0.42f);
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

    // 基本のアイコンサイズを、ウェーブ数に合わせて少しずつ縮めます。
    private float GetBaseIconSize(int totalWaves)
    {
        if (totalWaves <= 0)
            return MaxIconSize;

        float availableWidth = Mathf.Max(RootMinWidth, Screen.width - 120f) - RootHorizontalPadding - 24f;
        float sizeByWidth = (availableWidth - Mathf.Max(0, totalWaves - 1) * MinIconSpacing) / totalWaves;
        return Mathf.Clamp(sizeByWidth, MinIconSize, MaxIconSize);
    }

    // アイコン間隔も、ウェーブ数が多い時は詰めます。
    private float GetIconSpacing(int totalWaves)
    {
        if (totalWaves <= 8)
            return MaxIconSpacing;

        float t = Mathf.InverseLerp(8f, 24f, totalWaves);
        return Mathf.Lerp(MaxIconSpacing, MinIconSpacing, t);
    }

    // 現在ウェーブとボスウェーブは少し大きくしますが、レイアウトを崩さない範囲に抑えます。
    private float GetIconSize(int totalWaves, bool current, bool bossWave)
    {
        float size = GetBaseIconSize(totalWaves);
        if (bossWave)
            size += 3f;

        if (current)
            size += 4f;

        return Mathf.Min(size, MaxIconSize + 6f);
    }

    // 丸い進行アイコンをコードで作ります。
    private Sprite CreateCircleSprite(string spriteName)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = spriteName;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.38f;
        float radiusSqr = radius * radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distanceSqr = ((Vector2)new Vector2(x, y) - center).sqrMagnitude;
                texture.SetPixel(x, y, distanceSqr <= radiusSqr ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ボスウェーブ用のひし形アイコンをコードで作ります。
    private Sprite CreateDiamondSprite(string spriteName)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = spriteName;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.39f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // 指定インデックスがボスウェーブか確認します。
    private bool IsBossWaveIndex(int waveIndex, IReadOnlyList<bool> bossWaveFlags)
    {
        return bossWaveFlags != null
            && waveIndex >= 0
            && waveIndex < bossWaveFlags.Count
            && bossWaveFlags[waveIndex];
    }

    // 言語切替時に、現在の進行表示を同じ状態のまま書き直します。
    private void RefreshLanguage()
    {
        LocalizationManager.ApplyFont(titleText);
        if (lastTotalWaves > 0)
            SetProgress(lastNextWaveIndex, lastTotalWaves, lastGameOver, lastAllClear, new List<bool>(lastBossWaveFlags));
    }

    // 言語切替後も同じウェーブ表示へ戻せるよう、最後に受け取った進行状態を保存します。
    private void CacheProgress(int nextWaveIndex, int totalWaves, bool gameOver, bool allClear, IReadOnlyList<bool> bossWaveFlags)
    {
        lastNextWaveIndex = nextWaveIndex;
        lastTotalWaves = totalWaves;
        lastGameOver = gameOver;
        lastAllClear = allClear;
        lastBossWaveFlags.Clear();

        if (bossWaveFlags == null)
            return;

        for (int i = 0; i < bossWaveFlags.Count; i++)
            lastBossWaveFlags.Add(bossWaveFlags[i]);
    }
}
