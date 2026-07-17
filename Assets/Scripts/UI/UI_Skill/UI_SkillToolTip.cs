using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class UI_SkillToolTip : UI_ToolTip
{
    [Header("技能tooltip的文本组件")]
    [SerializeField] private TextMeshProUGUI skillName; // 技能名称的文本
    [SerializeField] private TextMeshProUGUI skillDescription;// 技能描述的文本组件（显示原始描述）
    [SerializeField] private TextMeshProUGUI currentEffect;// 技能当前效果的文本组件（显示当前等级效果）
    [SerializeField] private TextMeshProUGUI skillLevelInfo;// 技能等级的信息文本
    [SerializeField] private TextMeshProUGUI skillRequirements;// 技能需求的文本
    [SerializeField] private TextMeshProUGUI nextLevelBonus;// 下一级加成的文本

    [Header("文本配置")]
    [SerializeField] private string unCostSkillText = "技能点不足！"; // 技能点不足时显示的文本
    [SerializeField] private string lockedSkillText = "已选择了不同分支，该技能已被锁定"; // 锁定技能时显示的文本
    [SerializeField] private string metConditionHex = "#FFFFFF";// 满足条件的文本颜色
    [SerializeField] private string notMetConditionHex = "#FF0000";// 未满足条件的文本颜色
    [SerializeField] private string importantInfoHex = "#FFFF00";// 重要信息的文本颜色

    [SerializeField] private UI_SkillTree skillTree;// 技能树引用
    private Coroutine textEffectCo; // 文本闪烁协程
    private Skill_DataSo currentSkillData;// 当前技能数据
    private UI_TreeNode currentNode;// 当前技能节点

    protected override void Awake()
    {
        base.Awake();
        skillTree = GetComponentInParent<UI_SkillTree>();
    }

    // 刷新 tooltip 显示
    public void RefreshToolTip()
    {
        if (currentNode == null || currentSkillData == null) return;
        UpdateAllToolTipElements(); // 更新所有显示元素
    }

  
    // 显示技能 tooltip
    public void ShowToolTip(bool show, RectTransform targetRect, UI_TreeNode node)
    {
        base.ShowToolTip(show, targetRect);
        if (!show)
        {
            ClearCurrentData();
            return;
        }

        currentNode = node;
        currentSkillData = node.skillData;
        UpdateAllToolTipElements();
    }

    // 更新技能 tooltip 元素的显示
    private void UpdateAllToolTipElements()
    {
        UpdateSkillName();
        UpdateSkillDescription();
        UpdateCurrentEffect();
        UpdateLevelInfo();
        UpdateRequirements();
        UpdateNextLevelBonus();
    }

    // 更新技能名称
    private void UpdateSkillName()
    {
        skillName.text = currentSkillData.displayName;
    }

    // 更新技能描述（只显示原始描述，不显示当前效果）
    private void UpdateSkillDescription()
    {
        // 无论技能是否解锁，只显示原始描述
        skillDescription.text = currentSkillData.description;
    }


    // 更新技能下的当前效果文本（显示当前等级的效果）
    private void UpdateCurrentEffect()
    {
        // 如果技能未解锁或等级为0，清空当前效果文本
        if (!currentNode.isUnlocked || currentNode.CurrentLevel <= 0)
        {
            currentEffect.text = "";
            return;
        }

        // 检查当前等级数据是否存在
        if (currentNode.CurrentLevel - 1 < currentSkillData.levelDatas.Length)
        {
            var currentLevelData = currentSkillData.levelDatas[currentNode.CurrentLevel - 1];
            // 显示白色文本的当前效果
            currentEffect.text = GetColoredText(metConditionHex, $"当前: {currentLevelData.levelDescription}\n冷却时间： {currentLevelData.cooldown}");
        }
        else
        {
            currentEffect.text = "";
        }
    }


    // 更新等级信息
    private void UpdateLevelInfo()
    {
        int currentLevel = currentNode.isUnlocked ? currentNode.CurrentLevel : 0;
        string levelText = $"等级：{currentLevel}/{currentSkillData.maxLevel}";
        string colorHex = currentNode.isUnlocked ? metConditionHex : "#888888";
        skillLevelInfo.text = GetColoredText(colorHex, levelText);
    }

  
    // 更新需求信息
    private void UpdateRequirements()
    {
        skillRequirements.text = currentNode.isLocked
            ? GetColoredText(importantInfoHex, lockedSkillText)
            : BuildRequirementsText();
    }


    // 构建需求文本（前置技能和冲突技能）
    private string BuildRequirementsText()
    {
        var requirementsBuilder = new StringBuilder();

        // 添加前置技能需求
        AppendAllPreSkillRequirements(requirementsBuilder);

        // 冲突技能
        AppendConflictSkills(requirementsBuilder);

        return requirementsBuilder.ToString();
    }

  
    // 追加前置技能需求的文本（显示）
    private void AppendAllPreSkillRequirements(StringBuilder builder)
    {
        var allPreRequirements = currentSkillData.preSkillRequirements ?? new PreSkillRequirement[0];
        if (allPreRequirements.Length == 0) return;

        builder.AppendLine("\n前置技能：");
        foreach (var preReq in allPreRequirements)
        {
            UI_TreeNode preNode = null;
            
            if (currentNode != null)
            {
                preNode = currentNode.FindSkillNodeByType(preReq.preSkillType);
            }
            
            string skillName;
            bool isMet;
            string currentLevelText = "";
            
            if (preNode != null && preNode.skillData != null)
            {
                skillName = preNode.skillData.displayName;
                isMet = preNode.isUnlocked && preNode.CurrentLevel >= preReq.preSkillLevel;
                currentLevelText = $"/当前 {preNode.CurrentLevel}";
            }
            else
            {
                skillName = preReq.preSkillType.ToString();
                isMet = false;
            }
            
            string nodeColor = isMet ? metConditionHex : notMetConditionHex;
            builder.AppendLine(GetColoredText(nodeColor,
                $"{skillName} (等级 {preReq.preSkillLevel}{currentLevelText})"));
        }
    }


    // 追加冲突技能的文本（显示）
    private void AppendConflictSkills(StringBuilder builder)
    {
        var conflictTypes = currentSkillData.conflictSkillTypes ?? new SkillUpgradeType[0];
        if (conflictTypes.Length == 0) return;

        builder.Append("冲突技能: ");

        List<string> conflictNames = new List<string>();
        foreach (var conflictType in conflictTypes)
        {
            UI_TreeNode conflictNode = null;
            
            if (currentNode != null)
            {
                conflictNode = currentNode.FindSkillNodeByType(conflictType);
            }
            
            if (conflictNode != null && conflictNode.skillData != null)
            {
                conflictNames.Add(conflictNode.skillData.displayName);
            }
            else
            {
                conflictNames.Add(conflictType.ToString());
            }
        }

        builder.AppendLine(GetColoredText(importantInfoHex, string.Join(" ", conflictNames)));
    }


    // 更新下一级加成信息
    private void UpdateNextLevelBonus()
    {
        if (currentNode.isUnlocked && currentNode.CurrentLevel >= currentSkillData.maxLevel)
        {
            nextLevelBonus.text = "";
            return;
        }

        int targetLevelIndex = currentNode.isUnlocked ? currentNode.CurrentLevel : 0;
        if (targetLevelIndex >= currentSkillData.levelDatas.Length)
        {
            nextLevelBonus.text = "";
            return;
        }

        var targetLevelData = currentSkillData.levelDatas[targetLevelIndex];
        string bonusPrefix = currentNode.isUnlocked ? "下一级加成" : "解锁加成";
        string bonusText = $"{bonusPrefix}(消耗：{targetLevelData.levelUpCost} 技能点)\n{targetLevelData.levelDescription}";
        string colorHex = "#888888";

        nextLevelBonus.text = GetColoredText(colorHex, bonusText);
    }

 

   // 查找 FindSkillNodeByType 一次性查找所有节点，显示前置技能和冲突技能时，找到对应的节点，并从该节点获取当前指定的节点。
    private UI_TreeNode FindSkillNodeByType(SkillUpgradeType targetType)
    {
        if (targetType == SkillUpgradeType.None || skillTree == null) return null;

        foreach (var node in skillTree.GetComponentsInChildren<UI_TreeNode>(true))
        {
            if (node.skillData != null && node.skillData.upgradeType == targetType)
            {
                return node;
            }
        }
        return null;
    }

    // 添加技能点不足闪烁效果
    public void NotEnoughSkillPointsEffect()
    {
        if (textEffectCo != null)
            StopCoroutine(textEffectCo);
        textEffectCo = StartCoroutine(TextBlinkEffectCo(nextLevelBonus, .15f, 3));
    }

    // 锁定技能的闪烁效果
    public void LockedSkillEffect()
    {
        if (textEffectCo != null)
            StopCoroutine(textEffectCo);
        textEffectCo = StartCoroutine(TextBlinkEffectCo(skillRequirements, .15f, 3));
    }
    // 技能点不足闪烁效果


    // 文本闪烁协程
    private IEnumerator TextBlinkEffectCo(TextMeshProUGUI text, float blinkInterval, int blinkCount)
    {
        // 保存原始文本用于恢复
        string originalText = text.text;

        for (int i = 0; i < blinkCount; i++)
        {
            // 根据当前显示的文本显示相应的文本
            string displayText = text == skillRequirements ? lockedSkillText : unCostSkillText;
            text.text = GetColoredText(notMetConditionHex, displayText);
            yield return new WaitForSeconds(blinkInterval);
            text.text = GetColoredText(importantInfoHex, displayText);
            yield return new WaitForSeconds(blinkInterval);
        }

        text.text = originalText;// 恢复原始文本
    }

    // 清除当前数据
    private void ClearCurrentData()
    {
        currentNode = null;
        currentSkillData = null;
        if (textEffectCo != null)
        {
            StopCoroutine(textEffectCo);
            textEffectCo = null;
        }
    }

 
    // 获取带颜色的文本
    public new string GetColoredText(string hexColor, string text)
    {
        return $"<color={hexColor}>{text}</color>";
    }
}
