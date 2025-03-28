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

    [SerializeField] private LayerMask _detectionLayer;

    public Vector2Int GridPos;

    public int GridX => GridPos.x;
    public int GridY => GridPos.y;

    protected Player _player;
    protected Rigidbody2D _rb;
    public Rigidbody2D RB => _rb;

    private Animator _animator;

    protected bool _isFacingRight = true;

    public float JumpStartTime { get; protected set; }

    #region Path
    public Dictionary<Vector2Int, PathNode> _nodes { set; get; } = new();
    public Dictionary<Vector2Int, PathNode> _edges { set; get; } = new();
    public Queue<PathNode> CurrentPath { get; protected set; }
    public int FallSearchLimit { private set; get; } = 7;
    public int JumpSearchRadius { private set; get; } = 5;

    protected bool _grounded;
    #endregion

    [Header("Debug")]
    [SerializeField] private bool _reachedTarget;

    private MonsterController _monsterController;

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
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        CurrentHealth = _maxHealth;
    }

    protected virtual void Start()
    {
        _player = Player.Instance;
        _monsterController = MonsterController.Instance;
     
        UpdateGridPos();
    }

    private void UpdateGridPos()
    {
        var lastPos = GridPos;
        GridPos = new Vector2Int(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y));

        if(lastPos != GridPos)
        {
            _monsterController.MonsterMoved(this);
        }
    }

    protected void UpdateGraph()
    {
        MonsterController.Instance.CalculateGraph(this, FallSearchLimit, JumpSearchRadius);
    }

    private void FixedUpdate()
    {
        UpdateGridPos();
        _animator.SetFloat("horizontal", Mathf.Abs(_rb.velocity.x));
        Flip();
    }

    public abstract bool IsGrounded();

    public override void OnTakeDamage(DamageSource sourceType)
    {
        base.OnTakeDamage(sourceType);
        AudioManager.Instance.PlayMonsterDamage();
    }

    private void Flip()
    {
        if (!_grounded) return;
        Vector3 localScale = transform.localScale;
        if (_rb.velocity.x > .1)
        {
            localScale = transform.localScale;
            localScale.x = 1;
            _isFacingRight = true;
        }
        else if (_rb.velocity.x < -.1)
        {
            localScale = transform.localScale;
            localScale.x = -1;
            _isFacingRight = false;
        }

        transform.localScale = localScale;
    }

    public override void OnDie()
    {
        base.OnDie();
        Destroy(gameObject);
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

    protected bool IsPlayerVisible(float range)
    {
        Vector2 directionToPlayer = (_player.transform.position - transform.position);
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer >= range) return false;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToPlayer.normalized, range, _detectionLayer);

        if (hit.collider != null && hit.collider.gameObject.name == _player.gameObject.name)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool FindPath(Vector2Int target)
    {
        PathNode startNode = _nodes.ContainsKey(GridPos) ? _nodes[GridPos] : GetClosestNode(GridPos);
        PathNode goal = _nodes.ContainsKey(target) ? _nodes[target] : GetClosestNode(target);

        if (startNode != null && goal != null)
        {
            CurrentPath = Pathfinder.AStar(startNode, goal, _nodes);
            if (CurrentPath != null && CurrentPath.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    public Queue<PathNode> GetPath(Vector2Int target)
    {
        PathNode startNode = _nodes.ContainsKey(GridPos) ? _nodes[GridPos] : GetClosestNode(GridPos);
        PathNode goal = _nodes.ContainsKey(target) ? _nodes[target] : GetClosestNode(target);

        if (startNode != null && goal != null)
        {
            return Pathfinder.AStar(startNode, goal, _nodes);

        }
        else
        {
            return null;
        }
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
