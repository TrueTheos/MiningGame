using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Pathfinder
{
    public static Queue<PathNode> AStar(PathNode start, PathNode goal, Dictionary<Vector2Int, PathNode> nodes)
    {
        var openSet = new List<PathNode> { start };
        var cameFrom = new Dictionary<PathNode, PathNode>();

        // Cost from start along best known path.
        var gScore = new Dictionary<PathNode, float>();
        // Estimated total cost from start to goal through y.
        var fScore = new Dictionary<PathNode, float>();

        // Initialize all nodes in our search area.
        foreach (var node in nodes.Values)
        {
            gScore[node] = float.PositiveInfinity;
            fScore[node] = float.PositiveInfinity;
        }
        gScore[start] = 0f;
        fScore[start] = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            // Get node in openSet with lowest fScore.
            PathNode current = openSet.OrderBy(n => fScore[n]).First();

            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);

            foreach (var conn in current.Connections.Values)
            {
                PathNode neighbor = conn.Target;
                float tentativeGScore = gScore[current] + GetConnectionCost(conn, current);

                if (tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }
        // No path found.
        return null;
    }

    private static float GetConnectionCost(PathNode.PathNodeConnection connection, PathNode startNode)
    {
        PathNode.ConnectionType type = connection.ConnType;
        float distance = Vector2.Distance(startNode.Pos, connection.Target.Pos);

        return type switch
        {
            PathNode.ConnectionType.WALK => 1f * distance,
            PathNode.ConnectionType.FALL => 2f * distance,
            PathNode.ConnectionType.JUMP => 3f * distance,
            _ => 1f * distance,
        };
    }

    private static Queue<PathNode> ReconstructPath(Dictionary<PathNode, PathNode> cameFrom, PathNode current)
    {
        var totalPath = new List<PathNode> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Insert(0, current);
        }

        return new Queue<PathNode>(totalPath);
    }


    private static float Heuristic(PathNode a, PathNode b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }
}
