---
status: reverse-documented
source: Assets/Scripts/QuestSystem/
date: 2026-07-28
verified-by: oy
---

# 任务系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。[推断] = 代码分析推断.

---

## 1. 任务生命周期状态机

```
                          AcceptQuest()
    [未接取] ────────────────────────→ [进行中]
      │                                  │
      │ prerequisites未满足              ├── 非最终阶段目标全达成
      │ 或已完成/已失败                   │   → IsCurrentStageComplete = true
      │                                  │   → 等待 NPC 对话
      │                                  │
      │                                  ├── NPC: ClaimCurrentStageReward()
      │                                  │   → AdvanceToNextStage() → 下一阶段
      │                                  │
      │                                  └── 最终阶段目标全达成
      │                                      → SetQuestReadyToClaim()
      │                                        │
      ▼                                        ▼
    [已完成] ← ClaimFinalReward()         [可领取]
                                             │
    [已失败] ← MarkQuestFailed() ←────────────┘

自动链: ClaimFinalReward → foreach followUpQuest:
          if autoAccept: AcceptQuest(followUpId)
```

**6 个互斥状态，5 个集合追踪**:

| 状态 | 集合 | 何时进入 |
|------|------|----------|
| 未接取 | (推导) | 前提任务未完成 / 从未接取 |
| 进行中 | `activeQuests` | AcceptQuest() |
| 阶段完成 | 进行中的子状态 | 当前阶段目标全达成 (非最终阶段) |
| 可领取 | `readyToClaimQuests` | 最终阶段目标全达成 |
| 已完成 | `completedQuests` | ClaimFinalReward() |
| 已失败 | `failedQuests` | MarkQuestFailed() |

---

## 2. Objective 类型与追踪

### Kill (击杀)

```
触发链:
  Enemy.EntityDead()
    → QuestEvents.ReportEnemyKilled(uniqueID)
      → QuestManager.HandleEnemyKilled(enemyId)

HandleEnemyKilled:
  foreach active quest:
    foreach objective in currentStage:
      if obj.type == Kill && obj.targetId == enemyId:
        TryProgressObjective(questId, stageIndex, objIndex, count=1)
        // ⚠️ count 硬编码为 1

targetId 兼容处理:
  存储ID可能是 "010101 - Skeleton"
  比较时先 split(" - ")[0] → 用 "010101" 匹配
```

### Collect (收集)

```
触发链:
  ItemAbout 拾取物品
    → QuestEvents.ReportItemCollected(itemId, 1)
      → QuestManager.HandleItemCollected(itemId, count)

HandleItemCollected:
  与 Kill 相同, 但支持 count 参数

特殊处理 (接取时):
  SyncCollectProgressFromInventory():
    接取任务时扫描玩家已有背包
    → 如果已持有目标物品 → 预填进度
    → 防止玩家需要重新收集
```

### TalkToNPC (与NPC对话)

```
⚠️ 完全不可用!
  QuestEvents.OnNpcTalked 事件已定义
  QuestManager 已订阅
  但没有任何系统调用 QuestEvents.ReportNpcTalked()
  → TalkToNPC 目标永远无法推进
```

---

## 3. 阶段推进

```
QuestData 结构:
  stages: List<QuestStage>
    └── 每阶段: description, objectives[], stageReward?
  finalReward: QuestReward

接取时: QuestProgress { currentStageIndex=0, objectiveProgress=[0,0,...] }

目标达成 → TryProgressObjective():
  objectiveProgress[i]++ 
  if 当前阶段所有目标达成:
    if 是最终阶段 → SetQuestReadyToClaim() → 移入 readyToClaimQuests
    else → IsCurrentStageComplete=true → 等待 NPC

NPC 对话 (UI_QuestDialogue, StageComplete 模式):
  第一次点击:
    ClaimCurrentStageReward() → GrantReward(stageReward)
      收集类任务: RemoveCollectItems() 先扣除物品
    自动转为 InProgress 或 FinalClaim 模式
  第二次点击:
    AdvanceToNextStage() → currentStageIndex++ → 重置 objectiveProgress
    OnQuestStageChanged 事件

最终领取:
  ClaimFinalReward() → GrantReward(finalReward)
    → setWorldFlags → WorldState.Set(flag, true)
    → 自动接取 followUpQuestIds (如果 autoAccept)
    → 移入 completedQuests
```

---

## 4. 事件总线

### 外部 → 任务系统 (QuestEvents 静态类)

```
QuestEvents.OnEnemyKilled(string enemyId)      ← Enemy.EntityDead()
QuestEvents.OnItemCollected(string itemId, int) ← ItemAbout 拾取
QuestEvents.OnNpcTalked(string npcId)            ← ⚠️ 无人调用
```

### 任务系统 → UI (QuestManager 的事件)

