using System.Collections.Generic;
using UnityEngine;

// NPC 任务提供者。挂在 NPC 上，和 NPCBehaviour 协作。
// 负责查询任务状态、提供对话菜单所需的数据。
public class NPCQuestGiver : MonoBehaviour
{
    [Header("NPC 标识")]
    public string npcId;

    [Header("关联任务")]
    public List<QuestData> questsToGive;

    [Header("问候对话")]
    [TextArea] public string[] greetingLines;

    private QuestManager qm => QuestManager.Instance;

    // ─── 运行时查询 ───

    // 是否有可接取的任务
    public bool HasAvailableQuest()
    {
        foreach (var q in questsToGive)
        {
            if (qm != null && qm.IsQuestAvailable(q.questId))
                return true;
        }
        return false;
    }

    // 是否有进行中的任务（无所谓哪个阶段）
    public bool HasActiveQuest()
    {
        foreach (var q in questsToGive)
        {
            if (qm != null && qm.IsActive(q.questId))
                return true;
        }
        return false;
    }

    // 是否有非最终阶段已完成、待推进到下一阶段
    public bool HasStageToSubmit()
    {
        foreach (var q in questsToGive)
        {
            if (qm == null || !qm.IsActive(q.questId)) continue;
            if (qm.IsCurrentStageComplete(q.questId))
            {
                // 如果不是最终阶段（有 nextStageId）→ 可提交
                var stage = qm.GetCurrentStage(q.questId);
                if (stage != null && !string.IsNullOrEmpty(stage.nextStageId))
                    return true;
            }
        }
        return false;
    }

    // 是否有最终阶段完成、待领奖
    public bool HasFinalRewardToClaim()
    {
        foreach (var q in questsToGive)
        {
            if (qm != null && qm.IsReadyToClaim(q.questId))
                return true;
        }
        return false;
    }

    // ─── 获取首个符合条件的任务 ───

    public QuestData GetFirstAvailableQuest()
    {
        foreach (var q in questsToGive)
        {
            if (qm != null && qm.IsQuestAvailable(q.questId))
                return q;
        }
        return null;
    }

    public QuestData GetFirstActiveQuest()
    {
        foreach (var q in questsToGive)
        {
            if (qm != null && qm.IsActive(q.questId))
                return q;
        }
        return null;
    }

    public QuestData GetFirstReadyToClaimQuest()
    {
        foreach (var q in questsToGive)
        {
            if (qm != null && qm.IsReadyToClaim(q.questId))
                return q;
        }
        return null;
    }

    public QuestData GetFirstStageToSubmit()
    {
        foreach (var q in questsToGive)
        {
            if (qm == null || !qm.IsActive(q.questId)) continue;
            if (qm.IsCurrentStageComplete(q.questId))
            {
                var stage = qm.GetCurrentStage(q.questId);
                if (stage != null && !string.IsNullOrEmpty(stage.nextStageId))
                    return q;
            }
        }
        return null;
    }
}
