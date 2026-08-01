using System.Collections.Generic;
using UnityEngine;

// 装备词缀生成器 — 从 EquipmentAffixDatabase 按池（武器/防具/饰品）加权随机生成前缀+后缀
// 掉落时调用，返回结构化词缀列表（含名称/前后缀/效果），存入 Inventory_Item
public static class EquipmentAffixGenerator
{
    private static EquipmentAffixDatabase cachedDatabase;

    // 全局词缀数据库：首次访问时从 Resources 惰性加载（为 null 则使用 V1 固定 Modifier 模式）
    public static EquipmentAffixDatabase Database
    {
        get
        {
            if (cachedDatabase == null)
                cachedDatabase = Resources.Load<EquipmentAffixDatabase>("Data/EquipmentAffixDatabase");
            return cachedDatabase;
        }
        set => cachedDatabase = value;
    }

    // 为指定装备类型和稀有度生成词缀（前缀+后缀）
    // 返回 null 表示无词缀（普通装备50%无词缀 / 数据库未配置）
    public static GeneratedEquipmentAffix[] GenerateAffixes(ItemType itemType, LootRarity rarity)
    {
        if (Database == null)
            return null;

        // 根据装备类型确定可用池
        EquipmentAffixPool targetPool = GetPool(itemType);

        // 计算前缀/后缀数量
        int maxTotal = GetMaxAffixCount(rarity);
        if (maxTotal <= 0)
            return null;

        // 前缀和后缀各取一半（向上取整给前缀）
        int prefixCount = Mathf.CeilToInt(maxTotal / 2f);
        int suffixCount = maxTotal - prefixCount;

        var affixes = new List<GeneratedEquipmentAffix>();

        // 随机前缀
        var validPrefixes = FilterByPool(Database.prefixes, targetPool);
        if (prefixCount > 0 && validPrefixes.Count > 0)
            AddWeightedAffixes(affixes, validPrefixes, prefixCount, rarity, true);

        // 随机后缀
        var validSuffixes = FilterByPool(Database.suffixes, targetPool);
        if (suffixCount > 0 && validSuffixes.Count > 0)
            AddWeightedAffixes(affixes, validSuffixes, suffixCount, rarity, false);

        return affixes.Count > 0 ? affixes.ToArray() : null;
    }

    // 合成/制作补足用：生成恰好 total 条词缀（重载）
    // guaranteed 为已确定的保底词缀（原样保留、不参与随机），缺口按目标稀有度的池随机补齐
    public static GeneratedEquipmentAffix[] GenerateAffixes(ItemType itemType, LootRarity rarity,
        GeneratedEquipmentAffix[] guaranteed, int total)
    {
        if (Database == null)
            return null;

        var result = new List<GeneratedEquipmentAffix>();
        if (guaranteed != null)
        {
            foreach (var g in guaranteed)
            {
                if (g != null)
                    result.Add(g);
            }
        }

        int needed = total - result.Count;
        if (needed <= 0)
            return result.ToArray();

        // 排除保底已用的词缀名（避免随机补足时出现完全重复词缀）
        var excluded = new HashSet<string>();
        if (guaranteed != null)
        {
            foreach (var g in guaranteed)
            {
                if (g != null && !string.IsNullOrEmpty(g.displayName))
                    excluded.Add(g.displayName);
            }
        }

        EquipmentAffixPool targetPool = GetPool(itemType);

        // 前缀/后缀各取一半（向上取整给前缀）
        int prefixCount = Mathf.CeilToInt(needed / 2f);
        int suffixCount = needed - prefixCount;

        var validPrefixes = FilterByPool(Database.prefixes, targetPool);
        var validSuffixes = FilterByPool(Database.suffixes, targetPool);
        validPrefixes.RemoveAll(e => e == null || excluded.Contains(e.displayName));
        validSuffixes.RemoveAll(e => e == null || excluded.Contains(e.displayName));

        if (prefixCount > 0 && validPrefixes.Count > 0)
            AddWeightedAffixes(result, validPrefixes, prefixCount, rarity, true);
        if (suffixCount > 0 && validSuffixes.Count > 0)
            AddWeightedAffixes(result, validSuffixes, suffixCount, rarity, false);

        return result.ToArray();
    }

