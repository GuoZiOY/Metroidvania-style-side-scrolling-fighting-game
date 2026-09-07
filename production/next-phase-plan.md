# 破碎之城 — 新阶段开发计划

> **创建**: 2026-08-14 | **依据**: production/review/SUMMARY.md（12 系统审查）+ 架构文档 v2.1 + GDD 迁移计划
> **阶段基线**: Production | **代码基线**: git HEAD `7c7b82c`
> **目标**: 修复审查发现的 P0/P1 问题，补齐 MVP 闭环缺环（传送门网络 + 能力门控），完成文档治理，产出可玩的 MVP 纵向切片

---

## 一、阶段定位与目标

当前项目已具备完整玩法骨架（战斗/技能/装备/任务/存档/Boss），但存在 **3 类问题**：
1. **功能缺陷**（P0）：键位冲突、Boss/传送门不持久化、任务触发失效、编码损坏
2. **MVP 缺环**：传送门网络（C15）、能力门控（F6）未实现——"探索→解锁→新区域"核心循环不完整
3. **文档治理**：GDD 模板双轨、ADR 补齐（已完成 6 个）、反向文档漂移

**本阶段成功标准**：玩家能完整走通 `城镇 → 传送门 → 区域探索 → 击杀精英/Boss → 获得能力 → 解锁新区域` 循环，且无 P0 级缺陷。

---

## 二、迭代划分（建议 4 个迭代，每迭代 1-2 周）

### 迭代 1：P0 缺陷修复（技术债清算）
| # | 任务 | 来源 | 涉及文件 |
|---|------|------|----------|
| 1.1 | 键位冲突解绑：DomainExpansion 改独立键 + SetBinding 冲突检测 | review/input S1 + skill S1 | GameInput.cs |
| 1.2 | 技能输入接入 GameInput 阻塞（SkillSlot1-5 动作 + UI_Chat toggleKey 修复） | review/input S2 + skill S2 | SkillSlotManager.cs / UI_Chat.cs / GameInput.cs |
| 1.3 | Boss 击败状态持久化（WorldState flag） | review/enemy S1 | BossEncounter.cs / WorldState.cs |
| 1.4 | 战斗修复：冰抗减免死代码 + 自动元素身份（element=mainElement）+ 顿帧销毁防护 | review/combat #2/#3/#4 | Entity_StatusHandler.cs / Entity_Stats.cs / HitStopManager.cs |
| 1.5 | 任务触发机制：ItemPickup/triggerNpcId 分发层 | review/quest S1 | QuestManager.cs / QuestData.cs / NPCBehaviour.cs |
| 1.6 | 源码编码统一 UTF-8 + 注释还原（git 历史提取） | review/全部 S1 | 10 个 GBK 文件 + 13 个乱码文件 |
| 1.7 | Collect 目标提交时库存重同步（防白嫖） | review/quest S2 | QuestManager.cs |
| 验收 | 无 P0 缺陷；编码检查通过（.editorconfig + gitattributes 强制 UTF-8） | | |

### 迭代 2：MVP 闭环（银河城核心循环）
| # | 任务 | 来源 | 涉及文件 |
|---|------|------|----------|
| 2.1 | 实现 PortalManager：PortalNode + UnlockPortal/Teleport/IsPortalActive + 激活持久化（存档 activatedPortals） | review/area S1 + save S1 | 新建 PortalSystem/ + SaveManager.cs |
| 2.2 | 实现 AbilityGate：requiredAbility 枚举 + 锁/解锁 + Boss 击败 flag 解锁 | review/area S2 | 新建 AbilityGate/ + BossEncounter 联动 |
| 2.3 | 传送门/Boss 状态统一持久化（WorldState 方案对齐 ADR-0003） | review/save S1 | SaveManager / WorldState |
| 2.4 | 合成系统原子化（输入移除与产物入包回滚） | review/crafting M1 | CombineSystem.cs |
| 2.5 | Load 幂等（过渡中重复调用防护）+ 存档原子写入（File.Replace） | review/save S3/S5 | SaveManager.cs |
| 2.6 | 任务详情持久化（completedQuestProgress）+ 读档 UI 刷新事件 | review/quest M1/M3 | QuestManager.cs / SaveData.cs |
| 验收 | 可走通完整循环；存档/读档后传送门与 Boss 状态保持 | | |

