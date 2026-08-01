using UnityEngine;

// 数值词缀基类 — 为纯属性修改类词缀提供 OnApplied/OnRemoved 的通用实现
// 子类只需覆写 ApplyStats/RemoveStats 和属性标识方法
// 复杂行为词缀（光环/召唤/AoE）应直接实现 IEnemyAffix，不使用此类
public abstract class StatAffixBase : MonoBehaviour, IEnemyAffix
{
    // ─── 子类必须覆写的抽象成员 ───
    public abstract string AffixId { get; }
    public abstract string DisplayName { get; }
    public abstract AffixTier Tier { get; }
    public abstract float GetLootBonus();
    public abstract string GetTooltipText();

    // ─── 子类覆写以下方法以定义具体的属性修改逻辑 ───
    protected abstract void ApplyStats(Enemy enemy);   // 添加 Modifier（用 AffixId 作 source key）
    protected abstract void RemoveStats(Enemy enemy);  // 移除 Modifier（按 AffixId 精准匹配）

    // ─── 运行时状态 ───
    protected Enemy enemy;
    protected bool isApplied; // 防止重复移除

    // 生成时由 AffixSpawner 调用
    public virtual void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        isApplied = true;
        ApplyStats(enemy);
    }

    // 死亡/Destroy 时由 Enemy.RemoveAllAffixes 调用
    public virtual void OnRemoved(Enemy enemy)
    {
        if (!isApplied)
            return;

        RemoveStats(enemy);
        isApplied = false;
        this.enemy = null;
    }

    // 默认无逐帧行为（纯数值词缀不需要 OnBattleUpdate）
    public virtual void OnBattleUpdate(Enemy enemy) { }

    // 安全网：如果 GameObject 被直接销毁而未调用 OnRemoved
    protected void OnDestroy()
    {
        if (isApplied && enemy != null)
            RemoveStats(enemy);
    }
}
