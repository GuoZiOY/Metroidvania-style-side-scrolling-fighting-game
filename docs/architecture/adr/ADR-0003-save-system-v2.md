# ADR-0003: 存档系统采用 JsonUtility + 版本检查 + WorldState 持久化

## Status

Accepted（决策已落地于代码，reverse-doc 补记）

## Date

2026-08-14

## Decision Makers

oy + Claude

## Summary

存档系统使用 **JsonUtility 序列化 + 文件持久化 + SAVE_DATA_VERSION 版本检查**（`SaveData.version = 1`），并新增 WorldState flag 与任务阶段奖励持久化。读档采用"轮询系统就绪 → 强制等 1 帧 → 按序恢复"的延迟应用流程。**已知缺口**：版本迁移框架未实现（仅警告"将自动升级"无实际迁移），写入非原子（无 File.Replace）。

## Engine Compatibility

| 字段 | 值 |
|------|-----|
| **Engine** | Unity 6000.4.8f1 |
| **Domain** | Core / Serialization |
| **Knowledge Risk** | LOW |
| **Post-Cutoff APIs Used** | 无（JsonUtility/File API 稳定） |
| **Verification Required** | 版本检查 + 读档恢复顺序（技能→背包→装备→任务→WorldState） |

## Context

### Problem Statement
架构规划期需确定序列化方案；并修复历史问题：WorldState 不持久化（BUG-0012）、claimedStageRewards 丢失（BUG-0004）、读档时系统未就绪。

### Constraints
- 5 槽位存档 + profiles.json 元数据
- 须兼容旧档（低版本警告加载）
- Modifier 不存档——由装备系统读档后重新 AddModifier（避免序列化复杂对象）

## Decision

- **JsonUtility.ToJson/FromJson**（拒绝 Newtonsoft：避免依赖，SO 引用用 ID 序列化）
- **SAVE_DATA_VERSION 常量 + 版本检查**：高于当前版本拒绝加载；低于当前版本警告（**当前无迁移逻辑，见后果**）
- **WorldState 持久化**：`SaveManager.CollectSaveData` 收集 `worldFlags`，`ApplySaveData` 恢复 `WorldState.LoadFromSave`
- **claimedStageRewards 持久化**：`QuestSaveData` 新增字段，防阶段奖励重复领取
- **恢复顺序**：Player位置 → 属性(baseValue) → 技能 → 背包 → 仓库 → 装备 → 任务 → WorldState；技能先于背包（背包容量依赖技能等级，有注释支撑）
- **延迟应用**：轮询关键系统就绪（5s 上限）→ 强制等 1 帧（确保 Start 已执行、UI 订阅未漏）→ ApplySaveData → 0.1s 后刷新 UI
- **满血恢复**：读档统一 SetCurrentHP(MaxHP)（不恢复存档时血量）

## Consequences

- ✅ WorldState/任务阶段奖励持久化闭环（BUG-0012/0004 修复）
- ✅ 恢复顺序有注释支撑，技能先于背包的依赖被显式记录
- ⚠️ **版本迁移框架缺失**：未来格式变更（如 QuestSaveEntry 双份数据清理）需补 `Migrate_vN_to_vN+1` 链
- ⚠️ **写入非原子**：`File.WriteAllText` 写一半崩溃会损坏存档；建议临时文件 + `File.Replace`
- ⚠️ `Load()` 在场景过渡中重复调用会悬挂 pendingLoad（事件泄漏），需幂等保护
