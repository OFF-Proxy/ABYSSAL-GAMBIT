using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// 盤面タイルとベンチタイルを管理するクラスです。
// タイルをGraphのNodeへ変換し、移動経路・配置可能範囲・ホバー表示を扱います。
public class GridManager : Manager<GridManager>
{
    // 盤面タイルが子として入っている親オブジェクトです。
    public GameObject terrainGrid;

    // ドラッグ中に置き先候補として表示するホバー用タイル画像です。
    public UnityEngine.Tilemaps.Tile hoverTile;

    // 盤面の横幅と、プレイヤーが手動配置できる左側列数です。
    public int boardColumns = 10;
    public int playerDeploymentColumns = 5;

    // ベンチタイルをEditorメニューから自動生成する時の設定です。
    public int benchRows = 8;
    public float benchLeftX = -6.5f;
    public float benchRightX = 6.5f;
    public float benchBottomY = -3.5f;
    public float benchSpacing = 1f;

    // 盤面・ベンチ・ホバー表示の色設定です。
    public Color playerTileColor = Color.white;
    public Color blockedPlacementTileColor = new Color(1f, 0.72f, 0.72f, 1f);
    public Color playerBenchTileColor = Color.white;
    public Color enemyBenchTileColor = new Color(1f, 0.62f, 0.62f, 1f);
    public Color itemBenchTileColor = new Color(0.78f, 0.86f, 1f, 1f);
    public Color validHoverColor = new Color(0.75f, 1f, 0.9f, 1f);
    public Color invalidHoverColor = new Color(1f, 0.35f, 0.35f, 1f);

    // マウス位置からタイルを拾う時、この距離以内なら同じタイル上とみなします。
    public float tilePickRadius = 0.72f;

    // 盤面上のノードとエッジをまとめたグラフです。
    protected Graph graph;

    // 各チームの初期配置位置の目安です。古い処理との互換用に残しています。
    protected Dictionary<Team, int> startPositionPerTeam;

    // シーン上の全盤面タイルと、タイルからNodeを引くための対応表です。
    List<Tile> allTiles = new List<Tile>();
    Dictionary<Tile, Node> nodeByTile = new Dictionary<Tile, Node>();

    // Nodeが盤面の何列目かをキャッシュします。手動配置できる列の判定に使います。
    Dictionary<Node, int> columnByNode = new Dictionary<Node, int>();

    // すでに色設定を済ませたベンチタイルを覚えて、同じ処理を何度も走らせないようにします。
    HashSet<Tile> configuredBenchTiles = new HashSet<Tile>();

    // シーン開始時に、タイルからGraphと配置判定用データを作ります。
    protected void Awake()
    {
        // Manager<T>のSingleton登録を先に済ませます。
        base.Awake();

        // terrainGrid配下のTileコンポーネントを盤面として扱います。
        allTiles = terrainGrid.GetComponentsInChildren<Tile>().ToList();

        // 盤面タイルから移動用グラフと検索用キャッシュを作ります。
        InitializeGraph();
        InitializeTileLookup();
        InitializeNodeColumns();
        ConfigureTiles();
        ConfigureBenchTilesInScene();

        // チーム1は左端、チーム2は右端を初期位置として扱うためのデータです。
        startPositionPerTeam = new Dictionary<Team, int>();
        startPositionPerTeam.Add(Team.Team1, 0);
        startPositionPerTeam.Add(Team.Team2, graph.Nodes.Count -1);
    }

    // 指定チームが使える、まだ誰もいないNodeを1つ返します。
    public Node GetFreeNode(Team forTeam)
    {
        IEnumerable<Node> candidates = graph.Nodes.Where(node => IsDeploymentNode(forTeam, node) && !node.IsOccupied);

        // 味方は左から、敵は右から順に空きマスを探します。
        if (forTeam == Team.Team1)
            candidates = candidates.OrderBy(node => GetColumnIndex(node)).ThenBy(node => node.worldPosition.y);
        else
            candidates = candidates.OrderByDescending(node => GetColumnIndex(node)).ThenBy(node => node.worldPosition.y);

        return candidates.FirstOrDefault();
    }

