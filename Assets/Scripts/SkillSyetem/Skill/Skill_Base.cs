
using UnityEngine;
using System.Collections.Generic;

public class Skill_Base : MonoBehaviour
{
    public Player_SkillManager skillManager {  get; private set; }
    public DamageScaleData damageScaleData {  get; private set; }
    public Player player {  get; private set; }



    [Header("技能基础配置")]
    [SerializeField] public SkillType skillType;// 技能类型
    [SerializeField] public SkillUpgradeType upgradeType;// 技能升级类型
    [SerializeField] public int currentLevel;// 当前等级0
    [SerializeField] protected float cooldown;// 冷却时间
    [SerializeField] protected int totalLevel = 0;//总等级数
    private float lastTimeUsed;// 上次使用时间
    protected Dictionary<SkillUpgradeType, int> upgradeTypeLevels = new Dictionary<SkillUpgradeType, int>();// 记录每个升级类型的等级

    public float Cooldown => cooldown; // 公共属性访问冷却时间


    protected virtual void Awake()
    {
        skillManager = GetComponentInParent<Player_SkillManager>();
        player = GetComponentInParent<Player>();
        damageScaleData = new DamageScaleData();
        lastTimeUsed = lastTimeUsed - cooldown;
        // 初始化上次使用时间，使其减去冷却时间，确保技能在游戏开始时即可使用
    }

    public virtual void SetSkillLevelData(SkillUpgradeType upgradeType, LevelData levelData, int level, bool updateUpgradeType = true)//更新技能数据
    {
        totalLevel++;
        
        // 对于被动技能，即使updateUpgradeType为false，也需要设置upgradeType
        // 因为被动技能需要知道自己的SkillUpgradeType才能正确工作
        if (upgradeType != SkillUpgradeType.None)
        {
            if (updateUpgradeType)
            {
                this.upgradeType = upgradeType; // 绑定升阶标识（全局）
            }
            else if (this.upgradeType == SkillUpgradeType.None)
            {
                // 被动技能首次激活时设置upgradeType
                this.upgradeType = upgradeType;
            }
        }
        
        upgradeTypeLevels[upgradeType] = level;// 记录该升级类型的等级
        this.currentLevel = level;// 记录当前等级
        this.cooldown = levelData.cooldown;// 赋值本级冷却
        this.damageScaleData = levelData.damageScaleData; // 赋值本级伤害
        ResetCooldown();
    }
    public virtual void RefundSkillUpgrade()//重置技能数据为0
    {
        totalLevel = 0;
        currentLevel = 0;
        upgradeType = SkillUpgradeType.None;
        cooldown = 0;
        damageScaleData = new DamageScaleData();
        totalLevel = 0;
        upgradeTypeLevels.Clear();
    }

    public virtual void TryUseSkill()
    {

    }

    public bool CanUseSkill()
    {
        if(upgradeType == SkillUpgradeType.None)
            return false;

        if (IsOnCooldown())
        {
            player.VFX.CreatePopUpText($"{skillType} CD:{GetRemainingCooldown():F1}");
            Debug.Log($"{GetType().Name} 技能冷却中，剩余时间: {GetRemainingCooldown():F1}秒");
            return false;
        }
        return true;
    }

    protected bool Unlocked(SkillUpgradeType upgradeToCheck) => upgradeType == upgradeToCheck;//检测是否解锁

    protected bool IsOnCooldown() => Time.time < lastTimeUsed + cooldown;// 若当前时间小于（上次使用时间+冷却时间），则表示仍在冷却
    public void StartSkillCooldown() => lastTimeUsed = Time .time; // 设置技能进入冷却状态（将上次使用时间更新为当前时间）
    
    // 存档：获取所有升级类型的等级
    public Dictionary<SkillUpgradeType, int> GetAllUpgradeTypeLevels() => new Dictionary<SkillUpgradeType, int>(upgradeTypeLevels);

    public float GetRemainingCooldown()//剩余的冷却时间
    {
        if (IsOnCooldown() == false) return 0f;
        return (lastTimeUsed + cooldown) - Time.time;
    }

    public void ReduceCooldown(float reductionAmount)// 根据冷却缩减值减少冷却时间（用于减少剩余冷却时间）
    {
        if (reductionAmount <= 0) return;

        lastTimeUsed = Mathf.Max(0, lastTimeUsed - reductionAmount);
        Debug.Log($"{GetType().Name} 冷却时间减少 {reductionAmount:F1}秒");
    }
    public void ForceStartCooldown() => lastTimeUsed = Time.time;//强行进入技能冷却

    public void ResetCooldown() => lastTimeUsed = Time.time - cooldown;//强行结束技能冷却


}
