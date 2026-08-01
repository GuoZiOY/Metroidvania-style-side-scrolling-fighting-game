using UnityEngine;

public class EnemyLevelSystem : MonoBehaviour
{
    [Header("属性成长")]
    [SerializeField] private float statGrowthMultiplier = 0.1f; // 属性成长倍率（每级增加10%）

    [Header("百分比属性成长")]
    [SerializeField] private float critChancePerLevel = 0.5f; // 暴击率每级增加0.5%
    [SerializeField] private float critPowerPerLevel = 1f; // 暴击伤害每级增加1%
    [SerializeField] private float elementalResPerLevel = 0.5f; // 元素抗性每级增加0.5%

    [SerializeField] private Entity_Stats stats; // 实体属性引用
    private Entity_Health entityHealth; // 实体生命值引用

    private void Start() // 初始化
    {
        Initialize();
    }

    public void Initialize() // 初始化方法（供外部调用）
    {
        if (stats == null)
        {
            stats = GetComponentInParent<Entity_Stats>();
        }
        if (entityHealth == null)
        {
            entityHealth = GetComponentInParent<Entity_Health>();
        }
    }

    public void ApplyLevelBonus(int level) // 应用等级加成
    {
        Initialize(); // 确保已初始化

        if (stats == null)
        {
            Debug.LogError("无法应用等级加成：stats 未初始化");
            return;
        }

        if (level < 1)
        {
            Debug.LogWarning($"敌人等级不能小于1，当前值：{level}");
            return;
        }

        // 注意：默认值重置已移至 Enemy.InitializeEnemy() 中统一处理，
        // 避免覆盖类型系统 (EnemyTypeSystem) 已应用的属性加成
        int levelBonus = level - 1; // 等级加成（1级为0，2级为1，以此类推）
        float totalMultiplier = 1f + levelBonus * statGrowthMultiplier; // 总倍率

        ApplyMultiplierStats(totalMultiplier); // 应用倍率成长
        ApplyAdditiveStats(levelBonus); // 应用加法成长
        SyncCurrentHPToMaxHP(); // 同步当前血量到最大血量
    }

    private void ApplyMultiplierStats(float multiplier) // 应用倍率成长的属性
    {
        stats.resources.maxHP.ApplyMultiplier(multiplier);

        stats.offense.phyiscalDamage.ApplyMultiplier(multiplier);
        stats.offense.armorReduction.ApplyMultiplier(multiplier);

        stats.offense.elementalHeart.ApplyMultiplier(multiplier);
        stats.offense.fireDamage.ApplyMultiplier(multiplier);
        stats.offense.iceDamage.ApplyMultiplier(multiplier);
        stats.offense.lightningDamage.ApplyMultiplier(multiplier);

        stats.defense.armor.ApplyMultiplier(multiplier);
    }

    private void ApplyAdditiveStats(int levelBonus) // 应用加法成长的属性
    {
        stats.offense.critChance.AddBaseValue(levelBonus * critChancePerLevel);
        stats.offense.critPower.AddBaseValue(levelBonus * critPowerPerLevel);

        stats.defense.fireRes.AddBaseValue(levelBonus * elementalResPerLevel);
        stats.defense.iceRes.AddBaseValue(levelBonus * elementalResPerLevel);
        stats.defense.lightningRes.AddBaseValue(levelBonus * elementalResPerLevel);
    }

    private void SyncCurrentHPToMaxHP() // 同步当前血量到最大血量
    {
        if (entityHealth != null)
        {
            entityHealth.InitializeHealth();
        }
    }
}
