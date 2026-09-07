# ADR-0001: 精英词缀采用接口驱动 + 运行时组件注入

## Status

Accepted（决策已落地于代码，本文档为 reverse-doc 补记）

## Date

2026-08-14（决策实际发生：2026-07-30 架构 v2.0 规划期 → 现已实现）

## Decision Makers

oy + Claude（架构 create-architecture v2.0 决策）

## Summary

精英/Boss 词缀系统采用 **IEnemyAffix 接口 + 运行时 AddComponent 注入**（方案B），而非纯数据 SO 或静态配置组件。理由：复杂行为词缀（分身/召唤/AoE/光环）无法用纯数据表达；接口驱动让 16 种词缀以独立组件实现，行为与数值（StatAffixBase 抽象基类）分层。

## Engine Compatibility

| 字段 | 值 |
|------|-----|
| **Engine** | Unity 6000.4.8f1 |
| **Domain** | Core / Gameplay |
| **Knowledge Risk** | LOW |
| **Post-Cutoff APIs Used** | 无（AddComponent/静态事件均为稳定 API） |
| **Verification Required** | 16 词缀在精英/Boss 生成时正确注入并随敌人销毁 |

## Context

### Problem Statement
精英/Boss 需要差异化战斗能力（光环、分身、召唤、狂暴等），但纯数据 SO 无法表达复杂行为，需要确定词缀的承载架构。

### Constraints
- 词缀必须与现有 Enemy FSM 集成（OnBattleUpdate 每帧驱动）
- 词缀须响应战斗事件（造成/受到伤害）
- 词缀生命周期须随敌人销毁自动清理

## Decision

- 定义 `IEnemyAffix` 接口：`AffixId / DisplayName / Tier / OnApplied(Enemy) / OnRemoved(Enemy) / OnBattleUpdate(Enemy) / GetLootBonus() / GetTooltipText()`
- `AffixSpawner` 静态类：掷层级（普通0/精英1-3/ Boss固定3）→ 两步加权选词缀 → `gameObject.AddComponent(type)` → `OnApplied` → `enemy.AddAffix`
- 行为类词缀订阅 `Enemy.OnEnemyDealtDamage / OnEnemyTookDamage` 事件（由 Entity_Combat/Entity_Health 经 Report 方法触发）
- 简单数值词缀继承 `StatAffixBase`（AddModifier + OnDestroy 兜底清理属性）
- `AffixTypeMap` 字典：词缀 ID → 组件 Type 手工注册

## Consequences

- ✅ 行为表达能力完整（16 词缀含分身/自爆/光环等）
- ✅ 组件随敌人销毁，无生命周期泄漏
- ⚠️ 新增词缀需手工注册 AffixTypeMap（可改为 Attribute/反射自动收集）
- ⚠️ 调校值（legendaryChance/eliteChance）硬编码于 AffixSpawner，建议迁入 SO
