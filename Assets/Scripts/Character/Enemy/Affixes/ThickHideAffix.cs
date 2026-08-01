using UnityEngine;

// 厚皮词缀 — 增加最大生命值百分比
// 使用 ApplyMultiplier 而非 AddModifier，因为 MaxHP 需要等比缩放
public class ThickHideAffix : StatAffixBase
{
    [SerializeField] private float hpMultiplier = 0.3f; // 生命倍率加成（0.3 = +30%）

    public override string AffixId => "affix_thick_hide";
    public override string DisplayName => "血量加强";
    public override AffixTier Tier => AffixTier.Common;
    public override float GetLootBonus() => 0.05f;
    public override string GetTooltipText() => $"厚皮: 最大生命 +{hpMultiplier * 100}%";

    protected override void ApplyStats(Enemy enemy)
    {
        if (enemy?.stats?.resources?.maxHP != null)
            enemy.stats.resources.maxHP.ApplyMultiplier(1f + hpMultiplier);
    }

    protected override void RemoveStats(Enemy enemy)
    {
        // 反向乘回原值，恢复初始 MaxHP
        if (enemy?.stats?.resources?.maxHP != null)
            enemy.stats.resources.maxHP.ApplyMultiplier(1f / (1f + hpMultiplier));
    }
}
