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

    [Header("掉落效果")]
    [SerializeField] private float dropSpreadRadius = 1.5f;
    [SerializeField] private float dropHeight = 2f;
    [SerializeField] private float dropForce = 3f;
    [SerializeField] private float itemRadius = 0.4f;
    [SerializeField] private float minItemDistance = 0.8f;

    private const float RANDOM_ANGLE_RANGE = 15f;
    private const float RANDOM_RADIUS_RANGE = 0.2f;
    private const float VELOCITY_RANDOM_ANGLE_RANGE = 30f;

    #endregion

    #region 公共接口

    public void DropLoot(ILootable lootable)
    {
        if (lootable == null)
            return;

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

        if (lootable.LootPool != null)
        {
            items.AddRange(lootable.LootPool.GenerateLoot());
        }
        else if (lootable.LootTable != null)
        {
            items.AddRange(lootable.LootTable.GenerateLoot());
        }

        return items;
    }

    private void SpawnItems(List<LootedItem> items, Vector3 position)
    {
        if (items.Count == 0)
            return;

        List<Vector3> dropPositions = CalculateCircularDropPositions(position, items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            var lootedItem = items[i];
            if (lootedItem == null || lootedItem.baseItemData == null)
                continue;

            Vector3 spawnPosition = dropPositions[i];
            GameObject itemObject = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);

            ItemAbout itemAbout = itemObject.GetComponent<ItemAbout>();
            if (itemAbout != null)
            {
                itemAbout.InitializeLootedItem(lootedItem);
                ApplyDropPhysics(itemObject, spawnPosition - position);
            }
        }
    }

    #endregion

    #region 私有方法 - 位置计算

    private List<Vector3> CalculateCircularDropPositions(Vector3 centerPosition, int itemCount)
    {
        List<Vector3> positions = new List<Vector3>();

        if (itemCount == 1)
        {
            positions.Add(centerPosition + new Vector3(0, dropHeight, 0));
            return positions;
        }

        float angleStep = 360f / itemCount;
        float radius = CalculateDropRadius(itemCount);

        for (int i = 0; i < itemCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float randomAngleOffset = UnityEngine.Random.Range(-RANDOM_ANGLE_RANGE, RANDOM_ANGLE_RANGE) * Mathf.Deg2Rad;
            float randomRadiusOffset = UnityEngine.Random.Range(-RANDOM_RADIUS_RANGE, RANDOM_RADIUS_RANGE);

            float finalAngle = angle + randomAngleOffset;
            float finalRadius = Mathf.Max(minItemDistance / 2f, radius + randomRadiusOffset);

            float x = Mathf.Cos(finalAngle) * finalRadius;
            float z = Mathf.Sin(finalAngle) * finalRadius;

            Vector3 dropPosition = centerPosition + new Vector3(x, dropHeight, z);
            positions.Add(dropPosition);
        }

        return positions;
    }

    private float CalculateDropRadius(int itemCount)
    {
        float radius = dropSpreadRadius;
        if (itemCount > 3)
        {
            radius = dropSpreadRadius * (1f + (itemCount - 3) * 0.2f);
        }
        return radius;
    }

    #endregion

    #region 私有方法 - 物理效果

    private void ApplyDropPhysics(GameObject itemObject, Vector3 directionFromCenter)
    {
        Rigidbody2D rb = SetupRigidbody(itemObject);
        SetupCollider(itemObject);

        Vector2 velocityDirection = CalculateVelocityDirection(directionFromCenter);
        Vector2 rotatedDirection = ApplyRandomAngleOffset(velocityDirection);
        rb.linearVelocity = rotatedDirection * dropForce;
    }

    private Rigidbody2D SetupRigidbody(GameObject itemObject)
    {
        Rigidbody2D rb = itemObject.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = itemObject.AddComponent<Rigidbody2D>();
        }
        rb.gravityScale = 2f;
        return rb;
    }

    private void SetupCollider(GameObject itemObject)
    {
        Collider2D collider = itemObject.GetComponent<Collider2D>();
        if (collider == null)
        {
            collider = itemObject.AddComponent<CircleCollider2D>();
            ((CircleCollider2D)collider).radius = itemRadius;
        }
        collider.isTrigger = true;
    }

    private Vector2 CalculateVelocityDirection(Vector3 directionFromCenter)
    {
        Vector2 velocityDirection = new Vector2(directionFromCenter.x, directionFromCenter.z).normalized;
        if (velocityDirection == Vector2.zero)
        {
            velocityDirection = UnityEngine.Random.insideUnitCircle.normalized;
        }
        return velocityDirection;
    }

    private Vector2 ApplyRandomAngleOffset(Vector2 direction)
    {
        float randomAngle = UnityEngine.Random.Range(-VELOCITY_RANDOM_ANGLE_RANGE, VELOCITY_RANDOM_ANGLE_RANGE) * Mathf.Deg2Rad;
        float cosAngle = Mathf.Cos(randomAngle);
        float sinAngle = Mathf.Sin(randomAngle);
        return new Vector2(
            direction.x * cosAngle - direction.y * sinAngle,
            direction.x * sinAngle + direction.y * cosAngle
        );
    }

    #endregion
}
