using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 制作面板（F9）— 显示配方列表 + 材料需求 + 制作按钮
// 接线：Canvas 面板挂此脚本，配方行用 recipeRowPrefab 实例化
public class UI_CraftPanel : MonoBehaviour
{
    [Header("数据")]
    [SerializeField] private CraftingRecipeDB recipeDB;   // 制作配方数据库（Inspector 拖入）

    [Header("列表")]
    [SerializeField] private Transform recipeListRoot;    // 配方行的父节点（ScrollView Content）
    [SerializeField] private GameObject recipeRowPrefab;  // 配方行 prefab（需含：名称Text + 状态Text + Button）

    [Header("详情/按钮")]
    [SerializeField] private TextMeshProUGUI detailText; // 选中配方的材料/产物详情
    [SerializeField] private TextMeshProUGUI goldText;   // 金币显示
    [SerializeField] private Button craftButton;         // 制作按钮
    [SerializeField] private TextMeshProUGUI craftButtonText; // 制作按钮文字（"制作" / "材料不足"）

    private CraftingRecipe selectedRecipe;               // 当前选中配方
    private PlayerInventorySystem invSys;                // 玩家背包系统引用

    private void OnEnable()
    {
        invSys = FindAnyObjectByType<PlayerInventorySystem>();
        RefreshList();
    }

    // 刷新配方列表 + 金币显示
    public void RefreshList()
    {
        // 金币
        if (goldText != null && invSys != null)
            goldText.text = $"金币: {invSys.GetCurrency() / 10000}";

        if (recipeListRoot == null || recipeDB == null || recipeDB.recipes == null)
            return;

        // 清空旧行
        foreach (Transform child in recipeListRoot)
            Destroy(child.gameObject);

        // 实例化配方行
        foreach (var recipe in recipeDB.recipes)
        {
            if (recipe == null || recipe.resultItem == null)
                continue;

            if (recipeRowPrefab == null)
                continue;

            var row = Instantiate(recipeRowPrefab, recipeListRoot);
            var rowScript = row.GetComponent<UI_CraftRow>();
            if (rowScript != null)
            {
                rowScript.Setup(this, recipe);
                rowScript.RefreshState(invSys);
            }
        }
    }

    // 选中配方（由配方行回调）
    public void SelectRecipe(CraftingRecipe recipe)
    {
        selectedRecipe = recipe;
        RefreshDetail();
    }

    // 刷新选中配方详情 + 制作按钮状态
    public void RefreshDetail()
    {
        if (selectedRecipe == null)
            return;

        bool canCraft = invSys != null && CraftingSystem.CanCraft(selectedRecipe, invSys);

        // 详情文本
        if (detailText != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"产物: {selectedRecipe.resultItem.itemName}");
            if (selectedRecipe.materials != null)
            {
                foreach (var m in selectedRecipe.materials)
                {
                    if (m?.material == null)
                        continue;
                    int have = invSys != null ? CraftingSystem.GetMaterialCount(invSys.GetInventory(), m.material) : 0;
                    sb.AppendLine($"{m.material.itemName} ×{m.count}（已有 {have}）");
                }
            }
            sb.AppendLine($"费用: {selectedRecipe.goldCost / 10000} 金币");
            detailText.text = sb.ToString();
        }

        // 制作按钮
        if (craftButton != null)
            craftButton.interactable = canCraft;
        if (craftButtonText != null)
            craftButtonText.text = canCraft ? "制作" : "材料不足";
    }

    // 点击制作按钮
    public void OnClickCraft()
    {
        if (selectedRecipe == null || invSys == null)
            return;

        if (CraftingSystem.TryCraft(selectedRecipe, invSys))
        {
            // 制作成功：刷新列表 + 详情
            RefreshList();
            RefreshDetail();
        }
    }
}

// 单个配方行 — 由 UI_CraftPanel 实例化，展示配方名/可制作状态并回调选中
public class UI_CraftRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;   // 配方名
    [SerializeField] private TextMeshProUGUI stateText;  // "可制作" / "材料不足"
    [SerializeField] private Button rowButton;           // 行点击

    private CraftingRecipe recipe;   // 本行配方
    private UI_CraftPanel panel;     // 父面板引用

    // 初始化本行
    public void Setup(UI_CraftPanel panel, CraftingRecipe recipe)
    {
        this.panel = panel;
        this.recipe = recipe;

        if (nameText != null)
            nameText.text = recipe.resultItem != null ? recipe.resultItem.itemName : recipe.recipeName;

        if (rowButton != null)
            rowButton.onClick.AddListener(() => panel.SelectRecipe(recipe));
    }

    // 刷新可制作状态
    public void RefreshState(PlayerInventorySystem invSys)
    {
        bool can = invSys != null && CraftingSystem.CanCraft(recipe, invSys);
        if (stateText != null)
        {
            stateText.text = can ? "可制作" : "材料不足";
            stateText.color = can ? Color.green : Color.red;
        }
    }
}
