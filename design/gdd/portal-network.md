# 传送门网络

> **Status**: Designed
> **Author**: oy + Claude
> **Last Updated**: 2026-07-29
> **Implements Pillar**: Pillar 3 (城镇是家，荒野是猎场)

## Overview

传送门网络实现中心辐射式地图结构——城镇中有一组传送门，每个通往一个已解锁的野外区域。玩家首次通过正常路径到达某个区域后，该区域的传送门在城镇中激活，之后可通过传送门快速往返。每个区域内部也有返回传送门，方便回城。

## Player Fantasy

暗黑2 的传送点——发现一个新传送点时的那种"安全了，我随时可以回来"的安心感。这个系统就是 Pillar 3 的物理实现：每次远征从城镇传送门出发，找到区域内的返回传送门，回到城镇整理装备，再选择下一个目的地。

## Detailed Design

### Core Rules

**PortalNode数据结构**:
```
PortalNode:
  portalId: string           // 对应区域ID
  targetScene: string        // 目标场景名
  targetSpawnPoint: Vector2  // 传送后位置
  isActivated: bool          // 是否已激活
  requiredAbility: enum?     // 首次到达需要的能力(可选)
```

**激活流程**:
```
首次到达区域: 玩家通过正常路径进入 → 区域入口的PortalNode自动激活
  → PortalManager.UnlockPortal(portalId)
  → 城镇中对应传送门从灰色变为发光
  → 保存到存档

使用传送门: 玩家在城镇与发光传送门交互
  → SceneManager.LoadScene(targetScene, targetSpawnPoint)
  → 加载完成 → 玩家出现在目标位置

回城传送门: 每个区域的安全点有返回传送门
  → 交互 → 传送回城镇 (城镇场景 + 城镇出生点)
```

### States and Transitions

```
传送门状态:
  Locked (灰) → 未到达对应区域
  Active (亮) → 已到达，可使用
  Disabled → Boss战/事件期间暂时不可用

激活瞬间: 首次到达 → 特效+音效 → 灰变亮 → UI提示"传送门已连接"
```

### Interactions

| 系统 | 方向 | 接口 |
|------|------|------|
| 存档 (F2) | →下游 | 激活的传送门列表持久化 |
| 中心城镇 (F8) | →下游 | 城镇中的传送门GameObject |
| 地图区域 (C16) | ←上游 | 区域入口位置+返回传送门位置 |
| 能力门控 (F6) | ←上游 | 某些传送门需要能力才能首次到达 |

## Formulas

无。纯状态管理。

## Edge Cases

- **玩家在传送动画中被攻击**: 传送瞬间设无敌 + 传送门区域为安全区
- **存档中有已激活传送门但对应场景被删**: 该传送门保持灰色，日志警告
- **连续快速点击传送门**: PortalManager 加激活冷却(0.5s)
- **Boss战期间尝试使用传送门**: 禁用+提示"战斗中无法使用传送门"

## Dependencies

| 系统 | 方向 | 硬/软 |
|------|------|-------|
| 存档系统 (F2) | →下游 | 硬 — 激活状态持久化 |
| 地图区域 (C16) | ←上游 | 硬 — 传送门位置 |
| 中心城镇 (F8) | →下游 | 硬 — 城镇传送门GameObject |
| 能力门控 (F6) | ←上游 | 软 — 可选的能力需求 |

## Tuning Knobs

| 参数 | 默认值 | 说明 |
|------|--------|------|
| 传送冷却 | 0.5s | 防连点 |
| 传送无敌时间 | 0.3s | 传送后保护 |
| 激活特效时长 | 1.5s | 灰→亮的过渡动画 |

## Visual/Audio Requirements

- 锁定传送门: 灰暗+无粒子
- 激活传送门: 发光+对应的区域主题色粒子
- 传送过程: 屏幕淡黑(0.3s) → 加载 → 淡入(0.3s)
- SFX: 传送门激活音效 + 传送过程音效

## Acceptance Criteria

- **GIVEN** 玩家首次到达区域A入口，**WHEN** 走进入口触发器，**THEN** 城镇对应传送门激活(灰→亮+特效+音效)
- **GIVEN** 传送门未激活，**WHEN** 玩家与之交互，**THEN** 显示"传送门未连接"提示
- **GIVEN** 传送门已激活，**WHEN** 玩家使用传送门，**THEN** 加载目标场景并在正确位置生成
- **GIVEN** Boss战中，**WHEN** 玩家尝试使用传送门，**THEN** 传送门被禁用+显示提示

## Open Questions

1. 传送是否需要消耗(金币/道具)? → 暂定免费
2. 传送门数量是否会超过3个(Target阶段)? → PortalManager支持动态数量
