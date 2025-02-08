using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlideBoots : EquipableItem
{
    [SerializeField] private StatModifier _fallingGravityModifier;

    public override void Unequip()
    {
        base.Unequip();
        _playerMovement.RemoveModifier(_fallingGravityModifier.ID);
    }

    protected override void _Tick()
    {
        if (_playerMovement.IsFalling && Input.GetKey(KeyCode.Space))
        {
            _playerMovement.AddModifier(_fallingGravityModifier);
        }
        else
        {
            _playerMovement.RemoveModifier(_fallingGravityModifier.ID);
        }
    }
}
