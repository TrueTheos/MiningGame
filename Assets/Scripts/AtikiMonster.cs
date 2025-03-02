using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AtikiMonster : Monster
{
    private WorldManager _worldManager;
    private BoxCollider2D _boxCollider;

    private Animator _animator;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed;
    public override float MovementSpeed { get => _moveSpeed; protected set { _moveSpeed = value; } }
    [SerializeField] private float _jumpPower;
    public override float JumpPower { get => _jumpPower; protected set { _jumpPower = value; } }
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
    public bool Enraged { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool _isPlayerVisible;

    private int _margin = 20;
    private int _fallSearchLimit = 7;
    private int _jumpSearchRadius = 5;

    #region State Machine
    public override MonsterState _currentState { get; set; }
    public override MonsterIdleState _idleState { get; set; }
    public override MonsterJumpingState _jumpState { get; set; }
    public override MonsterFollowingPathState _followPathState { get; set; }
    public AtikiEnragedState _enragedState { get; private set; }
    public AtikiWanderState _wanderState { get; private set; }
    #endregion

    private Vector2 _targetJumpPosition;

    protected override void Awake()
    {
        base.Awake();
        _boxCollider = GetComponent<BoxCollider2D>();
        _animator = GetComponent<Animator>();

        _idleState = new AtikiIdleState(this);
        _followPathState = new AtikiFollowPathState(this);
        _jumpState = new MonsterJumpingState(this);
        _enragedState = new AtikiEnragedState(this);
        _wanderState = new AtikiWanderState(this);
    }

    protected override void Start()
    {
        base.Start();
        _worldManager = WorldManager.Instance;
        _currentTargetPos = Vector2Int.zero;
        _currentWanderCooldown = _wanderChangeTargetCooldown.Random();
        CalculateGraph();

        ChangeState(_idleState);
    }

    public void Enrage()
    {
        Enraged = true;
        ChangeState(_enragedState);
    }

    private void Update()
    {
        UpdatePosition();
        _isPlayerVisible = CheckTargetVisibility();

        if (_currentState != null)
        {
            _currentState.Update();
        }
    }

    private void FixedUpdate()
    {
        if (_currentState != null)
        {
            _currentState.FixedUpdate();
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

    public override bool IsGrounded()
    {
        Vector2 bottomLeft = new Vector2(_boxCollider.bounds.min.x + 0.1f, _boxCollider.bounds.min.y);
        Vector2 bottomRight = new Vector2(_boxCollider.bounds.max.x - 0.1f, _boxCollider.bounds.min.y);
        Vector2 bottomCenter = new Vector2(_boxCollider.bounds.center.x, _boxCollider.bounds.min.y);

        return Physics2D.Raycast(bottomLeft, Vector2.down, _groundedCheckDistance, _groundLayer) ||
               Physics2D.Raycast(bottomCenter, Vector2.down, _groundedCheckDistance, _groundLayer) ||
               Physics2D.Raycast(bottomRight, Vector2.down, _groundedCheckDistance, _groundLayer);
    }

    private void CalculateGraph()
    {
        int startX = Mathf.Max(0, GridX - _margin);
        int startY = Mathf.Max(0, GridY - _margin);

        int endX = Mathf.Min(_worldManager.WorldWidth, GridX + _margin);
        int endY = Mathf.Min(_worldManager.WorldHeight, GridY + _margin);

        //ignore this: todo too many vector creations. Maybe try creating 2d array of size from start to end, and then check if there is a node,
        //make sure to subtract positions when checking index in that array

        _nodes = new();
        _edges = new();

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
                    if (x == GridX && y == GridY) continue;
                    PathNode targetNode = null;
                    if (_edges.TryGetValue(new Vector2Int(x, y), out targetNode) && !node.Connections.ContainsKey(targetNode.Pos))
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

    public Vector2Int GetTargetPosition()
    {
        return _currentTargetPos;
    }

    public void StartFollowingPath()
    {
        _isFollowingPath = true;
        ChangeState(_followPathState);
    }

    public void StartJumping(Vector2 targetPosition)
    {
        _targetJumpPosition = targetPosition;
        JumpStartTime = Time.time;
        ChangeState(_jumpState);
    }

    public void HandleWalking(Vector2 targetPosition)
    {
        float direction = Mathf.Sign(targetPosition.x + 0.5f - transform.position.x);
        if (Mathf.Abs(targetPosition.x + 0.5f - transform.position.x) > 0.1f)
        {
            _rb.velocity = new Vector2(direction * _moveSpeed, _rb.velocity.y);
        }
    }

    public override void PerformJump(Vector2 targetPosition)
    {
        // Use direct jump force - simpler approach
        _rb.velocity = new Vector2(
            Mathf.Sign(targetPosition.x + 0.5f - transform.position.x) * _moveSpeed,
            _jumpPower
        );
    }

    public override void MaintainJumpMovement(Vector2 targetPosition)
    {
        Vector2 targetPosWithOffset = targetPosition + Vector2.one * 0.5f;
        float distanceToTarget = targetPosWithOffset.x - transform.position.x;
        float direction = Mathf.Sign(distanceToTarget);

        // For short jumps, maintain full speed to ensure we reach the target
        float speedMultiplier = (Vector2.Distance(transform.position, targetPosWithOffset) < 1.5f)
            ? 1.0f
            : Mathf.Min(1.0f, Mathf.Abs(distanceToTarget) / 0.5f);

        _rb.velocity = new Vector2(direction * _moveSpeed * speedMultiplier, _rb.velocity.y);
    }

    public override bool ShouldRecalculatePath()
    {
        if (Enraged)
        {
            // Player moved significantly
            if (Vector2Int.Distance(_player.Pos, _currentTargetPos) > 1)
                return true;

            // Regular recalculation interval
            if (!ReachedTarget() && Time.time - _lastPathCalcTime > _pathRecalcCooldown)
                return true;

            // Monster is stuck
            if (!ReachedTarget() && _isFollowingPath && _rb.velocity.magnitude < 0.1f && Time.time - _lastPathCalcTime > 0.2f)
                return true;
        }

        return false;
    }

    public bool ShouldUpdateWanderTarget()
    {
        return Time.time - _currentWanderCooldown > _lastWanderTargetChange;
    }

    public void UpdateWanderTargetTime()
    {
        _lastWanderTargetChange = Time.time;
        _currentWanderCooldown = _wanderChangeTargetCooldown.Random();
    }

    public List<Vector2Int> GetWanderTargets()
    {
        var freeCells = _worldManager.GetFreeCellsInCircle(GridPos, _wanderSearchNodeRadius);

        if (freeCells != null && freeCells.Count > 0)
        {
            return freeCells.Where(x => _nodes.ContainsKey(x)).ToList();
        }

        return new List<Vector2Int>();
    }

    private void OnDrawGizmos()
    {      
        if (CurrentPath != null && CurrentPath.Count > 0)
        {
            Gizmos.color = Color.white;
            for (int i = 0; i < CurrentPath.Count - 1; i++)
            {
                Vector3 start = CurrentPath.ElementAt(i).Pos.ToVector3(offset: .5f);
                Vector3 end = CurrentPath.ElementAt(i + 1).Pos.ToVector3(offset: .5f);
                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireSphere(start, .2f);

                // Draw connection type
                if (i < CurrentPath.Count - 1)
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
            Gizmos.DrawWireSphere(CurrentPath.Last().Pos.ToVector3(offset: .5f), .2f);
        }

        // Draw the current target
        if (CurrentTargetNode != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CurrentTargetNode.Pos.ToVector3(offset: .5f), .3f);
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
    }
}