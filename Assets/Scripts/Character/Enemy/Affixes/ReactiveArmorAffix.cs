using System.Collections.Generic;
using UnityEngine;

// 反应护甲词缀 — 每次受击获得一层护甲 buff，每层独立 4 秒计时
// 最多 5 层共 +90 护甲
public class ReactiveArmorAffix : MonoBehaviour, IEnemyAffix
{
    [SerializeField] private float armorPerStack = 18f; // 每层护甲值
    [SerializeField] private int maxStacks = 5;         // 最大层数
    [SerializeField] private float stackDuration = 4f;  // 每层持续时间（秒）

    public string AffixId => "affix_reactive_armor";
    public string DisplayName => "反应护甲";
    public AffixTier Tier => AffixTier.Rare;

    private Enemy enemy;
    private List<float> stackTimers = new(); // 每层的剩余时间，索引对应层号

    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        enemy.OnEnemyTookDamage += OnTookDamage; // 订阅受击事件
    }

    public void OnRemoved(Enemy enemy)
    {
        if (enemy != null)
            enemy.OnEnemyTookDamage -= OnTookDamage;
        ClearAllStacks();
        this.enemy = null;
    }

    // 受击时叠加一层（忽略 ≤1 的小额伤害如光环/DoT）
    private void OnTookDamage(float damage)
    {
        if (damage <= 1)
            return;

        if (stackTimers.Count >= maxStacks)
            return; // 已达上限

        stackTimers.Add(stackDuration);
        // 用唯一 source key（AffixId + 层索引）确保多层可独立移除
        enemy.stats?.defense?.armor?.AddModifier(armorPerStack, $"{AffixId}_{stackTimers.Count}");
    }

    // 每帧递减各层计时器，到期则移除
    public void OnBattleUpdate(Enemy enemy)
    {
        for (int i = stackTimers.Count - 1; i >= 0; i--)
        {
            stackTimers[i] -= Time.deltaTime;
            if (stackTimers[i] <= 0)
            {
                enemy.stats?.defense?.armor?.RemoveModifier($"{AffixId}_{i + 1}");
                stackTimers.RemoveAt(i);
            }
        }
    }

    private void ClearAllStacks()
    {
        for (int i = 0; i < stackTimers.Count; i++)
            enemy?.stats?.defense?.armor?.RemoveModifier($"{AffixId}_{i + 1}");
        stackTimers.Clear();
    }

    public float GetLootBonus() => 0.10f;
    public string GetTooltipText() => $"反应护甲: 受击+{armorPerStack}护甲(最多{maxStacks}层{armorPerStack * maxStacks})，每层{stackDuration}秒";
}
