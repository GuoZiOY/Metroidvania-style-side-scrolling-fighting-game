using UnityEngine;

// 身体接触伤害（史莱姆专属）——重叠检测 + 每源冷却，防"站进身体逐帧秒杀"
// 与玩家受击无敌帧互补：每源冷却管"同源反复刷"，无敌帧管"多源叠加"
[RequireComponent(typeof(Collider2D))]
public class ContactDamageArea : MonoBehaviour
{
    [SerializeField] private float contactDamage = 0.12f;   // 接触伤害（目标 MaxHP 的百分比，0.12=12%）
    [SerializeField] private float hitCooldown = 1f;        // 每源冷却（秒）
    [SerializeField] private bool damagePercentOfMaxHp = true; // true=按目标 MaxHP 百分比

    private float lastHitTime = float.NegativeInfinity; // 上次造成伤害的时间（每源冷却计时；负无穷使首击立即生效）

    private void Awake()
    {
        // 强制触发器：OnTriggerStay2D 只在 Trigger 上派发（挂到专用 trigger 碰撞体，勿挂物理身体 collider）
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player") == false)
            return; // 只伤害玩家（防误伤己方迷你王/其他敌人）
        if (Time.time - lastHitTime < hitCooldown)
            return; // 冷却内不重复触发

        if (other.TryGetComponent<Entity_Health>(out var health) == false)
            return;

        float dmg = damagePercentOfMaxHp ? health.GetMaxHP() * contactDamage : contactDamage;
        if (health.TakeDamage(dmg, 0f, ElementType.None, transform))
            lastHitTime = Time.time; // 只有真正造成伤害才刷新冷却（无敌帧/闪避/死亡挡下时不消耗）
    }
}
