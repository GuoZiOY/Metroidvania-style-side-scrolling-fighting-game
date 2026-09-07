# Boss系统

> **Status**: Designed
> **Author**: oy + Claude
> **Last Updated**: 2026-07-29
> **Implements Pillar**: Pillar 4 (掌握带来力量) + Pillar 2 (掉落改变打法)

## Overview

Boss 系统继承 Enemy V2 的 `BossConfig`/`BossPhase` 框架，实现多阶段 Boss 战的完整流程——专用血条 UI、HP 阈值触发阶段切换、阶段间无敌过渡动画、硬直机制、独立掉落表和战后能力解锁。MVP 3 个 Boss 各 2-3 阶段，每个区域一个。

## Player Fantasy

走进 Boss 房间，屏幕顶部出现分段大血条，BGM 切换——"来了"。第一次被机制打倒时的困惑。学会规律后的适应。最终击败时血条碎裂动画 + 全屏闪光 + 专属掉落弹出——"我做到了"。BOSS 战是 Pillar 4 的最高体现：击败 Boss 不是结束，是获得新能力后世界重新打开的开始。

## Detailed Design

### Core Rules

**BossConfig (ScriptableObject)**:
```
bossId: string
bossName: string
phases: BossPhase[]
  ├─ phaseIndex: 0,1,2
  ├─ hpThresholdStart: 1.0, 0.6, 0.3  // 进入此阶段的HP%
  ├─ activeMechanics: Mechanic[]      // 此阶段激活的攻击模式
  └─ transitionAnim: string           // 阶段过渡动画名

uniqueDropTable: LootTable            // 至少1件必掉
unlocksAbility: SkillUpgradeType?     // 击败后解锁的能力
staggerThreshold: float = 0.08        // 8% maxHP
```

**Boss 战斗生命周期**:
```
1. 玩家进入Boss房间 → BossRoom触发器
   → Boss血条UI激活(屏幕顶部)
   → Boss BGM切换
   → 房间门锁住(传送门禁用)

2. 战斗阶段循环:
   Phase[0] 激活 → HP降至60% → PhaseTransition(无敌+动画)
   → Phase[1] 激活 → HP降至30% → PhaseTransition(无敌+动画)
   → Phase[2] 激活 → HP降至0% → 死亡

3. 硬直:
   单次伤害 > maxHP × staggerThreshold(8%)
   → Boss短暂硬直(0.5s) + 额外承伤窗口
   → 冷却15s

4. Boss死亡:
   → 血条碎裂动画
   → 掉落生成(uniqueDropTable必掉1件 + 稀有度加成)
   → 能力解锁(若配置)
   → 房间门解锁 + 传送门恢复
   → Boss BGM淡出 + 胜利音效
```

### States and Transitions

Boss 在现有敌人状态机基础上增加 PhaseTransition:
```
BattleState → HP达阈值 → PhaseTransition → 新Phase的BattleState
AttackState → HP达阈值 → 同上的PhaseTransition

PhaseTransition:
  Enter: 无敌=true, 播放过渡动画, 旧阶段onExit
  Exit: 无敌=false, 新阶段onEnter, 激活新Mechanic
```

### Interactions

| 系统 | 方向 | 接口 |
|------|------|------|
| 敌人V2 (C4) | ←上游 | BossConfig, BossPhase, PhaseTransition |
| 战斗系统 (C1) | ←上游 | IDamgable, HitStop |
| 精英词缀 (C14) | ←上游 | Boss可带独有词缀 |
| 掉落系统 (C7) | →下游 | uniqueDropTable |
| 能力门控 (F6) | →下游 | unlocksAbility |
| 传送门 (C15) | ↔双向 | Boss战期间禁用传送 |
| UI (F4) | →下游 | Boss血条 + 名称 + 阶段线 |
| 存档 (F2) | →下游 | Boss击败状态持久化 |
| Boss追踪 (F13) | →下游 | BossConfig被追踪系统引用 |
| 世界条件 (P1) | →下游 | Boss击败事件触发条件 |

## Formulas

沿用 `enemy-system-v2.md`:
- 阶段HP阈值: [1.0, 0.6, 0.3] (3阶段) / [1.0, 0.5] (2阶段)
- 硬直阈值: 单次伤害 > maxHP × 0.08
- 硬直冷却: 15s

独有: Boss 掉落稀有度加成 = 基础 + 25% (高于精英的20%)

## Edge Cases

- **Boss战中途玩家死亡**: Boss HP/阶段/状态完全重置，Boss房间门解锁
- **Boss被一击秒杀(跨多个阈值)**: 逐阶段触发Transition（1帧内完成所有阶段切换）
- **阶段切换期间玩家攻击**: PhaseTransition期间Boss无敌=拒绝伤害
- **重复击败同一Boss**: 不重复解锁能力，掉落表正常触发
- **Boss房间内退出游戏**: 存档时记录Boss未击败，重进后Boss完全重置

## Dependencies

| 系统 | 方向 | 硬/软 |
|------|------|-------|
| 敌人V2 (C4) | ←上游 | 硬 — BossConfig, BossPhase |
| 战斗系统 (C1) | ←上游 | 硬 — 伤害管道 |
| 掉落系统 (C7) | →下游 | 硬 — 专属掉落 |
| 能力门控 (F6) | →下游 | 硬 — 战后解锁 |
| UI (F4) | →下游 | 硬 — Boss血条 |
| 存档 (F2) | →下游 | 软 — 击败状态 |

## Tuning Knobs

| 参数 | 范围 | 说明 |
|------|------|------|
| 阶段数 | 2-4 | MVP=2-3 |
| HP阈值 | 1.0→0.1 | 必须严格递减 |
| staggerThreshold | 0.03-0.15 | 太高→Boss站不起来 |
| 硬直冷却 | 10-30s | 太低→无限硬直 |
| 过渡动画时长 | 1-3s | 太短→感知不到阶段变化 |
| 专属掉落稀有度加成 | +20-40% | MVP默认+25% |

## Visual/Audio Requirements

- Boss血条: 屏幕顶部居中大血条，金色边框，分段显示阶段阈值线
- 阶段过渡: 全屏闪白→Boss动画→新阶段VFX激活
- Boss死亡: 血条碎裂→全屏暗金色闪光→慢动作(0.5s)→掉落弹出
- Boss BGM: 专属BGM + 阶段变化时加入新乐器层
- 入场: Boss房间门关闭SFX + BGM切换

## Acceptance Criteria

- **GIVEN** 玩家进入Boss房间，**WHEN** 触发BossRoom触发器，**THEN** Boss血条显示+房间门锁定+BGM切换
- **GIVEN** Boss HP降至60%，**WHEN** 触发阶段切换，**THEN** Boss进入无敌过渡动画→新阶段机制激活
- **GIVEN** Boss HP降至0%，**WHEN** 死亡，**THEN** 专属掉落生成+能力解锁+BGM淡出
- **GIVEN** Boss战中玩家死亡，**WHEN** 重进房间，**THEN** Boss完全重置(满HP+阶段1)
- **GIVEN** Boss被击败后，**WHEN** 再次进入房间，**THEN** 房间为空，无Boss

## Open Questions

1. Boss Rush 模式是否需要独立数据? → Full Vision阶段
2. Boss 是否支持"逃跑机制"(打到一定HP撤退)? → 暂不做
