---
status: reverse-documented
source: Assets/Scripts/SkillSyetem/
date: 2026-07-28
verified-by: oy
---

# 技能系统 — 设计文档

> **⚠️ 反向文档说明**: 本文档从现有实现反向生成。[推断] = 代码分析推断，[待确认] = 需验证。

---

## 1. 概述

**用途**: 管理所有玩家技能的完整生命周期：解锁、升级、装备到槽位、使用、冷却、附魔计数。

**范围**: 11 个技能（冲刺/冲刺虚化/时之碎片/时之回响/领域展开/火/冰/雷/二段跳/元素精通/强力反击追猎）+ 技能槽位系统 + 被动技能管理 + 技能点系统。

**架构特征**: 四路并行数据路径 — 组件层(Skill_Base) + 数据缓存(SkillDataManager) + 槽位(SkillSlotManager) + 被动(PassiveSkillManager)，通过事件同步。

---

## 2. 技能系统架构

### 2.1 四路并行数据路径

```
                           技能树UI
                              │
                    SkillDataManager (中心缓存)
                    UpdateSkillData(upgradeType, data, level)
                              │
                ┌─────────────┼──────────────┐
                │             │              │
                ▼             ▼              ▼
    OnSkillDataUpdated   OnPassiveSkillUpdated   GetSkillType/GetLevelData/...
         │                   │                    │
         ▼                   ▼                    ▼
   SkillSlotManager    PassiveSkillManager    外部查询 (存储/UI)
   (主动技能 → 槽位)    (被动技能 → 激活)
         │                   │
         ▼                   ▼
  Player_SkillManager.GetSkillByType()
         │
         ▼
  Skill_Base 组件 (Player GameObject 上)
  TryUseSkill() / SetSkillLevelData() / RefundSkillUpgrade()
```

### 2.2 技能类型

| SkillType | 对应类 | 使用方式 |
|-----------|--------|----------|
| Dash | Skill_Dash | 主动 → 槽位 |
| DashBlur | Skill_DashBlur | 主动 → 槽位 |
| TimeShard | Skill_Shard | 主动 → 槽位 |
| TimeEcho | Skill_TimeEcho | 主动 → 槽位 |
| DomainExpansion | Skill_DomainExpansion | 主动 → 槽位 |
| Fire | Skill_Fire | 主动(附魔) → 槽位 |
| Ice | Skill_Ice | 主动(附魔) → 槽位 |
| Lighting | Skill_Lighting | 主动(附魔) → 槽位 |
| DoubleJump | Skill_DoubleJump | 被动 → 自动激活 |
| ElementalMastery | Skill_ElementalMastery | 被动 → 自动激活 |
| PowerCounterChase | Skill_PowerCounterChase | 被动 → 自动激活 |

### 2.3 技能玩法分类

| 类别 | 包含技能 | 使用机制 |
|------|----------|----------|
| **移动** | Dash, DashBlur, DoubleJump | 按键直接触发 |
| **领域/召唤** | DomainExpansion, TimeEcho, TimeShard | 按键触发 + 生成 SkillObject |
| **元素附魔** | Fire, Ice, Lighting | 按键激活附魔 → 攻击消耗次数 |
| **被动增益** | ElementalMastery, PowerCounterChase | 解锁后自动生效 |

---

## 3. 核心工作流

### 3.1 技能解锁 → 激活全链路

```
1. 技能树UI: 玩家分配技能点 → UI_TreeNode 触发解锁

2. SkillDataManager.UpdateSkillData(upgradeType, skillData, level)
   ├── 创建/更新 SkillDataCache { skillData, currentLevel, skillType }
   ├── 触发 OnSkillDataUpdated(upgradeType, level)
   │     └── SkillSlotManager.OnSkillDataUpdated()
   │           ├── passive? → 跳过
   │           └── active? → 查找装备槽位 → ActivateUpgradeType()
   │                 └── skill.SetSkillLevelData(upgradeType, levelData, level)
   │                       ├── totalLevel++
   │                       ├── upgradeTypeLevels[type] = level
   │                       ├── cooldown = levelData.cooldown
   │                       ├── damageScaleData = levelData.damageScaleData
   │                       ├── ResetCooldown() // 解锁后立即可用
   │                       └── [元素技能] ApplyElementalDamageBonus()
   │                             └── stats.offense.[fire/ice/lightning]Damage.AddBaseValue(diff)
   │
   └── 如果是被动 → OnPassiveSkillUpdated(upgradeType, level)
         └── PassiveSkillManager.OnPassiveSkillUpdated()
               └── ActivatePassiveSkill(upgradeType, level)
                     ├── SkillDataManager.GetSkillType(upgradeType) → SkillType
                     ├── skillManager.GetSkillByType(skillType) → Skill_Base
                     ├── level > 0? → skill.SetSkillLevelData(upgradeType, levelData, level, false)
                     └── level == 0? → skill.RefundSkillUpgrade()
```

### 3.2 技能使用流程（主动技能）

