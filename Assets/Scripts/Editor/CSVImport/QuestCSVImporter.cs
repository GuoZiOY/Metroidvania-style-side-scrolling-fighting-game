using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class QuestCSVImporter
{
    private const string CSV_DIR = "Assets/Resources/CSV";
    private const string SO_DIR = "Assets/Resources/Data/QuestData";

    [MenuItem("Tools/CSV导入/导入任务")]
    public static void Import() { ImportInternal(); }

    private static void ImportInternal()
    {
        string path = $"{CSV_DIR}/Quests.csv";
        if (!File.Exists(path)) { Debug.LogError($"找不到 {path}"); return; }

        var rows = CSVHelper.ParseFile(path);
        if (rows == null || rows.Count == 0) { Debug.LogError("Quests.csv 为空"); return; }

        int created = 0, updated = 0;
        foreach (var row in rows)
        {
            string id = CSVHelper.GetId(row, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            QuestData so = FindByQuestId(id);
            bool isNew = so == null;

            if (isNew)
            {
                so = ScriptableObject.CreateInstance<QuestData>();
                string folderPath = SO_DIR;
                string fileName = SanitizeFileName(CSVHelper.GetString(row, "name"));
                if (!AssetDatabase.IsValidFolder(folderPath))
                    AssetDatabase.CreateFolder("Assets/Data", "QuestData");
                AssetDatabase.CreateAsset(so, $"{folderPath}/{fileName}.asset");
                created++;
            }
            else { updated++; }

            so.questId = id;
            so.questName = CSVHelper.GetString(row, "name");
            so.questType = CSVHelper.GetString(row, "type") == "Main" ? QuestType.Main : QuestType.Side;
            so.description = CSVHelper.GetString(row, "description");

                so.objectives = new List<ObjectiveConfig>();
            for (int i = 1; i <= 2; i++)
            {
                string t = CSVHelper.GetString(row, $"obj{i}_type");
                string targetId = CSVHelper.GetId(row, $"obj{i}_targetId");
                if (string.IsNullOrWhiteSpace(t) || string.IsNullOrWhiteSpace(targetId)) continue;
                int count = CSVHelper.GetInt(row, $"obj{i}_count", 1);
                so.objectives.Add(new ObjectiveConfig
                {
                    type = t == "Kill" ? ObjectiveType.Kill : ObjectiveType.Collect,
                    targetId = targetId,
                    requiredCount = count
                });
            }

            // 奖励
            so.reward = new QuestReward
            {
                expAmount = CSVHelper.GetInt(row, "rewardExp"),
                skillPoints = CSVHelper.GetInt(row, "rewardSkillPoints"),
                items = new List<RewardItem>()
            };
            for (int i = 1; i <= 2; i++)
            {
                string itemId = CSVHelper.GetId(row, $"rewardItem{i}_id");
                if (string.IsNullOrWhiteSpace(itemId)) continue;
                int amount = CSVHelper.GetInt(row, $"rewardItem{i}_amount", 1);
                ItemDataSo itemSo = FindItemById(itemId);
                if (itemSo != null)
                    so.reward.items.Add(new RewardItem { itemData = itemSo, amount = amount });
            }

            // 前置
            so.prerequisiteQuestIds = new List<string>();
            for (int i = 1; i <= 2; i++)
            {
                string pre = CSVHelper.GetId(row, $"prereq{i}");
                if (!string.IsNullOrWhiteSpace(pre)) so.prerequisiteQuestIds.Add(pre);
            }

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Quests.csv: 新建 {created}，更新 {updated}");
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unnamed";
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid) name = name.Replace(c.ToString(), "_");
        return name;
    }

    private static QuestData FindByQuestId(string id)
    {
        var guids = AssetDatabase.FindAssets("t:QuestData");
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var so = AssetDatabase.LoadAssetAtPath<QuestData>(p);
            if (so != null && so.questId == id) return so;
        }
        return null;
    }

    private static ItemDataSo FindItemById(string id)
    {
        var guids = AssetDatabase.FindAssets("t:ItemDataSo");
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var so = AssetDatabase.LoadAssetAtPath<ItemDataSo>(p);
            if (so != null && so.itemId == id) return so;
        }
        return null;
    }


}
