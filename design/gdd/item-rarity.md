---
status: reverse-documented
source: Assets/Scripts/Others/ItemSystem/LootSystem/RarityCalculator.cs, LootRarity.cs
date: 2026-07-28
verified-by: oy
---

# 稀有度系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 稀有度等级

```
LootRarity 枚举:
  普通(0) | 精良(1) | 稀有(2) | 史诗(3) | 传说(4)

基础属性倍率 (非等比):
  普通  1.0x
  精良  1.5x  (+0.5)
  稀有  2.0x  (+0.5)
  史诗  2.5x  (+0.5)
  传说  3.0x  (+0.5)

颜色映射:
  普通 → White
  精良 → Green
  稀有 → Blue
  史诗 → Purple (0.5, 0, 0.5)
  传说 → Red
```

---

## 2. 浮动算法

### 输入

```
GetVariedRarity(baseRarity, maxSteps, rarityBonus):
  baseIndex   = (int)baseRarity     // 基础稀有度索引 (0-4)
  maxSteps    = itemData.maxRaritySteps  // 最大浮动步数
  rarityBonus = LootTable.extraRarityDropChanceBonus  // 向上加成 (0-200)
```

### 浮动范围

```
minIndex = Max(0, baseIndex - maxSteps)
maxIndex = Min(4, baseIndex + maxSteps)

候选: 从 minIndex 到 maxIndex (含)
```

### 权重

```
距离 0 (基础稀有度):     weight = 100           // 主导
距离 +1 (向上 1 级):     weight = 15 + bonus×0.75
距离 +2 (向上 2 级):     weight =  3 + bonus×0.5
距离 +3 (向上 3 级):     weight =  1 + bonus×0.25
距离 -1 (向下 1 级):     weight = 15             // bonus 不影响向下
距离 -2 (向下 2 级):     weight =  3
距离 -3 (向下 3 级):     weight =  1

示例 (基础=普通(0), maxSteps=2, bonus=50%):
  候选: 普通(0), 精良(1), 稀有(2)
  权重: 100,  15+37.5=52.5,  3+25=28
  概率: 55.3%,      29.1%,    15.5%

示例 (基础=精良(1), maxSteps=3, bonus=50%):
  候选: 普通(0), 精良(1), 稀有(2), 史诗(3), 传说(4)
  权重: 15,      100,       52.5,     28,       13.5
  概率: 7.2%,    47.8%,     25.1%,    13.4%,    6.5%
```

### 返回值

```
weightedIndex = 加权随机选择 (0..候选数-1)
actualIndex = minIndex + weightedIndex
return (LootRarity)actualIndex
```

---

## 3. 倍率计算

```
差异倍率:
  GetRarityDifferenceMultiplier(baseRarity, actualRarity):
    return GetBaseMultiplier(actual) / GetBaseMultiplier(base)

示例:
  普通(1.0) → 传说(3.0): 3.0 / 1.0 = 3.0x
  精良(1.5) → 史诗(2.5): 2.5 / 1.5 = 1.67x
  史诗(2.5) → 普通(1.0): 1.0 / 2.5 = 0.4x

应用到属性:
  modifiedValue = Mathf.Round(baseValue × multiplier × 10) / 10
  // 四舍五入到 1 位小数

例: baseValue=10, 普通→传说(3.0x) → 30.0
    baseValue=10, 普通→精良(1.5x) → 15.0
    baseValue=7, 普通→稀有(2.0x) → 14.0
```

---

## 4. Bonus 传递链

```
LootTable.extraRarityDropChanceBonus (每个掉落表可配置, 默认50%)

  → LootDropItem.GetDroppedRarity(bonus):
       if useItemRarity:
         return itemData.GetDroppedRarity(bonus)
           → RarityCalculator.GetVariedRarity(rarity, maxSteps, bonus)
       else:
         return customRarity  // 不浮动

  → ItemDataSo.GetDroppedRarity(bonus):
       if allowRarityVariation:
         return GetVariedRarity(rarity, maxRaritySteps, bonus)
       else:
         return rarity  // 禁止浮动
```

---

## 5. 设计分析

### 意图 [推断]

- **不对称分布**: 向上浮动比向下概率高（bonus 只影响向上）— 给玩家"运气好"的正面体验
- **基础稀有度占主导**: weight=100 确保大部分时候拿到预期品质
- **bonus 机制**: 高难敌人/宝箱配高 bonus，让更好掉落集中在挑战性内容

### 实际效果

| Bonus | +1 权重 | +2 权重 | 效果 |
|-------|---------|---------|------|
| 0% | 15 | 3 | 基础: 升1级≈13%, 升2级≈2.5% |
| 50% | 52.5 | 28 | 中等: 升1级≈34%, 升2级≈22% |
| 100% | 90 | 53 | 高: 升1级≈47%, 升2级≈35% |
| 200% | 165 | 103 | 极高: 升1级≈62%, 升2级≈51% |

---

## 6. 与装备系统的集成点

```
掉落时:
  LootedItem(itemData, actualRarity) → statMultiplier

转换为背包物品:
  Inventory_Item(LootedItem):
    foreach modifier in EquipmentDataSo.modifiers:
      modifiedValue = lootedItem.GetModifiedValue(originalValue)
      item.Modifiers[i] = { statType, modifiedValue }

存档恢复:
  SaveData 存储 rarityMultiplier
  → 加载时重建 LootedItem { statMultiplier = 保存值 }
  → 构造函数重新计算 modifier 值 → 应与保存前一致
```

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
