---
status: reverse-documented
source: Assets/Scripts/Others/ItemSystem/EquipmentSystem.cs, Inventory_EquipmentSlot.cs
date: 2026-07-28
verified-by: oy
---

# 装备系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.
> **V2 增强**: 装备词缀系统 — 随机前缀+后缀替换固定 Modifier

---

## Overview (V2 新增)

装备词缀系统 V2 将当前固定的 `ItemModifier[]` 替换为随机词缀生成——每件掉落的装备随机获得 0-2 个前缀和 0-2 个后缀，词缀从 `AffixDatabase`（单一 SO 配置表）中按稀有度加权选取。词缀的数值范围由 Tier 和稀有度差异倍率决定，生成后的最终 Modifier 值存入 `Inventory_Item.Modifiers`，对下游装备/Modifier 系统和存档完全透明。

## Player Fantasy (V2 新增)

**"辨识装备"的瞬间**: 玩家捡起一件装备时，不是说"哦又一件蓝装"，而是"火焰前缀+吸血后缀——这可能是我的火系Build新核心"。暗黑2 玩家会停下来读装备名——同样的心理应该发生在这里。

**词缀驱动的Build**: 当你凑齐三件"烈焰之"前缀装备，火伤加成叠加到一个阈值，你决定洗掉冰系技能点全部投入火系——这就是 Pillar 2 "掉落改变打法"的实现。Build 不是从技能树规划的，而是被掉落引导的。

**参考**: 暗黑2 的"残忍之巨神之刃"——前缀+后缀+底材的组合让每一件暗金装备都独一无二。流放之路的词缀深度让玩家花数小时在交易站搜索特定组合。

---

## 1. 槽位结构

```
EquipmentSystem: MonoBehaviour

槽位配置:
  slotConfigs: SlotConfig[]
    └── 每个配置: ItemType + maxSlots
    // 例: 武器=1, 头盔=1, 盔甲=1, 靴子=1, 手套=1, 饰品=2

双字典存储 (需手动同步):
  slotDictionary: Dictionary<ItemType, List<Inventory_EquipmentSlot>>
    // ItemType → 该类型的槽位元数据列表
    // Inventory_EquipmentSlot { slotType, slotIndex, equippedItem }

  equipmentDictionary: Dictionary<ItemType, List<Inventory_Item>>
    // ItemType → 同索引的装备物品 (null = 空)
    // 与 slotDictionary[type][i] 一一对应

兼容层:
  equippedItems: List<Inventory_Item>  // 扁平列表, 向后兼容/序列化用
```

---

## 2. 装备工作流

### 2.1 装备

```
PlayerInventorySystem.TryEquipItem(item):
  │
  ├── GetItemSlot(item) → 从背包中找到槽索引
  ├── inventory.RemoveItem(item)
  │
  └── equipmentSystem.TryEquipItem(item):
        │
        ├── ValidateEquipmentItem(item):
        │     获取 item.itemData → EquipmentDataSo → ItemType
        │     检查 slotConfigs 中是否存在该类型 → 返回类型或 None
        │
        ├── FindFirstEmptySlot(type):
        │     foreach slot in slotDictionary[type]:
        │       if !slot.HasItem(): return slot.slotIndex
        │     无空槽 → 返回 slot[0] 的索引 (FIFO 驱逐!)
        │
        └── EquipItem(item, type, slotIndex):
              ├── slotDictionary[type][slotIndex] = item
              ├── equipmentDictionary[type][slotIndex] = item
              ├── UpdateCompatibilityList()  // 同步 equippedItems
              ├── item.AddModifiers(playerStats)
              │     └── foreach modifier in item.Modifiers:
              │           stat = playerStats.GetStatByType(mod.statType)
              │           stat.AddModifier(mod.value, item.itemID)
              │           // itemID 作为 source key
              └── OnEquipmentUpdated 事件

如果驱逐了旧装备 → TryEquipItem 把旧装备加回背包
```

