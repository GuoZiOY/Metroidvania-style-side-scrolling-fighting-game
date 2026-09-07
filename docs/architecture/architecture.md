# 破碎之城 — 主架构文档

## Document Status
- **Version**: 2.1
- **Last Updated**: 2026-08-14
- **Engine**: Unity 6000.4.8f1 (URP)
- **GDDs Covered**: 31/31
- **ADRs Referenced**: 6 (ADR-0001 ~ ADR-0006)
- **Stage**: Production（2026-08-14 由 /gate-check 核验，见 production/stage.txt）

## Engine Knowledge Gap Summary

| 风险 | 领域 | 关键变更 | 触及系统 |
|------|------|----------|----------|
| HIGH | URP 渲染 | Compatibility Mode 移除，Render Graph 强制 | C4/C14/F7 (VFX) |
| HIGH | EntityId | InstanceID(int)→EntityId，不可互转 | Editor 工具 |
| MEDIUM | 资源加载 | Addressables 2.9.1 推荐，当前用 Resources.Load | F2 存档加载 |
| MEDIUM | 2D Physics | 新 NativeArray 重载可用 | C1 攻击检测 |
| LOW | Animator | Evaluate Entry Transitions On Start | C3/C4 状态机 |

---

## System Layer Map

```
┌──────────────────────────────────────────────────────────────┐
│  CONTENT / POLISH 层                                         │
│  C16 地图区域×3  C17 地图扩展  P1 世界条件                    │
├──────────────────────────────────────────────────────────────┤
│  FEATURE 层                                                  │
│  F5 装备词缀  F6 能力门控  F7 Boss系统  F8 中心城镇           │
│  F9 制作  F10 分解  F11 合成  F12 宝石镶嵌  F13 Boss追踪     │
├──────────────────────────────────────────────────────────────┤
│  CORE 层                                                     │
│  C1 战斗  C2 技能  C3 玩家  C4 敌人V2  C5 背包  C6 装备      │
│  C7 掉落  C8 商店  C9 消耗品  C10 刷怪  C11 经验  C12 任务   │
│  C13 音频  C14 精英词缀  C15 传送门                          │
├──────────────────────────────────────────────────────────────┤
│  FOUNDATION 层                                               │
│  F1 输入  F2 存档  F3 稀有度  F4 UI                          │
├──────────────────────────────────────────────────────────────┤
│  PLATFORM 层                                                 │
│  Unity 6000.4.8f1 URP + Addressables 2.9.1 + Input 1.19     │
└──────────────────────────────────────────────────────────────┘
```

---

## Module Ownership — Foundation Layer

### F1: 输入系统
| | |
|---|---|
| **Owns** | `GameInput` 静态类, `Dictionary<Action, KeyCode>` 键位绑定, PlayerPrefs 持久化 |
| **Exposes** | `GetKeyDown(Action)`, `GetKey(Action)`, `GetKeyUp(Action)`, `Horizontal/Vertical` 轴, `IsGameBlocked` |
| **Consumes** | `PlayerPrefs` (bindings), `ModalStack` (IsGameBlocked) |
| **Engine APIs** | `Input.GetKeyDown/GetKey/GetKeyUp` (稳定 API, LOW risk) |
| **V2 变更** | 无 — 已实现，MVP 不需修改 |

### F2: 存档系统
| | |
|---|---|
| **Owns** | `SaveManager` 单例, `SaveData` 顶层结构, 5 槽位文件 + profiles.json, `SaveProfile` 元数据 |
| **Exposes** | `Save(slot)`, `Load(slot)`, `LoadWithReload(slot)`, `CollectSaveData()` → SaveData, `ApplySaveData(SaveData)`, `GetProfiles()` |
| **Consumes** | **全部系统**: Player.health/stats, PlayerInventorySystem, SkillDataManager, SkillSlotManager, QuestManager, EquipmentSystem, WorldState |
| **Engine APIs** | `JsonUtility.ToJson/FromJson`, `File.WriteAllText/ReadAllText`, `Application.persistentDataPath`, `SceneManager.sceneLoaded` (稳定, LOW risk); ⚠️ `Resources.Load` 加载技能数据 (MEDIUM risk — 应迁移 Addressables) |
| **V2 变更** | 新增: WorldState flags 持久化 (修复 quest-system.md BUG), `claimedStageRewards` 持久化, EliteAffix 不持久化(每次重生重新Roll) |

