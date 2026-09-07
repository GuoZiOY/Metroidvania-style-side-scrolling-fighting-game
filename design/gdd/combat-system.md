---
status: reverse-documented
source: Assets/Scripts/Character/Entity/, Character/CombatSystem/, Character/StatSystem/, Data/
date: 2026-07-28
verified-by: oy
---

# 战斗系统 — 设计文档

> **⚠️ 反向文档说明**
>
> 本文档从现有实现反向生成。设计意图部分基于代码分析推断（标记为 [推断]），
> 非开发者确认。标记为 [待确认] 的是需要验证的开放问题。

---

## 1. 概述

**用途**：游戏核心战斗循环 — 玩家/敌人之间的攻击判定、伤害计算、元素效果应用、击退反馈、顿帧演出。

**范围**：
- 包含：伤害公式、暴击、元素系统、闪避、护甲、击退、HitStop、元素状态
- 不包含：技能具体效果（属技能系统）、连击系统（属 Player_Combat）、经验奖励

**当前实现**：完整实现，已在项目中成熟运作。

**设计意图** [推断]：
- 物理+元素双轨伤害创建 Build 多样性（纯物理 vs 元素混合 vs 元素专精）
- 重击退阈值（30%HP）让高伤害攻击有视觉冲击力
- 顿帧让打击感超越一般 2D 动作水平
- 元素状态互斥（同一时刻只能生效一种）防止状态堆叠失控

---

## 2. 详细设计

### 2.1 伤害管道

#### AttackData 构造

```
触发: Combat 组件调用 CalculateAttackData()
输入: Entity_Stats(施法者属性) + DamageScaleData(技能伤害倍率)
输出: AttackData(physDmg, elemDmg, element, isCrit, effectData)
```

**步骤**：
1. 读取 `Entity_Stats.InputElement` 作为选定元素（可为 None）
2. 计算物理伤害 `GetPhyiscalDamage(scaleFactor)`
3. 计算元素伤害 `GetElementalDamage(InputElement, out element, scaleFactor)`
4. 进行暴击判定 `CalculateCritStatus()`
5. 若暴击，物理和元素伤害均乘以暴击倍率
6. 构造 `ElementalEffectData` — 包含灼烧/冰冻/感电的具体参数

#### 伤害结算

```
触发: IDamgable.TakeDamage(physDmg, elemDmg, element, dealer, isCrit)
流程:
  1. 死亡检查 — 已死亡实体不接受伤害
  2. 闪避判定 — Random(0,100) < evasion%，命中则跳过所有伤害
  3. 护甲减免计算 — 从攻击方获取 armorReduction，计算受击方 mitigation
  4. 元素抗性计算 — 根据元素类型获取 resistance%
  5. 伤害应用:
     - physicalDamageTaken = phys × (1 - mitigation)
     - elementalDamageTaken = elem × (1 - resistance)
  6. 击退计算 — 根据伤害占比判定轻/重击退
  7. ReduceHP() — 扣血、VFX、死亡检测
  8. 元素效果应用 — ApplyStatusEffect()
  9. HitStop 触发
  10. OnHealthUpdate 事件广播
```

### 2.2 核心战斗机制

#### A. 暴击系统
- **判定**: `Random.Range(0, 100) < critChance` — 无保底机制、无伪随机分布
- **效果**: 暴击时物理和元素伤害均乘以 `critPower`（暴击倍率 = 暴伤值/100）
- **属性关联**: 暴击率（敏捷×0.5）+ 暴击伤害（力量×1.0）
- **视觉反馈**: `Entity_VFX` 特殊暴击特效

#### B. 护甲系统
- **公式**: `有效护甲 = (护甲 + 活力×0.5) × (1 - 护甲穿透/100)` → `减免 = 有效护甲 / (有效护甲 + 100)`
- **上限**: 75%
- **穿透**: 攻击方提供，按百分比削减有效护甲
- **曲线特性**: 递减收益 — 护甲越高，每点收益越低

#### C. 闪避系统
- **判定时机**: 伤害计算第一步（护甲计算之前）
- **公式**: `evasion% = Clamp(基础闪避 + 敏捷×0.5, 0, 75)`
- **注意**: 闪避后完全不触发任何后续流程（无伤害、无元素、无击退）

#### D. 元素抗性
- **公式**: `resistance = Clamp(基础抗性 + 智力×0.25, 0, 75) / 100`
- **效果**: 对应元素伤害 × (1 - resistance)
- **三种抗性**: 火抗、冰抗、雷抗，各自独立

#### E. 击退系统
- **轻击退**: 伤害 < 30%最大HP → 默认 (1.5, 2.5) × 方向, 0.2s
- **重击退**: 伤害 ≥ 30%最大HP → 默认 (7, 7) × 方向, 0.5s
- **方向**: 攻击方在左侧 → 向右击退；在右侧 → 向左击退
- **开关**: `canKnockbacked` 控制是否接受击退

