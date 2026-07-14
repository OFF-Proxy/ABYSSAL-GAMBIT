using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// STORY-interlude: 戦闘外の長尺VN枠（幕間/エンディング）。
// ボス戦前3行(HeroBossDialogueUI)とは別枠。ト書き/ナレーション/通常セリフを1行ずつクリックで進める。
// 立ち絵は「話者1枚を切替表示」（確定方針）。台本は docs/Story/STORY_INTERLUDES.md・STORY_ENDINGS.md 由来。
public class InterludeUI : MonoBehaviour
{
    private const int SortingOrder = 25580; // ボス戦前ダイアログ(25560)より前面。

    public static InterludeUI Instance { get; private set; }

    // 1ビートの種別。Stage=ト書き（地の文・演出）/ Narration=ナレーション / Speech=セリフ。
    public enum BeatKind { Stage, Narration, Speech }

    public struct Beat
    {
        public BeatKind Kind;
        public string Name;       // 表示名（セリフ時）
        public string PortraitId; // 立ち絵解決用ユニットID（無ければ空）
        public string Text;
        public bool HeroSide;     // 立ち絵を右(主人公側)に出すか。falseは左。
    }

    private GameObject root;
    private Image dim;
    private Image portrait;
    private Image nameBar;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI hintText;
    private GameObject box;

    private readonly List<Beat> beats = new List<Beat>();
    private int index;
    private Action onComplete;

    public static InterludeUI EnsureExists()
    {
        if (Instance != null) return Instance;
        InterludeUI existing = FindObjectOfType<InterludeUI>(true);
        if (existing != null) { Instance = existing; existing.Build(); return existing; }

        GameObject go = new GameObject("InterludeUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(InterludeUI));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Instance = go.GetComponent<InterludeUI>();
        Instance.Build();
        return Instance;
    }

    private void Awake() { LocalizationManager.EnsureExists(); Instance = this; Build(); if (root != null) root.SetActive(false); }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public bool IsShowing => root != null && root.activeSelf;

    private void Build()
    {
        if (root != null) return;

        root = new GameObject("Root", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(transform, false);
        FillRect(root.GetComponent<RectTransform>());
        dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0.01f, 0.03f, 0.92f);
        root.GetComponent<Button>().transition = Selectable.Transition.None;
        root.GetComponent<Button>().onClick.AddListener(Advance);

        // 話者の立ち絵（1枚。話者の側に応じて左右へ寄せる）。画面いっぱいに大きく。
        portrait = MakeImage("Portrait", new Vector2(0.5f, 0.5f), new Vector2(1080f, 1180f));
        portrait.preserveAspect = true;

        // VN セリフ枠（下部）
        box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(root.transform, false);
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0.5f, 0f); br.anchorMax = new Vector2(0.5f, 0f); br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, 40f); br.sizeDelta = new Vector2(1420f, 280f);
        Image boxImg = box.GetComponent<Image>();
        Sprite panel = Resources.Load<Sprite>("UI/Augment/card_panel");
        if (panel != null) { boxImg.sprite = panel; boxImg.type = Image.Type.Sliced; boxImg.color = new Color(0.07f, 0.1f, 0.16f, 0.98f); }
        else boxImg.color = new Color(0.05f, 0.08f, 0.13f, 0.98f);
        boxImg.raycastTarget = false;

