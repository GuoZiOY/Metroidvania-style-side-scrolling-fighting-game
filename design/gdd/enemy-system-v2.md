# 敌人系统增强 V2 — 精英词缀 + Boss框架

> **Status**: In Design
> **Author**: oy + Claude
> **Last Updated**: 2026-07-29
> **Implements Pillar**: Pillar 2 (掉落改变打法) + Pillar 4 (掌握→力量→世界)

## Overview

敌人系统增强 V2 在已有 8 状态机、类型系统和等级缩放的基础上，新增两个子系统模块：

1. **精英词缀 (Elite Affix)** — 运行时注入到 Enemy 实例的随机 Buff 组件。每个精英敌人获得 1-3 个词缀（如"火焰光环""吸血""分身"），词缀影响战斗行为、属性数值和掉落品质。通过 `IEnemyAffix` 接口契约与现有 Enemy 类解耦。

2. **Boss 框架 (Boss Framework)** — 继承 Enemy 的多阶段战斗抽象层。每个 Boss 有 2-4 个阶段，阶段切换由 HP 阈值或机制触发。Boss 拥有独立掉落表、独特词缀池和演出级视觉反馈。

两个模块共用 `IEnemyAffix` 接口，向下兼容所有现有 Enemy 子类，不影响 Normal 类型敌人行为。增强后的敌人系统是装备词缀(F5)、Boss追踪(F13)和地图区域(C16)的前置依赖。

## Player Fantasy

**精英词缀**: 玩家看到一个带火焰光环的精英怪冲过来时，生理反应应该是"这不是普通怪，我需要改变策略"——而不是"换了个皮肤"。词缀的特效/行为变化是即时的威胁信号，击杀后的掉落光泽是风险的回报。

**Boss 战**: 第一次遇到 Boss 时的压迫感、被未知机制击败后的困惑、分析阶段模式后的适应、最终击败时的释放——这是银河恶魔城 Boss 战的完整情感弧线。击败 Boss 不仅获得独特装备，还是"我已掌握这个区域"的能力证明（Pillar 4: 掌握带来力量）。

**参考体验**: 空洞骑士的螳螂领主——战前铺垫→初次被碾压→学习节奏→完美反击→成就感。暗黑2 的精英怪词缀——看到"闪电强化+多重射击"的一刻就已经开始调整站位。

## Detailed Design

### Core Rules

**设计决策**: 词缀采用 `IEnemyAffix` 接口而非 ScriptableObject 纯数据方案，因为"分身""火焰光环""吸血"等词缀行为差异巨大——纯数据无法表达生成附属实体、AoE范围判定等复杂逻辑。简单数值 Buff 类词缀可通过 `StatAffixBase` 抽象基类减少重复代码。

**IEnemyAffix 接口**:
```csharp
public interface IEnemyAffix
{
    string AffixId { get; }
    string DisplayName { get; }
    AffixTier Tier { get; }
    void OnApplied(Enemy enemy);
    void OnRemoved(Enemy enemy);
    void OnBattleUpdate(Enemy enemy);
    string GetTooltipText();
}
```

**词缀等级与数量**:
| 精英等级 | 词缀数 | Tier分布 |
|----------|--------|----------|
| 精锐 (Rare) | 1 | 普通 |
| 稀优 (Elite) | 2 | 普通+普通 或 稀有+普通 |
| 传说 (Legendary) | 3 | 至少1个传说 |

**词缀类别（首批 MVP，每类 3-5 个）**:

| 类别 | 示例 | 效果类型 |
|------|------|----------|
| 元素 (Elemental) | 火焰光环、冰霜新星、感电充能 | 范围伤害/状态 |
| 防御 (Defensive) | 石肤、回复、护盾 | 属性/再生 |
| 攻击 (Offensive) | 多重射击、狂暴、吸血 | 伤害/攻速 |
| 召唤 (Summon) | 分身、召唤小怪 | 生成附属实体 |
| 诅咒 (Curse) | 减速光环、治疗抑制、易伤 | Debuff玩家 |

