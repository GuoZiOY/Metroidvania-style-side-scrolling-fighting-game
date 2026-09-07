# 制作系统系统审查报告

> 审查日期：2026-08-18 | 代码基线：git HEAD `7c7b82c`（2026-08-14）
> 审查范围：`Assets/Scripts/Others/ItemSystem/Crafting/`、`Combine/`、`Dismantle/`、`WarehouseSystem.cs`
> 参考文档：`design/gdd/crafting-system.md`（F9/F10/F11）、`docs/architecture/architecture.md`

---

## 1. 框架结构

### 1.1 核心类与职责划分

| 类 | 位置 | 类型 | 职责 | 行数 |
|---|---|---|---|---|
| `CraftingSystem` | Crafting/CraftingSystem.cs | 静态类 | 制作（F9）：条件检查/材料扣除/产物生成 | 173 |
| `CraftingRecipeDB` + `CraftingRecipe` + `MaterialEntry` | Crafting/CraftingRecipeDB.cs | ScriptableObject + 序列化数据类 | 配方配置（策划 Inspector 配置） | 30 |
| `CombineSystem` + `CombineResult` | Combine/CombineSystem.cs | 静态类 + 结果类 | 合成（F11）：3-9 件同大类同稀有度装备概率升级 | 379 |
| `DismantleSystem` + `DismantleOutput` | Dismantle/DismantleSystem.cs | 静态类 + 结果类 | 分解（F10）：配方反推产出材料 | 156 |
| `DismantleTable` + `DismantleEntry` + `DismantleMaterial` | Dismantle/DismantleTable.cs | ScriptableObject + 序列化类 | 分解产出表（**当前无任何引用，死代码**） | 27 |
| `WarehouseSystem` | WarehouseSystem.cs | MonoBehaviour 单例 | 仓库：存取转移/查询排序/存档序列化 | 385 |

### 1.2 架构模式评估

- **纯逻辑静态类 + 配置 SO 分离**：`CraftingSystem` / `CombineSystem` / `DismantleSystem` 均为无状态静态逻辑类，不持有任何 MonoBehaviour 生命周期，所有数值/配方来自 SO 配置或方法参数——**职责单一、可测试性良好**，符合架构"接口优先/数据透明"原则。
- **单例**：`WarehouseSystem` 采用 `Instance` 静态单例（挂在持久 UI 根下）；`UI_CombinePanel`/`UI_DismantlePanel` 采用惰性静态单例（含 `FindObjectsInactive.Include` 查找）；`UI_CraftPanel` 无单例、由 `UI_BlacksmithPanel` 直接驱动。三种面板单例风格不统一（见 §5）。
- **UI 与业务分离**：`UI_*Panel` 只做展示/输入收集，业务全部委托给静态类或 `WarehouseSystem`，分层清晰（UI_CraftPanel 注释明确"接线"职责，UI_WarehousePanel 声明"纯 UI 层"）。
- **未发现超大类**：所有受审文件 ≤400 行；`WarehouseSystem`（385）与 `UI_WarehousePanel`（400）逼近阈值且分别混合了"转移 + 查询视图 + 存档"、"开合动画 + 槽位搬运 + 网格重建"两类以上职责，SRP 处于临界状态。

---

## 2. 工作流程

### 2.1 制作流程（F9，`CraftingSystem.TryCraft`）

```
1. UI_CraftPanel.Open → OnEnable 缓存 PlayerInventorySystem → RefreshList 实例化配方行
2. 每个配方行 RefreshState → GetCraftFailReason（背包可容纳产物/金币/材料逐项检查）
3. 玩家点击制作 → OnClickCraft → TryCraft：
   a. CanCraft 复核（配方无效/背包未就绪/背包满/金币不足/材料不足）
   b. 扣金币 SpendCurrency
   c. ConsumeMaterials：遍历材料清单，按堆叠逐个 ReduceStack，堆清空则 RemoveItemAtSlot（复制字典防遍历修改）
   d. 材料扣除失败 → AddCurrency 回滚金币（兜底）
   e. CreateProduct：装备产物走 LootedItem(recipe.resultItem, resultRarity) 构造（自动生成词缀）；非装备普通构造
   f. inv.AddItem(product) 入背包（返回值未检查）
4. 成功 → RefreshList + RefreshDetail 刷新 UI
```

