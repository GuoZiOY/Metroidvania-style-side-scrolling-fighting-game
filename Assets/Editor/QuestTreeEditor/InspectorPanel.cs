using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// 任务属性面板。选中节点后在右侧显示，支持所有字段编辑。
public class InspectorPanel : VisualElement
{
    private QuestData currentQuest;
    private QuestNode currentNode;

    // 缓存的字段控件
    private TextField questIdField;
    private TextField questNameField;
    private EnumField questTypeField;
    private EnumField triggerField;
    private VisualElement triggerNpcIdField;
    private Toggle autoAcceptField;
    private TextField descriptionField;
    private ListView stagesListView;
    private Button addStageBtn;
    private Button deleteQuestBtn;
    private VisualElement stageDetailContainer;
    private VisualElement finalRewardContainer;

    private ScrollView scrollView;

    public InspectorPanel()
    {
        BuildUI();
        ClearSelection();
    }

    private void BuildUI()
    {
        // 标题
        var header = new Label("任务属性");
        header.style.fontSize = 16;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = Color.white;
        header.style.paddingLeft = 10;
        header.style.paddingTop = 8;
        header.style.paddingBottom = 8;
        header.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        Add(header);

        // 可滚动内容
        scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;
        scrollView.style.paddingLeft = 10;
        scrollView.style.paddingRight = 10;
        scrollView.style.paddingTop = 6;
        Add(scrollView);

        // ── 基础信息 ──
        AddSectionHeader("基础信息");

        questIdField = new TextField("任务 ID") { isDelayed = true };
        questIdField.RegisterValueChangedCallback(evt => { if (currentQuest != null) currentQuest.questId = evt.newValue; });
        scrollView.Add(questIdField);

        questNameField = new TextField("任务名称") { isDelayed = true };
        questNameField.RegisterValueChangedCallback(evt =>
        {
            if (currentQuest != null)
            {
                currentQuest.questName = evt.newValue;
                currentNode?.RefreshVisuals();
            }
        });
        scrollView.Add(questNameField);

        questTypeField = new EnumField("任务类型", QuestType.Side);
        questTypeField.RegisterValueChangedCallback(evt =>
        {
            if (currentQuest != null)
            {
                currentQuest.questType = (QuestType)evt.newValue;
                currentNode?.RefreshVisuals();
            }
        });
        scrollView.Add(questTypeField);

        // ── 触发配置 ──
        AddSectionHeader("触发配置");

        triggerField = new EnumField("触发方式", QuestTrigger.NpcTalk);
        triggerField.RegisterValueChangedCallback(evt =>
        {
            if (currentQuest != null) currentQuest.trigger = (QuestTrigger)evt.newValue;
        });
        scrollView.Add(triggerField);

        // NPC ID 下拉
        var npcChoices = LoadNpcChoices();
        var triggerNpcDropdown = new DropdownField("触发 NPC ID", npcChoices, 0);
        triggerNpcDropdown.RegisterValueChangedCallback(evt => { if (currentQuest != null) currentQuest.triggerNpcId = ParseTargetId(evt.newValue); });
        scrollView.Add(triggerNpcDropdown);
        // 缓存用于 ShowQuest 时同步值
        triggerNpcIdField = triggerNpcDropdown;

        autoAcceptField = new Toggle("自动接取");
        autoAcceptField.RegisterValueChangedCallback(evt => { if (currentQuest != null) currentQuest.autoAccept = evt.newValue; });
        scrollView.Add(autoAcceptField);

        // ── 描述 ──
        AddSectionHeader("描述");

        descriptionField = new TextField("任务描述") { isDelayed = true, multiline = true, style = { minHeight = 60 } };
        descriptionField.RegisterValueChangedCallback(evt => { if (currentQuest != null) currentQuest.description = evt.newValue; });
        scrollView.Add(descriptionField);

        // ── 阶段管理 ──
        AddSectionHeader("阶段列表");
        AddDeleteQuestButton();
        scrollView.Add(stagesListView = new ListView());

        addStageBtn = new Button(OnAddStage) { text = "+ 添加阶段" };
        addStageBtn.style.marginTop = 4;
        scrollView.Add(addStageBtn);

        // 阶段详情容器
        stageDetailContainer = new VisualElement();
        scrollView.Add(stageDetailContainer);

        // ── 最终奖励 ──
        AddSectionHeader("最终奖励");
        finalRewardContainer = new VisualElement();
        scrollView.Add(finalRewardContainer);
        // 在 ShowQuest 时填充 finalReward 字段
    }

