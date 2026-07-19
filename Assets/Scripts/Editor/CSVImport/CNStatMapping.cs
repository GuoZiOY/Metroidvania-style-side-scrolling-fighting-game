using System.Collections.Generic;

/// <summary>
/// StatType 中英文双向映射（与 UI_StatSlot.cs 保持一致）
/// </summary>
public static class CNStatMapping
{
    private static readonly Dictionary<string, string> EnToCn = new Dictionary<string, string>
    {
        ["MaxHP"] = "最大生命值",
        ["HealthRegen"] = "生命值恢复",
        ["Strength"] = "力量",
        ["Agility"] = "敏捷",
        ["Intelligence"] = "智力",
        ["Vitality"] = "活力",
        ["AttackSpeed"] = "攻击速度",
        ["CritChance"] = "暴击率",
        ["CritPower"] = "暴击伤害",
        ["ArmorReduction"] = "护甲穿透",
        ["ElementalHeart"] = "元素之心",
        ["PhyiscalDamage"] = "物理伤害",
        ["FireDamage"] = "火焰伤害",
        ["IceDamage"] = "冰霜伤害",
        ["LightningDamage"] = "闪电伤害",
        ["Evasion"] = "闪避",
        ["Armor"] = "护甲",
        ["IceResistance"] = "冰霜抗性",
        ["FireResistance"] = "火焰抗性",
        ["LightningResistance"] = "闪电抗性",
    };

    private static readonly Dictionary<string, string> CnToEn;

    static CNStatMapping()
    {
        CnToEn = new Dictionary<string, string>();
        foreach (var kv in EnToCn)
            CnToEn[kv.Value] = kv.Key;
    }

    /// <summary>英文 → 中文</summary>
    public static string ToChinese(string englishStatName)
    {
        return EnToCn.TryGetValue(englishStatName, out string cn) ? cn : englishStatName;
    }

    /// <summary>中文 → 英文</summary>
    public static string ToEnglish(string chineseStatName)
    {
        return CnToEn.TryGetValue(chineseStatName, out string en) ? en : chineseStatName;
    }

    /// <summary>中文 → StatType 枚举</summary>
    public static bool TryParseStatType(string chinese, out StatType statType)
    {
        string english = ToEnglish(chinese);
        if (!string.IsNullOrEmpty(english) && System.Enum.TryParse(english, true, out statType))
            return true;
        return System.Enum.TryParse(chinese, true, out statType);
    }
}
