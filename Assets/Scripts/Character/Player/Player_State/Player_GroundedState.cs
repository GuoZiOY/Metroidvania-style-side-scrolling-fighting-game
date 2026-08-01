using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_GroundedState : PlayerState
{
    public Player_GroundedState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        if (player.justLanded)
            player.LandSquash();
        player.ResetJumpCount();
        player.SetDoubleJumping(false);
    }
    public override void Update()
    {
        base.Update();

        if (player.isOnGround)
            player.UpdateLastGroundedTime();

        if (rb.linearVelocity.y < 0 && player.isOnGround == false && !player.CanUseCoyoteTime())
            stateMachine.ChangeState(player.fallState);

        if (GameInput.GetKeyDown(GameInput.Action.Jump) || (player.inputBuffer != null && player.inputBuffer.HasJumpBuffer()))
        {
            if (player.inputBuffer != null)
                player.inputBuffer.ClearJumpBuffer();
            stateMachine.ChangeState(player.jumpState);
        }

        if (GameInput.GetKeyDown(GameInput.Action.Attack) || (player.inputBuffer != null && player.inputBuffer.HasAttackBuffer()))
        {
            if (player.inputBuffer != null)
                player.inputBuffer.ClearAttackBuffer();

            if (TryCounterChase())
                return;

            stateMachine.ChangeState(player.basicAttackState);
        }

        // 反击切入已统一移到 PlayerState.Update 基类（CanUseCounter 控制），此处不重复检测
    }

}
