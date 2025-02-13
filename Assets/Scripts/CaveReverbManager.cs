using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CustomReverbZone : MonoBehaviour
{
    public AudioReverbZone reverbZone;
    public PolygonCollider2D zoneCollider;
    public float currentIntensity = 0f;
    public float targetIntensity = 0f;

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

        UpdateReverbIntensity(0f);
    }

    public void UpdateReverbIntensity(float intensity)
    {
        currentIntensity = intensity;

        intensity = Mathf.Clamp01(intensity);

        reverbZone.room = Mathf.RoundToInt(Mathf.Lerp(0, baseParameters.room, intensity));
        reverbZone.roomHF = Mathf.RoundToInt(Mathf.Lerp(0, baseParameters.roomHF, intensity));
        reverbZone.decayTime = Mathf.Lerp(0.1f, baseParameters.decayTime, intensity);
        reverbZone.decayHFRatio = Mathf.Lerp(0.1f, baseParameters.decayHFRatio, intensity);
        reverbZone.reflections = Mathf.RoundToInt(Mathf.Lerp(0, baseParameters.reflections, intensity));
        reverbZone.reflectionsDelay = Mathf.Lerp(0, baseParameters.reflectionsDelay, intensity);
        reverbZone.reverb = Mathf.RoundToInt(Mathf.Lerp(0, baseParameters.reverb, intensity));
        reverbZone.reverbDelay = Mathf.Lerp(0, baseParameters.reverbDelay, intensity);
        reverbZone.diffusion = Mathf.Lerp(0, baseParameters.diffusion, intensity);
        reverbZone.density = Mathf.Lerp(0, baseParameters.density, intensity);
        reverbZone.HFReference = baseParameters.HFReference;
        reverbZone.LFReference = baseParameters.LFReference;
    }
}

public class CaveReverbManager : MonoBehaviour
{
    [System.Serializable]
    public class ReverbSettings
    {
        [Header("Cave Size Settings")]
        public float minCaveSize = 50;
        public float maxCaveSize = 500;

        [Header("Transition Settings")]
        public float transitionSpeed = 2f;  // Seconds to transition between reverb states
        public float updateInterval = 0.1f;  // How often to check player position
    }

    public ReverbSettings reverbSettings;
    private Dictionary<Vector2Int, int> tileToRegionMap = new Dictionary<Vector2Int, int>();
    private Dictionary<int, CustomReverbZone> activeZones = new Dictionary<int, CustomReverbZone>();
    private Transform playerTransform;

    private void Start()
    {
        playerTransform = PlayerMovement.Instance.transform;
        StartCoroutine(UpdateReverbRoutine());
    }

    private System.Collections.IEnumerator UpdateReverbRoutine()
    {
        while (!WorldManager.Instance.Ready)
            yield return new WaitForSeconds(1f);

        UpdateCaveRegions(WorldManager.Instance.WorldData);

        while (true)
        {
            foreach (var zone in activeZones.Values)
            {
                if (zone.currentIntensity != zone.targetIntensity)
                {
                    zone.currentIntensity = Mathf.MoveTowards(
                        zone.currentIntensity,
                        zone.targetIntensity,
                        reverbSettings.transitionSpeed * Time.deltaTime
                    );
                    zone.UpdateReverbIntensity(zone.currentIntensity);
                }
            }
            yield return new WaitForSeconds(reverbSettings.updateInterval);
        }
    }

    public void UpdateCaveRegions(TileSO[,] tileMap)
    {
        tileToRegionMap.Clear();
        CleanupOldZones();

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
        Vector2 center = CalculateRegionCenter(region);
        GameObject zoneObj = new GameObject($"CaveReverbZone_{regionId}");
        zoneObj.transform.position = center;
        zoneObj.transform.SetParent(transform);

        CustomReverbZone zone = zoneObj.AddComponent<CustomReverbZone>();
        List<Vector2> boundaryPoints = CreateSimplifiedBoundary(region.Select(p => new Vector2(p.x, p.y)).ToList());
        zone.Initialize(boundaryPoints);

        float intensity = Mathf.InverseLerp(reverbSettings.minCaveSize, reverbSettings.maxCaveSize, region.Count);
        zone.targetIntensity = intensity;

        activeZones[regionId] = zone;
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

    private List<Vector2> CreateSimplifiedBoundary(List<Vector2> points)
    {
        int numPoints = Mathf.Min(points.Count, 16);
        List<Vector2> simplified = new List<Vector2>();

        for (int i = 0; i < numPoints; i++)
        {
            float angle = (i * 2 * Mathf.PI) / numPoints;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 furthestPoint = points.OrderByDescending(p => Vector2.Dot(p - points[0], direction)).First();
            simplified.Add(furthestPoint);
        }

        return simplified;
    }

    private void CleanupOldZones()
    {
        foreach (var zone in activeZones.Values)
        {
            if (zone != null)
                Destroy(zone.gameObject);
        }
        activeZones.Clear();
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