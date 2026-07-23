using System;
using System.Collections.Generic;
using UnityEngine;

public class AttributePointManager : MonoBehaviour // 属性点管理器
{
    public static AttributePointManager Instance { get; private set; } // 单例实例

    private static readonly StatType[] AllocatableAttributes = { // 可分配的属性类型
        StatType.Strength, StatType.Agility, StatType.Intelligence, StatType.Vitality
    };

    [Header("组件引用")]
    [SerializeField] private Entity_Stats playerStats; // 玩家属性组件

    private Dictionary<StatType, int> attributePointsMap; // 属性点分配字典

    public int TotalAttributePoints => GetTotalAllocatedPoints(); // 总分配的属性点数
    public int StrengthPoints => GetAttributePoints(StatType.Strength); // 力量点数
    public int AgilityPoints => GetAttributePoints(StatType.Agility); // 敏捷点数
    public int IntelligencePoints => GetAttributePoints(StatType.Intelligence); // 智力点数
    public int VitalityPoints => GetAttributePoints(StatType.Vitality); // 活力点数

    public event Action<StatType, int> OnAttributePointAllocated; // 属性点分配事件
    public event Action OnAttributesChanged; // 属性变化事件

    private void Awake() // 初始化单例
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAttributeMap();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        playerStats ??= GetComponent<Entity_Stats>() ?? FindAnyObjectByType<Entity_Stats>(); // 获取玩家属性组件
    }

    private void InitializeAttributeMap() // 初始化属性点字典
    {
        attributePointsMap = new Dictionary<StatType, int>();
        foreach (var attr in AllocatableAttributes) attributePointsMap[attr] = 0;
    }

    public bool AllocateAttributePoint(StatType attributeType) // 分配属性点
    {
        if (!ValidateAllocation() || !IsValidAttribute(attributeType)) return false;
        ModifyAttributePoint(attributeType, 1);
        PlayerLevelManager.Instance.UseAttributePoints(1);
        OnAttributePointAllocated?.Invoke(attributeType, GetAttributePoints(attributeType));
        OnAttributesChanged?.Invoke();
        return true;
    }

    private bool ValidateAllocation() // 验证分配条件
    {
        if (PlayerLevelManager.Instance == null || PlayerLevelManager.Instance.AttributePoints < 1 || playerStats == null)
            return false;
        return true;
    }

    private bool IsValidAttribute(StatType attributeType) => attributePointsMap.ContainsKey(attributeType); // 验证属性类型

    private void ModifyAttributePoint(StatType attributeType, int amount) // 修改属性点
    {
        if (!IsValidAttribute(attributeType)) return;
        int currentPoints = GetAttributePoints(attributeType);
        int newPoints = currentPoints + amount;
        if (newPoints < 0) return;

        Stat targetStat = playerStats.GetStatByType(attributeType);
        targetStat?.AddBaseValue(amount);
        attributePointsMap[attributeType] = newPoints;
    }

    public int GetAttributePoints(StatType attributeType) => attributePointsMap.TryGetValue(attributeType, out int points) ? points : 0; // 获取属性点数

    private int GetTotalAllocatedPoints() // 获取总分配的属性点数
    {
        int total = 0;
        foreach (var points in attributePointsMap.Values) total += points;
        return total;
    }

    public void ResetAttributePoints() // 重置属性点
    {
        int totalPoints = TotalAttributePoints;
        foreach (var attr in AllocatableAttributes)
        {
            int points = GetAttributePoints(attr);
            if (points > 0 && playerStats != null)
            {
                Stat stat = playerStats.GetStatByType(attr);
                stat?.AddBaseValue(-points);
            }
            attributePointsMap[attr] = 0;
        }
        PlayerLevelManager.Instance?.AddAttributePoints(totalPoints);
        OnAttributesChanged?.Invoke();
    }

    public void SetAttributePoints(StatType attributeType, int points) // 设置属性点数
    {
        if (points < 0 || !IsValidAttribute(attributeType)) return;
        int currentPoints = GetAttributePoints(attributeType);
        int difference = points - currentPoints;
        if (difference == 0) return;

        if (difference > 0)
        {
            if (PlayerLevelManager.Instance.AttributePoints >= difference)// 检查是否有足够的属性点
            {
                ModifyAttributePoint(attributeType, difference);// 分配属性点
                PlayerLevelManager.Instance.UseAttributePoints(difference);// 使用属性点
                OnAttributePointAllocated?.Invoke(attributeType, points);// 触发属性点分配事件
                OnAttributesChanged?.Invoke();// 触发属性变化事件
            }
        }
        else
        {
            ModifyAttributePoint(attributeType, difference);// 分配属性点
            PlayerLevelManager.Instance?.AddAttributePoints(-difference);// 增加属性点
            OnAttributePointAllocated?.Invoke(attributeType, points);// 触发属性点分配事件
            OnAttributesChanged?.Invoke();// 触发属性变化事件   
        }
    }

    public void AutoIncreaseMajorAttributes() // 自动增加主属性
    {
        if (playerStats == null) return;
        playerStats.major.strength.AddBaseValue(1);
        playerStats.major.agility.AddBaseValue(1);
        playerStats.major.intelligence.AddBaseValue(1);
        playerStats.major.vitality.AddBaseValue(1);
        OnAttributesChanged?.Invoke();
    }
}
