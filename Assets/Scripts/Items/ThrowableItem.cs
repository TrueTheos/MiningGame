using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrowableItem : Item
{
    [SerializeField] protected float _throwPower;

    [HideInInspector] public bool IsThrown;

    protected Rigidbody2D _rb;
    protected Collider2D _collider;

    protected void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _collider = GetComponent<Collider2D>();
        _collider.enabled = false;
    }

    public override void UseOnce()
    {
        Throw(_throwPower);
    }

    protected void Throw(float power)
    {
        if (transform.parent == null) return;

        var thrown = Instantiate(gameObject, PlayerMovement.Instance.transform.position, Quaternion.identity);
        thrown.transform.SetParent(null);
        Rigidbody2D rb = thrown.GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        var collider = thrown.GetComponent<Collider2D>();
        collider.enabled = true;

        Inventory.Instance.RemoveOne();

        if (rb != null)
        {
            thrown.transform.position = PlayerMovement.Instance.transform.position;
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 throwDirection = (mousePos - (Vector2)PlayerMovement.Instance.transform.position).normalized;

            thrown.GetComponent<ThrowableItem>().IsThrown = true;

            rb.AddForce(throwDirection * power, ForceMode2D.Impulse);
            thrown.GetComponent<ThrowableItem>().OnThrow(thrown.transform.position, power);
        }
    }

    public virtual void OnThrow(Vector2 origin, float power) { }

    public virtual void CollisionEnter(Collision2D collision) { }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        CollisionEnter(collision);
    }
}