    private void AddSectionHeader(string title)
    {
        var header = new Label(title);
        header.style.fontSize = 13;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = new Color(0.6f, 0.8f, 1.0f);
        header.style.marginTop = 12;
        header.style.marginBottom = 4;
        header.style.paddingBottom = 2;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
        scrollView.Add(header);
    }

    private void AddDeleteQuestButton()
    {
        deleteQuestBtn = new Button(OnDeleteQuest) { text = "🗑 删除此任务" };
        deleteQuestBtn.style.marginTop = 4;
        deleteQuestBtn.style.backgroundColor = new Color(0.5f, 0.15f, 0.15f);
        scrollView.Add(deleteQuestBtn);
    }

    public void ShowQuest(QuestData quest, QuestNode node)
    {
        currentQuest = quest;
        currentNode = node;

        questIdField.SetValueWithoutNotify(quest.questId);
        questNameField.SetValueWithoutNotify(quest.questName);
        questTypeField.SetValueWithoutNotify(quest.questType);
        triggerField.SetValueWithoutNotify(quest.trigger);
        if (triggerNpcIdField is DropdownField npcDropdown)
        {
            string npcId = quest.triggerNpcId ?? "";
            string bareNpcId = ParseTargetId(npcId);
            var match = npcDropdown.choices.FirstOrDefault(c => c.StartsWith(bareNpcId));
            if (match != null) npcDropdown.SetValueWithoutNotify(match);
            if (npcId != bareNpcId) quest.triggerNpcId = bareNpcId; // 迁移旧数据
        }
        autoAcceptField.SetValueWithoutNotify(quest.autoAccept);
        descriptionField.SetValueWithoutNotify(quest.description ?? "");

        RefreshStageList();
        RefreshFinalReward();
        style.display = DisplayStyle.Flex;
    }

    public void ClearSelection()
    {
        currentQuest = null;
        currentNode = null;
        style.display = DisplayStyle.None;
    }

    // ─── 阶段列表 ───

