using System.Collections;
using UnityEngine;

// 冰霜新星词缀 — 周期性释放扩散冰环，造成冰伤+减速
public class IceNovaAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float novaRadius = 4f;        // 冰环最终半径（米）
    [SerializeField] private float iceDamage = 15f;        // 冰伤
    [SerializeField] private float slowAmount = 0.4f;      // 减速比例（0.4 = 40%）
    [SerializeField] private float slowDuration = 2.5f;    // 减速持续时间（秒）
    [SerializeField] private float cooldown = 8f;          // 两次释放间隔（秒）
    [SerializeField] private GameObject novaVfxPrefab;     // 冰环 VFX 预制体

    public string AffixId => "affix_ice_nova";
    public string DisplayName => "冰霜新星";
    public AffixTier Tier => AffixTier.Rare;

    private Enemy enemy;
    private float lastNovaTime = float.MinValue; // 初始化为极小值，首次进入战斗即可释放

    public void OnApplied(Enemy enemy) { this.enemy = enemy; }
    public void OnRemoved(Enemy enemy) { this.enemy = null; }

    public void OnBattleUpdate(Enemy enemy)
    {
        if (Time.time - lastNovaTime < cooldown)
            return;

        lastNovaTime = Time.time;
        StartCoroutine(NovaRoutine());
    }

    // 冰环协程：先播放 VFX，短暂延迟后判定命中
    private IEnumerator NovaRoutine()
    {
        if (novaVfxPrefab != null)
            Instantiate(novaVfxPrefab, enemy.transform.position, Quaternion.identity);

        // 0.3 秒扩散延迟，给玩家闪避反应时间
        yield return new WaitForSeconds(0.3f);

        // 物理判定：检测冰环范围内的玩家
        var hits = Physics2D.OverlapCircleAll(enemy.transform.position, novaRadius, LayerMask.GetMask("Player"));
        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<Entity_Health>();
            if (health != null)
                health.TakeDamage(0, iceDamage, ElementType.Ice, enemy.transform);

            var entity = hit.GetComponentInParent<Entity>();
            if (entity != null)
                entity.SlowDownEntity(slowDuration, slowAmount);
        }
    }

    public float GetLootBonus() => 0.10f;
    public string GetTooltipText() => $"冰霜新星: 每{cooldown}秒释放冰环({novaRadius}米)，{iceDamage}冰伤+{slowAmount * 100}%减速{slowDuration}秒";
}
