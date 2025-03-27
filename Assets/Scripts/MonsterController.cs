using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterController : MonoBehaviour
{
    public static MonsterController Instance;

    private int _margin = 20;
    private int _graphRegenerationThreshold = 5;

    private WorldManager _worldManager;

    private HashSet<Monster> _monsters = new();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _worldManager = WorldManager.Instance;

        _worldManager.OnBlockBreak.AddListener(BlockChanged);
        _worldManager.OnBlockPlace.AddListener(BlockChanged);
    }

    public void RegisterMonster(Monster monster)
    {
        _monsters.Add(monster);
    }

    public void MonsterMoved(Monster monster)
    {
        if (IsMonsterNearGraphBorder(monster))
        {
            RegenerateMonsterGraph(monster);
        }
    }

    public void BlockChanged(int x, int y)
    {
        foreach (var monster in _monsters)
        {
            if (monster != null && monster.gameObject.activeSelf)
            {
                if (IsBlockInMonsterGraphRange(monster, x, y))
                {
                    RegenerateMonsterGraph(monster);
                }
            }
        }
    }

    private bool IsBlockInMonsterGraphRange(Monster monster, int blockX, int blockY)
    {
        int startX = Mathf.Max(0, monster.GridX - _margin);
        int startY = Mathf.Max(0, monster.GridY - _margin);

        int endX = Mathf.Min(_worldManager.WorldWidth, monster.GridX + _margin);
        int endY = Mathf.Min(_worldManager.WorldHeight, monster.GridY + _margin);

        return blockX >= startX && blockX < endX &&
               blockY >= startY && blockY < endY;
    }

    private bool IsMonsterNearGraphBorder(Monster monster)
    {
        int startX = Mathf.Max(0, monster.GridX - _margin);
        int startY = Mathf.Max(0, monster.GridY - _margin);

        int endX = Mathf.Min(_worldManager.WorldWidth, monster.GridX + _margin);
        int endY = Mathf.Min(_worldManager.WorldHeight, monster.GridY + _margin);

        // Check distance from current position to graph borders
        int distanceFromLeftBorder = monster.GridX - startX;
        int distanceFromRightBorder = endX - monster.GridX;
        int distanceFromTopBorder = monster.GridY - startY;
        int distanceFromBottomBorder = endY - monster.GridY;

        return distanceFromLeftBorder <= _graphRegenerationThreshold ||
               distanceFromRightBorder <= _graphRegenerationThreshold ||
               distanceFromTopBorder <= _graphRegenerationThreshold ||
               distanceFromBottomBorder <= _graphRegenerationThreshold;
    }

    private void RegenerateMonsterGraph(Monster monster)
    {
        CalculateGraph(monster, monster.FallSearchLimit, monster.JumpSearchRadius);
    }

    public void CalculateGraph(Monster monster, int _fallSearchLimit, int _jumpSearchRadius)
    {
        int startX = Mathf.Max(0, monster.GridX - _margin);
        int startY = Mathf.Max(0, monster.GridY - _margin);

        int endX = Mathf.Min(_worldManager.WorldWidth, monster.GridX + _margin);
        int endY = Mathf.Min(_worldManager.WorldHeight, monster.GridY + _margin);

        //ignore this: todo too many vector creations. Maybe try creating 2d array of size from start to end, and then check if there is a node,
        //make sure to subtract positions when checking index in that array

        Dictionary<Vector2Int, PathNode> _nodes = new();
        Dictionary<Vector2Int, PathNode> _edges = new();


        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if (_worldManager.PathNodes[x, y]) _nodes[new Vector2Int(x, y)] = new PathNode(x, y);
            }
        }

        foreach (var pair in _nodes)
        {
            PathNode node = pair.Value;
            Vector2Int left = node.Pos + Vector2Int.left;
            Vector2Int right = node.Pos + Vector2Int.right;

            if (!_worldManager.IsTileInBounds(left)) continue;
            if (!_worldManager.IsTileInBounds(right)) continue;

            // Add walking connections (horizontal)
            if (_nodes.ContainsKey(left)) node.AddConnection(PathNode.ConnectionType.WALK, _nodes[left]);
            if (_nodes.ContainsKey(right)) node.AddConnection(PathNode.ConnectionType.WALK, _nodes[right]);

            if (!node.Connections.ContainsKey(left))
            {
                if (_nodes.ContainsKey(left)) node.AddConnection(PathNode.ConnectionType.WALK, _nodes[left]);
                else _edges[node.Pos] = node;
            }

            if (!node.Connections.ContainsKey(right))
            {
                if (_nodes.ContainsKey(right)) node.AddConnection(PathNode.ConnectionType.WALK, _nodes[right]);
                else _edges[node.Pos] = node;
            }

            for (int y = left.y; y > left.y - _fallSearchLimit; y--)
            {
                var searchPos = new Vector2Int(left.x, y);
                if (_nodes.ContainsKey(searchPos))
                {
                    node.AddConnection(PathNode.ConnectionType.FALL, _nodes[searchPos]);
                    break;
                }
                else if (_worldManager.IsSolid(searchPos.x, searchPos.y)) break;
            }

            for (int y = right.y; y > right.y - _fallSearchLimit; y--)
            {
                var searchPos = new Vector2Int(right.x, y);
                if (_nodes.ContainsKey(searchPos))
                {
                    node.AddConnection(PathNode.ConnectionType.FALL, _nodes[searchPos]);
                    break;
                }
                else if (_worldManager.IsSolid(searchPos.x, searchPos.y)) break;
            }
        }

        foreach (var pair in _nodes)
        {
            PathNode node = pair.Value;
            for (int x = node.X - _jumpSearchRadius; x < node.X + _jumpSearchRadius; x++)
            {
                for (int y = node.Y - _jumpSearchRadius; y < node.Y + _jumpSearchRadius; y++)
                {
                    if (x == monster.GridX && y == monster.GridY) continue;
                    PathNode targetNode = null;
                    if (_edges.TryGetValue(new Vector2Int(x, y), out targetNode) && !node.Connections.ContainsKey(targetNode.Pos))
                    {
                        bool canMakeJump = CanMakeJump(monster, node.Pos, targetNode.Pos);
                        if (canMakeJump)
                        {
                            node.AddConnection(PathNode.ConnectionType.JUMP, targetNode);
                        }
                    }
                }
            }
        }

        monster._edges = _edges;
        monster._nodes = _nodes;
    }

    private bool CanMakeJump(Monster monster, Vector2 start, Vector2Int target)
    {
        float gravity = Physics2D.gravity.y;
        Vector3 targetPos = target.ToVector3(offset: .5f);

        float deltaX = targetPos.x - start.x;
        float deltaY = targetPos.y - start.y;

        if (Mathf.Abs(deltaX) > monster.MovementSpeed * 1.5f)
            return false;

        if (Mathf.Abs(deltaX) <= 1.5f && Mathf.Abs(deltaY) <= 1.5f)
        {
            float estimatedTime = Mathf.Abs(deltaX) / monster.MovementSpeed;
            float requiredVY = (deltaY - 0.5f * gravity * estimatedTime * estimatedTime) / estimatedTime;

            if (Mathf.Abs(requiredVY) <= monster.JumpPower)
            {
                return true;
            }
            return false;
        }

        const int steps = 20;
        for (int i = 1; i <= steps; i++)
        {
            float vX = (monster.MovementSpeed / steps) * i * Mathf.Sign(deltaX);
            if (vX == 0) continue;

            float time = deltaX / vX;
            if (time <= 0f) continue;

            float vY = (deltaY - 0.5f * gravity * time * time) / time;

            if (vY <= monster.JumpPower && vY > 0)
            {
                if (!CollidesWithObstacle(start, vX, vY, gravity, time))
                    return true;
            }
        }

        return false;
    }

    private bool CollidesWithObstacle(Vector2 start, float vX, float vY, float gravity, float totalTime)
    {
        int resolution = 50;
        for (int i = 0; i <= resolution; i++)
        {
            float t = totalTime * i / resolution;
            float x = start.x + vX * t;
            float y = start.y + vY * t + 0.5f * gravity * t * t;

            int gridX = Mathf.FloorToInt(x);
            int gridY = Mathf.FloorToInt(y);

            if (gridX < 0 || gridX >= _worldManager.WorldWidth || gridY < 0 || gridY >= _worldManager.WorldHeight)
                return true;

            if (_worldManager.IsSolid(gridX, gridY))
                return true;
        }

        return false;
    }
}
