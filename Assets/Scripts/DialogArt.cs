using System.Collections.Generic;
using UnityEngine;

// STORY: ダイアログ/幕間/ヒーロー画面の立ち絵を一元解決するヘルパ。
// 素材は Assets/Resources/UI/Dialog（duelyst のダイアログイラスト一式）に統一。
// ユニットID → 画像名 のマップで解決し、無ければユニットアイコンへフォールバック。
// 旧 Resources/UI/HeroArt・BossArt は廃止（重複解消・軽量化）。
public static class DialogArt
{
    private const string Root = "UI/Dialog/";

    // 小文字ID → Resources/UI/Dialog 配下のスプライト名（拡張子なし）。
    // 主人公は hero_<名前>、ボス将は ユニットID と同名ファイル（陣営名+キャラ名）。
    private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
    {
        // --- 主人公 ---
        { "heroaldin", "hero_aldin" },
        { "herokagachi", "hero_kagachi" },
        { "skindogehai", "hero_kagachi_skindogehai" }, // 犬化スキン専用立ち絵
        { "herovesna", "hero_vesna" },
        { "heroziran", "hero_ziran" },
        { "heroreva", "hero_reva" },
        { "herokara", "hero_kara" },
        { "herobrome", "hero_brome" },
        { "heroshidai", "hero_shidai" },
        { "heroilena", "hero_ilena" },
        // --- 章ボス将（ファイル名＝ユニットID） ---
        { "caliber", "general_boss_1" },              // 専用ボス絵
        { "solfist", "general_f1" },                  // Lyonar将（汎用）
        { "dissonance", "general_unknown" },          // 専用絵なし→プレースホルダ
        { "magmarvaath", "Magmarvaath" },
        { "magmarstarhorn", "Magmarstarhorn" },
        { "magmarragnora", "Magmarragnora" },
        { "abyssallilithe", "Abyssallilithe" },
        { "abyssalcassyva", "Abyssalcassyva" },
        { "abyssalmaehv", "Abyssalmaehv" },
        { "vetruvianzirix", "Vetruvianzirix" },
        { "vetruviansajj", "Vetruviansajj" },
        { "vetruvianscion", "Vetruvianscion" },
        // --- 章2/章3 最終ボス専用の大判立ち絵（ユーザー用意。IconMap の小アイコンより優先） ---
        { "neutral_rook", "neutral_rook" },           // 章2ボス: Assets/Resources/UI/Dialog/neutral_rook.png
        { "neutral_sister", "neutral_lkiansister" },  // 章3ボス: Assets/Resources/UI/Dialog/neutral_lkiansister.png
        { "arcana", "general_unknown" },              // 専用絵が無いため当面プレースホルダ
    };

    private const string IconRoot = "AddUnit/DialogueIcon/";

    // 中ボス・中立ボス用のダイアログアイコン（Assets/Resources/AddUnit/DialogueIcon）。
    // 大判 general_f* を持たないユニットはこちらのアイコンをダイアログ立ち絵に使う（ユーザー要望）。
    private static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>
    {
        // 中ボス（中立）
        { "neutral_beastmaster", "neutral_beastmaster" },
        { "neutral_gnasher", "neutral_gnasher" },
        { "neutral_rawr", "neutral_rawr" },
        { "neutral_rok", "neutral_rok" },
        { "neutral_zukong", "neutral_zukong" },
        // 同素体の色違いリカラー（別個体）。会話の顔アイコンも専用画像を使う。
        { "neutral_beastmaster_crimson", "neutral_beastmaster_crimson" }, // 傷鬣ロウガ
        { "neutral_gnasher_ice", "neutral_gnasher_ice" },                 // 白符メイリン
        { "neutral_rok_steelblue", "neutral_rok_steelblue" },             // 停足オルム
        { "neutral_rok_gold", "neutral_rok_gold" },                       // 番牙ロク
        { "neutral_rok_mossgreen", "neutral_rok_mossgreen" },             // 門石ガロ
        // 中ボス（各陣営 非将）
        { "silitharelder", "magmar_silitharelder" },
        { "makantorwarbeast", "magmar_makantorwarbeast" },
        { "veteransilithar", "magmar_veteransilithar" },
        { "gloomchaser", "abyssian_gloomchaser" },
        { "abyssalcrawler", "abyssian_abyssalcrawler" },
        { "pax", "vetruvian_pax" },
        { "rae", "vetruvian_rae" },
        { "starfirescarab", "vetruvian_starfirescarab" },
        { "pyromancer", "vetruvian_pyromancer" },
        // 中立 章ボス（大判が無いのでダイアログアイコンを使う）
        { "neutral_rook", "neutral_rook" },
        { "neutral_hydrax", "neutral_hydrax1" },
        { "neutral_mechaz0rwing", "neutral_wingsofmechaz0r" },
        { "neutral_mechaz0rsword", "neutral_swordofmechaz0r" },
        { "neutral_mechaz0rsuper", "neutral_mechaz0r" },
        { "neutral_mechaz0rhelm", "neutral_mechaz0r" },
        { "neutral_mechaz0rchassis", "neutral_mechaz0r" },
        { "neutral_mechaz0rcannon", "neutral_mechaz0r" },
    };

