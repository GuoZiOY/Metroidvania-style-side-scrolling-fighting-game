using UnityEngine;

public abstract class ExperienceTrigger : MonoBehaviour
{
    [Header("经验设置")]
    [SerializeField] protected int baseExperience = 10; // 基础经验值
    [SerializeField] protected ExperienceSourceType sourceType; // 经验来源类型
    [SerializeField] protected bool triggerOnce = false; // 是否只触发一次
    [SerializeField] protected bool useGlobalMultiplier = true; // 是否使用全局倍率

    [Header("特殊条件")]
    [SerializeField] protected bool hasSpecialCondition = false; // 是否有特殊条件
    [SerializeField] protected string conditionDescription = ""; // 条件描述

    protected bool hasTriggered = false; // 是否已触发

    protected virtual void Start() // 初始化
    {
        InitializeTrigger();
    }

    protected virtual void InitializeTrigger() // 初始化触发器（子类可重写）
    {
        // 子类可以重写此方法进行初始化
    }

    protected virtual void TriggerExperience(object sourceData = null, Vector3? position = null) // 触发经验获取
    {
        if (triggerOnce && hasTriggered)
            return;

        if (!CheckSpecialCondition())
            return;

        hasTriggered = true;

        ExperienceEvent expEvent = new ExperienceEvent(
            baseExperience,
            sourceType,
            sourceData,
            position ?? transform.position
        );

        ExperienceManager.Instance?.AddExperience(expEvent);

        OnExperienceTriggered(expEvent);
    }

    protected virtual bool CheckSpecialCondition() // 检查特殊条件（子类可重写）
    {
        // 子类可以重写此方法实现特殊条件检查
        return true;
    }

    protected virtual void OnExperienceTriggered(ExperienceEvent expEvent) // 经验触发后的回调（子类可重写）
    {
        // 子类可以重写此方法在经验触发后执行额外逻辑
    }

    public void SetBaseExperience(int exp) // 设置基础经验值
    {
        baseExperience = exp;
    }

    public void SetSourceType(ExperienceSourceType type) // 设置来源类型
    {
        sourceType = type;
    }

    public void ResetTrigger() // 重置触发器
    {
        hasTriggered = false;
    }

    public bool HasTriggered() // 检查是否已触发
    {
        return hasTriggered;
    }

    public int GetBaseExperience() // 获取基础经验值
    {
        return baseExperience;
    }

    public ExperienceSourceType GetSourceType() // 获取来源类型
    {
        return sourceType;
    }

    protected virtual void OnDestroy() // 清理（子类可重写）
    {
        // 子类可以重写此方法进行清理
    }
}