#### F. 生命恢复
- **机制**: `InvokeRepeating` 每 N 秒自动恢复
- **恢复量**: `resources.healthRegen.GetValue()`（支持 Modifier 修饰）
- **死亡时**: 自动停止恢复，复活后恢复

### 2.3 元素状态系统

#### 状态互斥规则

```
同一时刻仅允许一个元素状态生效，例外:
- 感电（Lightning）可以叠加充能值
- 当前无状态时：任何元素都可应用
- 当前有状态时：仅 Lightning 可以在 Lightning 状态上叠加
```

#### 灼烧 (Fire)
- **伤害模式**: DoT, 2 ticks/s
- **总伤害**: `offense.fireDamage × burnDamageScale × elemental_scaleFactor`
- **单 tick 伤害**: `总伤害 / (2 × duration)`
- **⚠️ Bug**: `ApplyBurnEffect()` 中将 `currentEffect` 设为 `ElementType.Ice` 而非 `ElementType.Fire`

#### 冰冻 (Ice)
- **效果**: 调用 `entity.SlowDownEntity(duration, slowMultiplier)` 减速实体
- **抗性减免**: `实际持续 = duration × (1 - iceResistance)`
- **减速倍率**: 从 `DamageScaleData.chillSlowMultiplier` 取 → 经过 `(1 + elemental_scale) / 2` 调整

#### 感电 (Lightning)
- **充能机制**: 每次攻击累积 `shockCharge` 值，达到 `maximumCharge`(1.0=100%) 时触发雷击
- **雷击**: 实例化 VFX + 造成 `shockDamage` 伤害 + 重置充能
- **超时**: duration 到期后充能归零、状态清除
- **[推断] 设计意图**: 鼓励持续攻击以触发雷击爆发，创造攻击节奏

### 2.4 HitStop 顿帧系统

#### 两层架构

| 层 | 触发条件 [推断] | 机制 | 影响范围 |
|----|---------------|------|----------|
| 全局 | 重击/大招/特殊事件 | `Time.timeScale → 0.01` | 整个游戏（含UI） |
| 局部 | 普通攻击命中 | `IHitStopable.SetAnimationSpeed(0→1)` | 仅攻击方+受击方 |

#### 三段式恢复曲线

```
Phase 1 - Freeze (50%):       动画速度 = localHitStopTimeScale (≈0)
Phase 2 - SlowRecovery (30%): 动画速度 = recoveryCurve(t) × 0.3
Phase 3 - FastRecovery (20%): 动画速度 = recoveryCurve(t) × 0.7 + 0.3
```

### 2.5 数据和状态

#### 核心数据结构

```
AttackData (运行时，每次攻击构造)
  ├─ phyiscalDamage: float       // 最终物理伤害（含暴击后）
  ├─ elementalDamage: float      // 最终元素伤害（含暴击后）
  ├─ isCrit: bool                // 是否暴击
  ├─ element: ElementType        // 主元素类型
  └─ effectData: ElementalEffectData  // 携带的状态效果参数

DamageScaleData (ScriptableObject 配置)
  ├─ phyiscal: float             // 物理伤害倍率
  ├─ elemental: float            // 元素伤害倍率
  ├─ burnDuration, burnDamageScale
  ├─ chillDuration, chillSlowMultiplier
  └─ shockDuration, shockDamageScale, shockCharge

ElementalEffectData (运行时，由 DamageScaleData + Entity_Stats 计算)
  ├─ chillDuration, chillSlowMultiplier
  ├─ burnDuration, totalBurnDamage
  └─ shockDuration, shockDamage, shockCharge
```

#### 实体属性结构

```
Entity_Stats
  ├─ resources: Stat_ResourceGroup (maxHP, healthRegen)
  ├─ offense: Stat_OffenseGroup (phyiscalDamage, critChance, critPower, armorReduction,
  │            elementalHeart, fireDamage, iceDamage, lightningDamage, attackSpeed)
  ├─ defense: Stat_DefenseGroup (armor, evasion, fireRes, iceRes, lightningRes)
  └─ major: Stat_MajorGroup (strength, agility, intelligence, vitality)
```

#### 持久化
- **存档**: 当前HP、属性基础值（StatType → baseValue）、元素之心值
- **不存档**: Modifier 列表（由装备系统重新应用）、HitStop 状态、临时元素效果

---

## 3. 工作流/时序图

### 完整攻击→伤害时序

