using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(EquipmentSystem), true)]
public class EquipmentSystem_Editor : Editor
{
    private SerializedProperty slotConfigsProp;
    private SerializedProperty playerStatsProp;
    private SerializedProperty equippedItemsProp;

    private Vector2 scrollPosition;
    private Dictionary<ItemType, bool> foldoutStates = new Dictionary<ItemType, bool>();

    private void OnEnable()
    {
        slotConfigsProp = serializedObject.FindProperty("slotConfigs");
        playerStatsProp = serializedObject.FindProperty("playerStats");
        equippedItemsProp = serializedObject.FindProperty("equippedItems");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EquipmentSystem equipmentSystem = (EquipmentSystem)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("装备系统字典存储可视化", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        //显示基础属性
        EditorGUILayout.PropertyField(slotConfigsProp);
        EditorGUILayout.PropertyField(playerStatsProp);
        EditorGUILayout.Space();

        //显示字典统计信息
        EditorGUILayout.LabelField("字典统计信息", EditorStyles.boldLabel);
        
        var slotDict = equipmentSystem.GetSlotDictionary();
        var equipDict = equipmentSystem.GetEquipmentDictionary();
        
        if (slotDict != null && slotDict.Count > 0)
        {
            int totalSlots = 0;
            int occupiedSlots = 0;
            int emptySlots = 0;

            foreach (var kvp in slotDict)
            {
                totalSlots += kvp.Value.Count;
                if (equipDict != null && equipDict.ContainsKey(kvp.Key))
                {
                    foreach (var item in equipDict[kvp.Key])
                    {
                        if (item != null)
                        {
                            occupiedSlots++;
                        }
                        else
                        {
                            emptySlots++;
                        }
                    }
                }
            }

            EditorGUILayout.HelpBox($"支持的装备类型: {slotDict.Count}\n总槽位数: {totalSlots}\n已占用槽位: {occupiedSlots}\n空槽位: {emptySlots}", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("字典未初始化或为空", MessageType.Warning);
        }

        EditorGUILayout.Space();

        //显示字典内容
        EditorGUILayout.LabelField("字典内容 (按装备类型分组)", EditorStyles.boldLabel);
        
        if (slotDict != null && slotDict.Count > 0)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
            
            foreach (var kvp in slotDict)
            {
                ItemType itemType = kvp.Key;
                List<Inventory_EquipmentSlot> slots = kvp.Value;
                
                //初始化折叠状态
                if (!foldoutStates.ContainsKey(itemType))
                {
                    foldoutStates[itemType] = false;
                }
                
                //显示装备类型折叠面板
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                //类型标题栏
                EditorGUILayout.BeginHorizontal();
                foldoutStates[itemType] = EditorGUILayout.Foldout(foldoutStates[itemType], $"装备类型: {itemType}", true);
                
                //类型统计信息
                int totalTypeSlots = slots.Count;
                int occupiedTypeSlots = 0;
                int emptyTypeSlots = 0;
                
                if (equipDict != null && equipDict.ContainsKey(itemType))
                {
                    foreach (var item in equipDict[itemType])
                    {
                        if (item != null)
                        {
                            occupiedTypeSlots++;
                        }
                        else
                        {
                            emptyTypeSlots++;
                        }
                    }
                }
                
                EditorGUILayout.LabelField($"槽位: {occupiedTypeSlots}/{totalTypeSlots}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"空位: {emptyTypeSlots}", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
                
                //显示槽位详情
                if (foldoutStates[itemType])
                {
                    EditorGUILayout.Space();
                    EditorGUI.indentLevel++;
                    
                    for (int i = 0; i < slots.Count; i++)
                    {
                        Inventory_EquipmentSlot slot = slots[i];
                        Inventory_Item item = equipDict != null && equipDict.ContainsKey(itemType) ? equipDict[itemType][i] : null;
                        
                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        
                        //槽位索引
                        EditorGUILayout.LabelField($"槽位 [{i}]", GUILayout.Width(60));
                        
                        //物品信息
                        if (item != null && item.itemData != null)
                        {
                            EditorGUILayout.LabelField(item.itemData.itemName, GUILayout.Width(120));
                            EditorGUILayout.LabelField($"类型: {item.itemData.itemType}", GUILayout.Width(80));
                            EditorGUILayout.LabelField($"ID: {item.itemID}", GUILayout.Width(120));
                            
                            //显示物品图标
                            if (item.itemData.itemIcon != null)
                            {
                                GUILayout.Box(item.itemData.itemIcon.texture, GUILayout.Width(32), GUILayout.Height(32));
                            }
                            
                            //显示装备属性信息
                            if (item.Modifiers != null && item.Modifiers.Length > 0)
                            {
                                EditorGUILayout.LabelField($"属性: {item.Modifiers.Length}个", GUILayout.Width(80));
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField("空槽位", EditorStyles.boldLabel, GUILayout.Width(120));
                            EditorGUILayout.LabelField($"类型: {slot.slotType}", GUILayout.Width(80));
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("字典为空，没有装备数据", MessageType.Info);
        }

        EditorGUILayout.Space();

        //显示已装备物品列表
        EditorGUILayout.LabelField("所有已装备物品", EditorStyles.boldLabel);
        List<Inventory_Item> allEquipped = equipmentSystem.GetEquippedItems();
        if (allEquipped != null && allEquipped.Count > 0)
        {
            foreach (var item in allEquipped)
            {
                if (item != null && item.itemData != null)
                {
                    EditorGUILayout.HelpBox($"{item.itemData.itemName} ({item.itemData.itemType})", MessageType.None);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("没有已装备的物品", MessageType.Info);
        }

        EditorGUILayout.Space();

        //显示支持的装备类型
        EditorGUILayout.LabelField("支持的装备类型", EditorStyles.boldLabel);
        List<ItemType> supportedTypes = equipmentSystem.GetSupportedItemTypes();
        if (supportedTypes.Count > 0)
        {
            EditorGUILayout.HelpBox($"支持的类型: {string.Join(", ", supportedTypes)}", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("没有配置支持的装备类型", MessageType.Warning);
        }

        EditorGUILayout.Space();

        //测试按钮
        EditorGUILayout.LabelField("调试工具", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("打印字典内容到控制台"))
        {
            DebugDictionaryContent(equipmentSystem);
        }
        if (GUILayout.Button("刷新字典状态"))
        {
            serializedObject.Update();
        }
        if (GUILayout.Button("测试槽位查询"))
        {
            TestSlotQueries(equipmentSystem);
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void DebugDictionaryContent(EquipmentSystem equipmentSystem)
    {
        Debug.Log("=== 装备系统字典内容 ===");
        
        var slotDict = equipmentSystem.GetSlotDictionary();
        var equipDict = equipmentSystem.GetEquipmentDictionary();
        
        Debug.Log($"支持的装备类型数量: {slotDict.Count}");
        
        foreach (var kvp in slotDict)
        {
            ItemType itemType = kvp.Key;
            List<Inventory_EquipmentSlot> slots = kvp.Value;
            
            Debug.Log($"装备类型: {itemType}, 槽位数量: {slots.Count}");
            
            if (equipDict.ContainsKey(itemType))
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    Inventory_Item item = equipDict[itemType][i];
                    if (item != null && item.itemData != null)
                    {
                        Debug.Log($"  槽位 [{i}]: {item.itemData.itemName} (ID: {item.itemID})");
                    }
                    else
                    {
                        Debug.Log($"  槽位 [{i}]: 空槽位");
                    }
                }
            }
        }
        
        Debug.Log("=== 字典内容结束 ===");
    }

    private void TestSlotQueries(EquipmentSystem equipmentSystem)
    {
        Debug.Log("=== 测试槽位查询 ===");
        
        var supportedTypes = equipmentSystem.GetSupportedItemTypes();
        foreach (var type in supportedTypes)
        {
            //通过其他方法计算槽位信息
            List<Inventory_EquipmentSlot> slots = equipmentSystem.GetSlotsByType(type);
            List<Inventory_Item> items = equipmentSystem.GetEquippedItemsByType(type);
            
            int totalSlots = slots.Count;
            int occupiedSlots = items.Count;
            int emptySlots = totalSlots - occupiedSlots;
            bool hasEmpty = equipmentSystem.HasEmptySlot(type);
            
            Debug.Log($"类型 {type}: 总槽位{totalSlots}, 已占用{occupiedSlots}, 空槽位{emptySlots}, 有空槽位{hasEmpty}");
            
            Debug.Log($"  已装备物品数量: {items.Count}");
        }
        
        Debug.Log("=== 测试结束 ===");
    }
}