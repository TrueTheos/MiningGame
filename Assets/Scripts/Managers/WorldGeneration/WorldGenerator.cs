using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class BiomeSettings
{
    public TileSO baseTile;
    public List<DecorativeBuildingChance> decorativeBuildings = new();
    public int nodeCount = 5;
    public int initialSpreadRadius = 50;
    public int spreadIterations = 4;
    public float spreadChance = 0.55f; // Chance to spread to neighboring tiles
    public float diagonalSpreadModifier = 0.7f; // Makes diagonal spread less likely
}

[Serializable]
public struct DecorativeBuildingChance
{
    public CustomBuilding Building;
    public float Chance;
}

[System.Serializable]
public class JungleBiomeSettings : BiomeSettings
{
    public Decoration jungleGrassTile; // For surface blocks
    public float grassSpawnChance;
    public Decoration vineTile;
    public Vector2Int maxVineLength;
    public float vineChance = 0.3f;
}

namespace Assets.Scripts.Managers.WorldGeneration
{
    public partial class WorldGenerator : MonoBehaviour
    {
        [Header("Terrain Settings")]
        [SerializeField] private float _terrainHeightMultiplier = 10f;
        [SerializeField] private float _terrainSmoothness = 0.1f;

        [Header("Jungle Biome Settings")]
        [SerializeField] private JungleBiomeSettings jungleSettings;

        [Header("Cave Settings (Cellular Automata)")]
        [SerializeField] private float _perlinCaveThreshold = 0.55f;
        [SerializeField] private float _perlinCaveScale = 0.1f;
        [SerializeField] private int _caveSimulationSteps = 4;
        [SerializeField] private float _initialCaveChance = 0.45f;
        [SerializeField] private List<int> _birthLimit = new();
        [SerializeField] private int _deathLimit = 2;

        [Header("Ore Generation")]
        [SerializeField] private float _oreSpawnChance = 0.1f;

        [Header("Tiles")]
        [SerializeField] private TileSO _stoneTile;
        [SerializeField] private TileSO _oreTile;

        [SerializeField] private CustomBuilding _vase;
        [SerializeField] private float _vaseSpawnChance = 0.02f;

        [Header("Web Biome Settings")]
        [SerializeField] private Web _webPrefab;
        [SerializeField] private int _webNodesCount;
        [SerializeField] private int _webNodeSize;

        private WorldManager _worldManager;

        private int _worldWidth => _worldManager.WorldWidth;
        private int _worldHeight => _worldManager.WorldHeight;

        private int CHUNK_SIZE => _worldManager.CHUNK_SIZE;

        private bool _ready = false;

        public bool Ready => _ready;

        private List<Vector2Int> _freeTiles = new();

        private static readonly Vector2Int[] directions =
        {
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
            new Vector2Int(-1,  0),                    new Vector2Int(1,  0),
            new Vector2Int(-1,  1), new Vector2Int(0,  1), new Vector2Int(1,  1)
        };

        public void Generate(WorldManager wm)
        {
            _worldManager = wm;
            _worldManager.MainTilemap.ClearAllTiles();
            StartCoroutine(GenerateWorldCoroutine());
        }

        private IEnumerator GenerateWorldCoroutine()
        {
            _ready = false;
            int numChunksX = Mathf.CeilToInt((float)_worldManager.WorldWidth / CHUNK_SIZE);
            int numChunksY = Mathf.CeilToInt((float)_worldManager.WorldHeight / CHUNK_SIZE);

            // Generate base terrain and caves chunk by chunk
            int chunksToGen = numChunksX * numChunksY;
            int generatedChunks = 0;

            for (int chunkX = 0; chunkX < numChunksX; chunkX++)
            {
                for (int chunkY = 0; chunkY < numChunksY; chunkY++)
                {
                    GenerateChunk(chunkX, chunkY);
                    generatedChunks++;

                    Debug.Log($"GENERATED CHUNK {generatedChunks} OUT OF {chunksToGen}");

                    if ((chunkX * numChunksY + chunkY) % 4 == 0)
                        yield return null;
                }
            }

            for (int step = 0; step < _caveSimulationSteps; step++)
            {
                yield return StartCoroutine(ProcessCellularAutomataCoroutine());
            }

            yield return StartCoroutine(GenerateOresJobified());

            yield return StartCoroutine(GenerateVasesCoroutine());

            GenerateJungleBiome();

            GenerateWebBiome();

            _ready = true;
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
                    _freeTiles.RemoveAt(_freeTiles.IndexOf(start));
                }
                while (!_worldManager.IsEmpty(start) && currAttempts <= attemptLimit);

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

        private void GenerateChunk(int chunkX, int chunkY)
        {
            int startX = chunkX * CHUNK_SIZE;
            int startY = chunkY * CHUNK_SIZE;
            int endX = Mathf.Min(startX + CHUNK_SIZE, _worldManager.WorldWidth);
            int endY = Mathf.Min(startY + CHUNK_SIZE, _worldManager.WorldHeight);

            // Generate terrain and initial caves for this chunk
            for (int x = startX; x < endX; x++)
            {
                int groundHeight = Mathf.FloorToInt(Mathf.PerlinNoise(x * _terrainSmoothness, 0)
                    * _terrainHeightMultiplier + (_worldManager.WorldHeight / 2));

                for (int y = startY; y < endY; y++)
                {
                    // Combine terrain generation and initial cave generation
                    float caveNoise = Mathf.PerlinNoise(x * _perlinCaveScale, y * _perlinCaveScale);
                    bool shouldBeCave = caveNoise > _perlinCaveThreshold || UnityEngine.Random.value < _initialCaveChance;

                    _worldManager.SetTile(x, y, shouldBeCave ? null : _stoneTile, false);
                }
            }
        }

