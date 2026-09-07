# 宝石镶嵌系统

> **Status**: Designed | **Date**: 2026-07-29 | **Pillar**: Pillar 2

## Overview

宝石镶嵌为每个已解锁的技能槽位提供 1 个宝石孔。玩家可将宝石嵌入槽位，宝石提供属性加成（力量/敏捷/智力/活力）或特殊效果（冷却缩减、技能范围扩大、额外元素伤害）。宝石有 5 个稀有度等级，3 颗同等级宝石合成 1 颗高一级宝石。

## Player Fantasy

暗黑2 "完美的红宝石"——打到一颗好宝石时立刻想"这能放哪个技能上？"。宝石是 Build 定制的最后 10%——装备和词缀定义了 Build 方向，宝石补充细节。

## Detailed Design

### Core Rules

**宝石数据结构**:
```
GemData (ScriptableObject):
  gemId, gemName, icon, tier (1-5)
  statBonus: StatType?          // 属性加成 (Strength等)
  statValue: float              // 基础值
  specialEffect: GemEffect?     // 特殊效果
```

**宝石效果类型**:
| 类型 | 效果 |
|------|------|
| 属性宝石 | +N 力量/敏捷/智力/活力 |
| 冷却宝石 | -X% 技能冷却 |
| 范围宝石 | +X% 技能范围/AoE |
| 元素宝石 | +X% 对应元素伤害 |
| 吸血宝石 | +X% 技能伤害转生命 |

**槽位与镶嵌**:
```
SkillSlot (已有 SkillSlotManager 的 5 个槽位):
  每槽位 1 个宝石孔

镶嵌: 拖宝石到技能槽位 → 宝石绑定到该技能
  效果: stat.AddModifier(gem.statValue, gem.gemId)
        + 特殊效果通过 Skill_Base 接口应用

拔出: 右键槽位 → 宝石回到背包
  RemoveModifier(gem.gemId)
```

### Interactions

| 系统 | 方向 | 接口 |
|------|------|------|
| 技能系统 (C2) | ← | SkillSlotManager 提供槽位引用 |
| 物品-背包 (C5) | ↔ | 宝石作为特殊物品存储 |
| 属性系统 | → | AddModifier / RemoveModifier |
| 合成系统 (F11) | ← | 3同合一升级 |
| 掉落 (C7) | ← | 宝石加入掉落表 |
| UI | → | 宝石槽位可视化 |

## Formulas

```
宝石属性值: statValue × tier
  tier 1=1.0, tier2=1.5, tier3=2.0, tier4=2.5, tier5=3.0

宝石升级(3合1): 3×tierN → 1×tier(N+1)
  合成费用 = tier² × 100
```

## Edge Cases

- **同槽已有宝石**: 拔出旧宝石→嵌入新宝石（不覆盖）
- **宝石从已绑定技能移出**: 效果立即移除
- **技能被退回(Refund)时宝石**: 宝石自动回到背包

## Dependencies

| 系统 | 方向 | 硬/软 |
|------|------|-------|
| 技能系统 (C2) | ← | 硬 |
| 物品-背包 (C5) | ↔ | 硬 |
| 合成系统 (F11) | ← | 软 — 宝石升级 |
| 掉落 (C7) | ← | 软 — 宝石来源 |

## Tuning Knobs

| 参数 | 范围 | 说明 |
|------|------|------|
| 宝石tier值(1-5) | 1.0-3.0× | tier5=3×tier1 |
| 特殊效果强度 | 5-25% | 冷却缩减等百分比值 |
| 宝石升级费用 | tier²×100 | tier1→2=100, 4→5=1600 |

## Acceptance Criteria

- **GIVEN** 技能槽有空宝石孔和背包有宝石，**WHEN** 拖宝石到孔，**THEN** 宝石嵌入+属性生效
- **GIVEN** 槽位有宝石，**WHEN** 右键拔出，**THEN** 宝石回背包+属性移除
- **GIVEN** 3颗tier2红宝石，**WHEN** 合成，**THEN** 产出1颗tier3红宝石

## Open Questions

1. 宝石是否应该有"职业/技能限定"(只能嵌入特定类型技能)? → 暂不限定
