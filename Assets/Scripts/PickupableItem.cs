using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickupableItem : Item
{
    [HideInInspector] public ItemAmount Drop;

    public void Init(ItemAmount item)
    {
        Drop = item;

        SpriteRend.sprite = Drop.Item.SpriteRend.sprite;
        if (Drop.Item is TileBuildableItem tile)
        {
            SpriteRend.sprite = tile.SpriteRend.sprite;
        }

        Destroy(gameObject, 60);
    }

    public void PickUp()
    {
        Inventory.Instance.AddItem(Drop);
        Destroy(Drop.Item.gameObject);
        Destroy(gameObject);
    }
}
