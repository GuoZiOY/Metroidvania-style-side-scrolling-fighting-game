# 任务系统系统审查报告

> 审查日期：2026-08-18 | 代码基线：git HEAD（7c7b82c，2026-08-14）
> 审查范围：`Assets/Scripts/QuestSystem`（6 文件）、`Assets/Scripts/NPCSystem`（3 文件）
> 参考文档：`design/gdd/quest-system.md`（2026-07-28 反向文档）、`design/gdd/save-system.md`、`docs/architecture/architecture.md` v2.0（2026-07-30）

---

## 1. 框架结构

### 1.1 核心类清单

| 类 | 类型 | 行数 | 职责 |
|---|---|---|---|
| `QuestManager` | MonoBehaviour 单例（DontDestroyOnLoad） | **706** | 任务状态机核心：接取/推进/进度/领奖/失败/追踪/存档适配 |
| `QuestData` | ScriptableObject | 130 | 任务静态配置（触发/前置/阶段/奖励/后续/世界状态） |
| `QuestStage` / `ObjectiveConfig` / `QuestReward` / `RewardItem` | [Serializable] 数据类 | — | 阶段、目标（Kill/Collect/TalkToNPC）、奖励配置 |
| `QuestProgress` | QuestManager 内嵌 [Serializable] | — | 运行时进度（currentStageIndex + objectiveProgress[]） |
| `QuestEvents` | 静态事件总线 | 31 | 上游 → 任务系统的输入总线（ReportXXX 模式） |
| `WorldState` | 静态键值存储 | 52 | 全局世界状态 flag（无命名空间、无变更事件） |
| `TargetNameResolver` | 静态工具 | 71 | 从 Resources/CSV 解析 targetId → 显示名（启动时缓存） |
| `QuestUIUtility` | 静态工具 | 74 | 任务 UI 色彩常量/类型名/奖励文本格式化 |
| `NPCBehaviour` | MonoBehaviour | 184 | NPC 交互枢纽：任务查询代理 + 商店/工作台/仓库入口 |
| `QuestIndicator` | MonoBehaviour | 88 | NPC 头顶 !/? 标记（每帧轮询状态） |
| `Gold` | MonoBehaviour | 63 | 金币掉落物（与任务无关，文件归属错误） |

**UI 消费端**（不在审查目录但属本系统信息链路）：`UI_NpcMenu`（对话菜单分发）、`UI_QuestDialogue`（5 模式对话弹窗）、`UI_QuestPanel`（任务日志）、`UI_QuestTracker`（HUD 追踪）、`UI_EventTip`（提示）。

### 1.2 模块划分评估

- **分层基本清晰**：配置（QuestData SO）→ 管理（QuestManager）→ 事件（QuestEvents/WorldState）→ 表现（NPCBehaviour + UI 面板）职责分明，QuestSystem 目录自包含。
- **职责单一性问题**：
  - `QuestManager`（706 行）同时承担 6 类职责：状态机流转、目标匹配与进度、**奖励发放**（直连 4 个下游系统）、**物品扣除**、**存档适配**（10 个 ForSave/Load 方法）、NPC 查询代理。奖励发放与存档适配应抽离为独立模块（如 `QuestRewardDispatcher` / `QuestSaveAdapter`），否则任何下游 API 变更都要求改动核心类。
  - `NPCBehaviour` 是 4 域交互枢纽（任务 + 商店 + 工作台 + 仓库），任务查询方法（HasAvailableQuest 等 8 个）只是 QuestManager 的转发壳，属于"查询代理"而非逻辑，尚可接受，但持续膨胀。
  - `QuestUIUtility` 是 UI 层格式化工具，却放在核心模块 QuestSystem 内，与 architecture.md 的 F4（UI 层）归属不符。

### 1.3 单例与组件模式

- `QuestManager.Instance`：Awake 判重 + DontDestroyOnLoad，标准写法；OnEnable/OnDisable 对称订阅/退订 `QuestEvents`，无泄漏。
- 无 interface 抽象：UI 面板直接引用具体类 `QuestManager.Instance`，下游无 `IQuestManager` 接口，测试与替换困难（与 architecture.md「接口优先」原则有出入，但任务系统属已有代码，未强制）。

