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
        playerInventorySystem = FindFirstObjectByType<PlayerInventorySystem>();

        if (playerInventorySystem != null)
        {
            playerInventorySystem.OnInventoryUpdated += UpdateInventoryUI;
            playerInventorySystem.OnEquipmentUpdated += UpdateEquipmentUI;

            InitializeSlots();
        }
        else
        {
            Debug.LogError("[UI_Inventory] PlayerInventorySystem 未找到！请确保场景中有该组件。");
        }
    }

    private void Start()
    {
        //游戏启动时强制更新所有槽位，确保稀有度背景正确显示
        if (playerInventorySystem != null)
        {
            UpdateInventorySlots();
            UpdateEquipmentSlots();
        }
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
        if (item != null)
        {
            playerInventorySystem.TryEquipItem(item);
        }
    }

    private void OnEquipmentSlotDoubleClicked(Inventory_Item item)
    {
        if (item != null)
        {
            playerInventorySystem.TryUnequipItem(item);
        }
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

        // 使用新的字典存储系统更新槽位
        for (int i = 0; i < uiItemSlots.Length; i++)
        {
            Inventory_Item item = inventory.GetItemAtSlot(i);
            uiItemSlots[i].UpdateSlot(item);
        }
    }
}
