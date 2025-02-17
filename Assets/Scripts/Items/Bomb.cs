using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Bomb : ThrowableItem
{
    public int Radius;
    public bool ExplodeOnCollision;
    public float ExplodeTime;
    public bool Sticky;
    public AudioPoint Audio;

    public override void OnThrow()
    {
        if (ExplodeTime > 0) StartCoroutine(ExplodeAfterDelay());
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

    public override void CollisionEnter(Collision2D collision)
    {
        if (ExplodeOnCollision)
        {
            Explode();
        }
    }
}
