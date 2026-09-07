# UI 系统审查报告

> 审查日期：2026-08-14 | 代码基线：git HEAD `7c7b82c`
> 审查方式：定向深读（UIManager / ModalStack / UI_AttributeManager / UI_AttributeButton）+ 61 文件目录全量核查 + 事件链路联动

## 1. 框架结构

### 1.1 核心类与模块划分

```
UIManager (唯一单例, DontDestroyOnLoad)          ← 所有面板显隐/切换/背景/Escape 统一入口
  ├── ModalStack (静态栈)                        ← 状态唯一事实源: Push/Pop/PopAll/Clear + OnChanged
  ├── PanelSwitcher ×3                          ← 主面板/技能子面板/设置子面板切换
  ├── 收编面板: shop/blacksmith/npcMenu/questDialogue/deathScreen/chat/pause/warehouse
  ├── 提示框: skillTip/skillToolTip/itemToolTip/statToolTip/eventTip
  └── BindTabButtons (按钮名匹配挂监听)
UI 面板子系统 (11 组):
  UI_Item/ (背包/装备/仓库/垃圾桶/槽位)  UI_Shop/  UI_Crafting/ (铁匠/制作/合成/分解)
  UI_Skill/ (技能树/节点/连线)           UI_Quest/ (面板/对话/追踪)
  UI_Boss/ (Boss 大血条)                 UI_InGame/ (HUD/聊天/技能槽)
  UI_MainMenu/  UI_Tip/  UI_Warehouse/
```

### 1.2 装配方式
- **单例 + 静态栈**：UIManager 跨场景持久（方案A），面板通过 `ModalStack.Push/Pop` 注册，Escape 按栈顶分发
- **事件驱动**：面板订阅数据系统事件（OnHealthUpdate/OnInventoryUpdated/OnQuestAccepted/OnSkillPointsChanged 等）
- **DOTween**：面板动画（SetUpdate=true 不受 timeScale 影响）

### 1.3 模块划分评价
- ✅ Escape 统一调度（BUG-0021 已修复）：UIManager.Update → HandleEscape → 聊天→栈顶→主面板优先级；UI_Chat/UI_ShopPanel 注释确认"Escape 由 UIManager 统一处理"
- ✅ 模态栈设计：`Pop` 仅弹栈顶（防乱序）、`PopAll`/`Clear` 场景切换清理
- ⚠️ 面板类体积大：UI_ShopPanel 409 行、UI_WarehousePanel 323 行、UI_TreeNode 371 行、UI_QuestDialogue 344 行
- ⚠️ 兼容旧代码：`UIManager.IsAnyPanelOpen => ModalStack.IsAnyModalOpen`（双入口）

## 2. 工作流程

### 2.1 面板开/关
1. 打开：面板.Open() → `ModalStack.Push(id)` → 显示面板 + 背景 →（商店/铁匠等）`Time.timeScale = 0` 暂停
2. Escape：`UIManager.Update` → `HandleEscape`：聊天聚焦优先关聊天 → `ModalStack.Top` 对应面板关闭（`Pop`）→ 恢复 timeScale → 重开 NPC 菜单（如适用）
3. 场景切换：`ModalStack.Clear()` 清栈

### 2.2 数据刷新（两种模式并存）
- **事件驱动**（推荐模式）：UI_SkillTree 订阅 `OnSkillPointsChanged`；UI_QuestPanel/Tracker 订阅 `OnQuestAccepted/OnQuestFailed` 等
- **每帧轮询**（反模式，BUG-0022 遗留）：`UI_AttributeManager.Update` 每帧 `UpdateDisplay()`；`UI_AttributeButton.Update` 每帧刷新按钮态；`UI_TreeNode.Update` 每帧 `UpdateLevelUpButtonState`

### 2.3 拖放
`UI_InventorySlot.OnItemDropped` / `UI_EquipSlot.OnItemDropped` / `UI_TrashCan.OnItemDropped` → `FindAnyObjectByType<PlayerInventorySystem>()`（**每次操作查找**）→ 移动/装备/丢弃

## 3. 信息链路

