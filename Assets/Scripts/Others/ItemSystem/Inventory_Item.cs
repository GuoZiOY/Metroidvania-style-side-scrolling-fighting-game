using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public class Inventory_Item
{
    public ItemDataSo itemData;//物品数据
    public string itemID;//物品ID

    public int currentStackSize;//当前物品数量
    private int maxStackSize = 99;//最大堆叠数量

    [field: SerializeField]
    public ItemModifier[] Modifiers{get; set;}
    
    public LootRarity? actualRarity; //实际稀有度（用于掉落物品）
    public float rarityMultiplier; //稀有度倍率

    public bool IsEquipment => itemData is EquipmentDataSo;
    public bool IsConsumable => itemData is ConsumableDataSo;

    public Inventory_Item(ItemDataSo itemData) //普通构造函数（用于手动创建物品）
    {
        this.itemData = itemData;//物品数据
        currentStackSize = 1;//初始化物品数量为1
        Modifiers = EquipmentData()?.modifiers;//获取装备数据

        //生成物品ID，包含稀有度信息
        string rarityName = RarityCalculator.GetRarityName(itemData.rarity);
        itemID = $"{itemData.itemType} - [{rarityName}] - {itemData.itemName}  - {Guid.NewGuid().ToString().Substring(0, 4)}";
    }

    public Inventory_Item(LootedItem lootedItem) //掉落物品构造函数（支持稀有度）
    {
        this.itemData = lootedItem.baseItemData;//物品数据
        currentStackSize = 1;//初始化物品数量为1
        this.actualRarity = lootedItem.actualRarity; //保存实际稀有度
        this.rarityMultiplier = lootedItem.statMultiplier; //保存稀有度倍率
        
        Debug.Log($"[Inventory_Item] 创建掉落物品: {itemData.itemName}, 基础稀有度: {lootedItem.baseRarity}, 实际稀有度: {lootedItem.actualRarity}, 倍率: {rarityMultiplier}");
        
        //获取原始修饰器并应用稀有度倍率
        EquipmentDataSo equipmentData = itemData as EquipmentDataSo;
        if (equipmentData != null && equipmentData.modifiers != null && equipmentData.modifiers.Length > 0)
        {
            Modifiers = new ItemModifier[equipmentData.modifiers.Length];
            for (int i = 0; i < equipmentData.modifiers.Length; i++)
            {
                float originalValue = equipmentData.modifiers[i].value;
                float modifiedValue = lootedItem.GetModifiedValue(originalValue);
                Modifiers[i] = new ItemModifier
                {
                    statType = equipmentData.modifiers[i].statType,
                    value = modifiedValue //应用稀有度倍率
                };
                Debug.Log($"[Inventory_Item] 属性 {equipmentData.modifiers[i].statType}: {originalValue:F1} -> {modifiedValue:F1}");
            }
        }
        else
        {
            Modifiers = null;
            Debug.Log($"[Inventory_Item] 物品 {itemData.itemName} 没有修饰器数据");
        }

        //生成物品ID，包含稀有度信息
        string rarityName = RarityCalculator.GetRarityName(actualRarity.Value);
        itemID = $"{itemData.itemType} - {itemData.itemName} - [{rarityName}] - {Guid.NewGuid().ToString().Substring(0, 4)}";
    }
    
    private EquipmentDataSo EquipmentData()//判定是否是装备数据
    {
        if(itemData is EquipmentDataSo equipment)
            return equipment;
            
        return null;
    }

    private ConsumableDataSo ConsumableData()//判定是否是消耗品数据
    {
        if(itemData is ConsumableDataSo consumable)
            return consumable;
            
        return null;
    }

    public void AddModifiers(Entity_Stats playerStats)
    {
        if(Modifiers == null) return;
        
        foreach(var mod in Modifiers)
        {
            Stat statToModify = playerStats.GetStatByType(mod.statType);
            statToModify.AddModifier(mod.value, itemID);
        }
    }

    public void RemoveModifiers(Entity_Stats playerStats)
    {
        if(Modifiers == null) return;
        
        foreach(var mod in Modifiers)
        {
            Stat statToModify = playerStats.GetStatByType(mod.statType);
            statToModify.RemoveModifier(itemID);
        }
    }


    public bool CanAddStack()
    {
        return itemData.canStackable && currentStackSize < maxStackSize;
    }

    public void AddStack() => currentStackSize ++;

    public bool CanUseConsumable()//判断是否可以使用消耗品
    {
        if (!IsConsumable)
            return false;

        ConsumableDataSo consumableData = ConsumableData();
        if (consumableData == null)
            return false;

        if (currentStackSize <= 0)
            return false;

        return true;
    }

    public ConsumableDataSo GetConsumableData()//获取消耗品数据
    {
        return ConsumableData();
    }
}