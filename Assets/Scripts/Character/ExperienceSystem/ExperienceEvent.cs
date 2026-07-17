using UnityEngine;

public class ExperienceEvent
{
    public int ExperienceAmount { get; private set; } // 经验值
    public ExperienceSourceType SourceType { get; private set; } // 经验来源类型
    public object SourceData { get; private set; } // 来源数据（可选，用于传递额外信息）
    public Vector3? Position { get; private set; } // 经验获取位置（可选，用于特效等）

    public ExperienceEvent(int experienceAmount, ExperienceSourceType sourceType, object sourceData = null, Vector3? position = null) // 构造函数
    {
        ExperienceAmount = experienceAmount;
        SourceType = sourceType;
        SourceData = sourceData;
        Position = position;
    }
}

public class EnemyExpData
{
    public string enemyName; // 敌人名称
    public int enemyLevel; // 敌人等级
    public Vector3 position; // 死亡位置
    public bool wasCounterKill; // 是否反击击杀
    public int comboCount; // 连击数
}

public enum ExperienceSourceType
{
    EnemyDefeated // 击败敌人
}
