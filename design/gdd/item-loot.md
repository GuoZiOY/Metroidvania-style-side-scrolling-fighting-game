---
status: reverse-documented
source: Assets/Scripts/Others/ItemSystem/LootSystem/
date: 2026-07-28
verified-by: oy
---

# 掉落系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 架构概览

```
[敌人死亡/宝箱打破]
        │
    LootDropper.OnDrop()          ← MonoBehaviour, 实现 ILootable
        │
    LootManager.DropLoot(ILootable) ← 单例, DontDestroyOnLoad
        │
    LootTable.GenerateLoot()       ← ScriptableObject 配置
        ├── LootDropItem.GetDroppedRarity(rarityBonus)
        │     └── RarityCalculator.GetVariedRarity()  (见 item-rarity.md)
        └── LootedItem(ItemDataSo, rarity)
        │
    LootManager.SpawnItems()       ← 实例化发射物
        │
    ItemAbout (拾取检测)           ← MonoBehaviour on item prefab
        └── Inventory_Base.AddItem()
```

---

## 2. LootTable 配置

```
LootTable : ScriptableObject
  lootItems: List<LootDropItem>
  minDropCount / maxDropCount: int    // 随机掉落件数范围
  allowDuplicates: bool               // 允许重复掉落同一物品
  extraRarityDropChanceBonus: float (0-200)  // 稀有度向上浮动加成
  currencyAmount: int                 // 同时掉落的铜币范围 [half, full]

LootDropItem ([Serializable])
  itemData: ItemDataSo
  dropChance: float (0-100)          // 加权随机的权重
  minDropCount / maxDropCount: int   // 该物品的掉落数量范围
  useItemRarity: bool
    真 → 使用 itemData.rarity + 浮动
    假 → 使用自定义 rarity (不浮动)
```

### GenerateLoot 算法

```
GenerateLoot():
  items = []

  // 货币掉落
  if currencyAmount > 0:
    amount = Random.Range(currencyAmount/2, currencyAmount+1)
    items.Add(LootedItem(currency: amount))

  // 物品掉落
  dropCount = Random.Range(minDropCount, maxDropCount+1)
  availableItems = lootItems (copy)

  for i in dropCount:
    selected = SelectRandomItem(availableItems)  // 加权随机
    if selected == null: continue

    actualRarity = selected.GetDroppedRarity(extraRarityDropChanceBonus)
    for j in Random.Range(selected.min, selected.max+1):
      items.Add(LootedItem(itemData, actualRarity))

    if !allowDuplicates:
      availableItems.Remove(selected)

  return items

SelectRandomItem(items):
  totalWeight = sum(item.dropChance)
  ⚠️ 若 totalWeight == 0 → 返回最后一个 (边界bug)
  roll = Random.Range(0, totalWeight)
  cumulative = 0
  foreach item:
    cumulative += item.dropChance
    if roll < cumulative: return item
```

---

## 3. LootedItem (中间数据)

```
LootedItem ([Serializable])
  baseItemData: ItemDataSo
  baseRarity: LootRarity         // 物品定义的基础稀有度
  actualRarity: LootRarity       // 浮动后的实际稀有度
  statMultiplier: float          // 稀有度差异倍率
  uniqueId: Guid                 // 运行时唯一标识
  currencyAmount: int            // 货币掉落用

构造:
  LootedItem(itemData, actualRarity):
    baseRarity = itemData.rarity
    statMultiplier = RarityCalculator.GetRarityDifferenceMultiplier(
                       baseRarity, actualRarity)

GetModifiedValue(baseValue):
  return Mathf.Round(baseValue × statMultiplier × 10) / 10
  // 四舍五入到 1 位小数

GetDisplayName():
  return $"[{RarityCalculator.GetName(actualRarity)}] {itemName}"
```

---

## 4. SpawnItems (物理生成)

```
LootManager.SpawnItems(items, position):
  foreach item in items:
    if item.currencyAmount > 0:
      → 拆分为金/银/铜面额
      → 逐个生成 coin prefab
      → 交替左右 + 随机上抛速度
    else:
      → Instantiate(itemPrefab, position)
      → ItemAbout.InitializeLootedItem(lootedItem)
          └── new Inventory_Item(lootedItem)  // 应用稀有度倍率
      → 交替左右散落 + 抛物线上抛
```

---

## 5. ItemAbout (拾取)

```
ItemAbout : MonoBehaviour (item prefab 上)

生命周期:
  Start() → 落地检测:
    CheckLanding(): 向下射线 (Ground layer)
    hasLanded = false → 等待
    hasLanded = true → 允许拾取

  OnTriggerEnter2D("Player"):
    if !hasLanded: return  // 落地前不可拾取

    inventory = player.GetComponentInChildren<Inventory_Base>()

    if !inventory.CanAddItem() && !CanAddToStack:
      return  // 背包满

    inventory.AddItem(itemToAdd)
    QuestEvents.ReportItemCollected(itemId, 1)
    PickupFX.AnimatePickup()  // DOTween 飞行动画
```

---

## 6. 掉落触发源

```
敌人死亡:
  Entity_Health.Die() → entity.EntityDead()
    → Enemy.EntityDead() → Enemy_DeadState.Enter()
      → enemy.lootDropper.OnDrop()

宝箱:
  Object_Chest (实现 IDamgable):
    TakeDamage → lootDropper.OnDrop()

ILootable 接口:
  LootTable[] LootTables { get; }
  Vector3 DropPosition { get; }
  void OnDrop()
```

---

## 7. 边界情况

- ✅ 单次掉落保护: `hasDropped` 防止重复触发
- ⚠️ LootTable 所有权重为 0 → 返回列表中最后一个物品
- ⚠️ 货币 coin 实例化计入全局计数器 (顺序编号) — 多个敌人同时掉落时分发索引共享
- ✅ ItemAbout 落地检测 — 防止出生即拾取

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
