# 战斗系统系统审查报告

> 审查日期：2026-08-18 | 代码基线：git HEAD（7c7b82c，2026-08-14）
>
> 审查范围：
> - `Assets/Scripts/Character/CombatSystem/`（IHitStopable.cs、HitStopManager.cs）
> - `Assets/Scripts/Character/Entity/Entity_Combat.cs`、`Entity_Health.cs`、`Entity_StatusHandler.cs`、`Entity_Stats.cs`
> - 关联上下文：`Entity.cs`、`Entity_VFX.cs`、`Player_Combat.cs`、`Enemy.cs`、`Enemy_Health.cs`、`Data/`（AttackData/DamageScaleData/ElementalEffectData）、`Interface/`（IDamgable/ICounterable）、`StatSystem/`、`SaveManager.cs`、`QuestEvents.cs`
> - 参考文档：`design/gdd/combat-system.md`（反向文档，2026-07-28）、`docs/architecture/architecture.md`（v2.0，2026-07-30）

---

## 1. 框架结构

### 1.1 模块划分与文件分布

战斗系统代码横跨 5 个目录，职责边界如下：

| 目录 | 文件 | 职责 |
|------|------|------|
| `Character/CombatSystem/` | `HitStopManager`（单例）、`IHitStopable`（接口） | 两层三段式顿帧调度 |
| `Character/Entity/` | `Entity`（基类，IHitStopable）、`Entity_Combat`（攻击）、`Entity_Health`（IDamgable，伤害结算）、`Entity_StatusHandler`（元素状态机）、`Entity_Stats`（属性+公式）、`Entity_VFX`（特效） | 战斗主体 |
| `Character/Player/` | `Player_Combat`（继承 Entity_Combat，3 段连击/反击/追击） | 玩家侧战斗扩展 |
| `Character/Enemy/` | `Enemy`（继承 Entity，ICounterable，词缀宿主）、`Enemy_Health`（覆写 TakeDamage/ReduceHP） | 敌人侧战斗扩展 |
| `Data/` | `AttackData`、`DamageScaleData`（SO）、`ElementalEffectData` | 战斗数据结构 |
| `Interface/` | `IDamgable`、`ICounterable` | 契约接口 |
| `Type/` | `ElementType` | 枚举 |

### 1.2 继承体系与组件模式

```
Entity (MonoBehaviour, IHitStopable)
 ├── Player         (16 状态 FSM)
 └── Enemy          (ICounterable, 词缀宿主)

Entity_Health (IDamgable)  ← Enemy_Health（覆写 TakeDamage 进入战斗状态 + 伤害数字弹出）
Entity_Combat  ← Player_Combat（覆写 CalculateAttackData 加入特殊攻击加成）
Entity_Stats / Entity_StatusHandler / Entity_VFX（独立组件，通过 GetComponentInParent 组合）
```

- 采用**组件组合 + 接口优先**模式：可受伤实体实现 `IDamgable`，可顿帧实体实现 `IHitStopable`，可反击实体实现 `ICounterable` —— 与 architecture.md「接口优先」原则一致。
- 唯一单例：`HitStopManager`（惰性创建 + Awake 双保险 + `DontDestroyOnLoad`）。战斗本身无全局单例，通过组件+接口解耦。

### 1.3 职责单一性评价

- ✅ `Entity_Stats` 集中全部属性公式（暴击/护甲/抗性/闪避/元素伤害），`Entity_Health` 只做结算与生命管理 —— 分层清晰。
- ⚠️ `Entity_Health` 同时承担伤害结算、击退、无敌帧、生命恢复、死亡，职责偏多但内聚可接受。
- ⚠️ `Entity_Stats.GetElementalDamage`（66 行）复杂度偏高：混合"自动选元素 + 元素之心 + 智力加成 + 被动精通 + 主/次要元素"，是全系统最不易读的方法。
- ⚠️ 战斗数据流横跨 4 个目录，`Entity_Combat` 直接依赖 `Data/`（AttackData）与 `StatSystem/`，耦合方向单向、可接受。