    // ── 立ち絵サイズの全章統一（実測ベース正規化）──────────────────────────
    // 原因：素材ごとに「画像の解像度」と「キャラ本体が画像内で占める高さの割合(hFrac)」が
    // バラバラなため、preserveAspect で枠に収めるとキャラの実寸が大きく異なる
    // （例: hero_aldin はキャラ高さ占有0.61で小さく、neutral_rook は0.94＆縦長で枠いっぱい）。
    // 一律倍率では揃わない。そこで各画像のアルファ境界を実測し、
    // 「画面内でキャラ本体が常に同じ高さ(TargetCharPx)になる」よう個別スケールを算出する。
    //
    // ■ サイズ調整はここの TargetCharPx 一箇所だけ。大きく/小さくしたいときはこの値を変える。
    private const float FrameW = 1080f;       // 立ち絵枠の幅（HeroBossDialogueUI.MakeImage と一致させること）
    private const float FrameH = 1180f;       // 立ち絵枠の高さ（同上）
    private const float TargetCharPx = 660f;  // 画面内でのキャラ本体の目標高さ（1080基準）。全立ち絵で統一。
                                              // ※上げ過ぎると横幅の広い立ち絵(Caliber等)が画面中央で重なる。660前後が安全上限。

    // 各立ち絵のアルファ境界実測値。hFrac=キャラ本体高さ/画像高さ、aspect=画像幅/高さ。
    // 値はビルド時に PIL で測定（横長2560x1850が主、rook/sisterは縦長）。
    private struct Metrics { public float hFrac; public float aspect; public Metrics(float h, float a) { hFrac = h; aspect = a; } }
    private static readonly Metrics DefaultMetrics = new Metrics(0.78f, 2560f / 1850f); // 未測定IDの近似値
    private static readonly Dictionary<string, Metrics> PortraitMetrics = new Dictionary<string, Metrics>
    {
        // --- 主人公（ファイル名キー） ---
        { "hero_aldin",  new Metrics(0.606f, 2560f/1850f) },
        { "hero_kagachi", new Metrics(0.754f, 2560f/1850f) },
        { "hero_kagachi_skindogehai", new Metrics(0.902f, 1407f/1118f) },
        { "hero_vesna",  new Metrics(0.785f, 2560f/1850f) },
        { "hero_ziran",  new Metrics(0.680f, 2560f/1850f) },
        { "hero_reva",   new Metrics(0.604f, 2560f/1850f) },
        { "hero_kara",   new Metrics(0.744f, 2560f/1850f) },
        { "hero_brome",  new Metrics(0.856f, 2560f/1850f) },
        { "hero_shidai", new Metrics(0.781f, 2560f/1850f) },
        { "hero_ilena",  new Metrics(0.728f, 2560f/1850f) },
        // --- 章ボス将 ---
        { "general_boss_1",     new Metrics(0.809f, 2560f/1850f) },
        { "general_f1",         new Metrics(0.606f, 2560f/1850f) },
        { "general_unknown",    new Metrics(0.753f, 2560f/1850f) },
        { "Magmarvaath",        new Metrics(0.883f, 2560f/1850f) },
        { "Magmarstarhorn",     new Metrics(0.919f, 2560f/1850f) },
        { "Magmarragnora",      new Metrics(0.869f, 2560f/1850f) },
        { "Abyssallilithe",     new Metrics(0.743f, 2560f/1850f) },
        { "Abyssalcassyva",     new Metrics(0.750f, 2560f/1850f) },
        { "Abyssalmaehv",       new Metrics(0.639f, 2560f/1850f) },
        { "Vetruvianzirix",     new Metrics(0.882f, 2560f/1850f) },
        { "Vetruviansajj",      new Metrics(0.788f, 2560f/1850f) },
        { "Vetruvianscion",     new Metrics(0.853f, 2560f/1850f) },
        // --- 章2/章3 最終ボス（縦長） ---
        { "neutral_rook",        new Metrics(0.944f, 1149f/1369f) },
        { "neutral_lkiansister", new Metrics(0.943f, 1117f/1409f) },
    };

