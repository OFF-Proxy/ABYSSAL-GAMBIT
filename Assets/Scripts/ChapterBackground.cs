using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// R4-chapter-background: 章ごとの動的バトル背景。
// 背景/中景/前景の視差レイヤーと、テーマに合った天候パーティクルを GameScene にランタイム構築する。
// 素材は Assets/Resources/maps と Assets/Resources/particles。EnsureExists パターン。
// GameManager の章初期化時に ApplyChapter(currentChapter) を呼ぶ。
// すべて盤面より後ろ（負の sortingOrder）に置き、盤面/ユニットの視認性を絶対に損なわない。
public class ChapterBackground : MonoBehaviour
{
    public static ChapterBackground Instance { get; private set; }

    private Camera cam;
    private Transform layerRoot;
    private readonly List<Layer> layers = new List<Layer>();
    private Vector3 camHome;
    private int appliedChapter = -1;

    // 視差レイヤー1枚分の追従/ゆらぎ情報。
    private class Layer
    {
        public Transform tr;
        public Vector3 home;
        public float parallax;   // カメラ移動への追従強さ（背景ほど小さい）。
        public float swayAmp;    // 自律ゆらぎの振幅（控えめ）。
        public float swaySpeed;
        public float swayPhase;
    }

    // ---- 天候の種類 ----
    private enum Weather { None, Snow, Petals, Embers, Ash, Dust, BlueDust, Rain, Clouds }

    // ---- テーマ定義（どのマップ素材＋どの天候か）----
    private class Theme
    {
        public string bg;                 // background 素材パス（null可）
        public string mid;                // middleground 素材パス
        public string[] midExtras;        // river/cracks/glow 等の追加中景（背面側）
        public string[] fg;               // foreground 素材パス
        public Weather weather;
        public Color tint;                // レイヤー全体の淡い色補正
        public Theme(string bg, string mid, string[] midExtras, string[] fg, Weather w, Color tint)
        { this.bg = bg; this.mid = mid; this.midExtras = midExtras; this.fg = fg; this.weather = w; this.tint = tint; }
    }

    private static readonly Color Neutral = new Color(0.92f, 0.93f, 0.96f, 1f);

    // 12テーマ。@2x優先で読み込む（LoadMap が自動でフォールバック）。
    private static Theme[] BuildThemes()
    {
        return new Theme[]
        {
            // 0 battlemap0: 石/中立
            new Theme("maps/battlemap0_background","maps/battlemap0_middleground",null,
                new[]{"maps/battlemap0_foreground_001","maps/battlemap0_foreground_002"}, Weather.Dust, Neutral),
            // 1 battlemap1: 中立アリーナ
            new Theme("maps/battlemap1_background","maps/battlemap1_middleground",null,null, Weather.Clouds, Neutral),
            // 2 battlemap2: 溶岩/夕焼け
            new Theme("maps/battlemap2_background","maps/battlemap2_middleground",null,
                new[]{"maps/battlemap2_foreground_001","maps/battlemap2_foreground_002"}, Weather.Embers, new Color(1f,0.93f,0.86f,1f)),
            // 3 battlemap3: 青い魔法
            new Theme("maps/battlemap3_background","maps/battlemap3_middleground",null,
                new[]{"maps/battlemap3_foreground"}, Weather.BlueDust, new Color(0.88f,0.93f,1f,1f)),
            // 4 battlemap4: 紫の毒沼
            new Theme("maps/battlemap4_background","maps/battlemap4_middleground",null,
                new[]{"maps/battlemap4_foreground_001","maps/battlemap4_foreground_002"}, Weather.Dust, new Color(0.95f,0.9f,1f,1f)),
            // 5 battlemap5: 森
            new Theme("maps/battlemap5_background","maps/battlemap5_middleground",null,
                new[]{"maps/battlemap5_foreground_001","maps/battlemap5_foreground_002"}, Weather.Petals, new Color(0.92f,0.97f,0.9f,1f)),
            // 6 battlemap6: 水辺（中景のみ）
            new Theme(null,"maps/battlemap6_middleground",null,null, Weather.Rain, new Color(0.88f,0.92f,0.98f,1f)),
            // 7 battlemap7: 星夜
            new Theme("maps/battlemap7_background","maps/battlemap7_middleground",null,
                new[]{"maps/battlemap7_foreground"}, Weather.Dust, new Color(0.85f,0.88f,1f,1f)),
            // 8 abyssian: 冥界
            new Theme("maps/abyssian/background","maps/abyssian/midground",
                new[]{"maps/abyssian/midground_river","maps/abyssian/midground_cracks_glow"},null, Weather.Ash, new Color(0.86f,0.84f,0.96f,1f)),
            // 9 redrock: 火山岩
            new Theme("maps/redrock/background","maps/redrock/midground",
                new[]{"maps/redrock/midground_glow"}, new[]{"maps/redrock/foreground"}, Weather.Embers, new Color(1f,0.9f,0.82f,1f)),
            // 10 shimzar: 密林
            new Theme("maps/shimzar/background","maps/shimzar/midground",null,
                new[]{"maps/shimzar/foreground"}, Weather.Petals, new Color(0.9f,0.97f,0.92f,1f)),
            // 11 vanar: 氷雪
            new Theme("maps/vanar/background","maps/vanar/midground",null,null, Weather.Snow, new Color(0.9f,0.95f,1f,1f)),
        };
    }

