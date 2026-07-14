using UnityEngine;

// HUD表示設定（settings.hud.*）を GameScene の各HUDへ実際に適用する常駐コンポーネント。
// 値の保存・既定値は SettingsStore が持ち、ここは「実際の表示/非表示」だけを担当する。
// SettingsStore.OnChanged を購読し、設定パネルでの切替を即時反映する。
public class HudSettingsApplier : MonoBehaviour
{
    private static HudSettingsApplier instance;

    public static HudSettingsApplier EnsureExists()
    {
        if (instance != null)
            return instance;

        var existing = FindObjectOfType<HudSettingsApplier>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject go = new GameObject("HudSettingsApplier");
        instance = go.AddComponent<HudSettingsApplier>();
        return instance;
    }

    private void OnEnable()
    {
        SettingsStore.OnChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        SettingsStore.OnChanged -= Apply;
    }

    // 各HUDの表示状態を現在の設定値へ合わせる。
    public void Apply()
    {
        // シナジーパネル（常設）。OFFならルートごと非表示。
        var synergy = SynergyPanelUI.EnsureExists();
        if (synergy != null)
            synergy.gameObject.SetActive(SettingsStore.GetHud("synergy"));

        // ラウンド進行表示（常設）。
        var round = RoundProgressUI.EnsureExists();
        if (round != null)
            round.gameObject.SetActive(SettingsStore.GetHud("round"));

        // コイン内訳パネル（クリックで開くポップアップ）。OFFなら開いていても閉じる（新規表示は Show 側で抑止）。
        if (!SettingsStore.GetHud("coin"))
            CoinIncomePanelUI.Hide();

        // ツールチップOFFなら、現在開いているものを閉じる（新規表示は各 Show で抑止）。
        if (!SettingsStore.GetHud("tooltip"))
        {
            SynergyTooltipUI.Hide();
            ItemTooltipUI.Hide();
            AugmentTooltipUI.Hide();
        }
    }
}
