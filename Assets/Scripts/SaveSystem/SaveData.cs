using System;
using System.Collections.Generic;
using UnityEngine;

// 存档系统的全部数据结构定义。
// 所有结构体均为 [Serializable]，直接用于 JsonUtility 序列化。

#region 顶层

[Serializable]
public class SaveData
{
    public int version = 1;
    public string saveTime;             // DateTime.UtcNow.ToString("O")
    public float playTime;              // 总游戏时长（秒）
    public string sceneName;            // SceneManager.GetActiveScene().name
    public float posX, posY, posZ;      // player.transform.position
    public string lastCheckpointId;     // 最近存档点 ID

    public PlayerSaveData player;
    public List<StatSaveEntry> stats;
    public List<InventorySlotData> inventory;
    public List<EquipSlotData> equipment;
    public SkillSaveData skills;
    public QuestSaveData quests;
    public List<string> worldFlags;      // WorldState 持久化 (key=true 的列表)
    public List<InventorySlotData> warehouse;  // 仓库内容（旧档为 null → 空仓库，向后兼容）
}

#endregion

#region 角色数据

[Serializable]
public class PlayerSaveData
{
    public float currentHP;
    public int currency;
    public int currentLevel;
    public int currentExp;
    public int unspentSkillPoints;       // PlayerLevelManager.skillPoints
    public int unspentAttributePoints;   // PlayerLevelManager.attributePoints
    public int totalSkillPoints;         // SkillPointManager.totalSkillPoints
    public int usedSkillPoints;          // SkillPointManager.usedSkillPoints
}

[Serializable]
public class StatSaveEntry
{
    public int statType;      // (int)StatType（MaxHP=0, ..., LightningResistance=20）
    public float baseValue;   // Stat.baseValue
}

#endregion

#region 背包 + 装备

[Serializable]
public class InventorySlotData
{
    public string itemId;           // ItemDataSo.itemId（如 "100101"=木剑）
    public int stackSize;
    public int slotIndex;
    public int rarity;               // (int)LootRarity
    public float rarityMultiplier;
    public List<AffixSaveData> affixes;   // 词缀存档（V2 后生效；旧存档为 null 时读档重新生成）
}

[Serializable]
public class EquipSlotData
{
    public string itemId;
    public int itemType;      // (int)ItemType
    public int slotIndex;
    public int rarity;
    public float rarityMultiplier;
    public List<AffixSaveData> affixes;   // 词缀存档（V2 后生效；旧存档为 null 时读档重新生成）
}

// 单个词缀的存档数据（与 GeneratedEquipmentAffix 一一对应）
[Serializable]
public class AffixSaveData
{
    public string displayName;            // 词缀显示名
    public int tier;                      // (int)AffixTier
    public bool isPrefix;                 // true=前缀, false=后缀
    public List<ModifierSaveData> modifiers; // 每段效果（含负值=负面效果）
}

// 单段词缀效果的存档数据（与 ItemModifier 一一对应）
[Serializable]
public class ModifierSaveData
{
    public int statType;                  // (int)StatType
    public float value;                   // 最终数值（百分比存小数如 -0.08）
    public bool isPercentage;             // true=百分比, false=固定值
}

#endregion

#region 技能

[Serializable]
public class SkillSaveData
{
    public List<SkillLevelEntry> learned;       // 所有已学习的技能
    public int[] slotBindings;                   // 5 个槽位，存 (int)SkillUpgradeType，-1=空
}

[Serializable]
public class SkillLevelEntry
{
    public int upgradeType;    // (int)SkillUpgradeType
    public int level;
}

#endregion

#region 任务

[Serializable]
public class QuestSaveData
{
    public List<QuestSaveEntry> active;
    public List<string> readyToClaim;
    public List<string> completed;
    public List<string> failed;
    public string trackedQuestId;
    public List<ClaimedStageEntry> claimedStageRewards;  // 已领取的阶段奖励
}

[Serializable]
public class ClaimedStageEntry
{
    public string questId;
    public int stageIndex;
}

[Serializable]
public class QuestSaveEntry
{
    public string questId;
    public int currentStageIndex;                    // 当前阶段索引
    public List<QuestObjectiveData> objectives;
    public int[] objectiveProgress;                  // 平铺进度数组（与 List 二选一）
}

[Serializable]
public class QuestObjectiveData
{
    public int index;
    public int currentCount;
}

#endregion

#region 存档元数据（主菜单展示用）

[Serializable]
public class SaveProfile
{
    public int slotIndex;
    public string saveTime;
    public float playTime;
    public string sceneName;
    public int playerLevel;
    public bool isEmpty;
}

[Serializable]
public class SaveProfileList
{
    public List<SaveProfile> profiles = new List<SaveProfile>();
}

#endregion
