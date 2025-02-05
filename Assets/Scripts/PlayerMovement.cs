using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Progress;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed = 8f;
    [SerializeField] private float _jumpingPower = 16f;

    private Rigidbody2D _rb;
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private Animator _animator;
    [SerializeField] private LayerMask _groundLayer;

    private float _horizontal;
    private bool isFacingRight = true;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        _horizontal = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump") && IsGrounded())
        {
            _rb.velocity = new Vector2(_rb.velocity.x, _jumpingPower);
        }

        if (Input.GetButtonUp("Jump") && _rb.velocity.y > 0f)
        {
            _rb.velocity = new Vector2(_rb.velocity.x, _rb.velocity.y * 0.5f);
        }

        Flip();
    }

    private void FixedUpdate()
    {
        _rb.velocity = new Vector2(_horizontal * _speed, _rb.velocity.y);
        _animator.SetFloat("Movement", Mathf.Abs(_rb.velocity.x));
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(_groundCheck.position, 0.2f, _groundLayer);
    }

    private void Flip()
    {
        if (isFacingRight && _horizontal < 0f || !isFacingRight && _horizontal > 0f)
        {
            isFacingRight = !isFacingRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }
}

[Serializable]
public record ItemAmount
{
    public Item Item;
    public int Amount;
}