**Boss 框架**:
```
BossPhase:
  phaseIndex: int
  hpThresholdStart: float     // 进入此阶段的HP% (如 1.0, 0.6, 0.3)
  activeMechanics: Mechanic[] // 此阶段激活的机制
  onEnter/onExit 事件

BossConfig (ScriptableObject):
  phases: BossPhase[]
  uniqueDropTable: LootTable
  staggerThreshold: float

阶段切换 (HP阈值触发):
  Enemy.TakeDamage() → HP降至阈值以下 → BossPhaseManager.Transition()
  1. 触发 onExit (旧阶段) → 清理机制
  2. 播放过渡动画 (无敌帧)
  3. 触发 onEnter (新阶段) → 激活新机制
  4. 恢复战斗
```

### States and Transitions

**精英词缀不影响现有状态机结构** — 词缀通过 `OnBattleUpdate()` 在 Battle/Attack 状态中注入行为：
```
BattleState.Update():
  原有逻辑 (检测+追击+攻击范围)
  + foreach affix in activeAffixes: affix.OnBattleUpdate(this)
```

**Boss 增加 PhaseTransition 状态**:
```
any Boss state → HP达阈值 → PhaseTransition (无敌+动画)
  → onEnter新阶段 → 返回 BattleState
```

### Interactions with Other Systems

| 交互系统 | 数据流向 | 接口 |
|----------|----------|------|
| 战斗系统 → 词缀 | 词缀修改 `Entity_Stats`、施加 `StatusEffect` | `OnApplied(enemy)` |
| 词缀 → 掉落系统 | 词缀等级影响稀有度 bonus | `Affix.GetLootBonus()` |
| Boss → 掉落系统 | Boss独立掉落表，击败触发 | `BossConfig.uniqueDropTable` |
| 词缀 → UI | 精英血条显示词缀图标+名称 | `DisplayName + Tier` |
| 装备词缀 → 敌人 | 同名词缀互动（如"对灼烧敌人+伤"） | 通过 ElementType 桥接 |

## Formulas

### 精英等级判定
```
eliteRoll = Random.value
if eliteRoll < eliteBaseChance + areaDifficulty.GetEliteChanceBonus():
  tierRoll = Random.value
  tier = tierRoll < legendaryChance ? Legendary
       : tierRoll < eliteChance ? Elite
       : Rare
```

### 词缀数量
```
affixCount = tier switch {
  Rare → 1
  Elite → 2
  Legendary → 3
}
```

### 词缀选择 (加权随机, 按类别池)
```
for i in affixCount:
  category = SelectWeighted(categoryWeights[tier])
  affix = SelectWeighted(affixPools[category])
  // 不可重复选择同一词缀
```

### 词缀掉落加成
```
lootBonus = Σ affix.GetLootBonus()
每词缀贡献: 普通=5%, 稀有=10%, 传说=20%
最终 rarityBonus = areaDifficulty.GetRarityBonus() + lootBonus
```

### Boss 阶段 HP 阈值
```
2阶段: [1.0, 0.5]
3阶段: [1.0, 0.6, 0.3]
4阶段: [1.0, 0.7, 0.4, 0.15]
// 阶段数由 BossConfig.phases.Length 决定
```

### Boss 硬直阈值
```
单次伤害 > maxHP × staggerThreshold (默认 0.08 = 8%)
硬直冷却: 15s
```

**变量表**:
| 变量 | 类型 | 范围 | 默认值 | 描述 |
|------|------|------|--------|------|
| eliteBaseChance | float | 0.05–0.3 | 0.1 | 基础精英生成率 |
| legendaryChance | float | 0.01–0.1 | 0.05 | 传说概率 |
| eliteChance | float | 0.05–0.25 | 0.15 | 稀优概率 |
| categoryWeights | float[] | 0–1 | 见Tuning Knobs | 每Tier的类别权重 |
| lootBonus(普通) | float | 0–0.15 | 0.05 | 普通词缀掉落加成 |
| lootBonus(稀有) | float | 0–0.25 | 0.10 | 稀有词缀掉落加成 |
| lootBonus(传说) | float | 0–0.40 | 0.20 | 传说词缀掉落加成 |
| staggerThreshold | float | 0.03–0.15 | 0.08 | Boss硬直阈值(HP%) |

