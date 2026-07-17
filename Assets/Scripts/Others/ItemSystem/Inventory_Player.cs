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

}