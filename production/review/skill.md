# 技能系统审查报告

> 审查日期：2026-08-14 | 代码基线：git HEAD `7c7b82c`
> 审查方式：定向深读（Skill_Base / SkillDataManager / SkillPointManager / PlayerState 技能调用）+ 24 文件目录全量核查

## 1. 框架结构

### 1.1 核心类与继承体系（四路并行架构）

```
A. Skill_Base 组件层 (Assets/Scripts/SkillSyetem/Skill/)
   Skill_Base (MonoBehaviour 基类: cooldown/upgradeType/currentLevel/upgradeTypeLevels)
   ├── Skill_Dash          ├── Skill_DashBlur
   ├── Skill_DoubleJump    ├── Skill_ElementalMastery
   ├── Skill_ElementalBase └── 元素系: Skill_Fire / Skill_Ice / Skill_Lighting
   ├── Skill_TimeEcho      ├── Skill_Shard
   ├── Skill_DomainExpansion ├── Skill_PowerCounterChase
   └── Skill_BagExpand     └── (11+ 技能组件)

B. SkillDataManager (单例) —— 中心数据缓存: Dictionary<SkillUpgradeType, SkillDataCache>
   ├── UpdateSkillData/RemoveSkillData (技能树调用)
   ├── OnSkillDataUpdated / OnPassiveSkillUpdated 事件

C. SkillSlotManager —— 5 槽位 (H/Y/U/I/O) 拖拽绑定/解绑/交换 + 每帧冷却显示
   └── HandleSkillSlotInput 每帧裸 Input.GetKeyDown(GetSlotKey(i)) ⚠️

D. PassiveSkillManager —— 被动技能激活即生效
SkillPointManager —— 技能点 (total/used/available) + OnSkillPointsChanged 事件
SkillObject/ 系列 —— 技能实体对象 (领域展开/碎片/时间回响/治疗等 7 个)
```

### 1.2 装配方式
- **组件模式**：技能作为子组件挂在 Player 下（`GetComponentInParent<Player>`），`Player_SkillManager` 聚合持有
- **数据驱动**：`Skill_DataSo` ScriptableObject 配置（Resources/Data/StillData 目录）；`LevelData` 定义每级 cooldown/damageScaleData
- **升级链**：`SkillDataManager.UpdateSkillData` → 缓存 → 事件 → 槽位/被动响应

### 1.3 模块划分评价
- ✅ 四路并行职责分离（数据缓存 / 槽位管理 / 被动 / 技能行为）与 architecture.md C2 描述一致
- ⚠️ **目录名拼写**：`SkillSyetem/`（应为 SkillSystem）；`Skill0bject_DomainExpansion.cs` 文件 '0' 字母混用
- ⚠️ `Skill_Base.cs` 115 行内 upgradeType/currentLevel/totalLevel 多字段并存，状态冗余

## 2. 工作流程

### 2.1 技能释放（TryUseSkill）
1. `PlayerState.Update`（基类）→ `skillManager.dash.StartSkillCooldown()` + `ChangeState(dashState)`（冲刺）；DomainExpansion 类似
2. 技能槽：`SkillSlotManager.HandleSkillSlotInput` 每帧裸检 `Input.GetKeyDown(GetSlotKey(i))` → 触发槽位技能
3. `Skill_Base.TryUseSkill`（虚方法，子类覆写：位移/伤害/召唤/领域展开等）
4. `CanUseSkill` 检查：upgradeType != None + 冷却完成 + （派生）条件

### 2.2 技能升级/加点
1. `SkillPointManager.AllocatePoint` → `OnSkillPointsChanged` 事件 → UI 技能树刷新
2. 技能树节点 → `SkillDataManager.UpdateSkillData(upgradeType, data, level)` → 事件 → `Skill_Base.SetSkillLevelData`（绑定升阶标识 + 记录等级 + 重置冷却）
3. 被动技能：`OnPassiveSkillUpdated` → `PassiveSkillManager` 立即激活效果

### 2.3 槽位绑定
1. UI 技能树拖拽 → `SkillSlotManager.BindSkillToSlot/UnbindSkillFromSlot`
2. 5 槽位映射 H/Y/U/I/O（`GetSlotKey` 读 GameInput 绑定）
3. `UI_SkillSlot.Update` 每帧刷新冷却显示

### 2.4 存档链路
`SaveManager` → `SkillDataManager` 恢复等级（Resources.Load 加载 Skill_DataSo）→ `SkillSlotManager` 恢复槽位绑定 → UI 刷新（`RefreshAllUI` 用 `lastLoadedSkills`）

## 3. 信息链路

| 链路 | 方向 | 说明 |
|------|------|------|
| `OnSkillDataUpdated` | SkillDataManager → UI 技能树 | 升级后刷新 |
| `OnPassiveSkillUpdated` | SkillDataManager → PassiveSkillManager | 被动立即生效 |
| `OnSkillPointsChanged` | SkillPointManager → UI | 技能点变化（UI_AttributeButton 等未订阅，见问题） |
| `OnSkillPointsChanged` | PlayerLevelManager → UI | **双系统技能点事件**（见问题 M-1） |
| 输入 | GameInput/裸 Input → SkillSlotManager | Alpha 键位 + H/Y/U/I/O |
| 属性 | Skill_Base → Entity_Stats | 元素技能读 InputElement/属性 |

