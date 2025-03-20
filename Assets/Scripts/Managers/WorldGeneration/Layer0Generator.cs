using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Layer0Generator : WorldLayer
{
    [Header("Terrain Settings")]
    [SerializeField] private float _terrainHeightMultiplier;
    [SerializeField] private float _terrainSmoothness;

    [Header("Cave Settings")]
    [SerializeField] private float _perlinCaveThreshold;
    [SerializeField] private float _perlinCaveScale;

    [Header("Cellular Settings")]
    [SerializeField] private int _caveSimulationSteps;
    [SerializeField] private float _initialCaveChance;

    [SerializeField] private CustomBuilding _vase;
    [SerializeField] private float _vaseSpawnChance = 0.02f;

    [Header("Web Biome Settings")]
    [SerializeField] private Web _webPrefab;
    [SerializeField] private int _webNodesCount;
    [SerializeField] private int _webNodeSize;

    [SerializeField] private Decoration _jungleGrassTile;
    [SerializeField] private float _grassSpawnChance;
    [SerializeField] private Decoration _vineTile;
    [SerializeField] private Vector2Int _maxVineLength;
    [SerializeField] private float _vineChance;
    [SerializeField] private TileSO _baseTile;
    [SerializeField] private List<DecorativeBuildingChance> _decorativeBuildings = new();

    private List<Vector2Int> _freeTiles = new();

    protected override IEnumerator GenerateLayer()
    {
        _worldGenerator.SetGenText("Generating layer 0 caves...");

        for (int x = 0; x < _width; x++)
        {
            for (int y = _startY; y < _endY; y++)
            {
                _worldManager.SetTile(x, y, _baseTile, false);
            }
        }

        //sGeneratePerlinCaves();

        yield return StartCoroutine(GenerateCellularAutomataCaves());
        GenerateJungleBiome();

        yield return StartCoroutine(GenerateVasesCoroutine());

        GenerateWebBiome();

        OnFinish();
    }

    private void GeneratePerlinCaves()
    {
        for (int x = 0; x < _width; x++)
        {
            int groundHeight = Mathf.FloorToInt(Mathf.PerlinNoise(x * _terrainSmoothness, 0)
                * _terrainHeightMultiplier + (_worldManager.WorldHeight / 2));

            for (int y = _startY; y < _endY; y++)
            {
                float caveNoise = Mathf.PerlinNoise(x * _perlinCaveScale, y * _perlinCaveScale);
                bool shouldBeCave = caveNoise > _perlinCaveThreshold || UnityEngine.Random.value < _initialCaveChance;

                _worldManager.SetTile(x, y, shouldBeCave ? null : _baseTile, false);
            }
        }
    }

    private IEnumerator GenerateCellularAutomataCaves()
    {
        _worldGenerator.SetGenText("Generating layer 0 caves...");

        int[,] map = new int[_width, _endY - _startY];

        for (int x = 0; x < _width; x++)
        {
            for (int y = _startY; y < _endY; y++)
            {
                if (x == 0 || x == _width - 1 || y == _startY || y == _endY - 1)
                {
                    map[x, y - _startY] = 1;
                }
                else
                {
                    map[x, y - _startY] = Random.Range(0,100) <= _initialCaveChance * 100 ? 1 : 0;
                }
            }
        }

        yield return null;

        // Smooth the map multiple times (like in SmoothMap)
        for (int i = 0; i < _caveSimulationSteps; i++)
        {
            _worldGenerator.SetGenText($"Smoothing caves... Step {i + 1}/{_caveSimulationSteps}");

            int[,] newMap = new int[_width, _endY - _startY];

            for (int x = 0; x < _width; x++)
            {
                for (int y = _startY; y < _endY; y++)
                {
                    // Use the rules from SmoothMap
                    int surroundingWalls = GetSurroundingWallCount(map, x, y - _startY);

                    // Apply the original MapGenerator rule
                    if (surroundingWalls > 4)
                        newMap[x, y - _startY] = 1;
                    else if (surroundingWalls < 4)
                        newMap[x, y - _startY] = 0;
                    else
                        newMap[x, y - _startY] = map[x, y - _startY];
                }
            }

            map = newMap;
            yield return null;
        }

        // Process regions for more complex caves (optional)
        yield return StartCoroutine(ProcessRegions(map));

        // Apply the finished map to the world
        _freeTiles.Clear();
        for (int x = 0; x < _width; x++)
        {
            for (int y = _startY; y < _endY; y++)
            {
                bool isSolid = map[x, y - _startY] == 1;
                _worldManager.SetTile(x, y, isSolid ? _baseTile : null, false);

                if (!isSolid)
                {
                    _freeTiles.Add(new Vector2Int(x, y));
                }

                if ((x * (_endY - _startY) + (y - _startY)) % 100 == 0)
                {
                    yield return null;
                }
            }
        }
    }

    private int GetSurroundingWallCount(int[,] map, int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
        {
            for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
            {
                if (IsInMapRange(map, neighborX, neighborY))
                {
                    if (neighborX != gridX || neighborY != gridY)
                    {
                        wallCount += map[neighborX, neighborY];
                    }
                }
                else
                {
                    wallCount++; // Count out-of-bounds as walls
                }
            }
        }
        return wallCount;
    }

    private bool IsInMapRange(int[,] map, int x, int y)
    {
        return x >= 0 && x < map.GetLength(0) && y >= 0 && y < map.GetLength(1);
    }

    private IEnumerator ProcessRegions(int[,] map)
    {
        _worldGenerator.SetGenText("Processing cave regions...");

        // Find all wall regions
        List<List<Vector2Int>> wallRegions = GetRegions(map, 1);
        int wallThresholdSize = 50; // You may want to expose this as a parameter

        foreach (var wallRegion in wallRegions)
        {
            if (wallRegion.Count < wallThresholdSize)
            {
                // Remove small wall regions by making them empty space
                foreach (var pos in wallRegion)
                {
                    map[pos.x, pos.y] = 0;
                }
            }

            yield return null;
        }

        // Find all room regions
        List<List<Vector2Int>> roomRegions = GetRegions(map, 0);
        int roomThresholdSize = 50; // You may want to expose this as a parameter

        foreach (var roomRegion in roomRegions)
        {
            if (roomRegion.Count < roomThresholdSize)
            {
                // Remove small rooms by filling them with walls
                foreach (var pos in roomRegion)
                {
                    map[pos.x, pos.y] = 1;
                }
            }

            yield return null;
        }
    }

    private List<List<Vector2Int>> GetRegions(int[,] map, int tileType)
    {
        List<List<Vector2Int>> regions = new List<List<Vector2Int>>();
        bool[,] mapFlags = new bool[map.GetLength(0), map.GetLength(1)];

        for (int x = 0; x < map.GetLength(0); x++)
        {
            for (int y = 0; y < map.GetLength(1); y++)
            {
                if (!mapFlags[x, y] && map[x, y] == tileType)
                {
                    List<Vector2Int> newRegion = GetRegionTiles(map, mapFlags, x, y);
                    regions.Add(newRegion);

                    foreach (var pos in newRegion)
                    {
                        mapFlags[pos.x, pos.y] = true;
                    }
                }
            }
        }

        return regions;
    }

    private List<Vector2Int> GetRegionTiles(int[,] map, bool[,] mapFlags, int startX, int startY)
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        int tileType = map[startX, startY];

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        mapFlags[startX, startY] = true;

        while (queue.Count > 0)
        {
            Vector2Int tile = queue.Dequeue();
            tiles.Add(tile);

            // Check 4-connected neighbors (orthogonal only) as in the original code
            for (int x = tile.x - 1; x <= tile.x + 1; x++)
            {
                for (int y = tile.y - 1; y <= tile.y + 1; y++)
                {
                    if (IsInMapRange(map, x, y) && (y == tile.y || x == tile.x)) // Orthogonal only
                    {
                        if (!mapFlags[x, y] && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = true;
                            queue.Enqueue(new Vector2Int(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }


    private void GenerateJungleBiome()
    {
        foreach (var freeTile in _freeTiles)
        {
            TileSO currentTile = _worldManager.GetTile(freeTile.x, freeTile.y);

            //if (currentTile != baseTile) return;

            if (_decorativeBuildings != null && _decorativeBuildings.Count > 0)
            {
                var randomDecoration = _decorativeBuildings.Random();
                if (randomDecoration.Building != null)
                {
                    if (Random.Range(0f, 1f) <= randomDecoration.Chance)
                    {
                        _worldManager.TryPlace(freeTile.x, freeTile.y, randomDecoration.Building);
                    }
                }
            }

            if (Random.value < _grassSpawnChance)
            {
                _worldManager.TryPlace(freeTile.x, freeTile.y, _jungleGrassTile);
            }

            if (_worldManager.IsEmpty(freeTile.x, freeTile.y - 1) && Random.value < _vineChance)
            {
                GenerateVine(freeTile.x, freeTile.y);
            }
        }
    }

    private void GenerateVine(int x, int startY)
    {
        int length = _maxVineLength.Random();

        for (int y = startY; y > startY - length && y >= 0; y--)
        {
            if (!_worldManager.IsEmpty(x, y)) break;
            _worldManager.TryPlace(x, y, _vineTile);
        }
    }

    private IEnumerator GenerateVasesCoroutine()
    {
        int i = 0;

        foreach (var freeTile in _freeTiles)
        {
            i++;
            if (_worldManager.GetTile(freeTile.x, freeTile.y - 1) != null)
            {
                if (Random.value < _vaseSpawnChance)
                {
                    _worldManager.PlaceBuilding(freeTile.x, freeTile.y, _vase);
                }
            }

            if(i % 10 == 0) yield return null;
        }
    }

    private void GenerateWebBiome()
    {
        HashSet<Vector2Int> ignore = new();

        int attemptLimit = 16;
        int currAttempts = 0;

        for (int i = 0; i < _webNodesCount; i++)
        {
            currAttempts = 0;

            Vector2Int start;

            do
            {
                attemptLimit++;
                start = _freeTiles.Random();
                if(_freeTiles.Count > 0) _freeTiles.RemoveAt(_freeTiles.IndexOf(start));
            }
            while (_freeTiles.Count > 0 && !_worldManager.IsEmpty(start) && currAttempts <= attemptLimit);

            if (!_worldManager.IsEmpty(start)) continue;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            int spread = 0;
            while (queue.Count > 0 && spread < _webNodeSize)
            {
                var pos = queue.Dequeue();
                List<Vector2Int> neighbors = GetNeighbors(pos.x, pos.y);

                foreach (var n in neighbors)
                {
                    if (ignore.Contains(n)) continue;

                    if (_worldManager.IsEmpty(n) && UnityEngine.Random.Range(0f, 1f) > 0.3)
                    {
                        _worldManager.PlaceBuilding(n.x, n.y, _webPrefab);
                        queue.Enqueue(n);
                        spread++;
                        if (spread >= _webNodeSize) break;
                    }
                    else
                    {
                        ignore.Add(n);
                    }
                }
            }
        }
    }
}
