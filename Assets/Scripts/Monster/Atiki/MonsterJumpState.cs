using System.Threading;
using UnityEngine;

public class MonsterJumpingState : MonsterState
{
    private Vector2 targetPosition;

    public MonsterJumpingState(AtikiMonster monster) : base(monster) { }

    public override void Enter()
    {
        targetPosition = monster.CurrentTargetNode.Pos;

        if (monster.IsGrounded())
        {
            monster.PerformJump(targetPosition);
            monster.Flip();
        }
    }

    public override void Update()
    {
        Vector2 targetPosWithOffset = targetPosition + Vector2.one * 0.5f;

        // Check if we've reached the target or if jump has timed out
        if (Vector2.Distance(monster.transform.position, targetPosWithOffset) < monster.PathNodeReachDistance ||
        Time.time - monster.JumpStartTime > 2.0f ||
            (monster.IsGrounded() && Time.time - monster.JumpStartTime > 0.2f))
        {
            monster.ChangeState(monster._followPathState);
        }
    }

    public override void FixedUpdate()
    {
        monster.MaintainJumpMovement(targetPosition);
    }
}