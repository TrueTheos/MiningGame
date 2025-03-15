using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class SpriteGridAnalyzerEditor : EditorWindow
{
    private Texture2D sourceTexture;
    private Sprite sourceSprite;
    private Vector2Int gridSize = new Vector2Int(5, 7);
    private Vector2Int originPosition = new Vector2Int(2, 0);
    private float coverageThreshold = 0.3f;
    private List<Vector2Int> coveredCells = new List<Vector2Int>();
    private bool showGridOverlay = true;
    private bool analyzeCompleted = false;
    private Vector2 scrollPosition;
    private Vector2 resultScrollPosition;

    [MenuItem("Tools/Sprite Grid Analyzer")]
    public static void ShowWindow()
    {
        GetWindow<SpriteGridAnalyzerEditor>("Sprite Grid Analyzer");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Sprite Grid Analyzer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Source Image", EditorStyles.boldLabel);
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Texture", sourceTexture, typeof(Texture2D), false);
        sourceSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", sourceSprite, typeof(Sprite), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        gridSize = EditorGUILayout.Vector2IntField("Grid Size (W, H)", gridSize);
        originPosition = EditorGUILayout.Vector2IntField("Origin Position (X, Y)", originPosition);
        coverageThreshold = EditorGUILayout.Slider("Coverage Threshold", coverageThreshold, 0.01f, 1f);
        showGridOverlay = EditorGUILayout.Toggle("Show Grid Overlay", showGridOverlay);
        
        gridSize.x = Mathf.Max(1, gridSize.x);
        gridSize.y = Mathf.Max(1, gridSize.y);
        originPosition.x = Mathf.Clamp(originPosition.x, 0, gridSize.x - 1);
        originPosition.y = Mathf.Clamp(originPosition.y, 0, gridSize.y - 1);

        EditorGUILayout.Space();
        if (GUILayout.Button("Analyze Image"))
        {
            AnalyzeImage();
        }

        if (analyzeCompleted)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

            // Display image with grid overlay
            if ((sourceTexture != null || sourceSprite != null) && showGridOverlay)
            {
                Texture2D texture = sourceSprite != null ? sourceSprite.texture : sourceTexture;

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
                    float cellWidth = rect.width / gridSize.x;
                    float cellHeight = rect.height / gridSize.y;

                    int gridX = (int)((e.mousePosition.x - rect.x) / cellWidth);
                    int gridY = (int)((rect.y + rect.height - e.mousePosition.y) / cellHeight);

                    Vector2Int relativePos = new Vector2Int(gridX - originPosition.x, gridY - originPosition.y);
                
                    if(coveredCells.Contains(relativePos))
                    {
                        coveredCells.Remove(relativePos);
                    }
                    else
                    {
                        coveredCells.Add(relativePos);
                    }

                    Repaint();
                    e.Use();
                }

                DrawGridOverlay(rect);
            }

            // Display covered cells
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Found {coveredCells.Count} cells with >={coverageThreshold * 100}% coverage");

            if (coveredCells.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("// List of covered cell positions relative to origin");
                sb.AppendLine("List<Vector2Int> coveredPositions = new List<Vector2Int>");
                sb.AppendLine("{");

                foreach (Vector2Int pos in coveredCells)
                {
                    sb.AppendLine($"    new Vector2Int({pos.x}, {pos.y}),");
                }

                // Remove trailing comma
                sb.Length -= 2;
                sb.AppendLine();
                sb.AppendLine("};");

                resultScrollPosition = EditorGUILayout.BeginScrollView(resultScrollPosition, GUILayout.Height(200));
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
        if (coveredCells.Count == 0)
            return "";

        StringBuilder sb = new StringBuilder();

        foreach (Vector2Int pos in coveredCells)
        {
            sb.AppendLine($"{pos.x}, {pos.y}");
        }

        return sb.ToString().TrimEnd();
    }

    private void AnalyzeImage()
    {
        if (sourceTexture == null && sourceSprite == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a texture or sprite first.", "OK");
            return;
        }

        analyzeCompleted = false;
        coveredCells.Clear();

        Texture2D pixelTexture;
        if (sourceSprite != null)
        {
            pixelTexture = sourceSprite.texture;
        }
        else
        {
            pixelTexture = sourceTexture;
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

        int cellWidth = texWidth / gridSize.x;
        int cellHeight = texHeight / gridSize.y;

        // Analyze each cell
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
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
                if (coverage >= coverageThreshold)
                {
                    Vector2Int relativePos = new Vector2Int(x - originPosition.x, y - originPosition.y);
                    coveredCells.Add(relativePos);
                }
            }
        }

        analyzeCompleted = true;
    }

    private void DrawGridOverlay(Rect rect)
    {
        if (sourceTexture == null && sourceSprite == null)
            return;

        float cellWidth = rect.width / gridSize.x;
        float cellHeight = rect.height / gridSize.y;

        Handles.color = new Color(0.5f, 0.5f, 1f, 0.5f);

        // Draw vertical lines
        for (int x = 0; x <= gridSize.x; x++)
        {
            float xPos = rect.x + x * cellWidth;
            Handles.DrawLine(new Vector3(xPos, rect.y), new Vector3(xPos, rect.y + rect.height));
        }

        // Draw horizontal lines
        for (int y = 0; y <= gridSize.y; y++)
        {
            float yPos = rect.y + y * cellHeight;
            Handles.DrawLine(new Vector3(rect.x, yPos), new Vector3(rect.x + rect.width, yPos));
        }

        // Draw origin marker
        Handles.color = Color.yellow;
        float originX = rect.x + originPosition.x * cellWidth + cellWidth / 2;
        float originY = rect.y + rect.height - originPosition.y * cellHeight - cellHeight / 2;
        Handles.DrawWireDisc(new Vector3(originX, originY), Vector3.forward, 5f);

        // Highlight covered cells
        if (analyzeCompleted)
        {
            foreach (Vector2Int pos in coveredCells)
            {
                float x = rect.x + (pos.x + originPosition.x) * cellWidth;
                float y = rect.y + rect.height - (pos.y + originPosition.y + 1) * cellHeight;

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
