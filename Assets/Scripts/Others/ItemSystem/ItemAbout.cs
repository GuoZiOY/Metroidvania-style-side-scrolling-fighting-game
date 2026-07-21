using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemAbout : MonoBehaviour
{
    #region 序列化字段

    [SerializeField] private ItemDataSo itemData;
    [SerializeField] private LootedItem lootedItem;
    [SerializeField] private Inventory_Item itemToAdd;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D itemCollider;

    [Header("稀有度背景设置")]
    [SerializeField] private SpriteRenderer rarityBackground;
    [SerializeField] private float rarityBackgroundAlpha = 0.5f;

    [Header("物品推开设置")]
    [SerializeField] private float pushForce = 2f;
    [SerializeField] private float pushDistance = 0.6f;

    #endregion

    #region 私有字段

    private Inventory_Base inventory;
    private SpriteRenderer sr;
    private Animator anim;
    private bool isPickedUp = false;
    private bool hasLanded = false;

    private const float GROUND_CHECK_DISTANCE = 0.6f;
    private const float DESTROY_DELAY = 1f;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        InitializeComponents();
        InitializeDefaultItem();
    }

    private void Update()
    {
        CheckLanding();
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
        ValidateItemData();
    }

    #endregion

    #region 初始化方法

    private void InitializeComponents()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        itemCollider = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void InitializeDefaultItem()
    {
        if (itemData != null)
        {
            itemToAdd = new Inventory_Item(itemData);
        }
    }

    public void InitializeItem(ItemDataSo data)
    {
        itemData = data;
        lootedItem = null;
        itemToAdd = new Inventory_Item(itemData);
        
        if (itemCollider != null)
        {
            itemCollider.isTrigger = false;
        }
        
        if (itemData != null)
        {
            gameObject.name = itemData.itemName;
            if (sr != null)
            {
                sr.sprite = itemData.itemIcon;
            }
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
        
        if (itemCollider != null)
        {
            itemCollider.isTrigger = false;
        }
        hasLanded = false;
    }

    #endregion

    #region 物品交互

    private void HandlePickup(Collider2D other)
    {
        if (isPickedUp || itemToAdd == null)
            return;
        
        if (!hasLanded)
            return;
        
        if (!other.CompareTag("Player"))
            return;
        
        inventory = other.GetComponentInChildren<Inventory_Base>();

        if (inventory == null)
            return;

        bool canAddStack = inventory.CanAddItem() || inventory.CanAddToStack(itemToAdd);
        if (canAddStack)
        {
            PickupItem(inventory);
        }
    }

    private void PickupItem(Inventory_Base targetInventory)
    {
        isPickedUp = true;
        targetInventory.AddItem(itemToAdd);

        if (itemData != null && !string.IsNullOrEmpty(itemData.itemId))
            QuestEvents.ReportItemCollected(itemData.itemId, 1);

        if (anim != null)
        {
            anim.Play("Items_PickUp");
        }

        DisableCollider();
        Destroy(gameObject, DESTROY_DELAY);
    }

    private void DisableCollider()
    {
        if (itemCollider != null)
        {
            itemCollider.enabled = false;
        }
    }

    private void HandleItemCollision(Collider2D other)
    {
        if (isPickedUp || !hasLanded || rb == null || itemCollider == null)
            return;
        
        ItemAbout otherItem = other.GetComponent<ItemAbout>();
        if (otherItem == null || otherItem == this || otherItem.isPickedUp)
            return;
        
        float distance = Vector2.Distance(transform.position, other.transform.position);
        
        if (distance < pushDistance && distance > 0.01f)
        {
            PushItemAway(otherItem, other.transform);
        }
    }

    private void PushItemAway(ItemAbout otherItem, Transform otherTransform)
    {
        Vector2 pushDirection = (transform.position - otherTransform.position).normalized;
        otherItem.rb.linearVelocity = pushDirection * pushForce;
    }

    #endregion

    #region 物理和落地

    private void CheckLanding()
    {
        if (hasLanded || rb == null || itemCollider == null)
            return;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, GROUND_CHECK_DISTANCE, LayerMask.GetMask("Ground"));
        
        if (hit.collider != null)
        {
            OnLanded();
        }
    }

    private void OnLanded()
    {
        hasLanded = true;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    #endregion

    #region 稀有度背景

    private void UpdateRarityBackground()
    {
        if (rarityBackground == null || lootedItem == null)
            return;

        Color rarityColor = lootedItem.GetRarityColor();
        rarityColor.a = rarityBackgroundAlpha;

        rarityBackground.enabled = true;
        rarityBackground.color = rarityColor;
    }

    #endregion

    #region 编辑器验证

    private void ValidateItemData()
    {
        if (itemData == null)
            return;

        gameObject.name = itemData.itemName;
    }

    #endregion
}
