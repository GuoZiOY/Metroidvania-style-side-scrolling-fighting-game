using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_DomainExpansionState : PlayerState
{
    public Player_DomainExpansionState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    
    private Vector2 originalPosition;//原始位置
    private float originalGravity;//原始重力
    private float maxDistanceToGoUp;//最大上升距离

    private bool isLevitating;//是否正在悬浮
    private bool createdDomain;//是否已经创建了领域
    
    public override void Enter()
    {
        base.Enter();
        originalPosition = player.transform.position;
        originalGravity = rb.gravityScale;
        maxDistanceToGoUp = GetAvailableRiseDistance(); 

        player.SetVelocity(0,player.riseSpeed);
        player.disableDynamicGravity = true;
    }

    public override void Update()
    {
        base.Update();
        if(Vector2.Distance(originalPosition,player.transform.position) >= maxDistanceToGoUp && !isLevitating == true)
            Levitate();

        if(isLevitating == true)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0;
            skillManager.domainExpansion.DoSpellCasting();
            if(stateTimer <= 0)
            {
                rb.gravityScale = originalGravity;
                isLevitating = false;
                stateMachine.ChangeState(player.idleState);
            }
                
        }  
    }

    public override void Exit()
    {
        base.Exit();
        createdDomain = false;
        player.disableDynamicGravity = false;
    }

    private void Levitate()
    {
        isLevitating = true;
        rb.linearVelocity = Vector2.zero;//停止移动
        rb.gravityScale = 0;

        stateTimer = skillManager.domainExpansion.GetDomainDuration();//设置状态时间为领域持续时间  

        if(createdDomain == false)
        {
            createdDomain = true;
            skillManager.domainExpansion.CreatedDomain();
        }
    }

    private float GetAvailableRiseDistance()
    {
        RaycastHit2D hit = Physics2D.Raycast(player.transform.position, Vector2.up, player.riseMaxDistance,player.whatIsGround);
        return hit.collider != null ? hit.distance - 1f : player.riseMaxDistance;
    }
}
