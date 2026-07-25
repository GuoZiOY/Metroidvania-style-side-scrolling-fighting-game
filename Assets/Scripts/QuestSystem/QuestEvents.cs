using System;

// 任务事件中心。
// 各系统在对应行为发生时调用 ReportXXX，QuestManager 监听并更新任务进度。
public static class QuestEvents
{
    // ─── 已有事件 ───

    /// <summary>敌人被击杀时调用</summary>
    public static event Action<string> OnEnemyKilled;
    public static void ReportEnemyKilled(string enemyId)
    {
        OnEnemyKilled?.Invoke(enemyId);
    }

    /// <summary>物品被拾取时调用</summary>
    public static event Action<string, int> OnItemCollected;
    public static void ReportItemCollected(string itemId, int count)
    {
        OnItemCollected?.Invoke(itemId, count);
    }

    // ─── 新增事件 ───

    /// <summary>和 NPC 对话时调用（含任务对话/问候等所有交互）</summary>
    public static event Action<string> OnNpcTalked;
    public static void ReportNpcTalked(string npcId)
    {
        OnNpcTalked?.Invoke(npcId);
    }
}
