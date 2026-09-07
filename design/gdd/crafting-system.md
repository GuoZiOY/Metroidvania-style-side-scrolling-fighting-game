# 制作系统

> **Status**: Designed | **Date**: 2026-07-29 | **Pillar**: Pillar 2

## Overview

制作系统让玩家使用从敌人掉落和分解装备获得的材料，通过配方制作装备和消耗品。游戏提供固定配方（如"3个铁矿石→1把铁剑"），所有配方存储在 `CraftingRecipeDB`（单一SO）中。制作结果受材料稀有度影响——高品质材料可提升制作品的稀有度下限。

## Player Fantasy

暗黑2 赫拉迪克方块的满足感——放入材料、合上、打开、一件新装备出现。制作不是随机赌博（那是掉落），而是有目的地填补 Build 缺失件的方式。

## Detailed Design

### Core Rules

**CraftingRecipeDB (ScriptableObject)**:
```
recipes: CraftingRecipe[]
  ├─ resultItem: ItemDataSo       // 制作产物
  ├─ materials: MaterialEntry[]   // 所需材料+数量
  │    └─ { itemId, count }
  ├─ minResultRarity: LootRarity? // 材料品质锁定的最低稀有度
  └─ goldCost: int                // 制作费用

MaterialEntry:
  itemId: string    // 对应 ItemDataSo.itemId
  count: int        // 需要数量
```

**制作流程**:
```
玩家在城镇铁匠处打开制作UI
  → 显示可用配方列表（灰色=缺材料，亮色=可制作）
  → 选择配方 → 预览产物（含稀有度范围）
  → 点击制作 → 扣除材料+金币 → 生成产物 → 入背包
```

**材料来源**:
| 来源 | 材料类型 |
|------|----------|
| 敌人掉落 | 怪物部位(皮/骨/爪) |
| 分解装备 | 矿石/布料/精华 |
| 采集点(可选) | 草药/矿石 |
| Boss掉落 | 独特制作材料 |

### Interactions

| 系统 | 方向 | 接口 |
|------|------|------|
| 物品-背包 (C5) | ↔ | 读取材料+存入产物 |
| 物品-掉落 (C7) | ← | 材料掉落配置 |
| 分解系统 (F10) | ← | 分解产物作为材料输入 |
| 稀有度 (F3) | ← | minResultRarity |
| 商店 (C8) | ↔ | 可购买基础材料 |
| UI | → | 制作面板 |

## Formulas

```
制作稀有度:
  resultRarity = max(
    recipe.minResultRarity,
    avgRarity(materials)  // 材料平均稀有度
  )
  // avgRarity: 加入稀有度最高+最低的两件材料计算平均值
```

## Edge Cases

- **材料不足**: 配方灰色不可制作
- **背包满**: 产物掉落在铁砧旁（不直接入包）
- **材料被其他配方消耗**: 实时检查背包中的实际数量
- **制作传说级装备**: 至少需要1个传说级材料

## Dependencies

| 系统 | 方向 | 硬/软 |
|------|------|-------|
| 物品-背包 (C5) | ↔ | 硬 |
| 物品-掉落 (C7) | ← | 软 — 配方依赖掉落生态 |
| 分解系统 (F10) | ← | 软 |
| 稀有度 (F3) | ← | 硬 — 稀有度计算 |
| UI (F4) | → | 硬 — 制作面板 |

## Tuning Knobs

| 参数 | 范围 | 说明 |
|------|------|------|
| 每配方材料数 | 1-5 | MVP=2-3 |
| 制作金币费用 | 0-10000 | 按产物价值等比 |
| minResultRarity提升 | 0-2级 | 全传说材料→至少稀有产出 |

## Acceptance Criteria

- **GIVEN** 背包有3个铁矿石和500金币，**WHEN** 制作铁剑，**THEN** 材料扣除+铁剑入背包
- **GIVEN** 缺1个材料，**WHEN** 查看配方，**THEN** 配方灰色不可点击
- **GIVEN** 使用传说级材料，**WHEN** 制作，**THEN** 产物最低稀有度≥稀有