- 稀有度计算：`resultRarity = max(配方最低稀有度, 材料数量加权平均稀有度)`（`CalculateResultRarity`）。

### 2.2 分解流程（F10，`DismantleSystem.TryDismantle`）

```
1. UI_DismantlePanel 选中物品 → RefreshDetail → GetDismantleFailReason（无配方/背包容量）
2. CalculateOutput：查 CraftingRecipeDB 反推该装备的制作配方
   → 每材料产出 count = max(1, RoundToInt(配方数量 × 0.5损耗 × 稀有度倍率))
3. 点击分解 → TryDismantle：
   a. 背包容量预检 CanHoldOutput（已有同种可堆叠材料则不占新槽）
   b. 若物品已装备 → TryUnequipItem 先卸下到背包（⚠ 见 §6-中-2：背包满但全为可堆叠材料时静默失败）
   c. 产出材料逐件 AddItem 入背包（每件触发一次事件）
   d. RemoveItem 移除被分解装备
```

### 2.3 合成流程（F11，`CombineSystem.TryCombine`）

```
1. UI_BlacksmithPanel 把背包/装备槽移入右栏，按当前 tab 分发格子点击
2. 合成 tab → UI_CombinePanel.ToggleSelect 多选（3-9 件，HashSet 查重，上限 9）
3. RefreshCombine：CanCombine 校验（同大类[武器/防具/饰品]+同稀有度+数量范围）→ 三通道概率展示
4. 点击合成 → TryCombine：
   a. 校验 + 背包空槽检查
   b. 掷骰：小成功(稀有度+1) / 大成功(换更高一级底材) 独立判定，双中=完全成功
   c. 大成功 → GetEquipmentPool(Resources.LoadAll) 选新底材（排除原底材）
   d. 完全成功 → 30% 概率触发突破（BuildResultAffixes 内替换产物最弱词缀）
   e. BuildResultAffixes：先生成 targetCount 条词缀 → 突破替换最弱 → 素材词缀降序 → 保底 K=max(1,(N-1)/2)+品质分/阶段标准 替换最弱
   f. 移除 N 件输入 → 生成产物入背包
```

### 2.4 仓库存取流程（`WarehouseSystem`）

```
存取：DepositFromBackpack / WithdrawToBackpack / DepositFromEquipment / DepositAllFromBackpack / WithdrawAllToBackpack
  → Transfer(source, target, item)：整堆转移（先并堆、后空槽），容量预检 + 放入失败回滚（源槽恢复）
一键存取：遍历 GetOccupiedSlots → Transfer 逐件转移
```

### 2.5 存档恢复顺序（涉及本系统部分）

```
SaveManager.ApplySaveData：
  玩家/属性 → 技能(被动背包扩容重算容量) → 背包 → 仓库(ApplyWarehouse) → 装备 → 任务 → WorldState → 满血恢复
```

---

## 3. 信息链路

### 3.1 事件/回调链路

| 链路 | 方式 | 方向 |
|---|---|---|
| 背包/仓库内容变更 → UI 刷新 | `Inventory_Base.OnInventoryUpdated`（C# event） | `PlayerInventorySystem` 订阅转发 `OnInventoryUpdated`/`OnEquipmentUpdated`/`OnGoldChanged` |
| 槽位点击 → 分解/合成面板 | `UI_ItemSlot.OnItemSlotClicked`（C# event）→ `UI_BlacksmithPanel.OnSlotClicked` → 按 tab 分发 | 事件 → 直接调用 |
| 合成按钮/制作按钮 | `Button.onClick.AddListener` | 直接绑定 |
| 失败提示 | `AudioManager.Instance?.PlayDenySfx()` + `FindAnyObjectByType<UI_EventTip>()?.ShowDenyTip(reason)` | 直接引用 + 场景查找 |
| 任务收集上报 | `QuestEvents.ReportItemCollected` 仅在 `ItemAbout`（拾取）触发 | **制作/分解产出不经过此链路**（见 §6-轻-5） |

### 3.2 系统依赖图

