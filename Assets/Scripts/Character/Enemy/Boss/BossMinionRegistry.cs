using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Boss 迷你王登记处——生成登记/死亡注销/批量销毁；Victory/Reset 时 Clear()
// 注：用基类 Enemy 而非 Boss_SlimeMinion（迷你王是 Enemy 子类，Task 8 才创建，解耦保证本文件可独立编译）
public class BossMinionRegistry : MonoBehaviour
{
    private readonly List<Enemy> minions = new(); // 登记的迷你王引用（Enemy 基类）

    public int Count => minions.Count(m => m != null); // 场上存活迷你王数（过滤已销毁未注销条目）

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
