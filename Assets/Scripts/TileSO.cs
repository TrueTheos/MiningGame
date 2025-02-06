using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Tile", menuName = "New Tile")]
public class TileSO : ScriptableObject
{
    public string Name;
    public int Durability;
    public Sprite Art;
    public TileBase Tile;
    public bool Solid;
    public List<Color> ParticleColors = new();
    public ItemAmount Drop;
    public bool DropItself = true;
}
