using System;
using UnityEngine;

// 商店系统——纯业务逻辑，不依赖 UI。
// 管理商店数据、选中状态、数量约束、购买/出售交易。
// UI 层通过事件监听数据变更后刷新界面。
public class ShopSystem
{
    public bool IsBuyMode { get; private set; }
    public ItemDataSo SelectedItemData { get; private set; }
    public Inventory_Item SelectedSellItem { get; private set; }
    public int Quantity { get; private set; }

    public ShopSO CurrentShop { get; private set; }
    public bool IsOpen { get; private set; }

    // 选中 NPC 商品在列表中的索引（-1 = 未选中）
    public int SelectedNpcIndex { get; private set; } = -1;

    // 数据变更事件，UI 层监听
    public event Action OnDataChanged;

    private PlayerInventorySystem playerInventory;
    private Inventory_Base inventory;

    // NPC 商品库存跟踪（-1 = 无限）
    private int[] npcStocks;

    // ==================== 打开/关闭 ====================

    public void Open(ShopSO shopData, PlayerInventorySystem pis, Inventory_Base inv)
    {
        CurrentShop = shopData;
        playerInventory = pis;
        inventory = inv;

        // 复制初始库存
        if (shopData.items != null)
        {
            npcStocks = new int[shopData.items.Length];
            for (int i = 0; i < shopData.items.Length; i++)
                npcStocks[i] = shopData.items[i].quantity;
        }
        else
        {
            npcStocks = Array.Empty<int>();
        }

        IsOpen = true;
        ClearSelection();
        NotifyDataChanged();
    }

    public void Close()
    {
        IsOpen = false;
        CurrentShop = null;
        playerInventory = null;
        inventory = null;
        npcStocks = null;
        ClearSelection();
    }

    // ==================== 选中 ====================

    public void SelectNpcItem(int index)
    {
        if (!IsOpen || CurrentShop == null) return;
        if (index < 0 || index >= CurrentShop.items.Length) return;
        if (npcStocks[index] == 0) return; // 已售罄

        DeselectSellItem();
        SelectedNpcIndex = index;
        IsBuyMode = true;
        SelectedItemData = CurrentShop.items[index].itemData;

        ResetQuantity();
        NotifyDataChanged();
    }

    public void SelectSellItem(Inventory_Item item)
    {
        if (!IsOpen || item == null) return;

        DeselectNpcItem();
        SelectedSellItem = item;
        SelectedItemData = item.itemData;
        IsBuyMode = false;

        ResetQuantity();
        NotifyDataChanged();
    }

    public void DeselectAll()
    {
        ClearSelection();
        NotifyDataChanged();
    }

    private void DeselectNpcItem()
    {
        SelectedNpcIndex = -1;
        if (!IsBuyMode) return; // 已在出售模式不清除数据
        SelectedItemData = null;
        IsBuyMode = false;
    }

    private void DeselectSellItem()
    {
        SelectedSellItem = null;
        if (IsBuyMode) return; // 已在购买模式不清除数据
        SelectedItemData = null;
        IsBuyMode = false;
    }

    // ==================== 数量 ====================

    public void SetQuantity(int qty)
    {
        Quantity = Mathf.Max(0, qty);
        NotifyDataChanged();
    }

    // 获取当前选中物品的合法最大数量（基于金钱/库存/持有数/背包空间）
    public int GetLegalMaxQuantity()
    {
        if (!IsOpen) return 0;
        float maxQty = 99;

        if (IsBuyMode && SelectedItemData != null)
        {
            int itemValue = SelectedItemData.value;
            if (itemValue > 0 && playerInventory != null)
            {
                maxQty = Mathf.Min(maxQty, playerInventory.GetCurrency() / itemValue);

                if (SelectedNpcIndex >= 0 && SelectedNpcIndex < npcStocks.Length)
                {
                    int s = npcStocks[SelectedNpcIndex];
                    if (s >= 0) maxQty = Mathf.Min(maxQty, s);
                }

                // 背包空间上限
                if (inventory != null)
                {
                    int emptySlots = inventory.maxInventorySize - inventory.GetOccupiedSlots().Count;
                    if (SelectedItemData.canStackable)
                    {
                        // 可堆叠：已有堆叠剩余空间 + 空槽位 × 最大堆叠
                        var existing = inventory.FindItem(SelectedItemData);
                        int stackRoom = existing != null ? (99 - existing.currentStackSize) : 0;
                        // stackRoom 可能为负（超出 maxStackSize 的数据异常），取 max(0)
                        if (stackRoom < 0) stackRoom = 0;
                        maxQty = Mathf.Min(maxQty, stackRoom + emptySlots * 99);
                    }
                    else
                    {
                        // 不可堆叠：每个占一个槽
                        maxQty = Mathf.Min(maxQty, emptySlots);
                    }
                }
            }
            else
            {
                maxQty = 0;
            }
        }
        else if (!IsBuyMode && SelectedSellItem != null && inventory != null)
        {
            int holdCount = SelectedSellItem.currentStackSize; // 用选中堆的堆叠数（而非 FindItem 第一个）
            maxQty = Mathf.Min(maxQty, holdCount);
        }
        else
        {
            maxQty = 0;
        }

        return Mathf.Max(1, (int)maxQty);
    }

