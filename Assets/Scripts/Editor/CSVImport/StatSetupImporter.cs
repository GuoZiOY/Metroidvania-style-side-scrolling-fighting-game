using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StatSetupImporter
{
    private const string CSV_DIR = "Assets/Resources/CSV";
    private const string SO_DIR = "Assets/Resources/Data/默认属性设置";

    [MenuItem("Tools/CSV导入/导入属性配置")]
    public static void Import() { ImportInternal(); }

    private static void ImportInternal()
    {
        string path = $"{CSV_DIR}/StatSetups.csv";
        if (!File.Exists(path)) { Debug.LogError($"找不到 {path}"); return; }

        var rows = CSVHelper.ParseFile(path);
        if (rows == null || rows.Count == 0) { Debug.LogError("StatSetups.csv 为空"); return; }

        int created = 0, updated = 0;
        foreach (var row in rows)
        {
            string id = CSVHelper.GetId(row, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            Stat_SetupSO so = FindBySetupId(id);
            bool isNew = so == null;

            if (isNew)
            {
                so = ScriptableObject.CreateInstance<Stat_SetupSO>();
                string fileName = SanitizeFileName(CSVHelper.GetString(row, "name"));
                string assetPath = $"{SO_DIR}/{fileName}.asset";
                EnsureFolder();
                AssetDatabase.CreateAsset(so, assetPath);
                created++;
            }
            else { updated++; }

            so.setupId = id;
            so.maxHP = CSVHelper.GetFloat(row, "maxHP", 100);
            so.healthRegen = CSVHelper.GetFloat(row, "healthRegen");
            so.damage = CSVHelper.GetFloat(row, "damage", 10);
            so.attackSpeed = CSVHelper.GetFloat(row, "attackSpeed", 1);
            so.critChance = CSVHelper.GetFloat(row, "critChance");
            so.critPower = CSVHelper.GetFloat(row, "critPower", 150);
            so.armorReduction = CSVHelper.GetFloat(row, "armorReduction");
            so.elementalHeart = CSVHelper.GetFloat(row, "elementalHeart");
            so.fireDamage = CSVHelper.GetFloat(row, "fireDamage");
            so.iceDamage = CSVHelper.GetFloat(row, "iceDamage");
            so.lightningDamage = CSVHelper.GetFloat(row, "lightningDamage");
            so.armor = CSVHelper.GetFloat(row, "armor");
            so.evasion = CSVHelper.GetFloat(row, "evasion");
            so.fireResistance = CSVHelper.GetFloat(row, "fireResistance");
            so.iceResistance = CSVHelper.GetFloat(row, "iceResistance");
            so.lightningResistance = CSVHelper.GetFloat(row, "lightningResistance");
            so.strength = CSVHelper.GetFloat(row, "strength");
            so.agility = CSVHelper.GetFloat(row, "agility");
            so.intelligence = CSVHelper.GetFloat(row, "intelligence");
            so.vitality = CSVHelper.GetFloat(row, "vitality");

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"StatSetups.csv: 新建 {created}，更新 {updated}");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(SO_DIR))
            AssetDatabase.CreateFolder("Assets/Data", "默认属性设置");
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unnamed";
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid) name = name.Replace(c.ToString(), "_");
        return name;
    }

    private static Stat_SetupSO FindBySetupId(string id)
    {
        var guids = AssetDatabase.FindAssets("t:Stat_SetupSO");
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var so = AssetDatabase.LoadAssetAtPath<Stat_SetupSO>(p);
            if (so != null && so.setupId == id)
                return so;
        }
        return null;
    }
}
