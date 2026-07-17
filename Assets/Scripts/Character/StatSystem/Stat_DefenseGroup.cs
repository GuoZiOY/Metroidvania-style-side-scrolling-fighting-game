using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[Serializable]
public class Stat_DefenseGroup
{
    //防御属性
    public Stat armor;//护甲值
    public Stat evasion;//闪避率
    [Space]
    //元素抗性
    public Stat fireRes;//火焰元素抗性
    public Stat iceRes;//冰冻元素抗性
    public Stat lightningRes;//雷电元素抗性
}
