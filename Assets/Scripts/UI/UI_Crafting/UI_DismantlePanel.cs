using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 分解面板（F10）— 列出背包装备，选中后预览分解产出并执行分解
// 接线：Canvas 面板挂此脚本，装备行用 itemRowPrefab 实例化
public class UI_DismantlePanel : MonoBehaviour
{
    [Header("数据")]
    [SerializeField] private DismantleTable dismantleTable; // 分解产出表（Inspector 拖入）

    [Header("列表")]
    [SerializeField] private Transform itemListRoot;       // 装备行父节点（ScrollView Content）
    [SerializeField] private GameObject itemRowPrefab;     // 装备行 prefab（名称 + 稀有度 + Button）

    [Header("详情/按钮")]
    [SerializeField] private TextMeshProUGUI detailText;  // 选中装备的分解产出预览
    [SerializeField] private Button dismantleButton;      // 分解按钮
    [SerializeField] private TextMeshProUGUI dismantleButtonText;

    private Inventory_Item selectedItem;   // 当前选中装备
    private PlayerInventorySystem invSys;  // 背包系统引用

    private void OnEnable()
    {
        invSys = FindAnyObjectByType<PlayerInventorySystem>();
        RefreshList();
    }

    // 刷新背包装备列表
    public void RefreshList()
    {
        if (itemListRoot == null)
            return;

        foreach (Transform child in itemListRoot)
            Destroy(child.gameObject);

        var inv = invSys != null ? invSys.GetInventory() : null;
        if (inv == null)
            return;

        foreach (var kvp in inv.itemDictionary)
        {
            var item = kvp.Value;
            if (item == null || !item.IsEquipment)
                continue;

            if (itemRowPrefab == null)
                continue;

            var row = Instantiate(itemRowPrefab, itemListRoot);
            var rowScript = row.GetComponent<UI_DismantleRow>();
            if (rowScript != null)
                rowScript.Setup(this, item);
        }
    }

    // 选中装备（由行回调）
    public void SelectItem(Inventory_Item item)
    {
        selectedItem = item;
        RefreshDetail();
    }

    // 刷新产出预览 + 分解按钮
    public void RefreshDetail()
    {
        if (selectedItem == null)
            return;

        bool canDismantle = invSys != null && dismantleTable != null;

        if (detailText != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"分解: {selectedItem.itemData.itemName}");
            var output = DismantleSystem.CalculateOutput(selectedItem, dismantleTable);
            if (output.Count == 0)
            {
                sb.AppendLine("（此装备无分解产出）");
                canDismantle = false;
            }
            else
            {
                foreach (var o in output)
                    sb.AppendLine($"  → {o.material.itemName} ×{o.count}");
            }
            detailText.text = sb.ToString();
        }

        if (dismantleButton != null)
            dismantleButton.interactable = canDismantle;
        if (dismantleButtonText != null)
            dismantleButtonText.text = canDismantle ? "分解" : "不可分解";
    }

    // 点击分解
    public void OnClickDismantle()
    {
        if (selectedItem == null || invSys == null || dismantleTable == null)
            return;

        if (DismantleSystem.TryDismantle(selectedItem, invSys, dismantleTable))
        {
            selectedItem = null;
            RefreshList();
            RefreshDetail();
        }
    }
}

// 单个装备行 — 由 UI_DismantlePanel 实例化
public class UI_DismantleRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;  // 装备名
    [SerializeField] private Button rowButton;          // 行点击

    private Inventory_Item item;        // 本行装备
    private UI_DismantlePanel panel;    // 父面板

    public void Setup(UI_DismantlePanel panel, Inventory_Item item)
    {
        this.panel = panel;
        this.item = item;

        if (nameText != null)
        {
            string rarityName = item.actualRarity.HasValue ? RarityCalculator.GetRarityName(item.actualRarity.Value) : "";
            nameText.text = $"{(rarityName.Length > 0 ? $"[{rarityName}] " : "")}{item.itemData.itemName}";
            nameText.color = item.actualRarity.HasValue ? RarityCalculator.GetRarityColor(item.actualRarity.Value) : Color.white;
        }

        if (rowButton != null)
            rowButton.onClick.AddListener(() => panel.SelectItem(item));
    }
}