```
UI_CraftPanel / UI_CombinePanel / UI_DismantlePanel（F4）
   ↓ 静态方法调用
CraftingSystem / CombineSystem / DismantleSystem（纯逻辑，无外部依赖注入）
   ↓ 依赖
PlayerInventorySystem（C5 背包）→ Inventory_Player → Inventory_Base
RarityCalculator / LootRarity（F3）
EquipmentAffixGenerator（F5 词缀）→ GeneratedEquipmentAffix
LootedItem（C7 掉落管道复用）
CraftingRecipeDB（SO 配置，Resources.Load "Data/CraftingRecipeDB"）
EquipmentDataSo 池（CombineSystem，Resources.LoadAll "Data/ItemData"）

WarehouseSystem（单例）→ Inventory_Base（第二容器）
   ← SaveManager.CollectWarehouse / ApplyWarehouse（F2 存档）
   → AudioManager / UI_EventTip / PlayerInventorySystem / EquipmentSystem
```

### 3.3 存档链路完整性

- **写入**：`SaveManager.CollectSaveData → CollectWarehouse → WarehouseSystem.GetSaveData()`（复用 `InventorySlotData`，含 `stackSize/rarity/rarityMultiplier/affixes/slotIndex`）✅ 已覆盖
- **读取**：`SaveManager.ApplySaveData → ApplyWarehouse → WarehouseSystem.LoadFromSave()`（先清空再按槽位重建，`ItemLookup.Find` 解析 itemId）✅ 已覆盖
- **制作/分解/合成均为无状态逻辑**，无独立存档需求 ✅
- 恢复顺序上仓库在背包之后、装备之前，与槽位落格无冲突 ✅
- 遗留风险：`LoadFromSave` 中 `AddItem(item, slotIndex)` 槽位冲突/越界时静默失败（见 §6-轻-1）

### 3.4 架构文档规定的依赖对照

- F9 制作：消耗 C5（材料存取）+ C7（材料掉落）+ F3（稀有度下限）——**实现吻合**（额外依赖 F5 词缀生成）。
- F10 分解：`baseCount × rarityMultiplier`——实现改为配方反推（见 §4）。
- F11 合成：依赖 F5 词缀操作——**实现吻合**（保底继承/突破均调用 `EquipmentAffixGenerator`）；但架构与 GDD 中的 `TransferAffix` 词缀转移**未实现**（全代码库无此符号）。

---

## 4. 与设计文档一致性

| # | 设计文档要求 | 实现状态 | 差异说明 |
|---|---|---|---|
| 1 | GDD：`MaterialEntry { itemId, count }` 按 itemId 匹配 | ❌ 偏离 | 实现改为直接引用 `ItemDataSo material`（引用匹配）。实现更简单且避开了 itemId 查表，但 GDD 结构未同步 |
| 2 | GDD：`resultRarity = max(minResultRarity, avgRarity(materials))`，其中 avgRarity = "加入稀有度最高+最低的两件材料计算平均值" | ⚠️ 部分偏离 | 实现为**全部材料按数量加权平均**，未实现"最高+最低两件"取平均的细节 |
| 3 | GDD Edge Case：背包满时"产物掉落在铁砧旁（不直接入包）" | ❌ 未落地 | 实现改为**制作按钮禁用**（"背包已满"文案）。属合理的 MVP 简化，但设计意图未交付，GDD 需同步 |
| 4 | GDD Edge Case：制作传说级装备需至少 1 个传说级材料 | ❌ 未实现 | 代码无传说级材料门槛校验，仅靠 minResultRarity + 平均值，不保证该行为 |
| 5 | GDD Acceptance：传说级材料 → 产物最低稀有度≥稀有 | ⚠️ 不保证 | 数量加权平均可能被普通材料拉低，且取决于配方 minResultRarity 配置 |
| 6 | GDD 分解：`DismantleTable` 按 ItemType 配置材料表 + 按权重分配 | ❌ 偏离 | 实现改为**配方表反推**（CraftingRecipeDB 反向查询，损耗 50%）；`DismantleTable` SO 成为无人引用的死代码。实现有注释说明是有意为之，但 GDD 与遗留配置均未清理 |
| 7 | GDD 分解：`baseCount 3-8`、按权重分配 | ⚠️ 偏离 | 实现按配方材料数 × 0.5 × 稀有度倍率，无权重分配（DismantleMaterial.weight 字段无人使用） |
| 8 | GDD 分解：分解金币费用 = 商店售价 × 0.25 | ❌ 未实现 | 分解只产出材料，无金币返还 |
| 9 | GDD 合成：确定性 3合1 升级（同 itemData、同稀有度、同词缀等级；`resultRarity = min(Min(inputRarities)+1, 传说)`） | ❌ 大幅偏离 | 实现为 **3-9 件概率系统**：同大类（非同底材）+ 同稀有度即可；成功概率 10%-50%，另有小成功/大成功（换底材）/完全成功/突破四通道；词缀保底继承最强 K 条 |
| 10 | GDD 合成：词缀转移 `TransferAffix`（Target 阶段） | ❌ 未实现 | 全代码库无 `TransferAffix` 符号；架构 F11/F5 亦声明该接口，均未落地 |
| 11 | 架构：F9-F13 为 "Target 阶段系统（架构预留）"，原则 5 "预留接口但不实现，避免 MVP 膨胀" | ❌ 与现状不符 | F9/F10/F11 已完整实现并接入铁匠 UI；架构文档（2026-07-30）已过期，需更新为已实现状态 |
| 12 | 架构：稀有度系统 F3 `RarityCalculator.GetBaseMultiplier` | ✅ 吻合 | 分解倍率（1.0/1.5/2.0/2.5/3.0）复用 F3 |
| 13 | GDD 制作流程：配方灰色=缺材料/亮色=可制作；UI 显示材料需求 | ✅ 吻合 | `UI_CraftRow.RefreshState` + 详情文本实现 |
| 14 | GDD Dependencies：商店可购买基础材料（C8 ↔） | ⚠️ 未验证 | 商店为 MVP 无限补货基础材料（架构 C8 描述），与制作的材料来源闭环是否已接线未在本审查范围内确认 |

