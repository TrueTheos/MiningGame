using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Layer1Generation : WorldLayer
{
    [SerializeField] private TileSO _baseTile;

    protected override IEnumerator GenerateLayer()
    {
        _worldGenerator.SetGenText("Generating layer 1...");

        for (int x = 0; x < _width; x++)
        {
            for (int y = _startY; y < _endY; y++)
            {
                _worldManager.SetTile(x, y, _baseTile, false);
            }
        }

        OnFinish();

        yield return null;
    }
}
