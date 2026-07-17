using UnityEngine;

public interface IItemDropTarget
{
    bool CanAcceptItem(Inventory_Item item);
    void OnItemDropped(Inventory_Item item, UI_ItemSlot sourceSlot);
}
