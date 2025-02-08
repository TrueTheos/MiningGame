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
    [SerializeField] private bool _equipmentSlot;
    public bool EquipmentSlot => _equipmentSlot;

    public ItemAmount ItemAmount { get; private set; }

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
        ItemAmount = new ItemAmount(null, 0);
    }

    private void Start()
    {
        _inventory = Inventory.Instance;
    }

    public void SetItem(ItemAmount itemAmount)
    {
        if (ItemAmount?.Item != null && ItemAmount.Item is EquipableItem previousEquipable) previousEquipable.Unequip();
        ItemAmount = itemAmount;
        if (ItemAmount?.Item != null && ItemAmount.Item is EquipableItem newEquipable) newEquipable.Equip();
        UpdateSlotUI();
    }

    public void ClearSlot()
    {
        if(ItemAmount.Item != null) Destroy(ItemAmount.Item.gameObject);
        ItemAmount = new ItemAmount(null, 0);
        UpdateSlotUI();
    }

    public void AddAmount(int amount)
    {
        ItemAmount.Amount += amount;
        UpdateSlotUI();
    }

    public void RemoveAmount(int amount)
    {
        ItemAmount.Amount -= amount;
        if(ItemAmount.Amount <= 0)
        {
            ClearSlot();
        }
        else
        {
            UpdateSlotUI();
        }
    }

    public void UpdateSlotUI()
    {
        if (ItemAmount.IsEmpty())
        {
            _icon.enabled = false;
            _quantityText.text = "";
        }
        else
        {
            _icon.enabled = true;
            _icon.sprite = ItemAmount.Item.SpriteRend.sprite;
            _quantityText.text = ItemAmount.Amount > 1 ? ItemAmount.Amount.ToString() : "";
        }

        if (transform.parent != _originalParent)
        {
            transform.SetParent(_originalParent, true);
        }
        _rectTransform.anchoredPosition = Vector2.zero;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right && !ItemAmount.IsEmpty())
        {
            if (ItemAmount.Item is EquipableItem equipableItem)
            {
                equipableItem.ToggleEquip();
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if(ItemAmount.IsEmpty()) return;

        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false;
        _originalParent = transform.parent;

        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out mousePos);
        _dragOffset = mousePos;

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
        transform.SetParent(_originalParent);
        _rectTransform.anchoredPosition = Vector2.zero;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        InventorySlotUI draggedSlotUI = eventData.pointerDrag.GetComponent<InventorySlotUI>();
        if (draggedSlotUI != null && draggedSlotUI != this)
        {
            _inventory.MoveItem(draggedSlotUI, this);
        }
    }
}
