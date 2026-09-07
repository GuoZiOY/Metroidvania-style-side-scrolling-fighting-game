using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_SkillManager : MonoBehaviour
{
    public static Player_SkillManager Instance { get; private set; }

    public Skill_Dash dash {  get; private set; }
    public Skill_DashBlur dashBlur { get; private set; }
    public Skill_PowerCounterChase powerCounterChase { get; private set; }
    public Skill_Shard shard { get; private set; }
    public Skill_TimeEcho timeEcho { get; private set; }
    public Skill_Fire fire{ get; private set; }
    public Skill_Ice ice{ get; private set; }
    public Skill_Lighting lighting{ get; private set; }
    public Skill_DomainExpansion domainExpansion{ get; private set; }
    public Skill_DoubleJump doubleJump { get; private set; }
    public Skill_ElementalMastery elementalMastery { get; private set; }
    public Skill_BagExpand bagExpand { get; private set; }



    public Skill_Base[] allSkills;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        dash = GetComponentInChildren<Skill_Dash>();
        dashBlur = GetComponentInChildren<Skill_DashBlur>();
        shard = GetComponentInChildren<Skill_Shard>();
        timeEcho = GetComponentInChildren<Skill_TimeEcho>();
        fire = GetComponentInChildren<Skill_Fire>();
        ice = GetComponentInChildren<Skill_Ice>();
        lighting = GetComponentInChildren<Skill_Lighting>();
        domainExpansion = GetComponentInChildren<Skill_DomainExpansion>();
        doubleJump = GetComponentInChildren<Skill_DoubleJump>();
        elementalMastery = GetComponentInChildren<Skill_ElementalMastery>();
        bagExpand = GetComponentInChildren<Skill_BagExpand>();
        powerCounterChase = GetComponentInChildren<Skill_PowerCounterChase>();

        allSkills = GetComponentsInChildren<Skill_Base>();
    }


    public void ReduceAllSkillCooldownBy(float amount)//减少所有技能冷却时间
    {
        foreach (var skill in allSkills)
            skill.ReduceCooldown(amount);
    }


    public Skill_Base GetSkillByType(SkillType type)//获取技能 
    {
        switch(type)
        {
            case SkillType.Dash:return dash;
            case SkillType.DashBlur:return dashBlur;
            case SkillType.TimeShard:return shard;
            case SkillType.TimeEcho:return timeEcho;
            case SkillType.Fire:return fire;
            case SkillType.Ice:return ice;
            case SkillType.Lighting:return lighting;
            case SkillType.DomainExpansion:return domainExpansion;
            case SkillType.DoubleJump:return doubleJump;
            case SkillType.ElementalMastery:return elementalMastery;
            case SkillType.PowerCounterChase:return powerCounterChase;
            case SkillType.BagExpand:return bagExpand;

            default:
                Debug.Log($"获取技能- {type} -没有");
                return null;
        }
    }
}
