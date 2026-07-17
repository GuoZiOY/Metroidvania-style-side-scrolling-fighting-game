using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_DashState : PlayerState
{
    

    public Player_DashState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public float originalGravityScale;
    private int dashDir;

    public override void Enter()
    {
        base.Enter();

        skillManager.dash.OnStartEffect();

        if (skillManager.dashBlur != null && skillManager.dashBlur.CanXuHua())
        {
            player.VFX.DoImageEchoEffect(player.dashDuration);
            player.health.canBeTakedDamage = false;
        }

        stateTimer = player.dashDuration;

        InPutDashDir();

        originalGravityScale = rb.gravityScale;
        rb.gravityScale = 0;
    }
    
    public override void Update()
    {
        base.Update();
        player.SetVelocity(player.dashSpeed * dashDir,0);
        CanelDashIfNeeded();
        if (stateTimer < 0)
            if(player.isOnGround)
                stateMachine.ChangeState(player.idleState);
            else
                stateMachine.ChangeState(player.fallState);

        if (player.inputBuffer != null && player.inputBuffer.HasAttackBuffer())
        {
            player.inputBuffer.ClearAttackBuffer();
            
            if (TryCounterChase())
                return;
            
            stateMachine.ChangeState(player.basicAttackState);
        }

        if (player.inputBuffer != null && player.inputBuffer.HasDoubleJumpBuffer() && !player.isOnGround && player.skillManager != null && player.skillManager.doubleJump != null && player.skillManager.doubleJump.CanDoubleJump())
        {
            player.inputBuffer.ClearDoubleJumpBuffer();
            stateMachine.ChangeState(player.doubleJumpState);
        }

        if (player.inputBuffer != null && player.inputBuffer.HasJumpBuffer() && player.isOnGround)
        {
            player.inputBuffer.ClearJumpBuffer();
            stateMachine.ChangeState(player.jumpState);
        }
    }

    public override void Exit()
    {
        base.Exit();

        skillManager.dash.OnEndEffect();

        if (skillManager.dashBlur != null && skillManager.dashBlur.CanXuHua())
        {
            player.health.canBeTakedDamage = true;
        }

        player.SetVelocity(0, 0);
        rb.gravityScale = originalGravityScale;
    }

    private void InPutDashDir()
    {
        if (player.xInput != 0)
            dashDir = ((int)player.xInput);
        else
            dashDir = player.facingDir;
    }

    public void CanelDashIfNeeded()
    {
        if (player.isOnWall)
        {
            if (player.isOnGround)
                stateMachine.ChangeState(player.idleState);
            else
                stateMachine.ChangeState(player.wallSlideState);
        }
    }
}
