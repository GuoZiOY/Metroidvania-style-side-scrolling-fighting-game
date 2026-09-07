# ADR-0002: 词缀数据库采用单一 ScriptableObject（EliteAffixDatabase）

## Status

Accepted（决策已落地于代码，reverse-doc 补记）

## Date

2026-08-14

## Decision Makers

oy + Claude

## Summary

精英词缀池使用**单一 `EliteAffixDatabase` ScriptableObject**（Resources/Data/EliteAffixDatabase），内部含分类池（`EliteAffixPool[]`，每池 categoryWeight + affixes[]），由 `AffixSpawner` 惰性加载（首次访问 `Resources.Load`）。装备词缀（前缀/后缀）由独立的 `EquipmentAffixGenerator` 生成，两套词缀体系物理分离但共用 Modifier 管道。

## Engine Compatibility

| 字段 | 值 |
|------|-----|
| **Engine** | Unity 6000.4.8f1 |
| **Domain** | Core / Data |
| **Knowledge Risk** | LOW |
| **Verification Required** | 词缀池权重配置在生成时正确归一化 |

## Context

### Problem Statement
架构规划期需确定：词缀池用单一 SO、多 SO 还是外部数据（CSV/JSON）。

### Constraints
- 策划需能调参（类别权重/词缀权重/Tier）而不改代码
- 加载路径须与现有 Resources 体系一致（暂不迁移 Addressables）
- 精英词缀与装备词缀（前缀/后缀）命名须避免冲突

## Decision

- **单一 EliteAffixDatabase SO**：`pools: EliteAffixPool[]`（类别池：火焰/冰霜/闪电/防御/召唤/数值等），每池 `categoryWeight` + `affixes: EliteAffixEntry[]`（affixId/tier/weight）
- **惰性加载**：`AffixSpawner.Database` 属性首次访问 `Resources.Load<EliteAffixDatabase>("Data/EliteAffixDatabase")`，GameBootstrap 可在启动时预置
- **装备词缀分离**：`EquipmentAffixGenerator`（前缀+后缀池，CSV 导入驱动）独立于精英词缀池；两者最终都写入 `Modifier[]` 走统一属性管道
- **不采用**：多 SO（管理碎片化）、外部 JSON（无 Unity 序列化优势）、CSV 直读（运行期解析开销）

## Consequences

- ✅ 策划可在 Inspector 直接调参词缀权重/类别
- ✅ 惰性加载避免启动开销
- ⚠️ Resources.Load 与 Addressables 迁移计划存在张力（ADR-0005 处理）
- ⚠️ 精英词缀与装备词缀两套生成逻辑并存，命名需注意（affix_ 前缀约定）
