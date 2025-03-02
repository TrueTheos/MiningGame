using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public abstract class Monster : MonoBehaviour, IChunkObject
{
    [SerializeField] private int _maxHealth;
    public int CurrentHealth;

    public abstract float MovementSpeed { get; protected set; }
    public abstract float JumpPower { get; protected set; }

    public abstract MonsterState _currentState { get; set; }
    public abstract MonsterIdleState _idleState { get; set; }
    public abstract MonsterJumpingState _jumpState { get; set; }
    public abstract MonsterFollowingPathState _followPathState { get; set; }

    public Vector2Int GridPos;

    public int GridX => GridPos.x;
    public int GridY => GridPos.y;

    protected PlayerMovement _player;
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
    protected Vector2Int _currentTargetPos;
    public Queue<PathNode> CurrentPath { get; protected set; }
    public PathNode CurrentTargetNode { get; protected set; }
    public int CurrentPathIndex { get; protected set; }
    protected bool _isFollowingPath;
    #endregion

    [Header("Debug")]
    [SerializeField] private bool _reachedTarget;

    public Vector2 Position => transform.position;

    protected virtual void Awake()
    {
        UpdatePosition();
        _rb = GetComponent<Rigidbody2D>();
        CurrentHealth = _maxHealth;
    }

    protected virtual void Start()
    {
        _player = PlayerMovement.Instance;
        ChunkManager.Instance.AddObjectToChunk(this);
    }

    protected void UpdatePosition()
    {
        GridPos = new Vector2Int(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y));
    }

    public Vector2Int GetPlayerPosition()
    {
        return _player.Pos;
    }

    [ContextMenu("Test take damage")]
    public void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
        OnTakeDamage();

        if (CurrentHealth <= 0) Die();
    }

    public virtual void OnTakeDamage() { }

    public virtual void OnDie() { }
    public abstract bool IsGrounded();
    public abstract void PerformJump(Vector2 targetPosition);
    public abstract void MaintainJumpMovement(Vector2 targetPosition);
    public abstract bool ShouldRecalculatePath();

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

    public void Die()
    {
        OnDie();

        Destroy(gameObject);
    }

    public void SetCurrentTargetNode(PathNode node)
    {
        CurrentTargetNode = node;
    }

    public void UpdatePathCalculationTime()
    {
        _lastPathCalcTime = Time.time;
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
        _currentState.Enter();
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
}
