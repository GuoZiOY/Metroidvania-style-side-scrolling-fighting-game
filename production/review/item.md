# 物品系统审查报告

> 审查日期：2026-08-14 | 代码基线：git HEAD `7c7b82c`
> 审查方式：定向深读（RarityCalculator / EquipmentSystem / ConsumableSystem）+ 24 文件目录全量核查 + 存档链路联动

## 1. 框架结构

### 1.1 核心类与模块划分

```
背包 C5: Inventory_Base (Dictionary<string, Inventory_Item> + 堆叠 99 + Add/Remove/Move/Swap)
         ├── Inventory_Item (数据: itemData/rarity/modifiers/stack)
         ├── Inventory_Player (玩家背包)
         └── PlayerInventorySystem (MonoBehaviour 门面: 拾取/扩容/装备协调)
装备 C6: EquipmentSystem (双字典: slotDictionary + equipmentDictionary + Modifier 应用 source=itemID)
掉落 C7: LootSystem/ 子目录
         ├── LootTable (SO: 掉落池配置)
         ├── LootDropItem / LootDropper (掉落物实例化+抛物线)
         ├── RarityCalculator (静态: 5 级几何倍率 1.0/1.5/2.0/2.5/3.0 + 加权波动算法)
         ├── LootedItem / LootManager (单例: 掉落管理)
         └── LootRarity (枚举 5 级)
商店 C8: ShopSystem (纯 C# 与 UI 分离) + ShopSO
消耗品 C9: ConsumableSystem (4 效果: 恢复/增益/复活/百分比 + 冷却字典)
词缀: EquipmentAffixGenerator (装备随机前缀+后缀 → Modifier[])
```

### 1.2 装配方式
- **数据驱动**：`ItemData` SO（Resources/Data/ItemData/）+ `LootTable` SO + CSV 导入（LootTableCSVImporter）
- **纯 C# 与 UI 分离**：ShopSystem 纯逻辑类（不引用 UnityEngine.UI），UI_ShopPanel 负责展示
- **存档**：背包/装备/金币进出 SaveManager（ApplyInventory 清空重建 / ApplyEquipment 按存档重建词缀）

### 1.3 模块划分评价
- ✅ 背包/装备/掉落/商店/消耗品五模块文件边界清晰；掉落管道 `LootTable→LootDropItem→RarityCalculator→LootedItem→ItemAbout` 完整
- ⚠️ `EquipmentSystem.cs` 433 行（双字典+FIFO 驱逐+Modifier 应用）；`PlayerInventorySystem.cs` 含 Update 每帧调试写入
- ⚠️ 装备词缀 V2（随机前缀+后缀）与精英词缀（IEnemyAffix）共用 `EquipmentAffixGenerator`——需确认命名空间不冲突

## 2. 工作流程

### 2.1 装备掉落生成
1. 敌人死亡 → `LootDropper` → `LootTable` 掷表 → `RarityCalculator.GetVariedRarity(base, maxSteps, lootBonus)` 加权波动
2. `EquipmentAffixGenerator` 按稀有度生成前缀+后缀 → `Modifier[]` 写入 `Inventory_Item.Modifiers`
3. 物理抛物线掉落（圆形散布）→ 玩家拾取 → `ItemAbout` → `PlayerInventorySystem.AddItem` + `QuestEvents.ReportItemCollected`

### 2.2 装备穿戴/卸下
1. `EquipmentSystem.EquipItem(item)`：`ValidateEquipmentItem`（类型校验）→ `GetFirstEmptySlotIndex` → 装备 + `AddModifier(source=itemID)`
2. `UnequipItem`：卸下 + `RemoveModifier(source=itemID)`；满槽时 FIFO 驱逐
3. `OnEquipmentUpdated` 事件 → UI 装备面板刷新

### 2.3 购买/出售（ShopSystem）
1. 买入：金币校验 → `AddItem`；出售：`RemoveItem` + 金币入账（buyBackRate=0.5）
2. 出售用选中物品实例（已修复：不再卖 FindItem 第一个同类，见 git cc5ba04）

### 2.4 消耗品使用
1. `ConsumableSystem.ApplyConsumableEffect`：4 效果分支（恢复/增益/复活/百分比）
2. 增益分支 `stat.AddModifier(value, buffID)` 接受 float（已修复 float→int 截断）
3. 冷却字典 `Dictionary<object, float>` 管理

## 3. 信息链路

