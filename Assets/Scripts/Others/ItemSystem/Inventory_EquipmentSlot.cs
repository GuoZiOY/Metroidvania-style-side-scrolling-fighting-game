using UnityEngine;
using System;

[Serializable]
public class Inventory_EquipmentSlot
{
    public ItemType slotType;//装备槽位类型
    public int slotIndex;//槽位索引
    public Inventory_Item equippedItem;//已装备的物品

    public bool HasItem()
    {
        return equippedItem != null && equippedItem.itemData != null;//判断是否有物品
    }

}
