using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_MoveState : Player_GroundedState
{
    private float footstepTimer;

    public Player_MoveState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        footstepTimer = 0;
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(player.xInput * player.moveSpeed, player.rb.linearVelocity.y);

        HandleFootstep();

        if (player.xInput == 0)
            stateMachine.ChangeState(player.idleState);
    }

    private void HandleFootstep()
    {
        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0)
        {
            AudioManager.Instance?.PlayFootstepSfx();
            footstepTimer = 0.3f / Mathf.Max(Mathf.Abs(player.xInput), 0.5f);
        }
    }
}
