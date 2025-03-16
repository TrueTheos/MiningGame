using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tree : CustomBuilding
{
    [SerializeField] private TileSO _planksSO;

    public override void OnBreak()
    {
        foreach (var req in RequiredFreeSpaces)
        {
            int newX = Pos.x + req.x;
            int newY = Pos.y + req.y;
            if (!50.RandTest()) continue;

            var tile = TileFactory.Instance.SpawnTile(_planksSO);
            WorldManager.Instance.SpawnPickupable(newX, newY, new ItemAmount(tile.GetComponent<Item>(), 1));
        }

        var particle = Instantiate(WorldManager.Instance.DestroyTileParticle, new Vector3(Pos.x + .5f, Pos.y + 4.5f, 0), Quaternion.identity);
        particle.startColor = ParticleColors.Random();
        particle.Play();
        particle.Emit(Random.Range(3, 8));

        base.OnBreak();
    }

    public override void PlayHitAnimation()
    {
        transform.DOScale(new Vector3(.98f, .98f, 1f), 0.05f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    transform.DOScale(Vector3.one, 0.05f)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() => {
                            WorldManager.Instance.OnHitAnimationEnd(Pos.x, Pos.y);
                        });
                });
    }
}
