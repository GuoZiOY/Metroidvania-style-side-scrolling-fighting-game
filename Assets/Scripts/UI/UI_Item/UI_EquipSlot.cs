using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_EquipSlot : UI_ItemSlot, IPointerClickHandler, IItemDropTarget
{
    public ItemType slotType;
    [SerializeField] private int typeSlotIndex = 0;//同类型槽位索引（如饰品槽1=0，饰品槽2=1）
    private EquipmentSystem equipmentSystem;

    [Header("高亮设置")]
    [SerializeField] private Image background;
    [SerializeField] private Color highlightColor = Color.yellow;
    private Color originalColor;

    private void OnValidate()
    {
        gameObject.name = "装备-" + slotType.ToString();
    }

    protected override void Awake()
    {
        base.Awake();
        if (background != null)
            originalColor = background.color;
    }

    public void Initialize(EquipmentSystem playerEquipmentSystem)
    {
        equipmentSystem = playerEquipmentSystem;
    }

    public int GetTypeSlotIndex()//获取同类型槽位索引
    {
        return typeSlotIndex;
    }

    public bool TryUnequipItemFromSlot()
    {
        if (itemInSlot == null)
            return false;

        if (equipmentSystem == null)
            return false;

        bool success = equipmentSystem.TryUnequipItem(itemInSlot);

        if (success)
        {
            Debug.Log($"已从槽位 {name} 卸下装备");
        }

        return success;
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (itemInSlot == null) return;

        // 左键——通知点击（供商店面板监听选中，含槽位引用）
        if (eventData.button == PointerEventData.InputButton.Left)
            NotifyItemSlotClicked(itemInSlot); // 内部会传 this

        if (eventData.clickCount == 2)
            NotifyItemSlotDoubleClicked(itemInSlot);
    }

    public override void UpdateSlot(Inventory_Item item)
    {
        base.UpdateSlot(item);

        if (itemInSlot == null)
        {
            if (background != null) background.enabled = true;
        }
        else
        {
            if (background != null) background.enabled = false;
        }
    }

    public override bool CanAcceptItem(Inventory_Item item)
    {
        if (item == null)
            return false;

        if (!item.IsEquipment)
        {
            Debug.Log("该物品不是装备，无法装备到装备槽");
            return false;
        }

        if (item.itemData.itemType != this.slotType)
        {
            Debug.Log($"物品类型 {item.itemData.itemType} 与槽位类型 {slotType} 不匹配");
            return false;
        }

        return true;
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (background != null)
            background.color = highlightColor;
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        if (background != null)
            background.color = originalColor;
    }

    // 槽位隐藏时恢复背景色（面板开关不触发 OnPointerExit，防悬停高亮残留）
    protected override void OnDisable()
    {
        base.OnDisable();
        if (background != null)
            background.color = originalColor;
    }

    public void OnItemDropped(Inventory_Item item, UI_ItemSlot sourceSlot)
    {
        PlayerInventorySystem playerInventorySystem = FindAnyObjectByType<PlayerInventorySystem>();

        if (playerInventorySystem == null)
        {
            Debug.LogError("PlayerInventorySystem 未找到");
            return;
        }

        if (sourceSlot is UI_EquipSlot sourceEquipSlot)
        {
            //装备槽之间的拖拽 - 交换或移动
            if (sourceEquipSlot.slotType != this.slotType)
            {
                Debug.Log($"不能在不同类型的装备槽之间拖拽：{sourceEquipSlot.slotType} -> {this.slotType}");
                return;
            }

            //交换同类型装备槽位的物品
            bool success = playerInventorySystem.TrySwapEquipmentSlots(this.slotType, sourceEquipSlot.GetTypeSlotIndex(), this.typeSlotIndex);

            if (success)
            {
                Debug.Log($"成功交换装备槽位：{sourceEquipSlot.name} -> {this.name}");
            }
            else
            {
                Debug.LogWarning($"交换装备槽位失败");
            }
        }
        else if (sourceSlot is UI_InventorySlot)
        {
            //从背包拖拽到装备槽 - 精准装备到指定槽位
            bool success = playerInventorySystem.TryEquipToSlot(item, this.typeSlotIndex);

            if (success)
            {
                Debug.Log($"成功将物品 {item.itemData.itemName} 装备到槽位 {this.slotType}[{this.typeSlotIndex}]");
            }
            else
            {
                Debug.LogWarning($"物品 {item.itemData.itemName} 装备失败");
            }
        }
    }
}
