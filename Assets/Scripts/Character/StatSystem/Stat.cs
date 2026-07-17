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
    //private bool needToCalcualte = true;//需要重新计算值


    public float GetValue()
    { 
        finalValue = GetFinalValue();//计算

        return finalValue;//返回最终值
    }

    public void AddModifier(float value, string source)//添加修饰符
    {
        StatModifier modToAdd = new StatModifier(value, source);
        modifiers.Add(modToAdd);
        //needToCalcualte = true;
    }

    public void RemoveModifier(string source)//移除修饰符
    {
        modifiers.RemoveAll(modifier => modifier.source == source);
       //needToCalcualte = true;
    }

    private float GetFinalValue()//得到最终值
    {
        float finalValue = baseValue;

        foreach (var modifier in modifiers)//循环遍历所有修饰符，计算最终值
        {
            finalValue = finalValue + modifier.value;
        }

        return finalValue;//返回
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
    public float value;//ֵ
    public string source;//��Դ

    public StatModifier(float value, string source)
    {
        this.value = value;
        this.source = source;
    }
}