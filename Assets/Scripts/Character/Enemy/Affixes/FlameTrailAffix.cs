using UnityEngine;

// 烈焰路径词缀 — 移动时在地面留下火焰，踏入造成灼烧
// 通过生成带触发器的火焰 GameObject 实现
public class FlameTrailAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float fireDamagePerSec = 8f;  // 每秒火伤
    [SerializeField] private float puddleLifetime = 3.5f;  // 火焰残留时间
    [SerializeField] private float puddleRadius = 1.2f;    // 火焰半径
    [SerializeField] private float puddleInterval = 0.4f;  // 生成间距（米），每移动此距离留一个
    [SerializeField] private GameObject firePuddlePrefab;  // 火焰预制体

    public string AffixId => "affix_flame_trail";
    public string DisplayName => "烈焰路径";
    public AffixTier Tier => AffixTier.Common;

    private Enemy enemy;
    private float distanceTraveled; // 累计移动距离
    private Vector3 lastPos;       // 上一帧位置

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        lastPos = enemy.transform.position;
        distanceTraveled = 0;
    }

    public void OnRemoved(Enemy enemy)
    {
        this.enemy = null;
    }

    public void OnBattleUpdate(Enemy enemy)
    {
        // 累计移动距离，达到间隔时生成火焰
        Vector3 pos = enemy.transform.position;
        distanceTraveled += Vector3.Distance(pos, lastPos);
        lastPos = pos;

        if (distanceTraveled >= puddleInterval)
        {
            distanceTraveled = 0f;
            if (firePuddlePrefab != null)
            {
                var puddle = Instantiate(firePuddlePrefab, pos, Quaternion.identity);
                var handler = puddle.AddComponent<FlameTrailPuddle>();
                handler.damagePerSec = fireDamagePerSec;
                handler.radius = puddleRadius;
                handler.lifetime = puddleLifetime;
            }
        }
    }

    public float GetLootBonus() => 0.05f;
    public string GetTooltipText() => $"烈焰路径: 移动留下火焰({puddleLifetime}秒)，踏入造成每秒{fireDamagePerSec}点火伤+灼烧";
}

// 火焰地面触发器 — 由 FlameTrailAffix 动态创建
// 挂载在单个火焰 puddle GameObject 上
public class FlameTrailPuddle : MonoBehaviour
{
    public float damagePerSec;  // 每秒伤害（由 FlameTrailAffix 设置）
    public float radius;        // 碰撞半径
    public float lifetime;      // 残留时间

    private float lastTick;     // 上次伤害时间
    private const float TickInterval = 0.5f; // 每秒判定 2 次

    private void Start()
    {
        // 动态添加圆形触发器，大小等于火焰半径
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = radius;
        col.isTrigger = true;

        // 到时自动销毁
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;
        if (Time.time - lastTick < TickInterval)
            return;

        lastTick = Time.time;
        var health = other.GetComponentInParent<Entity_Health>();
        if (health != null)
            health.TakeDamage(0, damagePerSec * TickInterval, ElementType.Fire, transform);
    }
}
