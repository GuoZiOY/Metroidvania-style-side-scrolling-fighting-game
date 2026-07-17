using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_CounterAttackState : PlayerState
{
    public Player_CounterAttackState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    private bool counteredSomebody;

    public override void Enter()
    {
        base.Enter();

        stateTimer = player.combat.CounterRecoveryDuration;
        counteredSomebody = player.combat.CounterAttackPerformed();
        anim.SetBool("counterAttackPerformed", counteredSomebody);

        if (counteredSomebody)
        {
            player.health.canBeTakedDamage = false;
            Debug.Log("反击成功，期间免疫伤害");
        }

        if (counteredSomebody && player.combat.ChaseTarget != null)
        {
            player.combat.ActivateChaseTime(player.combat.ChaseTarget);
        }
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(0, rb.velocity.y);

        if (stateTimer < 0)
            stateMachine.ChangeState(player.idleState);
    }

    public override void Exit()
    {
        base.Exit();

        if (counteredSomebody)
        {
            player.health.canBeTakedDamage = true;
            Debug.Log("反击状态结束，恢复伤害接收");
        }
    }
}
