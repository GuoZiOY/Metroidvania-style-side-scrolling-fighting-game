# 存档系统系统审查报告

> 审查日期：2026-08-18 | 代码基线：git HEAD（`7c7b82c`）
> 审查范围：`Assets/Scripts/SaveSystem/`（SaveManager.cs / SaveData.cs / Checkpoint.cs / ItemLookup.cs）
> 参考文档：`design/gdd/save-system.md`（reverse-documented）、`docs/architecture/architecture.md`（v2.0）

---

## 1. 框架结构

### 1.1 文件清单与角色

| 文件 | 行数 | 角色 | 模式 |
|------|------|------|------|
| `SaveManager.cs` | 734 | 存档总调度（单例，DontDestroyOnLoad） | MonoBehaviour 单例 + 门面（Facade） |
| `SaveData.cs` | 176 | 全部存档数据结构（`[Serializable]`，JsonUtility 直序列化） | 纯数据 DTO |
| `Checkpoint.cs` | 163 | 场景内检查点（触发器 + 提示 UI + 存档触发） | MonoBehaviour 组件 |
| `ItemLookup.cs` | 52 | itemId → ItemDataSo 运行时查找表 | 静态工具类 + 缓存 |

### 1.2 架构决策

- **单例 + 门面**：`SaveManager` 是唯一入口，`CollectSaveData()/ApplySaveData()` 只做**调度**，实际数据读写委托给各业务系统的 `GetSaveData()/LoadFromSave()` 系列方法（QuestManager、WarehouseSystem、EquipmentSystem、PlayerLevelManager、SkillPointManager、WorldState 等各提供专属接口）。这一"系统自报数据、管理器只调度"的契约模式职责划分清晰，符合架构文档"模块所有权"的意图。
- **DTO 与逻辑分离**：`SaveData.cs` 纯数据结构，无任何逻辑，直接对接 `JsonUtility`，规避了 Dictionary 不可序列化问题（全部手动转为 List/数组）。
- **组件职责**：`Checkpoint` 只管"玩家进入→提示→按 F→调 SaveManager"，不碰数据格式；`ItemLookup` 独立缓存避免读档时逐个 Resources 加载。
- **模块划分评价**：整体良好。主要问题是 `SaveManager` 单文件 734 行，混入了三类不属于"调度"的职责（见 §5）：① UI 刷新（`RefreshAllUI` 直接操纵 `UI_SkillTree`/`PassiveSkillManager`）；② 场景切换编排（`pendingLoad` + `sceneLoaded` 订阅）；③ 恢复时序协调（协程轮询系统就绪）。这三者应下沉到独立的"加载编排器/读档时序器"类。

---

## 2. 工作流程

### 2.1 手动存档时序（检查点 / 传送前 / 面板新游戏）

```
1. Checkpoint.OnTriggerEnter2D → playerInRange=true, ShowPrompt
2. Checkpoint.Update 检测 按F + 冷却(2s) → DoSave()
3. Checkpoint.DoSave → SaveManager.CurrentCheckpointId = checkpointId → SaveManager.Save()
4. SaveManager.Save()（CurrentSlotIndex<0 则报错返回）
5. CollectSaveData() 收集（见 2.2）
6. data.saveTime/playTime/lastCheckpointId 填充 → JsonUtility.ToJson(pretty)
7. File.WriteAllText(persistentDataPath/slot_N.json)
8. UpdateProfile(slotIndex, data) → 重写 profiles.json
9. 检查点播放光效/音效/UI 提示（DOTween 浮动）
```

### 2.2 数据收集管线（CollectSaveData）

