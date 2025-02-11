using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
[Serializable]
public struct LightSource
{
    public float LightPower;
    public Color Color;
}
 
public class LightManager : MonoBehaviour
{
    public static LightManager Instance;
 
    private WorldManager _worldManager;
 
    [SerializeField] private int _tileSize = 8;
    [SerializeField] private float _playerLightRadius = 5f;
    [SerializeField] private float _playerLightIntensity = 1.0f;
    [SerializeField] private float _lightLoseRate;
    [SerializeField] private float _wallLightLoseRate = .2f;
    [SerializeField] private PlayerMovement _player;
    [SerializeField] private Material _smoothingMaterial;
 
    private float[,] _lightMap;
    private Color[,] _lightColors;
    private float[,] _colorInfluence;
    private Dictionary<Vector2Int, LightSource> _persistentLights = new();
    private Texture2D _lightTexture;
    private SpriteRenderer _lightRenderer;
    private Camera _mainCamera;
 
    private int _worldWidth => _worldManager.WorldWidth;
    private int _worldHeight => _worldManager.WorldHeight;
    private int _textureWidth, _textureHeight;
    private int _texOriginX, _texOriginY;
    [SerializeField] private int _textureMargin = 2;
 
    private void Awake()
    {
        Instance = this;
    }
 
    private void Start()
    {
        if (_player == null) Debug.LogError("ASSIGN PLAYER");
 
        _worldManager = WorldManager.Instance;
 
        _mainCamera = Camera.main;
 
        _lightMap = new float[_worldWidth, _worldHeight];
        _colorInfluence = new float[_worldWidth, _worldHeight];
        _lightColors = new Color[_worldWidth, _worldHeight];
 
        for (int x = 0; x < _worldWidth; x++)
        {
            for (int y = 0; y < _worldHeight; y++)
            {
                _lightColors[x, y] = Color.white;
                _colorInfluence[x, y] = 0f;
            }
        }
 
        UpdateTextureRegion();
 
        _lightTexture = new Texture2D(_textureWidth, _textureHeight);
        _lightTexture.filterMode = FilterMode.Point;
 
        GameObject lightObject = new GameObject("LightingOverlay");
        _lightRenderer = lightObject.AddComponent<SpriteRenderer>();
        _lightRenderer.sprite = Sprite.Create(
            _lightTexture,
            new Rect(0, 0, _textureWidth, _textureHeight),
            new Vector2(0.5f, 0.5f));
        _lightRenderer.material = _smoothingMaterial;
        // Position the overlay at the center of the texture region in world space.
        _lightRenderer.transform.position = new Vector2(_texOriginX + _textureWidth / 2f,
                                                          _texOriginY + _textureHeight / 2f);
        _lightRenderer.transform.localScale *= 100f;
        _lightRenderer.sortingOrder = 10;
    }
 
    void UpdateTextureRegion()
    {
        Vector3 camPos = _mainCamera.transform.position;
        float camHalfWidth = _mainCamera.orthographicSize * _mainCamera.aspect;
        float camHalfHeight = _mainCamera.orthographicSize;
 
        // Compute visible region bounds and add a margin.
        _texOriginX = Mathf.FloorToInt(camPos.x - camHalfWidth) - _textureMargin;
        _texOriginY = Mathf.FloorToInt(camPos.y - camHalfHeight) - _textureMargin;
        int maxX = Mathf.CeilToInt(camPos.x + camHalfWidth) + _textureMargin;
        int maxY = Mathf.CeilToInt(camPos.y + camHalfHeight) + _textureMargin;
 
        _textureWidth = maxX - _texOriginX;
        _textureHeight = maxY - _texOriginY;
    }
 
