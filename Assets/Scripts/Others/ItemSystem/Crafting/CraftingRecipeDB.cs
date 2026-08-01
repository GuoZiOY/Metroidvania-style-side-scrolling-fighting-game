using UnityEngine;

// 制作配方数据库 — 存储所有可制作配方（F9 制作系统）
// 配方数据由策划在 Inspector 配置（材料 ItemDataSo 引用 + 数量 + 金币费用 + 产物）
[CreateAssetMenu(menuName = "破碎之城/制作配方数据库", fileName = "CraftingRecipeDB")]
public class CraftingRecipeDB : ScriptableObject
{
    [Header("制作配方列表")]
    public CraftingRecipe[] recipes;
}

// 单个制作配方
[System.Serializable]
public class CraftingRecipe
{
    public string recipeId;              // 配方标识（唯一）
    public string recipeName;            // 配方显示名（UI 用）
    public ItemDataSo resultItem;        // 制作产物（装备/消耗品/材料）
    public MaterialEntry[] materials;    // 所需材料清单
    public int goldCost;                 // 制作金币费用（铜币单位，1金币=10000）
    public LootRarity minResultRarity;   // 最低产物稀有度（材料品质锁定产物下限）
}

// 单种材料需求
[System.Serializable]
public class MaterialEntry
{
    public ItemDataSo material;   // 材料物品数据（按引用匹配背包中的同种物品）
    public int count;             // 需要数量
}