---

## 2. 工作流程

### 2.1 击杀目标推进（Kill）

1. 敌人死亡 → `Enemy.EntityDead()`（Enemy.cs:164）
2. → `QuestEvents.ReportEnemyKilled(uniqueID)`（Enemy.cs:170，仅当 uniqueID 非空）
3. → `QuestManager.HandleEnemyKilled`（QuestManager.cs:100）→ `TryProgressObjective(Kill, enemyId, 1)`
4. `TryProgressObjective`（121-168）：遍历**全部进行中任务**（CopyActiveKeys 拷贝键集合防迭代修改）→ 取当前阶段 → 逐目标匹配类型 + targetId（**bare-id 兼容**：`"010101 - 骷髅"` 切到 `"010101"`，139-142 行）→ 未满则 `Mathf.Min(+, requiredCount)` 累加
5. 触发 `OnObjectiveUpdated(questId, i, count)` → UI 面板/追踪器刷新
6. 若本阶段全部目标达成：最终阶段 → `SetQuestReadyToClaim()`（移入 readyToClaimQuests + 发事件）；非最终阶段 → 仅标记完成，等待 NPC 对话推进

### 2.2 收集目标推进（Collect）

1. 拾取 → `ItemAbout.PickupItem`（ItemAbout.cs:107）→ 背包 AddItem 成功 → `QuestEvents.ReportItemCollected(itemId, 1)`（118 行，**count 硬编码 1**）
2. 与 Kill 同管道进入 `TryProgressObjective(Collect, itemId, count)`
3. **接取时预填**：`AcceptQuest` → `SyncCollectProgressFromInventory`（505-524）扫描玩家已有背包，把已持有数量直接填进进度（防止重复收集）
4. **提交时扣除**：对话面板 → `RemoveCollectItemsForQuest`（185 行 UI 调用）→ `ClaimCurrentStageReward` 内部再调 `RemoveCollectItems`（274 行）遍历背包按目标物品扣除 requiredCount，含部分堆叠切割与整组删除，最后 `NotifyUpdate()`

> ⚠️ 注意 2.2 中存在**双重扣物调用**（UI 层一次 + Manager 层一次，二次调用因物品已扣为 no-op），以及**进度与库存解耦**问题，详见第 6 节。

### 2.3 对话目标推进（TalkToNPC）

1. 玩家进入触发区按 F → `NPCBehaviour.Update`（67-88）→ 先关商店再报事件 → `QuestEvents.ReportNpcTalked(npcId)`（81 行）
2. → `HandleNpcTalked` → 同一管道推进（每次按 F 计 1 次对话，即使 NPC 无可交互内容也计数）

### 2.4 接取任务

1. NPC 菜单（`UI_NpcMenu.OnQuestClicked`，94-128）按优先级分发：`HasStageToSubmit` > `HasFinalRewardToClaim` > `HasAvailableQuest` > `HasActiveQuest`
2. → `UI_QuestDialogue.ShowForAccept` → 玩家点「接受任务」→ `OnAcceptClicked` → `QuestManager.AcceptQuest(questId)`
3. `AcceptQuest`（179-210）：重复状态检查（active/readyToClaim/completed 任一命中即拒绝）→ **前置任务检查** `ArePrerequisitesMet`（全部前置须在 completedQuests）→ 初始化第一阶段的 `QuestProgress` → 预填收集进度 → `OnQuestAccepted` + `eventTip.ShowQuestAccepted`

### 2.5 阶段提交 + 领奖 + 推进

1. 阶段完成（非最终）→ NPC 菜单 → `ShowForStageComplete`（UI_QuestDialogue:84）
2. 玩家点主按钮 → `OnStageCompleteClicked`（175-197）：
   - 第一次点击：若含收集目标先 `RemoveCollectItemsForQuest` → `ClaimCurrentStageReward`（防重守卫 claimedStageRewards → `GrantReward(stageReward)` → `RemoveCollectItems`）→ `stageReadyToAdvance=true` → `TransitionAfterClaim`：有下一阶段则 `AdvanceToNextStage`（重置 objectiveProgress）切 InProgress 模式；否则若有最终奖励切 FinalClaim 模式
   - 第二次点击：`AdvanceOrFinish` → 继续推进或切最终领奖