---

## 2. 工作流程

### 2.1 攻击 → 伤害完整时序（编号步骤）

```
帧 N：Player_Combat.PerformAttack()
  1. UpdateCurrentAttackIndex() — 从 BasicAttackState.ComboIndex 取连击段数
  2. GetDectectedCollders() — OverlapCircleAll(targetCheck, targetCheckRadius, whatIsTarget)
     （空结果时 PlaySwingSfx 挥空音效）
  3. 遍历每个 Collider2D：
     a. target.GetComponent<IDamgable>() — 非所有碰撞体可受伤
     b. CalculateAttackData() — Player 覆写：物理/元素伤害 → 暴击判定（含特殊攻击暴击加成）
        → 暴击时 phys/elem ×= critPower；再 ×= 特殊攻击伤害倍率
        → 构造 ElementalEffectData
     c. damgable.TakeDamage(phys, elem, element, dealer, isCrit)：
        ① isDead → return false（死亡拒绝）
        ② AttackEvaded() → return false（闪避全额免伤，先于护甲）
        ③ 受击无敌帧检查（ignoreInvincibility=false 且时间未到 → false）
        ④ mitigation = attacker.armorReduction → GetArmorMitigation()
        ⑤ resistance = GetElementalResistance(element)
        ⑥ physTaken = phys × (1−mitigation)；elemTaken = elem × (1−resistance)
        ⑦ TakeKnockBack(dealer, physTaken) — 轻/重击退（阈值 30% maxHP）
        ⑧ ReduceHP() — VFX → 扣血 → OnHealthUpdate → 死亡检测 Die()
        ⑨ 记录 lastDamageTaken / lastAttackerName
        ⑩ Enemy 侧：ReportTookDamage()（词缀订阅）
     d. 命中成功时：
        ApplyElementalEffect() → Entity_StatusHandler.ApplyStatusEffect()
        CreateHitVFX() → Entity_VFX.CreateOnHitVFX()
        Enemy 攻击方：dealerEnemy.ReportDealtDamage()（词缀如吸血）
        OnAttackHitResult?.Invoke(element, true)
  4. TriggerLastComboHitStop() — 末段连击命中触发局部顿帧
  5. ResetSpecialAttackType()

帧 N+1：HitStopManager.Update()
  → UpdateGlobalHitStop()：Time.timeScale 恢复
  → UpdateLocalHitStop()：遍历字典，三段式（Freeze 50% / SlowRecovery 30% / FastRecovery 20%）
     SetAnimationSpeed 恢复动画/刚体
```

### 2.2 元素状态应用时序（互斥 + 感电叠加）

```
ApplyStatusEffect(element, effectData)
  ├─ CanBeApplied()：仅 currentEffect==None 可应用；Lightning 可在已有 Lightning 上叠加
  ├─ Fire   → BurnEffectCo：currentEffect=Fire → VFX → tickCount=2/s×duration
  │            每 tick entityHealth.TakeDamage(0, totalDamage/tickCount, Fire, self, ignoreInvincibility:true)
  ├─ Ice    → ChillCoEffecteCo：SlowDownEntity(duration, slowMultiplier) + VFX → 等待后清除
  └─ Lightning → currentCharge += charge → 达 maximumCharge(1.0) 触发雷击
                  DoLightningStrike：实例化 VFX + TakeDamage(0, shockDamage, Lightning, ignoreInvincibility:true)
                  否则 ShockEffectCo 计时，到期清除
```

### 2.3 顿帧时序（两层）

- **全局顿帧**：`TriggerGlobalHitStop` → 记录原 timeScale → `Time.timeScale = 0.01` → 协程 `WaitForSecondsRealtime` + Update 双通道计时 → `EndGlobalHitStop` 还原。
- **局部顿帧**：`TriggerLocalHitStop` → 加入 `hitStopTargets` + `hitStopDataMap` → `target.StartHitStop` → UpdateLocalHitStop 逐帧按三段曲线调 `SetAnimationSpeed` → 到期 `EndHitStop` 移除。

