using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathNode
{
    public enum ConnectionType { WALK, FALL, JUMP };
    public class PathNodeConnection
    {
        public PathNode Target;

        public ConnectionType ConnType;
        public float JumpPower = -1f;

        public PathNodeConnection(ConnectionType connType, PathNode target, float jumpPower)
        {
            ConnType = connType;
            Target = target;
            JumpPower = jumpPower;
        }
    }

    public Vector2Int Pos;
    public int X => Pos.x;
    public int Y => Pos.y;
    public Dictionary<Vector2Int, PathNodeConnection> Connections = new();
    public PathNode(int x, int y)
    {
        Pos = new Vector2Int(x, y);
    }

    public void AddConnection(ConnectionType type, PathNode node, float jumpPower = -1f)
    {
        if (Connections.ContainsKey(node.Pos)) return;
        Connections[node.Pos] = new PathNodeConnection(type, node, jumpPower);
    }
}

