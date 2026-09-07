# 区域/刷怪/传送门系统审查报告

> 审查日期：2026-08-14 | 代码基线：git HEAD `7c7b82c`
> 审查方式：定向深读（Portal.cs / EnemySpawner / EnemyArea 结构）+ 目录全量核查（Area / AbilityGate / PortalSystem）

## 1. 框架结构

### 1.1 核心类与模块划分

```
区域/刷怪 (Assets/Scripts/Others/Area/):
  EnemySpawner      —— 协程波次生成 (SpawnEnemies/CalculateEnemyLevel)
  EnemyArea         —— 区域触发器 (进入触发/重生/清空事件)
  AreaDifficulty    —— 区域难度参数
  AreaDetectorBase  —— 检测基类 (玩家进入提示)
  SceneLevelArea    —— 场景等级区域
  Portal            —— 场景传送门组件 (交互→切场景→落点)

能力门控 (Assets/Scripts/Others/AbilityGate/):  ⚠️ 目录为空 —— 0 个 .cs
传送门网络 (Assets/Scripts/Others/PortalSystem/): ⚠️ 目录为空 —— 0 个 .cs
```

### 1.2 装配方式
- **场景组件**：Portal/EnemyArea 等为场景内 MonoBehaviour，Inspector 接线（portalId/targetScene/spawnPoint）
- **生成器**：EnemySpawner 协程 + `InitializeEnemy(type, level)` 与敌人系统对接
- **传送**：`Portal.Teleport` → `SceneTransitionFader.TransitionToScene`（过场黑幕）→ `PlayerSpawner.MarkSpawnAtPortal` 落点定位

### 1.3 模块划分评价
- ✅ Portal 组件职责清晰（交互提示/锁定/传送/存档时序）
- ❌ **C15 传送门网络与 F6 能力门控完全未实现**——目录为空，只有场景级单点传送组件

## 2. 工作流程

### 2.1 区域刷怪
1. 玩家进入 `EnemyArea` 触发器 → `EnemySpawner.SpawnEnemies(count, eliteChance, difficulty, baseLevel)`
2. `CalculateEnemyLevel(isElite, difficulty, baseLevel)` 计算等级 → `InitializeEnemy(type, level)`（类型+等级+词缀）
3. 清空区域 → 清空事件通知；离开冷却后重生
4. 首次进入显示遭遇提示（AreaDetectorBase → UI_EventTip）

### 2.2 传送门传送
1. 玩家进入 Portal 触发区 → 按 F（`GameInput.Action.Interact`）→ `Teleport()`
2. `saveBeforeTeleport` → `SaveManager.Save()`（传送前存档）
3. `PlayerSpawner.MarkSpawnAtPortal(targetPortalId)` → `SceneTransitionFader.TransitionToScene(targetScene)`
4. 到达后 `PlayerSpawner.PlaceAtEntryPoint`（等 1 帧）：`arrivePortalId` 非空 → TryPlaceAtPortal（找不到回退入口存档点）
5. Boss 战期间 `Portal.SetLocked(true)` 锁定（BossEncounter 调用）

### 2.3 存档时序
`Portal.Teleport` 存档在**传送前**（保留当前进度）；到达后**不重存**（git 2fd1c1f 移除"到达后重存"，避免双重存档）；死亡读档回存档场景

## 3. 信息链路

| 链路 | 方向 | 说明 |
|------|------|------|
| 刷怪 | EnemyArea → EnemySpawner → Enemy | InitializeEnemy 对接 |
| 传送 | Portal → SceneTransitionFader → PlayerSpawner | 场景切换 + 落点 |
| 锁定 | BossEncounter → Portal.SetLocked | Boss 战禁止回头 |
| 存档 | Portal → SaveManager | 传送前存档 |
| **激活状态** | ❌ 无 PortalManager/无持久化 | 读档后传送门解锁全部丢失 |

## 4. 与设计文档一致性

