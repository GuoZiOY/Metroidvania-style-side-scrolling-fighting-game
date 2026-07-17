using System;
using UnityEngine;

[Serializable]
public class DamageScaleData
{
    [Header("ÉËº¦±¶ÂÊ")]
    public float phyiscal = 1;
    public float elemental = 1;

    [Header("±ù¶³")]
    public float chillDuration = 3;
    public float chillSlowMulitplier = 0.2f;

    [Header("×ÆÉÕ")]
    public float burnDuratin = 3;
    public float burnDamageScale = 1;

    [Header("À×»÷")]
    public float shockDuration = 3;
    public float shockDamageScale = 1;
    public float shockCharge = 0.4f;
}
