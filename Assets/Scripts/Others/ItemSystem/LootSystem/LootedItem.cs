using UnityEngine;

[System.Serializable]
public class LootedItem
{
    public ItemDataSo baseItemData; //基础物品数据
    public LootRarity baseRarity; //基准稀有度
    public LootRarity actualRarity; //实际稀有度
    public float statMultiplier; //数值倍率（基于差值）
    public string uniqueId; //唯一ID

    public LootedItem(ItemDataSo baseData, LootRarity actualRarity)
    {
        baseItemData = baseData;
        baseRarity = baseData.rarity;
        this.actualRarity = actualRarity;
        
        //计算差值倍率
        statMultiplier = RarityCalculator.GetRarityDifferenceMultiplier(baseRarity, actualRarity);
        uniqueId = System.Guid.NewGuid().ToString();
    }

    public float GetModifiedValue(int baseValue) //获取修改后的数值（保留一位小数）
    {
        return RarityCalculator.GetModifiedValue(baseValue, baseRarity, actualRarity);
    }

    public float GetModifiedValue(float baseValue) //获取修改后的数值（保留一位小数）
    {
        return RarityCalculator.GetModifiedValue(baseValue, baseRarity, actualRarity);
    }

    public string GetDisplayName() //获取显示名称（带稀有度）
    {
        string rarityName = RarityCalculator.GetRarityName(actualRarity);
        return $"[{rarityName}] {baseItemData.itemName}";
    }

    public Color GetRarityColor() //获取稀有度颜色
    {
        return RarityCalculator.GetRarityColor(actualRarity);
    }

    public float GetTotalMultiplier() //获取总倍率（包含差值）
    {
        return RarityCalculator.GetBaseMultiplier(baseRarity) * statMultiplier;
    }
}
