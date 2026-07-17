using UnityEngine;

public class Skill_ElementalMastery : Skill_Base
{
    [Header("元素精通配置")]
    [SerializeField] private float baseMasteryBonusPerLevel = 0.1f;

    protected override void Awake()
    {
        base.Awake();
    }

    //获取元素精通因子
    public float GetElementalMasteryFactor()
    {
        if (!Unlocked(SkillUpgradeType.ElementalMastery))
            return 0f;

        return currentLevel * baseMasteryBonusPerLevel;
    }

    //检查是否解锁元素精通
    public bool HasElementalMastery()
    {
        return Unlocked(SkillUpgradeType.ElementalMastery);
    }
}
