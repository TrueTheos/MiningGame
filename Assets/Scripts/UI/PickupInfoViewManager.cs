using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PickupInfoViewManager : MonoBehaviour
{
    public static PickupInfoViewManager Instance;

    [SerializeField] private PickupInfoUI _pickupPrefab;
    [SerializeField] private GameObject _parent;

    private List<PickupInfoUI> _infos = new();

    private void Awake()
    {
        Instance = this;
    }

    public void Pickup(ItemAmount item)
    {
        _infos.RemoveAll(info => info == null);

        if (_infos.Count > 0)
        {
            PickupInfoUI lastInfo = _infos.Last();
            if (lastInfo.ItemAmount.Item.EqualsType(item.Item))
            {
                lastInfo.Increase(item.Amount);
                return;
            }
        }

        PickupInfoUI newInfo = Instantiate(_pickupPrefab, _parent.transform).GetComponent<PickupInfoUI>();
        newInfo.gameObject.transform.SetAsFirstSibling();
        newInfo.Init(item);

        _infos.Add(newInfo);
    }
}