### 2.2 卸下

```
PlayerInventorySystem.TryUnequipItem(item):
  │
  ├── inventory.CanAddItem()? → 容量检查
  │
  └── equipmentSystem.TryUnequipItem(item):
        │
        ├── FindEquipmentSlot(item):
        │     foreach itemType in equipmentDictionary:
        │       foreach equipItem in slot list:
        │         if equipItem == item → return (type, slotIndex)
        │
        └── UnequipItem(item):
              ├── item.RemoveModifiers(playerStats)
              │     └── stat.RemoveModifier(item.itemID)  // 按 source key 精准移除
              ├── slotDictionary[type][slotIndex] = null
              ├── equipmentDictionary[type][slotIndex] = null
              ├── UpdateCompatibilityList()
              └── OnEquipmentUpdated 事件

卸下后 → TryUnequipItem 把装备加回背包
```

### 2.3 槽位交换

```
TrySwapEquipmentSlots(itemType, slotA, slotB):
  │
  ├── 交换 slotDictionary[type][a] ↔ slotDictionary[type][b]
  ├── 交换 equipmentDictionary[type][a] ↔ equipmentDictionary[type][b]
  ├── UpdateCompatibilityList()
  └── RecalculateModifiers(itemType):
        └── 先移除该类型所有装备的 modifiers
         → 再重新添加 (暴力一致性保证)
```

---

## 3. Modifier 修饰符系统

### 数据流

```
EquipmentDataSo (ScriptableObject)
  modifiers: ItemModifier[]
    └── ItemModifier { statType: StatType, value: float }

Inventory_Item 构造时:
  普通路径: Modifiers = EquipmentDataSo.modifiers (直接复制)
  掉落路径: Modifiers[i].value = lootedItem.GetModifiedValue(originalValue)
            // 应用稀有度倍率, 四舍五入到 1 位小数

装备时:
  foreach mod in item.Modifiers:
    stat = playerStats.GetStatByType(mod.statType)
    stat.AddModifier(mod.value, item.itemID)  // itemID = "装备名_稀有度"

卸下时:
  foreach mod in item.Modifiers:
    stat = playerStats.GetStatByType(mod.statType)
    stat.RemoveModifier(item.itemID)  // 精确匹配 source key

Stat 内部:
  GetValue(): baseValue + sum(modifierList[i].value)
  // 每次调用重新计算，无缓存
```

### 关键设计决策 [推断]

- **用 itemID 作为 Modifier 的 source key** — 确保卸下时精准移除自己的修饰符，不受其他装备/消耗品 Buff 干扰
- **Slot[0] FIFO 驱逐** — 当装备类型的所有槽位满了，替换最早的装备而非拒绝

---

## 4. 存档

### 保存

```
CollectEquipment():
  → EquipmentSystem.GetEquippedItemsForSave()
    → List<EquipSaveInfo>:
        foreach type, slot in equipmentDictionary:
          if item != null:
            { itemId, itemType, slotIndex, rarity, multiplier }
```

### 恢复

```
ApplyEquipment():
  1. UnequipAllForSave() → 清空所有装备 (触发所有 RemoveModifiers)
  2. foreach EquipSaveInfo:
       ItemLookup.Find(itemId) → ItemDataSo
       new LootedItem(itemData, rarity) { statMultiplier = value }
       new Inventory_Item(lootedItem)
       equipmentSystem.TryEquipItemToSlot(item, slotIndex)
```

---

## 5. 边界情况

- ✅ 卸下装备 → 背包满时 TryUnequipItem 拒绝操作
- ⚠️ 满槽位换装: FIFO 驱逐旧装备 → 如果背包满 → 旧装备丢失
- ⚠️ 装备未配置 ItemType 的槽位 → ValidateEquipmentItem 返回 None → 静默失败
- ⚠️ 双字典手动同步: 新增操作需调用 UpdateCompatibilityList()，遗漏会导致不一致
- ✅ RecalculateModifiers 暴力重建一致性

