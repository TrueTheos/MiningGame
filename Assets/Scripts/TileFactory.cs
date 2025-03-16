using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TileFactory : MonoBehaviour
{
    public static TileFactory Instance { get; private set; }

    [SerializeField] private TileBuildableItem _buildableTile;


    private void Awake()
    {
        Instance = this;
    }

    public TileBuildableItem SpawnTile(TileSO tile)
    {
        var clone = Instantiate(_buildableTile.gameObject);
        clone.SetActive(false);
        clone.GetComponent<TileBuildableItem>().Init(tile);
        return clone.GetComponent<TileBuildableItem>();
    }
}
