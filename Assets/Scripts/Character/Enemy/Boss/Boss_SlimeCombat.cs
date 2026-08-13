using UnityEngine;

// 史莱姆王专属战斗组件（Entity_Combat 子类）
// Boss 的专属攻击方式（接触/落点/冲刺/传送）通过 DealDamageTo 走标准攻击管线，
// 不污染公共 Entity_Combat（其他敌人共用）。
public class Boss_SlimeCombat : Entity_Combat
{
    // 对指定目标造成一次攻击伤害（Boss 自定义命中：接触/落点/冲刺/传送用）
    // multiplier = 招式伤害倍率（如冲刺 1.5x、传送 2x）；复用标准攻击管线（攻击力/暴击/护甲/VFX/词缀）
    public void DealDamageTo(Collider2D target, float multiplier = 1f)
    {
        IDamgable damgable = target != null ? target.GetComponent<IDamgable>() : null;
        if (damgable == null)
            return;

        AttackData attackData = CalculateAttackData();//计算攻击数据（含暴击）
        attackData.phyiscalDamage *= multiplier;//招式伤害倍率
        attackData.elementalDamage *= multiplier;

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
}
