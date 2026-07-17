using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillDataManager : MonoBehaviour
{
    public static SkillDataManager Instance { get; private set; }

    [Header("技能数据缓存")]
    private Dictionary<SkillUpgradeType, SkillDataCache> skillDataCache; // 技能数据缓存

    public event Action<SkillUpgradeType, int> OnSkillDataUpdated; // 技能数据更新事件
    public event Action<SkillUpgradeType, int> OnPassiveSkillUpdated; // 被动技能更新事件（用于立即激活被动技能）

    private void Awake() // 初始化单例
    {
        if (Instance != null && Instance != this) // 检查是否已存在实例
        {
            Destroy(gameObject); // 销毁重复实例
            return;
        }
        Instance = this; // 设置当前实例为单例

        skillDataCache = new Dictionary<SkillUpgradeType, SkillDataCache>(); // 初始化数据缓存
    }

    #region 技能树系统调用接口

    public void UpdateSkillData(SkillUpgradeType upgradeType, Skill_DataSo skillData, int currentLevel) // 更新技能数据（技能树系统调用）
    {
        if (upgradeType == SkillUpgradeType.None || skillData == null) // 检查参数是否有效
            return;

        if (!skillDataCache.ContainsKey(upgradeType)) // 如果缓存中不存在该技能
        {
            skillDataCache[upgradeType] = new SkillDataCache(); // 创建新的数据缓存
        }

        skillDataCache[upgradeType].skillData = skillData; // 更新技能数据
        skillDataCache[upgradeType].currentLevel = currentLevel; // 更新当前等级
        skillDataCache[upgradeType].skillType = skillData.skillType; // 更新技能类型

        // 触发数据更新事件
        OnSkillDataUpdated?.Invoke(upgradeType, currentLevel);

        // 如果是被动技能，触发被动技能更新事件，立即激活技能效果
        if (skillData.usageType == SkillUsageType.Passive)
        {
            Debug.Log($"[SkillDataManager] 触发被动技能事件: {upgradeType}, 等级: {currentLevel}");
            OnPassiveSkillUpdated?.Invoke(upgradeType, currentLevel);
        }
        else
        {
            Debug.Log($"[SkillDataManager] 触发主动技能事件: {upgradeType}, 等级: {currentLevel}");
        }
    }

    public void RemoveSkillData(SkillUpgradeType upgradeType) // 移除技能数据（技能树系统调用）
    {
        if (upgradeType == SkillUpgradeType.None) // 检查参数是否有效
            return;

        if (skillDataCache.ContainsKey(upgradeType)) // 如果缓存中存在该技能
        {
            skillDataCache[upgradeType].currentLevel = 0; // 重置等级为0

            // 如果是被动技能，触发被动技能更新事件，移除技能效果
            Skill_DataSo skillData = skillDataCache[upgradeType].skillData;
            if (skillData != null && skillData.usageType == SkillUsageType.Passive)
            {
                OnPassiveSkillUpdated?.Invoke(upgradeType, 0);
            }
        }
    }

    #endregion

    #region 技能槽系统调用接口

    public SkillType GetSkillType(SkillUpgradeType upgradeType) // 获取技能类型（技能槽系统调用）
    {
        if (upgradeType == SkillUpgradeType.None) // 检查参数是否有效
            return SkillType.None;

        if (skillDataCache.ContainsKey(upgradeType) && skillDataCache[upgradeType] != null) // 如果缓存中存在该技能
        {
            return skillDataCache[upgradeType].skillType; // 返回技能类型
        }

        return SkillType.None; // 未找到返回None
    }

    public Skill_DataSo GetSkillData(SkillUpgradeType upgradeType) // 获取技能数据（技能槽系统调用）
    {
        if (upgradeType == SkillUpgradeType.None) // 检查参数是否有效
            return null;

        if (skillDataCache.ContainsKey(upgradeType) && skillDataCache[upgradeType] != null) // 如果缓存中存在该技能
        {
            return skillDataCache[upgradeType].skillData; // 返回技能数据
        }

        return null; // 未找到返回null
    }

    public LevelData GetLevelData(SkillUpgradeType upgradeType, int level) // 获取等级数据（技能槽系统调用）
    {
        if (upgradeType == SkillUpgradeType.None) // 检查参数是否有效
            return null;

        if (skillDataCache.ContainsKey(upgradeType) && skillDataCache[upgradeType] != null) // 如果缓存中存在该技能
        {
            Skill_DataSo skillData = skillDataCache[upgradeType].skillData; // 获取技能数据
            if (skillData != null && skillData.levelDatas != null && level > 0 && level <= skillData.levelDatas.Length) // 检查等级是否有效
            {
                return skillData.levelDatas[level - 1]; // 返回等级数据
            }
        }

        return null; // 未找到返回null
    }

    public int GetCurrentLevel(SkillUpgradeType upgradeType) // 获取当前等级（技能槽系统调用）
    {
        if (upgradeType == SkillUpgradeType.None) // 检查参数是否有效
            return 0;

        if (skillDataCache.ContainsKey(upgradeType) && skillDataCache[upgradeType] != null) // 如果缓存中存在该技能
        {
            return skillDataCache[upgradeType].currentLevel; // 返回当前等级
        }

        return 0; // 未找到返回0
    }

    public bool IsSkillUnlocked(SkillUpgradeType upgradeType) // 检查技能是否已解锁（技能槽系统调用）
    {
        if (upgradeType == SkillUpgradeType.None) // 检查参数是否有效
            return false;

        if (skillDataCache.ContainsKey(upgradeType) && skillDataCache[upgradeType] != null) // 如果缓存中存在该技能
        {
            return skillDataCache[upgradeType].currentLevel > 0; // 检查等级是否大于0
        }

        return false; // 未找到返回false
    }

    #endregion

    public Dictionary<SkillUpgradeType, int> GetAllSkillLevels()
    {
        var result = new Dictionary<SkillUpgradeType, int>();
        foreach (var kvp in skillDataCache)
            result[kvp.Key] = kvp.Value.currentLevel;
        return result;
    }

    #region 数据缓存类

    private class SkillDataCache // 技能数据缓存类
    {
        public Skill_DataSo skillData; // 技能数据
        public int currentLevel; // 当前等级
        public SkillType skillType; // 技能类型
    }

    #endregion
}