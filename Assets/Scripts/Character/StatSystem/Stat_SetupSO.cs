using UnityEngine;

[CreateAssetMenu(menuName = "RPG设置/属性配置", fileName = "属性配置数据")]
public class Stat_SetupSO : ScriptableObject
{
    public string setupId; // CSV 导入用的ID

    [Header("资源")]
    public float maxHP = 100;
    public float healthRegen;

    [Header("Offense - 物理伤害")]
    public float attackSpeed = 1;
    public float damage = 10;
    public float critChance;
    public float critPower = 150;
    public float armorReduction;

    [Header("Offense - 元素伤害")]
    public float elementalHeart;
    public float fireDamage;
    public float iceDamage;
    public float lightningDamage;

    [Header("Defense - 防御属性")]
    public float armor;
    public float evasion;

    [Header("Defense - 元素抗性")]
    public float fireResistance;
    public float iceResistance;
    public float lightningResistance;

    [Header("Major - 主要属性")]
    public float strength;
    public float agility;
    public float intelligence;
    public float vitality;
}
