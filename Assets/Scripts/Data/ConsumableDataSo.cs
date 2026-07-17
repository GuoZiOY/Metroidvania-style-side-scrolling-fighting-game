using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(menuName = "RPG设置/物品数据/消耗品数据", fileName = "ItemData-Consumable-")]
public class ConsumableDataSo : ItemDataSo
{
    [Header("消耗品设置")]
    public ConsumableType consumableType;//消耗品类型
    public ConsumableEffectType effectType;//效果类型

    [Header("效果数值")]
    public float effectValue;//效果数值（如恢复HP数量）
    public float effectDuration;//效果持续时间（秒，用于增益效果）

    [Header("属性增益设置（仅当效果类型为属性增益时生效）")]
    public StatType buffStatType;//要增益的属性类型

    [Header("使用限制")]
    public int cooldownTime;//冷却时间（秒）
}
