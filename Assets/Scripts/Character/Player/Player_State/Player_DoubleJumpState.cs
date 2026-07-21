using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_DoubleJumpState : Player_AiredState
{
    public Player_DoubleJumpState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        AudioManager.Instance?.PlayJumpSfx(true);
        player.SetDoubleJumping(true);
        player.JumpSquashAndStretch();
        player.IncrementJumpCount();
        player.SetVelocity(0, player.skillManager.doubleJump.GetDoubleJumpForce()); // 二段跳时x速度设为0，保持当前朝向
    }

    public override void Update()
    {
        base.Update();
        if (rb.linearVelocity.y < 0 && stateMachine.currentState != player.jumpAttackState)
            stateMachine.ChangeState(player.fallState);
    }
}