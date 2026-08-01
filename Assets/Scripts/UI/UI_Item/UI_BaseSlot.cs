using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

// 物品槽位基类。仅负责图标/堆叠/稀有度背景显示、悬停提示/指示器、选中状态。
// 不包含拖拽、双击装备、右键消耗品逻辑——这些由 UI_ItemSlot 追加。
public abstract class UI_BaseSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Inventory_Item itemInSlot { get; protected set; }

    protected UI ui;
    protected RectTransform rect;

    [Header("背包槽位设置")]
    [SerializeField] protected Image itemIcon;
    [SerializeField] protected TextMeshProUGUI itemStackSize;

    [Header("悬停指示器")]
    [SerializeField] private GameObject hoverIndicator; // 鼠标悬停时显示的边框/发光框

    [Header("选中指示器")]
    [SerializeField] protected GameObject selectedIndicator; // 点击选中时显示的金色边框/发光框
    [SerializeField] private float selectedScale = 1.05f;      // 选中放大倍率

    [Header("稀有度背景设置")]
    [SerializeField] protected Image rarityBackground;

    protected virtual void Awake()
    {
        // 向上查找 UI 组件；找不到则全场景兜底。ShopSlot 可能在独立 Canvas 下。
        ui = GetComponentInParent<UI>();
        if (ui == null)
            ui = FindAnyObjectByType<UI>(FindObjectsInactive.Include);

        rect = GetComponent<RectTransform>();

        if (hoverIndicator != null)
            hoverIndicator.SetActive(false);
    }

    public virtual void UpdateSlot(Inventory_Item item)
    {
        itemInSlot = item;

        if (itemInSlot == null)
        {
            itemStackSize.text = "";
            itemIcon.enabled = false;
            if (rarityBackground != null)
                rarityBackground.enabled = false;
            ClearIndicators(); // 物品移除时清除悬停/选中指示器 + 复位缩放（防残留）
        }
        else
        {
            itemIcon.enabled = true;
            itemIcon.sprite = itemInSlot.itemData.itemIcon;
            itemIcon.color = Color.white;
            itemStackSize.text = itemInSlot.currentStackSize > 1 ? itemInSlot.currentStackSize.ToString() : "";

            UpdateRarityBackground();
        }
    }

    // 选中状态：显示金色选中边框 + 轻微放大（区别于悬停指示器的白色淡光），子类可追加额外效果
    public virtual void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);

        // 放大/恢复动画（SetUpdate 忽略 timeScale，商店/工作台暂停时也正常）
        transform.DOKill();
        if (selected)
            transform.DOScale(Vector3.one * selectedScale, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
        else
            transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    // 槽位激活/隐藏时复位指示器与缩放：面板开关、槽位移动后 SetActive 切换不触发 OnPointerExit，
    // 若不强制清除，悬停/选中指示器会在重新激活后残留（子类可覆写追加额外复位逻辑）
    protected virtual void OnEnable()
    {
        ClearIndicators();
    }

    protected virtual void OnDisable()
    {
        ClearIndicators();
    }

    // 清除悬停/选中指示器 + 复位缩放（防残留的统一入口）
    protected void ClearIndicators()
    {
        if (hoverIndicator != null)
            hoverIndicator.SetActive(false);
        if (selectedIndicator != null)
            selectedIndicator.SetActive(false);
        transform.DOKill();
        transform.localScale = Vector3.one;
    }

    public virtual bool CanAcceptItem(Inventory_Item item)
    {
        return item != null;
    }

    // ==================== 点击 ====================

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        AudioManager.Instance?.PlayButtonSfx();
    }

    // ==================== 悬停 ====================

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (itemInSlot != null && ui != null && ui.itemToolTip != null)
            ui.itemToolTip.ShowToolTip(true, rect, itemInSlot);

        if (hoverIndicator != null)
        {
            hoverIndicator.SetActive(true);

            var legacy = hoverIndicator.GetComponent<Animation>();
            if (legacy != null) { legacy.Stop(); legacy.Rewind(); legacy.Play(); }
            else
            {
                var mecAnim = hoverIndicator.GetComponent<Animator>();
                if (mecAnim != null)
                {
                    mecAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
                    mecAnim.enabled = false;
                    mecAnim.enabled = true;
                }
            }
        }
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        if (ui != null && ui.itemToolTip != null)
            ui.itemToolTip.ShowToolTip(false, null);

        if (hoverIndicator != null)
            hoverIndicator.SetActive(false);
    }

    // ==================== 稀有度 ====================

    protected void UpdateRarityBackground()
    {
        if (rarityBackground == null || itemInSlot == null)
            return;

        LootRarity rarity;
        if (itemInSlot.actualRarity.HasValue)
            rarity = itemInSlot.actualRarity.Value;
        else
            rarity = itemInSlot.itemData.rarity;

        Color rarityColor = RarityCalculator.GetRarityColor(rarity);

        rarityBackground.enabled = true;
        rarityBackground.color = rarityColor;
    }
}
