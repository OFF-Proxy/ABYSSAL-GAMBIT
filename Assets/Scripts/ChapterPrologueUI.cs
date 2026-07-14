using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// STORY: 章開始時、盤面の前に出す全画面プロローグ（一枚絵＋ナレーション字幕＋専用BGM）。
// 起承転結の「起」を見せる。各行はクリックか自動送りで進み、最後に暗転→ゲーム画面へフェードイン。
public class ChapterPrologueUI : MonoBehaviour
{
    public static ChapterPrologueUI Instance { get; private set; }

    private bool isBuilt;
    private Image artImage;       // 一枚絵（全画面）
    private Image blackOverlay;   // 暗転/フェード用の黒
    private RectTransform subBox;
    private CanvasGroup subGroup;
    private TextMeshProUGUI subText;
    private TextMeshProUGUI hintText;

    private const float AutoSecondsPerLine = 6.0f; // 「読み終えるくらい」の自動送り時間。

    public static ChapterPrologueUI EnsureExists()
    {
        if (Instance != null) return Instance;
        ChapterPrologueUI ex = FindObjectOfType<ChapterPrologueUI>(true);
        if (ex != null) { Instance = ex; ex.BuildIfNeeded(); return Instance; }

        GameObject root = new GameObject("ChapterPrologueUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 26000; // 盤面/HUD/警告/結果より前面の最上位。
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // 生成時の AddComponent 群に ChapterPrologueUI を含めていなかったため GetComponent が null を返し、
        // BuildIfNeeded で NRE → プロローグが生成されない不具合があった。明示的に AddComponent する。
        Instance = root.GetComponent<ChapterPrologueUI>() ?? root.AddComponent<ChapterPrologueUI>();
        Instance.BuildIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; BuildIfNeeded();
    }

    private void BuildIfNeeded()
    {
        if (isBuilt) return;

        artImage = MakeFullScreen("Art", new Color(0.02f, 0.02f, 0.03f, 1f));
        artImage.preserveAspect = false; // 全画面カバー（多少の伸びは許容）。
        artImage.raycastTarget = true;   // 背後のショップ等にクリックを貫通させない（購入/リロール防止）。

        subBox = MakeBottomBox(out subGroup, out subText);
        hintText = MakeLabel(transform as RectTransform, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 36f), new Vector2(900f, 40f), 26f, FontStyles.Italic, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center);

        blackOverlay = MakeFullScreen("Black", new Color(0f, 0f, 0f, 1f)); // 最前面の黒。最初は不透明。
        blackOverlay.raycastTarget = true; // 暗転中もクリックを吸収。

        isBuilt = true;
        gameObject.SetActive(true);
        SetRootActive(false);
    }

