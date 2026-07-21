using System.Collections.Generic;
using UnityEngine;

// 物品运行时查找表。建立 itemId → ItemDataSo 的映射，供存档读档时重建物品。
// 查找优先级：Inspector 手动赋值 > Resources/ItemData 目录扫描。
public static class ItemLookup
{
    private static Dictionary<string, ItemDataSo> _cache;
    private static bool _initialized;

    // 初始化（在 SaveManager 或游戏启动时调用一次）
    public static void Initialize()
    {
        if (_initialized) return;
        _cache = new Dictionary<string, ItemDataSo>();

        // 从 Resources/ItemData 加载所有 ItemDataSo
        var allItems = Resources.LoadAll<ItemDataSo>("Data/ItemData");
        foreach (var item in allItems)
        {
            if (!string.IsNullOrEmpty(item.itemId) && !_cache.ContainsKey(item.itemId))
            {
                _cache[item.itemId] = item;
            }
        }

        _initialized = true;
        Debug.Log($"[ItemLookup] 已加载 {_cache.Count} 个物品");
    }

    // 按 itemId 查找
    public static ItemDataSo Find(string itemId)
    {
        if (!_initialized) Initialize();
        if (string.IsNullOrEmpty(itemId)) return null;
        _cache.TryGetValue(itemId, out var result);
        if (result == null)
            Debug.LogWarning($"[ItemLookup] 未找到物品: {itemId}");
        return result;
    }

    // 手动注册（Inspector 赋值模式）
    public static void Register(ItemDataSo item)
    {
        if (_cache == null) _cache = new Dictionary<string, ItemDataSo>();
        if (!string.IsNullOrEmpty(item.itemId))
            _cache[item.itemId] = item;
    }

    public static bool IsReady => _initialized;
    public static int Count => _cache?.Count ?? 0;
}