    private void RefreshStageList()
    {
        if (currentQuest?.stages == null) return;

        stagesListView.itemsSource = currentQuest.stages;
        stagesListView.makeItem = () =>
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 1;
            row.style.paddingBottom = 1;

            var upBtn = new Button { text = "▲" };
            upBtn.style.width = 22;
            upBtn.style.fontSize = 9;
            upBtn.style.paddingLeft = 2;
            upBtn.style.paddingRight = 2;
            row.Add(upBtn);

            var downBtn = new Button { text = "▼" };
            downBtn.style.width = 22;
            downBtn.style.fontSize = 9;
            downBtn.style.paddingLeft = 2;
            downBtn.style.paddingRight = 2;
            row.Add(downBtn);

            var label = new Label();
            label.style.flexGrow = 1;
            label.style.paddingLeft = 4;
            label.style.paddingTop = 2;
            label.style.paddingBottom = 2;
            row.Add(label);

            return row;
        };
        stagesListView.bindItem = (element, index) =>
        {
            var stage = currentQuest.stages[index];
            var children = element.Children().ToList();

            var label = (Label)children[2];
            label.text = $"{index + 1}. {stage.description}";
            label.style.color = new Color(0.85f, 0.85f, 0.85f);

            var upBtn = (Button)children[0];
            upBtn.SetEnabled(index > 0);
            upBtn.clickable = new Clickable(() =>
            {
                if (index > 0 && currentQuest != null)
                {
                    var temp = currentQuest.stages[index];
                    currentQuest.stages[index] = currentQuest.stages[index - 1];
                    currentQuest.stages[index - 1] = temp;
                    RefreshStageList();
                    currentNode?.RefreshVisuals();
                }
            });

            var downBtn = (Button)children[1];
            downBtn.SetEnabled(index < currentQuest.stages.Count - 1);
            downBtn.clickable = new Clickable(() =>
            {
                if (index < currentQuest.stages.Count - 1 && currentQuest != null)
                {
                    var temp = currentQuest.stages[index];
                    currentQuest.stages[index] = currentQuest.stages[index + 1];
                    currentQuest.stages[index + 1] = temp;
                    RefreshStageList();
                    currentNode?.RefreshVisuals();
                }
            });
        };
        stagesListView.selectionType = SelectionType.Single;
        stagesListView.onSelectionChange += OnStageSelected;
        stagesListView.Rebuild();
    }

    private void OnStageSelected(IEnumerable<object> selection)
    {
        stageDetailContainer.Clear();
        foreach (var item in selection)
        {
            if (item is QuestStage stage)
                ShowStageDetail(stage);
        }
    }

    private void ShowStageDetail(QuestStage stage)
    {
        stageDetailContainer.Clear();

        var stageIndex = currentQuest?.stages?.IndexOf(stage) ?? 0;

        // 标题行 + 删除按钮
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginTop = 8;
        headerRow.style.marginBottom = 4;

        var header = new Label($"阶段 {stageIndex + 1}");
        header.style.fontSize = 13;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = new Color(0.6f, 1.0f, 0.6f);
        header.style.flexGrow = 1;
        headerRow.Add(header);

        var deleteStageBtn = new Button(() =>
        {
            if (currentQuest?.stages != null)
            {
                currentQuest.stages.Remove(stage);
                RefreshStageList();
                stageDetailContainer.Clear();
                currentNode?.RefreshVisuals();
            }
        }) { text = "🗑" };
        deleteStageBtn.style.width = 30;
        deleteStageBtn.style.backgroundColor = new Color(0.5f, 0.15f, 0.15f);
        headerRow.Add(deleteStageBtn);
        stageDetailContainer.Add(headerRow);


        var descField = new TextField("阶段描述") { isDelayed = true, value = stage.description, multiline = true, style = { minHeight = 40 } };
        descField.RegisterValueChangedCallback(evt => { stage.description = evt.newValue; currentNode?.RefreshVisuals(); });
        stageDetailContainer.Add(descField);

        // 目标列表
        AddSectionHeaderSmall(stageDetailContainer, "目标列表");
        var objectivesList = new ListView();
        objectivesList.style.maxHeight = 200;

        if (stage.objectives == null)
            stage.objectives = new List<ObjectiveConfig>();

        var itemDataByType = LoadItemDataByType();
        var entityChoices = LoadEntityChoices();
        var npcChoices = LoadNpcChoices();

        objectivesList.itemsSource = stage.objectives;
        objectivesList.makeItem = () =>
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.paddingTop = 2;
            container.style.paddingBottom = 2;
            container.style.alignItems = Align.Center;

            var objType = new EnumField(ObjectiveType.Kill);
            objType.style.width = 70;
            container.Add(objType);

            // 类别下拉（Collect 专用）
            var categoryField = new DropdownField();
            categoryField.style.width = 65;
            categoryField.style.display = DisplayStyle.None;
            container.Add(categoryField);

            // 目标下拉
            var targetField = new DropdownField();
            targetField.style.flexGrow = 1;
            targetField.style.width = 100;
            container.Add(targetField);

            var countField = new IntegerField { value = 1 };
            countField.style.width = 45;
            container.Add(countField);

            var deleteBtn = new Button(() =>
            {
                if (stage.objectives != null)
                {
                    int idx = stage.objectives.IndexOf((ObjectiveConfig)container.userData);
                    if (idx >= 0) stage.objectives.RemoveAt(idx);
                    objectivesList.Rebuild();
                }
            }) { text = "×" };
            deleteBtn.style.width = 20;
            container.Add(deleteBtn);

            return container;
        };
        objectivesList.bindItem = (element, index) =>
        {
            if (index >= (stage.objectives?.Count ?? 0)) return;
            var obj = stage.objectives[index];
            element.userData = obj;

            var children = element.Children().ToList();
            var typeField = (EnumField)children[0];
            var categoryField = (DropdownField)children[1];
            var targetField = (DropdownField)children[2];
            var countField = (IntegerField)children[3];

            typeField.SetValueWithoutNotify(obj.type);
            countField.SetValueWithoutNotify(obj.requiredCount);

            // 类别选择
            var categoryNames = itemDataByType.Keys
                .Select(k => GetItemTypeName(k))
                .ToList();
            categoryField.choices = categoryNames;
            categoryField.index = 0;

            // 初始化目标下拉
            void RefreshTargetDropdown()
            {
                if (obj.type == ObjectiveType.Collect)
                {
                    categoryField.style.display = DisplayStyle.Flex;
                    var selectedType = obj.type == ObjectiveType.Collect && categoryField.index >= 0
                        ? itemDataByType.Keys.ElementAt(categoryField.index)
                        : ItemType.武器;
                    UpdateTargetChoices(targetField, itemDataByType[selectedType], entityChoices, npcChoices, obj.type, selectedType);
                }
                else
                {
                    categoryField.style.display = DisplayStyle.None;
                    UpdateTargetChoices(targetField, null, entityChoices, npcChoices, obj.type);
                }
                if (!string.IsNullOrEmpty(obj.targetId))
                {
                    // 兼容旧数据：targetId 可能是 "id - name"，回读时顺便迁移为纯 ID
                    string bareId = ParseTargetId(obj.targetId);
                    var match = targetField.choices.FirstOrDefault(c => c.StartsWith(bareId));
                    if (match != null)
                    {
                        targetField.SetValueWithoutNotify(match);
                        if (obj.targetId != bareId)
                            obj.targetId = bareId;  // 迁移旧数据
                    }
                }
            }

            RefreshTargetDropdown();

            typeField.RegisterValueChangedCallback(evt =>
            {
                obj.type = (ObjectiveType)evt.newValue;
                obj.targetId = "";
                RefreshTargetDropdown();
            });
            categoryField.RegisterValueChangedCallback(evt =>
            {
                RefreshTargetDropdown();
            });
            targetField.RegisterValueChangedCallback(evt =>
            {
                obj.targetId = ParseTargetId(evt.newValue);
            });
            countField.RegisterValueChangedCallback(evt =>
            {
                obj.requiredCount = evt.newValue;
            });
        };
        stageDetailContainer.Add(objectivesList);

        var addObjBtn = new Button(() =>
        {
            if (stage.objectives == null) stage.objectives = new List<ObjectiveConfig>();
            stage.objectives.Add(new ObjectiveConfig { type = ObjectiveType.Kill, targetId = "", requiredCount = 1 });
            objectivesList.Rebuild();
        }) { text = "+ 添加目标" };
        addObjBtn.style.marginTop = 2;
        stageDetailContainer.Add(addObjBtn);

        // 阶段奖励
        AddSectionHeaderSmall(stageDetailContainer, "阶段奖励");
        AddRewardFields(stageDetailContainer, stage.stageReward ?? new QuestReward(), r => stage.stageReward = r);
    }

    private void AddSectionHeaderSmall(VisualElement parent, string title)
    {
        var header = new Label(title);
        header.style.fontSize = 12;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = new Color(0.8f, 0.9f, 1.0f);
        header.style.marginTop = 8;
        header.style.marginBottom = 2;
        parent.Add(header);
    }

    private void AddRewardFields(VisualElement parent, QuestReward reward, System.Action<QuestReward> onChanged)
    {
        var expField = new IntegerField("经验") { value = reward.expAmount };
        expField.RegisterValueChangedCallback(evt => { reward.expAmount = evt.newValue; onChanged(reward); });
        parent.Add(expField);

        var spField = new IntegerField("技能点") { value = reward.skillPoints };
        spField.RegisterValueChangedCallback(evt => { reward.skillPoints = evt.newValue; onChanged(reward); });
        parent.Add(spField);

        var goldField = new IntegerField("金币") { value = reward.goldAmount };
        goldField.RegisterValueChangedCallback(evt => { reward.goldAmount = evt.newValue; onChanged(reward); });
        parent.Add(goldField);

        // 奖励物品列表
        AddSectionHeaderSmall(parent, "奖励物品");
        if (reward.items == null)
            reward.items = new List<RewardItem>();

        var itemChoices = LoadItemChoices();
        var itemList = new ListView();
        itemList.style.maxHeight = 150;
        itemList.itemsSource = reward.items;
        itemList.makeItem = () =>
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 1;
            row.style.paddingBottom = 1;

            var dropdown = new DropdownField();
            dropdown.style.flexGrow = 1;
            dropdown.style.width = 120;
            row.Add(dropdown);

            var amountField = new IntegerField { value = 1 };
            amountField.style.width = 50;
            row.Add(amountField);

            var deleteBtn = new Button { text = "×" };
            deleteBtn.style.width = 20;
            row.Add(deleteBtn);

            return row;
        };
        itemList.bindItem = (element, index) =>
        {
            if (index >= (reward.items?.Count ?? 0)) return;
            var ri = reward.items[index];
            element.userData = ri;

            var children = element.Children().ToList();
            var dropdown = (DropdownField)children[0];
            var amountField = (IntegerField)children[1];
            var deleteBtn = (Button)children[2];

            dropdown.choices = itemChoices;
            if (ri.itemData != null)
            {
                var match = itemChoices.FirstOrDefault(c => c.StartsWith(ri.itemData.itemId));
                if (match != null) dropdown.SetValueWithoutNotify(match);
            }
            amountField.SetValueWithoutNotify(ri.amount);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                string id = ParseTargetId(evt.newValue);
                ri.itemData = FindItemById(id);
                onChanged(reward);
            });
            amountField.RegisterValueChangedCallback(evt =>
            {
                ri.amount = Mathf.Max(1, evt.newValue);
                onChanged(reward);
            });
            deleteBtn.clickable = new Clickable(() =>
            {
                reward.items.RemoveAt(index);
                itemList.Rebuild();
                onChanged(reward);
            });
        };
        parent.Add(itemList);

        var addItemBtn = new Button(() =>
        {
            reward.items.Add(new RewardItem());
            itemList.Rebuild();
            onChanged(reward);
        }) { text = "+ 添加物品" };
        addItemBtn.style.marginTop = 2;
        parent.Add(addItemBtn);
    }

    private ItemDataSo FindItemById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var guids = AssetDatabase.FindAssets("t:ItemDataSo");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSo>(path);
            if (item != null && item.itemId == id) return item;
        }
        return null;
    }

    private List<string> LoadItemChoices()
    {
        var choices = new List<string> { "" };
        var guids = AssetDatabase.FindAssets("t:ItemDataSo");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSo>(path);
            if (item != null)
                choices.Add($"{item.itemId} - {item.itemName}");
        }
        choices.Sort();
        return choices;
    }

    // ─── 按钮操作 ───

    private void OnAddStage()
    {
        if (currentQuest == null) return;
        if (currentQuest.stages == null)
            currentQuest.stages = new List<QuestStage>();

        var newStage = new QuestStage
        {
            description = "新阶段",
            objectives = new List<ObjectiveConfig>(),
        };
        currentQuest.stages.Add(newStage);
        RefreshStageList();
        currentNode?.RefreshVisuals();
    }

    private void OnDeleteQuest()
    {
        if (currentQuest == null) return;
        if (!EditorUtility.DisplayDialog("删除任务",
                $"确定要删除「{currentQuest.questName}」吗？\n此操作不可撤销。", "删除", "取消"))
            return;

        var path = AssetDatabase.GetAssetPath(currentQuest);
        if (!string.IsNullOrEmpty(path))
        {
            // 从树中移除
            var node = currentNode;
            if (node != null && node.parent is global::UnityEditor.Experimental.GraphView.GraphView gv)
                gv.RemoveElement(node);

            // 删除 SO 文件
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            ClearSelection();
            Debug.Log($"[任务树] 已删除: {currentQuest.questName}");
        }
    }

    // ─── ID 下拉数据加载 ───

    private Dictionary<ItemType, List<string>> LoadItemDataByType()
    {
        var result = new Dictionary<ItemType, List<string>>();
        var guids = AssetDatabase.FindAssets("t:ItemDataSo");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSo>(path);
            if (item == null) continue;
            if (!result.ContainsKey(item.itemType))
                result[item.itemType] = new List<string>();
            result[item.itemType].Add($"{item.itemId} - {item.itemName}");
        }
        foreach (var kvp in result)
            kvp.Value.Sort();
        return result;
    }

    private List<string> LoadEntityChoices()
    {
        return LoadCsvByType(excludeType: "NPC");
    }

    private List<string> LoadNpcChoices()
    {
        // 优先从场景中的 NPCBehaviour 读取 npcId + npcName
        var result = new List<string> { "" };
        var seen = new HashSet<string>();
        var npcs = Resources.FindObjectsOfTypeAll<NPCBehaviour>();
        foreach (var npc in npcs)
        {
            if (npc == null || string.IsNullOrEmpty(npc.name) || seen.Contains(npc.name)) continue;
            seen.Add(npc.name);
            result.Add($"{npc.name} - {npc.npcName}");
        }
        // 补充 CSV 中的 NPC 条目（如果有不在场景中的 NPC）
        var csvNpcs = LoadCsvByType(includeType: "NPC");
        foreach (var entry in csvNpcs)
        {
            if (entry != "" && !result.Contains(entry))
                result.Add(entry);
        }
        return result;
    }

    private List<string> LoadCsvByType(string includeType = null, string excludeType = null)
    {
        var choices = new List<string> { "" };
        var csv = Resources.Load<TextAsset>("CSV/Entities");
        if (csv == null) return choices;

        var lines = csv.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var parts = line.Split(',');
            if (parts.Length < 2) continue;

            var id = parts[0].Trim().Trim('"');
            var name = parts[1].Trim().Trim('"');
            var type = parts.Length >= 3 ? parts[2].Trim().Trim('"') : "";

            if (string.IsNullOrEmpty(id)) continue;
            if (includeType != null && type != includeType) continue;
            if (excludeType != null && type == excludeType) continue;

            choices.Add($"{id} - {name}");
        }
        return choices;
    }

    private string GetItemTypeName(ItemType type) => type switch
    {
        ItemType.武器 => "武器",
        ItemType.头盔 => "头盔",
        ItemType.盔甲 => "盔甲",
        ItemType.靴子 => "靴子",
        ItemType.手套 => "手套",
        ItemType.饰品 => "饰品",
        ItemType.材料 => "材料",
        ItemType.消耗品 => "消耗品",
        _ => type.ToString(),
    };

    private string ParseTargetId(string dropdownValue)
    {
        if (string.IsNullOrEmpty(dropdownValue)) return "";
        int dash = dropdownValue.IndexOf(" - ");
        return dash >= 0 ? dropdownValue.Substring(0, dash) : dropdownValue;
    }

    private void UpdateTargetChoices(DropdownField dropdown, List<string> items,
        List<string> entities, List<string> npcs, ObjectiveType type, ItemType? category = null)
    {
        var choices = type switch
        {
            ObjectiveType.Collect when items != null => items,
            ObjectiveType.Kill => entities,
            ObjectiveType.TalkToNPC => npcs,
            _ => new List<string>(),
        };
        if (choices.Count == 0) choices.Add("");
        dropdown.choices = choices;
        dropdown.index = 0;
    }

    private void RefreshFinalReward()
    {
        finalRewardContainer.Clear();
        if (currentQuest == null) return;

        if (currentQuest.finalReward == null)
            currentQuest.finalReward = new QuestReward();

        var reward = currentQuest.finalReward;
        AddRewardFields(finalRewardContainer, reward, r => currentQuest.finalReward = r);
    }
}
