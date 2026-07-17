using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EquipmentSystem : MonoBehaviour
{
    public event Action OnEquipmentUpdated;//装备更新事件

    [System.Serializable]
    public class SlotConfig//装备槽位配置
    {
        public ItemType slotType;
        public int maxSlots;
    }

    [SerializeField] private List<SlotConfig> slotConfigs;//装备槽位配置列表
    [SerializeField] private Entity_Stats playerStats;//玩家统计数据
    [SerializeField] private List<Inventory_Item> equippedItems;//兼容性字段，用于序列化

    //新的字典存储系统
    private Dictionary<ItemType, List<Inventory_EquipmentSlot>> slotDictionary;//装备槽位字典
    private Dictionary<ItemType, List<Inventory_Item>> equipmentDictionary;//装备物品字典   

    private void Awake()
    {
        InitializeSlots();
    }

    //初始化装备槽位
    private void InitializeSlots()
    {
        slotDictionary = new Dictionary<ItemType, List<Inventory_EquipmentSlot>>();
        equipmentDictionary = new Dictionary<ItemType, List<Inventory_Item>>();
        equippedItems = new List<Inventory_Item>();//兼容性初始化
        
        if (slotConfigs != null)
        {
            foreach (var config in slotConfigs)
            {
                //为每种装备类型创建槽位列表
                if (!slotDictionary.ContainsKey(config.slotType))
                {
                    slotDictionary[config.slotType] = new List<Inventory_EquipmentSlot>();
                    equipmentDictionary[config.slotType] = new List<Inventory_Item>();
                }

                //创建指定数量的槽位
                for (int i = 0; i < config.maxSlots; i++)
                {
                    slotDictionary[config.slotType].Add(new Inventory_EquipmentSlot { slotType = config.slotType, slotIndex = i });
                    equipmentDictionary[config.slotType].Add(null);//初始化为空槽位
                }
            }
        }
    }

    //验证装备物品的有效性，返回物品类型
    private ItemType ValidateEquipmentItem(Inventory_Item item)
    {
        if (item == null || !item.IsEquipment)
            return ItemType.None;

        ItemType itemType = item.itemData.itemType;

        //检查是否有该类型的槽位配置
        if (!slotDictionary.ContainsKey(itemType) || slotDictionary[itemType].Count == 0)
            return ItemType.None;

        return itemType;
    }

    //查找指定类型的第一个空槽位索引
    private int GetFirstEmptySlotIndex(ItemType itemType)
    {
        if (!equipmentDictionary.ContainsKey(itemType))
            return -1;

        var items = equipmentDictionary[itemType];

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null)
                return i;
        }

        return -1;
    }

    //尝试装备物品，返回被替换的物品（如果有，替换第一个槽位的装备）
    public Inventory_Item TryEquipItem(Inventory_Item item)
    {
        ItemType itemType = ValidateEquipmentItem(item);

        if (itemType == ItemType.None)
            return null;

        var items = equipmentDictionary[itemType];

        //查找第一个空槽位
        int emptySlotIndex = GetFirstEmptySlotIndex(itemType);

        if (emptySlotIndex != -1)
        {
            //有空槽位，直接装备
            EquipItem(item, itemType, emptySlotIndex);
            return null;
        }
        else
        {
            //没有空槽位，替换第一个槽位的装备
            var itemToUnequip = items[0];
            UnequipItem(itemToUnequip);
            EquipItem(item, itemType, 0);

            return itemToUnequip;
        }
    }

    //尝试装备物品到指定槽位，返回被替换的物品（如果有）
    public Inventory_Item TryEquipItemToSlot(Inventory_Item item, int targetSlotIndex)
    {
        ItemType itemType = ValidateEquipmentItem(item);

        if (itemType == ItemType.None)
            return null;

        var items = equipmentDictionary[itemType];

        //检查槽位索引是否有效
        if (targetSlotIndex < 0 || targetSlotIndex >= items.Count)
            return null;

        //获取目标槽位的当前物品
        var itemToUnequip = items[targetSlotIndex];

        //移除目标槽位的旧装备（如果有）
        if (itemToUnequip != null)
            UnequipItem(itemToUnequip);

        //装备新物品到指定槽位
        EquipItem(item, itemType, targetSlotIndex);

        return itemToUnequip;
    }

    //装备物品到指定槽位
    private void EquipItem(Inventory_Item itemToEquip, ItemType itemType, int slotIndex)
    {
        if (!equipmentDictionary.ContainsKey(itemType) || slotIndex >= equipmentDictionary[itemType].Count)
        {
            Debug.LogError("无效的装备类型或槽位索引");
            return;
        }

        //更新字典存储
        equipmentDictionary[itemType][slotIndex] = itemToEquip;
        slotDictionary[itemType][slotIndex].equippedItem = itemToEquip;

        //更新兼容性列表
        UpdateCompatibilityList();

        //应用装备属性
        if (itemToEquip.Modifiers != null)
            itemToEquip.AddModifiers(playerStats);

        OnEquipmentUpdated?.Invoke();
    }

    //尝试取消装备物品，返回是否成功
    public bool TryUnequipItem(Inventory_Item itemToUnequip)
    {
        if (itemToUnequip == null) return false;

        //在字典中查找装备
        var (itemType, slotIndex) = FindEquipmentSlot(itemToUnequip);
        if (itemType != ItemType.None && slotIndex != -1)
        {
            UnequipItem(itemToUnequip);
            return true;
        }

        return false;
    }

    //取消装备物品
    private void UnequipItem(Inventory_Item itemToUnequip)
    {
        if (itemToUnequip == null) return;

        //在字典中查找并移除装备
        var (itemType, slotIndex) = FindEquipmentSlot(itemToUnequip);
        if (itemType != ItemType.None && slotIndex != -1)
        {
            //从字典中移除
            equipmentDictionary[itemType][slotIndex] = null;
            slotDictionary[itemType][slotIndex].equippedItem = null;

            //移除装备属性
            if (itemToUnequip.Modifiers != null)
                itemToUnequip.RemoveModifiers(playerStats);

            //更新兼容性列表
            UpdateCompatibilityList();

            OnEquipmentUpdated?.Invoke();
        }
    }

    //更新兼容性列表
    private void UpdateCompatibilityList()
    {
        equippedItems.Clear();
        foreach (var kvp in equipmentDictionary)
        {
            foreach (var item in kvp.Value)
            {
                if (item != null)
                    equippedItems.Add(item);
            }
        }
    }

    //兼容性方法：获取所有槽位
    public List<Inventory_EquipmentSlot> GetEquipmentSlots()
    {
        List<Inventory_EquipmentSlot> allSlots = new List<Inventory_EquipmentSlot>();
        foreach (var kvp in slotDictionary)
        {
            allSlots.AddRange(kvp.Value);
        }
        return allSlots;
    }

    //兼容性方法：获取所有已装备物品
    public List<Inventory_Item> GetEquippedItems() => equippedItems;

    //兼容性方法：获取指定类型的第一个已装备物品
    public Inventory_Item GetEquippedItem(ItemType slotType)
    {
        if (!equipmentDictionary.ContainsKey(slotType))
            return null;

        foreach (var item in equipmentDictionary[slotType])
        {
            if (item != null)
                return item;
        }
        return null;
    }

    //新方法：获取指定类型的所有已装备物品
    public List<Inventory_Item> GetEquippedItemsByType(ItemType itemType)
    {
        if (equipmentDictionary == null || !equipmentDictionary.ContainsKey(itemType))
            return new List<Inventory_Item>();

        List<Inventory_Item> items = new List<Inventory_Item>();
        foreach (var item in equipmentDictionary[itemType])
        {
            if (item != null)
                items.Add(item);
        }
        return items;
    }

    //新方法：获取指定类型的所有槽位
    public List<Inventory_EquipmentSlot> GetSlotsByType(ItemType itemType)
    {
        if (slotDictionary == null || !slotDictionary.ContainsKey(itemType))
            return new List<Inventory_EquipmentSlot>();
        return new List<Inventory_EquipmentSlot>(slotDictionary[itemType]);
    }

    //新方法：获取指定类型指定索引的槽位
    public Inventory_EquipmentSlot GetSlot(ItemType itemType, int slotIndex)
    {
        if (slotDictionary == null || !slotDictionary.ContainsKey(itemType) || slotIndex >= slotDictionary[itemType].Count)
        {
            return null;
        }
        return slotDictionary[itemType][slotIndex];
    }

    //新方法：获取指定类型指定索引的物品
    public Inventory_Item GetItem(ItemType itemType, int slotIndex)
    {
        if (equipmentDictionary == null || !equipmentDictionary.ContainsKey(itemType) || slotIndex >= equipmentDictionary[itemType].Count)
        {
            return null;
        }
        return equipmentDictionary[itemType][slotIndex];
    }

    //新方法：检查指定类型是否有空槽位
    public bool HasEmptySlot(ItemType itemType)
    {
        if (equipmentDictionary == null || !equipmentDictionary.ContainsKey(itemType))
            return false;

        foreach (var item in equipmentDictionary[itemType])
        {
            if (item == null)
                return true;
        }
        return false;
    }

    //新方法：获取所有支持的装备类型
    public List<ItemType> GetSupportedItemTypes()
    {
        if (slotDictionary == null)
            return new List<ItemType>();

        return new List<ItemType>(slotDictionary.Keys);
    }

    //新方法：检查是否支持指定装备类型
    public bool SupportsItemType(ItemType itemType) =>
        slotDictionary.ContainsKey(itemType) && slotDictionary[itemType].Count > 0;

    //交换同类型装备槽位的物品
    public bool SwapEquipmentSlots(ItemType itemType, int slotA, int slotB)
    {
        //检查槽位索引是否有效
        if (!equipmentDictionary.ContainsKey(itemType))
            return false;

        var items = equipmentDictionary[itemType];

        if (slotA < 0 || slotA >= items.Count || slotB < 0 || slotB >= items.Count)
            return false;

        //交换物品
        var tempItem = items[slotA];
        items[slotA] = items[slotB];
        items[slotB] = tempItem;

        //同步到槽位字典
        slotDictionary[itemType][slotA].equippedItem = items[slotA];
        slotDictionary[itemType][slotB].equippedItem = items[slotB];

        RecalculateModifiers(itemType);//重新计算属性加成
        UpdateCompatibilityList();//更新兼容性列表

        OnEquipmentUpdated?.Invoke();
        return true;
    }

    //重新计算指定类型的装备属性加成
    private void RecalculateModifiers(ItemType itemType)
    {
        //移除该类型的所有装备属性
        if (equipmentDictionary.ContainsKey(itemType))
        {
            foreach (var item in equipmentDictionary[itemType])
            {
                if (item != null && item.Modifiers != null)
                {
                    item.RemoveModifiers(playerStats);
                }
            }

            //重新添加装备属性
            foreach (var item in equipmentDictionary[itemType])
            {
                if (item != null && item.Modifiers != null)
                {
                    item.AddModifiers(playerStats);
                }
            }
        }
    }

    //调试属性：获取槽位字典（仅用于编辑器可视化）
    public Dictionary<ItemType, List<Inventory_EquipmentSlot>> GetSlotDictionary() => slotDictionary;

    //调试属性：获取装备字典（仅用于编辑器可视化）
    public Dictionary<ItemType, List<Inventory_Item>> GetEquipmentDictionary() => equipmentDictionary;

    //调试属性：获取槽位配置
    public List<SlotConfig> GetSlotConfigs() => slotConfigs;

    //查找装备所在的槽位（返回装备类型和槽位索引）
    private (ItemType itemType, int slotIndex) FindEquipmentSlot(Inventory_Item item)
    {
        if (item == null)
            return (ItemType.None, -1);

        foreach (var kvp in equipmentDictionary)
        {
            int slotIndex = kvp.Value.IndexOf(item);
            if (slotIndex != -1)
            {
                return (kvp.Key, slotIndex);
            }
        }
        return (ItemType.None, -1);
    }
}