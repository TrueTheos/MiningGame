using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Progress;

public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement Instance { get; private set; }

    public int X { get; private set; }
    public int Y { get; private set; }

    [SerializeField] private float _speed;
    [SerializeField] private float _jumpingPower;
    [SerializeField] private float _climbSpeed;
    [SerializeField] private float _webSlowdownFactor;

    private Rigidbody2D _rb;
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private Animator _animator;
    [SerializeField] private LayerMask _groundLayer;

    private float _horizontal;
    private float _vertical;
    private bool _isFacingRight = true;
    private bool _isJumping;
    private float _coyoteTime = 0.1f;
    private float _coyoteTimeCounter;

    #region Statuses
    [HideInInspector] public bool InWeb;
    #endregion

    private float _jumpBufferTime = 0.2f;
    private float _jumpBufferCounter;

    private bool _isOnClimbable;
    private bool _isClimbing;

    private float _originalGravityScale;

    private float _currentSpeed => GetCurrentSpeed();
    private float _currentGravity => GetCurrentGravity();
    private float _currentJumpSpeed => GetCurrentJumpSpeed();

    private void Awake()
    {
        Instance = this;
        _rb = GetComponent<Rigidbody2D>();
        _originalGravityScale = _rb.gravityScale;
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

        if (InWeb && _rb.velocity.y < 0) res *= _webSlowdownFactor;

        return res;
    }

    private float GetCurrentJumpSpeed()
    {
        float res = _jumpingPower;

        if (InWeb) res *= _webSlowdownFactor * 1.5f;

        return res;
    }

    void Update()
    {
        X = Mathf.FloorToInt(transform.position.x);
        Y = Mathf.FloorToInt(transform.position.y);

        _horizontal = Input.GetAxisRaw("Horizontal");
        _vertical = Input.GetAxisRaw("Vertical");

        if (_isOnClimbable && Mathf.Abs(_vertical) > 0f)
        {
            _isClimbing = true;
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
    }

    private void FixedUpdate()
    {
        if (_isClimbing)
        {
            _rb.gravityScale = 0f;
            float currentClimbSpeed = InWeb ? _climbSpeed * _webSlowdownFactor : _climbSpeed;
            _rb.velocity = new Vector2(_horizontal * _currentSpeed, _vertical * currentClimbSpeed);
        }
        else
        {
            _rb.gravityScale = _currentGravity;

            _rb.velocity = new Vector2(_horizontal * _currentSpeed, _rb.velocity.y);
        }

        _animator.SetFloat("Movement", Mathf.Abs(_rb.velocity.x));
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var pickup = collision.gameObject.GetComponent<PickupableItem>();
        if (pickup != null)
        {
            pickup.PickUp();
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.tag == "Climbable")
        {
            _isOnClimbable = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Climbable")
        {
            _isOnClimbable = false;
            _isClimbing = false;
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
}