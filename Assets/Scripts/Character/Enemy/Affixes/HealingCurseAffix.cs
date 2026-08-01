using UnityEngine;

// 治疗诅咒词缀 — 范围内玩家生命恢复减半
// 通过 AddModifier 修改玩家的 healthRegen 属性，离开范围后移除
public class HealingCurseAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float curseRadius = 5f;       // 诅咒范围（米）
    [SerializeField] private float healingReduction = 0.5f; // 治疗削减比例（0.5 = -50%）
    [SerializeField] private float lingerDuration = 3f;     // 离开后诅咒残留时间（秒）

    public string AffixId => "affix_healing_curse";
    public string DisplayName => "治疗诅咒";
    public AffixTier Tier => AffixTier.Rare;

    private Enemy enemy;
    private Entity_Stats cursedPlayerStats; // 被诅咒的玩家 Stats（用于精准移除）
    private float exitTime;                 // 离开范围的时间
    private bool isActive;                  // 诅咒是否生效中

    public void OnApplied(Enemy enemy) { this.enemy = enemy; }

    public void OnRemoved(Enemy enemy)
    {
        RemoveCurse();
        this.enemy = null;
    }

    public void OnBattleUpdate(Enemy enemy)
    {
        var player = enemy.GetPlayerReference();
        if (player == null)
            return;

        float dist = Vector2.Distance(enemy.transform.position, player.position);
        bool inRange = dist <= curseRadius;

        if (inRange)
        {
            exitTime = Time.time; // 在范围内，刷新离开时间
            if (!isActive)
            {
                // 给玩家 healthRegen 加一个负值 Modifier（-0.5 = 回血减半）
                var stats = player.GetComponentInParent<Entity_Stats>();
                stats?.resources?.healthRegen?.AddModifier(-healingReduction, AffixId);
                cursedPlayerStats = stats;
                isActive = true;
            }
        }
        else if (isActive && Time.time - exitTime > lingerDuration)
        {
            // 离开范围超过残留时间，移除诅咒
            RemoveCurse();
        }
    }

    private void RemoveCurse()
    {
        if (!isActive)
            return;
        cursedPlayerStats?.resources?.healthRegen?.RemoveModifier(AffixId);
        cursedPlayerStats = null;
        isActive = false;
    }

    // 安全网：词缀被移除时确保清理玩家身上的 Modifier
    private void OnDestroy()
    {
        RemoveCurse();
    }

    public float GetLootBonus() => 0.10f;
    public string GetTooltipText() => $"治疗诅咒: {curseRadius}米内玩家生命恢复-{healingReduction * 100}%，离开{lingerDuration}秒恢复";
}
