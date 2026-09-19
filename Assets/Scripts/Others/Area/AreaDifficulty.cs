public enum AreaDifficulty
{
    Easy, // 简单
    Normal, // 普通
    Hard, // 困难
    Expert, // 专家
    Nightmare // 噩梦
}

public static class AreaDifficultyExtensions
{
    #region 难度加成方法

    // 获取等级加成
    public static int GetLevelBonus(this AreaDifficulty difficulty)
    {
        return difficulty switch
        {
            AreaDifficulty.Easy => 0,
            AreaDifficulty.Normal => 1,
            AreaDifficulty.Hard => 2,
            AreaDifficulty.Expert => 3,
            AreaDifficulty.Nightmare => 4,
            _ => 0
        };
    }

    // 获取精英概率加成
    public static float GetEliteChanceBonus(this AreaDifficulty difficulty)
    {
        return difficulty switch
        {
            AreaDifficulty.Easy => 0f,
            AreaDifficulty.Normal => 0.05f,
            AreaDifficulty.Hard => 0.1f,
            AreaDifficulty.Expert => 0.15f,
            AreaDifficulty.Nightmare => 0.2f,
            _ => 0f
        };
    }

    // 获取等级浮动概率（保持，降低，升高）
    public static (float keep, float lower, float upper) GetLevelFloatChances(this AreaDifficulty difficulty)
    {
        return difficulty switch
        {
            AreaDifficulty.Easy => (0.95f, 0.03f, 0.02f),
            AreaDifficulty.Normal => (0.90f, 0.05f, 0.05f),
            AreaDifficulty.Hard => (0.85f, 0.05f, 0.10f),
            AreaDifficulty.Expert => (0.80f, 0.05f, 0.15f),
            AreaDifficulty.Nightmare => (0.75f, 0.05f, 0.20f),
            _ => (0.90f, 0.05f, 0.05f)
        };
    }

    // 获取最大等级增量
    public static int GetMaxLevelIncrease(this AreaDifficulty difficulty)
    {
        return difficulty switch
        {
            AreaDifficulty.Easy => 1,
            AreaDifficulty.Normal => 1,
            AreaDifficulty.Hard => 2,
            AreaDifficulty.Expert => 2,
            AreaDifficulty.Nightmare => 3,
            _ => 1
        };
    }

    #endregion
}