| 链路 | 方向 | 说明 |
|------|------|------|
| `OnChanged` | ModalStack → GameInput | IsGameBlocked 刷新（**但技能槽未走此链路**，见 skill.md S-2） |
| 数据事件 | 各系统 → UI 面板 | OnHealthUpdate/OnInventoryUpdated/OnQuestAccepted/OnSkillPointsChanged |
| 直接引用 | UI → 数据系统 | FindAnyObjectByType 热路径（拖放/消耗品/面板单例） |
| 输出 | UI → 系统 | 商店购买/出售、技能加点、装备穿戴、任务提交 |

## 4. 与设计文档一致性

| 设计承诺 | 实现状态 |
|----------|----------|
| UIManager + ModalStack + PanelSwitcher（architecture F4） | ✅ 一致（含收编面板统一入口） |
| Escape 统一调度 | ✅ 已修复（BUG-0021） |
| 拖放系统 + Tooltip 系统 | ✅ 落地（IItemDropTarget/ISkillDropTarget） |
| Boss 血条 UI（F7） | ✅ 落地（UI_BossHealthBar 屏幕空间布局） |
| 事件驱动刷新（architecture 推荐模式） | ⚠️ 部分未落地（UI_AttributeManager/TreeNode 仍轮询） |

## 5. 代码质量

- ✅ `ModalStack` 实现精炼（防乱序 Pop、OnChanged 事件）；UIManager 统一入口注释清晰
- ⚠️ **每帧轮询反模式**（BUG-0022）：UI_AttributeManager/UI_AttributeButton/UI_TreeNode 各 `Update()` 每帧刷新，而 `OnSkillPointsChanged` 事件已存在但未订阅
- ⚠️ **FindAnyObjectByType 热路径**（BUG-0023）：UI_ItemSlot/UI_InventorySlot/UI_EquipSlot/UI_TrashCan 每次操作查找 `PlayerInventorySystem`
- ⚠️ 按钮音效双轨（input-audio-vfx.md M-3）：AudioManager 全局 HookSceneButtons 挂 `PlayButtonSfx` + 面板显式调用 → 静态面板双重发声
- ⚠️ 面板类体积偏大（4 个 >300 行）；`UI_StatToolTip .cs` 文件名尾部空格

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重
1. **技能输入绕过 UI 阻塞**（跨系统，已实证） — `SkillSlotManager.HandleSkillSlotInput` 裸 Input 检 H/Y/U/I/O，UI 面板打开（timeScale=0）期间技能仍可释放；聊天打字误触发技能。
   - 改进：技能槽输入改走 `GameInput.GetKeyDown(Action.SkillSlot1..5)`（复用 IsGameBlocked），见 skill.md S-2。

### 🟡 中等
2. **每帧轮询反模式（BUG-0022 遗留）** — UI_AttributeManager/UI_AttributeButton/UI_TreeNode `Update()` 每帧刷新。
   - 改进：改订阅 `OnSkillPointsChanged`（SkillPointManager 已有事件）/`OnAttributesChanged` 事件驱动刷新。
3. **FindAnyObjectByType 热路径（BUG-0023 遗留）** — 拖放/消耗品每次操作查找数据系统。
   - 改进：Awake/Start 缓存引用或 Service Locator。
4. **按钮音效双轨** — 全局挂接 + 显式调用双重发声。
   - 改进：二选一（保留全局挂接删显式调用，或保留显式调用删全局挂接改单轨注册）。

### 🟢 轻微
5. 面板类体积：UI_ShopPanel(409)/UI_WarehousePanel(323)/UI_TreeNode(371) 拆分（Data/View 分离）。
6. `UIManager.IsAnyPanelOpen` 兼容别名可删（已统一到 ModalStack）。
7. `UI_StatToolTip .cs` 文件名尾部空格重命名。
8. 高频事件合并：OnInventoryUpdated 每次 AddItem/RemoveItem 触发，批量操作（合成/分解）可合并刷新。

---

## 附：审查结论摘要

- **健康度**：良好。UIManager + ModalStack 单一事实源架构与设计一致，Escape 统一调度（BUG-0021）与场景切换清栈处理扎实；事件驱动订阅模式已大面积落地。
- **最需优先**：技能输入绕过 UI 阻塞（跨系统严重）、每帧轮询反模式改造、FindAnyObjectByType 热路径缓存。
- **最值得肯定**：ModalStack 防乱序 Pop 与 OnChanged 事件设计精炼；UIManager 将 8 个模态面板"收编统一入口"的改造使 Escape/背景/暂停逻辑完全收敛，是该 UI 架构最成功的部分。
