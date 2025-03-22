using Cinemachine;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;
using Random = UnityEngine.Random;

public class Player : Entity
{
    public static Player Instance;

    private PlayerMovement _playerMovement;
    public PlayerMovement Movement => _playerMovement;

    public Transform Hand => _playerMovement.Hand;
    public Vector2Int Pos => _playerMovement.Pos;

    public int Depth => _worldManager.WorldHeight - Pos.y; 
    public int Temperature => 10 + Depth / 10;

    private Rigidbody2D _rb;
    public Rigidbody2D RB => _rb;

    public bool IsFacingRight => _playerMovement.IsFacingRight;

    public int CurrentWarningTemperature = 30;
    public int CurrentDangerousTemperature = 50;

    private WorldManager _worldManager;
    private Collider2D _collider;

    private bool _canTakeDamage = true;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        _rb = GetComponent<Rigidbody2D>();
        _playerMovement = GetComponent<PlayerMovement>();
        _collider = GetComponent<Collider2D>();

        CurrentHealth = _maxHealth;
    }

    private void Start()
    {
        _worldManager = WorldManager.Instance;
    }

    public void AddModifier(StatModifier modifier)
    {
        Movement.StatModifiers[modifier.ID] = modifier;
    }

    public void RemoveModifier(Guid modifierId)
    {
        Movement.StatModifiers.Remove(modifierId);
    }

    public override void TakeDamage(int damage, DamageSource sourceType, Transform sourcePos = null)
    {
        if (sourceType == DamageSource.Fall)
        {
            List<float> forces = new() { -.2f, .2f };
            GetComponent<CinemachineImpulseSource>().GenerateImpulseWithVelocity(new(forces.Random(), forces.Random(), 0));
            AudioManager.Instance.PlayFallDamage();
        }
        else if(sourceType == DamageSource.Default)
        {
            if (!_canTakeDamage) return;
            StartCoroutine(IgnoreMonsterCollision());
            if(sourcePos != null)
            {
                Vector2 knockbackDirection = ((Vector2)transform.position - (Vector2)sourcePos.position).normalized;
                knockbackDirection *= 5f;
                knockbackDirection += Vector2.up * 5f;


                _playerMovement.AddForce(knockbackDirection, 0.25f);
            }
            AudioManager.Instance.PlayMonsterDamage();
        }
       
        base.TakeDamage(damage, sourceType);
    }

    public override void OnTakeDamage(DamageSource sourceType)
    {
        Movement.Anim.SetTrigger("Hurt");
    }

    private IEnumerator IgnoreMonsterCollision()
    {
        _canTakeDamage = false;
        yield return new WaitForSeconds(.75f);
        _canTakeDamage = true;
    }
}
