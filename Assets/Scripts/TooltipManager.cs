using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("Tooltip Settings")]
    [SerializeField] private GameObject tooltipPrefab;
    [SerializeField] private float fixedTooltipWidth = 300f;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(10f, -10f);
    [SerializeField] private TMP_FontAsset fontAsset;

    private GameObject currentTooltip;
    private RectTransform tooltipRectTransform;
    private RectTransform contentRectTransform;
    private bool isTooltipVisible = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (isTooltipVisible)
        {
            PositionTooltipNearMouse();
        }
    }

    public void ShowTooltip(TooltipData tooltipData)
    {
        // Remove any existing tooltip.
        if (currentTooltip != null)
        {
            Destroy(currentTooltip);
        }

        // Instantiate the tooltip prefab.
        currentTooltip = Instantiate(tooltipPrefab, transform);
        tooltipRectTransform = currentTooltip.GetComponent<RectTransform>();

        // Try to find a "Content" container inside the tooltip prefab.
        Transform contentTransform = currentTooltip.transform.Find("Content");
        if (contentTransform != null)
        {
            contentRectTransform = contentTransform.GetComponent<RectTransform>();
        }
        else
        {
            // Fallback: use the tooltip's RectTransform directly.
            contentRectTransform = tooltipRectTransform;
        }

        // Clear any existing children (in case the prefab has preset objects).
        foreach (Transform child in contentRectTransform)
        {
            Destroy(child.gameObject);
        }

        // Create text elements for each line in the TooltipData.
        foreach (var line in tooltipData.Lines)
        {
            CreateTextElement(line);
        }

        // Force a layout rebuild to ensure the content's preferred height is calculated.
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectTransform);
        float calculatedHeight = LayoutUtility.GetPreferredHeight(contentRectTransform);

        // Set the tooltip's size with a fixed width and the calculated height.
        tooltipRectTransform.sizeDelta = new Vector2(fixedTooltipWidth, calculatedHeight);

        // Position the tooltip near the mouse.
        PositionTooltipNearMouse();
        currentTooltip.SetActive(true);
        isTooltipVisible = true;
    }

    /// <summary>
    /// Creates a text element based on a TooltipLine and adds it to the tooltip.
    /// </summary>
    /// <param name="lineData">The tooltip line data (text, font size, and color).</param>
    private void CreateTextElement(TooltipData.TooltipLine lineData)
    {
        // Create a new GameObject for the text line.
        GameObject textObj = new GameObject("TooltipLine");
        textObj.transform.SetParent(contentRectTransform, false);

        // Add a TextMeshProUGUI component and set its properties.
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = lineData.text;
        textComponent.font = fontAsset;
        textComponent.fontSize = lineData.fontSize;
        textComponent.color = lineData.textColor;
        textComponent.enableWordWrapping = true;

        // Set the fixed width for this text element.
        textComponent.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fixedTooltipWidth);

        // Optional: add a LayoutElement to help the layout group compute sizes.
        LayoutElement layoutElem = textObj.AddComponent<LayoutElement>();
        layoutElem.preferredWidth = fixedTooltipWidth;
    }

    public void HideTooltip()
    {
        if (currentTooltip != null)
        {
            Destroy(currentTooltip);
            isTooltipVisible = false;
        }
    }

    private void PositionTooltipNearMouse()
    {
        if (tooltipRectTransform == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRectTransform = canvas.GetComponent<RectTransform>();
        Vector2 mousePosition = Input.mousePosition;

        // Set the pivot to the top-left so the tooltip expands downward/right.
        tooltipRectTransform.pivot = new Vector2(0, 1);
        Vector2 proposedPosition = mousePosition + tooltipOffset;

        // Adjust if the tooltip goes beyond the right edge.
        if (proposedPosition.x + fixedTooltipWidth > canvasRectTransform.rect.width)
        {
            proposedPosition.x = canvasRectTransform.rect.width - fixedTooltipWidth;
        }

        // Adjust if the tooltip goes below the bottom edge.
        if (proposedPosition.y - tooltipRectTransform.rect.height < 0)
        {
            proposedPosition.y = tooltipRectTransform.rect.height;
        }

        tooltipRectTransform.position = proposedPosition;
    }
}
