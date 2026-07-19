using System;
using System.Collections.Generic;
using UnityEngine;

// 任务管理器（单例）。管理所有任务的接受、进度追踪、领取奖励等。
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("事件提示")]
    [SerializeField] private UI_EventTip eventTip;

    [Header("所有可接任务")]
    [SerializeField] private List<QuestData> allQuestData;

    // 任务状态集合
    private Dictionary<string, QuestProgress> activeQuests = new();          // 进行中的任务 + 进度
    private Dictionary<string, QuestProgress> completedQuestProgress = new(); // 已完成任务的最终进度（用于面板显示）
    private HashSet<string> readyToClaimQuests = new();                       // 已完成条件、等待领取奖励
    private HashSet<string> completedQuests = new();                          // 已领取奖励
    private HashSet<string> failedQuests = new();
    private string trackedQuestId = null;                             // 已失败

    // 事件：任务状态变更时通知 UI
    public event Action<string> OnQuestAccepted;
    public event Action<string> OnQuestReadyToClaim;
    public event Action<string> OnQuestClaimed;
    public event Action<string> OnQuestFailed;
    public event Action<string, int, int> OnObjectiveUpdated;
    public event Action<string> OnTrackChanged; // questId, objectiveIndex, currentCount

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        TargetNameResolver.Initialize();
    }

    private void OnEnable()
    {
        QuestEvents.OnEnemyKilled += HandleEnemyKilled;
        QuestEvents.OnItemCollected += HandleItemCollected;
    }

    private void OnDisable()
    {
        QuestEvents.OnEnemyKilled -= HandleEnemyKilled;
        QuestEvents.OnItemCollected -= HandleItemCollected;
    }

    private void HandleEnemyKilled(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId))
            return;

        TryProgressAll(ObjectiveType.Kill, enemyId, 1);
    }

    private void HandleItemCollected(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId))
            return;

        TryProgressAll(ObjectiveType.Collect, itemId, count);
    }

    private void TryProgressAll(ObjectiveType type, string targetId, int count)
    {
        List<string> keys = CopyActiveKeys();

        foreach (var questId in keys)
        {
            var quest = GetQuestData(questId);
            if (quest == null)
                continue;

            var progress = activeQuests[questId];

            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var obj = quest.objectives[i];

                if (obj.type != type || obj.targetId != targetId)
                    continue;
                if (progress.objectiveProgress[i] >= obj.requiredCount)
                    continue;

                progress.objectiveProgress[i] = Mathf.Min(
                    progress.objectiveProgress[i] + count, obj.requiredCount);

                OnObjectiveUpdated?.Invoke(quest.questId, i, progress.objectiveProgress[i]);
            }
        }
    }

    private List<string> CopyActiveKeys()
    {
        List<string> keys = new List<string>();
        foreach (var kvp in activeQuests)
            keys.Add(kvp.Key);
        return keys;
    }

    private bool CheckQuestComplete(QuestData quest, QuestProgress progress)
    {
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            if (progress.objectiveProgress[i] < quest.objectives[i].requiredCount)
                return false;
        }
        return true;
    }

    private void SetQuestReadyToClaim(string questId)
    {
        if (readyToClaimQuests.Contains(questId))
            return;

        readyToClaimQuests.Add(questId);
        OnQuestReadyToClaim?.Invoke(questId);
        eventTip?.ShowQuestCompleted(GetQuestData(questId)?.questName ?? questId);
        Debug.Log($"[任务] 可领取奖励: {GetQuestData(questId)?.questName}");
    }

    public bool AcceptQuest(string questId)
    {
        if (activeQuests.ContainsKey(questId) ||
            readyToClaimQuests.Contains(questId) ||
            completedQuests.Contains(questId))
            return false;

        var quest = GetQuestData(questId);
        if (quest == null)
            return false;

        if (quest.prerequisiteQuestIds != null)
        {
            foreach (var prereq in quest.prerequisiteQuestIds)
            {
                if (!completedQuests.Contains(prereq))
                    return false;
            }
        }

        var progress = new QuestProgress(quest.objectives.Count);
        activeQuests[questId] = progress;

        // 接受任务时检查背包，更新已有物品的收集进度
        SyncCollectProgressFromInventory(quest, progress);

        OnQuestAccepted?.Invoke(questId);
        eventTip?.ShowQuestAccepted(GetQuestData(questId)?.questName ?? questId);
        Debug.Log($"[任务] 接受: {quest.questName}");
        return true;
    }

    public bool ClaimQuest(string questId)
    {
        if (!readyToClaimQuests.Contains(questId))
            return false;

        var quest = GetQuestData(questId);
        if (quest == null)
            return false;

        readyToClaimQuests.Remove(questId);
        completedQuests.Add(questId);

        if (activeQuests.TryGetValue(questId, out var finalProgress))
            completedQuestProgress[questId] = finalProgress;

        activeQuests.Remove(questId);

        var reward = quest.reward;

        if (reward.expAmount > 0 && PlayerLevelManager.Instance != null)
            PlayerLevelManager.Instance.AddExp(reward.expAmount);

        if (reward.skillPoints > 0)
        {
            if (PlayerLevelManager.Instance != null)
                PlayerLevelManager.Instance.AddSkillPoints(reward.skillPoints);
            else if (SkillPointManager.Instance != null)
                SkillPointManager.Instance.AddSkillPoints(reward.skillPoints, "任务奖励");
        }

        if (reward.items != null && reward.items.Count > 0)
        {
            var inventory = FindFirstObjectByType<Inventory_Player>();
            if (inventory != null)
            {
                foreach (var rewardItem in reward.items)
                {
                    if (rewardItem != null && rewardItem.itemData != null)
                    {
                        for (int i = 0; i < rewardItem.amount; i++)
                            inventory.AddItem(new Inventory_Item(rewardItem.itemData));
                    }
                }
            }
        }

        OnQuestClaimed?.Invoke(questId);
        Debug.Log($"[任务] 已领取奖励: {quest.questName}");
        return true;
    }

    /// <summary>手动提交任务：检查进度 → 收集任务扣除物品 → 完成</summary>
    public bool SubmitQuest(string questId)
    {
        if (!activeQuests.ContainsKey(questId))
            return false;

        var quest = GetQuestData(questId);
        if (quest == null)
            return false;

        var progress = activeQuests[questId];

        // 收集目标 → 检查并扣除背包物品
        var inventory = FindFirstObjectByType<Inventory_Player>();
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            if (quest.objectives[i].type != ObjectiveType.Collect)
                continue;

            int required = quest.objectives[i].requiredCount;
            if (inventory == null || CountItemsInInventory(inventory, quest.objectives[i].targetId) < required)
            {
                Debug.LogWarning("[任务] 背包中物品不足，无法提交");
                return false;
            }

            RemoveItemsFromInventory(inventory, quest.objectives[i].targetId, required);
            progress.objectiveProgress[i] = required;
            OnObjectiveUpdated?.Invoke(questId, i, progress.objectiveProgress[i]);
        }

        // 检查所有目标是否完成
        if (!CheckQuestComplete(quest, progress))
        {
            Debug.Log("[任务] 目标尚未全部完成，无法提交");
            return false;
        }

        SetQuestReadyToClaim(quest.questId);
        Debug.Log($"[任务] 提交完成: {quest.questName}");
        return true;
    }

    /// <summary>接受任务时：检查背包已有物品，同步收集进度</summary>
    private void SyncCollectProgressFromInventory(QuestData quest, QuestProgress progress)
    {
        var inventory = FindFirstObjectByType<Inventory_Player>();
        if (inventory == null)
            return;

        for (int i = 0; i < quest.objectives.Count; i++)
        {
            if (quest.objectives[i].type != ObjectiveType.Collect)
                continue;

            int count = CountItemsInInventory(inventory, quest.objectives[i].targetId);
            if (count <= 0)
                continue;

            progress.objectiveProgress[i] = Mathf.Min(count, quest.objectives[i].requiredCount);
            OnObjectiveUpdated?.Invoke(quest.questId, i, progress.objectiveProgress[i]);
        }
    }

    private int CountItemsInInventory(Inventory_Base inventory, string itemId)
    {
        int total = 0;
        foreach (var kvp in inventory.itemDictionary)
        {
            var item = kvp.Value;
            if (item != null && item.itemData != null && item.itemData.itemId == itemId)
                total += item.currentStackSize;
        }
        return total;
    }

    private void RemoveItemsFromInventory(Inventory_Base inventory, string itemId, int count)
    {
        int remaining = count;
        var keysToRemove = new List<int>();

        foreach (var kvp in inventory.itemDictionary)
        {
            if (remaining <= 0)
                break;

            var item = kvp.Value;
            if (item == null || item.itemData == null || item.itemData.itemId != itemId)
                continue;

            if (item.currentStackSize <= remaining)
            {
                remaining -= item.currentStackSize;
                keysToRemove.Add(kvp.Key);
            }
            else
            {
                item.currentStackSize -= remaining;
                remaining = 0;
            }
        }

        foreach (var key in keysToRemove)
            inventory.RemoveItemAtSlot(key);

        inventory.TriggerInventoryUpdate();
    }

    public void ToggleTrack(string questId)
    {
        if (trackedQuestId == questId)
            trackedQuestId = null;
        else
            trackedQuestId = questId;
        OnTrackChanged?.Invoke(questId);
    }

    public bool IsTracked(string questId) => trackedQuestId == questId;

    public List<string> GetTrackedQuestIds()
    {
        if (trackedQuestId == null) return new List<string>();
        return new List<string> { trackedQuestId };
    }

    public void MarkQuestFailed(string questId)
    {
        if (failedQuests.Contains(questId))
            return;

        activeQuests.Remove(questId);
        readyToClaimQuests.Remove(questId);
        failedQuests.Add(questId);
        OnQuestFailed?.Invoke(questId);
        eventTip?.ShowQuestFailed(GetQuestData(questId)?.questName ?? questId);
        Debug.Log($"[任务] 失败: {GetQuestData(questId)?.questName}");
    }

    public List<QuestData> GetAvailableQuests()
    {
        List<QuestData> available = new List<QuestData>();

        foreach (var quest in allQuestData)
        {
            if (activeQuests.ContainsKey(quest.questId))
                continue;
            if (readyToClaimQuests.Contains(quest.questId))
                continue;
            if (completedQuests.Contains(quest.questId))
                continue;
            if (failedQuests.Contains(quest.questId))
                continue;
            if (!ArePrerequisitesMet(quest))
                continue;

            available.Add(quest);
        }

        return available;
    }

    private bool ArePrerequisitesMet(QuestData quest)
    {
        if (quest.prerequisiteQuestIds == null)
            return true;

        foreach (var id in quest.prerequisiteQuestIds)
        {
            if (!completedQuests.Contains(id))
                return false;
        }
        return true;
    }

    public QuestData GetQuestData(string questId)
    {
        foreach (var quest in allQuestData)
        {
            if (quest.questId == questId)
                return quest;
        }
        return null;
    }

    public QuestProgress GetProgress(string questId)
    {
        if (activeQuests.TryGetValue(questId, out var progress))
            return progress;

        completedQuestProgress.TryGetValue(questId, out var completed);
        return completed;
    }

    public bool IsActive(string questId)       => activeQuests.ContainsKey(questId);
    public bool IsReadyToClaim(string questId) => readyToClaimQuests.Contains(questId);
    public bool IsCompleted(string questId)    => completedQuests.Contains(questId);
    public bool IsFailed(string questId)       => failedQuests.Contains(questId);

    public List<string> GetActiveQuestIds()
    {
        List<string> ids = new List<string>();
        foreach (var kvp in activeQuests)
            ids.Add(kvp.Key);
        return ids;
    }

    public List<string> GetReadyToClaimQuestIds()
    {
        List<string> ids = new List<string>();
        foreach (var id in readyToClaimQuests)
            ids.Add(id);
        return ids;
    }

    public List<string> GetCompletedQuestIds()
    {
        List<string> ids = new List<string>();
        foreach (var id in completedQuests)
            ids.Add(id);
        return ids;
    }

    public List<QuestData> GetAllQuestData() => allQuestData;

    [Serializable]
    public class QuestProgress
    {
        public int[] objectiveProgress;

        public QuestProgress(int objectiveCount)
        {
            objectiveProgress = new int[objectiveCount];
        }
    }
}
