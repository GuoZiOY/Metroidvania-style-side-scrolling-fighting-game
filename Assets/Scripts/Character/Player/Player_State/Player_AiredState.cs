using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_AiredState : PlayerState
{
    public Player_AiredState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        if (player.xInput != 0)
            player.SetVelocity(player.xInput *player.moveSpeed * player.inAirMoveMuliplier, rb.linearVelocity.y);

        if (GameInput.GetKeyDown(GameInput.Action.Attack) || (player.inputBuffer != null && player.inputBuffer.HasAttackBuffer()))
        {
            if (player.inputBuffer != null)
                player.inputBuffer.ClearAttackBuffer();

            if (TryCounterChase())
                return;

            stateMachine.ChangeState(player.jumpAttackState);
        }

        if (GameInput.GetKeyDown(GameInput.Action.Jump) && player.CanUseCoyoteTime() && player.jumpCount < 1)
            stateMachine.ChangeState(player.jumpState);

        if ((GameInput.GetKeyDown(GameInput.Action.Jump) || (player.inputBuffer != null && player.inputBuffer.HasDoubleJumpBuffer())) && player.skillManager != null && player.skillManager.doubleJump != null && player.skillManager.doubleJump.CanDoubleJump() && stateMachine.currentState != player.doubleJumpState && player.jumpCount < 2)
        {
            if (player.inputBuffer != null)
                player.inputBuffer.ClearDoubleJumpBuffer();
            stateMachine.ChangeState(player.doubleJumpState);
        }
    }
}
