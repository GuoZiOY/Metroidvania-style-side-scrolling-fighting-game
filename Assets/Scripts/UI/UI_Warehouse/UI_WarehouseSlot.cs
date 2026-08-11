using UnityEngine;
using UnityEngine.EventSystems;

// 仓库槽位：绑定仓库容器 + 实际槽位索引。
// 既是拖拽源（取出），也是放置目标（存入：背包→仓库；仓库内部交换）。
public class UI_WarehouseSlot : UI_ItemSlot, IItemDropTarget
{
    [SerializeField] private int slotIndex = -1; // 仓库实际槽位索引

    public WarehouseSystem warehouseSystem { get; private set; } // 所属仓库系统

    // 初始化：绑定仓库系统与槽位索引（由 UI_WarehousePanel 调用）
    public void InitializeWarehouse(WarehouseSystem system, int index)
    {
        warehouseSystem = system;
        slotIndex = index;
    }

    public int GetSlotIndex() => slotIndex;

    public override bool CanAcceptItem(Inventory_Item item)//判断是否可以接收（存入到本槽）
    {
        if (item == null)
            return false;

        // 不能拖到自己
        if (item == itemInSlot)
            return false;

        if (warehouseSystem == null || warehouseSystem.WarehouseInventory == null)
            return false;

        // 仓库内部拖拽 = 交换位置：任意槽都可接收
        if (UI_ItemDragHandler.Instance != null && UI_ItemDragHandler.Instance.IsDraggingFromWarehouse)
            return true;

        var wh = warehouseSystem.WarehouseInventory;

        // 本槽是真实仓库槽（固定视图）：空槽可放，或与已有同类堆整堆合并
        if (slotIndex >= 0)
        {
            Inventory_Item existing = wh.GetItemAtSlot(slotIndex);
            if (existing == null)
                return true;
            return existing.itemData == item.itemData && existing.CanAddStack()
                && existing.currentStackSize + item.currentStackSize <= existing.MaxStackSize;
        }

        // slotIndex == -1（排序视图的空位/未覆盖槽）：仓库有空位或可合并即可
        return wh.GetFirstAvailableSlot() != -1 || wh.CanAddToStack(item);
    }

    // 处理放置：背包→存入本槽；装备→卸装存入；仓库→内部交换
    public void OnItemDropped(Inventory_Item item, UI_ItemSlot sourceSlot)
    {
        if (warehouseSystem == null)
            return;

        if (sourceSlot is UI_InventorySlot sourceInventorySlot)
        {
            warehouseSystem.DepositFromBackpack(item, sourceInventorySlot.GetSlotIndex(), slotIndex);
        }
        else if (sourceSlot is UI_EquipSlot)
        {
            // 装备 → 仓库：直接从装备槽取下并存入（装备不可堆叠，放到首个空槽）
            warehouseSystem.DepositFromEquipment(item);
        }
        else if (sourceSlot is UI_WarehouseSlot sourceWarehouseSlot)
        {
            warehouseSystem.SwapWithinWarehouse(sourceWarehouseSlot.slotIndex, slotIndex);
        }
    }

    // 仓库槽：左键选中；双击直接取回背包（到背包首个空槽/合并）
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (itemInSlot == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
            NotifyItemSlotClicked(itemInSlot);

        if (eventData.clickCount == 2 && warehouseSystem != null)
            warehouseSystem.WithdrawToBackpack(itemInSlot, slotIndex, -1);
    }
}
