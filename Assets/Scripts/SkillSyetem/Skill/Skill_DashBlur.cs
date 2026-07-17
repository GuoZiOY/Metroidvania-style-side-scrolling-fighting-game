using UnityEngine;

public class Skill_DashBlur : Skill_Base
{
    private float baseImageEchoInterval;// 基础镜像回声间隔
    private float baseDashDuration;// 基础冲刺时间
    [SerializeField] private float imageEchoIntervalSubtract = 0.004f;// 每级减少的镜像回声间隔
    [SerializeField] private float dashDurationAddPerLevel = 0.01f;// 升级虚化类型时每级额外增加的冲刺时间

    protected override void Awake()
    {
        base.Awake();
    }

    protected virtual void Start()
    {
        baseImageEchoInterval = player.VFX.imageEchoInterval;
        baseDashDuration = player.dashDuration;
    }

    public override void SetSkillLevelData(SkillUpgradeType upgradeType, LevelData levelData, int level, bool updateUpgradeType = true)
    {
        base.SetSkillLevelData(upgradeType, levelData, level, updateUpgradeType);

        int dashBlurLevel = GetUpgradeTypeLevel(SkillUpgradeType.Dash_Blur);// Dash_Blur等级 = 升级类型等级（Dash_Blur）

        ApplyDashBlurBonuses(dashBlurLevel);

        Debug.Log($"{GetType().Name} 升级到 {level} 级，Dash_Blur等级: {dashBlurLevel}，冲刺时间: {player.dashDuration:F3}，镜像回声间隔: {player.VFX.imageEchoInterval:F3}");
    }

    private int GetUpgradeTypeLevel(SkillUpgradeType upgradeType)// 获取升级类型的等级
    {
        return upgradeTypeLevels.ContainsKey(upgradeType) ? upgradeTypeLevels[upgradeType] : 0;
    }

    private void ApplyDashBlurBonuses(int dashBlurLevel)// 应用冲刺虚化升级奖励
    {
        float extraDashDuration = dashBlurLevel * dashDurationAddPerLevel;// 每级额外增加的冲刺时间
        float imageEchoIntervalReduction = dashBlurLevel * imageEchoIntervalSubtract;// 每级减少的镜像回声间隔

        player.dashDuration = baseDashDuration + extraDashDuration;// 总冲刺时间 = 基础时间 + 每级额外增加时间
        player.VFX.imageEchoInterval = Mathf.Max(baseImageEchoInterval - imageEchoIntervalReduction, 0.04f);// 镜像回声间隔 = 基础间隔 - 每级减少间隔（不能小于0.04）
    }

    public override void RefundSkillUpgrade()// 重置技能升级
    {
        base.RefundSkillUpgrade();
        player.dashDuration = baseDashDuration;
        player.VFX.imageEchoInterval = baseImageEchoInterval;
        Debug.Log($"{GetType().Name} 重置技能，恢复基础值：冲刺时间 {player.dashDuration:F3}，镜像回声间隔 {player.VFX.imageEchoInterval:F3}");
    }

    public bool CanXuHua() => Unlocked(SkillUpgradeType.Dash_Blur);// 检查是否解锁冲刺虚化
}
