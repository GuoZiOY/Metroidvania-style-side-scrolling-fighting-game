using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Entity_Stats;

public class Skill_ElementalBase : Skill_Base
{
    [Header("元素技能或附魔属性")]
    [SerializeField] protected List<ElementalEnchantData> enchantDatas; // 元素附魔数据
    private ElementType currentActiveEnchant = ElementType.None; // 当前激活的附魔元素
    private bool isInitialized = false;

    [Header("引用组件")]
    private Player_Combat playerAttack;
    private Entity_Stats playerStats;
    private int appliedStatLevel;

protected override void Awake()
    {
        base.Awake();
        playerAttack = GetComponentInParent<Player_Combat>();
        playerStats = GetComponentInParent<Entity_Stats>();
        InitEnchantData();
        if (playerStats != null)
        {
            playerStats.InputElement = currentActiveEnchant;
        }
        if (playerAttack != null)
        {
            playerAttack.OnAttackHitResult += OnAttackHitResult;
        }
    }

    // 初始化所有元素的附魔数据
    private void InitEnchantData()
    {
        foreach (var data in enchantDatas)
        {
            data.ResetCount();
        }
        currentActiveEnchant = ElementType.None;
        isInitialized = true;
    }

    // 激活指定元素的附魔（使用元素技能时调用）
    public void ActivateEnchant(ElementType elementType)
    {
        if (!isInitialized || elementType == ElementType.None) return;

        ElementalEnchantData targetData = GetEnchantDataByType(elementType);
        if (targetData == null)
        {
            Debug.LogWarning($"未配置{elementType}类型的附魔数据！");
            return;
        }

        targetData.ResetCount();
        currentActiveEnchant = elementType;
        playerStats.InputElement = currentActiveEnchant;

        player.VFX.CreatePopUpText($"{elementType} {targetData.currentCount}");
        Debug.Log($"激活{elementType}附魔，剩余次数：{targetData.currentCount}");
    }

    // 重写 TryUseSkill 实现通过元素技能激活逻辑
    public override void TryUseSkill()
    {
        if (CanUseSkill() == false)
            return;

        // 根据 skillType 自动获取对应的元素类型
        ElementType? targetElementType = GetElementTypeFromSkillType(skillType);
        
        if (targetElementType.HasValue)
        {
            ActivateEnchant(targetElementType.Value);
            StartSkillCooldown();
        }
        else
        {
            Debug.LogWarning($"{GetType().Name} 的 skillType ({skillType}) 没有对应的 ElementType！");
        }
    }

    // 重写 SetSkillLevelData 实现附魔次数升级映射 + 元素伤害加成
    public override void SetSkillLevelData(SkillUpgradeType upgradeType, LevelData levelData, int level, bool updateUpgradeType = true)
    {
        base.SetSkillLevelData(upgradeType, levelData, level, updateUpgradeType);

        ElementType? targetElementType = GetElementTypeFromSkillType(skillType);
        if (targetElementType == null) return;

        // 每级赋予 1 点对应元素伤害
        ApplyElementalDamageBonus(targetElementType.Value);

        ElementalEnchantData targetData = GetEnchantDataByType(targetElementType.Value);
        if (targetData != null)
        {
            targetData.UpdateMaxCount(totalLevel);
            Debug.Log($"{GetType().Name} 升级到 {level} 级，totalLevel: {totalLevel}，{targetElementType.Value}附魔次数: {targetData.GetCurrentMaxCount()}");
        }
    }

    public override void RefundSkillUpgrade()
    {
        RemoveElementalDamageBonus();
        appliedStatLevel = 0;
        base.RefundSkillUpgrade();
    }

    private void ApplyElementalDamageBonus(ElementType elementType)
    {
        int diff = currentLevel - appliedStatLevel;
        if (diff == 0) return;

        Stat stat = GetElementalDamageStat(elementType);
        if (stat != null)
        {
            stat.AddBaseValue(diff);
            appliedStatLevel = currentLevel;
        }
    }

