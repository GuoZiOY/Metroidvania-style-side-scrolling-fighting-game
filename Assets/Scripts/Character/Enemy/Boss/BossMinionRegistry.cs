using System.Collections.Generic;
using UnityEngine;

// Boss 迷你王登记处——生成登记/死亡注销/批量销毁；Victory/场景卸载时 Clear()
// 注：用基类 Enemy 而非 Boss_SlimeMinion（迷你王是 Enemy 子类，Task 8 才创建，解耦保证本文件可独立编译）
public class BossMinionRegistry : MonoBehaviour
{
    private readonly List<Enemy> minions = new();

    public int Count => minions.Count; // 场上存活迷你王数（召唤上限判定用）

    // Boss 召唤时登记
    public void Register(Enemy minion)
    {
        if (minion != null && minions.Contains(minion) == false)
            minions.Add(minion);
    }

    // 迷你王死亡时注销
    public void Unregister(Enemy minion)
    {
        minions.Remove(minion);
    }

    // 批量销毁全部（Victory/Reset 调用，防卡死个体占满召唤上限）
    public void Clear()
    {
        foreach (var m in minions)
            if (m != null)
                Destroy(m.gameObject);
        minions.Clear();
    }
}
