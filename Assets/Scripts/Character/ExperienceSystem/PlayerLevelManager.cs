using System;
using UnityEngine;

public class PlayerLevelManager : MonoBehaviour // 玩家等级管理器
{
    public static PlayerLevelManager Instance { get; private set; } // 单例实例

    [Header("等级数据")]
    [SerializeField] private int currentLevel = 0; // 当前等级
    [SerializeField] private int currentExp = 0; // 当前经验值
    [SerializeField] private int skillPoints = 0; // 可用技能点数
    [SerializeField] private int attributePoints = 0; // 可用属性点数

    [Header("组件引用")]
    [SerializeField] private LevelCalculator levelCalculator; // 等级计算器
    [SerializeField] private AttributePointManager attributePointManager; // 属性点管理器
    private UI_EventTip eventTip; // 事件提示（场景对象，预制体无法序列化，运行时查找）

    public int CurrentLevel => currentLevel; // 获取当前等级
    public int CurrentExp => currentExp; // 获取当前经验值
    public int SkillPoints => skillPoints; // 获取可用技能点数
    public int AttributePoints => attributePoints; // 获取可用属性点数
    public int ExpToNextLevel => levelCalculator.GetExpToNextLevel(currentLevel); // 获取升级所需经验
    public int TotalExpToNextLevel => levelCalculator.GetTotalExpRequired(currentLevel + 1); // 获取达到下一级所需总经验
    public float ExpProgress => (float)currentExp / ExpToNextLevel; // 获取经验进度（0-1）

    public event Action<int> OnExpGained; // 经验获取事件
    public event Action<int> OnLevelUp; // 升级事件
    public event Action<int> OnSkillPointsChanged; // 技能点变化事件
    public event Action<int> OnAttributePointsChanged; // 属性点变化事件

    private void Awake() // 初始化单例
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start() // 初始化等级计算器和属性点管理器
    {
        if (levelCalculator == null)
        {
            levelCalculator = GetComponent<LevelCalculator>();
            if (levelCalculator == null)
            {
                levelCalculator = gameObject.AddComponent<LevelCalculator>();
            }
        }

        if (attributePointManager == null)
        {
            attributePointManager = GetComponent<AttributePointManager>() ?? FindAnyObjectByType<AttributePointManager>();
        }
    }

    public void AddExp(int expAmount) // 添加经验值
    {
        if (expAmount <= 0 || currentLevel >= levelCalculator.MaxLevel)
            return;
        currentExp += expAmount;
        OnExpGained?.Invoke(expAmount);

        CheckLevelUp();
    }

    private void CheckLevelUp() // 检查是否升级
    {
        while (currentExp >= ExpToNextLevel && currentLevel < levelCalculator.MaxLevel)
        {
            currentExp -= ExpToNextLevel;
            currentLevel++;
            
            int freeAttributePoints = CalculateFreeAttributePoints(currentLevel); // 计算自由属性点数
            attributePoints += freeAttributePoints;
            
            int skillPointsGain = CalculateSkillPoints(currentLevel); // 计算技能点数
            skillPoints += skillPointsGain;
            
            // 0-5级自动增加主属性
            if (currentLevel <= 5)
            {
                attributePointManager?.AutoIncreaseMajorAttributes();
            }
            
            // 通知 SkillPointManager 增加技能点
            if (SkillPointManager.Instance != null)
            {
                SkillPointManager.Instance.AddSkillPoints(skillPointsGain, "等级提升");
            }
            
            OnLevelUp?.Invoke(currentLevel);
            OnSkillPointsChanged?.Invoke(skillPoints);
            OnAttributePointsChanged?.Invoke(attributePoints);
            
            GetEventTip()?.ShowLevelUp(currentLevel); // 显示等级提升提示
        }
    }

    // 事件提示延迟解析：场景对象引用在预制体中无效，运行时查找
    private UI_EventTip GetEventTip()
    {
        if (eventTip == null)
            eventTip = FindAnyObjectByType<UI_EventTip>();
        return eventTip;
    }

    private int CalculateFreeAttributePoints(int level) // 计算自由属性点数（平稳成长型）
    {
        if (level <= 10) return 1; // 0-10级：1点自由点
        else if (level <= 30) return 2; // 11-30级：2点自由点
        else return 3; // 31级+：3点自由点
    }

    private int CalculateSkillPoints(int level) // 计算技能点数（平稳成长型）
    {
        if (level <= 15) return 1; // 0-15级：1点技能点
        else if (level <= 50) return 2; // 16-50级：2点技能点
        else return 3; // 51级+：3点技能点
    }

    public bool UseSkillPoints(int amount) // 使用技能点
    {
        if (skillPoints < amount)
            return false;
        skillPoints -= amount;
        OnSkillPointsChanged?.Invoke(skillPoints);
        return true;
    }

    public void AddSkillPoints(int amount) // 添加技能点
    {
        if (amount <= 0)
            return;
        skillPoints += amount;
        OnSkillPointsChanged?.Invoke(skillPoints);
    }

    public bool UseAttributePoints(int amount) // 使用属性点
    {
        if (attributePoints < amount)
            return false;
        attributePoints -= amount;
        OnAttributePointsChanged?.Invoke(attributePoints);
        return true;
    }

    public void AddAttributePoints(int amount) // 添加属性点
    {
        if (amount <= 0)
            return;
        attributePoints += amount;
        OnAttributePointsChanged?.Invoke(attributePoints);
    }

    public void LoadFromSave(int level, int exp, int skillPts, int attrPts)
    {
        currentLevel = level;
        currentExp = exp;
        skillPoints = skillPts;
        attributePoints = attrPts;
        // 读档后检查是否有未处理的升级（防止极边缘情况下的经验溢出）
        CheckLevelUp();
    }

    public void SetLevel(int level) // 设置等级
    {
        if (level < 1 || level > levelCalculator.MaxLevel)
            return;
        currentLevel = level;
        currentExp = 0;
        OnLevelUp?.Invoke(currentLevel);
    }

    public void SetExp(int exp) // 设置经验值
    {
        if (exp < 0)
            return; 
        currentExp = Mathf.Min(exp, ExpToNextLevel - 1);
    }

    public void ResetLevelSystem() // 重置等级系统
    {
        currentLevel = 0;
        currentExp = 0;
        skillPoints = 0;
        attributePoints = 0;
    }

    public float GetExpPercentage() // 获取经验进度百分比
    {
        return ExpProgress;
    }

    public int GetRemainingExpToNextLevel() // 获取距离升级还需多少经验
    {
        return ExpToNextLevel - currentExp;
    }
}
