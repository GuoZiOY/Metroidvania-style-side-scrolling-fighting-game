using System.Collections.Generic;
using UnityEngine;

// 合成系统（F11）— N 件同底材同稀有度装备合成 1 件更高稀有度（3≤N≤9，概率成功）
// 成功：稀有度+1（上限传说）；失败：稀有度不变（词缀重roll）
// 词缀：保底继承输入中 tier 最高的 K 条（K=max(1,(N-1)/2)），其余按目标稀有度随机补齐
public static class CombineSystem
{
    public const int MinCount = 3;   // 最少合成件数
    public const int MaxCount = 9;   // 最多合成件数

    // 完全成功（换底材+稀有度+1）时，突破生成一条高于当前稀有度词缀的概率
    public const float BreakthroughChance = 0.3f;

    // 大成功概率（换更高底材，稀有度不变）：每件+10%，3件30% → 9件90%（独立于小成功，完全成功=两者相乘）
    public static float GetBigSuccessRate(int count)
    {
        return Mathf.Min(0.9f, count * 0.10f);
    }

    // 基础成功率（稀有度+1）：按合成指向（目标稀有度）递减，普通→精良最高50%，史诗→传说最低10%
    // 稀有度越高词缀越稀有、素材越难堆，基础越低
    public static float GetBaseSuccessRate(LootRarity targetRarity)
    {
        return targetRarity switch
        {
            LootRarity.精良 => 0.50f,
            LootRarity.稀有 => 0.35f,
            LootRarity.史诗 => 0.20f,
            LootRarity.传说 => 0.10f,
            _ => 0.50f
        };
    }

    // 统计素材词缀：词缀总数 + 品质分（品质分 = Σ(tier+1)，普通1 ~ 传说5）
    public static void GetMaterialAffixStats(List<Inventory_Item> items, out int affixCount, out int qualityScore)
    {
        affixCount = 0;
        qualityScore = 0;
        if (items == null)
            return;
        foreach (var item in items)
        {
            if (item?.affixes == null)
                continue;
            foreach (var affix in item.affixes)
            {
                if (affix == null)
                    continue;
                affixCount++;
                qualityScore += (int)affix.tier + 1;
            }
        }
    }

    // 实际成功率 = 基础（按目标稀有度）+ 素材词缀加成（每词缀+2%），上限90%
    public static float GetEffectiveSuccessRate(List<Inventory_Item> items, LootRarity targetRarity)
    {
        GetMaterialAffixStats(items, out int affixCount, out _);
        float baseRate = GetBaseSuccessRate(targetRarity);
        return Mathf.Min(0.9f, baseRate + GetAffixCountBonus(affixCount));
    }

    // 素材词缀加成：每个词缀 +2%（UI 提示展示用，与 GetEffectiveSuccessRate 一致）
    public static float GetAffixCountBonus(int affixCount)
    {
        return affixCount * 0.02f;
    }

    // 保底继承词缀条数：K = max(1, (N-1)/2)，3件1条 → 5件2条 → 7件3条 → 9件4条
    public static int GetGuaranteeCount(int count)
    {
        return Mathf.Max(1, (count - 1) / 2);
    }

    // 保底标准：每 +1 条保底所需品质分，按合成指向（目标稀有度）递增，阶段越高越难堆
    public static int GetQualityPerKeep(LootRarity targetRarity)
    {
        return targetRarity switch
        {
            LootRarity.精良 => 5,   // 普通→精良：普通词缀少，低标准易保底
            LootRarity.稀有 => 15,  // 精良→稀有
            LootRarity.史诗 => 30,  // 稀有→史诗
            LootRarity.传说 => 50,  // 史诗→传说：高稀有度难堆
            _ => 5
        };
    }