```
CollectSaveData()
  ├─ FindAnyObjectByType<Player>（不存在 → 整个存档失败返回 null）
  ├─ sceneName = 当前场景; posX/Y/Z = player 位置
  ├─ CollectPlayerData  → health/currency/等级/经验/技能点/属性点（Player 组件逐个 GetComponent）
  ├─ CollectStatData    → 枚举全部 StatType 存 baseValue（不含 Modifier，由装备重建）
  ├─ CollectInventory   → 遍历 Inventory_Player.itemDictionary（含词缀 AffixSaveData）
  ├─ CollectWarehouse   → WarehouseSystem.GetSaveData()
  ├─ CollectEquipment   → EquipmentSystem.GetEquippedItemsForSave()（含词缀）
  ├─ CollectSkillData   → 双源合并（Player_SkillManager.allSkills + SkillDataManager）+ 槽位绑定
  ├─ CollectQuestData   → QuestManager 全部状态（active/readyToClaim/completed/failed/tracked/claimedStageRewards）
  └─ worldFlags         → WorldState.GetSaveData()
```

### 2.3 读档时序（Load）

```
1. UI_SavePanel.OnAction → SaveManager.Load(slot)
2. 读文件 → JsonUtility.FromJson<SaveData> → null 检查
3. 版本检查：version > 当前 → 拒绝加载；version < 当前 → 仅 LogWarning"将自动升级"（无实际迁移！见 §6）
4. 分支：
   ├─ 场景不同：pendingLoad=data；sceneLoaded += OnSceneLoadedForLoad；
   │   SceneTransitionFader.TransitionToScene(存档场景)   ← 黑幕过渡 + LoadSceneAsync(allowSceneActivation=false)
   │   → 场景激活后 OnSceneLoadedForLoad → ApplySaveDataDelayed(协程)
   │     ├─ 轮询 4 个系统就绪（Player/Inventory/SkillDataManager/SkillSlotManager），上限 5s，每帧 4 次 FindAnyObjectByType
   │     ├─ yield 一帧（强制等所有 MonoBehaviour.Start() 完成——技能槽绑定事件订阅时序修复）
   │     ├─ ApplySaveData(data)
   │     ├─ yield 0.1s（等装备/技能生效）
   │     └─ RefreshAllUI()
   └─ 场景相同：ApplySaveData(data) → RefreshAllUI()（无等待，注意时序差异，见 §6）
```

### 2.4 数据恢复顺序（ApplySaveData）——对时序敏感，注释详尽

```
1. 恢复累计游玩时间 + 会话开始时间
2. 玩家位置（覆盖 PlayerSpawner 的入口点定位）
3. ApplyPlayerData → 货币/等级/经验/技能点/属性点（刻意不恢复 HP，见 7）
4. ApplyStatData → 全部属性 baseValue
5. ApplySkillData（★ 先于背包：被动「背包扩容」按技能等级重算背包容量，
   背包按槽位落格依赖该容量，顺序靠后触发"无效槽位索引"）
6. ApplyInventory（清空 → 按 itemId 经 ItemLookup 重建 → 按槽位落格；词缀精确恢复，旧档 null 重随机）
7. ApplyWarehouse（旧档 null → 空仓库，向后兼容）
8. ApplyEquipment（UnequipAll → 按 itemId 重建并装备；词缀精确恢复）
9. ApplyQuestData → QuestManager.LoadFromSave
10. WorldState.LoadFromSave（★ 会先 Clear 再恢复——场景加载期间其他系统 Set 的 flag 会被抹掉）
11. 满血恢复（放在所有属性/装备恢复之后，确保 GetMaxHP 为最终值）
12. player.Revive() + 清状态效果(DoT/控制) + 清输入缓冲（死亡读档防残留）
13. CurrentCheckpointId = 存档值
```

### 2.5 死亡重载（LoadWithReload）

```
UI_DeathScreen.OnContinue → LoadWithReload(CurrentSlotIndex)
→ 读文件/解析/版本检查（★ 缺低版本警告分支，与 Load 不一致）
→ pendingLoad + TransitionToScene(存档所在场景)（跨场景死亡回到存档场景）
→ 走与 Load 相同的 ApplySaveDataDelayed 管线
```

### 2.6 删除 / 列举

