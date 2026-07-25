using System.Collections.Generic;
using UnityEngine;

// ===== 任务类型 =====

public enum QuestType
{
    Main,        // 主线
    Side,        // 支线
    Temporary,   // 临时
}

// ===== 目标类型 =====

public enum ObjectiveType
{
    Kill,        // 击杀指定 enemyId 的敌人
    Collect,     // 收集指定 itemId 的物品
    TalkToNPC,   // 与指定 npcId 的 NPC 对话
}

// ===== 接取方式 =====

public enum QuestTrigger
{
    NpcTalk,        // NPC 对话接取
    AutoUnlock,     // 前置满足后自动接取
    ItemPickup,     // 拾取特定物品触发
}

// ===== 目标配置 =====

[System.Serializable]
public class ObjectiveConfig
{
    public ObjectiveType type;
    public string targetId;               // enemyId / itemId / npcId
    public int requiredCount = 1;
    [TextArea] public string description;  // 显示给玩家的文本
}

// ===== 奖励条目 =====

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
    public int goldAmount;
    public List<RewardItem> items;
}

// ===== 任务阶段 =====

[System.Serializable]
public class QuestStage
{
    public string stageId;                          // "stage_1"
    [TextArea] public string description;            // 阶段描述

    public List<ObjectiveConfig> objectives;         // 本阶段目标

    [Header("阶段对话")]
    [TextArea] public string startDialogue;          // 进入阶段时 NPC 说的话
    [TextArea] public string inProgressDialogue;     // 进行中时 NPC 说的话
    [TextArea] public string completeDialogue;       // 完成时 NPC 说的话

    [Header("阶段奖励")]
    public QuestReward stageReward;                  // 阶段完成时的奖励

    [Header("下一阶段")]
    public string nextStageId;                       // 空 = 最终阶段
}

// ===== 任务数据 =====

[CreateAssetMenu(menuName = "RPG设置/任务系统/任务数据", fileName = "QuestData -")]
public class QuestData : ScriptableObject
{
    [Header("基础信息")]
    public string questId;
    public string questName;
    public QuestType questType;
    [TextArea] public string description;

    [Header("触发配置")]
    public QuestTrigger trigger = QuestTrigger.NpcTalk;
    public string triggerNpcId;               // 从哪个 NPC 接取（用于 NpcTalk）
    public string triggerItemId;              // 拾取哪个物品触发（用于 ItemPickup）
    public bool autoAccept;                   // 满足前置后是否自动接取

    [Header("前置条件")]
    public List<string> prerequisiteQuestIds;

    [Header("阶段列表")]
    public List<QuestStage> stages;

    [Header("最终奖励（全部阶段完成后）")]
    public QuestReward finalReward;

    [Header("后续任务")]
    public List<string> followUpQuestIds;

    [Header("完成时设置的世界状态")]
    public List<string> setWorldFlags;

    // ─── 辅助方法 ───

    public QuestStage GetStage(string stageId)
    {
        if (stages == null) return null;
        return stages.Find(s => s.stageId == stageId);
    }

    public QuestStage GetFirstStage()
    {
        return stages != null && stages.Count > 0 ? stages[0] : null;
    }
}