**结论**：核心玩法（制作/分解/合成主循环）与 GDD 意图一致，但**合成系统与 GDD 的确定性设计存在大幅偏离**（实现为概率系统，代码注释已详细阐述新设计），**分解表设计与实现路径相反**（反推 vs 正向表），且**架构文档仍标记 F9-F13 为"预留"**——三份文档均需与实现同步，建议先定稿"概率合成"新规则再更新 GDD。

---

## 5. 代码质量

### 5.1 注释规范（项目要求：行内中文注释覆盖字段/方法/关键逻辑）

- ✅ **整体优秀**：`CraftingSystem`/`CombineSystem`/`DismantleSystem` 类头注释说明系统归属（F9/F10/F11）+ 核心规则；方法均有职责注释；关键段落（掷骰、保底替换、突破）有分步行内注释；`CombineSystem` 的概率设计（小/大/完全成功）注释详尽，意图清晰。
- ⚠️ 小瑕疵：`WarehouseSystem.GetRarity`（表达式体方法）无注释；`PlayerInventorySystem.TryEquipItem` 注释 `//从背包装备物品` 缺少空格（风格一致性）；`Inventory_Item` 部分英文注释残留（`//物品数据` 等，非受审范围）。

### 5.2 语句风格（条件后必须换行）

- ✅ 受审文件基本合规；未发现 `if (x) return;` 单行写法。

### 5.3 耦合度

- ✅ 静态逻辑类对背包系统的依赖以**参数注入**（`PlayerInventorySystem invSys`）方式传入，未做全局查找——耦合度低、可测试性好，是本系统最大的优点。
- ⚠️ `WarehouseSystem` 反向：每个转移方法内 `FindAnyObjectByType<PlayerInventorySystem>()` 查找（6 处），依赖隐式场景存在，且与自身 `Instance` 单例并存，访问方式不统一（`UI_WarehousePanel` 也用 `FindAnyObjectByType` 而非 `WarehouseSystem.Instance`）。
- ⚠️ `DismantleSystem`/`CombineSystem` 内部直接 `Resources.Load` 硬编码路径（`"Data/CraftingRecipeDB"`、`"Data/ItemData"`、`"Data/EquipmentAffixDatabase"`），与 UI 层 Inspector 拖入的 `recipeDB` 形成**双路配置来源**，路径字符串散落（架构已标记 Resources→Addressables 为 MEDIUM 风险）。

