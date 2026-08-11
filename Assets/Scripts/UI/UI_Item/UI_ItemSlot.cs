using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

// 物品槽位。继承自 UI_BaseSlot，追加拖拽、双击装备、右键消耗品功能。
// 子类：UI_InventorySlot、UI_EquipSlot。
public class UI_ItemSlot : UI_BaseSlot, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    protected Inventory_Player inventory;

    private CanvasGroup canvasGroup;

    public event Action<Inventory_Item, UI_ItemSlot> OnItemSlotClicked;
    public event Action<Inventory_Item> OnItemSlotDoubleClicked;

    protected override void Awake()
    {
        base.Awake();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(Inventory_Player playerInventory)
    {
        inventory = playerInventory;
    }

    protected void NotifyItemSlotClicked(Inventory_Item item)
    {
        AudioManager.Instance?.PlayButtonSfx();
        OnItemSlotClicked?.Invoke(item, this);
    }

    protected void NotifyItemSlotDoubleClicked(Inventory_Item item)
    {
        OnItemSlotDoubleClicked?.Invoke(item);
    }

    // ==================== 点击 ====================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (itemInSlot == null && ui != null && ui.itemToolTip != null)
            ui.itemToolTip.ShowToolTip(false, null);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (itemInSlot == null)
            return;

        // 左键单点——供商城面板等外部监听
        if (eventData.button == PointerEventData.InputButton.Left)
            NotifyItemSlotClicked(itemInSlot);

        // 消耗品右键使用
        if (itemInSlot.IsConsumable && eventData.button == PointerEventData.InputButton.Right)
        {
            UseConsumable();
            return;
        }

        // 双击——对所有物品生效（背包=装备/存入仓库、仓库=取回，由监听方决定）
        if (eventData.clickCount == 2)
            NotifyItemSlotDoubleClicked(itemInSlot);
    }

    protected virtual void UseConsumable()
    {
        PlayerInventorySystem playerInventorySystem = FindAnyObjectByType<PlayerInventorySystem>();
        if (playerInventorySystem != null)
            playerInventorySystem.TryUseConsumable(itemInSlot);
    }

    // ==================== 拖拽 ====================

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (itemInSlot == null)
            return;

        if (UI_ItemDragHandler.Instance != null)
            UI_ItemDragHandler.Instance.StartDrag(itemInSlot, this, transform.position);

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (UI_ItemDragHandler.Instance != null && UI_ItemDragHandler.Instance.IsDragging)
            UI_ItemDragHandler.Instance.UpdateDragPosition(eventData.position);
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (UI_ItemDragHandler.Instance != null)
            UI_ItemDragHandler.Instance.EndDrag(eventData.position);
    }
}
