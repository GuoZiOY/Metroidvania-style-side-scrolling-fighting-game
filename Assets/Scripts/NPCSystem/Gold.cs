using UnityEngine;

public class Gold : MonoBehaviour
{
    [HideInInspector] public int worth;

    private bool pickedUp;
    private bool hasLanded;
    private Rigidbody2D rb;

    [Header("地面检测")]
    [SerializeField] private float groundCheckDistance = 0.6f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (pickedUp || !hasLanded) return;
        if (!other.CompareTag("Player")) return;

        pickedUp = true;

        AudioManager.Instance?.PlayGoldPickupSfx();

        var inv = other.GetComponent<PlayerInventorySystem>();
        if (inv != null)
            inv.AddCurrency(worth);

        var cols = GetComponents<Collider2D>();
        foreach (var c in cols) c.enabled = false;
        if (rb != null) rb.simulated = false;

        if (PickupFX.Instance != null)
            PickupFX.Instance.AnimatePickup(transform);
        else
            Destroy(gameObject);
    }
}
