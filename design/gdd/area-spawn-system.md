---
status: reverse-documented
source: Assets/Scripts/Others/Area/
date: 2026-07-28
verified-by: oy
---

# 区域/刷怪系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。

---

## 1. 架构

```
SceneLevelArea (场景级配置)
  ├── AreaDifficulty (区域难度参数)
  │     ├── 精英生成率加成
  │     ├── 等级加成
  │     ├── 等级浮动概率 (保持/降低/提升)
  │     └── 最大等级提升
  │
  └── EnemySpawner (敌人生成器)
        ├── enemyPrefabs: 可选敌人列表
        ├── spawnPoints: 生成点数组
        └── 协程生成: 延迟 + 间隔 + 数量

EnemyArea (敌人区域触发器)
  └── 玩家进入 → 触发 EnemySpawner.SpawnEnemies()
  └── 玩家离开 → 可选清理敌人?
```

---

## 2. EnemySpawner 生成流程

```
SpawnEnemies(count, eliteChance, difficulty, baseLevel, delay, interval, clearExisting):
  │
  ├── yield WaitForSeconds(delay)   // 延迟生成
  ├── if clearExisting: ClearSpawnedEnemies()
  │
  └── for i in count:
        │
        ├── ShouldSpawnElite(chance, difficulty):
        │     actualChance = eliteChance + difficulty.GetEliteChanceBonus()
        │     return Random.value < actualChance
        │
        ├── SelectEnemyPrefab():
        │     return enemyPrefabs[Random.Range(0, Count)]
        │
        ├── GetSpawnPosition():
        │     有 spawnPoints? → 随机选一个
        │     无? → transform.position + Random.insideUnitCircle * 2f
        │
        ├── SpawnEnemy(prefab, pos): Instantiate(prefab, pos, identity)
        │
        ├── InitializeEnemy:
        │     type = isElite ? Elite : Normal
        │     level = CalculateEnemyLevel(isElite, difficulty, baseLevel):
        │       baseLevel + difficulty.GetLevelBonus()
        │       + (isElite ? Random(2,4) : 等级浮动(-1/0/+1~N))
        │       clamp to Min(1)
        │
        └── OnEnemySpawned 事件

管理:
  GetAliveEnemyCount() → 遍历 spawnedEnemies, 检查 !IsDead
  ClearSpawnedEnemies() → 全部 Destroy → Clear list
```

---

## 3. 等级浮动机制

```
CalculateNormalLevelFloat(difficulty):
  (keepChance, lowerChance, upperChance) = difficulty.GetLevelFloatChances()
  roll = Random.value

  if roll < lowerChance:      return -1
  if roll > 1 - upperChance:  return Random(1, maxIncrease+1)
  else:                       return 0

等级组成:
  最终等级 = baseEnemyLevel + difficulty.GetLevelBonus() + 浮动值
  下限: Max(1, final)
```

---

## 4. SceneLevelArea

```
场景级别的区域管理器 [推断]:
  └── 持有 AreaDifficulty 引用
  └── 持有 EnemySpawner 引用
  └── 场景加载时配置该区域的基础难度/敌人生成参数
```

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
