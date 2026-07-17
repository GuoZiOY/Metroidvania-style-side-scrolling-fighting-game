using UnityEngine;

public class LootDropper : MonoBehaviour, ILootable
{
    [Header("战利品配置")]
    [SerializeField] private LootTable lootTable; //掉落表
    [SerializeField] private LootPool lootPool; //掉落池

    [Header("掉落位置偏移")]
    [SerializeField] private Vector3 dropOffset = Vector3.zero; //掉落位置偏移

    private bool hasDropped = false; //是否已经掉落过物品

    public LootTable LootTable => lootTable; //获取掉落表
    public LootPool LootPool => lootPool; //获取掉落池
    public Vector3 DropPosition => transform.position + dropOffset; //获取掉落位置

    private void OnValidate()
    {
        if (lootTable != null && lootPool != null)
        {
            Debug.LogWarning($"{gameObject.name}: 同时设置了掉落表和掉落池，将优先使用掉落池");
        }
    }

    public void OnDrop() //掉落时调用（实现ILootable接口）
    {
        if (hasDropped)return;
        
        if (LootManager.Instance != null)
        {
            LootManager.Instance.DropLoot(this);
            hasDropped = true; //标记为已掉落
        }
    }

    public void SetLootTable(LootTable table) //设置掉落表
    {
        lootTable = table;
        lootPool = null;
    }

    public void SetLootPool(LootPool pool) //设置掉落池
    {
        lootPool = pool;
        lootTable = null;
    }
}
