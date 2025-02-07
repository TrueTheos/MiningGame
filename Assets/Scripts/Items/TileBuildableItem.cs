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
    }

    public override void UseOnce()
    {
        var pos = MousePosToTilePos();
        Place(pos.x, pos.y);
    }
     
    public override bool CanPlace(int x, int y)
    {
        return WorldManager.Instance.GetTileAtMousePos() == null && WorldManager.Instance.GetBuildingAtMousePos() == null;
    }

    public override void Place(int x, int y)
    {
        if (!CanPlace(x, y)) return;

        WorldManager.Instance.SetTile(x, y, Tile);
        Inventory.Instance.RemoveOne();
    }
}
