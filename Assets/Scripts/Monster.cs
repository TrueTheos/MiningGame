using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Monster : Entity, IChunkObject
{
    [HideInInspector] public abstract MonsterType Type { get; }

    public abstract float MovementSpeed { get; protected set; }
    public abstract float JumpPower { get; protected set; }

    [SerializeField] protected bool _damageOnCollision;
    [SerializeField] protected int _damage;

    public abstract MonsterState _currentState { get; set; }
    public abstract MonsterIdleState _idleState { get; set; }
    public abstract MonsterJumpingState _jumpState { get; set; }
    public abstract MonsterFollowingPathState _followPathState { get; set; }

    public Vector2Int GridPos;

    public int GridX => GridPos.x;
    public int GridY => GridPos.y;

    protected Player _player;
    protected Rigidbody2D _rb;

    protected bool _isFacingRight = true;

    public float JumpStartTime { get; protected set; }

    #region Path
    [SerializeField] private float _pathNodeReachDistance = 0.5f;
    public float PathNodeReachDistance => _pathNodeReachDistance;
    protected Dictionary<Vector2Int, PathNode> _nodes = new();
    protected Dictionary<Vector2Int, PathNode> _edges = new();
    protected float _pathRecalcCooldown = 0.5f;
    protected float _lastPathCalcTime = 0f;
    protected Vector2Int _currentTargetPos = Vector2Int.zero;
    public Queue<PathNode> CurrentPath { get; protected set; }
    public PathNode CurrentTargetNode { get; protected set; }
    public int CurrentPathIndex { get; protected set; }
    protected bool _isFollowingPath;
    #endregion

    [Header("Debug")]
    [SerializeField] private bool _reachedTarget;

    public Vector2 Position
    {
        get
        {
            if (this == null || transform == null) return Vector2.zero;
            return transform.position;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        UpdatePosition();
        _rb = GetComponent<Rigidbody2D>();
        CurrentHealth = _maxHealth;
    }

    protected virtual void Start()
    {
        _player = Player.Instance;
    }

    protected void UpdatePosition()
    {
        GridPos = new Vector2Int(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y));
    }

    public Vector2Int GetPlayerPosition()
    {
        return _player != null ? _player.Pos : Vector2Int.zero;
    }

    public abstract bool IsGrounded();
    public abstract void PerformJump(Vector2 targetPosition);
    public abstract void MaintainJumpMovement(Vector2 targetPosition);
    public abstract bool ShouldRecalculatePath();

    public override void OnTakeDamage(DamageSource sourceType)
    {
        base.OnTakeDamage(sourceType);
        AudioManager.Instance.PlayMonsterDamage();
    }

    public void Flip()
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

    public void SetCurrentTargetNode(PathNode node)
    {
        CurrentTargetNode = node;
    }

    public void UpdatePathCalculationTime()
    {
        _lastPathCalcTime = Time.time;
    }

    public override void OnDie()
    {
        base.OnDie();
        Destroy(gameObject);
    }

    public void IncrementPathIndex()
    {
        CurrentPathIndex++;
    }

    public bool ReachedTarget()
    {
        _reachedTarget = Vector2.Distance(transform.position, _currentTargetPos.ToVector3(offset: .5f)) < _pathNodeReachDistance;
        return _reachedTarget;
    }

    public void SetTargetPosition(Vector2Int targetPos)
    {
        _currentTargetPos = targetPos;
    }

    private PathNode GetClosestNode(Vector2Int pos)
    {
        PathNode closest = null;
        float bestDist = float.PositiveInfinity;

        foreach (var node in _nodes.Values)
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

    public bool FindPath()
    {
        if (_currentTargetPos == Vector2.zero) return false;
        PathNode startNode = _nodes.ContainsKey(GridPos) ? _nodes[GridPos] : GetClosestNode(GridPos);

        Vector2Int targetPosInt = _currentTargetPos;
        PathNode goal = _nodes.ContainsKey(targetPosInt) ? _nodes[targetPosInt] : GetClosestNode(targetPosInt);

        if (startNode != null && goal != null)
        {
            CurrentPath = Pathfinder.AStar(startNode, goal, _nodes);
            if (CurrentPath != null && CurrentPath.Count > 0)
            {
                CurrentPathIndex = 0;
                _isFollowingPath = true;
                return true;
            }
            else
            {
                _isFollowingPath = false;
                return false;
            }
        }
        else
        {
            _isFollowingPath = false;
            return false;
        }
    }

    public void ChangeState(MonsterState newState)
    {
        if (_currentState != null)
        {
            _currentState.Exit();
        }

        _currentState = newState;
        _currentState?.Enter();
    }

    public PathNode.PathNodeConnection GetConnection(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || toIndex >= CurrentPath.Count)
        {
            return null;
        }

        PathNode fromNode = CurrentPath.ElementAt(fromIndex);
        PathNode toNode = CurrentPath.ElementAt(toIndex);

        if (fromNode.Connections.TryGetValue(toNode.Pos, out var connection))
        {
            return connection;
        }

        // If no direct connection is found, create a default one based on position
        if (toNode.Y > fromNode.Y)
        {
            return new PathNode.PathNodeConnection(PathNode.ConnectionType.JUMP, toNode, JumpPower);
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

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!_damageOnCollision) return;
        if(collision.gameObject.CompareTag("Player"))
        {
            _player.TakeDamage(_damage, DamageSource.Default, transform);
        }
    }
}
