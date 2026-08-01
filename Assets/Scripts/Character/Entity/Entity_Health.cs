using System;
using UnityEngine;

public class Entity_Health : MonoBehaviour, IDamgable
{
    public event Action OnHealthUpdate;

    public Entity_VFX entityVFX;
    private Entity_Stats entityStats;
    private Entity entity;

    [SerializeField] private float currentHP;
    [SerializeField] protected bool isDead;

    public bool canBeTakedDamage = true;

    [Header("生命恢复设置")]
    [SerializeField] private float regenInterval = 1;
    [SerializeField] private bool canRegenerateHP = true;
    public float lastDamageTaken { get; private set; }
    public string lastAttackerName { get; private set; }  // 最后一次攻击者名字（死亡原因用）

    [Header("击退设置")]
    public bool canKnockbacked = false;
    [SerializeField] private float knockbackDuration = 0.2f;
    [SerializeField] private Vector2 onDamageKnockback = new Vector2(1.5f, 2.5f);
    [Space]
    [Range(0, 1)]
    [SerializeField] private float heavyKnockThreshold = 0.3f;
    [SerializeField] private float heavyKnockbackDuration = 0.5f;
    [SerializeField] private Vector2 onHeavyDamapeKnockback = new Vector2(7, 7);

    private void Awake()
    {
        entity = GetComponentInParent<Entity>();
        entityVFX = GetComponentInParent<Entity_VFX>();
        entityStats = GetComponent<Entity_Stats>();

        InitializeHealth();
        StartHealthRegeneration();
    }

    public void InitializeHealth()
    {
        if (entityStats == null)
            return;

        currentHP = entityStats.GetMaxHP();
        OnHealthUpdate?.Invoke();
    }

    private void StartHealthRegeneration()
    {
        if (canRegenerateHP)
            InvokeRepeating(nameof(RegenerateHP), 0, regenInterval);
    }

    private void StopHealthRegeneration()
    {
        CancelInvoke(nameof(RegenerateHP));
    }

    public void Revive()
    {
        isDead = false;
        StartHealthRegeneration();
    }

    public virtual bool TakeDamage(float physicalDamage, float elementalDamage, ElementType element, Transform damageDealer, bool isCrit = false)
    {
        if (isDead) return false;

        if (AttackEvaded())
        {
            Debug.Log($"{gameObject.name} 躲避了攻击");
            return false;
        }

        Entity_Stats attackerStats = damageDealer.GetComponent<Entity_Stats>();
        float armorReduction = attackerStats != null ? attackerStats.GetArmorReduction() : 0;
        float mitigation = entityStats != null ? entityStats.GetArmorMitigation(armorReduction) : 0;
        float resistance = entityStats != null ? entityStats.GetElementalResistance(element) : 0;

        float physicalDamageTaken = physicalDamage * (1 - mitigation);
        float elementalDamageTaken = elementalDamage * (1 - resistance);

        TakeKnockBack(damageDealer, physicalDamageTaken);
        ReduceHP(physicalDamageTaken, elementalDamageTaken, element, isCrit);

        lastDamageTaken = physicalDamageTaken + elementalDamageTaken;
        lastAttackerName = damageDealer != null
            ? (damageDealer.GetComponent<Enemy>() != null
                ? damageDealer.GetComponent<Enemy>().enemyName
                : damageDealer.name)
            : "未知";

        // V2: 通知自身 Enemy 的词缀（如反应护甲）
        var selfEnemy = GetComponent<Enemy>();
        if (selfEnemy != null)
            selfEnemy.ReportTookDamage(lastDamageTaken);

        return true;
    }

    public virtual void ReduceHP(float physicalDamageTaken, float elementalDamageTaken, ElementType element, bool isCrit = false)
    {
        if (canBeTakedDamage == false)
            return;

        entityVFX?.PlayOnDamageVfx();

        float totalDamage = physicalDamageTaken + elementalDamageTaken;
        currentHP -= totalDamage;

        OnHealthUpdate?.Invoke();

        if (currentHP <= 0)
            Die();
    }

    public float GetCurrentHP() => currentHP;

    public float GetMaxHP() => entityStats != null ? entityStats.GetMaxHP() : 1;

    public void SetCurrentHP(float hp)
    {
        currentHP = Mathf.Clamp(hp, 0, GetMaxHP());
        OnHealthUpdate?.Invoke();
    }

    private bool AttackEvaded()
    {
        if (entityStats == false)
            return false;
        return UnityEngine.Random.Range(0, 100) < entityStats.GetEvasion();
    }

    private void RegenerateHP()
    {
        if (canRegenerateHP == false)
            return;

        float regenAmount = entityStats.resources.healthRegen.GetValue();
        IncreaseHP(regenAmount);
    }

    public void IncreaseHP(float healAmount)
    {
        if (isDead)
            return;

        float maxHP = GetMaxHP();
        currentHP = Mathf.Min(currentHP + healAmount, maxHP);
        OnHealthUpdate?.Invoke();
    }

    protected virtual void Die()
    {
        isDead = true;
        StopHealthRegeneration();
        entity?.EntityDead();
    }

    public float GetHealthPercent() => currentHP / GetMaxHP();

    public void SetHealthToPercent(float percent)
    {
        currentHP += GetMaxHP() * Mathf.Clamp01(percent);
        OnHealthUpdate?.Invoke();
    }

    private void TakeKnockBack(Transform damageDealer, float finalDamage)
    {
        float duration = CalculateDuration(finalDamage);
        Vector2 knockback = CalculateKnockBack(finalDamage, damageDealer);

        if (canKnockbacked == true) entity.ReciveKnockback(knockback, duration);
    }

    private Vector2 CalculateKnockBack(float damage, Transform damageDealer)
    {
        Vector2 knockback = IsHeavyDanmage(damage) ? onHeavyDamapeKnockback : onDamageKnockback;

        int direction = transform.position.x > damageDealer.transform.position.x ? 1 : -1;
        knockback.x = knockback.x * direction;
        return knockback;
    }

    private float CalculateDuration(float damage)
    {
        return IsHeavyDanmage(damage) ? heavyKnockbackDuration : knockbackDuration;
    }

    private bool IsHeavyDanmage(float damage)
    {
        if (entityStats == null)
            return false;
        return damage / GetMaxHP() >= heavyKnockThreshold;
    }
}