### F3: 稀有度系统
| | |
|---|---|
| **Owns** | `RarityCalculator` 静态类, `LootRarity` 枚举 (5级), 加权随机浮动算法, 差异倍率公式 |
| **Exposes** | `GetVariedRarity(base, maxSteps, bonus)` → LootRarity, `GetRarityDifferenceMultiplier(base, actual)` → float, `GetBaseMultiplier(rarity)` → float |
| **Consumes** | 无 — 纯计算层 |
| **Engine APIs** | `Random.value/Range` (稳定) |
| **V2 变更** | 无 — 已实现，MVP 不需修改 |

### F4: UI 系统
| | |
|---|---|
| **Owns** | `UIManager`, `ModalStack` 静态类, `PanelSwitcher`, 所有面板预置引用, 拖放系统, Tooltip 系统 |
| **Exposes** | `ShowPanel(index)`, `HideAll()`, ModalStack `Push/Pop(id)`, IItemDropTarget/ISkillDropTarget 接口 |
| **Consumes** | 所有数据系统的事件 (OnHealthUpdate, OnInventoryUpdated, OnQuestAccepted, OnSkillPointsChanged 等) |
| **Engine APIs** | `Canvas`, `EventSystem` (RaycastAll), `DOTween` (SetUpdate=true), `LayoutRebuilder` (稳定 API, LOW risk) |
| **V2 变更** | 新增: Boss 血条 UI (F7), 精英词缀图标行 (C14), 制作面板 (F9), 宝石槽位 (F12), Boss 追踪面板 (F13) |

---

## Module Ownership — Core Layer

### C1: 战斗系统
| | |
|---|---|
| **Owns** | 7步伤害管道 (AttackData→闪避→护甲→抗性→伤害→元素→HitStop), `HitStopManager` 两层三段式, `Entity_StatusHandler` 元素状态机, `ElementalEffectData` |
| **Exposes** | `IDamgable.TakeDamage(phys, elem, element, dealer, isCrit)`, `Entity_Stats.GetAttackData(scaleData)` → AttackData, `ApplyStatusEffect(element, effectData)`, `TriggerGlobalHitStop(duration)`, `TriggerLocalHitStop(target, duration)` |
| **Consumes** | `Stat` 系统 (Modifier 链), `DamageScaleData` SO, `ElementType` 枚举 |
| **Engine APIs** | `Physics2D.OverlapCircleAll` (MEDIUM risk — 检查新重载), `Time.timeScale`, `MonoBehaviour.InvokeRepeating` |
| **V2 变更** | 修复 `ApplyBurnEffect` Bug (currentEffect=Ice→Fire); 精英词缀通过 `OnBattleUpdate` 注入; Boss 硬直判定 (单次伤害>8% maxHP) |

### C2: 技能系统
| | |
|---|---|
| **Owns** | 四路并行: `Skill_Base` 组件 (11技能) + `SkillDataManager` (中心缓存) + `SkillSlotManager` (5槽位) + `PassiveSkillManager` (被动); `SkillPointManager`; `SkillObject` 系列 |
| **Exposes** | `TryUseSkill()`, `UpdateSkillData(upgradeType, data, level)`, `BindSkillToSlot/UnbindSkillFromSlot`, `GetSkillByType(type)` → Skill_Base |
| **Consumes** | `GameInput` (Alpha1-5), `Entity_Stats` (属性修改/InputElement), `Player_Combat` (攻击事件), `Skill_DataSo` |
| **Engine APIs** | `Input.GetKeyDown` (via GameInput), `FindAnyObjectByType` (⚠️ 被动技能获取 skillManager) |
| **V2 变更** | 宝石镶嵌 (F12) 嵌入技能槽 → 属性加成; Boss 击败→能力解锁→`UpdateSkillData` |

