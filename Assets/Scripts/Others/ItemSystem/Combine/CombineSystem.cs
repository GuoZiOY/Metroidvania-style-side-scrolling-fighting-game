using System.Collections.Generic;
using UnityEngine;

// 合成系统（F11）— N 件同底材同稀有度装备合成 1 件更高稀有度（3≤N≤9，概率成功）
// 成功：稀有度+1（上限传说）；失败：稀有度不变（词缀重roll）
// 词缀：保底继承输入中 tier 最高的 K 条（K=max(1,(N-1)/2)），其余按目标稀有度随机补齐
public static class CombineSystem
{
    public const int MinCount = 3;   // 最少合成件数
    public const int MaxCount = 9;   // 最多合成件数

    // 成功率：3件40%，每多1件+10%，9件100%
    public static float GetSuccessRate(int count)
    {
        return 0.4f + (count - MinCount) * 0.1f;
    }

    // 保底继承词缀条数：K = max(1, (N-1)/2)，3件1条 → 5件2条 → 7件3条 → 9件4条
    public static int GetGuaranteeCount(int count)
    {
        return Mathf.Max(1, (count - 1) / 2);
    }

    // 校验输入是否可合成（同底材 + 同稀有度 + 数量范围）
    public static bool CanCombine(List<Inventory_Item> items, out string failReason)
    {
        failReason = null;
        if (items == null || items.Count < MinCount || items.Count > MaxCount)
        {
            failReason = $"需要 {MinCount}-{MaxCount} 件装备（当前 {items?.Count ?? 0} 件）";
            return false;
        }

        Inventory_Item first = items[0];
        if (first == null || first.itemData == null || !first.IsEquipment)
        {
            failReason = "合成物必须是装备";
            return false;
        }
        if (!first.actualRarity.HasValue)
        {
            failReason = "装备缺少稀有度信息";
            return false;
        }

        // 同底材 + 同稀有度
        for (int i = 1; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null || it.itemData == null || !it.IsEquipment)
            {
                failReason = "合成物必须是装备";
                return false;
            }
            if (it.itemData != first.itemData)
            {
                failReason = "所有装备必须是同一种类（同底材）";
                return false;
            }
            if (it.actualRarity != first.actualRarity)
            {
                failReason = "所有装备稀有度必须相同";
                return false;
            }
        }
        return true;
    }

    // 执行合成：消耗 N 件 → 产出 1 件（成功/失败仅影响产物稀有度是否+1）
    public static CombineResult TryCombine(List<Inventory_Item> items, PlayerInventorySystem invSys)
    {
        var result = new CombineResult();

        if (!CanCombine(items, out string reason))
        {
            result.failReason = reason;
            return result;
        }

        var inv = invSys.GetInventory();
        if (inv == null)
        {
            result.failReason = "背包系统未就绪";
            return result;
        }
        if (!inv.CanAddItem())
        {
            result.failReason = "背包已满";
            return result;
        }

        Inventory_Item first = items[0];
        LootRarity inputRarity = first.actualRarity.Value;

        // 掷骰：成功 → 稀有度+1（上限传说），失败 → 稀有度不变
        bool upgraded = UnityEngine.Random.value < GetSuccessRate(items.Count);
        LootRarity resultRarity = upgraded
            ? (LootRarity)Mathf.Min((int)inputRarity + 1, (int)LootRarity.传说)
            : inputRarity;

        // 构建产物词缀：保底继承 + 随机补齐
        GeneratedEquipmentAffix[] affixes = BuildResultAffixes(items, first.itemData.itemType, resultRarity);

        // 移除 N 件输入
        foreach (var item in items)
            inv.RemoveItem(item);

        // 生成产物入背包
        var looted = new LootedItem(first.itemData, resultRarity);
        var product = new Inventory_Item(looted, affixes);
        inv.AddItem(product);

        result.success = true;
        result.upgraded = upgraded;
        result.resultRarity = resultRarity;
        result.product = product;
        return result;
    }

    // 构建产物词缀：保底继承输入中 tier 最高的 K 条（前后缀分取）+ 随机补齐到目标稀有度最大词缀数
    private static GeneratedEquipmentAffix[] BuildResultAffixes(List<Inventory_Item> inputs, ItemType itemType, LootRarity resultRarity)
    {
        int targetCount = GetMaxAffixCount(resultRarity);
        int K = GetGuaranteeCount(inputs.Count);
        int totalKeep = Mathf.Min(K, targetCount); // 保底条数不超过产物词缀上限

        // 收集输入所有词缀，分前缀/后缀
        var prefixes = new List<GeneratedEquipmentAffix>();
        var suffixes = new List<GeneratedEquipmentAffix>();
        foreach (var item in inputs)
        {
            if (item?.affixes == null)
                continue;
            foreach (var affix in item.affixes)
            {
                if (affix == null)
                    continue;
                if (affix.isPrefix)
                    prefixes.Add(affix);
                else
                    suffixes.Add(affix);
            }
        }

        // 各池按 tier 高 → 数值和大 降序排序（保底取最优）
        prefixes.Sort((a, b) => CompareAffix(a, b));
        suffixes.Sort((a, b) => CompareAffix(a, b));

        // 保底：前缀取 ceil(totalKeep/2)，后缀取剩余
        int prefixKeep = Mathf.CeilToInt(totalKeep / 2f);
        int suffixKeep = totalKeep - prefixKeep;

        var guaranteed = new List<GeneratedEquipmentAffix>();
        for (int i = 0; i < prefixKeep && i < prefixes.Count; i++)
            guaranteed.Add(prefixes[i]);
        for (int i = 0; i < suffixKeep && i < suffixes.Count; i++)
            guaranteed.Add(suffixes[i]);

        // 随机补齐缺口
        return EquipmentAffixGenerator.GenerateAffixes(itemType, resultRarity, guaranteed.ToArray(), targetCount);
    }

    // 词缀比较：tier 高优先；同 tier 比各段数值绝对值之和（强词缀优先继承）
    private static int CompareAffix(GeneratedEquipmentAffix a, GeneratedEquipmentAffix b)
    {
        if (a == null || b == null)
            return 0;
        if (a.tier != b.tier)
            return ((int)b.tier).CompareTo((int)a.tier);
        return GetAffixPower(b).CompareTo(GetAffixPower(a));
    }

    // 词缀强度 = 各段数值绝对值之和（衡量"多强"的代理指标）
    private static float GetAffixPower(GeneratedEquipmentAffix affix)
    {
        float sum = 0;
        if (affix?.modifiers != null)
        {
            foreach (var m in affix.modifiers)
            {
                if (m != null)
                    sum += Mathf.Abs(m.value);
            }
        }
        return sum;
    }

    // 目标稀有度的最大词缀数（与装备掉落规则一致）
    private static int GetMaxAffixCount(LootRarity rarity)
    {
        return rarity switch
        {
            LootRarity.普通 => 1,
            LootRarity.精良 => 2,
            LootRarity.稀有 => 3,
            LootRarity.史诗 => 4,
            LootRarity.传说 => 4,
            _ => 0
        };
    }
}

// 合成结果（含产物与是否升级信息）
public class CombineResult
{
    public bool success;            // 是否成功执行（校验通过）
    public bool upgraded;           // 是否稀有度+1（成功掷骰）
    public string failReason;       // 失败原因（校验失败时）
    public LootRarity resultRarity; // 产物稀有度
    public Inventory_Item product;  // 产物（成功执行时非 null）
}
