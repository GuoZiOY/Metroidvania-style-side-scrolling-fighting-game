# GDD 模板统一迁移计划

> **创建**: 2026-08-14 | **目的**: 解决 GDD 模板双轨制（design/CLAUDE.md 要求的 8 段模板 vs 实际混用），恢复自动化校验能力（/design-review、/consistency-check）

---

## 一、现状分类（31 个 GDD + 3 个附属文档）

### A. 英文 8 段模板（13 篇）— 已合规，仅需补齐缺失节
| 文档 | 缺失节 |
|------|--------|
| ability-gate.md | Detailed Rules |
| area-design.md | Player Fantasy / Detailed Rules / Formulas / Tuning Knobs |
| boss-slime-king.md | Detailed Rules |
| boss-system.md | Detailed Rules |
| boss-tracking.md | Detailed Rules / Formulas |
| crafting-system.md | Detailed Rules |
| elite-affix.md | Detailed Rules |
| enemy-system-v2.md | Detailed Rules / Acceptance Criteria |
| gem-socketing.md | Detailed Rules |
| hub-town.md | Detailed Rules |
| item-equipment.md | Detailed Rules / Edge Cases |
| portal-network.md | Detailed Rules |
| world-conditions.md | Detailed Rules |

### B. 中文旧模板（15 篇）— 需迁移到 8 段模板
combat-system.md / enemy-system.md / experience-system.md / input-system.md /
item-backpack.md / item-consumable.md / item-loot.md / item-rarity.md /
item-shop.md / item-system.md / player-system.md / quest-system.md /
save-system.md / skill-system.md / ui-system.md

### C. 混合/其他（3 篇）— 需评估重写
area-spawn-system.md / audio-system.md / balance-design.md

### D. 附属（3 篇，非系统 GDD）
game-concept.md（概念文档，非 GDD）/ systems-index.md（索引）/ gdd-cross-review-2026-07-29.md（历史报告）

---

## 二、迁移规则

1. **统一 8 段结构**（design/CLAUDE.md 要求，顺序固定）：
   `1. Overview → 2. Player Fantasy → 3. Detailed Rules → 4. Formulas → 5. Edge Cases → 6. Dependencies → 7. Tuning Knobs → 8. Acceptance Criteria`
2. **B 类迁移方式**：将中文旧模板章节映射到 8 段（概述→Overview、详细设计→Detailed Rules、边界情况→Edge Cases、集成点→Dependencies、平衡与调校→Tuning Knobs/Formulas），补 Player Fantasy 与 Acceptance Criteria
3. **A 类补齐**：按上表逐篇补缺失节（多数缺 Detailed Rules——当前内容在 Overview/Edge Cases 中混合）
4. **C 类**：audio-system.md / area-spawn-system.md 需按代码实测反向更新（审查发现与实现漂移）；balance-design.md 作为平衡总表保留独立格式
5. **迁移后**：运行 `/design-review` 逐篇校验 + `/consistency-check` 全量扫描

---

## 三、与审查发现的联动更新（迁移时一并处理）

| 文档 | 需同步的审查发现 |
|------|------------------|
| player-system.md | 状态计数 16→14、反击空中化、Dash 缓冲已修复、WallJumpState 死代码状态 |
| combat-system.md | Burn Bug 已修复、冰抗减免未落地、接口 6 参数、护甲公式 +200/0.6、满血读档 |
| crafting-system.md | 合成实现与 GDD 分叉（概率四通道 vs 确定性 3合1）、TransferAffix 未实现、DismantleTable 死代码 |
| input-system.md | 33 动作清单、缺 IsPlayerControlBlocked、O 键冲突、技能槽绕过阻塞 |
| audio-system.md | 随机选曲描述错误、API 缺 push/pop/打字机 |
| portal-network.md / ability-gate.md | 标注"未实现"状态（C15/F6 缺口） |
| save-system.md | 版本迁移框架缺失、原子写入、存档恢复顺序实测 |
| quest-system.md | ItemPickup/triggerNpcId 未实现、Collect 库存解耦、失败系统死功能 |
| item-equipment.md | rarityMultiplier 双应用风险未验证（W-3） |
| boss-system.md / boss-tracking.md | Boss v4 专属路线 vs BossConfig 设计背离 |

---

## 四、执行建议（分批）

| 批次 | 范围 | 预估 |
|------|------|------|
| 第 1 批 | A 类 13 篇补缺节（机械补齐 Detailed Rules 等） | 每篇 15-30 分钟 |
| 第 2 批 | B 类 15 篇模板迁移（保留内容，重组结构） | 每篇 20-40 分钟 |
| 第 3 批 | C 类 3 篇 + 联动更新表（需代码核对） | 每篇 30-60 分钟 |
| 收尾 | `/design-review` 全量 + `/consistency-check` + 更新 systems-index 状态 | 1 天 |

> **提示**：迁移工作量大，建议以"新阶段计划"（production/review/SUMMARY.md 四、P2 项）排入迭代；每次迁移 3-5 篇，与代码修改解耦。
