---
name: project-affix-redesign
description: 2026-08-01 词缀表重写 — 新6列格式、双刃/垃圾词缀、池×档位候选≥2 保底约束
metadata:
  type: project
---

2026-08-01 将 `Assets/Resources/CSV/EquipmentAffixes.csv` 从旧 8 列格式重写为新 6 列格式：
`affixId,displayName,tier,pool,stats,weight`，stats 段为 `属性名:min:max:pct`（`/` 分隔多段，段数≤4）。

**Why:** 用户新设计规则要求：新增双刃词缀（史诗/传说，≥1正+≥1负，负面≈正面40-70%，每池≥3条）、纯垃圾词缀（普通/优秀，全负，权重2，每池≥2条）；保底机制要求 每个池×每个稀有度档位 前缀/后缀候选≥2（档位=稀有度-1级 到 稀有度级）。

**How to apply:** 数值基准是 `design/gdd/balance-design.md` §6（正面数值只做格式转换+补齐缺口）。全伤% 按 balance-design §2 展开为 物理/火/冰/雷 4 段（占满4段，故双刃正面只能用单/双属性留出负面段）。当前表 93 条词缀，全部档位候选≥2。注意：优秀(t1)垃圾词缀在 稀有(2) 档位(t1-2)仍可被选出（权重2低频），如需严格过滤需将垃圾全设为普通(t0)。

关联：[[equipment-affix-importer-mismatch]]
