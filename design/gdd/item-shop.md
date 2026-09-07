---
status: reverse-documented
source: Assets/Scripts/UI/UI_Shop/ShopSystem.cs, Data/ShopSO.cs
date: 2026-07-28
verified-by: oy
---

# 商店系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 架构

```
ShopSO (ScriptableObject)        — 商店配置
ShopSystem (纯 C# 类, 非MB)      — 业务逻辑
UI_ShopPanel (MonoBehaviour)      — UI 层
PlayerInventorySystem             — 玩家背包+货币接口
```

设计: **纯业务逻辑与 UI 分离** — ShopSystem 是无 MonoBehaviour 依赖的纯数据类，UI 通过 `OnDataChanged` 事件刷新。

---

## 2. ShopSO 配置

```
ShopSO : ScriptableObject
  shopId: string
  shopName: string
  items: ShopItem[]
    └── ShopItem { itemData: ItemDataSo, quantity: int }
        quantity: -1 = 无限, 0 = 已售罄, >0 = 有限库存
  buyBackRate: float (0-1, 默认 0.5)
```

---

## 3. 商店状态机

```
ShopSystem:
  状态:
    IsOpen: bool
    IsBuyMode: bool         // 购买模式 vs 出售模式
    SelectedNpcIndex: int   // 当前选中的 NPC 商品 (-1 = 无)
    SelectedItemData: ItemDataSo
    SelectedSellItem: Inventory_Item
    Quantity: int           // 当前选择的数量

  库存:
    npcStocks: int[]        // Open 时从 ShopSO 复制，-1=无限
```

---

## 4. 买卖工作流

### 4.1 打开商店

```
UI_ShopPanel.Open(shopData, npcName):
  │
  ├── Time.timeScale = 0        // 暂停游戏
  ├── DOTween 缩放弹出动画
  ├── shopSystem.Open(shopData, playerInventory, inventory)
  │     ├── 复制库存: npcStocks[i] = shopData.items[i].quantity
  │     └── ClearSelection
  ├── 生成 NPC 商品槽位 (UI_ShopSlot)
  ├── 移动背包/装备面板到商店 (Transform.SetParent)
  ├── 订阅背包槽位的点击事件 (用于出售选择)
  └── RefreshUI

UI_ShopPanel.Close():
  ├── 清理拖拽状态
  ├── Time.timeScale = 1
  ├── 还原背包/装备面板位置
  └── 销毁 NPC 商品槽位
```

### 4.2 购买流程

```
玩家点击 NPC 商品槽位:
  → OnNpcSlotSelected(slot)
    → shopSystem.SelectNpcItem(index)
      ├── IsBuyMode = true
      ├── SelectedItemData = shopData.items[index]
      └── ResetQuantity() → Quantity = 1

数量调整 (±按钮 / Slider):
  → shopSystem.SetQuantity(qty)
    → Quantity = Clamp(qty, 0, GetLegalMaxQuantity)

GetLegalMaxQuantity() — 购买模式:
  maxQty = 99
  maxQty = Min(maxQty, 玩家持有货币 / 物品单价)    // 金钱限制
  maxQty = Min(maxQty, NPC库存)                    // 库存限制
  maxQty = Min(maxQty, 背包容量)                   // 空间限制
    // 可堆叠: 已有堆叠剩余 + 空槽 × 99
    // 不可堆叠: 空槽数量
  return Max(1, (int)maxQty)

点击购买按钮:
  → shopSystem.TryBuy():
    ├── 验证: IsBuyMode, Quantity, stock, 金钱
    ├── playerInventory.SpendCurrency(totalCost)
    ├── for i in qty:
    │     if 背包能放: inventory.AddItem(new Inventory_Item(itemData))
    │     else: break
    ├── 如果少买了: 退款 (已收总额 - 实际花费)
    ├── 扣库存: npcStocks[slotIndex] -= added
    ├── 售罄 → DeselectAll + RemoveNpcSlot
    └── NotifyDataChanged → UI 刷新
```

### 4.3 出售流程

```
玩家点击背包中的物品:
  → OnPlayerSlotClicked(item, slot)
    → shopSystem.SelectSellItem(item)
      ├── IsBuyMode = false
      └── ResetQuantity()

GetLegalMaxQuantity() — 出售模式:
  maxQty = Min(99, 背包中持有的堆叠数)

价格:
  GetUnitPrice() = itemData.value × buyBackRate  // 默认 50% 回收
  GetTotalPrice() = unitPrice × Quantity

点击出售按钮:
  → shopSystem.TrySell():
    ├── 验证: !IsBuyMode, Quantity, 物品在背包
    ├── 扣物品:
    │     stack > qty → stack -= qty
    │     stack == qty → inventory.RemoveItem(item)
    ├── playerInventory.AddCurrency(totalRevenue)
    ├── 物品卖光 → DeselectAll
    └── NotifyDataChanged
```

---

## 5. UI 交互细节

```
UI_ShopPanel:
  左栏 (NPC商品):
    └── UI_ShopSlot × N   (ShopMode.Buying)
        └── 点击 → 选中/取消 (toggle)
            显示: 图标、名称、单价、库存
            售罄 → 移除 + 销毁

  右栏 (玩家物品):
    └── 背包面板 (Transform移入)
    └── 装备面板 (Transform移入, 但不可出售)
        └── UI_ItemSlot 点击 → 选中出售
            同一物品再点 → 取消选中

  数量控件:
    Slider (0 → legalMax)
    ± 按钮
    显示文本

  价格显示:
    金/银/铜 三级 (CurrencyFormatter.Split)
    购买: 物品原价 × 数量
    出售: 物品原价 × buyBackRate × 数量
```

---

## 6. 货币系统

```
统一单位: 铜币
  1 金币 = 10000 铜币
  1 银币 = 100 铜币

PlayerInventorySystem:
  GetCurrency() → int         // 铜币总额
  AddCurrency(int) → void
  SpendCurrency(int) → bool   // 够不够扣

CurrencyFormatter.Split(copperAmount):
  → { gold, silver, copper }
```

---

### MVP 金币循环 (审查后新增)

商店基础材料（铁矿石/皮革/布料）和消耗品（血瓶/解毒剂）设为 `quantity = -1`（无限补货），确保MVP中金币有持续消耗渠道。Target阶段加入制作/合成费用作为额外金币消耗。

---

## 7. 边界情况

- ✅ 购买时背包空间不够 → 少买 + 退款
- ✅ 售罄自动清除选中 + 移除槽位
- ✅ 打开商店时暂停游戏 (Time.timeScale=0)
- ✅ Escape 键关闭商店
- ✅ ModalStack 管理面板层级
- ✅ 面板动画使用 SetUpdate(true) — 在 timescale=0 下仍播放

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
