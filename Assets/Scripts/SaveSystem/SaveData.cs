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
}

#endregion

#region 角色数据

[Serializable]
public class PlayerSaveData
{
    public float currentHP;
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
}

[Serializable]
public class EquipSlotData
{
    public string itemId;
    public int itemType;      // (int)ItemType
    public int slotIndex;
    public int rarity;
    public float rarityMultiplier;
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
}

[Serializable]
public class QuestSaveEntry
{
    public string questId;
    public List<QuestObjectiveData> objectives;
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
