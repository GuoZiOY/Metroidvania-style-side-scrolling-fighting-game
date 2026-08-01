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

    public ItemModifier[] baseModifiers;          // 基础属性（来自 EquipmentDataSo.modifiers，应用稀有度倍率）
    public GeneratedEquipmentAffix[] affixes;     // 词缀列表（含名称/前后缀/效果，V2 生成时填充）

    public LootRarity? actualRarity; //实际稀有度（用于掉落物品）
    public float rarityMultiplier; //稀有度倍率

    public bool IsEquipment => itemData is EquipmentDataSo;
    public bool IsConsumable => itemData is ConsumableDataSo;

    public Inventory_Item(ItemDataSo itemData) //普通构造函数（用于手动创建物品）
    {
        this.itemData = itemData;//物品数据
        currentStackSize = 1;//初始化物品数量为1

        EquipmentDataSo equipmentData = itemData as EquipmentDataSo;
        if (equipmentData != null)
        {
            // 稀有度 = 底材稀有度（商店/制作/任务奖励的装备获得稀有度，可参与合成/稀有度显示）
            actualRarity = equipmentData.rarity;
            rarityMultiplier = 1f;

            // 基础属性填充：商店装备/制作产物等无词缀装备，tooltip 也能显示底材属性
            if (equipmentData.modifiers != null && equipmentData.modifiers.Length > 0)
            {
                baseModifiers = new ItemModifier[equipmentData.modifiers.Length];
                for (int i = 0; i < equipmentData.modifiers.Length; i++)
                {
                    baseModifiers[i] = new ItemModifier
                    {
                        statType = equipmentData.modifiers[i].statType,
                        value = equipmentData.modifiers[i].value,
                        isPercentage = equipmentData.modifiers[i].isPercentage
                    };
                }
            }

            // 词缀系统：按底材稀有度生成（非百分百，普通可能 0 个，精良+按词缀系统规则）
            affixes = EquipmentAffixGenerator.GenerateAffixes(equipmentData.itemType, equipmentData.rarity);

            // Modifiers = 基础属性 + 词缀效果（实际装备生效）
            var allMods = new List<ItemModifier>();
            if (baseModifiers != null)
                allMods.AddRange(baseModifiers);
            if (affixes != null)
            {
                foreach (var affix in affixes)
                {
                    if (affix.modifiers != null)
                        allMods.AddRange(affix.modifiers);
                }
            }
            Modifiers = allMods.Count > 0 ? allMods.ToArray() : null;
        }
        else
        {
            Modifiers = null; // 非装备无修饰符
        }

        //生成物品ID，包含稀有度信息
        string rarityName = RarityCalculator.GetRarityName(itemData.rarity);
        itemID = $"{itemData.itemType} - [{rarityName}] - {itemData.itemName}  - {Guid.NewGuid().ToString().Substring(0, 4)}";
    }

    public Inventory_Item(LootedItem lootedItem, GeneratedEquipmentAffix[] savedAffixes = null) //掉落物品构造函数（支持稀有度；可注入已存档词缀避免重新随机）
    {
        this.itemData = lootedItem.baseItemData;//物品数据
        currentStackSize = 1;//初始化物品数量为1
        this.actualRarity = lootedItem.actualRarity; //保存实际稀有度
        this.rarityMultiplier = lootedItem.statMultiplier; //保存稀有度倍率
        
        Debug.Log($"[Inventory_Item] 创建掉落物品: {itemData.itemName}, 基础稀有度: {lootedItem.baseRarity}, 实际稀有度: {lootedItem.actualRarity}, 倍率: {rarityMultiplier}");

        EquipmentDataSo equipmentData = itemData as EquipmentDataSo;
        if (equipmentData != null)
        {
            // 基础属性（EquipmentDataSo.modifiers，应用稀有度倍率）—— 词缀系统的底材属性
            if (equipmentData.modifiers != null && equipmentData.modifiers.Length > 0)
            {
                baseModifiers = new ItemModifier[equipmentData.modifiers.Length];
                for (int i = 0; i < equipmentData.modifiers.Length; i++)
                {
                    baseModifiers[i] = new ItemModifier
                    {
                        statType = equipmentData.modifiers[i].statType,
                        value = lootedItem.GetModifiedValue(equipmentData.modifiers[i].value) // 应用稀有度倍率
                    };
                }
            }

            // 词缀（V2）：随机生成前缀+后缀，叠加在基础属性之上
            // 读档时传入 savedAffixes 按存档精确恢复（不重新随机）；无存档则新生成
            affixes = savedAffixes ?? EquipmentAffixGenerator.GenerateAffixes(itemData.itemType, lootedItem.actualRarity);

            // 合并 Modifiers = 基础属性 + 词缀效果（装备系统 AddModifiers 用完整列表）
            var allMods = new List<ItemModifier>();
            if (baseModifiers != null)
                allMods.AddRange(baseModifiers);
            if (affixes != null)
            {
                foreach (var affix in affixes)
                {
                    if (affix.modifiers != null)
                        allMods.AddRange(affix.modifiers);
                }
            }
            Modifiers = allMods.Count > 0 ? allMods.ToArray() : null;
        }
        else
        {
            Modifiers = null;
            Debug.Log($"[Inventory_Item] 物品 {itemData.itemName} 不是装备，无修饰符");
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
            statToModify.AddModifier(mod.value, itemID, mod.isPercentage);
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

    // 减少堆叠数量（制作/分解扣除材料用）；返回剩余数量，≤0 表示该堆已清空需由调用方从背包移除
    public int ReduceStack(int amount)
    {
        currentStackSize -= amount;
        return currentStackSize;
    }

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

    // 词缀列表 → 存档数据（SaveManager 保存时调用）
    public static List<AffixSaveData> ToSaveData(GeneratedEquipmentAffix[] affixes)
    {
        if (affixes == null || affixes.Length == 0)
            return null;

        var list = new List<AffixSaveData>();
        foreach (var a in affixes)
        {
            if (a == null)
                continue;

            var d = new AffixSaveData
            {
                displayName = a.displayName,
                tier = (int)a.tier,
                isPrefix = a.isPrefix,
                modifiers = new List<ModifierSaveData>()
            };

            if (a.modifiers != null)
            {
                foreach (var m in a.modifiers)
                {
                    d.modifiers.Add(new ModifierSaveData
                    {
                        statType = (int)m.statType,
                        value = m.value,
                        isPercentage = m.isPercentage
                    });
                }
            }
            list.Add(d);
        }
        return list.Count > 0 ? list : null;
    }

    // 存档数据 → 词缀列表（SaveManager 读档时调用，精确恢复不重新随机）
    public static GeneratedEquipmentAffix[] FromSaveData(List<AffixSaveData> saveData)
    {
        if (saveData == null || saveData.Count == 0)
            return null;

        var list = new List<GeneratedEquipmentAffix>();
        foreach (var s in saveData)
        {
            if (s == null)
                continue;

            var modifiers = new List<ItemModifier>();
            bool hasNegative = false;
            if (s.modifiers != null)
            {
                foreach (var m in s.modifiers)
                {
                    if (m.value < 0)
                        hasNegative = true; // 存档中任一负值段 → 标记负面词缀（UI 警示色）
                    modifiers.Add(new ItemModifier
                    {
                        statType = (StatType)m.statType,
                        value = m.value,
                        isPercentage = m.isPercentage
                    });
                }
            }

            list.Add(new GeneratedEquipmentAffix
            {
                displayName = s.displayName,
                tier = (AffixTier)s.tier,
                isPrefix = s.isPrefix,
                modifiers = modifiers.Count > 0 ? modifiers.ToArray() : null,
                hasNegative = hasNegative
            });
        }
        return list.Count > 0 ? list.ToArray() : null;
    }
}