### C3: 玩家系统
| | |
|---|---|
| **Owns** | 16状态 FSM, 动态重力, Coyote Time (0.1s), 5路输入缓冲, 3连击+Counter循环, 死亡序列 |
| **Exposes** | Player transform/position, `Player_SkillManager.GetSkillByType()`, `Player_Combat` 覆写 |
| **Consumes** | `GameInput` (全部动作), `Entity_Stats`, `PlayerInventorySystem`, DOTween |
| **Engine APIs** | `Rigidbody2D.linearVelocity`, `Animator` (SetFloat/SetBool), DOTween (`DOPunchScale`, `DOShakePosition`) |
| **V2 变更** | 修复 `PlayerState.cs` UnityEditor 引用 (构建报错); DashBlur→无敌冲刺门控; DoubleJump→二段跳门控 |

### C4: 敌人系统 V2
| | |
|---|---|
| **Owns** | 8状态 FSM, `EnemyTypeSystem` 继承链 (Normal/Elite/Boss), `EnemyLevelSystem`, `IEnemyAffix` 接口定义, `BossConfig`/`BossPhase` 数据结构 |
| **Exposes** | `IEnemyAffix` 接口契约, Enemy 生命周期 (InitializeEnemy→BattleState→DeadState), `ActiveAffixes` 列表 |
| **Consumes** | C1 (IDamgable/Entity_Stats/StatusHandler), C10 (EnemySpawner 参数), F3 (稀有度→掉落加成), `QuestEvents` (击杀上报) |
| **Engine APIs** | `MonoBehaviour` 协程, `Physics2D.Raycast` (玩家检测), `Animator` |
| **V2 变更** | 🔴 修复 `InitializeEnemy` 类型系统/等级系统冲突; 新增 `IEnemyAffix.OnApplied/OnRemoved/OnBattleUpdate`; `BossPhaseManager` 阶段切换; PhaseTransition 状态 |

### C5-C9: 物品子系统
| | |
|---|---|
| **C5 背包** | `Inventory_Base.itemDictionary`, AddItem/RemoveItem/MoveItem/SwapItems, 堆叠系统 (max 99) |
| **C6 装备** | 双字典 (slotDictionary + equipmentDictionary), EquipItem/UnequipItem, Modifier 应用 (source=itemID), FIFO 驱逐 |
| **C7 掉落** | `LootTable`→`LootDropItem`→`RarityCalculator`→`LootedItem`→`ItemAbout` 管道, `LootManager` 单例 |
| **C8 商店** | `ShopSystem` 纯C# (与UI分离), `ShopSO` 配置, 买卖两模式 (buyBackRate=0.5), MVP 无限补货基础材料 |
| **C9 消耗品** | `ConsumableSystem`, 4种效果 (恢复/增益/复活/百分比), 冷却系统 (Dictionary<object, float>) |
| **V2 变更** | C6: 装备词缀 V2 (随机前缀+后缀→Modifier[]); C7: 精英/Boss 掉落 bonus; C5: 材料存取供 F9/F10/F11 |

### C10: 区域/刷怪
| | |
|---|---|
| **Owns** | `EnemySpawner` 协程生成, `AreaDifficulty` 参数, `EnemyArea` 触发器, 等级浮动机制 |
| **Exposes** | `SpawnEnemies(count, eliteChance, difficulty, baseLevel)`, `CalculateEnemyLevel(isElite, difficulty, baseLevel)` |
| **Consumes** | C4 (Enemy prefab/InitializeEnemy), F3 (精英判定) |
| **Engine APIs** | `Instantiate`, `Coroutine` (WaitForSeconds) |
| **V2 变更** | 精英生成参数 → `AffixSpawner.OnSpawnElite(enemy, tier)`; Boss 房间独立生成 |

