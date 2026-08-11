using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static WarehouseSystem;

// 仓库面板 —— 纯 UI 层。业务（查询/分类/排序/转移/存档）委托给 WarehouseSystem。
// 职责：打开/关闭动画、移入玩家背包槽、分类/排序/搜索控制、动态重建仓库内容网格。
public class UI_WarehousePanel : MonoBehaviour
{
    [Header("仓库内容网格")]
    [SerializeField] private Transform warehouseGridRoot;    // 仓库内容网格容器（需覆盖仓库区域且可接收射线）
    [SerializeField] private GameObject warehouseSlotPrefab; // 仓库槽位 prefab（UI_WarehouseSlot）

    [Header("玩家槽位（打开时移入本面板，与铁匠面板同机制）")]
    [SerializeField] private Transform backpackSlotParent; // 背包槽（UI系统/角色/背包/背包槽，原父节点）
    [SerializeField] private Transform equipSlotParent;    // 装备槽（UI系统/角色/装备/装备槽，原父节点）
    [SerializeField] private Transform playerSlotContainer; // 右栏容器（背包/装备临时移入）

    [Header("打开时隐藏（可选，避免与角色/背包面板重叠，同商店/铁匠）")]
    [SerializeField] private GameObject[] hiddenOnOpen;    // 打开仓库时隐藏的对象（如原角色背包面板），关闭时恢复

    [Header("查询 / 分类 / 排序")]
    [SerializeField] private TMP_InputField searchInput;     // 搜索输入框
    [SerializeField] private TMP_Dropdown categoryDropdown;  // 分类下拉（全部 + 各 ItemType，代码自动填充）
    [SerializeField] private TMP_Dropdown sortDropdown;      // 排序下拉（默认/类型/稀有度/名称/数量，代码自动填充）
    [SerializeField] private Button sortOrderButton;         // 正序/逆序切换按钮（按钮上文字自动切换）

    [Header("退出按钮（关闭整个仓库面板）")]
    [SerializeField] private UnityEngine.UI.Button closeButton; // 与商店/铁匠面板一致的退出按钮

    [Header("一键存取")]
    [SerializeField] private Button depositAllButton;  // 全部存入（背包→仓库）
    [SerializeField] private Button withdrawAllButton; // 全部取出（仓库→背包）

    [Header("弹出动画")]
    [SerializeField] private float animDuration = 0.3f;

    private CanvasGroup canvasGroup;
    private WarehouseSystem warehouseSystem;
    private readonly List<UI_WarehouseSlot> warehouseSlots = new();

    private ItemType currentCategory = ItemType.None;
    private string currentQuery = "";
    private WarehouseSortMode currentSort = WarehouseSortMode.Default;
    private bool reverseSort; // 是否反向排序（正序/逆序切换）