    // 盤面の列・行番号からNodeを取得します。
    // columnNumberは左から1始まり、rowNumberは下から1始まりとして扱います。
    public Node GetNodeAtBoardCoordinate(int columnNumber, int rowNumber)
    {
        if (graph == null || graph.Nodes == null || boardColumns <= 0)
            return null;

        if (columnNumber < 1 || columnNumber > boardColumns || rowNumber < 1)
            return null;

        int columnIndex = columnNumber - 1;
        List<Node> columnNodes = graph.Nodes
            .Where(node => GetColumnIndex(node) == columnIndex)
            .OrderBy(node => node.worldPosition.y)
            .ToList();

        if (rowNumber > columnNodes.Count)
            return null;

        return columnNodes[rowNumber - 1];
    }

    // 手動配置できるNodeかどうかを判定します。
    public bool CanManuallyPlace(Team team, Node node)
    {
        // 今はプレイヤー側だけが手動配置できる仕様です。
        return team == Team.Team1 && IsDeploymentNode(team, node);
    }

    // 指定チームの初期配置エリアに含まれるNodeかどうかを判定します。
    public bool IsDeploymentNode(Team team, Node node)
    {
        if (node == null || boardColumns <= 0)
            return false;

        int column = GetColumnIndex(node);
        if (column < 0)
            return false;

        if (team == Team.Team1)
            return column < playerDeploymentColumns;

        return column >= boardColumns - playerDeploymentColumns;
    }

    // Graphに経路探索を依頼します。
    public List<Node> GetPath(Node from, Node to)
    {
        return graph.GetPath(from, to);
    }

    // 指定Nodeの周囲にあるNodeを返します。
    public List<Node> GetNodesCloseTo(Node to)
    {
        return graph.Neighbors(to);
    }

    // 指定Nodeを中心に、rangeInTilesマス分の円形範囲内にあるNodeを返します。
    public List<Node> GetNodesInRange(Node center, float rangeInTiles)
    {
        if (center == null || graph == null)
            return new List<Node>();

        // 少し余裕を足して、ちょうど境界線上のマスが取りこぼされにくいようにします。
        float range = Mathf.Max(0f, rangeInTiles) + 0.05f;
        return graph.Nodes
            .Where(node => node != null && Vector3.Distance(node.worldPosition, center.worldPosition) <= range)
            .ToList();
    }

    // Tileから対応するNodeを取得します。
    public Node GetNodeForTile(Tile t)
    {
        if (t != null && nodeByTile.TryGetValue(t, out Node node))
            return node;

        return null;
    }

    // ワールド座標に最も近いタイルを返します。
    public Tile GetTileAtWorldPosition(Vector3 worldPosition)
    {
        Tile closestTile = null;
        float closestDistance = tilePickRadius * tilePickRadius;

        for (int i = 0; i < allTiles.Count; i++)
        {
            Vector3 tilePosition = allTiles[i].transform.position;
            float distance = (tilePosition - worldPosition).sqrMagnitude;
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestTile = allTiles[i];
            }
        }

        return closestTile;
    }

    // 互換用です。チーム指定なしならプレイヤーベンチとして設定します。
    public void ConfigureBenchTile(Tile tile)
    {
        ConfigureBenchTile(tile, Team.Team1);
    }

    // ベンチタイルに色とホバー用スプライトを設定します。
    public void ConfigureBenchTile(Tile tile, Team team)
    {
        if (tile == null || configuredBenchTiles.Contains(tile))
            return;

        Color benchColor = team == Team.Team2 ? enemyBenchTileColor : playerBenchTileColor;
        tile.Configure(GetHoverSprite(), benchColor, validHoverColor, invalidHoverColor);
        configuredBenchTiles.Add(tile);
    }

    // アイテムベンチ用の色とホバー表示を設定します。
    public void ConfigureItemBenchTile(Tile tile)
    {
        if (tile == null)
            return;

        tile.Configure(GetHoverSprite(), itemBenchTileColor, validHoverColor, invalidHoverColor);
        configuredBenchTiles.Add(tile);
    }

