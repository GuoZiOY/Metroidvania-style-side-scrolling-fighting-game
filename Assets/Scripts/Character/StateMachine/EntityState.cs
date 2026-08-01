using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EntityState
{
    protected StateMachine stateMachine;
    protected string animBoolName;

    protected Animator anim;
    protected Rigidbody2D rb;
    protected Entity_Stats stats;

    protected float stateTimer;
    protected bool triggerCalled;


    public EntityState(StateMachine stateMachine, string animBoolName)
    {
        this.stateMachine = stateMachine;
        this.animBoolName = animBoolName;
    }
    public virtual void Enter()
    {
        anim.SetBool(animBoolName, true);
        triggerCalled = false;//进入状态时，重置触发标志位

    }
    public virtual void Update()
    { 

        stateTimer -= Time.deltaTime;//更新状态定时器
        UpdateAnimationParaeters();

    }
    public virtual void Exit()
    {
        anim.SetBool(animBoolName, false);//退出状态时，重置动画参数为false

    }
    public void AnimationTriggers()//触发动画事件
    {
        triggerCalled = true;
    }

    public virtual void UpdateAnimationParaeters()
    {

    }

    public void SyncAttackSpeed()//同步攻击速度
    {
        float attackSpeed = stats.offense.attackSpeed.GetValue();
        anim.SetFloat("attackSpeedMultiplier", attackSpeed);
    }
}
