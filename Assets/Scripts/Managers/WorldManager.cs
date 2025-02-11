using Assets.Scripts.Managers.WorldGeneration;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using UnityEngine.UIElements;
using UnityEngine.WSA;
using static UnityEditor.PlayerSettings;

public enum TilemapType { Main, Decorations}

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance;

    public int WorldWidth;
    public int WorldHeight;
    public Tilemap MainTilemap;

    [SerializeField] private GameObject _player;
    [SerializeField] private ParticleSystem _destroyTileParticle;
    [SerializeField] private PickupableItem _pickupableItem;
    [SerializeField] private TileBuildableItem _tileBuildableItem;
    [SerializeField] private int _playerShowTileRadius;
    [SerializeField] private Torch _torchPrefab;

    public readonly int CHUNK_SIZE = 64;

    public TileSO[,] WorldData { get; private set; } // Stores world tiles (0 = air, 1 = dirt, 2 = stone, 3 = ore)
    public CustomBuilding[,] Buildings { get; private set; }

    private Dictionary<Vector2Int, int> _durabilityLeft = new();

    private WorldGenerator _worldGenerator;

    [HideInInspector] public bool Ready = false;

    private Camera _cam;
    private Vector2Int _lastPlayerPosition;

    private void Awake()
    {
        Instance = this;
        _worldGenerator = GetComponent<WorldGenerator>();
        _cam = Camera.main;
    }

    private void Start()
    {
        WorldData = new TileSO[WorldWidth, WorldHeight];
        Buildings = new CustomBuilding[WorldWidth, WorldHeight];

        StartCoroutine(InitWorld());        
    }

    public bool IsEmpty(Vector2Int pos) => IsEmpty(pos.x, pos.y);

    public bool IsEmpty(int x, int y)
    {
        if(x <0 || x >= WorldWidth || y <0 || y >= WorldHeight) return false;
        return WorldData[x,y] == null && Buildings[x,y] == null;
    }

    public bool IsLightBlocker(int x, int y)
    {
        return (WorldData[x, y] != null && WorldData[x, y].Solid) || (Buildings[x, y] != null && Buildings[x, y].Solid);
    }

    private void Update()
    {
        Vector2Int playerPos = new Vector2Int(Mathf.RoundToInt(_player.transform.position.x), Mathf.RoundToInt(_player.transform.position.y));

        if (playerPos != _lastPlayerPosition)
        {
            RefreshVisibleTiles(playerPos);
            _lastPlayerPosition = playerPos;
        }
    }

    private void RefreshVisibleTiles(Vector2Int playerPos)
    {
        for (int x = playerPos.x - _playerShowTileRadius; x <= playerPos.x + _playerShowTileRadius; x++)
        {
            for (int y = playerPos.y - _playerShowTileRadius; y <= playerPos.y + _playerShowTileRadius; y++)
            {
                if (x >= 0 && x < WorldWidth && y >= 0 && y < WorldHeight)
                {
                    ShowTile(x, y);
                }
            }
        }
    }

    private IEnumerator InitWorld()
    {
        _player.gameObject.SetActive(false);

        MainTilemap.GetComponent<TilemapCollider2D>().enabled = false;
        _worldGenerator.Generate(this);

        while (!_worldGenerator.Ready)
        {
            yield return new WaitForSeconds(1);
        }

        for (int x = WorldWidth / 2 - 1; x <= WorldWidth / 2 + 1; x++)
        {
            for (int y = WorldHeight / 2 - 1; y <= WorldHeight / 2 + 1; y++)
            {
                SetTile(x, y, null);
            }
        }

        MainTilemap.GetComponent<TilemapCollider2D>().enabled = true;
        _player.transform.position = new Vector2(WorldWidth / 2, WorldHeight / 2);
        _player.gameObject.SetActive(true);

        TryPlace(WorldWidth / 2, WorldHeight / 2 - 1, _torchPrefab);

        Ready = true;
    }

    [System.Obsolete]
    public void Hit(Vector2Int pos, int power)
    {
        if (WorldData[pos.x, pos.y] == null && Buildings[pos.x,pos.y] == null) return;

        AudioManager.Instance.PlayMine();

        if (WorldData[pos.x, pos.y] != null)
        {
            if (!_durabilityLeft.ContainsKey(pos)) _durabilityLeft[pos] = WorldData[pos.x, pos.y].Durability;
            _durabilityLeft[pos] -= power;

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
                            
                            if (_durabilityLeft[pos] <= 0)
                            {
                                //playsound break
                                BreakTile(pos.x, pos.y);
                            }
                            MainTilemap.SetColor(tilePosition, originalColor);
                            DestroyImmediate(tempObj);
                        });
                });

        }
        else if(Buildings[pos.x, pos.y] != null)
        {
            if (!_durabilityLeft.ContainsKey(pos)) _durabilityLeft[pos] = Buildings[pos.x, pos.y].Durability;
            _durabilityLeft[pos] -= power;

            GameObject building = Buildings[pos.x, pos.y].gameObject;

            building.transform.DOScale(new Vector3(.8f, .8f, 1f), 0.05f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    building.transform.DOScale(Vector3.one, 0.05f)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() => {
                            if (_durabilityLeft[pos] <= 0)
                            {
                                //playsound break
                                BreakTile(pos.x, pos.y);
                            }
                        });
                });
        }
    }

    public TileSO GetTileAtMousePos()
    {
        Vector3 mousePos = _cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int mousePosition2D = new Vector2Int(Mathf.RoundToInt(mousePos.x - .5f), Mathf.RoundToInt(mousePos.y - .5f));
        return WorldData[mousePosition2D.x, mousePosition2D.y];
    }

    public PlacableItem GetBuildingAtMousePos()
    {
        Vector3 mousePos = _cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int mousePosition2D = new Vector2Int(Mathf.RoundToInt(mousePos.x - .5f), Mathf.RoundToInt(mousePos.y - .5f));
        return Buildings[mousePosition2D.x, mousePosition2D.y];
    }

    [System.Obsolete]
    public void BreakTile(int x, int y)
    {
        Vector3Int tilePosition = new Vector3Int(x, y, 0);

        _durabilityLeft.Remove(new Vector2Int(x, y));

        if (x < 0 || x >= WorldWidth || y < 0 || y >= WorldHeight) return;
        if (WorldData[x, y] != null)
        {  
            Vector3 worldPosition = MainTilemap.CellToWorld(tilePosition);
            var particle = Instantiate(_destroyTileParticle, tilePosition + new Vector3(.5f, .5f, 0), Quaternion.identity);
            particle.startColor = WorldData[x, y].ParticleColors.Random();
            particle.Play();
            particle.Emit(Random.Range(3, 8));
            if (WorldData[x, y].Drop.Item == null && WorldData[x, y].DropItself)
            {
                var clone = Instantiate(_tileBuildableItem.gameObject);
                clone.SetActive(false);
                clone.GetComponent<TileBuildableItem>().Init(WorldData[x, y]);
                SpawnPickupable(tilePosition.x + .5f, tilePosition.y + .5f, new ItemAmount(clone.GetComponent<Item>(), 1));
            }
            if (WorldData[x, y].Drop.Item != null)
            {
                SpawnPickupable(tilePosition.x + .5f, tilePosition.y + .5f, WorldData[x, y].Drop);
            }
            Destroy(particle.gameObject, particle.startLifetime);
            SetTile(x, y, null);
        }

        if (Buildings[x,y] != null)
        {
            if (Buildings[x, y].drop.Item == null && Buildings[x, y].dropItself)
            {
                var clone = Instantiate(Buildings[x, y].gameObject);
                clone.SetActive(false);
                SpawnPickupable(tilePosition.x + .5f, tilePosition.y + .5f, new ItemAmount(clone.GetComponent<Item>(), 1));
            }
            if(Buildings[x, y].drop.Item != null) SpawnPickupable(tilePosition.x + .5f, tilePosition.y + .5f, Buildings[x, y].drop); 
            Buildings[x, y].OnBreak();
            Buildings[x, y] = null;
        }
    }

    public void SpawnPickupable(float x, float y, ItemAmount itemAmount, bool randomOffset = true, bool randomRotation = true)
    {
        float rX = randomOffset ? Random.Range(-.2f, .2f) : 0;
        float rY = randomOffset ? Random.Range(-.2f, .2f) : 0;
        float rR = randomRotation ? Random.Range(0, 360) : 0;
        PickupableItem p = Instantiate(_pickupableItem, new Vector3(x + rX, y + rY, 0f), Quaternion.identity).GetComponent<PickupableItem>();
        p.transform.Rotate(0, 0, rR);
        p.Init(itemAmount);
    }

    public void PlaceBuilding(int x, int y, CustomBuilding building)
    {
        CustomBuilding newBuilding = Instantiate(building.gameObject, new Vector3(x + .5f, y + .5f, 0), Quaternion.identity).GetComponent<CustomBuilding>();

        Buildings[x, y] = newBuilding;
        newBuilding.OnPlace(x,y);
        newBuilding.Pos = new Vector2Int(x, y);
    }

    public bool TryPlace(int x, int y, CustomBuilding building)
    {
        if(x < 0 || y < 0 || x >= WorldWidth || y >= WorldHeight) return false;
        if (!building.IsPlacementValid(x, y)) return false;
        if (!IsEmpty(x, y))
        {
            if (Buildings[x, y] != null && Buildings[x, y].canBeDestroyedToReplace)
            {
                BreakTile(x, y);
                PlaceBuilding(x, y, building);
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            PlaceBuilding(x, y, building);
            return true;
        }
    }

    public void SetTile(int x, int y, TileSO tile, bool showTile = true)
    {
        WorldData[x,y] = tile;

        if(showTile) ShowTile(x, y);

        //if (IsTileInView(x, y)) ShowTile(x, y);
        //MainTilemap.SetTile(x,)
    }

    private void ShowTile(int x, int y)
    {
        var tile = WorldData[x,y];
        if (tile != null) MainTilemap.SetTile(new Vector3Int(x, y, 0), tile.Tile);
        else MainTilemap.SetTile(new Vector3Int(x, y, 0), null);
    }

    private bool IsTileInView(int x, int y)
    {
        Vector3 worldPos = MainTilemap.CellToWorld(new Vector3Int(x, y, 0));
        Vector3 viewportPoint = _cam.WorldToViewportPoint(worldPos);

        return viewportPoint.x >= 0 && viewportPoint.x <= 1 &&
               viewportPoint.y >= 0 && viewportPoint.y <= 1 &&
               viewportPoint.z > 0; // Ensure it's in front of the camera
    }

}
