using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Graph
{
    private List<Node> nodes;
    private List<Edge> edges;
    public List<Node> Nodes => nodes;
    public List<Edge> Edges => edges;

    public Graph()
    {
        nodes = new List<Node>(); // ノードのリストを初期化
        edges = new List<Edge>(); // エッジのリストを初期化
    }

    /// <summary>
    /// グラフに新しいノード（タイル）を追加する
    /// </summary>
    /// <param name="worldPosition">ノードのワールド座標</param>
    public void AddNode(Vector3 worldPosition)
    {
        nodes.Add(new Node(nodes.Count, worldPosition)); // ノードを追加
    }

    /// <summary>
    /// 2つのノードの間にエッジ（接続）を追加する
    /// </summary>
    /// <param name="from">接続元のノード</param>
    /// <param name="to">接続先のノード</param>
    public void AddEdge(Node from, Node to)
    {
        edges.Add(new Edge(from, to, 1)); // 重み1のエッジを追加
    }

    /// <summary>
    /// 2つのノードが接続されているか（隣接しているか）を判定する
    /// </summary>
    /// <param name="from">チェックするノード1</param>
    /// <param name="to">チェックするノード2</param>
    /// <returns>接続されていれば true、そうでなければ false</returns>
    public bool Adjacent(Node from, Node to)
    {
        foreach (Edge e in edges)
        {
            if (e.from == from && e.to == to)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 指定したノードの隣接ノード（接続されているノード）を取得する
    /// </summary>
    /// <param name="of">対象のノード</param>
    /// <returns>隣接するノードのリスト</returns>
    public List<Node> Neighbors(Node of)
    {
        List<Node> result = new List<Node>();

        foreach (Edge e in edges)
        {
            if (e.from == of)
            {
                result.Add(e.to);
            }
        }

        return result;
    }

    /// <summary>
    /// 2つのノード間の移動コスト（距離）を取得する
    /// </summary>
    /// <param name="from">開始ノード</param>
    /// <param name="to">目的ノード</param>
    /// <returns>移動コスト（エッジの重み）。移動できない場合は無限大</returns>
    public float Distance(Node from, Node to)
    {
        foreach (Edge e in edges)
        {
            if (e.from == from && e.to == to)
            {
                return e.GetWeight();
            }
        }

        return Mathf.Infinity; // 接続がない場合は無限大のコストを返す
    }

    public List<Node> GetPath(Node start, Node end)
    {
        List<Node> path = new List<Node>();

        if(start == null || end == null)
        {
            Debug.LogError("Start or end node is null in GetPath method.");
            return path; // スタートまたはエンドがnullの場合は空のリストを返す
        }

        if(start == end)
        {
            path.Add(start);
            return path;
        }

        List<Node> openList = new List<Node>();
        Dictionary<Node, Node> previous = new Dictionary<Node, Node>();
        Dictionary<Node, float> distances = new Dictionary<Node, float>();

        for(int i = 0; i < nodes.Count; i++)
        {
            openList.Add(nodes[i]);
            distances.Add(nodes[i], float.PositiveInfinity);
        }

        distances[start] = 0f;

        while(openList.Count > 0)
        {
            openList = openList.OrderBy(x => distances[x]).ToList();
            Node current = openList[0];
            openList.Remove(current);

            if(current == end)
            {
                while(previous.ContainsKey(current))
                {
                    path.Insert(0, current);
                    current = previous[current];
                }

                path.Insert(0, current);
                break;
            }

            foreach(Node neighbor in Neighbors(current))
            {
                if(neighbor == null) continue;

                float distance = Distance(current, neighbor);

                float candidateNewDistance = distances[current] + distance;

                if(candidateNewDistance < distances[neighbor])
                {
                    distances[neighbor] = candidateNewDistance;
                    if (!previous.ContainsKey(neighbor)) // 既に存在する場合は上書きしない
                    {
                        previous[neighbor] = current;
                    }
                    else
                    {
                        previous[neighbor] = current; // またはここで更新するか、必要に応じてロジックを追加
                    }
                }
            }
        }

        return path;
    }
}

/// <summary>
/// グラフのノード（1つのタイル）を表すクラス
/// </summary>
public class Node
{
    public int index; // ノードの一意なインデックス（ID）
    public Vector3 worldPosition; // ノードのワールド座標

    private bool occupied = false; // このノードが現在占有されているかどうか（初期状態は false）
    public bool IsOccupied => occupied; // 外部から占有状態を取得するプロパティ

    /// <summary>
    /// ノードのコンストラクタ
    /// </summary>
    /// <param name="index">ノードのID</param>
    /// <param name="worldPosition">ノードのワールド座標</param>
    public Node(int index, Vector3 worldPosition)
    {
        this.index = index;
        this.worldPosition = worldPosition;
        occupied = false; // 初期状態は空いている
    }

    /// <summary>
    /// ノードの占有状態を変更する
    /// </summary>
    /// <param name="val">true: 占有 / false: 空き</param>
    public void SetOccupied(bool val)
    {
        occupied = val;
    }
}

/// <summary>
/// グラフのエッジ（2つのノードをつなぐ経路）を表すクラス
/// </summary>
public class Edge
{
    public Node from; // エッジの開始ノード
    public Node to;   // エッジの終了ノード

    private float weight; // エッジの重み（移動コスト）

    /// <summary>
    /// エッジのコンストラクタ
    /// </summary>
    /// <param name="from">接続元のノード</param>
    /// <param name="to">接続先のノード</param>
    /// <param name="weight">移動コスト（重み）</param>
    public Edge(Node from, Node to, float weight)
    {
        this.from = from;
        this.to = to;
        this.weight = weight;
    }

    /// <summary>
    /// エッジの移動コストを取得する
    /// </summary>
    /// <returns>移動コスト。もし接続先のノードが占有されていた場合は無限大を返す</returns>
    public float GetWeight()
    {
        if (to.IsOccupied)
        {
            return Mathf.Infinity; // 移動先が占有されている場合、移動不可とする
        }

        return weight;
    }
}