### 2.4 战斗数据存档/读档时序

```
Save：CollectSaveData()
  → CollectPlayerData：player.health.GetCurrentHP()（⚠️ 保存但读档不恢复，见 §3.3）
  → CollectStatData：遍历全部 StatType → GetStatByType → 存 baseValue（含战斗属性全部）
  → 背包/装备/技能/任务/WorldState

Load：Load → ApplySaveData（同场景）或 ApplySaveDataDelayed（跨场景，轮询就绪 + 强制等 1 帧）
  → ApplyStatData：按 StatType 恢复 baseValue
  → 技能（先于装备，背包扩容依赖）→ 背包 → 装备（重新 AddModifier，Modifier 不存档）
  → 末尾：SetCurrentHP(GetMaxHP()) 统一满血恢复
  → player.Revive() + statusHandler.RemoveAllNegativeEffects()（清灼烧/冰冻/感电）+ 清输入缓冲
```

### 2.5 敌人击杀 → 任务链路

```
Entity_Health.Die() → entity.EntityDead()（Enemy 覆写）
  → base.EntityDead() → OnEntityDead 事件
  → uniqueID 非空时 QuestEvents.ReportEnemyKilled(uniqueID)
  → stateMachine.ChangeState(deadState)
```

---

## 3. 信息链路

### 3.1 事件/数据流盘点

| 事件 | 定义位置 | 发布方 | 订阅方 |
|------|---------|--------|--------|
| `OnHealthUpdate`（Action 无参） | Entity_Health | 扣血/加血/初始化 | UI 血条、Boss 血条 |
| `OnAttackHitResult`（Action<ElementType,bool>） | Entity_Combat | 命中时（恒传 true） | Audio/VFX/UI |
| `OnAttackHitWithIndex`（Action<int>） | Player_Combat | 命中时 | 音频（连击音效区分） |
| `OnEntityDead` / `OnFlipped` | Entity | 死亡/翻转 | 任务、表现 |
| `OnEnemyDealtDamage` / `OnEnemyTookDamage` | Enemy | Entity_Combat / Entity_Health 回调 | 词缀行为 |
| `QuestEvents.OnEnemyKilled`（静态总线） | QuestEvents | Enemy.EntityDead | QuestManager |

### 3.2 直接引用/组件查找（耦合点）

- `Entity_Combat`：`GetComponentInParent<Entity_VFX/Entity_Stats/Enemy>`（每命中一次 Enemy 查询）；
- `Entity_Health`：`GetComponent<Entity_Stats>()`（**仅自身**，与 entity/vfx 的 InParent 不一致）；
- `Enemy_Health`：`enemy => GetComponent<Enemy>()` 属性 —— **每次访问都查组件**；
- `Player_Combat`：直接写 `player.health.canBeTakedDamage = false/true`（跨组件字段穿透）；
- `Entity_VFX`：`FindAnyObjectByType<CinemaScreenShake>()` 惰性查找（已缓存）；
- `Enemy.GetPlayerReference`：`FindAnyObjectByType<Player>()` 兜底（射线失败时）。

### 3.3 上下游依赖与存档覆盖

- **上游**：StatSystem（Stat/StatModifier）、DamageScaleData SO、ElementType、Player_SkillManager（元素精通因子）。
- **下游**：SkillSystem（Player_Combat 攻击事件）、QuestSystem（击杀上报）、AudioManager、UI、VFX。
- **存档覆盖结论**：
  - ✅ 属性 baseValue 全量覆盖（含 MaxHP/暴击/护甲/抗性/元素伤害/元素之心）；
  - ✅ 元素状态、顿帧、Modifier 不存档 —— 与 GDD「不存档清单」一致；
  - ⚠️ **`currentHP` 被保存（CollectPlayerData）但读档从不应用**（ApplySaveData 统一满血）—— 死字段 + GDD 声称"存档当前HP"未落地；
  - ⚠️ `RemoveAllNegativeEffects` 未重置感电 `currentCharge` —— 读档/复活后残留充能，下次攻击可能瞬发雷击。

