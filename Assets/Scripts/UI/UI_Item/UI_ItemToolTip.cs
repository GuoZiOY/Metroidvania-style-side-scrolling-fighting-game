using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ItemToolTip : UI_ToolTip
{
    
    [SerializeField]private TextMeshProUGUI itemName;//物品名称文本  
    [SerializeField]private TextMeshProUGUI itemType;//物品类型文本
    [SerializeField]private TextMeshProUGUI itemInfo;//物品数量文本


    public void ShowToolTip(bool show,RectTransform targetRect,Inventory_Item itemToShow)
    {
        base.ShowToolTip(show, targetRect);
        if (!show || itemToShow == null) return; // 如果不显示或物品为空，直接返回
        
        //设置物品名称和稀有度标签
        if (itemToShow.actualRarity.HasValue)
        {
            LootRarity rarity = itemToShow.actualRarity.Value;
            string rarityName = RarityCalculator.GetRarityName(rarity);
            itemName.text = $"[{rarityName}]{itemToShow.itemData.itemName}"; //在名称前添加稀有度标签
            itemName.color = RarityCalculator.GetRarityColor(rarity); //应用稀有度颜色
        }
        else
        {
            itemName.text = itemToShow.itemData.itemName;
            itemName.color = Color.white; //默认白色
        }
        
        itemType.text = itemToShow.itemData.itemType.ToString();//设置物品类型
        itemInfo.text = GetItemInfo(itemToShow);//设置物品信息
        
        // 强制重新计算布局以获取正确的尺寸
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        
        base.ShowToolTip(show,targetRect);
    }

    public string GetItemInfo(Inventory_Item item)
    {
        if(item.itemData.itemType == ItemType.材料)
            return "材料，仅用于制作";

        if (item.IsConsumable)
        {
            return GetConsumableInfo(item);
        }

        StringBuilder sb = new StringBuilder();

        foreach(var mod in item.Modifiers)
        {
            string modType = GetStatNameByType(mod.statType);
            string modValue;
            
            if (IsPercentageStat(mod.statType))
            {
                //百分比属性，整数显示整数，小数显示一位小数
                modValue = FormatFloatValue(mod.value) + "%";
            }
            else
            {
                //非百分比属性，整数显示整数，小数显示一位小数
                modValue = FormatFloatValue(mod.value);
            }
            
            sb.AppendLine(" + " + modValue + " " + modType); 
        }

        return sb.ToString();
    }

    private string FormatFloatValue(float value) //格式化浮点数值：整数显示整数，小数显示一位小数
    {
        //如果数值等于其整数部分，显示整数
        if (value == Mathf.FloorToInt(value))
        {
            return value.ToString("F0");
        }
        //否则显示一位小数
        return value.ToString("F1");
    }

    private string GetConsumableInfo(Inventory_Item item)//获取消耗品信息
    {
        ConsumableDataSo consumableData = item.GetConsumableData();
        if (consumableData == null)
            return "";

        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"类型: {consumableData.consumableType}");
        sb.AppendLine($"效果: {GetEffectTypeName(consumableData.effectType)}");

        if (consumableData.effectType == ConsumableEffectType.恢复生命)
        {
            sb.AppendLine($"恢复数量: {consumableData.effectValue}");
        }
        else if (consumableData.effectType == ConsumableEffectType.恢复生命百分比)
        {
            sb.AppendLine($"恢复百分比: {consumableData.effectValue}%");
        }
        else if (consumableData.effectType == ConsumableEffectType.属性增益)
        {
            sb.AppendLine($"增益属性: {GetStatNameByType(consumableData.buffStatType)}");
            sb.AppendLine($"增加数值: {consumableData.effectValue}");
            sb.AppendLine($"持续时间: {consumableData.effectDuration}秒");
        }
        else if (consumableData.effectType == ConsumableEffectType.复活)
        {
            sb.AppendLine($"恢复50%最大生命值");
        }

        if (consumableData.cooldownTime > 0)
        {
            sb.AppendLine($"冷却时间: {consumableData.cooldownTime}秒");
        }

        sb.AppendLine("\n右键点击使用");

        return sb.ToString();
    }

    private string GetEffectTypeName(ConsumableEffectType effectType)//获取效果类型名称
    {
        switch (effectType)
        {
            case ConsumableEffectType.恢复生命:
                return "恢复生命值";
            case ConsumableEffectType.恢复生命百分比:
                return "恢复生命值百分比";
            case ConsumableEffectType.属性增益:
                return "属性增益";
            case ConsumableEffectType.复活:
                return "复活";
            default:
                return effectType.ToString();
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
            case StatType.PhyiscalDamage: return "物理伤害";
            case StatType.CritChance: return "暴击率";
            case StatType.CritPower: return "暴击伤害";
            case StatType.ArmorReduction: return "护甲穿透";

            case StatType.ElementalHeart: return "元素之心";

            case StatType.FireDamage: return "火焰伤害";
            case StatType.IceDamage: return "冰霜伤害";
            case StatType.LightningDamage: return "闪电伤害";

            case StatType.Armor: return "护甲";
            case StatType.Evasion: return "闪避";

            case StatType.IceResistance: return "冰霜抗性";
            case StatType.FireResistance: return "火焰抗性";
            case StatType.LightningResistance: return "闪电抗性";

            default:
                return type.ToString();
        }
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
}
