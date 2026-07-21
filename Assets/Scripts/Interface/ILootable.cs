using System.Collections.Generic;
using UnityEngine;

public interface ILootable
{
    LootTable[] LootTables { get; } //掉落表列表（每个独立掉落）
    Vector3 DropPosition { get; } //获取掉落位置

    void OnDrop(); //掉落时调用
}
