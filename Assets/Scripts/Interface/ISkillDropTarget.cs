using UnityEngine;

public interface ISkillDropTarget // 技能放置目标接口
{
    bool CanAcceptSkill(SkillUpgradeType upgradeType); // 检查是否可以接受指定升阶类型的技能
    void OnSkillDropped(SkillUpgradeType upgradeType, Skill_DataSo skillData, UI_TreeNode sourceNode, UI_SkillSlot sourceSlot); // 技能放置事件处理
}
