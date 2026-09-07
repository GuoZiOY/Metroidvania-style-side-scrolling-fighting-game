# 世界条件系统

> **Status**: Designed | **Date**: 2026-07-29 | **Pillar**: Pillar 4 + 全局

## Overview

世界条件系统是一组"条件→效果"的全局规则引擎。当满足特定条件时（玩家等级、Boss击败数、任务完成、持有物品等），自动触发预设的世界变化（难度提升、区域解锁、新敌人出现、NPC状态改变、事件触发等）。条件只在满足时触发一次或持续生效。

## Player Fantasy

玩家不应该看到"世界倾向 +30"——而应该感受到"这个世界因为我做的事在变化"。10级后矿坑敌人不再弱小——它们"变强了"。击败第二个Boss后庭院里出现了新的敌人类型——"发生了什么?"。世界条件让游戏感觉像一个有机、反应式的世界。

## Detailed Design

### Core Rules

**WorldCondition (ScriptableObject)**:
```
conditionId: string
conditionType: enum { PlayerLevel, BossDefeated, QuestCompleted,
                        ItemOwned, TotalDeaths, ElitesKilled, WorldFlag }

触发类型:
  TriggerOnce: 满足时触发一次 → 标记为已完成
  Continuous:  条件持续满足时效果持续 → 条件不满足时效果移除
  Threshold:   每次跨越阈值时触发 → 支持多级效果
```

**条件效果类型**:
| 效果 | 说明 |
|------|------|
| 全局难度倍率 | 所有敌人HP/伤害 × N |
| 区域解锁 | 激活特定传送门/入口 |
| 敌人替换 | 区域中某敌人类型替换为更强类型 |
| NPC出现/消失 | 特定NPC的可见性/位置 |
| 掉落表变更 | 解锁新掉落物品加入全局池 |
| 价格调整 | 商店价格 × 倍率 |
| 事件触发 | 触发任务、对话、Boss出现 |

**条件示例（MVP）**:
| 条件 | 触发类型 | 效果 |
|------|----------|------|
| 玩家Lv≥10 | Threshold | 全局敌人HP+20%, 伤害+10% |
| 玩家Lv≥20 | Threshold | 全局敌人HP再+20%, 掉落稀有度+10% |
| Boss1击败 | TriggerOnce | 解锁区域2传送门; 世界标记"boss1_defeated" |
| Boss2击败 | TriggerOnce | 解锁区域3传送门; 庭院出现新敌人类型"虚空触须" |
| 世界标记"boss1_defeated" | Continuous | 矿坑普通敌人Lv+3 (变强) |
| 持有物品"古老的钥匙" | TriggerOnce | 解锁隐藏区域入口 |
| 总死亡≥5 | Threshold | 商店出现"生命保险"消耗品 |
| Boss追踪×3完成 | TriggerOnce | 隐藏Boss生成条件解锁 |

### WorldState 集成

```
WorldState (已有静态Dictionary):
  Set("boss1_defeated", true)   → 触发条件检查
  Set("player_level_10", true)  → 触发阈值事件
  Has("boss1_defeated")         → 条件检查

存档: WorldState 键值对 → SaveData.worldFlags
  (修复了之前的Bug: SaveManager现在应该收集/恢复WorldState)
```

### 条件检查时机

```
触发检查点:
  玩家升级后           → 检查所有 PlayerLevel/Threshold 条件
  Boss战后             → 检查所有 BossDefeated 条件 + WorldFlag
  任务完成后           → 检查所有 QuestCompleted 条件
  物品获得后           → 检查所有 ItemOwned 条件
  死亡后               → 检查所有 TotalDeaths 条件
  加载存档后           → 重新评估所有 Continuous 条件
```

## Formulas

```
难度倍率叠加: 同类倍率**相加**（Lv10+20% + Lv20+20% = 总+40%），不同类型效果独立。所有条件（含Threshold和Continuous）均使用此叠加规则
  例: Lv10(+20%) × Boss1击败(+0%) = 1.2×
      Lv20(+20%) × Boss1击败(+0%) × Lv20稀有度(+10%) = 1.2× HP, +10% 稀有度

阈值触发: 每个阈值的条件独立触发一次
  Lv10: 触发 → 标记 done
  Lv20: 仍在Lv10以上 → 但Lv20有独立标记
```

## Edge Cases

- **条件满足后场景重载**: WorldState flag 已持久化 → 不会重复触发
- **多个条件同时满足**: 全部按定义顺序执行
- **条件效果冲突**: 最后一个满足的覆盖（所有效果累积乘算）

## Dependencies

| 系统 | 方向 | 接口 |
|------|------|------|
| WorldState (已有) | ↔ | 标记的 Set/Has |
| 存档 (F2) | → | 持久化 WorldState flags |
| Boss (F7) | ← | Boss击败事件 |
| 经验/等级 (C11) | ← | 升级事件 |
| 任务 (C12) | ← | 任务完成事件 |
| 掉落 (C7) | → | 新物品解锁 |

## Tuning Knobs

| 参数 | 范围 | 说明 |
|------|------|------|
| 条件阈值 | 任意 | Lv阈值/击杀阈值等 |
| 难度倍率 | 1.0-3.0× | 太高→后期敌人秒杀玩家 |
| 触发次数 | 1次/无限/每阈值 | TriggerOnce vs Continuous |

## Acceptance Criteria

- **GIVEN** 玩家达到Lv10，**WHEN** 升级完成，**THEN** 所有敌人HP+20%
- **GIVEN** Boss2被击败，**WHEN** 返回庭院，**THEN** 新敌人类型"虚空触须"出现
- **GIVEN** 世界标记"boss1_defeated"为true，**WHEN** 重新进入矿坑，**THEN** 矿坑敌人Lv+3
- **GIVEN** 加载存档后worldFlags已恢复，**WHEN** 进入区域，**THEN** Continuous条件重新评估
