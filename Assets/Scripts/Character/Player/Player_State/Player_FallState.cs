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
        {
            bool isDoubleJumpLand = player.jumpCount >= 2;
            AudioManager.Instance?.PlayLandingSfx(isDoubleJumpLand);
            float particleScale = isDoubleJumpLand ? 1.2f : 1f;
            player.VFX?.PlayFallHitVFX(false, particleScale);
            stateMachine.ChangeState(player.idleState);
        }

        if (player.isOnWall)
            stateMachine.ChangeState(player.wallSlideState);
    }
}
