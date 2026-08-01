// 精英词缀接口 — 所有词缀组件必须实现此契约
// 词缀通过 OnApplied → OnBattleUpdate → OnRemoved 生命周期注入 Enemy 实例
// 行为类词缀（AoE/召唤/光环）直接实现此接口，数值类词缀继承 StatAffixBase
public interface IEnemyAffix
{
    string AffixId { get; }         // 唯一标识（如 "affix_fire_aura"），与 AffixDatabase 中对应
    string DisplayName { get; }     // UI 显示名，显示在精英血条上方
    AffixTier Tier { get; }         // 词缀等级，影响掉落稀有度加成和选取权重

    void OnApplied(Enemy enemy);    // 生成时调用：注入 Stats Modifier、注册 VFX、启动协程
    void OnRemoved(Enemy enemy);    // 死亡/Destroy 时调用：还原 Stats、停止协程、清理 VFX
    void OnBattleUpdate(Enemy enemy); // 每帧 BattleState 调用：光环判定、AoE、召唤检测等
    float GetLootBonus();           // 掉落稀有度加成（Common=0.05, Rare=0.10, Legendary=0.20）
    string GetTooltipText();        // 玩家看到的词缀说明文本
}