```
帧N:   Player_Combat.PerformAttack()
       │
       ├── OverlapCircleAll(targetCheck.position, targetCheckRadius, whatIsTarget)
       │   返回 Collider2D[]
       │
       ├──foreach target:
       │   │
       │   ├── IDamgable damgable = target.GetComponent<IDamgable>()
       │   │   (接口查询 — 不是所有 Collider 都可受伤)
       │   │
       │   ├── AttackData = CalculateAttackData()
       │   │   └── stats.GetAttackData(scaleData) ← 构造 AttackData
       │   │       内部: phyiscalDamage = GetBasePhyiscalDamage() × scale × multiplier
       │   │             elementalDamage = GetElementalDamage() (完整元素公式)
       │   │             isCrit = CalculateCritStatus()
       │   │             若暴击: phys×=critPower, elem×=critPower
       │   │             effectData = new ElementalEffectData(stats, scaleData)
       │   │
       │   ├── damgable.TakeDamage(phys, elem, element, transform, isCrit)
       │   │   └── Entity_Health.TakeDamage():
       │   │       ① if(isDead) return false    // 死亡拒绝
       │   │       ② if(AttackEvaded()) return false  // 闪避
       │   │       ③ mitigation = entityStats.GetArmorMitigation(attacker.armorReduction)
       │   │       ④ resistance = entityStats.GetElementalResistance(element)
       │   │       ⑤ physTaken = phys × (1-mitigation)
       │   │       ⑥ elemTaken = elem × (1-resistance)
       │   │       ⑦ TakeKnockBack(dealer, physTaken)
       │   │       ⑧ ReduceHP(physTaken, elemTaken, element, isCrit)
       │   │       ⑨ 记录 lastDamageTaken / lastAttackerName
       │   │       ⑩ return true
       │   │
       │   ├── if(targetGotHit):
       │   │   ├── ApplyElementalEffect(target, attackData)
       │   │   │   └── Entity_StatusHandler.ApplyStatusEffect(element, effectData)
       │   │   │        ├─ Fire → BurnEffectCo(tick循环协程)
       │   │   │        ├─ Ice → ChillCoEffecteCo(减速+等待持续)
       │   │   │        └─ Lightning → ShockEffectCo(充能+雷击判定)
       │   │   └── CreateHitVFX(target, isCrit, element)
       │   │       (暴击特效/元素特效)
       │   │
       │   └── OnAttackHitResult?.Invoke(element, crit)
       │       (Audio/VFX/UI 响应)
       │
帧N+1: HitStopManager.Update()
       ├── UpdateGlobalHitStop()    // Time.timeScale 恢复
       └── UpdateLocalHitStop()     // 逐个 IHitStopable 动画恢复
```

### 元素状态协同时序

```
ApplyStatusEffect(element=FIRE, effectData)
  │
  ├── CanBeApplied(Fire)? → currentEffect==None? YES
  │
  ├── ApplyBurnEffect(duration=3s, totalDamage=45)
  │   └── StartCoroutine(BurnEffectCo):
  │        currentEffect = Fire  // ⚠️ 实际代码: = Ice
  │        totalTicks = 2ticks/s × 3s = 6 ticks
  │        damagePerTick = 45/6 = 7.5
  │        tickInterval = 0.5s
  │        for i in 6:
  │          entityHealth.TakeDamage(0, 7.5, Fire, self)
  │          yield WaitForSeconds(0.5)
  │        currentEffect = None
  │
  └── (3秒期间如果有新攻击触发Ice):
       CanBeApplied(Ice)? → currentEffect==Fire? NO → 被拒绝!
       但如果新攻击是 Lightning:
         CanBeApplied(Lightning)? → currentEffect==Lightning? NO,
         currentEffect==None? NO → 被拒! (只能叠加在已有Lightning上)
```

---

## 4. 集成点

### 依赖关系

| 依赖系统 | 提供什么 |
|----------|----------|
| StatSystem | Stat 类、Modifier 模式、属性分组 |
| Type/ 枚举 | ElementType, StatType, SkillType |
| Data/ ScriptableObjects | DamageScaleData, AttackData, ElementalEffectData 结构 |
| Entity（基类） | Animator, Rigidbody2D, 面朝方向、地面检测 |

### 被依赖方

| 依赖此系统的系统 | 如何使用 |
|------------------|----------|
| SkillSyetem | SkillObject 创建 AttackData、调用 IDamgable.TakeDamage |
| Player_Combat | 继承 Entity_Combat，覆写 CalculateAttackData（3段连击） |
| Enemy 系统 | Enemy_Health 覆写 TakeDamage 添加战斗状态切换 |
| UI | 监听 OnHealthUpdate → 更新血条 |
| QuestSystem | 监听 QuestEvents.OnEnemyKilled（由死亡驱动） |

### 公开接口

