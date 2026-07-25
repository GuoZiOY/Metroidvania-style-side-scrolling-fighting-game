using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

// 任务树节点。一个节点对应一个 QuestData SO。
// 显示任务名、类型标签、阶段摘要、前后置端口。
public class QuestNode : Node
{
    public QuestData QuestData { get; private set; }
    public Port InputPort { get; private set; }
    public Port OutputPort { get; private set; }

    private readonly QuestTreeEditorWindow editor;
    private readonly Label questNameLabel;
    private readonly Label typeLabel;
    private readonly Label stagesLabel;
    private readonly Foldout stagesFoldout;

    public QuestNode(QuestData quest, QuestTreeEditorWindow editorWindow) : base()
    {
        QuestData = quest;
        editor = editorWindow;

        title = "";
        viewDataKey = quest.questId;

        // 设置尺寸
        SetPosition(new Rect(100, 100, 280, 160));

        // ─── 标题区域 ───
        var titleContainer = new VisualElement();
        titleContainer.style.flexDirection = FlexDirection.Row;
        titleContainer.style.alignItems = Align.Center;
        titleContainer.style.paddingLeft = 8;
        titleContainer.style.paddingTop = 4;
        titleContainer.style.paddingBottom = 2;

        // 任务名
        questNameLabel = new Label(quest.questName);
        questNameLabel.style.fontSize = 15;
        questNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        questNameLabel.style.color = Color.white;
        questNameLabel.style.flexGrow = 1;
        titleContainer.Add(questNameLabel);

        // 类型标签
        typeLabel = new Label(GetTypeLabel(quest.questType));
        typeLabel.style.fontSize = 11;
        typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        typeLabel.style.color = GetTypeColor(quest.questType);
        typeLabel.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        typeLabel.style.paddingLeft = 6;
        typeLabel.style.paddingRight = 6;
        typeLabel.style.paddingTop = 2;
        typeLabel.style.paddingBottom = 2;
        typeLabel.style.borderTopLeftRadius = 4;
        typeLabel.style.borderTopRightRadius = 4;
        typeLabel.style.borderBottomLeftRadius = 4;
        typeLabel.style.borderBottomRightRadius = 4;
        titleContainer.Add(typeLabel);

        mainContainer.Add(titleContainer);

        // ─── 阶段摘要 ───
        stagesLabel = new Label();
        stagesLabel.style.whiteSpace = WhiteSpace.Normal;
        stagesLabel.style.fontSize = 12;
        stagesLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
        stagesLabel.style.paddingLeft = 8;
        stagesLabel.style.paddingRight = 8;
        stagesLabel.style.paddingTop = 4;
        stagesLabel.style.paddingBottom = 4;
        mainContainer.Add(stagesLabel);

        // ─── ID 标识（右下角小字） ───
        var idLabel = new Label($"ID: {quest.questId}");
        idLabel.style.fontSize = 10;
        idLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
        idLabel.style.paddingLeft = 8;
        idLabel.style.paddingBottom = 2;
        mainContainer.Add(idLabel);

        // ─── 端口 ───
        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(QuestData));
        InputPort.portName = "前置";
        inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(QuestData));
        OutputPort.portName = "后续";
        outputContainer.Add(OutputPort);

        // ─── 点击选中 ───
        RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button == 0)
                editor?.OnNodeSelected(this);
        });

        // ─── 节点背景色 ───
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        if (QuestData == null) return;

        questNameLabel.text = QuestData.questName;
        typeLabel.text = GetTypeLabel(QuestData.questType);
        typeLabel.style.color = GetTypeColor(QuestData.questType);

        // 阶段摘要
        string stagesText = "";
        if (QuestData.stages != null)
        {
            for (int i = 0; i < QuestData.stages.Count; i++)
            {
                var stage = QuestData.stages[i];
                int objCount = stage.objectives?.Count ?? 0;
                stagesText += $"阶段{i + 1}: {stage.description} ({objCount}目标)\n";
            }
        }
        if (string.IsNullOrEmpty(stagesText))
            stagesText = "<color=#666>未配置阶段</color>";
        stagesLabel.text = stagesText.TrimEnd('\n');

        // 背景色（根据任务类型）
        var color = QuestData.questType switch
        {
            QuestType.Main => new Color(0.25f, 0.35f, 0.55f, 0.9f),
            QuestType.Side => new Color(0.30f, 0.45f, 0.30f, 0.9f),
            QuestType.Temporary => new Color(0.45f, 0.35f, 0.25f, 0.9f),
            _ => new Color(0.3f, 0.3f, 0.3f, 0.9f),
        };
        mainContainer.style.backgroundColor = color;
    }

    private static string GetTypeLabel(QuestType type) => type switch
    {
        QuestType.Main => "主线",
        QuestType.Side => "支线",
        QuestType.Temporary => "临时",
        _ => "未知",
    };

    private static Color GetTypeColor(QuestType type) => type switch
    {
        QuestType.Main => new Color(0.5f, 0.7f, 1.0f),
        QuestType.Side => new Color(0.5f, 1.0f, 0.5f),
        QuestType.Temporary => new Color(1.0f, 0.8f, 0.4f),
        _ => Color.gray,
    };
}
