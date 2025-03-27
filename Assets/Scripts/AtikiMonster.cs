using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;

public class AtikiMonster : Monster
{
    private WorldManager _worldManager;
    private BoxCollider2D _boxCollider;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed;
    public override float MovementSpeed { get => _moveSpeed; protected set { _moveSpeed = value; } }
    [SerializeField] private float _jumpPower;
    public override float JumpPower { get => _jumpPower; protected set { _jumpPower = value; } }
    [SerializeField] private float _groundedCheckDistance = 0.1f;
    [SerializeField] private float _edgeCheckDistance = 0.5f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _targetDetectionRange;

    [Header("Wander Settings")]
    [SerializeField] private float _enrageOtherMonstersRadius;
    [SerializeField] private int _wanderSearchNodeRadius;
    [SerializeField] private Vector2 _wanderChangeTargetCooldown;
    private float _lastWanderTargetChange = 0f;
    private float _currentWanderCooldown;
    public bool Enraged { get; private set; }

    #region State Machine
    public override MonsterType Type => MonsterType.Atiki;
    #endregion

    private Vector2 _targetJumpPosition;

    private enum AtikiState { Idle, Wander, Walk, Jump, Fall, Follow }
    private AtikiState _state;

    protected override void Awake()
    {
        base.Awake();
        _boxCollider = GetComponent<BoxCollider2D>();
    }

    protected override void Start()
    {
        base.Start();
        _damageOnCollision = false; 
        _worldManager = WorldManager.Instance;
        _currentTargetPos = Vector2Int.zero;
        _currentWanderCooldown = _wanderChangeTargetCooldown.Random();
        UpdateGraph();
    }

    public void Update()
    {
        var lastState = _state;

        if (IsGrounded())
        {
            if (!_grounded)
            {
                FindPath();
            }
            _grounded = true;
        }
        else
        {
            _grounded = false;
        }

        switch (_state)
        {
            case AtikiState.Idle:
                IdleState();
                break;
            case AtikiState.Wander:
                WanderState();
                break;
            case AtikiState.Walk:
                WalkState();
                break;
            case AtikiState.Jump:
                JumpState();
                break;
            case AtikiState.Fall:
                FallState();
                break;
            case AtikiState.Follow:
                FollowState();
                break;
        }

        if (lastState == _state && ShouldRecalculatePath())
        {
            if (Enraged)
            {
                SetTargetPosition(Player.Instance.Pos);
                FindPath();
                UpdatePathCalculationTime();
                ChangeState(AtikiState.Follow);
            }
            else
            {
                ChangeState(AtikiState.Wander);
            }
        }
    }

    private void ChangeState(AtikiState newState)
    {
   //     FindPath();
        _state = newState;
    }

    private void IdleState()
    {
        if (Enraged)
        {
            SetTargetPosition(Player.Instance.Pos);
            FindPath();
            ChangeState(AtikiState.Follow);
        }
        else ChangeState(AtikiState.Wander);
    }

