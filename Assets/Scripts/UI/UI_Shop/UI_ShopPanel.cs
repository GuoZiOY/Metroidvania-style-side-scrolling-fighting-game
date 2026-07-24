using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 商店面板。双栏布局：左 NPC 商品（UI_ShopSlot），右玩家背包+装备（移动已有槽位）。
// 打开商店时把"背包槽""装备槽"节点从角色面板移到商店右侧，关闭时移回。
// 底部：数量滑块 +/-/拖拽 + 固定购买/出售按钮 + 总价货币显示。
public class UI_ShopPanel : MonoBehaviour
{
    private static UI_ShopPanel instance;
    public static UI_ShopPanel Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<UI_ShopPanel>(FindObjectsInactive.Include);
            return instance;
        }
        private set => instance = value;
    }

    [Header("商店数据")]
    [SerializeField] private TextMeshProUGUI shopNameText;

    [Header("左栏——NPC 商品")]
    [SerializeField] private Transform npcItemContainer;
    [SerializeField] private GameObject shopSlotPrefab;    // UI_ShopSlot 预制体

    [Header("右栏——移动已有背包/装备槽")]
    [SerializeField] private Transform playerSlotContainer;
    [SerializeField] private Transform backpackSlotParent;   // 场景中的"背包槽"
    [SerializeField] private Transform equipSlotParent;      // 场景中的"装备槽"

    [Header("面板关联")]
    [SerializeField] private GameObject panelBackground;     // 全屏深色遮罩
    [SerializeField] private GameObject[] hiddenOnOpen;      // 打开时隐藏（如底部切换按钮组）

    [Header("数量控件")]
    [SerializeField] private Slider quantitySlider;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private int maxQuantity = 99;

    [Header("总价显示（金/银/铜）")]
    [SerializeField] private TextMeshProUGUI totalGoldText;
    [SerializeField] private Image totalGoldIcon;
    [SerializeField] private TextMeshProUGUI totalSilverText;
    [SerializeField] private Image totalSilverIcon;
    [SerializeField] private TextMeshProUGUI totalCopperText;
    [SerializeField] private Image totalCopperIcon;

    [Header("持有货币显示")]
    [SerializeField] private TextMeshProUGUI curGoldText;
    [SerializeField] private TextMeshProUGUI curSilverText;
    [SerializeField] private TextMeshProUGUI curCopperText;
    [SerializeField] private Image curGoldIcon;
    [SerializeField] private Image curSilverIcon;
    [SerializeField] private Image curCopperIcon;

    [Header("操作按钮")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button closeButton;

    // ==================== 运行时数据 ====================

    private ShopSO currentShop;
    private PlayerInventorySystem playerInventory;
    private Inventory_Base inventory;

    private readonly List<UI_ShopSlot> npcSlots = new();
    private readonly List<int> npcStockQuantities = new();

    // 节点移动恢复数据
    private Transform backpackOriginalParent;
    private int backpackOriginalIndex;
    private Transform equipOriginalParent;
    private int equipOriginalIndex;

    // 订阅的槽位列表
    private readonly List<UI_ItemSlot> listenedSlots = new();

    // 选中状态
    private UI_ShopSlot selectedShopSlot;
    private Inventory_Item selectedSellItem;
    private UI_ItemSlot selectedSellSlot;
    private bool isBuyMode;

    // ==================== 生命周期 ====================

    private void Awake()
    {
        instance = this;

        buyButton.interactable = false;
        sellButton.interactable = false;
        gameObject.SetActive(false);

        buyButton.onClick.AddListener(OnBuyClicked);
        sellButton.onClick.AddListener(OnSellClicked);
        closeButton.onClick.AddListener(Close);

        minusButton.onClick.AddListener(OnMinusClicked);
        plusButton.onClick.AddListener(OnPlusClicked);
        quantitySlider.onValueChanged.AddListener(OnQuantityChanged);

        quantitySlider.minValue = 0;
        quantitySlider.maxValue = maxQuantity;
        quantitySlider.wholeNumbers = true;
        quantitySlider.value = 0;
    }

    // ==================== 打开 / 关闭 ====================

    public void Open(ShopSO shopData)
    {
        if (shopData == null) return;

        currentShop = shopData;

        // 确保根 Canvas 激活（否则子物体 SetActive 不会执行）
        transform.root.gameObject.SetActive(true);
        gameObject.SetActive(true);
        Time.timeScale = 0f;

        if (shopNameText != null)
            shopNameText.text = shopData.shopName;

        playerInventory = FindAnyObjectByType<PlayerInventorySystem>();
        inventory = playerInventory != null ? playerInventory.GetInventory() : null;

        ClearSelection();

        // 背景 / 按钮显隐
        if (panelBackground != null) panelBackground.SetActive(true);
        SetHiddenObjects(true);

        // 生成左栏 NPC 商品 → 移入右栏槽位 → 订阅点击
        GenerateNpcItems(shopData);
        MovePlayerSlotsToShop();
        SubscribePlayerSlots();

        RefreshCurrencyDisplay();
        UpdateQuantityControls();
    }

    public void Close()
    {
        // 还原 UI
        SetHiddenObjects(false);
        if (panelBackground != null) panelBackground.SetActive(false);

        Time.timeScale = 1f;

        // 注销订阅
        UnsubscribePlayerSlots();

        // 槽位移回原位
        RestorePlayerSlots();

        // 清选中
        ClearSelection();

        // 销毁左栏
        foreach (var slot in npcSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        npcSlots.Clear();
        npcStockQuantities.Clear();

        quantitySlider.value = 0;
        currentShop = null;

        gameObject.SetActive(false);
    }

    // ==================== 背景显隐 ====================

    private void SetHiddenObjects(bool hide)
    {
        foreach (var obj in hiddenOnOpen)
        {
            if (obj != null)
                obj.SetActive(!hide);
        }
    }

    // ==================== 节点移动 ====================

    private void MovePlayerSlotsToShop()
    {
        if (backpackSlotParent != null && playerSlotContainer != null)
        {
            backpackOriginalParent = backpackSlotParent.parent;
            backpackOriginalIndex = backpackSlotParent.GetSiblingIndex();
            backpackSlotParent.SetParent(playerSlotContainer, false);
        }

        if (equipSlotParent != null && playerSlotContainer != null)
        {
            equipOriginalParent = equipSlotParent.parent;
            equipOriginalIndex = equipSlotParent.GetSiblingIndex();
            equipSlotParent.SetParent(playerSlotContainer, false);
        }
    }

    private void RestorePlayerSlots()
    {
        if (backpackSlotParent != null && backpackOriginalParent != null)
        {
            backpackSlotParent.SetParent(backpackOriginalParent, false);
            backpackSlotParent.SetSiblingIndex(backpackOriginalIndex);
            backpackOriginalParent = null;
        }

        if (equipSlotParent != null && equipOriginalParent != null)
        {
            equipSlotParent.SetParent(equipOriginalParent, false);
            equipSlotParent.SetSiblingIndex(equipOriginalIndex);
            equipOriginalParent = null;
        }
    }

    // ==================== 订阅右栏槽位 ====================

    private void SubscribePlayerSlots()
    {
        UnsubscribePlayerSlots();

        if (playerSlotContainer == null) return;

        var allSlots = playerSlotContainer.GetComponentsInChildren<UI_ItemSlot>(true);
        foreach (var slot in allSlots)
        {
            slot.OnItemSlotClicked += OnPlayerSlotClicked;
            listenedSlots.Add(slot);
        }
    }

    private void UnsubscribePlayerSlots()
    {
        foreach (var slot in listenedSlots)
        {
            if (slot != null)
                slot.OnItemSlotClicked -= OnPlayerSlotClicked;
        }
        listenedSlots.Clear();
    }

    // ==================== 生成左栏（NPC 商品） ====================

    private void GenerateNpcItems(ShopSO shopData)
    {
        // 清理
        foreach (var slot in npcSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        npcSlots.Clear();
        npcStockQuantities.Clear();

        if (shopData.items == null || npcItemContainer == null) return;

        for (int i = 0; i < shopData.items.Length; i++)
        {
            var entry = shopData.items[i];
            if (entry.itemData == null) continue;
            if (entry.quantity == 0) continue; // 售罄

            var go = Instantiate(shopSlotPrefab, npcItemContainer);
            var slot = go.GetComponent<UI_ShopSlot>();
            if (slot == null) continue;

            slot.Setup(entry.itemData, UI_ShopSlot.ShopMode.Buying, quantity: entry.quantity);
            slot.OnSlotSelected += OnNpcSlotSelected;
            npcSlots.Add(slot);
            npcStockQuantities.Add(entry.quantity);
        }
    }

    // ==================== 选中处理 ====================

    private void OnNpcSlotSelected(UI_ShopSlot slot)
    {
        // 清其他选中
        if (selectedShopSlot != null && selectedShopSlot != slot)
            selectedShopSlot.SetSelected(false);

        DeselectSellSlot();

        // 同一格再点=取消
        if (selectedShopSlot == slot)
        {
            selectedShopSlot.SetSelected(false);
            selectedShopSlot = null;
        }
        else
        {
            selectedShopSlot = slot;
            selectedShopSlot.SetSelected(true);
        }

        selectedSellItem = null;
        isBuyMode = selectedShopSlot != null;

        ResetQuantityForCurrentSelection();
        UpdateBuySellButtons();
        UpdatePriceDisplay();
    }

    private void OnPlayerSlotClicked(Inventory_Item item, UI_ItemSlot slot)
    {
        if (item == null) return;

        // 装备槽不可出售
        if (slot is UI_EquipSlot) return;

        // 清 NPC 选中
        if (selectedShopSlot != null)
        {
            selectedShopSlot.SetSelected(false);
            selectedShopSlot = null;
        }

        // 同一物品再点=取消
        if (selectedSellItem == item)
        {
            DeselectSellSlot();
            selectedSellItem = null;
            isBuyMode = false;
            quantitySlider.SetValueWithoutNotify(0);
            UpdateBuySellButtons();
            UpdateQuantityControls();
            UpdatePriceDisplay();
            return;
        }

        // 切新选中
        DeselectSellSlot();
        selectedSellSlot = slot;
        selectedSellSlot.SetSelected(true);
        selectedSellItem = item;
        isBuyMode = false;

        ResetQuantityForCurrentSelection();
        UpdateBuySellButtons();
        UpdatePriceDisplay();
    }

    // 根据当前选中，算出合法 maxQty 后设置默认数量=1（若合法）
    private void ResetQuantityForCurrentSelection()
    {
        float maxQty = GetLegalMaxQuantity();

        if (maxQty < 1)
        {
            quantitySlider.SetValueWithoutNotify(0);
            quantitySlider.interactable = false;
            minusButton.interactable = false;
            plusButton.interactable = false;
            if (quantityText != null) quantityText.text = "0";
            return;
        }

        quantitySlider.interactable = true;
        minusButton.interactable = true;
        plusButton.interactable = true;
        quantitySlider.maxValue = maxQty;

        // 默认设 1
        quantitySlider.SetValueWithoutNotify(1);
        if (quantityText != null) quantityText.text = "1";
    }

    private float GetLegalMaxQuantity()
    {
        float maxQty = maxQuantity;

        if (isBuyMode && selectedShopSlot != null)
        {
            int itemValue = selectedShopSlot.ItemData.value;
            if (itemValue > 0 && playerInventory != null)
            {
                int byMoney = playerInventory.GetCurrency() / itemValue;
                maxQty = Mathf.Min(maxQty, byMoney);

                int idx = npcSlots.IndexOf(selectedShopSlot);
                if (idx >= 0 && idx < npcStockQuantities.Count)
                {
                    int s = npcStockQuantities[idx];
                    if (s >= 0) maxQty = Mathf.Min(maxQty, s);
                }
            }
            else
            {
                maxQty = 0;
            }
        }
        else if (!isBuyMode && selectedSellItem != null)
        {
            var held = inventory != null ? inventory.FindItem(selectedSellItem.itemData) : null;
            int holdCount = held != null ? held.currentStackSize : 0;
            maxQty = Mathf.Min(maxQty, holdCount);
        }
        else
        {
            maxQty = 0;
        }

        return maxQty;
    }

    private void DeselectSellSlot()
    {
        if (selectedSellSlot != null)
        {
            selectedSellSlot.SetSelected(false);
            selectedSellSlot = null;
        }
    }

    private void ClearSelection()
    {
        if (selectedShopSlot != null)
        {
            selectedShopSlot.SetSelected(false);
            selectedShopSlot = null;
        }
        DeselectSellSlot();
        selectedSellItem = null;
        isBuyMode = false;
    }

    // ==================== 数量控件 ====================

    private void OnMinusClicked()
    {
        quantitySlider.value = Mathf.Max(0, Mathf.RoundToInt(quantitySlider.value) - 1);
    }

    private void OnPlusClicked()
    {
        quantitySlider.value = Mathf.Min(quantitySlider.maxValue, Mathf.RoundToInt(quantitySlider.value) + 1);
    }

    private void OnQuantityChanged(float value)
    {
        int qty = Mathf.RoundToInt(value);
        if (qty < 0) qty = 0;

        quantitySlider.SetValueWithoutNotify(qty);
        if (quantityText != null)
            quantityText.text = qty.ToString();

        UpdatePriceDisplay();
        UpdateBuySellButtons();
    }

    // ==================== 按钮状态 ====================

    private void UpdateBuySellButtons()
    {
        bool hasQty = GetQuantity() > 0;

        if (isBuyMode && selectedShopSlot != null)
        {
            buyButton.interactable = hasQty && CanAfford();
            sellButton.interactable = false;
        }
        else if (!isBuyMode && selectedSellItem != null)
        {
            sellButton.interactable = hasQty && selectedSellItem.itemData.value > 0;
            buyButton.interactable = false;
        }
        else
        {
            buyButton.interactable = false;
            sellButton.interactable = false;
        }
    }

    private bool CanAfford()
    {
        if (selectedShopSlot == null || playerInventory == null) return false;
        return playerInventory.GetCurrency() >= selectedShopSlot.ItemData.value * GetQuantity();
    }

    // ==================== 购买 ====================

    private void OnBuyClicked()
    {
        if (selectedShopSlot == null || playerInventory == null || inventory == null) return;

        int qty = GetQuantity();
        if (qty <= 0) return;

        // 检查 NPC 库存
        int slotIndex = npcSlots.IndexOf(selectedShopSlot);
        if (slotIndex < 0 || slotIndex >= npcStockQuantities.Count) return;
        int stock = npcStockQuantities[slotIndex];
        if (stock >= 0 && qty > stock)
            qty = stock;
        if (qty <= 0) return;

        var itemData = selectedShopSlot.ItemData;
        int totalCost = itemData.value * qty;

        if (!playerInventory.SpendCurrency(totalCost))
            return;

        // 逐件添加
        int added = 0;
        int loopQty = qty;
        for (int i = 0; i < loopQty; i++)
        {
            var tempItem = new Inventory_Item(itemData);

            bool canAdd = itemData.canStackable
                ? (inventory.CanAddToStack(tempItem) || inventory.CanAddItem())
                : inventory.CanAddItem();

            if (!canAdd) break;

            inventory.AddItem(tempItem);
            added++;
        }

        // 空间不够 → 退款
        if (added < loopQty)
        {
            int refund = (loopQty - added) * itemData.value;
            playerInventory.AddCurrency(refund);
        }

        // 扣库存
        if (stock > 0)
        {
            int remaining = stock - added;
            npcStockQuantities[slotIndex] = remaining;
            if (remaining <= 0)
                RemoveNpcSlot(slotIndex);
            else
                selectedShopSlot.UpdateStockDisplay(remaining);
        }

        RefreshUI();
    }

    private void RemoveNpcSlot(int index)
    {
        if (index < 0 || index >= npcSlots.Count) return;

        var slot = npcSlots[index];
        if (slot == selectedShopSlot)
        {
            slot.SetSelected(false);
            selectedShopSlot = null;
            isBuyMode = false;
        }
        npcSlots.RemoveAt(index);
        npcStockQuantities.RemoveAt(index);
        if (slot != null) Destroy(slot.gameObject);
    }

    // ==================== 出售 ====================

    private void OnSellClicked()
    {
        if (selectedSellItem == null || selectedSellSlot == null) return;
        if (playerInventory == null || inventory == null || currentShop == null) return;

        // 装备槽禁止出售
        if (selectedSellSlot is UI_EquipSlot) return;

        int qty = GetQuantity();
        if (qty <= 0) return;

        if (selectedSellItem.itemData.value <= 0) return;

        // 验证背包里真实持有数
        var itemInInv = inventory.FindItem(selectedSellItem.itemData);
        if (itemInInv == null || itemInInv.currentStackSize < qty) return;

        int unitValue = Mathf.RoundToInt(selectedSellItem.itemData.value * currentShop.buyBackRate);
        int totalRevenue = unitValue * qty;

        // 扣物品
        if (itemInInv.currentStackSize > qty)
        {
            itemInInv.currentStackSize -= qty;
            inventory.TriggerInventoryUpdate();
        }
        else
        {
            inventory.RemoveItem(itemInInv);
        }

        playerInventory.AddCurrency(totalRevenue);

        // 物品卖光了就清选中
        if (itemInInv.currentStackSize <= 0)
        {
            DeselectSellSlot();
            selectedSellItem = null;
        }
        else if (selectedSellItem == itemInInv)
        {
            // 堆叠物品售出部分仍选中，数量滑块收束
        }

        RefreshUI();
    }

    // ==================== 刷新 ====================

    private void RefreshUI()
    {
        RefreshCurrencyDisplay();
        UpdateBuySellButtons();
        UpdateQuantityControls();
        UpdatePriceDisplay();
    }

    private void RefreshCurrencyDisplay()
    {
        if (playerInventory == null) return;

        var amt = CurrencyFormatter.Split(playerInventory.GetCurrency());

        if (curGoldText != null) curGoldText.text = amt.gold.ToString();
        if (curSilverText != null) curSilverText.text = amt.silver.ToString();
        if (curCopperText != null) curCopperText.text = amt.copper.ToString();
    }

    private void UpdateQuantityControls()
    {
        float maxQty = GetLegalMaxQuantity();

        if (maxQty < 1)
        {
            quantitySlider.SetValueWithoutNotify(0);
            quantitySlider.interactable = false;
            minusButton.interactable = false;
            plusButton.interactable = false;
            if (quantityText != null) quantityText.text = "0";
            return;
        }

        quantitySlider.interactable = true;
        minusButton.interactable = true;
        plusButton.interactable = true;
        quantitySlider.maxValue = maxQty;

        int cur = Mathf.RoundToInt(quantitySlider.value);
        if (cur < 1) quantitySlider.SetValueWithoutNotify(1);
        else if (cur > maxQty) quantitySlider.SetValueWithoutNotify((int)maxQty);
    }

    private void UpdatePriceDisplay()
    {
        int totalCopper = 0;
        if (isBuyMode && selectedShopSlot != null)
            totalCopper = selectedShopSlot.ItemData.value * GetQuantity();
        else if (!isBuyMode && selectedSellItem != null && currentShop != null)
        {
            int unit = Mathf.RoundToInt(selectedSellItem.itemData.value * currentShop.buyBackRate);
            totalCopper = unit * GetQuantity();
        }

        SetMultiPrice(totalGoldText, totalGoldIcon,
                      totalSilverText, totalSilverIcon,
                      totalCopperText, totalCopperIcon,
                      totalCopper);
    }

    // ==================== 价格排版 ====================

    private void SetMultiPrice(TextMeshProUGUI goldText, Image goldIcon,
                               TextMeshProUGUI silverText, Image silverIcon,
                               TextMeshProUGUI copperText, Image copperIcon,
                               int copperAmount)
    {
        var amt = CurrencyFormatter.Split(copperAmount);

        if (goldText != null)
        {
            goldText.text = amt.gold.ToString();
            goldText.gameObject.SetActive(amt.HasGold);
        }
        if (goldIcon != null) goldIcon.enabled = amt.HasGold;

        if (silverText != null)
        {
            silverText.text = amt.silver.ToString();
            silverText.gameObject.SetActive(amt.HasSilver);
        }
        if (silverIcon != null) silverIcon.enabled = amt.HasSilver;

        if (copperText != null)
        {
            copperText.text = amt.copper.ToString();
            copperText.gameObject.SetActive(amt.HasCopper);
        }
        if (copperIcon != null) copperIcon.enabled = amt.HasCopper;
    }

    // ==================== 辅助 ====================

    private int GetQuantity()
    {
        return Mathf.Max(0, Mathf.RoundToInt(quantitySlider.value));
    }
}
