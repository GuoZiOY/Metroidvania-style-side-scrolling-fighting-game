using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_JumpState : Player_AiredState
{
    public Player_JumpState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        AudioManager.Instance?.PlayJumpSfx(false);
        player.JumpSquashAndStretch();
        player.IncrementJumpCount();
        player.SetVelocity(0, player.jumpForce); // 跳跃时x速度设为0，保持当前朝向
    }
    public override void Update()
    {
        base.Update();
        if (rb.linearVelocity.y < 0 && stateMachine.currentState != player.jumpAttackState)//�Ż���Ծ����ʱ����ͬ֡�л��������bug����Ȼ����������֣�
            stateMachine.ChangeState(player.fallState);
    }


}