### C11: 经验/等级
| | |
|---|---|
| **Owns** | `LevelCalculator` (3阶段曲线, max 50), `PlayerLevelManager`, `AttributePointManager`, `ExperienceManager` |
| **Exposes** | `AddExp(expEvent)`, `AllocatePoint(StatType)`, `ResetAll()`, `OnLevelUp`/`OnExpGained` 事件 |
| **Consumes** | `EnemyDefeatedTrigger` (敌人死亡→exp), `Entity_Stats` (属性修改), `SkillPointManager` |
| **V2 变更** | 修复 `GetTotalExpRequired` 跳过 Lv0→1; 世界条件 (P1) 监听 `OnLevelUp` |

### C12: 任务系统
| | |
|---|---|
| **Owns** | `QuestManager` 单例, 6状态生命周期, `QuestData` SO, `QuestProgress` 运行时追踪, `WorldState` 静态字典 |
| **Exposes** | `AcceptQuest/ClaimReward`, `OnQuestAccepted/OnObjectiveUpdated/OnQuestClaimed` 事件, `QuestEvents` 静态事件总线 |
| **Consumes** | `NPCBehaviour` (接取/提交), `Enemy.EntityDead` (击杀目标), `ItemAbout` (收集目标) |
| **V2 变更** | 🔴 修复 `TalkToNPC` (添加 ReportNpcTalked 调用); 🔴 修复 `claimedStageRewards` 持久化; 🟡 修复 WorldState 存档; Boss击败→触发任务链 |

### C13: 音频系统
| | |
|---|---|
| **Owns** | `AudioManager` 单例, 分组 SFX (BGM/环境/UI/玩家/战斗/存档) |
| **Exposes** | `PlayHitSfx()`, `PlayCritSfx()`, `PlayJumpSfx()`, `PlayButtonSfx()` 等 |
| **Consumes** | 战斗事件, UI 事件 |
| **V2 变更** | Boss BGM 切换, 精英词缀激活 SFX, 传送门激活音效 |

### C14: 精英词缀系统 (新增 MVP)
| | |
|---|---|
| **Owns** | `AffixDatabase` SO (5类词缀池), `AffixSpawner`, `IEnemyAffix` 实例生命周期 |
| **Exposes** | `IEnemyAffix` 接口 (AffixId/DisplayName/Tier/OnApplied/OnRemoved/OnBattleUpdate/GetTooltipText), `GetLootBonus()` |
| **Consumes** | C4 (Enemy.ActiveAffixes, BattleState.Update注入), C1 (Entity_Stats/StatusHandler), C7 (掉落 bonus), F3 (加权随机) |
| **Engine APIs** | `MonoBehaviour` 协程 (光环tick), VFX 实例化, `Stat.AddModifier` |
| **架构决策** | 接口驱动而非纯数据 SO — 复杂行为(分身/召唤/AoE)无法纯数据表达; `StatAffixBase` 抽象基类供简单数值词缀复用 |

### C15: 传送门网络 (新增 MVP)
| | |
|---|---|
| **Owns** | `PortalManager`, `PortalNode` 数据结构 (portalId/targetScene/spawnPoint/isActivated) |
| **Exposes** | `UnlockPortal(portalId)`, `Teleport(portalId)`, `IsPortalActive(portalId)` |
| **Consumes** | F2 (激活状态持久化), C16 (传送门位置), F6 (可选能力需求), F8 (城镇传送门 GameObject) |
| **Engine APIs** | `SceneManager.LoadScene`, `SceneManager.sceneLoaded` (传送后位置设置) |
| **架构决策** | 简单状态管理 — 无运行时同步需求，存档驱动激活列表 |

---

## Module Ownership — Feature Layer

### F5: 装备词缀系统 (新增 MVP)
| | |
|---|---|
| **Owns** | AffixDatabase (前缀+后缀池, Tier权重), 词缀生成算法, `Inventory_Item.Modifiers` 写入 |
| **Exposes** | `GenerateAffixes(itemData, rarity)` → ItemModifier[], `TransferAffix(source, target, slot)` (Target阶段) |
| **Consumes** | C6 (装备时 AddModifiers), C7 (掉落时调用), F3 (rarityMultiplier), C14 (同名联动) |
| **架构决策** | 生成后直接写入 `Modifier[]` — 对下游装备/存档透明; `rarityMultiplier` 不二次应用 (⚠️ 防交叉审查 W-3) |

