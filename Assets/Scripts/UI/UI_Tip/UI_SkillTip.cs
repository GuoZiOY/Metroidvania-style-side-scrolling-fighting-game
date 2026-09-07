using UnityEngine;

public class UI_SkillTip : BaseTip
{
    #region 对外调用方法（保持原有接口）
    // 解锁成功提示
    public void ShowUnlockSuccess(string skillName)
    {
        ShowTip($"成功解锁技能: {skillName}", true, AnimationType.滑动, false); // 显示成功提示
    }

    // 升级成功提示
    public void ShowUpgradeSuccess(string skillName, int newLevel)
    {
        ShowTip($"技能升级成功: {skillName} Lv.{newLevel}", true, AnimationType.滑动, false); // 显示升级成功提示
    }

    public void ShowPreSkillNotMetTip()
    {
        ShowTip("前置技能未完全满足", false, AnimationType.基础, true); // 显示前置技能未满足提示
    }

    // 冲突分支锁定提示
    public void ShowConflictLockedTip()
    {
        ShowTip("已选择了不同分支，该技能已被锁定", false, AnimationType.基础, true); // 显示冲突分支锁定提示
    }

    // 技能点不足提示
    public void ShowNotEnoughSkillPointsTip()
    {
        ShowTip("技能点不足", false, AnimationType.基础, true); // 显示技能点不足提示
    }

    // 技能槽冲突提示
    public void ShowSkillSlotConflictTip(string message)
    {
        ShowTip(message, false, AnimationType.基础, true); // 显示技能槽冲突提示
    }
    #endregion
}
