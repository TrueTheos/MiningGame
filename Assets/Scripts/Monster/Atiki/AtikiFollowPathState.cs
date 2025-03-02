using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class AtikiFollowPathState : MonsterFollowingPathState
{
    private AtikiMonster _atiki;

    public AtikiFollowPathState(AtikiMonster monster) : base(monster) 
    {
        _atiki = monster;
    }

    public override void Update()
    {
        // Check if we need to recalculate path
        if(_atiki.Enraged)
        {
            if(monster.ShouldRecalculatePath())
            {
                monster.SetTargetPosition(monster.GetPlayerPosition());
                monster.FindPath();
                monster.UpdatePathCalculationTime();
            }
        }
        else
        {
            if(_atiki.ShouldUpdateWanderTarget())
            {
                _atiki.ChangeState(_atiki._wanderState);
            }
        }

        // Check if path is complete or if target is reached
        if (monster.ReachedTarget() || monster.CurrentPath == null || monster.CurrentPath.Count == 0 || monster.CurrentPathIndex >= monster.CurrentPath.Count)
        {
            monster.ChangeState(monster._idleState);
            return;
        }
    }

    public override void FixedUpdate()
    {
        Queue<PathNode> currentPath = monster.CurrentPath;
        if (currentPath == null || currentPath.Count == 0)
        {
            monster.ChangeState(monster._idleState);
            return;
        }

        int currentPathIndex = monster.CurrentPathIndex;
        if (currentPathIndex >= currentPath.Count)
        {
            monster.ChangeState(monster._idleState);
            return;
        }

        // Get current target node
        PathNode currentTargetNode = currentPath.ElementAt(currentPathIndex);
        monster.SetCurrentTargetNode(currentTargetNode);
        Vector2Int targetPosition = currentTargetNode.Pos;

        float distToTarget = Vector2.Distance(monster.transform.position, targetPosition + Vector2.one * 0.5f);

        // Check if we've reached the target node
        if (distToTarget < monster.PathNodeReachDistance)
        {
            monster.IncrementPathIndex();
            currentPathIndex = monster.CurrentPathIndex;

            if (currentPathIndex >= currentPath.Count)
            {
                monster.ChangeState(monster._idleState);
                return;
            }

            currentTargetNode = currentPath.ElementAt(currentPathIndex);
            monster.SetCurrentTargetNode(currentTargetNode);
            targetPosition = currentTargetNode.Pos;
        }

        // Get connection between current position and next node
        PathNode.PathNodeConnection connection = null;

        if (currentPathIndex > 0)
        {
            connection = monster.GetConnection(currentPathIndex - 1, currentPathIndex);
        }
        else
        {
            // For the first node, determine connection type based on position
            float heightDiff = targetPosition.y - monster.transform.position.y;
            if (heightDiff > 0.5f)
            {
                connection = new PathNode.PathNodeConnection(
                PathNode.ConnectionType.JUMP,
                currentTargetNode,
                    monster.JumpPower
                );
            }
            else
            {
                connection = new PathNode.PathNodeConnection(
                    heightDiff < -0.5f ? PathNode.ConnectionType.FALL : PathNode.ConnectionType.WALK,
                    currentTargetNode,
                    -1f
                );
            }
        }

        if (connection == null)
        {
            monster.FindPath();
            return;
        }

        // Act based on connection type
        switch (connection.ConnType)
        {
            case PathNode.ConnectionType.WALK:
            case PathNode.ConnectionType.FALL:
                _atiki.HandleWalking(targetPosition);
                monster.Flip();
                break;
            case PathNode.ConnectionType.JUMP:
                _atiki.StartJumping(targetPosition);
                break;
        }
    }
}
