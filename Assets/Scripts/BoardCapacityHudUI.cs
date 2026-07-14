using TMPro;
using UnityEngine;

// ① 盤面の配置数表示。盤面中央・背景とタイルの間に、数字のみを大きく半透明で出すバックドロップ。
//   重要: Canvasは作らない。以前ワールド空間Canvasで実装したところ、他UI(RoundProgressUI等)が
//   FindObjectOfType<Canvas>() でこのCanvasを拾って子付けし、表示位置が崩れる不具合が出たため、
//   Canvasに依存しない 3D の TextMeshPro（MeshRenderer）で描画する。
public class BoardCapacityHudUI : MonoBehaviour
{
    public static BoardCapacityHudUI Instance { get; private set; }

    private bool isBuilt;
    private TextMeshPro label;          // 3D TextMeshPro（Canvas非依存）。
    private MeshRenderer labelRenderer;
    private float prepReadySince = -1f; // 編成準備が安定して整った時刻（復元直後の数値ブレ吸収用）。

    public static BoardCapacityHudUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        BoardCapacityHudUI existing = FindObjectOfType<BoardCapacityHudUI>(true);
        if (existing != null)
        {
            Instance = existing;
            Instance.BuildIfNeeded();
            return Instance;
        }

        GameObject root = new GameObject("BoardCapacityHudUI", typeof(BoardCapacityHudUI));
        Instance = root.GetComponent<BoardCapacityHudUI>();
        Instance.BuildIfNeeded();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildIfNeeded();
    }

    private void BuildIfNeeded()
    {
        if (isBuilt) return;

        GameObject labelObj = new GameObject("Capacity", typeof(TextMeshPro));
        labelObj.transform.SetParent(transform, false);
        label = labelObj.GetComponent<TextMeshPro>();
        LocalizationManager.ApplyFont(label);
        label.fontSize = 36f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 1f, 1f, 0.5f);   // 背景に溶け込む半透明の大文字。
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color(0f, 0f, 0f, 0.7f);
        label.text = "";

        labelRenderer = labelObj.GetComponent<MeshRenderer>();
        isBuilt = true;
    }

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;

        // 編成準備中だけ表示。戦闘中/勝利演出/各種選択/ボス登場演出/導入演出/GameOver を除外し、
        // 戦闘直後の盤面復元（死亡ユニット復活）前に減った配置数が一瞬出ないよう少し待ってから表示する。
        bool lobbyVisible = LobbyUI.Instance != null && LobbyUI.Instance.gameObject.activeInHierarchy;
        bool ready = gm != null && !lobbyVisible && gm.IsPrepPhaseReady;
        if (!ready) prepReadySince = -1f;
        else if (prepReadySince < 0f) prepReadySince = Time.unscaledTime;
        bool show = ready && (Time.unscaledTime - prepReadySince) >= 0.45f;

        if (label != null && label.gameObject.activeSelf != show)
            label.gameObject.SetActive(show);
        if (!show) return;

        // 盤面中央に配置し、描画順を「背景の1つ上＝タイルの後ろ」に収める。
        Renderer bg = gm.cameraBoundsRenderer;
        if (bg != null)
        {
            Vector3 c = bg.bounds.center;
            transform.position = new Vector3(c.x, c.y, 0f);
            if (labelRenderer != null)
            {
                labelRenderer.sortingLayerID = bg.sortingLayerID;
                labelRenderer.sortingOrder = bg.sortingOrder + 1; // 背景とタイル(=100)の間。
            }
        }

        int placed = gm.PlacedTeam1Count;
        int limit = gm.PlacementLimit;
        // 満杯=赤寄り / 残り1=黄寄り / 余裕=白。いずれも半透明でバックドロップに馴染ませる。
        Color col;
        if (placed >= limit) col = new Color(1f, 0.5f, 0.45f, 0.5f);
        else if (limit - placed <= 1) col = new Color(1f, 0.9f, 0.5f, 0.5f);
        else col = new Color(1f, 1f, 1f, 0.5f);
        label.color = col;
        label.text = $"{placed} / {limit}"; // 文字（配置/Units）は出さず数字のみ。
    }
}