```
Delete(slot)：删 slot_N.json + RemoveProfile（profiles.json 重写）
ListProfiles()：读 profiles.json → 主菜单展示（无需解析完整存档）
```

---

## 3. 信息链路

### 3.1 收集/恢复契约矩阵（SaveManager 与各系统）

| 数据域 | 收集调用 | 恢复调用 | 链路完整性 |
|--------|----------|----------|------------|
| 玩家属性 | `CollectStatData` → `Stat.GetBaseValue()` | `ApplyStatData` → `Stat.SetBaseValue()` | ✅ |
| 货币/等级/技能点 | `Player.GetComponent<PlayerInventorySystem/LevelManager/SkillPointManager>` | 对应 `SetCurrency/LoadFromSave` | ✅ |
| 背包 | `Inventory_Player.itemDictionary` 直接遍历 | `ApplyInventory` 清空重建 | ✅（见 3.3 风险） |
| 仓库 | `WarehouseSystem.GetSaveData()` | `WarehouseSystem.LoadFromSave()` | ✅ |
| 装备 | `EquipmentSystem.GetEquippedItemsForSave()` | `UnequipAllForSave` + `TryEquipItemToSlot` | ✅ |
| 技能等级/槽位 | `Player_SkillManager.allSkills` + `SkillDataManager.GetAllSkillLevels()` 双源合并 | `SkillDataManager.UpdateSkillData` + `SkillSlotManager.BindSkillToSlot` | ✅ |
| 任务 | `QuestManager` 9 个 ForSave 接口 | `QuestManager.LoadFromSave` | ✅（见 3.4 事件缺失） |
| 世界状态 | `WorldState.GetSaveData()` | `WorldState.LoadFromSave()` | ✅ |
| **传送门激活** | ❌ 无 | ❌ 无 | 🔴 架构 V2 要求，未实现 |
| **Boss 击败状态** | ❌ 无（`BossEncounter.defeatedFlag` 仅运行时 bool） | ❌ 无 | 🔴 架构 F7 要求，未实现 |
| 精英词缀 | 设计上不持久化（重生重 Roll） | — | ✅ 符合架构 |

### 3.2 事件/数据流方式

- **直接调用为主**：SaveManager 与各系统之间全部是直接方法调用（契约式），无事件、无静态总线参与存档流程。符合架构"已有模式优先"。
- **间接事件链（读档生效的关键路径）**：
  ```
  ApplySkillData → SkillDataManager.UpdateSkillData
    → OnSkillDataUpdated → SkillSlotManager.ActivateUpgradeType → Skill_Base.SetSkillLevelData
    → OnPassiveSkillUpdated → PlayerInventorySystem.RecalculateCapacity（背包扩容）
  ```
  这是一条"读档→技能→被动→背包容量→背包落格"的依赖链，`CollectSkillData` 恢复顺序特意排在背包前，注释清楚说明这是踩坑后的修复（原顺序触发"无效的槽位索引"）。
- **QuestEvents 静态总线**：存档系统**不订阅** `QuestEvents`（OnEnemyKilled/OnItemCollected/OnNpcTalked）；该总线只被 QuestManager 消费，存档与它无耦合——正确（任务进度在存档时快照，无需事件）。
- **GetComponent/FindAnyObjectByType**：SaveManager 对 Player 子组件用 `GetComponent`；对系统级单例用 `X.Instance ?? FindAnyObjectByType<X>()` 双保险（兼容单例未 Awake 的时序）。这是本项目既有模式，但读档轮询中每帧 4 次 `FindAnyObjectByType`（见 §5 性能）。

### 3.3 读档链路完整性风险点

