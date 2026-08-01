using System.Collections.Generic;
using UnityEngine;

// 分解系统（F10）— 将不需要的装备拆解为基础材料
// 产出数量 = 基础数量 × 稀有度倍率（普通1.0→精良1.5→稀有2.0→史诗2.5→传说3.0）
// 分解是制作的逆过程：材料产出有损耗（产出量低于制作所需）
public static class DismantleSystem
{
    // 计算单件装备的分解产出（不执行，供 UI 预览）
    public static List<DismantleOutput> CalculateOutput(Inventory_Item item, DismantleTable table)
    {
        var result = new List<DismantleOutput>();
        if (item?.itemData == null || table == null)
            return result;

        var entry = FindEntry(table, item.itemData.itemType);
        if (entry == null || entry.materials == null)
            return result;

        // 稀有度倍率：实际稀有度优先，否则用物品基准稀有度
        float rarityMult = item.actualRarity.HasValue
            ? RarityCalculator.GetBaseMultiplier(item.actualRarity.Value)
            : RarityCalculator.GetBaseMultiplier(item.itemData.rarity);

        foreach (var m in entry.materials)
        {
            if (m?.material == null)
                continue;

            int count = Mathf.Max(1, Mathf.RoundToInt(m.baseCount * rarityMult));
            result.Add(new DismantleOutput { material = m.material, count = count });
        }
        return result;
    }

    // 执行分解：产出材料入背包 + 移除被分解装备；成功返回 true
    public static bool TryDismantle(Inventory_Item item, PlayerInventorySystem invSys, DismantleTable table)
    {
        if (item == null || invSys == null || table == null)
            return false;

        var inv = invSys.GetInventory();
        if (inv == null)
            return false;

        var output = CalculateOutput(item, table);
        if (output.Count == 0)
            return false;

        // 背包容量检查：材料可堆叠到已有堆，不足部分需要新开槽
        if (!CanHoldOutput(inv, output))
            return false;

        // 若物品已装备，先自动卸下到背包
        if (!IsInBackpack(inv, item))
        {
            if (!invSys.TryUnequipItem(item))
                return false;
        }

        // 产出材料入背包（AddItem 内部自动堆叠/开新槽）
        foreach (var o in output)
        {
            for (int i = 0; i < o.count; i++)
                inv.AddItem(new Inventory_Item(o.material));
        }

        // 移除被分解装备
        inv.RemoveItem(item);
        return true;
    }

    // 查找装备类型对应的分解配置
    private static DismantleEntry FindEntry(DismantleTable table, ItemType type)
    {
        if (table.entries == null)
            return null;
        foreach (var e in table.entries)
        {
            if (e != null && e.itemType == type)
                return e;
        }
        return null;
    }

    // 背包容量检查：已有同种可堆叠材料则不占新槽
    private static bool CanHoldOutput(Inventory_Player inv, List<DismantleOutput> output)
    {
        int newSlotsNeeded = 0;
        foreach (var o in output)
        {
            if (!HasStackRoom(inv, o.material))
                newSlotsNeeded++;
        }
        return inv.GetItemCount() + newSlotsNeeded <= inv.maxInventorySize;
    }

    // 背包中是否存在可继续堆叠的同种材料
    private static bool HasStackRoom(Inventory_Player inv, ItemDataSo material)
    {
        foreach (var kvp in inv.itemDictionary)
        {
            var item = kvp.Value;
            if (item?.itemData == material && item.CanAddStack())
                return true;
        }
        return false;
    }

    // 判断物品是否在背包中（而非装备槽）
    private static bool IsInBackpack(Inventory_Player inv, Inventory_Item item)
    {
        return inv.GetItemSlot(item) != -1;
    }
}

// 单次分解产出项（供 UI 预览与执行共用）
public class DismantleOutput
{
    public ItemDataSo material;   // 产出材料
    public int count;             // 产出数量
}