    // 章(1..20) → テーマindex。DESIGN_R4-chapter-background §3。ch8=abyssian固定。ここを書き換えれば一括変更可。
    private static readonly int[] ChapterToTheme =
    {
        /*ch1*/1, /*2*/0, /*3*/3, /*4*/5, /*5*/10, /*6*/2, /*7*/9, /*8*/8, /*9*/7, /*10*/4,
        /*11*/11, /*12*/6, /*13*/1, /*14*/10, /*15*/9, /*16*/3, /*17*/11, /*18*/2, /*19*/8, /*20*/9,
    };

    // 章番号から公開テーマ取得（ロビー側でも背景画像を引くために使う）。
    public static Sprite GetBackgroundSpriteForChapter(int chapter)
    {
        var t = ThemeForChapter(chapter);
        return LoadMap(t.bg ?? t.mid);
    }
    public static Sprite GetMiddleSpriteForChapter(int chapter)
    {
        return LoadMap(ThemeForChapter(chapter).mid);
    }
    public static Sprite GetForegroundSpriteForChapter(int chapter)
    {
        var t = ThemeForChapter(chapter);
        return (t.fg != null && t.fg.Length > 0) ? LoadMap(t.fg[0]) : null;
    }

    private static Theme ThemeForChapter(int chapter)
    {
        var themes = BuildThemes();
        int idx = (chapter >= 1 && chapter <= ChapterToTheme.Length) ? ChapterToTheme[chapter - 1] : 1;
        idx = Mathf.Clamp(idx, 0, themes.Length - 1);
        return themes[idx];
    }

    // ③④ 盤面形状: 章のテーマに応じて、四隅を何マス分“斜めに削る”かを返す（0=フル矩形）。
    // 丸型プラットフォームの陣営マップ(idx 8-11: abyssian/redrock/shimzar/vanar)は角丸=1で自然に丸へ寄せる。
    // 矩形の枠マップ(battlemap系)は削らない。値を上げると 2=八角形 / 3=菱形寄り（GridManagerが解釈）。
    public static int GetBoardCornerCut(int chapter)
    {
        int idx = (chapter >= 1 && chapter <= ChapterToTheme.Length) ? ChapterToTheme[chapter - 1] : 1;
        return (idx >= 8 && idx <= 11) ? 1 : 0;
    }