    // unitId → 立ち絵ファイル名（Map→IconMap）。PortraitMetrics 引き当て用。
    private static string ResolveFileName(string unitId)
    {
        string key = unitId.ToLowerInvariant();
        if (Map.TryGetValue(key, out string n)) return n;
        if (IconMap.TryGetValue(key, out string ic)) return ic;
        return null;
    }

    // 立ち絵ごとの表示スケール。キャラ本体が画面内で常に TargetCharPx 高になるよう正規化。
    public static float PortraitScale(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return 1f;
        string file = ResolveFileName(unitId);
        Metrics m = (file != null && PortraitMetrics.TryGetValue(file, out Metrics v)) ? v : DefaultMetrics;
        // preserveAspect で枠に収めた時の「画像全体の描画高さ」。
        float frameAspect = FrameW / FrameH;
        float renderedFullH = (m.aspect >= frameAspect) ? (FrameW / m.aspect) // 横長＝幅基準
                                                        : FrameH;             // 縦長＝高さ基準
        float charHeightAtScale1 = renderedFullH * m.hFrac;
        if (charHeightAtScale1 <= 1f) return 1f;
        float s = TargetCharPx / charHeightAtScale1;
        return Mathf.Clamp(s, 0.4f, 2.4f); // 念のため極端値を抑制
    }

    // 大判の立ち絵スプライト。大判Map→ダイアログアイコン→ユニットアイコン→null。
    public static Sprite Portrait(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return null;
        string key = unitId.ToLowerInvariant();
        if (Map.TryGetValue(key, out string name))
        {
            Sprite s = Resources.Load<Sprite>(Root + name);
            if (s != null) return s;
        }
        // ダイアログアイコン（中ボス・中立ボス）。
        if (IconMap.TryGetValue(key, out string icon))
        {
            Sprite s = Resources.Load<Sprite>(IconRoot + icon);
            if (s != null) return s;
        }
        // フォールバック：ショップ用ユニットアイコン。
        if (GameManager.Instance != null)
        {
            Sprite ic = GameManager.Instance.GetEntityIconById(unitId);
            if (ic != null) return ic;
        }
        return null;
    }

    // 主人公の顔アイコン（コンパクト中ボスダイアログ用）。大判立ち絵の縮小ではなく、
    // duelyst の将ダイアログアイコン（AddUnit/DialogueIcon）を使う。
    private static readonly Dictionary<string, string> HeroFaceMap = new Dictionary<string, string>
    {
        { "heroaldin", "lyonar_argeonhighmayne1" },
        { "herobrome", "lyonar_brome1" },
        { "heroziran", "lyonar_ziransunforge1" },
        { "heroreva", "songhai_revaeventide1" },
        { "heroshidai", "songhai_shidai1" },
        { "herokagachi", "songhai_kaleosxaan1" },     // 推定（Songhai将）
        { "herovesna", "vanar_faiebloodwing1" },       // 推定（Vanar将）
        { "herokara", "vanar_karawinterblade1" },
        { "heroilena", "vanar_ilena1" },
    };

    // 小さな顔アイコン（コンパクト表示用）。主人公=専用顔アイコン、ボス/中立=ダイアログアイコン、
    // 無ければユニットアイコン→null。大判立ち絵(Portrait)は使わない。
    public static Sprite FaceIcon(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return null;
        string key = unitId.ToLowerInvariant();
        if (HeroFaceMap.TryGetValue(key, out string face))
        {
            Sprite s = Resources.Load<Sprite>(IconRoot + face);
            if (s != null) return s;
        }
        if (IconMap.TryGetValue(key, out string icon))
        {
            Sprite s = Resources.Load<Sprite>(IconRoot + icon);
            if (s != null) return s;
        }
        if (GameManager.Instance != null)
        {
            Sprite ic = GameManager.Instance.GetEntityIconById(unitId);
            if (ic != null) return ic;
        }
        return null;
    }

    // 専用立ち絵（大判Map or ダイアログアイコン登録済み）が存在するか。
    public static bool HasDedicatedPortrait(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return false;
        string key = unitId.ToLowerInvariant();
        return Map.ContainsKey(key) || IconMap.ContainsKey(key);
    }
}
