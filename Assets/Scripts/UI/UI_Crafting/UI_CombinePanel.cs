using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 合成面板（F11）— 由铁匠面板打开，右侧背包/装备格子点击收集选中（3-9件）→ 显示成功率 → 合成
// 规则：N 件同底材同稀有度装备合成 1 件（成功稀有度+1/失败不变），保底继承词缀
public class UI_CombinePanel : MonoBehaviour
{
    [Header("合成区")]
    [SerializeField] private TextMeshProUGUI selectedText; // 已选装备 + 成功率显示
    [SerializeField] private Button combineButton;        // 合成按钮
    [SerializeField] private TextMeshProUGUI combineButtonText;

    private static UI_CombinePanel instance;

    // 单例（惰性查找：面板未激活时 Awake 不执行，首次访问用 FindAnyObjectByType 找到 inactive 实例）
    public static UI_CombinePanel Instance
    {
        get
        {
            if (instance == null)
                instance = Object.FindAnyObjectByType<UI_CombinePanel>(FindObjectsInactive.Include);
            return instance;
        }
    }

    private List<Inventory_Item> selectedItems = new();   // 当前选中的装备
    private HashSet<Inventory_Item> selectedSet = new();  // 快速查重
    private PlayerInventorySystem invSys;

    private void Awake()
    {
        instance = this;
        if (combineButton != null)
            combineButton.onClick.AddListener(OnClickCombine); // 合成按钮绑定
    }

    // 打开/关闭面板（由铁匠面板 PanelSwitcher 控制显示）
    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    private void OnEnable()
    {
        invSys = FindAnyObjectByType<PlayerInventorySystem>();
        selectedItems.Clear();
        selectedSet.Clear();
        RefreshCombine();
    }

    // 由 UI_BlacksmithPanel 分发：右栏背包/装备格子点击加入合成选中（3-9件）
    public void OnSlotClicked(Inventory_Item item)
    {
        if (item == null)
            return;
        ToggleSelect(item);
    }

    // 查询物品是否在合成选中列表（UI_BlacksmithPanel 刷新槽位选中视觉用）
    public bool IsSelected(Inventory_Item item)
    {
        return item != null && selectedSet.Contains(item);
    }

    // 切换选中/取消选中
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

