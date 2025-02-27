using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AtikiMonster : Monster
{
    private WorldManager _worldManager;
    private Rigidbody2D _rb;
    private BoxCollider2D _boxCollider;
    private PlayerMovement _player;
    private Animator _animator;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _jumpPower = 22f;
    [SerializeField] private float _pathNodeReachDistance = 0.5f;
    [SerializeField] private float _groundedCheckDistance = 0.1f;
    [SerializeField] private float _edgeCheckDistance = 0.5f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _targetDetectionRange;
    [SerializeField] private LayerMask _detectionLayer;

    [Header("Wander Settings")]
    [SerializeField] private float _enrageOtherMonstersRadius;
    [SerializeField] private int _wanderSearchNodeRadius;
    [SerializeField] private Vector2 _wanderChangeTargetCooldown;
    private float _lastWanderTargetChange = 0f;
    private float _currentWanderCooldown;
    private bool _enraged = false;

    public enum MovementState { Idle, FollowingPath, Jumping }

    [Header("Debug")]
    [SerializeField] private MovementState _state = MovementState.Idle;
    [SerializeField] private bool _isPlayerVisible;
    [SerializeField] private bool _reachedTarget;

    private float _pathRecalcCooldown = 0.5f;
    private float _lastPathCalcTime = 0f;
    private Vector2Int _currentTargetPos;

    private List<PathNode> _currentPath;
    private PathNode _currentTargetNode;
    private int _currentPathIndex;
    private bool _isFollowingPath;

    private Vector2 _targetJumpPosition;
    private float _jumpStartTime;

    private int _margin = 20;
    private int _fallSearchLimit = 7;
    private int _jumpSearchRadius = 5;

    private Vector2Int _Pos;
    private int _X => _Pos.x;
    private int _Y => _Pos.y;
    private bool _isFacingRight = true;

    Dictionary<Vector2Int, PathNode> nodes = new();
    Dictionary<Vector2Int, PathNode> edges = new();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _boxCollider = GetComponent<BoxCollider2D>();
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        _Pos.x = Mathf.FloorToInt(transform.position.x);
        _Pos.y = Mathf.FloorToInt(transform.position.y);
        _worldManager = WorldManager.Instance;
        _player = PlayerMovement.Instance;
        _currentTargetPos = Vector2Int.zero;
        _currentWanderCooldown = _wanderChangeTargetCooldown.Random();
        CalculateGraph();
    }

    public void Enrage()
    {
        _enraged = true;
    }

    private void Update()
    {
        _Pos.x = Mathf.FloorToInt(transform.position.x);
        _Pos.y = Mathf.FloorToInt(transform.position.y);
        _isPlayerVisible = CheckTargetVisibility();

        if (_enraged)
        {
            if (_state != MovementState.Jumping)
            {
                bool shouldRecalculate = false;

                // Player moved significantly
                if (Vector2Int.Distance(_player.Pos, _currentTargetPos) > 1)
                    shouldRecalculate = true;

                // Regular recalculation interval
                if (!ReachedTarget() && Time.time - _lastPathCalcTime > _pathRecalcCooldown)
                    shouldRecalculate = true;

                // Monster is stuck
                if (!ReachedTarget() && _isFollowingPath && _rb.velocity.magnitude < 0.1f && Time.time - _lastPathCalcTime > 0.2f)
                    shouldRecalculate = true;

                if (shouldRecalculate)
                {
                    _currentTargetPos = _player.Pos;
                    CalculateGraph();
                    FindPath();

                    _lastPathCalcTime = Time.time;
                }
            }
        }
        else
        {
            if(Time.time - _currentWanderCooldown > _lastWanderTargetChange)
            {
                _lastWanderTargetChange = Time.time;
                _currentWanderCooldown = _wanderChangeTargetCooldown.Random();

                var freeCells = _worldManager.GetFreeCellsInCircle(new Vector2Int(_X, _Y), _wanderSearchNodeRadius);

                if(freeCells != null && freeCells.Count > 0)
                {
                    freeCells = freeCells.Where(x => nodes.ContainsKey(x)).ToList();

                    _currentTargetPos = freeCells.Random();
                    CalculateGraph();
                    FindPath();
                }
            }
        }

        if(_state != MovementState.Jumping) Flip();
    }

    public bool ReachedTarget()
    {
        _reachedTarget = Vector2.Distance(transform.position, _currentTargetPos.ToVector3(offset:.5f)) < _pathNodeReachDistance;
        return _reachedTarget;
    }

    private void FixedUpdate()
    {
        if (_isFollowingPath && _currentPath != null && _currentPath.Count > 0)
        {
            FollowPath();
        }

        _animator.SetFloat("horizontal", Mathf.Abs(_rb.velocity.x));
    }

    private bool CheckTargetVisibility()
    {
        Vector2 directionToPlayer = (_player.transform.position - transform.position);
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer >= _targetDetectionRange) return false;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToPlayer.normalized, _targetDetectionRange, _detectionLayer);

        if (hit.collider != null && hit.collider.gameObject.name == _player.gameObject.name)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private void Flip()
    {
        if ((_rb.velocity.x > 0.1f && !_isFacingRight) ||
            (_rb.velocity.x < -0.1f && _isFacingRight))
        {
            _isFacingRight = !_isFacingRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }

    private bool IsGrounded()
    {
        Vector2 bottomLeft = new Vector2(_boxCollider.bounds.min.x + 0.1f, _boxCollider.bounds.min.y);
        Vector2 bottomRight = new Vector2(_boxCollider.bounds.max.x - 0.1f, _boxCollider.bounds.min.y);
        Vector2 bottomCenter = new Vector2(_boxCollider.bounds.center.x, _boxCollider.bounds.min.y);

        return Physics2D.Raycast(bottomLeft, Vector2.down, _groundedCheckDistance, _groundLayer) ||
               Physics2D.Raycast(bottomCenter, Vector2.down, _groundedCheckDistance, _groundLayer) ||
               Physics2D.Raycast(bottomRight, Vector2.down, _groundedCheckDistance, _groundLayer);
    }

    public bool FindPath()
    {
        PathNode startNode = nodes.ContainsKey(_Pos) ? nodes[_Pos] : GetClosestNode(_Pos);

        Vector2Int targetPosInt = _currentTargetPos;
        PathNode goal = nodes.ContainsKey(targetPosInt) ? nodes[targetPosInt] : GetClosestNode(targetPosInt);

        if (startNode != null && goal != null)
        {
            _currentPath = Pathfinder.AStar(startNode, goal, nodes);
            if (_currentPath != null && _currentPath.Count > 0)
            {
                _currentPathIndex = 0;
                _isFollowingPath = true;
                _state = MovementState.FollowingPath;
                return true;
            }
            else
            {
                _isFollowingPath = false;
                _state = MovementState.Idle;
                return false;
            }
        }
        else
        {
            _isFollowingPath = false;
            _state = MovementState.Idle;
            return false;
        }
    }

    private void CalculateGraph()
    {
        int startX = Mathf.Max(0, _X - _margin);
        int startY = Mathf.Max(0, _Y - _margin);

        int endX = Mathf.Min(_worldManager.WorldWidth, _X + _margin);
        int endY = Mathf.Min(_worldManager.WorldHeight, _Y + _margin);

        //ignore this: todo too many vector creations. Maybe try creating 2d array of size from start to end, and then check if there is a node,
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

            // Add walking connections (horizontal)
            if (nodes.ContainsKey(left)) node.AddConnection(PathNode.ConnectionType.WALK, nodes[left]);
            if (nodes.ContainsKey(right)) node.AddConnection(PathNode.ConnectionType.WALK, nodes[right]);

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

        if (Mathf.Abs(deltaX) > _moveSpeed * 1.5f)
            return false;

        if (Mathf.Abs(deltaX) <= 1.5f && Mathf.Abs(deltaY) <= 1.5f)
        {
            float estimatedTime = Mathf.Abs(deltaX) / _moveSpeed;
            float requiredVY = (deltaY - 0.5f * gravity * estimatedTime * estimatedTime) / estimatedTime;

            if (Mathf.Abs(requiredVY) <= _jumpPower)
            {
                return true;
            }
            return false;
        }

        const int steps = 20;
        for (int i = 1; i <= steps; i++)
        {
            float vX = (_moveSpeed / steps) * i * Mathf.Sign(deltaX);
            if (vX == 0) continue;

            float time = deltaX / vX;
            if (time <= 0f) continue;

            float vY = (deltaY - 0.5f * gravity * time * time) / time;

            if (vY <= _jumpPower && vY > 0)
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
    
    private void FollowPath()
    {
        if ((ReachedTarget() && _state != MovementState.Jumping) || _currentPath == null || _currentPath.Count == 0 || _currentPathIndex >= _currentPath.Count)
        {
            _currentTargetNode = null;
            _isFollowingPath = false;
            _state = MovementState.Idle;
            _rb.velocity = Vector2.zero;
            return;
        }

        _currentTargetNode = _currentPath[_currentPathIndex];
        Vector2Int targetPosition = _currentTargetNode.Pos;
        Vector2 currentPosition = transform.position;

        float distToTarget = Vector2.Distance(currentPosition, targetPosition + Vector2.one * 0.5f);

        // Check if we've reached the target
        if (distToTarget < _pathNodeReachDistance)
        {
            _currentPathIndex++;
            if (_currentPathIndex >= _currentPath.Count)
            {
                _isFollowingPath = false;
                _state = MovementState.Idle;
                return;
            }
            _currentTargetNode = _currentPath[_currentPathIndex];
            targetPosition = _currentTargetNode.Pos;
        }

        // Get connection between current position and next node
        PathNode.PathNodeConnection connection = null;

        if (_currentPathIndex > 0)
        {
            connection = GetConnection(_currentPathIndex - 1, _currentPathIndex);
        }
        else
        {
            // For the first node, determine connection type based on position
            float heightDiff = targetPosition.y - transform.position.y;
            if (heightDiff > 0.5f)
            {
                connection = new PathNode.PathNodeConnection(
                    PathNode.ConnectionType.JUMP,
                    _currentTargetNode,
                    _jumpPower
                );
            }
            else
            {
                connection = new PathNode.PathNodeConnection(
                    heightDiff < -0.5f ? PathNode.ConnectionType.FALL : PathNode.ConnectionType.WALK,
                    _currentTargetNode,
                    -1f
                );
            }
        }

        if (connection == null)
        {
            FindPath();
            return;
        }

        // Act based on connection type
        switch (connection.ConnType)
        {
            case PathNode.ConnectionType.WALK:
                HandleWalking(targetPosition);
                break;
            case PathNode.ConnectionType.FALL:
                HandleWalking(targetPosition);
                break;
            case PathNode.ConnectionType.JUMP:
                HandleJumping(targetPosition);
                break;
            default:
                break;
        }
    }

    private void HandleWalking(Vector2 targetPosition)
    {
        float direction = Mathf.Sign(targetPosition.x + 0.5f - transform.position.x);
        if (Mathf.Abs(targetPosition.x + 0.5f - transform.position.x) > 0.1f)
        {
            _rb.velocity = new Vector2(direction * _moveSpeed, _rb.velocity.y);
        }
    }

    private void HandleJumping(Vector2 targetPosition)
    {
        Vector2 targetPosWithOffset = targetPosition + Vector2.one * 0.5f;

        if (IsGrounded() && _state != MovementState.Jumping)
        {
            // Start jump with simple physics
            _state = MovementState.Jumping;
            _targetJumpPosition = targetPosition;
            _jumpStartTime = Time.time;

            // Use direct jump force - simpler approach
            _rb.velocity = new Vector2(
                Mathf.Sign(targetPosition.x + 0.5f - transform.position.x) * _moveSpeed,
                _jumpPower
            );

            Flip();
        }
        else if (_state == MovementState.Jumping)
        {
            // Maintain horizontal movement during jump
            float distanceToTarget = targetPosWithOffset.x - transform.position.x;
            float direction = Mathf.Sign(distanceToTarget);

            // For short jumps, maintain full speed to ensure we reach the target
            float speedMultiplier = (Vector2.Distance(transform.position, targetPosWithOffset) < 1.5f)
                ? 1.0f
                : Mathf.Min(1.0f, Mathf.Abs(distanceToTarget) / 0.5f);

            _rb.velocity = new Vector2(direction * _moveSpeed * speedMultiplier, _rb.velocity.y);

            // Check if we've reached the target or if jump has timed out
            if (Vector2.Distance(transform.position, targetPosWithOffset) < _pathNodeReachDistance ||
                Time.time - _jumpStartTime > 2.0f ||
                (IsGrounded() && Time.time - _jumpStartTime > 0.2f))
            {
                _state = MovementState.FollowingPath;
            }
        }
    }

    private PathNode.PathNodeConnection GetConnection(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || toIndex >= _currentPath.Count)
        {
            return null;
        }

        PathNode fromNode = _currentPath[fromIndex];
        PathNode toNode = _currentPath[toIndex];

        if (fromNode.Connections.TryGetValue(toNode.Pos, out var connection))
        {
            return connection;
        }

        // If no direct connection is found, create a default one based on position
        if (toNode.Y > fromNode.Y)
        {
            return new PathNode.PathNodeConnection(PathNode.ConnectionType.JUMP, toNode, _jumpPower);
        }
        else if (toNode.Y < fromNode.Y)
        {
            return new PathNode.PathNodeConnection(PathNode.ConnectionType.FALL, toNode, -1f);
        }
        else
        {
            return new PathNode.PathNodeConnection(PathNode.ConnectionType.WALK, toNode, -1f);
        }
    }

    public override void OnTakeDamage()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, _enrageOtherMonstersRadius, gameObject.layer);
        List<GameObject> monsters = new List<GameObject>();

        foreach (Collider2D collider in colliders)
        {
            if (collider.gameObject != gameObject)
            {
                monsters.Add(collider.gameObject);
            }
        }

        foreach (var monster in monsters)
        {
            if (monster.TryGetComponent<AtikiMonster>(out AtikiMonster atiki))
            {
                atiki.Enrage();
            }
        }
    }

    private void OnDrawGizmos()
    {      
        if (_currentPath != null && _currentPath.Count > 0)
        {
            Gizmos.color = Color.white;
            for (int i = 0; i < _currentPath.Count - 1; i++)
            {
                Vector3 start = _currentPath[i].Pos.ToVector3(offset: .5f);
                Vector3 end = _currentPath[i + 1].Pos.ToVector3(offset: .5f);
                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireSphere(start, .2f);

                // Draw connection type
                if (i < _currentPath.Count - 1)
                {
                    PathNode.PathNodeConnection conn = GetConnection(i, i + 1);
                    if (conn != null)
                    {
                        switch (conn.ConnType)
                        {
                            case PathNode.ConnectionType.WALK:
                                Gizmos.color = Color.green;
                                break;
                            case PathNode.ConnectionType.FALL:
                                Gizmos.color = Color.yellow;
                                break;
                            case PathNode.ConnectionType.JUMP:
                                Gizmos.color = Color.red;
                                break;
                        }
                        Gizmos.DrawLine(start, end);
                    }
                }
            }

            // Draw the target node
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_currentPath[^1].Pos.ToVector3(offset: .5f), .2f);
        }

        // Draw the current target
        if (_currentTargetNode != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_currentTargetNode.Pos.ToVector3(offset: .5f), .3f);
        }

        // Draw ground check rays
        Gizmos.color = Color.cyan;
        if (_boxCollider != null)
        {
            Vector2 bottomLeft = new Vector2(_boxCollider.bounds.min.x + 0.1f, _boxCollider.bounds.min.y);
            Vector2 bottomRight = new Vector2(_boxCollider.bounds.max.x - 0.1f, _boxCollider.bounds.min.y);
            Vector2 bottomCenter = new Vector2(_boxCollider.bounds.center.x, _boxCollider.bounds.min.y);

            Gizmos.DrawRay(bottomLeft, Vector2.down * _groundedCheckDistance);
            Gizmos.DrawRay(bottomCenter, Vector2.down * _groundedCheckDistance);
            Gizmos.DrawRay(bottomRight, Vector2.down * _groundedCheckDistance);
        }

        // Draw current state
        Gizmos.color = Color.white;
        if (Application.isPlaying)
        {
            GUI.Label(new Rect(Screen.width - 200, 10, 190, 20),
                     $"State: {_state}, Following: {_isFollowingPath}");
            GUI.Label(new Rect(Screen.width - 200, 30, 190, 20),
                     $"Velocity: {_rb.velocity}");
        }
    }
}