    private void RemoveElementalDamageBonus()
    {
        ElementType? elementType = GetElementTypeFromSkillType(skillType);
        if (elementType == null) return;
        Stat stat = GetElementalDamageStat(elementType.Value);
        if (stat != null)
            stat.AddBaseValue(-appliedStatLevel);
    }

    private Stat GetElementalDamageStat(ElementType elementType)
    {
        if (playerStats == null) return null;
        return elementType switch
        {
            ElementType.Fire => playerStats.offense.fireDamage,
            ElementType.Ice => playerStats.offense.iceDamage,
            ElementType.Lightning => playerStats.offense.lightningDamage,
            _ => null,
        };
    }

    // ���� SkillType ��ȡ��Ӧ�� ElementType
    private ElementType? GetElementTypeFromSkillType(SkillType skillType)
    {
        switch (skillType)
        {
            case SkillType.Fire:
                return ElementType.Fire;
            case SkillType.Ice:
                return ElementType.Ice;
            case SkillType.Lighting:
                return ElementType.Lightning;
            default:
                return null;
        }
    }

    #region 附魔计数逻辑
    // 根据元素类型获取数据
    protected ElementalEnchantData GetEnchantDataByType(ElementType elementType)
    {
        return enchantDatas.Find(data => data.elementType == elementType);
    }

    // 处理攻击命中结果
    private void OnAttackHitResult(ElementType attackElement, bool targetGotHit)
    {
        if (currentActiveEnchant == ElementType.None || attackElement != currentActiveEnchant)
            return;

        
        if (!targetGotHit)
        {
            Debug.Log($"空挥：{currentActiveEnchant}附魔次数未扣除");
            return;
        }

        ElementalEnchantData activeData = GetEnchantDataByType(currentActiveEnchant);
        if (activeData == null || activeData.currentCount <= 0)
        {
            currentActiveEnchant = ElementType.None;
            return;
        }

        bool isDeductSuccess = activeData.DeductCount();
        if (isDeductSuccess)
        {
            Debug.Log($"命中了：{currentActiveEnchant}附魔剩余次数：{activeData.currentCount}");
            player.VFX.CreatePopUpText($"{currentActiveEnchant} {activeData.currentCount}");
        }

        
        if (activeData.currentCount <= 0)
        {
            currentActiveEnchant = ElementType.None;
            playerStats.InputElement = currentActiveEnchant;

            Debug.Log($"{attackElement}附魔次数耗尽");
        }
    }
    #endregion
}


[System.Serializable]
public class ElementalEnchantData
{
    [Header("元素附魔数据")]
    public ElementType elementType; // 直接对应ElementType
    public int baseMaxCount = 3;    // 初始附魔次数（默认3次）
    private int upgradeAddCount = 1; // 升级增加次数

    [HideInInspector] public int currentCount; // 当前剩余次数（用于消耗限制）
    private int currentMaxCount; // 当前最大附魔次数（动态计算）

    // 初始化/重置附魔次数（使用技能时调用）
    public void ResetCount()
    {
        currentCount = currentMaxCount;
    }

    // 根据技能等级更新附魔次数（每3级增加1次）
    public void UpdateMaxCount(int totalLevel)
    {
        int bonusCount = totalLevel / 3; // 每3级增加1次
        currentMaxCount = baseMaxCount + bonusCount;
        currentCount = currentMaxCount; // 更新后重置
    }

    // 直接增加附魔次数上限（用于升级奖励）
    public void UpgradeCount()
    {
        baseMaxCount += upgradeAddCount;
        currentCount = currentMaxCount; // 重置次数
    }

    // 扣除一次命中次数（攻击命中时调用，返回是否扣除成功）
    public bool DeductCount()
    {
        if (currentCount <= 0) return false;
        currentCount--;
        return true;
    }

    // 获取当前最大附魔次数
    public int GetCurrentMaxCount()
    {
        return currentMaxCount;
    }
}