    // 玩家槽容器移动恢复数据（背包槽 + 装备槽）
    private Transform backpackOriginalParent;
    private int backpackOriginalIndex;
    private Transform equipOriginalParent;
    private int equipOriginalIndex;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 退出按钮走 UIManager 统一关闭（与商店/铁匠一致）
        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseWarehouse());

        BindCategoryDropdown();
        BindSortDropdown();
        if (sortOrderButton != null)
        {
            sortOrderButton.onClick.AddListener(OnSortOrderClicked);
            var label = sortOrderButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = "↑"; // 初始正向（升序）——用通用箭头避免字体缺字形
        }
        if (depositAllButton != null)
        {
            depositAllButton.onClick.AddListener(OnDepositAllClicked);
            var label = depositAllButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = "←"; // 全部存入（背包→仓库，指向仓库格）——用通用箭头避免字体缺字形
        }
        if (withdrawAllButton != null)
        {
            withdrawAllButton.onClick.AddListener(OnWithdrawAllClicked);
            var label = withdrawAllButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = "→"; // 全部取出（仓库→背包，指向背包）
        }
        if (searchInput != null)
            searchInput.onValueChanged.AddListener(OnSearchChanged);

        // 确保仓库区域可接收拖拽放置（无 Graphic 时补一个透明 Image + 存放区组件）
        EnsureDropZone();
    }

    // 正序/逆序切换：反转排序方向并刷新，按钮用符号表示（↑=升序 ↓=降序）
    private void OnSortOrderClicked()
    {
        reverseSort = !reverseSort;
        if (sortOrderButton != null)
        {
            var label = sortOrderButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = reverseSort ? "↓" : "↑";
        }
        RefreshGridSlots();
    }

    // 一键存入：背包全部移入仓库（网格由 Transfer 触发的仓库变更事件自动刷新）
    private void OnDepositAllClicked()
    {
        var ws = warehouseSystem ?? FindAnyObjectByType<WarehouseSystem>();
        if (ws == null)
            return;
        ws.DepositAllFromBackpack();
        AudioManager.Instance?.PlayButtonSfx();
    }

    // 一键取出：仓库全部移回背包
    private void OnWithdrawAllClicked()
    {
        var ws = warehouseSystem ?? FindAnyObjectByType<WarehouseSystem>();
        if (ws == null)
            return;
        ws.WithdrawAllToBackpack();
        AudioManager.Instance?.PlayButtonSfx();
    }

    // 分类下拉：代码填充"全部 + 各 ItemType"，选项索引直接映射 ItemType（0=全部）
    private void BindCategoryDropdown()
    {
        if (categoryDropdown == null)
            return;
        categoryDropdown.ClearOptions();
        var options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData("全部") };
        foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
        {
            if (type == ItemType.None)
                continue;
            options.Add(new TMP_Dropdown.OptionData(type.ToString()));
        }
        categoryDropdown.AddOptions(options);
        categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        categoryDropdown.value = (int)currentCategory; // 默认"全部"
    }

    // 排序下拉：代码填充选项，索引映射 WarehouseSortMode
    private void BindSortDropdown()
    {
        if (sortDropdown == null)
            return;
        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("默认"),
            new TMP_Dropdown.OptionData("类型"),
            new TMP_Dropdown.OptionData("稀有度"),
            new TMP_Dropdown.OptionData("名称"),
            new TMP_Dropdown.OptionData("数量"),
        });
        sortDropdown.onValueChanged.AddListener(OnSortChanged);
        sortDropdown.value = (int)currentSort; // 默认"默认"
    }

    private void EnsureDropZone()
    {
        if (warehouseGridRoot == null)
            return;

        // 无 Graphic 的对象不参与射线检测，补一个几乎透明的 Image
        if (warehouseGridRoot.GetComponent<Graphic>() == null)
        {
            var img = warehouseGridRoot.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.001f);
            img.raycastTarget = true;
        }

        if (warehouseGridRoot.GetComponent<UI_WarehouseDropZone>() == null)
            warehouseGridRoot.gameObject.AddComponent<UI_WarehouseDropZone>();
    }

    // ==================== 打开 / 关闭（由 UIManager 调用）====================

    public void Open()
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = Vector3.one * 0.85f;
        transform.DOKill();
        transform.DOScale(Vector3.one, animDuration).SetEase(Ease.OutBack, 1.3f).SetUpdate(true);
        canvasGroup.DOFade(1f, animDuration * 0.7f).SetUpdate(true);

        warehouseSystem = FindAnyObjectByType<WarehouseSystem>();
        if (warehouseSystem == null)
            Debug.LogWarning("[UI_WarehousePanel] WarehouseSystem 未找到，仓库面板内容不可用");

        // 存放区绑定仓库系统
        var drop = warehouseGridRoot != null ? warehouseGridRoot.GetComponent<UI_WarehouseDropZone>() : null;
        if (drop != null)
            drop.Init(warehouseSystem);

        // 移入玩家背包槽 + 装备槽（供拖拽存入，同商店/铁匠）
        MovePlayerSlotsIn();

        // 隐藏原角色/背包区域，避免移走槽位后留空框重叠（同商店/铁匠）
        SetHiddenObjects(true);

        // 订阅仓库容器变更 → 刷新网格
        if (warehouseSystem != null)
            warehouseSystem.WarehouseInventory.OnInventoryUpdated += OnWarehouseInventoryUpdated;

        // 清掉网格内所有旧槽位（含场景预置的），再按容量重建，防止叠加
        WipeGridRoot();
        EnsureGridSlotCount();
        RefreshGridSlots();
    }

    public void Close()
    {
        transform.DOKill();

        // 清理可能残留的拖拽状态
        if (UI_ItemDragHandler.Instance != null)
            UI_ItemDragHandler.Instance.CleanupDrag();

        if (warehouseSystem != null)
            warehouseSystem.WarehouseInventory.OnInventoryUpdated -= OnWarehouseInventoryUpdated;

        RestorePlayerSlots();
        SetHiddenObjects(false); // 恢复被隐藏的对象
        ClearWarehouseGrid();
        gameObject.SetActive(false);
    }

    // 打开时隐藏指定对象（如原角色背包面板），关闭时恢复（与商店/铁匠一致）
    private void SetHiddenObjects(bool hide)
    {
        if (hiddenOnOpen == null)
            return;
        foreach (var obj in hiddenOnOpen)
            if (obj != null)
                obj.SetActive(!hide);
    }

    // ==================== 玩家槽位移动（背包 + 装备）====================

    private void MovePlayerSlotsIn()
    {
        if (backpackSlotParent != null && playerSlotContainer != null)
        {
            backpackOriginalParent = backpackSlotParent.parent;
            backpackOriginalIndex = backpackSlotParent.GetSiblingIndex();
            backpackSlotParent.SetParent(playerSlotContainer, false);
        }

        if (equipSlotParent != null && playerSlotContainer != null)
        {
            equipOriginalParent = equipSlotParent.parent;
            equipOriginalIndex = equipSlotParent.GetSiblingIndex();
            equipSlotParent.SetParent(playerSlotContainer, false);
        }
    }

    private void RestorePlayerSlots()
    {
        if (backpackSlotParent != null && backpackOriginalParent != null)
        {
            backpackSlotParent.SetParent(backpackOriginalParent, false);
            backpackSlotParent.SetSiblingIndex(backpackOriginalIndex);
            backpackOriginalParent = null;
        }

        if (equipSlotParent != null && equipOriginalParent != null)
        {
            equipSlotParent.SetParent(equipOriginalParent, false);
            equipSlotParent.SetSiblingIndex(equipOriginalIndex);
            equipOriginalParent = null;
        }
    }

    // ==================== 网格刷新 ====================

    private void OnWarehouseInventoryUpdated() => RefreshGridSlots();

    private void OnSearchChanged(string value)
    {
        currentQuery = value;
        RefreshGridSlots();
    }

    private void OnCategoryChanged(int index)
    {
        currentCategory = (ItemType)index; // 索引0=全部(None), 1..N=ItemType
        AudioManager.Instance?.PlayButtonSfx(); // 切换分类时播放按钮音效
        RefreshGridSlots();
    }

    private void OnSortChanged(int index)
    {
        currentSort = (WarehouseSortMode)index;
        AudioManager.Instance?.PlayButtonSfx(); // 切换排序时播放按钮音效
        RefreshGridSlots();
    }

    // 摧毁网格根下所有槽位（含场景预置的），防止与动态槽位叠加
    private void WipeGridRoot()
    {
        if (warehouseGridRoot == null)
            return;
        var oldSlots = warehouseGridRoot.GetComponentsInChildren<UI_WarehouseSlot>(true);
        foreach (var s in oldSlots)
            if (s != null)
                Destroy(s.gameObject);
        warehouseSlots.Clear();
    }

    // 确保网格内有 capacity 个槽位（不足补建，超出隐藏）
    private void EnsureGridSlotCount()
    {
        if (warehouseSystem == null || warehouseGridRoot == null || warehouseSlotPrefab == null)
            return;
        int capacity = warehouseSystem.WarehouseInventory.maxInventorySize;

        for (int i = warehouseSlots.Count; i < capacity; i++)
        {
            var go = Instantiate(warehouseSlotPrefab, warehouseGridRoot);
            var uiSlot = go.GetComponent<UI_WarehouseSlot>();
            if (uiSlot == null)
            {
                Destroy(go);
                continue;
            }
            uiSlot.InitializeWarehouse(warehouseSystem, -1); // 先绑定系统，槽位索引随后按视图填充
            warehouseSlots.Add(uiSlot);
        }

        // 隐藏超出容量的槽位
        for (int i = 0; i < warehouseSlots.Count; i++)
            if (warehouseSlots[i] != null)
                warehouseSlots[i].gameObject.SetActive(i < capacity);
    }

    // 刷新网格：
    //   默认（无搜索/分类/排序）= 固定槽位（像背包）：第 i 格 = 仓库第 i 槽，放哪里就放哪里；
    //   搜索/分类/排序时 = 投影浏览视图：按条件重排显示，此时存入走首个空槽。
    private void RefreshGridSlots()
    {
        if (warehouseSystem == null)
            return;
        var wh = warehouseSystem.WarehouseInventory;
        int capacity = wh.maxInventorySize;

        bool isBrowse = currentSort != WarehouseSortMode.Default
            || currentCategory != ItemType.None
            || currentQuery.Trim().Length > 0;

        if (isBrowse)
        {
            // 浏览视图：按 分类+查询+排序 投影显示；未覆盖位置可存入（走首个空槽）
            var view = warehouseSystem.GetViewItems(currentCategory, currentQuery, currentSort, reverseSort);
            for (int i = 0; i < warehouseSlots.Count && i < capacity; i++)
            {
                var slot = warehouseSlots[i];
                if (slot == null)
                    continue;

                if (i < view.Count)
                {
                    slot.InitializeWarehouse(warehouseSystem, view[i].slot);
                    slot.UpdateSlot(view[i].item);
                }
                else
                {
                    slot.InitializeWarehouse(warehouseSystem, -1);
                    slot.UpdateSlot(null);
                }
            }
        }
        else
        {
            // 固定槽位（像背包）：第 i 格 = 仓库第 i 槽
            for (int i = 0; i < warehouseSlots.Count && i < capacity; i++)
            {
                var slot = warehouseSlots[i];
                if (slot == null)
                    continue;
                slot.InitializeWarehouse(warehouseSystem, i);
                slot.UpdateSlot(wh.GetItemAtSlot(i));
            }
        }
    }

    private void ClearWarehouseGrid()
    {
        foreach (var slot in warehouseSlots)
            if (slot != null)
                Destroy(slot.gameObject);
        warehouseSlots.Clear();
    }
}