---

## 4. 与设计文档一致性

### 4.1 实现与 GDD（combat-system.md）差异

| # | GDD 描述 | 实现现状 | 结论 |
|---|---------|---------|------|
| 1 | §2.3/§5 声称 `ApplyBurnEffect` 中 `currentEffect = Ice`（Bug） | 代码 L71 已是 `ElementType.Fire`（git 已修复） | ✅ 已修复，**GDD 过时** |
| 2 | `IDamgable.TakeDamage(phys, elem, element, dealer, isCrit)` 5 参数 | 实际 6 参数（含 `ignoreInvincibility`，元素 DoT 豁免无敌帧） | GDD 过时 |
| 3 | `event Action<float> OnHealthUpdate` | 实际 `event Action`（无参，UI 需自行 GetCurrentHP） | GDD 过时 |
| 4 | 护甲公式 `有效护甲/(有效护甲+100)`，上限 75% | 实际 `/(+200)`，上限 `0.6`（代码注释：原 0.75 过高下调） | **实现偏离 GDD，文档未同步** |
| 5 | 存档：当前 HP、属性基础值、元素之心值 | 属性/元素之心 ✅；当前 HP 保存但读档统一满血恢复 | GDD 过时 |
| 6 | §7 待实现：伤害数字弹出 | `Enemy_Health.ReduceHP → entityVFX.CreatePopUpText` 已实现 | GDD 过时 |
| 7 | `OnAttackHitResult?.Invoke(element, crit)` | 实现恒传 `true`，miss（闪避/未命中）永不广播 | 契约未完全落地 |
| 8 | 冰冻抗性减免：`实际持续 = duration × (1−iceRes)` | `finalDuration` 计算后**未使用**，传入原始 duration | **实现 Bug，GDD 未落地** |
| 9 | 减速倍率经 `(1+elemental)/2` 调整 | 实现同时调整 duration 与 slowMultiplier | 轻微偏离 |
| 10 | 暴击/护甲/抗性/闪避公式、状态互斥、顿帧三段式 | 与实现一致 | ✅ |
| 11 | AttackData 结构（phyiscalDamage 等） | 字段名一致（含拼写错误 phyiscalDamage） | ✅ |

### 4.2 与 architecture.md 一致性

| architecture.md 声明 | 现状 |
|----------------------|------|
| C1 拥有：7 步伤害管道、HitStopManager 两层三段式、Entity_StatusHandler 元素状态机 | ✅ 落地 |
| C1 公开接口：IDamgable / GetAttackData / ApplyStatusEffect / TriggerGlobal(HitStop) | ✅ 全部存在 |
| C1 V2 变更：修复 ApplyBurnEffect Bug | ✅ 已修复 |
| C1 引擎 API：OverlapCircleAll / Time.timeScale / InvokeRepeating | ✅ 使用中 |
| C4 依赖 C1（IDamgable/Entity_Stats/StatusHandler）+ QuestEvents 击杀上报 | ✅ |
| C2 警示：`FindAnyObjectByType`（被动技能获取 skillManager） | 全项目 88 处，**战斗中 6+ 处**，已成系统性问题 |
| 数据流 Frame Update Path | ✅ 与实现完全一致 |

---

## 5. 代码质量

### 5.1 注释规范（严重违规）

项目规范要求所有字段/方法/关键逻辑有行内中文注释。实测 **13 个文件的中文注释已被 U+FFFD 替换符永久破坏**（GBK/UTF-8 混合编码事故），且 `DamageScaleData.cs` 整文件为 GBK 编码（严格 UTF-8 解码失败）：

| 文件 | U+FFFD 数量 | 影响 |
|------|------------|------|
| Entity_StatusHandler.cs | 251 | 全部中文注释丢失（含 VFX/协程注释） |
| Entity_VFX.cs | 294 | 大部分注释丢失，仅新注释幸存 |
| Enemy_Health.cs | 133 | 覆写说明全部丢失 |
| Entity.cs | 110 | 后半段（减速/击退/顿帧）注释丢失 |
| DamageScaleData.cs | — | 整文件 GBK，非有效 UTF-8 |

