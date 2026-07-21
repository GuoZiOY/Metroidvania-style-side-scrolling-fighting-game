using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Entity_Stats;

public class Object_Chest : MonoBehaviour, IDamgable
{
    private Rigidbody2D rb => GetComponentInChildren<Rigidbody2D>();
    private Animator anim => GetComponentInChildren<Animator>();
    private Entity_VFX fx => GetComponent<Entity_VFX>();

    [SerializeField] private LootDropper lootDropper;

    [Header("Open Details")]
    [SerializeField] private Vector2 knockback;

    [SerializeField] public string uniqueID; // 存档唯一标识
    private bool hasDropped = false; //是否已经掉落过物品
    public bool HasDropped => hasDropped;
    public void MarkAsOpened() => hasDropped = true;

    public bool TakeDamage(float damage, float elementalDamage, ElementType element, Transform damageDealer, bool isCrit = false)
    {
        fx.PlayOnDamageVfx();
        anim.SetBool("chestOpen", true);
        rb.linearVelocity = knockback;
        rb.angularVelocity = Random.Range(-200f, 200f);
        
        //箱子被破坏后掉落物品（只掉落一次）
        if (!hasDropped && lootDropper != null)
        {
            AudioManager.Instance?.PlayChestSfx();
            lootDropper.OnDrop();
            hasDropped = true;
        }

        
        return true;
    }
    

}
