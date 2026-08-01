using System;
using System.Collections.Generic;
using UnityEngine;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance { get; private set; } // 单例实例

    [Header("经验倍率设置")]
    [SerializeField] private float globalExpMultiplier = 1f; // 全局经验倍率
    [SerializeField] private float levelDifferenceMultiplier = 0.1f; // 等级差倍率系数（奖励和惩罚共用）
    [SerializeField] private int maxLevelDifference = 10; // 最大等级差值
    [SerializeField] private float minExpMultiplier = 0.1f; // 最小经验倍率
    [SerializeField] private float maxExpMultiplier = 2.0f; // 最大经验倍率

    [Header("来源类型倍率")]
    [SerializeField] private float enemyDefeatedMultiplier = 1f; // 击败敌人倍率

    [Header("调试信息")]
    [SerializeField] private bool showDebugLogs = false; // 是否显示调试日志

    public event Action<ExperienceEvent> OnExperienceGained; // 经验获取事件
    public event Action<int> OnExperienceMultiplierApplied; // 经验倍率应用事件（参数：最终经验值）

    private Dictionary<ExperienceSourceType, float> sourceMultipliers; // 来源类型倍率字典

    private void Awake() // 初始化单例
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSourceMultipliers();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeSourceMultipliers() // 初始化来源类型倍率字典
    {
        sourceMultipliers = new Dictionary<ExperienceSourceType, float>
        {
            { ExperienceSourceType.EnemyDefeated, enemyDefeatedMultiplier },
            { ExperienceSourceType.Quest, 1f }
        };
    }

    public void AddExperience(ExperienceEvent expEvent) // 添加经验（使用经验事件）
    {
        if (expEvent == null || expEvent.ExperienceAmount <= 0)
            return;

        int finalExp = CalculateFinalExperience(expEvent);

        if (showDebugLogs)
        {
            Debug.Log($"经验获取 - 来源: {expEvent.SourceType}, 基础: {expEvent.ExperienceAmount}, 最终: {finalExp}");
        }

        OnExperienceGained?.Invoke(expEvent);
        OnExperienceMultiplierApplied?.Invoke(finalExp);

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.AddExp(finalExp);
        }
    }

    public void AddExperience(int baseExp, ExperienceSourceType sourceType, object sourceData = null, Vector3? position = null) // 添加经验（快捷方法）
    {
        ExperienceEvent expEvent = new ExperienceEvent(baseExp, sourceType, sourceData, position);
        AddExperience(expEvent);
    }

    private int CalculateFinalExperience(ExperienceEvent expEvent) // 计算最终经验值
    {
        float multiplier = globalExpMultiplier;

        multiplier *= GetSourceMultiplier(expEvent.SourceType);

        multiplier *= CalculateLevelDifferenceBonus(expEvent);

        return Mathf.RoundToInt(expEvent.ExperienceAmount * multiplier);
    }

    private float GetSourceMultiplier(ExperienceSourceType sourceType) // 获取来源类型倍率
    {
        if (sourceMultipliers.TryGetValue(sourceType, out float multiplier))
        {
            return multiplier;
        }
        return 1f;
    }

    private float CalculateLevelDifferenceBonus(ExperienceEvent expEvent) // 计算等级差奖励/惩罚倍率
    {
        if (expEvent.SourceType != ExperienceSourceType.EnemyDefeated)
        {
            return 1f;
        }

        int enemyLevel = 0;

        // 尝试从 EnemyExpData 中获取敌人等级
        if (expEvent.SourceData is EnemyExpData expData)
        {
            enemyLevel = expData.enemyLevel;
        }
        // 兼容直接传递 int 的情况
        else if (expEvent.SourceData is int level)
        {
            enemyLevel = level;
        }
        else
        {
            return 1f;
        }

        int playerLevel = PlayerLevelManager.Instance?.CurrentLevel ?? 1;
        int levelDifference = enemyLevel - playerLevel;

        float multiplier = 1f;

        if (levelDifference > 0)
        {
            multiplier = 1f + Mathf.Min(levelDifference * levelDifferenceMultiplier, maxLevelDifference * levelDifferenceMultiplier);
            multiplier = Mathf.Min(multiplier, maxExpMultiplier);
        }
        else if (levelDifference < 0)
        {
            multiplier = 1f - Mathf.Min(Mathf.Abs(levelDifference) * levelDifferenceMultiplier, maxLevelDifference * levelDifferenceMultiplier);
            multiplier = Mathf.Max(multiplier, minExpMultiplier);
        }

        return multiplier;
    }

    public void SetGlobalExpMultiplier(float multiplier) // 设置全局经验倍率
    {
        globalExpMultiplier = Mathf.Max(0, multiplier);
    }

    public void SetSourceMultiplier(ExperienceSourceType sourceType, float multiplier) // 设置来源类型倍率
    {
        if (sourceMultipliers.ContainsKey(sourceType))
        {
            sourceMultipliers[sourceType] = multiplier;
        }
    }

    public float GetGlobalExpMultiplier() // 获取全局经验倍率
    {
        return globalExpMultiplier;
    }

    public float GetSourceMultiplierValue(ExperienceSourceType sourceType) // 获取来源类型倍率值
    {
        if (sourceMultipliers.TryGetValue(sourceType, out float multiplier))
        {
            return multiplier;
        }
        return 1f;
    }
}
