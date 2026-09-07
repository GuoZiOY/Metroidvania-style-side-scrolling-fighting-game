# Cross-GDD Review Report

**Date**: 2026-07-29
**GDDs Reviewed**: 31
**Systems Covered**: 31/31
**Reviewers**: Claude (consistency + holism passes)

---

## Verdict: CONCERNS

**4 Critical blocking issues must be resolved before architecture begins.** 11 additional issues flagged as Warnings.

---

## 🔴 Blocking Issues → ✅ ALL RESOLVED (2026-07-29)

### B-1: Physical Build Mathematically Non-Viable

**Documents**: combat-system.md, skill-system.md

Combat-system.md 的元素伤害公式给"无元素输入"情况施加 **50% 伤害减半惩罚**。物理Build仅有一个 `GetBasePhyiscalDamage()`，而元素Build享有完整的双轨伤害+元素之心加成(+50HP/点)+元素精通因子。没有对等的物理加成技能或被动。

**Required**: Either remove the 50% penalty AND add physical-equivalent scaling options, OR explicitly declare physical as non-supported playstyle.

---

### B-2: MVP Gold Has No Sink Beyond Finite Shop Inventory

**Documents**: item-shop.md, crafting-system.md

商店库存有限（ShopSO.items 是固定数组）。一旦全部买完，金币积累无限无消耗。制作/分解/宝石费用均为 Target 阶段系统（systems-index.md），MVP 的 ShopSO 没有补货或重复消耗机制。

**Required**: Add at least one repeatable MVP gold sink (consumable restocking, respec cost, shop inventory rotation).

---

### B-3: Materials Have Zero MVP Sink

**Documents**: crafting-system.md, systems-index.md, item-loot.md

材料从敌人掉落和分解获得（crafting-system.md 定义），但制作系统本身是 Target 阶段。MVP 中材料只能堆积在背包中，无任何用途。

**Required**: Either move basic crafting recipes to MVP, add material-to-gold conversion, or remove material drops from MVP.

---

### B-4: World-Conditions Scaling Contradicts Area-Design Difficulty Curve

**Documents**: world-conditions.md, area-design.md, combat-system.md

area-design.md 定义了三级难度递进（矿坑 1-5 / 庭院 4-8 / 王座 7-12）。world-conditions.md 引入独立的全图缩放（Lv10: +20% HP/+10%伤，Boss1: 矿坑 Lv+3），两者未协调。玩家首次到达庭院时可能已触发多级全局buff，庭院实际难度远高于设计值。world-conditions 公式中的 `难度倍率` 变量在 combat-system.md / enemy-system.md 中无消费者接口。

**Required**: Add world-conditions dependency to area-design.md. Define exactly which enemy/scaling systems consume the difficulty multiplier. Reconcile level ranges.

---

## 🟡 Warning Issues

### W-1: Three Conflicting Player Identities

| Identity | GDD | Core Assumption |
|----------|-----|----------------|
| 被动适应 | item-equipment.md | "Build被掉落引导" |
| 主动规划 | skill-system.md, crafting-system.md | "有目的地填补Build缺失件" |
| 纯技巧 | boss-system.md, combat-system.md | "掌握Boss模式=通关" |

game-concept.md Pillar 2 明确优先"掉落定义Build"，但 skill-system / crafting-system / gem-socketing 都假设主动规划范式。未定义当三者冲突时的层级。

**Recommendation**: Define progression hierarchy in game-concept.md.

---

### W-2: Boss Fight Demands 12 Simultaneous Cognitive Threads

12 个系统在Boss战中同时争夺玩家注意力（移动/连击/Counter窗口/5技能冷却/附魔计数/HP管理/Boss阶段/Boss硬直/Boss攻击模式/元素状态/HitStop打断/精英光环）。附魔计数系统是争议最大的——在Boss战的心流中注入资源计数打断了沉浸感。

**Recommendation**: Consider making enchant duration-based instead of count-based for boss fights.

---

### W-3: item-equipment V2 RarityMultiplier Double-Application Risk

item-equipment.md V2 公式在词缀生成时应用 `rarityMultiplier`。但已有代码路径（`GetModifiedValue()`）也应用稀有度倍率。如果 V2 词缀同时走两条路径，传说装备（3.0×）的词缀值会被平方为 9×。

**Required**: V2 section must explicitly state whether it replaces or augments the existing modifier pipeline.