## Edge Cases

- **若词缀池为空**: 不分配词缀，精英退化为普通怪+属性加成。日志警告。
- **若Boss阶段数设为1**: 不创建PhaseTransition状态，按普通敌人逻辑运行。
- **若HP阈值低于0**: 该阶段永远不触发。BossConfig验证阈值必须>0且严格递减。
- **若词缀生成分身但场景容量已满**: 不生成分身，该词缀的其他效果保持。
- **若词缀被Remove时仍在协程中**: OnRemoved负责StopAllCoroutines。
- **若精英怪被Destroy但词缀未Remove**: Enemy.OnDestroy调用`RemoveAllAffixes()`。
- **Boss阶段切换期间玩家死亡**: Boss不重置阶段，保持当前HP和阶段状态。
- **同一词缀被赋予两次**: 池选择时排除已选ID。若只剩已选词缀则跳过分配。

## Dependencies

| 依赖系统 | 方向 | 硬/软 | 接口 |
|----------|------|-------|------|
| 战斗系统 (C1) | ←上游 | 硬 | `IDamgable`, `Entity_Stats`, `Entity_StatusHandler` |
| 敌人系统V1 (C4) | ←上游 | 硬 | `Enemy`, `EnemyState`, `EnemyTypeSystem` |
| 区域/刷怪 (C10) | ←上游 | 硬 | `EnemySpawner.SpawnEnemies()` 传入精英参数 |
| 稀有度系统 (F3) | ←上游 | 软 | `RarityCalculator.GetVariedRarity()` |
| 物品-掉落 (C7) | →下游 | 硬 | 词缀等级→LootTable rarityBonus |
| 精英词缀 (C14) | →下游 | 硬 | `IEnemyAffix` 接口契约 |
| Boss系统 (F7) | →下游 | 硬 | `BossConfig`, `BossPhase` 数据契约 |
| 装备词缀 (F5) | ↔双向 | 软 | 词缀ID同名→特殊互动 |

## Tuning Knobs

| 参数 | 安全范围 | 破坏点 | 影响 |
|------|----------|--------|------|
| eliteBaseChance | 0.05–0.3 | >0.5→精英泛滥 | 精英遭遇频率 |
| legendaryChance | 0.01–0.1 | >0.2→传说贬值 | 传说精英稀有度 |
| categoryWeights | 每类0–1，总和=1 | 某类=0→该类词缀永不出现 | 词缀多样性 |
| 普通词缀lootBonus | 0.03–0.15 | >0.25→普通怪掉落也爆炸 | 掉落品质曲线 |
| staggerThreshold | 0.03–0.15 | <0.03→永不硬直; >0.2→Boss站不起来 | Boss节奏感 |
| Boss阶段数 | 2–4 | 1→没阶段; >4→实现复杂度爆炸 | Boss战深度 |
| 每类词缀池大小 | 3–8 | <3→重复感; >8→平衡失控 | 词缀多样性 |
| 词缀冷却(如有) | 5–30s | 0→词缀行为刷屏 | 战斗可读性 |

## Visual/Audio Requirements

- 精英怪名字上方显示词缀图标行 (最多3个图标, 按Tier着色)
- 精英怪身体发对应Tier的光 (精锐=蓝、稀优=金、传说=红)
- Boss阶段切换: 全屏闪白→Boss变身动画→新阶段VFX激活
- Boss血条: 屏幕顶部专用大血条, 分段显示阶段阈值线
- 词缀行为VFX: 火焰光环用已有Fire VFX, 冰霜新星用Chill VFX, 召唤分身用Dash echo material
- 音频: Boss登场专属BGM片段, 阶段切换SFX, 词缀激活SFX

## Open Questions

1. Boss是否支持"逃跑机制"(HP降到X%后撤退, 后面再遇到)? → 待Phase 2决定
2. 词缀是否需要在存档中持久化 (已生成的精英怪词缀)? → 暂定不持久化, 每次进入区域重新生成
3. Boss Rush模式是否需要独立的Boss数据(不同HP/伤害倍率)? → 留到Full Vision阶段
