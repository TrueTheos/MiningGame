using Assets.Scripts.Managers.WorldGeneration;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using UnityEngine.UIElements;
using UnityEngine.WSA;
using static UnityEditor.PlayerSettings;
using Random = UnityEngine.Random;

public enum TilemapType { Main, Decorations}

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance;

    public int WorldWidth;
    public int WorldHeight;
    public Tilemap MainTilemap;

    [SerializeField] private ParticleSystem _destroyTileParticle;
    public ParticleSystem DestroyTileParticle => _destroyTileParticle;
    [SerializeField] private PickupableItem _pickupableItem;
    [SerializeField] private TileBuildableItem _tileBuildableItem;
    [SerializeField] private LightSourceCustomBuilding _torchPrefab;
    [SerializeField] private LayerMask _hideOutsideRenderDstanceMask;

    public UnityEvent OnWorldReady;

    public const int CHUNK_SIZE = 32;
    public readonly int RENDER_DISTANCE_CHUNKS = 1; //in each direction

    public TileSO[,] WorldData { get; private set; } // Stores world tiles (0 = air, 1 = dirt, 2 = stone, 3 = ore)
    public bool[,] PathNodes {  get; private set; }
    public CustomBuilding[,] Buildings { get; private set; }

    private Dictionary<Vector2Int, int> _durabilityLeft = new();

    private WorldGenerator _worldGenerator;

    [HideInInspector] public bool Ready = false;

    private Camera _cam;
    private Vector2Int _lastPlayerPosition;

    private GameObject _buildingsParent;
    private GameObject _randomParent;

    private ChunkManager _chunkManager;
    private PlayerMovement _player;

    private HashSet<GameObject> _objectsInRenderDistance = new();

    private void Awake()
    {
        Instance = this;
        _worldGenerator = GetComponent<WorldGenerator>();
        _cam = Camera.main;
    }

    private void Start()
    {
        WorldData = new TileSO[WorldWidth, WorldHeight];
        PathNodes = new bool[WorldWidth, WorldHeight];
        Buildings = new CustomBuilding[WorldWidth, WorldHeight];

        _buildingsParent = new GameObject("Buildings");
        _buildingsParent.transform.SetParent(transform);

        _randomParent = new GameObject("Random");
        _randomParent.transform.SetParent(transform);

        _player = PlayerMovement.Instance;
        _chunkManager = ChunkManager.Instance;

        StartCoroutine(InitWorld());        
    }

    public bool IsEmpty(Vector2Int pos) => IsEmpty(pos.x, pos.y);

    public bool IsEmpty(int x, int y)
    {
        if(!IsTileInBounds(x, y)) return false;
        return WorldData[x,y] == null && Buildings[x,y] == null;
    }

    public bool IsSolid(int x, int y)
    {
        return ((WorldData[x, y] != null && WorldData[x, y].Solid) || (Buildings[x, y] != null && Buildings[x, y].Solid));
    }

    public bool IsLightBlocker(int x, int y)
    {
        return (WorldData[x, y] != null && WorldData[x, y].Solid) || (Buildings[x, y] != null && Buildings[x, y].Solid);
    }

    private void Update()
    {
        if (_player.Pos != _lastPlayerPosition)
        {
            RefreshVisibleTiles();
            UpdateObjectsOutsideRenderDistance();
            _lastPlayerPosition = _player.Pos;
        }
    }

    private void UpdateObjectsOutsideRenderDistance()
    {
        int dis = RENDER_DISTANCE_CHUNKS * CHUNK_SIZE;
        Collider2D[] nearbyObjects = Physics2D.OverlapBoxAll(
            transform.position,
            new Vector2(dis * 2, dis * 2),
            0,
            _hideOutsideRenderDstanceMask);

        HashSet<GameObject> processedObjects = new HashSet<GameObject>();

        foreach (Collider2D obj in nearbyObjects)
        {
            GameObject go = obj.gameObject;
            processedObjects.Add(go);

            float distance = Vector2.Distance(transform.position, go.transform.position);

            if (distance <= dis)
            {
                if (!go.activeSelf)
                {
                    go.SetActive(true);
                }

                _objectsInRenderDistance.Add(go);
            }
        }

        foreach (GameObject item in new List<GameObject>(_objectsInRenderDistance))
        {
            if (item == null)
            {
                _objectsInRenderDistance.Remove(item);
                continue;
            }

            if (!processedObjects.Contains(item))
            {
                float distance = Vector2.Distance(transform.position, item.transform.position);

                if (distance > dis)
                {
                    item.SetActive(false);
                    _objectsInRenderDistance.Remove(item);
                }
            }
        }
    }

    private void RefreshVisibleTiles()
    {
        var playerChunk = _chunkManager.GetChunkByWorldPos(_player.Pos);
        if (playerChunk == null) return;

        for (int x = playerChunk.ChunkPosX - RENDER_DISTANCE_CHUNKS; x <= playerChunk.ChunkPosX + RENDER_DISTANCE_CHUNKS; x++)
        {
            for (int y = playerChunk.ChunkPosY - RENDER_DISTANCE_CHUNKS; y <= playerChunk.ChunkPosY + RENDER_DISTANCE_CHUNKS; y++)
            {
                _chunkManager.RenderChunk(x, y);
            }
        }
    }

    private IEnumerator InitWorld()
    {
        var time = Time.time;
        _player.gameObject.SetActive(false);

        MainTilemap.GetComponent<TilemapCollider2D>().enabled = false;
        _worldGenerator.Generate(this);

        while (!_worldGenerator.Ready)
        {
            yield return new WaitForSeconds(1);
        }

        OnWorldReady?.Invoke();
        OnWorldReady?.RemoveAllListeners();

        for (int x = WorldWidth / 2 - 1; x <= WorldWidth / 2 + 1; x++)
        {
            for (int y = WorldHeight / 2 - 1; y <= WorldHeight / 2 + 1; y++)
            {
                SetTile(x, y, null);
            }
        }

        MainTilemap.GetComponent<TilemapCollider2D>().enabled = true;
       
        TryPlace(WorldWidth / 2, WorldHeight / 2 - 1, _torchPrefab);

        Ready = true;

        _player.transform.position = new Vector2(WorldWidth / 2, WorldHeight / 2 + 1);
        _player.gameObject.SetActive(true);

        Debug.Log($"WORLD GENERATED: {Time.time - time}");
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

        if (!IsTileInBounds(x,y)) return;
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

            SetTile(x, y, null);

            for (int nx = x - 1; nx <= x + 1; nx++)
            {
                for (int ny = y - 1; ny <= y + 1; ny++)
                {
                    if (!IsTileInBounds(nx,ny)) continue;
                    var building = Buildings[nx, ny];

                    if (building != null) building.DestroyIfInvalid();
                }
            }
            Destroy(particle.gameObject, particle.startLifetime);
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
            var building = Buildings[x, y];
            Buildings[x, y] = null;
            UpdatePathNodeAt(x, y);
            UpdatePathNodeAt(x, y + 1);
            building.OnBreak();
        }

        CaveReverbManager.Instance.RecalculateZone(x, y);
    }

    public void SpawnPickupable(float x, float y, ItemAmount itemAmount, bool randomOffset = true, bool randomRotation = true)
    {
        float rX = randomOffset ? Random.Range(-.2f, .2f) : 0;
        float rY = randomOffset ? Random.Range(-.2f, .2f) : 0;
        float rR = randomRotation ? Random.Range(0, 360) : 0;
        PickupableItem p = Instantiate(_pickupableItem, new Vector3(x + rX, y + rY, 0f), Quaternion.identity).GetComponent<PickupableItem>();
        p.transform.SetParent(_randomParent.transform);
        p.transform.Rotate(0, 0, rR);
        p.Init(itemAmount);
    }

    public void PlaceBuilding(int x, int y, CustomBuilding building)
    {
        CustomBuilding newBuilding = Instantiate(building.gameObject, new Vector3(x + .5f, y + .5f, 0), Quaternion.identity).GetComponent<CustomBuilding>();
        newBuilding.transform.SetParent(_buildingsParent.transform);
        Buildings[x, y] = newBuilding;
        newBuilding.OnPlace(x,y);
        newBuilding.Pos = new Vector2Int(x, y);
        CaveReverbManager.Instance.RecalculateZone(x, y);

        UpdatePathNodeAt(x, y);
        UpdatePathNodeAt(x, y + 1);
    }

    public bool TryPlace(int x, int y, CustomBuilding building)
    {
        if (!IsTileInBounds(x, y)) return false;
        if (!building.IsPlacementValid(x, y)) return false;
        if (!IsEmpty(x, y))
        {
            if (Buildings[x, y] != null && Buildings[x, y].canBeDestroyedToReplace)
            {
                BreakTile(x, y);
                PlaceBuilding(x, y, building);
                AudioManager.Instance.PlayPlace();
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
            AudioManager.Instance.PlayPlace();
            return true;
        }
    }

    public bool TryPlaceTile(int x, int y, TileBuildableItem tile)
    {
        if (!IsTileInBounds(x,y)) return false;
        if (!IsEmpty(x, y))
        {
            if (Buildings[x, y] != null && Buildings[x, y].canBeDestroyedToReplace)
            {
                BreakTile(x, y);
                SetTile(x, y, tile.Tile);
                AudioManager.Instance.PlayPlace();
                CaveReverbManager.Instance.RecalculateZone(x, y);
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            SetTile(x, y, tile.Tile);
            AudioManager.Instance.PlayPlace();
            CaveReverbManager.Instance.RecalculateZone(x, y);
            return true;
        }
    }

    public void SetTile(int x, int y, TileSO tile, bool showTile = true)
    {
        WorldData[x,y] = tile;

        if(showTile) ShowTile(x, y);

        //if (IsTileInView(x, y)) ShowTile(x, y);
        //MainTilemap.SetTile(x,)
        UpdatePathNodeAt(x, y);
        UpdatePathNodeAt(x, y + 1);
    }

    private void UpdatePathNodeAt(int x, int y)
    {
        if (!IsTileInBounds(x, y)) return;
        bool isFree = (WorldData[x, y] == null || !WorldData[x,y].Solid) && (Buildings[x, y] == null || !Buildings[x, y].Solid);

        bool hasSolidSupport = (y - 1 >= 0) && ((WorldData[x, y - 1] != null && WorldData[x, y - 1].Solid) || (Buildings[x, y - 1] != null && Buildings[x, y - 1].Solid));

        PathNodes[x, y] = isFree && hasSolidSupport;
    }

    public void ShowTile(int x, int y)
    {
        if (!IsTileInBounds(x, y)) return;
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

    public bool IsTileInBounds(int x, int y)
    {
        return x >= 0 && x < WorldWidth && y >= 0 && y < WorldHeight;
    }

    public Vector2Int? GetRandomFreeCellInCircle(Vector2Int center, int radius)
    {
        List<Vector2Int> freeCells = new List<Vector2Int>();

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                float dist = Vector2Int.Distance(center, new Vector2Int(x, y));
                if (dist <= radius)
                {
                    if (IsTileInBounds(x,y) && !IsSolid(x,y))
                    {
                        freeCells.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        if (freeCells.Count == 0)
        {
            return null;
        }

        int randIndex = Random.Range(0, freeCells.Count);
        return freeCells[randIndex];
    }

    public List<Vector2Int> GetFreeCellsInCircle(Vector2Int center, int radius)
    {
        List<Vector2Int> freeCells = new List<Vector2Int>();

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                float dist = Vector2Int.Distance(center, new Vector2Int(x, y));
                if (dist <= radius)
                {
                    if (IsTileInBounds(x, y) && !IsSolid(x, y))
                    {
                        freeCells.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        return freeCells;
    }
}
