using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


public class Inventory_Player : Inventory_Base
{

    protected override void Awake()
    {
        base.Awake();
    }

    public bool TryRemoveItem(Inventory_Item itemToRemove)
    {
        // 检查物品是否在字典中
        foreach (var kvp in itemDictionary)
        {
            if (kvp.Value == itemToRemove)
            {
                RemoveItem(itemToRemove);
                return true;
            }
        }
        return false;
    }

    // 存档：按 ItemDataSo.itemId 查找背包中的物品
    public Inventory_Item FindItemByItemId(string itemId)
    {
        foreach (var kvp in itemDictionary)
        {
            if (kvp.Value != null && kvp.Value.itemData != null && kvp.Value.itemData.itemId == itemId)
                return kvp.Value;
        }
        return null;
    }
}