### 2.6 最终领奖

`ClaimFinalReward`（332-373）：readyToClaim 校验 → 移入 completedQuests + 快照 completedQuestProgress → 移出 activeQuests → `GrantReward(finalReward)` → 清理阶段领取记录 → **设置世界状态** `WorldState.Set(flag, true)`（356）→ **自动接取后续任务**（followUpQuestIds 中 autoAccept=true 者，360-367）→ `OnQuestClaimed` + eventTip。

### 2.7 保存 / 读档

- **保存**：`SaveManager.CollectSaveData`（SaveManager.cs:237）→ `CollectQuestData`（438-473，收集 active/stageIndex/readyToClaim/completed/failed/tracked/claimedStageRewards）+ `WorldState.GetSaveData`（274）→ JsonUtility → 文件
- **读档**：`Load`/`LoadWithReload` → 场景切换 → `ApplySaveDataDelayed`（轮询等待 Player+背包+技能系统就绪，超时 5s，再强制等 1 帧保证 Start 已执行）→ `ApplySaveData`（477-537）按序：时间/位置 → 角色 → 属性 → **技能（先于背包，因"背包扩容"被动重算容量）** → 背包 → 仓库 → 装备 → `ApplyQuestData` → `WorldState.LoadFromSave` → 满血 + `Revive` → `RefreshAllUI`
- `QuestManager.LoadFromSave`（672-705）：清空全部运行时集合 → 重建 active 进度（用 objectiveProgress 数组）→ 恢复 4 个集合 + claimedStageRewards + trackedQuestId

### 2.8 NPC 标记刷新

`QuestIndicator.Update`（25-54）：**每帧**轮询 NPCBehaviour 状态 → 待提交/待领奖显示蓝色 ?，可接取显示金色 !，否则隐藏；状态切换时启停浮动 Tween。

### 2.9 任务失败

`MarkQuestFailed`（540-553）已实现（清状态 + `OnQuestFailed` + 提示），但**全代码库无任何调用方**——失败状态机当前不可达（无超时、无失败条件配置）。

---

## 3. 信息链路

### 3.1 输入链路（上游 → 任务系统）

| 事件 | 发布方 | 订阅方 |
|---|---|---|
| `QuestEvents.OnEnemyKilled` | Enemy.cs:170 | 仅 QuestManager（OnEnable:86） |
| `QuestEvents.OnItemCollected` | ItemAbout.cs:118 | 仅 QuestManager（OnEnable:87） |
| `QuestEvents.OnNpcTalked` | NPCBehaviour.cs:81 | 仅 QuestManager（OnEnable:88） |

静态事件总线 + ReportXXX 模式符合 architecture.md 的事件通信决策；单一订阅者，无事件泄漏。

### 3.2 输出链路（任务系统 → UI）

`QuestManager` 7 个 C# event（58-64）：OnQuestAccepted / OnQuestStageChanged / OnQuestReadyToClaim / OnQuestClaimed / OnQuestFailed / OnObjectiveUpdated / OnTrackChanged → 订阅方 `UI_QuestPanel`（OnEnable 订阅 / OnDisable 退订，54-78）、`UI_QuestTracker`（Start 订阅 / OnDestroy 退订，64-84）。

**缺失事件**：阶段奖励领取（ClaimCurrentStageReward）不发任何事件，UI 只能依赖对话面板自身状态机，QuestPanel 无法感知"阶段奖励已领取"。

### 3.3 直接引用 / 查询链

