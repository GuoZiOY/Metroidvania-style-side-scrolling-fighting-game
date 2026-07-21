using UnityEditor;
using UnityEngine;

public abstract class PlayerState : EntityState
{
    protected Player player;
    protected Player_SkillManager skillManager;
    
    
    public PlayerState(Player player,StateMachine stateMachine, string animBoolName) :base(stateMachine , animBoolName) //创建状态
    {
        this.player = player;
        anim = player.anim;
        rb = player.rb;
        stats = player.stats;
        skillManager = player.skillManager;
    }


    public override void Enter()
    {
        base.Enter();
    }

    public override void Update()
    {
        base.Update();

        if (GameInput.GetKeyDown(GameInput.Action.Dash) && CanDash())
        {
            skillManager.dash.StartSkillCooldown();
            stateMachine.ChangeState(player.dashState);
        }

        if (GameInput.GetKeyDown(GameInput.Action.DomainExpansion) && skillManager.domainExpansion.CanUseSkill())
        {
            if (skillManager.domainExpansion.InstantDomain())
                skillManager.domainExpansion.CreatedDomain();
            else
                stateMachine.ChangeState(player.domainExpansionState);

            skillManager.domainExpansion.StartSkillCooldown();
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void UpdateAnimationParaeters()
    {
        base.UpdateAnimationParaeters();
        anim.SetFloat("yVelocity", rb.linearVelocity.y);

    }


    public bool CanDash()
    {  
        if (skillManager.dash.CanUseSkill() == false)
            return false;

        if (player.isOnWall)
            return false;

        if(stateMachine.currentState == player.dashState)
            return false;

        return true;
    }

    protected bool TryCounterChase()
    {
        if (!player.combat.IsChaseTimeActive)
            return false;

        Transform chaseTarget = player.combat.ChaseTarget;
        if (chaseTarget == null)
            return false;

        Debug.Log("检测到攻击输入，追击时间已激活");
        Debug.Log($"准备切换到追击状态，目标: {chaseTarget.name}");

        Player_CounterChaseState chaseState = player.counterChaseState as Player_CounterChaseState;
        if (chaseState == null)
            return false;

        chaseState.SetTarget(chaseTarget);
        player.combat.DeactivateChaseTime();
        stateMachine.ChangeState(player.counterChaseState);
        return true;
    }

}
