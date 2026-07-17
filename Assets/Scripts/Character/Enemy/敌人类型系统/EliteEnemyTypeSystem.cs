using UnityEngine;

public class EliteEnemyTypeSystem : EnemyTypeSystem // 精英敌人类型系统
{
    [Header("精英特有加成")]
    [SerializeField] private bool useBonus = true; // 是否使用加成
    [SerializeField] private float critChanceMultiplier = 0.75f; // 暴击率加成倍率

    protected override void ApplyTypeBonus() // 应用精英敌人加成
    {
        if (!useBonus) return; // 精英敌人不使用加成
        ApplyAllBonuses(); // 应用所有共有加成
        ApplySpecialStatBonus(critChanceMultiplier, statMultiplier * 0.8f); // 应用特有加成
    }

    public void SetCritChanceMultiplier(float multiplier) => critChanceMultiplier = multiplier; // 设置暴击率倍率
}