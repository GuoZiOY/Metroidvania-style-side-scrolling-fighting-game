using System;
using UnityEngine;
using static Entity_Stats;

[Serializable]
public class AttackData
{
    public float phyiscalDamage;
    public float elementalDamage;
    public bool isCrit;
    public ElementType element;
    public ElementalEffectData effectData;

    public AttackData()
    {
    }

    public AttackData(Entity_Stats entityStats, DamageScaleData scaleData)
    {
        ElementType InputElement = entityStats.InputElement;

        phyiscalDamage = entityStats.GetPhyiscalDamage(scaleData.phyiscal);
        elementalDamage = entityStats.GetElementalDamage(InputElement, out element, scaleData.elemental);

        bool isCrit = entityStats.CalculateCritStatus();
        this.isCrit = isCrit;

        if (isCrit)
        {
            float critPower = entityStats.GetCritPower();
            phyiscalDamage *= critPower;
            elementalDamage *= critPower;
        }

        effectData = new ElementalEffectData(entityStats, scaleData);
    }
}