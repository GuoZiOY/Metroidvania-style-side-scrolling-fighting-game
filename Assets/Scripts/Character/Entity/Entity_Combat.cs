using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using static Entity_Stats;

public class Entity_Combat : MonoBehaviour
{
    protected Entity_VFX vfx;
    protected Entity_Stats stats;

    public DamageScaleData basicAttacksData;

    [Header("攻击组块")]
    [SerializeField] private Transform targetCheck;
    [SerializeField] public float targetCheckRadius;
    [SerializeField] private LayerMask whatIsTarget;

protected virtual void Awake()
    {
        vfx = GetComponentInParent<Entity_VFX>();
        stats = GetComponentInParent<Entity_Stats>();
    }

    public event Action<ElementType, bool> OnAttackHitResult;

    public virtual void PerformAttack()//执行攻击
    {
        Collider2D[] targets = GetDectectedCollders();
        foreach (var target in targets)
        {
            ProcessAttackOnTarget(target);
        }
    }

    protected virtual void ProcessAttackOnTarget(Collider2D target)//处理对目标的攻击
    {
        IDamgable damgable = target.GetComponent<IDamgable>();//获取目标的可伤害组件
        if (damgable == null)
            return;

        AttackData attackData = CalculateAttackData();//计算攻击数据

        bool targetGotHit = damgable.TakeDamage(attackData.phyiscalDamage, attackData.elementalDamage, attackData.element, transform, attackData.isCrit);//对目标造成伤害

        if (targetGotHit)
        {
            OnTargetHit(target, attackData);//触发目标命中事件
            ApplyElementalEffect(target, attackData);//应用元素效果
            CreateHitVFX(target, attackData.isCrit, attackData.element);

            // V2: 通知自身 Enemy 的词缀（如吸血、闪电导体）
            var dealerEnemy = GetComponentInParent<Enemy>();
            if (dealerEnemy != null)
            {
                float totalDmg = attackData.phyiscalDamage + attackData.elementalDamage;
                dealerEnemy.ReportDealtDamage(totalDmg);
            }
        }
    }

    protected virtual AttackData CalculateAttackData()
    {
        return stats.GetAttackData(basicAttacksData);
    }

    protected virtual void OnTargetHit(Collider2D target, AttackData attackData)//目标命中事件
    {
        TriggerOnAttackHitResult(attackData.element, true);//触发攻击命中结果事件
    }

    protected virtual void ApplyElementalEffect(Collider2D target, AttackData attackData)
    {
        if (attackData.element == ElementType.None)
            return;

        Entity_StatusHandler statusHandler = target.GetComponent<Entity_StatusHandler>();
        if (statusHandler != null)
        {
            statusHandler.ApplyStatusEffect(attackData.element, attackData.effectData);//应用元素效果
        }
    }

    protected virtual void CreateHitVFX(Collider2D target, bool isCrit, ElementType element)//创建命中特效  
    {
        Entity_Health health = target.GetComponent<Entity_Health>();
        if (health != null && health.canBeTakedDamage)
        {
            vfx.CreateOnHitVFX(target.transform, isCrit, element);//创建命中特效
        }
    }

    protected virtual void TriggerOnAttackHitResult(ElementType element, bool hitResult)//触发攻击命中结果事件  
    {
        OnAttackHitResult?.Invoke(element, hitResult);
    }

    public Collider2D[] GetDectectedCollders()//获取检测到的碰撞体
    {
        return Physics2D.OverlapCircleAll(targetCheck.position,targetCheckRadius,whatIsTarget);
    }

    private void OnDrawGizmos()//绘制调试用的碰撞体检测范围
    {
        Gizmos.DrawWireSphere(targetCheck.position, targetCheckRadius);
    }

}
