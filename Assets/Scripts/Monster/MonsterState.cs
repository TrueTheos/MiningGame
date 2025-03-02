using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MonsterState
{
    protected Monster monster;

    public MonsterState(Monster monster)
    {
        this.monster = monster;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }
}
