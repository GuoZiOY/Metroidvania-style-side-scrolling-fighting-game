---
status: reverse-documented
source: Assets/Scripts/UI/
date: 2026-07-28
verified-by: oy
---

# UI 系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 架构概览

三层架构：**Modal Stack**（输入阻塞）→ **Panel Switcher**（显示路由）→ **Panels**（内容）

```
UI (MonoBehaviour 根节点)
├── UIManager (中央调度)
│   ├── mainPanelSwitcher    ← 顶层标签: Character/Skill/Setting/Quest
│   ├── skillPanelSwitcher   ← 技能子标签
│   └── settingPanelSwitcher ← 设置子标签
│
├── ModalStack (静态) — 管理全屏UI层级
│   IDs: "panel" | "shop" | "npc_menu" | "quest_dialogue"
│
├── 面板们 (预置在场景中, 非动态实例化)
│   ├── UI_SkillTree / UI_Inventory / UI_QuestPanel / UI_Setting
│   ├── UI_ShopPanel (例外: 动态生成商品槽位)
│   └── UI_NpcMenu / UI_QuestDialogue / UI_DeathScreen
│
├── Tooltip 系统
│   ├── UI_ToolTip (基类 + 位置算法)
│   ├── UI_SkillToolTip / UI_StatToolTip / UI_ItemToolTip
│   └── UI_SkillTip / UI_EventTip
│
└── 拖放系统
    ├── UI_ItemDragHandler (物品拖放)
    └── UI_SkillDragHandler (技能拖放)
```

---

## 2. Panel 生命周期

### 打开流程 (UIManager 面板)

```
UIManager.ShowPanel(index):
  1. PanelSwitcher.HideAllPanels() → SetActive(false)
  2. targetPanel.SetActive(true) + DOTween 动画 (ScaleFade / SlideFromRight)
  3. 同步按钮颜色 (UI_ButtonEffect)
  4. OnPanelShown 事件 → UIManager.OnMainPanelShown:
     ├── ModalStack.PopAll("panel") + Push("panel")
     ├── 显示/隐藏: 背景, 技能槽, 任务追踪器
     ├── 隐藏所有 Tooltip
     └── 重置子面板切换器到索引0

关闭流程:
  UIManager.HideAll():
    1. PanelSwitcher.HideAllPanels() → SetActive(false)
    2. OnAllHidden 事件 → UIManager.OnMainPanelHidden:
       ├── ModalStack.Pop("panel")
       ├── 隐藏背景
       └── 显示技能槽 + 任务追踪器
```

### 商店面板 (特殊流程)

```
打开:
  Time.timeScale = 0 (暂停游戏)
  DOTween 弹出动画 (SetUpdate=true → 不受timescale影响)
  ModalStack.Push("shop")
  生成 NPC 商品槽位 (Instantiate → 动态)
  移动背包/装备面板 Transform 到商店 (运行时重新父级)
  订阅玩家槽位点击事件

关闭:
  清理拖拽状态
  Time.timeScale = 1
  ModalStack.Pop("shop")
  取消订阅 → 销毁 NPC 槽位 → 还原背包/装备位置
  UI_NpcMenu.TryReopen() (如果来自NPC)
```

### NPC 对话框

```
UI_NpcMenu:
  打开 → ModalStack.Push("npc_menu")
  子选项触发 → Pop("npc_menu") → 打开子面板
  TryReopen(): 如果无子面板打开 → 重新弹出

UI_QuestDialogue:
  4 种模式: Accept | InProgress | StageComplete | FinalClaim
  阶段完成模式: 2 次点击 (领奖励 → 推进阶段)
```

---

## 3. Modal Stack

```
ModalStack (静态类):
  Stack<string> _stack

  Push(id)  → _stack.Push(id) → OnChanged
  Pop(id)   → if Top() == id: _stack.Pop()  (防止乱序弹出)
  PopAll(id) → 移除所有该 ID 的条目 (切换面板时用)
  Clear()   → 场景切换时清空

  IsAnyModalOpen → _stack.Count > 0
  → GameInput.IsGameBlocked → 阻塞游戏输入
```

当前层级: `panel` < `npc_menu` < `quest_dialogue` < `shop`

---

## 4. 输入路由

### GameInput (静态类)

```
所有 GetKeyDown/GetKey/GetKeyUp 调用 → 先检查 IsGameBlocked

例外 (不受阻塞的 Toggle 动作):
  Escape, Interact, ToggleCharacterPanel, ToggleSkillPanel,
  ToggleSettingsPanel, ToggleQuestPanel
  → 允许在有面板打开时仍能关闭/切换

⚠️ Escape 键竞争:
  UIManager.Update() → HasPanelOpen → HideAll + early return
  UI_ShopPanel.Update() → Close
  UI_Chat.Update() → 关闭聊天
  执行顺序取决于 GameObject 层级 → 无统一调度
```

### 拖放输入

```
使用 Unity EventSystem 接口:
  IBeginDragHandler / IDragHandler / IEndDragHandler
  IPointerDownHandler / IPointerEnterHandler / IPointerExitHandler

拖放处理器与 GameInput 独立 —
使用 EventSystem.current.RaycastAll() 做命中检测
```

---

## 5. 物品拖放系统

