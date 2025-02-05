using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;
    [SerializeField] private GameObject _hand;
    public Item CurrentItem { get; private set; }
    public List<ItemAmount> Items = new();
    private int _currentIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        InitializeInventory();
    }

    private void InitializeInventory()
    {
        // Clean up any null items in the inventory at start
        Items.RemoveAll(item => item == null || item.Item == null);

        if (Items.Count > 0)
        {
            _currentIndex = 0;
            ChangeItem(Items[0].Item);
        }
        else
        {
            _currentIndex = -1;
            ChangeItem(null);
        }
    }

    private void Update()
    {
        HandleItemUse();
        HandleItemScrolling();
    }

    private void HandleItemUse()
    {
        if (CurrentItem == null || Items.Count == 0) return;

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

    private void HandleItemScrolling()
    {
        if (Items.Count == 0) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            NextItem();
        }
        else if (scroll < 0f)
        {
            PreviousItem();
        }
    }

    public void RemoveOne()
    {
        if (_currentIndex < 0 || _currentIndex >= Items.Count) return;

        Items[_currentIndex].Amount--;
        if (Items[_currentIndex].Amount <= 0)
        {
            Items.RemoveAt(_currentIndex);
            if (Items.Count == 0)
            {
                _currentIndex = -1;
                ChangeItem(null);
                foreach (Transform child in _hand.transform)
                {
                    Destroy(child.gameObject);
                }
                return;
            }
        }
    }

    private void NextItem()
    {
        if (Items.Count == 0)
        {
            _currentIndex = -1;
            ChangeItem(null);
            return;
        }

        _currentIndex = (_currentIndex + 1) % Items.Count;
        ChangeItem(Items[_currentIndex].Item);
    }

    private void PreviousItem()
    {
        if (Items.Count == 0)
        {
            _currentIndex = -1;
            ChangeItem(null);
            return;
        }

        _currentIndex = (_currentIndex - 1 + Items.Count) % Items.Count;
        ChangeItem(Items[_currentIndex].Item);
    }

    private void ChangeItem(Item item)
    {
        if (_hand != null)
        {
            foreach (Transform child in _hand.transform)
            {
                Destroy(child.gameObject);
            }
        }

        if (item != null && item.gameObject != null && _hand != null)
        {
            var newItem = Instantiate(item.gameObject, _hand.transform);
            CurrentItem = newItem.GetComponent<Item>();
            newItem.transform.localPosition = Vector3.zero;
        }
    }

    public void AddItem(ItemAmount itemAmount)
    {
        if (itemAmount == null || itemAmount.Item == null) return;

        var existingIndex = Items.FindIndex(x => x.Item == itemAmount.Item);
        if (existingIndex != -1)
        {
            Items[existingIndex].Amount += itemAmount.Amount;
        }
        else
        {
            Items.Add(itemAmount);
            if (Items.Count == 1)
            {
                _currentIndex = 0;
                ChangeItem(itemAmount.Item);
            }
        }
    }
}