1. **背包/装备槽位越界**：`ApplyInventory` 直接 `inv.AddItem(item, slot.slotIndex)`、`ApplyEquipment` 直接 `TryEquipItemToSlot(item, e.slotIndex)`，若存档槽位 ≥ 当前 `maxInventorySize`（如未来背包扩容被动数值变化、存档手工篡改），AddItem 失败可能静默丢物品（仅有 Debug.Log 输出，无回滚/重分配逻辑）。
2. **物品 ID 缺失**：`ItemLookup.Find` 找不到 → `Debug.LogWarning` + `continue`，物品从背包/装备中**静默消失**，无任何兜底（GDD §8 已记录此边界）。
3. **WorldState 覆盖时序**：`WorldState.LoadFromSave` 先 `Clear()` 再恢复；若场景加载期间（读档协程等待窗口内）其他系统已 `Set` 了新 flag（如任务自动接取、区域初始化），这些 flag 会被存档快照整体覆盖。当前场景无明显触发者，但属于潜在竞态。
4. **同场景读档不刷新任务 UI**：`QuestManager.LoadFromSave` 不触发 `OnQuestAccepted/OnObjectiveUpdated/OnQuestClaimed`，而 `RefreshAllUI` 只刷技能树/被动/HP，**不刷任务 UI**。跨场景读档时 UI 面板随场景重建（OnEnable 里 Refresh）无碍；同场景读档（如游戏内读档入口）任务追踪/面板会显示旧状态。

### 3.4 数据冗余

- `QuestSaveEntry` 同时保存 `objectiveProgress`（int[]）与 `objectives`（List\<QuestObjectiveData\>）两份进度（SaveManager.cs L450-462 两者都写），但 `QuestManager.LoadFromSave` **只读 `objectiveProgress`**（L686-688）——`objectives` 是写而不读的死字段，存档体积翻倍且存在两份不一致风险。
- `PlayerSaveData.currentHP` 被收集（L282）但恢复时从不使用（读档统一满血，注释明确）——死字段。

---

## 4. 与设计文档一致性

### 4.1 与 GDD（save-system.md，reverse-documented）的差异

| GDD 描述 | 实现现状 | 判定 |
|----------|----------|------|
| §3.1/§4 存档/读档流程 | 与实现一致（GDD 本就是反向文档） | ✅ |
| §4.3 恢复顺序 5→6→7 为 Inventory→Equipment→Skills | 实现为 **Skills(5) → Inventory(6) → Warehouse → Equipment**（技能前置是容量修复） | ⚠️ GDD 未同步该时序修复 |
| §6.2 "Modifier 不存档—由装备系统重建" | 实现已升级为**词缀精确存档**（`AffixSaveData`/`ModifierSaveData`，旧档 null 才重随机），SaveData.cs 注释称"V2 后生效" | ⚠️ GDD 过时，未反映词缀 V2 |
| §8 "版本号硬编码 = 1，无迁移策略" | 仍成立（version 检查只有警告无迁移） | ✅ 问题仍然存在 |
| §8 "ApplySaveDataDelayed 超时 5s 后静默失败" | 仍成立（超时后照样 Apply，各方法内部 null 防御"优雅降级"，但无任何报错提示） | ✅ 问题仍然存在 |
| §2 `slotBindings: int[5]`、`-1=空` | 实现为动态槽位数（`GetSlotCount()`，注释提到曾出现 6 槽场景），空槽存 `SkillUpgradeType.None`(=0) 而非 -1 | ⚠️ GDD 与 SaveData.cs 注释(L107)均过时 |

### 4.2 与 architecture.md 的差异

