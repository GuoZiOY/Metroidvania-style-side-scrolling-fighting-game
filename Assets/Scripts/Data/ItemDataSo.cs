using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( menuName = "RPG设置/物品数据/物品数据", fileName = "ItemData -")]
public class ItemDataSo : ScriptableObject
{
    public string itemId;//物品ID（唯一标识，用于任务等系统引用）
    public string itemName;//物品名称
    public Sprite itemIcon;//物品图标
    public ItemType itemType;//物品类型
    public LootRarity rarity = LootRarity.普通;//物品稀有度

    [Header("稀有度波动")]
    public bool allowRarityVariation = true; //是否允许稀有度波动
    public int maxRaritySteps = 1; //最大稀有度波动步数（向上或向下最多几级）

    public bool canStackable;//是否可以堆叠

    public LootRarity GetDroppedRarity() //获取掉落时的稀有度（基于基准稀有度波动）
    {
        return GetDroppedRarity(0f);
    }

    public LootRarity GetDroppedRarity(float rarityBonus) //获取掉落时的稀有度（基于基准稀有度波动，带稀有度加成）
    {
        if (!allowRarityVariation)
            return rarity;

        return RarityCalculator.GetVariedRarity(rarity, maxRaritySteps, rarityBonus);
    }
}
