using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    private Dictionary<Vector2Int, float> _persistentLights = new Dictionary<Vector2Int, float>();
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

    private List<(Vector3, bool)> _drawLights = new();

    void UpdateLighting()
    {
        _drawLights = new();
        // Loop through only the tiles within the current texture region.
        for (int y = 0; y < _textureHeight; y++)
        {
            for (int x = 0; x < _textureWidth; x++)
            {
                // Translate texture coordinates back to world coordinates.
                int worldX = _texOriginX + x;
                int worldY = _texOriginY + y;
                // Ensure we’re within the bounds of the world.
                if (worldX >= 0 && worldY >= 0 && worldX < _worldWidth && worldY < _worldHeight)
                {
                    ComputeLightForTile(worldX, worldY);
                }
            }
        }

        ApplyLightingToTexture();
    }

    public void AddLight(int x, int y, float power)
    {
        Vector2Int position = new Vector2Int(x, y);
        if (_persistentLights.ContainsKey(position))
        {
            _persistentLights[position] = Mathf.Max(_persistentLights[position], power);
        }
        else
        {
            _persistentLights.Add(position, power);
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
        if(_persistentLights.ContainsKey(currentTile))
        {
            light = Mathf.Max(light, _persistentLights[currentTile]);
        }
        /*foreach (var lightSource in _persistentLights)
        {
            float dist = Vector2.Distance(currentTile, lightSource.Key);
            if (dist < lightSource.Value * 2) // Light radius is twice the power
            {
                bool visible = HasLineOfSight(lightSource.Key.x, lightSource.Key.y, x, y);
                visible = true;
                float intensityMultiplier = visible ? 1f : _wallLightLoseRate;
                float sourceLight = lightSource.Value * (1f - (dist / (lightSource.Value * 2))) * intensityMultiplier;
                light = Mathf.Max(light, sourceLight);
            }
        }*/

        // Player light (using tile centers)

        /*float distPlayer = Vector2.Distance(new Vector2(tileCenterX, tileCenterY), _player.Pos);
        if (distPlayer < _playerLightRadius)
        {
            bool visible;
            // If this is the player's tile, force visibility.
            if (_player.X == x && _player.Y == y)
            {
                visible = true;
            }
            else
            {
                visible = HasLineOfSight(PlayerMovement.Instance.X + 1, PlayerMovement.Instance.Y + 1, x, y);
            }

           // visible = true;

            _drawLights.Add((new Vector3(tileCenterX, tileCenterY, 0), visible));

            float intensityMultiplier = visible ? 1f : _wallLightLoseRate;
            float playerLight = _playerLightIntensity * (1f - (distPlayer / _playerLightRadius)) * intensityMultiplier;
            light = Mathf.Max(light, playerLight);
        }*/

        // Spread light from nearby tiles.

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < _worldWidth && ny < _worldHeight)
                {
                    if(!_worldManager.IsLightBlocker(nx,ny)) light = Mathf.Max(light, _lightMap[nx, ny] * _lightLoseRate);
                    else light = Mathf.Max(light, _lightMap[nx, ny] * _wallLightLoseRate);
                }
            }
        }

        // Obstruct light if the tile is occupied.
        if (_worldManager.IsLightBlocker(x, y))
            light *= _wallLightLoseRate;

        float clampedLight = light;

        var building = _worldManager.Buildings[x, y];
        if (building != null)
        {
            building.SpriteRend.color = new Color(clampedLight, clampedLight, clampedLight, 1);
        }

        _lightMap[x, y] = clampedLight;
    }

    public void OnDrawGizmos()
    {
        foreach (var item in _drawLights)
        {
            if(item.Item2)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(item.Item1, .5f);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(item.Item1, .5f);
            }
        }
    }

    /*bool HasLineOfSight(int x0, int y0, int x1, int y1)
    {
        int dx = x1 - x0;
        int dy = y1 - y0;

        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

        float xIncrement = dx / (float)steps;
        float yIncrement = dy / (float)steps;

        float x = x0;
        float y = y0;

        // Skip the first iteration
        bool firstIteration = true;
        for (int i = 0; i <= steps; i++)
        {
            int tileX = Mathf.RoundToInt(x);
            int tileY = Mathf.RoundToInt(y);

            if (!firstIteration && _worldManager.IsLightBlocker(tileX, tileY))
            {
                return false;
            }
            firstIteration = false;

            x += xIncrement;
            y += yIncrement;
        }

        return true;
    }*/


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
                if (worldX >= 0 && worldY >= 0 && worldX < _worldWidth && worldY < _worldHeight)
                    light = Mathf.Clamp01(_lightMap[worldX, worldY]);

                // Set the pixel’s alpha based on the light (darker where light is lower).
                colors[i++] = new Color(0, 0, 0, 1 - light);
            }
        }
        _lightTexture.SetPixels(colors);
        _lightTexture.Apply();
    }
}
