using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

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

public class Monster : MonoBehaviour
{
    private WorldManager _worldManager;

    private PlayerMovement _player;

    private int _margin = 20;
    private int _fallSearchLimit = 7;
    private int _jumpSearchRadius = 5;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _jumpPower;

    private List<PathNode> _currentPath;
    private int _currentPathIndex;
    private bool _isFollowingPath;
    private Vector2 _moveDirection;
    private bool _isJumping;
    [SerializeField] private float _pathNodeReachDistance = 0.5f;
    [SerializeField] private float _groundedCheckDistance = 0.1f;
    [SerializeField] private float _edgeCheckDistance = 0.5f;
    [SerializeField] private LayerMask _groundLayer;

    public bool grounded;

    private Vector2Int _Pos;
    private int _X => _Pos.x;
    private int _Y => _Pos.y;

    private Rigidbody2D _rb;
    private BoxCollider2D _boxCollider;

    private Vector2 _targetJumpPosition;
    private bool _isAttemptingJump;
    private float _jumpStartTime;
    private Vector2Int _lastTargetPos;
    private bool _isFacingRight = true;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _boxCollider = GetComponent<BoxCollider2D>();
    }

    private void Start()
    {
        _worldManager = WorldManager.Instance;
        _player = PlayerMovement.Instance;
        _lastTargetPos = _player.Pos;
        CalculateGraph();
        CalculatePath();
    }

    private void Update()
    {
        grounded = IsGrounded();

        _Pos.x = Mathf.FloorToInt(transform.position.x);
        _Pos.y = Mathf.FloorToInt(transform.position.y);

        if(ShouldRecalculatePath())
        {
            _lastTargetPos = _player.Pos;
            StopFollowingPath();
            CalculatePath();
        }

        if (_isFollowingPath && _currentPath != null && _currentPath.Count > 0)
        {
            FollowPath();
        }

        Flip();
    }

    private bool ShouldRecalculatePath()
    {
        if (_player.Pos != _lastTargetPos && _currentPath != null && _currentPath.Count > 0)
        {
            float distanceToPlayer = Vector2Int.Distance(_Pos, _player.Pos);
            float distanceToLastNode = Vector2Int.Distance(_Pos, _currentPath.Last().Pos);
            if (distanceToPlayer < distanceToLastNode) return true;
        }

        return false;
    }

    private void Flip()
    {
        if ((_rb.velocity.x > 0.1f && !_isFacingRight) || (_rb.velocity.x < -0.1f && _isFacingRight))
        {
            _isFacingRight = !_isFacingRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }

    private bool IsGrounded()
    {
        Vector2 bottomCenter = new Vector2(_boxCollider.bounds.center.x, _boxCollider.bounds.min.y);

        return Physics2D.OverlapCircle(bottomCenter, 0.2f, _groundLayer);
    }

    Dictionary<Vector2Int, PathNode> nodes = new();
    Dictionary<Vector2Int, PathNode> edges = new();

    public void CalculatePath()
    {
        PathNode startNode = nodes.ContainsKey(_Pos) ? nodes[_Pos] : GetClosestNode(_Pos);

        Vector2Int targetPosInt = _player.Pos;
        PathNode goal = nodes.ContainsKey(targetPosInt) ? nodes[targetPosInt] : GetClosestNode(targetPosInt);

        if (startNode != null && goal != null)
        {
            _currentPath = AStar(startNode, goal);
            if (_currentPath != null && _currentPath.Count > 0)
            {
                StopFollowingPath();
                _currentPathIndex = 0;
                _isFollowingPath = true;
                _isJumping = false;
                Debug.Log("Path found with " + _currentPath.Count + " nodes.");
                // Start moving along the path (1 node per second).
            }
            else
            {
                Debug.Log("No path found.");
            }
        }
        else
        {
            Debug.Log("Start or target node could not be determined.");
        }
    }

    private void CalculateGraph()
    {
        int startX = Mathf.Max(0, _X - _margin);
        int startY = Mathf.Max(0, _Y - _margin);

        int endX = Mathf.Min(_worldManager.WorldWidth, _X + _margin);
        int endY = Mathf.Min(_worldManager.WorldHeight, _Y + _margin);

        //todo too many vector creations. Maybe try creating 2d array of size from start to end, and then check if there is a node,
        //make sure to subtract positions when checking index in that array

        nodes = new();
        edges = new();

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if (_worldManager.PathNodes[x, y]) nodes[new Vector2Int(x, y)] = new PathNode(x, y);
            }
        }

        foreach (var pair in nodes)
        {
            PathNode node = pair.Value;
            Vector2Int left = node.Pos + Vector2Int.left;
            Vector2Int right = node.Pos + Vector2Int.right;
            if (!node.Connections.ContainsKey(left))
            {
                if (nodes.ContainsKey(left)) node.AddConnection(PathNode.ConnectionType.WALK, nodes[left]);
                else edges[node.Pos] = node;
            }

            if (!node.Connections.ContainsKey(right))
            {
                if (nodes.ContainsKey(right)) node.AddConnection(PathNode.ConnectionType.WALK, nodes[right]);
                else edges[node.Pos] = node;
            }

            for (int y = left.y; y > left.y - _fallSearchLimit; y--)
            {
                var searchPos = new Vector2Int(left.x, y);
                if (nodes.ContainsKey(searchPos))
                {
                    node.AddConnection(PathNode.ConnectionType.FALL, nodes[searchPos]);
                    break;
                }
                else if (_worldManager.IsSolid(searchPos.x, searchPos.y)) break;
            }

            for (int y = right.y; y > right.y - _fallSearchLimit; y--)
            {
                var searchPos = new Vector2Int(right.x, y);
                if (nodes.ContainsKey(searchPos))
                {
                    node.AddConnection(PathNode.ConnectionType.FALL, nodes[searchPos]);
                    break;
                }
                else if (_worldManager.IsSolid(searchPos.x, searchPos.y)) break;
            }   
        }
        foreach (var pair in nodes)
        {
            PathNode node = pair.Value;
            for (int x = node.X - _jumpSearchRadius; x < node.X + _jumpSearchRadius; x++)
            {
                for (int y = node.Y - _jumpSearchRadius; y < node.Y + _jumpSearchRadius; y++)
                {
                    if (x == _X && y == _Y) continue;
                    PathNode targetNode = null;
                    if (edges.TryGetValue(new Vector2Int(x, y), out targetNode) && !node.Connections.ContainsKey(targetNode.Pos))
                    {
                        bool canMakeJump = CanMakeJump(node.Pos, targetNode.Pos);
                        if (canMakeJump)
                        {
                            node.AddConnection(PathNode.ConnectionType.JUMP, targetNode);
                        }
                    }
                }
            }
        }
    }

    private bool CanMakeJump(Vector2 start, Vector2Int target)
    {
        float gravity = Physics2D.gravity.y;
        Vector3 targetPos = target.ToVector3(offset: .5f);

        float deltaX = targetPos.x - start.x;
        float deltaY = targetPos.y - start.y;

        // Early exit if the jump is too far horizontally
        if (Mathf.Abs(deltaX) > _moveSpeed * 1.5f) // Allow slightly longer jumps
            return false;

        // For very short jumps (1 tile), use simplified logic
        if (Mathf.Abs(deltaX) <= 1.5f && Mathf.Abs(deltaY) <= 1.5f)
        {
            float estimatedTime = Mathf.Abs(deltaX) / _moveSpeed;
            float requiredVY = (deltaY - 0.5f * gravity * estimatedTime * estimatedTime) / estimatedTime;

            if (Mathf.Abs(requiredVY) <= _jumpPower)
            {
                return !CollidesWithObstacle(start, _moveSpeed * Mathf.Sign(deltaX), requiredVY, gravity, estimatedTime);
            }
            return false;
        }

        // For longer jumps, test multiple trajectories
        const int steps = 20;
        for (int i = 1; i <= steps; i++)
        {
            float vX = (_moveSpeed / steps) * i * Mathf.Sign(deltaX);
            if (vX == 0) continue;

            float time = deltaX / vX;
            if (time <= 0f) continue;

            // Calculate required initial vertical velocity
            float vY = (deltaY - 0.5f * gravity * time * time) / time;

            if (vY <= _jumpPower && vY > 0) // Ensure positive initial vertical velocity
            {
                if (!CollidesWithObstacle(start, vX, vY, gravity, time))
                    return true;
            }
        }

        return false;
    }

    private bool CollidesWithObstacle(Vector2 start, float vX, float vY, float gravity, float totalTime)
    {
        int resolution = 50; // Większa wartość - dokładniejsza kolizja
        for (int i = 0; i <= resolution; i++)
        {
            float t = totalTime * i / resolution;
            float x = start.x + vX * t;
            float y = start.y + vY * t + 0.5f * gravity * t * t;

            int gridX = Mathf.FloorToInt(x);
            int gridY = Mathf.FloorToInt(y);

            if (gridX < 0 || gridX >= _worldManager.WorldWidth || gridY < 0 || gridY >= _worldManager.WorldHeight)
                return true; // Poza planszą traktujemy jako kolizję

            if (_worldManager.IsSolid(gridX, gridY))
                return true; // Trafiliśmy na przeszkodę
        }

        return false;
    }

    private List<PathNode> AStar(PathNode start, PathNode goal)
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
                float tentativeGScore = gScore[current] + GetConnectionCost(conn.ConnType);

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

    private float Heuristic(PathNode a, PathNode b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    private float GetConnectionCost(PathNode.ConnectionType type)
    {
        switch (type)
        {
            case PathNode.ConnectionType.WALK: return 1f;
            case PathNode.ConnectionType.FALL: return 1f;
            case PathNode.ConnectionType.JUMP: return 5f;
            default: return 1f;
        }
    }

    private List<PathNode> ReconstructPath(Dictionary<PathNode, PathNode> cameFrom, PathNode current)
    {
        var totalPath = new List<PathNode> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Insert(0, current);
        }
        return totalPath;
    }

    private PathNode GetClosestNode(Vector2Int pos)
    {
        PathNode closest = null;
        float bestDist = float.PositiveInfinity;

        foreach (var node in nodes.Values)
        {
            float dist = (node.Pos - pos).magnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = node;
            }
        }
        return closest;
    }

    private void Retry()
    {
        StopFollowingPath();
        CalculateGraph();
        CalculatePath();
    }
    
    private void FollowPath()
    {
        if (_currentPath == null || _currentPath.Count == 0 || _currentPathIndex >= _currentPath.Count)
        {
            StopFollowingPath();
            return;
        }

        PathNode currentTarget = _currentPath[_currentPathIndex];
        Vector2Int targetPosition = currentTarget.Pos;
        Vector2 currentPosition = transform.position;

        // Update move direction
        _moveDirection = (targetPosition - currentPosition).normalized;

        // Check if we've reached the current node
        if (Vector2.Distance(currentPosition, targetPosition.ToVector3(offset: .5f)) < _pathNodeReachDistance)
        {
            _currentPathIndex++;
            if (_currentPathIndex >= _currentPath.Count)
            {
                StopFollowingPath();
                return;
            }
            currentTarget = _currentPath[_currentPathIndex];
            targetPosition = currentTarget.Pos;
        }

        // Get the connection type to the next node
        PathNode.PathNodeConnection connection = GetConnection(_currentPathIndex - 1, _currentPathIndex);
        if (connection == null)
        {
            Retry();
            return;
        }
        _moveDirection = (targetPosition - currentPosition).normalized;
        switch (connection.ConnType)
        {
            case PathNode.ConnectionType.WALK:
                HandleWalking(targetPosition);
                break;
            case PathNode.ConnectionType.FALL:
                // Only initiate fall if we're at the edge
                if (!IsGrounded() || ShouldStartFalling(targetPosition))
                {
                    //HandleFalling(targetPosition);
                    HandleWalking(targetPosition);
                }
                else
                {
                    HandleWalking(targetPosition);
                }
                break;
            case PathNode.ConnectionType.JUMP:
                HandleJumping(targetPosition, connection.JumpPower);
                break;
        }
    }

    /*private void HandleFalling(Vector2 targetPosition)
    {
        float direction = Mathf.Sign(targetPosition.x + .5f - transform.position.x);
        _rb.velocity = new Vector2(direction * _moveSpeed * 0.5f, _rb.velocity.y);
    }*/

    private bool ShouldStartFalling(Vector2 targetPosition)
    {
        float direction = Mathf.Sign(targetPosition.x + .5f - transform.position.x);
        Vector2 edgeCheck = new Vector2(transform.position.x + direction * _edgeCheckDistance, transform.position.y);
        RaycastHit2D groundAhead = Physics2D.Raycast(edgeCheck, Vector2.down, _groundedCheckDistance, _groundLayer);

        if (!groundAhead.collider)
        {
            Vector2Int nextPos = new Vector2Int(
                Mathf.FloorToInt(targetPosition.x),
                Mathf.FloorToInt(targetPosition.y)
            );
            return nextPos.y < Mathf.FloorToInt(transform.position.y);
        }
        return false;
    }

    private void StopFollowingPath()
    {
        _isFollowingPath = false;
        _isJumping = false;
        _rb.velocity = Vector2.zero;      
    }

    private void HandleWalking(Vector2 targetPosition)
    {
        float direction = Mathf.Sign(targetPosition.x + .5f - transform.position.x);
        if (Mathf.Abs(targetPosition.x + 0.5f - transform.position.x) > 0.1f)
        {
            _rb.velocity = new Vector2(direction * _moveSpeed, _rb.velocity.y);
        }

        /*float direction = Mathf.Sign(targetPosition.x + .5f - transform.position.x);

        Vector2 edgeCheck = new Vector2(transform.position.x + direction * _edgeCheckDistance, transform.position.y);
        RaycastHit2D groundAhead = Physics2D.Raycast(edgeCheck, Vector2.down, _groundedCheckDistance, _groundLayer);

        if (!groundAhead.collider && _currentPathIndex < _currentPath.Count)
        {
            PathNode nextNode = _currentPath[_currentPathIndex];
            if (nextNode.Y < Mathf.FloorToInt(transform.position.y))
            {
                _rb.velocity = new Vector2(direction * _moveSpeed * 0.5f, _rb.velocity.y);
                return;
            }
        }

        if (Mathf.Abs(targetPosition.x + 0.5f - transform.position.x) > 0.1f)
        {
            _rb.velocity = new Vector2(direction * _moveSpeed, _rb.velocity.y);
        }
        else
        {
            _rb.velocity = new Vector2(0, _rb.velocity.y);
        }*/
    }

    private void HandleJumping(Vector2 targetPosition, float jumpPower)
    {
        if (IsGrounded() && !_isJumping)
        {
            // Initialize jump
            _targetJumpPosition = targetPosition;
            _isJumping = true;
            _isAttemptingJump = true;
            _jumpStartTime = Time.time;

            // Initial vertical velocity
            float dx = targetPosition.x + 0.5f - transform.position.x;
            float dy = targetPosition.y + 0.5f - transform.position.y;
            float gravity = Physics2D.gravity.y * _rb.gravityScale;

            // Time to reach target horizontally
            float timeToTarget = Mathf.Abs(dx) / _moveSpeed;

            // Calculate required initial velocity for the jump
            float initialJumpVelocity = (dy - (0.5f * gravity * timeToTarget * timeToTarget)) / timeToTarget;

            // Clamp the jump velocity to our maximum jump power
            initialJumpVelocity = Mathf.Min(Mathf.Max(initialJumpVelocity, 0), _jumpPower);

            _rb.velocity = new Vector2(_rb.velocity.x, initialJumpVelocity);
        }
        else if (_isJumping && _isAttemptingJump)
        {
            float dx = _targetJumpPosition.x + 0.5f - transform.position.x;
            float direction = Mathf.Sign(dx);

            // Reduce horizontal speed during jump for better control
            float jumpMovementSpeed = _moveSpeed * 0.8f;

            // Check for walls
            Vector2 rayStart = transform.position;
            RaycastHit2D hit = Physics2D.Raycast(rayStart, new Vector2(direction, 0),
                _edgeCheckDistance, _groundLayer);

            if (hit.collider == null)
            {
                _rb.velocity = new Vector2(direction * jumpMovementSpeed, _rb.velocity.y);
            }
            else
            {
                _rb.velocity = new Vector2(0, _rb.velocity.y);
            }

            // Check if we've reached the target or if the jump is taking too long
            if (Vector2.Distance(transform.position, _targetJumpPosition + Vector2.one * 0.5f) < _pathNodeReachDistance ||
                Time.time - _jumpStartTime > 2.0f)
            {
                _isAttemptingJump = false;
            }
        }
        else if (IsGrounded())
        {
            _isJumping = false;
            _isAttemptingJump = false;
            if (Vector2.Distance(transform.position, _targetJumpPosition + Vector2.one * 0.5f) > _pathNodeReachDistance)
            {
                Retry();
            }
        }
    }

    private PathNode.PathNodeConnection GetConnection(int fromIndex, int toIndex)
    {
        if (fromIndex < 0)
        {
            // If we're at the start, create a virtual WALK connection
            Vector2Int currentPos = new Vector2Int(_X, _Y);
            PathNode _toNode = _currentPath[toIndex];

            // Only allow walking to adjacent nodes at the same height or one below
            if (Mathf.Abs(_toNode.X - _X) <= 1 &&
                (_toNode.Y == _Y || _toNode.Y == _Y - 1))
            {
                return new PathNode.PathNodeConnection(
                    _toNode.Y == _Y ? PathNode.ConnectionType.WALK : PathNode.ConnectionType.FALL,
                    _toNode,
                    -1f
                );
            }
            return null;
        }

        // Normal connection checking
        if (toIndex >= _currentPath.Count) return null;

        PathNode fromNode = _currentPath[fromIndex];
        PathNode toNode = _currentPath[toIndex];

        if (fromNode.Connections.TryGetValue(toNode.Pos, out var connection))
        {
            return connection;
        }

        return null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Reset jumping state when monster hits the ground
        if (IsGrounded())
        {
            _isJumping = false;
        }
    }
}