using System;
using System.Collections.Generic;
using UnityEngine;

// 通用加权随机词缀选择器 — 精英词缀系统(C14)和装备词缀系统(C6/F5)共享
// 泛型+lambda，两套词缀条目类型无需实现公共接口
public static class AffixSelector
{
    // 从全量条目中加权随机选取 count 个不重复条目
    // 过滤条件：minTier ≤ Tier ≤ maxTier、不重复（按 getId 去重）
    // 选取逻辑：按 getWeight 加权随机
    public static List<T> Select<T>(
        IEnumerable<T> allEntries,       // 全量候选条目
        int count,                        // 需要选取的数量
        int maxTier,                      // 最大允许的 Tier 值（int 形式，如 (int)AffixTier.Rare = 2）
        Func<T, string> getId,            // 获取唯一标识（用于去重）
        Func<T, int> getTier,             // 获取 Tier 整数值（用于过滤）
        Func<T, int> getWeight,           // 获取权重值（用于加权随机）
        int minTier = 0                   // 最低允许的 Tier 值（保底机制用，默认 0 不限制）
    )
    {
        var result = new List<T>();
        var usedIds = new HashSet<string>(); // 防止同一词缀出现两次

        for (int i = 0; i < count; i++)
        {
            // 收集候选：Tier 在 [minTier, maxTier] 内 + 未重复
            var candidates = BuildCandidates(allEntries, usedIds, maxTier, getId, getTier, getWeight, minTier);
            if (candidates.Count == 0)
                break; // 池耗尽，提前结束

            T selected = WeightedRandom(candidates);
            result.Add(selected);
            usedIds.Add(getId(selected));
        }

        return result;
    }

    // 收集符合条件的候选（Tier 上下限过滤 + 去重）
    private static List<(T entry, int weight)> BuildCandidates<T>(
        IEnumerable<T> allEntries,
        HashSet<string> usedIds,
        int maxTier,
        Func<T, string> getId,
        Func<T, int> getTier,
        Func<T, int> getWeight,
        int minTier
    )
    {
        var candidates = new List<(T entry, int weight)>();
        foreach (var entry in allEntries)
        {
            if (entry == null)
                continue;
            int tier = getTier(entry);
            if (tier < minTier)                      // Tier 下限过滤（保底）
                continue;
            if (tier > maxTier)                      // Tier 上限过滤
                continue;
            if (usedIds.Contains(getId(entry)))       // 去重
                continue;
            candidates.Add((entry, getWeight(entry)));
        }
        return candidates;
    }

    // 加权随机：权重总和越大，被选中概率越高
    // 所有权重为 0 时退化为均匀随机（返回最后一项）
    private static T WeightedRandom<T>(List<(T entry, int weight)> candidates)
    {
        int totalWeight = 0;
        foreach (var c in candidates)
            totalWeight += c.weight;

        if (totalWeight <= 0)
            return candidates[candidates.Count - 1].entry;

        int roll = UnityEngine.Random.Range(0, totalWeight); // 显式限定 UnityEngine.Random（避免与 System.Random 歧义）
        int cumulative = 0;
        foreach (var c in candidates)
        {
            cumulative += c.weight;
            if (roll < cumulative)
                return c.entry;
        }

        return candidates[candidates.Count - 1].entry;
    }
}
