# ADR-0005: 资源加载暂用 Resources，Addressables 迁移为远期计划

## Status

Accepted（reverse-doc 补记）—— 迁移计划保留，不设时间点

## Date

2026-08-14

## Decision Makers

oy + Claude

## Summary

资源加载**保持 Resources.Load 体系**（技能/物品/词缀数据均经 Resources 加载），Addressables 2.9.1 迁移标记为**远期计划**（architecture.md 已标 MEDIUM 风险）。当前规模（114 个 SO 资产）下 Resources 足够；迁移仅在与热更新/大包体需求出现时启动。

## Engine Compatibility

| 字段 | 值 |
|------|-----|
| **Engine** | Unity 6000.4.8f1 |
| **Domain** | Core / Asset Loading |
| **Knowledge Risk** | MEDIUM（Addressables 2.9.1 为推荐版本，未实际迁移） |
| **Post-Cutoff APIs Used** | 无 |
| **Verification Required** | 若迁移，须验证场景依赖、远程目录、构建管线 |

## Context

### Problem Statement
架构规划期标记"Resources.Load 加载技能数据"为 MEDIUM 风险（QQ-01 悬而未决）。

### Constraints
- 单机 PC 项目，无热更新硬需求
- 独立开发，迁移成本需与收益匹配
- 现有代码大量 `Resources.Load`（SkillDataManager/存档/词缀数据库）

## Decision

- **维持 Resources**：所有 SO 数据资产（ItemData/LootTable/Skill_DataSo/EliteAffixDatabase）继续经 Resources 加载
- **近期优化优先**：为高频路径建静态缓存（SkillLookup/ItemLookup），消除每次 `Resources.LoadAll` 全量加载（存档读档热路径）
- **迁移触发条件**：出现热更新需求 / 包体超限 / 需要 Addressables 的引用计数管理时，一次性迁移并更新本文档

## Consequences

- ✅ 零迁移成本，现有链路稳定
- ⚠️ Resources 目录随内容增长膨胀（MVP 3 区域规模可控）
- ⚠️ 每次读档 `Resources.LoadAll<Skill_DataSo>("Data/StillData")` 全量加载（路径疑似拼写错误 "StillData"），需缓存修复
