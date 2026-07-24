using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

// 商店物品槽位（NPC 商品/玩家物品通用）。
// 继承自 UI_BaseSlot，追加选中高亮和槽位内金/银/铜价格显示。
// 不含拖拽/双击装备/右键消耗品。
public class UI_ShopSlot : UI_BaseSlot
{
    [Header("选中高亮")]
    [SerializeField] private Image selectionBorder;

    [Header("价格显示（金/银/铜）")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private Image goldIcon;
    [SerializeField] private TextMeshProUGUI silverText;
    [SerializeField] private Image silverIcon;
    [SerializeField] private TextMeshProUGUI copperText;
    [SerializeField] private Image copperIcon;

    public ItemDataSo ItemData { get; private set; }

    // 当前模式（Buying = NPC售价，Selling = 回收价）
    public ShopMode Mode { get; private set; }

    // 点击选中时触发，ShopPanel 监听
    public event System.Action<UI_ShopSlot> OnSlotSelected;

    public enum ShopMode { Buying, Selling }

    private Inventory_Item displayItem;
    private float buyBackRate = 0.5f;

    protected override void Awake()
    {
        base.Awake();

        if (selectionBorder != null)
            selectionBorder.enabled = false;

        HidePrice();
    }

    // 初始化槽位数据
    public void Setup(ItemDataSo data, ShopMode mode, float rate = 0.5f, int quantity = -1)
    {
        ItemData = data;
        Mode = mode;
        buyBackRate = rate;

        displayItem = new Inventory_Item(data);
        UpdateSlot(displayItem);

        UpdatePriceDisplay();
        UpdateStockDisplay(quantity);
    }

    // 更新库存显示：-1 = 无限（隐藏），>=0 = 显示剩余
    // 复用基类的 itemStackSize 文本
    public void UpdateStockDisplay(int quantity)
    {
        if (itemStackSize == null) return;

        if (quantity >= 0)
        {
            itemStackSize.text = quantity.ToString();
            itemStackSize.gameObject.SetActive(true);
        }
        else
        {
            itemStackSize.gameObject.SetActive(false);
        }
    }

    // 获取显示价格（铜币单位）
    public int GetPrice()
    {
        if (ItemData == null) return 0;
        return Mode == ShopMode.Buying
            ? ItemData.value
            : Mathf.RoundToInt(ItemData.value * buyBackRate);
    }

    // 更新槽位内金/银/铜显示
    // 货币图标已在预制体上直接拖入，运行时不修改 sprite
    private void UpdatePriceDisplay()
    {
        int price = GetPrice();
        if (price <= 0)
        {
            HidePrice();
            return;
        }

        var amt = CurrencyFormatter.Split(price);

        if (goldText != null)
        {
            goldText.text = amt.gold.ToString();
            goldText.gameObject.SetActive(amt.HasGold);
        }
        if (goldIcon != null)
            goldIcon.enabled = amt.HasGold;

        if (silverText != null)
        {
            silverText.text = amt.silver.ToString();
            silverText.gameObject.SetActive(amt.HasSilver);
        }
        if (silverIcon != null)
            silverIcon.enabled = amt.HasSilver;

        if (copperText != null)
        {
            copperText.text = amt.copper.ToString();
            copperText.gameObject.SetActive(amt.HasCopper);
        }
        if (copperIcon != null)
            copperIcon.enabled = amt.HasCopper;
    }

    private void HidePrice()
    {
        if (goldText != null) goldText.gameObject.SetActive(false);
        if (goldIcon != null) goldIcon.enabled = false;
        if (silverText != null) silverText.gameObject.SetActive(false);
        if (silverIcon != null) silverIcon.enabled = false;
        if (copperText != null) copperText.gameObject.SetActive(false);
        if (copperIcon != null) copperIcon.enabled = false;
    }

    // 设置选中状态。控制自己的 selectionBorder 边框。
    public override void SetSelected(bool selected)
    {
        base.SetSelected(selected);
        if (selectionBorder != null)
            selectionBorder.enabled = selected;
    }

    // ==================== 点击选中 ====================

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        base.OnPointerClick(eventData);
        OnSlotSelected?.Invoke(this);
    }
}
