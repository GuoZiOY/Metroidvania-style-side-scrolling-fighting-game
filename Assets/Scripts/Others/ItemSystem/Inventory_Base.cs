using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Inventory_Base : MonoBehaviour
{
    public event Action OnInventoryUpdated;//物品更新事件

    public int maxInventorySize = 10;
    public Dictionary<int, Inventory_Item> itemDictionary = new Dictionary<int, Inventory_Item>();//物品字典，键为槽位索引，值为物品
    public List<Inventory_Item> itemList => GetItemList();//兼容性属性，返回物品列表

    protected virtual void Awake()
    {
        itemDictionary = new Dictionary<int, Inventory_Item>();//初始化物品字典
    }

    //获取物品列表（兼容性方法）
    private List<Inventory_Item> GetItemList()
    {
        List<Inventory_Item> list = new List<Inventory_Item>();
        foreach (var kvp in itemDictionary)
        {
            if (kvp.Value != null)
                list.Add(kvp.Value);
        }
        return list;
    }

    public bool CanAddItem()//判断是否可以添加物品
    {
        return itemDictionary.Count < maxInventorySize;
    }

    public bool CanAddToStack(Inventory_Item item)//判断是否可以添加物品数量
    {
        foreach (var kvp in itemDictionary)
        {
            Inventory_Item stack = kvp.Value;
            if (stack != null && stack.itemData == item.itemData && stack.CanAddStack())
                return true;
        }
        return false;
    }

    public void AddItem(Inventory_Item itemToAdd)//添加物品到第一个可用槽位
    {
        AddItem(itemToAdd, GetFirstAvailableSlot());
    }

    public void AddItem(Inventory_Item itemToAdd, int slotIndex)//添加物品到指定槽位
    {
        if (slotIndex < 0 || slotIndex >= maxInventorySize)
        {
            Debug.LogError("无效的槽位索引: " + slotIndex);
            return;
        }

        Inventory_Item itemInInventory = FindItem(itemToAdd.itemData);//查找是否存在相同物品

        if (itemInInventory != null && itemInInventory.CanAddStack())
        {
            itemInInventory.AddStack();//添加物品数量
        }
        else
        {
            if (itemDictionary.ContainsKey(slotIndex))//判断槽位是否已被占用
            {
                Debug.LogWarning("槽位 " + slotIndex + " 已被占用，无法添加物品");
                return;
            }
            itemDictionary[slotIndex] = itemToAdd;//添加物品到字典
        }
        OnInventoryUpdated?.Invoke();//物品更新事件
    }

    public void RemoveItem(Inventory_Item itemToRemove)//移除物品
    {
        int slotToRemove = GetItemSlot(itemToRemove);

        if (slotToRemove != -1)
        {
            itemDictionary.Remove(slotToRemove);//移除物品从字典
            OnInventoryUpdated?.Invoke();//物品更新事件
        }
    }

    public void RemoveItemAtSlot(int slotIndex)//从指定槽位移除物品
    {
        if (itemDictionary.ContainsKey(slotIndex))
        {
            itemDictionary.Remove(slotIndex);
            OnInventoryUpdated?.Invoke();
        }
    }

    public Inventory_Item FindItem(ItemDataSo itemData)//获取物品
    {
        foreach (var kvp in itemDictionary)
        {
            if (kvp.Value != null && kvp.Value.itemData == itemData)
                return kvp.Value;
        }
        return null;
    }

    public Inventory_Item GetItemAtSlot(int slotIndex)//获取指定槽位的物品
    {
        if (itemDictionary.ContainsKey(slotIndex))
            return itemDictionary[slotIndex];

        return null;
    }

    public bool MoveItem(int fromSlot, int toSlot)//移动物品到指定槽位
    {
        if (!itemDictionary.ContainsKey(fromSlot) || itemDictionary.ContainsKey(toSlot))
            return false;

        Inventory_Item item = itemDictionary[fromSlot];
        itemDictionary.Remove(fromSlot);
        itemDictionary[toSlot] = item;
        OnInventoryUpdated?.Invoke();
        return true;
    }

    public bool SwapItems(int slotA, int slotB)//交换两个槽位的物品
    {
        if (!itemDictionary.ContainsKey(slotA) && !itemDictionary.ContainsKey(slotB))
            return false;

        Inventory_Item itemA = itemDictionary.ContainsKey(slotA) ? itemDictionary[slotA] : null;
        Inventory_Item itemB = itemDictionary.ContainsKey(slotB) ? itemDictionary[slotB] : null;

        if (itemA != null)
            itemDictionary[slotB] = itemA;
        else
            itemDictionary.Remove(slotB);

        if (itemB != null)
            itemDictionary[slotA] = itemB;
        else
            itemDictionary.Remove(slotA);

        OnInventoryUpdated?.Invoke();
        return true;
    }

    public int GetFirstAvailableSlot()//获取第一个可用槽位
    {
        for (int i = 0; i < maxInventorySize; i++)
        {
            if (!itemDictionary.ContainsKey(i))
                return i;
        }
        return -1;
    }

    public bool IsSlotEmpty(int slotIndex) => !itemDictionary.ContainsKey(slotIndex);//检查槽位是否为空

    public void UpdateItemList(List<Inventory_Item> newItemList)//更新物品列表（兼容性方法）
    {
        itemDictionary.Clear();
        for (int i = 0; i < newItemList.Count && i < maxInventorySize; i++)
        {
            if (newItemList[i] != null)
                itemDictionary[i] = newItemList[i];
        }
        OnInventoryUpdated?.Invoke();
    }

    public int GetItemCount() => itemDictionary.Count;//获取物品数量    

    public void TriggerInventoryUpdate()//触发背包更新事件
    {
        OnInventoryUpdated?.Invoke();
    }

    public List<int> GetOccupiedSlots() => new List<int>(itemDictionary.Keys);//获取已占用的槽位列表

    public int GetItemSlot(Inventory_Item item)//获取物品所在的槽位索引
    {
        if (item == null)
            return -1;

        foreach (var kvp in itemDictionary)
        {
            if (kvp.Value == item)
                return kvp.Key;
        }
        return -1;
    }
}
