using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using Unity.VisualScripting;
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
    [SerializeField] private float _enrageOtherMonstersRadius;

    public bool Enraged { get; private set; }

    #region State Machine
    public override MonsterType Type => MonsterType.Atiki;
    #endregion

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
        UpdateGraph();
    }

    private Queue<PathNode> _previousPath;

    private bool IsPathEndingSimilar(Queue<PathNode> newPath, Queue<PathNode> oldPath)
    {
        if (newPath == null || oldPath == null) return false;

        if(newPath.Count == 0 || oldPath.Count == 0) return false;

        // Get the last few nodes from both paths
        newPath.Dequeue();
        oldPath.Dequeue();
        var newPathEnd = newPath.ToList();
        var oldPathEnd = oldPath.ToList();

        // Compare the ends of the paths
        if (newPathEnd.Count != oldPathEnd.Count) return false;

        for (int i = 0; i < newPathEnd.Count; i++)
        {
            if (newPathEnd[i].Pos != oldPathEnd[i].Pos)
            {
                return false;
            }
        }

        return true;
    }

    private bool jumping;

    private Vector2Int _lastTargetPos;

    private float lastCheck;

    public void Update()
    {
        if (IsGrounded())
        {
            if (!_grounded)
            {
                jumping = false;
                FindPath(_player.Pos);
                _lastTargetPos = _player.Pos;
            }
            _grounded = true;
        }
        else
        {
            _grounded = false;
        }

        /*if(lastCheck + 2f < Time.time)
        {
            lastCheck = Time.time;
            FindPath(_player.Pos);
            _lastTargetPos = _player.Pos;
        }*/

        if (CurrentPath == null || (CurrentPath != null && CurrentPath.Count == 0) || _lastTargetPos == null || Vector2Int.Distance(_lastTargetPos, _player.Pos) > 1f)
        {
            FindPath(_player.Pos);
            _lastTargetPos = _player.Pos;
        }

        if (CurrentPath != null && CurrentPath.Count > 0)
        {
            FollowPath();
        }
    }

    private Vector2 currentGoal;
    private Vector2Int nodePos;

    private void FollowPath()
    {
        if(jumping)
        {
            Jump();
            return;
        }

        if (CurrentPath == null || CurrentPath.Count == 0) return;

        if(_player.Pos.y == GridPos.y && CurrentPath.All(x => x.Pos.y == GridPos.y))
        {
            Walk(_player.transform.position);
            return;
        }


        // Only update goal when significantly far from current goal
        if (GridPos == nodePos && CurrentPath.Count > 0)
        {
            nodePos = CurrentPath.Dequeue().Pos;
            currentGoal = nodePos.ToVector3(offset: .5f);
        }
        else
        {
            if (CurrentPath.Count > 0)
            {
                nodePos = CurrentPath.Peek().Pos;
                currentGoal = nodePos.ToVector3(offset: .5f);
            }
        }

        // More controlled movement between nodes
        float distanceToGoal = Vector2.Distance(transform.position, currentGoal);
        float direction = Mathf.Sign(currentGoal.x - transform.position.x);

        if (nodePos.y > GridPos.y)
        {
            Jump();
        }
        else if (nodePos.y < GridPos.y)
        {
            Fall();
        }
        else
        {
            Walk(currentGoal);
        }
    }

    private void Walk(Vector2 target)
    {
        float distanceToTarget = Mathf.Abs(target.x - transform.position.x);
        float direction = Mathf.Sign(target.x - transform.position.x);

        float speedMultiplier = Mathf.Min(1.0f, distanceToTarget / 0.5f);
        _rb.velocity = new Vector2(direction * MovementSpeed * speedMultiplier, _rb.velocity.y);
    }

    private float jumpDir = 0;

    private void Jump()
    {
        if (_grounded)
        {
            jumping = true;
            float jumpDirection = Mathf.Sign(currentGoal.x - transform.position.x);
            jumpDir = jumpDirection;

            // More controlled jump with consistent horizontal movement
            _rb.velocity = new Vector2(
                jumpDir * MovementSpeed,
                _jumpPower
            );
        }
        else
        {
            // Maintain horizontal momentum during jump
            _rb.velocity = new Vector2(
                jumpDir * MovementSpeed,
                _rb.velocity.y
            );
        }
    }

    private float fallDir = 0;

    private void Fall()
    {
        if(_grounded)
        {
            fallDir = Mathf.Sign(currentGoal.x - transform.position.x);
        }

        _rb.velocity = new Vector2(fallDir * MovementSpeed, _rb.velocity.y);
    }

  
    public void Enrage()
    {
        if (Enraged) return;
        _damageOnCollision = true;
        Enraged = true;
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
    private void OnDrawGizmosSelected()
    {
        foreach(var node in _nodes)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(node.Key.ToVector3(offset:.5f), .2f);
        }

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
        if (currentGoal != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentGoal, .3f);
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