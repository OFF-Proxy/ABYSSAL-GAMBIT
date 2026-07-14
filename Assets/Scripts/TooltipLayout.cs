// シナジー/アイテム/コイン内訳パネルを左上（シナジーパネルの右隣）に固定表示するための共有レイアウト値。
// pivot=(0,0) のパネルを前提に、左下角のスクリーン座標を計算する。
public static class TooltipLayout
{
    // シナジーパネルの右端(≈162px)の少し右。各パネルの左端X座標。
    public const float FixedPanelX = 170f;

    // 画面上端からパネル上端までの余白（シナジーパネル上端と揃える）。
    public const float FixedPanelTopMargin = 70f;
}
