# 能力锁/门控系统

> **Status**: In Design
> **Author**: oy + Claude
> **Last Updated**: 2026-07-29
> **Implements Pillar**: Pillar 4 (掌握带来力量，力量打开世界)

## Overview

能力门控系统使用玩家已有的三个核心移动能力（冲刺、无敌冲刺、二段跳）作为探索门控。每种能力对应一类可通过的障碍，获得能力前障碍不可逾越。冲刺是初始能力（开局可用），无敌冲刺和二段跳在击败区域 Boss 后解锁，分别打开通往新区域和捷径的路径。

## Player Fantasy

空洞骑士的螳螂爪——拿到前你看着高处的平台望尘莫及，拿到后整个世界重新打开。同样的心理锚定：走过一条被伤害地形守卫的通道时，你知道"等我拿到无敌冲刺再回来"——这不是阻挡，是承诺。

## Detailed Design

### Core Rules

**三种门控类型**:

| 能力 | 门控障碍 | 视觉标记 | 获得方式 |
|------|----------|----------|----------|
| 冲刺 (Dash) | 无 — 初始拥有 | — | 开局 |
| 无敌冲刺 (DashBlur) | 伤害地形/激光/毒雾通道 | 红色闪烁警告 | Boss 1 击败 |
| 二段跳 (DoubleJump) | 高台/宽沟 | 可望不可及的平台 | Boss 2 击败 |

**AbilityGate 触发器**:
```
AbilityGate (MonoBehaviour, 挂载到障碍物):
  requiredAbility: enum { DashBlur, DoubleJump }
  onPlayerApproach():
    if player.skillManager[ability].upgradeType == None:
      // 显示"需要XX能力"提示
    else:
      // 允许通过/自动激活
```

**解锁流程**:
```
Boss击败 → QuestManager/BossSystem触发
  → SkillDataManager.UpdateSkillData(abilityUpgradeType, data, level=1)
    → 技能树/被动系统激活能力
    → 世界中的对应AbilityGate变为可通过
    → OnAbilityUnlocked事件 → UI提示 + 地图更新
```

### States and Transitions

```
能力状态 (每个能力):
  Locked (未解锁) → 门控显示红色/锁图标
  Unlocked (已解锁) → 门控显示绿色/通过

获得能力的瞬间:
  Boss击败 → 播放解锁动画 → UI提示"获得了XX能力!"
  → 所属区域的门控从红变绿
  → 之前经过的不可通过障碍现在可通过
```

### Interactions with Other Systems

| 系统 | 方向 | 接口 |
|------|------|------|
| 技能系统 (C2) | ←上游 | SkillDataManager — 检测技能是否解锁 |
| 玩家系统 (C3) | ←上游 | player.skillManager.dashBlur/doubleJump |
| Boss系统 (F7) | ←上游 | Boss击败→触发能力解锁 |
| 地图区域 (C16) | →下游 | 区域设计依赖门控位置 |
| 传送门 (C15) | →下游 | 解锁后可传送的新区域入口 |
| UI (F4) | →下游 | 解锁提示 + 门控HUD标记 |

## Formulas

无数学公式。能力门控是二元状态（锁/解锁）。

## Edge Cases

- **玩家通过seq break到达门控后方**: 不阻止 — 银河城的seq break是传统而非bug
- **能力解锁动画期间玩家移动**: 动画播放时保持玩家输入
- **门控障碍物被销毁后重进场景**: OnEnable中检查能力状态，正确显示锁/解锁
- **重复击败Boss**: 能力不会重复解锁 — SkillDataManager已防重复

## Dependencies

| 系统 | 方向 | 硬/软 |
|------|------|-------|
| 技能系统 (C2) | ←上游 | 硬 — SkillDataManager.IsSkillUnlocked |
| 玩家系统 (C3) | ←上游 | 硬 — Player_SkillManager |
| Boss系统 (F7) | ←上游 | 硬 — Boss击败事件 |
| 地图区域 (C16) | →下游 | 硬 — 门控位置由区域设计决定 |
| UI (F4) | →下游 | 软 — 解锁提示 |

## Tuning Knobs

| 参数 | 说明 |
|------|------|
| 门控视觉标记颜色 | 锁=红, 解锁=绿 |
| 解锁提示持续时间 | 默认3s |
| 提示文本 | "需要无敌冲刺" / "需要二段跳" |

## Visual/Audio Requirements

- 门控障碍物: 锁定时红色粒子+锁图标; 解锁时绿色+消失动画
- 解锁瞬间: 全屏微闪+对应能力图标放大
- SFX: 解锁音效(不同于普通物品拾取)

## Acceptance Criteria

- **GIVEN** 玩家没有无敌冲刺，**WHEN** 接近伤害通道门控，**THEN** 显示红色锁标记+无法通过
- **GIVEN** 玩家击败Boss1解锁无敌冲刺，**WHEN** 返回之前的伤害通道，**THEN** 门控变绿+可通过
- **GIVEN** 玩家没有二段跳，**WHEN** 尝试跳到高台，**THEN** 高度不够无法到达
- **GIVEN** 玩家获得二段跳，**WHEN** 再次尝试同一高台，**THEN** 可到达

## Open Questions

1. 是否允许 seq break (未获得能力但通过技巧到达)? → 暂定允许
2. 冲刺是初始能力还是也需要解锁? → 初始拥有
