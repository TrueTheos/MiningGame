using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{
    [SerializeField] private GameObject tooltipPrefab;
    private GameObject _currentTooltip;
    private RectTransform _tooltipRectTransform;
    private bool isTooltipVisible = false;

    [Header("Tooltip Sizing")]
    [SerializeField] private float horizontalPadding = 20f;
    [SerializeField] private float verticalPadding = 10f;
    [SerializeField] private float minWidth = 100f;
    [SerializeField] private float maxWidth = 300f;
    [SerializeField] private TMP_FontAsset font;

    public static TooltipManager Instance { get; private set; }

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
        if (isTooltipVisible) PositionTooltipNearMouse();
    }

    public void ShowTooltip(Vector2 position, TooltipData tooltipData)
    {
        if (_currentTooltip != null)
        {
            Destroy(_currentTooltip);
        }

        _currentTooltip = Instantiate(tooltipPrefab, transform);
        _tooltipRectTransform = _currentTooltip.GetComponent<RectTransform>();

        foreach (var line in tooltipData.Lines)
        {
            CreateTextElement(line);
        }

        StartCoroutine(AdjustTooltipSize());
    }

    private void PositionTooltipNearMouse()
    {
        if (_tooltipRectTransform == null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRectTransform = canvas.GetComponent<RectTransform>();

        Vector2 mousePosition = Input.mousePosition;
        Vector2 tooltipSize = _tooltipRectTransform.rect.size;

        Vector2 offset = new Vector2(tooltipSize.x / 2, tooltipSize.y / 2);

        Vector2 proposedPosition = mousePosition + offset;

        if (proposedPosition.x + tooltipSize.x > canvasRectTransform.rect.width)
        {
            proposedPosition.x = mousePosition.x - tooltipSize.x - 20;
        }

        if (proposedPosition.y - tooltipSize.y < 0)
        {
            proposedPosition.y = mousePosition.y + tooltipSize.y + 20;
        }

        _tooltipRectTransform.position = proposedPosition;
    }

    private void CreateTextElement(TooltipData.TooltipLine lineData)
    {
        GameObject textObject = new GameObject("TooltipLine");
        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();

        RectTransform rectTransform = textComponent.rectTransform;
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(1, 0);
        rectTransform.sizeDelta = new Vector2(0, lineData.fontSize * 1.5f);

        textComponent.text = lineData.text;
        textComponent.fontSize = lineData.fontSize;
        textComponent.color = lineData.textColor;
        textComponent.raycastTarget = false;
        textComponent.font = font;

        textComponent.transform.SetParent(_currentTooltip.transform, false);
    }

    private IEnumerator AdjustTooltipSize()
    {
        yield return new WaitForEndOfFrame();

        float preferredHeight = LayoutUtility.GetPreferredHeight(_currentTooltip.GetComponent<RectTransform>());

        float preferredWidth = Mathf.Clamp(
            LayoutUtility.GetPreferredWidth(_currentTooltip.GetComponent<RectTransform>()) + horizontalPadding * 2,
            minWidth,
            maxWidth
        );

        _currentTooltip.GetComponent<RectTransform>().sizeDelta = new Vector2(
            preferredWidth,
            preferredHeight + verticalPadding * 2
        );

        PositionTooltipNearMouse();

        isTooltipVisible = true;
    }

    public void HideTooltip()
    {
        if (_currentTooltip != null)
        {
            Destroy(_currentTooltip);
        }
    }
}
