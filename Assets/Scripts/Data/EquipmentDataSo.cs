using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu( menuName = "RPG设置/物品数据/装备数据", fileName = "ItemData-Equipment-")]
public class EquipmentDataSo : ItemDataSo
{
    [Header("装备设置")]
    public ItemModifier[] modifiers;
}

[Serializable]
public class ItemModifier
{
    public StatType statType;
    public float value; //改为float类型以支持小数
}
