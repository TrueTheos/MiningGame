using static UnityEngine.RuleTile;
using System;
using UnityEngine.Tilemaps;
using UnityEngine;

[CreateAssetMenu]
public class SiblingRuleTile : RuleTile
{
    public override bool RuleMatch(int neighbor, TileBase other)
    {
        if (other is RuleOverrideTile)
            other = (other as RuleOverrideTile).m_InstanceTile;

        switch (neighbor)
        {
            case TilingRule.Neighbor.This:
                {
                    return other != null;
                }
            case TilingRule.Neighbor.NotThis:
                {
                    return other == null;
                }
        }

        return base.RuleMatch(neighbor, other);
    }
}