    // 突破标准：保底满后，每 +1 条突破（词缀位+1，突破当前稀有度上限）所需品质分
    public static int GetBreakthroughPerQuality(LootRarity targetRarity)
    {
        return targetRarity switch
        {
            LootRarity.精良 => 15,   // 普通→精良：容易突破（普通武器带多条词缀）
            LootRarity.稀有 => 40,
            LootRarity.史诗 => 80,
            LootRarity.传说 => 150,  // 史诗→传说：突破极难
            _ => 15
        };
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

        // 同大类（武器/装备[头甲靴手]/饰品）+ 同稀有度
        int category = GetCategory(first.itemData.itemType);
        for (int i = 1; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null || it.itemData == null || !it.IsEquipment)
            {
                failReason = "合成物必须是装备";
                return false;
            }
            if (GetCategory(it.itemData.itemType) != category)
            {
                failReason = "所有装备必须属于同一大类（武器/装备/饰品）";
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

    // 装备大类：武器 / 装备(头盔/盔甲/靴子/手套) / 饰品
    private static int GetCategory(ItemType type)
    {
        if (type == ItemType.武器) return 1;
        if (type == ItemType.头盔 || type == ItemType.盔甲 || type == ItemType.靴子 || type == ItemType.手套) return 2;
        if (type == ItemType.饰品) return 3;
        return 0;
    }

    // 从物品表加载同大类 + 目标稀有度的装备池（大成功换底材用）
    private static ItemDataSo[] GetEquipmentPool(ItemType type, LootRarity rarity)
    {
        int category = GetCategory(type);
        var list = new List<ItemDataSo>();
        foreach (var equip in Resources.LoadAll<EquipmentDataSo>("Data/ItemData"))
        {
            if (equip == null)
                continue;
            if (GetCategory(equip.itemType) != category)
                continue;
            if (equip.rarity != rarity)
                continue;
            list.Add(equip);
        }
        return list.ToArray();
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
        // 合成指向 = 目标稀有度（输入+1），基础成功率按目标稀有度递减（普通→精良最高50%，史诗→传说最低10%）
        LootRarity targetRarity = (LootRarity)Mathf.Min((int)inputRarity + 1, (int)LootRarity.传说);

        // 统计素材词缀（词缀总数→成功率；品质分→保底继承）
        GetMaterialAffixStats(items, out int totalAffixCount, out int qualityScore);

        // 掷骰：小成功(稀有度+1) 与 大成功(换更高一级底材) 独立判定，两者都中 = 完全成功
        // 大成功只换底材（底材池上移一个稀有度），稀有度不提升；小成功才稀有度+1
        bool rarityBoost = UnityEngine.Random.value < GetEffectiveSuccessRate(items, targetRarity);
        bool changeBase = UnityEngine.Random.value < GetBigSuccessRate(items.Count);

        LootRarity resultRarity = rarityBoost ? targetRarity : inputRarity;

        // 大成功：底材池 = 输入稀有度+1 的装备（如精良铜剑 → 优秀级铁剑），产物稀有度随小成功走
        ItemDataSo productData = first.itemData;
        bool bigSuccess = changeBase;
        if (changeBase)
        {
            LootRarity basePoolRarity = (LootRarity)Mathf.Min((int)inputRarity + 1, (int)LootRarity.传说);
            var pool = GetEquipmentPool(first.itemData.itemType, basePoolRarity);
            var filtered = new List<ItemDataSo>();
            foreach (var e in pool)
                if (e != first.itemData)
                    filtered.Add(e);
            if (filtered.Count > 0)
                productData = filtered[UnityEngine.Random.Range(0, filtered.Count)];
            else
                bigSuccess = false; // 无更高一级底材，回退同底材
        }

        // 完全成功（换底材+稀有度+1）时 Roll 突破；突破在 BuildResultAffixes 内替换产物最弱词缀（不增加词缀数）
        bool breakthrough = rarityBoost && bigSuccess && UnityEngine.Random.value < BreakthroughChance;
        GeneratedEquipmentAffix[] affixes = BuildResultAffixes(items, productData.itemType, resultRarity, qualityScore, breakthrough);

        // 移除 N 件输入
        foreach (var item in items)
            inv.RemoveItem(item);

        // 生成产物入背包
        var looted = new LootedItem(productData, resultRarity);
        var product = new Inventory_Item(looted, affixes);
        inv.AddItem(product);

        result.success = true;
        result.upgraded = rarityBoost;              // 稀有度是否+1（小成功）
        result.bigSuccess = bigSuccess;             // 是否换更高底材（大成功）
        result.completeSuccess = rarityBoost && bigSuccess; // 完全成功：既换底材又稀有度+1
        result.breakthrough = breakthrough;         // 完全成功时概率突破出高一级词缀
        result.resultRarity = resultRarity;
        result.product = product;
        return result;
    }

    // 构建产物词缀：词缀数固定 targetCount（无突破上限机制），先突破替换最弱，再保底替换最弱
    private static GeneratedEquipmentAffix[] BuildResultAffixes(List<Inventory_Item> inputs, ItemType itemType, LootRarity resultRarity, int qualityScore, bool breakTrigger)
    {
        int targetCount = GetMaxAffixCount(resultRarity);
        if (targetCount <= 0)
            return null;

        int count = inputs.Count;
        int qualityPerKeep = GetQualityPerKeep(resultRarity); // 保底标准（按目标稀有度阶段）

        // 1. 先按目标稀有度生成词缀（词缀数 = targetCount 固定，突破/保底只替换不增数）
        var result = new List<GeneratedEquipmentAffix>(
            EquipmentAffixGenerator.GenerateAffixes(itemType, resultRarity, null, targetCount));
        if (result.Count == 0)
            return result.ToArray();

        // 2. 突破（先）：完全成功触发时，生成一条更高稀有度词缀，替换产物最弱词缀
        if (breakTrigger)
        {
            LootRarity breakthroughRarity = (LootRarity)Mathf.Min((int)resultRarity + 1, (int)LootRarity.传说);
            GeneratedEquipmentAffix extraAffix = null;
            for (int attempt = 0; attempt < 5 && extraAffix == null; attempt++)
            {
                var extra = EquipmentAffixGenerator.GenerateAffixes(itemType, breakthroughRarity, null, 1);
                if (extra != null && extra.Length > 0 && (int)extra[0].tier >= (int)breakthroughRarity)
                    extraAffix = extra[0];
            }
            if (extraAffix != null)
            {
                // 找产物最弱词缀，突破词缀更强则替换（不增加词缀数）
                int weakestIdx = 0;
                for (int i = 1; i < result.Count; i++)
                    if (CompareAffix(result[i], result[weakestIdx]) < 0)
                        weakestIdx = i;
                if (CompareAffix(extraAffix, result[weakestIdx]) < 0)
                    result[weakestIdx] = extraAffix;
            }
        }

        // 3. 收集素材词缀，按强度降序（最强在前）
        var bestFromInputs = new List<GeneratedEquipmentAffix>();
        foreach (var item in inputs)
        {
            if (item?.affixes == null)
                continue;
            foreach (var affix in item.affixes)
            {
                if (affix != null)
                    bestFromInputs.Add(affix);
            }
        }
        bestFromInputs.Sort((a, b) => CompareAffix(a, b));
        if (bestFromInputs.Count == 0)
            return result.ToArray();

        // 4. 保底条数 = 数量保底 + 品质分/保底标准（阶段标准，无上限），不超过产物词缀位
        int keepBonus = qualityScore / qualityPerKeep;
        int K = Mathf.Min(GetGuaranteeCount(count) + keepBonus, targetCount);

        // 5. 保底（后）：产物按强度升序（最弱在前），用素材最强词缀替换产物最弱的 K 条，
        //    仅当素材词缀更强时替换——产物已达目标上限则不动
        result.Sort((a, b) => CompareAffix(b, a));
        int replaced = 0;
        for (int i = 0; i < result.Count && replaced < K && replaced < bestFromInputs.Count; i++)
        {
            if (CompareAffix(bestFromInputs[replaced], result[i]) < 0)
            {
                result[i] = bestFromInputs[replaced];
                replaced++;
            }
        }

        return result.ToArray();
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

    // 目标稀有度的最大词缀数（与装备掉落规则一致，合成产物词缀位上限）
    public static int GetMaxAffixCount(LootRarity rarity)
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
    public bool upgraded;           // 是否稀有度+1（小成功）
    public bool bigSuccess;         // 是否换更高一级底材（大成功，稀有度不提升）
    public bool completeSuccess;    // 完全成功：小成功+大成功，换底材且稀有度+1
    public bool breakthrough;       // 突破：完全成功时概率生成一条高于当前稀有度的词缀
    public string failReason;       // 失败原因（校验失败时）
    public LootRarity resultRarity; // 产物稀有度
    public Inventory_Item product;  // 产物（成功执行时非 null）
}
