# 破碎之城 — 全系统审查汇总报告

> **审查日期**: 2026-08-14
> **代码基线**: git HEAD `7c7b82c` (master)
> **方法**: 12 个功能系统并行深度审查（框架结构 / 工作流程 / 信息链路 / 与设计一致性 / 代码质量 / 问题分级）
> **单系统报告**: `production/review/<system>.md`

---

## 一、总览

| 系统 | 报告文件 | 健康度 | 严重问题 | 关键发现 |
|------|----------|--------|----------|----------|
| 玩家系统 | [player.md](player.md) | 良好 | 4 | 注释编码损坏、攻击位移锁死疑云、连击数组越界 |
| 战斗系统 | [combat.md](combat.md) | 良好 | 4 | 源码编码损坏（系统性）、冰抗减免死代码、顿帧目标销毁崩溃、自动元素模式元素身份丢失 |
| 敌人系统 | [enemy.md](enemy.md) | 良好 | 2 | Boss 击败状态不持久化、编码损坏 |
| 技能系统 | [skill.md](skill.md) | 良好 | 2 | **O 键双绑定冲突（已实证）**、技能输入绕过 UI 阻塞（已实证） |
| 物品系统 | [item.md](item.md) | 良好 | 1 | 制作/分解产物不触发收集任务、拖放热路径、rarity 双重应用风险 |
| 制作系统 | [crafting.md](crafting.md) | 良好但文档失配 | 1 | 合成实现与 GDD 分叉、TransferAffix 未实现、合成非原子 |
| 任务系统 | [quest.md](quest.md) | 良好 | 3 | 任务触发机制失效、Collect 进度可白嫖、失败系统死功能 |
| 存档系统 | [save.md](save.md) | 扎实 | 3 | 传送门/Boss 击败不持久化、Load 悬挂风险、版本迁移缺框架 |
| UI 系统 | [ui.md](ui.md) | 良好 | 1 | 技能输入绕过 UI 阻塞（跨系统）、每帧轮询、FindAnyObjectByType 热路径 |
| 区域/传送门 | [area.md](area.md) | 中等 | 3 | **C15 传送门网络未实现**、**F6 能力门控未实现**、状态不持久化 |
| 输入/音频/VFX | [input-audio-vfx.md](input-audio-vfx.md) | 良好 | 2 | 键位冲突 O 键双绑定（已实证）、技能输入绕过 UI 阻塞（已实证） |
| 网络/数据管线 | [network-data.md](network-data.md) | 技术储备 | 2 | 网络层无 GDD、与核心玩法零接线；数据管线（xlsx→csv→SO）良好 |

---

## 二、跨系统重大问题（汇总）

### 🔴 严重 — 系统性

1. **源码注释编码损坏（多系统）** — 10 个 .cs 文件为 GBK 编码（非严格 UTF-8）：`Parallax _Background.cs`、`ParallaxLayer.cs`、`DamageScaleData.cs`、`Enemy_VFX.cs`、`VFX_AutoController.cs`、`SkillObject_Health.cs`、`SkillObject_Shard.cs`、`UI_TreeConnectHandler.cs`、`UI_TreeConnection.cs`、`UI_SkillTip.cs`；另有多文件 UTF-8 但注释字节已乱码（`Entity.cs`、`StateMachine.cs`、`EntityState.cs`、`EnemyState.cs` 等 13+ 文件）。
   - 影响：违反项目注释规范；GBK 文件存在编译/IL2CPP 风险；中文注释无法恢复（需 git 历史）。
   - 建议：批量统一 UTF-8；从早期 git 提交还原注释；`.editorconfig` + `gitattributes` 强制编码。

2. **输入键位冲突 O 键双绑定（已实证）** — `GameInput.cs` L131-132：`SkillSlot5` 与 `DomainExpansion` 都绑定 `KeyCode.O`，按 O 同时触发技能槽 5 与领域展开。
   - 建议：DomainExpansion 改独立键；`SetBinding` 增加同键冲突检测。

3. **技能输入绕过 UI 阻塞（已实证）** — `SkillSlotManager.HandleSkillSlotInput` 裸 `Input.GetKeyDown(GetSlotKey(i))`：UI 面板打开（timeScale=0）期间技能仍可释放；聊天打字（H/Y/U/I/O 为技能键）误触发技能，Y 键同时是聊天 toggleKey。
   - 建议：技能槽输入改走 `GameInput.GetKeyDown(Action.SkillSlot1..5)`，复用 IsGameBlocked 与重绑链路。

4. **Boss 击败状态不持久化（已实证）** — `BossEncounter.defeatedFlag` 仅运行时内存标志（L23/L90），读档后 Boss 重生、出口传送门失效、能力解锁状态错乱。
   - 建议：击败时 `WorldState.Set("boss_xxx_defeated", true)`，场景初始化查询该 flag。

5. **传送门激活状态不持久化 + C15/F6 未实现** — `Portal.cs` 仅运行时组件；`PortalManager`/`AbilityGate` 目录为空。读档后传送门解锁全部丢失；银河城"解锁→新区域"循环缺环（architecture.md F2 V2/F6 承诺未落地）。
   - 建议：实现轻量 `PortalManager`（激活列表入档）+ `AbilityGate`（Boss 击败 flag 解锁）。

6. **任务触发机制失效（已实证）** — `QuestTrigger.ItemPickup`/`AutoUnlock` 无运行时实现；`triggerNpcId` 零消费；`MarkQuestFailed` 无调用方（失败系统死功能）；Collect 目标进度与库存解耦（可白嫖/误扣）。
   - 建议：QuestManager 增加触发分发层 + 提交时库存重同步 + 明确失败触发源或降级。

