using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG设置/战利品系统/掉落池", fileName = "LootPool -")]
public class LootPool : ScriptableObject
{
    [System.Serializable]
    public class PoolEntry
    {
        [SerializeField] private LootTable lootTable; //掉落表
        [SerializeField] private float weight = 1f; //权重
        [SerializeField] private int minDrops = 1; //最小掉落次数
        [SerializeField] private int maxDrops = 1; //最大掉落次数

        public LootTable LootTable 
        { 
            get => lootTable; 
            set => lootTable = value; 
        }
        public float Weight 
        { 
            get => weight; 
            set => weight = value; 
        }
        public int MinDrops 
        { 
            get => minDrops; 
            set => minDrops = value; 
        }
        public int MaxDrops 
        { 
            get => maxDrops; 
            set => maxDrops = value; 
        }
    }

    [Header("掉落池配置")]
    [SerializeField] private List<PoolEntry> poolEntries = new List<PoolEntry>(); //掉落池条目

    [Header("全局配置")]
    [SerializeField] private int minTotalDrops = 1; //最小总掉落数
    [SerializeField] private int maxTotalDrops = 5; //最大总掉落数
    [SerializeField] private bool allowDuplicates = false; //是否允许重复掉落

    public List<LootedItem> GenerateLoot() //生成掉落物品
    {
        List<LootedItem> droppedItems = new List<LootedItem>();
        List<PoolEntry> availableEntries = new List<PoolEntry>(poolEntries);

        int totalDrops = UnityEngine.Random.Range(minTotalDrops, maxTotalDrops + 1);

        for (int i = 0; i < totalDrops; i++)
        {
            PoolEntry selectedEntry = SelectRandomEntry(availableEntries);

            if (selectedEntry != null && selectedEntry.LootTable != null)
            {
                int dropCount = UnityEngine.Random.Range(selectedEntry.MinDrops, selectedEntry.MaxDrops + 1);
                for (int j = 0; j < dropCount; j++)
                {
                    List<LootedItem> tableLoot = selectedEntry.LootTable.GenerateLoot();
                    droppedItems.AddRange(tableLoot);
                }

                if (!allowDuplicates)
                {
                    availableEntries.Remove(selectedEntry);
                }
            }
        }

        return droppedItems;
    }

    private PoolEntry SelectRandomEntry(List<PoolEntry> entries) //随机选择一个掉落池条目
    {
        if (entries.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (var entry in entries)
        {
            totalWeight += entry.Weight;
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var entry in entries)
        {
            currentWeight += entry.Weight;
            if (randomValue <= currentWeight)
            {
                return entry;
            }
        }

        return entries[entries.Count - 1];
    }

    public void AddLootTable(LootTable table, float weight = 1f, int minDrops = 1, int maxDrops = 1) //添加掉落表
    {
        if (table == null)
            return;

        PoolEntry entry = new PoolEntry();
        entry = new PoolEntry
        {
            LootTable = table,
            Weight = weight,
            MinDrops = minDrops,
            MaxDrops = maxDrops
        };
        poolEntries.Add(entry);
    }

    public void RemoveLootTable(LootTable table) //移除掉落表
    {
        poolEntries.RemoveAll(entry => entry.LootTable == table);
    }
}
