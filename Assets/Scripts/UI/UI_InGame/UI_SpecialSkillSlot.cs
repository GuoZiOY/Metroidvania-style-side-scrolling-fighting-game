using UnityEngine;
using UnityEngine.UI;

public class UI_SpecialSkillSlot : UI_SkillSlot
{
    [Header("特殊技能槽配置")]
    [SerializeField] private SkillType restrictedSkillType; // 限制的技能类型（只能绑定该类型的技能）

    [Header("视觉反馈")]
    [SerializeField] private Color restrictedColor = new Color(0.8f, 0.4f, 0.4f); // 限制颜色（红色）

    public override bool CanAcceptSkill(SkillUpgradeType upgradeType) // 重写：检查是否可以接受该技能
    {
        // 基础检查
        if (upgradeType == SkillUpgradeType.None)
            return false;

        // 获取技能类型
        SkillType skillType = GetSkillTypeByUpgradeType(upgradeType);
        if (skillType == SkillType.None)
            return false;

        // 检查是否符合限制类型
        return skillType == restrictedSkillType;
    }

    public override void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) // 重写：鼠标进入事件处理
    {
        // 显示技能类型提示
        if (UI_SkillDragHandler.Instance != null && UI_SkillDragHandler.Instance.IsDragging)
        {
            SkillUpgradeType draggedUpgradeType = UI_SkillDragHandler.Instance.DraggingUpgradeType;
            bool canAccept = CanAcceptSkill(draggedUpgradeType);

            if (background != null)
            {
                background.color = canAccept ? highlightColor : restrictedColor;
            }
        }
    }

    public override void OnSkillDropped(SkillUpgradeType upgradeType, Skill_DataSo skillData, UI_TreeNode sourceNode, UI_SkillSlot sourceSlot) // 重写：技能放置事件处理
    {
        // 检查是否可以接受该技能
        if (!CanAcceptSkill(upgradeType))
        {
            Debug.LogWarning($"该槽位只能绑定 {restrictedSkillType} 类型的技能！");
            return;
        }

        // 调用基类方法
        base.OnSkillDropped(upgradeType, skillData, sourceNode, sourceSlot);
    }

    private SkillType GetSkillTypeByUpgradeType(SkillUpgradeType upgradeType) // 从DataManager获取技能大类型
    {
        if (upgradeType == SkillUpgradeType.None) return SkillType.None;
        return SkillDataManager.Instance?.GetSkillType(upgradeType) ?? SkillType.None;
    }

    public SkillType RestrictedSkillType => restrictedSkillType; // 获取限制的技能类型
}