## Open Questions

1. 是否需要"制作熟练度"系统(制作越多越容易出高品质)? → 暂不做

---

# 分解系统

## Overview

分解系统是制作的逆向操作——将不需要的装备拆解为基础材料。每件装备根据其ItemType和稀有度产出对应的材料数量和品质。分解是制作系统的主要材料来源之一。

## Player Fantasy

暗黑3 铁砧——把不需要的传说装备拆了，"至少我拿到了魂"。分解让垃圾装备有价值：一件重复的蓝色头盔不再是商店的1金币——它可能是3个铁矿石，明天用来做更好的装备。

## Detailed Design

**分解规则**:
```
DismantleItem(item):
  materialTable = GetMaterialTable(item.itemData.itemType)
  baseCount = materialTable.GetBaseCount()
  rarityBonus = RarityCalculator.GetBaseMultiplier(item.actualRarity)  // 1.0-3.0
  totalCount = Mathf.RoundToInt(baseCount × rarityBonus)
  
  foreach materialType in materialTable.materials:
    count = totalCount → 按权重分配
    inventory.AddItem(new Inventory_Item(materialType), count)
  
  inventory.RemoveItem(item)
```

**材料产出表** (每种ItemType对应不同材料):
| 装备类型 | 主要材料 | 稀有度加成产出 |
|----------|----------|----------------|
| 武器 | 金属碎片 | +精华 |
| 头盔/盔甲 | 金属碎片+布料 | +保护精华 |
| 靴子/手套 | 皮革+布料 | +敏捷精华 |
| 饰品 | 宝石碎片+精华 | +元素精华 |

## Formulas

```
分解产出数量 = baseCount × rarityMultiplier
  baseCount: 按ItemType基础(3-8)
  rarityMultiplier: 普通=1.0, 精良=1.5, 稀有=2.0, 史诗=2.5, 传说=3.0

分解金币费用 = 物品商店售价 × 0.25 (远低于直接出售)
```

## Edge Cases

- **分解唯一装备**: 确认UI弹窗"确定要分解[装备名]?"
- **背包满无法接受分解材料**: 分解前检查背包容量
- **已装备物品被分解**: 先自动卸下再分解

---

# 合成系统

## Overview

合成是高级物品升级方式——3件同底材同稀有度装备合成1件更高稀有度的同底材装备（保留最好的词缀）。词缀转移——将A装备的一个词缀转移到B装备——作为高级合成选项。

## Player Fantasy

暗黑2 赫拉迪克方块最经典的"3颗同宝石→1颗更高宝石"——同样的心理模型：3件垃圾传说→1件更好的传说。合成是制作的高级形式，面向已经有重复装备的中后期玩家。

## Detailed Design

**合成规则 (3合1升级)**:
```
Combine(items[3]):
  验证: 同itemData、同稀有度、同词缀等级
  result = new Inventory_Item(itemData, rarity+1)
  resultRarity = min(currentMaxRarity + 1, 传说)
  保留词缀: 从3件中选取最好的词缀组合
  // 3个稀有→1个史诗, 3个史诗→1个传说
```

**词缀转移** (Target阶段高级功能):
```
TransferAffix(source, target, affixSlot):
  消耗: 特殊材料 "转移精华" + 金币
  source.RemoveAffix(affixSlot) → target.AddAffix(affixSlot)
  每件装备最多转移1次
```

## Formulas

```
升级产物稀有度 = min(Min(inputRarities) + 1, 传说)
// 3个稀有 → 1个史诗; 3个传说 → 传说(不升级)
```

## Edge Cases

- **3件装备有不同词缀**: 保留所有不冲突的词缀组合
- **词缀转移后源装备词缀槽清空**: 该槽位永久锁定

## Dependencies (三合一)

| 系统 | 方向 |
|------|------|
| 物品-背包 (C5) | ↔ |
| 装备词缀 (F5) | ← 合成保留/转移词缀 |
| 稀有度 (F3) | ← |
| UI (F4) | → |