    private void FollowState()
    {
        if (ShouldRecalculatePath())
        {
            if (Enraged)
            {
                SetTargetPosition(Player.Instance.Pos);
                FindPath();
                UpdatePathCalculationTime();
            }
            else
            {
                ChangeState(AtikiState.Wander);
                return;
            }
        }

        int currentPathIndex = CurrentPathIndex;

        if (ReachedTarget() || CurrentPath == null || CurrentPath.Count == 0 || CurrentPathIndex >= CurrentPath.Count)
        {
            ChangeState(AtikiState.Idle);
            return;
        }

        PathNode currentTargetNode = CurrentPath.ElementAt(currentPathIndex);
        SetCurrentTargetNode(currentTargetNode);
        Vector2Int targetPosition = currentTargetNode.Pos;

        float distToTarget = Vector2.Distance(transform.position, targetPosition + Vector2.one * 0.5f);

        if (distToTarget < PathNodeReachDistance)
        {
            IncrementPathIndex();
            currentPathIndex = CurrentPathIndex;

            if (currentPathIndex >= CurrentPath.Count)
            {
                ChangeState(AtikiState.Idle);
                return;
            }

            currentTargetNode = CurrentPath.ElementAt(currentPathIndex);
            SetCurrentTargetNode(currentTargetNode);
            targetPosition = currentTargetNode.Pos;
            _currentTargetPos = targetPosition;
        }

        PathNode.PathNodeConnection connection = null;

        if (currentPathIndex > 0)
        {
            connection = GetConnection(currentPathIndex - 1, currentPathIndex);
        }
        else
        {
            float heightDiff = targetPosition.y + .5f - transform.position.y;

            if (heightDiff > 0.5f)
            {
                connection = new PathNode.PathNodeConnection(
                PathNode.ConnectionType.JUMP,
                currentTargetNode,
                    JumpPower
                );
            }
            else
            {
                connection = new PathNode.PathNodeConnection(
                    heightDiff < -0.5f ? PathNode.ConnectionType.FALL : PathNode.ConnectionType.WALK,
                    currentTargetNode,
                    -1f
                );
            }
        }

        if (connection == null)
        {
            FindPath();
            return;
        }

        switch (connection.ConnType)
        {
            case PathNode.ConnectionType.WALK:
                ChangeState(AtikiState.Walk);
                break;
            case PathNode.ConnectionType.FALL:
                ChangeState(AtikiState.Fall);
                break;
            case PathNode.ConnectionType.JUMP:
                PerformJump(targetPosition);
                ChangeState(AtikiState.Jump);
                break;
        }
    }

    private void WanderState()
    {
        if (ShouldUpdateWanderTarget())
        {
            UpdateWanderTargetTime();
            List<Vector2Int> nodes = GetWanderTargets();

            if (nodes == null || nodes.Count == 0)
            {
                ChangeState(AtikiState.Idle);
            }
            else
            {
                SetTargetPosition(GetWanderTargets().Random());
                FindPath();

                ChangeState(AtikiState.Follow);
            }
        }
    }

    private void WalkState()
    {
        if (Enraged)
        {
            if (_player.Pos.y == GridPos.y)
            {
                float dist = Mathf.Abs(transform.position.x - _player.transform.position.x);
                if (dist < 7)
                {
                    float dir = Mathf.Sign(_player.transform.position.x - transform.position.x);

                    float speedMultiplier = Mathf.Min(1.0f, dist / 0.5f);
                    _rb.velocity = new Vector2(dir * MovementSpeed * speedMultiplier, _rb.velocity.y);

                    SetTargetPosition(Player.Instance.Pos);
                    FindPath();
                    UpdatePathCalculationTime();
                    return;
                }
            }
        }

        if (CurrentPath == null || CurrentPath.Count == 0)
        {
            ChangeState(AtikiState.Idle);
            return;
        }

        PathNode currentTargetNode = CurrentPath.ElementAt(CurrentPathIndex);

        float distToTarget = Mathf.Abs(transform.position.x - currentTargetNode.Pos.x + .5f);

        if (distToTarget < PathNodeReachDistance)
        {
            IncrementPathIndex();

            if (CurrentPathIndex >= CurrentPath.Count)
            {
                ChangeState(AtikiState.Idle);
                return;
            }

            currentTargetNode = CurrentPath.ElementAt(CurrentPathIndex);
            distToTarget = Mathf.Abs(transform.position.x - currentTargetNode.Pos.x + .5f);
        }
      
        float direction = Mathf.Sign(currentTargetNode.Pos.x + 0.5f - transform.position.x);

        if (distToTarget > 0.1f)
        {
            float speedMultiplier = Mathf.Min(1.0f, distToTarget / 0.5f);
            _rb.velocity = new Vector2(direction * MovementSpeed * speedMultiplier, _rb.velocity.y);
        }
        else
        {
            _rb.velocity = new Vector2(0, _rb.velocity.y);
        }


        if (_grounded)
        {
            if ((direction > 0) != (_rb.velocity.x > 0) ||
                (direction < 0) != (_rb.velocity.x < 0))
            {
                ChangeState(AtikiState.Idle);
                return;
            }
        }
    }

