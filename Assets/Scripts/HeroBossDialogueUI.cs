using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// R3-boss-factions: ボス戦前の掛け合いダイアログ（ヒーロー将 vs ボス将）。
// 左にボス・右にヒーローの立ち絵、下部にVN風のセリフ枠。クリックで進み、最後にコールバックで戦闘開始。
public class HeroBossDialogueUI : MonoBehaviour
{
    private const int SortingOrder = 25560;

    public static HeroBossDialogueUI Instance { get; private set; }

    private struct Line { public bool Hero; public string Name; public string Text; }

    private GameObject root;
    private Image dim;
    private Image bossPortrait;
    private Image heroPortrait;
    private float heroBaseScale = 1f, bossBaseScale = 1f; // 立ち絵ごとのサイズ感補正（Highlightはこれに掛ける）。
    private Image nameBar;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI hintText;
    private Coroutine typeRoutine;       // セリフの一文字ずつ表示（タイプライター）。
    private bool revealing;              // 文字送り中か。表示途中のクリックは「全文即表示」に使う。
    private const float TypeCharInterval = 0.035f; // 1文字あたりの送り間隔（秒, 実時間）。
    private GameObject box;          // セリフ枠（コンパクト時のアイコン親）
    private GameObject skip;         // スキップボタン（常に最前面に保つ）
    private Image compactFrame;      // 中ボス用：左の小さな枠
    private Image compactIcon;       // 中ボス用：話者の小アイコン
    private bool compact;            // 中ボス＝コンパクト表示 / 章ボス＝大立ち絵
    private string curHeroId;
    private string curBossId;

    private readonly List<Line> lines = new List<Line>();
    private int index;
    private Action onComplete;

    public static HeroBossDialogueUI EnsureExists()
    {
        if (Instance != null) return Instance;
        HeroBossDialogueUI existing = FindObjectOfType<HeroBossDialogueUI>(true);
        if (existing != null) { Instance = existing; existing.Build(); return existing; }

        GameObject go = new GameObject("HeroBossDialogueUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HeroBossDialogueUI));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Instance = go.GetComponent<HeroBossDialogueUI>();
        Instance.Build();
        return Instance;
    }

    private void Awake() { LocalizationManager.EnsureExists(); Instance = this; Build(); if (root != null) root.SetActive(false); }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Build()
    {
        if (root != null) return;

        root = new GameObject("Root", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(transform, false);
        FillRect(root.GetComponent<RectTransform>());
        dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0.01f, 0.03f, 0.82f);
        root.GetComponent<Button>().transition = Selectable.Transition.None;
        root.GetComponent<Button>().onClick.AddListener(Advance); // 画面クリックで進む

        // ヒーロー立ち絵（左＝ゲーム中の自軍盤面と同じ側）。画面いっぱいに大きく表示。
        // 立ち絵を実測サイズで揃えて拡大するため、左右をやや外側へ寄せて中央での重なりを防ぐ。
        heroPortrait = MakeImage("Hero", new Vector2(0.24f, 0.5f), new Vector2(1080f, 1180f));
        heroPortrait.preserveAspect = true;
        // ボス立ち絵（右＝敵側）。
        bossPortrait = MakeImage("Boss", new Vector2(0.76f, 0.5f), new Vector2(1080f, 1180f));
        bossPortrait.preserveAspect = true;

        // VN セリフ枠（下部）
        box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(root.transform, false);
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0.5f, 0f); br.anchorMax = new Vector2(0.5f, 0f); br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, 40f); br.sizeDelta = new Vector2(1360f, 260f);
        Image boxImg = box.GetComponent<Image>();
        Sprite panel = Resources.Load<Sprite>("UI/Augment/card_panel");
        if (panel != null) { boxImg.sprite = panel; boxImg.type = Image.Type.Sliced; boxImg.color = new Color(0.07f, 0.1f, 0.16f, 0.98f); }
        else boxImg.color = new Color(0.05f, 0.08f, 0.13f, 0.98f);
        boxImg.raycastTarget = false;