风险：Unity 按 UTF-8 解析源码，GBK 文件在高版本/IL2CPP 下可能编译异常；注释不可恢复（需从 git 历史还原或重写）。

### 5.2 命名与拼写

- 拼写错误：`GetDectectedCollders`（Entity_Combat）、`burnDuratoin`/`chillSlowMulitplier`（DamageScaleData/ElementalEffectData）、`lighingStrikeVfx`（StatusHandler）、`onHeavyDamapeKnockback`（Health）、`ChockVFX`（VFX）；
- `elementalHeartValue()` 方法名违反 PascalCase；
- `SetHealthToPercent(percent)` 实际语义为"按百分比加血"（调用方 Skill_Shard 按此使用），命名误导。

### 5.3 耦合度

- `Player_Combat` 直接穿透写 `player.health.canBeTakedDamage` —— 应封装为 `health.SetDamageable(bool)`；
- `Enemy_Health.enemy` 属性化 GetComponent，每命中多次查询；
- `SaveManager` 与 `Entity_Health/Entity_StatusHandler` 通过 public 字段/方法硬耦合（可接受，属调度层职责）。

### 5.4 性能风险

| 位置 | 问题 | 频率 |
|------|------|------|
| `Player_Combat.PerformAttack` | 每次攻击最多 **3 次** `OverlapCircleAll`（挥空检测 + base + 末段顿帧） | 每次攻击，3 次数组分配 |
| `Entity_Stats.GetElementalDamage` | `OrderByDescending(...).First()` LINQ 分配 | 每次攻击（自动元素模式） |
| `HitStopManager.UpdateLocalHitStop` | 每帧 `new List<IHitStopable>` 临时分配 | 顿帧期间每帧 |
| `SaveManager.ApplySaveDataDelayed` | 每帧 4 次 `FindAnyObjectByType` 轮询（最长 5s） | 每次读档 |
| `FindAnyObjectByType` | 全项目 88 处（战斗相关 6+ 处） | 架构文档已预警 |
| `Stat.GetValue` | 每次调用重算修饰符链，无缓存 | 每帧多处调用（量小可接受） |
| `Debug.Log` | 生产路径残留（反击冷却/追击/创建暴击粒子） | 高频战斗动作时刷屏 |

无每帧重型 Update 轮询热点（Player_Combat.Update 三个轻量 tick 判断可接受）。

### 5.5 魔法数字硬编码

击退阈值 `0.3`、护甲公式 `+200`/`cap 0.6`、抗性/闪避上限 `75`、智力系数 `0.25`、元素之心 `0.1/50/5`、顿帧段乘数 `0.3/0.7/0.3`、`Destroy(gameObject, 2)`、随机待机区间（1.5~4/4~5.5/3~6/6~8）、VFX 闪烁 `0.25s/×1.2/×0.8` —— 均未配置化。

### 5.6 超大类与重复代码

- 审查范围内最大 `Player_Combat` 396 行（<400 临界）；关联 `SaveManager` 734 行（范围外）；
- `AttackData` 构造函数与 `Player_Combat.CalculateAttackData` **重复**伤害计算逻辑（暴击乘区、元素计算），未来公式修改存在不同步风险；
- `Entity.cs` 未使用 using：`System.Runtime.InteropServices.WindowsRuntime`、`Unity.VisualScripting.Antlr3.Runtime.Misc`（构建/平台风险）；`Entity_Combat` 未使用 `System.Data`；`Entity_VFX` 未使用 `System.Diagnostics`/`System.Xml.Linq`。

---

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重

