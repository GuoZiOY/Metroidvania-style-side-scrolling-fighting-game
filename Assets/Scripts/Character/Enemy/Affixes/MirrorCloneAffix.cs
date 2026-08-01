using UnityEngine;

// 镜像分身词缀 — HP 低于 50% 时分裂出 40% HP 的无词缀复制体
// 一场战斗只触发一次
public class MirrorCloneAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float triggerHpRatio = 0.5f; // 触发阈值（HP 低于此比例）
    [SerializeField] private float cloneHpRatio = 0.4f;   // 分身 HP = 原体 MaxHP × 此比例

    public string AffixId => "affix_mirror_clone";
    public string DisplayName => "镜像分身";
    public AffixTier Tier => AffixTier.Legendary;

    private Enemy enemy;
    private Entity_Health health;
    private bool hasTriggered; // 确保只触发一次

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        health = enemy.GetComponent<Entity_Health>();
        enemy.OnEnemyTookDamage += CheckTrigger; // 每次受击检查 HP 阈值
    }

    public void OnRemoved(Enemy enemy)
    {
        if (enemy != null)
            enemy.OnEnemyTookDamage -= CheckTrigger;
        this.enemy = null;
    }

    // 受击后检查是否达到分裂阈值
    private void CheckTrigger(float damage)
    {
        if (hasTriggered || health == null)
            return;

        float hpRatio = health.GetCurrentHP() / (enemy.stats?.GetMaxHP() ?? 1);
        if (hpRatio <= triggerHpRatio)
        {
            hasTriggered = true;
            SpawnClone();
        }
    }

    private void SpawnClone()
    {
        // 在随机偏移位置生成分身
        Vector2 offset = Random.insideUnitCircle * 2f;
        Vector3 spawnPos = enemy.transform.position + (Vector3)offset;

        GameObject clone = Instantiate(enemy.gameObject, spawnPos, Quaternion.identity);

        // 分身移除所有词缀（分身不再分裂）
        var cloneEnemy = clone.GetComponent<Enemy>();
        if (cloneEnemy != null)
            cloneEnemy.RemoveAllAffixes();

        // 设置分身的初始 HP
        float hp = (enemy.stats?.GetMaxHP() ?? 100) * cloneHpRatio;
        clone.GetComponent<Entity_Health>()?.SetCurrentHP(hp);
    }

    public void OnBattleUpdate(Enemy enemy) { }
    public float GetLootBonus() => 0.20f;
    public string GetTooltipText() => $"镜像分身: HP<{triggerHpRatio * 100}%时分裂出{cloneHpRatio * 100}%HP无词缀复制体";
}
