using System;
using UnityEngine;

[Serializable]
public class Stat_MajorGroup
{
    public Stat strength;//每点增加0.5点物理伤害并每点提供1%暴击伤害
    public Stat agility;//每点提供0.3%暴击率，每点提供0.3%闪避率
    public Stat intelligence;//每点增加1点魔法伤害，每点+0.005元素穿透，每点提供0.3%元素抗性
    public Stat vitality;//每点+3点最大生命值，每点+0.3点护甲值
}