    private void JumpState()
    {
        Vector2 targetPosWithOffset = _targetJumpPosition + Vector2.one * 0.5f;

        if (Vector2.Distance(transform.position, targetPosWithOffset) < PathNodeReachDistance ||
        Time.time - JumpStartTime > 2.0f ||
            (IsGrounded() && Time.time - JumpStartTime > 0.2f))
        {
            ChangeState(AtikiState.Idle);
        }

        float distanceToTarget = targetPosWithOffset.x - transform.position.x;
        float direction = Mathf.Sign(distanceToTarget);

        // For short jumps, maintain full speed to ensure we reach the target
        float speedMultiplier = (Vector2.Distance(transform.position, targetPosWithOffset) < 1.5f)
            ? 1.0f
            : Mathf.Min(1.0f, Mathf.Abs(distanceToTarget) / 0.5f);

        _rb.velocity = new Vector2(direction * _moveSpeed * speedMultiplier, _rb.velocity.y);
    }

    private void FallState()
    {
        if(CurrentPath == null || CurrentPath.Count == 0)
        {
            ChangeState(AtikiState.Idle);
            return;
        }

        PathNode currentTargetNode = CurrentPath.ElementAt(CurrentPathIndex);

        float distToTarget = Vector2.Distance(transform.position, currentTargetNode.Pos + Vector2.one * 0.5f);

        if (distToTarget < PathNodeReachDistance)
        {
            ChangeState(AtikiState.Follow);
            return;
        }

        float direction = Mathf.Sign(_currentTargetPos.x + 0.5f - transform.position.x);

        _rb.velocity = new Vector2(direction * MovementSpeed, _rb.velocity.y);

        if (_grounded)
        {
            if ((direction > 0) != (_rb.velocity.x > 0) ||
                (direction < 0) != (_rb.velocity.x < 0))
            {
                ChangeState(AtikiState.Idle);
                return;
            }
        }
    }

    public void Enrage()
    {
        if (Enraged) return;
        _damageOnCollision = true;
        Enraged = true;
        SetTargetPosition(Player.Instance.Pos);
        FindPath();
        UpdatePathCalculationTime();

        ChangeState(AtikiState.Follow);
    }

    public void PerformJump(Vector2 targetPosition)
    {
        _targetJumpPosition = targetPosition;
        JumpStartTime = Time.time;

        _rb.velocity = new Vector2(
            Mathf.Sign(targetPosition.x + 0.5f - transform.position.x) * _moveSpeed,
            _jumpPower
        );
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

    public override void OnTakeDamage(DamageSource sourceType)
    {
        base.OnTakeDamage(sourceType);

        Vector2 hitDirection = (transform.position - Player.Instance.transform.position).normalized;
        float knockbackForceX = 5f;
        float knockbackForceY = 5f;

        _rb.AddForce(new Vector2(hitDirection.x * knockbackForceX, knockbackForceY), ForceMode2D.Impulse);

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, _enrageOtherMonstersRadius, gameObject.layer);
        List<GameObject> monsters = new List<GameObject>();

        foreach (Collider2D collider in colliders)
        {
            if (collider.gameObject != gameObject)
            {
                monsters.Add(collider.gameObject);
            }
        }

        Enrage();

        foreach (var monster in monsters)
        {
            if (monster.TryGetComponent<AtikiMonster>(out AtikiMonster atiki))
            {
                atiki.Enrage();
            }
        }
    }

    public override bool ShouldRecalculatePath()
    {
        if (Enraged)
        {
            // Player moved significantly
            if (CurrentPath != null && Vector2Int.Distance(_player.Pos, CurrentPath.Last().Pos) > 1)
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

    private void OnDrawGizmosSelected()
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