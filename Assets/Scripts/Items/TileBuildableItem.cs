using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileBuildableItem : BuildableItem
{
    public TileSO Tile;

    public override void UseOnce()
    {
        Place();
    }

    private void Start()
    {
        Art.sprite = Tile.Art;
    }

    public override void Place()
    {
        if (WorldManager.Instance.GetTileAtMousePos() != null) return;

        WorldManager.Instance.SetTileAtMouse(Tile);
        Inventory.Instance.RemoveOne();
    }
}
