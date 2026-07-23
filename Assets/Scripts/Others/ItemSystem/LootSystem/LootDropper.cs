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

        if (LootManager.Instance != null)
            LootManager.Instance.DropLoot(this);
    }

    public void SetLootTables(LootTable[] tables) => lootTables = tables;
}