- 单例直连：`QuestManager.Instance`（UI/NPC 侧）、`UIManager.Instance`（NPCBehaviour 菜单开关）
- `FindAnyObjectByType`：`QuestManager.GetInventory()`（43-48，**已缓存**）；`GrantReward` 发金币处（399，**未缓存**，每次发金查找一次）；SaveManager 各处（仅存档/读档时执行，可接受）
- 死代码：`GetPlayerInvSystem()`（49-54）定义后从未被使用，而金币发放处反而绕开它裸调 FindAnyObjectByType

### 3.4 存档链路完整性评估（SaveManager 覆盖检查）

| 数据 | 收集（CollectSaveData） | 恢复（ApplySaveData） | 结论 |
|---|---|---|---|
| active 任务 + 阶段索引 + 进度 | ✓ CollectQuestData（SaveManager.cs:444-464） | ✓ LoadFromSave（QuestManager.cs:684-691） | 完整 |
| readyToClaim / completed / failed | ✓（466-468） | ✓（693-698） | 完整 |
| trackedQuestId | ✓（469） | ✓（704） | 完整 |
| **claimedStageRewards** | ✓（470） | ✓（700-702） | **已修复**（GDD 曾标注致命） |
| **WorldState flags** | ✓（274） | ✓（516-517） | **已修复**（GDD 曾标注重大） |
| completedQuestProgress（已完成任务详情） | ✗ 未序列化 | ✗ | 读档后已完成任务详情丢失（GDD 已标注） |
| QuestSaveEntry.objectives | ✓ 写入（450-462） | ✗ **从不读取**（LoadFromSave 只用 objectiveProgress） | 冗余双份数据 |

**读档 UI 同步缺口**：`ApplySaveData` 恢复任务状态后不发任何任务事件，`RefreshAllUI`（158-182）也只刷新技能/被动/血量——`UI_QuestTracker` 若在暂停菜单读档场景下处于激活状态，追踪栏内容将保持读档前旧值，直到下一次任务事件触发（LoadFromSave 本身也不重发事件）。场景切换读档时因 tracker 重走 Start→RefreshAll 且状态已在 Start 之后恢复，同样存在一帧陈旧窗口。

---

## 4. 与设计文档一致性

### 4.1 GDD（quest-system.md，2026-07-28 反向文档）问题修复核对

| GDD 记录的问题 | 严重度（GDD） | 当前状态（2026-08-18） | 证据 |
|---|---|---|---|
| TalkToNPC 目标永远无法推进 | 🔴 致命 | ✅ **已修复** | NPCBehaviour.cs:81 `QuestEvents.ReportNpcTalked(npcId)` |
| claimedStageRewards 不持久化 | 🔴 致命 | ✅ **已修复** | SaveManager.cs:470 + QuestManager.cs:700-702 |
| WorldState 不存档 | 🟡 重大 | ✅ **已修复** | SaveManager.cs:274 / 516-517 |
| triggerNpcId 字段被忽略 | 🟡 重大 | ❌ **仍存在** | 全库仅 QuestData.cs:100 定义，无运行时消费 |
| ItemPickup 触发类型未实现 | 🟡 重大 | ❌ **仍存在** | QuestTrigger 枚举无任何运行时分发 |
| Kill 目标 count 硬编码 1 | 🟢 低 | ❌ 仍存在 | ItemAbout.cs:118 与 HandleEnemyKilled 调用点 |
| QuestIndicator 每帧轮询 | 🟢 低 | ❌ 仍存在 | QuestIndicator.cs:25 |
| CSV 解析不支持含逗号名字 | 🟢 低 | ❌ 仍存在 | TargetNameResolver.cs:42 裸 Split(',') |

### 4.2 architecture.md 一致性

