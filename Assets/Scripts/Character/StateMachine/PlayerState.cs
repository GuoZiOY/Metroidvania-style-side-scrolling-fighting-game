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

        if ((GameInput.GetKeyDown(GameInput.Action.Dash) || (player.inputBuffer != null && player.inputBuffer.HasDashBuffer())) && CanDash())
        {
            if (player.inputBuffer != null)
                player.inputBuffer.ClearDashBuffer();
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

        // 反击切入：像冲刺一样，多数状态可强行切到反击（排除不可行动状态，见 CanUseCounter）
        if ((GameInput.GetKeyDown(GameInput.Action.CounterAttack) || (player.inputBuffer != null && player.inputBuffer.HasCounterAttackBuffer())) && CanUseCounter())
        {
            if (player.inputBuffer != null)
                player.inputBuffer.ClearCounterAttackBuffer();

            if (player.combat.IsCounterCooldownActive)
            {
                Debug.Log($"反击冷却中，剩余时间: {player.combat.CurrentCounterCooldown:F2}秒");
                return;
            }

            stateMachine.ChangeState(player.counterAttackState);
        }
    }

    // 反击可用性：排除无法执行反击的状态
    public bool CanUseCounter()
    {
        if (player.IsDead)
            return false;

        var cur = stateMachine.currentState;
        // 不能反击的状态：死亡、领域展开（无法行动）、反击/追击中（防止重入）
        if (cur == player.deadState || cur == player.domainExpansionState ||
            cur == player.counterAttackState || cur == player.counterChaseState)
            return false;

        return true;
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
