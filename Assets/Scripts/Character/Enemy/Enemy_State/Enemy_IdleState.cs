using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_IdleState : Enemy_GroundState
{
    public Enemy_IdleState(Enemy enemy, StateMachine stateMachine, string animBoolName) : base(enemy, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = enemy.idleTime = Random.Range(enemy.minIdleTime, enemy.maxIdleTime);

    }

    public override void Update()
    {
        base.Update();
        if (stateTimer <= 0)
            stateMachine.ChangeState(enemy.moveState);
    }

    public override void Exit()
    {
        base.Exit();
        
    }
}
