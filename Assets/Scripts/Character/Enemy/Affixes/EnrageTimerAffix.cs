using UnityEngine;

// 激怒词缀 — 战斗中每 8 秒叠加一层：+6 伤害 +0.08 攻速，但 -12 护甲
// 无层数上限，脱离战斗后层数清零
public class EnrageTimerAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float interval = 8f;           // 叠层间隔（秒）
    [SerializeField] private float damagePerStack = 6f;     // 每层物理伤害加成
    [SerializeField] private float attackSpeedPerStack = 0.08f; // 每层攻速加成
    [SerializeField] private float armorLossPerStack = 12f; // 每层护甲减少

    public string AffixId => "affix_enrage_timer";
    public string DisplayName => "激怒";
    public AffixTier Tier => AffixTier.Common;

    private Enemy enemy;
    private float timer;  // 距下一次叠层的时间
    private int stacks;   // 当前层数

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        timer = 0;
        stacks = 0;
    }

    public void OnRemoved(Enemy enemy)
    {
        RemoveAllStacks(); // 移除时需还原所有属性
        this.enemy = null;
    }

    public void OnBattleUpdate(Enemy enemy)
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0;
            stacks++;
            ApplyStack(); // 叠加一层
        }
    }

    // 使用 AddBaseValue 而非 AddModifier，因为层数可叠加且脱离战斗后需全部移除
    private void ApplyStack()
    {
        var stats = enemy?.stats;
        if (stats == null)
            return;

        stats.offense.phyiscalDamage?.AddBaseValue(damagePerStack);
        stats.offense.attackSpeed?.AddBaseValue(attackSpeedPerStack);
        stats.defense.armor?.AddBaseValue(-armorLossPerStack); // 负值 = 减少护甲
    }

    // 脱离战斗时一次性还原所有层数
    private void RemoveAllStacks()
    {
        var stats = enemy?.stats;
        if (stats == null || stacks <= 0)
            return;

        stats.offense.phyiscalDamage?.AddBaseValue(-damagePerStack * stacks);
        stats.offense.attackSpeed?.AddBaseValue(-attackSpeedPerStack * stacks);
        stats.defense.armor?.AddBaseValue(armorLossPerStack * stacks); // 加回护甲
    }

    public float GetLootBonus() => 0.05f;
    public string GetTooltipText() => $"激怒: 战斗中每{interval}秒+{damagePerStack}伤害+{attackSpeedPerStack}攻速，-{armorLossPerStack}护甲(已叠{stacks}层)";
}
