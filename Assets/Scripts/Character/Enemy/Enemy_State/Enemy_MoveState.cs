using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_MoveState : Enemy_GroundState
{
    public Enemy_MoveState(Enemy enemy, StateMachine stateMachine, string animBoolName) : base(enemy, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = enemy.moveTime = Random.Range(enemy.minMoveTime,enemy.maxMoveTime);

        if (enemy.isOnGround == false || enemy.isOnWall)
            enemy.Flip();
    }

    public override void Update()
    {
        base.Update();
        enemy.SetVelocity(enemy.GetMoveSpeed() * enemy.facingDir, rb.linearVelocity.y);

        if (enemy.isOnGround == false || enemy.isOnWall || stateTimer <= 0)
        {
            stateMachine.ChangeState(enemy.idleState);
        }
    }

    public override void Exit()
    {
        base.Exit();
    }
}
