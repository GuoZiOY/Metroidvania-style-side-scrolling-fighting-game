using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

public class UI_ItemSlot : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Inventory_Item itemInSlot { get; protected set; }
    protected Inventory_Player inventory;
    protected UI ui;
    protected RectTransform rect;

    public event Action<Inventory_Item> OnItemSlotClicked;
    public event Action<Inventory_Item> OnItemSlotDoubleClicked;

    [Header("背包槽位设置")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemStackSize;

    [Header("高亮设置")]
    [SerializeField] protected Image background;
    [SerializeField] protected Color highlightColor = Color.yellow;
    protected Color originalColor;

    [Header("稀有度背景设置")]
    [SerializeField] private Image rarityBackground; //稀有度背景Image组件
    [SerializeField] private float rarityBackgroundAlpha = 0.5f; //稀有度背景透明度

    private CanvasGroup canvasGroup;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        ui = GetComponentInParent<UI>();
        rect = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if (background != null)
        {
            originalColor = background.color;
        }
    }

    public void Initialize(Inventory_Player playerInventory)
    {
        inventory = playerInventory;
    }

    protected void NotifyItemSlotClicked(Inventory_Item item)
    {
        OnItemSlotClicked?.Invoke(item);
    }

    protected void NotifyItemSlotDoubleClicked(Inventory_Item item)
    {
        OnItemSlotDoubleClicked?.Invoke(item);
    }

    public virtual void OnPointerDown(PointerEventData eventData)
    { 
        if (itemInSlot == null)
            ui.itemToolTip.ShowToolTip(false,null);
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (itemInSlot == null)
            return;

        if (itemInSlot.IsConsumable)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                UseConsumable();
            }
            return;
        }

        if (itemInSlot.IsEquipment == false)
        {
            return;
        }

        if (eventData.clickCount == 2)
        {
            NotifyItemSlotDoubleClicked(itemInSlot);
        }
    }

    protected virtual void UseConsumable()//使用消耗品
    {
        PlayerInventorySystem playerInventorySystem = FindAnyObjectByType<PlayerInventorySystem>();
        if (playerInventorySystem != null)
        {
            playerInventorySystem.TryUseConsumable(itemInSlot);
        }
    }

    public virtual void UpdateSlot(Inventory_Item item)
    {
        itemInSlot = item;

        if (itemInSlot == null)
        {
            itemStackSize.text = "";
            itemIcon.enabled = false;
            //清空稀有度背景
            if (rarityBackground != null)
            {
                rarityBackground.enabled = false;
            }
        }
        else
        {
            itemIcon.enabled = true;
            Color color = Color.white;
            color.a = 1f;
            itemIcon.color = color;
            itemIcon.sprite = itemInSlot.itemData.itemIcon;
            itemStackSize.text = item.currentStackSize > 1 ? item.currentStackSize.ToString() : "";
            
            //应用稀有度背景颜色
            UpdateRarityBackground();
        }
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (itemInSlot == null)
            return;

        if (UI_ItemDragHandler.Instance != null)
        {
            UI_ItemDragHandler.Instance.StartDrag(itemInSlot, this, transform.position);
        }

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (UI_ItemDragHandler.Instance != null && UI_ItemDragHandler.Instance.IsDragging)
        {
            UI_ItemDragHandler.Instance.UpdateDragPosition(eventData.position);
        }
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (UI_ItemDragHandler.Instance != null)
        {
            UI_ItemDragHandler.Instance.EndDrag(eventData.position);
        }
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {

        if (itemInSlot != null)
            ui.itemToolTip.ShowToolTip(true,rect,itemInSlot);

        if (UI_ItemDragHandler.Instance == null || !UI_ItemDragHandler.Instance.IsDragging)
            return;

        Inventory_Item draggedItem = UI_ItemDragHandler.Instance.DraggingItem;

        if (CanAcceptItem(draggedItem) && background != null)
            background.color = highlightColor;
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        ui.itemToolTip.ShowToolTip(false,null);

        if (background != null)
            background.color = originalColor;
    }

    public virtual bool CanAcceptItem(Inventory_Item item)
    {   
        return item != null;
    }

    private void UpdateRarityBackground() //更新稀有度背景颜色
    {
        if (rarityBackground == null || itemInSlot == null)
            return;

        //获取物品的稀有度
        LootRarity rarity;
        if (itemInSlot.actualRarity.HasValue)
        {
            //优先使用实际稀有度（掉落物品）
            rarity = itemInSlot.actualRarity.Value;
        }
        else
        {
            //使用物品数据的基准稀有度
            rarity = itemInSlot.itemData.rarity;
        }

        //获取稀有度颜色并应用透明度
        Color rarityColor = RarityCalculator.GetRarityColor(rarity);
        rarityColor.a = rarityBackgroundAlpha;

        //应用颜色到背景
        rarityBackground.enabled = true;
        rarityBackground.color = rarityColor;
    }
}