7. **合成实现与设计分叉** — GDD 定义确定性 3合1 升级，实现为 3-9 件概率四通道；`TransferAffix` 未实现；`DismantleTable` 死代码；architecture.md 仍标记 F9-F13"预留"；合成执行非原子（输入移除与产物入包无回滚）。
   - 建议：设计决策（回归 GDD or 更新 GDD）+ 原子化提交 + 文档同步。

### 🟡 中等 — 代表性

- 战斗：冰抗减免死代码（finalDuration 未传入）、局部顿帧目标销毁 MissingReferenceException、自动元素模式元素身份丢失（`element = InputElement` 应为 `mainElement`）、每攻击 3 次 OverlapCircleAll
- 存档：Load 过渡中重复调用悬挂（pendingLoad 事件泄漏）、版本迁移只有警告无逻辑、写入非原子（无 File.Replace）、SaveManager 734 行超大类
- 任务：读档后任务 UI 不同步、WorldState 只写不读（死设计）、completedQuestProgress 不持久化、QuestSaveEntry 双份数据
- 玩家：WallJumpState 死代码、Player_Combat 职责过重（396 行）、攻击位移锁死疑云（S2 需确认意图）
- 输入：输入层依赖上层 UI/网络（层倒置）、按钮音效双轨注册双重发声、暴击粒子元素着色参数被忽略
- 制作：分解已装备+背包满静默失败、底材池每次 Resources.LoadAll
- 物品：rarityMultiplier 双重应用风险未验证（W-3）
- 敌人：Boss 死亡轮询每帧 FindAnyObjectByType、Boss_SlimeKing 568 行超大
- 网络：网络层无 GDD、与玩法零接线（技术储备定位需决策）

### 🟢 轻微 — 代表性

- 拼写错误批量（canChangeSate/ReciveKnockback/GetDectectedCollders 等，用户已确认保留）
- `/// <summary>` XML 注释违规、单行 if 不换行、魔法数字硬编码、高频 Debug.Log
- 命名违规：SkillSyetem 目录、Skill0bject_DomainExpansion.cs、Enemy_AimatorTriggers..cs、UI_StatToolTip .cs、Parallax _Background.cs
- 文件归属：Gold.cs 在 NPCSystem、QuestUIUtility 在 QuestSystem
- 每帧 FindAnyObjectByType 热路径（读档轮询 5s、UI 槽拖放、Boss 死亡轮询）

---

## 三、跨系统共性结论

1. **注释规范总体执行良好**（新代码行内中文注释），但**编码损坏**使 ~10% 文件注释不可读——最高优先修复项，且不可逆（需 git 历史还原）。
2. **架构分层落地扎实**：Entity+StateMachine、IDamgable 管道、事件通信（C# event + QuestEvents 总线）、Modifier 属性链与 architecture.md 高度吻合；已确认的 Bug（InitializeEnemy/TalkToNPC/WorldState/Burn 等）全部在代码中闭环。
3. **文档-实现漂移是第二大问题**：架构承诺（F2 传送门/Boss 持久化、F6 门控、F9-F13 预留）与实际不符；GDD 双模板（13 英文 8 段 / 15 中文旧 / 3 混合）；player/combat/crafting/input 反向文档均过期。
4. **性能热点集中**：每帧 FindAnyObjectByType（读档轮询、UI 槽、BossEncounter 死亡轮询）、每攻击多次 OverlapCircleAll、每帧 UI Update 轮询（AttributeManager/TreeNode）、Resources.Load 全量加载。
5. **存档链路设计优秀**（恢复顺序注释支撑、满血时机、Start 订阅时序修复），但**持久化覆盖面不足**（传送门/Boss/任务详情/completedProgress）。
6. **未实现的设计缺口集中**：C15 传送门网络、F6 能力门控（MVP 缺环）；F12 宝石镶嵌、P1 世界条件、F13 Boss 追踪（Target 计划内）；网络层（无 GDD 的技术储备）。

---

## 四、优先修复建议（跨系统合并）

| 优先级 | 事项 | 涉及系统 |
|--------|------|----------|
| P0 | 源码编码统一 UTF-8 + 注释还原（git 历史） | 全部 |
| P0 | 键位冲突 O 键解绑 + 技能输入接入 GameInput 阻塞 | 输入/技能/UI |
| P0 | Boss 击败状态持久化（WorldState） | 存档/Boss |
| P0 | 传送门激活状态持久化 + PortalManager | 存档/区域 |
| P1 | 任务触发机制实现（ItemPickup/triggerNpcId/失败源） | 任务 |
| P1 | 战斗：冰抗减免死代码 + 自动元素身份 + 顿帧销毁防护 | 战斗 |
| P1 | 合成系统设计决策 + 原子化 + 文档同步 | 制作/文档 |
| P1 | 实现 F6 能力门控（Boss 解锁能力→新区域） | 区域/敌人 |
| P2 | Load 幂等 + 原子写入 + 版本迁移框架 | 存档 |
| P2 | 文档回写（GDD 模板统一、architecture.md 同步、ADR 补写、entities.yaml 填充） | 文档 |
| P2 | 网络层定位决策（冻结 or 规划 GDD） | 网络/文档 |
| P3 | 每帧轮询/FindAnyObjectByType 事件化与缓存改造 | UI/存档/敌人 |
| P3 | 命名规范批量清理（目录/文件/拼写） | 全部 |

---

*本文档由 2026-08-14 全系统并行审查生成；单系统细节见 production/review/ 下各文件。*
