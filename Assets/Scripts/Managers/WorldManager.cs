using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using UnityEngine.UIElements;
using static UnityEditor.PlayerSettings;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance;

    public int WorldWidth = 100;
    public int WorldHeight = 50;
    public Tilemap MainTilemap;

    [SerializeField] private GameObject _player;
    [SerializeField] private ParticleSystem _destroyTileParticle;

    public TileSO[,] WorldData { get; private set; } // Stores world tiles (0 = air, 1 = dirt, 2 = stone, 3 = ore)

    private Dictionary<Vector2Int, int> _durabilityLeft = new();

    private WorldGenerator _worldGenerator;

    [HideInInspector] public bool Ready = false;

    private void Awake()
    {
        Instance = this;
        _worldGenerator = GetComponent<WorldGenerator>();
    }


    private void Start()
    {
        WorldData = new TileSO[WorldWidth, WorldHeight];

        _worldGenerator.Generate(this);

        for (int x = WorldWidth / 2 - 1; x <= WorldWidth / 2 + 1; x++)
        {
            for (int y = WorldHeight / 2 - 1; y <= WorldHeight / 2 + 1; y++)
            {
                SetTile(x, y, null);
            }
        }

        _player.transform.position = new Vector2(WorldWidth / 2, WorldHeight / 2);

        Ready = true;
    }

    [System.Obsolete]
    public void Hit(Vector2Int pos, int power)
    {
        if (WorldData[pos.x, pos.y] == null) return;

        if (!_durabilityLeft.ContainsKey(pos)) _durabilityLeft[pos] = WorldData[pos.x, pos.y].Durability;

        _durabilityLeft[pos] -= power;

        AudioManager.Instance.PlayMine();

        Vector3Int tilePosition = new Vector3Int(pos.x, pos.y, 0);
        TileBase tile = MainTilemap.GetTile(tilePosition);
        Vector3 worldPosition = MainTilemap.CellToWorld(tilePosition);

        GameObject tempObj = new GameObject("TempTile");
        SpriteRenderer sr = tempObj.AddComponent<SpriteRenderer>();

        sr.sprite = MainTilemap.GetSprite(tilePosition);

        sr.sortingLayerID = MainTilemap.GetComponent<TilemapRenderer>().sortingLayerID;
        sr.sortingOrder = MainTilemap.GetComponent<TilemapRenderer>().sortingOrder + 1;
        tempObj.transform.position = worldPosition + new Vector3(.5f, .5f, 0);

        var originalColor = MainTilemap.GetColor(tilePosition);
        MainTilemap.SetColor(tilePosition, new Color(0, 0, 0, 0));

        tempObj.transform.DOScale(new Vector3(.8f, .8f, 1f), 0.05f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                tempObj.transform.DOScale(Vector3.one, 0.05f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => {
                        MainTilemap.SetColor(tilePosition, originalColor);
                        if (_durabilityLeft[pos] <= 0)
                        {
                            //playsound break
                            BreakTile(pos.x, pos.y);
                        }
                        Destroy(tempObj);
                    });
            });
    }

    public TileSO GetTileAtMousePos()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int mousePosition2D = new Vector2Int(Mathf.RoundToInt(mousePos.x - .5f), Mathf.RoundToInt(mousePos.y - .5f));
        return WorldData[mousePosition2D.x, mousePosition2D.y];
    }

    [System.Obsolete]
    public void BreakTile(int x, int y)
    {
        if (WorldData[x, y] == null) return;
        Vector3Int tilePosition = new Vector3Int(x, y, 0);
        Vector3 worldPosition = MainTilemap.CellToWorld(tilePosition);
        var particle = Instantiate(_destroyTileParticle, tilePosition + new Vector3(.5f,.5f, 0), Quaternion.identity);
        particle.startColor = WorldData[x, y].ParticleColors.Random();
        particle.Play();
        particle.Emit(Random.Range(3, 8));
        Destroy(particle.gameObject, particle.startLifetime);
        SetTile(x, y, null);
    }

    public void SetTileAtMouse(TileSO tile)
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int mousePosition2D = new Vector2Int(Mathf.RoundToInt(mousePos.x - .5f), Mathf.RoundToInt(mousePos.y - .5f));

        SetTile(mousePosition2D.x, mousePosition2D.y, tile);
    }

    public void SetTile(int x, int y, TileSO tile)
    {
        WorldData[x,y] = tile;
        if(tile != null) MainTilemap.SetTile(new Vector3Int(x, y, 0), tile.Tile);
        else MainTilemap.SetTile(new Vector3Int(x, y, 0), null);
        //MainTilemap.SetTile(x,)
    }
}
