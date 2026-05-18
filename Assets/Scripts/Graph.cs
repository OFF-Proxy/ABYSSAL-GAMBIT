using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// 盤面のマス同士のつながりを表す簡単なグラフです。
// Nodeが1マス、Edgeが「このマスからこのマスへ移動できる」という接続を表します。
public class Graph
{
    // Graphが持っている全Nodeと全Edgeです。
    private List<Node> nodes;
    private List<Edge> edges;

    // 外部から読み取り専用でNode/Edge一覧を使えるようにしています。
    public List<Node> Nodes => nodes;
    public List<Edge> Edges => edges;

    // Graphを作る時に、NodeとEdgeの空リストを用意します。
    public Graph()
    {
        // グラフ作成時に、NodeとEdgeを入れる空のリストを用意します。
        nodes = new List<Node>();
        edges = new List<Edge>();
    }

    // グラフに新しいNodeを追加します。
    // worldPositionは、実際にユニットが立つワールド座標です。
    public void AddNode(Vector3 worldPosition)
    {
        nodes.Add(new Node(nodes.Count, worldPosition));
    }

    // 2つのNodeの間にEdgeを追加します。
    // weightは移動コストで、今は全て1マス移動として扱っています。
    public void AddEdge(Node from, Node to)
    {
        edges.Add(new Edge(from, to, 1));
    }

    // fromからtoへ直接移動できるEdgeがあるか確認します。
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

    // 指定Nodeから直接移動できる隣接Nodeを一覧で返します。
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

    // fromからtoへ移動するコストを返します。
    // つながっていない、または移動先が塞がっている場合はInfinityになります。
    public float Distance(Node from, Node to)
    {
        foreach (Edge e in edges)
        {
            if (e.from == from && e.to == to)
            {
                return e.GetWeight();
            }
        }

        return Mathf.Infinity;
    }

    // startからendまでの移動経路を返します。
    // ダイクストラ法に近い考え方で、現在分かっている最短距離を更新しながら道を探します。
    public List<Node> GetPath(Node start, Node end)
    {
        List<Node> path = new List<Node>();

        if(start == null || end == null)
        {
            Debug.LogError("Start or end node is null in GetPath method.");
            return path;
        }

        // 同じNodeを指定された場合は、移動不要なのでそのNodeだけ返します。
        if(start == end)
        {
            path.Add(start);
            return path;
        }

        // openListはこれから調べるNode一覧、previousは最短経路を復元するための親Node記録です。
        List<Node> openList = new List<Node>();
        Dictionary<Node, Node> previous = new Dictionary<Node, Node>();
        Dictionary<Node, float> distances = new Dictionary<Node, float>();

        // 最初は全Nodeの距離を無限大にして、開始Nodeだけ0にします。
        for(int i = 0; i < nodes.Count; i++)
        {
            openList.Add(nodes[i]);
            distances.Add(nodes[i], float.PositiveInfinity);
        }

        distances[start] = 0f;

        while(openList.Count > 0)
        {
            // まだ調べていないNodeの中で、開始地点から一番近いものを取り出します。
            openList = openList.OrderBy(x => distances[x]).ToList();
            Node current = openList[0];
            openList.Remove(current);

            // 目的地に着いたら、previousを逆にたどって経路を作ります。
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

            // 隣接Nodeへ進んだ場合の距離を計算し、より短ければ更新します。
            foreach(Node neighbor in Neighbors(current))
            {
                if(neighbor == null) continue;

                float distance = Distance(current, neighbor);

                float candidateNewDistance = distances[current] + distance;

                if(candidateNewDistance < distances[neighbor])
                {
                    distances[neighbor] = candidateNewDistance;
                    if (!previous.ContainsKey(neighbor))
                    {
                        previous[neighbor] = current;
                    }
                    else
                    {
                        previous[neighbor] = current;
                    }
                }
            }
        }

        return path;
    }
}

// 盤面上の1マスを表すクラスです。
public class Node
{
    // Nodeの通し番号と、Unityシーン上の位置です。
    public int index;
    public Vector3 worldPosition;

    // このNodeにユニットが立っているかどうかです。
    private bool occupied = false;
    public bool IsOccupied => occupied;

    // Nodeを作る時に、番号とワールド座標を保存します。
    public Node(int index, Vector3 worldPosition)
    {
        this.index = index;
        this.worldPosition = worldPosition;
        occupied = false;
    }

    // ユニットが乗った/離れた時に占有状態を変更します。
    public void SetOccupied(bool val)
    {
        occupied = val;
    }
}

// Node同士のつながりを表すクラスです。
public class Edge
{
    // fromからtoへ向かう一方向の接続です。
    public Node from;
    public Node to;

    // 移動コストです。今は基本的に1です。
    private float weight;

    // Edgeを作る時に、始点・終点・移動コストを保存します。
    public Edge(Node from, Node to, float weight)
    {
        this.from = from;
        this.to = to;
        this.weight = weight;
    }

    // このEdgeを通る時のコストを返します。
    // 移動先が占有されている場合は、移動できないようにInfinityを返します。
    public float GetWeight()
    {
        if (to.IsOccupied)
        {
            return Mathf.Infinity;
        }

        return weight;
    }
}
