using UnityEngine;

public static class RarityCalculator
{
    //稀有度数值倍率（几何倍率，每级增加50%，增长系数1.5）
    private static readonly float[] RarityMultipliers = new float[]
    {
        1.0f,   // 普通
        1.5f,   // 精良
        2.0f,  // 优秀
        2.5f,  // 史诗
        3.0f   // 传说
    };

    public static float GetBaseMultiplier(LootRarity rarity) //获取基准数值倍率
    {
        int index = (int)rarity;
        if (index >= 0 && index < RarityMultipliers.Length)
            return RarityMultipliers[index];
        return 1.0f;
    }

    public static LootRarity GetVariedRarity(LootRarity baseRarity, int maxSteps) //获取波动后的稀有度（加权随机）
    {
        return GetVariedRarity(baseRarity, maxSteps, 0f);
    }

    public static LootRarity GetVariedRarity(LootRarity baseRarity, int maxSteps, float rarityBonus) //获取波动后的稀有度（加权随机，带稀有度加成）
    {
        if (maxSteps <= 0)
            return baseRarity;

        int baseIndex = (int)baseRarity;
        int minIndex = Mathf.Max(0, baseIndex - maxSteps);
        int maxIndex = Mathf.Min((int)LootRarity.传说, baseIndex + maxSteps);

        //使用加权随机，基准稀有度占绝对大头
        int range = maxIndex - minIndex;
        int[] weights = new int[range + 1];

        for (int i = 0; i <= range; i++)
        {
            int distance = i - (baseIndex - minIndex); //距离基准稀有度的步数（正数=向上，负数=向下）
            
            //权重计算：距离越远，权重越低
            //基准稀有度权重最高，每远离一步权重大幅降低
            //稀有度加成只影响向上浮动的权重，步长越远加成效果递减
            //向下浮动保持原始权重，不应用加成
            switch (distance)
            {
                case 0:
                    weights[i] = 100; //基准稀有度：100权重
                    break;
                case 1:
                    weights[i] = 15 + Mathf.RoundToInt(rarityBonus * 0.75f); //+1步：15权重 + 加成值×0.75
                    break;
                case 2:
                    weights[i] = 3 + Mathf.RoundToInt(rarityBonus * 0.5f); //+2步：3权重 + 加成值×0.5
                    break;
                case 3:
                    weights[i] = 1 + Mathf.RoundToInt(rarityBonus * 0.25f); //+3步：1权重 + 加成值×0.25
                    break;
                case -1:
                    weights[i] = 15; //-1步：15权重（保持原始值）
                    break;
                case -2:
                    weights[i] = 3; //-2步：3权重（保持原始值）
                    break;
                case -3:
                    weights[i] = 1; //-3步：1权重（保持原始值）
                    break;
                default:
                    weights[i] = 1; //更远：1权重
                    break;
            }
        }

        //加权随机选择
        int totalWeight = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            totalWeight += weights[i];
        }

        int randomWeight = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;
        int selectedIndex = 0;

        for (int i = 0; i < weights.Length; i++)
        {
            currentWeight += weights[i];
            if (randomWeight < currentWeight)
            {
                selectedIndex = i;
                break;
            }
        }

        return (LootRarity)(minIndex + selectedIndex);
    }

    public static float GetRarityDifferenceMultiplier(LootRarity baseRarity, LootRarity actualRarity) //获取稀有度差值倍率
    {
        float baseMultiplier = GetBaseMultiplier(baseRarity);
        float actualMultiplier = GetBaseMultiplier(actualRarity);
        return actualMultiplier / baseMultiplier;
    }

    public static float GetModifiedValue(int baseValue, LootRarity baseRarity, LootRarity actualRarity) //获取修改后的数值（保留一位小数）
    {
        float differenceMultiplier = GetRarityDifferenceMultiplier(baseRarity, actualRarity);
        
        //最终数值 = 基础数值 × 差值倍率，保留一位小数
        return Mathf.Round(baseValue * differenceMultiplier * 10f) / 10f;
    }

    public static float GetModifiedValue(float baseValue, LootRarity baseRarity, LootRarity actualRarity) //获取修改后的浮点数值（保留一位小数）
    {
        float differenceMultiplier = GetRarityDifferenceMultiplier(baseRarity, actualRarity);
        
        //最终数值 = 基础数值 × 差值倍率，保留一位小数
        return Mathf.Round(baseValue * differenceMultiplier * 10f) / 10f;
    }

    public static string GetRarityName(LootRarity rarity) //获取稀有度名称
    {
        switch (rarity)
        {
            case LootRarity.普通: return "普通";
            case LootRarity.精良: return "精良";
            case LootRarity.稀有: return "稀有";
            case LootRarity.史诗: return "史诗";
            case LootRarity.传说: return "传说";
            default: return "未知";
        }
    }

    public static Color GetRarityColor(LootRarity rarity) //获取稀有度颜色
    {
        switch (rarity)
        {
            case LootRarity.普通: return Color.white;      //白色
            case LootRarity.精良: return Color.green;       //绿色
            case LootRarity.稀有: return Color.blue;     //蓝色
            case LootRarity.史诗: return new Color(0.5f, 0f, 0.5f); //紫色
            case LootRarity.传说: return Color.red;     //红色
            default: return Color.gray;
        }
    }
}
