using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Entity
{
    public static Player Instance;

    private PlayerMovement _playerMovement;
    public PlayerMovement Movement => _playerMovement;

    public Transform Hand => _playerMovement.Hand;
    public Vector2Int Pos => _playerMovement.Pos;

    private void Awake()
    {
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

}
