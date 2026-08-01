using System.Collections.Generic;
using UnityEngine;

// 铁匠面板管理（NPC 铁匠打开）— 左半区三面板由 PanelSwitcher 切换，右半区临时移入背包/装备槽
// 打开时把背包槽+装备槽移到右栏并订阅格子点击，关闭时移回原父（与商店面板同机制）
// 格子点击按当前 tab 分发到分解/合成面板选中
public class UI_BlacksmithPanel : MonoBehaviour
{
    [Header("槽位引用")]
    [SerializeField] private Transform backpackSlotParent;   // 背包槽（UI系统/角色/背包/背包槽）
    [SerializeField] private Transform equipSlotParent;      // 装备槽（UI系统/角色/装备/装备槽）
    [SerializeField] private Transform playerSlotContainer;  // 右栏容器（背包/装备临时移入）

    [Header("切换")]
    [SerializeField] private PanelSwitcher switcher;         // 左半区面板切换器（制作/分解/合成）

    [Header("退出按钮（关闭整个铁匠面板）")]
    [SerializeField] private UnityEngine.UI.Button closeButton;

    [Header("打开时隐藏（可选，避免与角色面板重叠）")]
    [SerializeField] private GameObject[] hiddenOnOpen;

    private Transform backpackOriginalParent;  // 背包槽原父（移回用）
    private int backpackOriginalIndex;
    private Transform equipOriginalParent;     // 装备槽原父（移回用）
    private int equipOriginalIndex;
    private List<UI_ItemSlot> listenedSlots = new(); // 已订阅的槽位（取消订阅用）

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance?.CloseBlacksmith()); // 退出按钮走 UIManager 统一关闭
    }

    // 打开铁匠面板（UIManager.ShowBlacksmith 调用）。UI 级操作（入栈/暂停/背景）已在 UIManager 完成，本方法只负责内容
    public void Open()
    {
        gameObject.SetActive(true);
        MovePlayerSlotsToPanel();
        SubscribePlayerSlots();
        SetHiddenObjects(true);
        if (switcher != null)
            switcher.ShowPanel(0); // 默认显示制作
    }

    // 关闭铁匠面板（UIManager.CloseBlacksmith 调用）。UI 级操作（出栈/恢复/背景）已在 UIManager 完成，本方法只负责内容
    public void Close()
    {
        DeselectSlot();
        RefreshSlotSelection(); // 清空所有槽位选中视觉
        UnsubscribePlayerSlots();
        RestorePlayerSlots();
        SetHiddenObjects(false);
        gameObject.SetActive(false);
    }

    // 外部切换子面板（0=制作 1=分解 2=合成）
    public void ShowPanel(int index)
    {
        if (switcher != null)
            switcher.ShowPanel(index);
        DeselectSlot();
        RefreshSlotSelection(); // 切换 tab 时清空选中视觉
    }

    // 把背包/装备槽移到右栏（商店同款机制）
    private void MovePlayerSlotsToPanel()
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

    // 背包/装备槽移回原父
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

    // 订阅右栏所有槽位点击（背包+装备）
    private void SubscribePlayerSlots()
    {
        UnsubscribePlayerSlots();
        if (playerSlotContainer == null)
            return;

        var slots = playerSlotContainer.GetComponentsInChildren<UI_ItemSlot>(true);
        foreach (var s in slots)
        {
            if (s == null)
                continue;
            s.OnItemSlotClicked += OnSlotClicked;
            listenedSlots.Add(s);
        }
    }

    private void UnsubscribePlayerSlots()
    {
        foreach (var s in listenedSlots)
        {
            if (s != null)
                s.OnItemSlotClicked -= OnSlotClicked;
        }
        listenedSlots.Clear();
    }

    private UI_ItemSlot selectedSlot;  // 当前选中的槽位（分解单选）

    // 格子点击 → 切换选中视觉 + 按当前 tab 分发（分解单选/合成多选）
    private void OnSlotClicked(Inventory_Item item, UI_ItemSlot slot)
    {
        if (item == null || switcher == null)
            return;

        if (switcher.CurrentIndex == 1 && UI_DismantlePanel.Instance != null)
        {
            // 分解：单选切换，同一槽再点取消
            if (selectedSlot == slot)
            {
                DeselectSlot();
                UI_DismantlePanel.Instance.OnSlotDeselected();
            }
            else
            {
                DeselectSlot();
                selectedSlot = slot;
                UI_DismantlePanel.Instance.OnSlotClicked(item);
            }
        }
        else if (switcher.CurrentIndex == 2 && UI_CombinePanel.Instance != null)
        {
            // 合成：多选切换（合成面板内部查重/上限）
            UI_CombinePanel.Instance.ToggleSelect(item);
        }

        RefreshSlotSelection(); // 统一刷新所有槽位的选中视觉
    }

    // 刷新所有订阅槽位的选中视觉（分解看 selectedSlot，合成看合成面板选中列表）
    private void RefreshSlotSelection()
    {
        foreach (var s in listenedSlots)
        {
            if (s == null)
                continue;
            s.SetSelected(IsSlotSelected(s));
        }
    }

    // 判断槽位是否处于选中态
    private bool IsSlotSelected(UI_ItemSlot slot)
    {
        if (slot.itemInSlot == null)
            return false;
        if (switcher.CurrentIndex == 1)
            return selectedSlot == slot;
        if (switcher.CurrentIndex == 2 && UI_CombinePanel.Instance != null)
            return UI_CombinePanel.Instance.IsSelected(slot.itemInSlot);
        return false;
    }

    // 取消当前选中槽位
    private void DeselectSlot()
    {
        if (selectedSlot != null)
        {
            selectedSlot.SetSelected(false);
            selectedSlot = null;
        }
    }

    // 打开时隐藏指定对象（如原角色背包面板），关闭时恢复
    private void SetHiddenObjects(bool show)
    {
        if (hiddenOnOpen == null)
            return;
        foreach (var obj in hiddenOnOpen)
        {
            if (obj != null)
                obj.SetActive(!show);
        }
    }
}
