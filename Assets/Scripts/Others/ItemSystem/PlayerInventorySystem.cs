using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventorySystem : MonoBehaviour
{
    private const int BaseInventorySize = 15; // 背包基础格数（与玩家预制体现值一致）

    public event Action OnInventoryUpdated;
    public event Action OnEquipmentUpdated;
    public event Action OnGoldChanged;

    [Header("调试")]
    [SerializeField] private int debugCurrency;   // Inspector 实时观察用

    private int currency;  // 统一铜币单位（1金币=10000，1银币=100）

    private void Update()
    {
        debugCurrency = currency;
    }

    public int Currency
    {
        get => currency;
        set { currency = Mathf.Max(0, value); OnGoldChanged?.Invoke(); }
    }

    public int DisplayGold => currency / 10000;
    public int DisplaySilver => (currency % 10000) / 100;
    public int DisplayCopper => currency % 100;   // 铜币

    public void AddCurrency(int amount) => Currency += amount;
    public bool SpendCurrency(int amount) { if (currency < amount) return false; Currency -= amount; return true; }
    public int GetCurrency() => currency;
    public void SetCurrency(int amount) => Currency = amount;

    [SerializeField] private Inventory_Player inventory;
    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private ConsumableSystem consumableSystem;

    private void Awake()
    {
        InitializeSystems();
    }

    private void InitializeSystems()
    {
        if (inventory != null)
        {
            inventory.OnInventoryUpdated += HandleInventoryUpdated;
        }

        if (equipmentSystem != null)
        {
            equipmentSystem.OnEquipmentUpdated += HandleEquipmentUpdated;
        }

        if (consumableSystem != null)
        {
            consumableSystem.OnConsumableUsed += HandleConsumableUsed;
        }
    }

    private void HandleInventoryUpdated()
    {
        OnInventoryUpdated?.Invoke();
    }

    private void HandleEquipmentUpdated()
    {
        OnEquipmentUpdated?.Invoke();
    }

    private void HandleConsumableUsed()
    {
        OnInventoryUpdated?.Invoke();
    }

    private void Start()
    {
        // 被动「背包扩容」：订阅被动技能等级变化（技能树升级/读档恢复都会触发）
        if (SkillDataManager.Instance != null)
            SkillDataManager.Instance.OnPassiveSkillUpdated += OnPassiveSkillUpdated;

        RecalculateCapacity(); // 启动兜底（未学被动时加成=0，容量=基础值）
    }

    // 被动技能更新回调：仅关心背包扩容
    private void OnPassiveSkillUpdated(SkillUpgradeType upgradeType, int level)
    {
        if (upgradeType == SkillUpgradeType.BagExpand)
            RecalculateCapacity();
    }

    // 被动「背包扩容」：背包上限 = 基础15 + 被动加成，并通知 UI 刷新槽位数量
    public void RecalculateCapacity()
    {
        var bagExpand = FindAnyObjectByType<Skill_BagExpand>();
        int bonus = bagExpand != null ? bagExpand.GetBagSlotBonus() : 0;

        var inv = GetInventory();
        if (inv != null)
        {
            inv.maxInventorySize = BaseInventorySize + bonus;
            inv.TriggerInventoryUpdate();
        }
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnInventoryUpdated -= HandleInventoryUpdated;
        }

        if (equipmentSystem != null)
        {
            equipmentSystem.OnEquipmentUpdated -= HandleEquipmentUpdated;
        }

        if (consumableSystem != null)
        {
            consumableSystem.OnConsumableUsed -= HandleConsumableUsed;
        }

        if (SkillDataManager.Instance != null)
            SkillDataManager.Instance.OnPassiveSkillUpdated -= OnPassiveSkillUpdated;
    }

    public bool TryEquipItem(Inventory_Item item)//从背包装备物品
    {
        if (item == null || equipmentSystem == null || inventory == null)
            return false;

        int originalSlotIndex = inventory.GetItemSlot(item);
        if (originalSlotIndex == -1)
            return false;

        var oldItem = equipmentSystem.TryEquipItem(item);
        inventory.RemoveItem(item);

        if (oldItem != null)
        {
            if (inventory.CanAddItem())
                inventory.AddItem(oldItem);
        }

        return true;
    }

    public bool TryUnequipItem(Inventory_Item item)//卸载物品到背包
    {
        if (item == null || equipmentSystem == null || inventory == null)
            return false;

        if (!inventory.CanAddItem())
            return false;

        if (equipmentSystem.TryUnequipItem(item))
        {
            inventory.AddItem(item);
            return true;
        }

        return false;
    }

    public bool TryUnequipItemToSlot(Inventory_Item item, int targetSlot)//将装备卸下到指定槽位
    {
        if (item == null || equipmentSystem == null || inventory == null)
            return false;

        if (targetSlot < 0 || targetSlot >= inventory.maxInventorySize)
            return false;

        if (!inventory.IsSlotEmpty(targetSlot))
            return false;

        if (equipmentSystem.TryUnequipItem(item))
        {
            inventory.AddItem(item, targetSlot);
            return true;
        }

        return false;
    }

    public bool TryEquipToSlot(Inventory_Item item, int targetSlotIndex)//将装备安装到指定槽位
    {
        if (item == null || equipmentSystem == null || inventory == null)
            return false;

        int originalSlotIndex = inventory.GetItemSlot(item);
        if (originalSlotIndex == -1)
            return false;

        var oldItem = equipmentSystem.TryEquipItemToSlot(item, targetSlotIndex);
        inventory.RemoveItem(item);

        if (oldItem != null)
        {
            if (originalSlotIndex >= 0 && originalSlotIndex < inventory.maxInventorySize)
            {
                if (inventory.IsSlotEmpty(originalSlotIndex))
                {
                    inventory.AddItem(oldItem, originalSlotIndex);
                }
                else
                {
                    if (inventory.CanAddItem())
                    {
                        inventory.AddItem(oldItem);
                    }
                }
            }
            else
            {
                if (inventory.CanAddItem())
                {
                    inventory.AddItem(oldItem);
                }
            }
        }

        return true;
    }

    public bool TrySwapEquipmentSlots(ItemType itemType, int slotA, int slotB)//交换装备槽位
    {
        return equipmentSystem.SwapEquipmentSlots(itemType, slotA, slotB);
    }

    public Inventory_Player GetInventory()
    {
        if (inventory == null) inventory = GetComponentInChildren<Inventory_Player>(true);
        if (inventory == null) inventory = FindAnyObjectByType<Inventory_Player>();
        return inventory;
    }

    public EquipmentSystem GetEquipmentSystem()
    {
        if (equipmentSystem == null) equipmentSystem = GetComponentInChildren<EquipmentSystem>(true);
        if (equipmentSystem == null) equipmentSystem = FindAnyObjectByType<EquipmentSystem>();
        return equipmentSystem;
    }

    public ConsumableSystem GetConsumableSystem()//获取消耗品系统
    {
        return consumableSystem;
    }

    public bool TryUseConsumable(Inventory_Item item)//尝试使用消耗品
    {
        if (consumableSystem != null)
        {
            return consumableSystem.TryUseConsumable(item);
        }
        return false;
    }
}