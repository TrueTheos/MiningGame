using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
    public float spreadChance = 0.55f;
    public float diagonalSpreadModifier = 0.7f;
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
    public Decoration jungleGrassTile;
    public float grassSpawnChance;
    public Decoration vineTile;
    public Vector2Int maxVineLength;
    public float vineChance = 0.3f;
}

namespace Assets.Scripts.Managers.WorldGeneration
{
    [BurstCompile]
    public struct TerrainGenerationJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<byte> TerrainData;
        public int Width;
        public int Height;
        public float TerrainSmoothness;
        public float TerrainHeightMultiplier;
        public float PerlinCaveScale;
        public float PerlinCaveThreshold;
        public float InitialCaveChance;
        public uint Seed;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;

            var random = new Unity.Mathematics.Random(Seed + (uint)index);

            float groundHeight = noise.cnoise(new float2(x * TerrainSmoothness, 0))
                * TerrainHeightMultiplier + (Height / 2);

            float caveNoise = noise.cnoise(new float2(x * PerlinCaveScale, y * PerlinCaveScale));
            bool shouldBeCave = caveNoise > PerlinCaveThreshold || random.NextFloat() < InitialCaveChance;

            TerrainData[index] = (byte)(shouldBeCave ? 0 : 1);
        }
    }

    [BurstCompile]
    public struct CellularAutomataJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> CurrentMap;
        [WriteOnly] public NativeArray<byte> NewMap;
        public int Width;
        public int Height;
        public int DeathLimit;
        [ReadOnly] public NativeArray<int> BirthLimits;
        public uint Seed;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;
            var random = new Unity.Mathematics.Random(Seed + (uint)index);

            int neighborCount = CountNeighbors(x, y);
            bool currentCell = CurrentMap[index] == 1;

            if (currentCell)
            {
                NewMap[index] = (byte)(neighborCount >= DeathLimit ? 1 : 0);
            }
            else
            {
                int randomBirthLimit = BirthLimits[random.NextInt(0, BirthLimits.Length)];
                NewMap[index] = (byte)(neighborCount >= randomBirthLimit ? 1 : 0);
            }
        }

        private int CountNeighbors(int x, int y)
        {
            int count = 0;
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;

                    int nx = x + i;
                    int ny = y + j;

                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                    {
                        count++;
                        continue;
                    }

                    count += CurrentMap[nx + ny * Width];
                }
            }
            return count;
        }
    }

    [BurstCompile]
    public struct OreGenerationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> TerrainData;
        [WriteOnly] public NativeArray<bool> ShouldBeOre;
        public float OreChance;
        public uint Seed;

        public void Execute(int index)
        {
            var random = new Unity.Mathematics.Random(Seed + (uint)index);
            if (TerrainData[index] == 1 && random.NextFloat() < OreChance)
            {
                ShouldBeOre[index] = true;
            }
        }
    }

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

        [Header("UI Info")]
        [SerializeField] private GameObject _generationUIParent;
        [SerializeField] private TextMeshProUGUI _currentInfo;

        [SerializeField] private GameObject _borderPrefab;

        private WorldManager _worldManager;

        private int _worldWidth => _worldManager.WorldWidth;
        private int _worldHeight => _worldManager.WorldHeight;

        private int _chunkSize;

        private bool _ready = false;

        public bool Ready => _ready;

        private List<Vector2Int> _freeTiles = new();

        private Vector2Int[] sixDirections =
        {
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
            new Vector2Int(-1,  0),                    new Vector2Int(1,  0),
            new Vector2Int(-1,  1), new Vector2Int(0,  1), new Vector2Int(1,  1)
        };

        public void Generate(WorldManager wm)
        {
            _worldManager = wm;
            _chunkSize = WorldManager.CHUNK_SIZE;
            _worldManager.MainTilemap.ClearAllTiles();
            StartCoroutine(GenerateWorldCoroutine());
        }

        private IEnumerator GenerateWorldCoroutine()
        {
            _generationUIParent.gameObject.SetActive(true);

            _ready = false;
            int numChunksX = Mathf.CeilToInt((float)_worldManager.WorldWidth / _chunkSize);
            int numChunksY = Mathf.CeilToInt((float)_worldManager.WorldHeight / _chunkSize);

            // Generate base terrain and caves chunk by chunk
            int chunksToGen = numChunksX * numChunksY;
            int generatedChunks = 0;

            _currentInfo.text = "Generating chunks...";
            for (int chunkX = 0; chunkX < numChunksX; chunkX++)
            {
                for (int chunkY = 0; chunkY < numChunksY; chunkY++)
                {
                    GenerateChunk(chunkX, chunkY);
                    generatedChunks++;

                    if ((chunkX * numChunksY + chunkY) % 4 == 0)
                        yield return null;
                }
            }

            for (int step = 0; step < _caveSimulationSteps; step++)
            {
                _currentInfo.text = "Generating caves...";
                yield return StartCoroutine(ProcessCellularAutomataCoroutine());
            }

            _currentInfo.text = "Generating ores...";
            yield return StartCoroutine(GenerateOresJobified());

            _currentInfo.text = "Spawning vases...";
            yield return StartCoroutine(GenerateVasesCoroutine());

            _currentInfo.text = "Spreading jungle...";
            GenerateJungleBiome();

            _currentInfo.text = "Spawning webs...";
            GenerateWebBiome();

            CreateBorder();
            _ready = true;

            Destroy(_generationUIParent);
        }

        private void CreateBorder()
        {
            float width = _worldWidth;
            float height = _worldHeight;

            float borderThickness = 5;

            Vector2 bottomPosition = new Vector2(width / 2, -borderThickness / 2);
            GameObject bottomBorder = Instantiate(_borderPrefab, bottomPosition, Quaternion.identity, transform);
            bottomBorder.name = "BottomBorder";
            bottomBorder.transform.localScale = new Vector3(width + 2 * borderThickness, borderThickness, 1f);

            // Instantiate the top border.
            Vector2 topPosition = new Vector2(width / 2, height + borderThickness / 2);
            GameObject topBorder = Instantiate(_borderPrefab, topPosition, Quaternion.identity, transform);
            topBorder.name = "TopBorder";
            topBorder.transform.localScale = new Vector3(width + 2 * borderThickness, borderThickness, 1f);

            // Instantiate the left border.
            // It should span the full height plus extra on top and bottom.
            Vector2 leftPosition = new Vector2(-borderThickness / 2, height / 2);
            GameObject leftBorder = Instantiate(_borderPrefab, leftPosition, Quaternion.identity, transform);
            leftBorder.name = "LeftBorder";
            leftBorder.transform.localScale = new Vector3(borderThickness, height + 2 * borderThickness, 1f);

            // Instantiate the right border.
            Vector2 rightPosition = new Vector2(width + borderThickness / 2, height / 2);
            GameObject rightBorder = Instantiate(_borderPrefab, rightPosition, Quaternion.identity, transform);
            rightBorder.name = "RightBorder";
            rightBorder.transform.localScale = new Vector3(borderThickness, height + 2 * borderThickness, 1f);
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
            int startX = chunkX * _chunkSize;
            int startY = chunkY * _chunkSize;
            int endX = Mathf.Min(startX + _chunkSize, _worldManager.WorldWidth);
            int endY = Mathf.Min(startY + _chunkSize, _worldManager.WorldHeight);

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

            for (int chunkX = 0; chunkX < _worldManager.WorldWidth; chunkX += _chunkSize)
            {
                for (int chunkY = 0; chunkY < _worldManager.WorldHeight; chunkY += _chunkSize)
                {
                    ProcessCellularAutomataChunk(chunkX, chunkY, newMap);

                    if ((chunkX + chunkY) % (_chunkSize * 2) == 0)
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
            int endX = Mathf.Min(startX + _chunkSize, _worldManager.WorldWidth);
            int endY = Mathf.Min(startY + _chunkSize, _worldManager.WorldHeight);

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    int neighborCount = 0;

                    for (int nx = x - 1; nx <= x + 1; nx++)
                    {
                        for (int ny = y - 1; ny <= y + 1; ny++)
                        {
                            if (nx == x && ny == y) continue; // Skip the current cell

                            if (nx >= 0 && nx < _worldWidth && ny >= 0 && ny < _worldHeight)
                            {
                                if (_worldManager.WorldData[nx, ny] != null) neighborCount++;
                            }
                            else
                            {
                                neighborCount++; // Treat out-of-bounds as solid
                            }
                        }
                    }

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

            foreach (var dir in sixDirections)
            {
                int nx = x + dir.x, ny = y + dir.y;
                if (nx >= 0 && nx < _worldWidth && ny >= 0 && ny < _worldHeight)
                {
                    res.Add(new Vector2Int(nx, ny));
                }
            }

            return res;
        }
    }
}