- ✅ **C12 模块所有权落地**：QuestManager 单例、6 状态生命周期、QuestEvents 总线、WorldState 均与文档一致；C12 V2 变更（修复 TalkToNPC/claimedStageRewards/WorldState 存档）已全部落地
- ✅ **事件通信模式**（C# event + 静态总线）与 Data Flow 章节一致
- ⚠️ **存档恢复顺序与文档不一致**：文档（284-297 行）写 Player→Stats→Inventory→Equipment→Skills→Quests→WorldState；实现（SaveManager.cs:494-517）为 Skills（先于背包）→Inventory→Warehouse→Equipment→Quests→WorldState。实现顺序有明确注释理由（背包扩容被动依赖技能等级），**文档已陈旧需更新**
- ⚠️ **WorldState 桥接承诺未兑现**：P1 世界条件（Target 阶段）依赖 WorldState Set/Has 桥接，但当前 WorldState 除存档外**零读取方**（见 4.3）
- ⚠️ **ADR 缺失**：architecture.md 声明"ADR 数量 0，待创建"，其中 ADR-003（存档系统）、ADR-004（事件通信模式）直接涉及本系统；`docs/architecture/` 下无任何 ADR 文件

### 4.3 设计意图 vs 实现行为差异

1. **WorldState 是"只写"状态**：`QuestManager.ClaimFinalReward` 只写入（356），存档读写（SaveManager），但**没有任何系统读取**（grep `WorldState.Get/Has` 零命中）——任务完成设置的 flag 对当前世界无任何可观察影响，属于"有数据的死设计"（GDD 第 6 节已指出，仍未变化）。
2. **三种接取方式仅 NpcTalk 全功能可用**：`QuestTrigger.NpcTalk` 走 NPC 列表发放 ✓；`AutoUnlock` 仅通过 `followUpQuestIds + autoAccept` 间接生效，无独立轮询/事件接取机制；`ItemPickup` 完全未实现（字段 `triggerItemId` 无人消费）。
3. **GDD 6 状态机与实现一致**，但"已失败"状态因无调用方而不可达（2.9 节）。

---

## 5. 代码质量

### 5.1 注释规范（CLAUDE.md 要求：行内中文注释、禁 `/// <summary>`）

- ✅ **优点**：QuestManager/QuestData/NPCBehaviour 的关键逻辑段落中文行内注释充分且高质量（如数组 resize 原因、阶段完成等待 NPC 的说明、读档顺序的理由），防御性代码有解释。
- ❌ **违反"禁 `/// <summary>`"**：QuestEvents.cs:9/16/25、TargetNameResolver.cs:4、QuestManager.cs:321（共 5 处 XML 注释，与项目规范冲突）。
- ❌ **违反"if 条件后必须换行（含单行）"**：QuestSystem 目录内 **19 处** `if (条件) return/continue/break;` 单行写法（QuestManager.cs 18 处 + TargetNameResolver.cs 1 处），正是 CLAUDE.md 的 ❌ 反例形态。

### 5.2 耦合度

- `QuestManager.GrantReward`（377-419）同时直连 4 个下游单例/系统：`ExperienceManager`/`PlayerLevelManager`、`PlayerLevelManager`/`SkillPointManager`、`PlayerInventorySystem`（金币 + 物品）；且 EXP 与技能点采用"双实现回退"（先 A 后 B），说明下游系统存在双轨并存的历史包袱，任务系统被迫写兼容分支——**这是典型的跨系统强耦合**。
- UI 面板全部持有 `QuestManager.Instance` 具体类，无接口抽象（与 architecture.md「接口优先」的张力，属已有代码债务）。
- `NPCBehaviour` 的 `qm` 属性（140 行）在 8 个查询方法中重复解引用，无缓存问题不大，但 `QuestManager.Instance` 为 null 时各方法返回 false 的静默降级，可能掩盖配置错误。

### 5.3 性能风险

| 位置 | 风险 | 频率 |
|---|---|---|
| `QuestIndicator.Update`（25-54） | 每帧轮询 NPCBehaviour 状态（每 NPC 每帧 3-4 次列表遍历 + 字典查询），**无距离裁剪**，城镇 NPC 多时恒定开销 | 每帧 × 每 NPC |
| `TryProgressObjective` 内 `CopyActiveKeys`（170-175） | 每次击杀/拾取/对话事件分配新 List | 每次战斗事件 |
| `GetQuestData`（566-574） | 线性扫描 allQuestData（事件处理中每任务调用一次），未建 questId → QuestData 字典缓存 | 每次事件 |
| `GrantReward` 金币（399） | 未走缓存 `GetPlayerInvSystem`，裸 `FindAnyObjectByType` | 每次发金 |
| `UI_QuestPanel.RebuildList`（136-165） | 每次刷新全量 Destroy + Instantiate 条目，且 `GetAllQuestData().ToArray()` 每帧分配 | 每次任务事件 |
| `Gold.Update`（19-30） | 落地前每帧 Raycast + `LayerMask.GetMask("Ground")` 参数数组分配 | 每帧 × 每个金币 |

