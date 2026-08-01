using UnityEngine;

// 精英词缀数据库 — 供 AffixSpawner (C14) 使用
// 按类别分组，每个词缀条目指向一个 IEnemyAffix 预制体
[CreateAssetMenu(menuName = "破碎之城/精英词缀数据库", fileName = "EliteAffixDatabase")]
public class EliteAffixDatabase : ScriptableObject
{
    [Header("精英词缀池 — 按类别分组")]
    public EliteAffixPool[] pools;
}

// 精英词缀类别 — 决定词缀池分组和选取权重
[System.Serializable]
public class EliteAffixPool
{
    public AffixCategory category;              // 元素/防御/攻击/召唤/诅咒
    [Range(0f, 1f)] public float categoryWeight; // 类别权重（所有类别权重总和建议=1）
    public EliteAffixEntry[] affixes;            // 此类别下的所有词缀条目
}

// 精英词缀条目 — 对应一个 IEnemyAffix 组件预制体
[System.Serializable]
public class EliteAffixEntry
{
    public string affixId;                  // 对应 IEnemyAffix.AffixId（如 "affix_fire_aura"）
    public AffixTier tier;                  // 词缀等级（Common/Rare/Legendary）
    [Range(1, 10)] public int weight = 5;   // 词缀权重（值越大越常见）
}

// 精英词缀类别
public enum AffixCategory
{
    Elemental,  // 元素 — 火焰光环/冰霜新星/感电充能
    Defensive,  // 防御 — 石肤/护盾/反应护甲/厚皮
    Offensive,  // 攻击 — 狂暴/吸血/激怒
    Summon,     // 召唤 — 镜像分身/自爆
    Curse       // 诅咒 — 减速光环/易伤/治疗诅咒/闪电导体
}
