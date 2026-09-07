---
status: reverse-documented
source: Assets/Scripts/Others/ItemSystem/Inventory_Base.cs, Inventory_Player.cs
date: 2026-07-28
verified-by: oy
---

# 背包系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 存储结构

```
Inventory_Base: MonoBehaviour
  maxInventorySize: int = 10
  itemDictionary: Dictionary<int, Inventory_Item>
    // key = slotIndex (0-9)
    // value = Inventory_Item
    // 空槽 = 字典中不存在该 key

Inventory_Player : Inventory_Base
  附加: FindItemByItemId(string) — 存档恢复用
        TryRemoveItem(item) — 安全检查
```

---

## 2. 核心操作工作流

### 2.1 AddItem

```
AddItem(item, [slotIndex]):
  │
  ├── 堆叠优先:
  │     if itemData.canStackable:
  │       existingItem = FindItem(itemData)  // O(n) 线性扫描
  │       if existingItem && existingItem.CanAddStack():
  │         existingItem.currentStackSize += item.currentStackSize
  │         NotifyUpdate → return
  │
  ├── 放入槽位:
  │     if slotIndex specified:
  │       if !itemDictionary.ContainsKey(slotIndex):
  │         itemDictionary[slotIndex] = item → NotifyUpdate
  │       else: return false
  │     else:
  │       firstEmpty = GetFirstAvailableSlot()  // O(n) 扫描 0→9
  │       itemDictionary[firstEmpty] = item → NotifyUpdate
  │
  └── 隐含约束:
        CanAddItem() = Count < maxInventorySize (不计堆叠)

  ⚠ 注意: 堆叠路径默认忽略 CanAddItem() 的槽位检查，
           因为堆叠不占用新槽位。这是正确的行为。
```

### 2.2 RemoveItem

```
RemoveItem(item):
  ├── GetItemSlot(item)  // O(n) 线性扫描值匹配
  │     foreach kvp in itemDictionary:
  │       if kvp.Value == item: return kvp.Key
  └── RemoveItemAtSlot(slotIndex)
        └── itemDictionary.Remove(slotIndex) → NotifyUpdate

RemoveItemAtSlot(slotIndex):
  └── itemDictionary.Remove(slotIndex) → NotifyUpdate
```

### 2.3 MoveItem / SwapItems

```
MoveItem(fromSlot, toSlot):
  ├── fromSlot 为空? → 失败
  ├── toSlot 已占用? → 失败 (使用 SwapItems)
  └── itemDictionary[toSlot] = item → Remove fromSlot → NotifyUpdate

SwapItems(slotA, slotB):
  ├── 两个都有物品: 交换引用
  ├── 仅 A 有: A移到B, B位置清空
  ├── 仅 B 有: B移到A, A位置清空
  └── 两个都空: 无操作
```

---

## 3. 堆叠规则

```
Inventory_Item (纯数据类)
  currentStackSize: int (默认1, 最大99)
  CanAddStack(): itemData.canStackable && currentStackSize < 99

堆叠条件:
  1. itemData.canStackable == true
  2. 已存在相同 itemData 引用的物品
  3. 已有物品的 currentStackSize < 99

AddItem 的堆叠行为:
  新物品堆叠数 ≤ (99 - 已有堆叠数): 全部合并
  新物品堆叠数 > 剩余空间: 合并到99, 超出部分丢失? 
  → 当前实现: currentStackSize += item.currentStackSize (不做cap，可能超99)
  ⚠ 需要验证: 如果两个49堆叠物品合并, 会得到 98, 正常
               如果两个99合并, 会得到 198, 超出上限
```

---

## 4. 查询操作（全部 O(n)）

```
FindItem(ItemDataSo)        → Inventory_Item?   // 线性扫描引用来匹配
FindItemByItemId(string)    → Inventory_Item?   // 线性扫描 itemData.itemId
GetItemSlot(Inventory_Item) → int               // 线性扫描值来匹配
GetFirstAvailableSlot()     → int               // 线性扫描 0→9 找空key
CanAddToStack(item)         → bool              // FindItem + CanAddStack
GetOccupiedSlots()          → List<int>         // 遍历字典key
Count { get }               → itemDictionary.Count
```

性能影响: 10 个槽位的背包 O(n) 可忽略不计。

---

## 5. 事件系统

```
OnInventoryUpdated (Action)
  触发时机:
    - AddItem 成功后
    - RemoveItem/RemoveItemAtSlot 成功后
    - MoveItem/SwapItems 成功后
    - TriggerInventoryUpdate() 手动触发 (消耗品使用等)

  监听者:
    - UI_Inventory: 刷新所有槽位图标
    - SaveManager: 标记需要存档
```

---

## 6. PlayerInventorySystem 门面

```
PlayerInventorySystem: MonoBehaviour (协调层)

持有引用: Inventory_Player, EquipmentSystem, ConsumableSystem

门面方法:
  TryEquipItem(item) → 背包移除 → 装备系统 → 旧装备返回背包
  TryUnequipItem(item) → 容量检查 → 装备卸下 → 背包添加
  TryEquipToSlot(item, slotIdx) → 装备到指定槽位
  TryUnequipItemToSlot(item, targetIdx) → 卸到指定背包槽
  TrySwapEquipmentSlots(type, slotA, slotB) → 委托 EquipmentSystem
  TryUseConsumable(item) → 委托 ConsumableSystem

货币:
  AddCurrency(int) / SpendCurrency(int) → bool
  GetCurrency() → int (铜币)
  DisplayGold/Silver/Copper → 显示用属性
```

---

## 7. 边界情况

- ✅ 堆叠检查在 slot 检查之前 — 不会产生重复槽位
- ✅ RemoveItem 通过引用匹配而非 ID — 精确删除同一实例
- ⚠️ 堆叠超 99 没有保护 — 可能通过多次 AddItem 突破上限
- ⚠️ AddItem 未指定 slotIndex 时，空槽查找从 0 开始 — 碎片化后前面的空槽优先填充
- ⚠️ MoveItem 和 SwapItems 是分开的操作 — 命名语义不够清晰

## Dependencies (V2 新增)

| 系统 | 方向 | 硬/软 | 接口 |
|------|------|-------|------|
| 制作系统 (F9) | → | 硬 | 提供材料存放/读取；产出物品存入 |
| 分解系统 (F10) | → | 硬 | 分解产物入背包；源物品移除 |
| 合成系统 (F11) | → | 硬 | 材料移除+产物存入 |
| 宝石镶嵌 (F12) | → | 硬 | 宝石作为特殊物品存取 |
| 商店 (C8) | ↔ | 软 | 买卖操作 |
| 掉落 (C7) | ← | 硬 | 物品来源 |

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |

---

*本文档由 `/reverse-document design` 生成*