    private Image MakeFullScreen(string n, Color c)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
        return img;
    }

    private RectTransform MakeBottomBox(out CanvasGroup grp, out TextMeshProUGUI txt)
    {
        GameObject box = new GameObject("SubBox", typeof(RectTransform), typeof(CanvasGroup));
        box.transform.SetParent(transform, false);
        RectTransform r = box.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0f); r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0f, 96f);
        r.sizeDelta = new Vector2(1500f, 220f);
        grp = box.GetComponent<CanvasGroup>(); grp.alpha = 0f;

        GameObject bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(box.transform, false);
        RectTransform br = bg.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
        Image bgi = bg.GetComponent<Image>(); bgi.color = new Color(0.02f, 0.03f, 0.05f, 0.62f); bgi.raycastTarget = false;

        txt = MakeLabel(box.transform as RectTransform, "Text", new Vector2(0f, 0f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero, 40f, FontStyles.Normal, new Color(0.97f, 0.96f, 0.95f), TextAlignmentOptions.Center);
        txt.rectTransform.offsetMin = new Vector2(60f, 24f); txt.rectTransform.offsetMax = new Vector2(-60f, -24f);
        txt.enableWordWrapping = true;
        return r;
    }

    private TextMeshProUGUI MakeLabel(RectTransform parent, string n, Vector2 aMin, Vector2 aMax, Vector2 anchoredPos, Vector2 size, float fs, FontStyles style, Color col, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        if (aMin == aMax) { r.anchoredPosition = anchoredPos; r.sizeDelta = size; }
        else { r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.alignment = align; LocalizationManager.ApplyFont(t);
        t.fontSize = fs; t.fontStyle = style; t.color = col; t.raycastTarget = false;
        t.outlineWidth = 0.2f; t.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        return t;
    }

    // プロローグ表示中か（背後ショップ等の入力を抑止する判定に使う）。
    public bool IsShowing => isBuilt && artImage != null && artImage.gameObject.activeSelf;

    private Action pendingComplete; // 演出途中で打ち切る時に呼ぶ完了コールバック（→オープニングVNへ）。

    // オートプレイ用：プロローグ演出を即終了して次へ進める。表示中に解決したら true。
    // プロローグは実時間(unscaled)で進むため、高速オートプレイ中は表示が残り IsStoryIntroBlocking で
    // 購入がブロックされ続ける（＝空編成で全滅）。これを即解決して購入を解放する。
    public bool DebugAutoResolve()
    {
        if (!IsShowing) return false;
        StopAllCoroutines();
        AttackEffectPlayer.PlayBattleBgm();
        var cb = pendingComplete; pendingComplete = null;
        SetRootActive(false);
        cb?.Invoke();
        return true;
    }

    private void SetRootActive(bool on)
    {
        if (artImage != null) artImage.gameObject.SetActive(on);
        if (subBox != null) subBox.gameObject.SetActive(on);
        if (hintText != null) hintText.gameObject.SetActive(on);
        if (blackOverlay != null) blackOverlay.gameObject.SetActive(on);
        var gr = GetComponent<GraphicRaycaster>(); if (gr != null) gr.enabled = on;
    }

    // art: 一枚絵（null可＝暗い背景）。lines: ナレーション字幕。bgmPaths: 専用BGMの候補(Resources)。
    public void Show(Sprite art, string[] lines, string[] bgmPaths, Action onComplete)
    {
        BuildIfNeeded();
        if (art != null) artImage.sprite = art;
        artImage.color = art != null ? Color.white : new Color(0.03f, 0.03f, 0.05f, 1f);
        pendingComplete = onComplete; // 途中打ち切り(DebugAutoResolve)用に保持。
        SetRootActive(true);
        StartCoroutine(Run(lines, bgmPaths, onComplete));
    }

    private IEnumerator Run(string[] lines, string[] bgmPaths, Action onComplete)
    {
        bool ja = LocalizationManager.IsJapanese;
        // 専用BGMへ切替。
        if (bgmPaths != null && bgmPaths.Length > 0) AttackEffectPlayer.PlayBgm(bgmPaths);

        SetBlack(1f); subGroup.alpha = 0f; hintText.text = "";
        // 黒→透明：一枚絵がフェードインで現れる。
        yield return Fade(blackOverlay, 0f, 1.1f);

        if (lines != null)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                subText.text = lines[i];
                LocalizationManager.ApplyFont(subText);
                hintText.text = (i >= lines.Length - 1)
                    ? (ja ? "クリックで進む" : "Click to continue")
                    : (ja ? "クリックで次へ" : "Click for next");
                yield return FadeCanvas(subGroup, 1f, 0.3f);
                yield return WaitClickOrTime(AutoSecondsPerLine);
                if (i < lines.Length - 1) yield return FadeCanvas(subGroup, 0f, 0.2f);
            }
        }
        // 暗転：一枚絵→黒。字幕も消す。
        FadeCanvas(subGroup, 0f, 0.4f); hintText.text = "";
        yield return Fade(blackOverlay, 1f, 1.0f);

        // ゲームBGMへ戻し、暗転からフェードインでゲーム画面へ。
        AttackEffectPlayer.PlayBattleBgm();
        yield return new WaitForSecondsRealtime(0.3f);
        onComplete?.Invoke();
        // 黒をフェードアウトする前に、自分の一枚絵/字幕を先に隠す。
        // （隠さないと黒明けでプロローグの一枚絵が一瞬だけ再表示されてしまう。）
        if (artImage != null) artImage.gameObject.SetActive(false);
        if (subBox != null) subBox.gameObject.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);
        yield return Fade(blackOverlay, 0f, 1.1f);

        SetRootActive(false);
    }

    private IEnumerator WaitClickOrTime(float seconds)
    {
        float t = 0f;
        // 表示直後の誤クリック防止に最低0.3s待つ。
        while (t < 0.3f) { t += Time.unscaledDeltaTime; yield return null; }
        while (t < seconds)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) yield break;
            t += Time.unscaledDeltaTime; yield return null;
        }
    }

    private IEnumerator Fade(Image img, float targetAlpha, float dur)
    {
        if (img == null) yield break;
        img.DOKill();
        yield return img.DOFade(targetAlpha, dur).SetUpdate(true).WaitForCompletion();
    }

    private IEnumerator FadeCanvas(CanvasGroup g, float a, float dur)
    {
        if (g == null) yield break;
        g.DOKill();
        yield return g.DOFade(a, dur).SetUpdate(true).WaitForCompletion();
    }

    private void SetBlack(float a)
    {
        if (blackOverlay == null) return;
        Color c = blackOverlay.color; c.a = a; blackOverlay.color = c;
    }
}
