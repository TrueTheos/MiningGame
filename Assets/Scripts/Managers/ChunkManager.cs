using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class ChunkManager : MonoBehaviour
{
    public static ChunkManager Instance;

    private WorldManager _worldManager;
    private Player _player;

    private Chunk[,] _chunks;
    private HashSet<Chunk> _visibleChunks = new HashSet<Chunk>();

    private int _chunkSize;
    private int _renderDistance => _worldManager.RENDER_DISTANCE_CHUNKS;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _worldManager = WorldManager.Instance;
        _player = Player.Instance;
        _chunkSize = WorldManager.CHUNK_SIZE;

        int numChunksX = Mathf.CeilToInt((float)_worldManager.WorldWidth / _chunkSize);
        int numChunksY = Mathf.CeilToInt((float)_worldManager.WorldHeight / _chunkSize);

        _chunks = new Chunk[numChunksX, numChunksY]; 

        for (int chunkX = 0; chunkX < numChunksX; chunkX++)
        {
            for (int chunkY = 0; chunkY < numChunksY; chunkY++)
            {
                int startX = chunkX * _chunkSize;
                int startY = chunkY * _chunkSize;

                GameObject newChunk = new GameObject($"Chunk_{chunkX}_{chunkY}");
                newChunk.transform.SetParent(transform);
                Chunk chunkComponent = newChunk.AddComponent<Chunk>();
                chunkComponent.Init(chunkX, chunkY, _chunkSize);
                _chunks[chunkX, chunkY] = chunkComponent;
            }
        }
    }

    private void Update()
    {
        UpdateVisibleChunks();
    }

    private void FixedUpdate()
    {
        foreach (var chunk in _visibleChunks)
        {
            for(int i = chunk.ChunkObjects.Count - 1; i >= 0; i--)
            {
                UpdateChunkObjectPosition(chunk, chunk.ChunkObjects[i]);
            }
        }
    }

    public void UpdateChunkObjectPosition(Chunk lastChunk, IChunkObject obj)
    {
        Chunk currentChunk = GetChunkByWorldPos(obj.Position);

        if (lastChunk != currentChunk)
        {
            lastChunk.RemoveObject(obj);

            currentChunk.AddObject(obj);

            MonoBehaviour mono = obj as MonoBehaviour;

            if (mono != null)
            {
                mono.gameObject.SetActive(_visibleChunks.Contains(currentChunk));
            }
        }
    }

    public Chunk GetChunkByWorldPos(Vector2 pos)
    {
        int chunkIndexX = Mathf.FloorToInt(pos.x / _chunkSize);
        int chunkIndexY = Mathf.FloorToInt(pos.y / _chunkSize);

        if (!IsChunksInBounds(chunkIndexX, chunkIndexY))
        {
            Debug.LogError("NULL CHUNK?");
            return null;
        }
        return _chunks[chunkIndexX, chunkIndexY];
    }

    public Chunk GetChunkByGridPos(int x, int y)
    {
        if (!IsChunksInBounds(x, y)) return null;
        return _chunks[x, y];
    }

    private bool IsChunksInBounds(int chunkX, int chunkY)
    {
        if (chunkX < 0 || chunkX >= _chunks.GetLength(0) || chunkY < 0 || chunkY >= _chunks.GetLength(1)) return false;
        return true;
    }

    public void RenderChunk(int chunkX, int chunkY)
    {
        if (!IsChunksInBounds(chunkX, chunkY)) return;

        if (_chunks[chunkX, chunkY] == null) return;
        _chunks[chunkX, chunkY].Render();
    }

    public void AddObjectToChunk(IChunkObject obj)
    {
        var chunk = GetChunkByWorldPos(obj.Position);
        if (chunk == null)
        {
            Debug.LogError($"ADD TO NULL CHUNK? {obj.Position}");
            return;
        }

        chunk.AddObject(obj);
    }

    private void UpdateVisibleChunks()
    {
        if (_player == null) return;

        var playerChunk = GetChunkByWorldPos(_player.Pos);
        if (playerChunk == null) return;

        HashSet<Chunk> newVisibleChunks = new();

        for (int x = playerChunk.ChunkPosX - _renderDistance; x <= playerChunk.ChunkPosX + _renderDistance; x++)
        {
            for (int y = playerChunk.ChunkPosY - _renderDistance; y <= playerChunk.ChunkPosY + _renderDistance; y++)
            {
                var chunk = GetChunkByGridPos(x,y);
                if(chunk != null) newVisibleChunks.Add(chunk);
            }
        }

        foreach (var chunk in newVisibleChunks)
        {
            if(!_visibleChunks.Contains(chunk))
            {
                chunk.SetActive(true);
            }
        }

        foreach (var chunk in _visibleChunks)
        {
            if (!newVisibleChunks.Contains(chunk))
            {
                chunk.SetActive(false);
            }
        }

        _visibleChunks = newVisibleChunks;
    }
}

public class Chunk : MonoBehaviour
{
    public int ChunkPosX { get; private set; }
    public int ChunkPosY { get; private set; }

    private int _chunkSize;

    private int _worldX => ChunkPosX * _chunkSize;
    private int _worldY => ChunkPosY * _chunkSize;

    public List<IChunkObject> ChunkObjects = new List<IChunkObject>();

    private bool _active = false;
    public bool Rendered { get; private set; }

    public void Init(int x, int y, int chunkSize)
    {
        ChunkPosX = x;
        ChunkPosY = y;
        _chunkSize = chunkSize;
    }

    public void SetActive(bool active)
    {
        if (_active == active)
            return;

        _active = active;
        gameObject.SetActive(active);
        foreach (IChunkObject obj in ChunkObjects)
        {
            MonoBehaviour mb = obj as MonoBehaviour;
            if (mb != null)
            {
                mb.gameObject.SetActive(active);
            }
        }
    }

    public void AddObject(IChunkObject obj)
    {
        if (!ChunkObjects.Contains(obj))
        {
            ChunkObjects.Add(obj);
            MonoBehaviour mb = obj as MonoBehaviour;
            if (mb == null) return;
            mb.transform.SetParent(transform);
            if (mb != null)
            {
                mb.gameObject.SetActive(_active);
            }
        }
    }

    public void RemoveObject(IChunkObject obj)
    {
        if (ChunkObjects.Contains(obj))
            ChunkObjects.Remove(obj);
    }

    public void Render()
    {
        if (Rendered) return;

        for (int x = _worldX; x <= _worldX + _chunkSize; x++)
        {
            for (int y = _worldY; y <= _worldY + _chunkSize; y++)
            {
                WorldManager.Instance.ShowTile(x, y);
            }
        }

        Rendered = true;
    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.color = Rendered ? Color.green : Color.red;

        Gizmos.DrawWireCube(new(_worldX + _chunkSize / 2, _worldY + _chunkSize / 2), new(_chunkSize, _chunkSize));
    }
}

public interface IChunkObject
{
    Vector2 Position { get; }

    /* public Vector2 Position
    {
        get
        {
            if (this == null || transform == null) return Vector2.zero;
            return transform.position;
        }
    }*/
}
