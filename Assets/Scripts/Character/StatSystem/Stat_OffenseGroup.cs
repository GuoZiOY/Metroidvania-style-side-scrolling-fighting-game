using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[Serializable]
public class Stat_OffenseGroup 
{
    public Stat attackSpeed;//攻速

    public Stat phyiscalDamage;//物理伤害
    public Stat critChance;//暴击率
    public Stat critPower;//暴击伤害
    public Stat armorReduction;//物理穿透
    [Space]
    public Stat elementalHeart;//元素之心
    public Stat fireDamage;//火焰伤害
    public Stat iceDamage;//冰冻伤害
    public Stat lightningDamage;//雷电伤害
}
