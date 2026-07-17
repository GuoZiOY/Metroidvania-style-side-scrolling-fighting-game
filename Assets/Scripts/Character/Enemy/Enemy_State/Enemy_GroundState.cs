using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_GroundState : EnemyState
{
    public Enemy_GroundState(Enemy enemy, StateMachine stateMachine, string animBoolName) : base(enemy, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        if (enemy.PlayerDetected() == true)//如果检测到玩家，进入战斗状态
            stateMachine.ChangeState(enemy.battleState);
    }
}