        RefreshCombine();
    }

    // 刷新合成区：数量 + 成功率 + 按钮状态
    public void RefreshCombine()
    {
        bool valid = CombineSystem.CanCombine(selectedItems, out string reason);

        // 背包容量：合成产物（装备）需 1 空槽位，背包满时禁用避免溢出
        bool bagHasRoom = invSys != null && invSys.GetInventory() != null && invSys.GetInventory().CanAddItem();
        if (selectedItems.Count >= CombineSystem.MinCount && valid && !bagHasRoom)
            reason = "背包已满";

        if (selectedText != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"已选 {selectedItems.Count} / {CombineSystem.MinCount}-{CombineSystem.MaxCount} 件");
            foreach (var item in selectedItems)
                sb.AppendLine($"  {item.itemData.itemName}");

            if (selectedItems.Count >= CombineSystem.MinCount && valid)
            {
                // 三通道统计（基础成功率按合成指向的目标稀有度递减）
                CombineSystem.GetMaterialAffixStats(selectedItems, out int affixCount, out int qualityScore);
                LootRarity inputRarity = selectedItems[0].actualRarity.Value;
                LootRarity targetRarity = (LootRarity)Mathf.Min((int)inputRarity + 1, (int)LootRarity.传说);
                float baseRate = CombineSystem.GetBaseSuccessRate(targetRarity);
                float affixBonus = CombineSystem.GetAffixCountBonus(affixCount);
                float effRate = CombineSystem.GetEffectiveSuccessRate(selectedItems, targetRarity);
                float bigRate = CombineSystem.GetBigSuccessRate(selectedItems.Count);
                int baseKeep = CombineSystem.GetGuaranteeCount(selectedItems.Count);
                int qualityPerKeep = CombineSystem.GetQualityPerKeep(targetRarity);
                int keepBonus = qualityScore / qualityPerKeep;
                int keepTotal = Mathf.Min(baseKeep + keepBonus, CombineSystem.GetMaxAffixCount(targetRarity));

                sb.AppendLine("");
                sb.AppendLine($"<color=#FFD700>── 成功率（稀有度+1）──</color>");
                sb.AppendLine($"基础 {baseRate * 100f:F0}%（{RarityCalculator.GetRarityName(inputRarity)}→{RarityCalculator.GetRarityName(targetRarity)}）");
                sb.AppendLine($"+ 词缀 {affixBonus * 100f:F0}%（素材{affixCount}个词缀，每词缀+2%）");
                sb.AppendLine($"= <color=#FFD700>{effRate * 100f:F0}%</color>（上限90%）");

                sb.AppendLine($"<color=#FF6B35>── 大成功（换更高底材）──</color>");
                sb.AppendLine($"{bigRate * 100f:F0}%（每件+10%，{selectedItems.Count}件）；稀有度不变");

                sb.AppendLine($"<color=#51CF66>── 保底继承 ──</color>");
                sb.AppendLine($"保底 {keepTotal} 条（基础{baseKeep} + 品质{keepBonus}）");
                sb.AppendLine($"素材品质分{qualityScore}（每{qualityPerKeep}分+1条保底，阶段标准）");
                sb.AppendLine($"<color=#FFD700>突破</color>：完全成功时 {CombineSystem.BreakthroughChance * 100f:F0}% 概率生成一条更高稀有度词缀");

                // 背包满警告：产物无法放入
                if (!bagHasRoom)
                    sb.AppendLine($"<color=red>⚠ 背包已满，无法放入产物</color>");
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

        bool canCombine = selectedItems.Count >= CombineSystem.MinCount && valid && bagHasRoom;
        if (combineButton != null)
            combineButton.interactable = canCombine;
        if (combineButtonText != null)
            combineButtonText.text = canCombine ? "合成" : (selectedItems.Count >= CombineSystem.MinCount && valid ? "背包已满" : "条件不足");
    }

    // 点击合成
    public void OnClickCombine()
    {
        if (invSys == null || selectedItems.Count == 0)
            return;

        var result = CombineSystem.TryCombine(new List<Inventory_Item>(selectedItems), invSys);
        if (result.success)
        {
            // 合成完成：清空选中 + 显示结果提示（小成功/大成功/完全成功/失败）
            selectedItems.Clear();
            selectedSet.Clear();
            if (selectedText != null)
                selectedText.text = BuildResultText(result);
            UpdateCombineButton();
        }
        else if (result.failReason != null)
        {
            Debug.Log($"[UI_CombinePanel] 合成失败: {result.failReason}");
        }
    }

    // 合成结果提示（含小成功/大成功/完全成功/突破/失败）
    private string BuildResultText(CombineResult result)
    {
        string rarityName = RarityCalculator.GetRarityName(result.resultRarity);
        if (result.completeSuccess)
        {
            string breakthroughText = result.breakthrough
                ? $"\n<color=#FFD700>✦ 突破!</color> 额外获得一条更高稀有度词缀"
                : "";
            return $"<color=#FFD700>★ 完全成功!</color>\n换更高底材 + 稀有度提升{breakthroughText}\n获得 <color=#FFD700>{result.product.itemData.itemName}</color>（{rarityName}）";
        }
        if (result.bigSuccess)
            return $"<color=#FF6B35>★ 大成功!</color>\n换更高底材（稀有度不变）\n获得 <color=#FF6B35>{result.product.itemData.itemName}</color>（{rarityName}）";
        if (result.upgraded)
            return $"<color=#51CF66>★ 小成功!</color>\n稀有度+1\n获得 <color=#51CF66>{result.product.itemData.itemName}</color>（{rarityName}）";
        return $"<color=#FF4444>合成失败</color>\n{result.product.itemData.itemName}（{rarityName}）稀有度不变";
    }

    // 合成后按钮置灰（下次选中物品时 RefreshCombine 恢复）
    private void UpdateCombineButton()
    {
        if (combineButton != null)
            combineButton.interactable = false;
        if (combineButtonText != null)
            combineButtonText.text = "已合成";
    }
}
