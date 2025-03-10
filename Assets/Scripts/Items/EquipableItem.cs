using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EquipableItem : Item
{
    public bool Equipped { get; private set; }

    protected Player _player;

    public virtual void Equip()
    {
        _player = Player.Instance;
        gameObject.SetActive(true);
        Equipped = true;
    }

    public virtual void Unequip()
    {
        gameObject.SetActive(false);
        Equipped = false;
    }

    public void ToggleEquip()
    {
        if (Equipped) Unequip();
        else Equip();
    }

    public void Tick()
    {
        if (Equipped) _Tick();
    }

    protected abstract void _Tick();
}
