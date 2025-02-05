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
        Place(pos.x, pos.y);
    }

    public override bool CanPlace(int x, int y)
    {
        return WorldManager.Instance.GetTileAtMousePos() == null &&
            WorldManager.Instance.GetBuildingAtMousePos() == null &&
            IsPlacementValid(x, y);
    }

    public override void Place(int x, int y)
    {
        if (!CanPlace(x, y)) return;

        WorldManager.Instance.PlaceBuilding(x, y, this);
        Inventory.Instance.RemoveOne();
    }
}
