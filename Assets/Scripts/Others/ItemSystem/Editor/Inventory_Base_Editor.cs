using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(Inventory_Base), true)]
public class Inventory_Base_Editor : Editor
{
    private SerializedProperty maxInventorySizeProp;
    private SerializedProperty itemDictionaryProp;

    private void OnEnable()
    {
        maxInventorySizeProp = serializedObject.FindProperty("maxInventorySize");
        itemDictionaryProp = serializedObject.FindProperty("itemDictionary");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Inventory_Base inventory = (Inventory_Base)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("背包系统字典存储可视化", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        //显示基础属性
        EditorGUILayout.PropertyField(maxInventorySizeProp);
        EditorGUILayout.Space();

        //显示字典统计信息
        EditorGUILayout.LabelField("字典统计信息", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox($"总槽位数: {inventory.maxInventorySize}\n已占用槽位: {inventory.GetItemCount()}\n空槽位: {inventory.maxInventorySize - inventory.GetItemCount()}", MessageType.Info);
        EditorGUILayout.Space();

        //显示字典内容
        EditorGUILayout.LabelField("字典内容 (槽位索引 -> 物品)", EditorStyles.boldLabel);
        
        if (inventory.itemDictionary != null && inventory.itemDictionary.Count > 0)
        {
            //创建滚动视图
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            
            foreach (var kvp in inventory.itemDictionary)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                //槽位索引
                EditorGUILayout.LabelField($"槽位 [{kvp.Key}]", GUILayout.Width(80));
                
                //物品信息
                if (kvp.Value != null && kvp.Value.itemData != null)
                {
                    EditorGUILayout.LabelField(kvp.Value.itemData.itemName, GUILayout.Width(150));
                    EditorGUILayout.LabelField($"类型: {kvp.Value.itemData.itemType}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"数量: {kvp.Value.currentStackSize}", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"ID: {kvp.Value.itemID}", GUILayout.Width(150));
                    
                    //显示物品图标
                    if (kvp.Value.itemData.itemIcon != null)
                    {
                        GUILayout.Box(kvp.Value.itemData.itemIcon.texture, GUILayout.Width(32), GUILayout.Height(32));
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("空槽位", EditorStyles.boldLabel);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("字典为空，没有物品", MessageType.Info);
        }

        EditorGUILayout.Space();

        //显示已占用槽位列表
        EditorGUILayout.LabelField("已占用槽位列表", EditorStyles.boldLabel);
        List<int> occupiedSlots = inventory.GetOccupiedSlots();
        if (occupiedSlots.Count > 0)
        {
            EditorGUILayout.HelpBox($"已占用槽位: {string.Join(", ", occupiedSlots)}", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("没有已占用的槽位", MessageType.Info);
        }

        EditorGUILayout.Space();

        //显示第一个可用槽位
        int firstAvailable = inventory.GetFirstAvailableSlot();
        EditorGUILayout.LabelField($"第一个可用槽位: {(firstAvailable >= 0 ? firstAvailable.ToString() : "无可用槽位")}", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        //测试按钮
        EditorGUILayout.LabelField("调试工具", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("打印字典内容到控制台"))
        {
            DebugDictionaryContent(inventory);
        }
        if (GUILayout.Button("刷新字典状态"))
        {
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private Vector2 scrollPosition;

    private void DebugDictionaryContent(Inventory_Base inventory)
    {
        Debug.Log("=== 背包字典内容 ===");
        Debug.Log($"总槽位数: {inventory.maxInventorySize}");
        Debug.Log($"已占用槽位: {inventory.GetItemCount()}");
        
        foreach (var kvp in inventory.itemDictionary)
        {
            if (kvp.Value != null && kvp.Value.itemData != null)
            {
                Debug.Log($"槽位 [{kvp.Key}]: {kvp.Value.itemData.itemName} (数量: {kvp.Value.currentStackSize}, ID: {kvp.Value.itemID})");
            }
            else
            {
                Debug.Log($"槽位 [{kvp.Key}]: 空槽位");
            }
        }
        Debug.Log("=== 字典内容结束 ===");
    }
}