using UnityEngine;

public class Player_IdleState : Player_GroundedState
{
    public Player_IdleState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }


    public override void Enter()
    {
        base.Enter();
        player.SetVelocity(0, rb.linearVelocity.y);//idleʱԭ�ز����������ƶ�/���/��Ծ����ٶȸ���
    }

    public override void Update()
    {
        base.Update();

        if (player.xInput != 0)
            stateMachine.ChangeState(player.moveState);
    }
}
