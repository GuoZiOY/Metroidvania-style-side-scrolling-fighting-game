using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_AimatorTriggers : Entity_AimatorTriggers
{
    private Enemy enemy;
    private Enemy_VFX enemyVFX;

    

    protected override void Awake()
    {
        base.Awake();
        enemy = GetComponentInParent<Enemy>();
        enemyVFX = GetComponentInParent<Enemy_VFX>();
    }

    private void EnableCounterTime()
    {
        enemyVFX.EnableAttackAlert(true);
        enemy.EnableCounterTime(true);
    }

    private void DisbaleCounterTime()
    {
        enemyVFX.EnableAttackAlert(false);
        enemy.EnableCounterTime(false);
    }
}

