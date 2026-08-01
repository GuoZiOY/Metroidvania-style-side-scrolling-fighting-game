#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// 词缀系统初始化工具 — 一键生成两个词缀数据库
// 菜单: Tools/破碎之城/初始化词缀系统
// 精英词缀采用方案B（AddComponent），无需预制体
// 注意：场景接线（GameBootstrap 挂载、EnemySpawner 挂 AffixSpawner 并引用数据库）需手动完成
public static class AffixSystemGenerator
{

    // ==================== 精英词缀数据库 ====================

    private static void GenerateEliteAffixDatabase()
    {
        var db = ScriptableObject.CreateInstance<EliteAffixDatabase>();

        // 5 个类别，16 个词缀（ID 必须与 AffixSpawner.AffixTypeMap 一致）
        db.pools = new[]
        {
            Pool(AffixCategory.Elemental, 0.25f,
                Elite("affix_fire_aura", AffixTier.Common),
                Elite("affix_flame_trail", AffixTier.Common),
                Elite("affix_ice_nova", AffixTier.Rare),
                Elite("affix_lightning_conduit", AffixTier.Rare)),
            Pool(AffixCategory.Defensive, 0.20f,
                Elite("affix_stone_skin", AffixTier.Common),
                Elite("affix_thick_hide", AffixTier.Common),
                Elite("affix_reactive_armor", AffixTier.Rare),
                Elite("affix_barrier_shield", AffixTier.Legendary)),
            Pool(AffixCategory.Offensive, 0.20f,
                Elite("affix_berserk", AffixTier.Rare),
                Elite("affix_vampiric_strike", AffixTier.Legendary),
                Elite("affix_enrage_timer", AffixTier.Common)),
            Pool(AffixCategory.Summon, 0.15f,
                Elite("affix_mirror_clone", AffixTier.Legendary),
                Elite("affix_self_destruct", AffixTier.Rare)),
            Pool(AffixCategory.Curse, 0.20f,
                Elite("affix_slow_aura", AffixTier.Rare),
                Elite("affix_vulnerability_hex", AffixTier.Common),
                Elite("affix_healing_curse", AffixTier.Rare)),
        };

        // 生成到 Resources/Data 下，供 GameBootstrap 通过 Resources.Load 加载
        string dir = "Assets/Resources/Data";
        EnsureDirectory(dir);
        string path = $"{dir}/EliteAffixDatabase.asset";

        AssetDatabase.CreateAsset(db, path);
        Debug.Log($"[AffixSystemGenerator] 精英词缀数据库已生成: {path}");
    }

    private static EliteAffixPool Pool(AffixCategory category, float weight, params EliteAffixEntry[] entries)
    {
        return new EliteAffixPool { category = category, categoryWeight = weight, affixes = entries };
    }

    private static EliteAffixEntry Elite(string id, AffixTier tier)
    {
        return new EliteAffixEntry { affixId = id, tier = tier, weight = 5 };
    }

    // ==================== 工具 ====================

    private static void EnsureDirectory(string dir)
    {
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}
#endif
