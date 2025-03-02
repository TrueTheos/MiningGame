using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AtikiIdleState : MonsterIdleState
{
    private AtikiMonster _atiki;

    public AtikiIdleState(AtikiMonster monster) : base(monster)
    {
        _atiki = monster;
    }

    public override void Update()
    {
        // Check if we should transition to other states
        if (_atiki.Enraged)
        {
            _atiki.ChangeState(_atiki._enragedState);
        }
        else
        {
            _atiki.ChangeState(_atiki._wanderState);
        }
    }
}
