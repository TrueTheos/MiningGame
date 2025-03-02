using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterIdleState : MonsterState
{
    public MonsterIdleState(Monster monster) : base(monster) { }

    public override void Enter()
    {
        // Reset velocity when entering idle state
        monster.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
    }

    public override void Update()
    {

    }
}
