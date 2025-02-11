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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            PickUp();
        }
    }

    public void PickUp()
    {
        Inventory.Instance.AddItem(Drop);
        AudioManager.Instance.PlayPickup();
        Destroy(Drop.Item.gameObject);
        Destroy(gameObject);
    }
}
