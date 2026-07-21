using UnityEngine;

public class LootDropper : MonoBehaviour, ILootable
{
    [Header("战利品配置")]
    [SerializeField] private LootTable[] lootTables; //掉落表列表（替代原掉落池+单掉落表）

    [Header("掉落位置偏移")]
    [SerializeField] private Vector3 dropOffset = Vector3.zero; //掉落位置偏移

    private bool hasDropped = false; //是否已经掉落过物品

    public LootTable[] LootTables => lootTables; //获取掉落表列表
    public Vector3 DropPosition => transform.position + dropOffset; //获取掉落位置

    public void OnDrop() //掉落时调用（实现ILootable接口）
    {
        if (hasDropped) return;

        if (LootManager.Instance != null)
        {
            LootManager.Instance.DropLoot(this);
            hasDropped = true;
        }
    }

    public void SetLootTables(LootTable[] tables) //设置掉落表列表
    {
        lootTables = tables;
    }
}
