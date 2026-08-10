using System.Collections.Generic;
using UnityEngine;

// 分解系统（F10）— 将不需要的装备拆解为基础材料
// 产出数量 = 基础数量 × 稀有度倍率（普通1.0→精良1.5→稀有2.0→史诗2.5→传说3.0）
// 分解是制作的逆过程：材料产出有损耗（产出量低于制作所需）
public static class DismantleSystem
{
    private const float LossRatio = 0.5f; // 分解损耗：只返还制作所需材料的一半（分解=制作逆过程）

    private static CraftingRecipeDB cachedDB; // 配方数据库缓存（配方反推用）
    private static CraftingRecipeDB DB => cachedDB != null ? cachedDB : cachedDB = Resources.Load<CraftingRecipeDB>("Data/CraftingRecipeDB");

    // 计算单件装备的分解产出（不执行，供 UI 预览）
    // 纯配方表反推：查 CraftingRecipeDB 该装备的制作配方，产出配方材料的 50% × 稀有度倍率
    // 未在配方表中的装备不可分解（返回空）
    public static List<DismantleOutput> CalculateOutput(Inventory_Item item)
    {
        var result = new List<DismantleOutput>();
        if (item?.itemData == null)
            return result;

        // 稀有度倍率：实际稀有度优先，否则用物品基准稀有度
        float rarityMult = item.actualRarity.HasValue
            ? RarityCalculator.GetBaseMultiplier(item.actualRarity.Value)
            : RarityCalculator.GetBaseMultiplier(item.itemData.rarity);

        // 配方反推：该装备有制作配方则按配方材料产出（损耗一半）
        var recipe = FindRecipe(DB, item.itemData);
        if (recipe == null || recipe.materials == null)
            return result;

        foreach (var m in recipe.materials)
        {
            if (m?.material == null)
                continue;
            int count = Mathf.Max(1, Mathf.RoundToInt(m.count * LossRatio * rarityMult));
            result.Add(new DismantleOutput { material = m.material, count = count });
        }
        return result;
    }

    // 查找装备对应的制作配方（resultItem 匹配）
    private static CraftingRecipe FindRecipe(CraftingRecipeDB db, ItemDataSo itemData)
    {
        if (db == null || db.recipes == null)
            return null;
        foreach (var r in db.recipes)
        {
            if (r != null && r.resultItem == itemData)
                return r;
        }
        return null;
    }

    // 检查是否可分解（含背包容量，供 UI 按钮禁用判断）；null=可分解
    public static string GetDismantleFailReason(Inventory_Item item, PlayerInventorySystem invSys)
    {
        if (item == null || invSys == null)
            return "无效目标";

        var inv = invSys.GetInventory();
        if (inv == null)
            return "背包未就绪";

        var output = CalculateOutput(item);
        if (output.Count == 0)
            return "不可分解"; // 无配方

        // 背包能否容纳分解产出（材料可堆叠则不占新槽）
        if (!CanHoldOutput(inv, output))
            return "背包已满";

        return null;
    }

    // 检查是否可分解（含背包容量）
    public static bool CanDismantle(Inventory_Item item, PlayerInventorySystem invSys)
    {
        return GetDismantleFailReason(item, invSys) == null;
    }

    // 执行分解：产出材料入背包 + 移除被分解装备；成功返回 true
    public static bool TryDismantle(Inventory_Item item, PlayerInventorySystem invSys)
    {
        if (item == null || invSys == null)
            return false;

        var inv = invSys.GetInventory();
        if (inv == null)
            return false;

        var output = CalculateOutput(item);
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
