using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEditor.Progress;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;
    [SerializeField] private GameObject _hand;
    public Item CurrentItem { get; private set; }
    [SerializeField] private List<ItemAmount> _startItems = new();

    [Header("Inventory Settings")]
    [SerializeField] private int slotCount;
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private Transform _slotsParent;
    private List<InventorySlotUI> _slotsUI = new();
    public ItemAmount[] Items;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Items = new ItemAmount[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            Items[i] = new(null, 0);
            var newSlot = Instantiate(_slotPrefab, _slotsParent.transform);
            InventorySlotUI slotUI = newSlot.GetComponentInChildren<InventorySlotUI>();
            slotUI.SlotIndex = i;
            _slotsUI.Add(slotUI);
        }
    }

    private void Start()
    {
        InitializeInventory();
    }

    private void InitializeInventory()
    {
        _startItems.RemoveAll(item => item == null || item.Item == null);

        foreach (var startItem in _startItems)
        {
            AddItem(startItem);
        }
    }

    private void Update()
    {
        HandleItemUse();
    }

    public void RemoveOne() { }
    private void HandleItemUse()
    {
        if (CurrentItem == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            CurrentItem.UseOnce();
        }
        if (Input.GetMouseButton(0))
        {
            CurrentItem.Holding();
        }
        if (Input.GetMouseButtonUp(0))
        {
            CurrentItem.EndUse();
        }
    }

    public bool RemoveItems(List<ItemAmount> requirements)
    {
        foreach (var req in requirements)
        {
            int total = 0;
            for (int i = 0; i < Items.Length; i++)
            {
                if (!Items[i].IsEmpty() && Items[i].Item == req.Item)
                    total += Items[i].Amount;
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
            for (int i = 0; i < Items.Length; i++)
            {
                if (!Items[i].IsEmpty() && Items[i].Item == req.Item)
                {
                    if (Items[i].Amount > toRemove)
                    {
                        Items[i].Amount -= toRemove;
                        toRemove = 0;
                        break;
                    }
                    else
                    {
                        toRemove -= Items[i].Amount;
                        Destroy(Items[i].Item.gameObject);
                        Items[i].Item = null;
                        Items[i].Amount = 0;
                    }
                }
            }
        }

        RefreshAllSlotUIs();

        return true;
    }

    public void RefreshAllSlotUIs()
    {
        foreach (InventorySlotUI slotUI in _slotsUI)
        {
            slotUI.UpdateSlotUI();
        }
    }

    public void SwapSlots(int indexA, int indexB)
    {
        ItemAmount temp = Items[indexA];
        Items[indexA] = Items[indexB];
        Items[indexB] = temp;

        RefreshAllSlotUIs();
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        ItemAmount fromSlot = Items[fromIndex];
        ItemAmount toSlot = Items[toIndex];

        if (fromSlot.IsEmpty())
            return;

        if (toSlot.IsEmpty())
        {
            // Move item to the empty slot.
            Items[toIndex] = fromSlot;
            Items[fromIndex] = new ItemAmount(null, 0);
        }
        else if (toSlot.Item == fromSlot.Item)
        {
            // Same item type: stack them.
            int total = fromSlot.Amount + toSlot.Amount;
            if (total <= 100)
            {
                toSlot.Amount = total;
                Items[fromIndex] = new ItemAmount(null, 0);
            }
            else
            {
                toSlot.Amount = 100;
                fromSlot.Amount = total - 100;
            }
        }
        else
        {
            SwapSlots(fromIndex, toIndex);
        }

        RefreshAllSlotUIs();
    }

    private void ChangeItem(Item item)
    {
        if (CurrentItem != null)
        {
            CurrentItem.gameObject.SetActive(false);
        }

        if (item != null && _hand != null)
        {
            // Create a new instance of the item instead of reusing the existing one
            CurrentItem = item;
            CurrentItem.gameObject.SetActive(true);
            CurrentItem.gameObject.transform.localPosition = Vector3.zero;
        }
        else
        {
            Destroy(CurrentItem.gameObject);
            CurrentItem = null;
        }
    }

    public bool AddItem(ItemAmount itemAmount)
    {
        if (itemAmount == null || itemAmount.Item == null) return false;

        Item item = itemAmount.Item;
        int remaining = itemAmount.Amount;

        for (int i = 0; i < Items.Length; i++)
        {
            if (!Items[i].IsEmpty() && Items[i].Item == item)
            {
                int canAdd = 100 - Items[i].Amount;
                if (canAdd > 0)
                {
                    int add = Mathf.Min(canAdd, remaining);
                    Items[i].Amount += add;
                    remaining -= add;
                    RefreshAllSlotUIs();
                    if (remaining <= 0)
                        return true;
                }
            }
        }

        for (int i = 0; i < Items.Length; i++)
        {
            if (Items[i].IsEmpty())
            {
                int add = Mathf.Min(100, remaining);

                var newItem = Instantiate(item.gameObject, _hand.transform);
                newItem.gameObject.SetActive(false);
                Items[i].Item = newItem.GetComponent<Item>();
                Items[i].Amount = add;
                remaining -= add;
                RefreshAllSlotUIs();
                if (remaining <= 0)
                    return true;
            }
        }

        return false;
    }
}