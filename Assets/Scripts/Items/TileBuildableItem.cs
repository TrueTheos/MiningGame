using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileBuildableItem : PlacableItem
{
    public TileSO Tile;

    private void Start()
    {
        if(Tile != null)
        {
            SpriteRend.sprite = Tile.Art;
        }
    }

    public void Init(TileSO tile)
    {
        Tile = tile;
        SpriteRend.sprite = Tile.Art;
        Name = tile.Name;
    }

    public override void Holding()
    {
        base.Holding();
        var pos = MousePosToTilePos();
        if(WorldManager.Instance.TryPlaceTile(pos.x, pos.y, this))
        {
            Inventory.Instance.RemoveOne();
        }
    }
}
