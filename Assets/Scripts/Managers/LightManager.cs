using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightManager : MonoBehaviour
{
    private WorldManager _worldManager;

    [SerializeField] private int _tileSize = 8;
    [SerializeField] private float _playerLightRadius = 5f;
    [SerializeField] private float _playerLightIntensity = 1.0f;
    [SerializeField] private float _lightLoseRate = .5f;
    [SerializeField] private float _wallLightLoseRate = .2f;
    [SerializeField] private GameObject _player;

    private float[,] _lightMap;
    private Texture2D _lightTexture;
    private SpriteRenderer _lightRenderer;
    private Camera _mainCamera;

    private int _worldWidth => _worldManager.WorldWidth;
    private int _worldHeight => _worldManager.WorldHeight;
    private int _textureWidth, _textureHeight;

    private void Start()
    {
        if (_player == null) Debug.LogError("ASSIGN PLAYER");

        _worldManager = WorldManager.Instance;

        _mainCamera = Camera.main;

        _lightMap = new float[_worldWidth, _worldHeight];

        _textureWidth = _worldWidth;
        _textureHeight = _worldHeight;
        _lightTexture = new Texture2D(_textureWidth, _textureHeight);
        _lightTexture.filterMode = FilterMode.Point;

        GameObject lightObject = new GameObject("LightingOverlay");
        _lightRenderer = lightObject.AddComponent<SpriteRenderer>();
        _lightRenderer.sprite = Sprite.Create(_lightTexture, new Rect(0, 0, _textureWidth, _textureHeight), new Vector2(0.5f, 0.5f));
        _lightRenderer.transform.position = new Vector2(_worldWidth / 2, _worldHeight / 2);
        _lightRenderer.transform.localScale *= 100;
        _lightRenderer.color = new Color(1, 1, 1, 1);
        _lightRenderer.sortingOrder = 10;
    }

    void Update()
    {
        if (_worldManager == null || !_worldManager.Ready) return;
        UpdateLighting();
    }

    void UpdateLighting()
    {
        Vector3 camPos = _mainCamera.transform.position;
        float camHalfWidth = _mainCamera.orthographicSize * _mainCamera.aspect;
        float camHalfHeight = _mainCamera.orthographicSize;

        // Get the visible tile range from camera
        int minX = Mathf.Max(0, Mathf.FloorToInt(camPos.x - camHalfWidth));
        int maxX = Mathf.Min(_worldWidth, Mathf.CeilToInt(camPos.x + camHalfWidth));
        int minY = Mathf.Max(0, Mathf.FloorToInt(camPos.y - camHalfHeight));
        int maxY = Mathf.Min(_worldHeight, Mathf.CeilToInt(camPos.y + camHalfHeight));

        Vector2 playerPos = Vector2.zero;
        if (_player != null)
        {
            playerPos = new Vector2(_player.transform.position.x, _player.transform.position.y);
        }

        // Loop through only visible tiles
        for (int y = minY; y < maxY; y++)  // bottom-to-top
        {
            for (int x = minX; x < maxX; x++)  // left-to-right
            {
                ComputeLightForTile(x, y, playerPos);
            }
        }

        ApplyLightingToTexture();
    }

    void ComputeLightForTile(int x, int y, Vector2 playerPos)
    {
        float light = 0f;

        // Player light (compute center-based distance)
        if (_player != null)
        {
            int tileX = x;
            int tileY = y;
            float dist = Vector2.Distance(new Vector2(tileX + 0.5f, tileY + 0.5f), new Vector2(_player.transform.position.x, _player.transform.position.y));
            if (dist < _playerLightRadius)
            {
                // Check if the tile is directly visible from the player
                int playerTileX = Mathf.FloorToInt(_player.transform.position.x);
                int playerTileY = Mathf.FloorToInt(_player.transform.position.y);
                bool visible = HasLineOfSight(playerTileX, playerTileY, tileX, tileY);

                // If visible, full light; if not, reduce the light intensity
                float intensityMultiplier = visible ? 1f : _wallLightLoseRate;
                float playerLight = _playerLightIntensity * (1f - (dist / _playerLightRadius)) * intensityMultiplier;
                light = Mathf.Max(light, playerLight);
            }
        }

        // Spread light from nearby tiles (using cached values)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < _worldWidth && ny < _worldHeight)
                {
                    light = Mathf.Max(light, _lightMap[nx, ny] * 0.85f);
                }
            }
        }

        // Obstruct light if the tile is occupied (or solid)
        if (_worldManager.WorldData[x, y] != null)
            light *= _lightLoseRate;

        _lightMap[x, y] = Mathf.Clamp01(light);
    }

    bool HasLineOfSight(int x0, int y0, int x1, int y1)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = (x0 < x1) ? 1 : -1;
        int sy = (y0 < y1) ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            // If this tile is an obstacle, block light (you may decide whether to allow partial light)
            if (_worldManager.WorldData[x0, y0] != null)
            {
                // Optionally, you can allow a small amount of light to pass if you want partial occlusion.
                return false;
            }

            // Reached the target tile?
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        return true;
    }

    void ApplyLightingToTexture()
    {
        Color[] colors = new Color[_textureWidth * _textureHeight];
        int i = 0;
        for (int y = 0; y < _textureHeight; y++)
        {
            for (int x = 0; x < _textureWidth; x++)
            {
                float light = _lightMap[x, y];
                // The alpha is 1 - light, so dark areas have high alpha
                colors[i++] = new Color(0, 0, 0, 1 - light);
            }
        }
        _lightTexture.SetPixels(colors);
        _lightTexture.Apply();
    }
}
