using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AtikiWanderState : MonsterState
{
    private AtikiMonster _atiki;

    public AtikiWanderState(AtikiMonster monster) : base(monster)
    {
        _atiki = monster;
    }

    public override void Update()
    {
        if(_atiki.ShouldUpdateWanderTarget())
        {
            _atiki.UpdateWanderTargetTime();
            List<Vector2Int> nodes = _atiki.GetWanderTargets();

            if (nodes == null || nodes.Count == 0)
            {
                _atiki.ChangeState(monster._idleState);
            }
            else
            {
                _atiki.SetTargetPosition(_atiki.GetWanderTargets().Random());
                monster.FindPath();

                monster.ChangeState(monster._followPathState);
            }
        }
    }
}
