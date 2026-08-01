using UnityEngine;

public class LootDropper : MonoBehaviour, ILootable
{
    [Header("战利品配置")]
    [SerializeField] private LootTable[] lootTables;

    [Header("掉落位置偏移")]
    [SerializeField] private Vector3 dropOffset = Vector3.zero;

    private bool hasDropped = false;

    public LootTable[] LootTables => lootTables;
    public Vector3 DropPosition => transform.position + dropOffset;

    public void OnDrop()
    {
        if (hasDropped) return;
        hasDropped = true;

        // 计算精英词缀的掉落稀有度加成（GetLootBonus 之和），传递给 LootManager
        float affixBonus = CalculateAffixLootBonus();
        if (LootManager.Instance != null)
            LootManager.Instance.DropLoot(this, affixBonus);
    }

    // 汇总敌人身上所有词缀的掉落加成
    // 普通词缀 +5%，稀有 +10%，传说 +20%；3 词缀传说精英 = +60%
    private float CalculateAffixLootBonus()
    {
        var enemy = GetComponentInParent<Enemy>();
        if (enemy == null || enemy.ActiveAffixes == null || enemy.ActiveAffixes.Count == 0)
            return 0f;

        float bonus = 0f;
        foreach (var affix in enemy.ActiveAffixes)
        {
            if (affix != null)
                bonus += affix.GetLootBonus();
        }
        return bonus;
    }

    public void SetLootTables(LootTable[] tables) => lootTables = tables;
}