```
开始拖放:
  UI_ItemSlot.OnBeginDrag()
    → canvasGroup.alpha = 0.6
    → UI_ItemDragHandler.Instance.StartDrag(item, source, position)
       → 从对象池取拖拽视觉 → 设置图标 → 跟随鼠标

UpdateDragPosition():
  rectTransform.position = Input.mousePosition

EndDrag — 命中检测:
  1. 创建 PointerEventData(mousePosition)
  2. EventSystem.current.RaycastAll()
  3. 遍历结果 → GetComponent<IItemDropTarget>
  4. 跳过源槽位
  5. CanAcceptItem(draggingItem)? → OnItemDropped
  6. 否则 → ReturnItemToSourceSlot (还原状态)

IItemDropTarget 实现:
  UI_InventorySlot:  接受背包↔背包 (Move/Swap)
                     接受装备卸回 (TryUnequipItemToSlot)
  UI_EquipSlot:      仅接受匹配 ItemType
                     装备↔装备交换 (同类型) | 背包→装备
  UI_TrashCan:       接受任何物品 → 卸下 + 从背包永久删除

DropTargetHighlighter:
  IPointerEnter 时检查 CanAcceptItem → 绿色(接受)/红色(拒绝)
```

### 技能拖放 (类似架构, 完全独立)

```
源: UI_TreeNode (技能树) | UI_SkillSlot (快捷栏)
目标: UI_SkillSlot | UI_SpecialSkillSlot
操作: 槽位↔槽位(Swap) | 树→槽位(Bind) | 自动解绑同类技能
```

---

## 6. Tooltip 系统

### 层级

```
UI_ToolTip (基类)
  ├── ShowToolTip(bool, RectTransform) → 位置计算 + 显隐
  ├── UpdatePosition():
  │     ┌─ 默认: 目标右侧 + 下方
  │     ├─ 右边界溢出 → 翻转到左侧
  │     ├─ 下边界溢出 → 翻转到上方
  │     └─ Clamp 到 Canvas 边界
  └── 隐藏: position = (9999, 9999)

UI_SkillToolTip : UI_ToolTip
  技能名称、描述、当前效果、等级、需求、下级奖励
  闪烁文字 (NotEnoughSkillPointsEffect, LockedSkillEffect)

UI_StatToolTip : UI_ToolTip
  属性类型静态描述

UI_ItemToolTip : UI_ToolTip
  物品名称(稀有度颜色) + 类型 + 修饰符或消耗品信息
  LayoutRebuilder.ForceRebuildLayoutImmediate → 动态尺寸
```

### 触发

```
UI_BaseSlot.OnPointerEnter → ui.itemToolTip.ShowToolTip(true, rect, item)
UI_TreeNode.OnPointerEnter → ui.skillToolTip.ShowToolTip(true, rect, this)
UI_StatSlot.OnPointerClick → 切换 ui.statToolTip (点击切换, 非悬停)
```

### 全局隐藏

```
UIManager.HideAllTooltips() → 面板切换时调用
```

---

## 7. 数据绑定模式

### 事件驱动 (良好模式)

```
UI_InGame:        health.OnHealthUpdate + LevelManager.OnLevelUp
UI_Inventory:     inventory.OnInventoryUpdated + OnEquipmentUpdated
UI_QuestPanel:    QuestManager.OnQuestAccepted/OnObjectiveUpdated/...
UI_QuestTracker:  QuestManager.OnTrackChanged/OnQuestReadyToClaim
UI_SkillTree:     SkillPointManager.OnSkillPointsChanged
UI_SkillSlot:     SkillSlotManager.OnSkillSlotChanged
ShopSystem→UI:    OnDataChanged → RefreshUI()
```

### 轮询模式 (⚠️ 可优化)

```
UI_AttributeManager.Update()  → 每帧 UpdateDisplay() 轮询属性点
UI_AttributeButton.Update()   → 每帧检查按钮交互状态
UI_TreeNode.Update()          → 每帧 UpdateLevelUpButtonState()
HealthBar.Update()            → 每帧 GetHealthPercent() (缓冲条必需)
```

---

## 8. 重构重复项

| 重复内容 | 出现位置 | 建议 |
|----------|----------|------|
| FindSkillNodeByType | UI_TreeNode, UI_SkillToolTip | 移到 UI_SkillTree |
| GetStatNameByType (switch) | UI_StatSlot, UI_ItemToolTip | 共享工具类 |
| IsPercentageStat | UI_StatSlot, UI_ItemToolTip | 共享工具类 |
| 物品/技能拖放处理器 | UI_ItemDragHandler, UI_SkillDragHandler | 泛型基类 |
| 货币显示 | UI_ShopPanel (12字段), UI_ShopSlot (6字段) | 可复用组件 |

---

## 9. 发现的问题

| 严重度 | 问题 | 位置 |
|--------|------|------|
| 🟡 中 | Escape 键竞态: 多个 Update() 独立处理 | UIManager/ShopPanel/Chat |
| 🟡 中 | Update() 轮询反模式: AttributeManager 等每帧刷新 | UI_AttributeManager |
| 🟡 中 | 无 UI 状态机 — 布尔标志 + 字符串 ID 做 ad-hoc 追踪 | 全局 |
| 🟢 低 | UI_BaseSlot.Awake() FindAnyObjectByType 回退可能返回 null | UI_BaseSlot |
| 🟢 低 | UI_ItemToolTip.ShowToolTip 对 show=true 调用 base 两次 | UI_ItemToolTip |
| 🟢 低 | UI_SkillToolTip.GetColoredText 用 new 隐藏基类方法 | UI_SkillToolTip |
| 🟢 低 | 部分中文注释乱码 (编码问题) | UI_TreeConnectHandler 等 |

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
