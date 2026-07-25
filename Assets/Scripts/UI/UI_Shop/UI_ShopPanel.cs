using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 商店面板——纯 UI 层。业务逻辑委托给 ShopSystem。
// 职责：打开/关闭动画、槽位生成、按钮绑定、视觉状态同步。
public class UI_ShopPanel : MonoBehaviour
{
    private static UI_ShopPanel instance;
    public static UI_ShopPanel Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<UI_ShopPanel>(FindObjectsInactive.Include);
            return instance;
        }
        private set => instance = value;
    }

    public static bool IsShopOpen => instance != null && instance.gameObject.activeInHierarchy;

    [Header("商店名称")]
    [SerializeField] private TextMeshProUGUI shopNameText;

    [Header("左栏——NPC 商品")]
    [SerializeField] private Transform npcItemContainer;
    [SerializeField] private GameObject shopSlotPrefab;

    [Header("右栏——移动已有背包/装备槽")]
    [SerializeField] private Transform playerSlotContainer;
    [SerializeField] private Transform backpackSlotParent;
    [SerializeField] private Transform equipSlotParent;

    [Header("面板关联")]
    [SerializeField] private GameObject panelBackground;
    [SerializeField] private GameObject[] hiddenOnOpen;

    [Header("数量控件")]
    [SerializeField] private Slider quantitySlider;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;

    [Header("总价显示")]
    [SerializeField] private TextMeshProUGUI totalGoldText;
    [SerializeField] private Image totalGoldIcon;
    [SerializeField] private TextMeshProUGUI totalSilverText;
    [SerializeField] private Image totalSilverIcon;
    [SerializeField] private TextMeshProUGUI totalCopperText;
    [SerializeField] private Image totalCopperIcon;

    [Header("持有货币显示")]
    [SerializeField] private TextMeshProUGUI curGoldText;
    [SerializeField] private TextMeshProUGUI curSilverText;
    [SerializeField] private TextMeshProUGUI curCopperText;
    [SerializeField] private Image curGoldIcon;
    [SerializeField] private Image curSilverIcon;
    [SerializeField] private Image curCopperIcon;

    [Header("操作按钮")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button closeButton;

    [Header("弹出动画")]
    [SerializeField] private float animDuration = 0.3f;

    private CanvasGroup canvasGroup;

    // ==================== 系统 ====================

    private readonly ShopSystem shopSystem = new();

    // NPC 槽位列表（用于视觉管理和索引查询）
    private readonly List<UI_ShopSlot> npcSlots = new();

    // 节点移动恢复数据
    private Transform backpackOriginalParent;
    private int backpackOriginalIndex;
    private Transform equipOriginalParent;
    private int equipOriginalIndex;

    // 订阅的玩家槽位
    private readonly List<UI_ItemSlot> listenedSlots = new();

    // 视觉选中状态（数据选中状态在 ShopSystem 中）
    private UI_ShopSlot selectedShopSlot;
    private UI_ItemSlot selectedSellSlot;

    // ==================== 生命周期 ====================

    private void Awake()
    {
        instance = this;

        buyButton.interactable = false;
        sellButton.interactable = false;
        gameObject.SetActive(false);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        buyButton.onClick.AddListener(OnBuyClicked);
        sellButton.onClick.AddListener(OnSellClicked);
        closeButton.onClick.AddListener(Close);

        minusButton.onClick.AddListener(OnMinusClicked);
        plusButton.onClick.AddListener(OnPlusClicked);
        quantitySlider.onValueChanged.AddListener(OnQuantityChanged);

        quantitySlider.minValue = 0;
        quantitySlider.wholeNumbers = true;
        quantitySlider.value = 0;

        shopSystem.OnDataChanged += RefreshUI;
    }

    private void Update()
    {
        if (GameInput.GetKeyDown(GameInput.Action.Escape))
            Close();
    }

    // ==================== 打开 / 关闭 ====================

    public void Open(ShopSO shopData, string npcName = "")
    {
        if (shopData == null) return;

        // 关闭所有已打开的面板，避免 IsAnyPanelOpen 状态错乱
        var uiMgr = FindAnyObjectByType<UIManager>();
        if (uiMgr != null) uiMgr.HideAllPanels();

        transform.root.gameObject.SetActive(true);
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = Vector3.one * 0.85f;
        transform.DOKill();
        transform.DOScale(Vector3.one, animDuration).SetEase(Ease.OutBack, 1.3f).SetUpdate(true);
        canvasGroup.DOFade(1f, animDuration * 0.7f).SetUpdate(true);
        Time.timeScale = 0f;

        if (shopNameText != null)
        {
            if (!string.IsNullOrEmpty(npcName))
                shopNameText.text = $"{npcName} 的 {shopData.shopName}";
            else
                shopNameText.text = shopData.shopName;
        }

        var pis = FindAnyObjectByType<PlayerInventorySystem>();
        var inv = pis != null ? pis.GetInventory() : null;

        shopSystem.Open(shopData, pis, inv);

        ClearVisualSelection();

        if (panelBackground != null) panelBackground.SetActive(true);
        SetHiddenObjects(true);

        GenerateNpcItems(shopData);
        MovePlayerSlotsToShop();
        SubscribePlayerSlots();

        ModalStack.Push("shop");
        RefreshUI();
    }

    public void Close()
    {
        transform.DOKill();
        SetHiddenObjects(false);
        if (panelBackground != null) panelBackground.SetActive(false);

        // 清理可能残留的拖拽状态
        if (UI_ItemDragHandler.Instance != null)
            UI_ItemDragHandler.Instance.CleanupDrag();

        shopSystem.Close();

        Time.timeScale = 1f;
        ModalStack.Pop("shop");

        UnsubscribePlayerSlots();
        RestorePlayerSlots();
        ClearVisualSelection();

        foreach (var slot in npcSlots)
            if (slot != null) Destroy(slot.gameObject);
        npcSlots.Clear();

        quantitySlider.SetValueWithoutNotify(0);
        gameObject.SetActive(false);
    }

    // ==================== 背景显隐 ====================

    private void SetHiddenObjects(bool hide)
    {
        foreach (var obj in hiddenOnOpen)
            if (obj != null) obj.SetActive(!hide);
    }

    // ==================== 节点移动 ====================

    private void MovePlayerSlotsToShop()
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

    // ==================== 订阅右栏槽位 ====================

    private void SubscribePlayerSlots()
    {
        UnsubscribePlayerSlots();

        if (playerSlotContainer == null) return;

        var allSlots = playerSlotContainer.GetComponentsInChildren<UI_ItemSlot>(true);
        foreach (var slot in allSlots)
        {
            slot.OnItemSlotClicked += OnPlayerSlotClicked;
            listenedSlots.Add(slot);
        }
    }

    private void UnsubscribePlayerSlots()
    {
        foreach (var slot in listenedSlots)
        {
            if (slot != null)
                slot.OnItemSlotClicked -= OnPlayerSlotClicked;
        }
        listenedSlots.Clear();
    }

    // ==================== 生成左栏 NPC 商品 ====================

    private void GenerateNpcItems(ShopSO shopData)
    {
        foreach (var slot in npcSlots)
            if (slot != null) Destroy(slot.gameObject);
        npcSlots.Clear();

        if (shopData.items == null || npcItemContainer == null) return;

        for (int i = 0; i < shopData.items.Length; i++)
        {
            var entry = shopData.items[i];
            if (entry.itemData == null) continue;
            if (entry.quantity == 0) continue;

            var go = Instantiate(shopSlotPrefab, npcItemContainer);
            var slot = go.GetComponent<UI_ShopSlot>();
            if (slot == null) continue;

            slot.Setup(entry.itemData, UI_ShopSlot.ShopMode.Buying, quantity: entry.quantity);
            slot.OnSlotSelected += OnNpcSlotSelected;
            npcSlots.Add(slot);
        }
    }

    // ==================== 选中处理 ====================

    private void OnNpcSlotSelected(UI_ShopSlot slot)
    {
        int index = npcSlots.IndexOf(slot);
        if (index < 0) return;

        // 同一格再点 = 取消
        if (selectedShopSlot == slot)
        {
            selectedShopSlot.SetSelected(false);
            selectedShopSlot = null;
            shopSystem.DeselectAll();
            return;
        }

        // 切新选中
        DeselectSellSlotVisual();
        if (selectedShopSlot != null)
            selectedShopSlot.SetSelected(false);

        selectedShopSlot = slot;
        selectedShopSlot.SetSelected(true);

        shopSystem.SelectNpcItem(index);
    }

    private void OnPlayerSlotClicked(Inventory_Item item, UI_ItemSlot slot)
    {
        if (item == null) return;
        if (slot is UI_EquipSlot) return;   // 装备槽不可出售

        // 同一物品再点 = 取消
        if (selectedSellSlot == slot && shopSystem.SelectedSellItem == item)
        {
            DeselectSellSlotVisual();
            if (selectedShopSlot != null)
            {
                selectedShopSlot.SetSelected(false);
                selectedShopSlot = null;
            }
            shopSystem.DeselectAll();
            return;
        }

        // 清 NPC 选中
        if (selectedShopSlot != null)
        {
            selectedShopSlot.SetSelected(false);
            selectedShopSlot = null;
        }

        DeselectSellSlotVisual();
        selectedSellSlot = slot;
        selectedSellSlot.SetSelected(true);

        shopSystem.SelectSellItem(item);
    }

    private void DeselectSellSlotVisual()
    {
        if (selectedSellSlot != null)
        {
            selectedSellSlot.SetSelected(false);
            selectedSellSlot = null;
        }
    }

    private void ClearVisualSelection()
    {
        if (selectedShopSlot != null)
        {
            selectedShopSlot.SetSelected(false);
            selectedShopSlot = null;
        }
        DeselectSellSlotVisual();
    }

    // ==================== 数量控件 ====================

    private void OnMinusClicked()
    {
        quantitySlider.value = Mathf.Max(0, Mathf.RoundToInt(quantitySlider.value) - 1);
    }

    private void OnPlusClicked()
    {
        quantitySlider.value = Mathf.Min(quantitySlider.maxValue, Mathf.RoundToInt(quantitySlider.value) + 1);
    }

    private void OnQuantityChanged(float value)
    {
        int qty = Mathf.RoundToInt(value);
        if (qty < 0) qty = 0;

        shopSystem.SetQuantity(qty);

        // 同步 UI（ShopSystem 可能 clamp 了值）
        SyncSliderToSystem();
    }

    // 将 Slider 数值与 ShopSystem.Quantity 对齐（防止递归用 SetValueWithoutNotify）
    private void SyncSliderToSystem()
    {
        int systemQty = shopSystem.Quantity;
        int sliderVal = Mathf.RoundToInt(quantitySlider.value);

        if (sliderVal != systemQty)
            quantitySlider.SetValueWithoutNotify(systemQty);

        if (quantityText != null)
            quantityText.text = systemQty.ToString();
    }

    // ==================== 按钮事件 ====================

    private void OnBuyClicked()
    {
        // 在 TryBuy 前保存索引（TryBuy 售罄时会清空 SelectedNpcIndex）
        int slotIndex = shopSystem.SelectedNpcIndex;

        int added = shopSystem.TryBuy();
        if (added > 0)
        {
            AudioManager.Instance?.PlayButtonSfx();

            // 更新 NPC 槽位视觉（售罄槽位移除或库存更新）
            if (slotIndex >= 0 && slotIndex < npcSlots.Count)
            {
                int stock = shopSystem.GetNpcStock(slotIndex);
                if (stock == 0)
                    RemoveNpcSlot(slotIndex);
                else
                    npcSlots[slotIndex].UpdateStockDisplay(stock);
            }
        }
        else
        {
            AudioManager.Instance?.PlayDenySfx();
        }
    }

    private void OnSellClicked()
    {
        int sold = shopSystem.TrySell();
        if (sold > 0)
        {
            AudioManager.Instance?.PlayButtonSfx();

            // 卖光后清除视觉选中
            if (shopSystem.SelectedSellItem == null)
            {
                DeselectSellSlotVisual();
                ClearVisualSelection();
            }
        }
        else
        {
            AudioManager.Instance?.PlayDenySfx();
        }
    }

    // ==================== UI 刷新（由 ShopSystem.OnDataChanged 触发）====================

    private void RefreshUI()
    {
        RefreshCurrencyDisplay();
        UpdateBuySellButtons();
        UpdateQuantityControls();
        UpdatePriceDisplay();
    }

    private void RefreshCurrencyDisplay()
    {
        var amt = shopSystem.GetPlayerCurrency();

        if (curGoldText != null)
            curGoldText.text = amt.gold.ToString();
        if (curGoldIcon != null)
            curGoldIcon.enabled = true;

        if (curSilverText != null)
            curSilverText.text = amt.silver.ToString();
        if (curSilverIcon != null)
            curSilverIcon.enabled = true;

        if (curCopperText != null)
            curCopperText.text = amt.copper.ToString();
        if (curCopperIcon != null)
            curCopperIcon.enabled = true;
    }

    private void UpdateBuySellButtons()
    {
        // 按钮始终保持可点击（便于播放音效），有效性在 OnClick 中判断
        buyButton.interactable = true;
        sellButton.interactable = true;
    }

    private void UpdateQuantityControls()
    {
        int maxQty = shopSystem.GetLegalMaxQuantity();
        if (maxQty < 1) maxQty = 1;

        quantitySlider.interactable = true;
        minusButton.interactable = true;
        plusButton.interactable = true;

        float curMax = quantitySlider.maxValue;
        if (Mathf.Abs(curMax - maxQty) > 0.01f)
            quantitySlider.maxValue = maxQty;

        // 同步当前值
        SyncSliderToSystem();
    }

    private void UpdatePriceDisplay()
    {
        int total = shopSystem.GetTotalPrice();
        SetMultiPrice(totalGoldText, totalGoldIcon,
                      totalSilverText, totalSilverIcon,
                      totalCopperText, totalCopperIcon,
                      total);
    }

    // 总价始终显示金/银/铜三级，数值为 0 时显示 0
    private void SetMultiPrice(TextMeshProUGUI goldText, Image goldIcon,
                               TextMeshProUGUI silverText, Image silverIcon,
                               TextMeshProUGUI copperText, Image copperIcon,
                               int copperAmount)
    {
        var amt = CurrencyFormatter.Split(copperAmount);

        if (goldText != null) goldText.text = amt.gold.ToString();
        if (goldIcon != null) goldIcon.enabled = true;

        if (silverText != null) silverText.text = amt.silver.ToString();
        if (silverIcon != null) silverIcon.enabled = true;

        if (copperText != null) copperText.text = amt.copper.ToString();
        if (copperIcon != null) copperIcon.enabled = true;
    }

    // ==================== 槽位管理 ====================

    private void RemoveNpcSlot(int index)
    {
        if (index < 0 || index >= npcSlots.Count) return;

        var slot = npcSlots[index];
        if (slot == selectedShopSlot)
        {
            slot.SetSelected(false);
            selectedShopSlot = null;
        }
        npcSlots.RemoveAt(index);
        if (slot != null) Destroy(slot.gameObject);
    }
}
