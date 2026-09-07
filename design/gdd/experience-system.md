---
status: reverse-documented
source: Assets/Scripts/Character/ExperienceSystem/
date: 2026-07-28
verified-by: oy
---

# 经验/等级系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。

---

## 1. 等级曲线

```
LevelCalculator: 三阶段公式, 最大等级 50, 起始经验 100

Phase 1 (Lv 0-9):   100 × (level+1)
                     线性: 100, 200, 300, ..., 1000

Phase 2 (Lv 10-29):  100 × (level+1) × 1.5^((level-9)/10)
                     温和指数 (每10级×1.5)

Phase 3 (Lv 30-49):  100 × (level+1) × 2.0^((level-29)/20)
                     陡峭指数 (每20级×2.0)

Lv 50 (Max): GetExpToNextLevel = 0

⚠️ 玩家起始等级为 0 (非 1)
   首次升级 (0→1) 需要 100 exp
```

### ⚠️ Bug: GetTotalExpRequired 跳过等级 0

```
loop: for i = 1 to targetLevel-1  // 从 1 开始, 漏掉了 0→1 的 100 exp
→ 总经验少算 100
→ GetCurrentLevelFromExp 可能返回错误等级
修复: loop 从 i = 0 开始
```

---

## 2. 经验获取全链路

```
敌人死亡
  → Entity_Health.Die() → entity.EntityDead() → OnEntityDead 事件
  → EnemyDefeatedTrigger.OnEnemyDead():
      exp = 20 × enemyLevel (基本经验)
      → TriggerExperience(EnemyExpData)

ExperienceManager.AddExperience(expEvent):
  最终经验 = baseExp × 全局倍率 × 来源倍率 × 等级差调整

  等级差调整 (仅 EnemtDefeated 来源):
    diff = enemyLevel - playerLevel
    bonus:  1.0 + min(diff×0.1, 10×0.1), 上限 2.0
    penalty: 1.0 - min(|diff|×0.1, 10×0.1), 下限 0.1
    例: 敌人高5级 → 1.5x, 敌人高10级+ → 2.0x
        敌人低5级 → 0.5x, 敌人低10级- → 0.1x

PlayerLevelManager.AddExp(finalExp):
  currentExp += exp
  OnExpGained 事件
  → CheckLevelUp():
      while currentExp >= ExpToNextLevel && level < MaxLevel:
        currentExp -= ExpToNextLevel
        currentLevel++
        attributePoints += CalculateFreeAttributePoints(level)
        skillPoints += CalculateSkillPoints(level)
        if level <= 5: AutoIncreaseMajorAttributes() → 四属性+1
        SkillPointManager.AddSkillPoints(skillGain, "等级提升")
        OnLevelUp 事件

⚠️ 任务经验绕过 ExperienceManager:
  QuestManager.GrantReward 直接调用 PlayerLevelManager.AddExp
  → 不受全局倍率/等级差调整影响
  → ExperienceSourceType 没有 Quest 条目
```

---

## 3. 奖励系统

### 属性点

| 等级段 | 每级获得 |
|--------|----------|
| 1-10 | 1 |
| 11-30 | 2 |
| 31+ | 3 |

### 技能点

| 等级段 | 每级获得 |
|--------|----------|
| 1-15 | 1 |
| 16-50 | 2 |
| 51+ | 3 (死代码 — MaxLevel=50) |

### 自动属性增长 (Lv 1-5)

```
每次升级 → AutoIncreaseMajorAttributes():
  Strength   +1 baseValue
  Agility    +1 baseValue
  Intelligence +1 baseValue
  Vitality   +1 baseValue

特点:
  - 直接修改 Stat.baseValue, 不经过 AttributePointManager
  - 不可重置 (ResetAttributePoints 不会影响这些值)
  - 通过 StatSaveEntry.baseValue 隐式持久化
```

---

## 4. AttributePointManager

```
4 个可分配属性: 力量/敏捷/智力/活力

AllocatePoint(StatType):
  stat.AddBaseValue(1)
  PlayerLevelManager.UseAttributePoints(1)
  OnAttributePointAllocated 事件

ResetAll():
  1. 统计所有已分配点数
  2. 从每个属性的 baseValue 中扣除
  3. 清空分配记录
  4. 将总点数返还给 PlayerLevelManager

⚠️ 无单项属性上限 — 可全投入单一属性
```

---

## 5. 敌人等级缩放

```
EnemyLevelSystem (放在敌人 GameObject 上):

属性倍率 = 1 + (enemyLevel - 1) × 0.1

乘法类 (×倍率): MaxHP, 物理伤害, 护甲穿透, 元素之心, 火/冰/雷伤, 护甲
加法类:        暴击率 +0.5%/级, 暴击伤害 +1%/级, 元素抗性 +0.5%/级

例: Lv 10 敌人 → 1.9×乘法属性
    Lv 50 敌人 → 5.9×乘法属性

安全: ApplyLevelBonus 先重置默认值再应用 → 防止重复乘算
```

### 敌人生成时的等级

```
EnemySpawner.CalculateEnemyLevel(isElite, difficulty, baseLevel):
  level = baseLevel + difficulty.GetLevelBonus()
  + (isElite? Random(2,4) : 等级浮动(-1/0/+1~N))
  → Max(1, level)
```

---

## 6. 事件链

```
EnemyDefeatedTrigger
  → ExperienceManager.OnExperienceGained
  → PlayerLevelManager.AddExp
    → OnExpGained(int)              ← UI_InGame 监听 → 更新经验条
    → CheckLevelUp():
        → OnLevelUp(int)            ← UI_InGame 监听
        → OnSkillPointsChanged
        → OnAttributePointsChanged
        → eventTip.ShowLevelUp()
```

---

## 7. 持久化

```
存档:
  PlayerSaveData.currentLevel / currentExp
  PlayerSaveData.unspentSkillPoints / unspentAttributePoints
  SkillPointManager.totalSkillPoints / usedSkillPoints (分别存档)
  StatSaveEntry[].baseValue (隐式包含自动属性增长)

读档:
  PlayerLevelManager.LoadFromSave(level, exp, pts) — 直接赋值, 无 CheckLevelUp
  ⚠️ 如果存档时经验够升级但未处理, 读档后保持原等级
  SkillPointManager.LoadFromSave(total, used)
  StatSaveEntry 逐项恢复
```

---

## 8. 问题汇总

| 严重度 | 问题 | 位置 |
|--------|------|------|
| 🟡 中 | GetTotalExpRequired 跳过 Lv0→1 的 100exp | LevelCalculator |
| 🟡 中 | LoadFromSave 不调用 CheckLevelUp — 待处理升级丢失 | PlayerLevelManager |
| 🟢 低 | 任务经验绕过 ExperienceManager (无倍率/无事件) | QuestManager |
| 🟢 低 | SetLevel(0) 被拒绝但与初始值 0 不一致 | PlayerLevelManager |
| 🟢 低 | 技能点跟踪双系统(PlayerLevelManager + SkillPointManager)不同步风险 | 两个系统 |
| 🟢 低 | 无单项属性分配上限 — 可极端单属性 Build | AttributePointManager |

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
