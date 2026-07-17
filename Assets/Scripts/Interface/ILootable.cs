using System.Collections.Generic;
using UnityEngine;

public interface ILootable
{
    LootTable LootTable { get; } //获取掉落表
    LootPool LootPool { get; } //获取掉落池
    Vector3 DropPosition { get; } //获取掉落位置

    void OnDrop(); //掉落时调用
}
