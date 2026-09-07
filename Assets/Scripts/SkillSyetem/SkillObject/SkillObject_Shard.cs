using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillObject_Shard : SkillObject_Base
{
    public event Action OnExplode;
    private Skill_Shard shardManager;

    [SerializeField] private GameObject VFXPrefab;//?????§¹

    private Transform target;
    private float speed;

    private void Update()
    {
        if (target == null)
            return;

        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }

    public void MoveTowardsClosestTarget(float speed,Transform newTarget = null)
    {
        target = newTarget == null ? FindClosestTarget() : newTarget;
        this.speed = speed;
    }


    public void SetupShard(Skill_Shard shardManager)
    {
        this.shardManager = shardManager;

        playerStats = shardManager.player.stats;
        damageScaleData = shardManager.damageScaleData;

        float detonationTime = shardManager.GetDetonateTime();
        Invoke(nameof(Explode), detonationTime);
    }

    public void SetupShard(Skill_Shard shardManager, float detonationTime, bool canMove, float shardSpeed, Transform target = null)
    {
        this.shardManager = shardManager;

        playerStats = shardManager.player.stats;
        damageScaleData = shardManager.damageScaleData;

        Invoke(nameof(Explode), detonationTime);

        if(canMove) MoveTowardsClosestTarget(shardSpeed,target);
    }

    public void Explode()
    {
        DamageEnemiesInRadius(transform,checkRadius);//??????
        GameObject vfx = Instantiate(VFXPrefab, transform.position, Quaternion.identity);//?????§¹???????
        vfx.GetComponentInChildren<SpriteRenderer>().color = shardManager.player.VFX.UpdateOnHitColor(usedElement);//??????????????

        OnExplode?.Invoke();//??§Ö???
        Destroy(gameObject);

    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.GetComponent<Enemy>() == null)//??????????????????Enemy????????????????
            return;//???????????

        Explode();
    }
}
