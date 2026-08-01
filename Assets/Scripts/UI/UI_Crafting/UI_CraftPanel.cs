using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 制作面板（F9）— 显示配方列表 + 材料需求 + 制作按钮
// 接线：Canvas 面板挂此脚本，配方行用 recipeRowPrefab 实例化
// 退出按钮在铁匠面板（UI_BlacksmithPanel）上，本面板由 PanelSwitcher 切换显示
public class UI_CraftPanel : MonoBehaviour
{
    [Header("数据")]
    [SerializeField] private CraftingRecipeDB recipeDB;   // 制作配方数据库（Inspector 拖入）

    [Header("列表")]
    [SerializeField] private Transform recipeListRoot;    // 配方行的父节点（ScrollView Content）
    [SerializeField] private GameObject recipeRowPrefab;  // 配方行 prefab（需含：名称Text + 状态Text + Button）

    [Header("详情/按钮")]
    [SerializeField] private TextMeshProUGUI detailText; // 选中配方的材料/产物详情
    [SerializeField] private TextMeshProUGUI goldText;   // 持有货币文本（金/银/铜富文本显示）
    [SerializeField] private Button craftButton;         // 制作按钮
    [SerializeField] private TextMeshProUGUI craftButtonText; // 制作按钮文字（"制作" / "材料不足"）

    private CraftingRecipe selectedRecipe;               // 当前选中配方
    private PlayerInventorySystem invSys;                // 玩家背包系统引用
    private readonly List<UI_CraftRow> rows = new();     // 当前配方行（选中视觉切换用，RefreshList 重建时填充）

    private void Awake()
    {
        if (craftButton != null)
            craftButton.onClick.AddListener(OnClickCraft); // 制作按钮绑定
    }

    // 打开/关闭面板（由铁匠面板 PanelSwitcher 控制显示）
    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    private void OnEnable()
    {
        invSys = FindAnyObjectByType<PlayerInventorySystem>();
        RefreshList();
    }

    // 刷新配方列表 + 持有货币显示
    public void RefreshList()
    {
        // 持有货币（金/银/铜富文本，与商店同进制：1金=10000铜 1银=100铜）
        if (goldText != null && invSys != null)
        {
            var amt = CurrencyFormatter.Split(invSys.GetCurrency());
            goldText.text = $"<color=#FFD700>金:{amt.gold}</color>  <color=#C0C0C0>银:{amt.silver}</color>  <color=#CD7F32>铜:{amt.copper}</color>";
        }

        if (recipeListRoot == null || recipeDB == null || recipeDB.recipes == null)
            return;

        // 清空旧行（旧行销毁，选中引用一并清除，防止残留）
        rows.Clear();
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
            AudioManager.Instance?.RegisterButton(row.GetComponent<Button>()); // 配方行按钮注册点击音效（与任务节点一致）
            var rowScript = row.GetComponent<UI_CraftRow>();
            if (rowScript == null)
                rowScript = row.AddComponent<UI_CraftRow>(); // 预制体未挂脚本时运行时自动补挂（Unity 不识别同文件第二个类，无法 Inspector 挂载）
            rowScript.Setup(this, recipe);
            rowScript.RefreshState(invSys);
            rows.Add(rowScript);
        }
    }

    // 配方行点击选中视觉：放大当前行，恢复其他行（点击后保持放大）
    public void OnRowClicked(UI_CraftRow row)
    {
        foreach (var r in rows)
        {
            if (r != null && r != row)
                r.SetSelected(false);
        }
        row.SetSelected(true);
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
                    sb.AppendLine($"{m.material.itemName} × {m.count}（已有 {have}）");
                }
            }
            var cost = CurrencyFormatter.Split(selectedRecipe.goldCost);
            sb.AppendLine($"费用: <color=#FFD700>金:{cost.gold}</color> <color=#C0C0C0>银:{cost.silver}</color> <color=#CD7F32>铜:{cost.copper}</color>");
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

    // 引用兜底：prefab 序列化引用丢失时，运行时按子对象名自动查找（保证文本能显示）
    private void EnsureReferences()
    {
        if (rowButton == null)
            rowButton = GetComponent<Button>();
        if (nameText != null && stateText != null)
            return;
        foreach (var t in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (t.gameObject.name == "名称" && nameText == null)
                nameText = t;
            if (t.gameObject.name == "状态" && stateText == null)
                stateText = t;
        }
    }

    // 初始化本行
    public void Setup(UI_CraftPanel panel, CraftingRecipe recipe)
    {
        this.panel = panel;
        this.recipe = recipe;
        EnsureReferences();

        if (nameText != null)
            nameText.text = recipe.resultItem != null ? recipe.resultItem.itemName : recipe.recipeName;

        if (rowButton != null)
            rowButton.onClick.AddListener(() =>
            {
                panel.OnRowClicked(this); // 选中视觉：放大本行，恢复其他行
                panel.SelectRecipe(recipe);
            });
    }

    // 选中状态：点击后保持放大 / 恢复其他行（走 UI_ButtonEffect，hover 在其基础上叠加不覆盖选中）
    public void SetSelected(bool selected)
    {
        if (rowButton == null)
            return;
        var effect = rowButton.GetComponent<UI_ButtonEffect>();
        if (effect != null)
        {
            effect.SetSelected(selected);
            return;
        }
        // fallback：无 effect 时直接缩放
        rowButton.transform.DOKill();
        if (selected)
            rowButton.transform.DOScale(Vector3.one * 1.05f, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
        else
            rowButton.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    // 刷新可制作状态
    public void RefreshState(PlayerInventorySystem invSys)
    {
        EnsureReferences();
        bool can = invSys != null && CraftingSystem.CanCraft(recipe, invSys);
        if (stateText != null)
        {
            stateText.text = can ? "可制作" : "材料不足";
            stateText.color = can ? Color.green : Color.red;
        }
    }
}
