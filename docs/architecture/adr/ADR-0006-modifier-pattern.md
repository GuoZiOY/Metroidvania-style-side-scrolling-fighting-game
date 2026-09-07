# ADR-0006: 属性 Modifier 采用多源叠加 + FIFO 驱逐 + source 隔离

## Status

Accepted（决策已落地于代码，reverse-doc 补记）

## Date

2026-08-14

## Decision Makers

oy + Claude

## Summary

属性修改采用 **`Stat.AddModifier(value, sourceId)` 多源叠加模式**：装备（source=itemID）、消耗品（source=buffID）、技能被动、精英词缀（StatAffixBase）全部写入同一 Modifier 链，按 sourceId 隔离、FIFO 驱逐防泄漏。该模式是 C6 装备双字典 + Modifier 应用的支柱。

## Engine Compatibility

| 字段 | 值 |
|------|-----|
| **Engine** | Unity 6000.4.8f1 |
| **Domain** | Core / Stats |
| **Knowledge Risk** | LOW |
| **Verification Required** | 多源同属性叠加正确、卸装/过期正确移除 |

## Context

### Problem Statement
架构规划期需解决多源 Modifier 冲突（装备+宝石+消耗品+词缀同属性叠加）。

### Constraints
- 属性组已分四类（资源/主属性/攻击/防御），Stat 有 base/multiplier/additive 结构
- 词缀（消耗品）需能过期移除；装备换装需整组替换；存档不存 Modifier（读档重建）

## Decision

- **统一入口 `Stat.AddModifier(value, sourceId)`**：同 sourceId 覆盖（同装备重复加不叠加），不同 sourceId 叠加
- **FIFO 驱逐**：装备 Modifier 列表超上限驱逐最旧（防无限膨胀）
- **装备**：`EquipItem → AddModifier(source=itemID)`；`UnequipItem → RemoveModifier(source=itemID)`；换装 = 移除旧 + 添加新
- **存档**：Modifier 不序列化——读档后由装备系统按存档 itemID 重新 AddModifier（ADR-0003 约定）
- **词缀**：StatAffixBase 用 AddModifier + OnDestroy 兜底移除（防敌人销毁泄漏）

## Consequences

- ✅ 多源叠加语义统一，装备/消耗品/词缀共享管道
- ✅ FIFO 驱逐 + OnDestroy 兜底防泄漏
- ⚠️ **rarityMultiplier 双重应用风险未验证**（W-3）：`GetModifiedValue` 与词缀生成两条路径是否都应用稀有度倍率，传说 3.0× 可能平方为 9×——需核对 `EquipmentAffixGenerator` 与 `Entity_Stats.GetModifiedValue`
- ⚠️ 敌人初始化顺序敏感：InitializeEnemy 先重置默认值再类型/等级加成（ADR 关联 BUG-0001 修复）
