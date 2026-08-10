using System.Collections.Generic;
using UnityEngine;

// 制作系统（F9）— 静态逻辑类：检查材料/金币 → 扣除 → 生成产物 → 入背包
// 配方数据来自 CraftingRecipeDB（策划配置）；材料来源为掉落/分解产出
public static class CraftingSystem
{
    // 检查玩家是否满足制作条件（只检查不扣除）
    public static bool CanCraft(CraftingRecipe recipe, PlayerInventorySystem invSys)
    {
        return GetCraftFailReason(recipe, invSys) == null;
    }

    // 获取不可制作原因（null=可制作）：背包已满/金币不足/材料不足，供 UI 按钮文案区分
    public static string GetCraftFailReason(CraftingRecipe recipe, PlayerInventorySystem invSys)
    {
        if (recipe == null || recipe.resultItem == null || invSys == null)
            return "配方无效";

        var inv = invSys.GetInventory();
        if (inv == null)
            return "背包未就绪";

        // 背包能否放入产物（可堆叠产物在背包满但有堆空间时也允许制作）
        if (!inv.CanAddItem(recipe.resultItem))
            return "背包已满";

        // 金币不足
        if (invSys.GetCurrency() < recipe.goldCost)
            return "金币不足";

        // 材料不足
        if (!HasEnoughMaterials(recipe, inv))
            return "材料不足";

        return null;
    }

    // 计算制作产物的稀有度：max(配方最低稀有度, 材料平均稀有度)
    // 材料按数量加权平均——高品质材料可提升产物稀有度下限
    public static LootRarity CalculateResultRarity(CraftingRecipe recipe, Inventory_Player inv)
    {
        int minRarity = (int)recipe.minResultRarity;
        int totalRarity = 0;
        int totalCount = 0;

        if (recipe.materials != null)
        {
            foreach (var m in recipe.materials)
            {
                if (m?.material == null)
                    continue;
                totalRarity += (int)m.material.rarity * m.count;
                totalCount += m.count;
            }
        }

        int avg = totalCount > 0 ? Mathf.RoundToInt((float)totalRarity / totalCount) : 0;
        return (LootRarity)Mathf.Max(minRarity, avg);
    }

    // 执行制作：扣除金币+材料 → 生成产物 → 入背包；成功返回 true
    public static bool TryCraft(CraftingRecipe recipe, PlayerInventorySystem invSys)
    {
        if (!CanCraft(recipe, invSys))
            return false;

        var inv = invSys.GetInventory();

        // 1. 扣除金币
        invSys.SpendCurrency(recipe.goldCost);

        // 2. 扣除材料（按堆叠扣减）
        if (!ConsumeMaterials(recipe, inv))
        {
            invSys.AddCurrency(recipe.goldCost); // 兜底：材料扣除失败回滚金币
            return false;
        }

        // 3. 生成产物（装备带稀有度+词缀，非装备普通构造）
        Inventory_Item product = CreateProduct(recipe, inv);
        if (product == null)
        {
            invSys.AddCurrency(recipe.goldCost);
            return false;
        }

        // 4. 入背包
        inv.AddItem(product);
        return true;
    }

    // 背包中某材料的总数量（含堆叠）
    public static int GetMaterialCount(Inventory_Player inv, ItemDataSo material)
    {
        int total = 0;
        foreach (var kvp in inv.itemDictionary)
        {
            var item = kvp.Value;
            if (item?.itemData == material)
                total += item.currentStackSize;
        }
        return total;
    }

    // 检查配方所需材料是否足够
    private static bool HasEnoughMaterials(CraftingRecipe recipe, Inventory_Player inv)
    {
        if (recipe.materials == null)
            return true;

        foreach (var m in recipe.materials)
        {
            if (m?.material == null)
                continue;
            if (GetMaterialCount(inv, m.material) < m.count)
                return false;
        }
        return true;
    }

    // 扣除配方所需材料（遍历堆叠逐个扣减，扣到0则移除该堆）
    private static bool ConsumeMaterials(CraftingRecipe recipe, Inventory_Player inv)
    {
        if (recipe.materials == null)
            return true;

        foreach (var m in recipe.materials)
        {
            if (m?.material == null)
                continue;

            int need = m.count;
            // 复制字典避免遍历时修改
            foreach (var kvp in new List<KeyValuePair<int, Inventory_Item>>(inv.itemDictionary))
            {
                if (need <= 0)
                    break;

                var item = kvp.Value;
                if (item?.itemData != m.material)
                    continue;

                int take = Mathf.Min(need, item.currentStackSize);
                need -= take;
                if (item.ReduceStack(take) <= 0)
                    inv.RemoveItemAtSlot(kvp.Key); // 该堆清空，移除
                else
                    inv.NotifyUpdate();            // 堆未清空，刷新 UI
            }

            if (need > 0)
                return false; // 材料不足（理论 CanCraft 已拦截）
        }
        return true;
    }

    // 生成制作产物
    private static Inventory_Item CreateProduct(CraftingRecipe recipe, Inventory_Player inv)
    {
        LootRarity resultRarity = CalculateResultRarity(recipe, inv);

        // 装备产物：带稀有度 + 词缀（走 LootedItem 构造）
        if (recipe.resultItem is EquipmentDataSo)
        {
            var looted = new LootedItem(recipe.resultItem, resultRarity);
            return new Inventory_Item(looted);
        }

        // 非装备产物（材料/消耗品）：普通构造，可堆叠
        return new Inventory_Item(recipe.resultItem);
    }
}
