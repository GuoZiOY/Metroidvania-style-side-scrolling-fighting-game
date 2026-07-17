using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class UI_StatSlot : MonoBehaviour, IPointerClickHandler, IPointerExitHandler
{
    [SerializeField] private Player player;
    private RectTransform rect;
    private UI ui;

    [SerializeField] private StatType statType;
    [SerializeField] private TextMeshProUGUI statName;
    [SerializeField] private TextMeshProUGUI statValue;

    private bool isToolTipVisible;//提示框是否可见


    private void Awake()
    {
        ui = GetComponentInParent<UI>();
        rect = GetComponent<RectTransform>();
        player = FindFirstObjectByType<Player>();
    }

    private void OnValidate()
    {
        gameObject.name = "UI-属性名-" + GetStatNameByType(statType);
        if (statName != null)
            statName.text = GetStatNameByType(statType);
        
        if (player != null && player.stats != null)
            UpdateStateValue();
    }

    public void OnPointerClick(PointerEventData eventData)//点击事件处理
    {
        isToolTipVisible = !isToolTipVisible;
        ui.statToolTip.ShowToolTip(isToolTipVisible, rect, statType);
    }

    public void OnPointerExit(PointerEventData eventData)//鼠标离开事件处理
    {
        if (isToolTipVisible)
        {
            isToolTipVisible = false;
            ui.statToolTip.ShowToolTip(false, null);
        }
    }

    public void UpdateStateValue()
    {
        if (player == null || player.stats == null)
            return;

        Stat statToUpdate = player.stats.GetStatByType(statType);
        if (statToUpdate == null)
            return;

        float value = 0;

        switch (statType)
        {
            case StatType.Strength:
                value = player.stats.major.strength.GetValue();
                break;
            case StatType.Agility:
                value = player.stats.major.agility.GetValue();
                break;
            case StatType.Intelligence:
                value = player.stats.major.intelligence.GetValue();
                break;
            case StatType.Vitality:
                value = player.stats.major.vitality.GetValue();
                break;

            case StatType.MaxHP:
                value = player.stats.GetMaxHP();
                break;
            case StatType.HealthRegen:
                value = player.stats.resources.healthRegen.GetValue();
                break;

            case StatType.PhyiscalDamage:
                value = player.stats.GetBasePhyiscalDamage();
                break;
            case StatType.AttackSpeed:
                value = player.stats.offense.attackSpeed.GetValue() * 100;
                break;
            case StatType.CritChance:
                value = player.stats.GetBaseCritChance();
                break;
            case StatType.CritPower:
                value = player.stats.GetBaseCritPower();
                break;

            case StatType.FireDamage:
                value = player.stats.offense.fireDamage.GetValue();
                break;
            case StatType.IceDamage:
                value = player.stats.offense.iceDamage.GetValue();
                break;
            case StatType.LightningDamage:
                value = player.stats.offense.lightningDamage.GetValue();
                break;

            case StatType.ElementalHeart:
                value = player.stats.elementalHeartValue();
                break;

            case StatType.Armor:
                value = player.stats.GetBaseArmor();
                break;
            case StatType.ArmorReduction:
                value = player.stats.GetArmorReduction() * 100;
                break;
            case StatType.Evasion:
                value = player.stats.GetEvasion();
                break;

            case StatType.FireResistance:
                value = player.stats.GetElementalResistance(ElementType.Fire) * 100;
                break;
            case StatType.IceResistance:
                value = player.stats.GetElementalResistance(ElementType.Ice) * 100;
                break;
            case StatType.LightningResistance:
                value = player.stats.GetElementalResistance(ElementType.Lightning) * 100;
                break;
        }

        statValue.text = IsPercentageStat(statType) ? value + "%" : value.ToString();
    }

        private bool IsPercentageStat(StatType type)
    {
        switch (type)
        {
            case StatType.CritChance://暴击率
            case StatType.CritPower://暴击伤害
            case StatType.AttackSpeed://攻速
            case StatType.ArmorReduction://护甲穿透
            case StatType.Evasion://闪避率
            case StatType.IceResistance://冰霜抗性
            case StatType.FireResistance://火焰抗性
            case StatType.LightningResistance://闪电抗性
                return true;
            default:
                return false;
        }
    }
    


    private string GetStatNameByType(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHP: return "最大生命值";
            case StatType.HealthRegen: return "生命值恢复";

            case StatType.Strength: return "力量";
            case StatType.Agility: return "敏捷";
            case StatType.Intelligence: return "智力";
            case StatType.Vitality: return "活力";

            case StatType.AttackSpeed: return "攻击速度";
            case StatType.CritChance: return "暴击率";
            case StatType.CritPower: return "暴击伤害";
            case StatType.ArmorReduction: return "护甲穿透";

            case StatType.ElementalHeart: return "元素之心";

            case StatType.PhyiscalDamage: return "物理伤害";
            case StatType.FireDamage: return "火焰伤害";
            case StatType.IceDamage: return "冰霜伤害";
            case StatType.LightningDamage: return "闪电伤害";

            case StatType.Evasion: return "闪避";

            case StatType.Armor: return "护甲";
            case StatType.IceResistance: return "冰霜抗性";
            case StatType.FireResistance: return "火焰抗性";
            case StatType.LightningResistance: return "闪电抗性";

            default:
                return type.ToString();
        }
    }
}
