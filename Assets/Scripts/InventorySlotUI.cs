using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI & Slot Info")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _quantityText;

    [HideInInspector] public int SlotIndex;
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Vector3 _originalPosition;
    private Transform _originalParent;
    private Inventory _inventory;
    private Canvas _canvas;
    private Vector2 _dragOffset;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
        _originalPosition = _rectTransform.position;
        _canvas = GetComponentInParent<Canvas>();
        _icon.enabled = false;

        _originalParent = transform.parent;
    }

    private void Start()
    {
        _inventory = Inventory.Instance;
    }

    public void UpdateSlotUI()
    {
        ItemAmount slot = _inventory.Items[SlotIndex];
        if (slot.IsEmpty())
        {
            _icon.enabled = false;
            _quantityText.text = "";
        }
        else
        {
            _icon.enabled = true;
            _icon.sprite = slot.Item.SpriteRend.sprite;
            _quantityText.text = slot.Amount > 1 ? slot.Amount.ToString() : "";
        }

        if (transform.parent != _originalParent)
        {
            transform.SetParent(_originalParent, true);
        }
        _rectTransform.anchoredPosition = Vector2.zero;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ItemAmount slot = _inventory.Items[SlotIndex];
        if (slot.IsEmpty())
            return;

        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false;

        // Store original parent
        _originalParent = transform.parent;

        // Calculate offset between mouse position and rect center
        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out mousePos);
        _dragOffset = mousePos;

        // Move to canvas for dragging
        transform.SetParent(_canvas.transform);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_canvas == null) return;

        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out mousePos);

        _rectTransform.anchoredPosition = mousePos - _dragOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        // If not dropped on a valid target, return to original position
        transform.SetParent(_originalParent);
        _rectTransform.anchoredPosition = Vector2.zero;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        InventorySlotUI draggedSlotUI = eventData.pointerDrag.GetComponent<InventorySlotUI>();
        if (draggedSlotUI != null && draggedSlotUI != this)
        {
            _inventory.MoveItem(draggedSlotUI.SlotIndex, this.SlotIndex);
            draggedSlotUI.UpdateSlotUI();
            UpdateSlotUI();
        }
    }
}
