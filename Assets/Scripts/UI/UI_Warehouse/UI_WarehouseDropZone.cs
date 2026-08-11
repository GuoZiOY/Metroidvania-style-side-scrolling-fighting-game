using UnityEngine;

// 仓库存放区：覆盖仓库网格区域的放置目标（需挂在一个带 Graphic 的对象上以接收射线）。
// 背包物品拖到仓库任意空白处即可存入（首个空槽/整堆合并），即使仓库为空也可接收。
public class UI_WarehouseDropZone : MonoBehaviour, IItemDropTarget
{
    private WarehouseSystem warehouseSystem;

    // 绑定仓库系统（由 UI_WarehousePanel 在打开时调用）
    public void Init(WarehouseSystem system) => warehouseSystem = system;

    public bool CanAcceptItem(Inventory_Item item)
    {
        if (item == null || warehouseSystem?.WarehouseInventory == null)
            return false;
        return warehouseSystem.WarehouseInventory.CanAddItem(item.itemData);
    }

    public void OnItemDropped(Inventory_Item item, UI_ItemSlot sourceSlot)
    {
        if (warehouseSystem == null)
            return;

        if (sourceSlot is UI_InventorySlot sourceInvSlot)
            warehouseSystem.DepositFromBackpack(item, sourceInvSlot.GetSlotIndex());
        else if (sourceSlot is UI_EquipSlot)
            warehouseSystem.DepositFromEquipment(item); // 装备 → 仓库
    }
}
