using UnityEditor;
using UnityEngine;

public class CSVImportWindow : EditorWindow
{
    [MenuItem("Tools/CSV导入/导入窗口")]
    public static void ShowWindow()
    {
        var w = GetWindow<CSVImportWindow>("CSV 导入工具");
        w.minSize = new Vector2(350, 360);
    }

    private void OnGUI()
    {
        GUILayout.Space(15);
        GUIStyle title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("📊 CSV 导入工具", title);
        GUILayout.Space(15);

        if (GUILayout.Button("📦 一键同步并导入 (F5)", GUILayout.Height(35)))
            ItemCSVImporter.SyncAndImport();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("物品", EditorStyles.boldLabel);
        if (GUILayout.Button("导入物品", GUILayout.Height(25))) ItemCSVImporter.ImportItems();
        if (GUILayout.Button("导入装备", GUILayout.Height(25))) ItemCSVImporter.ImportEquipment();
        if (GUILayout.Button("导入消耗品", GUILayout.Height(25))) ItemCSVImporter.ImportConsumables();

        GUILayout.Space(5);
        EditorGUILayout.LabelField("系统", EditorStyles.boldLabel);
        if (GUILayout.Button("导入属性配置", GUILayout.Height(25))) StatSetupImporter.Import();
        // if (GUILayout.Button("导入任务", GUILayout.Height(25))) QuestCSVImporter.Import(); // 已改用任务树编辑器
        if (GUILayout.Button("导入掉落表", GUILayout.Height(25))) LootTableCSVImporter.Import();
    }
}
