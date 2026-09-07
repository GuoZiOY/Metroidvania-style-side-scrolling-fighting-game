---
status: reverse-documented
source: Assets/Scripts/Others/ItemSystem/, Data/
date: 2026-07-28
verified-by: oy
---

# 物品系统 — 总览

> **⚠️ 反向文档**
> 本文档是物品系统的索引入口。各子系统的详细设计见独立文档。

---

## 子系统索引

| 文档 | 内容 | 核心文件 |
|------|------|----------|
| [背包系统](item-backpack.md) | 存储结构、堆叠、槽位操作、容量 | `Inventory_Base.cs`, `Inventory_Player.cs` |
| [装备系统](item-equipment.md) | 装备槽位、Modifier应用/移除、换装 | `EquipmentSystem.cs`, `Inventory_EquipmentSlot.cs` |
| [消耗品系统](item-consumable.md) | 使用流程、效果类型、Buff/Debuff、冷却 | `ConsumableSystem.cs`, `ConsumableDataSo.cs` |
| [掉落系统](item-loot.md) | 掉落表、物品生成、拾取、货币掉落 | `LootTable.cs`, `LootManager.cs`, `LootDropper.cs`, `ItemAbout.cs` |
| [商店系统](item-shop.md) | 买卖逻辑、库存管理、回收折扣、交易UI | `ShopSystem.cs`, `ShopSO.cs`, `UI_ShopPanel.cs` |
| [稀有度系统](item-rarity.md) | 浮动算法、倍率计算、颜色映射 | `RarityCalculator.cs`, `LootRarity.cs` |

## 数据总览

```
ItemDataSo (ScriptableObject)
  ├── EquipmentDataSo ── ItemModifier[] (属性修饰)
  └── ConsumableDataSo ── ConsumableType + EffectType

Inventory_Item (运行时包装, [Serializable])
  ├── 引用 ItemDataSo
  ├── actualRarity (掉落稀有度)
  ├── rarityMultiplier (稀有度差异倍率)
  └── Modifiers (稀有度加成后的实际属性值)
```

## 核心工作流

```
敌人死亡 → LootDropper → LootManager → LootTable.GenerateLoot()
  → RarityCalculator (稀有度浮动)
  → SpawnItems → ItemAbout (拾取检测)
  → Inventory_Base.AddItem() (入背包)
  → PlayerInventorySystem:
       ├── TryEquipItem() → EquipmentSystem → Modifier生效
       └── TryUseConsumable() → ConsumableSystem → 效果应用
  → SaveManager (序列化)
```

## 门面层

`PlayerInventorySystem` 是物品系统的统一入口，协调背包+装备+消耗品+货币四个子系统。

---

*本文档由 `/reverse-document design` 生成*
