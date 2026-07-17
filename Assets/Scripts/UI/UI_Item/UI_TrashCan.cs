using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_TrashCan : MonoBehaviour, IItemDropTarget, IPointerEnterHandler, IPointerExitHandler
{
    [Header("垃圾桶设置")]
    [SerializeField] private Image trashIcon;
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.red;
    [SerializeField] private float highlightAlpha = 0.5f;

    private Color originalColor;

    private void Awake()
    {
        if (background != null)
        {
            originalColor = background.color;
        }
    }

    public bool CanAcceptItem(Inventory_Item item)
    {
        return item != null;
    }

    public void OnItemDropped(Inventory_Item item, UI_ItemSlot sourceSlot)
    {
        if (item == null)
            return;

        PlayerInventorySystem playerInventorySystem = FindObjectOfType<PlayerInventorySystem>();

        if (playerInventorySystem == null)
        {
            Debug.LogError("PlayerInventorySystem 未找到");
            return;
        }

        var inventory = playerInventorySystem.GetInventory();

        if (inventory == null)
        {
            Debug.LogError("Inventory 未找到");
            return;
        }

        if (sourceSlot is UI_EquipSlot)
        {
            playerInventorySystem.TryUnequipItem(item);
        }

        inventory.RemoveItem(item);
        Debug.Log($"物品 {item.itemData.itemName} 已销毁");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UI_ItemDragHandler.Instance != null && UI_ItemDragHandler.Instance.IsDragging && background != null)
        {
            Color color = highlightColor;
            color.a = highlightAlpha;
            background.color = color;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (background != null)
        {
            background.color = originalColor;
        }
    }
}