| 架构要求 | 实现现状 | 判定 |
|----------|----------|------|
| F2 Consumes「全部系统」 | 基本兑现（Player/Inventory/Equipment/Skills/Quests/WorldState/Warehouse） | ✅ |
| Save/Load Path「[新增] WorldState→PortalManager(activatedPortals)」 | **无 PortalManager 类**（仅 `Portal.cs` 运行时组件，无激活状态持久化），存档数据结构中无任何 portal 字段 | 🔴 未落地 |
| F7「F2 (击败状态持久化)」 | `BossEncounter.defeatedFlag` 是局部 bool，不写 WorldState/存档；读档回 Boss 房会重新开战、出口传送门激活状态丢失 | 🔴 未落地 |
| F2「EliteAffix 不持久化(重生重 Roll)」 | 符合（敌人词缀不存，物品词缀才存） | ✅ |
| F2 V2「WorldState flags 持久化」 | 已实现（worldFlags 字段） | ✅ |
| F2 V2「claimedStageRewards 持久化」 | 已实现 | ✅ |
| F2 Consumes 引擎风险「Resources.Load 技能数据 (MEDIUM, 应迁 Addressables)」 | `ApplySkillData` 每次读档 `Resources.LoadAll<Skill_DataSo>("Data/StillData")`——硬编码路径 + 每次读档全量加载 | ⚠️ 未解决，且路径疑似拼写错误（"StillData"） |
| F2 Exposes 接口名 | `Save(slot)/Load(slot)/LoadWithReload(slot)/CollectSaveData/ApplySaveData` 全对齐；`GetProfiles()` 实现为 `ListProfiles()` | ⚠️ 接口名微差（`GetProfiles` vs `ListProfiles`） |
| 初始化顺序「WorldState→PortalManager→WorldConditions 重新评估」 | WorldState 恢复有，PortalManager/WorldConditions 不存在 | 🔴 部分未落地 |

---

## 5. 代码质量

### 5.1 注释规范（对照 CLAUDE.md）

- ❌ **违反"不使用 `/// <summary>`"**：`SaveManager.cs` L35（`/// <summary>当前总游玩时间`）、`Checkpoint.cs` L4-7（类头三行 `/// <summary>`）。项目要求一律 `//` 行内注释。
- ✅ 其余注释质量**优秀**：关键时序修复均有中文行内注释说明"为什么"（如 L146-149 Start 订阅时序、L499-501 背包容量顺序、L519-521 满血时机、L523-531 死亡残留清理），是全文最大亮点。

### 5.2 语句风格（对照 CLAUDE.md）

- ❌ L27 `if (Instance != null && Instance != this) { Destroy(gameObject); return; }` ——条件后未换行、花括号同行。
- ❌ L140 `if (player != null && invSys != null && sdm != null && ssm != null) break;` ——单行 if 未换行。
- 其余大体符合（多行分支均换行+花括号）。

### 5.3 性能风险

| 位置 | 风险 | 频率 |
|------|------|------|
| `ApplySaveDataDelayed` L136-143 | 每帧 4 次 `FindAnyObjectByType` 场景全扫，最长 5s（≈300 帧 × 4 次） | 仅读档时，可接受但浪费 |
| `CollectStatData` L311 | `Enum.GetValues(typeof(StatType))` 每次存档分配数组 | 低频（仅存档时） |
| `CollectSkillData` | HashSet/Dictionary 每次存档新建 | 低频 |
| `ApplySkillData` L643 | `Resources.LoadAll<Skill_DataSo>("Data/StillData")` 每次读档全量加载全部技能数据（未缓存，与 ItemLookup 的缓存做法不一致） | 每次读档 |
| `ItemLookup` | 启动一次性 `LoadAll` + 字典缓存 | ✅ 无问题 |
| 无每帧轮询（除读档窗口） | `Checkpoint.Update` 仅少量分支判断 | ✅ 无问题 |

### 5.4 超大类与耦合

- `SaveManager.cs` **734 行 > 400**，且职责杂糅：数据调度 + 场景切换编排 + 协程时序 + UI 刷新（`RefreshAllUI` 直接引用 `UI_SkillTree`、`PassiveSkillManager`、`AudioManager`）+ Profile 文件管理。违反单一职责，UI 刷新逻辑放在 FOUNDATION 层管理器里属于层间倒挂（F2 直接依赖 F4 UI）。
- 魔法数字：5s 超时、0.1s 等待、满血默认 100（L282）、2s 冷却、0.3f Gizmos 半径等未提取为常量（仅文件前缀/槽位数/版本号有 const）。
- `Load()` 与 `LoadWithReload()` 存在约 25 行重复（读文件→解析→版本检查→pendingLoad+订阅+过渡），可抽取公共私有方法。

