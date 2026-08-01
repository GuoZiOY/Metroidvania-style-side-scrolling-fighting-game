using UnityEngine;

// 火焰光环词缀 — 周期性对光环范围内的玩家造成火元素伤害
// 行为类，直接实现 IEnemyAffix
public class FireAuraAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float auraRadius = 3f;     // 光环半径（米）
    [SerializeField] private float damagePerTick = 5f;  // 每跳火伤
    [SerializeField] private float tickInterval = 0.5f; // 间隔（秒），每秒2跳

    public string AffixId => "affix_fire_aura";
    public string DisplayName => "火焰光环";
    public AffixTier Tier => AffixTier.Common;

    private Enemy enemy;
    private float lastTickTime;  // 上次判定时间
    private Transform player;    // 缓存的玩家引用

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
        // 冷却检查
        if (Time.time - lastTickTime < tickInterval)
            return;

        lastTickTime = Time.time;

        // 缓存玩家引用，避免每帧 GetComponent
        if (player == null)
            player = enemy.GetPlayerReference();
        if (player == null)
            return;

        // 距离判定
        float dist = Vector2.Distance(enemy.transform.position, player.position);
        if (dist <= auraRadius)
        {
            var health = player.GetComponentInParent<Entity_Health>();
            // 光环伤害只有元素部分（火伤），不造成物理伤害
            if (health != null)
                health.TakeDamage(0, damagePerTick, ElementType.Fire, enemy.transform);
        }
    }

    public float GetLootBonus() => 0.05f;
    public string GetTooltipText() => $"火焰光环: 每{tickInterval}秒对周围{auraRadius}范围造成{damagePerTick}点火伤";

    // Editor 可视化：在 Scene 窗口显示光环范围
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}
