using System;
using UnityEngine;

[Serializable]
public class LootDropItem
{
    [Header("物品配置")]
    [SerializeField] private ItemDataSo itemData; //物品数据
    [SerializeField] private bool useItemRarity = true; //是否使用物品自带的稀有度
    [SerializeField] private LootRarity rarity = LootRarity.普通; //物品稀有度（当useItemRarity为false时使用）

    [Header("掉落概率")]
    [SerializeField] private float dropChance = 100f; //掉落概率(0-100)
    [SerializeField] private int minDropCount = 1; //最小掉落数量
    [SerializeField] private int maxDropCount = 1; //最大掉落数量

    public ItemDataSo ItemData => itemData; //获取物品数据
    public LootRarity Rarity => useItemRarity && itemData != null ? itemData.rarity : rarity; //获取稀有度
    public float DropChance => dropChance; //获取掉落概率

    public LootRarity GetDroppedRarity() //获取掉落时的稀有度（使用物品的波动配置）
    {
        return GetDroppedRarity(0f);
    }

    public LootRarity GetDroppedRarity(float rarityBonus) //获取掉落时的稀有度（使用物品的波动配置，带稀有度加成）
    {
        if (useItemRarity && itemData != null)
        {
            // 使用物品自带的波动配置
            return itemData.GetDroppedRarity(rarityBonus);
        }
        else
        {
            // 使用自定义稀有度（不允许波动）
            return rarity;
        }
    }

    public LootDropItem(ItemDataSo itemData, LootRarity rarity, float dropChance = 100f, int minDropCount = 1, int maxDropCount = 1)
    {
        this.itemData = itemData;
        this.rarity = rarity;
        this.dropChance = dropChance;
        this.minDropCount = minDropCount;
        this.maxDropCount = maxDropCount;
        this.useItemRarity = false;
    }

    public LootDropItem(ItemDataSo itemData, float dropChance = 100f, int minDropCount = 1, int maxDropCount = 1)
    {
        this.itemData = itemData;
        this.dropChance = dropChance;
        this.minDropCount = minDropCount;
        this.maxDropCount = maxDropCount;
        this.useItemRarity = true;
    }

    public bool CanDrop() //判断是否可以掉落
    {
        float randomValue = UnityEngine.Random.Range(0f, 100f);
        return randomValue <= dropChance;
    }

    public int GetDropCount() //获取掉落数量
    {
        return UnityEngine.Random.Range(minDropCount, maxDropCount + 1);
    }
}
