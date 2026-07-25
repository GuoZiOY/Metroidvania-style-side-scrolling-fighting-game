using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

// 任务树画板。管理所有节点、连线、选择、过滤。
public class QuestGraphView : GraphView
{
    private readonly QuestTreeEditorWindow editor;
    private string filterText = "";

    public QuestGraphView(QuestTreeEditorWindow editorWindow)
    {
        editor = editorWindow;

        // 画板设置
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        // 网格背景
        var grid = CreateGridBackground();
        Insert(0, grid);

        // 节点除连
        graphViewChanged += OnGraphViewChanged;

        // 右键菜单
        RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
    }

    // ─── 网格背景 ───

    private GridBackground CreateGridBackground()
    {
        var grid = new GridBackground();
        grid.StretchToParentSize();
        return grid;
    }

    // ─── 节点操作 ───

    public void RebuildFromQuests(List<QuestData> quests)
    {
        DeleteElements(graphElements.ToList());
        foreach (var quest in quests)
            AddQuestNode(quest);
        ConnectPrerequisites();
    }

    public QuestNode AddQuestNode(QuestData quest)
    {
        // 检查是否已存在
        var existing = GetNodeByGuid(quest.questId) as QuestNode;
        if (existing != null) return existing;

        var node = new QuestNode(quest, editor);
        node.SetPosition(new Rect(Random.Range(100, 600), Random.Range(100, 400), 280, 150));
        AddElement(node);

        // 连接前置→后续
        if (quest.prerequisiteQuestIds != null)
        {
            foreach (var prereqId in quest.prerequisiteQuestIds)
            {
                var prereqNode = GetNodeByGuid(prereqId) as QuestNode;
                if (prereqNode != null)
                    ConnectNodes(prereqNode, node);
            }
        }

        return node;
    }

    public void FilterNodes(string filter)
    {
        filterText = filter?.ToLower() ?? "";
        foreach (var node in nodes.OfType<QuestNode>())
        {
            bool match = string.IsNullOrEmpty(filterText) ||
                         node.QuestData.questName.ToLower().Contains(filterText) ||
                         node.QuestData.questId.ToLower().Contains(filterText);
            node.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    // ─── 连线 ───

    private void ConnectNodes(QuestNode from, QuestNode to)
    {
        var edge = from.OutputPort.ConnectTo(to.InputPort);
        AddElement(edge);
    }

    private void ConnectPrerequisites()
    {
        foreach (var node in nodes.OfType<QuestNode>())
        {
            var quest = node.QuestData;
            if (quest.prerequisiteQuestIds == null) continue;

            foreach (var prereqId in quest.prerequisiteQuestIds)
            {
                var prereqNode = GetNodeByGuid(prereqId) as QuestNode;
                if (prereqNode != null)
                {
                    // 检查是否已连接
                    bool alreadyConnected = edges.Any(e =>
                        e.output.node == prereqNode && e.input.node == node);
                    if (!alreadyConnected)
                        ConnectNodes(prereqNode, node);
                }
            }
        }
    }

    // ─── 自动排列 ───

    public void AutoLayoutNodes()
    {
        var allNodes = nodes.OfType<QuestNode>().ToList();
        if (allNodes.Count == 0) return;

        // 按连线构建层级
        var queue = new Queue<QuestNode>();
        var visited = new HashSet<string>();
        float x = 50, y = 50;
        float xSpacing = 320, ySpacing = 200;

        // 找根节点（没有被别的节点指向的）
        var hasIncoming = new HashSet<string>();
        foreach (var edge in edges)
        {
            if (edge.input.node is QuestNode target)
                hasIncoming.Add(target.QuestData.questId);
        }

        // BFS 排列
        int layer = 0;
        var currentLayer = allNodes.Where(n => !hasIncoming.Contains(n.QuestData.questId)).ToList();
        if (currentLayer.Count == 0) currentLayer = new List<QuestNode> { allNodes[0] };

        while (currentLayer.Count > 0)
        {
            float xPos = x;
            for (int i = 0; i < currentLayer.Count; i++)
            {
                currentLayer[i].SetPosition(new Rect(xPos, y + layer * ySpacing, 280, 150));
                visited.Add(currentLayer[i].QuestData.questId);
                xPos += xSpacing;
            }

            var nextLayer = new List<QuestNode>();
            foreach (var node in currentLayer)
            {
                foreach (var edge in edges)
                {
                    if (edge.output.node == node && edge.input.node is QuestNode target &&
                        !visited.Contains(target.QuestData.questId) && !nextLayer.Contains(target))
                        nextLayer.Add(target);
                }
            }
            currentLayer = nextLayer;
            layer++;
        }
    }

    // ─── 从节点同步数据到 SO ───

    public void ApplyNodeDataToQuests()
    {
        foreach (var node in nodes.OfType<QuestNode>())
        {
            var quest = node.QuestData;
            if (quest == null) continue;

            // 更新前置任务列表
            quest.prerequisiteQuestIds = edges
                .Where(e => e.input.node == node && e.output.node is QuestNode src)
                .Select(e => ((QuestNode)e.output.node).QuestData.questId)
                .ToList();

            // 更新后续任务列表
            quest.followUpQuestIds = edges
                .Where(e => e.output.node == node && e.input.node is QuestNode dst)
                .Select(e => ((QuestNode)e.input.node).QuestData.questId)
                .ToList();
        }
    }

    // ─── 事件处理 ───

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        // 添加上下文菜单创建节点
        return change;
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button == 1)  // 右键
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("新建任务"), false, () =>
            {
                editor.CreateNewQuest();
            });
            menu.ShowAsContext();
        }
    }

    // ─── GraphView 必须实现的抽象方法 ───

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatible = new List<Port>();
        foreach (var port in ports.ToList())
        {
            if (port.node == startPort.node) continue;
            if (port.direction == startPort.direction) continue;
            compatible.Add(port);
        }
        return compatible;
    }
}
