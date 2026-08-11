using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class UI_InventorySlot : UI_ItemSlot, IItemDropTarget
{
    [SerializeField] private int slotIndex = -1;//槽位索引

    protected override void Awake()
    {
        base.Awake();
    }

    public override bool CanAcceptItem(Inventory_Item item)//判断是否可以接受物品
    {
        if (item == null)
            return false;

        //不能拖拽到自己
        if (item == itemInSlot)
            return false;

        //检查是否从装备槽拖拽
        bool isDraggingFromEquipment = UI_ItemDragHandler.Instance != null && UI_ItemDragHandler.Instance.IsDraggingFromEquipment;

        //检查是否从仓库拖拽（取出）
        bool isDraggingFromWarehouse = UI_ItemDragHandler.Instance != null && UI_ItemDragHandler.Instance.IsDraggingFromWarehouse;

        //仓库物品只能放入空背包槽（取出不交换）
        if (isDraggingFromWarehouse)
            return itemInSlot == null;

        //如果槽位已有物品
        if (itemInSlot != null)
        {
            //从装备槽拖拽到背包槽位，不能交换
            if (isDraggingFromEquipment)
            {
                return false;
            }

            //从背包槽位拖拽到背包槽位，可以交换
            return true;
        }

        //空槽位可以接受任何物品
        return true;
    }

    public void Initialize(Inventory_Player playerInventory, int index)
    {
        base.Initialize(playerInventory);
        slotIndex = index;
    }

    //处理物品掉落
    public void OnItemDropped(Inventory_Item item, UI_ItemSlot sourceSlot)
    {
        PlayerInventorySystem playerInventorySystem = FindAnyObjectByType<PlayerInventorySystem>();

        if (playerInventorySystem == null)
        {
            Debug.LogError("PlayerInventorySystem 未找到");
            return;
        }

        if (sourceSlot is UI_EquipSlot)
        {
            bool success = playerInventorySystem.TryUnequipItemToSlot(item, slotIndex);
        }
        else if (sourceSlot is UI_InventorySlot sourceInventorySlot)
        {
            HandleInventoryToInventory(item, sourceInventorySlot);
        }
        else if (sourceSlot is UI_WarehouseSlot sourceWarehouseSlot)
        {
            // 仓库 → 背包：取出到当前空槽
            var ws = sourceWarehouseSlot.warehouseSystem ?? FindAnyObjectByType<WarehouseSystem>();
            if (ws != null)
                ws.WithdrawToBackpack(item, sourceWarehouseSlot.GetSlotIndex(), slotIndex);
        }
    }

    //处理库存到库存的物品交换
    private void HandleInventoryToInventory(Inventory_Item item, UI_InventorySlot sourceSlot)
    {
        if (inventory == null)
        {
            Debug.LogError("Inventory 未初始化");
            return;
        }

        if (sourceSlot.slotIndex == -1 || slotIndex == -1)
        {
            Debug.LogError("槽位索引未设置");
            return;
        }

        // 使用新的字典存储系统进行物品交换
        if (itemInSlot == null)
        {
            // 移动物品到空槽位
            inventory.MoveItem(sourceSlot.slotIndex, slotIndex);
        }
        else
        {
            // 交换两个槽位的物品
            if (!inventory.SwapItems(sourceSlot.slotIndex, slotIndex))
            {
                Debug.LogWarning($"交换物品失败");
            }
        }
    }

    public int GetSlotIndex()//获取槽位索引
    {
        return slotIndex;
    }
}
