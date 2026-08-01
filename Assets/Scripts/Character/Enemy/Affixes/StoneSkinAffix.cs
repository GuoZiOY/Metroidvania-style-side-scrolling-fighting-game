using UnityEngine;

// 石肤词缀 — 增加护甲值
// 纯数值类，继承 StatAffixBase，通过 AddModifier/RemoveModifier 管理
public class StoneSkinAffix : StatAffixBase
{
    [SerializeField] private float armorBonus = 50f; // 护甲加成值

    public override string AffixId => "affix_stone_skin";
    public override string DisplayName => "石肤";
    public override AffixTier Tier => AffixTier.Common;

    public override float GetLootBonus() => 0.05f;
    public override string GetTooltipText() => $"石肤: 护甲 +{armorBonus}";

    protected override void ApplyStats(Enemy enemy)
    {
        // 用 AffixId 作 source key，确保卸下时精准移除
        if (enemy?.stats?.defense?.armor != null)
            enemy.stats.defense.armor.AddModifier(armorBonus, AffixId);
    }

    protected override void RemoveStats(Enemy enemy)
    {
        if (enemy?.stats?.defense?.armor != null)
            enemy.stats.defense.armor.RemoveModifier(AffixId);
    }
}
