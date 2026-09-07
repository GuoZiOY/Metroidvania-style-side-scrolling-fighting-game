# Boss追踪系统

> **Status**: Designed | **Date**: 2026-07-29 | **Pillar**: Pillar 1 + 2

## Overview

Boss 追踪系统提供一个暗黑式"赏金狩猎"元层：通过 NPC 传闻、物品线索或击败前置敌人解锁稀有 Boss 的生成条件。满足条件后 Boss 在指定区域生成，击败后获得独特掉落和世界条件变化。

## Player Fantasy

"听酒馆老板说在废墟深处有个发光的巨兽"——不是任务系统的"去杀X个Y"，而是环境叙事驱动的狩猎。追踪系统让重复区域探索有新的目标。

## Detailed Design

**追踪流程**:
```
1. 获取线索:
   ├── NPC对话: "听说东废墟深处有东西..."
   ├── 掉落物品: "染血的巨兽鳞片" (使用→获得追踪)
   └── Boss击败后解锁: 击败BossA解锁BossB的追踪

2. 激活追踪:
   BossTrackerUI 中勾选追踪 → 地图标记目标区域
   → 前往区域 → 满足条件(击杀足够敌人/在特定时间)
   → Boss生成 → Boss血条显示 + 独特BGM

3. 讨伐完成:
   → 独特掉落(非普通Boss掉落表的额外物品)
   → 追踪完成标记
   → 可重复挑战(冷却24h游戏时间)
```

**BossTrackConfig (ScriptableObject)**:
```
trackId: string
bossConfig: BossConfig           // 对应的Boss数据
clueType: enum { NPC, Item, Boss }
spawnConditions: SpawnCondition[]
  ├─ killCount: int?             // 击杀N个区域敌人
  ├─ timeWindow: 游戏时间范围
  └─ requiredItem: string?       // 需持有某物品
repeatableCooldown: float        // 可重复讨伐冷却(游戏时间秒)
```

### Interactions

| 系统 | 方向 | 接口 |
|------|------|------|
| Boss系统 (F7) | ← | BossConfig |
| 任务系统 (C12) | ← | NPC线索对话 |
| 掉落 (C7) | → | 独特掉落 |
| 世界条件 (P1) | → | 讨伐影响世界状态 |
| UI | → | 追踪面板 |

## Edge Cases

- **线索物品被卖掉/分解**: Boss追踪不受影响，线索已激活
- **多次获取同一线索**: 重复线索自动忽略
- **冷却期间的Boss再触发**: 区域条件满足但不生成Boss

## Dependencies

| 系统 | 方向 | 硬/软 |
|------|------|-------|
| Boss系统 (F7) | ← | 硬 |
| 任务系统 (C12) | ← | 软 — NPC线索 |
| 世界条件 (P1) | → | 软 |
| UI | → | 硬 |

## Tuning Knobs

| 参数 | 范围 | 说明 |
|------|------|------|
| 可追踪Boss数量 | MVP=3, Target=5 | 对应区域数 |
| 重复讨伐冷却 | 12-48h游戏时间 | 防止刷爆 |
| 生成条件难度 | killCount 20-100 | 区域敌人击杀要求 |

## Acceptance Criteria

- **GIVEN** 玩家获得线索，**WHEN** 打开追踪面板，**THEN** 显示Boss名称和生成条件
- **GIVEN** 满足生成条件，**WHEN** 进入目标区域，**THEN** Boss生成+血条显示
- **GIVEN** Boss被讨伐，**WHEN** 捡起掉落，**THEN** 独特物品+追踪完成标记