```
1. SkillSlotManager.Update()
   └── HandleSkillSlotInput()
         └── if(Input.GetKeyDown(KeyCode.Alpha1-5))
               └── UseSkillInSlot(slotIndex)

2. UseSkillInSlot(slotIndex)
   ├── GetUpgradeTypeInSlot(slotIndex) → SkillUpgradeType
   ├── skillManager.GetSkillByType(skillType) → Skill_Base
   └── skill.TryUseSkill()

3. Skill_Base.CanUseSkill()
   ├── upgradeType == None? → false
   └── IsOnCooldown()? → 显示"CD: X.X" → false

4. 各技能 TryUseSkill() 覆写:
   ├── Skill_Dash.TryUseSkill() → 修改 player.dashSpeed → 生成时之碎片
   ├── Skill_ElementalBase.TryUseSkill() → ActivateEnchant(elementType)
   └── Skill_DomainExpansion.TryUseSkill() → 实例化 DomainExpansion SkillObject
```

### 3.3 元素附魔工作流

```
Skill_ElementalBase.TryUseSkill()
  │
  ├── CanUseSkill()? → 检查 CD + 是否解锁
  │
  ├── GetElementTypeFromSkillType(skillType)
  │   Fire → ElementType.Fire
  │   Ice → ElementType.Ice
  │   Lighting → ElementType.Lightning
  │
  ├── ActivateEnchant(elementType)
  │   ├── GetEnchantDataByType(type) → ElementalEnchantData
  │   ├── data.ResetCount() → currentCount = currentMaxCount
  │   ├── playerStats.InputElement = elementType
  │   └── 显示 "${elementType} {count}"
  │
  └── StartSkillCooldown()

每次攻击命中:
  OnAttackHitResult(attackElement, targetGotHit)
    ├── currentActiveEnchant == None? → 跳过
    ├── attackElement != currentActiveEnchant? → 跳过
    ├── !targetGotHit? → "空挥：附魔次数未扣除" → 跳过
    ├── data.DeductCount() → currentCount--
    └── currentCount <= 0?
         ├── currentActiveEnchant = None
         ├── playerStats.InputElement = None
         └── "附魔次数耗尽"

每级升级:
  UpdateMaxCount(totalLevel)
    └── currentMaxCount = baseMaxCount + (totalLevel / 3) // 每3级+1次

每次升阶:
  ApplyElementalDamageBonus(elementType)
    └── stats.offense.[element]Damage.AddBaseValue(diff) // +1/级
```

### 3.4 技能点系统

```
SkillPointManager (单例, DontDestroyOnLoad)

totalSkillPoints         // 累计获得
usedSkillPoints          // 已使用
AvailableSkillPoints = total - used

来源:
  PlayerLevelManager → AddSkillPoints(amount, "升级")
  QuestManager → AddSkillPoints(amount, "任务奖励")

消耗:
  UI_TreeNode → UseSkillPoints(amount) → true/false

返还:
  UI_TreeNode 重置 → RefundSkillPoints(amount)
  ResetSkillPoints() → 全部返还

事件: OnSkillPointsChanged(Available) → UI 更新
       OnSkillPointsUsed(Available) → 日志
```

---

## 4. 各技能实现细节

### 4.1 Skill_Dash

```
TryUseSkill():
  └── 修改 player.dashSpeed (根据等级)
      └── FlashDash 等级 → dashSpeed 倍率递增

SetSkillLevelData():
  └── 升级时自动生成时之碎片(clone)效果
```

### 4.2 Skill_DashBlur

```
被动效果: Dash 期间 invulnerability (虚化状态)
```

### 4.3 Skill_Shard (时之碎片)

```
TryUseSkill():
  └── 实例化 SkillObject_Shard
      ├── 可传送至碎片位置
      ├── 多重投射 (multicast 升级)
      └── 自动追踪敌人 (追踪升级)
```

### 4.4 Skill_TimeEcho (时之回响)

```
TryUseSkill():
  └── 生成克隆体
      克隆升级:
        ├── 单体攻击 → 多重攻击
        ├── 复制本体动作
        └── 生成治疗/净化/冷却精灵(wisp)
```

### 4.5 Skill_DomainExpansion

```
TryUseSkill():
  └── 实例化 SkillObject_DomainExpansion (大范围 AoE)
      领域内效果:
        ├── 减速 (领域基础)
        ├── 时之碎片自动生成
        └── 回响增强
```

### 4.6 Skill_ElementalBase (火/冰/雷 基类)

```
元素附魔系统 (详见 3.3)

差异:
  Fire:   灼烧 DoT (通过战斗系统 ApplyBurnEffect)
  Ice:    减速 (通过战斗系统 ApplyChillEffect)
  Lightning: 感电充能 (通过战斗系统 ApplyShockEffect)
```

### 4.7 Skill_DoubleJump

```
被动: 解锁后 player 获得二段跳能力
实现: 在 Player_AiredState 中检查是否已解锁
```

### 4.8 Skill_ElementalMastery

```
被动: GetElementalMasteryFactor()
公式 [推断]: totalLevel × 每个等级的 mastery 加成
影响: 元素伤害公式中的 次要元素贡献比率
```

### 4.9 Skill_PowerCounterChase

