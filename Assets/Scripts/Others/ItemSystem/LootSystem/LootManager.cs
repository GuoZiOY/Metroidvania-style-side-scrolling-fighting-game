using System.Collections.Generic;
using UnityEngine;

public class LootManager : MonoBehaviour
{
    #region 单例模式

    public static LootManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region 配置参数

    [Header("物品预制体")]
    [SerializeField] private GameObject itemPrefab;

    [Header("货币预制体")]
    [SerializeField] private GameObject copperPrefab;
    [SerializeField] private GameObject silverPrefab;
    [SerializeField] private GameObject goldPrefab;

    [Header("掉落效果")]
    [SerializeField] private float dropHeight = 2f;
    [SerializeField] private float dropForce = 3f;

    private int currencyCoinCount; // 货币生成计数器，用于左右分布

    #endregion

    #region 公共接口

    public void DropLoot(ILootable lootable)
    {
        if (lootable == null)
            return;

        currencyCoinCount = 0;
        List<LootedItem> itemsToDrop = GenerateLoot(lootable);

        if (itemsToDrop.Count > 0)
        {
            SpawnItems(itemsToDrop, lootable.DropPosition);
        }
    }

    public void DropItemDirectly(ItemDataSo itemData, Vector3 position)
    {
        if (itemData == null)
            return;

        LootRarity actualRarity = itemData.GetDroppedRarity();
        LootedItem lootedItem = new LootedItem(itemData, actualRarity);
        DropLootedItemDirectly(lootedItem, position);
    }

    public void DropItemsDirectly(List<ItemDataSo> items, Vector3 position)
    {
        if (items == null || items.Count == 0)
            return;

        List<LootedItem> lootedItems = new List<LootedItem>();
        foreach (var itemData in items)
        {
            if (itemData != null)
            {
                LootRarity actualRarity = itemData.GetDroppedRarity();
                LootedItem lootedItem = new LootedItem(itemData, actualRarity);
                lootedItems.Add(lootedItem);
            }
        }
        DropLootedItemsDirectly(lootedItems, position);
    }

    public void DropLootedItemDirectly(LootedItem lootedItem, Vector3 position)
    {
        if (lootedItem == null)
            return;

        List<LootedItem> items = new List<LootedItem> { lootedItem };
        SpawnItems(items, position);
    }

    public void DropLootedItemsDirectly(List<LootedItem> items, Vector3 position)
    {
        if (items == null || items.Count == 0)
            return;

        SpawnItems(items, position);
    }

    #endregion

    #region 私有方法 - 掉落生成

    private List<LootedItem> GenerateLoot(ILootable lootable)
    {
        List<LootedItem> items = new List<LootedItem>();

        if (lootable.LootTables != null)
        {
            foreach (var table in lootable.LootTables)
            {
                if (table != null)
                    items.AddRange(table.GenerateLoot());
            }
        }

        return items;
    }

    private List<GameObject> spawnedItems = new List<GameObject>();

    private void SpawnItems(List<LootedItem> items, Vector3 position)
    {
        if (items.Count == 0) return;

        Vector3 origin = position + new Vector3(0, dropHeight, 0);
        spawnedItems.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            var lootedItem = items[i];
            if (lootedItem == null) continue;

            // 货币掉落
            if (lootedItem.currencyAmount > 0)
            {
                SpawnCurrency(lootedItem.currencyAmount, position);
                continue;
            }

            // 物品掉落
            if (lootedItem.baseItemData == null) continue;

            GameObject itemObject = Instantiate(itemPrefab, origin, Quaternion.identity);
            ItemAbout itemAbout = itemObject.GetComponent<ItemAbout>();
            if (itemAbout != null)
            {
                itemAbout.InitializeLootedItem(lootedItem);
                // 与之前生成的掉落物互不碰撞
                foreach (var prev in spawnedItems)
                {
                    if (prev != null)
                        Physics2D.IgnoreCollision(itemObject.GetComponent<Collider2D>(), prev.GetComponent<Collider2D>());
                }
                spawnedItems.Add(itemObject);
                ApplyDropPhysics(itemObject, i, items.Count);
            }
        }
    }

    private void SpawnCurrency(int amount, Vector3 position)
    {
        var denoms = new (int worth, GameObject prefab, float scale)[]
        {
            (500000, goldPrefab, 1.8f), (200000, goldPrefab, 1.4f), (100000, goldPrefab, 1.2f), (10000, goldPrefab, 1.0f),
            (5000, silverPrefab, 1.8f), (2000, silverPrefab, 1.4f), (1000, silverPrefab, 1.2f), (100, silverPrefab, 1.0f),
            (50, copperPrefab, 1.8f), (20, copperPrefab, 1.4f), (10, copperPrefab, 1.2f), (1, copperPrefab, 1.0f),
        };

        foreach (var d in denoms)
        {
            if (amount <= 0 || d.prefab == null) break;
            int count = amount / d.worth;
            if (count <= 0) continue;

            // 收集所有要生成的货币，统一左右散开
            int coinStart = currencyCoinCount;
            currencyCoinCount += count;

            for (int i = 0; i < count; i++)
            {
                int idx = coinStart + i;
                Vector3 spawnPos = position + new Vector3(0, dropHeight, 0);
                var coin = Instantiate(d.prefab, spawnPos, Quaternion.identity).transform;
                coin.localScale = Vector3.one * d.scale;
                var gold = coin.GetComponent<Gold>();
                gold.worth = d.worth;
                // 与之前生成的掉落物/金币互不碰撞
                foreach (var prev in spawnedItems)
                {
                    if (prev != null)
                        Physics2D.IgnoreCollision(coin.GetComponent<Collider2D>(), prev.GetComponent<Collider2D>());
                }
                spawnedItems.Add(coin.gameObject);
                ApplyDropPhysics(coin.gameObject, idx, currencyCoinCount);
            }
            amount -= count * d.worth;
        }
    }

    #endregion

    #region 私有方法 - 物理效果

    private void ApplyDropPhysics(GameObject itemObject, int index, int total)
    {
        Rigidbody2D rb = itemObject.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 左右交替，排成一条线
        float direction = (index % 2 == 0) ? -1f : 1f;
        float speed = dropForce * (0.4f + (index / 2) * 0.5f);
        float upSpeed = dropForce * 1.0f;

        rb.linearVelocity = new Vector2(direction * speed, upSpeed);
    }

    #endregion
}
