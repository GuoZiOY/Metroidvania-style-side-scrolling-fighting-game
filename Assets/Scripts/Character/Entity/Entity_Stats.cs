using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Entity_Stats : MonoBehaviour
{
    public Stat_SetupSO defaultStatSetup;

    public Stat_ResourceGroup resources;
    public Stat_OffenseGroup offense;
    public Stat_DefenseGroup defense;
    public Stat_MajorGroup major;

    private Player_SkillManager skillManager;//技能管理器引用

    public ElementType InputElement;

    private void Awake()
    {
        skillManager = GetComponent<Player_SkillManager>();
    }

    public AttackData GetAttackData(DamageScaleData scaleData)
    {
        return new AttackData(this, scaleData);
    }

    //元素之心值
    public float elementalHeartValue() => Mathf.Clamp(offense.elementalHeart.GetValue(), 0, 5);//获取元素之心值,最多获得5点元素之心    
    //基础物理伤害 = 物理伤害 + 力量值 * 0.5f
    public float GetBasePhyiscalDamage() => offense.phyiscalDamage.GetValue() + major.strength.GetValue() * 0.5f;
    //暴击率 = 基础暴击率 + 敏捷值 * 0.5f
    public float GetBaseCritChance() => offense.critChance.GetValue() + major.agility.GetValue() * 0.5f;
    //暴击伤害 = 基础暴击伤害 + 力量值 * 1f
    public float GetBaseCritPower() => offense.critPower.GetValue() + major.strength.GetValue() * 1f;
    //基础护甲值 = 护甲值 + 活力值 * 0.5f
    public float GetBaseArmor() => defense.armor.GetValue() + major.vitality.GetValue() * 0.5f;
    //护甲穿透 = 护甲穿透值 / 100
    public float GetArmorReduction() => offense.armorReduction.GetValue() / 100;

    public float GetMaxHP()//最大生命值 = 基础生命值 +  vitaltiy * 5
    {
        float baseHP = resources.maxHP.GetValue();
        float bonusHO = major.vitality.GetValue() * 5;//活力值加成 = 活力值 * 5
        float heartBonus = elementalHeartValue() * 50;//元素之心值加成 = 元素之心值 * 50    
        float finalHP = baseHP + bonusHO + heartBonus;
        return finalHP;
    }

    public bool CalculateCritStatus(float critChanceBonus = 0f)//计算暴击几率
    {
        float critChance = GetBaseCritChance() + critChanceBonus;
        return Random.Range(0, 100) < critChance;
    }

    public float GetCritPower() => GetBaseCritPower() / 100;//暴击伤害

    public float GetPhyiscalDamage(float scaleFactor = 1)
    {
        return GetPhyiscalDamage(scaleFactor, 1f);
    }

    public float GetPhyiscalDamage(float scaleFactor, float damageMultiplier) => GetBasePhyiscalDamage() * scaleFactor * damageMultiplier;


    public float GetElementalDamage(ElementType InputElement, out ElementType element, float scaleFactor = 1.0f)
    {
        element = ElementType.None;//默认无元素伤害

        float fireDamage = Mathf.Max(0, offense.fireDamage.GetValue());//获取火焰伤害数值
        float iceDamage = Mathf.Max(0, offense.iceDamage.GetValue());//获取冰霜伤害数值
        float lightningDamage = Mathf.Max(0, offense.lightningDamage.GetValue());//获取闪电伤害数值     

        var elementDamages = new Dictionary<ElementType, float>//元素伤害字典
        {
            { ElementType.Fire, fireDamage },
            { ElementType.Ice, iceDamage },
            { ElementType.Lightning, lightningDamage }
        };

        
        float elementalHeartBonus = 0.1f * elementalHeartValue();//元素之心加成 = 元素之心值 * 0.1f

        float intelligenceValue = Mathf.Max(0, major.intelligence.GetValue());//获取智力值
        float elementalBaseBonus = intelligenceValue * (0.25f + elementalHeartBonus);//元素伤害基础奖励 = 智力值 * (0.25f + 元素之心加成)     

        float finalMasteryFactor = GetElementalMasteryFactor();//从被动技能获取元素精通因子

        ElementType mainElement;//主要元素
        float mainElementDamage;//主要元素伤害

        if (InputElement != ElementType.None)//如果有输入元素
        {
            mainElement = InputElement;//主要元素 = 输入元素
            mainElementDamage = elementDamages[mainElement];//主要元素伤害 = 输入元素伤害
        }
        else//如果没有输入元素
        {
            var topElement = elementDamages.OrderByDescending(kv => kv.Value).First();//获取元素伤害字典中伤害最高的元素
            mainElement = topElement.Key;//主要元素 = 伤害最高的元素
            mainElementDamage = topElement.Value;//主要元素伤害 = 伤害最高的元素伤害    
        }

        if (mainElementDamage <= 0)//如果主要元素伤害小于等于0
        {
            element = ElementType.None;//则无元素伤害，函数保护措施
            return 0;
        }

        element = InputElement;//元素 = 输入元素类型
        float secondaryElementsDamage = 0;//次要元素伤害从0开始计算

        foreach (var (type, damage) in elementDamages)//遍历元素伤害字典
        {
            if (type != mainElement)//如果元素类型不是主要元素
            {
                secondaryElementsDamage += damage * (0.25f + finalMasteryFactor);
                //次要元素伤害 = 次要元素伤害 * (1 + 最终元素精通因子)
            }
        }

        float finalDamage = (mainElementDamage + secondaryElementsDamage + elementalBaseBonus) 
                            *(InputElement == ElementType.None ? (0.5f + elementalHeartBonus) : (1 + elementalHeartBonus))   
                            * scaleFactor;
        //最终元素伤害 = （主要元素伤害 + 次要元素伤害 + 元素伤害基础奖励） * (如果没有输入元素则乘以0.5 + 元素之心加成，否则乘以1 + 元素之心加成) * 缩放倍数
                            
                     
        return Mathf.Max(0, finalDamage);//返回最终元素伤害，保护措施，防止负数伤害
    }

    public float GetElementalResistance(ElementType element)//获取元素抗性
    {
        float baseResistance = 0;
        float bonusResistance = major.intelligence.GetValue() * 0.25f;

        switch (element)
        {
            case ElementType.Fire:
                baseResistance = defense.fireRes.GetValue();
                break;
            case ElementType.Ice:
                baseResistance = defense.iceRes.GetValue();
                break;
            case ElementType.Lightning:
                baseResistance = defense.lightningRes.GetValue();
                break;
        }

        float resistance = baseResistance + bonusResistance;
        float resistanceCap = 75f;
        float finalResistance = Mathf.Clamp(resistance, 0, resistanceCap) / 100;

        return finalResistance;
    }

    public float GetArmorMitigation(float armorReduction)//获取护甲减免
    {
        
        float reductionMutliplier = Mathf.Clamp(1 - armorReduction, 0, 1);//护甲减免系数 = 1 - 护甲穿透
        float effectiveArmor = GetBaseArmor() * reductionMutliplier;//有效护甲 = 护甲值 * 护甲减免系数   

        float mitigation = effectiveArmor / (effectiveArmor + 200);//护甲减免 = 有效护甲 / (有效护甲 + 200)，分母加大降低免伤强度
        float mitigationCap = 0.6f;//护甲减免上限（原 0.75 过高，下调）

        float finalMitigation = Mathf.Clamp(mitigation, 0, mitigationCap);

        return finalMitigation;//返回最终护甲减免
    }

    private float GetElementalMasteryFactor()//获取元素精通因子
    {
        if (skillManager == null || skillManager.elementalMastery == null)
            return 0f;

        return skillManager.elementalMastery.GetElementalMasteryFactor();
    }


    public float GetEvasion()//获取闪避
    {
        float baseEvasion = defense.evasion.GetValue();
        float bonuseEvasion = major.agility.GetValue() * 0.5f;

        float totalEvasion = baseEvasion + bonuseEvasion;
        float evasionCap = 75f;

        float finalEvasion = Mathf.Clamp(totalEvasion, 0, evasionCap);
        return finalEvasion;
    }

    public Stat GetStatByType(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHP: return resources.maxHP;
            case StatType.HealthRegen: return resources.healthRegen;

            case StatType.Strength: return major.strength;
            case StatType.Agility: return major.agility;
            case StatType.Intelligence: return major.intelligence;
            case StatType.Vitality: return major.vitality;

            case StatType.AttackSpeed: return offense.attackSpeed;
            case StatType.PhyiscalDamage: return offense.phyiscalDamage;
            case StatType.CritChance: return offense.critChance;
            case StatType.CritPower: return offense.critPower;
            case StatType.ArmorReduction: return offense.armorReduction;

            case StatType.ElementalHeart: return offense.elementalHeart;

            case StatType.FireDamage: return offense.fireDamage;
            case StatType.IceDamage: return offense.iceDamage;
            case StatType.LightningDamage: return offense.lightningDamage;

            case StatType.Armor: return defense.armor;
            case StatType.Evasion: return defense.evasion;

            case StatType.IceResistance: return defense.iceRes;
            case StatType.FireResistance: return defense.fireRes;
            case StatType.LightningResistance: return defense.lightningRes;

            default:
                Debug.LogWarning($"StatType {type} not implemented yet.");
                return null;
        }
    }

    [ContextMenu("应用默认属性配置")]
    public void ApplyDefaultStatSetup()
    {
        if (defaultStatSetup == null)
        {
            Debug.Log("No default stat setup assigned");
            return;
        }

        resources.maxHP.SetBaseValue(defaultStatSetup.maxHP);
        resources.healthRegen.SetBaseValue(defaultStatSetup.healthRegen);

        major.strength.SetBaseValue(defaultStatSetup.strength);
        major.agility.SetBaseValue(defaultStatSetup.agility);
        major.intelligence.SetBaseValue(defaultStatSetup.intelligence);
        major.vitality.SetBaseValue(defaultStatSetup.vitality);

        offense.attackSpeed.SetBaseValue(defaultStatSetup.attackSpeed);
        offense.phyiscalDamage.SetBaseValue(defaultStatSetup.damage);
        offense.critChance.SetBaseValue(defaultStatSetup.critChance);
        offense.critPower.SetBaseValue(defaultStatSetup.critPower);
        offense.armorReduction.SetBaseValue(defaultStatSetup.armorReduction);

        offense.elementalHeart.SetBaseValue(defaultStatSetup.elementalHeart);

        offense.iceDamage.SetBaseValue(defaultStatSetup.iceDamage);
        offense.fireDamage.SetBaseValue(defaultStatSetup.fireDamage);
        offense.lightningDamage.SetBaseValue(defaultStatSetup.lightningDamage);

        defense.armor.SetBaseValue(defaultStatSetup.armor);
        defense.evasion.SetBaseValue(defaultStatSetup.evasion);

        defense.iceRes.SetBaseValue(defaultStatSetup.iceResistance);
        defense.fireRes.SetBaseValue(defaultStatSetup.fireResistance);
        defense.lightningRes.SetBaseValue(defaultStatSetup.lightningResistance);

    }
}
