using System.Collections.Generic;
using UnityEngine;

/// <summary>运行时根据 targetId 解析目标显示名称（从 Resources/CSV 读取）</summary>
public static class TargetNameResolver
{
    private static Dictionary<string, string> itemNames;
    private static Dictionary<string, string> entityNames;
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        itemNames = new Dictionary<string, string>();
        entityNames = new Dictionary<string, string>();

        // 从 Resources/CSV/ 读取物品名称
        TextAsset itemsCsv = Resources.Load<TextAsset>("CSV/Items");
        if (itemsCsv != null)
            ParseCsv(itemsCsv.text, itemNames);
        else
            Debug.LogWarning("TargetNameResolver: 找不到 Resources/CSV/Items.csv");

        // 从 Resources/CSV/ 读取实体名称
        TextAsset entitiesCsv = Resources.Load<TextAsset>("CSV/Entities");
        if (entitiesCsv != null)
            ParseCsv(entitiesCsv.text, entityNames);
        else
            Debug.LogWarning("TargetNameResolver: 找不到 Resources/CSV/Entities.csv");
    }

    private static void ParseCsv(string csvText, Dictionary<string, string> target)
    {
        string[] lines = csvText.Split('\n');
        for (int i = 1; i < lines.Length; i++) // 跳过表头
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] parts = line.Split(',');
            if (parts.Length >= 2)
            {
                string id = parts[0].Trim().Trim('"');
                string name = parts[1].Trim().Trim('"');
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name) && !target.ContainsKey(id))
                    target[id] = name;
            }
        }
    }

    public static string Resolve(ObjectiveType type, string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return targetId;
        if (!initialized) Initialize();

        // 兼容旧数据可能存了 "id - name" 格式，提取纯 ID
        int dash = targetId.IndexOf(" - ");
        string bareId = dash >= 0 ? targetId.Substring(0, dash) : targetId;

        var dict = type switch
        {
            ObjectiveType.Kill => entityNames,
            ObjectiveType.Collect => itemNames,
            ObjectiveType.TalkToNPC => entityNames,
            _ => itemNames,
        };
        return dict.TryGetValue(bareId, out var name) ? name : bareId;
    }
}
