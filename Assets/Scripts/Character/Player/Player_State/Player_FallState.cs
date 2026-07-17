using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_FallState : Player_AiredState
{
    public Player_FallState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        if (player.isOnGround)
            stateMachine.ChangeState(player.idleState);

        if (player.isOnWall)
            stateMachine.ChangeState(player.wallSlideState);
    }
}
