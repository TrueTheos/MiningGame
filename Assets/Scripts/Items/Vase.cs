using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vase : CustomBuilding
{
    [SerializeField] private List<Sprite> _sprites = new();   

    private void Start()
    {
        SpriteRend.sprite = _sprites.Random();
    }

    public override void OnBreak()
    {
        base.OnBreak();
    }
}
