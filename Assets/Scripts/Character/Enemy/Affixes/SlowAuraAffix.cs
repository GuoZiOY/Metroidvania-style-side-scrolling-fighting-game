using UnityEngine;

// 减速光环词缀 — 周期性对周围玩家施加减速效果
public class SlowAuraAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float auraRadius = 4f;       // 光环范围（米）
    [SerializeField] private float slowMultiplier = 0.3f; // 减速比例（0.3 = 30%减速）
    [SerializeField] private float slowDuration = 1.5f;   // 减速持续时间（秒）

    public string AffixId => "affix_slow_aura";
    public string DisplayName => "减速光环";
    public AffixTier Tier => AffixTier.Rare;

    private Enemy enemy;
    private float lastTickTime;   // 上次判定时间
    private Transform player;     // 缓存的玩家引用
    private const float TickInterval = 1f; // 每秒判定一次

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        lastTickTime = Time.time;
    }

    public void OnRemoved(Enemy enemy)
    {
        this.enemy = null;
        player = null;
    }

    public void OnBattleUpdate(Enemy enemy)
    {
        if (Time.time - lastTickTime < TickInterval)
            return;

        lastTickTime = Time.time;

        // 缓存玩家引用
        if (player == null)
            player = enemy.GetPlayerReference();
        if (player == null)
            return;

        float dist = Vector2.Distance(enemy.transform.position, player.position);
        if (dist <= auraRadius)
        {
            // 通过 Entity.SlowDownEntity 应用减速（使用现有减速系统）
            var entity = player.GetComponentInParent<Entity>();
            if (entity != null)
                entity.SlowDownEntity(slowDuration, slowMultiplier);
        }
    }

    public float GetLootBonus() => 0.10f;
    public string GetTooltipText() => $"减速光环: 周围{auraRadius}米内玩家减速{slowMultiplier * 100}%";

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}