#if UNITY_EDITOR
    // Inspectorの右クリックメニューからベンチタイルを作り直すためのEditor専用処理です。
    [ContextMenu("Build Bench Tiles")]
    public void BuildBenchTiles()
    {
        Transform gridParent = terrainGrid != null && terrainGrid.transform.parent != null
            ? terrainGrid.transform.parent
            : transform;

        // 盤面と同じタイルPrefabを使って、左右にベンチを作ります。
        GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/SingleTile.prefab");
        if (tilePrefab == null)
        {
            Debug.LogError("Bench build failed: Assets/Prefabs/Tiles/SingleTile.prefab was not found.");
            return;
        }

        Transform leftBench = RebuildBenchParent(gridParent, "BenchLeft");
        Transform rightBench = RebuildBenchParent(gridParent, "BenchRight");

        for (int i = 0; i < benchRows; i++)
        {
            float y = benchBottomY + i * benchSpacing;
            CreateBenchTile(tilePrefab, leftBench, $"BenchTile_L_{i}", new Vector3(benchLeftX, y, 0f));
            CreateBenchTile(tilePrefab, rightBench, $"BenchTile_R_{i}", new Vector3(benchRightX, y, 0f));
        }

        // GameManagerにも新しく作ったベンチ親を渡し、シーンに保存対象として印を付けます。
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.team1BenchTilesParent = leftBench;
            gameManager.team2BenchTilesParent = rightBench;
            gameManager.benchSlotCount = benchRows;
            EditorUtility.SetDirty(gameManager);
        }

        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // 既存ベンチ親があれば削除し、空の親オブジェクトを作り直します。
    private Transform RebuildBenchParent(Transform gridParent, string benchName)
    {
        Transform existing = gridParent.Find(benchName);
        if (existing != null)
            DestroyImmediate(existing.gameObject);

        GameObject bench = new GameObject(benchName);
        Undo.RegisterCreatedObjectUndo(bench, $"Create {benchName}");
        bench.transform.SetParent(gridParent);
        bench.transform.localPosition = Vector3.zero;
        bench.transform.localRotation = Quaternion.identity;
        bench.transform.localScale = Vector3.one;
        return bench.transform;
    }

    // ベンチ用タイルPrefabを1枚生成し、位置と名前を設定します。
    private void CreateBenchTile(GameObject tilePrefab, Transform parent, string tileName, Vector3 localPosition)
    {
        GameObject tileObject = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, parent);
        tileObject.name = tileName;
        tileObject.transform.localPosition = localPosition;
        tileObject.transform.localRotation = Quaternion.identity;
        tileObject.transform.localScale = Vector3.one;

        if (tileObject.GetComponent<Tile>() == null)
            tileObject.AddComponent<Tile>();

        Undo.RegisterCreatedObjectUndo(tileObject, $"Create {tileName}");
    }
