using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_MoveState : Player_GroundedState
{
    public Player_MoveState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(player.xInput * player.moveSpeed,player.rb.velocity.y);//½ÇÉ«ÒÆ¶¯

        if (player.xInput == 0)
            stateMachine.ChangeState(player.idleState);
        
    }
}
