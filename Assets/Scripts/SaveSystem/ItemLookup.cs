using System.Collections.Generic;
using UnityEngine;

// 物品运行时查找表。建立 itemId → ItemDataSo 的映射，供存档读档时重建物品。
// 查找优先级：Inspector 手动赋值 > Resources/ItemData 目录扫描。
public static class ItemLookup
{
    private static Dictionary<string, ItemDataSo> cache;
    private static bool initialized;

    // 初始化（在 SaveManager 或游戏启动时调用一次）
    public static void Initialize()
    {
        if (initialized) return;
        cache = new Dictionary<string, ItemDataSo>();

        // 从 Resources/ItemData 加载所有 ItemDataSo
        var allItems = Resources.LoadAll<ItemDataSo>("Data/ItemData");
        foreach (var item in allItems)
        {
            if (!string.IsNullOrEmpty(item.itemId) && !cache.ContainsKey(item.itemId))
            {
                cache[item.itemId] = item;
            }
        }

        initialized = true;
        Debug.Log($"[ItemLookup] 已加载 {cache.Count} 个物品");
    }

    // 按 itemId 查找
    public static ItemDataSo Find(string itemId)
    {
        if (!initialized) Initialize();
        if (string.IsNullOrEmpty(itemId)) return null;
        cache.TryGetValue(itemId, out var result);
        if (result == null)
            Debug.LogWarning($"[ItemLookup] 未找到物品: {itemId}");
        return result;
    }

    // 手动注册（Inspector 赋值模式）
    public static void Register(ItemDataSo item)
    {
        if (cache == null) cache = new Dictionary<string, ItemDataSo>();
        if (!string.IsNullOrEmpty(item.itemId))
            cache[item.itemId] = item;
    }

    public static bool IsReady => initialized;
    public static int Count => cache?.Count ?? 0;
}
