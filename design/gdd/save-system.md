---
status: reverse-documented
source: Assets/Scripts/SaveSystem/
date: 2026-07-28
verified-by: oy
---

# 存档系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 架构概览

```
SaveManager (单例, DontDestroyOnLoad)
  ├── CollectSaveData()        // 收集 → 构造 SaveData
  │     ├── CollectPlayerData()
  │     ├── CollectStatData()
  │     ├── CollectInventory()
  │     ├── CollectEquipment()
  │     ├── CollectSkillData()
  │     └── CollectQuestData()
  │
  ├── 序列化: JsonUtility.ToJson(SaveData)
  ├── 写入: File.WriteAllText(path, json)
  │
  ├── ApplySaveData(data)       // 读取 → 恢复
  │     ├── ApplyPlayerData()
  │     ├── ApplyStatData()
  │     ├── ApplyInventory()
  │     ├── ApplyEquipment()
  │     ├── ApplySkillData()
  │     └── ApplyQuestData()
  │
  └── Profile 管理: 元数据列表 (主菜单展示)
```

---

## 2. SaveData 完整结构

```
SaveData (顶层)
  ├─ version: int = 1
  ├─ saveTime: string           // DateTime.UtcNow.ToString("O")
  ├─ playTime: float            // 总游玩时间(秒)
  ├─ sceneName: string          // 当前场景名
  ├─ posX, posY, posZ: float    // 玩家位置
  ├─ lastCheckpointId: string   // 最近存档点ID
  │
  ├─ player: PlayerSaveData
  │    ├─ currentHP, currency
  │    ├─ currentLevel, currentExp
  │    ├─ unspentSkillPoints, unspentAttributePoints
  │    └─ totalSkillPoints, usedSkillPoints
  │
  ├─ stats: StatSaveEntry[]
  │    └─ { statType: int, baseValue: float }
  │
  ├─ inventory: InventorySlotData[]
  │    └─ { itemId, stackSize, slotIndex, rarity, rarityMultiplier }
  │
  ├─ equipment: EquipSlotData[]
  │    └─ { itemId, itemType, slotIndex, rarity, rarityMultiplier }
  │
  ├─ skills: SkillSaveData
  │    ├─ learned: SkillLevelEntry[]  // { upgradeType, level }
  │    └─ slotBindings: int[5]        // 槽位0-4的 upgradeType
  │
  └─ quests: QuestSaveData
       ├─ active: QuestSaveEntry[]    // { questId, currentStageIndex, objectives[] }
       ├─ readyToClaim: string[]
       ├─ completed: string[]
       ├─ failed: string[]
       └─ trackedQuestId: string
```

---

## 3. 存档流程

### 3.1 手动存档

```
触发: 主菜单/检查点 → SaveManager.Save(slotIndex)

Save(slotIndex):
  │
  ├── CollectSaveData():
  │     ├── FindAnyObjectByType<Player> → 验证玩家存在
  │     ├── 记录场景名 + 位置 (Transform.position)
  │     ├── CollectPlayerData → PlayerSaveData
  │     │     └── 从 Player.health, PlayerInventorySystem,
  │     │         PlayerLevelManager, SkillPointManager 收集
  │     ├── CollectStatData → StatSaveEntry[]
  │     │     └── 枚举所有 StatType → GetBaseValue() (不含 Modifier)
  │     │         ⚠️ Modifier 不存档 — 由装备系统重新应用
  │     ├── CollectInventory → InventorySlotData[]
  │     │     └── 遍历 Inventory_Player.itemDictionary
  │     ├── CollectEquipment → EquipSlotData[]
  │     │     └── EquipmentSystem.GetEquippedItemsForSave()
  │     ├── CollectSkillData → SkillSaveData
  │     │     └── 双源合并: Player_SkillManager.allSkills
  │     │                   + SkillDataManager.GetAllSkillLevels()
  │     │         → slotBindings 从 SkillSlotManager 获取
  │     └── CollectQuestData → QuestSaveData
  │           └── QuestManager 获取全部状态字典
  │
  ├── data.saveTime = DateTime.UtcNow
  ├── data.playTime = TotalPlayTime (累计 + 本会话)
  ├── json = JsonUtility.ToJson(data, pretty: true)
  ├── File.WriteAllText(persistentDataPath/slot_N.json)
  └── UpdateProfile(slotIndex, data) → profiles.json
```

### 3.2 自动存档 (检查点)

```
Checkpoint : MonoBehaviour
  OnTriggerEnter2D("Player"):
    if 冷却中: return (防止频繁触发)
    SaveManager.CurrentCheckpointId = checkpointId
    SaveManager.Save() → 写到 CurrentSlotIndex
    显示 DOTween 浮动提示
```

---

## 4. 读档流程

