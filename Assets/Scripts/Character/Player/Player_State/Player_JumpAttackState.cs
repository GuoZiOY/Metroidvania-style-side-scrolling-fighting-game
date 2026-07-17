using UnityEngine;

public class Player_JumpAttackState : PlayerState
{
    private bool touchedGround;
    private bool hasTriggeredHitStop;

    public Player_JumpAttackState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        touchedGround = false;
        hasTriggeredHitStop = false;

        player.combat.SetSpecialAttackType(Player_Combat.SpecialAttackType.JumpAttack);
        player.combat.OnAttackHitResult += HandleAttackHit;
    }

    public override void Exit()
    {
        base.Exit();
        player.combat.OnAttackHitResult -= HandleAttackHit;
    }

    private void HandleAttackHit(ElementType element, bool hitResult)
    {
        if (hitResult && !hasTriggeredHitStop && player.combat.EnableJumpAttackHitStop)
        {
            hasTriggeredHitStop = true;
            Collider2D[] targets = player.combat.GetDectectedCollders();
            if (targets.Length > 0)
            {
                foreach (var target in targets)
                {
                    IDamgable damgable = target.GetComponent<IDamgable>();
                    if (damgable != null)
                    {
                        HitStopManager.Instance.TriggerLocalHitStop(player.gameObject, target.gameObject, player.combat.JumpAttackHitStopDuration);
                        
                        player.VFX.ShakeScreenForJumpAttack();
                        
                        break;
                    }
                }
            }
        }
    }

    public override void Update()
    {
        base.Update();

        if (player.isOnGround && touchedGround == false)//落地后使用jumpAttackTrigger
        {
            touchedGround = true;//落地后播放jumpAttack_end
            anim.SetTrigger("jumpAttackTrigger");
            player.SetVelocity(0, rb.velocity.y);
        }

        if (triggerCalled && player.isOnGround)//落地后播放jumpAttack_end切换idle
            stateMachine.ChangeState(player.idleState);
    }

}