1. **源码文件编码损坏（系统性）**
   - 位置：`Entity_StatusHandler.cs`（251 处）、`Entity_VFX.cs`（294）、`Enemy_Health.cs`（133）、`Entity.cs`（110）、`Enemy_VFX.cs`/`Enemy_BattleState.cs`/`StateMachine.cs` 等共 13 文件；`DamageScaleData.cs` 整文件 GBK。
   - 影响：注释全部丢失（违反项目注释规范）；GBK 文件非有效 UTF-8，存在编译/IL2CPP 风险；中文恢复需从 git 历史提取。
   - 改进：批量将全部 `.cs` 统一转存 UTF-8（含 BOM 可选）；从早期 git 提交还原被破坏注释的中文原文；后续提交强制 UTF-8（.editorconfig + gitattributes）。

2. **冰冻抗性减免无效（死代码）**
   - 位置：`Entity_StatusHandler.ApplyChillEffect` L90-93。
   - 现象：`finalDuration = duration × (1 − iceResistance)` 计算后未使用，`ChillCoEffecteCo(duration, ...)` 传入原始 duration —— 冰抗对减速时长承诺（GDD §2.3）未落地。
   - 改进：将 `finalDuration` 传入协程（并同步给 `SlowDownEntity` 与 `WaitForSeconds`）。

3. **局部顿帧目标销毁导致异常**
   - 位置：`HitStopManager.UpdateLocalHitStop` L97-136。
   - 现象：`hitStopDataMap` 持有已销毁的 `IHitStopable`，调用 `EndHitStop()/SetAnimationSpeed()` 抛 `MissingReferenceException`；无 `OnDestroy` 清理。
   - 改进：Manager 提供 `Unregister(target)`，目标 `OnDestroy` 时注销；或遍历时用 `target == null` / `TryGetComponent` 防御。

4. **自动元素模式元素身份丢失**
   - 位置：`Entity_Stats.GetElementalDamage` L111（`element = InputElement;` 应为 `element = mainElement;`）。
   - 现象：`InputElement==None` 时按最高元素算伤害（含 0.5 惩罚系数）但输出 `element=None` → 元素伤害全额生效（抗性 0）却**无状态效果、无元素特效、事件元素为 None**。
   - 改进：确认为意图后改为 `element = mainElement;`；同步影响 `Player_Combat.CalculateAttackData` 与 `AttackData` 构造（共用 GetElementalDamage）。

### 🟡 中等

5. **反击无敌期间仍可被击退/挂元素状态**
   - 位置：`Entity_Health.TakeDamage`（击退 L98 与元素应用在 `ReduceHP` 的 `canBeTakedDamage` 检查之前）；`Player_Combat.StartCounterInvincibility` 仅置 `canBeTakedDamage=false` 不置 `invincibleUntil`。
   - 现象：无敌期间仍被击退、仍可被挂灼烧/冰冻（DoT 随后穿透免疫）。
   - 改进：`canBeTakedDamage=false` 时在 TakeDamage 早期统一 return false（击退/状态一并跳过），或无敌期间同时置 `invincibleUntil`。

6. **`Enemy_Health.enemy` 属性化 GetComponent**
   - 位置：`Enemy_Health.cs` L8。
   - 改进：Awake 缓存字段。

7. **每次攻击 3 次 `OverlapCircleAll` 分配**
   - 位置：`Player_Combat.PerformAttack` / `TriggerLastComboHitStop`。
   - 改进：PerformAttack 内只查一次，数组复用于挥空判定与末段顿帧；或使用非分配重载（NativeArray）。

8. **`GetElementalDamage` LINQ 分配**
   - 位置：`Entity_Stats.cs` L100。
   - 改进：手写 max 扫描替代 `OrderByDescending`。

9. **`OnAttackHitResult` 恒 true，miss 分支永不广播**
   - 位置：`Entity_Combat.ProcessAttackOnTarget` / `OnTargetHit`。
   - 改进：闪避/无敌 miss 时广播 `(element, false)`，供 UI/音频区分。

10. **击退仅基于物理伤害**
    - 位置：`Entity_Health.TakeKnockBack(damageDealer, physicalDamageTaken)`。
    - 现象：纯元素攻击（phys=0）零击退。
    - 改进：按总伤害判定（或元素伤害×系数），与 GDD 设计意图核对。