    void Update()
    {
        if (_worldManager == null || !_worldManager.Ready) return;
        Vector3 camPos = _mainCamera.transform.position;
        int newOriginX = Mathf.FloorToInt(camPos.x - _mainCamera.orthographicSize * _mainCamera.aspect) - _textureMargin;
        int newOriginY = Mathf.FloorToInt(camPos.y - _mainCamera.orthographicSize) - _textureMargin;
        if (newOriginX != _texOriginX || newOriginY != _texOriginY)
        {
            UpdateTextureRegion();
            // Re-create the texture and sprite if the size changes.
            _lightTexture = new Texture2D(_textureWidth, _textureHeight);
            _lightTexture.filterMode = FilterMode.Point;
            _lightRenderer.sprite = Sprite.Create(
                _lightTexture,
                new Rect(0, 0, _textureWidth, _textureHeight),
                new Vector2(0.5f, 0.5f));
            _lightRenderer.transform.position = new Vector2(_texOriginX + (_textureWidth / 2f),
                                                              _texOriginY + (_textureHeight / 2f));
        }
 
        UpdateLighting();
    }
 
    void UpdateLighting()
    {
        for (int y = 0; y < _textureHeight; y++)
        {
            for (int x = 0; x < _textureWidth; x++)
            {
                int worldX = _texOriginX + x;
                int worldY = _texOriginY + y;
                if (worldX >= 0 && worldY >= 0 && worldX < _worldWidth && worldY < _worldHeight)
                {
                    ComputeLightForTile(worldX, worldY);
                }
            }
        }
 
        ApplyLightingToTexture();
    }
 
    public void AddLight(int x, int y, LightSource light)
    {
        Vector2Int position = new Vector2Int(x, y);
        if (_persistentLights.ContainsKey(position))
        {
            _persistentLights[position] = light;
        }
        else
        {
            _persistentLights.Add(position, light);
        }
    }
 
    public void RemoveLight(int x, int y)
    {
        Vector2Int position = new Vector2Int(x, y);
        if (_persistentLights.ContainsKey(position))
        {
            _persistentLights.Remove(position);
        }
    }
 
    void ComputeLightForTile(int x, int y)
    {
        float light = 0f;
        Vector2Int currentTile = new Vector2Int(x, y);
 
        if (_persistentLights.ContainsKey(currentTile))
        {
            var lightSource = _persistentLights[currentTile];
            light = Mathf.Max(light, lightSource.LightPower);
        }
 
        int[][] directions = new int[][] {
            new int[] { 1, 0 },
            new int[] { -1, 0 },
            new int[] { 0, 1 },
            new int[] { 0, -1 }
        };
 
        foreach (var dir in directions)
        {
            int nx = x + dir[0];
            int ny = y + dir[1];
 
            if (nx >= 0 && ny >= 0 && nx < _worldWidth && ny < _worldHeight)
            {
                float neighborLight = !_worldManager.IsLightBlocker(nx, ny)
                    ? _lightMap[nx, ny] * _lightLoseRate
                    : _lightMap[nx, ny] * _wallLightLoseRate;
 
                light = Mathf.Max(light, neighborLight);
            }
        }
 
        // Obstruct light if the tile is occupied.
        if (_worldManager.IsLightBlocker(x, y))
            light *= _wallLightLoseRate;
 
        _lightMap[x, y] = light;
 
        var building = _worldManager.Buildings[x, y];
        if (building != null)
        {
            Color finalColor = building.SpriteRend.color;
            finalColor.a = 1f;
            building.SpriteRend.color = finalColor;
        }
    }
 
    void ApplyLightingToTexture()
    {
        Color[] colors = new Color[_textureWidth * _textureHeight];
        int i = 0;
        // Iterate over the texture region.
        for (int y = 0; y < _textureHeight; y++)
        {
            for (int x = 0; x < _textureWidth; x++)
            {
                int worldX = _texOriginX + x;
                int worldY = _texOriginY + y;
                float light = 0f;
                Color color = Color.white;
                if (worldX >= 0 && worldY >= 0 && worldX < _worldWidth && worldY < _worldHeight)
                {
                    light = Mathf.Clamp01(_lightMap[worldX, worldY]);
                }
 
                //colors[i++] = new Color(finalColor.r, finalColor.g, finalColor.b, 1 - light);
                colors[i++] = new Color(0,0,0, 1f - light);
            }
        }
        _lightTexture.SetPixels(colors);
        _lightTexture.Apply();
    }
}