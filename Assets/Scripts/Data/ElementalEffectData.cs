using UnityEngine;
using System;

[Serializable]
public class ElementalEffectData
{
    public float chillDuration;
    public float chillSlowMultiplier;

    public float burnDuratoin;
    public float totalBurnDamage;

    public float shockDuration;
    public float shockDamage;
    public float shockCharge;

    public ElementalEffectData(Entity_Stats entityStats, DamageScaleData damageScale)
    {
        chillDuration = damageScale.chillDuration * ((1 + damageScale.elemental) / 2);
        chillSlowMultiplier = damageScale.chillSlowMulitplier * ((1+ damageScale.elemental) / 2);

        burnDuratoin = damageScale.burnDuratin;
        totalBurnDamage = entityStats.offense.fireDamage.GetValue() * damageScale.burnDamageScale * damageScale.elemental;

        shockDuration = damageScale.shockDuration;
        shockDamage = entityStats.offense.lightningDamage.GetValue() * damageScale.shockDamageScale * damageScale.elemental;
        shockCharge = damageScale.shockCharge;
    }
}
