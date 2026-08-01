using System.Collections.Generic;
using UnityEngine;

// 任务 UI 共享工具：色彩常量、奖励格式化。
// 统一 UI_QuestDialogue 和 UI_QuestPanel 的显示风格。
public static class QuestUIUtility
{
    // ─── 色彩常量 ───
    public const string ColorExp = "#4FC3F7";
    public const string ColorSkillPt = "#81C784";
    public const string ColorGold = "#FFD700";
    public const string ColorSilver = "#C0C0C0";
    public const string ColorCopper = "#CD7F32";
    public const string ColorMain = "#4A90D9";
    public const string ColorSide = "#5CB85C";
    public const string ColorTemporary = "#F0AD4E";

    // ─── 任务类型 ───

    public static string GetQuestTypeName(QuestType type) => type switch
    {
        QuestType.Main => "主线",
        QuestType.Side => "支线",
        QuestType.Temporary => "临时",
        _ => "",
    };

    public static string GetQuestTypeColor(QuestType type) => type switch
    {
        QuestType.Main => ColorMain,
        QuestType.Side => ColorSide,
        QuestType.Temporary => ColorTemporary,
        _ => "#FFFFFF",
    };

    public static string GetStageCnx(int stageIndex)
    {
        return stageIndex switch
        {
            0 => "一",
            1 => "二",
            2 => "三",
            _ => $"{stageIndex + 1}",
        };
    }

    // ─── 奖励格式化（两面板共用） ───

    public static string FormatReward(QuestReward reward)
    {
        if (reward == null) return "";
        var parts = new List<string>();
        if (reward.expAmount > 0) parts.Add($"<color={ColorExp}>经验x{reward.expAmount}</color>");
        if (reward.skillPoints > 0) parts.Add($"<color={ColorSkillPt}>技能点x{reward.skillPoints}</color>");
        if (reward.goldAmount > 0)
        {
            var amt = CurrencyFormatter.Split(reward.goldAmount);
            var goldParts = new List<string>();
            if (amt.gold > 0) goldParts.Add($"<color={ColorGold}>金x{amt.gold}</color>");
            if (amt.silver > 0) goldParts.Add($"<color={ColorSilver}>银x{amt.silver}</color>");
            if (amt.copper > 0) goldParts.Add($"<color={ColorCopper}>铜x{amt.copper}</color>");
            parts.Add(string.Join("", goldParts));
        }
        if (reward.items != null)
        {
            foreach (var item in reward.items)
            {
                if (item?.itemData != null)
                    parts.Add($"{item.itemData.itemName}x{item.amount}");
            }
        }
        return string.Join("  ", parts);
    }
}
