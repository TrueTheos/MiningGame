using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomBuilding : PlacableItem
{
    public bool Solid;
    public int Durability;

    public override void Holding()
    {
        base.Holding();
        var pos = MousePosToTilePos();
        bool result = WorldManager.Instance.TryPlace(pos.x, pos.y, this);

        if(result) Inventory.Instance.RemoveOne();
    }
}