        // 中ボス用：枠の左に置く小さな顔アイコン（章ボス時は非表示）。画像1のようなコンパクト表示。
        compactFrame = MakeChildImage(box.transform, "CompactFrame", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        RectTransform cfr = compactFrame.rectTransform;
        cfr.pivot = new Vector2(0f, 0.5f); cfr.anchoredPosition = new Vector2(20f, 0f); cfr.sizeDelta = new Vector2(220f, 220f);
        compactFrame.color = new Color(0.04f, 0.06f, 0.10f, 0.98f);
        compactIcon = MakeChildImage(compactFrame.transform, "CompactIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RectTransform cir = compactIcon.rectTransform;
        cir.pivot = new Vector2(0.5f, 0.5f); cir.anchoredPosition = Vector2.zero; cir.sizeDelta = new Vector2(200f, 200f);
        compactIcon.preserveAspect = true;
        compactFrame.gameObject.SetActive(false);

        // 名前プレート
        nameBar = MakeChildImage(box.transform, "NameBar", new Vector2(0f, 1f), new Vector2(0f, 1f));
        RectTransform nbr = nameBar.rectTransform;
        nbr.pivot = new Vector2(0f, 1f); nbr.anchoredPosition = new Vector2(34f, 30f); nbr.sizeDelta = new Vector2(360f, 56f);
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
        hr.anchoredPosition = new Vector2(-24f, 10f); hr.sizeDelta = new Vector2(400f, 28f);
        hintText.color = new Color(0.7f, 0.8f, 0.95f, 0.8f);

        // スキップ
        skip = new GameObject("Skip", typeof(RectTransform), typeof(Image), typeof(Button));
        skip.transform.SetParent(root.transform, false);
        RectTransform sr = skip.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(1f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(1f, 1f);
        sr.anchoredPosition = new Vector2(-24f, -24f); sr.sizeDelta = new Vector2(160f, 52f);
        skip.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.3f, 0.95f);
        skip.GetComponent<Button>().onClick.AddListener(Finish);
        TextMeshProUGUI sl = MakeText(skip.transform, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
        FillRect(sl.rectTransform); sl.text = LocalizationManager.IsJapanese ? "スキップ ▶" : "SKIP ▶"; sl.raycastTarget = false;
    }

    // compactMode=true（中ボス）＝左に小アイコン＋セリフ枠（画像1）。false（章ボス4-10）＝大立ち絵（画像2）。
    // bossNameOverride: 中ボス別キャラ化の表示名（異名）。scriptOverride: バリアント固有のかませ台本。
    public void Show(string heroId, string bossId, Sprite bossIcon, Action complete, bool compactMode = false, string bossNameOverride = null, string[] scriptOverride = null, Color? midTint = null)
    {
        Build();
        onComplete = complete;
        compact = compactMode;
        curHeroId = heroId; curBossId = bossId;
        // midTint は色フィルター廃止に伴い未使用（シグネチャは呼び出し側互換のため保持）。
        bool ja = LocalizationManager.IsJapanese;

        // 大立ち絵（章ボス）と小アイコン（中ボス）の表示切替。
        heroPortrait.gameObject.SetActive(!compact);
        bossPortrait.gameObject.SetActive(!compact);
        compactFrame.gameObject.SetActive(compact);

        Sprite heroArt = DialogArt.Portrait(heroId);
        heroPortrait.sprite = heroArt;
        heroPortrait.color = heroArt != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        heroBaseScale = DialogArt.PortraitScale(heroId);
        heroPortrait.rectTransform.localScale = Vector3.one * heroBaseScale; // 立ち絵ごとのサイズ感補正。
        // ボス立ち絵：統一したダイアログ素材(DialogArt)を優先。無ければ呼び出し元のユニットアイコン。
        Sprite bossPort = DialogArt.Portrait(bossId);
        if (bossPort == null) bossPort = bossIcon;
        bossPortrait.sprite = bossPort;
        bossPortrait.color = bossPort != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
        bossBaseScale = DialogArt.PortraitScale(bossId);
        bossPortrait.rectTransform.localScale = Vector3.one * bossBaseScale; // 同上（neutral_rook等は縮小）。

        // コンパクト時はセリフ/名前枠を小アイコン分だけ右へ寄せる。
        float leftPad = compact ? 270f : 40f;
        bodyText.rectTransform.offsetMin = new Vector2(leftPad, 30f);
        nameBar.rectTransform.anchoredPosition = new Vector2(compact ? 260f : 34f, 30f);

        string heroName = LocalizationManager.UnitName(heroId);
        string bossName = !string.IsNullOrEmpty(bossNameOverride) ? bossNameOverride : LocalizationManager.UnitName(bossId);

        lines.Clear();
        // STORY: バリアント固有台本(scriptOverride)＞ボス×主人公の確定台本＞bossID単位の汎用、の順で優先。
        // 台本は日本語確定稿のため、日本語表示時のみ使用（EN は従来の汎用英語文を使う）。
        string[] script = (scriptOverride != null && scriptOverride.Length >= 1) ? scriptOverride : (ja ? GetScriptedLines(bossId, heroId) : null);
        // 中ボス（主人公別台本なし）は bossID 単位の主人公共通台本へフォールバック。偶数=ボス/奇数=主人公。
        if (script == null && ja) script = GetMidBossLines(bossId);
        if (script != null && script.Length >= 1)
        {
            // 可変行数：偶数index＝ボス / 奇数index＝主人公（交互）。3行に限定しない。
            for (int i = 0; i < script.Length; i++)
            {
                bool heroLine = (i % 2 == 1);
                lines.Add(new Line { Hero = heroLine, Name = heroLine ? heroName : bossName, Text = script[i] });
            }
        }
        else
        {
            lines.Add(new Line { Hero = false, Name = bossName, Text = BossTaunt(bossId, ja) });
            lines.Add(new Line { Hero = true, Name = heroName, Text = HeroRetort(heroId, ja) });
            lines.Add(new Line { Hero = false, Name = bossName, Text = BossCloser(bossId, ja) });
        }

        index = 0;
        root.SetActive(true);
        RenderLine();
    }

    // ボス撃破“後”の短い会話（章ボス4-10クリア後）。常に大立ち絵。セリフ本体はGPT(Codex)が postBossLines に追記。
    public void ShowPostBoss(string heroId, string bossId, Sprite bossIcon, Action complete)
    {
        Build();
        onComplete = complete;
        compact = false;
        curHeroId = heroId; curBossId = bossId;
        heroPortrait.gameObject.SetActive(true);
        bossPortrait.gameObject.SetActive(true);
        compactFrame.gameObject.SetActive(false);

        Sprite heroArt = DialogArt.Portrait(heroId);
        heroPortrait.sprite = heroArt; heroPortrait.color = heroArt != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        heroBaseScale = DialogArt.PortraitScale(heroId);
        heroPortrait.rectTransform.localScale = Vector3.one * heroBaseScale;
        Sprite bp = DialogArt.Portrait(bossId); if (bp == null) bp = bossIcon;
        bossPortrait.sprite = bp; bossPortrait.color = bp != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
        bossBaseScale = DialogArt.PortraitScale(bossId);
        bossPortrait.rectTransform.localScale = Vector3.one * bossBaseScale;
        bodyText.rectTransform.offsetMin = new Vector2(40f, 30f);
        nameBar.rectTransform.anchoredPosition = new Vector2(34f, 30f);

        string heroName = LocalizationManager.UnitName(heroId);
        string bossName = LocalizationManager.UnitName(bossId);
        string[] script = GetPostBossLines(bossId, heroId);
        lines.Clear();
        for (int i = 0; i < script.Length; i++)
        {
            bool heroLine = (i % 2 == 1); // 偶数=ボス(敗者)/奇数=主人公。
            lines.Add(new Line { Hero = heroLine, Name = heroLine ? heroName : bossName, Text = script[i] });
        }
        index = 0;
        root.SetActive(true);
        RenderLine();
    }

    // GPT(Codex)が "<bossId>|<heroId>"（小文字）で個別の撃破後台本を追記する。未登録は汎用2行。
    private static Dictionary<string, string[]> postBossLines;
    private static string[] GetPostBossLines(string bossId, string heroId)
    {
        if (postBossLines == null) BuildPostBossLines();
        bool ja = LocalizationManager.IsJapanese;
        if (postBossLines.TryGetValue((bossId + "|" + heroId).ToLowerInvariant(), out string[] v) && v != null && v.Length >= 1)
            return v;
        // 汎用フォールバック（個別台本が入るまでの暫定）。
        return ja ? new[] { "……見事だ。ここまで来るとは、思わなかった。", "まだ、終われない。次へ進むだけだ。" }
                  : new[] { "...Well fought. I did not think you'd come this far.", "Not finished yet. Onward." };
    }
    private static void BuildPostBossLines()
    {
        postBossLines = new Dictionary<string, string[]>();
        // ch1-12 章ボス×9主人公の撃破後台本（GPT/Codex 確定稿、POSTBOSS_DIALOGUE_CH1_12_CODEX_DRAFT 由来）。
        // 偶数index=ボス（敗者）/奇数index=主人公。キーは小文字 "<bossId>|<heroId>"。
        postBossLines["caliber|heroaldin"] = new[]{ "アルディン……私は、何をしていた。白門を守るつもりで、また守るべき者へ剣を向けていたのか。リオラの名まで、私の恐れで汚しかけた。", "戻ってきたなら、それでいい。兄弟子、俺たちは失ったものを抱えたまま進むしかない。リオラの名も、あなたの後悔も、眠りの理由にはさせない。", "兜の奥で、白く優しい声がした。私の痛みを撫で、終わらせれば楽になると囁いた……この観測片を持て、兄弟弟子よ。あの声の主へ辿り着け。" };
        postBossLines["caliber|herokagachi"] = new[]{ "私は正気を失っていたのか。弱き者を眠らせれば救えると、本気で信じかけていた。お前の吠え声がなければ、まだ慈悲の名で斬っていたかもしれん。", "目が覚めたならそれでいいだろ。弱い奴を勝手に寝かせんな。転ぶ奴も泣く奴も、立つ気があるなら尻を叩いてやりゃいい。終わりを押しつけるな。", "その荒い優しさを覚えておこう。私に触れた白い声は、まだ他の門も見ているはずだ。この観測片を持て、カガチ。お前の足で先へ行け。" };
        postBossLines["caliber|herovesna"] = new[]{ "目が覚めた……私は火を恐れ、温もりまで敵に見ていたらしい。守れなかった夜の冷たさが、私にすべての熱を疑わせていた。", "怖がることは恥じゃない。でも火は焼くだけじゃないよ。手を伸ばす場所があれば、凍えた者はそこへ戻って来られる。あなたも、まだ戻れた。", "ならばその火を先へ運べ。私の胸に残る観測片が、次の門を指している。終わりを優しさと呼ぶ声に、お前の熱を突きつけてやれ。" };
        postBossLines["caliber|heroziran"] = new[]{ "癒やし手よ、私は傷を閉じる名で眠りを選びかけた。あれは私の意志だったのか、それとも、傷口に入り込んだ誰かの慈悲だったのか。", "どちらでも、あなたは戻って来ました。傷が残っても、生きる道はあります。苦しみを消すだけでなく、苦しむ人の隣に立つことも癒やしです。", "白い声が、痛みを終えろと囁いた。この観測片を持て、ジラン。あの声の主へ近づけるなら、どうかその手で、私以外の眠りもほどいてくれ。" };
        postBossLines["caliber|heroreva"] = new[]{ "私は選ぶことを捨てかけた。守護の名で、ただ命じられるまま門を閉じていた。お前の騒がしい自由が、あの静けさにひびを入れたのだな。", "騒がしいは余計だけど、まあ褒め言葉にしておくわ。戻ってきたなら、また選べる。泣くのも、怒るのも、通すのも、自分で決めなさい。", "ならば私は、お前たちを通すと選ぶ。私を操った静かな声を追ってくれ。この観測片が、終わりを美しく語る何者かへ繋がるはずだ。" };
        postBossLines["caliber|herokara"] = new[]{ "待つことを捨て、眠らせることを選びかけた。戻らぬ者への恐れに、私は呑まれたのだ。お前の静けさは、私の焦りよりずっと強かった。", "焦りは責めない。長い夜では、誰でも火を消したくなる。だが戻れる者がいるなら、灯りは残す。私はそうして待つことを選ぶ。", "その灯りを先へ持っていけ、カーラ。私の中に残った観測片が、まだ誰かの視線を宿している。終わりを急がせる声に、冬の忍耐を見せてやれ。" };
        postBossLines["caliber|herobrome"] = new[]{ "私の号令に、従騎士たちまで巻き込んだ。守護の旗を、私は眠りの命令に変えていた。ブローム、お前の声がなければ、私は旗の意味を忘れたままだった。", "旗は命令だけではない。違う者が傷つきながらも、もう一度並ぶための目印だ。倒れた旗なら、拾い上げればいい。まだ誰かが見ている。", "ならばこの観測片を掲げて進め。私を見ていた何者かへ、まだ軍旗は折れぬと示してくれ。お前の号令なら、眠る者の胸にも届くかもしれん。" };
        postBossLines["caliber|heroshidai"] = new[]{ "影よ、私は見えぬ声に動かされていた。守るべきものの影まで敵と見誤っていたのだ。名を求めぬお前の刃が、私の盲目を裂いた。", "戻ったなら十分だ。見えない声があるなら、見えない道から追えばいい。光の前に立つ必要はない、届く場所を探して進むだけだ。", "この観測片を持て、シダイ。誰かが私の後悔を観ていた……その視線の影を辿ってくれ。名もない働きが、終わりの裏側へ届くことを願う。" };
        postBossLines["caliber|heroilena"] = new[]{ "観測する者よ、私の中に別の視線があった。白門の痛みを冷たくなぞり、最も優しい終わりだけを選ばせようとする、静かな誰かの目だ。", "その視線は、あなたの後悔を利用した可能性が高い。けれど、利用された事実だけであなたを定義する必要はありません。私は記録し、次の分岐へ進みます。", "名は分からぬ。ただ、終わりを慈愛のように勧める声だった。この観測片が、その主へ繋がるはずだ。イリーナ、その冷静さで私の敗北を未来へ変えてくれ。" };
        postBossLines["neutral_rook|heroaldin"] = new[]{ "門は開く。守る者が進むほど傷を増やすと知っても、お前の盾は退かなかった。", "傷が増えても、守るべき背中に届くなら進む。止まるだけでは守れない。", "観測片を持て。静かな声は、進む者をまだ止めたがっている。" };
        postBossLines["neutral_rook|herokagachi"] = new[]{ "門は開く。転ぶ足を止めれば救えると思ったが、お前は傷ごと石を越えた。", "傷があるなら持っていく。伏せたまま終わるより、転んで進む方がましだ。", "観測片を持て。あの方の静けさは、まだ道の先で待っている。" };
        postBossLines["neutral_rook|herovesna"] = new[]{ "門は開く。冷えた者は止まるべきだと思ったが、お前は門影に火を置いた。", "冷たい場所でも、火を置けば進める。休む場所は閉ざされた門じゃない。", "観測片を持て。静かな声は、その火さえ眠らせようとしている。" };
        postBossLines["neutral_rook|heroziran"] = new[]{ "門は開く。動けば傷が開くと知っても、お前は手を止めなかった。", "傷が開くなら支えます。苦しみごと続く命を、門前に置いてはいけません。", "観測片を持て。あの方の眠りは、痛みのない救いを名乗っている。" };
        postBossLines["neutral_rook|heroreva"] = new[]{ "門は開く。選択を閉じれば迷わぬと思ったが、お前の足は別の道を選んだ。", "迷ってもいい。どこへ進むかを自分で決められるなら、それが自由だよ。", "観測片を持て。静かな声は、自由さえ休ませようとしている。" };
        postBossLines["neutral_rook|herokara"] = new[]{ "門は開く。待つ者なら止まると思ったが、お前の待つは道を閉じなかった。", "待つのは、戻る道を残すためです。塞いで眠らせるためじゃありません。", "観測片を持て。あの方は、戻る足音さえ静かにしたがっている。" };
        postBossLines["neutral_rook|herobrome"] = new[]{ "門は開く。違う足並みは崩れると思ったが、軍旗は石を押し返した。", "揃わない足でも進める。違う者同士が、一つの道を選ぶことはできる。", "観測片を持て。静かな声は、争いも結束も同じく止めようとしている。" };
        postBossLines["neutral_rook|heroshidai"] = new[]{ "門は開く。影は外に残ると思ったが、お前の支えは石の奥まで届いた。", "見えなくても支えは届く。影が残るから、先へ進める光もある。", "観測片を持て。あの方の目は、見えぬ影まで眠らせようとしている。" };
        postBossLines["neutral_rook|heroilena"] = new[]{ "門は開く。観測者なら止まると思ったが、お前は見た先へ手を伸ばした。", "観測は諦めの別名ではありません。見たうえで、未来を選ぶ余地があります。", "観測片を持て。静かな声の主は、その余地を閉じようとしている。" };
        postBossLines["neutral_sister|heroaldin"] = new[]{ "祈りは折れました。けれど、捧げた痛みは消えません。捧げれば軽くなると思っていたのに。", "消えなくていい。痛みを理由に誰かを眠らせるより、重いまま盾を持つ。", "あなたの盾は、祈りより重いのですね。その重さに、私は膝をつきました。" };
        postBossLines["neutral_sister|herokagachi"] = new[]{ "あの方に捧げれば、弱さも赦されたのに。赦しのない道は、寂しくありませんか。", "赦されなくていい。寂しくても、弱いまま俺の足で歩けるならそれでいい。", "赦しの外で立つ人を、初めて見ました。その弱さは、捧げるには惜しいものです。" };
        postBossLines["neutral_sister|herovesna"] = new[]{ "信じる場所を持てば、迷わずに済みます。作った場所は、いつか壊れるかもしれないのに。", "与えられた場所じゃなくて、隣に作る場所がいい。壊れても、誰かと置いた温度は残る。", "それは祈りより、ずっと温かいのですね。迷う火に敗れた理由が、少し分かりました。" };
        postBossLines["neutral_sister|heroziran"] = new[]{ "苦しむ命を眠りへ捧げることが救いです。朝が来ても、痛みは残るのですから。", "苦しむ命に、朝まで付き添うことも救いです。残る痛みには、次の手を添えます。", "その手は、祈りより長く残るのですね。私の献身は、少し急ぎすぎたのかもしれません。" };
        postBossLines["neutral_sister|heroreva"] = new[]{ "献身とは、自分を捨てること。選べば迷いが生まれ、迷いは祈りを濁らせます。", "違うよ。捨てるか残るか、自分で選ぶことだ。迷っても、自分の足で立てるならいい。", "選ぶ献身……私には眩しすぎます。その眩しさを、あの方はどう見るのでしょう。" };
        postBossLines["neutral_sister|herokara"] = new[]{ "祈りは答えを待ちません。ただ従うだけ。待つほど、不安は濃くなるでしょう。", "待つことにも答えはある。急かさず隣にいれば、不安は一人にならずに済む。", "待つ祈りを、私は知らなかった。従うだけでは届かない静けさがあるのですね。" };
        postBossLines["neutral_sister|herobrome"] = new[]{ "一つの信仰だけが、人を迷わせません。違う声は、いずれ互いを傷つけます。", "違う声が並んでも、前へ進める。争っても同じ明日を守れるなら、それを軍と呼ぶ。", "祈りより大きな合唱……聞こえます。迷いを消すのでなく抱える声なのですね。" };
        postBossLines["neutral_sister|heroshidai"] = new[]{ "影に捧げる名などありません。それでも、誰かに見つけてほしくはないのですか。", "名を呼ばれなくても、支えた命は消えない。必要な時にだけ見えればいい。", "祈りの外にも、確かに光があるのですね。見えない献身を、私は捧げ損ねました。" };
        postBossLines["neutral_sister|heroilena"] = new[]{ "観測者の結論は、祈りより確かなはずでした。選択は、記録を曇らせます。", "確かなものを見ても、人は違う未来を選べます。曇りの中でしか見えない星もあります。", "それもまた、祈りなのでしょうか。ならば私は、まだ祈りの名を狭く見ていたのですね。" };
        postBossLines["magmarvaath|heroaldin"] = new[]{ "くそ……庇うだけの盾が、俺の炎を押し返したか。守るってのは、もっと臆病なもんだと思ってた。", "怖いから前に出る。後ろに燃やしたくないものがあるなら、盾はただ耐えるだけでは足りない。", "いい本能だ。まだ燃えてやがる。守る獣ってやつも、案外悪くねえ。" };
        postBossLines["magmarvaath|herokagachi"] = new[]{ "迷う獣のくせに、噛みつきだけは本物か。本能だけなら、もっと楽に燃えられるぞ。", "楽じゃなくていい。迷っても噛めるし、考えても走れる。俺は選んで前に出る。", "吠えるなと言ったが……その牙は覚えた。迷いごと噛み砕いて進め。" };
        postBossLines["magmarvaath|herovesna"] = new[]{ "氷の中の火が、俺より熱い顔をするとはな。消えない火は、燃やす火よりしつこい。", "焼くためじゃない。消えないための火だ。誰かの場所を温められるなら、しつこくていい。", "なら燃やし続けろ。凍るなよ。その面倒な火が、俺の炎を押し返した。" };
        postBossLines["magmarvaath|heroziran"] = new[]{ "癒やす手で殴るか。生かすために牙を剥くなんざ、獣より面倒で矛盾してやがる。", "命は綺麗な形だけではありません。生かすためなら、止める力も使います。", "生に噛みつく手か。悪くねえ。ぬるい救いより、よほど熱がある。" };
        postBossLines["magmarvaath|heroreva"] = new[]{ "逃げ足の速い獲物かと思ったが、戻って刺したな。獣なら強い方へ従うものだ。", "私は従わない。逃げるか戻るかは私が決めるし、強くても道までは渡さない。", "自由な獣だ。追い甲斐がある。次に会うなら、もっと速く走れ。" };
        postBossLines["magmarvaath|herokara"] = new[]{ "待つだけの氷が、俺の炎に耐えたか。動かねえ奴ほど、焼きやすいはずだった。", "待つのは弱さじゃない。焼かれても、誰かを支える場所は簡単には動かさない。", "ちっ……冷たいくせに、芯が熱い。その熱なら、炎の前でも砕けねえか。" };
        postBossLines["magmarvaath|herobrome"] = new[]{ "群れて吠えるだけなら焼けたはずだ。違う匂いが混ざれば、獣は散るものだからな。", "群れではない。違う者が同じ旗を選んだ軍だ。違いがあるから、互いを掴める。", "その旗、燃やすには骨が折れる。獣の鼻では測れねえ強さだ。" };
        postBossLines["magmarvaath|heroshidai"] = new[]{ "影の牙に、炎が届き損ねたか。隠れて噛むだけじゃ、獣は名を残せねえぞ。", "名より先に、守る背中がある。光を守る影は、燃え残る場所を知っている。", "見えねえ牙ほど痛えもんだな。名を残さなくても、傷は確かに残ったぜ。" };
        postBossLines["magmarvaath|heroilena"] = new[]{ "観るだけの目が、俺の炎を読んだか。未来を読めるなら、怖さも見えただろうに。", "見えました。それでも、怖さだけでは決めません。観たうえで燃え尽きない道を選びます。", "その目、獣よりしぶとい。見えてなお踏み込む奴は、喰いづらいもんだ。" };
        postBossLines["magmarstarhorn|heroaldin"] = new[]{ "ははっ、守りの戦がここまで荒れるとはな。ただ耐える盾なら退屈だったが、お前は踏み込んだ。", "守るための戦いにも、譲れない熱はある。耐えるだけでは守れない時があるからだ。", "いい嵐だ。退屈せずに倒れられる。守りの旗にも、戦場を震わせる力があるらしい。" };
        postBossLines["magmarstarhorn|herokagachi"] = new[]{ "立って、転んで、また来たか。進むだけなら避けてもよかった戦いを、真正面から選んだな。", "避けた先で止まるなら、ここでぶつかる。勝つためだけじゃなく、進むために戦う。", "その先でも暴れろ。嵐は止まるな。お前の転び方は、戦場に似合っている。" };
        postBossLines["magmarstarhorn|herovesna"] = new[]{ "背中を預ける場所が、お前の嵐を強くしたか。群れの風は、火を消すこともあるのに。", "この風は、消すんじゃなく広げてくれた。一人の火じゃないから、簡単には消えない。", "いい火勢だ。風をくれてやった甲斐がある。居場所を持つ炎は、思ったより荒い。" };
        postBossLines["magmarstarhorn|heroziran"] = new[]{ "癒やし手が、戦場の真ん中で倒れぬとはな。傷を塞ぐ者が、傷を恐れぬか。", "恐れます。だから一人では負わせません。命を支えるには、嵐の中にも立ちます。", "その覚悟、戦士のそれだ。癒やしの手にも、嵐を割る力があるとはな。" };
        postBossLines["magmarstarhorn|heroreva"] = new[]{ "逃げず、隠れず、自由に撃った。明日は別の場所へ飛ぶとしても、今日はここを選んだのだな。", "私の場所は私が選ぶ。どこへでも行けるからこそ、ここにいたことが私の答えだよ。", "その選択、嵐に刻んでおけ。自由な矢ほど、戦場ではよく響く。" };
        postBossLines["magmarstarhorn|herokara"] = new[]{ "待つ者が、ここぞで踏み込む。戦場で待つのは臆病者のすることかと思っていた。", "臆病でもいい。急がないだけで、誰かが崩れる前に動ければそれでいい。", "静かな闘争もあるものだな。遅い刃ほど、決まると重い。" };
        postBossLines["magmarstarhorn|herobrome"] = new[]{ "ばらばらの声が、戦場で一つに鳴ったな。騒がしすぎて号令も飲まれるかと思ったぞ。", "それでも前へ出る声を、私は聞き分ける。揃わないから強く、互いの穴を埋められる。", "その軍歌、嵐より騒がしい。だが悪くない、戦場にはそのくらいの音が要る。" };
        postBossLines["magmarstarhorn|heroshidai"] = new[]{ "影まで戦場を楽しむか。熱があるなら、なぜ前に名乗らん。", "楽しみはしない。名乗るより早く届くべき場所がある。支える戦いにも熱はある。", "名のない熱か。覚えておくぞ。嵐の裏側にも、火種はあるものだ。" };
        postBossLines["magmarstarhorn|heroilena"] = new[]{ "結末を読んでなお、嵐に踏み込んだか。未来を外すには、相応の熱がいる。", "熱ならあります。諦めない者たちを観てきましたし、読めた結末だけが未来ではありません。", "なら書き換えろ。派手にな。観測者の頁にも、嵐を吹かせてやれ。" };
        postBossLines["magmarragnora|heroaldin"] = new[]{ "我らは減らぬ。お前の盾は、いつか群れの波に沈む。それでも一人を立たせるのか。", "いつか沈むとしても、その時まで誰かを立たせる。一人が立てば、次の一人を支えられる。", "個の灯が、群れの闇に穴を開けるか。小さいが、消えぬ明かりだった。" };
        postBossLines["magmarragnora|herokagachi"] = new[]{ "一匹の足で、群れの波を越えたか。群れに属さず、なお孤独ではないと言うのだな。", "俺は一匹じゃない。転んだら起こす手があるし、並び方は群れだけじゃねえ。", "群れとは別のつながり……厄介だ。その不揃いな足並みが、我らを越えた。" };
        postBossLines["magmarragnora|herovesna"] = new[]{ "砂粒の火が、群れを焼いた。集まれば互いを焦がすこともあるというのに。", "小さくても集まれば火床になる。焦がさない距離を、みんなで探せばいい。", "ならば燃えろ。消えぬ群れのように。お前の火床は、我らの腹より温かい。" };
        postBossLines["magmarragnora|heroziran"] = new[]{ "数多の命を前に、救える数などわずか。群れは個を埋め、声を飲む。", "わずかでも救います。埋もれた声を拾うのが私の役目で、数で命の重さは決めません。", "その手は、群れを数えぬのだな。数えぬ手に、群れの圧が裂かれた。" };
        postBossLines["magmarragnora|heroreva"] = new[]{ "群れに従えば、迷わず速く進める。選ばぬ群れの方が、ずっと楽だ。", "従わない。速さだけで行き先を決めたくないし、誰と並ぶかも私が選ぶ。", "選ぶ群れ……それはもう群れではない。だが、その迷いが波を割った。" };
        postBossLines["magmarragnora|herokara"] = new[]{ "群れは待たぬ。遅れた者を飲み込み、待つ者を置いていく。", "だから私は待つ。置いていかれても、遅れた誰かが帰れる場所にはなれる。", "群れの外に、そんな巣があるのか。弱い巣だが、壊しきれぬ温度がある。" };
        postBossLines["magmarragnora|herobrome"] = new[]{ "我らの群れを、軍旗が押し返すか。意志は腹を満たさぬし、数に潰されるはずだ。", "それでも、意志がなければ数はただの波だ。違うまま並ぶ意志が、軍を立たせる。", "ばらばらの個が、我らより強く鳴るとは。群れの母として、少し妬ましい。" };
        postBossLines["magmarragnora|heroshidai"] = new[]{ "群れの中では、影の名など潰える。散れば弱く、群れはそこを食う。", "名を潰されないために、影は散って支える。散った影は、届かない隙間に入れる。", "見えぬ絆が、群れの腹を裂いた。名のないものを、我らは軽く見た。" };
        postBossLines["magmarragnora|heroilena"] = new[]{ "観測すれば分かる。群れはいつも個を飲み、乱れは死を呼ぶ。", "分かっても、飲まれない選択はできます。乱れの中でしか、生まれない未来もあります。", "観測に逆らう個……未来の種か。その小ささを、群れは侮った。" };
        postBossLines["abyssallilithe|heroaldin"] = new[]{ "別れは来るわ。どれほど守っても、離す痛みは避けられない。それを知ってまだ握るのね。", "知っているからこそだ。別れが来るから、今の手を離さず、空いた手を悔いだけにしたくない。", "その強情さ、少しだけ羨ましいわ。影に還すには、温もりが強すぎた。" };
        postBossLines["abyssallilithe|herokagachi"] = new[]{ "進むほど、置いていくものが増えるのよ。背負えば背負うほど、足は遅くなるわ。", "置いていくんじゃない。背負って進む。遅くてもいい、速さだけが前じゃねえ。", "重い背中ね。でも、倒れなかった。その歩き方なら、別れも少し困るでしょうね。" };
        postBossLines["abyssallilithe|herovesna"] = new[]{ "居場所は失うためにある。温めるほど、失った時に冷えるのに、怖くないの？", "怖い。だから失う前に温める。冷えた記憶だけで、居場所を終わらせたくない。", "怖いまま笑えるのね。ずるい子。影の方が、少し寂しくなったわ。" };
        postBossLines["abyssallilithe|heroziran"] = new[]{ "手を取った命とも、いつか別れる。分けた痛みは、あなたにも残るわ。", "残っても、独りにするよりはいい。その時まで痛みを分けるのが、私の手です。", "優しい別れも、あるのかしら。あなたの手は、影より静かに残るのね。" };
        postBossLines["abyssallilithe|heroreva"] = new[]{ "離れる自由もあるわ。傷つく前に飛び去れば、別れを正面から受けずに済む。", "残る自由もある。背中を向けて失うより、私はここで受け止める方を選んだ。", "いいわね。風は、別れも選べる。その自由を、少し羨ましく思うわ。" };
        postBossLines["abyssallilithe|herokara"] = new[]{ "待っても、帰らない人はいるわ。灯りを見て、余計に寂しくなる夜もある。", "それでも灯りは消さない。その夜にも、戻れる人のために消えないものが必要だから。", "あなたの待つ影、少し温かい。寂しさを消さずに抱く影なのね。" };
        postBossLines["abyssallilithe|herobrome"] = new[]{ "旗の下に集めても、別れは列を裂く。届かない声だってあるわ。", "それでも呼ぶ。裂かれても声は残るし、届く声まで失わないために旗がある。", "その声が、影まで届いたわ。静かな別れには、少し騒がしすぎるくらいに。" };
        postBossLines["abyssallilithe|heroshidai"] = new[]{ "影はいつも見送る側ね。覚えていても、戻らない光はあるでしょう。", "見送るだけじゃない。戻らないなら、その光が進んだ先を守る。それも影の役目だ。", "名もなく待つ者……寂しいのに強い。そんな影なら、別れも簡単には呑めないわ。" };
        postBossLines["abyssallilithe|heroilena"] = new[]{ "観測者は、別れの数だけ冷えていく。観れば観るほど、終わりばかり増えるのに。", "終わりが増えても、始まりも記録できます。冷えても、結論まで凍らせる必要はありません。", "あの人にも、そう言ってあげて。影ではなく、あなたの声で届かせて。" };
        postBossLines["abyssalcassyva|heroaldin"] = new[]{ "ほらね。守るの、しんどいのにやめられないでしょ。そのしんどさ、投げ捨てたくならない？", "なる。だからこそ、投げ捨てないと決める。しんどいから、生きている者を見失わずに済む。", "いい顔。まだまだ生き汚くいきなさい。そのしつこさこそ、生の匂いよ。" };
        postBossLines["abyssalcassyva|herokagachi"] = new[]{ "その姿でも勝つんだ。刃も威圧も削いだのに、痛くて、腹が減って、息が上がって、それでも前を見るのね。", "笑うな。いや、笑ってろ。剥き身で格好悪くても、俺はこの足で進むし、その格好悪さごと連れていく。", "そう、それ。それが生きてるってことよ。屈辱じゃなくて、しぶとい命の形として覚えておきなさい。" };
        postBossLines["abyssalcassyva|herovesna"] = new[]{ "火傷しながら居場所を作るなんて、ほんと面倒な子。温めれば温めるほど、守りたくなっちゃうでしょ。", "うん。守りたいと思える場所が欲しかった。面倒でも、冷たいまま終わるよりずっといい。", "あは、好きよ。そういうしぶとさ。生き物は面倒だから面白いの。" };
        postBossLines["abyssalcassyva|heroziran"] = new[]{ "苦しむ命にまだ付き合うの？ 付き合うあなたも傷つくのに、ほんと物好きね。", "傷ついても、隣にいる意味は消えません。苦しむからこそ、一人にはしません。", "いいじゃない。生はそうでなくちゃ。痛みまで含めて、息をしているのね。" };
        postBossLines["abyssalcassyva|heroreva"] = new[]{ "逃げてもいいのに、残るんだ。痛いのも怖いのも、自分で選んだってこと？", "そう。残りたいから残る。選んだなら、痛いのも怖いのもちゃんと私のものだよ。", "自分で選ぶ鼓動か。退屈しないわね。そういう命は、終わりにくくていい。" };
        postBossLines["abyssalcassyva|herokara"] = new[]{ "待つのって、退屈で、痛くて、お腹も減るでしょ。生き物は待つのが下手なのにね。", "下手でも待つ。待たれた記憶が、戻る命を急かさずに支えることもあるから。", "生きてる待ち方ね。嫌いじゃないわ。退屈まで抱えるなんて、たいしたものよ。" };
        postBossLines["abyssalcassyva|herobrome"] = new[]{ "いろんな命を一つに束ねるなんて、騒がしいわ。騒がしさは、いずれ衝突になるわよ。", "衝突しても立て直す。生きた軍は静かに終わらないし、違う声があるから前へ届く。", "その騒音、終焉には毒ね。いいわ、もっと騒がしく生きなさい。" };
        postBossLines["abyssalcassyva|heroshidai"] = new[]{ "影まで息をしてる。守るためにそんなに自分を薄くするの、隠しても分かるわよ。", "薄くても、消えたわけじゃない。息を殺すのは死ぬためじゃなく、守るためだ。", "ふふ、生きてる影なんて最高じゃない。見えない鼓動ほど、意外としぶといのよ。" };
        postBossLines["abyssalcassyva|heroilena"] = new[]{ "観測して、分析して、それでも生に賭けるの？ 揺れは誤差で、時々ただ痛いだけよ。", "その痛みごと、記録から捨てたくありません。数字の外で、命は何度も揺れます。", "その揺れが面白いのよ。覚えておきなさい、未来はいつも少し不格好に跳ねるの。" };
        postBossLines["abyssalmaehv|heroaldin"] = new[]{ "あなたはまだ、眠りを拒むのですね。名を抱けば夜は長くなり、救いから遠ざかります。", "眠れない名がある。長い夜でも盾は下ろさない。その名を守り続けると決めたから。", "救済より深い痛み……届きませんでしたか。ならばその夜を、あなたの足で越えてください。" };
        postBossLines["abyssalmaehv|herokagachi"] = new[]{ "苦しみから救って差し上げたかった。抱えたままでは、あなたはまた倒れます。", "倒れたら起きる。起きられなきゃ這う。苦しみも俺のものだ、勝手に終わらせるな。", "その強情さも、命なのですね。眠りではほどけない結び目を見ました。" };
        postBossLines["abyssalmaehv|herovesna"] = new[]{ "深淵なら、居場所を探す苦しみも消えます。見つからなければ、ただ凍えるだけです。", "消したくない。見つからない時は、凍えた場所に火を置く。探した先に、誰かがいるから。", "温もりは、救済より残酷です。それでもあなたは、その残酷さを選ぶのですね。" };
        postBossLines["abyssalmaehv|heroziran"] = new[]{ "苦しみを終えることも救いです。支えても、朝を迎えられない命があると知っているでしょう。", "知っています。それでも夜を独りにはしません。祈りは過去を戻さず、次の手を温めます。", "あなたの救いは、眠りより長いのですね。救えなかった名まで、静かに連れていくのですか。" };
        postBossLines["abyssalmaehv|heroreva"] = new[]{ "休めば、選ぶ苦しみから解かれます。選択はいつも傷を増やすものです。", "傷つかない自由なんて、たぶん自由じゃない。選べる苦しさごと、私は私でいたい。", "自由は、安らぎより重いのですか。あなたの翼は、眠りに畳まれませんでした。" };
        postBossLines["abyssalmaehv|herokara"] = new[]{ "待つ痛みからも、眠れば解かれます。戻らぬ命を待つ夜は、あまりに長い。", "それでも、戻れる命のために腕を空けておく。待つ痛みがあるから、抱ける命もある。", "その腕には、眠りでは届きませんね。救いの外にある温度を、あなたは守るのですか。" };
        postBossLines["abyssalmaehv|herobrome"] = new[]{ "争う声を眠らせれば、皆が静かになります。違う声は、互いを傷つけるだけではありませんか。", "静かでなくていい。違う声が生きている証だし、旗は黙らせるためではなく聞き合うためにある。", "救いは、少し静かすぎたのでしょうか。あなたの軍旗は、眠りより騒がしく人を結ぶ。" };
        postBossLines["abyssalmaehv|heroshidai"] = new[]{ "影は眠れば、名を求めずに済みます。名を得られぬままの生は、寂しいでしょう。", "寂しくても、誰かの明日を支えられる。名を求めるのは、まだ生きているからだ。", "生きる影を、眠らせ損ねました。あなたの寂しさは、深淵より静かに強い。" };
        postBossLines["abyssalmaehv|heroilena"] = new[]{ "観測者の疲れを、あなたも知っているはずです。見続けることは、心を摩耗させます。", "知っています。それでも、見たものから希望を選ぶことはできます。忘れたなら、もう一度見せます。", "同じ夜から、違う朝を見るのですね。ならばその朝を、あの方にも差し出してください。" };
        postBossLines["vetruvianzirix|heroaldin"] = new[]{ "管理不能。守護行動、予測値を超過。誤差を残せば、全体最適は崩壊する。", "命は誤差ではない。その誤差の中に守るべき顔があり、守る手は数式では止まらない。", "未分類要素……記録を継続する。管理不能だが、削除だけでは説明できない。" };
        postBossLines["vetruvianzirix|herokagachi"] = new[]{ "前進経路、計算不能。転倒後の再起回数、異常。転倒は失敗であり、修正対象。", "異常でいい。失敗した足でも次を踏めるし、転ぶたび道を増やせる。", "逸脱値により、管理式が破損した。失敗を燃料にする個体は、規格外である。" };
        postBossLines["vetruvianzirix|herovesna"] = new[]{ "居場所の自律生成を確認。割当規則に反する。未許可の熱源は秩序を乱す。", "規則の外でも火は置ける。乱れても、誰かが凍えるよりはずっといい。", "規格外の熱源。再計算不能。だが、その熱で停止した領域が動いた。" };
        postBossLines["vetruvianzirix|heroziran"] = new[]{ "苦痛除去ではなく、苦痛共存を選択。非効率。苦痛継続は、損耗を拡大する。", "非効率でも命は切り捨てません。損耗する命を支えるために、私はいます。", "効率外の価値……当機には未実装。だが未実装要素により、当機は停止した。" };
        postBossLines["vetruvianzirix|heroreva"] = new[]{ "自由選択により、行動予測が分岐し過ぎた。分岐削減により、安定化が可能。", "安定だけで生きるなら、空は要らない。分岐するから自由なんだよ。", "未確定分岐、管理領域を圧迫。だがその圧迫が、閉じた経路を破壊した。" };
        postBossLines["vetruvianzirix|herokara"] = new[]{ "待機行動の目的、保留ではなく支援と判定。静止状態に価値を認めるのは非合理。", "止まることと支えることは違う。動けない誰かには、その静止が必要になる。", "静止に意味があるとは、管理外だった。待機を無価値とした判定を修正する。" };
        postBossLines["vetruvianzirix|herobrome"] = new[]{ "異種混成軍、統制不能。だが崩壊せず。不統一は故障率を上げるはずである。", "同じにする必要はない。違いがあるから、片方の故障を片方が補える。", "統一なき秩序……理解不能。だが崩壊しない以上、未知の秩序として記録する。" };
        postBossLines["vetruvianzirix|heroshidai"] = new[]{ "非表示要素が戦況を左右。観測漏れ。非表示要素は、管理対象から除外される。", "除外された場所からしか、届かない支えもある。見えないものにも役目はある、名前もな。", "影の変数、危険度を上方修正。不可視要素は、削除ではなく再観測対象とする。" };
        postBossLines["vetruvianzirix|heroilena"] = new[]{ "観測結果から希望を選択。論理矛盾。記録は結論へ収束するべきである。", "矛盾ではありません。観測は命令ではなく、収束しない余白が未来を作ります。", "記録と選択の分離……重大な逸脱。だがその逸脱が、当機の結論を停止させた。" };
        postBossLines["vetruviansajj|heroaldin"] = new[]{ "愛したから諦めた。その理は、まだ届かないか。守るほど、失う数を知ることになる。", "それでも、数えた名を捨てない。愛したなら、守る理由にはなっても諦める理由にはしない。", "その答えが、砂を少し焦がした。あの方が忘れた熱に似ている。" };
        postBossLines["vetruviansajj|herokagachi"] = new[]{ "進んでも砂だ。倒しても、また渇く。探しても水が見つからぬ時、人は諦める。", "その時まで進んでから決める。渇いても、水を探す足まで捨てる気はねえ。", "乾いた火ほど、消えにくい。お前の足跡は、砂にしては深すぎる。" };
        postBossLines["vetruviansajj|herovesna"] = new[]{ "居場所は砂に還る。熱も、名も。十分と思えぬから、あの方は疲れた。", "還る日まで火を置く。それで十分だと思える小さな温度を、私は忘れない。", "十分……私が忘れた言葉だ。その小ささが、砂漠には眩しい。" };
        postBossLines["vetruviansajj|heroziran"] = new[]{ "苦しむ命を見続ければ、やがて諦める。支えた手にも、砂は積もる。", "積もった砂を払って、また手を伸ばします。諦めるためではなく、支えるために見続けます。", "その眼差しは、砂嵐に伏せない。乾いた世界には、少し痛い光だ。" };
        postBossLines["vetruviansajj|heroreva"] = new[]{ "逃げる自由を選べば、焼かれずに済む。その選択も、いつか重荷になるだろう。", "重くなったら、また選び直す。ここに残る自由も、焼かれても私の選択だ。", "自由は、諦めより熱いのか。その熱なら、砂の上でもしばらく残る。" };
        postBossLines["vetruviansajj|herokara"] = new[]{ "待っても、砂は足跡を消す。覚えている者も、いずれ疲れる。", "疲れたら休む。でも忘れるためには休まない。歩いた人がいたことは、私が覚える。", "覚える者がいる限り、砂も完全ではない。お前の待つ時間は、砂より遅く強い。" };
        postBossLines["vetruviansajj|herobrome"] = new[]{ "軍旗も、最後は砂に倒れる。拾う者がいなければ、ただの布に戻るだけだ。", "だから拾う者を増やしてきた。旗は一人のものではなく、倒れても誰かが継ぐ。", "継がれるものを、私は侮った。砂に残らぬと思った印が、まだ立っている。" };
        postBossLines["vetruviansajj|heroshidai"] = new[]{ "影の働きも、砂に残らぬ。前へ進んだ者が、影を忘れることもある。", "忘れられても、支えた事実は消えない。守られた背中が前へ進むなら、それでいい。", "名のない足跡が、砂を乱した。残らぬはずの影が、私の視界に焼きついた。" };
        postBossLines["vetruviansajj|heroilena"] = new[]{ "観測すれば、諦めに至る。何度も失えば、希望は記録の中で擦り切れる。", "擦り切れても、次の一行は書けます。同じものを観ても、同じ結論にはしません。", "その違いを、あの方に見せられるか。閉じる前の頁に、まだ続きがあると。" };
        postBossLines["vetruvianscion|heroaldin"] = new[]{ "定めの糸が、盾に絡んで切れたか。糸を切れば、次の痛みも乱れ込む。", "それでも、守る相手を定めに渡さない。運命でも、守る手を止める理由にはならない。", "終幕の舞に、まだ続きが生まれる。お前の盾が、舞台の幕を押し返した。" };
        postBossLines["vetruvianscion|herokagachi"] = new[]{ "砂に描いた道を、お前は泥まみれで踏み越えた。描かれた終わりを、足跡で汚すか。", "汚れてもいい。線の上でも外でも、走るのは俺だ。綺麗な終わりなんて要らねえ。", "その足取り、定めには重すぎる。終幕の砂に、よくもまあ深く踏み込んだものだ。" };
        postBossLines["vetruvianscion|herovesna"] = new[]{ "視えた結末に、火が揺らぎを足した。揺らぎは、結末を不確かにする。", "不確かだから、居場所を作る余地がある。誰かと囲む火なら、揺らいでも消えない。", "終幕に、温度が残るとは。視えた未来が、少しだけ曇った。" };
        postBossLines["vetruvianscion|heroziran"] = new[]{ "苦しみも別れも、定めに織り込まれている。手を添えても、布の終わりは変わらぬ。", "終わるまでの肌触りは、変えられます。織り込まれていても、寄り添う手は選べます。", "優しさは、運命の余白に咲くのか。定めの布に、柔らかなほころびを見た。" };
        postBossLines["vetruvianscion|heroreva"] = new[]{ "お前の矢筋も、舞の一節に見えていた。決めたと思う心も、定めの内かもしれぬ。", "それでも、この指で弦を離した。見えていたとしても、撃つと決めたのは私だ。", "選ぶ矢は、定めを少し外す。わずかな外れが、舞の形を変えた。" };
        postBossLines["vetruvianscion|herokara"] = new[]{ "待つ者の結末は、遅れて届く悲しみだ。待つ間にも、終幕は近づく。", "近づくなら、倒れないよう支える時間も必要になる。それでも待つ意味はある。", "遅い舞にも、意味は宿るのだな。急がぬ足が、終幕の拍を乱した。" };
        postBossLines["vetruvianscion|herobrome"] = new[]{ "それぞれの定めが、軍旗の下で絡み合った。絡みすぎれば、身動きも取れぬ。", "その時は互いにほどく。一本では切れる糸も、束なら残るし、軍とは命令だけではない。", "運命を束ねる旗……見事だ。揃わぬ糸が、終幕の布を破った。" };
        postBossLines["vetruvianscion|heroshidai"] = new[]{ "影の糸は、舞台の下で終わるはずだった。観客は、その糸を見ぬ。", "見えなくていい。下で支える糸が切れなければ、光は倒れず、舞台は続く。", "終幕の床板までも、影が支えていたか。見えぬ糸を、私は読み落とした。" };
        postBossLines["vetruvianscion|heroilena"] = new[]{ "観測した結末を、なお疑うのか。終幕を観た者は、幕を下ろす安堵も知る。", "知っています。けれど、安堵だけでは明日は生まれません。私は疑うのではなく、続きを選びます。", "では、その一行を終焉の先へ書け。観測者に近い者よ、同じ結論では終わらせるな。" };
        // ch13-20（新ボス8体×9主人公=72組）。Mechaz0r連章は機械口調、ハイドラックス=増殖、Arcana=エンディングへの橋渡し。
        postBossLines["neutral_mechaz0rwing|heroaldin"] = new[]{ "翼部、破損。自由飛行を否定する規格が、守護行動により突破された。", "守るための前進まで、規格に閉じ込めさせはしない。抱えた名ごと、私は進む。", "記録。守護は拘束ではなく推進となる。規格外の盾を、次工程へ送る。" };
        postBossLines["neutral_mechaz0rwing|herokagachi"] = new[]{ "翼部、失速。転倒を前提とする歩行個体が、飛行規格を逸脱した。", "飛べなくても進める。転んだ足でも、地面を蹴れば前には出る。", "記録。低位歩行は自由否定の対象外ではない。規格外の転倒を継続観測。" };
        postBossLines["neutral_mechaz0rwing|herovesna"] = new[]{ "翼部、凍結解除不能。割当外の熱源が、飛行経路を変質させた。", "居場所は割り当てられるものじゃない。冷たい空にも、火を置けば進める。", "記録。自律生成された温度は、自由抑制規格に干渉する。" };
        postBossLines["neutral_mechaz0rwing|heroziran"] = new[]{ "翼部、機能低下。苦痛保持個体を廃棄せず支える行動が、飛行効率を阻害した。", "効率より、落ちる命に添う手を選びます。苦しみごと支えれば、また羽ばたける命もあります。", "記録。支援行動は非効率だが、墜落後の再起率を上昇させる。" };
        postBossLines["neutral_mechaz0rwing|heroreva"] = new[]{ "翼部、制御喪失。自由選択個体の軌道が、拘束翼の予測円を外れた。", "空を飛ぶか、地上に残るかは私が決める。翼があるから従うわけじゃない。", "記録。自由は移動能力ではなく、選択権として機能する。" };
        postBossLines["neutral_mechaz0rwing|herokara"] = new[]{ "翼部、停止。待機行動が逃避ではなく、帰還経路の維持として作用した。", "飛べない時に、戻る場所が必要になる。待つことも、落ちないための支えだから。", "記録。静止は自由の否定ではなく、再出発地点となり得る。" };
        postBossLines["neutral_mechaz0rwing|herobrome"] = new[]{ "翼部、編隊崩壊。異種混成軍の不揃いな進路が、単一飛行規格を破壊した。", "同じ翼で飛ぶ必要はない。違う者同士でも、同じ旗の下なら進路を合わせられる。", "記録。不統一編隊、規格化翼より高い継続性を示す。" };
        postBossLines["neutral_mechaz0rwing|heroshidai"] = new[]{ "翼部、観測漏れ。不可視支援体が、主軌道の墜落を防止した。", "影は飛ばなくてもいい。光が落ちないよう支えるなら、それにも名がある。", "記録。不可視要素は、自由否定規格の死角から機能する。" };
        postBossLines["neutral_mechaz0rwing|heroilena"] = new[]{ "翼部、論理停止。観測済み終端から希望を選択した個体により、飛行経路が分岐した。", "結末を観ても、同じ結論を選ぶ義務はありません。空白の先へ進むこともできます。", "記録。観測は拘束条件ではない。自由否定規格、再定義を要求。" };
        postBossLines["neutral_mechaz0rsword|heroaldin"] = new[]{ "剣部、破断。断罪効率は、守護対象を抱えた個体に対して不十分。", "断ち切れば軽くなるものばかりじゃない。守れなかった名も、切らずに持っていく。", "記録。未断罪の後悔が、戦闘継続力へ変換された。" };
        postBossLines["neutral_mechaz0rsword|herokagachi"] = new[]{ "剣部、切断失敗。弱点判定多数の個体が、断罪手順を突破した。", "弱点が多くても終わりじゃねえ。斬られて転んでも、そこから進める。", "記録。脆弱性は停止条件ではない。断罪効率、低下。" };
        postBossLines["neutral_mechaz0rsword|herovesna"] = new[]{ "剣部、熱変形。居場所なき個体への切断命令が、自律熱源により阻害された。", "私の居場所は、あなたの刃で決まらない。冷たい場所にも、火を置けば変わる。", "記録。切断対象が環境を生成。効率的断罪に不適。" };
        postBossLines["neutral_mechaz0rsword|heroziran"] = new[]{ "剣部、裁定不能。苦痛継続個体を救済対象ではなく支援対象とした。", "苦しみを切り捨てるだけが救いではありません。その命が続くなら、私は支えます。", "記録。切断による救済、支援による継続に敗北。" };
        postBossLines["neutral_mechaz0rsword|heroreva"] = new[]{ "剣部、命中予測喪失。自由選択個体は、最短断罪線上に留まらなかった。", "最短の道が正しいとは限らない。逃げるのも残るのも、私が選ぶ。", "記録。効率線は自由行動に対し脆弱。断罪手順、再演算。" };
        postBossLines["neutral_mechaz0rsword|herokara"] = new[]{ "剣部、斬撃停滞。待機個体が、切断線上で後続防護を継続した。", "すぐ動けなくてもいい。崩れないよう支える時間が、誰かを守ることもある。", "記録。遅延行動は非効率ではなく、防護層として機能。" };
        postBossLines["neutral_mechaz0rsword|herobrome"] = new[]{ "剣部、分断失敗。異種混成軍は、切断後も連結を保持した。", "違う者同士でも、一つになれる。切られた声は、また旗の下で結び直す。", "記録。断罪剣は不統一結束を完全分断できない。" };
        postBossLines["neutral_mechaz0rsword|heroshidai"] = new[]{ "剣部、対象喪失。不可視支援体が、切断前に主対象を移動させた。", "影は刃の前に出るためだけにあるんじゃない。光を支える道にも名はある。", "記録。不可視名義の支援、断罪処理を遅延させる。" };
        postBossLines["neutral_mechaz0rsword|heroilena"] = new[]{ "剣部、判定不能。観測済み罪状から希望選択へ移行した個体を断罪できず。", "観測した悲劇は罪状ではありません。そこから何を選ぶかが、まだ残っています。", "記録。断罪は観測者の選択余地を消去できない。" };
        postBossLines["neutral_mechaz0rsuper|heroaldin"] = new[]{ "完全体、損壊。機械神の統一意思は、守れなかった名を抱く盾に阻止された。", "完全な神など要らない。不完全でも、守り続ける手があるなら私はそれを選ぶ。", "記録。欠落を保持する守護個体、完成神話に対する重大な反証。" };
        postBossLines["neutral_mechaz0rsuper|herokagachi"] = new[]{ "完全体、損壊。転倒反復個体が、完成された機械神の歩行理論を破壊した。", "完成なんかしてなくていい。弱いまま、転んだ姿ごと前へ進めるなら十分だ。", "記録。不完全歩行、完成機構を超過。神格演算に亀裂。" };
        postBossLines["neutral_mechaz0rsuper|herovesna"] = new[]{ "完全体、熱暴走。自律生成された居場所の火が、機械神の冷却秩序を侵した。", "完成した場所じゃなくていい。冷たい場所にも、誰かと火を置けば居場所になる。", "記録。不完全な温度が完全機構を溶融。神格維持、不能。" };
        postBossLines["neutral_mechaz0rsuper|heroziran"] = new[]{ "完全体、損傷。苦痛除去神性は、苦しみごと支える癒しにより否定された。", "苦しみを消すだけが救いではありません。苦しむ命を支え、続かせることもできます。", "記録。完全な沈黙より、不完全な継続が選択された。" };
        postBossLines["neutral_mechaz0rsuper|heroreva"] = new[]{ "完全体、統制喪失。全経路を統一する神性が、自由選択の分岐に敗北した。", "完全に決まった道なんて退屈だよ。残って戦うのも、飛び去るのも、私が選ぶ。", "記録。自由分岐は神格統制を拒絶。完全性、棄却。" };
        postBossLines["neutral_mechaz0rsuper|herokara"] = new[]{ "完全体、停止。即時変化を要求する神性が、待機支援行動を突破できず。", "すぐ変われなくていい。待つことも、崩れないよう支えることも守ることだから。", "記録。遅延と未完成は、神格命令への抵抗値を持つ。" };
        postBossLines["neutral_mechaz0rsuper|herobrome"] = new[]{ "完全体、分解。単一神格は、異なる者同士の結束により統一性を失った。", "一つになるとは、同じになることではない。違う声を束ねて、遠くへ進むことだ。", "記録。不一致の統合、完全なる単一性を破壊。" };
        postBossLines["neutral_mechaz0rsuper|heroshidai"] = new[]{ "完全体、盲点露出。神格視野外の影が、主構造の保持を妨害した。", "光を支える影にも名がある。見えないまま終わるためじゃなく、次を守るために。", "記録。不可視名は神格観測の外部から完全性を破る。" };
        postBossLines["neutral_mechaz0rsuper|heroilena"] = new[]{ "完全体、論理破綻。全悲劇観測後の希望選択が、機械神の終端演算を反転させた。", "同じ悲劇を観ても、同じ結論にはしません。観測した上で、希望を選びます。", "記録。希望選択は完成神の終端より上位の未確定要素。" };
        postBossLines["neutral_mechaz0rhelm|heroaldin"] = new[]{ "兜部、命令停止。守護対象を切り捨てる命令に、盾持つ個体が従わず。", "命令で忘れられる名ではない。リオラの名も、守るべき者も、私が抱えて進む。", "記録。記憶保持は命令服従を阻害。だが戦闘継続を強化。" };
        postBossLines["neutral_mechaz0rhelm|herokagachi"] = new[]{ "兜部、思考制御失敗。弱点多数個体が、停止命令を拒否。", "弱いって分かってても、命令通り止まる気はねえ。転んでも俺が決める。", "記録。脆弱個体の自己決定、命令系統に対し高抵抗。" };
        postBossLines["neutral_mechaz0rhelm|herovesna"] = new[]{ "兜部、冷却命令不履行。異端熱源が、指定居場所への帰属を拒否。", "私の場所は命令で決まらない。冷たい場所にも、火を置く相手は自分で選ぶ。", "記録。熱源自律性、命令系統を溶解。" };
        postBossLines["neutral_mechaz0rhelm|heroziran"] = new[]{ "兜部、救済命令拒絶。苦痛個体の停止ではなく、支援継続を選択。", "命を止める命令には従いません。苦しみごと生きる命を、私は支えます。", "記録。癒し手の倫理、命令優先度を上書き。" };
        postBossLines["neutral_mechaz0rhelm|heroreva"] = new[]{ "兜部、指揮不能。自由個体が、進路命令を軽視。", "命令されて飛ぶのは自由じゃない。ここに残るのも、私が選んだから意味がある。", "記録。選択主体を持つ個体、命令系統への組み込み不能。" };
        postBossLines["neutral_mechaz0rhelm|herokara"] = new[]{ "兜部、即応命令失敗。待機個体が、変化命令より支援維持を優先。", "すぐ変われなくていい。急がせずに支える時間が、守る力になることもある。", "記録。非即応行動は命令遅延ではなく、防衛意志。" };
        postBossLines["neutral_mechaz0rhelm|herobrome"] = new[]{ "兜部、単一指揮崩壊。混成軍の複数意思が、命令一元化を拒否。", "軍は命令だけで動くものではない。違う者同士が同じ旗を選ぶから、遠くへ行ける。", "記録。多声指揮、単一命令より高い復元性を示す。" };
        postBossLines["neutral_mechaz0rhelm|heroshidai"] = new[]{ "兜部、認識漏れ。不可視支援体が、命令対象の行動自由度を維持。", "見えなくても、影には役目がある。命令で消える名じゃない。", "記録。不可視名、思考停止命令への干渉要因。" };
        postBossLines["neutral_mechaz0rhelm|heroilena"] = new[]{ "兜部、思考停止失敗。観測者型個体が、結論命令への服従を拒否。", "観測は命令ではありません。同じ悲劇を観ても、希望を選ぶ余地はあります。", "記録。観測知性、思考停止命令の外部で未来を生成。" };
        postBossLines["neutral_mechaz0rchassis|heroaldin"] = new[]{ "胴部、量産停止。守護対象を持つ個体が、規格化された心臓部を貫通。", "命は同じ形に揃えるものではない。守れなかった名も、今守る命も、それぞれ違う。", "記録。個別名の保持、量産規格に対する阻害因子。" };
        postBossLines["neutral_mechaz0rchassis|herokagachi"] = new[]{ "胴部、増殖規格停止。転倒反復個体が、量産歩行モデルに適合せず。", "俺は型通りには歩けねえ。転んだ跡も、そのまま俺の道だ。", "記録。不規則歩行、量産同期を破壊。" };
        postBossLines["neutral_mechaz0rchassis|herovesna"] = new[]{ "胴部、炉心干渉。自律熱源が、量産居住区画を再定義。", "同じ箱に入れられた場所なんて、居場所じゃない。誰かと火を置いて初めてそう呼べる。", "記録。居場所生成は量産規格に従属しない。" };
        postBossLines["neutral_mechaz0rchassis|heroziran"] = new[]{ "胴部、同期異常。苦痛個体の標準停止処理が、支援行動により中断。", "標準で命を測りません。苦しみごと生きる一人を、私は一人として支えます。", "記録。個別支援、量産処理を減速させるが、命の継続率を上げる。" };
        postBossLines["neutral_mechaz0rchassis|heroreva"] = new[]{ "胴部、量産統制不能。自由個体が、同一進路配列から離脱。", "みんなと同じ道を走る必要はない。残るのも離れるのも、私の選択だよ。", "記録。自由分岐、量産隊列を解体。" };
        postBossLines["neutral_mechaz0rchassis|herokara"] = new[]{ "胴部、製造遅延。待機支援個体が、即時量産サイクルを阻害。", "速く増やすことが強さじゃない。変われない誰かを支える時間も必要だから。", "記録。遅延は欠陥ではなく、保護機構として作動。" };
        postBossLines["neutral_mechaz0rchassis|herobrome"] = new[]{ "胴部、規格混線。異種混成軍が、単一量産規格を拒否。", "違う者同士だから、一つになれる。揃えられた部品では、軍旗は掲げられない。", "記録。不統一構成、量産性を失うが、結束強度を増す。" };
        postBossLines["neutral_mechaz0rchassis|heroshidai"] = new[]{ "胴部、内部支援検出。影が心臓部への標準侵入経路外から干渉。", "見えない道にも意味はある。影の名は、支えたものの中に残る。", "記録。不可視経路、量産防壁を無効化。" };
        postBossLines["neutral_mechaz0rchassis|heroilena"] = new[]{ "胴部、同期停止。観測個体が、量産される結論を拒否。", "悲劇が繰り返されても、答えまで量産する必要はありません。希望を選ぶ余白があります。", "記録。反復観測から非反復結論を生成。量産不能。" };
        postBossLines["neutral_mechaz0rcannon|heroaldin"] = new[]{ "砲部、発射停止。殲滅命令は、守護盾の前進により不達。", "終末兵器の前でも、守る手は下ろさない。守れなかった名が、私を退かせない。", "記録。殲滅対象内の個別名が、終末処理を阻害。" };
        postBossLines["neutral_mechaz0rcannon|herokagachi"] = new[]{ "砲部、照準逸脱。転倒個体の再起動作が、殲滅予測を外した。", "砲で吹き飛ばされても、まだ進む。弱い足ほど、止め方が面倒なんだよ。", "記録。脆弱再起個体、終末兵器に対し高い残存性。" };
        postBossLines["neutral_mechaz0rcannon|herovesna"] = new[]{ "砲部、熱量逆流。殲滅火力は、居場所を作る小火により制御を失った。", "焼き尽くす火と、誰かを温める火は違う。私は冷たい場所に置く火を選ぶ。", "記録。微小熱源、終末火力を反転。" };
        postBossLines["neutral_mechaz0rcannon|heroziran"] = new[]{ "砲部、殲滅判定失敗。苦痛を消す目的の火力が、支援対象に遮断された。", "苦しみを消すために命ごと消すなら、それは救いではありません。私は続く命を支えます。", "記録。支援倫理により終末処理停止。" };
        postBossLines["neutral_mechaz0rcannon|heroreva"] = new[]{ "砲部、照準不能。自由選択個体が、殲滅線上への固定を拒否。", "狙われた場所に立ち続ける義理はない。でも逃げるか残るかは、私が決める。", "記録。自由個体、終末照準を分散。" };
        postBossLines["neutral_mechaz0rcannon|herokara"] = new[]{ "砲部、発射遅延。待機支援個体が、殲滅前の防護時間を拡張。", "急いで終わらせる力には、急がず支える力で向き合う。待つ時間も守ることだから。", "記録。遅延防護、終末兵器の決定性を低下。" };
        postBossLines["neutral_mechaz0rcannon|herobrome"] = new[]{ "砲部、範囲殲滅失敗。混成軍の分散結束が、単一爆心を無効化。", "違う者同士だから、一撃では折れない。旗は一か所ではなく、皆の胸にある。", "記録。分散結束、終末火力に対し有効。" };
        postBossLines["neutral_mechaz0rcannon|heroshidai"] = new[]{ "砲部、影域検出不能。不可視支援体が、発射前に照準をずらした。", "見えないから届く場所がある。影の名は、守った次の一歩に残る。", "記録。不可視支援、終末兵器の盲点として機能。" };
        postBossLines["neutral_mechaz0rcannon|heroilena"] = new[]{ "砲部、終端演算停止。観測済み破滅から希望を選ぶ個体により、殲滅結論が不成立。", "破滅を観たからといって、同じ結論へ撃ち込む必要はありません。私は続きを選びます。", "記録。希望選択、終末兵器の最終命令を上書き。" };
        postBossLines["neutral_hydrax|heroaldin"] = new[]{ "首を落としても、まだ脈が残る……守る者よ、お前の盾は増える終わりを押し返した。", "終わりが増えるなら、守る手も増やす。リオラの名を抱えたまま、私は退かない。", "なら進め……だが忘れるな。斬っても増えるものは、まだ水底で目を開けている。" };
        postBossLines["neutral_hydrax|herokagachi"] = new[]{ "噛み砕いても、這い戻る足か。お前は増える牙の中で、転びながら道を作った。", "何度増えても進む。弱い足でも、止まらなきゃ次の地面に届く。", "行け……だが水音を忘れるな。斬っても増える首は、またお前の足を嗅ぎつける。" };
        postBossLines["neutral_hydrax|herovesna"] = new[]{ "火を置く者よ、水底の群れまで温めるとはな。冷たい増殖が、お前の居場所を呑めなかった。", "呑まれても、また火を置く。冷たい場所にも、誰かと作れる場所はある。", "その火を抱えて進め……だが湿った闇で、斬っても増える影はまだ蠢く。" };
        postBossLines["neutral_hydrax|heroziran"] = new[]{ "癒し手よ、増える苦痛を終わらせず支えたな。水底の喉は、お前の手を呑みきれなかった。", "苦しみが増えても、命ごと切り捨てません。届く手がある限り、支えます。", "進め……だが覚えておけ。斬っても増える痛みは、静かに次の傷口を探す。" };
        postBossLines["neutral_hydrax|heroreva"] = new[]{ "風の足よ、増える首の輪を外れたな。水底の牙は、お前を一つの道に閉じ込められなかった。", "閉じ込められる気はないよ。逃げるのも残るのも、私が選ぶ自由だから。", "飛べ……だが水面を見ろ。斬っても増える首は、次の自由を映している。" };
        postBossLines["neutral_hydrax|herokara"] = new[]{ "待つ者よ、増える牙の前で崩れなかったな。水底の群れは、お前の静けさを噛み切れなかった。", "すぐ変われなくてもいい。戻る命のために、支える場所を残すだけ。", "なら待て……だが深みに耳を澄ませろ。斬っても増えるものは、戻る道にも潜む。" };
        postBossLines["neutral_hydrax|herobrome"] = new[]{ "軍旗持つ者よ、増える首を束ね返したか。ばらばらの声が、水底の群れより強く鳴った。", "違う者同士でも、一つになれる。首がいくつ増えても、旗は散らない。", "進軍せよ……だが忘れるな。斬っても増える群れは、次の旗の影で孵る。" };
        postBossLines["neutral_hydrax|heroshidai"] = new[]{ "影よ、増える首の隙間を通ったな。水底の目は、お前の名を捕まえられなかった。", "光を支える影にも名がある。見えない道からでも、次を守れる。", "行け……だが暗がりを侮るな。斬っても増える首は、影の中でも息をする。" };
        postBossLines["neutral_hydrax|heroilena"] = new[]{ "観測する者よ、増殖の結末を見ても退かなかったな。水底の数は、お前の希望を飲めなかった。", "同じ悲劇を観ても、同じ結論にはしません。増える終わりの中でも、未来を選びます。", "なら観ろ……だが記録に残せ。斬っても増えるものは、希望の縁にも牙をかける。" };
        postBossLines["arcana|heroaldin"] = new[]{ "まだ、守り続けるのですね。守れなかった名を抱えたまま歩く姿を、私はずっと観ていました。", "消えない名があるから、私は進めます。リオラの名も、ここまでの痛みも、未来へ連れていく。", "……そうですか。では、あなたたちの続きを観ましょう。終わりではなく、その先の音を。" };
        postBossLines["arcana|herokagachi"] = new[]{ "何度倒れても、まだ前を見るのですね。その不格好な歩みを、私は止められませんでした。", "弱いままでも進める。転んだ姿ごと、俺が前へ連れていく。", "……そうですか。では、あなたの続きを観ましょう。その足音がどこへ届くのか。" };
        postBossLines["arcana|herovesna"] = new[]{ "あなたの火は、冷たい場所にも残りました。居場所を作るという小さな奇跡を、私は見落としていたのかもしれません。", "居場所は最初からあるものじゃない。誰かと火を置けば、冷たい場所にも作れる。", "……そうですか。では、その温度の続きを観ましょう。世界がまだ冷えきらないのなら。" };
        postBossLines["arcana|heroziran"] = new[]{ "苦しみを消さず、支え続けるのですね。あなたの手は、眠りよりも長く命に寄り添いました。", "苦しみを消すだけが救いではありません。苦しみごと生きる命を、私は支えます。", "……そうですか。では、その祈りの続きを観ましょう。痛みの先にも朝があるのなら。" };
        postBossLines["arcana|heroreva"] = new[]{ "逃げてもよかったのに、あなたは残ることを選びました。その自由を、私は終わらせられませんでした。", "自由は逃げ道じゃない。ここに残って戦うことも、私が選んだ自由だよ。", "……そうですか。では、その選択の続きを観ましょう。まだ空が開いているのなら。" };
        postBossLines["arcana|herokara"] = new[]{ "急がず、変わりきれないものを待つのですね。あなたの静けさは、終幕よりも長く残りました。", "すぐに変われなくてもいい。待つことも、崩れないよう支えることも、守ることだから。", "……そうですか。では、その待つ時間の続きを観ましょう。戻る足音があるのなら。" };
        postBossLines["arcana|herobrome"] = new[]{ "違う声が、まだ一つの旗の下に立つのですね。揃わぬ者たちが崩れない理由を、私は見誤りました。", "違う者でも、一つになれる。揃わない声だからこそ、遠くまで届く。", "……そうですか。では、その軍旗の続きを観ましょう。争いの先にも結束があるのなら。" };
        postBossLines["arcana|heroshidai"] = new[]{ "見えない影にも名があるのですね。私は、光だけを観ていたのかもしれません。", "光を支える影にも、名がある。見えないまま終わるためじゃなく、次を守るために。", "……そうですか。では、その影の続きを観ましょう。見えない支えが世界を残すのなら。" };
        postBossLines["arcana|heroilena"] = new[]{ "あなたは、私と近い場所から同じ悲劇を観ました。それでも、同じ結論には来なかったのですね。", "私はあなたと同じ悲劇を観ました。だからこそ、同じ結論ではなく希望を選びます。", "……そうですか。では、あなたたちの続きを観ましょう。観測が諦めの別名ではないのなら。" };
    }

    private void RenderLine()
    {
        if (index < 0 || index >= lines.Count) { Finish(); return; }
        Line l = lines[index];
        bool ja = LocalizationManager.IsJapanese;

        LocalizationManager.ApplyFont(nameText);
        LocalizationManager.ApplyFont(bodyText);
        nameText.text = l.Name;
        bodyText.text = l.Text;
        nameBar.color = l.Hero ? new Color(0.35f, 0.7f, 1f, 0.95f) : new Color(0.9f, 0.45f, 0.3f, 0.95f);
        hintText.text = index >= lines.Count - 1
            ? (ja ? "クリックで戦闘開始" : "Click to begin battle")
            : (ja ? "クリックで次へ" : "Click for next");

        if (compact)
        {
            // 中ボス：左の小枠に「話者」の顔アイコンを出す（大立ち絵の縮小ではなく顔アイコン）。
            Sprite ic = DialogArt.FaceIcon(l.Hero ? curHeroId : curBossId);
            compactIcon.sprite = ic;
            // 色違い（同素体）は専用スプライト/アイコンで判別できるため、色フィルター(tint)は廃止し純色で表示。
            if (ic == null) compactIcon.color = new Color(1f, 1f, 1f, 0f);
            else compactIcon.color = Color.white;
        }
        else
        {
            // 章ボス：話者を強調（手前＋明るく）、相手を暗く奥に。
            Highlight(l.Hero ? heroPortrait : bossPortrait, true);
            Highlight(l.Hero ? bossPortrait : heroPortrait, false);
        }

        // 立ち絵が名前/セリフ枠を隠さないよう、枠とスキップを常に最前面へ。
        box.transform.SetAsLastSibling();
        skip.transform.SetAsLastSibling();

        // セリフ枠の小さなポップ。
        bodyText.transform.parent.localScale = Vector3.one * 0.99f;
        bodyText.transform.parent.DOScale(1f, 0.12f).SetUpdate(true);

        // キャラがしゃべっている感じに、本文を一文字ずつ表示する。
        StartTypewriter();
    }

    // 本文を maxVisibleCharacters で 0 から徐々に開く（rich-text崩れを避けるため substring は使わない）。
    private void StartTypewriter()
    {
        if (typeRoutine != null) { StopCoroutine(typeRoutine); typeRoutine = null; }
        bodyText.ForceMeshUpdate();
        int total = bodyText.textInfo.characterCount;
        if (total <= 0) { bodyText.maxVisibleCharacters = int.MaxValue; revealing = false; return; }
        bodyText.maxVisibleCharacters = 0;
        revealing = true;
        typeRoutine = StartCoroutine(TypewriterRoutine(total));
    }

    private IEnumerator TypewriterRoutine(int total)
    {
        int shown = 0;
        while (shown < total)
        {
            shown++;
            bodyText.maxVisibleCharacters = shown;
            yield return new WaitForSecondsRealtime(TypeCharInterval);
        }
        bodyText.maxVisibleCharacters = int.MaxValue;
        revealing = false;
        typeRoutine = null;
    }

    // 文字送り中のクリックで全文を即座に表示する。
    private void CompleteReveal()
    {
        if (typeRoutine != null) { StopCoroutine(typeRoutine); typeRoutine = null; }
        bodyText.maxVisibleCharacters = int.MaxValue;
        revealing = false;
    }

    private void Highlight(Image portrait, bool active)
    {
        if (portrait == null) return;
        portrait.DOKill();
        portrait.DOColor(active ? Color.white : new Color(0.45f, 0.48f, 0.55f, 0.9f), 0.15f).SetUpdate(true);
        RectTransform r = portrait.rectTransform;
        // 立ち絵ごとの基準スケールに、ハイライト演出(話者=等倍/非話者=0.94)を掛ける（基準サイズを上書きしない）。
        float baseScale = (portrait == heroPortrait) ? heroBaseScale : bossBaseScale;
        r.DOScale(baseScale * (active ? 1f : 0.94f), 0.15f).SetUpdate(true);
        if (active) portrait.transform.SetAsLastSibling();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // オートプレイ(debug)用：ダイアログ表示中ならスキップして戦闘開始へ進める（onComplete発火）。
    public bool DebugAutoResolve()
    {
        if (root == null || !root.activeSelf) return false;
        Finish();
        return true;
    }
#endif

    private void Advance()
    {
        // 文字送り中のクリックは「次へ」ではなく、まず全文を即表示する。
        if (revealing) { CompleteReveal(); return; }
        index++;
        if (index >= lines.Count) Finish();
        else RenderLine();
    }

    private void Finish()
    {
        if (root != null) root.SetActive(false);
        Action cb = onComplete; onComplete = null;
        cb?.Invoke();
    }

    // ====== STORY: 章ボス × 主人公 の確定台本（日本語・3行）。docs/Story/STORY_DIALOGUE_DRAFTS.md 由来。======
    // キー = "<bossId>|<heroId>"（小文字）。値 = [ボス挑発, 主人公の返し, ボス締め]。
    // 無い組み合わせ（中ボス・残り主人公）は従来の汎用文へフォールバックする。
    private static Dictionary<string, string[]> scriptedLines;

    private static string[] GetScriptedLines(string bossId, string heroId)
    {
        if (string.IsNullOrEmpty(bossId) || string.IsNullOrEmpty(heroId)) return null;
        if (scriptedLines == null) BuildScriptedLines();
        return scriptedLines.TryGetValue(bossId.ToLowerInvariant() + "|" + heroId.ToLowerInvariant(), out string[] v) ? v : null;
    }

    private static void BuildScriptedLines()
    {
        scriptedLines = new Dictionary<string, string[]>();
        void S(string boss, string hero, string l1, string l2, string l3) =>
            scriptedLines[(boss + "|" + hero).ToLowerInvariant()] = new[] { l1, l2, l3 };

        // Ch1 Caliber（後悔）
        S("Caliber", "HeroAldin", "アルディン……お前だけは来るなと願っていた。白門の音がまた止まり、リオラの小さな手が私の中で冷えていく。兄弟弟子よ、なぜ私にもう一度あの夜を見せる。", "兄弟子、俺もあの夜を忘れていない。忘れられるはずがない。だからこそ、リオラの名を眠りの理由にさせない。守れなかった痛みごと、あなたを連れ戻す。", "ならば剣を抜け、アルディン。私の盾を砕けぬ者に、あの子の名を抱えて進む資格はない。優しい言葉ではなく、守る力で私を否定してみせろ。");
        S("Caliber", "HeroKagachi", "小さき足音よ、白門へ近づくな。弱き者は願うほど傷つき、立ち上がるほど誰かを巻き込む。ならば眠らせる方が慈悲だ、せめて痛みを知らぬまま終われ。", "うるせえ、勝手に終わらせんな。弱いまま吠える奴も、転んで泥だらけで進む奴もいる。そういう奴らを笑うのは俺の役目じゃねえ、背中を押す方が性に合う。", "吠えるだけで門は越えられぬ。ならばその足で来い、カガチ。転ぶ者を通せば、また白門は血に濡れる。私の巨剣が、お前の強がりを砕いてやる。");
        S("Caliber", "HeroVesna", "異端の火よ、白門へ近づくな。炎は温める顔をして、風が変われば守る場所さえ焼き尽くす。私はもう、希望の名で燃え広がるものを信じられぬ。", "火を恐れる目は嫌いじゃない。焼け跡を知っている目だからね。でも私の火は、凍えた者が手を伸ばす場所にもなる。燃やすだけなら、とっくに捨てている。", "ならば示せ、ヴェスナ。その火が誰かの居場所になるのか、それともまた灰を増やすだけか。私の剣は、熱に惑わされず答えを斬り分ける。");
        S("Caliber", "HeroZiran", "癒やし手よ、傷は塞がっても夜は戻る。私は何度も包帯を替え、祈りを聞き、それでも白門の泣き声を止められなかった。眠りで閉じるしかない傷もある。", "傷が残っても、命は続きます。祈りが届かない夜もありますが、それでも手を離さず支えることはできます。終わらせる慈悲だけが、救いではありません。", "その穏やかな手で私を止めてみろ、ジラン。守れなかった痛みは、祈りひとつで退くほど柔らかくない。癒やしが剣に勝てるのか、ここで見せよ。");
        S("Caliber", "HeroReva", "自由な足なら逃げられるはずだ、レヴァ。なぜわざわざ白門の後悔へ戻る。ここは選択を誇る者ほど迷い、迷った者から斬られる門だ。", "逃げる自由も、踊る自由も、誰かの首根っこを掴んで戻る自由もあるのよ。今日は三つ目の気分。英雄さまが悪い夢を見ているなら、派手に叩き起こしてあげる。", "軽口で白門の夜を越えられると思うな。ならば選択ごと断つ、自由人よ。お前の刃が本当に誰かを起こすものか、私の巨剣で試してやる。");
        S("Caliber", "HeroKara", "カーラ、待っても戻らぬ者がいる。白門はそれを教えた。雪の下で名を呼び続けても、答えぬ声はある。だから今、門は閉じねばならない。", "戻らぬ者はいる。それでも、戻る者のために火を絶やさぬ場所が要る。私は長く待てる。凍る夜を知っているから、急いで終わりを選びはしない。", "その静かな灯りを消してやる。待つだけでは二度目の白門は守れない。お前の忍耐が慈悲より強いと言うなら、私の剣の前で折れずに立て。");
        S("Caliber", "HeroBrome", "ブローム、軍旗は白門で倒れた。励ましの声も、並んだ盾も、喪失の前では裂けていく。違う者を集めても、終わりの前では列は保てぬ。", "裂けても呼び合えるのが旗だ、キャリバー。恐れる者も、怒る者も、膝をつく者も、もう一度立つ目印になる。私はそのために声を張る。", "ならば旗を掲げて来い。私の巨剣が、その結束の重さを量る。空の言葉で兵は救えぬ、だが本物の号令なら、この鎧の奥へ届くかもしれん。");
        S("Caliber", "HeroShidai", "影よ、白門では見えぬ者から消えていった。名も残らず、支えた手も知られぬまま、ただ静かに折れていく。影であることに、何の救いがある。", "見えないまま終わるために影でいるんじゃない。光が進む道を、誰かが汚れた足で踏み固める必要がある。今日はその足で、英雄の前へ出る。", "ならば影ごと断つ、シダイ。名を求めぬ支えが、暴走した英雄を止められるか。白門の闇で折れなかった誇りを、私の剣に見せてみろ。");
        S("Caliber", "HeroIlena", "観測する者よ、白門の結末を見れば分かるはずだ。人は痛みを抱えれば同じ失敗へ戻る。ならば守れぬ未来を閉じ、眠りへ渡す方が誠実ではないか。", "観測した悲劇から、同じ結論を選ぶ義務はありません。私は失敗の反復ではなく、分岐の余白を見ます。あなたの痛みも、まだ確定した終末ではありません。", "ならばその余白を示せ、イリーナ。冷たい分析が慈悲を越えるのか、希望という不確定が巨剣を止めるのか。観測者の目で、私を否定してみろ。");
        // Ch2 Solfist（覚悟）
        S("Solfist", "HeroAldin", "守る覚悟はあるか。中途半端は、皆を道連れにするぞ。", "だから、最後の一人になるまで盾になる。", "いい目だ。試させてもらう。");
        S("Solfist", "HeroKagachi", "前へ進むだけでは、いつか折れる。それでも往くか。", "折れたら、折れたまま這ってでも進む。", "…面白い。来い。");
        S("Solfist", "HeroVesna", "己を信じられぬ炎は、味方をも焼くぞ。", "信じられないなら、信じてくれる奴の隣にいる。", "ならば、その輪を見せてみろ。");
        // Ch3 Dissonance（違和感）
        S("Dissonance", "HeroAldin", "セカイ、不要。マモル、無駄。…繰リ返ス、ダケ。", "繰り返すなら、何度でも守るまでだ。", "ロウヒ。終ワラセル。");
        S("Dissonance", "HeroKagachi", "ススム、無意味。同ジ場所ニ、戻ル。", "同じ場所でも、戻るたび俺は変わってる。", "ヘンカ…エラー。消去。");
        S("Dissonance", "HeroVesna", "イバショ、再構築不能。破棄、推奨。", "壊れても、また作る。何度でも。", "…理解、不能。排除。");
        // Ch4 Magmarvaath（本能）
        S("Magmarvaath", "HeroAldin", "弱きを庇うほど、共に燃えるぞ。", "燃やすなら、まず私を超えてみろ。", "いい炎だ。喰ってやる。");
        S("Magmarvaath", "HeroKagachi", "迷う獣は餌だ。本能で来い。", "考えるのも、進むためだ。本能だけのお前とは違う。", "吠えるな。焼くぞ。");
        S("Magmarvaath", "HeroVesna", "氷の癖に炎を抱くか。中途半端め。", "この炎は焼くためじゃない。…守るためだ。", "ならば、本物の炎を見せてやる。");
        // Ch5 Magmarstarhorn（闘争）
        S("Magmarstarhorn", "HeroAldin", "守りなど退屈だ。もっと激しく抗え！", "派手さはいらない。倒れない盾であればいい。", "ならば砕くまで！");
        S("Magmarstarhorn", "HeroKagachi", "いい面構えだ。闘争こそ生きる証よ。", "同感だ。だが俺は、勝って前へ進むためにやる。", "来い、嵐に乗れ！");
        S("Magmarstarhorn", "HeroVesna", "群れずに立てるか、異端の炎。", "一人じゃない。背中を預ける場所がある。", "では、その絆ごと吹き飛ばす！");
        // Ch6 Magmarragnora（群れ）
        S("Magmarragnora", "HeroAldin", "殺しても湧く。守っても減らぬ。世界は、変われぬのだ。", "変われないなら、変わるまで守り続ける。", "ならば、群れに沈め。");
        S("Magmarragnora", "HeroKagachi", "一匹倒しても無駄。終わりは来ぬ。", "終わらないなら、終わらせない俺がいるだけだ。", "飲み込め、群れよ。");
        S("Magmarragnora", "HeroVesna", "数の前で、お前の居場所など砂粒だ。", "砂粒でも、寄り集まれば火床になる。", "燃え尽きるがいい。");
        // Ch7 Abyssallilithe（別れ）
        S("Abyssallilithe", "HeroAldin", "守った者とも、いつか別れる。なら最初から手放せば？", "別れが来るからこそ、今を守る価値がある。", "…その温もり、影へ還してあげる。");
        S("Abyssallilithe", "HeroKagachi", "進んだ先に待つのは、別れだけよ。", "別れも背負って進む。それが前に進むってことだ。", "強がりね。…試しましょう。");
        S("Abyssallilithe", "HeroVesna", "居場所を作っても、いつか失う。寂しくない？", "失っても、また誰かと作る。だから怖くない。", "…うらやましい。だから、奪うわ。");
        // Ch8 Abyssalcassyva（生・カガチ犬化）
        S("Abyssalcassyva", "HeroKagachi", "ふふ、いい目。…でも、四つ足のほうが似合うかも？", "っ…この姿でも、進むのはやめねえ。", "あはは、それでこそ。生きてるって、そういうことよ。");
        S("Abyssalcassyva", "HeroAldin", "守るのって、しんどいでしょ。でも…やめられない。それが生きてるってこと。", "ああ。だから、手放さない。", "いいわ、その生き汚さ。遊びましょ。");
        S("Abyssalcassyva", "HeroVesna", "居場所探し？ 生きてる証拠じゃない。嫌いじゃないわ。", "……あなた、終わりを望む側じゃ、ないのか。", "さあ？ でも、退屈よりはずっとマシでしょ。");
        // Ch9 Abyssalmaehv（救済）
        S("Abyssalmaehv", "HeroAldin", "もう、楽になっていいのですよ。死は、やさしい救済です。", "救いを語るその声…誰かに、似ている。", "……気づきましたか。なら、なおさら眠らせて差し上げます。");
        S("Abyssalmaehv", "HeroKagachi", "苦しいでしょう。救って差し上げます。", "苦しいから、生きてるんだ。余計なお世話だ。", "強情な。…では、深淵でお休みなさい。");
        S("Abyssalmaehv", "HeroVesna", "居場所など、深淵に還ればもう要らない。", "還らない。作った場所で、生きていく。", "その熱も、いずれ冷めます。");
        // Ch10 Vetruvianzirix（管理）
        S("Vetruvianzirix", "HeroAldin", "守りは非効率。変化は誤差。すべて管理下に収める。", "その誤差の中に、守るべき命がある。", "ならば、誤差ごと修正する。");
        S("Vetruvianzirix", "HeroKagachi", "前進も設計の内。お前の道は計算済みだ。", "計算外を見せてやる。それが前に進むってことだ。", "…逸脱値。排除する。");
        S("Vetruvianzirix", "HeroVesna", "居場所は割り当てるもの。お前が作るものではない。", "割り当てられた檻なんて、居場所じゃない。", "規格外め。再設計する。");
        // Ch11 Vetruviansajj（諦め）
        S("Vetruviansajj", "HeroAldin", "無駄だ。あの方も、愛したからこそ諦めた。お前も同じになる。", "愛したのなら、諦めない理由になる。", "…まぶしいな。だが、灼く。");
        S("Vetruviansajj", "HeroKagachi", "進んだ果ても、結局この砂だ。諦めろ。", "諦めるかは、進んでから決める。", "強情な火だ。乾かしてやる。");
        S("Vetruviansajj", "HeroVesna", "居場所も、いずれ砂に還る。あの方もそう知って、疲れた。", "…彼女は、世界を、愛してたのか。", "もう遅い。灼け尽きろ。");
        // Ch12 Vetruvianscion（運命）
        S("Vetruvianscion", "HeroAldin", "定められた終わりだ。観測者は、ただ見届ける。", "運命でも、守る手は止めない。", "では、その定めを舞で飾ろう。");
        S("Vetruvianscion", "HeroKagachi", "お前の道も、すでに砂に描かれている。", "描かれた線の上でも、走り方は俺が決める。", "舞え。終幕だ。");
        S("Vetruvianscion", "HeroVesna", "観測者は、もう結末を視ている。覆せはしない。", "結末を視てるなら…まだ、見届けたい何かが、あるんだろ。", "……砂塵に問うか。いいだろう。");
        // Ch13 Arcana（希望・主人公の答え）
        S("Arcana", "HeroAldin", "まだ歩くのですね。…守れぬものを抱えて、疲れませんか。", "守れなくても、守り続ける。それしか、できないから。", "…ずっと、その姿を観てきました。もう、休んでも、いいのですよ。");
        S("Arcana", "HeroKagachi", "何度倒れても、立つのですね。…もう、休んでもいいのですよ。", "弱くても、進める。それを証明しに来た。", "……その不格好な歩みを、もう一度だけ、観てみましょうか。");
        S("Arcana", "HeroVesna", "あなたの炎は、いつも独りでしたね。…もう、消えても、いいのですよ。", "独りじゃない。居場所は、作れるって知ったから。", "……温もり。久しく、観ていませんでした。見せてください、その続きを。");
        // 残り6主人公×20ボス ＋ 既存3主人公×新ボス9体（戦前・GPT/Codex確定稿、各3行 偶数=ボス/奇数=主人公）。
        scriptedLines["caliber|heroziran"] = new[]{ "癒やし手よ、傷は塞がっても夜は戻る。私は何度も包帯を替え、祈りを聞き、それでも白門の泣き声を止められなかった。眠りで閉じるしかない傷もある。", "傷が残っても、命は続きます。祈りが届かない夜もありますが、それでも手を離さず支えることはできます。終わらせる慈悲だけが、救いではありません。", "その穏やかな手で私を止めてみろ、ジラン。守れなかった痛みは、祈りひとつで退くほど柔らかくない。癒やしが剣に勝てるのか、ここで見せよ。" };
        scriptedLines["neutral_rook|heroziran"] = new[]{ "止まれ。動けば傷は開く。眠る門なら、苦しむ命も静かになる。", "傷が開いても支えます。静けさだけを救いとは呼びません。", "ならば手を止める。石は癒しの迷いを通さぬ。" };
        scriptedLines["neutral_sister|heroziran"] = new[]{ "苦しむ命は眠りへ捧げましょう。それが最もやさしい救いです。", "眠らせる前に、支えられる夜があります。私はその夜を見捨てません。", "ならば祈りの前で膝をつきなさい。" };
        scriptedLines["magmarvaath|heroziran"] = new[]{ "癒やしの手など、炎の前ではよく燃える枝だ。", "枝でも、誰かを支える杖になります。燃やさせはしません。", "吠える手だ。ならば灰まで焼いてやる。" };
        scriptedLines["magmarstarhorn|heroziran"] = new[]{ "戦場で命を拾うか。嵐の中で、何人抱えられる？", "抱えられるだけ抱えます。置き去りにするための手ではありません。", "よし、その覚悟ごと吹き飛ばしてやろう。" };
        scriptedLines["magmarragnora|heroziran"] = new[]{ "群れは数で押し潰す。お前の手では、救える命などわずかだ。", "わずかでも救います。数で命の重さは決めません。", "ならば群れに沈め。拾える声が残るか見てやる。" };
        scriptedLines["abyssallilithe|heroziran"] = new[]{ "どれほど癒やしても、最後には別れるわ。疲れないの？", "疲れます。それでも、別れまで独りにしない手があります。", "優しいのね。だから影へ招きたくなる。" };
        scriptedLines["abyssalcassyva|heroziran"] = new[]{ "苦しむ命に付き合うなんて物好きね。生って、そんなに面倒よ？", "面倒でも、苦しみごと生きる命を支えます。", "ふふ、いいわ。あなたの手がどこまで温かいか見せて。" };
        scriptedLines["abyssalmaehv|heroziran"] = new[]{ "もう休ませてあげましょう。苦しみを終えることも救いです。", "終える前に支えられる命があります。私は眠りだけを救いとは呼びません。", "では、その祈りを深淵で試しましょう。" };
        scriptedLines["vetruvianzirix|heroziran"] = new[]{ "苦痛個体の継続支援は非効率。停止処理を推奨。", "非効率でも、命は切り捨てません。支えることに意味があります。", "非合理な癒し手。修正する。" };
        scriptedLines["vetruviansajj|heroziran"] = new[]{ "苦しむ命を見続ければ、いずれ諦める。あの方もそうだった。", "見続けます。諦めるためではなく、支えるために。", "その眼差し、砂で灼いて伏せさせる。" };
        scriptedLines["vetruvianscion|heroziran"] = new[]{ "苦しみも別れも、定めの織り目だ。抗っても布は変わらぬ。", "織り目は変わらなくても、触れる手の温度は変えられます。", "ならば舞え。運命の余白に咲くか見よう。" };
        scriptedLines["neutral_mechaz0rwing|heroziran"] = new[]{ "翼部起動。苦痛個体は飛行効率を低下させる。支援は不要。", "落ちる命を置いて飛ぶなら、その翼は救いではありません。", "支援思想を異常値として記録。排除する。" };
        scriptedLines["neutral_mechaz0rsword|heroziran"] = new[]{ "剣部起動。苦痛の根源を切断すれば、救済は完了する。", "命ごと切る救済は受け入れません。続く命を支えます。", "断罪処理開始。抵抗する手を切除する。" };
        scriptedLines["neutral_mechaz0rsuper|heroziran"] = new[]{ "完全体起動。完全な沈黙こそ苦痛なき秩序である。", "苦しみのない沈黙より、苦しみごと続く命を選びます。", "不完全な生命賛歌を検出。機械神が停止させる。" };
        scriptedLines["neutral_mechaz0rhelm|heroziran"] = new[]{ "兜部起動。思考停止命令により、苦痛対象の救済を開始。", "命を止める命令には従いません。私は支える側に立ちます。", "命令違反を確認。沈黙へ移行させる。" };
        scriptedLines["neutral_mechaz0rchassis|heroziran"] = new[]{ "胴部起動。個別苦痛は量産処理に適さない。標準停止へ移行。", "標準で命を測りません。一人ずつ、苦しみごと支えます。", "個別支援は規格外。圧壊する。" };
        scriptedLines["neutral_mechaz0rcannon|heroziran"] = new[]{ "砲部起動。全苦痛領域を殲滅し、救済完了とする。", "苦しみを消すために命を消すなら、それは救いではありません。", "終末砲、照準。支援倫理を焼却する。" };
        scriptedLines["neutral_hydrax|heroziran"] = new[]{ "痛みは増える。首を落としても、また別の喉が哭く。", "増える痛みでも、一つずつ支えます。命ごと投げ出しはしません。", "ならば支えてみろ。増える傷口が、お前の手を呑む。" };
        scriptedLines["arcana|heroziran"] = new[]{ "苦しむ命に寄り添い続けるのですね。疲れませんか。もう休ませてもいいのですよ。", "苦しみを消すだけが救いではありません。生きる命を、苦しみごと支えます。", "そうですか。では、その手がどこまで届くのか、観せてください。" };
        scriptedLines["caliber|heroreva"] = new[]{ "自由な足なら逃げられるはずだ、レヴァ。なぜわざわざ白門の後悔へ戻る。ここは選択を誇る者ほど迷い、迷った者から斬られる門だ。", "逃げる自由も、踊る自由も、誰かの首根っこを掴んで戻る自由もあるのよ。今日は三つ目の気分。英雄さまが悪い夢を見ているなら、派手に叩き起こしてあげる。", "軽口で白門の夜を越えられると思うな。ならば選択ごと断つ、自由人よ。お前の刃が本当に誰かを起こすものか、私の巨剣で試してやる。" };
        scriptedLines["neutral_rook|heroreva"] = new[]{ "止まれ。自由な足ほど、迷いを増やす。ここで道を一つに閉じろ。", "迷えるから自由なんだよ。進むか残るかは、私が決める。", "ならば道を消す。門は選択を許さぬ。" };
        scriptedLines["neutral_sister|heroreva"] = new[]{ "迷う自由など苦しみです。すべてを捧げれば、選ばずに済みます。", "選べる苦しさまで、私のものだよ。捧げる先は自分で決める。", "その我執を祈りで縛りましょう。" };
        scriptedLines["magmarvaath|heroreva"] = new[]{ "逃げ足だけの獲物か。風より先に炎が回るぞ。", "逃げるか戻るかは私が決める。追いつけるなら追ってみな。", "いい挑発だ。焼けた羽根の匂いを嗅がせろ。" };
        scriptedLines["magmarstarhorn|heroreva"] = new[]{ "風の射手よ、闘争の嵐に留まれるか。", "留まるのも飛び去るのも私の自由。今日はここで撃つ。", "ならば嵐に刻め。その矢が折れるまで。" };
        scriptedLines["magmarragnora|heroreva"] = new[]{ "群れに従えば迷わぬ。自由な足は、やがて孤独に食われる。", "誰と並ぶかも自分で選ぶ。群れに飲まれるつもりはないよ。", "ならば群れよ、自由な獲物を囲め。" };
        scriptedLines["abyssallilithe|heroreva"] = new[]{ "別れが怖いなら、先に離れればいい。あなたにはその自由があるわ。", "残る自由もある。今日は背を向けずに選ぶ。", "強がりね。その選択を影で試しましょう。" };
        scriptedLines["abyssalcassyva|heroreva"] = new[]{ "逃げてもいいのに残るんだ。痛いのも怖いのも、自分で選ぶの？", "そう。選んだ痛みなら、ちゃんと私のものだよ。", "あは、いい鼓動。じゃあその自由、遊んであげる。" };
        scriptedLines["abyssalmaehv|heroreva"] = new[]{ "選ぶ苦しみから解かれましょう。眠れば、もう迷わずに済みます。", "迷えるから自由なんだよ。眠らされるくらいなら、傷ついても選ぶ。", "では、その翼を深淵で畳みましょう。" };
        scriptedLines["vetruvianzirix|heroreva"] = new[]{ "自由選択は分岐過多。最適経路へ固定する。", "最適って誰の都合？ 私の道は私が選ぶ。", "未確定分岐を排除。統制開始。" };
        scriptedLines["vetruviansajj|heroreva"] = new[]{ "逃げれば灼かれずに済む。残る選択など、いずれ重荷になる。", "重くなったらまた選ぶ。ここに残るのも私の自由だよ。", "その自由、砂ごと灼き払う。" };
        scriptedLines["vetruvianscion|heroreva"] = new[]{ "お前の矢筋も、すでに舞の一節に見えている。", "見えてても、撃つと決めたのは私だ。そこは譲らない。", "ならば放て。定めを外す矢か見届けよう。" };
        scriptedLines["neutral_mechaz0rwing|heroreva"] = new[]{ "翼部起動。自由飛行は危険。全軌道を固定する。", "翼があるからって、決められた空を飛ぶ気はないよ。", "自由軌道を異常値認定。拘束する。" };
        scriptedLines["neutral_mechaz0rsword|heroreva"] = new[]{ "剣部起動。自由選択は非効率。最短断罪線へ誘導。", "最短ルートなんて興味ない。私が選んだ軌道で撃つ。", "軌道逸脱を確認。切断する。" };
        scriptedLines["neutral_mechaz0rsuper|heroreva"] = new[]{ "完全体起動。全自由分岐を統一し、神性秩序へ接続する。", "完全に決められた道なんて退屈。私は未確定の空がいい。", "未確定要素を神格演算から除去する。" };
        scriptedLines["neutral_mechaz0rhelm|heroreva"] = new[]{ "兜部起動。自由思考を停止。命令に従え。", "命令されて飛ぶのは自由じゃない。遠慮するよ。", "服従拒否を検出。思考封鎖を開始。" };
        scriptedLines["neutral_mechaz0rchassis|heroreva"] = new[]{ "胴部起動。全個体を同一進路へ量産配列。", "同じ道を走る気はない。私は私の場所を選ぶ。", "配列外個体を捕捉。再規格化する。" };
        scriptedLines["neutral_mechaz0rcannon|heroreva"] = new[]{ "砲部起動。自由分岐を殲滅し、終末線へ収束させる。", "狙われた線上に立つかどうかも、私が決める。", "終末照準、補正。自由を焼却する。" };
        scriptedLines["neutral_hydrax|heroreva"] = new[]{ "逃げても首は増える。水面すべてが牙になる。", "だったら水面ごと飛び越える。どこを走るかは私の自由。", "飛べるなら飛べ。増える首が、空まで追う。" };
        scriptedLines["arcana|heroreva"] = new[]{ "どこへでも行けるあなたが、まだここに残るのですね。もう、逃げてもいいのですよ。", "自由は逃げ道じゃない。ここに残って戦うのも、私が選んだ自由だよ。", "そうですか。では、その選択の先を観せてください。" };
        scriptedLines["caliber|herokara"] = new[]{ "カーラ、待っても戻らぬ者がいる。白門はそれを教えた。雪の下で名を呼び続けても、答えぬ声はある。だから今、門は閉じねばならない。", "戻らぬ者はいる。それでも、戻る者のために火を絶やさぬ場所が要る。私は長く待てる。凍る夜を知っているから、急いで終わりを選びはしない。", "その静かな灯りを消してやる。待つだけでは二度目の白門は守れない。お前の忍耐が慈悲より強いと言うなら、私の剣の前で折れずに立て。" };
        scriptedLines["neutral_rook|herokara"] = new[]{ "止まれ。待つ者なら分かるはずだ。動かぬことも守りになる。", "分かります。でも私の待つは、戻る道を残すためで、閉じるためじゃない。", "ならば待つ場ごと塞ぐ。石は帰路も進路も同じく閉ざす。" };
        scriptedLines["neutral_sister|herokara"] = new[]{ "変われぬ者は、祈りへ身を委ねればいいのです。", "すぐ変われなくてもいい。支えながら待つ時間にも意味があります。", "その遅さを、献身で正してあげましょう。" };
        scriptedLines["magmarvaath|herokara"] = new[]{ "動かぬ氷は燃えやすい。待っている間に灰になるぞ。", "燃えやすくても、支える場所は簡単に動かさない。", "ならばその芯まで焼いてやる。" };
        scriptedLines["magmarstarhorn|herokara"] = new[]{ "戦場で待つか。嵐は遅い者を置き去りにするぞ。", "置き去りにされる誰かのために待つ。急がない強さもあります。", "面白い。静かな闘争を見せてみろ。" };
        scriptedLines["magmarragnora|herokara"] = new[]{ "群れは待たぬ。遅れた者は飲まれるだけだ。", "だから私は待つ。遅れた人が戻れる場所を残します。", "群れの外の巣か。踏み潰してやろう。" };
        scriptedLines["abyssallilithe|herokara"] = new[]{ "待っても帰らない人はいるわ。寂しさに耐えられる？", "寂しくても灯りは消しません。帰れる人まで迷わせたくないから。", "優しい影ね。だから奪いたくなる。" };
        scriptedLines["abyssalcassyva|herokara"] = new[]{ "待つのって退屈で痛いでしょ。生き物は待つのが下手なのに。", "下手でも待ちます。待たれた記憶が、誰かを戻すこともあります。", "ふふ、いいわ。その退屈ごと遊びましょ。" };
        scriptedLines["abyssalmaehv|herokara"] = new[]{ "待つ痛みから解かれましょう。眠れば、戻らぬ足音を聞かずに済みます。", "それでも腕を空けて待ちます。戻れる命を急かしたくありません。", "では、その腕ごと深淵へ沈めましょう。" };
        scriptedLines["vetruvianzirix|herokara"] = new[]{ "待機行動、非効率。即時変化を命令する。", "すぐ変われなくてもいい。支える時間が必要な時もあります。", "遅延個体を修正する。" };
        scriptedLines["vetruviansajj|herokara"] = new[]{ "待っても砂は足跡を消す。記憶もやがて疲れる。", "疲れたら休みます。でも、忘れるためには待ちません。", "その灯りを砂で覆う。" };
        scriptedLines["vetruvianscion|herokara"] = new[]{ "待つ者の結末は、遅れて届く悲しみだ。", "遅れても、届く前に支えられるものがあります。", "ならば遅い舞を見せよ。" };
        scriptedLines["neutral_mechaz0rwing|herokara"] = new[]{ "翼部起動。静止個体は自由飛行の障害。排除する。", "飛べない時に戻る場所が必要です。待つことも支えです。", "待機支援を異常値認定。切除する。" };
        scriptedLines["neutral_mechaz0rsword|herokara"] = new[]{ "剣部起動。遅延行動は断罪効率を阻害する。", "急がないことが、誰かを守る時間になることもあります。", "遅延防護を切断する。" };
        scriptedLines["neutral_mechaz0rsuper|herokara"] = new[]{ "完全体起動。未完成状態を許容しない。即時統合せよ。", "未完成でもいい。変われない時間を支えることも必要です。", "不完全許容を神格秩序から除去する。" };
        scriptedLines["neutral_mechaz0rhelm|herokara"] = new[]{ "兜部起動。待機命令は不要。即応せよ。", "命令で急かされても、支えるべき時には待ちます。", "即応拒否を確認。思考を停止する。" };
        scriptedLines["neutral_mechaz0rchassis|herokara"] = new[]{ "胴部起動。遅い個体は量産サイクルから除外。", "遅くても、支える役目はあります。速さだけで測らないで。", "非同期個体を圧壊する。" };
        scriptedLines["neutral_mechaz0rcannon|herokara"] = new[]{ "砲部起動。終末処理は即時完了を要求する。", "急いで終わらせる力には、急がず支える力で向き合います。", "終末砲、充填。待機地点を消滅させる。" };
        scriptedLines["neutral_hydrax|herokara"] = new[]{ "待つ間にも首は増える。戻る道にも牙が生えるぞ。", "それでも待ちます。帰る場所を消さないために。", "ならば深みに沈め。待つ灯りごと噛み砕く。" };
        scriptedLines["arcana|herokara"] = new[]{ "変われないものを待ち続けるのですね。もう、急がなくても休んでいいのですよ。", "すぐ変われなくてもいい。待つことも、支えることも、守ることです。", "そうですか。では、その静かな強さを観せてください。" };
        scriptedLines["caliber|herobrome"] = new[]{ "ブローム、軍旗は白門で倒れた。励ましの声も、並んだ盾も、喪失の前では裂けていく。違う者を集めても、終わりの前では列は保てぬ。", "裂けても呼び合えるのが旗だ、キャリバー。恐れる者も、怒る者も、膝をつく者も、もう一度立つ目印になる。私はそのために声を張る。", "ならば旗を掲げて来い。私の巨剣が、その結束の重さを量る。空の言葉で兵は救えぬ、だが本物の号令なら、この鎧の奥へ届くかもしれん。" };
        scriptedLines["neutral_rook|herobrome"] = new[]{ "止まれ。軍列は門前で崩れる。違う足並みは、石を越えられぬ。", "違う足並みだから支え合える。全員で押せば、門も道になる。", "ならば軍ごと受け止める。石は数にも声にも揺れぬ。" };
        scriptedLines["neutral_sister|herobrome"] = new[]{ "一つの祈りだけが人を迷わせません。違う声は争いを生みます。", "違う声だからこそ届く場所がある。軍旗は黙らせるためにない。", "ならば祈りで、その騒がしい旗を伏せましょう。" };
        scriptedLines["magmarvaath|herobrome"] = new[]{ "群れて吠える軍か。炎で散らせば、すぐ獣に戻る。", "これは群れではない。違う者たちが同じ旗を選んだ軍だ。", "ならば旗ごと喰い破る。" };
        scriptedLines["magmarstarhorn|herobrome"] = new[]{ "いい軍列だ。闘争の嵐で、どれだけ声を揃えられる？", "揃わない声だから強い。互いの穴を埋めて進む。", "よし、その軍歌を嵐で試そう。" };
        scriptedLines["magmarragnora|herobrome"] = new[]{ "我らの群れに、軍旗で挑むか。数では飲まれるぞ。", "数だけなら群れだ。意志を束ねるから軍になる。", "ならば群れよ、その意志を腹へ沈めよ。" };
        scriptedLines["abyssallilithe|herobrome"] = new[]{ "旗の下に集めても、別れは列を裂くわ。", "裂かれても呼び合える。声が残る限り、軍は終わらない。", "その声を影へ還してあげる。" };
        scriptedLines["abyssalcassyva|herobrome"] = new[]{ "いろんな命を束ねるなんて、騒がしくて面倒ね。", "騒がしいから生きている。静かな終わりには渡さない。", "あは、いいわ。その騒音で踊ってみせて。" };
        scriptedLines["abyssalmaehv|herobrome"] = new[]{ "争う声を眠らせれば、皆が静かになります。", "静かでなくていい。違う声を聞き合うために旗がある。", "では、その旗を深淵の眠りへ。" };
        scriptedLines["vetruvianzirix|herobrome"] = new[]{ "異種混成軍、統制不能。単一規格へ修正する。", "同じにする必要はない。違うまま並ぶから強い。", "不統一構成を再設計する。" };
        scriptedLines["vetruviansajj|herobrome"] = new[]{ "軍旗も最後は砂に倒れる。継ぐ者など、いずれ疲れる。", "倒れても誰かが拾う。そのために、私は旗を掲げる。", "その布を砂で灼く。" };
        scriptedLines["vetruvianscion|herobrome"] = new[]{ "それぞれの定めは、一本の旗では束ねきれぬ。", "束ねきれなくていい。絡み合う糸ごと前へ進む。", "ならば運命の舞で、その旗を裂こう。" };
        scriptedLines["neutral_mechaz0rwing|herobrome"] = new[]{ "翼部起動。不揃い編隊は飛行効率を低下させる。", "同じ翼で飛ばずともよい。同じ旗を見れば進路は合う。", "不統一編隊を墜落させる。" };
        scriptedLines["neutral_mechaz0rsword|herobrome"] = new[]{ "剣部起動。軍列を分断し、効率的に断罪する。", "切られても結び直す。軍旗は一か所だけに立たない。", "分断耐性を検出。さらなる切断を実行。" };
        scriptedLines["neutral_mechaz0rsuper|herobrome"] = new[]{ "完全体起動。単一神格の前で、不統一な軍は崩れる。", "一つになるとは、同じになることではない。", "不一致結束を破壊し、完全性へ統合する。" };
        scriptedLines["neutral_mechaz0rhelm|herobrome"] = new[]{ "兜部起動。複数意思は指揮系統を乱す。命令を一元化する。", "軍は命令だけで動かない。違う者が選んで並ぶのだ。", "多声指揮を停止する。" };
        scriptedLines["neutral_mechaz0rchassis|herobrome"] = new[]{ "胴部起動。異種構成は量産不能。規格へ変換する。", "部品では軍にならない。違う者の意志が必要だ。", "非量産軍を圧壊する。" };
        scriptedLines["neutral_mechaz0rcannon|herobrome"] = new[]{ "砲部起動。混成軍を範囲殲滅し、旗を焼却する。", "旗は布だけではない。皆の胸にある限り、砲では折れない。", "終末砲、照準。結束を焼き払う。" };
        scriptedLines["neutral_hydrax|herobrome"] = new[]{ "首は増える。旗の下に集めるほど、噛まれる数も増える。", "それでも集う。違う者同士でも、一つになれるからだ。", "ならば群れより深い腹で、その旗を呑む。" };
        scriptedLines["arcana|herobrome"] = new[]{ "違う声を束ね続けるのですね。争いに疲れた世界を、もう休ませてもいいのですよ。", "違う者でも、一つになれる。揃わない声だからこそ遠くまで届く。", "そうですか。では、その軍旗の先を観せてください。" };
        scriptedLines["caliber|heroshidai"] = new[]{ "影よ、白門では見えぬ者から消えていった。名も残らず、支えた手も知られぬまま、ただ静かに折れていく。影であることに、何の救いがある。", "見えないまま終わるために影でいるんじゃない。光が進む道を、誰かが汚れた足で踏み固める必要がある。今日はその足で、英雄の前へ出る。", "ならば影ごと断つ、シダイ。名を求めぬ支えが、暴走した英雄を止められるか。白門の闇で折れなかった誇りを、私の剣に見せてみろ。" };
        scriptedLines["neutral_rook|heroshidai"] = new[]{ "止まれ。影は門の外に残る。名なき足音は、石に刻まれぬ。", "刻まれなくていい。外に残る影があるから、進める光もある。", "ならば影を潰す。門は見えぬ支えを認めぬ。" };
        scriptedLines["neutral_sister|heroshidai"] = new[]{ "影に捧げる名などありません。祈りの外で消えなさい。", "名を呼ばれなくても、支えた命は消えない。", "その無名の献身を、終焉へ捧げます。" };
        scriptedLines["magmarvaath|heroshidai"] = new[]{ "隠れる獲物か。炎は影ごと舐めるぞ。", "影は逃げるためだけにあるんじゃない。光を守るために動く。", "ならば光ごと焼いて、影を剥がす。" };
        scriptedLines["magmarstarhorn|heroshidai"] = new[]{ "名乗らぬ影にも熱はあるか。戦場で見せてみろ。", "名乗るより先に届くべき場所がある。それだけだ。", "いい静けさだ。嵐で暴いてやる。" };
        scriptedLines["magmarragnora|heroshidai"] = new[]{ "群れの中では影の名など潰える。", "潰されないために散る。届かない隙間へ入る。", "群れよ、見えぬ絆を食い裂け。" };
        scriptedLines["abyssallilithe|heroshidai"] = new[]{ "影はいつも見送る側。寂しくないの？", "寂しくても、戻る道を覚えている。それが影の役目だ。", "なら、その道ごと影へ閉じましょう。" };
        scriptedLines["abyssalcassyva|heroshidai"] = new[]{ "影まで息をしてる。隠しても、生きてる匂いはするわよ。", "息を殺すのは死ぬためじゃない。守るためだ。", "ふふ、いいわ。生きてる影と遊びましょ。" };
        scriptedLines["abyssalmaehv|heroshidai"] = new[]{ "影は眠れば、名を求めずに済みます。", "名を求めるのは、生きているからだ。眠るためじゃない。", "では、その寂しさを深淵へ。" };
        scriptedLines["vetruvianzirix|heroshidai"] = new[]{ "非表示要素は管理外。影を検出次第、削除する。", "見えないものにも役目はある。名前もな。", "不可視変数を排除する。" };
        scriptedLines["vetruviansajj|heroshidai"] = new[]{ "影の働きも砂に残らぬ。誰も覚えない。", "覚えられなくても、守られた背中は前へ進む。", "その名なき足跡を灼く。" };
        scriptedLines["vetruvianscion|heroshidai"] = new[]{ "影の糸は、舞台の下で終わる定め。", "下で支える糸が切れなければ、光は倒れない。", "ならば終幕の床ごと舞わせよう。" };
        scriptedLines["neutral_mechaz0rwing|heroshidai"] = new[]{ "翼部起動。不可視支援は飛行規格外。検出不能要素を排除。", "影は飛ばなくても支えられる。見えない道にも名はある。", "規格外支援を切除する。" };
        scriptedLines["neutral_mechaz0rsword|heroshidai"] = new[]{ "剣部起動。不可視対象を断罪する。", "見えないから届く場所がある。刃ではそこを断てない。", "影域ごと切断する。" };
        scriptedLines["neutral_mechaz0rsuper|heroshidai"] = new[]{ "完全体起動。神格観測外の影は存在を許可されない。", "見えなくても存在する。光を支える影にも名がある。", "不可視名を神格秩序から消去する。" };
        scriptedLines["neutral_mechaz0rhelm|heroshidai"] = new[]{ "兜部起動。影の判断を停止。命令対象外は削除。", "命令の外にいるから、支えられる時がある。", "思考外要素を封鎖する。" };
        scriptedLines["neutral_mechaz0rchassis|heroshidai"] = new[]{ "胴部起動。不可視経路は量産防壁に対する脅威。", "見えない道にも役目がある。守るためならそこを通る。", "影経路を圧壊する。" };
        scriptedLines["neutral_mechaz0rcannon|heroshidai"] = new[]{ "砲部起動。影域を含む全範囲を殲滅。", "影ごと撃つなら、照準の外から支えるだけだ。", "終末砲、全影域へ拡散照準。" };
        scriptedLines["neutral_hydrax|heroshidai"] = new[]{ "影の中にも首は増える。隠れる場所などない。", "隠れるためじゃない。守るために影を使う。", "ならば影ごと噛み砕く。" };
        scriptedLines["arcana|heroshidai"] = new[]{ "見えないまま支え続けるのですね。もう、その役目を下ろしてもいいのですよ。", "光を支える影にも名がある。次を守るために、まだ下ろさない。", "そうですか。では、その影の名を観せてください。" };
        scriptedLines["caliber|heroilena"] = new[]{ "観測する者よ、白門の結末を見れば分かるはずだ。人は痛みを抱えれば同じ失敗へ戻る。ならば守れぬ未来を閉じ、眠りへ渡す方が誠実ではないか。", "観測した悲劇から、同じ結論を選ぶ義務はありません。私は失敗の反復ではなく、分岐の余白を見ます。あなたの痛みも、まだ確定した終末ではありません。", "ならばその余白を示せ、イリーナ。冷たい分析が慈悲を越えるのか、希望という不確定が巨剣を止めるのか。観測者の目で、私を否定してみろ。" };
        scriptedLines["neutral_rook|heroilena"] = new[]{ "止まれ。観測は門前で足りる。先を見れば、悲劇も増える。", "観測は停止ではありません。見たうえで、開ける手もあります。", "ならば手を石にする。門は余白を閉じる。" };
        scriptedLines["neutral_sister|heroilena"] = new[]{ "観測者の結論は確かです。終焉を祈りとして受け入れなさい。", "確かなものを見ても、人は違う未来を選べます。", "ならば祈りで、その選択を眠らせます。" };
        scriptedLines["magmarvaath|heroilena"] = new[]{ "観るだけの目で炎を止めるか。恐れも見えているだろう。", "見えています。それでも、怖さだけで未来は決めません。", "ならばその目を焼いてやる。" };
        scriptedLines["magmarstarhorn|heroilena"] = new[]{ "結末を読んでなお、嵐に踏み込むか。", "読めた結末だけが未来ではありません。書き換える余地はあります。", "よし、その余地を嵐で試そう。" };
        scriptedLines["magmarragnora|heroilena"] = new[]{ "観測すれば分かる。群れはいつも個を飲む。", "分かっても、飲まれない選択はできます。", "ならば群れよ、その選択を呑み込め。" };
        scriptedLines["abyssallilithe|heroilena"] = new[]{ "観測者は、別れの数だけ冷えていくわ。", "冷えても、結論まで凍らせる必要はありません。", "その温度を影で奪ってあげる。" };
        scriptedLines["abyssalcassyva|heroilena"] = new[]{ "観測して、分析して、それでも生に賭けるの？", "はい。数字の外で、命は何度も揺れます。", "ふふ、その揺れを見せて。" };
        scriptedLines["abyssalmaehv|heroilena"] = new[]{ "観測者の疲れを、あなたも知っているはずです。眠りを受け入れなさい。", "知っています。だからこそ、眠りだけを答えにしません。", "では、同じ夜へ沈みましょう。" };
        scriptedLines["vetruvianzirix|heroilena"] = new[]{ "観測結果から希望を選択。論理矛盾。", "矛盾ではありません。観測は命令ではない。", "逸脱知性を修正する。" };
        scriptedLines["vetruviansajj|heroilena"] = new[]{ "観測すれば、諦めに至る。あの方のように。", "同じものを観ても、同じ結論にはしません。", "その違いを砂で灼く。" };
        scriptedLines["vetruvianscion|heroilena"] = new[]{ "観測した結末を、なお疑うのか。", "疑うのではありません。続きを選ぶのです。", "ならば終幕の先へ、一行を書いてみせよ。" };
        scriptedLines["neutral_mechaz0rwing|heroilena"] = new[]{ "翼部起動。観測結果を自由分岐へ接続する行為を禁止。", "観測したからこそ、別の空を選べます。", "自由分岐を固定する。" };
        scriptedLines["neutral_mechaz0rsword|heroilena"] = new[]{ "剣部起動。観測された罪状を断罪する。", "悲劇の記録は罪状ではありません。そこから何を選ぶかです。", "選択余地を切断する。" };
        scriptedLines["neutral_mechaz0rsuper|heroilena"] = new[]{ "完全体起動。全悲劇観測後の唯一結論は終端である。", "同じ悲劇を観ても、私は希望を選びます。", "希望選択を神格演算から消去する。" };
        scriptedLines["neutral_mechaz0rhelm|heroilena"] = new[]{ "兜部起動。観測知性の思考を停止。結論へ服従せよ。", "結論に服従するために観測しているのではありません。", "非服従知性を封鎖する。" };
        scriptedLines["neutral_mechaz0rchassis|heroilena"] = new[]{ "胴部起動。反復悲劇から反復結論を量産する。", "悲劇が繰り返されても、答えまで量産する必要はありません。", "非反復結論を圧壊する。" };
        scriptedLines["neutral_mechaz0rcannon|heroilena"] = new[]{ "砲部起動。観測済み破滅へ終末砲を収束。", "破滅を観たからこそ、その線上から外れる選択をします。", "終末照準、希望選択へ固定。" };
        scriptedLines["neutral_hydrax|heroilena"] = new[]{ "終わりは増える。観測しても、数は止まらぬ。", "増える終わりの中でも、希望を選ぶ余地はあります。", "ならば観ろ。増える首が、その余地を噛む。" };
        scriptedLines["arcana|heroilena"] = new[]{ "あなたは私と近い場所から悲劇を観ました。もう、同じ結論で休んでもいいのですよ。", "私はあなたと同じ悲劇を観ました。だからこそ、同じ結論ではなく希望を選びます。", "そうですか。では、その違う答えを観せてください。" };
        scriptedLines["neutral_rook|heroaldin"] = new[]{ "止まれ。守る者ほど、進めば失う。石になれば、もう誰も落とさぬ。", "守るべき者が後ろにいるから越える。止まるだけでは、届く盾も届かない。", "ならば越えてみろ。お前の盾ごと、門は沈黙を守る。" };
        scriptedLines["neutral_sister|heroaldin"] = new[]{ "守れなかった名を祈りへ捧げなさい。終焉なら痛みも眠ります。", "その名は捧げない。リオラの名を抱えたまま、私は守り続ける。", "ならば祈りで、その重い盾を伏せましょう。" };
        scriptedLines["neutral_mechaz0rwing|heroaldin"] = new[]{ "翼部起動。守護対象を抱える個体は、自由飛行に不適。", "抱えているから進む。守るための前進まで奪わせはしない。", "守護推進を異常値認定。墜落させる。" };
        scriptedLines["neutral_mechaz0rsword|heroaldin"] = new[]{ "剣部起動。後悔を切断し、効率的に進軍せよ。", "後悔は切り捨てない。守れなかった名ごと、私は進む。", "未切断要素を断罪する。" };
        scriptedLines["neutral_mechaz0rsuper|heroaldin"] = new[]{ "完全体起動。不完全な守護は神格秩序に不要。", "不完全でも守り続ける。完全な神より、その手を選ぶ。", "欠落保持個体を消去する。" };
        scriptedLines["neutral_mechaz0rhelm|heroaldin"] = new[]{ "兜部起動。記憶を停止し、命令に従え。", "忘れられない名がある。命令で盾は下ろさない。", "記憶保持を封鎖する。" };
        scriptedLines["neutral_mechaz0rchassis|heroaldin"] = new[]{ "胴部起動。個別名は量産秩序を乱す。", "命は同じ形に揃えるものではない。一人ずつ守る。", "個別名を圧壊する。" };
        scriptedLines["neutral_mechaz0rcannon|heroaldin"] = new[]{ "砲部起動。全守護対象を殲滅し、終末処理を完了する。", "終末兵器の前でも盾は下ろさない。私を越えてから撃て。", "終末砲、照準。盾ごと焼却する。" };
        scriptedLines["neutral_hydrax|heroaldin"] = new[]{ "守っても首は増える。終わりは、水底から何度でも戻る。", "終わりが増えるなら、守る手も増やす。私は退かない。", "ならば盾ごと噛み砕く。増える牙に耐えてみろ。" };
        scriptedLines["neutral_rook|herokagachi"] = new[]{ "止まれ。転ぶ足は、先でまた血を呼ぶ。ここで伏せれば傷は増えぬ。", "伏せたまま腐るくらいなら、転んでも進む。傷ごと前へ出るのが俺だ。", "ならば足を砕く。石は転ぶ者を通さぬ。" };
        scriptedLines["neutral_sister|herokagachi"] = new[]{ "弱さを祈りに預けなさい。赦されれば、もう進まずに済みます。", "赦されなくていい。弱いままでも、俺の足で前へ出る。", "その強情を祈りで折りましょう。" };
        scriptedLines["neutral_mechaz0rwing|herokagachi"] = new[]{ "翼部起動。転倒個体は自由規格に不適。歩行停止を推奨。", "飛べなくても進める。転んだ足で地面を蹴るだけだ。", "低位歩行を異常値認定。拘束する。" };
        scriptedLines["neutral_mechaz0rsword|herokagachi"] = new[]{ "剣部起動。脆弱性多数。断罪処理に適合。", "弱点が多くても終わりじゃねえ。斬られても進む。", "脆弱個体を切断する。" };
        scriptedLines["neutral_mechaz0rsuper|herokagachi"] = new[]{ "完全体起動。不完全歩行は神格秩序に不要。", "完成なんかしてなくていい。弱いまま進めるって証明する。", "不完全歩行を消去する。" };
        scriptedLines["neutral_mechaz0rhelm|herokagachi"] = new[]{ "兜部起動。弱点保持個体へ停止命令。", "命令で止まるくらいなら、とっくに倒れてる。", "停止拒否を検出。思考封鎖。" };
        scriptedLines["neutral_mechaz0rchassis|herokagachi"] = new[]{ "胴部起動。不規則歩行は量産モデルに不適。", "型通りに歩けねえから、俺の道になるんだよ。", "不規則経路を圧壊する。" };
        scriptedLines["neutral_mechaz0rcannon|herokagachi"] = new[]{ "砲部起動。脆弱再起個体を終末線上に捕捉。", "吹き飛ばされても、まだ進む。止められるもんなら撃ってみろ。", "終末砲、発射準備。再起を焼却する。" };
        scriptedLines["neutral_hydrax|herokagachi"] = new[]{ "転んだ足を嗅いだ。首は増える。逃げ場はない。", "逃げる気もねえ。増える牙の間を、転んでも進む。", "ならば噛まれろ。弱い足から飲み込んでやる。" };
        scriptedLines["neutral_rook|herovesna"] = new[]{ "止まれ。冷えた身は門影で休め。進む火は、また何かを焼く。", "休む場所は自分で作る。冷たい道にも、火を置いて進める。", "ならば火ごと塞ぐ。石は温度を求めない。" };
        scriptedLines["neutral_sister|herovesna"] = new[]{ "居場所を探す苦しみも、祈りに捧げれば終わります。", "終わらせない。冷たい場所にも、火を置けば居場所は作れる。", "その火を祈りで鎮めましょう。" };
        scriptedLines["neutral_mechaz0rwing|herovesna"] = new[]{ "翼部起動。割当外熱源は飛行規格を乱す。", "割り当てられなくても、火は置ける。そこから進める。", "自律熱源を拘束する。" };
        scriptedLines["neutral_mechaz0rsword|herovesna"] = new[]{ "剣部起動。未登録の居場所生成を断罪する。", "居場所は登録で決まらない。誰かと火を置いて作る。", "未登録熱源を切断する。" };
        scriptedLines["neutral_mechaz0rsuper|herovesna"] = new[]{ "完全体起動。冷却秩序に反する自律火を検出。", "完成した場所じゃなくていい。冷たい場所を温められれば。", "不完全熱源を神格秩序から除去する。" };
        scriptedLines["neutral_mechaz0rhelm|herovesna"] = new[]{ "兜部起動。居場所割当命令へ従属せよ。", "私の場所は命令で決まらない。火を置く相手は自分で選ぶ。", "自律選択を停止する。" };
        scriptedLines["neutral_mechaz0rchassis|herovesna"] = new[]{ "胴部起動。量産区画へ熱源を収容する。", "同じ箱に入れられた場所なんて、居場所じゃない。", "区画外熱源を圧壊する。" };
        scriptedLines["neutral_mechaz0rcannon|herovesna"] = new[]{ "砲部起動。焼却により全冷却問題を解決する。", "焼き尽くす火と、温める火は違う。私は後者を選ぶ。", "終末火力で微小熱源を消去する。" };
        scriptedLines["neutral_hydrax|herovesna"] = new[]{ "水底の群れが火を呑む。居場所ごと冷やしてやる。", "呑まれても、また火を置く。冷たい場所にも居場所は作れる。", "ならば深みに沈め。火の匂いを水で消す。" };
    }

    // ====== STORY: 中ボス（仲間化候補）の主人公共通台本（日本語）。キー=bossId（小文字）。値=[ボス,主人公,ボス…]。======
    // 章ボスのような主人公別台本は持たず、bossId単位で共通。主人公側は固有名を避けた一般的な返し。
    private static Dictionary<string, string[]> midBossLines;
    private static string[] GetMidBossLines(string bossId)
    {
        if (string.IsNullOrEmpty(bossId)) return null;
        if (midBossLines == null) BuildMidBossLines();
        return midBossLines.TryGetValue(bossId.ToLowerInvariant(), out string[] v) ? v : null;
    }
    private static void BuildMidBossLines()
    {
        midBossLines = new Dictionary<string, string[]>();
        midBossLines["silitharelder"] = new[]{ "古き殻は砕けぬ。燃える血を持たぬ者に、この熱は越えられん。", "古さも熱も、道を塞ぐ理由にはならない。ここを通る。", "ならば焼け残ってみせろ。灰の匂いで、お前の覚悟を測る。" };
        midBossLines["makantorwarbeast"] = new[]{ "グルル……前へ出たな。ならば踏み潰される覚悟もあるのだろう。", "大きさで勝負が決まるなら、ここまで来ていない。", "よく吠えた。牙と蹄で、その言葉を試してやる。" };
        midBossLines["veteransilithar"] = new[]{ "戦を生き延びた鱗は硬い。半端な刃では、傷ひとつ残せん。", "傷を残すためじゃない。越えるために刃を振るう。", "いい目だ。ならば古傷ごと、こちらも本気で応えよう。" };
        midBossLines["gloomchaser"] = new[]{ "影は足元から伸びる。気づいた時には、帰る道も呑まれているわ。", "影が伸びるなら、そこに光を置く。帰る道は失わない。", "ふふ、強がりね。では、その光をどこまで守れるか見せて。" };
        midBossLines["abyssalcrawler"] = new[]{ "地の下では、叫びも祈りも同じ泥になる。お前も沈め。", "沈むつもりはない。足を取られても、前へ出る。", "なら這い上がってみせろ。闇の腹は、簡単には吐き出さぬ。" };
        midBossLines["rae"] = new[]{ "砂は嘘を嫌う。足跡ひとつで、迷いまで暴かれる。", "迷いがあっても進む。足跡は、その証でいい。", "ならば砂に問おう。お前の歩みが本物かどうかを。" };
        midBossLines["starfirescarab"] = new[]{ "星火の甲は砕けぬ。砂漠の夜を越えた熱を、侮るな。", "侮らない。だからこそ、ここで止まらず越える。", "その意志、星火に照らしてやる。燃え尽きるなよ。" };
        midBossLines["pax"] = new[]{ "砂の秩序を乱す者は、群れに囲まれ道を失う。", "囲まれても道は作れる。仲間の背を見失わなければいい。", "ならば数で試す。秩序なき足並みが、どこまで持つか。" };
        midBossLines["pyromancer"] = new[]{ "砂と炎は相性がいい。乾いた願いほど、よく燃える。", "願いは燃やすためにあるんじゃない。進むために持つものだ。", "いい返しだ。では、その願いが灰になるまで焼いてみよう。" };
        midBossLines["neutral_beastmaster"] = new[]{ "獣は嘘を嗅ぎ分ける。恐れの匂いが、ずいぶん濃いな。", "恐れても退かない。恐れを理由に、誰かを置いてはいけない。", "なら獣たちに見せてみろ。震える足で、どこまで立てるか。" };
        midBossLines["neutral_gnasher"] = new[]{ "ギチギチ……柔らかい意志だ。噛めばすぐ砕けそうだな。", "噛まれても、砕けない芯はある。試してみればいい。", "ギャハ。気に入った。まずはその強がりから噛み砕く。" };
        midBossLines["neutral_rawr"] = new[]{ "ガァァッ！ 小さき者よ、森の咆哮に耐えられるか。", "声の大きさでは退かない。通るべき道がある。", "ならば咆哮の奥へ来い。牙の届く距離で語ろう。" };
        midBossLines["neutral_rok"] = new[]{ "岩は動かぬ。急ぐ者ほど、ここで砕ける。", "動かないなら越える。砕けても、道を開くために進む。", "よかろう。岩の重さで、その覚悟を量ってやる。" };
        midBossLines["neutral_zukong"] = new[]{ "ほう、ここまで来たか。だが森の賢者は、無謀な足音を見逃さぬ。", "無謀でも必要なら進む。止まるために来たわけじゃない。", "ならば知恵と爪で試そう。進む者に、どれほどの理由があるかを。" };
    }

    // ====== セリフ（仮・JA/EN）。ボスIDで分岐、無ければ汎用。 ======
    private string BossTaunt(string bossId, bool ja)
    {
        switch ((bossId ?? string.Empty).ToLowerInvariant())
        {
            case "magmarvaath": return ja ? "原始の炎よ、燃え盛れ。貴様の軍勢、灰にしてくれる。" : "Primal fire, rise. I'll turn your host to ash.";
            case "magmarstarhorn": return ja ? "嵐を統べる我に、児戯は通じぬ。" : "I command the storm. Your tricks mean nothing.";
            case "magmarragnora": return ja ? "我が同胞が次々と孵る。終わりなき群れに抗えるか？" : "My brood hatches without end. Can you withstand the swarm?";
            case "abyssallilithe": return ja ? "影は無限。お前の魂もじき、私の眷属となろう。" : "The shadows are endless. Your soul will join my brood.";
            case "abyssalcassyva": return ja ? "死は終わりではない――腐敗の始まりだ。" : "Death is no end—only the start of decay.";
            case "abyssalmaehv": return ja ? "深淵が口を開ける。抗うほど、堕ちるのが早まるぞ。" : "The abyss yawns wide. The more you struggle, the faster you fall.";
            case "vetruvianzirix": return ja ? "砂は全てを呑む。我が機巧の軍勢、止められるか？" : "The sands consume all. Can you halt my artifice legion?";
            case "vetruviansajj": return ja ? "太陽の威光の前に、影など瞬く間に蒸発する。" : "Before the sun's glory, shadows evaporate in an instant.";
            case "vetruvianscion": return ja ? "舞え、砂塵の刃。お前の最期を飾ろう。" : "Dance, blades of dust—I'll adorn your final hour.";
            case "arcana": return ja ? "まだ、歩くのですね。…もう、休んでもいいのですよ。" : "Still walking? …You may rest now.";
            case "caliber": return ja ? "我はキャリバー・O。光の名のもとに、貴様の進軍はここで断つ。" : "I am Caliber-O. In the name of the Light, your march ends here.";
            // 中ボス等の汎用：ボスIDで安定的に振り分け、全員同じセリフになるのを防ぐ。
            default: return Pick(bossId, ja ? GenericTauntsJa : GenericTauntsEn);
        }
    }

    private string HeroRetort(string heroId, bool ja)
    {
        switch ((heroId ?? string.Empty).ToLowerInvariant())
        {
            case "heroaldin": return ja ? "聖盾の名にかけて、仲間は一人も死なせはしない。" : "By my Aegis, not one ally falls today.";
            case "herokagachi": return ja ? "修羅の道、貴様ごときで止まると思うな。" : "The path of carnage won't end with the likes of you.";
            case "herovesna": return ja ? "蒼き炎で、その驕りごと焼き払う。" : "My azure flame will burn away that arrogance.";
            case "heroziran": return ja ? "癒やしの手は、戦うためにもある。退いてもらう。" : "These healing hands can fight, too. Stand down.";
            case "heroreva": return ja ? "風より速く。あんたの目で追えるかな？" : "Faster than the wind—can your eyes even follow?";
            case "herokara": return ja ? "凍てつく刃の前で、その勢いはどこまで保つ？" : "Before my frozen blade, how long does that bravado last?";
            case "herobrome": return ja ? "違う者たちを束ねた軍だ。お前一人で崩せるか。" : "This is an army of the different. Can you alone break it?";
            case "heroshidai": return ja ? "影は音もなく届く。気づいた時には遅い。" : "The shadow arrives in silence. By the time you notice, it's late.";
            case "heroilena": return ja ? "結末は観測した。だが、私が書き換える。" : "I've observed the ending. But I will rewrite it.";
            default: return ja ? "受けて立つ。覚悟はいいか。" : "I accept your challenge. Ready yourself.";
        }
    }

    // 安定ハッシュで配列から1つ選ぶ（同じIDなら常に同じ＝中ボス毎に固定の汎用セリフ）。
    private static string Pick(string seed, string[] arr)
    {
        if (arr == null || arr.Length == 0) return string.Empty;
        int h = 0; foreach (char c in seed ?? string.Empty) h = h * 31 + c;
        return arr[((h % arr.Length) + arr.Length) % arr.Length];
    }

    private static readonly string[] GenericTauntsJa = {
        "よくぞここまで来た。だが、ここで潰える。",
        "その歩み、私が止めてやろう。",
        "終焉へ近づく足音が、よく聞こえる。",
        "勇ましいな。だが無謀と紙一重だ。",
        "立ちはだかる者の意味を、教えてやる。",
        "貴様の旅も、ここまでだ。",
    };
    private static readonly string[] GenericTauntsEn = {
        "You've come far. But here you fall.",
        "Your advance ends with me.",
        "I can hear your footsteps nearing the end.",
        "Brave—or merely reckless.",
        "Let me show you what it means to stand in the way.",
        "Your journey ends here.",
    };
    private static readonly string[] GenericClosersJa = {
        "面白い。では――始めよう。",
        "ならば、見せてもらおう。その力を。",
        "退屈しのぎには、ちょうどいい。",
        "悔いの残らぬよう、全力で来い。",
        "終わらせる。手短に済ませよう。",
        "来い。語るのはもう十分だ。",
    };
    private static readonly string[] GenericClosersEn = {
        "Amusing. Then… let us begin.",
        "Then show me—your strength.",
        "A fine way to pass the time.",
        "Come at me fully, leave no regrets.",
        "I'll end this. Let's make it quick.",
        "Come. Enough talk.",
    };

    private string BossCloser(string bossId, bool ja)
    {
        switch ((bossId ?? string.Empty).ToLowerInvariant())
        {
            case "magmarvaath":
            case "magmarstarhorn":
            case "magmarragnora": return ja ? "ならば来い。原始の力、思い知らせてやろう！" : "Then come! Witness primal power!";
            case "abyssallilithe":
            case "abyssalcassyva":
            case "abyssalmaehv": return ja ? "ふふ……闇に呑まれて消えるがいい。" : "Heh… be swallowed by the dark.";
            case "caliber": return ja ? "光は我にあり。さあ、聖戦を始めよう！" : "The Light is with me. Now—let the crusade begin!";
            case "arcana": return ja ? "…そうですか。では、見せてください。あなたの続きを。" : "…I see. Then show me—what comes after.";
            default: return Pick(bossId, ja ? GenericClosersJa : GenericClosersEn);
        }
    }

    private static void FillRect(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

    private Image MakeImage(string name, Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root.transform, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = size;
        Image img = go.GetComponent<Image>(); img.raycastTarget = false;
        return img;
    }

    private Image MakeChildImage(Transform parent, string name, Vector2 aMin, Vector2 aMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        Image img = go.GetComponent<Image>(); img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, float size, FontStyles style, TextAlignmentOptions align)
    {
        GameObject go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        LocalizationManager.ApplyFont(t);
        t.fontSize = size; t.fontStyle = style; t.alignment = align; t.raycastTarget = false;
        t.color = Color.white;
        return t;
    }
}