### 5.5 死代码/冗余

- `ItemLookup.Register` 无任何调用方（全仓库 grep 无引用）——"Inspector 手动赋值"模式是死路径。
- `QuestSaveEntry.objectives`（写而不读）、`PlayerSaveData.currentHP`（写而不读）。
- `SaveData.cs` L107 注释"5 个槽位，-1=空"与实际（动态槽数、None=0）不符。
- `LoadWithReload` 缺 `version < SAVE_DATA_VERSION` 警告分支（与 `Load` 不一致）。
- `lastLoadedSkills` 缓存字段在 `d == null` 时不清空——若某存档 skills 为 null，`RefreshAllUI` 会用上一次读档的残留技能数据刷 UI（边界场景）。

---

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重

1. **传送门激活状态不持久化（架构 F2 V2 要求未落地）**
   - 位置：`Assets/Scripts/SaveSystem/SaveManager.cs`（无 portal 收集/恢复）、`Assets/Scripts/Others/Area/Portal.cs`（仅运行时组件）、architecture.md Save/Load Path。
   - 影响：读档后传送门解锁状态全部丢失，玩家需重新跑图解锁；架构文档声明的"激活状态持久化"与实际不符。
   - 建议：补 `PortalManager`（或 WorldState flag 方案），存档增加 `activatedPortals: List<string>`，读档后按列表解锁；同步更新 architecture.md 与 GDD。

2. **Boss 击败状态不持久化（架构 F7 要求未落地）**
   - 位置：`Assets/Scripts/Character/Enemy/Boss/BossEncounter.cs` L23/L90（`defeatedFlag` 仅运行时）、SaveManager.cs（无该数据）。
   - 影响：击杀 Boss 后存档再读档 → Boss 重生、出口传送门重新激活失效，任务链/能力解锁（依赖击败）状态错乱；当前靠"任务完成写 WorldState"间接兜底，但直接击败状态本身丢失。
   - 建议：Boss 击败时 `WorldState.Set("boss_xxx_defeated", true)`，场景初始化/BossEncounter.Start 查询该 flag 跳过开战并激活出口；读档链路已覆盖 WorldState，无需改 SaveManager。

3. **`Load()` 在场景过渡进行中被再次调用 → `pendingLoad` 悬挂/事件泄漏**
   - 位置：`SaveManager.cs` L107-110、L120-128、L222-226。
   - 触发：过渡中（`isTransitioning`）再次 Load/LoadWithReload → `TransitionToScene` 直接 return → `sceneLoaded` 永不触发 → 订阅永久挂起，`pendingLoad` 残留；下一次任意场景加载（如玩家正常传送）会错误地触发旧存档恢复。
   - 建议：进入 Load 时先 `if (pendingLoad != null) { 取消旧订阅; }` 并检测 `SceneTransitionFader` 过渡态；或统一走"场景切换队列"；至少保证 `Load` 幂等（重复调用时覆盖 pendingLoad 而非叠加订阅）。

### 🟡 中等

4. **版本迁移只有警告、无实际迁移逻辑**
   - 位置：`SaveManager.cs` L98-101（`Debug.LogWarning("将自动升级")` 后原样加载）。
   - 影响：日志与行为不符（"自动升级"未发生）；低版本字段缺失时靠默认值静默降级（如 affixes null → 重随机、warehouse null → 空，尚可接受），但无版本迁移框架，未来格式变更无路可走。
   - 建议：定义 `IMigration { int FromVersion; void Migrate(SaveData) }` 链，按版本逐级升级后再 Apply；或至少将警告文案改为"以兼容模式加载"。

