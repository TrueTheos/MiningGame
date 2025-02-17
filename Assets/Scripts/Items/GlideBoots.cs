using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlideBoots : EquipableItem
{
    [SerializeField] private StatModifier _fallingGravityModifier;
    [SerializeField] private ParticleSystem _particleSystem;

    public override void Unequip()
    {
        base.Unequip();
        _playerMovement.RemoveModifier(_fallingGravityModifier.ID);
        _particleSystem.Stop();
    }

    protected override void _Tick()
    {
        if (_playerMovement.IsFalling && Input.GetKey(KeyCode.Space))
        {
            _playerMovement.AddModifier(_fallingGravityModifier);
            _particleSystem.Play();
        }
        else
        {
            _playerMovement.RemoveModifier(_fallingGravityModifier.ID);
            _particleSystem.Stop();
        }
    }
}
