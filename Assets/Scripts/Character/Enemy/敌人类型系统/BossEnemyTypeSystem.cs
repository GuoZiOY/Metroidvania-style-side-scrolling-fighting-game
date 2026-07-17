using UnityEngine;

public class BossEnemyTypeSystem : EnemyTypeSystem // Boss敌人类型系统
{
    [Header("Boss特有加成")]
    [SerializeField] private bool useBonus = true; // 是否使用加成

    protected override void ApplyTypeBonus() // 应用Boss敌人加成
    {
        if (!useBonus) return; // 原生Boss不使用加成
        ApplyAllBonuses(); // 应用所有共有加成
        ApplySpecialStatBonus(statMultiplier, statMultiplier); // 应用特有加成
    }

    public void SetUseBonus(bool use) => useBonus = use; // 设置是否使用加成
    public bool GetUseBonus() => useBonus; // 获取是否使用加成
}