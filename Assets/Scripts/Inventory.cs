using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEditor.Progress;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;
    [SerializeField] private GameObject _itemHolder;
    public Item CurrentItem { get; private set; }
    [SerializeField] private List<ItemAmount> _startItems = new();

    [Header("Inventory Settings")]
    [SerializeField] private int slotCount;
    [SerializeField] private int firstRowCount;
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private Transform _slotsParent;
    [SerializeField] private List<InventorySlotUI> _equipmentSlots = new();
    private List<InventorySlotUI> _slotsUI = new();
    private int _currentSlotIndex = 0;

    private GameObject _player;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < slotCount; i++)
        {
            var newSlot = Instantiate(_slotPrefab, _slotsParent.transform);
            InventorySlotUI slotUI = newSlot.GetComponentInChildren<InventorySlotUI>();
            slotUI.SlotIndex = i;
            _slotsUI.Add(slotUI);
        }
    }

    private void Start()
    {
        _player = PlayerMovement.Instance.gameObject;
        InitializeInventory();
    }

    private void InitializeInventory()
    {
        _startItems.RemoveAll(item => item == null || item.Item == null);

        foreach (var startItem in _startItems)
        {
            AddItem(startItem);
        }

        ChangeSlot(_currentSlotIndex);
        ToggleInventory(false);
    }

    private void Update()
    {
        for (int i = 1; i <= firstRowCount; i++)
        {
            if (Input.GetKeyDown(i.ToString()))
            {
                ChangeSlot(i - 1);
            }
        }

        if (Input.GetAxis("Mouse ScrollWheel") > 0f)
        {
            if (_currentSlotIndex + 1 >= firstRowCount) ChangeSlot(0);
            else ChangeSlot(_currentSlotIndex + 1);
        }
        else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
        {
            if (_currentSlotIndex - 1 < 0) ChangeSlot(firstRowCount - 1);
            else ChangeSlot(_currentSlotIndex - 1);
        }

        if (Input.GetKeyDown(KeyCode.Tab)) ToggleInventory(true);

        if(Input.GetKeyUp(KeyCode.Tab)) ToggleInventory(false);

        foreach (var slot in _equipmentSlots)
        {
            if (!slot.ItemAmount.IsEmpty() && slot.ItemAmount.Item is EquipableItem equipable)
            {
                equipable.Tick();
            }
        }

        HandleItemUse();
    }

    private void ToggleInventory(bool value)
    {
        for (int i = firstRowCount; i < _slotsUI.Count; i++)
        {
            _slotsUI[i].ToggleVisibility(value);
        }

        foreach(var slot in _equipmentSlots)
        {
            slot.ToggleVisibility(value);
        }
    }

    public void RemoveOne()
    {
        var currentSlot = _slotsUI[_currentSlotIndex];
        if (!currentSlot.ItemAmount.IsEmpty())
        {
            currentSlot.RemoveAmount(1);
            if (CurrentItem == null) return;
            if (currentSlot.ItemAmount == null || currentSlot.ItemAmount.IsEmpty() || (currentSlot.ItemAmount.IsEmpty() && currentSlot.ItemAmount.Item.EqualsInstance(CurrentItem)))
            {
                CurrentItem = null;
            }
        }
    }

    private void HandleItemUse()
    {
        if (CurrentItem == null) return;

        if (!EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonDown(0))
            {
                CurrentItem?.UseOnce();
            }
            if (Input.GetMouseButton(0))
            {
                CurrentItem?.Holding();
            }
            if (Input.GetMouseButtonUp(0))
            {
                CurrentItem?.EndUse();
            }
        }
    }

    public bool RemoveItems(List<ItemAmount> requirements)
    {
        foreach (var req in requirements)
        {
            int total = 0;
            foreach (var slot in _slotsUI)
            {
                if (!slot.ItemAmount.IsEmpty() && slot.ItemAmount.Item.EqualsType(req.Item))
                {
                    total += slot.ItemAmount.Amount;
                }
            }
            if (total < req.Amount)
            {
                Debug.Log("Not enough " + req.Item.Name);
                return false;
            }
        }

        foreach (var req in requirements)
        {
            int toRemove = req.Amount;
            foreach (var slot in _slotsUI)
            {
                if (!slot.ItemAmount.IsEmpty() && slot.ItemAmount.Item.EqualsType(req.Item))
                {
                    if (slot.ItemAmount.Amount > toRemove)
                    {
                        slot.RemoveAmount(toRemove);
                        break;
                    }
                    else
                    {
                        toRemove -= slot.ItemAmount.Amount;
                        slot.ClearSlot();
                    }
                }
            }
        }

        return true;
    }

    public void MoveItem(InventorySlotUI fromSlot, InventorySlotUI toSlot)
    {
        if (fromSlot.ItemAmount.IsEmpty())
            return;

        if (toSlot.EquipmentSlot && fromSlot.ItemAmount.Item is not EquipableItem) return;

        if (toSlot.ItemAmount.IsEmpty())
        {
            // Move item to empty slot
            var newItem = Instantiate(fromSlot.ItemAmount.Item.gameObject, _player.transform);
            newItem.SetActive(false);
            toSlot.SetItem(new ItemAmount(newItem.GetComponent<Item>(), fromSlot.ItemAmount.Amount));
            fromSlot.ClearSlot();
        }
        else if (toSlot.ItemAmount.Item.EqualsType(fromSlot.ItemAmount.Item))
        {
            // Stack items
            int total = fromSlot.ItemAmount.Amount + toSlot.ItemAmount.Amount;
            if (total <= 100)
            {
                toSlot.AddAmount(fromSlot.ItemAmount.Amount);
                fromSlot.ClearSlot();
            }
            else
            {
                toSlot.ItemAmount.Amount = 100;
                fromSlot.ItemAmount.Amount = total - 100;
                toSlot.UpdateSlotUI();
                fromSlot.UpdateSlotUI();
            }
        }
        else
        {
            // Swap iems
            var tempItemAmount = new ItemAmount(toSlot.ItemAmount.Item, toSlot.ItemAmount.Amount);
            toSlot.SetItem(new ItemAmount(fromSlot.ItemAmount.Item, fromSlot.ItemAmount.Amount));
            fromSlot.SetItem(tempItemAmount);
        }
    }

    public void ChangeSlot(InventorySlotUI slot)
    {
        if (!_slotsUI.Contains(slot)) return;

        ChangeSlot(_slotsUI.IndexOf(slot));
    }

    private void ChangeSlot(int slotIndex)
    {
        _slotsUI[_currentSlotIndex].OnDeselect();
        _currentSlotIndex = slotIndex;
        var slot = _slotsUI[slotIndex];
        slot.OnSelect();

        if (CurrentItem != null)
        {
            CurrentItem.gameObject.transform.SetParent(_player.transform);
            CurrentItem.gameObject.SetActive(false);
        }

        if (!slot.ItemAmount.IsEmpty() && _itemHolder != null)
        {
            CurrentItem = slot.ItemAmount.Item;
            CurrentItem.gameObject.transform.SetParent(_itemHolder.transform);
            CurrentItem.gameObject.SetActive(true);
            CurrentItem.gameObject.transform.localPosition = Vector3.zero;
        }
        else
        {
            CurrentItem = null;
        }
    }

    public bool AddItem(ItemAmount itemAmount)
    {
        if (itemAmount == null || itemAmount.Item == null) return false;

        Item item = itemAmount.Item;
        int remaining = itemAmount.Amount;

        // First try to stack with existing items
        foreach (var slot in _slotsUI)
        {
            if (!slot.ItemAmount.IsEmpty() && slot.ItemAmount.Item.EqualsType(item))
            {
                int canAdd = 100 - slot.ItemAmount.Amount;
                if (canAdd > 0)
                {
                    int add = Mathf.Min(canAdd, remaining);
                    slot.AddAmount(add);
                    remaining -= add;
                    if (remaining <= 0)
                        return true;
                }
            }
        }

        foreach (var slot in _slotsUI)
        {
            if (slot.ItemAmount.IsEmpty())
            {
                int add = Mathf.Min(100, remaining);
                var newItem = Instantiate(item.gameObject, _player.transform);
                newItem.SetActive(false);
                slot.SetItem(new ItemAmount(newItem.GetComponent<Item>(), add));
                remaining -= add;
                if (remaining <= 0)
                    return true;
            }
        }

        return false;
    }
}