using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : Manager<GridManager>
{  
    public GameObject terrainGrid; // ゲーム内のタイルマップを参照
    protected Graph graph; // グリッドのノードとエッジを管理するグラフ
    protected Dictionary<Team, int> startPositionPerTeam; // 各チームの初期配置位置を保持する辞書

    List<Tile> allTiles = new List<Tile>();

        /// 初期化時に呼び出され、グラフを生成し、各チームの開始位置を設定する
    protected void Awake()
    {
        base.Awake(); // 親クラスの Awake() を呼び出す
        allTiles = terrainGrid.GetComponentsInChildren<Tile>().ToList();
        
        InitializeGraph();
        startPositionPerTeam = new Dictionary<Team, int>();
        startPositionPerTeam.Add(Team.Team1, 0);
        startPositionPerTeam.Add(Team.Team2, graph.Nodes.Count -1);
    }

    public Node GetFreeNode(Team forTeam)
    {
        int startIndex = startPositionPerTeam[forTeam]; // チームの開始位置を取得
        int currentIndex = startIndex;

        // ノードがすでに占有されている場合、次の空いているノードを探す
        while(graph.Nodes[currentIndex].IsOccupied)
        {
            if(startIndex == 0)
            {
                currentIndex++;
                if(currentIndex == graph.Nodes.Count) // すべてのノードが占有されている場合
                    return null;
            }
            else
            {
                currentIndex--;
                if(currentIndex == -1) // すべてのノードが占有されている場合
                    return null;
            }
        }
        return graph.Nodes[currentIndex];
    }

    public List<Node> GetPath(Node from, Node to)
    {
        return graph.GetPath(from, to);
    }

    public List<Node> GetNodesCloseTo(Node to)
    {
        return graph.Neighbors(to);
    }

    public Node GetNodeForTile(Tile t)
    {
        var allNodes = graph.Nodes;

        for (int i = 0; i < allNodes.Count; i++)
        {
            if (t.transform.GetSiblingIndex() == allNodes[i].index)
            {
                return allNodes[i];
            }
        }

        return null;
    }
    
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
