---
status: reverse-documented
source: Assets/Scripts/Character/Player/
date: 2026-07-28
verified-by: oy
---

# 玩家系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 移动架构

### 动态重力

```
Entity.SetVelocity(x, y) → 若未击退/未HitStop → rb.linearVelocity = (x,y)

重力倍率 (相对于 originalGravityScale):

  地面:      1.0x (基准)
  普通跳跃上升: 0.9x   ← 浮空感
  普通跳跃下落: 1.3x   ← 快速下落
  二段跳上升:   1.0x
  二段跳下落:   1.6x   ← 更重

disableDynamicGravity = true → 冻结重力 (领域展开时)
```

### Coyote Time

```
离开平台后 0.1s 内仍可跳跃
CanUseCoyoteTime() = Time.time - lastGroundedTime <= 0.1s
用于: GroundedState 延迟下落转换 + AiredState 直接跳跃检查
```

### 墙壁滑行/跳跃

```
WallSlideState:
  不按 ↓: SetVelocity(xInput, y * wallSlideSlowMuliplier)
  按 ↓:  全速下滑

  ⚠️ Bug: 按跳跃 → jumpState (非 wallJumpState)
           → WallJumpState 从未被使用

WallJumpState:
  SetVelocity(wallJumpForce.x * -facingDir, wallJumpForce.y)
  ⚠️ 无任何状态转换到此状态 → 死代码
```

### 冲刺

```
DashState:
  持续时间: 0.25s, 速度: 20
  重力=0, 方向=input方向 ?? facingDir
  DashBlur解锁: canBeTakedDamage = false (无敌)
  撞墙: CanelDashIfNeeded → wallSlide / idle
  缓冲消耗: 攻击→basicAttack, 二段跳→doubleJump, 跳跃→jump(地面)
```

---

## 2. 三连击系统

```
Player_BasicAttackState:

comboIndex: 1→2→3, 超时 1s 重置
comboLimit: 3

每击参数:
  displacement: attack_PlayerVelocity[comboIndex-1] (Vector2)
  displacement持续时间: attack_PlayerVelocity_Duration

伤害倍率 (Player_Combat.SpecialAttackType):
  第1/2击: 1.0x
  第3击:   1.2x + 暴击率+5% (ThirdComboAttack)
  追击第3击: 1.2x + 暴击率+5% + 击晕判定 (ChaseAttack)

连击队列:
  Update中检测 Mouse0 → comboAttackQueued = true
  triggerCalled(动画事件) → 若队列有下一击 → EnterAttackStateWithDelay(1帧)
  否则 → idleState

⚠️ 硬编码 Mouse0 绕过 GameInput
```

---

## 3. Counter 反击循环

```
阶段1 — 反击 (Player_CounterAttackState):
  按键/Buffer触发 → 仅限地面状态
  combat.CounterAttackPerformed():
    遍历检测到的目标
    if ICounterable.IsInCounterTime:
      关闭敌人counter窗口, SFX, VFX(屏幕震动+冲击波)
      counterable.HandleCounter(knockbackMultiplier) → 击晕+击退
      延迟HitStop协程
      若 CanBeChased: ActivateChaseTime(target)
    无目标: 激活1s冷却
  无敌 + 0.2s恢复

阶段2 — 追击窗口:
  chaseTimeActive = true, 持续 0.5s
  按攻击 → Player_CounterChaseState

阶段3 — 追击 (Player_CounterChaseState):
  无敌, 速度=24, 零重力
  移向目标直到距离≤0.8 或 超时0.5s
  Exit: 设置 comboIndex=3 (最强第3击)
  无敌延长 0.2s+extraInv

阶段4 — 击晕 + 连击:
  若追击命中+击晕几率判定成功 → 敌人晕眩
  玩家落地第3击+追击伤害加成
```

---

## 4. 输入缓冲

```
Player_InputBuffer: 5路独立缓冲, 各 0.1s

  Jump / DoubleJump / Attack / Dash / CounterAttack

填充 (Player.HandleInputBuffer):
  按键按下 → Add*Buffer()

消耗:
  Jump:          GroundedState, WallSlideState, DashState(地面)
  DoubleJump:    AiredState, DashState(空中)
  Attack:        GroundedState, AiredState, DashState
  CounterAttack: GroundedState (仅限地面!)
  Dash:          ⚠️ 从未被消费 — PlayerState检查的是直接输入

⚠️ CounterAttack仅限地面 → 空中无法反击
⚠️ Dash缓冲填充了但无人读取 → 死代码
```

---

## 5. 死亡序列

```
Entity_Health.Die() → EntityDead() → DeathSequence协程:

  1. DOTween: Time.timeScale → 0.05 (慢镜, 2s, SetUpdate=true)
  2. DOTween: 摄像机放大 1.5× (2s)
  3. DOTween: 摄像机平移到玩家位置 (2s)
  4. 2s后: 停止所有Tween, Time.timeScale=0 (冻结)
  5. UI_DeathScreen.Show()

⚠️ 摄像机Tween无字段引用 → 无法中途停止
⚠️ 场景卸载时Tween泄漏
```

---

## 6. 16 个状态汇总

```
EntityState
  └─ PlayerState (abstract)
       ├─ Player_GroundedState (abstract)
       │    ├─ Idle: xInput=0? 静止 | xInput≠0? → Move
       │    └─ Move: SetVelocity(x*moveSpeed) | xInput=0? → Idle
       ├─ Player_AiredState (abstract)
       │    ├─ Jump: SetVelocity(0, jumpForce) | y<0 → Fall
       │    ├─ DoubleJump: 二段跳力 | y<0 → Fall
       │    ├─ Fall: 触地→LandSquash+Idle | 触墙→WallSlide
       │    └─ JumpAttack: 触地→VFX+SFX | triggerCalled+地面→Idle
       ├─ WallSlide: 减速下降 | 按↓→全速
       ├─ WallJump: ⚠️ 死代码 — 无人使用
       ├─ Dash: 0.25s冲刺 | 无敌(若DashBlur) | 可中断至WallSlide/Idle
       ├─ BasicAttack: 3连击(见第2节)
       ├─ CounterAttack: 0.2s反击窗口
       ├─ CounterChase: 追击目标→自动第3击
       ├─ DomainExpansion: 上升→悬浮→领域展开→计时
       └─ Dead: 设置死亡动画
```

---

## 7. 问题汇总

| 严重度 | 问题 |
|--------|------|
| 🔴 构建 | PlayerState.cs 引用 `UnityEditor` — 打包报错 |
| 🟡 中 | WallJumpState 从未被使用 — 死代码 |
| 🟡 中 | Dash缓冲填充但从不消费 |
| 🟡 中 | CounterAttack 仅限地面 |
| 🟡 中 | 死亡序列相机Tween泄漏 |
| 🟡 中 | JumpAttackState 不继承 AiredState — 空中移动+二段跳丢失 |
| 🟢 低 | CoyoteTime 在地面攻击期间不更新 |
| 🟢 低 | BasicAttackState 硬编码 Mouse0 |
| 🟢 低 | 状态机 ChangeState 不中止当前 Update |
| 🟢 低 | canBeTakedDamage / ReciveKnockback / CanelDash / 等多处拼写错误 |

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