### F6: 能力门控 (新增 MVP)
| | |
|---|---|
| **Owns** | `AbilityGate` MonoBehaviour (requiredAbility 枚举, 锁/解锁二元状态) |
| **Exposes** | `IsAbilityUnlocked(ability)` → bool |
| **Consumes** | C2 (SkillDataManager — 检测技能解锁), C3 (Player_SkillManager), F7 (Boss击败事件→解锁) |
| **架构决策** | 不硬锁 (允许 seq break) — 物理/伤害地形自然阻挡; 二元状态 (锁/解锁) |

### F7: Boss 系统 (新增 MVP)
| | |
|---|---|
| **Owns** | `BossConfig` SO (phases/staggerThreshold/uniqueDropTable/unlocksAbility), `BossRoom` 触发器, Boss UI (顶部大血条+阶段线) |
| **Exposes** | Boss 战斗生命周期事件 (入场/阶段切换/死亡), `BossDefeated` 事件 |
| **Consumes** | C4 (BossPhase/PhaseTransition), C1 (伤害管道/硬直判定), C7 (uniqueDropTable), F6 (能力解锁), C15 (战斗中禁用传送), F2 (击败状态持久化) |
| **架构决策** | Boss 继承 Enemy V2 + IEnemyAffix; PhaseTransition 是新增状态 (无敌+动画+新阶段onEnter); 硬直 8%HP/15s cd |

### F8: 中心城镇 (新增 MVP)
| | |
|---|---|
| **Owns** | 曙光城 Unity Scene, NPC 配置, 安全区逻辑 (无伤害/无敌人) |
| **Exposes** | 城镇场景加载/卸载 |
| **Consumes** | C15 (传送门大厅), C8 (商店 NPC), C12 (任务 NPC), C2 (技能树 UI), F4 (城镇 UI 面板) |
| **架构决策** | 独立 Scene — 简化场景管理; 静态安全区; 无独立系统代码 (纯场景+配置) |

### F9-F13: Target 阶段系统 (架构预留)
| | |
|---|---|
| **F9 制作** | `CraftingRecipeDB` SO → 材料检查+产物生成; 消耗 C5(材料存取)+C7(材料掉落)+F3(稀有度下限) |
| **F10 分解** | 装备→材料逆向; `baseCount × rarityMultiplier`; 依赖 C6(装备)+C5(材料入背包) |
| **F11 合成** | 3合1升级 (同底材+同稀有度→高1级); 词缀转移 (TransferAffix); 依赖 F5(词缀操作) |
| **F12 宝石镶嵌** | 5槽位×1孔; 5类宝石×5Tier; Stat.AddModifier(gemId); 依赖 C2(技能槽)+C5(宝石存储)+F11(宝石升级) |
| **F13 Boss追踪** | `BossTrackConfig` SO → 线索→条件→生成→讨伐; 依赖 F7(BossConfig)+C12(NPC线索)+P1(世界条件) |

---

## Module Ownership — Content Layer

### C16: 地图区域×3 (MVP)
| | |
|---|---|
| **Owns** | 3个区域场景 (矿坑/庭院/王座), 区域调色板, 敌人分布, Boss房间位置 |
| **Exposes** | 区域场景引用, 传送门位置, 门控位置 |
| **Consumes** | C4 (敌人构成), C10 (EnemySpawner), F6 (AbilityGate 位置), F7 (BossConfig), C15 (PortalNode 位置), P1 (世界条件缩放) |

### P1: 世界条件 (Target/Full Vision)
| | |
|---|---|
| **Owns** | `WorldCondition` SO (conditionType/triggerType/effects), 条件→效果规则引擎 |
| **Exposes** | 条件检查触发器 (OnLevelUp/OnBossDefeated/OnQuestComplete等) |
| **Consumes** | F2 (WorldState 持久化), C11 (升级事件), F7 (Boss击败事件), C12 (任务完成事件), C7 (掉落表变更) |
| **架构决策** | 条件→效果模型 (非倾向滑条); 同类倍率相加, 不同类型独立; WorldState Set/Has 桥接 |

