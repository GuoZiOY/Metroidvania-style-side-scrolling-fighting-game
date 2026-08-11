using System;
using System.Collections.Generic;
using UnityEngine;

// 仓库（仓储）系统 —— 独立于玩家背包的第二份 Inventory_Base 容器。
// 负责：存取转移（背包↔仓库整堆合并）、查询/分类/排序视图、存档序列化。
// 挂在持久 UI 根下（DontDestroyOnLoad），单例访问。
public class WarehouseSystem : MonoBehaviour
{
    public static WarehouseSystem Instance { get; private set; }

    [Header("仓库配置")]
    [SerializeField] private int capacity = 70;            // 仓库固定格数
    [SerializeField] private Inventory_Base warehouseInventory; // 仓库容器（未指定则自动创建）

    // 排序维度
    public enum WarehouseSortMode { Default, Type, Rarity, Name, Quantity }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 仓库容器（懒创建：优先用序列化引用，否则挂在自身）
    public Inventory_Base WarehouseInventory
    {
        get
        {
            if (warehouseInventory == null)
                warehouseInventory = GetComponent<Inventory_Base>() ?? gameObject.AddComponent<Inventory_Base>();
            if (warehouseInventory.maxInventorySize != capacity)
                warehouseInventory.maxInventorySize = capacity;
            return warehouseInventory;
        }
    }

    // 从玩家背包存入仓库（源槽 = 背包槽位索引；targetWarehouseSlot = 落点仓库槽，-1 或越界 = 首个空槽/合并）
    // 像背包一样：放到指定槽位，不自动排到第一个空槽。
    public bool DepositFromBackpack(Inventory_Item item, int backpackSlot, int targetWarehouseSlot = -1)
    {
        var playerInv = FindAnyObjectByType<PlayerInventorySystem>()?.GetInventory();
        if (playerInv == null || item == null)
        {
            NotifyTransferFail("操作失败");
            return false;
        }
        var wh = WarehouseInventory;

        // 目标槽可放置判定：指定槽为空或可整堆并入；未指定则看仓库有无空间
        bool specified = targetWarehouseSlot >= 0 && targetWarehouseSlot < wh.maxInventorySize;
        if (specified)
        {
            Inventory_Item existing = wh.GetItemAtSlot(targetWarehouseSlot);
            bool canMerge = existing != null && existing.itemData == item.itemData
                && existing.CanAddStack()
                && existing.currentStackSize + item.currentStackSize <= existing.MaxStackSize;
            if (existing != null && !canMerge)
                return false; // 目标槽被不可合并的物品占据 → 拒绝（拖拽路径已被 CanAcceptItem 拦截，此处兜底静默）
        }
        else if (!CanPlace(wh, item))
        {
            NotifyTransferFail("仓库已满"); // 仓库无空位且不可合并
            return false;
        }

        // 从背包移除
        playerInv.RemoveItem(item);
        playerInv.TriggerInventoryUpdate();

        if (specified)
        {
            Inventory_Item existing = wh.GetItemAtSlot(targetWarehouseSlot);
            if (existing != null)
                existing.currentStackSize += item.currentStackSize; // 并入目标槽已有堆
            else
                wh.itemDictionary[targetWarehouseSlot] = item; // 放到指定槽
        }
        else
        {
            PlaceInto(wh, item); // 首个空槽/合并
        }

        wh.TriggerInventoryUpdate();
        return true;
    }

    // 从仓库取出到玩家背包（源槽 = 仓库槽位索引，目标槽 = 背包落点槽位）
    public bool WithdrawToBackpack(Inventory_Item item, int warehouseSlot, int targetBackpackSlot)
    {
        var playerInv = FindAnyObjectByType<PlayerInventorySystem>()?.GetInventory();
        if (playerInv == null || item == null)
        {
            NotifyTransferFail("操作失败");
            return false;
        }

        bool ok = Transfer(WarehouseInventory, playerInv, item, targetBackpackSlot);
        if (!ok)
            NotifyTransferFail("背包已满"); // 取出失败：背包无空位且不可合并
        return ok;
    }

    // 仓库内部交换（拖动仓库物品到另一仓库物品上）
    public void SwapWithinWarehouse(int slotA, int slotB)
    {
        var wh = WarehouseInventory;
        if (wh != null)
            wh.SwapItems(slotA, slotB);
    }

