using UnityEngine;

// 1920x1080 基準の画面レイアウト定数を一元管理する。
// （DESIGN_CODEX_DUELYST_UI_RELAYOUT.md / Phase1）
// 目的: 各UIが実行時に座標を上書きしても、値がここに集約されていれば互いに押し戻し合わない。
// UIShop / ItemBenchCanvasUI / SynergyPanelUI / UnitStatusPanelUI / RoundProgressUI / AugmentHudUI が参照する。
public static class GameHudLayout
{
    public const float RefWidth = 1920f;
    public const float RefHeight = 1080f;

    // ----- 画面を5領域に分割（Rect は左下原点、x,y=左下角 / w,h=幅高さ、1920x1080基準）-----
    public static readonly Rect Arena      = new Rect(260f, 105f, 1280f, 665f);  // 中央: 盤面/アリーナ
    public static readonly Rect TopHud     = new Rect(0f, 975f, 1920f, 105f);    // 上部: プレイヤー/ウェーブ/章
    public static readonly Rect LeftDock   = new Rect(0f, 180f, 230f, 720f);     // 左: アイテムベンチ+シナジー
    public static readonly Rect RightDock  = new Rect(1540f, 300f, 370f, 560f);  // 右: ユニット詳細ドック（下部ショップを避ける）
    public static readonly Rect BottomShop = new Rect(230f, 0f, 1450f, 290f);    // 下部: ショップ/経済/戦闘開始

    // ----- 下部ショップの操作系 -----
    // 経済クラスタ（bottom-left(0,0) アンカーからの anchoredPosition）
    // 1枚のパネル内に「マナ／レベル・経験値／Ex購入」をきれいに縦積みする。
    public static readonly Vector2 ManaGemPos    = new Vector2(44f, 254f);
    public static readonly Vector2 ManaGemSize   = new Vector2(36f, 36f);
    public static readonly Vector2 MoneyPos      = new Vector2(86f, 252f);
    public static readonly Vector2 LevelPos      = new Vector2(46f, 202f);   // "Lv N"（短縮）
    public static readonly Vector2 ExpPos        = new Vector2(120f, 202f);
    public static readonly Vector2 ExpButtonPos  = new Vector2(124f, 150f);
    public static readonly Vector2 ExpButtonSize = new Vector2(206f, 54f);

    // 経済パネル（上記すべてを内包する暗半透明角丸。コンパクトに）。
    public static readonly Vector2 EconomyPanelPos  = new Vector2(126f, 202f);
    public static readonly Vector2 EconomyPanelSize = new Vector2(232f, 178f);

    // アクションクラスタ（bottom-right(1,0) アンカーからの anchoredPosition）
    public static readonly Vector2 RerollPos  = new Vector2(-170f, 210f);
    public static readonly Vector2 RerollSize = new Vector2(240f, 78f);
    public static readonly Vector2 FightPos   = new Vector2(-175f, 100f);
    public static readonly Vector2 FightSize  = new Vector2(300f, 112f);

    // ショップ5枠（中央寄せ＋弧）
    public const float ShopSlotSpacing = 240f;
    public const float ShopArcDepth    = 18f;   // doc: 弧配置を少し弱める
    public const float ShopSlotRaise   = 58f;   // 全枠を上へ（弧で下がる中央のコスト表示が隠れないように）

    // ----- 上部HUD -----
    public static readonly Vector2 OptionsPos = new Vector2(-90f, -34f); // top-right(1,1) 基準

    // 画面中心X（ワールド, ScreenSpaceOverlay/scaleFactor=1 前提）。枠の中央寄せに使う。
    public static float ScreenCenterX => Screen.width * 0.5f;
}
