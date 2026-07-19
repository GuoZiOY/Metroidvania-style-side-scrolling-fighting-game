using System.Collections.Generic;
using UnityEngine;

public enum QuestType { Main, Side }

public enum ObjectiveType
{
    Kill,    // 击杀指定敌人
    Collect  // 收集指定物品
}

[System.Serializable]
public class ObjectiveConfig
{
    public ObjectiveType type;
    public string targetId;         // 敌人 ID 或 物品 ID
    public int requiredCount = 1;
}

[System.Serializable]
public class RewardItem
{
    public ItemDataSo itemData;
    public int amount = 1;
}

[System.Serializable]
public class QuestReward
{
    public int expAmount;
    public int skillPoints;
    public List<RewardItem> items;
}

[CreateAssetMenu(menuName = "RPG设置/任务系统/任务数据", fileName = "QuestData -")]
public class QuestData : ScriptableObject
{
    public string questId;
    public string questName;
    public QuestType questType;
    [TextArea] public string description;

    public List<ObjectiveConfig> objectives;
    public QuestReward reward;

    [Header("前置条件")]
    public List<string> prerequisiteQuestIds;
}
