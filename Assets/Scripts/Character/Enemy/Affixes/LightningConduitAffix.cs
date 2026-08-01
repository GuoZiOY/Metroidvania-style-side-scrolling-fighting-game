using UnityEngine;

// 闪电导体词缀 — 每次命中累积充能，满 100% 触发雷击+眩晕
// 充能在脱战时每秒衰减
public class LightningConduitAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float chargePerHit = 15f;     // 每次命中充能百分比
    [SerializeField] private float maxCharge = 100f;       // 触发阈值
    [SerializeField] private float strikeDamage = 25f;     // 雷击伤害
    [SerializeField] private float stunDuration = 0.5f;    // 眩晕时间
    [SerializeField] private float decayPerSec = 5f;       // 脱战每秒衰减
    [SerializeField] private GameObject lightningVfxPrefab; // 雷击 VFX 预制体

    public string AffixId => "affix_lightning_conduit";
    public string DisplayName => "闪电导体";
    public AffixTier Tier => AffixTier.Rare;

    private Enemy enemy;
    private float currentCharge;  // 当前充能值（0-100）
    private bool isInBattle;      // 本帧是否处于战斗（由 OnDealtDamage 标记）

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        enemy.OnEnemyDealtDamage += OnDealtDamage; // 订阅伤害事件，每次命中累加充能
    }

    public void OnRemoved(Enemy enemy)
    {
        if (enemy != null)
            enemy.OnEnemyDealtDamage -= OnDealtDamage;
        currentCharge = 0;
        this.enemy = null;
    }

    // 敌人在本帧造成伤害 → 累加充能
    private void OnDealtDamage(float totalDamage)
    {
        isInBattle = true;
        currentCharge += chargePerHit;

        if (currentCharge >= maxCharge)
            TriggerStrike();
    }

    // 脱战衰减：非战斗帧衰减，战斗帧重置标记
    public void OnBattleUpdate(Enemy enemy)
    {
        if (!isInBattle)
        {
            currentCharge = Mathf.Max(0, currentCharge - decayPerSec * Time.deltaTime);
        }
        isInBattle = false; // 每帧重置，等待 OnDealtDamage 重新标记
    }

    private void TriggerStrike()
    {
        currentCharge = 0; // 触发后重置充能

        var player = enemy.GetPlayerReference();
        if (player == null)
            return;

        // 在玩家位置生成雷击 VFX
        if (lightningVfxPrefab != null)
            Instantiate(lightningVfxPrefab, player.position, Quaternion.identity);

        // 造成雷元素伤害
        var health = player.GetComponentInParent<Entity_Health>();
        if (health != null)
            health.TakeDamage(0, strikeDamage, ElementType.Lightning, enemy.transform);

        // 眩晕：玩家不是 Enemy，无 TryStun。
        // 用 Entity.SlowDownEntity(时长, 1f) 完全减速 = 定身，模拟 0.5 秒眩晕
        var playerEntity = player.GetComponentInParent<Entity>();
        if (playerEntity != null)
            playerEntity.SlowDownEntity(stunDuration, 1f);
    }

    public float GetLootBonus() => 0.10f;
    public string GetTooltipText() => $"闪电导体: 命中+{chargePerHit}%充能，满{maxCharge}%雷击{strikeDamage}雷伤+{stunDuration}秒眩晕";
}
