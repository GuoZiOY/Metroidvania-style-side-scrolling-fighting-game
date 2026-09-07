using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_VFX : Entity_VFX
{
    [Header("可被反击窗口")]
    [SerializeField] private GameObject attackAlert;//预攻击警报


    public void EnableAttackAlert(bool enable)
    {

        if (attackAlert == null)
            return;

        attackAlert.SetActive(enable);
    }


}