    // 一键存入：把背包所有物品移入仓库（首个空槽/合并），返回移动数量
    public int DepositAllFromBackpack()
    {
        var playerInv = FindAnyObjectByType<PlayerInventorySystem>()?.GetInventory();
        if (playerInv == null)
            return 0;
        var wh = WarehouseInventory;

        if (playerInv.GetItemCount() == 0)
        {
            NotifyTransferFail("背包是空的，没有可存入的物品");
            return 0;
        }

        int moved = 0;
        foreach (int slot in playerInv.GetOccupiedSlots())
        {
            var item = playerInv.GetItemAtSlot(slot);
            if (item == null)
                continue;
            if (Transfer(playerInv, wh, item))
                moved++;
        }

        if (moved == 0)
            NotifyTransferFail("仓库已满"); // 有物品但全部转移失败（无空位且不可合并）
        return moved;
    }

    // 一键取出：把仓库所有物品移回背包（首个空槽/合并），返回移动数量
    public int WithdrawAllToBackpack()
    {
        var playerInv = FindAnyObjectByType<PlayerInventorySystem>()?.GetInventory();
        if (playerInv == null)
            return 0;
        var wh = WarehouseInventory;

        if (wh.GetItemCount() == 0)
        {
            NotifyTransferFail("仓库是空的，没有可取出的物品");
            return 0;
        }

        int moved = 0;
        foreach (int slot in wh.GetOccupiedSlots())
        {
            var item = wh.GetItemAtSlot(slot);
            if (item == null)
                continue;
            if (Transfer(wh, playerInv, item))
                moved++;
        }

        if (moved == 0)
            NotifyTransferFail("背包已满"); // 有物品但全部转移失败（背包无空位且不可合并）
        return moved;
    }

    // 整堆转移：source → target。先尝试整堆并入 target 已有同类堆，否则整堆放入指定空槽（或首个空槽）。
    // 直接操作字典绕开 AddItem 的 +1 堆叠逻辑，避免跨容器转移丢数量。
    private bool Transfer(Inventory_Base source, Inventory_Base target, Inventory_Item item, int targetSlot = -1)
    {
        int sourceSlot = source.GetItemSlot(item);
        if (sourceSlot == -1)
            return false;

        // 容量预检：可整堆并入已有堆，或目标有空槽
        if (!CanPlace(target, item, targetSlot))
            return false;

        source.itemDictionary.Remove(sourceSlot);
        source.TriggerInventoryUpdate();

        if (!PlaceInto(target, item, targetSlot))
        {
            source.itemDictionary[sourceSlot] = item; // 兜底回滚
            source.TriggerInventoryUpdate();
            return false;
        }
        return true;
    }

    // 装备 → 仓库：直接从装备槽取下并存入仓库（不经背包，避免背包满时无法取下）
    public bool DepositFromEquipment(Inventory_Item item)
    {
        var invSys = FindAnyObjectByType<PlayerInventorySystem>();
        if (invSys == null || item == null)
        {
            NotifyTransferFail("操作失败");
            return false;
        }
        var eq = invSys.GetEquipmentSystem();
        var wh = WarehouseInventory;
        if (eq == null || wh == null)
        {
            NotifyTransferFail("操作失败");
            return false;
        }

        if (!CanPlace(wh, item))
        {
            NotifyTransferFail("仓库已满"); // 仓库无空间
            return false;
        }

        if (!eq.TryUnequipItem(item))
        {
            NotifyTransferFail("无法卸下该装备");
            return false;
        }

        if (!PlaceInto(wh, item))
        {
            eq.TryEquipItem(item); // 极端情况回滚重新装备
            return false;
        }
        return true;
    }

    // 转移失败提示：失败/拒绝音效 + 事件提示框显示原因（供各转移方法失败时调用）
    private void NotifyTransferFail(string reason)
    {
        AudioManager.Instance?.PlayDenySfx(); // 失败/拒绝音效
        FindAnyObjectByType<UI_EventTip>()?.ShowDenyTip(reason); // 红色抖动提示原因
    }

    // 目标是否能容纳该物品（可整堆并入已有堆，或目标有空槽/落点空槽）
    private bool CanPlace(Inventory_Base target, Inventory_Item item, int targetSlot = -1)
    {
        Inventory_Item existing = target.FindItem(item.itemData);
        bool canMergeWhole = existing != null && existing.CanAddStack()
            && existing.currentStackSize + item.currentStackSize <= existing.MaxStackSize;
        if (canMergeWhole)
            return true;

        int freeSlot = (targetSlot >= 0 && target.IsSlotEmpty(targetSlot)) ? targetSlot : target.GetFirstAvailableSlot();
        return freeSlot != -1;
    }

