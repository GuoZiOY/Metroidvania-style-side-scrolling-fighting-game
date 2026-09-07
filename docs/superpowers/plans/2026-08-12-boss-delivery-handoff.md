# 史莱姆王 Boss 交付清单（代码完成 → 你接线 + 验收）

> 代码 Tasks 1-10 全部完成并提交（master，18 commits）。以下是你需要做的。

## 0. 第一步（最重要）：Unity 编译检查

打开 Unity 让脚本编译，看 Console。
- **无红错** → 继续
- **有红错** → 把报错贴给我，我立刻修（所有代码只过了静态检查，未经 Unity 编译管线）

> 附注：几个 `.meta` 文件是子智能体生成的（缺 MonoImporter 段），Unity 首次导入会自动补齐，不是错误。

## 1. 场景接线（你手动搭）

### 1a. Boss 房场景（新建，如 `BossRoom_KingSlime`）
| 对象 | 内容 |
|------|------|
| 场景 | 独立 .unity；进 Build Settings |
| 到达传送门 | `Portal`（`portalId="BossRoomEntry"`，玩家从这里进入的落点锚） |
| 出口传送门 | `Portal`（`targetScene=城镇`，**初始 inactive**，胜利后激活） |
| follow vcam | 命名含 "Follow"（PlayerSpawner 按名绑定） |
| Intro vcam | 命名含 "Intro"、**priority=0**、对准 Boss 出场点 |
| `BossEncounter` | 场景摆一个，拖引用（见下表） |
| `BossMinionRegistry` | 场景摆一个 |
| 血条 Canvas | 挂**持久 HUD 层级**（UIManager 根下），`UI_BossHealthBar` 拖引用；Fill/背景 `raycastTarget=false` |

### 1b. BossEncounter 引用
`bossPrefab`(史莱姆王) / `spawnPoint`(出场点) / `arrivalPortal`(到达传送门) / `exitPortal`(出口传送门) / `followCam` / `introCam` / `bossBgm`(calm) / `bossBgmRage`(狂暴段,可空) / `healthBar` / `minionRegistry` / `screenShake`

### 1c. 史莱姆王预制体 `Boss_SlimeKing.prefab`
- `Boss_SlimeKing` + Enemy 组件（Enemy/Entity_Stats/Entity_Health）
- **身体 Trigger Collider2D** + `ContactDamageArea`（12%、1s，**挂专用 trigger 不挂物理身体**）
- `canKnockbacked=false`、`canBeStunned=false`（防御性确认）
- 动画 idle/move/attack/dead 通用；迷你王预制体引用；`playerCheck`/`groundCheck` 赋值
- Rigidbody2D Dynamic + 重力（参考 Enemy_Slime）

### 1d. 迷你王预制体 `Boss_SlimeMinion.prefab`
- `Boss_SlimeMinion` + Enemy 组件 + `ContactDamageArea`（5~8%、1s）+ 身体 trigger

### 1e. 关卡侧入口传送门
关卡里 boss 房传送门：`targetScene=BossRoom场景名`、`targetPortalId="BossRoomEntry"`、`saveBeforeTeleport=true`

### 1f. 死亡流程
玩家死亡 → 现有死亡面板 → `SaveManager.LoadWithReload` 读上次存档点（Boss 房场景卸载 → Boss 重置）。存档点设在 Boss 房外即可。

## 2. 验收测试（对照设计文档 AC）

**出场**：进 Boss 房 → 到达传送门锁定（无法交互）→ Intro vcam 特写 → Boss 坠入+震屏 → BGM 切 Boss 曲 → 血条 100% 出现 → 开始战斗
**战斗**：跳砸（蓄力前摇→落点预告→落地 AoE→落地后摇窗口）→ 召唤迷你王（2 只，上限 3，不分裂）→ 拉远/被卡传送（3~6m 偏移落点）→ HP<30% 狂暴（召唤变 3、后摇变短、BGM 切 rage、变红）
**击杀**：血条解绑→迷你王清→相机恢复→BGM 恢复→出口传送门激活（指向城镇）→ 二次进入不触发
**死亡**：死亡面板 → 读档回存档点 → 重进 Boss 房 Boss 满血
**公平**：受击无敌 0.7s——AoE+接触+迷你王同帧最多结算一次；DoT 不受无敌帧影响

## 3. 已知留待后续（设计文档 Open Questions）
- 奖励内容（二段跳）等世界条件系统实现后接入（`VictoryEvent` 挂载点已留）
- `ability-gate.md` 映射需调整（Boss1=二段跳，与用户决策一致）
- 已击败标志持久化（WorldState 未实现，MVP 场景内生效）