| 设计承诺 | 实现状态 |
|----------|----------|
| C10 区域/刷怪：EnemySpawner 协程 + AreaDifficulty + 等级浮动 | ✅ 落地 |
| C15 传送门网络：PortalManager/PortalNode/UnlockPortal/Teleport/IsPortalActive + 激活持久化 | ❌ **未实现**（仅场景级 Portal.cs 单向传送） |
| F6 能力门控：AbilityGate/IsAbilityUnlocked/锁-解锁二元 | ❌ **未实现**（目录为空） |
| C16 地图区域×3（矿坑/庭院/王座）+ Boss 房间 | ⚠️ 部分：level0/level1/BOSS 场景存在，但区域设计（调色板/门控位置）未按 GDD 落地 |
| F2 存档：传送门激活状态持久化（V2 新增） | ❌ 未落地（save.md 同报） |

## 5. 代码质量

- ✅ `Portal.cs`（161 行）注释规范、防御性检查到位（targetScene 空校验、spawnPoint 回退）、DOTween 清理完整
- ⚠️ 传送门激活状态仅运行时（无持久化）——重开游戏全部要重跑图
- ⚠️ `AbilityGate`/`PortalSystem` 空目录与架构文档"已设计"状态不符（文档承诺 vs 实现缺口）

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重
1. **C15 传送门网络未实现（设计缺口）** — `PortalManager`/`PortalNode`/`UnlockPortal` 无任何代码；只有场景级 `Portal.cs` 单向传送。银河城"解锁传送点→快速往返"核心体验缺失，读档后无传送网络。
   - 改进：实现轻量 `PortalManager`（单例，`Dictionary<string, PortalNode>`），传送门激活写入存档（`activatedPortals: List<string>`，SaveManager 收集/恢复）；`Portal.cs` 接入 `IsPortalActive` 控制。
2. **F6 能力门控未实现（设计缺口）** — `AbilityGate` 目录为空。Boss 解锁能力→新区域的设计链路（Pillar 4 核心）未落地，DashBlur/二段跳等技能已实现但无门控位置。
   - 改进：实现 `AbilityGate`（requiredAbility 枚举 + 锁/解锁），Boss 击败（WorldState flag）→ `IsAbilityUnlocked`。
3. **传送门/Boss 击败状态不持久化** — 读档后传送门解锁与 Boss 击败状态全部丢失（与 save.md/enemy.md 交叉确认）。
   - 改进：统一用 WorldState flag（`portal_xxx_activated`/`boss_xxx_defeated`）持久化，读档链路已覆盖 WorldState。

### 🟡 中等
4. **区域设计未按 GDD 落地** — area-design.md 的 3 区域调色板/门控位置/难度曲线与 level0/level1 实际场景未对齐；area-spawn-system.md 等文档状态与实现脱节。
   - 改进：以 level0/level1/BOSS 实际场景为准反向更新 area-design.md，或按 GDD 补齐区域内容。
5. **`CalculateEnemyLevel` 难度曲线核对** — 与 area-design 三级难度（矿坑 1-5/庭院 4-8/王座 7-12）需验证实现一致。

### 🟢 轻微
6. `SceneLevelArea` 与 `EnemyArea` 职责重叠（场景级 vs 区域级触发器），可统一。
7. 传送提示参数（fadeDuration/floatHeight/floatSpeed）与 NPC 提示重复实现，可抽共享组件。
8. 空目录 `AbilityGate/`、`PortalSystem/` 建议删除或放占位 README 说明状态。

---

## 附：审查结论摘要

- **健康度**：区域刷怪（C10）落地良好，Portal 组件质量高；但 **C15 传送门网络与 F6 能力门控两个 MVP 设计完全未实现**，是当前与架构文档差距最大的系统。
- **最需优先**：实现 PortalManager（激活持久化）+ AbilityGate（Boss 解锁能力），补齐银河城核心循环（探索→解锁→新区域）的缺环。
- **最值得肯定**：`Portal.cs` 的传送时序设计（传送前存档 + 落点定位 + Boss 锁定 + 过场黑幕）质量高，未来实现 PortalManager 时可直接复用其 `targetPortalId` 落点机制。