    public static ChapterBackground EnsureExists()
    {
        if (Instance != null) return Instance;
        ChapterBackground ex = FindObjectOfType<ChapterBackground>(true);
        if (ex != null) { Instance = ex; return Instance; }
        GameObject go = new GameObject("ChapterBackground");
        Instance = go.AddComponent<ChapterBackground>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 指定章のテーマで背景を再構築する。
    public void ApplyChapter(int chapter)
    {
        cam = Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();
        if (cam == null) return;
        if (appliedChapter == chapter && layerRoot != null) return;
        appliedChapter = chapter;
        camHome = cam.transform.position;

        // 旧静的背景は見た目だけ消す（boundsはカメラクランプ/ベンチ計算に使うのでオブジェクトは残す）。
        GameObject old = GameObject.Find("battlemap1_middleground");
        if (old != null)
        {
            SpriteRenderer osr = old.GetComponent<SpriteRenderer>();
            if (osr != null) osr.enabled = false;
        }

        Rebuild(ThemeForChapter(chapter));
    }

    private void Rebuild(Theme theme)
    {
        StopAllCoroutines(); // 前回のフォアグラウンド開閉コルーチンを止める。
        if (layerRoot != null) Destroy(layerRoot.gameObject);
        layers.Clear();
        fgLeft = fgRight = null;

        GameObject rootGo = new GameObject("BgLayers");
        layerRoot = rootGo.transform;
        layerRoot.SetParent(transform, false);

        // 背面→前面の順に積む。z は背景ほど遠く、order は全て負（盤面より後ろ）。
        // background
        AddSpriteLayer(theme.bg, 6.0f, -200, theme.tint, 0.04f, 0.10f, 9f);
        // 中景追加（river/cracks/glow など、middleground より背面）＝プラットフォーム扱い（盤面中心・拡大）。
        if (theme.midExtras != null)
        {
            for (int i = 0; i < theme.midExtras.Length; i++)
                AddSpriteLayer(theme.midExtras[i], 4.3f - i * 0.05f, -190 + i, theme.tint, 0.07f, 0.10f, 7.5f, platform: true);
        }
        // middleground（主役＝プラットフォーム。盤面中心に合わせ、フィールドが盤面を包むよう拡大）。
        AddSpriteLayer(theme.mid, 4.0f, -180, theme.tint, 0.10f, 0.10f, 6.5f, platform: true);
        // 天候パーティクル（中景の手前・盤面の後ろ）
        BuildWeather(theme.weather, -160);
        // foreground = 章開始時に左右へ開く「カーテン」。盤面/ユニットより手前(前面)に出し、開くと盤面が見える。
        BuildForegroundCurtains(theme);
    }

    // ---- ⑤ フォアグラウンド開閉演出（章開始で左右に開き、戦闘中は開いたまま）----
    private Transform fgLeft, fgRight;
    // 開演出中は盤面/ユニット(CalculateSortingOrder最大〜12400)より手前、ScreenSpaceOverlay UIより後ろ。
    private const int ForegroundOrder = 13000;
    // 開き切った後は盤面より後ろへ回す（戦闘中は絶対に盤面/ユニットを隠さない）。middleground(-180)より手前・盤面(0+)より後ろ。
    private const int ForegroundRestOrder = -150;

    private void BuildForegroundCurtains(Theme theme)
    {
        if (theme.fg == null || theme.fg.Length == 0) return;
        float z = -2f; // 盤面(z=0)より手前（開演出中のみ）。
        float halfW, halfH; GetViewExtents(z, out halfW, out halfH);

        if (theme.fg.Length >= 2)
        {
            // 2枚組: _001=左カーテン / _002=右カーテン。開くと盤面の外まで退く（大きめに開く）。
            fgLeft = MakeForegroundPiece(theme.fg[0], z, ForegroundOrder, theme.tint);
            fgRight = MakeForegroundPiece(theme.fg[1], z, ForegroundOrder, theme.tint);
            StartCoroutine(RevealCurtains(halfW * 2.1f, false));
        }
        else
        {
            // 単一fg: 上へ大きく退避して盤面を見せる。
            fgLeft = MakeForegroundPiece(theme.fg[0], z, ForegroundOrder, theme.tint);
            StartCoroutine(RevealCurtains(halfH * 2.2f, true));
        }
    }

    private Transform MakeForegroundPiece(string path, float z, int order, Color tint)
    {
        Sprite sp = LoadMap(path);
        if (sp == null) return null;
        GameObject go = new GameObject("FG_" + path.Substring(path.LastIndexOf('/') + 1));
        go.transform.SetParent(layerRoot, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.color = tint; sr.sortingOrder = order;
        float hw, hh; GetViewExtents(z, out hw, out hh);
        Vector2 s = sp.bounds.size; if (s.x <= 0.001f || s.y <= 0.001f) s = Vector2.one;
        float scale = Mathf.Max((hw * 2f * 1.12f) / s.x, (hh * 2f * 1.12f) / s.y);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.position = new Vector3(camHome.x, camHome.y, z); // 初期=閉（中央、盤面を覆う）。
        return go.transform;
    }

    // 閉（中央）→ 開（左右/上）へスライド。実時間(unscaled)で進行し、開いたまま維持。
    private IEnumerator RevealCurtains(float shift, bool vertical)
    {
        float cx = camHome.x, cy = camHome.y;
        yield return new WaitForSecondsRealtime(0.35f); // 表示直後の一拍。
        float dur = 1.15f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            if (vertical)
            {
                if (fgLeft != null) fgLeft.position = new Vector3(cx, cy + shift * k, fgLeft.position.z);
            }
            else
            {
                if (fgLeft != null) fgLeft.position = new Vector3(cx - shift * k, cy, fgLeft.position.z);
                if (fgRight != null) fgRight.position = new Vector3(cx + shift * k, cy, fgRight.position.z);
            }
            yield return null;
        }
        // 開き切ったら盤面より後ろへ回す（戦闘中は絶対に盤面/ユニットを隠さない）。
        SetForegroundBehind(fgLeft);
        SetForegroundBehind(fgRight);
    }

    private static void SetForegroundBehind(Transform fg)
    {
        if (fg == null) return;
        SpriteRenderer sr = fg.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = ForegroundRestOrder;
    }

    // ② プラットフォーム(middleground)を盤面に合わせる調整。
    // 盤面中心(実測 y≈-0.04)にプラットフォーム層を合わせ、フィールドが盤面(幅8.1)を内側に収めるよう拡大する。
    private const float BoardCenterY = -0.04f;
    private const float PlatformFitScale = 1.3f; // 大きいほど足場が枠内に収まる（テーマ調整可）。

    // 1枚のスプライトレイヤーを生成し、画面を覆うようスケールして登録する。
    // platform=true の層(middleground/中景)は、カメラ中心ではなく盤面中心に合わせ、フィールドが盤面を包むよう拡大する。
    private void AddSpriteLayer(string path, float z, int order, Color tint, float parallax, float swayAmp, float swaySpeed, bool platform = false)
    {
        if (string.IsNullOrEmpty(path)) return;
        Sprite sp = LoadMap(path);
        if (sp == null) return;

        GameObject go = new GameObject("Layer_" + path.Substring(path.LastIndexOf('/') + 1));
        go.transform.SetParent(layerRoot, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.color = tint;
        sr.sortingOrder = order;

        // z平面でカメラ視界を覆うスケールを求める。プラットフォームは盤面を包むよう更に拡大。
        float halfW, halfH;
        GetViewExtents(z, out halfW, out halfH);
        Vector2 sw = sp.bounds.size; // ワールド単位の素サイズ
        if (sw.x <= 0.001f || sw.y <= 0.001f) sw = Vector2.one;
        float margin = 1.12f;
        float scale = Mathf.Max((halfW * 2f * margin) / sw.x, (halfH * 2f * margin) / sw.y);
        if (platform) scale *= PlatformFitScale;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        // プラットフォームは盤面中心(y≈-0.04)に、背景等はカメラ中心に合わせる。
        float centerY = platform ? BoardCenterY : camHome.y;
        Vector3 home = new Vector3(camHome.x, centerY, z);
        go.transform.position = home;

        layers.Add(new Layer
        {
            tr = go.transform,
            home = home,
            parallax = parallax,
            swayAmp = swayAmp,
            swaySpeed = swaySpeed,
            swayPhase = Random.Range(0f, 6.28f),
        });
    }

    // カメラの z平面における可視範囲（半幅・半高）。透視/正射の両対応。
    private void GetViewExtents(float planeZ, out float halfW, out float halfH)
    {
        if (cam.orthographic)
        {
            halfH = cam.orthographicSize;
        }
        else
        {
            float dist = Mathf.Abs(planeZ - cam.transform.position.z);
            halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
        }
        halfW = halfH * Mathf.Max(0.1f, cam.aspect);
    }

    // ---- 天候パーティクル ----
    private static Material alphaMat;
    private static Material addMat;

    private static Material GetParticleMaterial(Texture2D tex, bool additive)
    {
        // テクスチャ毎に material を共有せず、必要時に生成（テクスチャが違うので個別）。
        Shader sh = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        if (tex != null) m.mainTexture = tex;
        return m;
    }

    private void BuildWeather(Weather w, int order)
    {
        if (w == Weather.None) return;

        float halfW, halfH;
        GetViewExtents(2.5f, out halfW, out halfH);
        float cx = camHome.x, cy = camHome.y, z = 2.5f;

        switch (w)
        {
            case Weather.Snow:
                MakeFalling("particles/snow", order, cx, cy, z, halfW, halfH,
                    new Color(1f, 1f, 1f, 0.7f), false, 0.07f, 0.16f, 5f, 8f, 0.55f, 14f, true);
                break;
            case Weather.Petals:
                MakeFalling("particles/petals_001", order, cx, cy, z, halfW, halfH,
                    new Color(1f, 0.85f, 0.9f, 0.8f), false, 0.13f, 0.30f, 6f, 9f, 0.5f, 9f, true);
                break;
            case Weather.Rain:
                MakeRain("particles/rain", order, cx, cy, z, halfW, halfH);
                break;
            case Weather.Embers:
                MakeFloating("particles/dotorb", order, cx, cy, z, halfW, halfH,
                    new Color(1f, 0.6f, 0.25f, 0.55f), true, 0.05f, 0.12f, 5f, 8f, 0.18f, 10f, -0.04f);
                break;
            case Weather.Ash:
                MakeFalling("particles/dotorb", order, cx, cy, z, halfW, halfH,
                    new Color(0.35f, 0.33f, 0.4f, 0.4f), false, 0.05f, 0.12f, 7f, 11f, 0.25f, 10f, true);
                break;
            case Weather.Dust:
                MakeFloating("particles/dotorb", order, cx, cy, z, halfW, halfH,
                    new Color(1f, 0.95f, 0.82f, 0.32f), true, 0.05f, 0.13f, 7f, 12f, 0.1f, 7f, 0f);
                break;
            case Weather.BlueDust:
                MakeFloating("particles/dotorb", order, cx, cy, z, halfW, halfH,
                    new Color(0.55f, 0.8f, 1f, 0.4f), true, 0.05f, 0.13f, 7f, 12f, 0.12f, 8f, 0f);
                break;
            case Weather.Clouds:
                MakeClouds("particles/cloud_002", order, cx, cy, z, halfW, halfH);
                break;
        }
    }

    private ParticleSystem NewPS(string name, int order, Texture2D tex, bool additive, Color startColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(layerRoot, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = GetParticleMaterial(tex, additive);
        rend.sortingOrder = order;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    // 落下系（雪・花びら・灰）。
    private void MakeFalling(string texPath, int order, float cx, float cy, float z, float halfW, float halfH,
        Color col, bool additive, float sizeMin, float sizeMax, float lifeMin, float lifeMax, float speed, float rate, bool rotate)
    {
        Texture2D tex = Resources.Load<Texture2D>(texPath);
        ParticleSystem ps = NewPS("fx_fall", order, tex, additive, col);
        ps.transform.position = new Vector3(cx, cy + halfH + 0.5f, z);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.7f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = col;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.gravityModifier = 0.02f;
        main.maxParticles = 200;

        var em = ps.emission; em.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(halfW * 2.4f, 0.2f, 1f);
        shape.rotation = new Vector3(90f, 0f, 0f); // 下向きに放出

        var vol = ps.velocityOverLifetime; vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.y = new ParticleSystem.MinMaxCurve(-speed);

        var noise = ps.noise; noise.enabled = true;
        noise.strength = 0.2f; noise.frequency = 0.18f; noise.scrollSpeed = 0.1f;

        if (rotate)
        {
            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        }
        ApplyFade(ps, col);
        ps.Play();
    }

    // 浮遊系（塵・残り火・青塵）。画面全体に漂う。
    private void MakeFloating(string texPath, int order, float cx, float cy, float z, float halfW, float halfH,
        Color col, bool additive, float sizeMin, float sizeMax, float lifeMin, float lifeMax, float driftSpeed, float rate, float gravity)
    {
        Texture2D tex = Resources.Load<Texture2D>(texPath);
        ParticleSystem ps = NewPS("fx_float", order, tex, additive, col);
        ps.transform.position = new Vector3(cx, cy, z);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, driftSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = col;
        main.gravityModifier = gravity; // 残り火は負（上昇）
        main.maxParticles = 160;

        var em = ps.emission; em.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(halfW * 2.2f, halfH * 2f, 1f);

        var noise = ps.noise; noise.enabled = true;
        noise.strength = 0.25f; noise.frequency = 0.12f; noise.scrollSpeed = 0.08f;

        ApplyFade(ps, col);
        ps.Play();
    }

    // 雨。Stretch billboard で縦に伸ばす。
    private void MakeRain(string texPath, int order, float cx, float cy, float z, float halfW, float halfH)
    {
        Texture2D tex = Resources.Load<Texture2D>(texPath);
        Color col = new Color(0.7f, 0.8f, 0.95f, 0.5f);
        ParticleSystem ps = NewPS("fx_rain", order, tex, false, col);
        ps.transform.position = new Vector3(cx, cy + halfH + 0.5f, z);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        main.startColor = col;
        main.startRotation = 0.18f; // 少し斜め
        main.gravityModifier = 0.4f;
        main.maxParticles = 300;

        var em = ps.emission; em.rateOverTime = 70f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(halfW * 2.6f, 0.2f, 1f);
        shape.rotation = new Vector3(90f, 0f, 0f);

        var vol = ps.velocityOverLifetime; vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.y = new ParticleSystem.MinMaxCurve(-8f); vol.x = new ParticleSystem.MinMaxCurve(-1.2f);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.12f; rend.lengthScale = 2.5f;
        ps.Play();
    }

    // 雲。巨大・極低速で横へ流れ、極低Alpha。
    private void MakeClouds(string texPath, int order, float cx, float cy, float z, float halfW, float halfH)
    {
        Texture2D tex = Resources.Load<Texture2D>(texPath);
        Color col = new Color(1f, 1f, 1f, 0.16f);
        ParticleSystem ps = NewPS("fx_clouds", order, tex, false, col);
        ps.transform.position = new Vector3(cx - halfW - 1.5f, cy + halfH * 0.45f, z);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(30f, 45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(halfH * 1.2f, halfH * 2.2f);
        main.startColor = col;
        main.maxParticles = 8;

        var em = ps.emission; em.rateOverTime = 0.18f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.2f, halfH * 1.2f, 1f);

        var vol = ps.velocityOverLifetime; vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(0.18f);

        ApplyFade(ps, col);
        // 開始時点で画面に数枚見えるよう、少し先まで進める。
        ps.Simulate(30f, true, false);
        ps.Play();
    }

    // 出現/消滅で淡くフェードさせる colorOverLifetime。
    private void ApplyFade(ParticleSystem ps, Color col)
    {
        var colOver = ps.colorOverLifetime; colOver.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) }
        );
        colOver.color = new ParticleSystem.MinMaxGradient(g);
    }

    private void LateUpdate()
    {
        if (cam == null || layers.Count == 0) return;
        Vector3 camDelta = cam.transform.position - camHome;
        float t = Time.time;
        for (int i = 0; i < layers.Count; i++)
        {
            Layer L = layers[i];
            if (L.tr == null) continue;
            // 視差（カメラ移動への弱い追従）＋自律ゆらぎ（控えめなサイン揺れ）。
            float swayX = Mathf.Sin(t * L.swaySpeed * 0.1f + L.swayPhase) * L.swayAmp;
            float swayY = Mathf.Cos(t * L.swaySpeed * 0.08f + L.swayPhase) * L.swayAmp * 0.4f;
            L.tr.position = new Vector3(
                L.home.x + camDelta.x * L.parallax + swayX,
                L.home.y + camDelta.y * L.parallax + swayY,
                L.home.z);
        }
    }

    // @2x を優先し、無ければ等倍を読み込むマップ素材ローダ。
    private static Sprite LoadMap(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        Sprite s = Resources.Load<Sprite>(path + "@2x");
        if (s == null) s = Resources.Load<Sprite>(path);
        return s;
    }
}