---

## Formulas (V2 新增)

### 词缀数量
```
affixCount = rarity switch {
  普通 → 0-1 (50%概率有1个)
  精良 → 1-2
  稀有 → 2-3
  史诗 → 3-4 (至少1前缀+1后缀)
  传说 → 4 (固定2前缀+2后缀)
}
```

### 词缀选取
```
SelectAffixes(rarity):
  prefixPool = affixDB.prefixes.Where(tier ≤ rarity)
  suffixPool = affixDB.suffixes.Where(tier ≤ rarity)
  
  prefixCount = Random.Range(0, maxPrefix[rarity]+1)
  suffixCount = Random.Range(0, maxSuffix[rarity]+1)
  
  selectedPrefixes = SelectWeighted(prefixPool, prefixCount, noDuplicates)
  selectedSuffixes = SelectWeighted(suffixPool, suffixCount, noDuplicates)
```

### 数值范围
```
statValue = Random.Range(affix.minValue, affix.maxValue) × rarityMultiplier
// rarityMultiplier 由物品稀有度浮动决定 (已有 RarityCalculator)
// 例: 传说装备的词缀值最终 ×3.0
```

**变量表**:
| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| maxPrefix[rarity] | int[] | [0,1,2,3,2] | 每稀有度的最大前缀数 |
| maxSuffix[rarity] | int[] | [0,1,2,3,2] | 每稀有度的最大后缀数 |
| affix.minValue | float | — | 词缀的最小数值 |
| affix.maxValue | float | — | 词缀的最大数值 |
| rarityMultiplier | float | 1.0–3.0 | 已由RarityCalculator提供 |

## Dependencies (V2 新增)

| 依赖系统 | 方向 | 接口 |
|----------|------|------|
| 稀有度系统 (F3) | ←上游 | `RarityCalculator` 提供 rarityMultiplier |
| 物品-掉落 (C7) | ←上游 | 掉落时触发 `AffixDatabase.GenerateAffixes()` |
| 敌人-精英词缀 (C14) | →下游 | 精英词缀与装备词缀共享词缀池（同名联动） |
| 物品-制作 (F9) | →下游 | 制作时保留/转移特定词缀 |
| 存档系统 (F2) | →下游 | `Inventory_Item.Modifiers` 已包含生成后的值 |
| UI系统 (F4) | →下游 | 装备Tooltip显示词缀名称+数值 |

## Tuning Knobs (V2 新增)

| 参数 | 安全范围 | 破坏点 | 影响 |
|------|----------|--------|------|
| 每稀有度最大词缀数 | 0-4 | >4→装备太强 | 掉落价值和Build深度 |
| 词缀数值范围(min-max) | 1-100 | min>max→bug | 单件装备的强度 |
| 前缀/后缀池大小 | 20-50每类 | <10→重复感 | 装备多样性 |
| Tier权重 (精/稀/史/传) | 见稀有度系统 | — | 高级词缀出现频率 |
| 特定词缀出现权重 | 1-10 | 0→永不出现 | 稀有词缀的实际稀有度 |

## Acceptance Criteria (V2 新增)

- **GIVEN** 一件稀有掉落的装备，**WHEN** 玩家查看装备详情，**THEN** 显示 2-3 个随机词缀（前缀+后缀），每个词缀有名称和数值
- **GIVEN** 两件同底材装备，**WHEN** 比较它们的词缀，**THEN** 词缀组合不同（随机生成）
- **GIVEN** 稀有装备有3个词缀，**WHEN** 装上装备，**THEN** 所有词缀的Modifier值正确应用到玩家属性
- **GIVEN** 传说装备，**WHEN** 生成词缀，**THEN** 必含至少1个传说级词缀
- **GIVEN** 装备被卸下，**WHEN** 查看属性，**THEN** 所有词缀Modifier被正确移除

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |

---

*本文档由 `/reverse-document design` 生成*
