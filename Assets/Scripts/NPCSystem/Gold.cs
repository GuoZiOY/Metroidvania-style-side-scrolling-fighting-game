using UnityEngine;

/// <summary>
/// 货币掉落物。物理由 LootManager.ApplyDropPhysics 处理，
/// 拾取后飞向 UI 目标点。
/// </summary>
public class Gold : MonoBehaviour
{
    [HideInInspector] public int worth;

    private bool pickedUp;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (pickedUp) return;
        if (!other.CompareTag("Player")) return;

        pickedUp = true;

        var inv = other.GetComponent<PlayerInventorySystem>();
        if (inv != null)
            inv.AddCurrency(worth);

        var cols = GetComponents<Collider2D>();
        foreach (var c in cols) c.enabled = false;
        if (GetComponent<Rigidbody2D>() != null)
            GetComponent<Rigidbody2D>().simulated = false;

        if (PickupFX.Instance != null)
            PickupFX.Instance.AnimatePickup(transform);
        else
            Destroy(gameObject);
    }
}