## 4. 与设计文档一致性

| 设计承诺 | 实现状态 |
|----------|----------|
| 四路并行架构（Skill_Base + SkillDataManager + SkillSlotManager + PassiveSkillManager） | ✅ 一致 |
| 5 槽位拖拽绑定 | ✅ 落地 |
| 升级分支（冲刺系/碎片系/时间回响系/领域系） | ✅ 落地（Skill_* 系列对应） |
| 被动技能解锁即生效 | ✅ 落地（OnPassiveSkillUpdated） |
| 技能输入走 GameInput 阻塞策略 | ❌ 未实现——SkillSlotManager 裸 Input，绕过 IsGameBlocked（见问题 S-2） |
| 宝石镶嵌 (F12) 嵌入技能槽 | ❌ Target 未实现（符合计划） |

## 5. 代码质量

- ✅ `Skill_Base` 升级链注释清晰；`SkillDataManager` 单例防御完整（重复实例销毁）
- ⚠️ **键位冲突（已实证）**：`GameInput.cs` L131-132 `SkillSlot5` 与 `DomainExpansion` 都绑 `KeyCode.O`
- ⚠️ **技能输入绕过 UI 阻塞**：`SkillSlotManager.HandleSkillSlotInput` 每帧裸 `Input.GetKeyDown`，UI 面板打开（timeScale=0）期间技能仍可释放；聊天打字误触发技能（H/Y/U/I/O 恰为技能键）
- ⚠️ 高频 `Debug.Log`（SkillDataManager 升级路径每级打点）
- ⚠️ `Skill0bject_DomainExpansion.cs`/`SkillSyetem/` 命名违规；`SkillObject_Health.cs`/`SkillObject_Shard.cs` 为 GBK 编码（非 UTF-8）

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重
1. **键位冲突 O 键双绑定（已实证）** — `GameInput.cs` L131-132：按下 O 同时触发「技能槽5」与「领域展开」，技能资源双份消耗。
   - 改进：DomainExpansion 改独立键（如 R）；`SetBinding` 增加同键冲突检测（写前扫描、冲突拒绝或自动解绑）。
2. **技能输入绕过 GameInput 阻塞策略（已实证）** — `SkillSlotManager.HandleSkillSlotInput` 裸 `Input.GetKeyDown(GetSlotKey(i))`：
   - ① UI 面板打开期间技能仍可释放（面板设计意图"打开即暂停"被破坏）
   - ② 聊天输入框打字按 H/Y/U/I/O 误触发技能；Y 键同时是 `UI_Chat.toggleKey`，打字输入 y 直接关聊天
   - 改进：改用 `GameInput.GetKeyDown(GameInput.Action.SkillSlot1..5)` 复用阻塞与重绑链路；删除 `GetSlotKey`/裸 Input 依赖；UI_Chat toggleKey 走 GameInput 或移除 Y 默认值。

### 🟡 中等
3. **技能点双系统跟踪（BUG-0024 遗留）** — `PlayerLevelManager.skillPoints` 与 `SkillPointManager.totalSkillPoints/usedSkillPoints` 分别跟踪同一数据，存档分别保存，调试修改一个会导致不一致持久化。
   - 改进：统一单一数据源（SkillPointManager 为唯一真相，PlayerLevelManager 只读）。
4. **Resources.Load 全量加载技能数据** — `SaveManager` 每次读档 `Resources.LoadAll<Skill_DataSo>("Data/StillData")`（路径疑似拼写错误 "StillData"）且不缓存。
   - 改进：建 `SkillLookup` 缓存（仿 ItemLookup）；纳入 Addressables 迁移计划（架构已标 MEDIUM）。

### 🟢 轻微
5. 命名违规：`SkillSyetem/` 目录、`Skill0bject_DomainExpansion.cs`（'0'）、`SkillObject_Health.cs`/`SkillObject_Shard.cs` GBK 编码。
6. `Skill_Base` upgradeType/currentLevel/totalLevel 冗余字段，可收敛。
7. 高频 Debug.Log 收敛为 `#if UNITY_EDITOR` 或移除。
8. 技能对象（SkillObject_*）无对象池，高频技能（碎片/残影）每发 Instantiate/Destroy。

---

## 附：审查结论摘要

- **健康度**：良好。四路并行架构与设计一致，升级/加点/槽位/被动全链路完整；`Skill_Base` 升级链与事件驱动设计清晰。
- **最需优先**：S-1 O 键位冲突（按 O 同时放技能+领域展开）、S-2 技能输入绕过 UI 阻塞（面板打开/聊天时误触发技能）。
- **最值得肯定**：`SkillDataManager` 中心缓存 + 事件分发（主动/被动分离）的设计让"数据、行为、UI"三层解耦到位，被动技能即时生效机制实现简洁。
