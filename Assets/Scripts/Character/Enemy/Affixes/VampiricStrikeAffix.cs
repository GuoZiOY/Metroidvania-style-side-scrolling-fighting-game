using UnityEngine;

// 吸血打击词缀 — 造成伤害的 15% 转化为生命恢复
// 通过订阅 Enemy.OnEnemyDealtDamage 事件实现
public class VampiricStrikeAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float lifestealRatio = 0.15f; // 吸血比例（0.15 = 15%）

    public string AffixId => "affix_vampiric_strike";
    public string DisplayName => "吸血打击";
    public AffixTier Tier => AffixTier.Legendary;

    private Enemy enemy;
    private Entity_Health health; // 敌人自身血量组件，用于 IncreaseHP

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        health = enemy.GetComponent<Entity_Health>();
        enemy.OnEnemyDealtDamage += OnDealtDamage; // 每次对玩家造成伤害时触发
    }

    public void OnRemoved(Enemy enemy)
    {
        if (enemy != null)
            enemy.OnEnemyDealtDamage -= OnDealtDamage;
        this.enemy = null;
    }

    // 伤害事件回调：按比例回复血量
    private void OnDealtDamage(float totalDamage)
    {
        if (health != null)
            health.IncreaseHP(totalDamage * lifestealRatio);
    }

    public void OnBattleUpdate(Enemy enemy) { } // 无逐帧行为，生命恢复由伤害事件驱动
    public float GetLootBonus() => 0.20f;
    public string GetTooltipText() => $"吸血打击: 造成伤害的{lifestealRatio * 100}%转为生命恢复";
}
