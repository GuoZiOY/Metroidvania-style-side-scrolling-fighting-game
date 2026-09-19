using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_TimeEcho : Skill_Base
{

    [SerializeField] private GameObject timeEchoPrefab;
    [SerializeField] private float timeEchoDuration;

    [Header("攻击升级")]
    [SerializeField] private int maxAttacks = 3;
    [SerializeField] private float duplicateChance = .3f;

    [Header("恢复升级")]
    [SerializeField] private float damagePercentHealed = .3f;//恢复比列
    [SerializeField] private float cooldownReducedInSeconds;


    public float GetPercentOfDamageHealed()//恢复的是克隆体收到的伤害比列
    {
        if (ShouldBeWisp() == false)
            return 0;

        return damagePercentHealed;
    }

    public float GetCooldownReduceInSeconds()//减少CD
    {
        if (upgradeType != SkillUpgradeType.TimeEcho_CooldownWisp)
            return 0;

        return cooldownReducedInSeconds;
    }

    public bool CanRemoveNegativeEffects()//净化，移除身上所有的负面buff
    {
        return upgradeType == SkillUpgradeType.TimeEcho_CleanseWisp;
    }

    public bool ShouldBeWisp()//允许幽灵
    {
        
        return upgradeType == SkillUpgradeType.TimeEcho_HealWisp//恢复
            || upgradeType == SkillUpgradeType.TimeEcho_CleanseWisp//净化
            || upgradeType == SkillUpgradeType.TimeEcho_CooldownWisp;//减少CD
    }

    public float GetDuplicateChance()
    {
        if (upgradeType != SkillUpgradeType.TimeEcho_ChanceToDuplicate)
            return 0;

        return duplicateChance;
    }

    public int GetMaxAttacks()
    {
        if (upgradeType == SkillUpgradeType.TimeEcho_SingleAttack || upgradeType == SkillUpgradeType.TimeEcho_ChanceToDuplicate)
            return 1;

        if (upgradeType == SkillUpgradeType.TimeEcho_MultiAttack)
            return maxAttacks;

        return 0;
    }

    public float GetEchoDuration()
    {
        return timeEchoDuration;
    }

    public override void TryUseSkill()
    {
        if (CanUseSkill() == false)
            return;
        CreateTimeEcho();
        StartSkillCooldown();

    }


    public void CreateTimeEcho(Vector3? targetPosition = null)
    {
        Vector3 position = targetPosition ?? transform.position;

        GameObject timeEcho = Instantiate(timeEchoPrefab, position, Quaternion.identity);
        timeEcho.GetComponent<SkillObject_TimeEcho>().SetupEcho(this);
    }
}
