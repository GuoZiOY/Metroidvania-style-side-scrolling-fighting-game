# 网络/数据管线系统审查报告

> 审查日期：2026-08-14 | 代码基线：git HEAD `7c7b82c`
> 审查方式：定向深读（NetworkManager / IGameTransport / LiteNetLib 分层）+ 目录全量核查 + CSV 数据管线联动

## 1. 框架结构

### 1.1 网络层核心类

```
Networking/ (Unity 侧):
  NetworkManager    —— 单例, Host/Client/Single 模式入口 (StartHost/StartClient/Stop)
  NetworkObject     —— 网络对象组件
  NetworkBehaviour  —— 网络行为基类
  SpawnSystem       —— 网络生成/销毁同步
  ChatManager       —— 聊天
  NetworkTest       —— 联机测试场景
  NetUtil           —— 工具

LiteNetLib/ (传输层自研封装):
  IGameTransport    —— 传输抽象接口 (StartServer/Connect/Send/事件)
  LiteNetTransport  —— 基于 ReliableChannel + ConnectionManager + NetworkClient 的实现
  Connection/       —— ConnectionManager + NetworkClient (连接生命周期)
  Reliability/      —— ReliableChannel (序列号 + ACK + 超时重传)
  Dispatch/         —— 消息分发
  Protocol/         —— 协议定义
```

### 1.2 分层设计
- **接口隔离**：`IGameTransport` 定义 Server/Client 双向接口 + 事件（OnServerDataReceived/OnClientDataReceived 等），上层不依赖底层实现；`RegisterTransport<T>` 可切换 LiteNetTransport/未来 SteamTransport
- **可靠通道**：`ReliableChannel` 序列号+ACK+超时重传（自研，非 LiteNetLib 第三方库而是项目内封装）

### 1.3 数据管线
```
Assets/Resources/CSV/ (xlsx 源文件 → csv 导出):
  Items.csv / Equipment.csv / EquipmentAffixes.csv / LootTables.csv
  Quests.csv / Consumables.csv / CraftingRecipes.csv / Entities.csv
  ↓ Editor CSVImport 工具 (Assets/Scripts/Editor/CSVImport/)
  ↓
ScriptableObject 数据资产 (Assets/Resources/Data/ 114 个)
```

## 2. 工作流程

### 2.1 网络连接（Host/Client）
1. `NetworkManager.StartHost(port)` → `LiteNetTransport.StartServer` + `ConnectAsLocal` 环回
2. 客户端 `Connect(address, port)` → `ConnectionManager` 建立 → 事件 `OnClientConnected`
3. `Update()` 每帧驱动 `_transport.Update()`（心跳 + 重传定时器）

### 2.2 可靠消息
1. 发送：`SendToClient/SendToServer(data, reliable)` → `ReliableChannel` 分配序列号
2. 接收端 ACK → 发送端超时未 ACK 重传
3. `Dispatch/` 按消息类型分发 → NetworkObject/Behaviour 响应

### 2.3 数据导入
1. 策划编辑 xlsx → 导出 csv → `LootTableCSVImporter` 等 Editor 工具导入 → SO 资产
2. 运行时 `Resources.Load` 加载 SO（存档/掉落/技能等）

## 3. 信息链路

| 链路 | 方向 | 说明 |
|------|------|------|
| 网络事件 | IGameTransport → NetworkManager → 上层 | OnServerDataReceived/OnClientConnected 等 |
| 消息分发 | Dispatch/ → NetworkObject/Behaviour | 按协议类型路由 |
| **玩法接入** | ⚠️ 网络 ↔ 核心玩法 | **基本未接线**：仅 13 处引用（ChatManager/NetworkTest 等），战斗/物品/任务不参与网络同步 |
| 数据管线 | xlsx → csv → Editor 导入 → SO → Resources.Load | 单向数据流完整 |

## 4. 与设计文档一致性

| 设计承诺 | 实现状态 |
|----------|----------|
| IGameTransport 抽象传输层 | ✅ 落地（接口 + LiteNetTransport 实现） |
| 可靠消息（序列号+ACK+超时重传） | ✅ 落地（ReliableChannel） |
| NetworkObject/NetworkBehaviour/SpawnSystem | ✅ 存在 |
| Host/Client/Single 三模式 | ✅ 落地 |
| **网络 GDD** | ❌ **无任何网络设计文档**（游戏系统解剖报告提及"网络联机支持"但无 GDD/架构章节） |
| **玩法联机** | ❌ 未接入——MVP 单机架构，网络层为独立技术储备 |

## 5. 代码质量

- ✅ `IGameTransport` 接口设计清晰（Server/Client 双向 + 事件 + Update 驱动）；NetworkManager 单例防御完整
- ✅ 数据管线标准化（xlsx→csv→SO 单向流 + Editor 工具）
- ⚠️ **网络层与核心玩法完全脱节**：13 处引用均为测试/聊天；`NetworkTest` 为联机测试场景——是"技术储备"而非"已接入功能"
- ⚠️ `NetworkTest.cs` 大量 `FindAnyObjectByType`（测试代码可接受）
- ⚠️ 数据导入工具仅覆盖部分表（LootTableCSVImporter 等），导入校验（CSV 转义/重复键）不完整

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重
1. **网络层无 GDD/架构决策** — 网络系统是唯一没有设计文档的系统；architecture.md 平台层未提网络；游戏系统解剖报告将其列为亮点但无实现承诺。
   - 改进：先决定网络定位——若 MVP 单机，将网络标记为"远期技术储备"并在 architecture.md 记录决策；若目标联机，需补网络 GDD（同步模型/消息协议/状态权威）再接线。
2. **网络与玩法脱节（技术储备状态）** — 战斗/物品/任务/玩家状态均无网络同步；`SpawnSystem` 等无实际消费者。
   - 改进：与 1 一并决策——短期不接线则冻结该层（防无效维护）；长期接线需定义同步范围（谁 authoritative）。

### 🟡 中等
3. **数据管线缺校验与文档** — CSV 导入无完整校验（逗号/转义/重复键）；`Assets/Resources/CSV` 的 xlsx 与 csv 双份源需明确唯一权威。
   - 改进：xlsx 为唯一权威 + 导入工具加校验日志；`Resources/CSV` 中的 csv 视为导出产物。
4. **Resources.Load 全量加载** — 技能/物品数据每次读档全量加载（与 Addressables 迁移计划衔接，architecture 已标 MEDIUM）。
   - 改进：按迁移计划推进 Addressables 或至少建静态缓存。

### 🟢 轻微
5. `NetworkTest.cs` 测试入口无开关保护（正式包中可能残留）。
6. `ChatManager` 与 UI_Chat 耦合（Y 键 toggleKey 冲突，见 input-audio-vfx.md S-2）。
7. 网络事件未统一消息序列化方案（byte[] 手写协议 vs MessagePack 等）——远期决策。

---

## 附：审查结论摘要

- **健康度**：网络层为"技术储备"——`IGameTransport` 抽象 + ReliableChannel 可靠传输实现扎实，但**与核心玩法零接线、无 GDD**；数据管线（xlsx→csv→SO）标准化良好。
- **最需优先**：决策网络定位（单机 MVP 冻结 vs 联机规划），补网络 GDD/架构记录；短期冻结避免无效维护。
- **最值得肯定**：`IGameTransport` 传输抽象 + `RegisterTransport<T>` 可替换设计（LiteNetTransport → SteamTransport）是干净的架构预留，未来接入时无需改动上层。
