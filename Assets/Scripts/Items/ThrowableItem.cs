using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrowableItem : Item
{
    public float ThrowPower;
    [HideInInspector] public bool IsThrown;

    protected void Awake()
    {
        Debug.Log($"{name} 1");
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var collider = GetComponent<Collider2D>();
        collider.enabled = false;
    }

    public override void UseOnce()
    {
        if (transform.parent == null) return;

        var thrown = Instantiate(gameObject, transform.position, Quaternion.identity);
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
            Vector2 throwDirection = (mousePos - (Vector2)transform.position).normalized;

            thrown.GetComponent<ThrowableItem>().IsThrown = true;

            rb.AddForce(throwDirection * ThrowPower, ForceMode2D.Impulse);
            thrown.GetComponent<ThrowableItem>().OnThrow();
        }
    }

    public virtual void OnThrow() { }

    public virtual void CollisionEnter(Collision2D collision) { }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        CollisionEnter(collision);
    }
}
