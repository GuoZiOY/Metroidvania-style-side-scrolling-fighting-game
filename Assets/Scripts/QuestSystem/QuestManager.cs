using System;
using System.Collections.Generic;
using UnityEngine;

// 任务管理器（单例）。
// 管理所有任务的生命周期：接取、阶段推进、进度追踪、领奖、存档。
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("事件提示")]
    [SerializeField] private UI_EventTip eventTip;

    [Header("所有可接任务")]
    [SerializeField] private List<QuestData> allQuestData;

    // ─── 任务运行时状态 ───

    // 进行中的任务（任务ID → 进度）
    private Dictionary<string, QuestProgress> activeQuests = new();

    // 已完成但尚未领取最终奖励的任务
    private HashSet<string> readyToClaimQuests = new();

    // 已完成的最终任务进度（供面板查阅）
    private Dictionary<string, QuestProgress> completedQuestProgress = new();

    // 已领取奖励
    private HashSet<string> completedQuests = new();

    // 已失败
    private HashSet<string> failedQuests = new();

    // 正在追踪的任务ID
    private string trackedQuestId = null;

    // ─── 事件 ───

    public event Action<string> OnQuestAccepted;              // questId
    public event Action<string> OnQuestStageChanged;          // questId → 新 stageId
    public event Action<string> OnQuestReadyToClaim;          // 最终阶段完成
    public event Action<string> OnQuestClaimed;               // 奖励已领取
    public event Action<string> OnQuestFailed;                // 任务失败
    public event Action<string, int, int> OnObjectiveUpdated; // questId, objIndex, currentCount
    public event Action<string> OnTrackChanged;               // 追踪变更

    // ─── 初始化 ───

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
        QuestEvents.OnNpcTalked += HandleNpcTalked;
    }

    private void OnDisable()
    {
        QuestEvents.OnEnemyKilled -= HandleEnemyKilled;
        QuestEvents.OnItemCollected -= HandleItemCollected;
        QuestEvents.OnNpcTalked -= HandleNpcTalked;
    }

    // ─── 事件处理 ───

    private void HandleEnemyKilled(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return;
        TryProgressObjective(ObjectiveType.Kill, enemyId, 1);
    }

    private void HandleItemCollected(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        TryProgressObjective(ObjectiveType.Collect, itemId, count);
    }

    private void HandleNpcTalked(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        TryProgressObjective(ObjectiveType.TalkToNPC, npcId, 1);
    }

    // ─── 进度更新（核心） ───

    // 对所有进行中任务的当前阶段，匹配目标类型+ID并增加进度
    private void TryProgressObjective(ObjectiveType type, string targetId, int count)
    {
        foreach (var questId in CopyActiveKeys())
        {
            var quest = GetQuestData(questId);
            if (quest == null) continue;

            var progress = activeQuests[questId];
            var stage = quest.GetStage(progress.currentStageId);
            if (stage == null || stage.objectives == null) continue;

            bool anyUpdated = false;

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var obj = stage.objectives[i];
                if (obj.type != type || obj.targetId != targetId) continue;

                // 确保进度数组长度匹配
                if (i >= progress.objectiveProgress.Length)
                    Array.Resize(ref progress.objectiveProgress, stage.objectives.Count);

                if (progress.objectiveProgress[i] >= obj.requiredCount) continue;

                progress.objectiveProgress[i] = Mathf.Min(
                    progress.objectiveProgress[i] + count, obj.requiredCount);

                OnObjectiveUpdated?.Invoke(questId, i, progress.objectiveProgress[i]);
                anyUpdated = true;
            }

            // 当前阶段所有目标完成 → 标记「阶段完成」（仅记录，等待 NPC 对话推进）
            if (anyUpdated && IsCurrentStageComplete(quest, progress))
            {
                // 检查是否是最终阶段 → 标记为可领奖
                if (string.IsNullOrEmpty(stage.nextStageId))
                {
                    SetQuestReadyToClaim(questId);
                }
                // 非最终阶段：等待 NPC 对话调用 AdvanceToNextStage 推进
            }
        }
    }

    private List<string> CopyActiveKeys()
    {
        var keys = new List<string>();
        foreach (var kvp in activeQuests)
            keys.Add(kvp.Key);
        return keys;
    }

    // ─── 接取任务 ───

    public bool AcceptQuest(string questId)
    {
        if (activeQuests.ContainsKey(questId) ||
            readyToClaimQuests.Contains(questId) ||
            completedQuests.Contains(questId))
            return false;

        var quest = GetQuestData(questId);
        if (quest == null) return false;

        // 前置任务检查
        if (!ArePrerequisitesMet(quest)) return false;

        // 初始化为第一阶段
        var firstStage = quest.GetFirstStage();
        if (firstStage == null)
        {
            Debug.LogWarning($"[任务] {quest.questName} 没有配置阶段");
            return false;
        }

        var progress = new QuestProgress(firstStage.stageId, firstStage.objectives?.Count ?? 0);
        activeQuests[questId] = progress;

        // 接受时同步背包已有的收集进度
        SyncCollectProgressFromInventory(quest, progress);

        OnQuestAccepted?.Invoke(questId);
        eventTip?.ShowQuestAccepted(quest.questName ?? questId);
        Debug.Log($"[任务] 接受: {quest.questName}");
        return true;
    }

    // ─── 阶段推进 ───

    // 推进到下一阶段（NPC 对话中调用）
    public bool AdvanceToNextStage(string questId)
    {
        if (!activeQuests.ContainsKey(questId)) return false;

        var quest = GetQuestData(questId);
        if (quest == null) return false;

        var progress = activeQuests[questId];
        var currentStage = quest.GetStage(progress.currentStageId);
        if (currentStage == null) return false;

        // 检查当前阶段是否真的完成了
        if (!IsCurrentStageComplete(quest, progress))
        {
            Debug.Log($"[任务] 阶段未完成，无法推进");
            return false;
        }

        // 发放阶段奖励
        GrantReward(currentStage.stageReward);

        // 自动找到下一阶段（按列表顺序）
        int currentIdx = quest.stages.IndexOf(currentStage);
        if (currentIdx < 0 || currentIdx >= quest.stages.Count - 1)
        {
            Debug.LogWarning("[任务] 已是最终阶段，无法推进");
            return false;
        }

        var nextStage = quest.stages[currentIdx + 1];
        progress.currentStageId = nextStage.stageId;
        progress.objectiveProgress = new int[nextStage.objectives?.Count ?? 0];

        OnQuestStageChanged?.Invoke(questId);
        Debug.Log($"[任务] 推进到阶段 {nextStage.stageId}: {quest.questName}");
        return true;
    }

    // ─── 领取最终奖励 ───

    public bool ClaimFinalReward(string questId)
    {
        if (!readyToClaimQuests.Contains(questId)) return false;

        var quest = GetQuestData(questId);
        if (quest == null) return false;

        readyToClaimQuests.Remove(questId);
        completedQuests.Add(questId);

        if (activeQuests.TryGetValue(questId, out var finalProgress))
            completedQuestProgress[questId] = finalProgress;
        activeQuests.Remove(questId);

        // 发放最终奖励
        GrantReward(quest.finalReward);

        // 设置世界状态
        if (quest.setWorldFlags != null)
        {
            foreach (var flag in quest.setWorldFlags)
                WorldState.Set(flag, true);
        }

        // 自动接取后续任务
        if (quest.followUpQuestIds != null)
        {
            foreach (var nextId in quest.followUpQuestIds)
            {
                if (GetQuestData(nextId)?.autoAccept == true)
                    AcceptQuest(nextId);
            }
        }

        OnQuestClaimed?.Invoke(questId);
        eventTip?.ShowQuestCompleted(quest.questName ?? questId);
        Debug.Log($"[任务] 已完成并领取奖励: {quest.questName}");
        return true;
    }

    // ─── 奖励发放 ───

    private void GrantReward(QuestReward reward)
    {
        if (reward == null) return;

        if (reward.expAmount > 0 && PlayerLevelManager.Instance != null)
            PlayerLevelManager.Instance.AddExp(reward.expAmount);

        if (reward.skillPoints > 0)
        {
            if (PlayerLevelManager.Instance != null)
                PlayerLevelManager.Instance.AddSkillPoints(reward.skillPoints);
            else if (SkillPointManager.Instance != null)
                SkillPointManager.Instance.AddSkillPoints(reward.skillPoints, "任务奖励");
        }

        if (reward.goldAmount > 0)
        {
            var invSys = FindAnyObjectByType<PlayerInventorySystem>();
            if (invSys != null)
                invSys.AddCurrency(reward.goldAmount);
        }

        if (reward.items != null)
        {
            var inventory = FindAnyObjectByType<Inventory_Player>();
            if (inventory != null)
            {
                foreach (var rewardItem in reward.items)
                {
                    if (rewardItem?.itemData != null)
                    {
                        for (int i = 0; i < rewardItem.amount; i++)
                            inventory.AddItem(new Inventory_Item(rewardItem.itemData));
                    }
                }
            }
        }
    }

    // ─── 查询 ───

    public bool IsQuestAvailable(string questId)
    {
        if (activeQuests.ContainsKey(questId) ||
            readyToClaimQuests.Contains(questId) ||
            completedQuests.Contains(questId) ||
            failedQuests.Contains(questId))
            return false;

        var quest = GetQuestData(questId);
        return quest != null && ArePrerequisitesMet(quest);
    }

    public QuestStage GetCurrentStage(string questId)
    {
        if (!activeQuests.TryGetValue(questId, out var progress)) return null;
        var quest = GetQuestData(questId);
        return quest?.GetStage(progress.currentStageId);
    }

    public string GetCurrentStageId(string questId)
    {
        return activeQuests.TryGetValue(questId, out var progress) ? progress.currentStageId : null;
    }

    public int[] GetStageProgress(string questId)
    {
        return activeQuests.TryGetValue(questId, out var progress) ? progress.objectiveProgress : null;
    }

    public bool IsCurrentStageComplete(QuestData quest, QuestProgress progress)
    {
        var stage = quest.GetStage(progress.currentStageId);
        if (stage?.objectives == null) return false;

        // 确保进度数组长度匹配
        if (progress.objectiveProgress.Length < stage.objectives.Count)
            Array.Resize(ref progress.objectiveProgress, stage.objectives.Count);

        for (int i = 0; i < stage.objectives.Count; i++)
        {
            if (progress.objectiveProgress[i] < stage.objectives[i].requiredCount)
                return false;
        }
        return true;
    }

    public bool IsCurrentStageComplete(string questId)
    {
        if (!activeQuests.TryGetValue(questId, out var progress)) return false;
        var quest = GetQuestData(questId);
        return quest != null && IsCurrentStageComplete(quest, progress);
    }

    private bool ArePrerequisitesMet(QuestData quest)
    {
        if (quest.prerequisiteQuestIds != null)
        {
            foreach (var id in quest.prerequisiteQuestIds)
            {
                if (!completedQuests.Contains(id))
                    return false;
            }
        }
        return true;
    }

    // ─── 标记为可领奖 ───

    private void SetQuestReadyToClaim(string questId)
    {
        if (readyToClaimQuests.Contains(questId)) return;
        readyToClaimQuests.Add(questId);
        OnQuestReadyToClaim?.Invoke(questId);
    }

    // ─── 同步背包收集进度 ───

    private void SyncCollectProgressFromInventory(QuestData quest, QuestProgress progress)
    {
        var stage = quest.GetStage(progress.currentStageId);
        if (stage?.objectives == null) return;

        var inventory = FindAnyObjectByType<Inventory_Player>();
        if (inventory == null) return;

        for (int i = 0; i < stage.objectives.Count; i++)
        {
            if (stage.objectives[i].type != ObjectiveType.Collect) continue;
            if (i >= progress.objectiveProgress.Length) break;

            int count = CountItemsInInventory(inventory, stage.objectives[i].targetId);
            if (count <= 0) continue;

            progress.objectiveProgress[i] = Mathf.Min(count, stage.objectives[i].requiredCount);
            OnObjectiveUpdated?.Invoke(quest.questId, i, progress.objectiveProgress[i]);
        }
    }

    private int CountItemsInInventory(Inventory_Base inventory, string itemId)
    {
        int total = 0;
        foreach (var kvp in inventory.itemDictionary)
        {
            var item = kvp.Value;
            if (item?.itemData != null && item.itemData.itemId == itemId)
                total += item.currentStackSize;
        }
        return total;
    }

    // ─── 失败 / 追踪 ───

    public void MarkQuestFailed(string questId)
    {
        if (failedQuests.Contains(questId)) return;

        activeQuests.Remove(questId);
        readyToClaimQuests.Remove(questId);
        failedQuests.Add(questId);
        OnQuestFailed?.Invoke(questId);

        var quest = GetQuestData(questId);
        eventTip?.ShowQuestFailed(quest?.questName ?? questId);
        Debug.Log($"[任务] 失败: {quest?.questName}");
    }

    public void ToggleTrack(string questId)
    {
        trackedQuestId = trackedQuestId == questId ? null : questId;
        OnTrackChanged?.Invoke(questId);
    }

    public bool IsTracked(string questId) => trackedQuestId == questId;
    public string GetTrackedQuestId() => trackedQuestId;

    // ─── 状态查询 ───

    public QuestData GetQuestData(string questId)
    {
        foreach (var quest in allQuestData)
        {
            if (quest.questId == questId) return quest;
        }
        return null;
    }

    public QuestProgress GetProgress(string questId)
    {
        if (activeQuests.TryGetValue(questId, out var p)) return p;
        completedQuestProgress.TryGetValue(questId, out var cp);
        return cp;
    }

    public bool IsActive(string questId)       => activeQuests.ContainsKey(questId);
    public bool IsReadyToClaim(string questId) => readyToClaimQuests.Contains(questId);
    public bool IsCompleted(string questId)    => completedQuests.Contains(questId);
    public bool IsFailed(string questId)       => failedQuests.Contains(questId);

    public QuestData[] GetAllQuestData()       => allQuestData?.ToArray();

    public List<string> GetActiveQuestIds()
    {
        var ids = new List<string>();
        foreach (var kvp in activeQuests) ids.Add(kvp.Key);
        return ids;
    }

    public List<string> GetReadyToClaimQuestIds()
    {
        return new List<string>(readyToClaimQuests);
    }

    public List<string> GetCompletedQuestIds()
    {
        return new List<string>(completedQuests);
    }

    public List<string> GetFailedQuestIds()
    {
        return new List<string>(failedQuests);
    }

    // ─── 获取可接取的任务列表（供面板和 NPC 使用） ───

    public List<QuestData> GetAvailableQuests()
    {
        var available = new List<QuestData>();
        if (allQuestData == null) return available;

        foreach (var quest in allQuestData)
        {
            if (!IsQuestAvailable(quest.questId)) continue;
            available.Add(quest);
        }
        return available;
    }

    // ─── 存档 ───

    [Serializable]
    public class QuestProgress
    {
        public string currentStageId;
        public int[] objectiveProgress;

        public QuestProgress(string stageId, int objectiveCount)
        {
            currentStageId = stageId;
            objectiveProgress = new int[objectiveCount];
        }
    }

    public Dictionary<string, int[]> GetActiveQuestsForSave()
    {
        var result = new Dictionary<string, int[]>();
        foreach (var kvp in activeQuests)
            result[kvp.Key] = (int[])kvp.Value.objectiveProgress.Clone();
        return result;
    }

    public Dictionary<string, string> GetActiveQuestStageIdsForSave()
    {
        var result = new Dictionary<string, string>();
        foreach (var kvp in activeQuests)
            result[kvp.Key] = kvp.Value.currentStageId;
        return result;
    }

    public List<string> GetReadyToClaimQuestsForSave() => new(readyToClaimQuests);
    public List<string> GetCompletedQuestsForSave()    => new(completedQuests);
    public List<string> GetFailedQuestsForSave()       => new(failedQuests);
    public string GetTrackedQuestIdForSave()            => trackedQuestId;

    public void LoadFromSave(QuestSaveData d)
    {
        if (d == null) return;

        activeQuests.Clear();
        readyToClaimQuests.Clear();
        completedQuests.Clear();
        failedQuests.Clear();

        if (d.active != null)
        {
            foreach (var entry in d.active)
            {
                var progress = new QuestProgress(entry.currentStageId, entry.objectiveProgress?.Length ?? 0);
                if (entry.objectiveProgress != null)
                    progress.objectiveProgress = (int[])entry.objectiveProgress.Clone();
                activeQuests[entry.questId] = progress;
            }
        }

        if (d.readyToClaim != null)
            foreach (var id in d.readyToClaim) readyToClaimQuests.Add(id);
        if (d.completed != null)
            foreach (var id in d.completed) completedQuests.Add(id);
        if (d.failed != null)
            foreach (var id in d.failed) failedQuests.Add(id);

        trackedQuestId = d.trackedQuestId;
    }
}
