using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Skeleton : Enemy,ICounterable
{
    [Header("追击设置")]
    [SerializeField] private bool canBeChased = true;

    public bool IsInCounterTime { get => isInCounterTime; }
    public bool CanBeChased { get => canBeChased; }

    protected override void Awake()
    {
        base.Awake();

        idleState = new Enemy_IdleState(this,stateMachine,"idle");
        moveState = new Enemy_MoveState(this, stateMachine, "move");
        attackState = new Enemy_AttackState(this, stateMachine, "attack");
        battleState = new Enemy_BattleState(this, stateMachine, "battle");
        deadState = new Enemy_DeadState(this,stateMachine,"dead");
        stunnedState = new Enemy_StunnedState(this, stateMachine, "stunned");

    }
    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();

    }

    public void HandleCounter(float knockbackMultiplier = 1f)
    {
        if(canBeStunned == false) return;
        ApplyCounterKnockback(knockbackMultiplier);
        stateMachine.ChangeState(stunnedState);
    }

    private void ApplyCounterKnockback(float multiplier)
    {
        Vector2 counterKnockback = new Vector2(stunnedVelocity.x * multiplier, stunnedVelocity.y * multiplier);
        rb.linearVelocity = new Vector2(counterKnockback.x * -DirctionToPlayer(), counterKnockback.y);
    }
}
