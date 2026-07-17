using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SkillUpgradeType
{
    None,

    FlashDash,
    Dash_Blur,
    Dash_Storage,//冲刺存储
    Dash_CloneOnStart,//冲刺开始时创建一个残影。Dash 
    Dash_CloneOnStartAndArrival,//冲刺开始和结束时创建残影
    Dash_ShardOnStart,//冲刺开始时生成碎片
    Dash_ShardOnStartAndArrival,//冲刺开始和结束时生成碎片

    Shard, //碎片在敌人死亡时会在敌人位置爆炸
    Shard_MoveToEnemy, //碎片会向敌人的位置移动
    Shard_Multicast, //碎片可以命中N个敌人。如果命中全部施法
    Shard_Teleport, //玩家可以传送到碎片的位置
    Shard_TeleportHpRewind, //玩家传送到碎片位置时生命值百分比会恢复到使用碎片时的状态

     // ------ Time Echo -------
    TimeEcho,  // 创建玩家的克隆体，克隆体会对敌人造成伤害
    TimeEcho_SingleAttack, // 时间回响只会执行一次攻击
    TimeEcho_MultiAttack, // 时间回响会执行N次攻击
    TimeEcho_ChanceToDuplicate, // 时间回响在攻击时有几率额外生成一个时间回响。
    TimeEcho_HealWisp, // 当时间回响消失时会生成一个治疗精灵飞向玩家，可以恢复生命值，同时也会对敌人造成伤害的百分比
    TimeEcho_CleanseWisp, // 治疗精灵在接触时移除身上的负面效果
    TimeEcho_CooldownWisp, // 治疗精灵使用时会让技能的冷却时间减少N秒。 

     // ------ Domain Expansion -------
    Domain_SlowDown, // 领域展开时会减缓敌人
    Domain_EchoSpam, // 领域展开时会生成时间回响。
    Domain_ShardSpam, // 领域展开时会生成时间碎片。

    Fire,


    Ice,


    Lighting,

    DoubleJump,

    ElementalMastery,

    PowerCounterChase,
}
