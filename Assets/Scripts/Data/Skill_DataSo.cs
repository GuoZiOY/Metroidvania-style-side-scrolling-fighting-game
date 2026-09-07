using System;
using UnityEngine;

public enum SkillUsageType
{
    Active, // 主动技能 - 需要装载到技能槽
    Passive // 被动技能 - 解锁即生效，不需要装载到技能槽
}

[CreateAssetMenu(menuName = "RPG技能/技能数据", fileName = "技能数据")]
public class Skill_DataSo : ScriptableObject
{
    [Header("技能基础信息")]
    public string displayName; // 技能显示名称
    [TextArea]
    public string description; // 技能描述
    public Sprite icon; // 技能图标

    [Header("解锁&升阶核心配置")]
    public bool isUnlocked;
    public SkillType skillType; // 技能基础类型（如Dash/Attack）
    public SkillUpgradeType upgradeType; // 技能升阶标识（绑定技能本身，用于升阶判定）
    public SkillUpgradeType[] conflictSkillTypes;// 冲突技能类型列表
    public SkillUsageType usageType = SkillUsageType.Active; // 技能使用类型（主动/被动）


    [Header("前置等级要求（技能树解锁）")]
    public PreSkillRequirement[] preSkillRequirements; // 多前置列表（支持多个、跨树）

    [Header("等级系统配置")]
    public int maxLevel = 10; // 技能最大等级
    public int currentLevel;// 存储当前等级（用于存档/读档）
    public LevelData[] levelDatas; // 每级数值配置（索引0=1级，索引1=2级...）

}

[Serializable]
public class LevelData
{
    public int levelUpCost; // 本级升级消耗（每升一级扣一次）
    public float cooldown; // 本级冷却时间
    public DamageScaleData damageScaleData; // 本级伤害数据
    [TextArea]
    public string levelDescription; // 本级升级描述（如"冷却-0.5s，伤害+10%"）
}

[System.Serializable]
public class PreSkillRequirement
{
    public SkillUpgradeType preSkillType; // 前置技能类型（跨技能树也能匹配）
    public int preSkillLevel; // 前置技能需要达到的等级
}