---

## Data Flow

### Frame Update Path (每帧)
```
GameInput (F1)
  → Player FSM (C3) 检查状态转换
  → Player_Combat 攻击检测 (C1: OverlapCircleAll)
  → IDamgable.TakeDamage (C1: 7步管道)
  → Entity_StatusHandler 元素状态 (C1)
  → HitStopManager (C1: 全局/局部)
  → OnHealthUpdate 事件 → UI (F4) 血条刷新
```

### Event/Signal Path (跨系统通信)
```
C# event Action (点对点):
  QuestManager.OnQuestAccepted → UI_QuestPanel
  Inventory.OnInventoryUpdated → UI_Inventory
  SkillPointManager.OnSkillPointsChanged → UI_SkillTree

静态事件总线 (广播):
  QuestEvents.OnEnemyKilled ← Enemy.EntityDead
  QuestEvents.OnItemCollected ← ItemAbout

新系统推荐: 统一使用 C# event Action (已有模式)
  精英词缀 OnApplied → UI 词缀图标
  Boss 阶段切换 OnPhaseChanged → Boss UI + BGM
  PortalManager.OnPortalUnlocked → 城镇传送门 VFX + UI 提示
```

### Save/Load Path
```
Save: SaveManager.CollectSaveData()
  → 遍历: Player, Stats(baseValue only), Inventory, Equipment,
          Skills(SkillDataManager+SlotManager), Quests(QuestManager),
          [新增] WorldState(worldFlags), PortalManager(activatedPortals)
  → JsonUtility.ToJson → File.WriteAllText

Load: SaveManager.Load(slot)
  → 场景切换 (如需要) → ApplySaveDataDelayed (轮询 5s)
  → 恢复顺序: Player→Stats→Inventory→Equipment→Skills→Quests
  → [新增] WorldState→PortalManager
  → Modifier 不存档 — 由装备系统重新 AddModifier
```

### Initialization Order (启动顺序)
```
1. Unity Awake: 单例创建 (SaveManager, QuestManager, AudioManager, etc.)
2. Scene Load: 场景中的 Player, EnemySpawner, UI 根节点
3. ApplySaveDataDelayed: 轮询等待 Player + 关键系统就绪
4. 数据恢复: 按顺序应用 (Stats→Inventory→Equipment→Skills→Quests)
5. [新增] WorldState flags → PortalManager → WorldConditions 重新评估
```

---

## API Boundaries

### 核心接口契约

```csharp
// 伤害接口 — 所有可受伤实体
public interface IDamgable
{
    bool TakeDamage(float physDmg, float elemDmg, ElementType element,
                    Transform dealer, bool isCrit = false);
}

// 精英词缀接口 — NEW (C14)
public interface IEnemyAffix
{
    string AffixId { get; }
    string DisplayName { get; }
    AffixTier Tier { get; }
    void OnApplied(Enemy enemy);
    void OnRemoved(Enemy enemy);
    void OnBattleUpdate(Enemy enemy);  // 每帧 BattleState 调用
    float GetLootBonus();              // 掉落稀有度加成
    string GetTooltipText();
}

// 词缀生成器 — NEW (C14)
public class AffixSpawner
{
    public void OnSpawnElite(Enemy enemy, EliteTier tier);
    // 从 AffixDatabase 加权选取 1-3 词缀 → OnApplied
}

// 传送门管理 — NEW (C15)
public class PortalManager : MonoBehaviour
{
    public void UnlockPortal(string portalId);
    public void Teleport(string portalId);
    public bool IsPortalActive(string portalId);
    public List<string> GetActivatedPortals();  // 存档用
}

// Boss 配置 — NEW (F7)
[CreateAssetMenu]
public class BossConfig : ScriptableObject
{
    public string bossId;
    public BossPhase[] phases;            // [1.0, 0.6, 0.3]
    public LootTable uniqueDropTable;
    public float staggerThreshold = 0.08f;
    public SkillUpgradeType? unlocksAbility;
}

// Boss 阶段 — NEW (F7)
[Serializable]
public class BossPhase
{
    public int phaseIndex;
    public float hpThresholdStart;        // 0-1
    public Mechanic[] activeMechanics;
    public string transitionAnim;
}
```