    // ItemType → EquipmentAffixPool
    private static EquipmentAffixPool GetPool(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.武器 => EquipmentAffixPool.Weapon,
            ItemType.头盔 or ItemType.盔甲 or ItemType.靴子 or ItemType.手套 => EquipmentAffixPool.Armor,
            ItemType.饰品 => EquipmentAffixPool.Accessory,
            _ => EquipmentAffixPool.All // 未知类型使用通用池
        };
    }

    // 过滤词缀数组：只保留匹配池或 All 池的词缀
    private static List<EquipmentAffixEntry> FilterByPool(EquipmentAffixEntry[] entries, EquipmentAffixPool targetPool)
    {
        var result = new List<EquipmentAffixEntry>();
        if (entries == null)
            return result;

        foreach (var e in entries)
        {
            if (e.pool == targetPool || e.pool == EquipmentAffixPool.All)
                result.Add(e);
        }
        return result;
    }

    // 稀有度 → 允许的词缀类型集合（稀有度调性表）
    // 普通=正/双/负（赌博）  优秀=正/双  稀有=全双刃（温和代价）  史诗=全正面  传说=双刃+正面
    private static AffixType[] GetAllowedAffixTypes(LootRarity rarity)
    {
        return rarity switch
        {
            LootRarity.普通 => new[] { AffixType.Positive, AffixType.DoubleEdge, AffixType.Negative },
            LootRarity.精良 => new[] { AffixType.Positive, AffixType.DoubleEdge },
            LootRarity.稀有 => new[] { AffixType.DoubleEdge },
            LootRarity.史诗 => new[] { AffixType.Positive },
            LootRarity.传说 => new[] { AffixType.DoubleEdge, AffixType.Positive },
            _ => new[] { AffixType.Positive }
        };
    }

    // 过滤词缀池：只保留稀有度允许的词缀类型（调性表落地）
    private static List<EquipmentAffixEntry> FilterByType(List<EquipmentAffixEntry> pool, AffixType[] allowedTypes)
    {
        var result = new List<EquipmentAffixEntry>();
        foreach (var e in pool)
        {
            if (e == null)
                continue;
            foreach (var t in allowedTypes)
            {
                if (e.Type == t)
                {
                    result.Add(e);
                    break;
                }
            }
        }
        return result;
    }

    // 使用通用 AffixSelector 加权随机选取，为每个选中词缀生成效果并封装为 GeneratedEquipmentAffix
    private static void AddWeightedAffixes(List<GeneratedEquipmentAffix> result, List<EquipmentAffixEntry> pool,
        int count, LootRarity rarity, bool isPrefix)
    {
        // 稀有度调性：先按稀有度过滤词缀类型（普通=正/双/负，优秀=正/双，稀有=全双刃，史诗=全正面，传说=双/正）
        pool = FilterByType(pool, GetAllowedAffixTypes(rarity));
        if (pool.Count == 0)
            return; // 该稀有度在池内无可用类型词缀

        // 保底机制：词缀 tier 下限 = 稀有度-1 级（普通装保底普通，传说装保底史诗），与上限同为稀有度级
        int floorTier = Mathf.Max(0, (int)rarity - 1);
        int maxTier = (int)rarity;

        // 委托 AffixSelector 处理加权随机+去重+Tier上下限过滤（minTier=保底）
        var selected = AffixSelector.Select(
            pool,
            count,
            maxTier,
            getId: e => e.affixId,
            getTier: e => (int)e.tier,
            getWeight: e => e.weight,
            minTier: floorTier
        );

        // 为每个选中的词缀生成效果（每段 statModifier 独立 roll，含负面段）
        foreach (var entry in selected)
        {
            if (entry.statModifiers == null || entry.statModifiers.Length == 0)
                continue;

            // 方案B：词缀数值只看词缀自身 tier 的 min-max，稀有度只决定可选词缀池深度（不放大数值）
            var modifiers = new ItemModifier[entry.statModifiers.Length];
            for (int i = 0; i < entry.statModifiers.Length; i++)
            {
                AffixStatModifier sm = entry.statModifiers[i];

                // 该段独立 roll：负值段（min<0）roll 出负数 = 负面效果
                float value = Mathf.Round(Random.Range(sm.minValue, sm.maxValue));

                // 百分比词缀存为小数（8% → 0.08，-8% → -0.08），固定值存绝对值
                if (sm.isPercentage)
                    value = value / 100f;

                modifiers[i] = new ItemModifier
                {
                    statType = sm.statType,
                    value = value,
                    isPercentage = sm.isPercentage
                };
            }

            result.Add(new GeneratedEquipmentAffix
            {
                displayName = entry.displayName,
                tier = entry.tier,
                isPrefix = isPrefix,
                modifiers = modifiers,
                hasNegative = entry.HasNegative
            });
        }
    }

    // 根据稀有度确定最大词缀总数（前+后）
    private static int GetMaxAffixCount(LootRarity rarity)
    {
        return rarity switch
        {
            LootRarity.普通 => Random.value < 0.5f ? 1 : 0, // 50% 概率 1 个词缀
            LootRarity.精良 => Random.Range(1, 3),          // 1-2 个
            LootRarity.稀有 => Random.Range(2, 4),          // 2-3 个
            LootRarity.史诗 => Random.Range(3, 5),          // 3-4 个（至少1前+1后）
            LootRarity.传说 => 4,                            // 固定 4 个（2前+2后）
            _ => 0
        };
    }
}

// 单个已生成的装备词缀（含名称/前后缀/具体效果）
[System.Serializable]
public class GeneratedEquipmentAffix
{
    public string displayName;        // 词缀显示名（前缀如 "烈焰"，后缀如 "灼热"）
    public AffixTier tier;            // 词缀等级（UI 富文本着色用）
    public bool isPrefix;             // true=前缀（物品名前）, false=后缀（物品名后）
    public ItemModifier[] modifiers;  // 该词缀的具体效果（全伤/全抗展开为多个）
    public bool hasNegative;          // 是否含负面效果（双刃/纯垃圾）— UI 警示色判断用
}
