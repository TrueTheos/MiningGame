using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CustomReverbZone : MonoBehaviour
{
    public AudioReverbZone reverbZone;
    public PolygonCollider2D zoneCollider;

    private static readonly ReverbParameters baseParameters = new ReverbParameters
    {
        room = -1000,
        roomHF = 0,
        roomLF = 0,
        decayTime = 2.91f,
        decayHFRatio = 1.3f,
        reflections = -602,
        reflectionsDelay = 0.015f,
        reverb = -302,
        reverbDelay = 0.022f,
        diffusion = 100.0f,
        density = 100.0f,
        HFReference = 5000.0f,
        LFReference = 250.0f
    };

    private struct ReverbParameters
    {
        public int room;
        public int roomHF;
        public int roomLF;
        public float decayTime;
        public float decayHFRatio;
        public int reflections;
        public float reflectionsDelay;
        public int reverb;
        public float reverbDelay;
        public float diffusion;
        public float density;
        public float HFReference;
        public float LFReference;
    }

    public void Initialize(List<Vector2> points)
    {
        zoneCollider = gameObject.AddComponent<PolygonCollider2D>();
        zoneCollider.isTrigger = true;
        zoneCollider.points = points.Select(p => (Vector2)transform.InverseTransformPoint(p)).ToArray();

        reverbZone = gameObject.AddComponent<AudioReverbZone>();
        reverbZone.reverbPreset = AudioReverbPreset.User;

        reverbZone.room = baseParameters.room;
        reverbZone.roomHF = baseParameters.roomHF;
        reverbZone.decayTime = baseParameters.decayTime;
        reverbZone.decayHFRatio = baseParameters.decayHFRatio;
        reverbZone.reflections = baseParameters.reflections;
        reverbZone.reflectionsDelay = baseParameters.reflectionsDelay;
        reverbZone.reverb = baseParameters.reverb;
        reverbZone.reverbDelay = baseParameters.reverbDelay;
        reverbZone.diffusion = baseParameters.diffusion;
        reverbZone.density = baseParameters.density;
        reverbZone.HFReference = baseParameters.HFReference;
        reverbZone.LFReference = baseParameters.LFReference;
    }
}

public class CaveReverbManager : MonoBehaviour
{
    public static CaveReverbManager Instance;

    [System.Serializable]
    public class ReverbSettings
    {
        [Header("Cave Size Settings")]
        public float minCaveSize = 50;
        public float maxCaveSize = 500;
    }

