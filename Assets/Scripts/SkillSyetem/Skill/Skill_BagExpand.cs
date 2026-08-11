using UnityEngine;

// 被动技能：背包扩容 —— 每升一级给角色背包槽 +1（背包上限 = 基础15 + 被动等级）
public class Skill_BagExpand : Skill_Base
{
    [Header("背包扩容配置")]
    [SerializeField] private int bonusPerLevel = 1; // 每级增加的背包槽数

    protected override void Awake()
    {
        base.Awake();
    }

    // 每级增加的背包槽数（供容量计算/UI 显示）
    public int BonusPerLevel => bonusPerLevel;

    // 当前总背包槽加成 = 被动等级 × 每级加成。
    // 等级以 SkillDataManager 缓存为准（UpdateSkillData 在事件触发前已同步写入，
    // 避免 OnPassiveSkillUpdated 订阅顺序导致读到旧等级）
    public int GetBagSlotBonus()
    {
        int level = SkillDataManager.Instance != null
            ? SkillDataManager.Instance.GetCurrentLevel(SkillUpgradeType.BagExpand)
            : currentLevel;
        return level * bonusPerLevel;
    }
}
