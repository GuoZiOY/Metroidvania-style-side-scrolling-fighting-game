# 破碎之城 · Shattered City

一款 2D 类银河恶魔城 + RPG 动作游戏 Demo，包含战斗、技能、装备、任务、存档等完整功能系统。

> **项目性质**：本项目是对已有网络教学资源的学习复刻与实现，用于巩固 Unity 客户端开发与游戏系统设计能力。核心系统已完成并跑通完整游玩循环。

| 项目概况 | |
|---|---|
| 引擎 | Unity 6000.4.8f1（URP） |
| 语言 | C# |
| 自研代码 | 约 **26,000 行 / 274 个文件** |
| 编辑器工具 | 约 **2,100 行** |
| 配置资产 | **114 个 ScriptableObject** |
| 开发周期 | 2026.07 – 2026.09 |

---

## 目录

- [核心玩法](#核心玩法)
- [技术要点](#技术要点)
- [运行方式](#运行方式)
- [数据配置管线](#数据配置管线)
- [项目结构](#项目结构)
- [已知缺陷](#已知缺陷)
- [声明](#声明)

---

## 核心玩法

**角色控制** — 待机 / 移动 / 跳跃 / 二段跳 / 下落 / 蹬墙滑行 / 蹬墙跳 / 冲刺。包含土狼时间与输入缓冲优化操作手感，动态重力让上升更飘、下落更干脆。

**战斗** — 三段连击（超时重置）、跳跃攻击、反击与反击后自动追击连段。完整的打击感反馈：顿帧、屏幕震动、伤害飘字、受击闪烁、暴击特效。元素状态包含火焰灼烧、冰霜减速、闪电蓄能落雷。

**技能** — 14 个主动技能（冲刺残影、追踪碎片、时间回响、领域展开、元素附魔等），支持升级分支、5 槽位拖拽装配与技能树解锁（前置依赖、冲突分支、技能点）。

**装备与经济** — 6 部位多槽位装备、程序化词缀生成（前缀/后缀/双刃负面）、5 档稀有度、加权掉落系统、制作 / 分解 / 合成三套铁匠功能。

**敌人与 Boss** — 史莱姆、骷髅、链锤骷髅及 16 种可组合精英词缀（火焰光环、冰霜新星、镜像分身、自爆、屏障护盾等）。Boss 史莱姆王包含追击、大跳、冲刺、传送、召唤，以及分裂阶段与濒死隐身保命阶段。

**任务** — 多阶段任务（击杀 / 收集 / 对话目标）、前置任务链、阶段与最终奖励。

**存档** — 4 槽位存档，装备词缀与稀有度精确还原，支持跨场景读档与死亡重载。

---

## 技术要点

### 1. 组件式状态机（FSM）

三套独立状态机：玩家 14 态、敌人 6 态、Boss 4 阶段。`StateMachine` 为**纯 C# 类**而非 MonoBehaviour，状态由构造函数注入宿主依赖，避免 `GetComponent` 开销并便于单测。

状态基类采用**模板方法**统一 `Enter / Update / Exit` 生命周期；`Player_GroundedState` / `Player_AiredState` 构成**分层状态机**中间层，承载地面与空中的共有逻辑。全局中断输入（冲刺、反击、领域展开）在 `PlayerState.Update` 基类统一处理，不可用状态以黑名单排除，新增状态默认即支持这些操作。

攻击判定时机由 **Animator 动画事件**驱动而非计时器，实现表现层与逻辑层解耦。

### 2. 事件总线与接口隔离

三级事件体系：实体级（`OnHealthUpdate`）、系统级（`OnInventoryUpdated`）、**全局静态总线**（`QuestEvents`）共 53 处 C# 事件。

全局总线使玩法层与任务层完全解耦——`Enemy` 在死亡时、`ItemAbout` 在拾取时、`NPCBehaviour` 在对话时仅上报静态事件，任务系统订阅后自行匹配目标，**新增任务目标类型无需改动战斗代码**。

配合 `IDamgable` / `ICounterable` / `IEnemyAffix` / `ILootable` 等细粒度接口隔离各系统。

### 3. 双通道修饰符属性系统

属性最终值按 `(基础值 + Σ固定值) × (1 + Σ百分比)` 计算，固定值与百分比分属两条独立通道，保证结果**与装备穿戴顺序无关**。每个修饰符带来源标记（source key），装备卸下时按来源精确回退，不依赖索引，因此中间插入或移除其他来源不会错位。

### 4. 时间膨胀与逐实体时间缩放

顿帧系统分双轨：全局侧调整 `Time.timeScale`，局部侧以**逐目标字典**管理各自的 `AnimationCurve` 恢复进度，仅冻结攻击者与受击者。

恢复曲线为三段式：冻结（速度 0）→ 慢恢复（曲线 0→0.3）→ 快恢复（0.3→1.0）。使用 `unscaledDeltaTime` 计时以避免时间缩放失效，进出各保存还原动画速度、线速度与刚体约束。

### 5. 存档的 ID 间接层

`JsonUtility` 无法可靠序列化 `ScriptableObject` 引用（存对象引用重新编译即断，存 `GetInstanceID` 每次运行都变）。因此存档只写入**稳定业务 ID 字符串**，读档时通过 `ItemLookup` 间接层反查重建，使存档与资产解耦。

恢复顺序按**依赖关系**排列（本质为拓扑排序）：技能必须先于背包（背包容量由被动技能决定），满血必须置于最后（否则会被恢复前的旧上限 `Clamp`）。程序化生成的装备词缀存最终结果而非随机种子，保证读档精确还原且可对代码演进免疫。

### 6. 数据驱动的程序化生成

- **加权轮盘赌 + 距离衰减**实现稀有度浮动，加成仅作用于向上浮动且随步长递减
- **泛型策略参数化**的 `AffixSelector` 让装备词缀与精英词缀共用同一套加权采样逻辑
- **稀有度调性表**决定词缀种类构成（史诗仅正面、稀有全双刃），而非单纯数值放大
- `IEnemyAffix` 定义词缀生命周期契约，运行时 `AddComponent` 注入，**新增词缀无需修改战斗代码**

### 7. 编辑器工具

**任务树可视化编辑器**（`Tools → 任务树编辑器`）基于 Unity GraphView 与 UI Toolkit 构建，支持画布连线、BFS 分层自动布局与资产回写。图**不落盘**，而是单向投影为 ScriptableObject 的 `prerequisiteQuestIds` 与 `followUpQuestIds` 字段，使资产成为唯一事实源，避免图与数据不同步。

另含背包与装备槽的可视化编辑器、CSV 导入工具窗口。

---

## 运行方式

**环境要求**：Unity 6000.4.8f1 或更高版本。

1. 克隆仓库

   ```bash
   git clone https://github.com/GuoZiOY/Metroidvania-style-side-scrolling-fighting-game.git
   ```

2. 本项目使用 **Git LFS** 管理美术、音频等二进制资源，克隆前请确保已安装：

   ```bash
   git lfs install
   ```

3. 用 Unity Hub 打开项目根目录，等待资源导入完成。

4. 打开 `Assets/Scenes/主菜单.unity` 并运行。

**场景说明**

| 场景 | 内容 |
|---|---|
| `主菜单.unity` | 入口场景，含存档槽位选择 |
| `level0.unity` / `level1.unity` | 关卡场景 |
| `BOSS.unity` | 史莱姆王 Boss 战 |

---

## 数据配置管线

游戏内容通过 **Excel → CSV → ScriptableObject** 管线配置，策划无需接触代码。

**工作流**

1. 在 `Assets/Resources/CSV/` 下编辑 `.xlsx` 表格
2. 打开 Unity，执行 `Tools → CSV导入 → 一键同步并导入 _F5`

该菜单会先调用 `sync_xlsx_to_csv.ps1` 将 Excel 同步为 CSV（含 GBK→UTF8 编码转换与前导零 ID 处理），再依次导入各配置表。

**配置表**

| 文件 | 内容 |
|---|---|
| `Items.csv` | 物品基础数据（66 项） |
| `Equipment.csv` | 装备底材与属性 |
| `EquipmentAffixes.csv` | 装备词缀（114 条） |
| `LootTables.csv` | 掉落表 |
| `CraftingRecipes.csv` | 制作配方（49 条） |
| `Consumables.csv` | 消耗品 |
| `Quests.csv` | 任务 |
| `Entities.csv` | 敌人与 NPC 实体 |
| `StatSetups.csv` | 属性配置 |

**单独导入**：`Tools → CSV导入 → 导入窗口` 可按表分别导入。

> `sync_xlsx_to_csv.ps1` 依赖 Windows 与本机安装的 Excel，仅用于开发期同步。

---

## 项目结构

```
Assets/
├── Scripts/
│   ├── Character/
│   │   ├── StateMachine/      状态机核心（StateMachine / EntityState / PlayerState / EnemyState）
│   │   ├── Entity/            实体基类（Entity / Entity_Health / Entity_Combat / Entity_Stats / Entity_StatusHandler）
│   │   ├── Player/            玩家总装与全部玩家状态
│   │   ├── Enemy/             敌人与 Boss（含 Affixes/ 16 种词缀）
│   │   ├── StatSystem/        双通道修饰符属性系统
│   │   └── ExperienceSystem/  经验与等级
│   ├── SkillSyetem/           技能系统（Skill/ 技能逻辑、SkillObject/ 运行时实体）
│   ├── Others/
│   │   ├── ItemSystem/        背包、装备、仓库、战利品、制作/分解/合成
│   │   ├── Area/              区域刷怪、难度、传送门
│   │   └── AbilityGate/       能力门控（规划中）
│   ├── QuestSystem/           任务逻辑与事件总线
│   ├── SaveSystem/            存档、数据定义、ItemLookup 间接层
│   ├── UI/                    全部界面面板
│   ├── Data/                  ScriptableObject 数据定义
│   └── Editor/CSVImport/      CSV 导入工具
├── Editor/QuestTreeEditor/    任务树可视化编辑器
├── Resources/
│   ├── CSV/                   配置表源文件
│   └── Data/                  生成的配置资产（114 个）
├── Scenes/                    场景
├── Prefab/                    预制体
└── Art/                       美术与音频资源
```

---

## 已知缺陷

- 存档未持久化 Boss 击败状态与传送门激活状态
- 存档写入非原子，无版本迁移逻辑
- 商店在背包容量不足时存在退款重复计算
- 冰霜抗性未作用于减速持续时间
- 部分中文注释因历史编码问题显示为乱码

**输入系统说明**

项目当前使用 Unity 旧版 `Input` API，并封装了自研键位映射层（`GameInput`），通过 `Dictionary<Action, KeyCode>` 统一输入入口，实现集中式输入阻塞与键位重绑定的 PlayerPrefs 持久化。

---

## 声明

本项目为**个人学习项目**，是对已有网络教学资源的复刻与实现，仅用于学习交流，不作商业用途。美术与音频素材版权归原作者所有。
