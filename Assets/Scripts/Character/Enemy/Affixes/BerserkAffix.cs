using UnityEngine;

// 狂暴词缀 — 同时增加攻击速度和物理伤害
// 两个属性独立使用 Modifier，可分别移除
public class BerserkAffix : StatAffixBase
{
    [SerializeField] private float attackSpeedBonus = 0.3f; // 攻速加成（0.3 = +30% 攻击间隔缩短）
    [SerializeField] private float damageBonus = 10f;        // 物理伤害加成值

    public override string AffixId => "affix_berserk";
    public override string DisplayName => "狂暴";
    public override AffixTier Tier => AffixTier.Rare;

    public override float GetLootBonus() => 0.10f;
    public override string GetTooltipText() => $"狂暴: 攻速 +{attackSpeedBonus}, 物理伤害 +{damageBonus}";

    protected override void ApplyStats(Enemy enemy)
    {
        var offense = enemy?.stats?.offense;
        if (offense == null)
            return;

        // 攻速和伤害各自独立 Modifier，用 AffixId 统一管理
        offense.attackSpeed?.AddModifier(attackSpeedBonus, AffixId);
        offense.phyiscalDamage?.AddModifier(damageBonus, AffixId);
    }

    protected override void RemoveStats(Enemy enemy)
    {
        var offense = enemy?.stats?.offense;
        if (offense == null)
            return;

        offense.attackSpeed?.RemoveModifier(AffixId);
        offense.phyiscalDamage?.RemoveModifier(AffixId);
    }
}
