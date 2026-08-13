using UnityEngine;

public class ItemAbout : MonoBehaviour
{
    [Header("稀有度背景设置")]
    [SerializeField] private SpriteRenderer rarityBackground;
    [SerializeField] private float rarityBackgroundAlpha = 0.5f;

    private Inventory_Base inventory;
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private ItemDataSo itemData;
    private LootedItem lootedItem;
    private Inventory_Item itemToAdd;
    private bool isPickedUp = false;
    private bool hasLanded = false;

    [Header("地面检测")]
    [SerializeField] private float groundCheckDistance = 0.6f;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        InitializeDefaultItem();
    }

    private void Update()
    {
        CheckLanding();
    }

    private void InitializeDefaultItem()
    {
        if (itemData != null)
            itemToAdd = new Inventory_Item(itemData);
    }

    public void InitializeItem(ItemDataSo data)
    {
        itemData = data;
        lootedItem = null;
        itemToAdd = new Inventory_Item(itemData);
        if (itemData != null)
        {
            gameObject.name = itemData.itemName;
            if (sr != null) sr.sprite = itemData.itemIcon;
        }
    }

    public void InitializeLootedItem(LootedItem data)
    {
        if (data == null || data.baseItemData == null)
        {
            Debug.LogError("[ItemAbout] InitializeLootedItem: 数据为空");
            return;
        }
        lootedItem = data;
        itemData = data.baseItemData;
        itemToAdd = new Inventory_Item(lootedItem);
        gameObject.name = lootedItem.GetDisplayName();
        if (sr != null)
        {
            sr.sprite = itemData.itemIcon;
            sr.color = Color.white;
        }
        UpdateRarityBackground();
    }

    private void CheckLanding()
    {
        if (hasLanded || rb == null) return;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance, LayerMask.GetMask("Ground"));
        if (hit.collider != null)
        {
            hasLanded = true;
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePickup(other);
    }

    private void OnValidate()
    {
        if (itemData != null)
            gameObject.name = itemData.itemName;
    }

    private void HandlePickup(Collider2D other)
    {
        if (isPickedUp || itemToAdd == null || !hasLanded) return;
        if (!other.CompareTag("Player")) return;

        inventory = other.GetComponentInChildren<Inventory_Base>();
        if (inventory == null) return;

        bool canAdd = inventory.CanAddItem() || inventory.CanAddToStack(itemToAdd);
        if (canAdd)
            PickupItem(inventory);
    }

    private void PickupItem(Inventory_Base targetInventory)
    {
        isPickedUp = true;
        bool added = targetInventory.AddItem(itemToAdd);
        if (!added)
        {
            // 背包满且不可堆叠：保护——不拾取（物品留在地上，重置状态，程序不卡）
            isPickedUp = false;
            return;
        }
        if (itemData != null && !string.IsNullOrEmpty(itemData.itemId))
            QuestEvents.ReportItemCollected(itemData.itemId, 1);

        var cols = GetComponents<Collider2D>();
        foreach (var c in cols) c.enabled = false;
        if (rb != null) rb.simulated = false;

        if (PickupFX.Instance != null)
            PickupFX.Instance.AnimatePickup(transform);
        else
            Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }

    private void UpdateRarityBackground()
    {
        if (rarityBackground == null || lootedItem == null) return;
        Color rarityColor = lootedItem.GetRarityColor();
        rarityColor.a = rarityBackgroundAlpha;
        rarityBackground.enabled = true;
        rarityBackground.color = rarityColor;
    }
}
