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
            // 反击成功：开启无敌帧（由 Player_Combat 独立计时，不随本状态退出而提前结束）
            player.combat.StartCounterInvincibility();
            Debug.Log("反击成功，进入无敌帧");
        }

        if (counteredSomebody && player.combat.ChaseTarget != null)
        {
            player.combat.ActivateChaseTime(player.combat.ChaseTarget);
        }
    }

    public override void Update()
    {
        base.Update();

        // 基类检测可能已切入冲刺/领域展开等状态（如反击中按冲刺键）
        // 此时当前方法剩余逻辑不应再执行，否则 SetVelocity(0,...) 会覆盖新状态第一帧速度
        if (stateMachine.currentState != this)
            return;

        player.SetVelocity(0, rb.linearVelocity.y);

        if (stateTimer < 0)
            stateMachine.ChangeState(player.idleState);
    }

    public override void Exit()
    {
        base.Exit();
        // 注意：不在此恢复 canBeTakedDamage。
        // 反击无敌由 Player_Combat.UpdateCounterInvincibility 计时管理，
        // 若进入追击状态则由 Player_CounterChaseState 接管。
    }
}
