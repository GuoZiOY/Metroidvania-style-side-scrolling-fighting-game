using System;

public static class QuestEvents
{
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

    /// <summary>任务完成时调用（仅供 QuestManager 内部使用）</summary>
    internal static event Action<string> OnQuestCompleted;
    internal static void ReportQuestCompleted(string questId)
    {
        OnQuestCompleted?.Invoke(questId);
    }
}
