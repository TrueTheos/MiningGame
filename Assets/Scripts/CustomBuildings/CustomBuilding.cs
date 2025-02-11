using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomBuilding : PlacableItem
{
    public bool Solid;
    public int Durability;

    public override void UseOnce()
    {
        var pos = MousePosToTilePos();
        bool result = WorldManager.Instance.TryPlace(pos.x, pos.y, this);

        if(result) Inventory.Instance.RemoveOne();
    }

    public override bool CanPlace(int x, int y)
    {
        return WorldManager.Instance.GetTileAtMousePos() == null &&
            WorldManager.Instance.GetBuildingAtMousePos() == null &&
            IsPlacementValid(x, y);
    }
}
