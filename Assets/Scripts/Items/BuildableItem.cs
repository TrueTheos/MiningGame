using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BuildableItem : Item
{
    public SpriteRenderer Art;

    public abstract void Place();
}
