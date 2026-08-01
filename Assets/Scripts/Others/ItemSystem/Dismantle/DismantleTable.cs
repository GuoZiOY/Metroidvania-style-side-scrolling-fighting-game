using UnityEngine;

// 分解产出表 — 每种装备类型对应可分解出的材料（F10 分解系统）
// 材料种类与基础数量由策划配置；分解 = 制作逆过程，产出数量有损耗
[CreateAssetMenu(menuName = "破碎之城/分解产出表", fileName = "DismantleTable")]
public class DismantleTable : ScriptableObject
{
    [Header("按装备类型配置分解产出材料")]
    public DismantleEntry[] entries;
}

// 单个装备类型的分解配置
[System.Serializable]
public class DismantleEntry
{
    public ItemType itemType;             // 装备类型（武器/防具/饰品）
    public DismantleMaterial[] materials; // 可产出的材料列表
}

// 单种分解产出材料
[System.Serializable]
public class DismantleMaterial
{
    public ItemDataSo material;   // 产出材料物品数据
    public int baseCount;         // 基础产出数量（最终 = baseCount × 稀有度倍率）
    public float weight = 1f;     // 权重（同类型多材料时按比例分配，当前简单实现为每材料独立计算）
}
