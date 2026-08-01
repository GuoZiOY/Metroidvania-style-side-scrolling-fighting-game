using System.Collections;
using UnityEngine;

// 自爆词缀 — 死亡后延时爆炸，对范围内玩家造成最大生命百分比伤害
// 爆炸前有 3 秒闪烁预警，给玩家撤离时间
// 修复：死亡瞬间（OnEntityDead）即启动爆炸，爆炸逻辑跑在独立对象上，
//       不随 Enemy.Destroy(gameObject, 2) 销毁、不被 OnRemoved 停止协程
public class SelfDestructAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float delay = 3f;             // 死亡到爆炸的延迟（秒）
    [SerializeField] private float blastRadius = 5f;       // 爆炸半径（米）
    [SerializeField] private float damageRatio = 0.5f;     // 伤害 = MaxHP × 此比例
    [SerializeField] private GameObject explosionVfxPrefab; // 爆炸 VFX 预制体

    public string AffixId => "affix_self_destruct";
    public string DisplayName => "自爆";
    public AffixTier Tier => AffixTier.Rare;

    private Enemy enemy;
    private bool isPrimed; // 是否已进入爆炸倒计时

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        enemy.OnEntityDead += OnEnemyDead; // 订阅死亡事件：死亡瞬间即启动爆炸倒计时
    }

    public void OnRemoved(Enemy enemy)
    {
        if (enemy != null)
            enemy.OnEntityDead -= OnEnemyDead;
        this.enemy = null;
    }

    // 敌人死亡回调（由 Enemy.EntityDead → base.EntityDead → OnEntityDead 触发）
    private void OnEnemyDead()
    {
        if (isPrimed)
            return;

        isPrimed = true;
        PrimedSelfDestruct(enemy);
    }

    // 在独立临时对象上启动爆炸倒计时，避免被 Enemy 销毁连带终止协程
    private void PrimedSelfDestruct(Enemy deadEnemy)
    {
        if (deadEnemy == null)
            return;

        // 创建独立的临时对象承载爆炸逻辑，与 Enemy 生命周期解耦
        GameObject bombRunner = new GameObject($"SelfDestruct_{deadEnemy.name}");
        bombRunner.transform.position = deadEnemy.transform.position;
        bombRunner.transform.localScale = deadEnemy.transform.localScale;

        var bomb = bombRunner.AddComponent<SelfDestructBomb>();
        bomb.Init(deadEnemy, delay, blastRadius, damageRatio, explosionVfxPrefab);
    }

    // 数值词缀逻辑可保留 OnBattleUpdate 为空实现
    public void OnBattleUpdate(Enemy enemy) { }

    public float GetLootBonus() => 0.10f;
    public string GetTooltipText() => $"自爆: 死亡后{delay}秒爆炸，半径{blastRadius}米，伤害={damageRatio * 100}%最大生命";
}

// 自爆执行器 — 独立于 Enemy 生命周期，在死亡敌人位置执行闪烁预警与爆炸
public class SelfDestructBomb : MonoBehaviour
{
    private float delay;              // 爆炸延迟（秒）
    private float blastRadius;        // 爆炸半径（米）
    private float damageRatio;        // 伤害 = MaxHP × 此比例
    private GameObject explosionVfx;  // 爆炸 VFX 预制体

    // 由 SelfDestructAffix 调用，缓存死亡敌人数据后启动倒计时
    public void Init(Enemy deadEnemy, float delaySec, float radius, float ratio, GameObject vfx)
    {
        delay = delaySec;
        blastRadius = radius;
        damageRatio = ratio;
        explosionVfx = vfx;

        // 缓存最大生命，避免爆炸时 enemy 已被销毁
        float maxHp = deadEnemy.stats != null ? deadEnemy.stats.GetMaxHP() : 100f;
        StartCoroutine(ExplosionRoutine(maxHp));
    }

    private IEnumerator ExplosionRoutine(float maxHp)
    {
        float elapsed = 0;
        Transform t = transform;

        // 闪烁预警：缩放抖动（持续 delay 秒）
        while (elapsed < delay)
        {
            float scale = 1f + Mathf.Sin(elapsed * 10f) * 0.1f;
            t.localScale = Vector3.one * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 爆炸 VFX
        if (explosionVfx != null)
            Instantiate(explosionVfx, t.position, Quaternion.identity);

        // 伤害 = 敌人最大 HP × 比例（纯物理伤害）
        float damage = maxHp * damageRatio;
        var hits = Physics2D.OverlapCircleAll(t.position, blastRadius, LayerMask.GetMask("Player"));
        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<Entity_Health>();
            if (health != null)
                health.TakeDamage(damage, 0, ElementType.None, t);
        }

        // 爆炸完成后销毁临时对象
        Destroy(gameObject);
    }
}