```
OnQuestAccepted(string questId)          → UI_QuestPanel, UI_QuestDialogue
OnQuestStageChanged(string questId)      → UI_QuestPanel
OnQuestReadyToClaim(string questId)      → UI_QuestPanel, UI_QuestTracker
OnQuestClaimed(string questId)           → UI_QuestPanel
OnQuestFailed(string questId)            → UI_QuestPanel
OnObjectiveUpdated(questId, idx, count)  → UI_QuestPanel, UI_QuestTracker
OnTrackChanged(string questId)           → UI_QuestTracker
```

---

## 5. 任务前提与链式接取

### 前提 (Prerequisites)

```
QuestData.prerequisiteQuestIds: List<string>
  → ArePrerequisitesMet(): 全部在 completedQuests 中
  → 在 AcceptQuest() 和 IsQuestAvailable() 中检查
```

### 后续任务 (Follow-up)

```
QuestData.followUpQuestIds: List<string>
  → ClaimFinalReward() 后自动迭代
  → autoAccept=true → AcceptQuest(followUpId)
  → autoAccept=false → 保持可用, 需手动接取

⚠️ 无分支/条件链: 所有 followUp 都在完成后统一触发
```

---

## 6. WorldState

```
WorldState: 静态 Dictionary<string, bool>
  Get(key) / Set(key, value) / Remove(key) / Has(key)

与任务系统的交互:
  写入: ClaimFinalReward() → set flags
  读取: ⚠️ 无! WorldState 不作为任务前提
         → 当前是 "只写" 状态

⚠️ 存档丢失:
  WorldState 有 GetSaveData/LoadFromSave 方法
  但 SaveManager 从未调用
  → 重载后所有 world flag 丢失
```

---

## 7. NPC 集成

### NPCBehaviour

```
职能: 任务发放 + 商店入口

任务查询方法 (对 questsToGive 列表进行判断):
  HasAvailableQuest()        → 前提满足, 未接取/未完成
  HasActiveQuest()           → 在 activeQuests 中
  HasStageToSubmit()         → 当前阶段完成 (非最终)
  HasFinalRewardToClaim()    → 在 readyToClaimQuests 中

交互 (按 F → UI_NpcMenu):
  按钮优先级: StageToSubmit > FinalRewardToClaim > Available > Active
  委托给 UI_QuestDialogue 对应模式

⚠️ triggerNpcId 字段被忽略:
  QuestData.triggerNpcId 已定义但从未在运行时查询
  NPC 可发放任何 questsToGive 列表中的任务
  即使 triggerNpcId 不匹配
```

### QuestIndicator (NPC 头上标记)

```
金色 ! : NPC 有新任务 (HasAvailableQuest)
蓝色 ? : NPC 有待提交/领取 (HasStageToSubmit / HasFinalRewardToClaim)
Update() 每帧轮询 — 对少量 NPC 可接受
```

---

## 8. 存档/读档

### 保存数据

```
QuestSaveData:
  active: QuestSaveEntry[]
    └── { questId, currentStageIndex, objectiveProgress[], objectives[] }
  readyToClaim: string[]
  completed: string[]
  failed: string[]
  trackedQuestId: string
```

### 恢复

```
QuestManager.LoadFromSave(data):
  1. 清空所有运行时状态 (activeQuests, readyToClaim, completed, failed, claimedStageRewards)
  2. 从 active 列表重建 QuestProgress 对象
  3. 恢复 readyToClaim / completed / failed
  4. 恢复 trackedQuestId
```

### ⚠️ 已知数据丢失

```
⚠️ claimedStageRewards: 读档时清空 → 阶段奖励可重复领取
⚠️ completedQuestProgress: 未序列化 → 已完成任务看不到详情
⚠️ WorldState: SaveManager 未收集/恢复 → 所有 world flag 丢失
```

---

## 9. 发现的问题汇总

| 严重度 | 问题 | 影响 |
|--------|------|------|
| 🔴 致命 | TalkToNPC 目标永远无法推进 | 此类型任务不可用 |
| 🔴 致命 | claimedStageRewards 不持久化 | 读档后可重复领取阶段奖励 |
| 🟡 重大 | WorldState 不存档 | 依赖 world flag 的逻辑会在重载后失效 |
| 🟡 重大 | triggerNpcId 字段被忽略 | 设计意图与实际行为不一致 |
| 🟡 重大 | ItemPickup 触发类型未实现 | 拾取物品触发任务的机制不可用 |
| 🟢 低 | Kill 目标 count 硬编码为 1 | 单个击杀无法推进多步 |
| 🟢 低 | QuestIndicator 每帧轮询 | 性能影响微乎其微 |
| 🟢 低 | CSV 解析不支持含逗号的名字 | 边界bug |

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
