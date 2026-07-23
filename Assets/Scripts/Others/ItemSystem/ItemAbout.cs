using UnityEngine;

public class ItemAbout : MonoBehaviour
{
    [Header("稀有度背景设置")]
    [SerializeField] private SpriteRenderer rarityBackground;
    [SerializeField] private float rarityBackgroundAlpha = 0.5f;

    [Header("物品推开设置")]
    [SerializeField] private float pushForce = 2f;
    [SerializeField] private float pushDistance = 0.6f;

    private Inventory_Base inventory;
    private SpriteRenderer sr;
    private Animator anim;
    private Rigidbody2D rb;
    private ItemDataSo itemData;
    private LootedItem lootedItem;
    private Inventory_Item itemToAdd;
    private bool isPickedUp = false;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        InitializeDefaultItem();
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleItemCollision(other);
    }

    private void OnValidate()
    {
        if (itemData != null)
            gameObject.name = itemData.itemName;
    }

    private void HandlePickup(Collider2D other)
    {
        if (isPickedUp || itemToAdd == null) return;
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
        targetInventory.AddItem(itemToAdd);

        if (itemData != null && !string.IsNullOrEmpty(itemData.itemId))
            QuestEvents.ReportItemCollected(itemData.itemId, 1);

        // 飞向 UI 目标点
        var cols = GetComponents<Collider2D>();
        foreach (var c in cols) c.enabled = false;
        if (rb != null) rb.simulated = false;

        if (PickupFX.Instance != null)
            PickupFX.Instance.AnimatePickup(transform);
        else
            Destroy(gameObject);
    }

    private void HandleItemCollision(Collider2D other)
    {
        if (isPickedUp || rb == null) return;

        ItemAbout otherItem = other.GetComponent<ItemAbout>();
        if (otherItem == null || otherItem == this || otherItem.isPickedUp) return;

        float distance = Vector2.Distance(transform.position, other.transform.position);
        if (distance < pushDistance && distance > 0.01f)
        {
            Vector2 dir = (transform.position - other.transform.position).normalized;
            otherItem.rb.linearVelocity = dir * pushForce;
        }
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
