using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class SpriteGridAnalyzerEditor : EditorWindow
{
    private Texture2D _sourceTexture;
    private Sprite _sourceSprite;
    private Vector2Int _gridSize = new();
    private Vector2Int _originPosition = new();
    private float _coverageThreshold = 0.3f;
    private List<Vector2Int> _coveredCells = new List<Vector2Int>();
    private bool _showGridOverlay = true;
    private bool _analyzeCompleted = false;
    private Vector2 _scrollPosition;
    private Vector2 _resultScrollPosition;

    [MenuItem("Tools/Sprite Grid Analyzer")]
    public static void ShowWindow()
    {
        GetWindow<SpriteGridAnalyzerEditor>("Sprite Grid Analyzer");
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        EditorGUILayout.LabelField("Sprite Grid Analyzer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Source Image", EditorStyles.boldLabel);
        _sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Texture", _sourceTexture, typeof(Texture2D), false);
        _sourceSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", _sourceSprite, typeof(Sprite), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _gridSize = EditorGUILayout.Vector2IntField("Grid Size (W, H)", _gridSize);
        _originPosition = EditorGUILayout.Vector2IntField("Origin Position (X, Y)", _originPosition);
        _coverageThreshold = EditorGUILayout.Slider("Coverage Threshold", _coverageThreshold, 0.01f, 1f);
        _showGridOverlay = EditorGUILayout.Toggle("Show Grid Overlay", _showGridOverlay);
        
        _gridSize.x = Mathf.Max(1, _gridSize.x);
        _gridSize.y = Mathf.Max(1, _gridSize.y);
        _originPosition.x = Mathf.Clamp(_originPosition.x, 0, _gridSize.x - 1);
        _originPosition.y = Mathf.Clamp(_originPosition.y, 0, _gridSize.y - 1);

        if (EditorGUI.EndChangeCheck())
        {
            AnalyzeImage();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Analyze Image"))
        {
            AnalyzeImage();
        }

        if (_analyzeCompleted)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

            // Display image with grid overlay
            if ((_sourceTexture != null || _sourceSprite != null) && _showGridOverlay)
            {
                Texture2D texture = _sourceSprite != null ? _sourceSprite.texture : _sourceTexture;

                Rect availableRect = GUILayoutUtility.GetRect(0, 10000, 0, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                float aspectRatio = (float)texture.width / texture.height;
                float availableWidth = availableRect.width;
                float availableHeight = availableRect.height;

                float previewWidth = availableWidth;
                float previewHeight = availableHeight / aspectRatio;

                if (previewHeight > availableHeight)
                {
                    previewHeight = availableHeight;
                    previewWidth = availableHeight * aspectRatio;
                }

                Rect rect = new Rect(
                    availableRect.x + (availableWidth - previewWidth) * 0.5f,
                    availableRect.y + (availableHeight - previewHeight) * 0.5f,
                    previewWidth,
                    previewHeight
                );

                EditorGUI.DrawTextureTransparent(rect, texture);

                Event e = Event.current;
                if(e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    float cellWidth = rect.width / _gridSize.x;
                    float cellHeight = rect.height / _gridSize.y;

                    int gridX = (int)((e.mousePosition.x - rect.x) / cellWidth);
                    int gridY = (int)((rect.y + rect.height - e.mousePosition.y) / cellHeight);

                    Vector2Int relativePos = new Vector2Int(gridX - _originPosition.x, gridY - _originPosition.y);
                
                    if(_coveredCells.Contains(relativePos))
                    {
                        _coveredCells.Remove(relativePos);
                    }
                    else
                    {
                        _coveredCells.Add(relativePos);
                    }

                    Repaint();
                    e.Use();
                }

                DrawGridOverlay(rect);
            }

            // Display covered cells
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Found {_coveredCells.Count} cells with >={_coverageThreshold * 100}% coverage");

            if (_coveredCells.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("// List of covered cell positions relative to origin");
                sb.AppendLine("List<Vector2Int> coveredPositions = new List<Vector2Int>");
                sb.AppendLine("{");

                foreach (Vector2Int pos in _coveredCells)
                {
                    sb.AppendLine($"    new Vector2Int({pos.x}, {pos.y}),");
                }

                // Remove trailing comma
                sb.Length -= 2;
                sb.AppendLine();
                sb.AppendLine("};");

                _resultScrollPosition = EditorGUILayout.BeginScrollView(_resultScrollPosition, GUILayout.Height(200));
                EditorGUILayout.TextArea(sb.ToString(), GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Copy Code"))
                {
                    EditorGUIUtility.systemCopyBuffer = sb.ToString();
                }

                if (GUILayout.Button("Copy Pastable Format"))
                {
                    EditorGUIUtility.systemCopyBuffer = GeneratePastableList();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private string GeneratePastableList()
    {
        if (_coveredCells.Count == 0)
            return "";

        StringBuilder sb = new StringBuilder();

        foreach (Vector2Int pos in _coveredCells)
        {
            sb.AppendLine($"{pos.x}, {pos.y}");
        }

        return sb.ToString().TrimEnd();
    }

    private void AnalyzeImage()
    {
        if (_sourceTexture == null && _sourceSprite == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a texture or sprite first.", "OK");
            return;
        }

        _analyzeCompleted = false;
        _coveredCells.Clear();

        Texture2D pixelTexture;
        if (_sourceSprite != null)
        {
            pixelTexture = _sourceSprite.texture;
        }
        else
        {
            pixelTexture = _sourceTexture;
        }

        if (!pixelTexture.isReadable)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Texture Not Readable",
                "The texture is not marked as readable. Would you like to create a readable copy?\n\n" +
                "(This will not modify your original texture, but will create a temporary copy for analysis)",
                "Create Copy", "Cancel");

            if (proceed)
            {
                pixelTexture = CreateReadableCopy(pixelTexture);
            }
            else
            {
                return;
            }
        }

        Color[] pixels = pixelTexture.GetPixels();

        int texWidth = pixelTexture.width;
        int texHeight = pixelTexture.height;

        int cellWidth = texWidth / _gridSize.x;
        int cellHeight = texHeight / _gridSize.y;

        // Analyze each cell
        for (int y = 0; y < _gridSize.y; y++)
        {
            for (int x = 0; x < _gridSize.x; x++)
            {
                int pixelCount = 0;
                int nonTransparentCount = 0;

                // Check each pixel in the cell
                for (int cy = 0; cy < cellHeight; cy++)
                {
                    for (int cx = 0; cx < cellWidth; cx++)
                    {
                        int pixelX = x * cellWidth + cx;
                        int pixelY = y * cellHeight + cy;

                        // Skip if outside texture bounds
                        if (pixelX >= texWidth || pixelY >= texHeight)
                            continue;

                        pixelCount++;

                        Color pixel = pixels[pixelY * texWidth + pixelX];

                        // Check if pixel is not transparent
                        if (pixel.a > 0.1f)
                        {
                            nonTransparentCount++;
                        }
                    }
                }

                float coverage = pixelCount > 0 ? (float)nonTransparentCount / pixelCount : 0f;

                // If coverage is above threshold, add to list
                if (coverage >= _coverageThreshold)
                {
                    Vector2Int relativePos = new Vector2Int(x - _originPosition.x, y - _originPosition.y);
                    _coveredCells.Add(relativePos);
                }
            }
        }

        _analyzeCompleted = true;
    }

    private void DrawGridOverlay(Rect rect)
    {
        if (_sourceTexture == null && _sourceSprite == null)
            return;

        float cellWidth = rect.width / _gridSize.x;
        float cellHeight = rect.height / _gridSize.y;

        Handles.color = new Color(0.5f, 0.5f, 1f, 0.5f);

        // Draw vertical lines
        for (int x = 0; x <= _gridSize.x; x++)
        {
            float xPos = rect.x + x * cellWidth;
            Handles.DrawLine(new Vector3(xPos, rect.y), new Vector3(xPos, rect.y + rect.height));
        }

        // Draw horizontal lines
        for (int y = 0; y <= _gridSize.y; y++)
        {
            float yPos = rect.y + y * cellHeight;
            Handles.DrawLine(new Vector3(rect.x, yPos), new Vector3(rect.x + rect.width, yPos));
        }

        // Draw origin marker
        Handles.color = Color.yellow;
        float originX = rect.x + _originPosition.x * cellWidth + cellWidth / 2;
        float originY = rect.y + rect.height - _originPosition.y * cellHeight - cellHeight / 2;
        Handles.DrawWireDisc(new Vector3(originX, originY), Vector3.forward, 5f);

        // Highlight covered cells
        if (_analyzeCompleted)
        {
            foreach (Vector2Int pos in _coveredCells)
            {
                float x = rect.x + (pos.x + _originPosition.x) * cellWidth;
                float y = rect.y + rect.height - (pos.y + _originPosition.y + 1) * cellHeight;

                Handles.color = new Color(0f, 1f, 0f, 0.3f);
                Handles.DrawSolidRectangleWithOutline(
                    new Rect(x, y, cellWidth, cellHeight),
                    new Color(0f, 1f, 0f, 0.3f),
                    new Color(0f, 1f, 0f, 0.5f)
                );
            }
        }
    }

    private Texture2D CreateReadableCopy(Texture2D source)
    {
        RenderTexture renderTex = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32
        );

        // Copy the texture to the render texture
        Graphics.Blit(source, renderTex);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;

        Texture2D readableTexture = new Texture2D(source.width, source.height);
        readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableTexture.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);

        return readableTexture;
    }
}
