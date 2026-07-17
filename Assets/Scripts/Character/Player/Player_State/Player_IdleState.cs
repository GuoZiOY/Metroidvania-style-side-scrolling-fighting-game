using UnityEngine;

public class Player_IdleState : Player_GroundedState
{
    public Player_IdleState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }


    public override void Enter()
    {
        base.Enter();
        player.SetVelocity(0, rb.velocity.y);//idle时原地不动，避免移动/冲刺/跳跃后的速度赋予
    }

    public override void Update()
    {
        base.Update();

        if (player.xInput != 0)
            stateMachine.ChangeState(player.moveState);
    }
}
