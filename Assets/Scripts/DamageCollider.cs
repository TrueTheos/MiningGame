using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageCollider : MonoBehaviour
{
    private IDamageColliderReceiver _receiver;

    private void Awake()
    {
        _receiver = GetComponentInParent<IDamageColliderReceiver>();
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if(_receiver != null) _receiver.Receive(collision.gameObject);
    }
}
