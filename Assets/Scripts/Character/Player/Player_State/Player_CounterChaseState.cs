using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_CounterChaseState : PlayerState
{
    private Transform targetEnemy;
    private float chaseSpeed;
    private float stopDistance;
    private float originalGravityScale;
    private bool originalHitStopEnabled; // 进入追击前的顿帧开关状态
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

        // 追击状态接管无敌管理：清除反击无敌计时，避免到期提前恢复受伤
        player.combat.CancelCounterInvincibility();
        player.health.canBeTakedDamage = false;
        hasReachedTarget = false;

        // 追击期间禁用顿帧：顿帧会 FreezeAll 冻结物理 + SetAnimationSpeed 冻结动画，
        // 导致追击突进被卡住（空中悬浮或移动停下）。追击突进不应被顿帧打断
        originalHitStopEnabled = player.HitStopEnabled;
        player.HitStopEnabled = false;

        // 结束残留顿帧（防御：禁用开关只阻止新的顿帧，已触发的需主动结束）
        if (player.IsHitStopActive)
            player.EndHitStop();

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

        // 顿帧不应打断追击突进：反击命中触发的顿帧是延迟协程（等1帧），可能在本状态内触发。
        // 每帧强制结束顿帧，避免 FreezeAll 冻结物理 + SetVelocity 被拒导致空中悬浮落下
        if (player.IsHitStopActive)
            player.EndHitStop();

        // 距离判定：只比较 X 轴（追击是水平冲刺，Y 差不应导致永远追不上）
        float currentDistance = Mathf.Abs(player.transform.position.x - targetEnemy.position.x);

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

        // 恢复顿帧开关（追击结束，后续状态可正常顿帧）
        player.HitStopEnabled = originalHitStopEnabled;

        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = Vector2.zero;

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
