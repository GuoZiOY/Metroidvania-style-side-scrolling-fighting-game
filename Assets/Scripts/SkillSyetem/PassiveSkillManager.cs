using System;
using UnityEngine;

public class PassiveSkillManager : MonoBehaviour
{
    public static PassiveSkillManager Instance { get; private set; }

    private Player_SkillManager skillManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 确保跨场景持久化
    }

    private void Start()
    {
        skillManager = Player_SkillManager.Instance ?? FindObjectOfType<Player_SkillManager>();

        if (SkillDataManager.Instance != null)
        {
            SkillDataManager.Instance.OnPassiveSkillUpdated += OnPassiveSkillUpdated;
            Debug.Log($"[PassiveSkillManager] 已监听被动技能事件");
        }
        else
        {
            Debug.LogError($"[PassiveSkillManager] SkillDataManager.Instance 为 null");
        }
    }

    private void OnDestroy()
    {
        if (SkillDataManager.Instance != null)
        {
            SkillDataManager.Instance.OnPassiveSkillUpdated -= OnPassiveSkillUpdated;
        }
    }

    private void OnPassiveSkillUpdated(SkillUpgradeType upgradeType, int newLevel)
    {
        Debug.Log($"[PassiveSkillManager] 收到被动技能更新事件: {upgradeType}, 等级: {newLevel}");
        ActivatePassiveSkill(upgradeType, newLevel);
    }

    private void ActivatePassiveSkill(SkillUpgradeType upgradeType, int level)
    {
        if (upgradeType == SkillUpgradeType.None)
            return;

        SkillType skillType = SkillDataManager.Instance?.GetSkillType(upgradeType) ?? SkillType.None;
        if (skillType == SkillType.None)
            return;

        Skill_Base skill = skillManager?.GetSkillByType(skillType);
        if (skill == null)
            return;

        if (level > 0)
        {
            LevelData levelData = SkillDataManager.Instance?.GetLevelData(upgradeType, level);
            if (levelData != null)
            {
                skill.SetSkillLevelData(upgradeType, levelData, level, false);
                Debug.Log($"被动技能 {skillType} 已激活，等级：{level}");
            }
        }
        else
        {
            skill.RefundSkillUpgrade();
            Debug.Log($"被动技能 {skillType} 已移除");
        }
    }

    public void RefreshAllPassiveSkills()
    {
        if (SkillDataManager.Instance == null || skillManager == null)
            return;

        Skill_Base[] allSkills = skillManager.allSkills;
        if (allSkills == null)
            return;

        foreach (var skill in allSkills)
        {
            if (skill == null || skill.upgradeType == SkillUpgradeType.None)
                continue;

            Skill_DataSo skillData = SkillDataManager.Instance.GetSkillData(skill.upgradeType);
            if (skillData != null && skillData.usageType == SkillUsageType.Passive)
            {
                int currentLevel = SkillDataManager.Instance.GetCurrentLevel(skill.upgradeType);
                ActivatePassiveSkill(skill.upgradeType, currentLevel);
            }
        }
    }
}
