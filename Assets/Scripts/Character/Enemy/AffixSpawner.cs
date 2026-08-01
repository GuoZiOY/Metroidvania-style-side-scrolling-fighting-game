using System;
using System.Collections.Generic;
using UnityEngine;

// 词缀分配工具（静态）— 精英敌人生成时分配随机词缀并 AddComponent 注入
// 不依赖场景组件，由 EnemySpawner 生成精英后调用 ApplyEliteAffixes
// 数据库由 GameBootstrap 启动时赋值（与装备词缀一致）
public static class AffixSpawner
{
    private static EliteAffixDatabase cachedDatabase;

    // 精英词缀数据库：首次访问时从 Resources 惰性加载
    public static EliteAffixDatabase Database
    {
        get
        {
            if (cachedDatabase == null)
                cachedDatabase = Resources.Load<EliteAffixDatabase>("Data/EliteAffixDatabase");
            return cachedDatabase;
        }
        set => cachedDatabase = value;
    }

    // 精英层级概率（数值调校，改代码即可）
    private static float legendaryChance = 0.05f; // 传说级精英概率（3词缀）
    private static float eliteChance = 0.15f;    // 稀优级精英概率（2词缀）

    // 词缀 ID → 组件类型 映射（方案B：运行时 AddComponent）
    // 新增词缀时在此注册对应组件类型
    private static readonly Dictionary<string, Type> AffixTypeMap = new()
    {
        { "affix_fire_aura", typeof(FireAuraAffix) },
        { "affix_flame_trail", typeof(FlameTrailAffix) },
        { "affix_ice_nova", typeof(IceNovaAffix) },
        { "affix_lightning_conduit", typeof(LightningConduitAffix) },
        { "affix_slow_aura", typeof(SlowAuraAffix) },
        { "affix_vulnerability_hex", typeof(VulnerabilityHexAffix) },
        { "affix_healing_curse", typeof(HealingCurseAffix) },
        { "affix_stone_skin", typeof(StoneSkinAffix) },
        { "affix_thick_hide", typeof(ThickHideAffix) },
        { "affix_reactive_armor", typeof(ReactiveArmorAffix) },
        { "affix_barrier_shield", typeof(BarrierShieldAffix) },
        { "affix_berserk", typeof(BerserkAffix) },
        { "affix_vampiric_strike", typeof(VampiricStrikeAffix) },
        { "affix_enrage_timer", typeof(EnrageTimerAffix) },
        { "affix_mirror_clone", typeof(MirrorCloneAffix) },
        { "affix_self_destruct", typeof(SelfDestructAffix) },
    };

    // 精英敌人生成时调用：掷骰层级 → 两步选词缀 → AddComponent 注入
    public static void ApplyEliteAffixes(Enemy enemy, EnemyType enemyType)
    {
        if (enemy == null || Database == null)
            return;

        // 掷骰决定精英层级（三级精英体系 — 精锐1词缀/稀优2词缀/传说3词缀）
        int tierLevel = RollTierLevel(enemyType);
        if (tierLevel <= 0)
            return;

        int affixCount = GetAffixCount(tierLevel);
        if (affixCount <= 0)
            return;

        // 两步选取：先按类别权重掷类别，再从该类别加权选词缀
        var selectedIds = SelectAffixes(affixCount, tierLevel);

        // 逐个 AddComponent 注入
        foreach (var id in selectedIds)
            ApplyAffixToEnemy(enemy, id);
    }

    // 掷骰决定精英层级：Boss 恒为传说级(3)，精英按概率分布
    private static int RollTierLevel(EnemyType type)
    {
        if (type == EnemyType.Boss)
            return 3; // Boss 固定 3 词缀

        if (type != EnemyType.Elite)
            return 0; // 普通：无词缀

        float roll = UnityEngine.Random.value;
        if (roll < legendaryChance)
            return 3; // 传说级：3 词缀
        if (roll < legendaryChance + eliteChance)
            return 2; // 稀优级：2 词缀
        return 1;     // 精锐级：1 词缀
    }

    private static int GetAffixCount(int tierLevel)
    {
        return tierLevel switch
        {
            1 => 1, // 精锐 (Rare)
            2 => 2, // 稀优 (Elite)
            3 => 3, // 传说 (Legendary) / Boss
            _ => 0
        };
    }

    // 两步选取词缀：每次先按 categoryWeight 掷类别，再从该类别加权随机选一个词缀
    // 使用全局 usedIds 保证跨类别也不重复
    private static List<string> SelectAffixes(int count, int tierLevel)
    {
        var ids = new List<string>();
        var usedIds = new HashSet<string>(); // 全局去重（跨类别）

        for (int i = 0; i < count; i++)
        {
            // 步骤1：按类别权重掷出类别池
            var pool = RollCategory();
            if (pool == null || pool.affixes == null || pool.affixes.Length == 0)
                break; // 无可用类别

            // 步骤2：从该类别收集可用词缀（Tier 合格 + 未使用）
            var candidates = new List<EliteAffixEntry>();
            foreach (var entry in pool.affixes)
            {
                if (entry == null)
                    continue;
                if (usedIds.Contains(entry.affixId))
                    continue;
                if ((int)entry.tier > tierLevel)
                    continue;
                candidates.Add(entry);
            }

            if (candidates.Count == 0)
                continue; // 该类别无可用词缀，重新掷类别

            // 在类别内加权随机选 1 个
            var selected = AffixSelector.Select(
                candidates,
                1,
                tierLevel,
                getId: e => e.affixId,
                getTier: e => (int)e.tier,
                getWeight: e => e.weight
            );

            if (selected.Count > 0)
            {
                usedIds.Add(selected[0].affixId);
                ids.Add(selected[0].affixId);
            }
        }

        return ids;
    }

    // 按 categoryWeight 加权随机掷出一个类别池
    private static EliteAffixPool RollCategory()
    {
        // 收集有权重的类别
        var weighted = new List<(EliteAffixPool pool, float weight)>();
        float totalWeight = 0f;
        foreach (var p in Database.pools)
        {
            if (p != null && p.categoryWeight > 0f)
            {
                weighted.Add((p, p.categoryWeight));
                totalWeight += p.categoryWeight;
            }
        }

        if (weighted.Count == 0)
            return null; // 无配置权重的类别

        if (totalWeight <= 0f)
            return weighted[0].pool; // 退化：返回第一个

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;
        foreach (var w in weighted)
        {
            cumulative += w.weight;
            if (roll < cumulative)
                return w.pool;
        }

        return weighted[weighted.Count - 1].pool;
    }

    // 方案B：根据词缀 ID 在敌人 GameObject 上动态 AddComponent 并注入
    private static void ApplyAffixToEnemy(Enemy enemy, string affixId)
    {
        if (!AffixTypeMap.TryGetValue(affixId, out Type affixType))
        {
            Debug.LogWarning($"[AffixSpawner] 未注册词缀类型: {affixId}，请在 AffixTypeMap 中添加");
            return;
        }

        // 直接挂到敌人 GameObject（词缀组件随敌人销毁，StatAffixBase 的 OnDestroy 兜底清理属性）
        var affix = (IEnemyAffix)enemy.gameObject.AddComponent(affixType);
        affix.OnApplied(enemy);
        enemy.AddAffix(affix);
    }
}