```csharp
// 核心伤害接口
bool IDamgable.TakeDamage(float phys, float elem, ElementType, Transform dealer, bool isCrit)

// 属性查询（Entity_Stats）
float GetAttackData(DamageScaleData) → AttackData
float GetMaxHP() / GetBasePhyiscalDamage() / GetCritPower()
float GetElementalDamage(ElementType, out ElementType, float scale) → float
float GetArmorMitigation(float armorReduction) → float
float GetElementalResistance(ElementType) → float
float GetEvasion() → float
Stat GetStatByType(StatType) → Stat

// 状态效果（Entity_StatusHandler）
void ApplyStatusEffect(ElementType, ElementalEffectData)

// HitStop（HitStopManager）
void TriggerGlobalHitStop(float duration)
void TriggerLocalHitStop(IHitStopable target, float duration)

// 事件
event Action<float> OnHealthUpdate
event Action<ElementType, bool> OnAttackHitResult
```

---

## 5. 边界情况

### 已处理
- ✅ 死亡实体不接受伤害（TakeDamage 首行检查）
- ✅ 伤害为负数 → Mathf.Max(0, ...) 保护
- ✅ 护甲减免/抗性/闪避均有 Clamp 上限(75%)
- ✅ 击退方向根据攻击方相对位置动态计算
- ✅ 支持纯物理和元素混合两种Build路线（物理无惩罚，元素享有额外加成但需面对抗性）

### 发现的问题
- ⚠️ **Bug**: `ApplyBurnEffect()` 第 71 行 `currentEffect = ElementType.Ice` 应为 `ElementType.Fire`
- ⚠️ **元素状态叠加逻辑狭窄**: 只有 Lightning 可在已有 Lightning 上叠加。Fire 和 Ice 必须等当前状态结束后才能重新应用 — 可能导致 DoT 伤害丢失
- ⚠️ **闪避判定在护甲计算之前** — 闪避后全额免伤 vs 护甲减免平摊伤害，哪个优先影响 Build 策略

### 未覆盖场景
- ❓ 护甲穿透 > 100% 时的行为（有效护甲变负？）：`Mathf.Clamp(1 - armorReduction, 0, 1)` 保护穿透上限100%
- ❓ 暴击率 > 100% 时无超暴击机制
- ❓ 元素状态持续期间施加同类型 — 目前被静默忽略，可能需要刷新持续时间的机制

---

## 6. 平衡与调校

### 当前数值（默认值）

| 参数 | 默认值 | 调校建议 |
|------|--------|----------|
| 护甲减免上限 | 75% | 可能需要降为 65-70% 防止后期坦克化 |
| 闪避上限 | 75% | 与护甲重叠 → 考虑差异化角色定位 |
| 元素抗性上限 | 75% | 配合穿透机制 |
| 重击退阈值 | 30% maxHP | 对于 Boss 可能触发过频繁 |
| HitStop 冻结比 | 50%/30%/20% | 冻结阶段过长会让轻击感觉不流畅 |
| 元素之心 HP 加成 | 50 HP/点 | 5 点 = 250 HP → 可能过高，需对比整体 HP 池 |
| 灼烧 tick 频率 | 2/s | 偏低 → 3/s 可能让灼烧更"紧急" |

### 建议的平衡检查
- `/balance-check CombatSystem` — 验证伤害公式的边际收益
- 专注测试：元素Build vs 物理Build 的 DPS 差距
- 后期的护甲值在 0-1000 范围时减免曲线的实际表现

---

## 7. 已完成/缺失清单

### 已实现
- ✅ 完整的伤害管道（构造→判定→结算→反馈）
- ✅ 双轨伤害（物理+元素）
- ✅ 暴击系统
- ✅ 护甲/闪避/元素抗性
- ✅ 三种元素状态效果
- ✅ 两层三段式 HitStop
- ✅ 轻/重击退
- ✅ ScriptableObject 驱动伤害倍率配置
- ✅ 生命恢复系统

### 待实现
- ❌ 伤害数字弹出（部分实现，需确认 UI 系统接口）
- ❌ 元素反应系统（火+冰=？火+雷=？）— 目前元素间无交互
- ❌ 破防/破甲机制
- ❌ 伤害统计/战斗日志

---

## 8. 后续工作

- [ ] **修复 Bug**: `ApplyBurnEffect` 中 `currentEffect` 赋值错误
- [ ] **元素系统增强**: 允许同类元素刷新持续时间、增加元素反应
- [ ] **暴击保底**: 考虑伪随机分布替代纯随机判定
- [ ] **重击退阈值**: 对大血量 Boss 考虑绝对伤害阈值替代百分比
- [ ] **创建 ADR**: 记录双轨伤害+护甲公式的设计决策

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 从 Character/CombatSystem + Entity/ + StatSystem/ + Data/ 反向生成 |

---

*本文档由 `/reverse-document design` 生成*
