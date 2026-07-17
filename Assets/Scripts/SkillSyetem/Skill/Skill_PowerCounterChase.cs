using UnityEngine;

public class Skill_PowerCounterChase : Skill_Base
{
    [Header("反击强化配置")]
    [SerializeField] private float invincibilityBonusPerLevel = 0.05f;
    [SerializeField] private float baseStunChance = 0.25f;
    [SerializeField] private float stunChancePerLevel = 0.05f;
    [SerializeField] private float stunDurationBonusPerLevel = 0.1f;

    public float GetExtraInvincibilityDuration()
    {
        if (!Unlocked(SkillUpgradeType.PowerCounterChase))
            return 0f;
        return currentLevel * invincibilityBonusPerLevel;
    }

    public float GetStunChance()
    {
        if (!Unlocked(SkillUpgradeType.PowerCounterChase))
            return 0f;
        return baseStunChance + currentLevel * stunChancePerLevel;
    }

    public float GetExtraStunDuration()
    {
        if (!Unlocked(SkillUpgradeType.PowerCounterChase))
            return 0f;
        return currentLevel * stunDurationBonusPerLevel;
    }
}
