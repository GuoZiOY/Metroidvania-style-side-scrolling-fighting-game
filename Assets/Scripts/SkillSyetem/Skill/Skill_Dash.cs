
using Unity.VisualScripting;
using UnityEngine;

public class Skill_Dash : Skill_Base
{

    private float baseDashSpeed;// 基础冲刺速度
    [SerializeField] private float dashSpeedAddPerLevel = 0.2f;// 升级冲刺类型时每级额外增加的冲刺速度

    protected override void Awake()
    {
        base.Awake();
    }

    protected virtual void Start()
    {
        baseDashSpeed = player.dashSpeed;
    }

    public override void SetSkillLevelData(SkillUpgradeType upgradeType, LevelData levelData, int level, bool updateUpgradeType = true)
    {
        base.SetSkillLevelData(upgradeType, levelData, level, updateUpgradeType);

        int dashLevel = GetUpgradeTypeLevel(SkillUpgradeType.FlashDash);// FlashDash等级 = 升级类型等级（FlashDash）

        ApplyDashBonuses(dashLevel);

        Debug.Log($"{GetType().Name} 升级到 {level} 级，Dash等级: {dashLevel}，冲刺速度: {player.dashSpeed:F2}");
    }

    private int GetUpgradeTypeLevel(SkillUpgradeType upgradeType)// 获取升级类型的等级
    {
        return upgradeTypeLevels.ContainsKey(upgradeType) ? upgradeTypeLevels[upgradeType] : 0;
    }

    private void ApplyDashBonuses(int dashLevel)// 应用冲刺升级奖励
    {
        float extraDashSpeed = dashLevel * dashSpeedAddPerLevel;// 每级额外增加的冲刺速度

        player.dashSpeed = baseDashSpeed + extraDashSpeed;// 总冲刺速度 = 基础速度 + 每级额外增加速度
    }

    public override void RefundSkillUpgrade()// 重置技能升级
    {
        base.RefundSkillUpgrade();
        player.dashSpeed = baseDashSpeed;
        Debug.Log($"{GetType().Name} 重置技能，恢复基础值：速度 {player.dashSpeed:F2}");
    }

    public void OnStartEffect()
    {
        if (Unlocked(SkillUpgradeType.Dash_CloneOnStart) || Unlocked(SkillUpgradeType.Dash_CloneOnStartAndArrival))
            CreateClone();

        if (Unlocked(SkillUpgradeType.Dash_ShardOnStart) || Unlocked(SkillUpgradeType.Dash_ShardOnStartAndArrival))
            CreateShard();
    }

    public void OnEndEffect()
    {
        if (Unlocked(SkillUpgradeType.Dash_CloneOnStartAndArrival))
            CreateClone();


        if (Unlocked(SkillUpgradeType.Dash_ShardOnStartAndArrival))
            CreateShard();
    }

    private void CreateShard()
    {
        skillManager.shard.CreateRawShard();
    }

    private void CreateClone()
    {
        skillManager.timeEcho.CreateTimeEcho();
    }

}
