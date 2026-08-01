#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 装备词缀 CSV 导入器 — 从 Resources/CSV/EquipmentAffixes.csv 读取词缀数据生成 EquipmentAffixDatabase
// 策划工作流：编辑 xlsx → sync_xlsx_to_csv.ps1 → 本菜单导入 → SO 更新
// 菜单: Tools/破碎之城/导入词缀CSV
public static class EquipmentAffixCSVImporter
{
    private const string CsvPath = "Assets/Resources/CSV/EquipmentAffixes.csv";
    private const string OutputPath = "Assets/Resources/Data/EquipmentAffixDatabase.asset";

    [MenuItem("Tools/破碎之城/导入词缀CSV")]
    public static void Import()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"[EquipmentAffixCSVImporter] 找不到词缀CSV: {CsvPath}");
            return;
        }

        string[] lines = File.ReadAllLines(CsvPath);
        var prefixes = new List<EquipmentAffixEntry>();
        var suffixes = new List<EquipmentAffixEntry>();

        for (int i = 1; i < lines.Length; i++) // 跳过表头
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue; // 跳过空行和注释

            var fields = line.Split(',');
            if (fields.Length < 5)
                continue;

            var entry = ParseEntry(fields);
            if (entry == null)
                continue;

            // 按 affixId 前缀区分前缀/后缀
            if (entry.affixId.StartsWith("prefix_"))
                prefixes.Add(entry);
            else if (entry.affixId.StartsWith("suffix_"))
                suffixes.Add(entry);
            else
                Debug.LogWarning($"[EquipmentAffixCSVImporter] 第{i + 1}行 affixId 未以 prefix_/suffix_ 开头，跳过: {entry.affixId}");
        }

        // 生成数据库
        var db = ScriptableObject.CreateInstance<EquipmentAffixDatabase>();
        db.prefixes = prefixes.ToArray();
        db.suffixes = suffixes.ToArray();

        if (File.Exists(OutputPath))
            AssetDatabase.DeleteAsset(OutputPath);

        AssetDatabase.CreateAsset(db, OutputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EquipmentAffixCSVImporter] 词缀数据库已更新: 前缀 {db.prefixes.Length} 个，后缀 {db.suffixes.Length} 个");
    }

    // CSV 行 → EquipmentAffixEntry（中文映射）
    // 分列格式（对齐 Equipment.csv 策划风格，固定 4 组×4 列）:
    //   affixId,displayName,tier,pool,weight,
    //   stat1,stat1_min,stat1_max,stat1_pct, stat2,..., stat4
    // 空组（属性列为空）跳过；负值即负面效果（双刃/垃圾词缀）
    private static EquipmentAffixEntry ParseEntry(string[] f)
    {
        var entry = new EquipmentAffixEntry
        {
            affixId = f[0].Trim(),
            displayName = f[1].Trim(),
            tier = ParseTier(f[2].Trim()),
            pool = ParsePool(f[3].Trim()),
            weight = f.Length > 4 && float.TryParse(f[4].Trim(), out float w) ? Mathf.Max(1, Mathf.RoundToInt(w)) : 5
        };

        // 从第 6 列起，每 4 列为一组效果段（属性/min/max/pct）
        var statMods = new List<AffixStatModifier>();
        for (int i = 5; i + 3 < f.Length; i += 4)
        {
            string statName = f[i].Trim();
            if (statName.Length == 0)
                continue; // 空组，跳过

            var st = ParseStat(statName);
            if (!st.HasValue)
            {
                Debug.LogWarning($"[EquipmentAffixCSVImporter] 词缀 {entry.affixId} 属性名无法识别: {statName}");
                continue;
            }
            if (!float.TryParse(f[i + 1].Trim(), out float min) || !float.TryParse(f[i + 2].Trim(), out float max))
            {
                Debug.LogWarning($"[EquipmentAffixCSVImporter] 词缀 {entry.affixId} 数值格式错误: {statName}");
                continue;
            }
            if (min > max)
            {
                Debug.LogWarning($"[EquipmentAffixCSVImporter] 词缀 {entry.affixId} min>max，交换: {statName}");
                (min, max) = (max, min);
            }

            statMods.Add(new AffixStatModifier
            {
                statType = st.Value,
                minValue = min,
                maxValue = max,
                isPercentage = f[i + 3].Trim().ToLowerInvariant() == "true" // pct 列: true=百分比 / false=固定值
            });
        }

        if (statMods.Count == 0)
        {
            Debug.LogWarning($"[EquipmentAffixCSVImporter] 词缀 {entry.affixId} 无有效属性段，跳过");
            return null;
        }

        entry.statModifiers = statMods.ToArray();
        return entry;
    }

    // 中文稀有度 → AffixTier
    private static AffixTier ParseTier(string s)
    {
        return s switch
        {
            "普通" => AffixTier.Common,
            "优秀" => AffixTier.Fine,
            "稀有" => AffixTier.Rare,
            "史诗" => AffixTier.Epic,
            "传说" => AffixTier.Legendary,
            _ => AffixTier.Common
        };
    }

    // 中文池 → EquipmentAffixPool
    private static EquipmentAffixPool ParsePool(string s)
    {
        return s switch
        {
            "武器" => EquipmentAffixPool.Weapon,
            "防具" => EquipmentAffixPool.Armor,
            "饰品" => EquipmentAffixPool.Accessory,
            _ => EquipmentAffixPool.All
        };
    }

    // 中文属性名 → StatType
    private static StatType? ParseStat(string s)
    {
        return s switch
        {
            "最大生命值" => StatType.MaxHP,
            "生命值恢复" => StatType.HealthRegen,
            "力量" => StatType.Strength,
            "敏捷" => StatType.Agility,
            "智力" => StatType.Intelligence,
            "活力" => StatType.Vitality,
            "攻击速度" => StatType.AttackSpeed,
            "物理伤害" => StatType.PhyiscalDamage,
            "暴击率" => StatType.CritChance,
            "暴击伤害" => StatType.CritPower,
            "护甲穿透" => StatType.ArmorReduction,
            "元素之心" => StatType.ElementalHeart,
            "火焰伤害" => StatType.FireDamage,
            "冰霜伤害" => StatType.IceDamage,
            "闪电伤害" => StatType.LightningDamage,
            "护甲" => StatType.Armor,
            "闪避" => StatType.Evasion,
            "冰霜抗性" => StatType.IceResistance,
            "火焰抗性" => StatType.FireResistance,
            "闪电抗性" => StatType.LightningResistance,
            _ => null // 未知属性名
        };
    }
}
#endif