### 迭代 3：文档治理 + 平衡验证
| # | 任务 | 来源 |
|---|------|------|
| 3.1 | GDD 模板迁移第 1-2 批（A 类补齐 + B 类迁移，见 gdd-template-migration-plan.md） | production/gdd-template-migration-plan.md |
| 3.2 | 反向文档同步（player/combat/crafting/input/quest/save 联动更新表） | 迁移计划第三节 |
| 3.3 | 合成系统设计决策（回归 GDD 确定性 vs 概率四通道定稿）+ 更新 crafting GDD | review/crafting S1 |
| 3.4 | 补 ADR-0007（Boss 阶段机）/0008（传送门网络）——迭代 2 实现后补记 | architecture.md |
| 3.5 | 平衡校验：rarityMultiplier 双应用（W-3）、W-7 防御上限、B-2 金币 sink | registry + review/item |
| 3.6 | 每帧轮询/FindAnyObjectByType 事件化改造（UI_AttributeManager/TreeNode、拖放缓存） | review/ui M2/M3 |
| 验收 | `/design-review` + `/consistency-check` 通过；entities.yaml 覆盖新增内容 | |

### 迭代 4：内容完善 + 质量收尾
| # | 任务 | 来源 |
|---|------|------|
| 4.1 | 区域内容按 area-design 补齐（level0/1/BOSS 场景对齐难度曲线） | review/area M4 |
| 4.2 | 网络层定位决策（冻结 or 规划 GDD） | review/network S1/S2 |
| 4.3 | Boss_SlimeKing 拆分（BossPhaseMachine + BossActionPool）+ 死亡轮询改事件 | review/enemy M3/M4 |
| 4.4 | /test-setup 建立单元测试（伤害公式/词缀生成/掉落加权/任务状态机） | 质量基线 |
| 4.5 | 命名规范批量清理（SkillSyetem 目录、Skill0bject 文件、双点文件名等） | review 轻微项 |
| 验收 | 可发布 MVP 内部版本（毕业设计演示） | |

---

## 三、风险与依赖

| 风险 | 等级 | 缓解 |
|------|------|------|
| 编码修复不可逆（注释乱码） | 高 | 立即从 git 历史提取原文，越早越好（迭代 1.6 前置） |
| 合成设计分叉影响策划 | 中 | 迭代 3.3 定稿前暂不新增合成内容 |
| C15/F6 实现需场景接线 | 中 | PortalManager 用现有 Portal.cs 落点机制复用，减少场景改动 |
| 存档格式变更（新字段） | 中 | 迭代 2 前补版本迁移框架（ADR-0003 缺口） |
| 范围膨胀（F9-F13 前移已发生） | 中 | 迭代 4 前做 scope-check，明确 MVP 边界 |

---

## 四、验收标准（MVP 纵向切片）

- [ ] 完整循环可玩：城镇 → 传送门 → 区域 → 精英/Boss → 能力解锁 → 新区域
- [ ] 存档/读档状态保持：传送门激活、Boss 击败、任务阶段、世界 flag
- [ ] 无 P0 缺陷（键位冲突/技能误触/任务白嫖/顿帧崩溃）
- [ ] 全部 .cs 文件统一 UTF-8，注释可读
- [ ] GDD 模板统一（31 篇 8 段合规），ADR 6+ 齐全
- [ ] 构建通过（BUG-0005 类打包问题归零）

---

*本计划由 2026-08-14 全系统审查驱动生成；与 production/review/SUMMARY.md、gdd-template-migration-plan.md、docs/architecture/architecture.md v2.1 配套使用。*
