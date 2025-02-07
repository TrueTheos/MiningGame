using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;
    [SerializeField] private GameObject _hand;
    public Item CurrentItem { get; private set; }
    [SerializeField] private List<ItemAmount> _startItems = new();
    private List<ItemAmount> _items = new();
    private int _currentIndex = -1;

    [Header("Inventory Settings")]
    [SerializeField] private int slotCount = 10;
    private ItemAmount[] slots;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        slots = new ItemAmount[slotCount];
    }

    private void Start()
    {
        InitializeInventory();
    }

    private void InitializeInventory()
    {
        // Clean up any null items in the inventory at start
        _startItems.RemoveAll(item => item == null || item.Item == null);

        foreach (var startItem in _startItems)
        {
            var item = Instantiate(startItem.Item.gameObject, _hand.transform).GetComponent<Item>();
            item.gameObject.SetActive(false);
            _items.Add(new ItemAmount(item, startItem.Amount));
        }

        if (_items.Count > 0)
        {
            _currentIndex = 0;
            ChangeItem(_items[0].Item);
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
        if (CurrentItem == null || _items.Count == 0) return;

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
        if (_items.Count == 0) return;

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
        if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

        _items[_currentIndex].Amount--;
        if (_items[_currentIndex].Amount <= 0)
        {
            _items.RemoveAt(_currentIndex);

            if (_items.Count == 0)
            {
                _currentIndex = -1;
                ChangeItem(null);
                return;
            }
            else
            {
                // Ensure the next valid item is selected
                Destroy(CurrentItem.gameObject);
                _currentIndex = Mathf.Clamp(_currentIndex, 0, _items.Count - 1);
                ChangeItem(_items[_currentIndex].Item);
            }
        }
    }

    private void NextItem()
    {
        if (_items.Count == 0)
        {
            _currentIndex = -1;
            ChangeItem(null);
            return;
        }

        _currentIndex = (_currentIndex + 1) % _items.Count;
        ChangeItem(_items[_currentIndex].Item);
    }

    private void PreviousItem()
    {
        if (_items.Count == 0)
        {
            _currentIndex = -1;
            ChangeItem(null);
            return;
        }

        _currentIndex = (_currentIndex - 1 + _items.Count) % _items.Count;
        ChangeItem(_items[_currentIndex].Item);
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

    public void AddItem(ItemAmount itemAmount)
    {
        if (itemAmount == null || itemAmount.Item == null) return;

        int existingIndex = -1;
        if(itemAmount.Item is TileBuildableItem tileBuildable)
        {
            existingIndex = _items.FindIndex(
                x => x.Item is TileBuildableItem existing && existing.Tile == tileBuildable.Tile
            );
        }

        if(existingIndex == -1)  existingIndex = _items.FindIndex(x => x.Item.Name == itemAmount.Item.Name);

        if (existingIndex != -1)
        {
            _items[existingIndex].Amount += itemAmount.Amount;
        }
        else
        {
            var newItem = Instantiate(itemAmount.Item.gameObject, _hand.transform);
            _items.Add(new ItemAmount(newItem.GetComponent<Item>(), itemAmount.Amount));
            if (_items.Count == 1)
            {
                _currentIndex = 0;
                ChangeItem(_items[0].Item);
            }
        }
    }
}