| 链路 | 方向 | 说明 |
|------|------|------|
| `OnInventoryUpdated` | Inventory_Base → UI | 背包变化刷新 |
| `OnEquipmentUpdated` | EquipmentSystem → UI | 装备变化刷新 |
| `QuestEvents.ReportItemCollected` | ItemAbout → QuestManager | 拾取上报（**仅地面拾取**，制作/分解产物不触发） |
| Modifier 链 | 装备/消耗品/词缀 → Entity_Stats | `AddModifier(source=itemID)` 多源叠加（FIFO 驱逐） |
| 存档 | SaveManager ↔ 背包/装备 | CollectSaveData 收集 → Apply 重建（词缀不入档，由装备系统重建） |

## 4. 与设计文档一致性

| 设计承诺 | 实现状态 |
|----------|----------|
| 装备双字典 + FIFO 驱逐 + Modifier(source=itemID) | ✅ 与 architecture.md C6 一致 |
| RarityCalculator 加权随机 + 差异倍率公式 | ✅ 落地（5 级几何倍率） |
| 掉落管道 LootTable→Rarity→LootedItem | ✅ 一致 |
| 装备词缀 V2（前缀+后缀→Modifier[]） | ✅ 落地（EquipmentAffixGenerator） |
| rarityMultiplier 双重应用风险（W-3） | ⚠️ 需核对——`GetModifiedValue` 与词缀生成两条路径是否都应用倍率 |
| 商店 MVP 无限补货基础材料 | ⚠️ 需核对 ShopSO 补货逻辑 |
| 制作/分解产物计收集 | ❌ 不触发 ReportItemCollected（设计未明确） |

## 5. 代码质量

- ✅ RarityCalculator 权重算法注释详尽（距离权重 100/15/3/1 + 加成递减）；ShopSystem 纯 C# 可测
- ⚠️ 每帧轮询：`PlayerInventorySystem.Update`（L18-21 每帧调试字段写入）
- ⚠️ `FindAnyObjectByType` 热路径：`UI_InventorySlot`/`UI_EquipSlot`/`UI_TrashCan`/`UI_ItemSlot` 每次拖放/使用都查找 `PlayerInventorySystem`
- ⚠️ `WarehouseSystem` 有 Instance 却仍 6 处 `FindAnyObjectByType` 转移入口重复查找
- ⚠️ `EquipmentSystem.cs` 433 行接近超大；Linq 使用（`using System.Linq`）需评估热路径开销

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重
1. **制作/分解产物不触发收集任务**（跨系统链路缺失） — 只有地面拾取上报 `ReportItemCollected`；分解产出材料、制作产物直接入包不推进"收集 X"任务。
   - 改进：在 `CraftingSystem.TryCraft`/`DismantleSystem.TryDismantle` 产物入包处补 `ReportItemCollected`；或在 GDD 明确"仅拾取计收集"。

### 🟡 中等
2. **拖放/使用热路径 FindAnyObjectByType**（BUG-0023 遗留） — `UI_ItemSlot.UseConsumable`/`UI_InventorySlot.OnItemDropped`/`UI_EquipSlot.OnItemDropped`/`UI_TrashCan.OnItemDropped` 每次操作查找 `PlayerInventorySystem`。
   - 改进：Awake/Start 缓存引用或 Service Locator；`WarehouseSystem` 统一走 Instance。
3. **rarityMultiplier 双重应用风险未验证**（交叉审查 W-3） — `GetModifiedValue` 与词缀生成两条路径。
   - 改进：核对 `EquipmentAffixGenerator` 生成值与 `Entity_Stats.GetModifiedValue` 是否都应用稀有度倍率（传说 3.0× 平方成 9× 风险）。
4. **玩家背包扩容被动依赖技能等级**（存档顺序约束） — `ApplySaveData` 技能先于背包（注释明确"背包容量依赖技能等级"），与 architecture.md 文档顺序不一致（quest.md M-8 同报）。
   - 改进：更新 architecture.md 存档顺序章节。

### 🟢 轻微
5. `EquipmentSystem` 433 行拆分（EquipValidator + ModifierApplier）。
6. `PlayerInventorySystem.Update` 每帧调试写入改 `OnValidate` 或移除。
7. 词缀生成/掉落掉落参数（散布/抛物线）硬编码，建议提 SO。
8. 商店补货机制与 GDD 核对（B-2 金币 sink 问题遗留）。

---

## 附：审查结论摘要

- **健康度**：良好。背包/装备/掉落/商店/消耗品五模块结构清晰，RarityCalculator 权重算法注释优秀，纯 C# 商店可测；float 截断等历史 Bug 已修复。
- **最需优先**：制作/分解产物收集链路缺失（任务系统依赖）、拖放热路径 FindAnyObjectByType、rarityMultiplier 双重应用风险验证。
- **最值得肯定**：掉落管道全链路（LootTable→稀有度波动→抛物线掉落→拾取上报）实现完整且与设计一致，词缀生成直接写入 `Modifier[]` 对下游透明。