#endif

    // 盤面タイルの位置からGraphを作り、近いタイル同士を移動可能な隣接Nodeとしてつなぎます。
    private void InitializeGraph()
    {
        graph = new Graph();

        // 各タイルの位置にNodeを1つずつ作ります。
        for (int i = 0; i < allTiles.Count; i++)
        {
            Vector3 place = allTiles[i].transform.position;
            graph.AddNode(place);
        }

        var allNodes = graph.Nodes;

        // すべてのNode同士を確認し、縦横斜め1マス以内ならEdgeでつなぎます。
        foreach (Node from in allNodes)
        {
            foreach (Node to in allNodes)
            {
                if ((Mathf.Abs(from.worldPosition.x - to.worldPosition.x) <= 1 && Mathf.Abs(from.worldPosition.y - to.worldPosition.y) <= 1) && from != to)
                {
                    graph.AddEdge(from, to);
                }
            }
        }
    }

    // TileとNodeの対応表を作ります。
    private void InitializeTileLookup()
    {
        nodeByTile.Clear();

        int count = Mathf.Min(allTiles.Count, graph.Nodes.Count);
        for (int i = 0; i < count; i++)
        {
            Tile tile = allTiles[i];
            Node node = graph.Nodes[i];
            nodeByTile[tile] = node;
        }
    }

    // Nodeが盤面の何列目にあるかを計算してキャッシュします。
    private void InitializeNodeColumns()
    {
        columnByNode.Clear();

        // x座標が近いNodeを同じ列としてまとめます。
        List<float> columns = new List<float>();
        foreach (Node node in graph.Nodes.OrderBy(node => node.worldPosition.x))
        {
            if (!columns.Any(x => Mathf.Abs(x - node.worldPosition.x) <= 0.05f))
                columns.Add(node.worldPosition.x);
        }

        for (int i = 0; i < graph.Nodes.Count; i++)
            columnByNode[graph.Nodes[i]] = GetClosestColumn(columns, graph.Nodes[i].worldPosition.x);
    }

    // 盤面タイルに通常色、配置不可色、ホバー色を設定します。
    private void ConfigureTiles()
    {
        Sprite hoverSprite = GetHoverSprite();

        for (int i = 0; i < allTiles.Count; i++)
        {
            Node node = GetNodeForTile(allTiles[i]);
            bool canPlaceTeam1 = IsDeploymentNode(Team.Team1, node);
            Color baseColor = canPlaceTeam1 ? playerTileColor : blockedPlacementTileColor;
            allTiles[i].Configure(hoverSprite, baseColor, validHoverColor, invalidHoverColor);
        }
    }

    // シーン内に置かれている左右ベンチタイルの色設定を行います。
    private void ConfigureBenchTilesInScene()
    {
        ConfigureBenchTileParent(GameObject.Find("Grid/BenchLeft"), Team.Team1);
        ConfigureBenchTileParent(GameObject.Find("Grid/BenchRight"), Team.Team2);
    }

    // 指定ベンチ親の子Tileをまとめて設定します。
    private void ConfigureBenchTileParent(GameObject benchParent, Team team)
    {
        if (benchParent == null)
            return;

        Tile[] benchTiles = benchParent.GetComponentsInChildren<Tile>(true);
        for (int i = 0; i < benchTiles.Length; i++)
            ConfigureBenchTile(benchTiles[i], team);
    }

    // ホバー用スプライトを取得します。未設定ならEditor上でassetから探します。
    private Sprite GetHoverSprite()
    {
        if (hoverTile != null)
            return hoverTile.sprite;

#if UNITY_EDITOR
        hoverTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Prefabs/Tiles/tile_hover.asset");
        if (hoverTile != null)
            return hoverTile.sprite;
#endif

        return null;
    }

    // Nodeに対応する列番号を返します。
    private int GetColumnIndex(Node node)
    {
        if (node != null && columnByNode.TryGetValue(node, out int column))
            return column;

        return -1;
    }

    // x座標に最も近い列番号を探します。
    private int GetClosestColumn(List<float> columns, float xPosition)
    {
        int closestIndex = -1;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < columns.Count; i++)
        {
            float distance = Mathf.Abs(columns[i] - xPosition);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    // Sceneビューで経路確認をする時の開始・終了Node番号です。
    public int fromIndex = 0;
    public int toIndex = 0;

    // UnityのSceneビュー上に、Node・Edge・確認用経路を描画します。
    private void OnDrawGizmos()
    {
        if (graph == null)
            return;

        // Node同士のつながりを線で表示します。
        var allEdges = graph.Edges;
        if (allEdges != null && allEdges.Count > 0)
        {
            foreach(Edge e in allEdges)
            {
                Gizmos.DrawLine(e.from.worldPosition, e.to.worldPosition);
            }
        }

        // 空きNodeは緑、占有済みNodeは赤で表示します。
        var allNodes = graph.Nodes;
        if (allNodes != null)
        {
            foreach (Node n in allNodes)
            {
                Gizmos.color = n.IsOccupied ? Color.red : Color.green;
                Gizmos.DrawSphere(n.worldPosition, 0.1f);
            }
        }

        // fromIndexからtoIndexへの経路を赤線で表示します。
        if(fromIndex < allNodes.Count && toIndex < allNodes.Count)
        {
            List<Node> path = graph.GetPath(allNodes[fromIndex], allNodes[toIndex]);
            if(path.Count > 1)
            {
                for(int i = 1; i < path.Count; i++)
                {
                    Debug.DrawLine(path[i - 1].worldPosition, path[i].worldPosition, Color.red, 1);
                }
            }
        }
    }
}