无 Update 中 FindAnyObjectByType；热路径（每帧）仅 QuestIndicator/Gold，其余为事件驱动，总体可接受但可优化。

### 5.4 魔法数字 / 硬编码

- `count=1` 硬编码（ItemAbout.cs:118、HandleEnemyKilled/TalkToNPC 调用点）
- `" - "` 兼容分隔符魔法字符串重复 3 处（QuestManager.cs:140、TargetNameResolver.cs:59、再次比较 294 行）——进度匹配做了 bare-id 归一化，但 **RemoveCollectItems 的物品匹配（294 行）未做归一化**，两者行为不一致（见第 6 节）
- `LayerMask.GetMask("Ground")`（Gold.cs:23）、`Resources.Load("CSV/Items")` 等路径硬编码
- `groundCheckDistance=0.6f`、float 参数等数值均为场景配置，无集中配置表（可接受，但无 tuning 出口）

### 5.5 超大类

- `QuestManager.cs` = **706 行**（>400 阈值），`SaveManager.cs` = 734 行（存档系统，超出本次范围但同属读档链路）、`UI_QuestDialogue.cs` = 409 行（逼近阈值）。

---

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重

**S-1. 任务接取触发机制两套失效（ItemPickup / AutoUnlock / triggerNpcId）**
- 文件：`QuestData.cs:99-102`、`QuestManager.cs`（全文件无 trigger 分发）
- 现状：`QuestTrigger.ItemPickup` 与通用 `AutoUnlock` 无任何运行时实现（仅 `followUpQuestIds + autoAccept` 链式接取可用）；`triggerNpcId` 字段零消费——任何 NPC 可发放列表内任意任务，配置了 triggerNpcId 的任务其接取约束被静默忽略，属于"配置承诺与行为不一致"。
- 改进：在 `QuestManager` 增加触发分发层（`TryTriggerQuest(QuestTrigger, targetId)`）：拾取物品处（ItemAbout 已有收集上报点）加 `QuestEvents.ReportItemPicked(itemId)` 并在此接取 ItemPickup 任务；`ArePrerequisitesMet` 通过后在事件驱动的时机（场景加载/前置任务完成/升级等）轮询 AutoUnlock 任务批量接取；`NPCBehaviour` 发放前校验 `quest.triggerNpcId == npcId`。

**S-2. Collect 目标"进度与库存解耦 + 扣除匹配不一致"导致可白嫖/误扣**
- 文件：`QuestManager.cs:121-168`（进度）、`278-319`（扣除）、`294`（匹配）、`505-524`（预填）
- 现状：进度是纯计数器——玩家接取后收集满 requiredCount，随后把物品卖掉/制作消耗，提交时 `RemoveCollectItems` 发现背包无物 → **照样领奖**（无库存校验）；反向地，进度未满但玩家把物品用掉后进度不回落。且进度匹配做了 bare-id 归一化（139-142），扣除匹配（294 行）却是全等比较，**同一目标两种匹配规则**，旧数据格式下会出现"进度满但扣不掉"。
- 改进：提交领奖时以 `Mathf.Min(库存量, requiredCount)` 重新同步进度（复用 SyncCollectProgressFromInventory 的逻辑），不足则拒绝提交并提示；把 bare-id 归一化抽为 `TargetIdResolver.Normalize(id)` 供进度/扣除/显示三处共用。

**S-3. 任务失败系统不可达（死功能）**
- 文件：`QuestManager.cs:540-553`
- 现状：`MarkQuestFailed` 无任何调用方，`failedQuests` 集合与 `OnQuestFailed` 事件永远不会被触发；GDD 状态机的"已失败"状态形同虚设。
- 改进：明确失败触发源（超时/离开区域/NPC 好感度/世界状态变化）后接入；若 MVP 不需要，建议在 GDD 中降级该状态并在代码注释说明，避免死代码长期滞留。

