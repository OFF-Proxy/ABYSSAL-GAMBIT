using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class GridManager : Manager<GridManager>
{  
    public GameObject terrainGrid; // ゲーム内のタイルマップを参照
    public UnityEngine.Tilemaps.Tile hoverTile;
    public int boardColumns = 10;
    public int playerDeploymentColumns = 5;
    public int benchRows = 8;
    public float benchLeftX = -6.5f;
    public float benchRightX = 6.5f;
    public float benchBottomY = -3.5f;
    public float benchSpacing = 1f;
    public Color playerTileColor = Color.white;
    public Color blockedPlacementTileColor = new Color(1f, 0.72f, 0.72f, 1f);
    public Color playerBenchTileColor = Color.white;
    public Color enemyBenchTileColor = new Color(1f, 0.62f, 0.62f, 1f);
    public Color validHoverColor = new Color(0.75f, 1f, 0.9f, 1f);
    public Color invalidHoverColor = new Color(1f, 0.35f, 0.35f, 1f);
    public float tilePickRadius = 0.72f;
    protected Graph graph; // グリッドのノードとエッジを管理するグラフ
    protected Dictionary<Team, int> startPositionPerTeam; // 各チームの初期配置位置を保持する辞書

    List<Tile> allTiles = new List<Tile>();
    Dictionary<Tile, Node> nodeByTile = new Dictionary<Tile, Node>();
    Dictionary<Node, int> columnByNode = new Dictionary<Node, int>();
    HashSet<Tile> configuredBenchTiles = new HashSet<Tile>();

        /// 初期化時に呼び出され、グラフを生成し、各チームの開始位置を設定する
    protected void Awake()
    {
        base.Awake(); // 親クラスの Awake() を呼び出す
        allTiles = terrainGrid.GetComponentsInChildren<Tile>().ToList();
        
        InitializeGraph();
        InitializeTileLookup();
        InitializeNodeColumns();
        ConfigureTiles();
        ConfigureBenchTilesInScene();
        startPositionPerTeam = new Dictionary<Team, int>();
        startPositionPerTeam.Add(Team.Team1, 0);
        startPositionPerTeam.Add(Team.Team2, graph.Nodes.Count -1);
    }

    public Node GetFreeNode(Team forTeam)
    {
        IEnumerable<Node> candidates = graph.Nodes.Where(node => IsDeploymentNode(forTeam, node) && !node.IsOccupied);

        if (forTeam == Team.Team1)
            candidates = candidates.OrderBy(node => GetColumnIndex(node)).ThenBy(node => node.worldPosition.y);
        else
            candidates = candidates.OrderByDescending(node => GetColumnIndex(node)).ThenBy(node => node.worldPosition.y);

        return candidates.FirstOrDefault();
    }

    public bool CanManuallyPlace(Team team, Node node)
    {
        return team == Team.Team1 && IsDeploymentNode(team, node);
    }

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

    public List<Node> GetPath(Node from, Node to)
    {
        return graph.GetPath(from, to);
    }

    public List<Node> GetNodesCloseTo(Node to)
    {
        return graph.Neighbors(to);
    }

    public List<Node> GetNodesInRange(Node center, float rangeInTiles)
    {
        if (center == null || graph == null)
            return new List<Node>();

        float range = Mathf.Max(0f, rangeInTiles) + 0.05f;
        return graph.Nodes
            .Where(node => node != null && Vector3.Distance(node.worldPosition, center.worldPosition) <= range)
            .ToList();
    }

    public Node GetNodeForTile(Tile t)
    {
        if (t != null && nodeByTile.TryGetValue(t, out Node node))
            return node;

        return null;
    }

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

    public void ConfigureBenchTile(Tile tile)
    {
        ConfigureBenchTile(tile, Team.Team1);
    }

    public void ConfigureBenchTile(Tile tile, Team team)
    {
        if (tile == null || configuredBenchTiles.Contains(tile))
            return;

        Color benchColor = team == Team.Team2 ? enemyBenchTileColor : playerBenchTileColor;
        tile.Configure(GetHoverSprite(), benchColor, validHoverColor, invalidHoverColor);
        configuredBenchTiles.Add(tile);
    }

#if UNITY_EDITOR
    [ContextMenu("Build Bench Tiles")]
    public void BuildBenchTiles()
    {
        Transform gridParent = terrainGrid != null && terrainGrid.transform.parent != null
            ? terrainGrid.transform.parent
            : transform;

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
    
    /// グラフを初期化し、タイルマップに基づいてノードとエッジを作成する
    private void InitializeGraph()
    {
        graph = new Graph(); // グラフを新規作成

        for (int i = 0; i < allTiles.Count; i++)
        {
            Vector3 place = allTiles[i].transform.position;
            graph.AddNode(place);
        }

        var allNodes = graph.Nodes; // 全ノードを取得

        // すべてのノードをループし、隣接ノードとのエッジを作成
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

    private void InitializeNodeColumns()
    {
        columnByNode.Clear();

        List<float> columns = new List<float>();
        foreach (Node node in graph.Nodes.OrderBy(node => node.worldPosition.x))
        {
            if (!columns.Any(x => Mathf.Abs(x - node.worldPosition.x) <= 0.05f))
                columns.Add(node.worldPosition.x);
        }

        for (int i = 0; i < graph.Nodes.Count; i++)
            columnByNode[graph.Nodes[i]] = GetClosestColumn(columns, graph.Nodes[i].worldPosition.x);
    }

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

    private void ConfigureBenchTilesInScene()
    {
        ConfigureBenchTileParent(GameObject.Find("Grid/BenchLeft"), Team.Team1);
        ConfigureBenchTileParent(GameObject.Find("Grid/BenchRight"), Team.Team2);
    }

    private void ConfigureBenchTileParent(GameObject benchParent, Team team)
    {
        if (benchParent == null)
            return;

        Tile[] benchTiles = benchParent.GetComponentsInChildren<Tile>(true);
        for (int i = 0; i < benchTiles.Length; i++)
            ConfigureBenchTile(benchTiles[i], team);
    }

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

    private int GetColumnIndex(Node node)
    {
        if (node != null && columnByNode.TryGetValue(node, out int column))
            return column;

        return -1;
    }

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

    public int fromIndex = 0;
    public int toIndex = 0;


    /// Unity のエディタ上でグラフを可視化するために Gizmos を描画する
    private void OnDrawGizmos()
    {
        if (graph == null) // グラフが存在しない場合は何もしない
            return;

        var allEdges = graph.Edges;
        if (allEdges != null && allEdges.Count > 0)
        {
            foreach(Edge e in allEdges)
            {
                Gizmos.DrawLine(e.from.worldPosition, e.to.worldPosition); // ノード間のエッジを線で描画
            }
        }

        var allNodes = graph.Nodes;
        if (allNodes != null)
        {
            foreach (Node n in allNodes)
            {
                Gizmos.color = n.IsOccupied ? Color.red : Color.green; // 占有されているノードは赤、それ以外は緑
                Gizmos.DrawSphere(n.worldPosition, 0.1f); // ノードの位置に小さな球を描画
            }
        }


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