### 5.4 性能风险

| 位置 | 问题 | 频率 | 严重度 |
|---|---|---|---|
| `CombineSystem.GetEquipmentPool` | 每次大成功掷骰 `Resources.LoadAll<EquipmentDataSo>("Data/ItemData")` 全量加载 + 分配数组/List（大成功触发率 30%-90%） | 每次合成 | 中 |
| `DismantleSystem.TryDismantle` | 产出材料**逐件** `AddItem`（每个 `new Inventory_Item` + 每次触发 `OnInventoryUpdated` 事件） | 每次分解 | 低 |
| `CraftingSystem.ConsumeMaterials` | 每种材料 `new List<KeyValuePair>(dict)` 复制字典 | 每次制作 | 低 |
| `WarehouseSystem` 各方法 | `FindAnyObjectByType` 于 UI 频率调用（非每帧） | UI 交互 | 低 |
| `PlayerInventorySystem.Update` | 每帧 `debugCurrency = currency`（调试字段） | 每帧 | 低（可移除） |
| UI 面板 | 全部事件驱动刷新（`OnEnable`/按钮/事件），**无每帧轮询** | - | ✅ 良好 |

### 5.5 魔法数字 / 硬编码

- `CombineSystem` 集中出现平衡数值：每词缀 +2%（`0.02f`）、大成功每件 +10%、成功率上限 `0.9f`、保底品质分（5/15/30/50）、突破品质分（15/40/80/150）、`BreakthroughChance 0.3f`、`GetBaseSuccessRate` 的 50%/35%/20%/10%。GDD 有 Tuning Knobs 章节，这些常量应抽取为可配置 SO（如 `CombineConfig`），否则调平衡需改代码。
- 资源路径字符串（见 §5.3）散落 3 处。
- `DismantleSystem.LossRatio = 0.5f` 为具名常量 ✅（好示范）。
- `CraftingRecipe.goldCost` 注释标明铜币单位（1金币=10000），与 `PlayerInventorySystem` 货币口径一致 ✅。

### 5.6 健壮性观察

- ✅ `CraftingSystem.TryCraft` 有材料失败回滚金币的兜底；`WarehouseSystem.Transfer` 有放入失败回滚源槽；`DepositFromEquipment` 有卸装失败回滚重新装备。
- ⚠️ `CombineSystem.TryCombine` 移除 N 件输入与入包产物之间无回滚（见 §6-中-1）。
- ⚠️ `CraftingSystem.TryCraft` 忽略 `AddItem` 返回值（预检后理论不可能失败，但缺防御）。
- ✅ 背包容量检查统一使用 `CanAddItem(itemData)`（堆叠感知，修复过"第一堆满 99 找第二堆"的 Bug2）。

---

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重

1. **【文档与实现严重脱节：合成系统设计分叉 + 架构/分解文档过期】**
   - 文件：`Combine/CombineSystem.cs` vs `design/gdd/crafting-system.md`（合成章节）+ `docs/architecture/architecture.md`（F9-F13 标记为"Target 预留"、原则 5 要求不实现）
   - 问题：GDD 定义的是确定性 3合1 升级（同 itemData），实现是 3-9 件概率系统（同大类即可）＋小/大/完全成功/突破四通道；`TransferAffix` 词缀转移（GDD+架构 F11/F5 均声明）完全未实现；分解系统实现路径（配方反推）与 GDD（DismantleTable 正向表）相反且 `DismantleTable` 沦为死代码；架构文档仍声明 F9-F13 不实现。
   - 建议：① 先召开设计决策：以概率合成方案为最终规则并更新 GDD 合成章节（含新公式与 Tuning Knobs），或回归确定性方案；② 删除或标注 `DismantleTable` 死代码，GDD 分解章节改写为"配方反推 + 50% 损耗"；③ 更新 architecture.md：F9/F10/F11 移出"预留"、原则 5 补充例外说明；④ `TransferAffix` 若 Target 阶段仍需，在 GDD 中保留并注明未实现。

### 🟠 中等

