---
status: reverse-documented
source: Assets/Scripts/Character/Enemy/
date: 2026-07-28
verified-by: oy
---

# 敌人系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 架构

```
Entity (基类)
  └─ Enemy
       ├─ 身份: enemyName, uniqueID, EnemyType {Normal, Elite, Boss}
       ├─ 状态机: idle → move → battle → attack → dead
       │                              └── stunned
       ├─ 检测: PlayerDetected() — 水平射线, 仅检测 "Player" 层
       ├─ 战斗: battleMoveSpeed, attackDistance, battleTime(5s), retreat
       ├─ 击晕: stunnedDuration(1s), canBeStunned, isInCounterTime
       ├─ 掉落: LootDropper → OnDrop → LootManager
       ├─ 类型系统: Normal/Elite/BossEnemyTypeSystem
       ├─ 等级系统: EnemyLevelSystem (×倍率 + +加点)
       └─ 减速重写: SlowDownEntityCo → anim.speed × activeSlowMultiplier

仅有一个具体子类: Enemy_Skeleton : Enemy, ICounterable
```

---

## 2. 战斗状态机

```
┌──────────┐  timer<=0   ┌──────────┐
│  idle    │◄──────────►│  move    │  随机时长巡逻
│ (2-4s)   │──────────►│ (5-10s)  │
└────┬─────┘           └────┬─────┘
     │ PlayerDetected()      │ PlayerDetected()
     ▼                       ▼
┌─────────────┐  在攻击范围  ┌──────────┐
│  battle     │──────────►│  attack  │
│  追击/撤退  │◄──────────│  攻击动画 │
└──────┬──────┘ 动画完成   └──────────┘
     │  ▲
     │  │ 击晕结束
     ▼  │
┌──────────┐  HandleCounter()
│ stunned  │◄──────────────────  (玩家反击)
│ (1s眩晕) │
└──────────┘

任何状态 — health<=0 → dead → 掉落 → Destroy(2s延迟)
```

### 战斗状态细节

```
BattleState.Enter():
  检测到玩家 → 进入战斗
  距离<minRetreatDistance? → 后跳撤退

BattleState.Update():
  每帧 PlayerDetected()
  检测到 → 刷新战斗计时器 (5s超时)
  未检测到5s → 回到 idleState
  在攻击范围内 + 检测到 → attackState
  否则 → 向玩家移动 (battleMoveSpeed × DirctionToPlayer)

AttackState:
  Enter: SyncAttackSpeed → anim.SetFloat("attackSppedMultiplier")
  Update: 等 triggerCalled → 回到 battleState

⚠️ 攻击范围仅水平距离(无Y轴): Mathf.Abs(player.x - enemy.x)
⚠️ entryToBattle 记录 lastTimeWasInBattle=0 → 若从未检测 → 立即超时回 idle
```

---

## 3. 敌人类型系统

```
EnemyTypeSystem (抽象基类)
  └── NormalEnemyTypeSystem: 无加成 (空)
  └── EliteEnemyTypeSystem:  ApplyAllBonuses + critChance×0.75 + res×0.8
  └── BossEnemyTypeSystem:    ApplyAllBonuses + critChance×statMult + res×statMult

ApplyAllBonuses():
  ├── scaleMultiplier        → transform.localScale
  ├── statMultiplier         → 基础属性 × multiplier
  ├── moveSpeedMultiplier    → moveSpeed, battleMoveSpeed
  ├── attackRangeMultiplier  → targetCheckRadius
  ├── attackDistanceMultiplier → attackDistance
  └── detectionRangeMultiplier → checkPlayer_Distance

⚠️ 致命Bug: InitializeEnemy 先调 ApplyTypeBonus 再调 ApplyLevelBonus
   ApplyLevelBonus → ApplyDefaultStatSetup() → 重置所有属性!
   → 类型系统的 statMultiplier 加成被完全覆盖
   → 仅非Stat属性(moveSpeed/detection等)存活

⚠️ 所有三个类型组件必须在prefab上手动赋值 (但运行时只用1个)
```

---

## 4. Counter 系统 (敌人侧)

```
ICounterable 接口:
  IsInCounterTime { get; }
  CanBeChased { get; }
  HandleCounter(float knockbackMultiplier)

敌人攻击动画 → Animation Event → EnableCounterTime(true)
  → Enemy_VFX.EnableAttackAlert(true) — 显示攻击预警
  → isInCounterTime = true — 打开反击窗口

玩家 CounterAttackPerformed():
  遍历目标 → ICounterable.IsInCounterTime?
    命中:
      EnableCounterTime(false) — 立即关闭窗口 (防双重反击)
      → HandleCounter(knockbackMult):
          canBeStunned? → ApplyCounterKnockback → stunnedState

⚠️ isInCounterTime 死亡时未重置 (不影响实际游戏)
```

---

## 5. 击晕系统

```
Enemy.TryStun(float extraDuration):
  canBeStunned? → stunnedDuration = Max(1f, current + extra)
  → ChangeState(stunnedState)

StunnedState:
  Enter: 关闭攻击预警VFX, 关闭counter窗口
  Update: stateTimer到期 → ChangeState(battleState)
          ⚠️ 总是回到battleState (即使从未在战斗中)

Enemy_Skeleton.HandleCounter():
  canBeStunned? → ApplyCounterKnockback → ChangeState(stunnedState)
```

---

## 6. 等级缩放

```
EnemyLevelSystem:
  statGrowthMultiplier = 0.1 (每级+10%)

  levelBonus = level - 1 (Lv1 = 无加成)

  乘法属性 (× (1 + levelBonus × 0.1)):
    maxHP, physicalDamage, armorReduction, elementalHeart,
    fireDamage, iceDamage, lightningDamage, armor

  加法属性:
    暴击率: +0.5%/级
    暴伤:   +1%/级
    元素抗: +0.5%/级/种

  Lv5:  1.4×属性
  Lv10: 1.9×属性
  Lv50: 5.9×属性

⚠️ ApplyLevelBonus 先重置默认值再应用 → 防重复乘算
⚠️ 但这导致了与类型系统的冲突 (见第3节)
```

---

## 7. 死亡流程

```
Entity_Health.Die()
  → enemy.EntityDead() (覆写):
      base.EntityDead() — IsDead=true, OnEntityDead事件
      QuestEvents.ReportEnemyKilled(uniqueID) — (前提: uniqueID非空)
      ChangeState(deadState)

DeadState.Enter():
  SwitchOffStateMachine() — 禁止后续状态切换
  DropLoot() → lootDropper.OnDrop() → LootManager (hasDropped防重复)

DeadState.Update():
  triggerCalled (死亡动画结束) → DestroyEntity()
    → Destroy(gameObject, 2) — 2秒延迟
```

---

## 8. 问题汇总

| 严重度 | 问题 |
|--------|------|
| 🔴 致命 | InitializeEnemy: 类型系统stat加成被等级系统重置覆盖 |
| 🟡 中 | StunnedState 总回到battleState — 若从未在战斗则瞬切idle |
| 🟡 中 | BattleState 字段在Exit时不清理 — 场景重载可能引旧Player |
| 🟡 中 | 攻击动画速度参数拼写 "attackSppedMultiplier" → 可能同步失败 |
| 🟢 低 | 撤退Y速度无视减速乘数 |
| 🟢 低 | 每帧 PlayerDetected 射线 — 大量敌人时性能影响 |
| 🟢 低 | 3个类型组件需全赋值但只用1个 — Inspector 浪费 |
| 🟢 低 | DisbaleCounterTime / ReciveKnockback 等多处拼写 |

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
