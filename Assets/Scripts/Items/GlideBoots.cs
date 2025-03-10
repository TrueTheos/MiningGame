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
        _player.RemoveModifier(_fallingGravityModifier.ID);
        _particleSystem.Stop();
    }

    protected override void _Tick()
    {
        if (_player.Movement.IsFalling && Input.GetKey(KeyCode.Space))
        {
            _player.AddModifier(_fallingGravityModifier);
            _particleSystem.Play();
        }
        else
        {
            _player.RemoveModifier(_fallingGravityModifier.ID);
            _particleSystem.Stop();
        }
    }
}
