using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class UI_StatToolTip : UI_ToolTip
{

    private TextMeshProUGUI statToolTipText;
    private Player player;

    protected override void Awake()
    {
        base.Awake();
        player = FindAnyObjectByType<Player>();
        statToolTipText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void ShowToolTip(bool show, RectTransform targetRect, StatType statType)
    {
        base.ShowToolTip(show, targetRect);
        if (!show) return; // 如果不显示，直接返回，避免访问空引用
        statToolTipText.text = SetStatText(statType);
    }

    private string SetStatText(StatType type)//获取属性描述文本
    {
        switch (type)
        {
            case StatType.MaxHP: return "角色的最大生命值";
            case StatType.HealthRegen: return $"每秒恢复N点生命值";

            case StatType.Strength: return "每点增加0.5点物理伤害、1%暴击伤害";
            case StatType.Agility: return "每点增加0.5%暴击率、0.5点闪避率";
            case StatType.Intelligence: return "每点增加0.25元素基础伤害,0.5点元素减免";
            case StatType.Vitality: return $"每点增加3点最大生命值和0.5点护甲值";

            case StatType.AttackSpeed: return "决定你的攻击速度，数值越高频率越快";
            case StatType.PhyiscalDamage: return "决定你的基础物理伤害";
            case StatType.CritChance: return "有N%的概率触发暴击";
            case StatType.CritPower: return "如果暴击,总伤害倍率乘以N%";   
            case StatType.ArmorReduction: return "减少敌人护甲减免效果";

            case StatType.ElementalHeart: return "大幅提升角色各项属性、特别是元素伤害";
            case StatType.FireDamage: return "影响火焰技能的伤害输出";
            case StatType.IceDamage: return "影响冰霜技能的伤害输出";
            case StatType.LightningDamage: return "影响闪电技能的伤害输出";

            case StatType.Armor: 
            {
                float armorMitigation = player != null && player.stats != null ? player.stats.GetArmorMitigation(0) : 0;
                return $"减少受到的物理伤害，当前伤害减免:{(armorMitigation * 100):F1}%，上限:75%";
            }
            case StatType.Evasion: return "有概率完全避免伤害，闪避率上限:75%";   

            case StatType.FireResistance: return "减少受到的火焰伤害，元素抗性减免上限:75%";
            case StatType.IceResistance: return "减少受到的冰霜伤害，元素抗性减免上限:75%";     
            case StatType.LightningResistance: return "减少受到的闪电伤害，元素抗性减免上限:75%"; 

            default:
                return type.ToString();
        }
    }
}
