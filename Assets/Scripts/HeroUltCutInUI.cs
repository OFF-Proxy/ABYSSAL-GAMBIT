using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// R3-hero-depth: ヒーロー必殺のカットイン演出。
// ヒーロー別（Aldin/Kagachi/Vesna）に色・SE・立ち絵を変え、必殺アップグレード(1=A/2=B)で色味を変化させる。
// 戦闘は止めず（raycast off・unscaled）、約1.1秒で自動消滅。専用SE（他ユニット未使用のアナウンサーボイス）。
public class HeroUltCutInUI : MonoBehaviour
{
    private const int SortingOrder = 25550; // HUDより前面・リザルト(25500)より前・ボスバナー(25600)より後。

    public static HeroUltCutInUI Instance { get; private set; }

    private GameObject root;
    private CanvasGroup group;
    private Image flash;
    private Image bandTop;
    private Image bandBottom;
    private Image portrait;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI tagText;
    private AudioSource audioSource;

    public static HeroUltCutInUI EnsureExists()
    {
        if (Instance != null) return Instance;
        HeroUltCutInUI existing = FindObjectOfType<HeroUltCutInUI>(true);
        if (existing != null) { Instance = existing; existing.Build(); return existing; }

        GameObject go = new GameObject("HeroUltCutInUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HeroUltCutInUI));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Instance = go.GetComponent<HeroUltCutInUI>();
        Instance.Build();
        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        Build();
        if (root != null) root.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Build()
    {
        if (root != null) return;

        root = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        FillRect(root.GetComponent<RectTransform>());
        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // 斜めの帯（上下から挟む）
        bandTop = MakeImage("BandTop", root.transform);
        RectTransform btr = bandTop.rectTransform;
        btr.anchorMin = new Vector2(0f, 0.62f); btr.anchorMax = new Vector2(1f, 0.86f);
        btr.offsetMin = Vector2.zero; btr.offsetMax = Vector2.zero; btr.localEulerAngles = new Vector3(0, 0, -6f);

        bandBottom = MakeImage("BandBottom", root.transform);
        RectTransform bbr = bandBottom.rectTransform;
        bbr.anchorMin = new Vector2(0f, 0.16f); bbr.anchorMax = new Vector2(1f, 0.40f);
        bbr.offsetMin = Vector2.zero; bbr.offsetMax = Vector2.zero; bbr.localEulerAngles = new Vector3(0, 0, -6f);

        // 立ち絵（左から）
        portrait = MakeImage("Portrait", root.transform);
        portrait.preserveAspect = true;
        RectTransform pr = portrait.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.28f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(760f, 760f);

        // 必殺名
        nameText = MakeText("Name", root.transform, new Vector2(0.62f, 0.56f), new Vector2(1000f, 110f), 76f, FontStyles.Bold);
        tagText = MakeText("Tag", root.transform, new Vector2(0.62f, 0.44f), new Vector2(1000f, 56f), 34f, FontStyles.Bold);

        // フラッシュ（全画面・最前面）
        flash = MakeImage("Flash", root.transform);
        FillRect(flash.rectTransform);
        flash.color = new Color(1f, 1f, 1f, 0f);

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void Play(string heroId, int upgrade, string ultName)
    {
        Build();
        GetStyle(heroId, upgrade, out Color main, out Color accent, out string tag);

        bandTop.color = new Color(main.r, main.g, main.b, 0.92f);
        bandBottom.color = new Color(accent.r, accent.g, accent.b, 0.92f);

        Sprite art = DialogArt.Portrait(heroId);
        portrait.sprite = art;
        portrait.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);

        nameText.text = ultName;
        nameText.color = new Color(1f, 0.98f, 0.92f);
        nameText.outlineColor = main; nameText.outlineWidth = 0.3f;
        tagText.text = tag;
        tagText.color = accent;

        flash.color = new Color(main.r, main.g, main.b, 0.85f);

        root.SetActive(true);
        PlaySe(heroId);
        AttackEffectPlayer.ShakeCamera(0.3f, 0.25f);
        Animate(main);
    }

    private void Animate(Color main)
    {
        group.DOKill();
        group.alpha = 1f;

        // 帯：左外から流れ込む
        SweepBand(bandTop.rectTransform, -1400f, 0f, 0.22f, 0f);
        SweepBand(bandBottom.rectTransform, 1400f, 0f, 0.22f, 0.04f);

        // 立ち絵：左外からスライドイン＋わずかにズーム
        RectTransform pr = portrait.rectTransform;
        pr.DOKill();
        Vector2 home = new Vector2(0f, 0f);
        pr.anchoredPosition = home + new Vector2(-700f, 0f);
        pr.localScale = Vector3.one * 1.08f;
        pr.DOAnchorPos(home, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
        pr.DOScale(1f, 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);

        // 必殺名：右からスライド＋拡大
        RectTransform nr = nameText.rectTransform;
        nr.DOKill();
        Vector2 nHome = nr.anchoredPosition;
        nr.anchoredPosition = nHome + new Vector2(220f, 0f);
        nameText.transform.localScale = Vector3.one * 1.18f;
        nr.DOAnchorPos(nHome, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
        nameText.transform.DOScale(1f, 0.32f).SetEase(Ease.OutQuad).SetUpdate(true);

        // フラッシュ：すぐ消える
        flash.DOKill();
        flash.DOFade(0f, 0.35f).SetUpdate(true);

        // 全体：少し見せてからフェードアウト
        group.DOKill();
        group.alpha = 1f;
        DOTween.Sequence().SetUpdate(true)
            .AppendInterval(0.85f)
            .Append(group.DOFade(0f, 0.3f).SetUpdate(true))
            .AppendCallback(() => { if (root != null) root.SetActive(false); });
    }

    private void SweepBand(RectTransform band, float fromX, float toX, float dur, float delay)
    {
        band.DOKill();
        Vector2 home = band.anchoredPosition;
        band.anchoredPosition = new Vector2(home.x + fromX, home.y);
        band.DOAnchorPos(new Vector2(home.x + toX, home.y), dur).SetEase(Ease.OutCubic).SetUpdate(true).SetDelay(delay);
    }

    private void PlaySe(string heroId)
    {
        // 他ユニットでは使っていないアナウンサーボイスを必殺SEに流用（ヒーロー別に陣営を割当）。
        string clipName;
        switch ((heroId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin": clipName = "sfx_announcer_lyonar_1st"; break;   // 光・聖
            case "herokagachi": clipName = "sfx_announcer_songhai_1st"; break; // 攻・修羅
            case "herovesna": clipName = "sfx_announcer_vanar_1st"; break;     // 蒼・氷雷
            default: clipName = "sfx_announcer_versus"; break;
        }
        AudioClip clip = Resources.Load<AudioClip>("sfx/" + clipName);
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, Mathf.Clamp01(AttackEffectPlayer.SfxVolumeMultiplier));
    }

    // ヒーロー＋アップグレードで色とタグを決める。
    private void GetStyle(string heroId, int upgrade, out Color main, out Color accent, out string tag)
    {
        bool ja = LocalizationManager.IsJapanese;
        switch ((heroId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin":
                main = new Color(1f, 0.82f, 0.32f); accent = new Color(1f, 0.95f, 0.7f);
                if (upgrade == 1) { main = new Color(1f, 0.9f, 0.55f); tag = ja ? "鉄壁" : "Bulwark"; }
                else if (upgrade == 2) { main = new Color(0.55f, 0.9f, 1f); accent = new Color(0.85f, 0.97f, 1f); tag = ja ? "不屈" : "Endurance"; }
                else tag = ja ? "聖盾" : "Aegis";
                break;
            case "herokagachi":
                main = new Color(0.9f, 0.18f, 0.16f); accent = new Color(1f, 0.45f, 0.2f);
                if (upgrade == 1) { main = new Color(1f, 0.22f, 0.12f); tag = ja ? "修羅烈" : "Carnage"; }
                else if (upgrade == 2) { main = new Color(1f, 0.5f, 0.08f); accent = new Color(1f, 0.75f, 0.2f); tag = ja ? "神速" : "Godspeed"; }
                else tag = ja ? "修羅" : "Asura";
                break;
            case "herovesna":
                main = new Color(0.35f, 0.65f, 1f); accent = new Color(0.6f, 0.9f, 1f);
                if (upgrade == 1) { main = new Color(1f, 0.5f, 0.16f); accent = new Color(1f, 0.78f, 0.3f); tag = ja ? "業火" : "Inferno"; }
                else if (upgrade == 2) { main = new Color(0.4f, 0.82f, 1f); accent = new Color(0.8f, 0.95f, 1f); tag = ja ? "追雷" : "Thunderclap"; }
                else tag = ja ? "蒼炎" : "Azure";
                break;
            default:
                main = new Color(1f, 0.85f, 0.4f); accent = new Color(1f, 0.95f, 0.7f); tag = string.Empty;
                break;
        }
    }

    private static void FillRect(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    private Image MakeImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeText(string name, Transform parent, Vector2 anchor, Vector2 size, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = size;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(t);
        t.fontSize = fontSize; t.fontStyle = style; t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; t.outlineColor = Color.black; t.outlineWidth = 0.2f;
        return t;
    }
}
