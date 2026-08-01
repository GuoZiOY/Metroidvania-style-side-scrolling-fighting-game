#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 配方 CSV 导入器 — 从 Resources/CSV/CraftingRecipes.csv 读取配方生成 CraftingRecipeDB
// 策划工作流：编辑 CraftingRecipes.xlsx → sync_xlsx_to_csv.ps1 → 本菜单导入 → SO 更新
// 一张配方表同时服务制作（消耗材料）与分解（配方反推产出 50%）
// 菜单: Tools/CSV导入/导入配方（正式纳入 CSV 系统，F5 一键同步含配方）
public static class CraftingRecipeCSVImporter
{
    private const string CsvPath = "Assets/Resources/CSV/CraftingRecipes.csv";
    private const string OutputPath = "Assets/Resources/Data/CraftingRecipeDB.asset";

    [MenuItem("Tools/CSV导入/导入配方")]
    public static void Import()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"[CraftingRecipeCSVImporter] 找不到配方CSV: {CsvPath}");
            return;
        }

        // 建立 itemId → ItemDataSo 字典（材料与装备都从 Resources 加载）
        var itemMap = new Dictionary<string, ItemDataSo>();
        foreach (var item in Resources.LoadAll<ItemDataSo>("Data/ItemData"))
        {
            if (item != null && !itemMap.ContainsKey(item.itemId))
                itemMap[item.itemId] = item;
        }

        string[] lines = File.ReadAllLines(CsvPath);
        var recipes = new List<CraftingRecipe>();

        for (int i = 1; i < lines.Length; i++) // 跳过表头
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue; // 跳过空行和注释

            var f = line.Split(',');
            if (f.Length < 9)
                continue;

            // 列: recipeId,recipeName,resultItemId,mat1Id,mat1Count,mat2Id,mat2Count,goldCost,minRarity
            if (!itemMap.TryGetValue(f[2].Trim(), out var resultItem))
            {
                Debug.LogWarning($"[CraftingRecipeCSVImporter] 第{i + 1}行产物 itemId 未找到: {f[2]}");
                continue;
            }

            var materials = new List<MaterialEntry>();
            bool valid = true;

            // 材料1（必填）
            if (!itemMap.TryGetValue(f[3].Trim(), out var mat1) || !int.TryParse(f[4].Trim(), out int mat1Count))
            {
                Debug.LogWarning($"[CraftingRecipeCSVImporter] 第{i + 1}行材料1无效: {f[3]}");
                valid = false;
            }
            else
            {
                materials.Add(new MaterialEntry { material = mat1, count = mat1Count });
            }

            // 材料2（mat2Id=0 表示无）
            if (f[5].Trim() != "0" && f[5].Trim().Length > 0)
            {
                if (itemMap.TryGetValue(f[5].Trim(), out var mat2) && int.TryParse(f[6].Trim(), out int mat2Count))
                    materials.Add(new MaterialEntry { material = mat2, count = mat2Count });
                else
                    Debug.LogWarning($"[CraftingRecipeCSVImporter] 第{i + 1}行材料2无效: {f[5]}");
            }

            if (!valid)
                continue;

            if (!int.TryParse(f[7].Trim(), out int goldCost))
                goldCost = 0;

            recipes.Add(new CraftingRecipe
            {
                recipeId = f[0].Trim(),
                recipeName = f[1].Trim(),
                resultItem = resultItem,
                materials = materials.ToArray(),
                goldCost = goldCost,
                minResultRarity = ParseRarity(f[8].Trim())
            });
        }

        // 生成数据库
        var db = ScriptableObject.CreateInstance<CraftingRecipeDB>();
        db.recipes = recipes.ToArray();

        if (File.Exists(OutputPath))
            AssetDatabase.DeleteAsset(OutputPath);

        AssetDatabase.CreateAsset(db, OutputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CraftingRecipeCSVImporter] 配方库已更新: {recipes.Count} 条 → {OutputPath}");
    }

    // 中文稀有度 → LootRarity
    private static LootRarity ParseRarity(string s)
    {
        return s switch
        {
            "普通" => LootRarity.普通,
            "精良" => LootRarity.精良,
            "稀有" => LootRarity.稀有,
            "史诗" => LootRarity.史诗,
            "传说" => LootRarity.传说,
            _ => LootRarity.普通
        };
    }
}
#endif