### 🟡 中等

**M-1. 读档后任务 UI 不同步**
- 文件：`SaveManager.cs:477-537`（ApplySaveData）、`UI_QuestTracker.cs:54-72`、`QuestManager.cs:672-705`
- 现状：LoadFromSave 恢复状态后不发任何事件，RefreshAllUI 不刷新任务面板/追踪器；追踪 HUD 在暂停菜单读档场景会显示读档前的旧任务/进度。
- 改进：`LoadFromSave` 末尾广播一次性恢复事件（如 `OnQuestsRestored`），`UI_QuestTracker`/`UI_QuestPanel` 订阅后全量刷新。

**M-2. WorldState 只写不读（死设计 + 冗余存档）**
- 文件：`WorldState.cs`、`QuestManager.cs:356`、`SaveManager.cs:274/516`
- 现状：任务完成写入 flag、存档落盘，但全库无 `WorldState.Get/Has` 消费方——flag 对世界无任何影响，玩家看到的是"任务完成但世界纹丝不动"。
- 改进：短期至少将 P1 世界条件骨架（监听 flag 的门/宝箱/区域变化）落地为 MVP 可验证的最小形态；否则移除 setWorldFlags 配置以免误导内容策划。

**M-3. 已完成任务详情丢失（completedQuestProgress 不持久化）**
- 文件：`QuestManager.cs:26/342/576-581`、`SaveData.cs`（QuestSaveData 无此字段）
- 现状：`completedQuestProgress` 仅在当前会话内存中保留；读档后 `GetProgress` 对已完成任务返回 null，任务日志（UI_QuestPanel 含 completed 列表）显示空详情（GDD 已标注，未修复）。
- 改进：在 QuestSaveData 增加 `completedProgress` 序列化（复用 QuestSaveEntry），LoadFromSave 恢复。

**M-4. QuestSaveEntry.objectives 冗余双份数据（写入不读取）**
- 文件：`SaveManager.cs:450-462`、`QuestManager.cs:684-691`
- 现状：存档同时写 `objectives`（List）与 `objectiveProgress`（int[]），读档只读后者——存档体积膨胀、双份真相易漂移（改一个漏一个）。
- 改进：删除 `objectives` 字段及写入循环（向后兼容：旧档读 objectiveProgress 即可），或删 `objectiveProgress` 统一用 List。

**M-5. 收集物品双重扣除调用（UI 层 + Manager 层）**
- 文件：`UI_QuestDialogue.cs:185`（`RemoveCollectItemsForQuest`）、`QuestManager.cs:274`（`ClaimCurrentStageReward` 内部 `RemoveCollectItems`）
- 现状：同一批物品被连续处理两次（第二次为 no-op），且 UI 先扣物、Manager 后发奖的顺序使"扣物失败/领奖失败"状态难以判定；`NotifyUpdate` 可能被触发两次。
- 改进：二选一——保留 Manager 内扣除（UI 删掉 185 行调用，`RemoveCollectItemsForQuest` 转为内部方法或删除），或在 UI 层调用后让 `ClaimCurrentStageReward` 跳过内置扣除（加参数/拆分方法）。

**M-6. QuestManager 超大类 + 多职责（706 行）**
- 文件：`QuestManager.cs`
- 改进：抽离 `QuestRewardDispatcher`（GrantReward + 下游单例兼容分支）、`QuestSaveAdapter`（ForSave/Load 全家桶）、目标匹配归一化工具，主类收敛到状态机 + 进度。

**M-7. 阶段奖励领取无事件**
- 文件：`QuestManager.cs:254-276`、`UI_QuestPanel.cs:54-78`
- 现状：ClaimCurrentStageReward 成功后仅靠对话面板内部状态流转，QuestPanel 与 Tracker 无法感知"阶段奖励已领取/物品已提交"（Tracker 的提交按钮态由 HasStageToSubmit 轮询兜底，但面板无即时刷新）。
- 改进：新增 `OnStageRewardClaimed(questId, stageIndex)` 事件，UI 订阅刷新。

