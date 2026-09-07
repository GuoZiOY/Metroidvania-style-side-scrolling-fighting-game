using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class UI_Inventory : MonoBehaviour
{
    private PlayerInventorySystem playerInventorySystem;
    private UI_InventorySlot[] uiItemSlots;
    private UI_EquipSlot[] uiEquipSlots;

    [SerializeField] private Transform uiItemSlotParent;
    [SerializeField] private Transform uiEquipSlotParent;

    private void Awake()
    {
        uiItemSlots = uiItemSlotParent.GetComponentsInChildren<UI_InventorySlot>();
        uiEquipSlots = uiEquipSlotParent.GetComponentsInChildren<UI_EquipSlot>();
    }

    private void Start()
    {
        // 玩家由 PlayerSpawner 在场景加载后生成（sceneLoaded 事件，晚于本组件 Awake），
        // 因此延迟到 Start 查找并初始化背包 UI；等待几帧兜底
        StartCoroutine(InitWhenPlayerReady());
    }

    // 等待玩家系统就绪后初始化（订阅 + 槽位绑定 + 首刷）
    private System.Collections.IEnumerator InitWhenPlayerReady()
    {
        for (int i = 0; i < 30 && playerInventorySystem == null; i++)
        {
            playerInventorySystem = FindAnyObjectByType<PlayerInventorySystem>();
            if (playerInventorySystem != null) break;
            yield return null;
        }

        if (playerInventorySystem == null)
        {
            Debug.LogWarning("[UI_Inventory] PlayerInventorySystem 未找到（玩家未生成？），背包 UI 跳过初始化");
            yield break;
        }

        playerInventorySystem.OnInventoryUpdated += UpdateInventoryUI;
        playerInventorySystem.OnEquipmentUpdated += UpdateEquipmentUI;

        InitializeSlots();

        // 确保槽位数量与当前背包容量一致（被动扩容后容量可大于预置槽数）
        var inventory = playerInventorySystem.GetInventory();
        EnsureSlotCount(inventory != null ? inventory.maxInventorySize : uiItemSlots.Length);

        // 游戏启动时强制更新所有槽位，确保稀有度背景正确显示
        UpdateInventorySlots();
        UpdateEquipmentSlots();
    }

    private void OnDestroy()
    {
        if (playerInventorySystem != null)
        {
            playerInventorySystem.OnInventoryUpdated -= UpdateInventoryUI;
            playerInventorySystem.OnEquipmentUpdated -= UpdateEquipmentUI;
        }
    }

    private void InitializeSlots()
    {
        var inventory = playerInventorySystem.GetInventory();
        var equipmentSystem = playerInventorySystem.GetEquipmentSystem();

        // 初始化背包槽位，分配槽位索引
        for (int i = 0; i < uiItemSlots.Length; i++)
        {
            uiItemSlots[i].Initialize(inventory, i);
            uiItemSlots[i].OnItemSlotDoubleClicked += OnInventorySlotDoubleClicked;
        }

        // 初始化装备槽位，根据槽位类型和索引匹配
        foreach (var slot in uiEquipSlots)
        {
            slot.Initialize(equipmentSystem);
            slot.OnItemSlotDoubleClicked += OnEquipmentSlotDoubleClicked;
        }
    }

    private void OnInventorySlotDoubleClicked(Inventory_Item item)
    {
        if (item == null)
            return;

        // 仓库打开时：双击把背包物品存入仓库
        if (UIManager.Instance != null && UIManager.Instance.IsWarehouseOpen)
        {
            var ws = WarehouseSystem.Instance ?? FindAnyObjectByType<WarehouseSystem>();
            var inv = playerInventorySystem.GetInventory();
            int slot = inv != null ? inv.GetItemSlot(item) : -1;
            if (ws != null && slot != -1)
                ws.DepositFromBackpack(item, slot);
            return;
        }

        playerInventorySystem.TryEquipItem(item);
    }

    private void OnEquipmentSlotDoubleClicked(Inventory_Item item)
    {
        if (item == null)
            return;

        // 仓库打开时：双击把已装备物品卸下存入仓库
        if (UIManager.Instance != null && UIManager.Instance.IsWarehouseOpen)
        {
            var ws = WarehouseSystem.Instance ?? FindAnyObjectByType<WarehouseSystem>();
            if (ws != null)
                ws.DepositFromEquipment(item);
            return;
        }

        playerInventorySystem.TryUnequipItem(item);
    }

    private void UpdateInventoryUI()
    {
        UpdateInventorySlots();
    }

    private void UpdateEquipmentUI()
    {
        UpdateEquipmentSlots();
    }

    private void UpdateEquipmentSlots()
    {
        var equipmentSystem = playerInventorySystem.GetEquipmentSystem();

        // 根据槽位类型和索引更新装备槽位
        foreach (var slot in uiEquipSlots)
        {
            var itemType = slot.slotType;
            var slotIndex = slot.GetTypeSlotIndex();

            // 从装备系统获取指定类型和索引的物品
            var item = equipmentSystem.GetItem(itemType, slotIndex);
            slot.UpdateSlot(item);
        }
    }

    private void UpdateInventorySlots()
    {
        var inventory = playerInventorySystem.GetInventory();
        if (inventory == null)
            return;

        // 被动扩容后容量可变，先确保槽位数量与容量一致
        EnsureSlotCount(inventory.maxInventorySize);

        // 使用新的字典存储系统更新槽位（仅刷新容量内的有效槽位）
        int count = Mathf.Min(uiItemSlots.Length, inventory.maxInventorySize);
        for (int i = 0; i < count; i++)
        {
            Inventory_Item item = inventory.GetItemAtSlot(i);
            uiItemSlots[i].UpdateSlot(item);
        }
    }

    // 确保背包槽位数量与容量一致：不足则克隆首个槽位补足，超出则隐藏（被动扩容后容量可变）
    private void EnsureSlotCount(int capacity)
    {
        if (uiItemSlots == null || uiItemSlots.Length == 0 || capacity <= 0)
            return;

        if (uiItemSlots.Length < capacity)
        {
            var inventory = playerInventorySystem.GetInventory();
            int start = uiItemSlots.Length;
            for (int i = start; i < capacity; i++)
            {
                var go = Instantiate(uiItemSlots[0].gameObject, uiItemSlotParent);
                go.name = $"背包槽位{i}";
                go.SetActive(true); // 确保克隆槽位激活（模板可能被隐藏）
            }

            // 重新收集全部槽位（含 inactive），只初始化新增部分
            var newSlots = uiItemSlotParent.GetComponentsInChildren<UI_InventorySlot>(true);
            for (int i = start; i < newSlots.Length; i++)
            {
                newSlots[i].Initialize(inventory, i);
                newSlots[i].OnItemSlotDoubleClicked += OnInventorySlotDoubleClicked;
            }
            uiItemSlots = newSlots;
        }

        // 隐藏超出容量的槽位（容量一般只增不减，防御回缩）
        for (int i = 0; i < uiItemSlots.Length; i++)
            uiItemSlots[i].gameObject.SetActive(i < capacity);
    }
}
