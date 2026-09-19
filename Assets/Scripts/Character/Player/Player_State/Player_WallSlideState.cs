using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_WallSlideState : PlayerState
{
    public Player_WallSlideState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        HandleWallSlide();

        if (GameInput.GetKeyDown(GameInput.Action.Jump) || player.inputBuffer.HasJumpBuffer())
        {
            player.inputBuffer.ClearJumpBuffer();
            stateMachine.ChangeState(player.jumpState);
        }

        if (player.isOnWall == false)
            stateMachine.ChangeState(player.fallState);

        if (player.isOnGround)
        {
            stateMachine.ChangeState(player.idleState);
            if (player.facingDir != player.xInput)
                player.Flip();
        }
    }
    
    private void HandleWallSlide()
    {
        if (player.yInput < 0)//��ǽʱ����s��ԭ���»�
            player.SetVelocity(player.xInput, rb.linearVelocity.y);
        else//���򣬻����»�
            player.SetVelocity(player.xInput, rb.linearVelocity.y * player.wallSlideSlowMoveMuliplier);
    }
}
 