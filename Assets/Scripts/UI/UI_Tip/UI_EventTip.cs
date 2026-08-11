using UnityEngine;

public class UI_EventTip : BaseTip
{
    #region 对外调用方法

    // 显示物品获得提示
    public void ShowItemObtained(string itemName, int count = 1)
    {
        string message = count > 1 ? $"获得 {itemName} x{count}" : $"获得 {itemName}";
        ShowTip(message, true); // 显示物品获得提示
    }

    // 通用失败提示（红色 + 基础动画 + 抖动），供仓库存取等操作失败时调用
    public void ShowDenyTip(string message)
    {
        ShowTip(message, false, AnimationType.基础, true);
    }

    // 显示等级提升提示
    public void ShowLevelUp(int newLevel)
    {
        ShowTip($"等级提升! Lv.{newLevel}", true); // 显示等级提升提示
    }


    // ========== 任务相关提示 ==========

    public void ShowQuestAccepted(string questName)
    {
        ShowTip($"接受任务: {questName}", true, AnimationType.滑动, false);
    }

    public void ShowQuestCompleted(string questName)
    {
        ShowTip($"任务完成: {questName}", true, AnimationType.滑动, false);
    }

    public void ShowQuestFailed(string questName)
    {
        ShowTip($"任务失败: {questName}", false, AnimationType.基础, true);
    }
    // ========== 存档提示 ==========

    public void ShowSaveSuccess()
    {
        ShowTip("存档成功", true, AnimationType.基础, false);
    }

    // ========== 区域相关提示 ==========

    // 显示遭遇敌人提示（基础动画）
    public void ShowEnemyEncountered(int enemyCount)
    {
        ShowTip($"遭遇敌人！(数量: {enemyCount})", false, AnimationType.基础, true);
    }

    // 显示区域清空提示（基础动画）
    public void ShowAreaCleared(int defeatedCount)
    {
        ShowTip($"已歼灭当前区域所有敌人！(歼灭: {defeatedCount})", true, AnimationType.基础, false);
    }

    // 显示进入关卡提示（滑动动画）
    public void ShowLevelEnter(string levelName)
    {
        ShowTip($"进入关卡: {levelName}", true, AnimationType.滑动, false);
    }

    // 显示关卡完成提示
    public void ShowLevelComplete(string levelName, int defeatedCount)
    {
        ShowTip($"完成关卡: {levelName} (击败: {defeatedCount})", true, AnimationType.滑动, false);
    }

    // 显示关卡失败提示
    public void ShowLevelFailed(string levelName)
    {
        ShowTip($":{levelName} 关卡失败！", false, AnimationType.基础, true);
    }
    #endregion
}