    // 把物品放入目标容器：整堆合并已有堆，否则放入指定空槽/首个空槽（调用方需先 CanPlace 预检）
    private bool PlaceInto(Inventory_Base target, Inventory_Item item, int targetSlot = -1)
    {
        Inventory_Item existing = target.FindItem(item.itemData);
        bool canMergeWhole = existing != null && existing.CanAddStack()
            && existing.currentStackSize + item.currentStackSize <= existing.MaxStackSize;

        if (canMergeWhole)
        {
            existing.currentStackSize += item.currentStackSize; // 整堆合并数量
        }
        else
        {
            int freeSlot = (targetSlot >= 0 && target.IsSlotEmpty(targetSlot)) ? targetSlot : target.GetFirstAvailableSlot();
            if (freeSlot == -1)
                return false;
            target.itemDictionary[freeSlot] = item; // 整堆放入空槽
        }

        target.TriggerInventoryUpdate();
        return true;
    }

    // 查询/分类/排序视图：满足 分类 + 查询 + 排序 的 (槽位索引, 物品) 列表，供 UI 重建网格
    // descending=true 时对排序结果反向（正序/逆序切换）
    public List<(int slot, Inventory_Item item)> GetViewItems(ItemType category, string query, WarehouseSortMode sort, bool descending = false)
    {
        var result = new List<(int, Inventory_Item)>();
        var wh = WarehouseInventory;
        if (wh == null)
            return result;

        string q = query?.Trim().ToLower() ?? "";
        foreach (var kvp in wh.itemDictionary)
        {
            Inventory_Item item = kvp.Value;
            if (item?.itemData == null)
                continue;

            // 分类过滤（ItemType.None = 全部）
            if (category != ItemType.None && item.itemData.itemType != category)
                continue;

            // 查询过滤（名称包含，忽略大小写）
            if (q.Length > 0 && !item.itemData.itemName.ToLower().Contains(q))
                continue;

            result.Add((kvp.Key, item));
        }

        switch (sort)
        {
            case WarehouseSortMode.Type:
                result.Sort((a, b) => a.Item2.itemData.itemType.CompareTo(b.Item2.itemData.itemType));
                break;
            case WarehouseSortMode.Rarity:
                result.Sort((a, b) => GetRarity(a.Item2).CompareTo(GetRarity(b.Item2)));
                break;
            case WarehouseSortMode.Name:
                result.Sort((a, b) => string.Compare(a.Item2.itemData.itemName, b.Item2.itemData.itemName, StringComparison.Ordinal));
                break;
            case WarehouseSortMode.Quantity:
                result.Sort((a, b) => a.Item2.currentStackSize.CompareTo(b.Item2.currentStackSize));
                break;
            // Default：保持字典顺序（槽位升序）
        }

        if (descending)
            result.Reverse(); // 反向排序（正序/逆序切换）

        return result;
    }

    private LootRarity GetRarity(Inventory_Item item)
        => item.actualRarity.HasValue ? item.actualRarity.Value : item.itemData.rarity;

    // ==================== 存档 ====================

    // 序列化仓库内容（复用 InventorySlotData，含词缀）
    public List<InventorySlotData> GetSaveData()
    {
        var list = new List<InventorySlotData>();
        var wh = WarehouseInventory;
        if (wh == null)
            return list;

        foreach (var kvp in wh.itemDictionary)
        {
            var item = kvp.Value;
            if (item == null || item.itemData == null)
                continue;

            list.Add(new InventorySlotData
            {
                itemId = item.itemData.itemId,
                stackSize = item.currentStackSize,
                slotIndex = kvp.Key,
                rarity = item.actualRarity.HasValue ? (int)item.actualRarity.Value : (int)item.itemData.rarity,
                rarityMultiplier = item.rarityMultiplier > 0 ? item.rarityMultiplier : 1f,
                affixes = Inventory_Item.ToSaveData(item.affixes),
            });
        }
        return list;
    }

    // 读档恢复仓库内容（先清空再重建）
    public void LoadFromSave(List<InventorySlotData> items)
    {
        var wh = WarehouseInventory;
        if (wh == null || items == null)
            return;

        foreach (int slot in new List<int>(wh.itemDictionary.Keys))
            wh.RemoveItemAtSlot(slot);

        foreach (var slot in items)
        {
            var itemData = ItemLookup.Find(slot.itemId);
            if (itemData == null)
                continue;

            LootRarity rarity = (LootRarity)slot.rarity;
            float multiplier = slot.rarityMultiplier > 0 ? slot.rarityMultiplier : 1f;
            var looted = new LootedItem(itemData, rarity) { statMultiplier = multiplier };
            var item = new Inventory_Item(looted, Inventory_Item.FromSaveData(slot.affixes));
            item.currentStackSize = Mathf.Max(1, slot.stackSize);
            wh.AddItem(item, slot.slotIndex);
        }
        wh.TriggerInventoryUpdate();
    }
}
