using UnityEngine;

// 装备词缀数据库 — 供 EquipmentAffixGenerator (C6/F5) 使用
// 按池（武器/防具/饰品）分组的前缀和后缀，每个条目定义具体属性加成
[CreateAssetMenu(menuName = "破碎之城/装备词缀数据库", fileName = "EquipmentAffixDatabase")]
public class EquipmentAffixDatabase : ScriptableObject
{
    [Header("装备词缀 — 前缀")]
    public EquipmentAffixEntry[] prefixes;

    [Header("装备词缀 — 后缀")]
    public EquipmentAffixEntry[] suffixes;
}

// 装备词缀池 — 决定词缀可出现在哪些装备类型上
public enum EquipmentAffixPool
{
    Weapon,     // 武器 — 伤害/攻速/暴击/暴伤/元素伤/护甲穿透
    Armor,      // 防具(头盔/盔甲/靴子/手套) — 护甲/闪避/HP/HP回复/全抗/防御%
    Accessory,  // 饰品 — 主属性/全伤%/全抗%
    All         // 通用 — 所有装备类型都可出现
}

// 装备词缀条目 — 定义单个前缀或后缀的属性加成规则
// 支持多段效果：全伤=物/火/冰/雷各一段；双刃词缀=正段+负段；纯垃圾词缀=纯负段
[System.Serializable]
public class EquipmentAffixEntry
{
    public string affixId;                  // 词缀标识（如 "prefix_blazing"）
    public string displayName;              // UI 显示名（前缀如 "烈焰"，后缀如 "灼热"）
    public AffixTier tier;                  // 词缀等级（5级，对应装备稀有度）
    [Range(1, 10)] public int weight = 5;   // 词缀权重（值越大越常见）
    public EquipmentAffixPool pool;         // 所属池：武器/防具/饰品/通用
    public AffixStatModifier[] statModifiers; // 每段效果的独立数值（含负值=负面效果），上限4段

    // 词缀类型（从效果段自动推断）：正面全正 / 双刃正负共存 / 负面全负
    public AffixType Type
    {
        get
        {
            bool hasPositive = false;
            bool hasNegative = false;
            if (statModifiers != null)
            {
                foreach (var sm in statModifiers)
                {
                    if (sm == null)
                        continue;
                    if (sm.maxValue > 0)
                        hasPositive = true;
                    if (sm.minValue < 0)
                        hasNegative = true;
                }
            }
            if (hasPositive && hasNegative)
                return AffixType.DoubleEdge;
            if (hasNegative)
                return AffixType.Negative;
            return AffixType.Positive;
        }
    }

    // 是否含负面效果（纯垃圾或双刃）——UI 警示色判断用
    public bool HasNegative => Type != AffixType.Positive;
}

// 词缀类型 — 决定该词缀能被哪些稀有度装备选用（配合稀有度调性表）
public enum AffixType
{
    Positive,    // 正面 — 全正效果
    DoubleEdge,  // 双刃 — 正负共存（强力但带代价）
    Negative     // 负面/垃圾 — 全负效果
}

// 单段词缀效果 — 一个属性 + 独立数值范围（可为负值）
[System.Serializable]
public class AffixStatModifier
{
    public StatType statType;              // 影响的属性（如 物理伤害/护甲）
    public float minValue;                 // 数值范围下限（负数=负面效果）
    public float maxValue;                 // 数值范围上限（负数=负面效果）
    public bool isPercentage;              // true=百分比(如+15%物伤), false=固定值(如+5火伤)
}