        [BurstCompile]
        private struct OreGenerationJob : IJobParallelFor
        {
            [ReadOnly] public int Width;
            [ReadOnly] public float OreChance;
            [ReadOnly] public uint Seed;

            [NativeDisableParallelForRestriction]
            public NativeArray<bool> IsStone;

            [NativeDisableParallelForRestriction]
            public NativeArray<bool> ShouldBeOre;

            public void Execute(int index)
            {
                // Create a thread-safe random generator with a unique seed per index.
                var random = new Unity.Mathematics.Random(Seed + (uint)index);
                if (IsStone[index] && random.NextFloat() < OreChance)
                {
                    ShouldBeOre[index] = true;
                }
            }
        }

        private IEnumerator GenerateOresJobified()
        {
            int totalTiles = _worldManager.WorldWidth * _worldManager.WorldHeight;

            var isStone = new NativeArray<bool>(totalTiles, Allocator.TempJob);
            var shouldBeOre = new NativeArray<bool>(totalTiles, Allocator.TempJob);

            for (int i = 0; i < totalTiles; i++)
            {
                int x = i % _worldManager.WorldWidth;
                int y = i / _worldManager.WorldWidth;
                isStone[i] = _worldManager.WorldData[x, y] == _stoneTile;
            }

            uint seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);

            var job = new OreGenerationJob
            {
                Width = _worldManager.WorldWidth,
                OreChance = _oreSpawnChance,
                Seed = seed,
                IsStone = isStone,
                ShouldBeOre = shouldBeOre
            };

            JobHandle handle = job.Schedule(totalTiles, 64);
            handle.Complete();

            for (int i = 0; i < totalTiles; i++)
            {
                if (shouldBeOre[i])
                {
                    int x = i % _worldManager.WorldWidth;
                    int y = i / _worldManager.WorldWidth;
                    _worldManager.SetTile(x, y, _oreTile, false);
                }

                if (i % 10000 == 0)
                    yield return null;
            }

            isStone.Dispose();
            shouldBeOre.Dispose();
        }

        private IEnumerator ProcessCellularAutomataCoroutine()
        {
            var newMap = new TileSO[_worldManager.WorldWidth, _worldManager.WorldHeight];

            for (int chunkX = 0; chunkX < _worldManager.WorldWidth; chunkX += CHUNK_SIZE)
            {
                for (int chunkY = 0; chunkY < _worldManager.WorldHeight; chunkY += CHUNK_SIZE)
                {
                    ProcessCellularAutomataChunk(chunkX, chunkY, newMap);

                    if ((chunkX + chunkY) % (CHUNK_SIZE * 2) == 0)
                        yield return null;
                }
            }

            for (int x = 0; x < _worldManager.WorldWidth; x++)
            {
                for (int y = 0; y < _worldManager.WorldHeight; y++)
                {
                    _worldManager.SetTile(x, y, newMap[x, y], false);
                }

                if (x % 100 == 0)
                    yield return null;
            }
        }

        private void ProcessCellularAutomataChunk(int startX, int startY, TileSO[,] newMap)
        {
            int endX = Mathf.Min(startX + CHUNK_SIZE, _worldManager.WorldWidth);
            int endY = Mathf.Min(startY + CHUNK_SIZE, _worldManager.WorldHeight);

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    int neighborCount = CountSolidNeighbors(_worldManager.WorldData, x, y);

                    if (_worldManager.WorldData[x, y] != null)
                    {
                        var newtile = (neighborCount >= _deathLimit) ? _worldManager.WorldData[x, y] : null;
                        newMap[x, y] = newtile;

                        if (newtile == null) _freeTiles.Add(new Vector2Int(x, y));
                    }
                    else
                    {
                        var newtile = (neighborCount >= _birthLimit.Random()) ? _stoneTile : null;
                        newMap[x, y] = newtile;

                        if (newtile == null) _freeTiles.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        private IEnumerator GenerateVasesCoroutine()
        {
            for (int x = 0; x < _worldManager.WorldWidth; x++)
            {
                for (int y = 1; y < _worldManager.WorldHeight; y++)
                {
                    if (_worldManager.WorldData[x, y] == null && _worldManager.WorldData[x, y - 1] != null)
                    {
                        if (UnityEngine.Random.value < _vaseSpawnChance)
                        {
                            _worldManager.PlaceBuilding(x, y, _vase);
                        }
                    }
                }

                if (x % 100 == 0)
                    yield return null;
            }
        }

        public List<Vector2Int> GetNeighbors(int x, int y)
        {
            List<Vector2Int> res = new List<Vector2Int>();

            foreach (var dir in directions)
            {
                int nx = x + dir.x, ny = y + dir.y;
                if (nx >= 0 && nx < _worldWidth && ny >= 0 && ny < _worldHeight)
                {
                    res.Add(new Vector2Int(nx, ny));
                }
            }

            return res;
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
    }
}
