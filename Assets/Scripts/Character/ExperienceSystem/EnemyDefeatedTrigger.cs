using UnityEngine;

public class EnemyDefeatedTrigger : ExperienceTrigger
{
    [Header("敌人设置")]
    [SerializeField] private Enemy targetEnemy; // 目标敌人
    [SerializeField] private int enemyLevel = 1; // 敌人等级

    [Header("经验设置")]
    [SerializeField] private bool useLevelBasedExp = true; // 是否使用基于等级的经验计算
    [SerializeField] private int baseExpPerLevel = 20; // 每级基础经验值

    [Header("特殊条件")]
    [SerializeField] private bool requireCounterKill = false; // 是否需要反击击杀
    [SerializeField] private bool requireComboKill = false; // 是否需要连击击杀
    [SerializeField] private int minComboCount = 3; // 最小连击数

    private bool hasCounterKilled = false; // 是否反击击杀
    private int currentCombo = 0; // 当前连击数

    protected override void InitializeTrigger() // 初始化触发器
    {
        base.InitializeTrigger();

        if (targetEnemy == null)
        {
            targetEnemy = GetComponent<Enemy>();
        }

        if (targetEnemy != null)
        {
            targetEnemy.OnEntityDead += OnEnemyDead;
        }

        sourceType = ExperienceSourceType.EnemyDefeated;
    }

    private void OnEnemyDead() // 敌人死亡回调
    {
        if (targetEnemy == null)
            return;

        enemyLevel = targetEnemy.GetEnemyLevel();
        int finalExp = CalculateExperience();

        EnemyExpData expData = new EnemyExpData
        {
            enemyName = targetEnemy.name,
            enemyLevel = enemyLevel,
            position = targetEnemy.transform.position,
            wasCounterKill = hasCounterKilled,
            comboCount = currentCombo
        };

        TriggerExperience(expData, targetEnemy.transform.position);
    }

    protected override bool CheckSpecialCondition() // 检查特殊条件
    {
        if (!hasSpecialCondition)
            return true;

        if (requireCounterKill && !hasCounterKilled)
            return false;

        if (requireComboKill && currentCombo < minComboCount)
            return false;

        return true;
    }

    private int CalculateExperience() // 计算经验值
    {
        int exp = baseExperience;

        if (useLevelBasedExp)
        {
            exp = baseExpPerLevel * enemyLevel;
        }

        return exp;
    }

    public void OnCounterKill() // 记录反击击杀
    {
        hasCounterKilled = true;
    }

    public void OnComboHit(int comboCount) // 记录连击数
    {
        currentCombo = comboCount;
    }

    public void SetEnemyLevel(int level) // 设置敌人等级
    {
        enemyLevel = level;
    }

    public void SetTargetEnemy(Enemy enemy) // 设置目标敌人
    {
        if (targetEnemy != null)
        {
            targetEnemy.OnEntityDead -= OnEnemyDead;
        }

        targetEnemy = enemy;

        if (targetEnemy != null)
        {
            targetEnemy.OnEntityDead += OnEnemyDead;
        }
    }

    protected override void OnDestroy() // 清理
    {
        if (targetEnemy != null)
        {
            targetEnemy.OnEntityDead -= OnEnemyDead;
        }

        base.OnDestroy();
    }

    protected override void OnExperienceTriggered(ExperienceEvent expEvent) // 经验触发后的回调
    {
        base.OnExperienceTriggered(expEvent);

        if (expEvent.SourceData is EnemyExpData expData)
        {
            Debug.Log($"击败敌人: {expData.enemyName}, 等级: {expData.enemyLevel}, 获得经验: {expEvent.ExperienceAmount}");
        }
    }
}