1. **【合成执行非原子性：输入移除与产物入包无回滚】**
   - 文件：`Combine/CombineSystem.cs` `TryCombine`（L234-241）
   - 问题：逐件 `inv.RemoveItem(item)` 不检查返回值，随后无条件 `inv.AddItem(product)`。若任一输入引用已失效（多面板并发、物品已被其他路径消耗），会出现"少扣输入却仍得产物"或"输入扣了产物入包失败"的脏状态；`AddItem` 失败时无回滚。
   - 建议：移除输入前复核 `GetItemSlot(item) != -1`，任一失败即中止并回滚已移除项；产物 `AddItem` 失败时回滚全部输入；或先"收集→一次性提交"。

2. **【分解已装备物品 + 背包满但全为可堆叠材料时静默失败】**
   - 文件：`Dismantle/DismantleSystem.cs` `GetDismantleFailReason`/`TryDismantle`（L98-118）+ `PlayerInventorySystem.TryUnequipItem`
   - 问题：`CanHoldOutput` 只统计"材料新开槽数"（已有可堆叠堆则不占槽），但卸下装备需要**空槽**（`CanAddItem()` 仅查 `itemDictionary.Count < max`）。背包满且全是可堆叠材料时：按钮显示"分解"可用 → 点击 `TryUnequipItem` 失败 → `TryDismantle` 静默返回 false，UI 无任何反馈。
   - 建议：`GetDismantleFailReason` 对已装备物品额外检查 `inv.CanAddItem()`（空槽）；或分解路径改为"先移除装备（自动腾出空槽）再卸下"；失败时给出 `NotifyTransferFail` 式提示。

3. **【合成底材池每次 `Resources.LoadAll` 全量加载】**
   - 文件：`Combine/CombineSystem.cs` `GetEquipmentPool`（L158-173）
   - 问题：大成功触发率 30%-90%，每次合成都会全量加载 `Data/ItemData` 并分配数组/List，属可避免的 IO+GC 开销（合成是高频中后期玩法）。
   - 建议：仿照 `DismantleSystem.cachedDB`/`EquipmentAffixGenerator.Database` 做静态缓存（按 ItemType 分组），或预生成"大类×稀有度→底材池"索引缓存；顺带将资源路径收敛为常量/配置。

4. **【制作传说级材料门槛（GDD Edge Case）缺失 + 稀有度公式不保证验收标准】**
   - 文件：`Crafting/CraftingSystem.cs` `CalculateResultRarity`（L41-60）
   - 问题：GDD 要求"制作传说级装备至少 1 个传说级材料"且验收"传说级材料→产物≥稀有"；当前数量加权平均会被低品质材料拉低，且无最低材料品质校验，两个行为均不保证。
   - 建议：若保留 GDD 规则，在 `CanCraft`/`CalculateResultRarity` 增加"最高级材料门槛"；否则与 GDD 同步删除该规则并更新验收标准。

5. **【背包满产物"掉落铁砧旁"设计未落地】**
   - 文件：`UI/UI_Crafting/UI_CraftPanel.cs`（按钮禁用）vs GDD Edge Case
   - 问题：设计意图是产物掉落在铁砧旁（不直接入包），实现选择"背包满禁用制作"。功能上可接受，但玩家在背包将满时无法用制作腾空间，体验与设计不符。
   - 建议：二选一——实现"产物落铁砧旁"（生成 `ItemAbout` 掉落物），或在 GDD 中正式废弃该 Edge Case 并记录决策。

### 🟢 轻微

1. **【仓库读档槽位冲突/越界静默丢弃物品】**
   - 文件：`WarehouseSystem.cs` `LoadFromSave`（L370-382）
   - 问题：`wh.AddItem(item, slot.slotIndex)` 在槽位被占用或 `slotIndex >= maxInventorySize` 时打印警告并丢弃该物品（无堆叠尝试、无落空槽兜底）。存档被外部编辑或容量调整时可能丢物品。
   - 建议：失败时回退 `AddItem(item)`（自动堆叠/首空槽），并记一条警告日志。

2. **【`WarehouseSystem` 单例访问方式不一致 + 转移方法大量场景查找】**
   - 文件：`WarehouseSystem.cs`（L46/95/119/148/201/239）、`UI_WarehousePanel.cs`、`UI_Inventory.cs`、`UI_InventorySlot.cs`
   - 问题：存在 `Instance` 却仍用 `FindAnyObjectByType` 查找；6 处转移入口重复查找 `PlayerInventorySystem`，应缓存。
   - 建议：统一走 `Instance`；`PlayerInventorySystem` 引用在 `Awake`/`Open` 时缓存，`NotifyTransferFail` 的 `UI_EventTip` 改为事件或缓存引用。

