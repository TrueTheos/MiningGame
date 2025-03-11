using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class Spear : ThrowableItem, IWeapon
{
    [SerializeField] private int _damage;
    [SerializeField] private GameObject _inHandVer;
    [SerializeField] private GameObject _thrownVer;
    [SerializeField] private float _minThrowPower;
    [SerializeField] private float _throwChargeSpeed;
    [SerializeField] private float _maxFallDistance = 10f;

    private float _originalGravity;
    private Vector2 _throwOrigin;

    private float _currentThrowPower;

    public int Damage => _damage;

    public override void OnThrow(Vector2 origin, float power)
    {
        AudioManager.Instance.PlaySpearThrow();

        _inHandVer.SetActive(false);
        _thrownVer.SetActive(true);

        _originalGravity = _rb.gravityScale;
        _throwOrigin = origin;
        _currentThrowPower = power;

        StartCoroutine(EnablePlayerCollision());

        _rb.gravityScale = 0f;
    }

    private IEnumerator EnablePlayerCollision()
    {
        yield return new WaitForSeconds(.2f);
        _collider.excludeLayers = _collider.excludeLayers.RemoveMask(Layers.PLAYER_LAYER);
    }

    private void Update()
    {
        if (!IsThrown || _rb.bodyType == RigidbodyType2D.Static) return;

        float fallDistanceThreshold = CalculateFallDistance();

        if (Vector2.Distance(transform.position, _throwOrigin) >= fallDistanceThreshold)
        {
            _rb.gravityScale = _originalGravity;
        }

        transform.right = _rb.velocity;
    }

    private float CalculateFallDistance()
    {
        float normalizedPower = Mathf.InverseLerp(_minThrowPower, _maxThrowPower, _currentThrowPower);
        return Mathf.Lerp(0f, _maxFallDistance, normalizedPower);
    }

    /*private float CalculateGravityScale()
    {
        float normalizedPower = Mathf.InverseLerp(_minThrowPower, _throwPower, _currentThrowPower);
        return Mathf.Lerp(1f, 3f, normalizedPower);
    }*/

    public override void UseOnce()
    {
        base.UseOnce();
        _inHandVer.SetActive(false);
        _thrownVer.SetActive(true);
        _currentThrowPower = _minThrowPower;
    }

    public override void Holding()
    {
        base.Holding();
        Vector2 handPos = _hand.position;
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePos - handPos;
        _hand.right = direction;

        if (_hand.localScale.x < 0)
        {
            direction = -direction;
        }

        _hand.right = direction;

        if (_currentThrowPower < _maxThrowPower)
        {
            if (_currentThrowPower < _maxThrowPower)
            {
                _currentThrowPower = Mathf.Min(_currentThrowPower + _throwChargeSpeed * Time.deltaTime, _maxThrowPower);
            }

            if(_currentThrowPower ==  _maxThrowPower)
            {
                _thrownVer.GetComponentInChildren<SpriteRenderer>().Blink();
            }
        }
    }

    public override void EndUse()
    {
        base.EndUse();
        _hand.right = Vector2.right;
        _inHandVer.SetActive(true);
        _thrownVer.SetActive(false);
        Throw(Mathf.Max(_minThrowPower, _currentThrowPower));
        _currentThrowPower = _minThrowPower;
    }

    public override void CollisionEnter(Collision2D collision)
    {
        var rotationOnHit = transform.rotation;
        if (collision.gameObject.layer.InMask(Layers.MONSTER_LAYER))
        {
            HitMonster(collision.gameObject.GetComponent<Monster>());
        }
        else if (collision.gameObject.layer.InMask(Layers.GROUND_LAYER))
        {
            _rb.bodyType = RigidbodyType2D.Static;
            _collider.enabled = false;
            transform.rotation = rotationOnHit;
        }
    }

    private void HitMonster(Monster monster)
    {
        float normalizedPower = (_currentThrowPower - _minThrowPower) / (_maxThrowPower - _minThrowPower);

        monster.TakeDamage(Mathf.RoundToInt(Mathf.Lerp(0, Damage, normalizedPower)), DamageSource.Weapon);
        Destroy(gameObject);
    }
}