**M-8. 存档恢复顺序与 architecture.md 文档不一致**
- 文件：`SaveManager.cs:494-517` vs `docs/architecture/architecture.md:284-297`
- 现状：实现为"技能→背包→仓库→装备→任务→世界状态"，文档写"背包→装备→技能→任务→世界状态"；实现有正当理由（背包扩容被动），文档未同步。
- 改进：更新 architecture.md 数据流章节，注明技能先于背包的依赖原因（防止后续重构误改顺序）。

### 🟢 轻微

**L-1. 注释/风格违规**：`/// <summary>` 5 处（QuestEvents.cs:9/16/25、TargetNameResolver.cs:4、QuestManager.cs:321）；单行 `if (cond) return;` 19 处（QuestManager.cs 为主）。改进：按 CLAUDE.md 统一为 `//` 行内注释 + 条件换行（可一次性批量格式化）。

**L-2. 死代码**：`GetPlayerInvSystem()`（QuestManager.cs:49-54）从未使用，且金币发放（399 行）绕开缓存裸调 FindAnyObjectByType。改进：删除死方法或让 GrantReward 走缓存访问器。

**L-3. 事件热路径 GC**：`CopyActiveKeys` 每次事件分配 List（170-175）；`GetQuestData` 线性扫描（566-574）。改进：事件处理改用"先收集变更再统一提交"或直接遍历 Dictionary（事件内不改集合即可免拷贝）；为 allQuestData 建 `Dictionary<string, QuestData>` 索引。

**L-4. QuestIndicator 每帧轮询**（25-54）：无距离/可见性裁剪。改进：改为事件驱动（订阅 OnQuestAccepted/ReadyToClaim/Claimed/Failed 刷新本 NPC 状态）+ 进入屏幕或靠近时再激活 Update。

**L-5. 文件归属问题**：`Gold.cs`（拾取物逻辑）放在 NPCSystem 目录；`QuestUIUtility`（UI 格式化）放在 QuestSystem 核心模块。改进：按 architecture.md 分层迁移（Gold → 掉落系统/场景对象；QuestUIUtility → UI 层或独立工具目录）。

**L-6. 兼容逻辑散落**：`" - "` bare-id 分隔符魔法字符串 3 处（QuestManager.cs:140、TargetNameResolver.cs:59、以及 294 行缺失处）。改进：收敛为 `TargetIdResolver.Normalize` 静态方法，三处统一调用（同时解决 S-2 的匹配不一致）。

**L-7. 边界健壮性**：CSV 解析不支持逗号/转义（TargetNameResolver.cs:42）；`UI_QuestTracker.Start` 若 `QuestManager.Instance` 为 null 则静默跳过订阅（无重试）；TalkToNPC 对无交互 NPC 的 F 键也计数。

**L-8. 存档版本与迁移**：SAVE_DATA_VERSION=1 无迁移框架（SaveManager.cs:14，save-system.md 已标注），后续 QuestSaveData 结构变更（如 M-3/M-4）需迁移策略。

---

## 附：值得肯定之处

1. **历史致命问题全部修复**：GDD 反向文档标注的 3 个 🔴/🟡 问题（TalkToNPC、claimedStageRewards、WorldState 存档）在当前代码中均已闭环，读档链路（CollectSaveData → ApplySaveData → LoadFromSave）对本系统数据覆盖完整。
2. **事件驱动架构清晰**：输入（QuestEvents 静态总线，ReportXXX）与输出（7 个 C# event）单向流动、单一订阅者，订阅/退订对称，无事件泄漏；无任务状态 Update 轮询（除 QuestIndicator 表现层）。
3. **注释与防御性编程质量高**：数组 resize 兜底、读档顺序的理由注释、"兼容旧数据"的显式说明，体现了良好的工程习惯。

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-18 | System Reviewer | 初始审查（基线 git HEAD 7c7b82c） |
