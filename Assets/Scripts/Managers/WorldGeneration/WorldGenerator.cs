using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public struct DecorativeBuildingChance
{
    public CustomBuilding Building;
    public float Chance;
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

    public class WorldGenerator : MonoBehaviour
    {
        [Header("Ore Generation")]
        [SerializeField] private float _oreSpawnChance = 0.1f;

        [Header("Tiles")]
        [SerializeField] private TileSO _stoneTile;
        [SerializeField] private TileSO _oreTile;

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

        [SerializeField] private List<WorldLayer> _layers = new List<WorldLayer>();

        private Queue<WorldLayer> _layersQueue = new Queue<WorldLayer>();

        private int _layerStartY;

        private void Awake()
        {
            foreach (var layer in _layers)
            {
                _layersQueue.Enqueue(layer);
            }

            _layerStartY = GetHeight();
        }

        public void SetGenText(string text)
        {
            _currentInfo.text = text;
        }

        public int GetHeight()
        {
            return _layers.Sum(x => x.Height);
        }

        public void Generate(WorldManager wm)
        {
            _worldManager = wm;
            _chunkSize = WorldManager.CHUNK_SIZE;
            _worldManager.MainTilemap.ClearAllTiles();
            SetupGeneration();
            GenerateNextLayer();
        }

        private void SetupGeneration()
        {
            _generationUIParent.gameObject.SetActive(true);
            _ready = false;
        }

        public void GenerateNextLayer()
        {
            if(_layersQueue == null || _layersQueue.Count == 0)
            {
                OnGenerateAllLayers();
                return;
            }

            var nextLayer = _layersQueue.Dequeue();

            _layerStartY -= nextLayer.Height;

            nextLayer.Generate(_layerStartY);
        }

        private void OnGenerateAllLayers()
        {
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
                isStone[i] = _worldManager.GetTile(x, y) == _stoneTile;
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
    }
}