    // 重置数量为 1（保证在合法范围内）
    public void ResetQuantity()
    {
        Quantity = Mathf.Clamp(1, 0, GetLegalMaxQuantity());
        if (Quantity < 1) Quantity = 1;
    }

    // ==================== 价格 ====================

    public int GetTotalPrice()
    {
        if (SelectedItemData == null) return 0;

        if (IsBuyMode)
            return SelectedItemData.value * Quantity;
        else
        {
            int unit = Mathf.RoundToInt(SelectedItemData.value * CurrentShop.buyBackRate);
            return unit * Quantity;
        }
    }

    public int GetUnitPrice()
    {
        if (SelectedItemData == null) return 0;
        if (IsBuyMode) return SelectedItemData.value;
        return Mathf.RoundToInt(SelectedItemData.value * CurrentShop.buyBackRate);
    }

    public bool CanAfford()
    {
        if (!IsBuyMode || SelectedItemData == null || playerInventory == null) return false;
        return playerInventory.GetCurrency() >= SelectedItemData.value * Quantity;
    }

    // ==================== 交易 ====================

    // 返回值：成功购买的数量（0 = 失败）
    public int TryBuy()
    {
        if (!IsBuyMode || SelectedItemData == null || playerInventory == null || inventory == null)
            return 0;
        if (Quantity <= 0) return 0;

        int qty = Quantity;
        int slotIndex = SelectedNpcIndex;
        if (slotIndex < 0 || slotIndex >= npcStocks.Length) return 0;

        // 受 NPC 库存限制
        int stock = npcStocks[slotIndex];
        if (stock >= 0 && qty > stock) qty = stock;
        if (qty <= 0) return 0;

        var itemData = SelectedItemData;
        int totalCost = itemData.value * qty;

        if (!playerInventory.SpendCurrency(totalCost)) return 0;

        // 逐件添加
        int added = 0;
        for (int i = 0; i < qty; i++)
        {
            var tempItem = new Inventory_Item(itemData);
            bool canAdd = itemData.canStackable
                ? (inventory.CanAddToStack(tempItem) || inventory.CanAddItem())
                : inventory.CanAddItem();
            if (!canAdd) break;
            inventory.AddItem(tempItem);
            added++;
        }

        // 背包空间不够 → 退款
        if (added < qty)
        {
            int refund = (qty - added) * itemData.value;
            playerInventory.AddCurrency(refund);
        }

        // 扣库存
        if (stock > 0)
            npcStocks[slotIndex] = stock - added;

        if (added <= 0)
        {
            // 完全没买到 → 退全款
            playerInventory.AddCurrency(totalCost);
            return 0;
        }

        // 如果当前选中的商品售罄，清除选中
        if (npcStocks[slotIndex] == 0)
            DeselectAll();
        else
            ResetQuantity();

        NotifyDataChanged();
        return added;
    }

    // 返回值：成功出售的数量（0 = 失败）
    public int TrySell()
    {
        if (IsBuyMode || SelectedSellItem == null || SelectedItemData == null)
            return 0;
        if (playerInventory == null || inventory == null || CurrentShop == null)
            return 0;
        if (Quantity <= 0) return 0;
        if (SelectedItemData.value <= 0) return 0;

        // 用选中的具体物品实例（而非 FindItem 第一个匹配），背包多把同类时只卖选中那把
        var itemInInv = SelectedSellItem;
        if (itemInInv == null || itemInInv.currentStackSize < Quantity) return 0;

        int qty = Quantity;
        int unitValue = Mathf.RoundToInt(SelectedItemData.value * CurrentShop.buyBackRate);
        int totalRevenue = unitValue * qty;

        // 扣物品（操作选中实例）
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

        // 选中的实例已不在背包（卖光）则清除选中，否则重置数量
        if (inventory.GetItemSlot(itemInInv) == -1)
        {
            DeselectAll();
        }
        else
        {
            ResetQuantity();
        }

        NotifyDataChanged();
        return qty;
    }

    // ==================== 查询 ====================

    public CurrencyFormatter.CurrencyAmount GetPlayerCurrency()
    {
        if (playerInventory == null) return new CurrencyFormatter.CurrencyAmount();
        return CurrencyFormatter.Split(playerInventory.GetCurrency());
    }

    // 获取 NPC 商品库存（-1 = 无限，0 = 售罄）
    public int GetNpcStock(int index)
    {
        if (index < 0 || index >= (npcStocks?.Length ?? 0)) return -1;
        return npcStocks[index];
    }

    public bool IsNpcItemSoldOut(int index) => GetNpcStock(index) == 0;

    // ==================== 内部 ====================

    // 清除选中但不触发事件（用于 Open/Close 内部）
    private void ClearSelection()
    {
        SelectedNpcIndex = -1;
        SelectedSellItem = null;
        SelectedItemData = null;
        IsBuyMode = false;
        Quantity = 1;
    }

    private void NotifyDataChanged()
    {
        OnDataChanged?.Invoke();
    }
}