```
被动: 强化 Counter Attack
效果:
  ├── 追猎时附加晕眩几率
  └── 额外无敌帧
```

---

## 5. 技能槽位系统

```
SkillSlotManager (单例)

5 个槽位 (KeyCode.Alpha1-5)

BindSkillToSlot(slotIndex, upgradeType):
  ├── 检查冲突 (同一技能不能装两个槽)
  └── skillSlots[slotIndex].Bind(upgradeType)

UnbindSkillFromSlot(slotIndex):
  └── skillSlots[slotIndex].Unbind()

按键盘 1-5 → UseSkillInSlot(index):
  ├── 获取槽位的 upgradeType
  ├── DataManager.GetSkillType(type) → SkillType
  ├── skillManager.GetSkillByType(skillType) → Skill_Base
  └── skill.TryUseSkill()
```

---

## 6. 数据结构

### Skill_DataSo (ScriptableObject)

```
displayName, icon
skillType: SkillType          // 对应哪个 Skill_Base 组件
upgradeType: SkillUpgradeType // 升级节点的枚举标识
conflictSkillTypes[]          // 互斥技能 (不能同时装备)
usageType: SkillUsageType     // Active | Passive
preSkillRequirements[]        // 前置技能
maxLevel, currentLevel
LevelData[]:
  └── cooldown: float
  └── damageScaleData: DamageScaleData
  └── [各技能特定数据]
```

### SkillDataCache (SkillDataManager 内部)

```
skillData: Skill_DataSo
currentLevel: int
skillType: SkillType
```

### SkillSlotData

```
slotIndex: int
upgradeType: SkillUpgradeType | None
isOccupied: bool
```

### ElementalEnchantData

```
elementType: ElementType
baseMaxCount: int = 3          // 初始附魔次数
currentCount: int              // 当前剩余
currentMaxCount: int           // 动态最大值 = baseMaxCount + totalLevel/3
```

---

## 7. 持久化

存档内容 (SkillSaveData):
```
- 每个 SkillUpgradeType 的等级 (Dictionary<SkillUpgradeType, int>)
- totalSkillPoints, usedSkillPoints
- 槽位绑定 (Dictionary<int, SkillUpgradeType>)
```

恢复流程:
```
SaveManager → SkillDataManager.UpdateSkillData() → 事件链 → 技能激活
SaveManager → SkillPointManager.LoadFromSave(total, used)
SaveManager → SkillSlotManager.RestoreBindings(bindings)
SaveManager → PassiveSkillManager.RefreshAllPassiveSkills()
```

---

## 8. 集成点

| 上游依赖 | 提供 |
|----------|------|
| Player_SkillManager | 11 个 Skill_Base 组件引用、GetSkillByType() switch |
| Skill_DataSo | SO 技能配置数据 |
| Entity_Stats / Player_Combat | 属性修改、攻击事件 |
| GameInput (静态) | 键盘输入 (Alpha1-5) |

| 下游被依赖 | 使用 |
|-----------|------|
| UI_SkillTree | SkillDataManager.UpdateSkillData() |
| UI_SkillSlot | SkillSlotManager.Bind/Unbind + 显示 |
| 宝石镶嵌 (F12) | 宝石嵌入技能槽 → 属性/特殊效果 |
| SaveSystem | GetAllSkillLevels() + slot bindings |
| CombatSystem | InputElement (元素附魔) |

---

## 9. 边界情况和发现的问题

### 已处理
- ✅ 解锁后 ResetCooldown() — 立即可以使用
- ✅ 被动技能 OnPassiveSkillUpdated 传 level=0 表示移除
- ✅ 元素附魔空挥不消耗次数
- ✅ 销毁时清理事件订阅 (OnDestroy)
- ✅ 技能退回: RefundSkillUpgrade() 清除所有升级数据

### 发现的问题
- ⚠️ **Skill_Base 的 `damageScaleData` 每次 `new DamageScaleData()` 初始化为全 1** — 未解锁时伤害倍率为 1（无意义但无害）
- ⚠️ **GetSkillByType() 是大 switch 语句** — 新增技能需要手动更新
- ⚠️ **PassiveSkillManager.Start() 用 `FindAnyObjectByType` 获取 skillManager** — 启动时序依赖
- ⚠️ **`upgradeType` 升级时覆盖** — SetSkillLevelData 中 `this.upgradeType = upgradeType` 假设一个 Skill_Base 只绑定一个 UpgradeType，对于多升级路径的技能可能出错
- ⚠️ **存档恢复时 `RefreshAllPassiveSkills` 需要在所有技能数据加载完毕后调用** — 时序依赖

---

## 10. 后续工作

- [ ] 为多升级路径技能增强 upgradeType 追踪 (目前只记录最后一个)
- [ ] 将 `GetSkillByType()` switch 改为 Dictionary 自动注册
- [ ] 用 Service Locator 替换 `FindAnyObjectByType`
- [ ] ADR: 记录为什么选择四路并行而非统一技能框架

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 从 SkillSyetem/ 反向生成 |

---

*本文档由 `/reverse-document design` 生成*
