using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    [SerializeField] private float _terrainHeightMultiplier = 10f;
    [SerializeField] private float _terrainSmoothness = 0.1f;

    [Header("Cave Settings (Cellular Automata)")]
    [SerializeField] private float _perlinCaveThreshold = 0.55f;
    [SerializeField] private float _perlinCaveScale = 0.1f;
    [SerializeField] private int _caveSimulationSteps = 4;
    [SerializeField] private float _initialCaveChance = 0.45f;
    [SerializeField] private int _birthLimit = 3;
    [SerializeField] private int _deathLimit = 2;

    [Header("Ore Generation")]
    [SerializeField] private float _oreSpawnChance = 0.1f;

    [Header("Tiles")]
    [SerializeField] private TileSO _stoneTile;
    [SerializeField] private TileSO _oreTile;

    [SerializeField] private CustomBuilding _vase;
    [SerializeField] private float _vaseSpawnChance = 0.02f;

    private WorldManager _worldManager;

    private int _worldWidth => _worldManager.WorldWidth;
    private int _worldHeight => _worldManager.WorldHeight;

    public void Generate(WorldManager wm)
    {
        _worldManager = wm;
        _worldManager.MainTilemap.ClearAllTiles();
        GenerateWorld();
    }

    void GenerateWorld()
    {
        // Step 1: Generate terrain using Perlin Noise
        for (int x = 0; x < _worldWidth; x++)
        {
            int groundHeight = Mathf.FloorToInt(Mathf.PerlinNoise(x * _terrainSmoothness, 0) * _terrainHeightMultiplier + (_worldHeight / 2));

            for (int y = 0; y < _worldHeight; y++)
            {
                _worldManager.PlaceTile(x, y, _stoneTile);
            }
        }

        GeneratePerlinCaves();

        GenerateCavesWithCellularAutomata();

        GenerateOres();

        GenerateVases();
    }

    private void GenerateVases()
    {
        for (int x = 0; x < _worldWidth; x++)
        {
            for (int y = 1; y < _worldHeight; y++)
            {
                if (_worldManager.WorldData[x, y] == null && _worldManager.WorldData[x, y - 1] != null)
                {
                    if (Random.value < _vaseSpawnChance)
                    {
                        _worldManager.PlaceBuilding(x, y, _vase);
                    }
                }
            }
        }
    }

    void GeneratePerlinCaves()
    {
        for (int x = 0; x < _worldWidth; x++)
        {
            for (int y = 0; y < _worldHeight; y++)
            {
                if (_worldManager.WorldData[x, y] != null) // Only modify solid tiles
                {
                    float noiseValue = Mathf.PerlinNoise(x * _perlinCaveScale, y * _perlinCaveScale);
                    if (noiseValue > _perlinCaveThreshold)
                    {
                        _worldManager.PlaceTile(x, y, null); // Turn into air (Cave)
                    }
                }
            }
        }
    }

    void GenerateCavesWithCellularAutomata()
    {
        // Randomly initialize caves
        for (int x = 0; x < _worldWidth; x++)
        {
            for (int y = 0; y < _worldHeight; y++)
            {
                if (_worldManager.WorldData[x, y] != null) // Only affect solid tiles
                {
                    _worldManager.PlaceTile(x, y, (Random.value < _initialCaveChance) ? null : _worldManager.WorldData[x, y]);
                }
            }
        }

        // Apply cellular automata rules multiple times
        for (int step = 0; step < _caveSimulationSteps; step++)
        {
            var cStep = CellularAutomataStep(_worldManager.WorldData);

            for (int x = 0; x < _worldWidth; x++)
            {
                for (int y = 0; y < _worldHeight; y++)
                {
                    _worldManager.PlaceTile(x, y, cStep[x,y]);
                }
            }
        }
    }

    TileSO[,] CellularAutomataStep(TileSO[,] map)
    {
        TileSO[,] newMap = new TileSO[_worldWidth, _worldHeight];

        for (int x = 0; x < _worldWidth; x++)
        {
            for (int y = 0; y < _worldHeight; y++)
            {
                int neighborCount = CountSolidNeighbors(map, x, y);

                if (map[x, y] != null) // If it's a solid block (stone or dirt)
                {
                    newMap[x, y] = (neighborCount >= _deathLimit) ? map[x, y] : null; // Become air if too isolated
                }
                else // If it's already air
                {
                    newMap[x, y] = (neighborCount >= _birthLimit) ? _stoneTile : null; // Become solid if surrounded
                }
            }
        }

        return newMap;
    }

    int CountSolidNeighbors(TileSO[,] map, int x, int y)
    {
        int count = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
        {
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue; // Skip the current cell

                if (nx >= 0 && nx < _worldWidth && ny >= 0 && ny < _worldHeight)
                {
                    if (map[nx, ny] != null) count++;
                }
                else
                {
                    count++; // Treat out-of-bounds as solid
                }
            }
        }
        return count;
    }

    void GenerateOres()
    {
        for (int x = 0; x < _worldWidth; x++)
        {
            for (int y = 0; y < _worldHeight; y++)
            {
                if (_worldManager.WorldData[x, y] == _stoneTile) // Only inside stone
                {
                    if (Random.value < _oreSpawnChance)
                    {
                        _worldManager.WorldData[x, y] = _oreTile; // Ore
                    }
                }
            }
        }
    }
}
