# ADR-0004: 事件通信采用 C# event + 静态事件总线（QuestEvents）

## Status

Accepted（决策已落地于代码，reverse-doc 补记）

## Date

2026-08-14

## Decision Makers

oy + Claude

## Summary

跨系统通信采用 **C# event Action（点对点订阅）+ 静态事件总线（QuestEvents，广播）** 双模式；拒绝引入 UniRx/消息队列等第三方框架。点对点用于明确上下游（QuestManager.OnQuestAccepted → UI_QuestPanel），静态总线用于多方广播（击杀/收集/对话上报）。UI 阻塞状态通过静态 `ModalStack` + `OnChanged` 事件分发。

## Engine Compatibility

| 字段 | 值 |
|------|-----|
| **Engine** | Unity 6000.4.8f1 |
| **Domain** | Core / Architecture |
| **Knowledge Risk** | LOW |
| **Verification Required** | 订阅/退订对称性（OnEnable/OnDisable 配对），无事件泄漏 |

## Context

### Problem Statement
架构规划期需确定跨系统通信模式，防止耦合爆炸。

### Constraints
- 单机项目，无服务端消息需求
- 团队（独立开发者 + agent）偏好显式、可读的依赖关系
- 不得引入新框架增加心智负担

## Decision

- **点对点 C# event Action**（默认）：明确消费者，如 `QuestManager.OnQuestAccepted`、`Inventory.OnInventoryUpdated`、`SkillPointManager.OnSkillPointsChanged`、`EquipmentSystem.OnEquipmentUpdated`
- **静态事件总线 `QuestEvents`**（广播）：`OnEnemyKilled/OnItemCollected/OnNpcTalked`，由 `Report*` 方法触发，QuestManager 订阅
- **UI 阻塞**：静态 `ModalStack`（Push/Pop/PopAll/Clear + OnChanged），`GameInput.IsGameBlocked` 统一查询
- **词缀事件**：`Enemy.OnEnemyDealtDamage/OnEnemyTookDamage`（实例级，词缀订阅）
- **不采用**：UniRx（学习成本）、消息队列框架（过度设计）、反射式 EventBus（可读性差）

## Consequences

- ✅ 依赖显式、可读，与 architecture.md 数据流一致
- ✅ 订阅/退订配对严格（多数实现无泄漏）
- ⚠️ **输入层依赖倒置**：GameInput 硬引用 `UI_Chat.IsChatFocused`/`UIManager.IsAnyPanelOpen`（Foundation 层依赖上层），建议抽象 `IInputBlockSource` 注册制
- ⚠️ **技能槽输入绕过阻塞**：SkillSlotManager 裸 Input 不查 IsGameBlocked（见 ADR-0001 关联问题）
- ⚠️ 每帧轮询反模式残留：UI_AttributeManager/TreeNode 未订阅事件仍每帧刷新（应改事件驱动）
