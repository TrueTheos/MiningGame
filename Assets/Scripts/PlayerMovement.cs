using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using static UnityEditor.Progress;

public enum StatType { Speed, JumpPower, GravityScale}

[Serializable]
public struct StatModifier
{
    public Guid ID;
    public StatType Type;
    public float Value;

    public StatModifier(StatType type, float value)
    {
        Type = type;
        Value = value;
        ID = Guid.NewGuid();
    }
}

public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement Instance { get; private set; }

    public int X { get; private set; }
    public int Y { get; private set; }

    public Vector2Int Pos => new Vector2Int(X, Y);

    [SerializeField] private float _speed;
    [SerializeField] private float _jumpingPower;
    [SerializeField] private float _climbSpeed;
    [SerializeField] private float _webSlowdownFactor;
    [SerializeField] private float _jumpBufferTime = 0.2f;
    [SerializeField] private float _coyoteTime = 0.1f;
    [SerializeField] private float _footstepDistanceThreshold = 1.0f;

    [Header("Attraction Settings")]
    [Tooltip("The radius within which pickups are attracted to the player.")]
    [SerializeField] private float _attractionRadius = 5f;
    [Tooltip("Maximum speed that a pickupable object can be attracted at.")]
    [SerializeField] private float _maxPickupSpeed = 5f;
    [Tooltip("Layer mask to identify pickupable objects.")]
    [SerializeField] private LayerMask _pickupableLayer;

    [Header("Components")]
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private Animator _animator;
    [SerializeField] private LayerMask _groundLayer;
    private Rigidbody2D _rb;

    #region States
    private bool _wasFalling = false;
    public bool IsFalling => _IsFalling();
    public bool IsOnClimbable { get; private set; }
    public bool IsClimbing  { get; private set; }
    #endregion

    #region Statuses
    [HideInInspector] public bool InWeb;
    #endregion

    #region Events
    public UnityEvent StartFallingEvent;
    public UnityEvent StopFallingEvent;
    #endregion

    public Dictionary<Guid, StatModifier> StatModifiers = new();

    private float _currentSpeed => GetCurrentSpeed();
    private float _currentGravity => GetCurrentGravity();
    private float _currentJumpSpeed => GetCurrentJumpSpeed();

    private float _horizontal;
    private float _vertical;
    private bool _isFacingRight = true;
    private bool _isJumping;
    private float _coyoteTimeCounter;
    private float _jumpBufferCounter;
    private float _originalGravityScale;
    private Vector3 _lastFootstepPosition;

    private void Awake()
    {
        Instance = this;
        _rb = GetComponent<Rigidbody2D>();
        _originalGravityScale = _rb.gravityScale;
    }

    private bool _IsFalling()
    {
        return !IsClimbing && _rb.velocity.y < 0;
    }

    private float GetCurrentSpeed()
    {
        float res = _speed;

        if (InWeb) res *= _webSlowdownFactor;

        return res;
    }

    private float GetCurrentGravity()
    {
        float res = _originalGravityScale;

        if (IsClimbing) return 0f;
        if (InWeb && _rb.velocity.y < 0) res *= _webSlowdownFactor;
        res *= GetStatModifiers(StatType.GravityScale);

        return res;
    }

    private float GetCurrentJumpSpeed()
    {
        float res = _jumpingPower;

        if (InWeb) res *= _webSlowdownFactor * 1.5f;
        res *= GetStatModifiers(StatType.JumpPower);

        return res;
    }

    public void AddModifier(StatModifier modifier)
    {
        StatModifiers[modifier.ID] = modifier;
    }

    public void RemoveModifier(Guid modifierId)
    {
        StatModifiers.Remove(modifierId);
    }

    public float GetStatModifiers(StatType type)
    {
        float res = 1f;

        foreach (StatModifier modifier in StatModifiers.Values.Where(x => x.Type == type))
        {
            res *= modifier.Value;
        }

        return res;
    }

    private void Update()
    {
        X = Mathf.FloorToInt(transform.position.x);
        Y = Mathf.FloorToInt(transform.position.y);

        _horizontal = Input.GetAxisRaw("Horizontal");
        _vertical = Input.GetAxisRaw("Vertical");

        if (IsFalling && !_wasFalling) StartFallingEvent?.Invoke();
        else if(!IsFalling && _wasFalling) StopFallingEvent?.Invoke();
        _wasFalling = IsFalling;

        _rb.gravityScale = _currentGravity;

        if (IsOnClimbable && Mathf.Abs(_vertical) > 0f)
        {
            IsClimbing = true;
        }

        if (IsGrounded())
        {
            _coyoteTimeCounter = _coyoteTime;
        }
        else
        {
            _coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            _jumpBufferCounter = _jumpBufferTime;
        }
        else
        {
            _jumpBufferCounter -= Time.deltaTime;
        }

        if (_coyoteTimeCounter > 0f && _jumpBufferCounter > 0f && !_isJumping)
        {
            _rb.velocity = new Vector2(_rb.velocity.x, _currentJumpSpeed);

            _jumpBufferCounter = 0f;

            StartCoroutine(JumpCooldown());
        }

        if (Input.GetButtonUp("Jump") && _rb.velocity.y > 0f)
        {
            _rb.velocity = new Vector2(_rb.velocity.x, _rb.velocity.y * 0.5f);

            _coyoteTimeCounter = 0f;
        }

        Flip();
        UpdateFootsteps();
    }

    private void UpdateFootsteps()
    {
        float distanceTraveled = Vector3.Distance(transform.position, _lastFootstepPosition);

        if (distanceTraveled >= _footstepDistanceThreshold)
        {
            TileSO tileData = WorldManager.Instance.WorldData[X, Y - 1];

            if (tileData != null)
            {
                if (tileData.FootstepSounds != null && tileData.FootstepSounds.Count > 0)
                {
                    AudioManager.Instance.Play(tileData.FootstepSounds.Random());
                }
                _lastFootstepPosition = transform.position;
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsClimbing)
        {
            float currentClimbSpeed = InWeb ? _climbSpeed * _webSlowdownFactor : _climbSpeed;
            _rb.velocity = new Vector2(_horizontal * _currentSpeed, _vertical * currentClimbSpeed);
        }
        else
        {
            _rb.velocity = new Vector2(_horizontal * _currentSpeed, _rb.velocity.y);
        }

        _animator.SetFloat("Movement", Mathf.Abs(_rb.velocity.x));

        Collider2D[] pickupsInRange = Physics2D.OverlapCircleAll(transform.position, _attractionRadius, _pickupableLayer);

        foreach (Collider2D pickupCollider in pickupsInRange)
        {
            Rigidbody2D pickupRb = pickupCollider.GetComponent<Rigidbody2D>();
            if (pickupRb != null)
            {
                float verticalDistance = Mathf.Abs(transform.position.y - pickupCollider.transform.position.y);

                if (verticalDistance > .4f)
                    continue;

                float horizontalDistance = transform.position.x - pickupCollider.transform.position.x;
                float absHorizontalDistance = Mathf.Abs(horizontalDistance);

                if (absHorizontalDistance > 0.01f)
                {
                    float directionX = Mathf.Sign(horizontalDistance);

                    float speed = Mathf.Lerp(0, _maxPickupSpeed, 1 - (absHorizontalDistance / _attractionRadius));

                    pickupRb.velocity = new Vector2(directionX * speed, pickupRb.velocity.y);
                }
                else
                {
                    pickupRb.velocity = new Vector2(0f, pickupRb.velocity.y);
                }
            }
        }
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(_groundCheck.position, 0.2f, _groundLayer);
    }

    private IEnumerator JumpCooldown()
    {
        _isJumping = true;
        yield return new WaitForSeconds(0.4f);
        _isJumping = false;
    }

    private void Flip()
    {
        if (_isFacingRight && _horizontal < 0f || !_isFacingRight && _horizontal > 0f)
        {
            _isFacingRight = !_isFacingRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.tag == "Climbable")
        {
            IsOnClimbable = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Climbable")
        {
            IsOnClimbable = false;
            IsClimbing = false;
        }
    }
}

[Serializable]
public class ItemAmount
{
    public Item Item;
    public int Amount;

    public ItemAmount(Item item, int amount)
    {
        Item = item;
        Amount = amount;
    }

    public bool IsEmpty() => Item == null;
}