using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Decoration : CustomBuilding
{
    [SerializeField] private List<Sprite> _art = new();

    private void Start()
    {
        if (_art == null || _art.Count == 0) return;
        SpriteRend.sprite = _art.Random();
    }
}
