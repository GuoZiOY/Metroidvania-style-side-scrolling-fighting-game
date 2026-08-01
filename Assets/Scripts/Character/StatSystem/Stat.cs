using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Stat
{
    [SerializeField] private float baseValue;
    [SerializeField] private List<StatModifier> modifiers = new List<StatModifier>();//修饰符列表，存储所有修饰符

    private float finalValue;

    public float GetValue()
    {
        finalValue = GetFinalValue();//计算

        return finalValue;//返回最终值
    }

    public void AddModifier(float value, string source, bool isPercentage = false)//添加修饰符（isPercentage=true 表示百分比乘区）
    {
        StatModifier modToAdd = new StatModifier(value, source, isPercentage);
        modifiers.Add(modToAdd);
    }

    public void RemoveModifier(string source)//移除修饰符
    {
        modifiers.RemoveAll(modifier => modifier.source == source);
    }

    // 最终值 = (基础值 + 固定值修饰符之和) × (1 + 百分比修饰符之和)
    // 固定值先加，百分比统一乘——百分比词缀（+8%物伤）才能正确生效
    private float GetFinalValue()
    {
        float finalValue = baseValue;
        float percentageSum = 0f;

        foreach (var modifier in modifiers)
        {
            if (modifier.isPercentage)
                percentageSum += modifier.value; // 百分比：累加乘区
            else
                finalValue += modifier.value;    // 固定值：直接加
        }

        return finalValue * (1f + percentageSum);
    }

    public void SetBaseValue(float value) => baseValue = value;
    public float GetBaseValue() => baseValue;

    public void ApplyMultiplier(float multiplier) // 应用倍率
    {
        baseValue *= multiplier;
    }

    public void AddBaseValue(float value) // 添加基础值
    {
        baseValue += value;
    }

}
[Serializable]
public class StatModifier
{
    public float value;//数值
    public string source;//来源
    public bool isPercentage;//是否百分比乘区（true=按百分比加成, false=固定值加法）

    public StatModifier(float value, string source, bool isPercentage = false)
    {
        this.value = value;
        this.source = source;
        this.isPercentage = isPercentage;
    }
}
