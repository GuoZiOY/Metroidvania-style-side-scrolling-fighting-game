using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ItemToolTip : UI_ToolTip
{
    [SerializeField] private TextMeshProUGUI itemName;//物品名称文本
    [SerializeField] private TextMeshProUGUI itemType;//物品类型文本
    [SerializeField] private TextMeshProUGUI itemInfo;//物品数量文本

    public void ShowToolTip(bool show, RectTransform targetRect, Inventory_Item itemToShow)
    {
        base.ShowToolTip(show, targetRect);
        if (!show || itemToShow == null) return; // 如果不显示或物品为空，直接返回

        // 名称：前缀词缀 + 物品名 + 后缀词缀（词缀名富文本按 Tier 着色）
        itemName.text = BuildNameWithAffixes(itemToShow);
        itemName.color = GetRarityColorForName(itemToShow);

        // 类型：稀有度标签 + 装备类型，如 "[稀有] 武器"
        itemType.text = BuildTypeText(itemToShow);

        itemInfo.text = GetItemInfo(itemToShow);//设置物品信息

        // 强制重新计算布局以获取正确的尺寸
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        base.ShowToolTip(show, targetRect);
    }

    // 名称 = 前缀 + 物品名 + 后缀，词缀富文本着色
    private string BuildNameWithAffixes(Inventory_Item item)
    {
        string itemDisplayName = item.itemData.itemName;
        if (item.affixes == null || item.affixes.Length == 0)
            return itemDisplayName;

        // 拼接前缀（物品名前）和后缀（物品名后）
        StringBuilder sb = new StringBuilder();
        foreach (var affix in item.affixes)
        {
            if (affix != null && affix.isPrefix)
                sb.Append(RichAffixName(affix) + " ");
        }
        sb.Append(itemDisplayName);
        foreach (var affix in item.affixes)
        {
            if (affix != null && !affix.isPrefix)
                sb.Append(" " + RichAffixName(affix));
        }
        return sb.ToString();
    }

    // 词缀名富文本：英文方括号 [名字] 按 Tier 着色（负面不整体灰，只有数值段灰）
    private string RichAffixName(GeneratedEquipmentAffix affix)
    {
        string hex = ColorUtility.ToHtmlStringRGB(GetAffixTierColor(affix.tier));
        return $"<color=#{hex}>[{affix.displayName}]</color>";
    }

    // 物品名颜色：有稀有度用稀有度色，否则白色
    private Color GetRarityColorForName(Inventory_Item item)
    {
        if (item.actualRarity.HasValue)
            return RarityCalculator.GetRarityColor(item.actualRarity.Value);
        return Color.white;
    }

    // 类型文本："[稀有] 武器"
    private string BuildTypeText(Inventory_Item item)
    {
        string typeName = item.itemData.itemType.ToString();
        if (item.actualRarity.HasValue)
        {
            string rarityName = RarityCalculator.GetRarityName(item.actualRarity.Value);
            return $"[{rarityName}] {typeName}";
        }
        return typeName;
    }

    public string GetItemInfo(Inventory_Item item)
    {
        if (item.itemData.itemType == ItemType.材料)
            return "材料，仅用于制作";

        if (item.IsConsumable)
        {
            return GetConsumableInfo(item);
        }

        StringBuilder sb = new StringBuilder();

        // 基础属性（底材自带，应用稀有度倍率）
        if (item.baseModifiers != null)
        {
            foreach (var mod in item.baseModifiers)
            {
                sb.AppendLine(FormatSignedModValue(mod) + " " + GetStatNameByType(mod.statType));
            }
        }

        // 词缀效果（紧随基础属性之后）：词缀名按 Tier 着色，只有负面数值段用灰色
        if (item.affixes != null && item.affixes.Length > 0)
        {
            foreach (var affix in item.affixes)
            {
                if (affix == null || affix.modifiers == null)
                    continue;

                string nameHex = ColorUtility.ToHtmlStringRGB(GetAffixTierColor(affix.tier));
                sb.Append($"<color=#{nameHex}>[{affix.displayName}]</color>");
                foreach (var mod in affix.modifiers)
                {
                    // 只有负值（负面效果）用灰色，正值保持词缀 Tier 色
                    string modHex = mod.value < 0 ? ColorUtility.ToHtmlStringRGB(Color.gray) : nameHex;
                    sb.Append($"  <color=#{modHex}>{FormatSignedModValue(mod)} {GetStatNameByType(mod.statType)}</color>");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    // 带正负号格式化：正值 +X，负值 -X（百分比存小数，显示时乘回 100）
    private string FormatSignedModValue(ItemModifier mod)
    {
        bool negative = mod.value < 0;
        float abs = Mathf.Abs(mod.value);
        string number = FormatFloatValue(mod.isPercentage ? abs * 100f : abs);
        string suffix = mod.isPercentage ? "%" : "";
        return (negative ? "-" : "+") + number + suffix;
    }

    // 格式化修饰符值：百分比存的是小数（0.16=16%），显示时乘回 100 再加 %
    private string FormatModValue(ItemModifier mod)
    {
        if (mod.isPercentage)
            return FormatFloatValue(mod.value * 100f) + "%";
        return FormatFloatValue(mod.value);
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

    // 装备词缀等级 → 颜色（5 级，对应稀有度配色）
    private Color GetAffixTierColor(AffixTier tier)
    {
        return tier switch
        {
            AffixTier.Common => Color.white,
            AffixTier.Fine => Color.green,
            AffixTier.Rare => Color.blue,
            AffixTier.Epic => new Color(0.5f, 0f, 0.5f),   // 紫色
            AffixTier.Legendary => Color.red,
            _ => Color.white
        };
    }
}
