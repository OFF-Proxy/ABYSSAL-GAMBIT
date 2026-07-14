using System;
using UnityEngine;

// ゲーム全体の設定を一元管理（PlayerPrefs で永続化）。GameScene 側からも参照・適用できる静的API。
// 音量/言語は既存 OptionsPanelUI とキーを共有。表示/グラフィック/カーソル/UIサイズはここで適用する。
// HUD表示やキーバインドの「実際の適用」は GameScene 側のフックで読む想定（値の保存だけここで持つ）。
public static class SettingsStore
{
    // --- PlayerPrefs キー ---
    public const string KeyMasterVolume = "options.masterVolume";
    public const string KeyBgmVolume = "options.bgmVolume";
    public const string KeySfxVolume = "options.sfxVolume";
    public const string KeyMuted = "options.muted";

    public const string KeyFullscreen = "settings.fullscreen";
    public const string KeyResIndex = "settings.resIndex";
    public const string KeyQuality = "settings.quality";
    public const string KeyCursor = "settings.cursor";   // 0=システム標準 / 1=ゲーム用
    public const string KeyUiScale = "settings.uiScale";  // 0=小 1=中 2=大

    public const string KeyHudPrefix = "settings.hud.";   // settings.hud.<name> = 0/1
    public const string KeyBindPrefix = "settings.key.";   // settings.key.<action> = KeyCode名

    public const string KeySkipPrologue = "settings.skipPrologue"; // 0=毎回再生 / 1=スキップ

    public static event Action OnChanged;
    public static void RaiseChanged() { OnChanged?.Invoke(); }

    // ===== 値アクセサ =====
    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVolume, 1f));
        set { PlayerPrefs.SetFloat(KeyMasterVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }
    public static float BgmVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyBgmVolume, 1f));
        set { PlayerPrefs.SetFloat(KeyBgmVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }
    public static float SfxVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, 1f));
        set { PlayerPrefs.SetFloat(KeySfxVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }
    public static bool Muted
    {
        get => PlayerPrefs.GetInt(KeyMuted, 0) != 0;
        set { PlayerPrefs.SetInt(KeyMuted, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) != 0;
        set { PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static int ResolutionIndex
    {
        get => PlayerPrefs.GetInt(KeyResIndex, -1);
        set { PlayerPrefs.SetInt(KeyResIndex, value); PlayerPrefs.Save(); }
    }
    public static int QualityLevel
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        set { PlayerPrefs.SetInt(KeyQuality, value); PlayerPrefs.Save(); }
    }
    public static int CursorStyle
    {
        get => PlayerPrefs.GetInt(KeyCursor, 0);
        set { PlayerPrefs.SetInt(KeyCursor, value); PlayerPrefs.Save(); }
    }
    // 0=小 1=中 2=大
    public static int UiScaleStep
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyUiScale, 1), 0, 2);
        set { PlayerPrefs.SetInt(KeyUiScale, Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
    }
    public static float UiScaleFactor
    {
        get { int s = UiScaleStep; return s == 0 ? 0.85f : (s == 2 ? 1.18f : 1f); }
    }

    // 章プロローグ（章開始時の全画面一枚絵演出）をスキップするか。既定OFF（毎回再生）。
    public static bool SkipPrologue
    {
        get => PlayerPrefs.GetInt(KeySkipPrologue, 0) != 0;
        set { PlayerPrefs.SetInt(KeySkipPrologue, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool GetHud(string name, bool defaultOn = true)
    {
        return PlayerPrefs.GetInt(KeyHudPrefix + name, defaultOn ? 1 : 0) != 0;
    }
    public static void SetHud(string name, bool on)
    {
        PlayerPrefs.SetInt(KeyHudPrefix + name, on ? 1 : 0);
        PlayerPrefs.Save();
        RaiseChanged();
    }

    public static KeyCode GetBind(string action, KeyCode fallback)
    {
        string s = PlayerPrefs.GetString(KeyBindPrefix + action, fallback.ToString());
        if (Enum.TryParse(s, out KeyCode code)) return code;
        return fallback;
    }
    public static void SetBind(string action, KeyCode code)
    {
        PlayerPrefs.SetString(KeyBindPrefix + action, code.ToString());
        PlayerPrefs.Save();
        RaiseChanged();
    }

    // ===== 適用 =====
    public static void ApplyAll()
    {
        ApplyAudio();
        ApplyDisplay();
        ApplyQuality();
        ApplyCursor();
        ApplyUiScale();
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = Muted ? 0f : MasterVolume;
        AttackEffectPlayer.SetBgmVolume(BgmVolume);
        AttackEffectPlayer.SetSfxVolume(SfxVolume);
    }

    public static void ApplyDisplay()
    {
        Resolution[] res = Screen.resolutions;
        int idx = ResolutionIndex;
        bool fs = Fullscreen;
        if (res != null && res.Length > 0 && idx >= 0 && idx < res.Length)
            Screen.SetResolution(res[idx].width, res[idx].height, fs);
        else
            Screen.fullScreen = fs;
    }

    public static void ApplyQuality()
    {
        if (QualitySettings.names.Length > 0)
            QualitySettings.SetQualityLevel(Mathf.Clamp(QualityLevel, 0, QualitySettings.names.Length - 1), true);
    }

    private static Texture2D fallbackCursorTex;

    // カーソル候補（duelyst mouse_*）。index0=システム標準(null)。
    public static readonly string[] CursorResources =
    {
        null,                          // 0 システム標準
        "UI/Cursors/cursor_select",    // 1 選択
        "UI/Cursors/cursor_assist",    // 2 補助
        "UI/Cursors/cursor_disabled",  // 3 無効
    };

    public static int CursorCount => CursorResources.Length;

    public static void ApplyCursor()
    {
        int idx = Mathf.Clamp(CursorStyle, 0, CursorResources.Length - 1);
        string res = CursorResources[idx];
        if (string.IsNullOrEmpty(res))
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }
        Texture2D tex = Resources.Load<Texture2D>(res);
        if (tex == null)
        {
            if (fallbackCursorTex == null) fallbackCursorTex = BuildArrowCursor();
            tex = fallbackCursorTex;
        }
        Cursor.SetCursor(tex, Vector2.zero, CursorMode.Auto);
    }

    // ゲーム用カーソル（白縁の矢印）を手続き生成。
    private static Texture2D BuildArrowCursor()
    {
        int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                tex.SetPixel(x, y, clear);
        // 左上原点の矢印（y を反転して描画）
        for (int y = 0; y < n; y++)
        {
            int row = n - 1 - y;
            for (int x = 0; x < n; x++)
            {
                bool fill = (x <= y) && (x < n / 2) && (y < n - 2);
                if (fill)
                {
                    bool edge = (x == 0) || (x == y) || (y >= n - 3);
                    tex.SetPixel(x, row, edge ? new Color(0.05f, 0.05f, 0.08f, 1f) : new Color(0.95f, 0.97f, 1f, 1f));
                }
            }
        }
        tex.Apply();
        return tex;
    }

    public static void ApplyUiScale()
    {
        // ConstantPixelSize の CanvasScaler に scaleFactor を反映（オーバーレイUI全般）。
        float f = UiScaleFactor;
        var scalers = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.CanvasScaler>();
        foreach (var sc in scalers)
        {
            if (sc.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize)
                sc.scaleFactor = f;
        }
    }
}
