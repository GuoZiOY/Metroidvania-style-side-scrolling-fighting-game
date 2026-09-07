using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Entity_Stats;

public interface IDamgable//接口
{
    public bool TakeDamage(float damage,float elementalDamage ,ElementType element,  Transform damageDealer, bool isCrit = false, bool ignoreInvincibility = false);

}
