using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 分解面板（F10）— 由铁匠面板打开，右侧背包/装备格子点击选中物品 → 预览分解产出 → 分解
// 分解产出 = 配方表反推（CraftingRecipeDB，损耗50%），无需单独配置分解产出表
public class UI_DismantlePanel : MonoBehaviour
{
    [Header("详情/按钮")]
    [SerializeField] private TextMeshProUGUI detailText;      // 选中装备的分解产出预览
    [SerializeField] private Button dismantleButton;          // 分解按钮
    [SerializeField] private TextMeshProUGUI dismantleButtonText;

    private static UI_DismantlePanel instance;

    // 单例（惰性查找：面板未激活时 Awake 不执行，首次访问用 FindAnyObjectByType 找到 inactive 实例）
    public static UI_DismantlePanel Instance
    {
        get
        {
            if (instance == null)
                instance = Object.FindAnyObjectByType<UI_DismantlePanel>(FindObjectsInactive.Include);
            return instance;
        }
    }

    private Inventory_Item selectedItem;   // 当前选中装备（由 UI_BlacksmithPanel 分发）
    private PlayerInventorySystem invSys;  // 背包系统引用

    private void Awake()
    {
        instance = this;
        if (dismantleButton != null)
            dismantleButton.onClick.AddListener(OnClickDismantle); // 分解按钮绑定
    }

    // 打开/关闭面板（由铁匠面板 PanelSwitcher 控制显示）
    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    private void OnEnable()
    {
        invSys = FindAnyObjectByType<PlayerInventorySystem>();
        OnSlotDeselected(); // 切到分解 tab 时重置选中预览（槽位视觉由 UI_BlacksmithPanel 统一清，防止切换残留）
    }

    // 由 UI_BlacksmithPanel 分发：右栏背包/装备格子点击选中该物品（分解）
    public void OnSlotClicked(Inventory_Item item)
    {
        if (item == null)
            return;
        selectedItem = item;
        RefreshDetail();
    }

    // 取消选中（同一槽再点或切换）：清除预览
    public void OnSlotDeselected()
    {
        selectedItem = null;
        if (detailText != null)
            detailText.text = "";
        if (dismantleButton != null)
            dismantleButton.interactable = false;
        if (dismantleButtonText != null)
            dismantleButtonText.text = "未选择";
    }

    // 刷新产出预览 + 分解按钮（分解 = 配方表反推）
    public void RefreshDetail()
    {
        if (selectedItem == null)
            return;

        bool canDismantle = invSys != null;

        if (detailText != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"分解: {selectedItem.itemData.itemName}");
            var output = DismantleSystem.CalculateOutput(selectedItem);
            if (output.Count == 0)
            {
                sb.AppendLine("（此装备无配方，不可分解）");
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
        if (selectedItem == null || invSys == null)
            return;

        if (DismantleSystem.TryDismantle(selectedItem, invSys))
        {
            selectedItem = null;
            RefreshDetail();
        }
    }
}