### 4.1 首次读档 (主菜单)

```
SaveManager.Load(slotIndex):
  │
  ├── 读取文件 → JsonUtility.FromJson<SaveData>
  ├── 验证解析结果
  │
  ├── 场景不同? → 切换场景:
  │     pendingLoad = data
  │     SceneManager.sceneLoaded += OnSceneLoadedForLoad
  │     SceneManager.LoadScene(data.sceneName)
  │
  └── 场景相同? → 直接恢复:
        ApplySaveData(data)

场景加载后:
  OnSceneLoadedForLoad:
    → StartCoroutine(ApplySaveDataDelayed(pendingLoad))

ApplySaveDataDelayed (协程):
  // 轮询等待所有关键系统就绪 (最多等5秒):
  while timeout > 0:
    if Player && PlayerInventorySystem && SkillDataManager && SkillSlotManager:
      break
    yield return null
    timeout -= deltaTime

  就绪后:
    ApplySaveData(data)
    yield WaitForSeconds(0.1)  // 短暂等待装备/技能生效
    RefreshAllUI()
```

### 4.2 死亡后重载

```
SaveManager.LoadWithReload(slotIndex):
  与 Load 相同, 但强制重载当前场景
  → 重置敌人/宝箱等
```

### 4.3 数据恢复的顺序

```
ApplySaveData:
  1. 恢复游玩时间 [accumulatedPlayTime, sessionStartTime]
  2. 恢复位置 [player.transform.position]
  3. ApplyPlayerData → HP, 货币, 等级, 技能点
  4. ApplyStatData → 所有属性 baseValue
  5. ApplyInventory → 清空背包 → 逐个 AddItem (保留槽位)
  6. ApplyEquipment → UnequipAll → 逐个 EquipItemToSlot
  7. ApplySkillData → SkillDataManager.UpdateSkillData → 事件链激活
  8. ApplyQuestData → QuestManager.LoadFromSave
```

---

## 5. Profile 系统

```
profiles.json (与存档文件分开):
  SaveProfileList { profiles: SaveProfile[] }

SaveProfile:
  slotIndex: int
  saveTime: string
  playTime: float
  sceneName: string
  playerLevel: int
  isEmpty: bool

用途: 主菜单展示存档列表 (不需完整解析每个存档文件)
更新: 每次存档时同步更新
删除: Delete + RemoveProfile
```

---

## 6. 跨系统协调

### 6.1 数据收集的 "系统就绪" 假设

```
CollectSaveData 假设以下系统立即可用:
  FindAnyObjectByType<Player>          // 场景中存在
  Player.health, Player.stats          // 组件已初始化
  PlayerInventorySystem                // FindAnyObjectByType
  SkillDataManager.Instance            // 单例已创建
  SkillSlotManager.Instance            // 单例已创建
  QuestManager.Instance                // 单例已创建

读档恢复假设:
  ApplySaveDataDelayed 轮询至多 5s 等待系统就绪
  → 如果某系统拒绝初始化超过 5s → 静默失败
```

### 6.2 Modifier 不存档 — 由装备系统重建

```
存档: StatSaveEntry.baseValue (仅基础值)
      ⚠️ Modifier 值不保存

读档:
  1. ApplyStatData → setBaseValue
  2. ApplyEquipment → EquipItemToSlot → AddModifiers
     → Modifier 重新应用 → finalValue = baseValue + sum(modifiers)
  3. 如果装备了同样属性值但不同稀有度的物品 → modifier 值可能略不同
     ⚠️ 由于 statMultiplier 也被保存，这个值应与存档前一致
```

---

## 7. 文件存储

```
Application.persistentDataPath/
  ├── slot_0.json        // 存档槽 0
  ├── slot_1.json        // 存档槽 1
  ├── slot_2.json        // 存档槽 2
  ├── slot_3.json        // 存档槽 3
  └── profiles.json      // 所有槽位元数据

格式: JsonUtility (不支持 Dictionary → 需手动转换)
```

---

## 8. 边界情况

- ✅ 存档槽不存在时 Load 报错
- ✅ 场景切换时等待系统就绪 (轮询+超时)
- ✅ Modifier 不存档 → 由装备恢复重建 (确保一致性)
- ✅ Profile 与存档文件分离 → 快速列出存档列表
- ⚠️ 版本号硬编码 = 1，无迁移策略
- ⚠️ 收集数据失败时整个存档失败 (没有部分恢复)
- ⚠️ ApplySaveDataDelayed 超时 5s 后静默失败 (应该报错)
- ⚠️ 统计数据通过 Resources.LoadAll 加载 Skills (硬编码路径 "Data/StillData")
- ⚠️ 物品 ID 找不到时静默跳过 (物品从背包消失)

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