---

## ADR Audit

**当前 ADR 数量**: 6 — ADR-0001 ~ ADR-0006 已创建（2026-08-14 reverse-doc 补记，记录已落地决策）。

| ADR | 标题 | 状态 |
|-----|------|------|
| ADR-0001 | 精英词缀接口驱动 + 运行时组件注入 | ✅ Accepted（已实现） |
| ADR-0002 | 词缀数据库单一 SO（EliteAffixDatabase） | ✅ Accepted（已实现） |
| ADR-0003 | 存档系统 JsonUtility + 版本检查 + WorldState 持久化 | ✅ Accepted（已实现，缺口见文档） |
| ADR-0004 | 事件通信 C# event + 静态事件总线 | ✅ Accepted（已实现） |
| ADR-0005 | 资源加载维持 Resources，Addressables 远期迁移 | ✅ Accepted |
| ADR-0006 | Modifier 多源叠加 + FIFO 驱逐 + source 隔离 | ✅ Accepted（已实现） |

## Required ADRs

### 必须在任何编码前创建 (Foundation + Core 层决策) — ✅ 全部已创建

| # | ADR | 覆盖的技术需求 | 优先级 | 状态 |
|----|-----|---------------|--------|------|
| 1 | **IEnemyAffix 接口架构** — 接口驱动 vs 纯数据 SO vs 组件模式 | TR-enemyv2-*, TR-eliteaffix-*, TR-boss-* | 🔴 阻塞 | ✅ [ADR-0001](adr/ADR-0001-elite-affix-interface.md) |
| 2 | **AffixDatabase 架构** — 单一 SO vs 多 SO vs 外部数据 | TR-eliteaffix-*, TR-equipment-* | 🔴 阻塞 | ✅ [ADR-0002](adr/ADR-0002-affix-database-so.md) |
| 3 | **存档系统 V2** — JsonUtility vs 其他序列化方案, WorldState 持久化修复, 版本迁移策略 | TR-save-*, TR-quest-*, TR-world-* | 🔴 阻塞 | ✅ [ADR-0003](adr/ADR-0003-save-system-v2.md) |
| 4 | **事件通信模式** — C# event vs 静态事件总线 vs UniRx | TR-combat-*, TR-quest-*, TR-skill-* | 🟡 高 | ✅ [ADR-0004](adr/ADR-0004-event-communication.md) |
| 5 | **资源加载策略** — Resources vs Addressables 迁移 | TR-save-* (skill data loading) | 🟡 高 | ✅ [ADR-0005](adr/ADR-0005-resource-loading.md) |
| 6 | **Modifier 模式扩展** — 多源 Modifier 冲突解决 (装备+宝石+消耗品+词缀) | TR-equipment-*, TR-gem-*, TR-consumable-* | 🟡 高 | ✅ [ADR-0006](adr/ADR-0006-modifier-pattern.md) |

### 应在对应系统构建前创建 — ⚠️ 待实现系统对应 ADR 仍未创建:

| # | ADR | 覆盖 | 状态 |
|----|-----|------|------|
| 7 | **Boss 阶段状态机** — PhaseTransition 与现有 FSM 集成方式（注：Boss_SlimeKing v4 已走"专属内容"路线，与 F7 设计部分背离） | TR-boss-* | ⚠️ 待实现时补记 |
| 8 | **传送门网络** — 场景切换策略, 传送安全保证（注：C15 尚未实现，见下"实现状态标注"） | TR-portal-* | ⚠️ 待实现时补记 |
| 9 | **世界条件规则引擎** — 条件→效果评估时序（注：P1 为 Target 阶段） | TR-world-* | ⚠️ 待实现时补记 |

---