3. **【合成词缀数量规则注释与实现不一致】**
   - 文件：`Combine/CombineSystem.cs` `GetMaxAffixCount`（L354）注释"与装备掉落规则一致"
   - 问题：`EquipmentAffixGenerator` 的掉落词缀数含"无词缀概率"（普通60%…传说20%）且稀有/史诗为区间随机；合成侧为固定 1/2/3/4/4（无无词缀概率）。注释"一致"表述不准确（合成固定词缀位是有意的，但注释会误导）。
   - 建议：改写注释说明"合成产物词缀位固定，无掉落的无词缀概率"。

4. **【平衡常量硬编码 + 魔法数字集中】**
   - 文件：`Combine/CombineSystem.cs`（0.02f/0.1f/0.9f/5,15,30,50/15,40,80,150/0.3f）
   - 建议：按 GDD Tuning Knobs 抽 `CombineConfig SO`（或至少具名常量区），支持策划调参不改代码；`BreakthroughChance` 已为具名常量（好示范），其余跟进。

5. **【制作/分解产出不触发 `QuestEvents.ReportItemCollected`】**
   - 文件：`QuestSystem/QuestEvents.cs`、`Others/ItemSystem/ItemAbout.cs`（唯一上报点）
   - 问题：只有地面拾取上报任务"收集 X"进度；分解产出的材料、制作产物直接入背包不会推进收集类任务。若设计上"制作的 X 也算收集"，则链路缺失；若不算，属预期行为，建议在 GDD 明确。
   - 建议：明确规则后二选一——在 `CraftingSystem.TryCraft`/`DismantleSystem.TryDismantle` 产物入包处补 `ReportItemCollected`，或在 GDD 注明仅拾取计收集。

6. **【`recipeId` 字段无人消费 / `PlayerInventorySystem.Update` 每帧写调试字段】**
   - 文件：`Crafting/CraftingRecipeDB.cs`（recipeId）、`PlayerInventorySystem.cs`（L18-21）
   - 建议：`recipeId` 若仅作标识可保留但标注用途（任务/成就引用）；`Update` 调试赋值改为 `OnValidate` 或移除，消除每帧无意义写入。

7. **【`WarehouseSystem.GetRarity` 等少量成员缺注释】**
   - 文件：`WarehouseSystem.cs`（L328-329）
   - 建议：按项目注释规范补 `// 实际稀有度优先，否则用底材稀有度`。

---

## 附：审查范围文件清单

| 文件 | 行数 | 状态 |
|---|---|---|
| Assets/Scripts/Others/ItemSystem/Crafting/CraftingSystem.cs | 173 | 已审查 |
| Assets/Scripts/Others/ItemSystem/Crafting/CraftingRecipeDB.cs | 30 | 已审查 |
| Assets/Scripts/Others/ItemSystem/Combine/CombineSystem.cs | 379 | 已审查 |
| Assets/Scripts/Others/ItemSystem/Dismantle/DismantleSystem.cs | 156 | 已审查 |
| Assets/Scripts/Others/ItemSystem/Dismantle/DismantleTable.cs | 27 | 已审查（死代码） |
| Assets/Scripts/Others/ItemSystem/WarehouseSystem.cs | 385 | 已审查 |
| Assets/Scripts/UI/UI_Crafting/UI_CraftPanel.cs | 228 | 联动审查 |
| Assets/Scripts/UI/UI_Crafting/UI_CombinePanel.cs | 200 | 联动审查 |
| Assets/Scripts/UI/UI_Crafting/UI_DismantlePanel.cs | 113 | 联动审查 |
| Assets/Scripts/UI/UI_Crafting/UI_BlacksmithPanel.cs | 203 | 联动审查 |
| Assets/Scripts/UI/UI_Warehouse/UI_WarehousePanel.cs | 400 | 联动审查 |
| Assets/Scripts/SaveSystem/SaveManager.cs / SaveData.cs | - | 存档链路审查 |
