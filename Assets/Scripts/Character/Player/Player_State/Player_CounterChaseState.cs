using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_CounterChaseState : PlayerState
{
    private Transform targetEnemy;
    private float chaseSpeed;
    private float stopDistance;
    private float originalGravityScale;
    private int chaseDir;
    private bool hasReachedTarget;

    public Player_CounterChaseState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public void SetTarget(Transform target)
    {
        targetEnemy = target;
    }

    public override void Enter()
    {
        base.Enter();

        if (targetEnemy == null)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        player.health.canBeTakedDamage = false;
        hasReachedTarget = false;

        chaseSpeed = player.dashSpeed * player.combat.ChaseSpeedMultiplier;
        stopDistance = player.combat.ChaseStopDistance;
        chaseDir = player.transform.position.x < targetEnemy.position.x ? 1 : -1;
        originalGravityScale = rb.gravityScale;
        rb.gravityScale = 0;

        stateTimer = 0.5f;

        player.combat.SetSpecialAttackType(Player_Combat.SpecialAttackType.ChaseAttack);
    }

    public override void Update()
    {
        base.Update();

        if (targetEnemy == null)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        if (player.IsHitStopActive)
            return;

        float currentDistance = Vector2.Distance(player.transform.position, targetEnemy.position);

        if (currentDistance > stopDistance)
        {
            player.SetVelocity(chaseSpeed * chaseDir, 0);
        }
        else
        {
            hasReachedTarget = true;
            player.SetVelocity(0, 0);
        }

        if (hasReachedTarget || stateTimer < 0)
            stateMachine.ChangeState(player.basicAttackState);
    }

    public override void Exit()
    {
        base.Exit();

        rb.gravityScale = originalGravityScale;
        rb.velocity = Vector2.zero;

        float extraInv = skillManager.powerCounterChase?.GetExtraInvincibilityDuration() ?? 0f;
        player.StartCoroutine(DelayedResetInvulnerability(player, 0.2f + extraInv));

        Player_BasicAttackState attackState = player.basicAttackState as Player_BasicAttackState;
        attackState?.SetComboIndex(3);
    }

    private static IEnumerator DelayedResetInvulnerability(Player p, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (p != null) p.health.canBeTakedDamage = true;
    }
}
