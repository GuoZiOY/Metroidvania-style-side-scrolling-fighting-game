using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

// 任务树编辑器主窗口。
// Tools → 任务树编辑器
public class QuestTreeEditorWindow : EditorWindow
{
    private QuestGraphView graphView;
    private InspectorPanel inspectorPanel;

    private string assetPath = "Assets/Resources/Data";
    private List<QuestData> allQuests = new();

    [MenuItem("Tools/任务树编辑器")]
    public static void Open()
    {
        var w = GetWindow<QuestTreeEditorWindow>();
        w.titleContent = new GUIContent("任务树编辑器");
        w.minSize = new Vector2(1000, 600);
        w.Show();
    }

    private void OnEnable()
    {
        BuildGraphView();
        BuildInspectorPanel();
        BuildToolbar();
        LoadAllQuests();
    }

    private void BuildGraphView()
    {
        graphView = new QuestGraphView(this)
        {
            name = "任务树画板",
            viewDataKey = "QuestTreeGraph"
        };
        graphView.StretchToParentSize();
        rootVisualElement.Add(graphView);
    }

    private void BuildInspectorPanel()
    {
        inspectorPanel = new InspectorPanel();
        inspectorPanel.style.width = 320;
        inspectorPanel.style.position = Position.Absolute;
        inspectorPanel.style.right = 0;
        inspectorPanel.style.top = 40;
        inspectorPanel.style.bottom = 0;
        inspectorPanel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.95f);
        rootVisualElement.Add(inspectorPanel);
    }

    private void BuildToolbar()
    {
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.paddingLeft = 4;
        toolbar.style.paddingRight = 4;
        toolbar.style.paddingTop = 2;
        toolbar.style.paddingBottom = 2;
        toolbar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        toolbar.style.borderBottomWidth = 1;
        toolbar.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);

        toolbar.Add(MakeToolbarButton("💾 保存全部", SaveAllQuests));
        toolbar.Add(MakeToolbarButton("🔄 重载", LoadAllQuests));
        toolbar.Add(MakeToolbarButton("➕ 新建", CreateNewQuest));
        toolbar.Add(MakeToolbarButton("📐 排列", AutoLayout));

        rootVisualElement.Add(toolbar);
    }

    private static Button MakeToolbarButton(string text, System.Action action)
    {
        var btn = new Button(action);
        btn.text = text;
        btn.style.fontSize = 12;
        btn.style.paddingLeft = 8;
        btn.style.paddingRight = 8;
        btn.style.paddingTop = 3;
        btn.style.paddingBottom = 3;
        btn.style.marginLeft = 2;
        btn.style.marginRight = 2;
        btn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
        btn.style.color = Color.white;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        return btn;
    }

    // ─── 数据操作 ───

    private void LoadAllQuests()
    {
        var guids = AssetDatabase.FindAssets("t:QuestData", new[] { "Assets" });
        allQuests.Clear();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
            if (quest != null) allQuests.Add(quest);
        }

        graphView?.RebuildFromQuests(allQuests);
        inspectorPanel?.ClearSelection();
        Debug.Log($"[任务树编辑器] 加载了 {allQuests.Count} 个任务");
    }

    private void SaveAllQuests()
    {
        // 从 GraphView 同步所有节点数据到 SO
        graphView?.ApplyNodeDataToQuests();

        foreach (var quest in allQuests)
        {
            EditorUtility.SetDirty(quest);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[任务树编辑器] 已保存 {allQuests.Count} 个任务");
    }

    public void CreateNewQuest()
    {
        var quest = ScriptableObject.CreateInstance<QuestData>();
        quest.questId = "new_quest_" + System.DateTime.Now.Ticks;
        quest.questName = "新任务";
        quest.questType = QuestType.Side;
        quest.stages = new List<QuestStage>();

        // 默认添加一个阶段
        quest.stages.Add(new QuestStage
        {
            description = "新阶段",
            objectives = new List<ObjectiveConfig>(),
        });

        // 保存为 asset
        string path = EditorUtility.SaveFilePanelInProject(
            "保存任务数据", quest.questName, "asset", "选择保存位置", assetPath);

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(quest, path);
            AssetDatabase.SaveAssets();
            allQuests.Add(quest);
            graphView?.AddQuestNode(quest);
            Debug.Log($"[任务树编辑器] 创建任务: {quest.questName}");
        }
        else
        {
            Object.DestroyImmediate(quest);
        }
    }

    private void AutoLayout()
    {
        graphView?.AutoLayoutNodes();
    }

    public void OnNodeSelected(QuestNode node)
    {
        inspectorPanel?.ShowQuest(node.QuestData, node);
    }

    public void OnNodeDeselected()
    {
        inspectorPanel?.ClearSelection();
    }

    public void RefreshNodeUI(QuestNode node)
    {
        node?.RefreshVisuals();
    }

    public List<QuestData> GetAllQuests() => allQuests;
}
