using System.Collections.Generic;
using UnityEngine;

// 全局世界状态键值存储。
// 任何系统都可读写，用于任务前置条件检查、完成时影响世界、条件奖励等。
public static class WorldState
{
    private static readonly Dictionary<string, bool> flags = new();

    public static bool Get(string key)
    {
        return flags.TryGetValue(key, out bool val) && val;
    }

    public static void Set(string key, bool value)
    {
        flags[key] = value;
    }

    public static void Remove(string key)
    {
        flags.Remove(key);
    }

    public static bool Has(string key)
    {
        return flags.ContainsKey(key);
    }

    // ─── 存档接口 ───

    public static List<string> GetSaveData()
    {
        var list = new List<string>();
        foreach (var kvp in flags)
        {
            if (kvp.Value)
                list.Add(kvp.Key);
        }
        return list;
    }

    public static void LoadFromSave(List<string> savedFlags)
    {
        flags.Clear();
        if (savedFlags != null)
        {
            foreach (var key in savedFlags)
                flags[key] = true;
        }
    }
}
