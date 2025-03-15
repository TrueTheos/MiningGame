using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Managers.WorldGeneration
{
    public partial class WorldGenerator
    {
        private void GenerateJungleBiome()
        {
            HashSet<Vector2Int> biomeTiles = new HashSet<Vector2Int>();
            List<Vector2Int> biomeNodes = new List<Vector2Int>();

            // Create initial nodes
            for (int i = 0; i < jungleSettings.nodeCount; i++)
            {
                Vector2Int node = GetValidBiomeNode(biomeNodes, jungleSettings.initialSpreadRadius / 2);
                if (node != default)
                {
                    biomeNodes.Add(node);
                    biomeTiles.Add(node);
                    // Create initial cluster around node
                    SpreadFromPoint(node, jungleSettings.initialSpreadRadius / 3, biomeTiles);
                }
            }

            // Perform several iterations of spreading to create organic shape
            for (int iteration = 0; iteration < jungleSettings.spreadIterations; iteration++)
            {
                HashSet<Vector2Int> newTiles = new HashSet<Vector2Int>();

                foreach (var tile in biomeTiles)
                {
                    SpreadFromTile(tile, newTiles, biomeTiles);
                }

                biomeTiles.UnionWith(newTiles);
            }

            // Convert all marked tiles to jungle
            foreach (var tile in biomeTiles)
            {
                ConvertToJungle(tile.x, tile.y);
            }
        }

        private void SpreadFromPoint(Vector2Int center, int radius, HashSet<Vector2Int> biomeTiles)
        {
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(center);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                if (!biomeTiles.Add(current)) continue;

                foreach (var dir in GetRandomizedDirections())
                {
                    Vector2Int next = current + dir;

                    if (!IsValidTile(next)) continue;

                    float dist = Vector2.Distance(center, next);
                    if (dist > radius) continue;

                    // Add some randomness to the spread
                    float spreadRoll = Random.value;
                    float threshold = jungleSettings.spreadChance * (1 - dist / radius);

                    // Reduce spread chance for diagonal directions
                    if (Mathf.Abs(dir.x) + Mathf.Abs(dir.y) == 2)
                    {
                        threshold *= jungleSettings.diagonalSpreadModifier;
                    }

                    if (spreadRoll < threshold)
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        private void SpreadFromTile(Vector2Int tile, HashSet<Vector2Int> newTiles, HashSet<Vector2Int> existingTiles)
        {
            foreach (var dir in GetRandomizedDirections())
            {
                Vector2Int next = tile + dir;

                if (!IsValidTile(next)) continue;
                if (existingTiles.Contains(next)) continue;
                if (newTiles.Contains(next)) continue;

                float spreadRoll = Random.value;
                float threshold = jungleSettings.spreadChance;

                // Reduce spread chance for diagonal directions
                if (Mathf.Abs(dir.x) + Mathf.Abs(dir.y) == 2)
                {
                    threshold *= jungleSettings.diagonalSpreadModifier;
                }

                // Add noise based on position
                threshold *= Mathf.PerlinNoise(next.x * 0.1f, next.y * 0.1f) + 0.5f;

                if (spreadRoll < threshold)
                {
                    newTiles.Add(next);
                }
            }
        }

        private Vector2Int[] GetRandomizedDirections()
        {
            Vector2Int[] dirs = new Vector2Int[]
            {
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
            new Vector2Int(-1,  0),                        new Vector2Int(1,  0),
            new Vector2Int(-1,  1), new Vector2Int(0,  1), new Vector2Int(1,  1)
            };

            // Fisher-Yates shuffle
            for (int i = dirs.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = dirs[i];
                dirs[i] = dirs[j];
                dirs[j] = temp;
            }

            return dirs;
        }

        private bool IsValidTile(Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= _worldWidth || pos.y < 0 || pos.y >= _worldHeight)
                return false;

            return _worldManager.WorldData[pos.x, pos.y] == _stoneTile;
        }

        private Vector2Int GetValidBiomeNode(List<Vector2Int> existingNodes, int minDistance)
        {
            const int maxAttempts = 100;
            for (int i = 0; i < maxAttempts; i++)
            {
                Vector2Int pos = new Vector2Int(
                    Random.Range(0, _worldWidth),
                    Random.Range(_worldHeight / 4, _worldHeight * 3 / 4) // Prefer middle sections
                );

                // Check distance from other nodes
                bool tooClose = false;
                foreach (var node in existingNodes)
                {
                    if (Vector2Int.Distance(pos, node) < minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose && _worldManager.WorldData[pos.x, pos.y] == _stoneTile)
                {
                    return pos;
                }
            }
            return default;
        }
      
        private void ConvertToJungle(int x, int y)
        {
            TileSO currentTile = _worldManager.WorldData[x, y];

            if (currentTile != _stoneTile) return;

            _worldManager.SetTile(x, y, jungleSettings.baseTile, false);

            if(_worldManager.IsEmpty(x, y + 1))
            {
                if (jungleSettings.decorativeBuildings != null && jungleSettings.decorativeBuildings.Count > 0)
                {
                    var randomDecoration = jungleSettings.decorativeBuildings.Random();
                    if(randomDecoration.Building != null)
                    {
                        if (Random.Range(0f, 1f) < randomDecoration.Chance)
                        {
                            _worldManager.TryPlace(x, y + 1, randomDecoration.Building);
                        }
                    }
                }
            }

            if (Random.value < jungleSettings.grassSpawnChance)
            {
                _worldManager.TryPlace(x, y + 1, jungleSettings.jungleGrassTile);
            }

            // Generate vines below empty spaces
            if (_worldManager.IsEmpty(x, y - 1) && Random.value < jungleSettings.vineChance)
            {
                GenerateVine(x, y - 1);
            }
        }

        private void GenerateVine(int x, int startY)
        {
            int length = jungleSettings.maxVineLength.Random();

            for (int y = startY; y > startY - length && y >= 0; y--)
            {
                if (!_worldManager.IsEmpty(x, y)) break;
                _worldManager.TryPlace(x, y, jungleSettings.vineTile);
            }
        }
    }
}
