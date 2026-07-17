using UnityEngine;

public abstract class EnemyTypeSystem : MonoBehaviour // 敌人类型系统基类
{
    [Header("基础设置")]
    [SerializeField] protected Enemy enemy; // 敌人引用

    [Header("共有加成设置")]
    [SerializeField] protected float scaleMultiplier = 1.0f; // 缩放倍率
    [SerializeField] protected float statMultiplier = 1.0f; // 属性加成倍率
    [SerializeField] protected float moveSpeedMultiplier = 1.0f; // 移动速度加成倍率
    [SerializeField] protected float attackRangeMultiplier = 1.0f; // 攻击范围加成倍率
    [SerializeField] protected float attackDistanceMultiplier = 1.0f; // 攻击追击范围加成倍率
    [SerializeField] protected float detectionRangeMultiplier = 1.0f; // 索敌范围加成倍率

    public virtual void Initialize(Enemy targetEnemy) // 初始化类型系统
    {
        enemy = targetEnemy;
        ApplyTypeBonus();
    }

    protected abstract void ApplyTypeBonus(); // 应用类型加成

    protected Entity_Stats GetEnemyStats() => enemy?.stats; // 获取敌人属性组件

    protected void ApplyStatMultiplier(Stat stat, float multiplier) // 应用属性倍率加成
    {
        if (stat != null && multiplier > 0) stat.ApplyMultiplier(multiplier);
    }

    protected void ApplyScaleMultiplier(float multiplier) // 应用物体缩放
    {
        if (enemy == null || multiplier <= 0) return;
        enemy.transform.localScale *= multiplier;
    }

    protected void ApplyMoveSpeedMultiplier(float multiplier) // 应用移动速度加成
    {
        if (enemy == null || multiplier <= 0) return;
        enemy.moveSpeed *= multiplier;
        enemy.battleMoveSpeed *= multiplier;
    }

    protected void ApplyAttackRangeMultiplier(float multiplier) // 应用攻击范围加成
    {
        if (enemy == null || multiplier <= 0) return;
        Entity_Combat combat = enemy.GetComponent<Entity_Combat>();
        if (combat != null) combat.targetCheckRadius *= multiplier;
    }

    protected void ApplyAttackDistanceMultiplier(float multiplier) // 应用攻击追击范围加成
    {
        if (enemy == null || multiplier <= 0) return;
        enemy.attackDistance *= multiplier;
    }

    protected void ApplyDetectionRangeMultiplier(float multiplier) // 应用索敌范围加成
    {
        if (enemy == null || multiplier <= 0) return;
        enemy.checkPlayer_Distance *= multiplier;
    }

    protected void ApplyBaseStatBonus(float multiplier) // 应用基础属性加成
    {
        Entity_Stats enemyStats = GetEnemyStats();
        if (enemyStats == null) return;

        // 基础属性
        ApplyStatMultiplier(enemyStats.resources.maxHP, multiplier);
        ApplyStatMultiplier(enemyStats.defense.armor, multiplier);
        ApplyStatMultiplier(enemyStats.offense.phyiscalDamage, multiplier);
        ApplyStatMultiplier(enemyStats.offense.critPower, multiplier);
        ApplyStatMultiplier(enemyStats.offense.armorReduction, multiplier);
        
        // 元素属性
        ApplyStatMultiplier(enemyStats.offense.elementalHeart, multiplier);
        ApplyStatMultiplier(enemyStats.offense.fireDamage, multiplier);
        ApplyStatMultiplier(enemyStats.offense.iceDamage, multiplier);
        ApplyStatMultiplier(enemyStats.offense.lightningDamage, multiplier);
        
        // 应用属性加成后同步血量
        SyncHealthAfterBonus();
    }

    protected void ApplySpecialStatBonus(float critChanceMultiplier, float resistanceMultiplier) // 应用特殊属性加成
    {
        Entity_Stats enemyStats = GetEnemyStats();
        if (enemyStats == null) return;
        // 特殊属性
        ApplyStatMultiplier(enemyStats.offense.critChance, critChanceMultiplier);
        ApplyStatMultiplier(enemyStats.defense.fireRes, resistanceMultiplier);
        ApplyStatMultiplier(enemyStats.defense.iceRes, resistanceMultiplier);
        ApplyStatMultiplier(enemyStats.defense.lightningRes, resistanceMultiplier);
    }

    protected void SyncHealthAfterBonus() // 应用加成后同步血量
    {
        if (enemy == null) return;
        Entity_Health entityHealth = enemy.GetComponent<Entity_Health>();
        entityHealth?.InitializeHealth();
    }

    protected virtual void ApplyAllBonuses() // 应用所有加成
    {
        ApplyScaleMultiplier(scaleMultiplier);// 应用物体缩放倍率
        ApplyBaseStatBonus(statMultiplier);// 应用基础属性倍率加成
        ApplyMoveSpeedMultiplier(moveSpeedMultiplier);// 应用移动速度倍率加成
        ApplyAttackRangeMultiplier(attackRangeMultiplier);// 应用攻击范围倍率加成
        ApplyAttackDistanceMultiplier(attackDistanceMultiplier);// 应用攻击追击范围倍率加成
        ApplyDetectionRangeMultiplier(detectionRangeMultiplier);// 应用索敌范围倍率加成 
    }
}