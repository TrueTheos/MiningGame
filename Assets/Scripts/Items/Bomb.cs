using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Bomb : Item
{
    public int Radius;
    public float ThrowPower;
    public bool ExplodeOnCollision;
    public float ExplodeTime;
    public bool Sticky;
    public AudioPoint Audio;

    private void Awake()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var collider = GetComponent<Collider2D>();
        collider.enabled = false;
    }

    public override void UseOnce()
    {
        if (transform.parent == null) return;

        var newBomb = Instantiate(gameObject, transform.position, Quaternion.identity);
        newBomb.transform.SetParent(null);
        Rigidbody2D rb = newBomb.GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        var collider = newBomb.GetComponent<Collider2D>();
        collider.enabled = true;

        Inventory.Instance.RemoveOne();

        if (rb != null)
        {
            newBomb.transform.position = PlayerMovement.Instance.transform.position;
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 throwDirection = (mousePos - (Vector2)transform.position).normalized;

            rb.AddForce(throwDirection * ThrowPower, ForceMode2D.Impulse);

            if (ExplodeTime > 0) newBomb.GetComponent<Bomb>().StartExploding();
        }
    }

    public void StartExploding()
    {
        StartCoroutine(ExplodeAfterDelay());
    }

    private void Explode()
    {
        var wm = WorldManager.Instance;

        Vector3Int centerCell = wm.MainTilemap.WorldToCell(transform.position);

        int range = Mathf.CeilToInt(Radius);
        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                Vector3Int tilePos = centerCell + new Vector3Int(x, y, 0);

                Vector3 worldPos = wm.MainTilemap.GetCellCenterWorld(tilePos);

                if (Vector2.Distance(worldPos, transform.position) <= Radius)
                {
                    wm.BreakTile(tilePos.x, tilePos.y);
                }
            }
        }

        AudioManager.Instance.PlayMine();
        AudioManager.Instance.PlayAtPos(Audio, transform.position);

        Destroy(gameObject);
    }

    private IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(ExplodeTime);
        Explode();
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        if(ExplodeOnCollision)
        {
            Explode();
        }
    }
}
