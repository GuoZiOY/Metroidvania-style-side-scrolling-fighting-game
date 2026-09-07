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
        stateTimer = enemy.GetRandomIdleTime(); // 智能随机待机时长（不暴露参数）

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