    public ReverbSettings reverbSettings;
    private Dictionary<Vector2Int, int> tileToRegionMap = new Dictionary<Vector2Int, int>();
    private Dictionary<int, CustomReverbZone> zones = new Dictionary<int, CustomReverbZone>();
    private Dictionary<int, HashSet<Vector2Int>> regionTiles = new Dictionary<int, HashSet<Vector2Int>>();
    private Transform playerTransform;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        playerTransform = PlayerMovement.Instance.transform;
        StartCoroutine(UpdateReverbRoutine());
    }

    public void RecalculateZone(int x, int y)
    {
        Vector2Int changedTile = new Vector2Int(x, y);
        List<int> affectedRegions = new List<int>();

        // Check all stored regions to see if the changed tile is inside or adjacent.
        foreach (var kvp in regionTiles)
        {
            int regionId = kvp.Key;
            HashSet<Vector2Int> region = kvp.Value;

            // Check if the changed tile is in the region or touches it.
            if (region.Contains(changedTile) ||
                changedTile.GetNeighbors().Any(n => region.Contains(n)))
            {
                affectedRegions.Add(regionId);
            }
        }

        // For each affected region, recalculate the zone.
        foreach (int id in affectedRegions)
        {
            // Remove the old zone.
            if (zones.TryGetValue(id, out CustomReverbZone zone))
            {
                Destroy(zone.gameObject);
                zones.Remove(id);
            }
            regionTiles.Remove(id);

            // If the changed tile now represents an open space, recalc the region
            if (WorldManager.Instance.WorldData[x, y] == null)
            {
                var newRegion = FloodFillRegion(changedTile, id, WorldManager.Instance.WorldData);
                if (newRegion.Count >= reverbSettings.minCaveSize)
                {
                    CreateReverbZone(newRegion, id);
                }
            }
        }
    }

    private IEnumerator UpdateReverbRoutine()
    {
        while (!WorldManager.Instance.Ready)
            yield return new WaitForSeconds(1f);

        UpdateCaveRegions(WorldManager.Instance.WorldData);

        Vector2 playerPos = playerTransform.position;

        while (true)
        {
            playerPos = playerTransform.position;
            foreach (var zone in zones.Values)
            {
                if (zone.zoneCollider.OverlapPoint(playerPos))
                {
                    zone.reverbZone.enabled = true;
                }
                else
                {
                    zone.reverbZone.enabled = false;
                }
            }
            yield return new WaitForSeconds(.1f);
        }
    }

    public void UpdateCaveRegions(TileSO[,] tileMap)
    {
        tileToRegionMap.Clear();

        int currentRegion = 0;
        int width = WorldManager.Instance.WorldWidth;
        int height = WorldManager.Instance.WorldHeight;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (tileMap[x, y] == null && !tileToRegionMap.ContainsKey(pos))
                {
                    var region = FloodFillRegion(pos, currentRegion, tileMap);
                    if (region.Count >= reverbSettings.minCaveSize)
                    {
                        CreateReverbZone(region, currentRegion);
                    }
                    currentRegion++;
                }
            }
        }
    }

    private void CreateReverbZone(HashSet<Vector2Int> region, int regionId)
    {
        regionTiles[regionId] = region;

        Vector2 center = CalculateRegionCenter(region);
        GameObject zoneObj = new GameObject($"CaveReverbZone_{regionId}");
        zoneObj.transform.position = center;
        zoneObj.transform.SetParent(transform);

        CustomReverbZone zone = zoneObj.AddComponent<CustomReverbZone>();
        // Use a convex hull to create a boundary that better matches the cave’s shape.
        List<Vector2> boundaryPoints = ComputeConvexHull(region.Select(p => new Vector2(p.x, p.y)).ToList());
        zone.Initialize(boundaryPoints);

        float maxRadius = 0f;
        foreach (Vector2 point in boundaryPoints)
        {
            float distance = Vector2.Distance(center, point);
            if (distance > maxRadius)
                maxRadius = distance;
        }
        zone.reverbZone.minDistance = maxRadius * 0.8f;
        zone.reverbZone.maxDistance = maxRadius;

        zones[regionId] = zone;
    }

    private HashSet<Vector2Int> FloodFillRegion(Vector2Int start, int regionId, TileSO[,] tileMap)
    {
        HashSet<Vector2Int> region = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (!WorldManager.Instance.IsTileInBounds(current.x, current.y) ||
                tileMap[current.x, current.y] != null ||
                region.Contains(current))
                continue;

            region.Add(current);
            tileToRegionMap[current] = regionId;

            queue.Enqueue(current + Vector2Int.up);
            queue.Enqueue(current + Vector2Int.down);
            queue.Enqueue(current + Vector2Int.left);
            queue.Enqueue(current + Vector2Int.right);
        }

        return region;
    }

    private List<Vector2> ComputeConvexHull(List<Vector2> points)
    {
        if (points.Count <= 1)
            return new List<Vector2>(points);

        List<Vector2> sorted = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
        List<Vector2> lower = new List<Vector2>();
        foreach (Vector2 p in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
            {
                lower.RemoveAt(lower.Count - 1);
            }
            lower.Add(p);
        }

        List<Vector2> upper = new List<Vector2>();
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            Vector2 p = sorted[i];
            while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0)
            {
                upper.RemoveAt(upper.Count - 1);
            }
            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);

        List<Vector2> hull = new List<Vector2>();
        hull.AddRange(lower);
        hull.AddRange(upper);
        return hull;
    }

    private float Cross(Vector2 o, Vector2 a, Vector2 b)
    {
        return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }

    private Vector2 CalculateRegionCenter(HashSet<Vector2Int> tiles)
    {
        if (tiles == null || tiles.Count == 0)
            return Vector2.zero;

        float sumX = 0, sumY = 0;
        foreach (var tile in tiles)
        {
            sumX += tile.x;
            sumY += tile.y;
        }
        return new Vector2(sumX / tiles.Count, sumY / tiles.Count);
    }
}