---

### W-4: "世界倾向" → Stale Reference

boss-tracking.md 和 game-concept.md 中引用了 "世界倾向 (P1)"——这个 GDD 不存在。实际文件是 world-conditions.md（世界条件），是条件→效果规则引擎，而不是恶魔之魂式的倾向滑条。名称和概念都有实质性差异。

**Required**: Update stale references to "世界条件" in boss-tracking.md and game-concept.md.

---

### W-5: V1 InitializeEnemy Fatal Bug Not Addressed by V2

enemy-system.md 记录的致命Bug（InitializeEnemy 中 `ApplyTypeBonus` 被 `ApplyLevelBonus` 重置覆盖）在 V2 文档中未被提及。V2 的 `IEnemyAffix.OnApplied()` 使用 `stat.AddModifier()` 而非 `ApplyTypeBonus`，可能绕过Bug。但未文档化。

**Required**: Document how V2 interacts with/exempts from the V1 bug.

---

### W-6: MVP Material/Gold Economy Incomplete

合并 B-2 和 B-3 的经济分析结论。

---

### W-7: 75% Evasion + 75% Armor Cap Stacking

两个防御上限独立运作。全堆敏捷(闪避)+活力(护甲)可达到两层独立75%减伤，相当于~6.25%的概率受到全额伤害。无组合防御上限。

**Recommendation**: Consider a combined defense cap or diminishing returns.

---

### W-8: No Defined Progression Hierarchy

经验/等级、技能点数、装备掉落三个并列的进度驱动系统，无任何文档定义哪个主导。当技能点投资了冰系但掉落全是火系词缀装备时，无设计规则解决此冲突。

**Recommendation**: Define hierarchy in game-concept.md.

---

### W-9: Missing Reverse Dependencies (4 docs)

| Upstream Doc | Missing In |
|-------------|------------|
| crafting-system → | item-backpack.md |
| gem-socketing → | skill-system.md |
| world-conditions → | boss-system.md, experience-system.md |
| boss-tracking → | boss-system.md, quest-system.md |

---

### W-10: combat-system.md Has Zero Pillar Attribution

核心战斗文档无任何 Pillar 声明。经验系统同理。

**Recommendation**: Add pillar attribution to core GDD headers.

---

### W-11: V1 Elite vs V2 Elite Naming Collision / V1 BossTypeSystem vs V2 BossConfig Undefined

V1 `EnemyType.Elite` 与 V2 精英子层 "Elite" 命名冲突。V1 `BossEnemyTypeSystem`（属性加成）与 V2 `BossConfig`（阶段+掉落+硬直）的关系未定义——替代？共存？叠加？

---

## ℹ️ Minor Issues

- No per-stat allocation cap (degenerate single-stat builds possible)
- Discovery tracking system absent despite Pillar 1 priority
- Boss knockback (30% HP) + stagger (8% HP) interaction undocumented
- Boss 掉落加成 25% 与精英单词缀 20% 比较不准确（精英可3词缀=60%）
- MVP 词缀池大小: elite-affix 说 4-6，enemy-v2 说 3-5
- rarityMultiplier 范围 "1.0-3.0" 在基础倍率与差异倍率之间含糊

---

## GDDs Flagged for Revision

| GDD | Reason | Type | Priority |
|-----|--------|------|----------|
| combat-system.md | 物理Build惩罚50% + 无Pillar归属 | Design | 致命 |
| item-shop.md | MVP无重复金币消耗 | Design | 致命 |
| item-equipment.md | rarityMultiplier双重应用风险 | Consistency | 致命 |
| world-conditions.md | 与area-design难度曲线矛盾 | Design | 致命 |
| boss-tracking.md | "世界倾向"过时引用 | Stale Ref | 高 |
| game-concept.md | "世界倾向"过时引用 + 无进度层级 | Stale Ref | 高 |
| item-backpack.md | 无Dependencies节（缺失crafting反向引用） | Consistency | 高 |
| skill-system.md | 缺失gem-socketing反向引用 | Consistency | 高 |
| area-design.md | 未引用world-conditions作为依赖 | Consistency | 高 |
| boss-system.md | 缺失boss-tracking/world-conditions反向引用 | Consistency | 高 |
| experience-system.md | 无Pillar归属 + 缺失world-conditions反向引用 | Consistency | 高 |

---

*This report generated by `/review-all-gdds`*
