using UnityEngine;

// 屏障护盾词缀 — 每 15 秒生成吸收护盾，护盾量为敌人最大 HP 的 25%
// 修复：护盾通过 OnTookDamage 后的 IncreaseHP 回补实现真实伤害吸收
//       （Entity_Health 不知晓护盾，需在受击回调中把被吸收的部分补回 HP）
public class BarrierShieldAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float shieldHpRatio = 0.25f; // 护盾量 = MaxHP 比例
    [SerializeField] private float cooldown = 15f;        // 护盾再生间隔（秒）

    public string AffixId => "affix_barrier_shield";
    public string DisplayName => "屏障护盾";
    public AffixTier Tier => AffixTier.Legendary;

    private Enemy enemy;
    private Entity_Health health;               // 敌人血量组件（用于回补被护盾吸收的伤害）
    private float shieldHp;                     // 当前护盾剩余量
    private float lastShieldTime = float.MinValue; // 上次护盾生成/破碎时间

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        health = enemy.GetComponent<Entity_Health>();
        enemy.OnEnemyTookDamage += OnTookDamage;
    }

    public void OnRemoved(Enemy enemy)
    {
        if (enemy != null)
            enemy.OnEnemyTookDamage -= OnTookDamage;
        this.enemy = null;
    }

    // 受击时护盾先吸收伤害：把被护盾吸收的部分回补到敌人 HP，等效减免
    private void OnTookDamage(float damage)
    {
        if (shieldHp <= 0)
            return;

        float absorbed = Mathf.Min(damage, shieldHp);
        shieldHp -= absorbed;

        // 回补被吸收的伤害（护盾抵挡 = 没扣这部分 HP）
        // 若伤害致死（damage > 剩余HP），IncreaseHP 不会复活已死敌人，护盾不防死亡
        if (absorbed > 0 && health != null)
            health.IncreaseHP(absorbed);
    }

    // 每帧检查是否需要重新生成护盾
    public void OnBattleUpdate(Enemy enemy)
    {
        if (Time.time - lastShieldTime > cooldown && shieldHp <= 0)
        {
            float maxHp = enemy.stats?.GetMaxHP() ?? 100;
            shieldHp = maxHp * shieldHpRatio;
            lastShieldTime = Time.time;
        }
    }

    public float GetLootBonus() => 0.20f;
    public string GetTooltipText() => $"屏障护盾: 每{cooldown}秒生成{shieldHpRatio * 100}%HP护盾，吸收等量伤害";
}
