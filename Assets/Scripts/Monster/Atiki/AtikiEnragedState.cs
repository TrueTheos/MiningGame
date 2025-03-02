using System.Threading;

public class AtikiEnragedState : MonsterState
{
    private AtikiMonster _atiki;

    public AtikiEnragedState(AtikiMonster monster) : base(monster) 
    {
        _atiki = monster;
    }

    public override void Enter()
    {
        // When entering enraged state, set target to player and calculate path
        _atiki.SetTargetPosition(monster.GetPlayerPosition());
        monster.FindPath();
        monster.UpdatePathCalculationTime();

        // Transition to following path
        monster.ChangeState(monster._followPathState);
    }
}