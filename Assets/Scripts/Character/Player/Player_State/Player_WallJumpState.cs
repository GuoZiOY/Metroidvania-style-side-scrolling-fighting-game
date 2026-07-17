using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_WallJumpState : PlayerState

{
    public Player_WallJumpState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }


    public override void Enter()
    {
        base.Enter();
        player.SetVelocity(player.wallJumpForce.x * -player.facingDir,player.wallJumpForce.y);//翻转角色朝向即滑墙时角色面相按wallJumpForce跳跃

    }
    public override void Update()
    {
        base.Update();

        if(rb.velocity.y < 0)
            stateMachine.ChangeState(player.fallState);

        if(player.isOnWall)
            stateMachine.ChangeState(player.wallSlideState);
    }
}
