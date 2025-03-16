using Cinemachine;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Player : Entity
{
    public static Player Instance;

    private PlayerMovement _playerMovement;
    public PlayerMovement Movement => _playerMovement;

    public Transform Hand => _playerMovement.Hand;
    public Vector2Int Pos => _playerMovement.Pos;

    public bool IsFacingRight => _playerMovement.IsFacingRight;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        _playerMovement = GetComponent<PlayerMovement>();

        CurrentHealth = _maxHealth;
    }

    public void AddModifier(StatModifier modifier)
    {
        Movement.StatModifiers[modifier.ID] = modifier;
    }

    public void RemoveModifier(Guid modifierId)
    {
        Movement.StatModifiers.Remove(modifierId);
    }

    public override void OnTakeDamage(DamageSource sourceType)
    {
        if (sourceType == DamageSource.Fall)
        {
            List<float> forces = new() { -.2f, .2f};
            GetComponent<CinemachineImpulseSource>().GenerateImpulseWithVelocity(new(forces.Random(), forces.Random(), 0));
            AudioManager.Instance.PlayFallDamage();
        }
        Blink();
    }
}
