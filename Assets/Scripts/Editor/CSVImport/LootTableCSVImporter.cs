using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LootTableCSVImporter
{
    private const string CSV_DIR = "Assets/Data/CSV";
    private const string SO_DIR = "Assets/Data/ItemData/战利品/掉落表";

    [MenuItem("Tools/CSV导入/导入掉落表")]
    public static void Import() { ImportInternal(); }

    private static void ImportInternal()
    {
        ItemCSVImporter.SyncXlsxToCsv();
        string path = $"{CSV_DIR}/LootTables.csv";
        if (!File.Exists(path)) { Debug.LogError($"找不到 {path}"); return; }

        var rows = CSVHelper.ParseFile(path);
        if (rows == null || rows.Count == 0) { Debug.LogError("LootTables.csv 为空"); return; }

        // 按 tableId 分组
        var groups = new Dictionary<string, List<Dictionary<string, string>>>();
        var tableNames = new Dictionary<string, string>();
        foreach (var row in rows)
        {
            string tid = CSVHelper.GetString(row, "tableId");
            if (string.IsNullOrWhiteSpace(tid)) continue;
            if (!groups.ContainsKey(tid)) groups[tid] = new List<Dictionary<string, string>>();
            groups[tid].Add(row);
            string tn = CSVHelper.GetString(row, "tableName");
            if (!string.IsNullOrWhiteSpace(tn)) tableNames[tid] = tn;
        }

        int created = 0, updated = 0;
        foreach (var kv in groups)
        {
            string tid = kv.Key;
            var itemRows = kv.Value;

            LootTable table = FindByTableId(tid);
            bool isNew = table == null;

            if (isNew)
            {
                table = ScriptableObject.CreateInstance<LootTable>();
                string folderPath = SO_DIR;
                string fileName = SanitizeFileName(tableNames.ContainsKey(tid) ? tableNames[tid] : tid);
                string fullPath = $"{folderPath}/{fileName}.asset";
                EnsureFolder();
                AssetDatabase.CreateAsset(table, fullPath);
                created++;
            }
            else { updated++; }

            table.tableId = tid;
            table.ClearLootItems();

            foreach (var row in itemRows)
            {
                string itemId = CSVHelper.GetId(row, "itemId");
                if (string.IsNullOrWhiteSpace(itemId)) continue;

                ItemDataSo itemSo = FindItemById(itemId);
                if (itemSo == null) { Debug.LogWarning($"找不到物品 {itemId}，跳过"); continue; }

                float chance = CSVHelper.GetFloat(row, "dropChance", 100);
                int minC = CSVHelper.GetInt(row, "minCount", 1);
                int maxC = CSVHelper.GetInt(row, "maxCount", 1);
                table.AddLootItem(itemSo, chance, minC, maxC);
            }

            EditorUtility.SetDirty(table);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"LootTables.csv: 新建 {created}，更新 {updated}");
    }

    private static void EnsureFolder()
    {
        string p = "Assets/Data/ItemData";
        foreach (var part in "战利品/掉落表".Split('/'))
        {
            string sub = $"{p}/{part}";
            if (!AssetDatabase.IsValidFolder(sub)) AssetDatabase.CreateFolder(p, part);
            p = sub;
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unnamed";
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid) name = name.Replace(c.ToString(), "_");
        return name;
    }

    private static LootTable FindByTableId(string id)
    {
        var guids = AssetDatabase.FindAssets("t:LootTable");
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var t = AssetDatabase.LoadAssetAtPath<LootTable>(p);
            if (t != null && t.tableId == id) return t;
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
