using UnityEngine;

// 身体接触伤害（史莱姆专属）——重叠检测 + 每源冷却，防"站进身体逐帧秒杀"
// 与玩家受击无敌帧互补：每源冷却管"同源反复刷"，无敌帧管"多源叠加"
[RequireComponent(typeof(Collider2D))]
public class ContactDamageArea : MonoBehaviour
{
    [SerializeField] private float contactDamage = 0.12f;   // 接触伤害（目标 MaxHP 的百分比，0.12=12%）
    [SerializeField] private float hitCooldown = 1f;        // 每源冷却（秒）
    [SerializeField] private bool damagePercentOfMaxHp = true; // true=按目标 MaxHP 百分比

    private float lastHitTime; // 上次造成伤害的时间（每源冷却计时）

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time - lastHitTime < hitCooldown)
            return; // 冷却内不重复触发

        if (other.TryGetComponent<Entity_Health>(out var health) == false)
            return;

        float dmg = damagePercentOfMaxHp ? health.GetMaxHP() * contactDamage : contactDamage;
        health.TakeDamage(dmg, 0f, ElementType.None, transform);
        lastHitTime = Time.time;
    }
}
