using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG设置/战利品系统/掉落表", fileName = "LootTable -")]
public class LootTable : ScriptableObject
{
    public string tableId; // CSV 导入用的ID

    [Header("掉落表配置")]
    [SerializeField] private List<LootDropItem> lootItems = new List<LootDropItem>(); //掉落物品列表

    [Header("掉落规则")]
    [SerializeField] private int minDropCount = 1; //最小掉落物品数量
    [SerializeField] private int maxDropCount = 3; //最大掉落物品数量
    [SerializeField] private bool allowDuplicates = false; //是否允许重复掉落相同物品

    [Header("稀有度加成")]
    [Range(0f, 200f)]
    [SerializeField] private float extraRarityDropChanceBonus = 50f; //额外稀有度掉落概率加成（百分比，0=不加成，50=提升50%，100=提升100%）

    [Header("货币掉落（>0 时额外掉货币，与物品并行）")]
    [SerializeField] private int currencyAmount = 0; //基础货币量

    public List<LootDropItem> LootItems => lootItems; //获取掉落物品列表
    public int CurrencyAmount => currencyAmount; //货币掉落量
    public void SetCurrencyAmount(int amount) => currencyAmount = Mathf.Max(0, amount);

    public List<LootedItem> GenerateLoot() //生成掉落物品
    {
        List<LootedItem> droppedItems = new List<LootedItem>();

        // 货币掉落（与物品并行）
        if (currencyAmount > 0)
        {
            int amount = Random.Range(currencyAmount / 2, currencyAmount + 1);
            droppedItems.Add(new LootedItem(null, LootRarity.普通) { currencyAmount = amount });
        }

        List<LootDropItem> availableItems = new List<LootDropItem>(lootItems);

        // 尝试掉落次数由 min/maxDropCount 决定
        int dropCount = UnityEngine.Random.Range(minDropCount, maxDropCount + 1);

        for (int i = 0; i < dropCount; i++)
        {
            if (availableItems.Count == 0) break;

            LootDropItem selectedItem = SelectRandomItem(availableItems); // 权重选物品（已含概率）
            if (selectedItem == null) continue;

            // 去掉 CanDrop()，因为 SelectRandomItem 已按权重做了概率筛选
            int count = selectedItem.GetDropCount();
            for (int j = 0; j < count; j++)
            {
                LootRarity actualRarity = selectedItem.GetDroppedRarity(extraRarityDropChanceBonus);
                droppedItems.Add(new LootedItem(selectedItem.ItemData, actualRarity));
            }

            if (!allowDuplicates)
                availableItems.Remove(selectedItem);
        }

        return droppedItems;
    }

    private LootDropItem SelectRandomItem(List<LootDropItem> items) //随机选择一个物品
    {
        if (items.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (var item in items)
        {
            totalWeight += item.DropChance;
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var item in items)
        {
            currentWeight += item.DropChance;
            if (randomValue <= currentWeight)
            {
                return item;
            }
        }

        return items[items.Count - 1];
    }

    public List<ItemDataSo> GetItemsByRarity(LootRarity rarity) //根据稀有度获取物品
    {
        List<ItemDataSo> items = new List<ItemDataSo>();
        foreach (var lootItem in lootItems)
        {
            if (lootItem.Rarity == rarity)
            {
                items.Add(lootItem.ItemData);
            }
        }
        return items;
    }

    public void AddLootItem(ItemDataSo itemData, float dropChance = 100f, int minDropCount = 1, int maxDropCount = 1) //添加掉落物品（使用物品自带稀有度）
    {
        if (itemData == null)
            return;

        LootDropItem lootItem = new LootDropItem(itemData, dropChance, minDropCount, maxDropCount);
        lootItems.Add(lootItem);
    }

    public void AddLootItemWithCustomRarity(ItemDataSo itemData, LootRarity rarity, float dropChance = 100f, int minDropCount = 1, int maxDropCount = 1) //添加掉落物品（使用自定义稀有度）
    {
        if (itemData == null)
            return;

        LootDropItem lootItem = new LootDropItem(itemData, rarity, dropChance, minDropCount, maxDropCount);
        lootItems.Add(lootItem);
    }

    public void RemoveLootItem(ItemDataSo itemData) //移除掉落物品
    {
        lootItems.RemoveAll(item => item.ItemData == itemData);
    }

    public void ClearLootItems() //清空掉落物品列表
    {
        lootItems.Clear();
    }
}