5. **存档写入非原子 + IO 无异常处理**
   - 位置：`SaveManager.cs` L66（`File.WriteAllText`）、L84/L208（`File.ReadAllText`）、L707（profiles.json 写入）。
   - 影响：写一半崩溃/断电 → slot 文件损坏，读档解析失败永久丢档；磁盘满/权限异常直接抛异常打断游戏流程。
   - 建议：写入改为"临时文件 + `File.Replace` 原子替换"；读写包 try/catch，失败时保留旧档并弹提示；`profiles.json` 损坏时降级重建。

6. **`QuestSaveEntry` 双份进度数据（写而不读）**
   - 位置：`SaveManager.cs` L450-462、`SaveData.cs` L143-145、`QuestManager.cs` L686-688。
   - 影响：存档体积约翻倍；两字段无一致性保证（未来若改读 `objectives` 又忘写，数据错乱）。
   - 建议：删除 `objectives`/`QuestObjectiveData`，仅保留 `objectiveProgress`（加载端唯一消费方）。

7. **同场景读档不刷新任务 UI**
   - 位置：`SaveManager.cs` `RefreshAllUI`（未刷任务面板）、`QuestManager.cs` `LoadFromSave`（不触发状态事件）、`UI_QuestTracker.cs`/`UI_QuestPanel.cs`（仅事件驱动）。
   - 影响：同场景读档后任务追踪/面板显示旧状态，直到下一次任务事件才刷新。
   - 建议：`LoadFromSave` 末尾广播一次 `OnQuestAccepted`（或新增 `OnQuestsLoaded` 事件）；或在 `RefreshAllUI` 中显式刷新任务 UI。

8. **SaveManager 超大类 + 层间倒挂（F2 依赖 F4 UI）**
   - 位置：`SaveManager.cs`（734 行，含 `RefreshAllUI`、协程、场景编排）。
   - 建议：拆分为 `SaveManager`（纯数据调度）+ `SaveLoadSequencer`（协程时序/系统就绪等待）+ `SaveLoadUIRefresher`（UI 刷新，归 F4 层或经事件通知 UI 自刷新）。

### 🟢 轻微

9. **注释规范违规**：`SaveManager.cs` L35、`Checkpoint.cs` L4-7 使用 `/// <summary>`，改为 `//` 行内注释。

10. **语句风格违规**：`SaveManager.cs` L27、L140 条件后未换行。

11. **`LoadWithReload` 与 `Load` 逻辑不一致**：缺低版本警告分支；建议抽取公共 `ReadAndValidateSave(slot)`。

12. **`Resources.LoadAll<Skill_DataSo>("Data/StillData")`**（SaveManager.cs L643）：硬编码路径疑似拼写错误（"StillData"），且每次读档全量加载未缓存；建议建 `SkillLookup`（仿 `ItemLookup`）缓存，并纳入 Addressables 迁移计划（架构已标记 MEDIUM）。

13. **死代码**：`ItemLookup.Register` 无调用方；`QuestSaveEntry.objectives`、`PlayerSaveData.currentHP` 写而不读。

14. **注释与实现不符**：`SaveData.cs` L107 "5 个槽位，-1=空" → 实际动态槽数、空槽为 `None(0)`；GDD §4.3 恢复顺序未同步技能前置修复。

15. **`lastLoadedSkills` 残留风险**：`ApplySkillData(null)` 时不清缓存，`RefreshAllUI` 可能用旧数据刷 UI；建议 Load 入口处重置。

16. **魔法数字**：5s 超时、0.1s 等待、默认 HP 100、检查点冷却 2s 等建议提为具名常量。

---

### 审查结论

存档系统**核心链路（收集/恢复/Profile/向后兼容）实现扎实**，恢复时序注释与修复经验（背包容量、满血时机、Start 订阅时序）是高质量工程实践的体现；但存在 3 个严重问题集中在**架构承诺未落地**（传送门/Boss 击败持久化）与**读档编排的悬挂风险**，建议优先处理 §6 中第 1-3 项，随后补齐版本迁移与原子写入。
