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

    // 选中状态，子类可追加额外视觉效果（如商店的 selectionBorder）
    public virtual void SetSelected(bool selected)
    {
        // 基类不做默认表现，子类覆写
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