11. **`RemoveAllNegativeEffects` 未重置感电充能**
    - 位置：`Entity_StatusHandler.cs` L35-40。
    - 现象：读档/复活后 `currentCharge` 残留，下次感电攻击可能瞬发雷击。
    - 改进：重置 `currentCharge = 0`。

12. **读档轮询每帧 4 次 `FindAnyObjectByType`（最长 5s）**
    - 位置：`SaveManager.ApplySaveDataDelayed` L134-143。
    - 改进：改为事件驱动（系统就绪信号）或单例引用 + 超时兜底。

13. **组件查找不一致**
    - 位置：`Entity_Health.Awake`（stats 用 `GetComponent`，entity/vfx 用 `GetComponentInParent`）。
    - 风险：层级调整后 `entityStats` 静默为 null → `GetMaxHP()=1`、减免/抗性=0、`InitializeHealth` 提前 return（currentHP 保持序列化值）。
    - 改进：统一 `GetComponentInParent`，并为关键依赖缺失加 `Debug.LogError`。

14. **全局顿帧双计时 + 局部顿帧双实现**
    - 位置：`HitStopManager.TriggerGlobalHitStop`（协程 + Update 双通道 End）；`Entity.StartHitStop/HitStopCo` 自带计时与 Manager 计时并存；`HitStopData.originalSpeed` 死字段；段乘数 0.3/0.7/0.3 硬编码。
    - 改进：单一计时通道；段乘数暴露为 SerializeField；删除死字段。

15. **未使用/危险 using**
    - `Entity.cs`：`System.Runtime.InteropServices.WindowsRuntime`、`Unity.VisualScripting.Antlr3.Runtime.Misc`；`Entity_Combat`：`System.Data`；`Entity_VFX`：`System.Diagnostics`、`System.Xml.Linq`。
    - 风险：编辑器专属/平台专属命名空间可能破坏构建，需清理。

### 🟢 轻微

16. 拼写错误批量修正（`GetDectectedCollders`→`GetDetectedColliders`、`burnDuratoin`、`chillSlowMulitplier`、`lighingStrikeVfx`、`onHeavyDamapeKnockback`、`ChockVFX`），注意 GDD/序列化字段同步。
17. `SetHealthToPercent` 改名（如 `HealByMaxPercent`），消除"设置"语义误导。
18. 清理生产路径 `Debug.Log`（Player_Combat 反击/追击/冷却、Entity_VFX 暴击粒子）。
19. 魔法数字配置化：击退阈值/公式常量/顿帧段乘数/随机待机区间 → SerializeField 或 SO。
20. 消除 `AttackData` 构造与 `Player_Combat.CalculateAttackData` 的重复逻辑（统一走同一管道，特殊攻击加成参数化）。
21. GDD 同步更新（§4.1 差异表 7 项）：接口 6 参数、`OnHealthUpdate` 无参、护甲公式 +200/0.6、满血读档、伤害数字已实现、Burn Bug 已修复、冰抗减免未落地。
22. `Entity.SlowDownEntityCo` 基类空实现（未覆写则减速静默失效）—— 基类提供默认实现或断言覆写。
23. `Enemy.EntityDead` 击杀上报以 `uniqueID` 非空为前提 —— 未配置 uniqueID 的敌人不触发任务进度，需检查所有敌人预制体配置完整性。

---

## 附：系统健康度小结

- **优点**：接口优先 + 组件组合架构落地扎实；双轨伤害（物理+元素）、两层三段式顿帧、元素状态互斥等核心机制完整且与 architecture.md 数据流一致；已知的 Burn Bug 已在 git 中修复；存档对战斗属性的覆盖（baseValue 全量）完整。
- **主要风险**：源码编码损坏（系统性、不可逆）、冰抗减免死代码、顿帧目标销毁崩溃、自动元素模式元素身份丢失 —— 4 项严重问题建议在新增功能前优先修复。
