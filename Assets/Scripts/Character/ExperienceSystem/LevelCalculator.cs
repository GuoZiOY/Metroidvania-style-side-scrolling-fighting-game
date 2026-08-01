using UnityEngine;

public class LevelCalculator : MonoBehaviour
{
    [Header("基础参数")]
    [SerializeField] private float baseExp = 100f; // 基础经验值

    [Header("分段点")]
    [SerializeField] private int phase1MaxLevel = 10; // 第一阶段最大等级（1-10级）
    [SerializeField] private int phase2MaxLevel = 30; // 第二阶段最大等级（11-30级）
    [SerializeField] private int maxLevel = 50; // 最大等级

    public int MaxLevel => maxLevel; // 获取最大等级

    public int GetExpToNextLevel(int currentLevel) // 获取升级所需经验
    {
        if (currentLevel >= maxLevel)
            return 0;

        if (currentLevel < phase1MaxLevel)
        {
            return CalculatePhase1Exp(currentLevel);
        }
        else if (currentLevel < phase2MaxLevel)
        {
            return CalculatePhase2Exp(currentLevel);
        }
        else
        {
            return CalculatePhase3Exp(currentLevel);
        }
    }

    private int CalculatePhase1Exp(int level) // 计算第一阶段经验（0-10级，线性增长）
    {
        return Mathf.RoundToInt(baseExp * (level + 1)); // 线性增长：0级100，1级200，2级300...
    }

    private int CalculatePhase2Exp(int level) // 计算第二阶段经验（10-29级，指数增长）
    {
        float growthFactor = 1.5f; // 增长系数
        float phaseMultiplier = Mathf.Pow(growthFactor, (level - 9) / 10f); // 阶段倍率
        return Mathf.RoundToInt(baseExp * (level + 1) * phaseMultiplier);
    }

    private int CalculatePhase3Exp(int level) // 计算第三阶段经验（30-49级，指数增长）
    {
        float growthFactor = 2.0f; // 增长系数（比第二阶段更大）
        float phaseMultiplier = Mathf.Pow(growthFactor, (level - 29) / 20f); // 阶段倍率
        return Mathf.RoundToInt(baseExp * (level + 1) * phaseMultiplier);
    }

    public int GetTotalExpRequired(int targetLevel) // 获取达到目标等级所需总经验
    {
        if (targetLevel <= 0)
            return 0;

        int totalExp = 0;
        for (int i = 0; i < targetLevel; i++)
        {
            totalExp += GetExpToNextLevel(i);
        }
        return totalExp;
    }

    public int GetCurrentLevelFromExp(int currentExp) // 从经验值反推当前等级
    {
        int level = 0;
        int accumulatedExp = 0;

        while (level < maxLevel)
        {
            int expNeeded = GetExpToNextLevel(level);
            if (accumulatedExp + expNeeded > currentExp)
                break;

            accumulatedExp += expNeeded;
            level++;
        }

        return level;
    }
}
