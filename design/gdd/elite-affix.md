# 精英词缀系统

> **Status**: Designed
> **Author**: oy + Claude
> **Last Updated**: 2026-07-29
> **Implements Pillar**: Pillar 2 (掉落改变打法)

## Overview

精英词缀系统是 `IEnemyAffix` 接口的具体实现层。在 `EnemySpawner` 生成精英敌人时，从 `AffixDatabase` 按稀有度加权随机选取 1-3 个词缀注入到 Enemy 实例。词缀通过 `OnBattleUpdate()` 在战斗状态中激活行为（AoE、召唤、Buff等），并通过敌人血条上的词缀图标向玩家传达即时威胁信号。

## Player Fantasy

暗黑2 玩家看到"闪电强化+多重射击"精英怪时，在交火前就已调整站位 — 同样的即时威胁识别应发生在这里。词缀图标 + Tier 颜色（蓝/金/红）是"一眼判断危险程度"的信号系统。击败带词缀精英后的掉落光泽是风险的回报。

## Detailed Design

### Core Rules

**AffixSpawner** (挂载到 EnemySpawner):
```
OnSpawnElite(enemy, tier):
  count = GetAffixCount(tier)       // 1-3
  pool = affixDB.GetPool(tier)
  selected = WeightedRandom(pool, count, noDuplicate)
  
  foreach affix in selected:
    instance = affix.CreateInstance()
    instance.OnApplied(enemy)       // 修改stats/VFX
    enemy.ActiveAffixes.Add(instance)
```

**词缀实例生命周期**:
| 阶段 | 触发 | 行为 |
|------|------|------|
| OnApplied | 生成时 | `stat.AddModifier()`、注册VFX、启动协程 |
| OnBattleUpdate | 每帧Battle时 | 光环tick、AoE判定、召唤检测 |
| OnRemoved | 死亡/Destroy | 还原stats、停止协程、清理VFX |

**词缀类别** (每类首批 4-6 个):

| 类别 | 示例词缀 | 效果 |
|------|----------|------|
| 元素 | 火焰光环、冰霜新星、感电充能 | AoE伤害/状态 |
| 防御 | 石肤(+护甲)、回复(再生)、护盾(吸收) | 属性/吸收 |
| 攻击 | 多重射击、狂暴(+攻速)、吸血 | 伤害/攻速/回复 |
| 召唤 | 分身、召唤小怪 | 生成附属实体 |
| 诅咒 | 减速光环、治疗抑制、易伤(+承受伤) | Debuff玩家 |

### States and Transitions

词缀**不修改**敌人状态机结构。注入点: `BattleState.Update()` 末尾调用 `foreach affix: affix.OnBattleUpdate(enemy)`。

```
BattleState.Update():
  原有: 检测+追击+攻击范围判定
  + foreach affix in enemy.ActiveAffixes: affix.OnBattleUpdate(enemy)
```

### Interactions with Other Systems

| 系统 | 方向 | 接口 |
|------|------|------|
| 敌人V2 (C4) | ←上游 | `IEnemyAffix` 接口契约 |
| 敌人系统V1 | ←上游 | Enemy.ActiveAffixes 列表 |
| 掉落系统 (C7) | →下游 | `affix.GetLootBonus()` → LootTable rarityBonus |
| 装备词缀 (F5) | ↔双向 | 同名联动 (如敌"火焰" + 玩家"对灼烧+伤") |
| UI (F4) | →下游 | 血条词缀图标 + 名称显示 |

## Formulas

沿用 `enemy-system-v2.md` 公式体系 (精英判定 → 词缀数量 → 类别加权 → 掉落加成)。

| 参数 | 来源 | 值 |
|------|------|-----|
| affixCount(tier) | enemy-v2 | 1/2/3 |
| lootBonus(tier) | enemy-v2 | 5%/10%/20% (percentage points) |
| categoryWeights | AffixDatabase | 可配置 |
| 词缀数值 | AffixDatabase.affix.range | 可配置 |

## Edge Cases

- **同名词缀重复**: 池选择排除已选ID，若池耗尽 → 跳过剩余分配
- **VFX资源缺失**: 词缀效果保持，VFX跳过 + 日志警告
- **精英被一击秒杀**: OnRemoved在OnDestroy中触发，确保stats还原
- **多精英同时生成**: 每个独立Roll词缀，不共享池状态
- **场景切换/卸载**: Enemy.OnDestroy → RemoveAllAffixes

## Dependencies

| 系统 | 方向 | 硬/软 |
|------|------|-------|
| 敌人系统V2 (C4) | ←上游 | 硬 — IEnemyAffix接口 |
| 敌人系统V1 (C4) | ←上游 | 硬 — Enemy类 |
| 战斗系统 (C1) | ←上游 | 硬 — Entity_Stats, StatusEffect |
| 掉落系统 (C7) | →下游 | 软 — loot bonus |
| 装备词缀 (F5) | ↔双向 | 软 — 同名联动 |
| UI系统 (F4) | →下游 | 硬 — 血条词缀显示 |

## Tuning Knobs

| 参数 | 范围 | 说明 |
|------|------|------|
| 每类词缀池大小 | 4-10 | MVP=4-6 |
| 词缀数值范围 | 见AffixDatabase | 平衡曲线需要测试 |
| 类别权重 | 0-1, 总和=1 | 某类=0则该类不出 |
| 具体词缀权重 | 1-10 | 稀有词缀的值更高 |

## Visual/Audio Requirements

- 精英怪身体Tier光晕: 蓝(精锐) / 金(稀优) / 红(传说)
- 血条上方显示词缀图标行 (最多3个)
- VFX: 火焰光环粒子、冰霜地面贴花、分身生成闪光
- SFX: 词缀VFX对应的持续音效 (光环hum等)

## Acceptance Criteria

- **GIVEN** 一个稀优精英生成，**WHEN** 查看其血条，**THEN** 显示2个词缀图标，金色边框
- **GIVEN** 火焰光环词缀精英在战斗中，**WHEN** 玩家进入光环范围，**THEN** 每秒受到火伤+灼烧状态
- **GIVEN** 传说精英被击杀，**WHEN** 触发掉落，**THEN** LootTable rarityBonus受3个词缀的总加成(+60%)
- **GIVEN** 精英怪被Destroy，**WHEN** 检查playerStats，**THEN** 所有词缀的Modifier已被移除

## Open Questions

1. 词缀图标资源谁来画? → 占位用彩色圆点，后期替换
2. 词缀与词缀之间的元素反应? → 暂不做，留到Target阶段