## Architecture Principles

1. **接口优先** — 新功能通过接口契约与已有系统集成 (IEnemyAffix → Enemy, IDamgable → Combat)
2. **数据透明** — ScriptableObject 驱动配置, 生成后的最终值存入已有数据结构 (如 Modifier[])
3. **修复优先于新增** — 已有致命 Bug (InitializeEnemy, TalkToNPC, WorldState) 必须在新增功能前修复
4. **已有模式优先** — 沿用 C# event Action + 单例 + Modifier 模式，不引入新框架
5. **MVP 边界清晰** — Feature 层 Target 系统预留接口但不实现 (F9-F13), 避免 MVP 膨胀

---

## Open Questions

| ID | 问题 | 优先级 | 解决路径 | 状态 |
|----|------|--------|----------|------|
| QQ-01 | Addressables 迁移是否在 Technical Setup 阶段执行？ | Medium | ADR-0005 | ✅ 已解决（维持 Resources，远期迁移） |
| QQ-02 | AffixDatabase 前缀/后缀池是否与精英词缀池共享？ | Medium | ADR-0002 | ✅ 已解决（分离，EquipmentAffixGenerator 独立） |
| QQ-03 | Boss 房间是独立场景还是当前场景的子区域？ | Medium | ADR-007 | ⚠️ 实测已采用独立场景（BOSS.unity），待补记 ADR |
| QQ-04 | 世界条件 (P1) 的"条件→效果"引擎在 MVP 阶段是否需要骨架代码？ | Low | ADR-009 | ⚠️ 待实现时决策 |
| QQ-05 | 现有 Editor 工具 (QuestTreeEditor) 是否需要迁移 EntityId API？ | Low | 编辑器独立评估 | 未启动 |

---

## 实现状态标注（2026-08-14 全系统审查后同步）

> 依据 production/review/ 12 份系统审查报告，以下架构承诺与实际实现存在差异，需注意：

| 架构承诺 | 实际状态 | 说明 |
|----------|----------|------|
| C15 传送门网络（PortalManager/PortalNode/激活持久化） | ❌ **未实现** | 仅场景级 `Portal.cs` 单向传送；读档后解锁丢失（review/area.md S-1） |
| F6 能力门控（AbilityGate） | ❌ **未实现** | 目录为空；Boss 解锁能力→新区域缺环（review/area.md S-2） |
| F2 存档：传送门激活状态持久化 | ❌ 未落地 | 需 WorldState/activatedPortals 方案（review/save.md S-1） |
| F7 Boss 击败状态持久化 | ❌ 未落地 | `BossEncounter.defeatedFlag` 仅运行时（review/enemy.md S-1） |
| F9-F13 预留接口但不实现（原则 5） | ⚠️ **F9/F10/F11 已提前实现** | 制作/分解/合成/仓库已落地（Target 功能前移，范围膨胀需评估）；F12 宝石/F13 Boss追踪未实现 |
| F5 装备词缀 V2 | ✅ 已实现 | EquipmentAffixGenerator 前缀+后缀 → Modifier[] |
| C14 精英词缀 | ✅ 已实现 | IEnemyAffix 接口 + AffixSpawner 注入（ADR-0001） |

### 存档恢复顺序（2026-08-14 实测同步）
实现为：`Player位置 → 属性(baseValue) → 技能 → 背包 → 仓库 → 装备 → 任务 → WorldState`。
**技能先于背包**（背包容量依赖技能等级，有注释支撑）——本文档原描述顺序（背包→装备→技能）已过期，以实现为准（ADR-0003 已记录）。

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | v1.0 从 Assets/Scripts/ 反向生成 |
| 2026-07-30 | Claude (create-architecture) | v2.0 基于 31 GDD 的系统分层+模块所有权+数据流+ADR规划 |
| 2026-08-14 | Claude (system-review 同步) | v2.1 补记 ADR-0001~0006；新增实现状态标注（C15/F6 缺口、F9-F13 前移、存档顺序实测）；阶段更新为 Production |

---

*本文档由 `/create-architecture` 生成*