        nameBar = MakeChildImage(box.transform, "NameBar", new Vector2(0f, 1f), new Vector2(0f, 1f));
        RectTransform nbr = nameBar.rectTransform;
        nbr.pivot = new Vector2(0f, 1f); nbr.anchoredPosition = new Vector2(34f, 30f); nbr.sizeDelta = new Vector2(380f, 56f);
        nameBar.color = new Color(0.9f, 0.7f, 0.3f, 0.95f);
        nameText = MakeText(nameBar.transform, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
        FillRect(nameText.rectTransform);
        nameText.color = new Color(0.06f, 0.05f, 0.02f);

        bodyText = MakeText(box.transform, 28f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        RectTransform body = bodyText.rectTransform;
        body.anchorMin = new Vector2(0f, 0f); body.anchorMax = new Vector2(1f, 1f);
        body.offsetMin = new Vector2(40f, 30f); body.offsetMax = new Vector2(-40f, -84f);
        bodyText.enableWordWrapping = true; bodyText.color = new Color(0.95f, 0.97f, 1f);

        hintText = MakeText(box.transform, 18f, FontStyles.Italic, TextAlignmentOptions.BottomRight);
        RectTransform hr = hintText.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(1f, 0f);
        hr.anchoredPosition = new Vector2(-24f, 10f); hr.sizeDelta = new Vector2(420f, 28f);
        hintText.color = new Color(0.7f, 0.8f, 0.95f, 0.8f);

        // スキップ
        GameObject skip = new GameObject("Skip", typeof(RectTransform), typeof(Image), typeof(Button));
        skip.transform.SetParent(root.transform, false);
        RectTransform sr = skip.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(1f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(1f, 1f);
        sr.anchoredPosition = new Vector2(-24f, -24f); sr.sizeDelta = new Vector2(170f, 52f);
        skip.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.3f, 0.95f);
        skip.GetComponent<Button>().onClick.AddListener(Finish);
        TextMeshProUGUI sl = MakeText(skip.transform, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
        FillRect(sl.rectTransform); sl.text = LocalizationManager.IsJapanese ? "スキップ ▶" : "SKIP ▶"; sl.raycastTarget = false;
    }

    // 幕間/エンドを表示。interludeId は INT_01 / INT_08 / INT_12 / INT_13 / ENDING。
    // currentHeroId は「主人公」差し替え＆主人公別の締め/エピローグ挿入に使う。
    public void Show(string interludeId, string currentHeroId, Action complete)
    {
        Build();
        onComplete = complete;
        beats.Clear();
        beats.AddRange(InterludeScript.GetBeats(interludeId, currentHeroId));
        if (beats.Count == 0) { onComplete?.Invoke(); onComplete = null; return; }

        index = 0;
        root.SetActive(true);
        RenderBeat();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：表示中なら即終了してコールバックを発火。
    public bool DebugAutoResolve()
    {
        if (root == null || !root.activeSelf) return false;
        Finish();
        return true;
    }
#endif

    private void RenderBeat()
    {
        if (index < 0 || index >= beats.Count) { Finish(); return; }
        Beat b = beats[index];
        bool ja = LocalizationManager.IsJapanese;

        LocalizationManager.ApplyFont(nameText);
        LocalizationManager.ApplyFont(bodyText);

        bool isSpeech = b.Kind == BeatKind.Speech;
        nameBar.gameObject.SetActive(isSpeech);

        if (isSpeech)
        {
            nameText.text = b.Name;
            nameBar.color = b.HeroSide ? new Color(0.35f, 0.7f, 1f, 0.95f) : new Color(0.9f, 0.5f, 0.35f, 0.95f);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.color = new Color(0.95f, 0.97f, 1f);
            bodyText.fontStyle = FontStyles.Normal;
        }
        else
        {
            // ト書き/ナレーションは中央寄せ・やや淡色・斜体で「地の文」感を出す。
            bodyText.alignment = TextAlignmentOptions.Center;
            bodyText.fontStyle = FontStyles.Italic;
            bodyText.color = b.Kind == BeatKind.Stage
                ? new Color(0.72f, 0.78f, 0.88f, 0.92f)
                : new Color(0.92f, 0.94f, 1f, 0.98f);
        }
        bodyText.text = b.Text;

        // 立ち絵：セリフかつPortraitIdが解決できれば表示。無ければ淡くフェード。
        Sprite art = isSpeech ? ResolvePortrait(b.PortraitId) : null;
        portrait.sprite = art;
        portrait.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        if (art != null)
        {
            RectTransform pr = portrait.rectTransform;
            // 味方(主人公)＝左、敵(ボス)＝右。ゲーム中の自軍盤面（左側）に揃える。
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(b.HeroSide ? 0.27f : 0.73f, 0.5f);
            pr.DOKill();
            pr.localScale = Vector3.one * 0.97f;
            pr.DOScale(1f, 0.16f).SetUpdate(true);
        }

        hintText.text = index >= beats.Count - 1
            ? (ja ? "クリックで進む" : "Click to continue")
            : (ja ? "クリックで次へ" : "Click for next");

        box.transform.localScale = Vector3.one * 0.99f;
        box.transform.DOScale(1f, 0.1f).SetUpdate(true);
    }

    private static Sprite ResolvePortrait(string id)
    {
        // 統一したダイアログ素材(DialogArt)で解決（無ければユニットアイコン→null）。
        return DialogArt.Portrait(id);
    }

    private void Advance()
    {
        index++;
        if (index >= beats.Count) Finish();
        else RenderBeat();
    }

    private void Finish()
    {
        if (portrait != null) portrait.rectTransform.DOKill();
        if (root != null) root.SetActive(false);
        Action cb = onComplete; onComplete = null;
        cb?.Invoke();
    }

    // ===== UI 生成ヘルパ（HeroBossDialogueUI に倣う） =====
    private Image MakeImage(string name, Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root.transform, false);
        Image img = go.GetComponent<Image>();
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        img.raycastTarget = false;
        return img;
    }

    private Image MakeChildImage(Transform parent, string name, Vector2 amin, Vector2 amax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.rectTransform.anchorMin = amin; img.rectTransform.anchorMax = amax;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, float size, FontStyles style, TextAlignmentOptions align)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = style; t.alignment = align;
        t.raycastTarget = false; t.enableWordWrapping = true;
        LocalizationManager.ApplyFont(t);
        return t;
    }

    private static void FillRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
