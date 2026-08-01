using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 合成面板（F11）— 点选同底材同稀有度装备加入合成槽（3-9件），显示成功率并合成
// 交互：点击装备行切换选中/取消；凑够 3-9 件同底材同稀有度可合成
public class UI_CombinePanel : MonoBehaviour
{
    [Header("列表")]
    [SerializeField] private Transform itemListRoot;      // 装备行父节点
    [SerializeField] private GameObject itemRowPrefab;    // 装备行 prefab

    [Header("合成区")]
    [SerializeField] private TextMeshProUGUI selectedText; // 已选装备 + 成功率显示
    [SerializeField] private Button combineButton;        // 合成按钮
    [SerializeField] private TextMeshProUGUI combineButtonText;

    private List<Inventory_Item> selectedItems = new();   // 当前选中的装备
    private HashSet<Inventory_Item> selectedSet = new();  // 快速查重
    private PlayerInventorySystem invSys;

    private void OnEnable()
    {
        invSys = FindAnyObjectByType<PlayerInventorySystem>();
        selectedItems.Clear();
        selectedSet.Clear();
        RefreshList();
        RefreshCombine();
    }

    // 刷新背包装备列表（高亮已选）
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
            var rowScript = row.GetComponent<UI_CombineRow>();
            if (rowScript != null)
                rowScript.Setup(this, item, selectedSet.Contains(item));
        }
    }

    // 点击装备行：切换选中/取消
    public void ToggleSelect(Inventory_Item item)
    {
        if (selectedSet.Contains(item))
        {
            selectedSet.Remove(item);
            selectedItems.Remove(item);
        }
        else
        {
            if (selectedItems.Count >= CombineSystem.MaxCount)
                return; // 超过 9 件上限
            selectedSet.Add(item);
            selectedItems.Add(item);
        }

        RefreshList();       // 刷新高亮
        RefreshCombine();    // 刷新合成区
    }

    // 刷新合成区：数量 + 成功率 + 按钮状态
    public void RefreshCombine()
    {
        bool valid = CombineSystem.CanCombine(selectedItems, out string reason);

        if (selectedText != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"已选 {selectedItems.Count} / {CombineSystem.MinCount}-{CombineSystem.MaxCount} 件");
            foreach (var item in selectedItems)
                sb.AppendLine($"  {item.itemData.itemName}");

            if (selectedItems.Count >= CombineSystem.MinCount && valid)
            {
                float rate = CombineSystem.GetSuccessRate(selectedItems.Count);
                sb.AppendLine($"成功率: {rate * 100f:F0}%");
            }
            else if (selectedItems.Count >= CombineSystem.MinCount)
            {
                sb.AppendLine($"<color=red>{reason}</color>");
            }
            else
            {
                sb.AppendLine("至少选择 3 件同底材同稀有度的装备");
            }
            selectedText.text = sb.ToString();
        }

        bool canCombine = selectedItems.Count >= CombineSystem.MinCount && valid;
        if (combineButton != null)
            combineButton.interactable = canCombine;
        if (combineButtonText != null)
            combineButtonText.text = canCombine ? "合成" : "条件不足";
    }

    // 点击合成
    public void OnClickCombine()
    {
        if (invSys == null || selectedItems.Count == 0)
            return;

        var result = CombineSystem.TryCombine(new List<Inventory_Item>(selectedItems), invSys);
        if (result.success)
        {
            // 合成完成：清空选中 + 刷新
            selectedItems.Clear();
            selectedSet.Clear();
            RefreshList();
            RefreshCombine();
        }
        else if (result.failReason != null)
        {
            Debug.Log($"[UI_CombinePanel] 合成失败: {result.failReason}");
        }
    }
}

// 单个装备行 — 由 UI_CombinePanel 实例化，显示名称+稀有度，点击切换选中
public class UI_CombineRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;  // 装备名
    [SerializeField] private Image highlight;           // 选中高亮背景
    [SerializeField] private Button rowButton;          // 行点击

    private Inventory_Item item;      // 本行装备
    private UI_CombinePanel panel;    // 父面板

    public void Setup(UI_CombinePanel panel, Inventory_Item item, bool isSelected)
    {
        this.panel = panel;
        this.item = item;

        if (nameText != null)
        {
            string rarityName = item.actualRarity.HasValue ? RarityCalculator.GetRarityName(item.actualRarity.Value) : "";
            nameText.text = $"{(rarityName.Length > 0 ? $"[{rarityName}] " : "")}{item.itemData.itemName}";
            nameText.color = item.actualRarity.HasValue ? RarityCalculator.GetRarityColor(item.actualRarity.Value) : Color.white;
        }

        if (highlight != null)
            highlight.enabled = isSelected;

        if (rowButton != null)
            rowButton.onClick.AddListener(() => panel.ToggleSelect(item));
    }

    // 由面板刷新选中状态（可选：重建行时调用）
    public void SetHighlight(bool isSelected)
    {
        if (highlight != null)
            highlight.enabled = isSelected;
    }
}
