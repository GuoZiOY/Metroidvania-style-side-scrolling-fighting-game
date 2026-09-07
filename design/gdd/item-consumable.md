---
status: reverse-documented
source: Assets/Scripts/Others/ItemSystem/ConsumableSystem.cs, Data/ConsumableDataSo.cs
date: 2026-07-28
verified-by: oy
---

# 消耗品系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 数据结构

```
ConsumableDataSo : ItemDataSo
  consumableType: ConsumableType    // 恢复 / 增益 / 特殊
  effectType: ConsumableEffectType  // 恢复生命|恢复百分比|属性增益|复活
  effectValue: float                // 效果数值
  effectDuration: float             // Buff持续时间 (秒, 仅属性增益)
  buffStatType: StatType            // Buff目标属性 (仅属性增益)
  cooldownTime: float               // 使用后冷却 (秒)
```

---

## 2. 使用流程

```
右键点击消耗品槽位
  → UI_ItemSlot.UseConsumable()
    → PlayerInventorySystem.TryUseConsumable(item)
      → ConsumableSystem.TryUseConsumable(item)

ConsumableSystem.TryUseConsumable(item):
  │
  ├── 1. 验证:
  │     item == null? → false
  │     !IsConsumable? → false
  │     CanUseConsumable(item):
  │        ConsumableData() == null? → false
  │        currentStackSize <= 0? → false
  │
  ├── 2. 冷却检查:
  │     冷却键 = (特殊物品用 itemName, 普通用 consumableType)
  │     若在冷却中 → false
  │
  ├── 3. ApplyConsumableEffect(consumableData):
  │     ┌──────────────────┬─────────────────────────────┐
  │     │ 恢复生命         │ health.IncreaseHP(value)    │
  │     │ 恢复生命百分比   │ IncreaseHP(maxHP×value/100) │
  │     │ 属性增益         │ stat.AddModifier((int)v,id) │
  │     │                  │ + coroutine → delay后移除    │
  │     │ 复活             │ SetCurrentHP(maxHP×0.5)     │
  │     └──────────────────┴─────────────────────────────┘
  │
  ├── 4. 消耗:
  │     currentStackSize--
  │     若耗尽: inventory.RemoveItem(item)
  │     否则: inventory.TriggerInventoryUpdate()
  │
  ├── 5. 冷却: consumableCooldowns[key] = cooldownTime
  │
  └── 6. OnConsumableUsed 事件
```

---

## 3. 冷却系统

```
consumableCooldowns: Dictionary<object, float>
  键:
    特殊消耗品 → item.itemName (string)
    普通消耗品 → consumableType (enum)

冷却更新 (Update):
  UpdateConsumableCooldowns():
    foreach key in cooldowns.Keys:
      cooldowns[key] -= Time.deltaTime
      if cooldowns[key] <= 0: 标记删除
    删除所有过期 key

查询:
  GetConsumableCooldown(item) → float (剩余秒数, 0 = 可用)
```

---

## 4. 属性增益 Buff

```
ApplyConsumableEffect → 属性增益:
  │
  ├── stat = playerStats.GetStatByType(buffStatType)
  ├── buffID = "ConsumableBuff_" + Guid.NewGuid()  // 唯一 source key
  ├── stat.AddModifier((int)effectValue, buffID)
  │     ⚠️ Bug: float → int 截断
  │
  └── StartCoroutine(RemoveBuffAfterDelay):
        yield WaitForSeconds(effectDuration)
        stat.RemoveModifier(buffID)
```

---

## 5. 依赖获取

```
ConsumableSystem:
  inventory = GetComponentInParent<Inventory_Base>()  // 背包引用
  playerStats = GetComponentInParent<Entity_Stats>()
  entityHealth = GetComponentInParent<Entity_Health>()
  ⚠️ 全部依赖 GetComponentInParent 正确的层级结构
  ⚠️ 如果父级有多个 Inventory_Base (如商店), 会取到错误的引用
```

---

## 6. 事件

```
OnConsumableUsed (Action<Inventory_Item>)
  触发: TryUseConsumable 成功后
  监听: UI刷新 / 日志 / 统计

间接事件:
  inventory.OnInventoryUpdated (通过 TriggerInventoryUpdate)
  → UI_Inventory.UpdateInventoryUI()
```

---

## 7. 发现的问题

| 问题 | 影响 |
|------|------|
| `stat.AddModifier((int)value)` float→int 截断 | 所有消耗品 Buff 值被取整 |
| `GetComponentInParent<Inventory_Base>()` 层级假设 | 多 Inventory 的场景下取错引用 |

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
