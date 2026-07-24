using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ItemCSVImporter
{
    private const string CSV_DIR = "Assets/Resources/CSV";
    private const string SO_DIR = "Assets/Resources/Data/ItemData";

    private static readonly HashSet<string> EquipItemTypes = new HashSet<string>
        { "武器", "头盔", "盔甲", "靴子", "手套", "饰品" };

    [MenuItem("Tools/CSV导入/一键同步并导入 _F5")]
    public static void SyncAndImport()
    {
        SyncXlsxToCsv();
        ImportAllInternal();
        Debug.Log("✅ 一键同步并导入完成！");
    }

    public static void SyncXlsxToCsv()
    {
        string scriptPath = System.IO.Path.GetFullPath("Assets/Resources/CSV/sync_xlsx_to_csv.ps1");
        if (!System.IO.File.Exists(scriptPath))
        {
            Debug.LogError($"找不到同步脚本: {scriptPath}");
            return;
        }

        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = "powershell";
        process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrEmpty(output)) Debug.Log(output);
        if (!string.IsNullOrEmpty(error)) Debug.LogError(error);
        AssetDatabase.Refresh();
    }

    private static void ImportAllInternal()
    {
        ImportItemsInternal();
        ImportEquipmentInternal();
        LootTableCSVImporter.Import();
        ImportConsumablesInternal();
    }

    [MenuItem("Tools/CSV导入/导入物品")]
    public static void ImportItems() { SyncXlsxToCsv(); ImportItemsInternal(); }

    private static void ImportItemsInternal()
    {
        var rows = LoadCSV("Items.csv");
        if (rows == null) return;

        int created = 0, updated = 0;
        foreach (var row in rows)
        {
            string id = CSVHelper.GetId(row, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            string itemType = CSVHelper.GetString(row, "type");
            string itemName = CSVHelper.GetString(row, "name");
            string folderName = string.IsNullOrWhiteSpace(itemType) ? "通用" : itemType;

            ItemDataSo so = FindSOByItemId(id);
            bool isNew = so == null;

            if (isNew)
            {
                string folderPath = $"{SO_DIR}/{folderName}";
                string fileName = SanitizeFileName(itemName);
                string assetPath = $"{folderPath}/{fileName}.asset";

                if (EquipItemTypes.Contains(itemType))
                    so = ScriptableObject.CreateInstance<EquipmentDataSo>();
                else if (itemType == "消耗品")
                    so = ScriptableObject.CreateInstance<ConsumableDataSo>();
                else
                    so = ScriptableObject.CreateInstance<ItemDataSo>();

                EnsureFolder(folderPath);
                AssetDatabase.CreateAsset(so, assetPath);
                created++;
            }
            else { updated++; }

            so.itemId = id;
            so.itemName = itemName;
            so.itemType = ParseItemType(itemType);
            so.rarity = CSVHelper.GetEnum<LootRarity>(row, "rarity", LootRarity.普通);
            so.canStackable = CSVHelper.GetBool(row, "canStackable");
            so.allowRarityVariation = CSVHelper.GetBool(row, "allowRarityVariation", true);
            so.maxRaritySteps = CSVHelper.GetInt(row, "maxRaritySteps", 1);
            so.value = CSVHelper.GetInt(row, "value");

            string iconPath = CSVHelper.GetString(row, "iconPath");
            string iconName = CSVHelper.GetString(row, "iconName");
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                if (!string.IsNullOrWhiteSpace(iconName))
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(iconPath);
                    foreach (var a in assets)
                        if (a is Sprite s && s.name == iconName) { so.itemIcon = s; break; }
                }
                else
                {
                    Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                    if (icon != null) so.itemIcon = icon;
                }
            }

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();

        // 删除CSV里不再存在的物品
        var csvIds = new HashSet<string>();
        foreach (var row in rows) csvIds.Add(CSVHelper.GetId(row, "id"));
        int deleted = 0;
        var allGuids = AssetDatabase.FindAssets("t:ItemDataSo");
        foreach (var g in allGuids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var so = AssetDatabase.LoadAssetAtPath<ItemDataSo>(p);
            if (so != null && !string.IsNullOrWhiteSpace(so.itemId) && !csvIds.Contains(so.itemId))
            {
                AssetDatabase.DeleteAsset(p);
                deleted++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"Items.csv: 新建 {created}，更新 {updated}，删除 {deleted}");
    }

    [MenuItem("Tools/CSV导入/导入装备")]
    public static void ImportEquipment() { SyncXlsxToCsv(); ImportEquipmentInternal(); }

    private static void ImportEquipmentInternal()
    {
        var rows = LoadCSV("Equipment.csv");
        if (rows == null) return;

        int updated = 0;
        foreach (var row in rows)
        {
            string id = CSVHelper.GetId(row, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var so = FindSOByItemId(id);
            if (!(so is EquipmentDataSo equip)) continue;

            var modifiers = new List<ItemModifier>();
            for (int i = 1; i <= 4; i++)
            {
                string t = CSVHelper.GetString(row, $"modifier{i}_type");
                string v = CSVHelper.GetString(row, $"modifier{i}_value");
                if (string.IsNullOrWhiteSpace(t) || string.IsNullOrWhiteSpace(v)) continue;
                if (CNStatMapping.TryParseStatType(t.Trim(), out StatType st) &&
                    float.TryParse(v.Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float val))
                    modifiers.Add(new ItemModifier { statType = st, value = val });
            }
            equip.modifiers = modifiers.ToArray();
            EditorUtility.SetDirty(equip);
            updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Equipment.csv: 更新 {updated}");
    }

    [MenuItem("Tools/CSV导入/导入消耗品")]
    public static void ImportConsumables() { SyncXlsxToCsv(); ImportConsumablesInternal(); }

    private static void ImportConsumablesInternal()
    {
        var rows = LoadCSV("Consumables.csv");
        if (rows == null) return;

        int updated = 0;
        foreach (var row in rows)
        {
            string id = CSVHelper.GetId(row, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var so = FindSOByItemId(id);
            if (!(so is ConsumableDataSo cons)) continue;

            cons.consumableType = CSVHelper.GetEnum<ConsumableType>(row, "consumableType");
            cons.effectType = CSVHelper.GetEnum<ConsumableEffectType>(row, "effectType");
            cons.effectValue = CSVHelper.GetFloat(row, "effectValue");
            cons.effectDuration = CSVHelper.GetFloat(row, "effectDuration");
            cons.buffStatType = CSVHelper.GetEnum<StatType>(row, "buffStatType");
            cons.cooldownTime = CSVHelper.GetInt(row, "cooldownTime");
            EditorUtility.SetDirty(cons);
            updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Consumables.csv: 更新 {updated}");
    }

    // ===== 辅助 =====

    private static ItemType ParseItemType(string cn)
    {
        if (cn == "武器") return ItemType.武器; if (cn == "头盔") return ItemType.头盔;
        if (cn == "盔甲") return ItemType.盔甲; if (cn == "靴子") return ItemType.靴子;
        if (cn == "手套") return ItemType.手套; if (cn == "饰品") return ItemType.饰品;
        if (cn == "材料") return ItemType.材料; if (cn == "消耗品") return ItemType.消耗品;
        return ItemType.None;
    }

    private static List<Dictionary<string, string>> LoadCSV(string file)
    {
        string path = $"{CSV_DIR}/{file}";
        if (!File.Exists(path)) { Debug.LogError($"找不到 {path}"); return null; }
        var rows = CSVHelper.ParseFile(path);
        if (rows == null || rows.Count == 0) { Debug.LogError($"{file} 为空"); return null; }
        return rows;
    }

    /// <summary>文件名不能包含的字符替换为下划线</summary>
    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unnamed";
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
            name = name.Replace(c.ToString(), "_");
        return name;
    }

    private static ItemDataSo FindSOByItemId(string id)
    {
        var guids = AssetDatabase.FindAssets("t:ItemDataSo");
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var so = AssetDatabase.LoadAssetAtPath<ItemDataSo>(p);
            if (so != null && so.itemId == id)
                return so;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = SO_DIR;
        foreach (var part in path.Replace(SO_DIR + "/", "").Split('/'))
        {
            string sub = $"{parent}/{part}";
            if (!AssetDatabase.IsValidFolder(sub)) AssetDatabase.CreateFolder(parent, part);
            parent = sub;
        }
    }
}
