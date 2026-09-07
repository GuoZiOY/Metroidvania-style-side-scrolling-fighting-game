---
status: created
date: 2026-07-29
source: /map-systems 破碎之城 MVP
---

# 系统索引 — 破碎之城

## 概览

| 指标 | 数量 |
|------|------|
| 总系统数 | 31 |
| 已有(需增强) | 17 |
| 新增 | 14 |
| MVP | 7新增 + 17增强 |
| Target | 5新增 + 内容扩展 |
| Full Vision | 2新增 + Boss Rush |

---

## Layer 1: Foundation

| # | 系统 | 新增/已有 | MVP | 设计顺序 | 状态 |
|---|------|-----------|-----|----------|------|
| F1 | 输入系统 | 已有 | ✅ | — | ✅ 已实现 |
| F2 | 存档系统 | 已有 ★ | ✅ | — | ✅ 已实现 |
| F3 | 稀有度系统 | 已有 | ✅ | — | ✅ 已实现 |
| F4 | UI系统 | 已有 ★ | ✅ | — | ✅ 已实现 |

## Layer 2: Core

| # | 系统 | 新增/已有 | MVP | 设计顺序 | 状态 |
|---|------|-----------|-----|----------|------|
| C1 | 战斗系统 | 已有 ★ | ✅ | — | ✅ 已实现 |
| C2 | 技能系统 | 已有 ★ | ✅ | — | ✅ 已实现 |
| C3 | 玩家系统 | 已有 | ✅ | — | ✅ 已实现 |
| C4 | 敌人系统 | 已有 ★ | ✅ | 1 | ✅ Designed — [enemy-system-v2.md](enemy-system-v2.md) |
| C5 | 物品-背包 | 已有 ★ | ✅ | — | ✅ 已实现 |
| C6 | 物品-装备 | 已有 ★ | ✅ | 2 | ✅ Designed — [item-equipment.md](item-equipment.md#v2) |
| C7 | 物品-掉落 | 已有 ★ | ✅ | — | ✅ 已实现 |
| C8 | 物品-商店 | 已有 | ✅ | — | ✅ 已实现 |
| C9 | 物品-消耗品 | 已有 | ✅ | — | ✅ 已实现 |
| C10 | 区域/刷怪 | 已有 ★ | ✅ | — | ✅ 已实现 |
| C11 | 经验/等级 | 已有 | ✅ | — | ✅ 已实现 |
| C12 | 任务系统 | 已有 | ✅ | — | ✅ 已实现 |
| C13 | 音频系统 | 已有 | ✅ | — | ✅ 已实现 |
| **C14** | **精英词缀** | **新增** | **✅** | **3** | ✅ Designed — [elite-affix.md](elite-affix.md) |
| **C15** | **传送门网络** | **新增** | **✅** | **6** | ✅ Designed — [portal-network.md](portal-network.md) |

## Layer 3: Feature

| # | 系统 | 新增/已有 | MVP | 设计顺序 | 状态 |
|---|------|-----------|-----|----------|------|
| **F5** | **装备词缀** | **新增** | **✅** | **4** | ✅ Designed — [item-equipment.md](item-equipment.md#v2) |
| **F6** | **能力锁/门控** | **新增** | **✅** | **5** | ✅ Designed — [ability-gate.md](ability-gate.md) |
| **F7** | **Boss系统** | **新增** | **✅** | **7** | ✅ Designed — [boss-system.md](boss-system.md) |
| **F8** | **中心城镇** | **新增** | **✅** | **8** | ✅ Designed — [hub-town.md](hub-town.md) |
| F9 | 制作系统 | 新增 | ❌ Target | 9 | ✅ Designed — [crafting-system.md](crafting-system.md) |
| F10 | 分解系统 | 新增 | ❌ Target | 10 | ✅ Designed — [crafting-system.md](crafting-system.md#分解系统) |
| F11 | 合成系统 | 新增 | ❌ Target | 11 | ✅ Designed — [crafting-system.md](crafting-system.md#合成系统) |
| F12 | 宝石镶嵌 | 新增 | ❌ Target | 12 | ✅ Designed — [gem-socketing.md](gem-socketing.md) |
| F13 | Boss追踪 | 新增 | ❌ Target | 13 | ✅ Designed — [boss-tracking.md](boss-tracking.md) |

## Layer 4: Content / Polish

| # | 系统 | 新增/已有 | MVP | 设计顺序 | 状态 |
|---|------|-----------|-----|----------|------|
| **C16** | **地图区域×3** | **新增** | **✅** | **最后** | ✅ Designed — [area-design.md](area-design.md) |
| C17 | 地图区域×5-6 | 扩展 | ❌ Target | — | ❌ 未开始 |
| P1 | 世界条件 | 新增 | ❌ Full | — | ✅ Designed — [world-conditions.md](world-conditions.md) |

---

## MVP 设计顺序（推荐）

```
1. 敌人系统增强 (C4)      ← 精英词缀的前置
2. 物品-装备增强 (C6)      ← 装备词缀的前置
3. 精英词缀 (C14)          ← 让战斗差异化
4. 装备词缀 (F5)           ← 让掉落有意义
5. 能力锁/门控 (F6)        ← 银河城核心
6. 传送门网络 (C15)        ← 中心辐射结构
7. Boss系统 (F7)           ← 区域高潮
8. 中心城镇 (F8)           ← 循环锚点
最后 地图区域×3 (C16)      ← 内容填充
```

## 瓶颈系统

| 系统 | 风险 | 缓解 |
|------|------|------|
| 装备词缀 (F5) | 🔴 6个系统依赖 | MVP先行，Target系统后续集成 |
| 敌人系统 (C4) | 🟡 5个系统依赖 | 接口先行，精英/Boss分步实现 |

---

*本文档由 `/map-systems` 生成*
