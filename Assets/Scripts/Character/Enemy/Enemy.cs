using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType
{
    Normal,//普通
    Elite,//精英
    Boss//Boss
}

public class Enemy : Entity, ICounterable
{

    public Enemy_IdleState idleState;
    public Enemy_MoveState moveState;
    public Enemy_AttackState attackState;
    public Enemy_BattleState battleState;
    public Enemy_DeadState deadState;
    public Enemy_StunnedState stunnedState;


    [Header("敌人信息")]
    [SerializeField] public string enemyName = "Enemy"; // 敌人名称
    [SerializeField] public string uniqueID; // 存档唯一标识
    [SerializeField] public EnemyType enemyType = EnemyType.Normal; // 敌人类型
    [Header("等级系统")]
    [SerializeField] private EnemyLevelSystem levelSystem; // 敌人等级系统
    [SerializeField] private int enemyLevel = 1; // 敌人等级

    [Header("敌人类型系统")]
    [SerializeField] private NormalEnemyTypeSystem normalTypeSystem; // 普通敌人类型系统
    [SerializeField] private EliteEnemyTypeSystem eliteTypeSystem; // 精英敌人类型系统
    [SerializeField] private BossEnemyTypeSystem bossTypeSystem; // Boss敌人类型系统
    private EnemyTypeSystem activeTypeSystem; // 当前激活的类型系统

    [Header("精英词缀 (V2)")]
    public List<IEnemyAffix> ActiveAffixes { get; private set; } = new(); // 当前激活的词缀列表（由 AffixSpawner 注入）

    // 词缀系统伤害事件 — 行为类词缀通过订阅这些事件响应战斗
    // 由 Entity_Combat（造成伤害时）和 Entity_Health（受到伤害时）调用下面的公开方法触发
    public event System.Action<float> OnEnemyDealtDamage; // 敌人对玩家造成伤害时触发，参数=伤害总量（物理+元素）
    public event System.Action<float> OnEnemyTookDamage;   // 敌人受到伤害时触发，参数=伤害总量

    // 供 Entity_Combat 调用：通知词缀敌人造成了伤害
    public void ReportDealtDamage(float totalDamage)
    {
        OnEnemyDealtDamage?.Invoke(totalDamage);
    }

    // 供 Entity_Health 调用：通知词缀敌人受到了伤害
    public void ReportTookDamage(float totalDamage)
    {
        OnEnemyTookDamage?.Invoke(totalDamage);
    }


    [Header("玩家检测")]
    [SerializeField] private LayerMask whatIsPlayer;//
    [SerializeField] private Transform playerCheck;//
    [SerializeField] public float checkPlayer_Distance;//检测玩家的距离
    public Transform player {  get; private set; }


    [Header("战斗设置")]
    public float battleMoveSpeed = 3;//战斗时移动速度
    public float attackDistance = 2;//攻击距离
    public float battleTimeDuration = 5;//战斗持续时间
    public float minRetreatDistance = 1;//应该后退时的最小距离
    public Vector2 reteatVelocity;//二维的退避速度

    [Header("眩晕状态详细设置")]
    public float stunnedDuration = 1;
    public Vector2 stunnedVelocity = new Vector2(7,7);
    [SerializeField]protected bool canBeStunned;//是否可以被眩晕
    protected bool isInCounterTime;//是否处于可反击时间

    [Header("战利品配置")]
    [SerializeField] public LootDropper lootDropper;//战利品掉落器



    [Header("移动模式")]
    public float moveSpeed = 1.4f;
    [Range(0,2)]
    public float moveAnimSpeedMultiplier = 1;//角色移动时动画速度倍率

    // 智能随机待机时间（不暴露参数）：1.5~4.5s，偶尔长待机（5%概率 4~5.5s）增加不可预测性
    public float GetRandomIdleTime()
    {
        if (Random.value < 0.05f)
            return Random.Range(4f, 5.5f);
        return Random.Range(1.5f, 4f);
    }

    // 智能随机巡逻时间（不暴露参数）：3~7s，偶尔长巡逻（5%概率 6~8s）
    public float GetRandomMoveTime()
    {
        if (Random.value < 0.05f)
            return Random.Range(6f, 8f);
        return Random.Range(3f, 6f);
    }

    public float activeSlowMultiplier { get; private set; } = 1;//当前有效的减速倍率

    public float GetMoveSpeed()=> moveSpeed * activeSlowMultiplier;//获取移动速度
    public float GetBattleMoveSpeed()=> battleMoveSpeed * activeSlowMultiplier;//获取战斗移动速度


    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override IEnumerator SlowDownEntityCo(float duration, float slowMultiplier)
    {

        activeSlowMultiplier = 1 - slowMultiplier;//计算减速后的速度倍率

        anim.speed = anim.speed * activeSlowMultiplier;//设置动画速度

        yield return new WaitForSeconds(duration);//等待指定时间 
        StopSlowDown();//重置减速倍率并恢复移动速度
    }

    public override void StopSlowDown()//重置减速倍率并恢复移动速度
    {   
        activeSlowMultiplier = 1;//重置减速倍率
        anim.speed = 1;//重置动画播放速度
        base.StopSlowDown();
    }

    public void EnableCounterTime(bool enable)=> isInCounterTime = enable;//开启/关闭可反击时间

    // ─── ICounterable 实现（提到基类，所有敌人通用可被反击）───
    [SerializeField] private bool canBeChased = true; // 反击成功后是否可被追击

    public bool IsInCounterTime => isInCounterTime; // 是否处于可反击时间
    public bool CanBeChased => canBeChased; // 反击后是否可追击

    // 反击命中：击退 + 眩晕（不可眩晕的敌人不被反击打断，如Boss/精英）
    public void HandleCounter(float knockbackMultiplier = 1f)
    {
        if (canBeStunned == false)
            return;

        Vector2 counterKnockback = new Vector2(stunnedVelocity.x * knockbackMultiplier, stunnedVelocity.y * knockbackMultiplier);
        rb.linearVelocity = new Vector2(counterKnockback.x * -DirctionToPlayer(), counterKnockback.y);
        stateMachine.ChangeState(stunnedState);
    }

    public int DirctionToPlayer()//返回指向玩家的方向
    {
        if (player == null)
            return 0;
        return player.position.x > transform.position.x ? 1 : -1;
    }

    public override void EntityDead()
    {
        base.EntityDead();

        // 通知任务系统敌人被击杀
        if (!string.IsNullOrEmpty(uniqueID))
            QuestEvents.ReportEnemyKilled(uniqueID);

        stateMachine.ChangeState(deadState);
    }

    public virtual void DestroyEntity()
    {
        RemoveAllAffixes();
        Destroy(gameObject,2);
    }

    // ─── 精英词缀管理 (V2) ───

    public void AddAffix(IEnemyAffix affix) // 添加词缀
    {
        if (affix == null || ActiveAffixes.Contains(affix))
            return;
        ActiveAffixes.Add(affix);
    }

    public void RemoveAffix(IEnemyAffix affix) // 移除单个词缀
    {
        if (affix == null || !ActiveAffixes.Contains(affix))
            return;
        affix.OnRemoved(this);
        ActiveAffixes.Remove(affix);
    }

    public void RemoveAllAffixes() // 移除全部词缀
    {
        foreach (var affix in ActiveAffixes)
        {
            if (affix != null)
                affix.OnRemoved(this);
        }
        ActiveAffixes.Clear();
    }

    // 每帧 BattleState 调用，驱动所有词缀的逐帧行为
    public void UpdateAffixes()
    {
        foreach (var affix in ActiveAffixes)
        {
            if (affix != null)
                affix.OnBattleUpdate(this);
        }
    }
    public void TryEnterBattleState(Transform player)//尝试进入战斗状态，通常在受到攻击时调用（Enemy_Health被攻击时）
    {
        if (stateMachine.currentState == battleState || stateMachine.currentState == attackState)
            return;
        this.player = player;
        stateMachine.ChangeState(battleState);
    }

    public Transform GetPlayerReference()//获取玩家的transform引用
    {
        if (player == null)
            player = PlayerDetected().transform;
        return player;
    }


    public void InitializeEnemy(EnemyType type, int level) // 统一初始化敌人（类型 + 等级）
    {
        // 0. 先重置属性到默认值，避免类型和等级加成累积
        if (stats != null && stats.defaultStatSetup != null)
            stats.ApplyDefaultStatSetup();

        // 1. 设置类型
        enemyType = type;

        // 2. 应用类型加成
        ApplyTypeBonus();

        // 3. 应用等级加成
        ApplyLevelBonus(level);

        Debug.Log($"{enemyName} 初始化完成：类型={type}, 等级={level}");
    }

    public EnemyType GetEnemyType() // 获取敌人类型
    {
        return enemyType;
    }

    public int GetEnemyLevel() // 获取敌人等级
    {
        return enemyLevel;
    }

    public void ApplyLevelBonus(int level) // 应用等级加成
    {
        if (level < 1)
        {
            Debug.LogWarning($"敌人等级不能小于1，当前值：{level}");
            return;
        }

        enemyLevel = level;
        if (levelSystem != null)
        {
            levelSystem.Initialize(); // 确保等级系统已初始化
            levelSystem.ApplyLevelBonus(enemyLevel);
        }
    }

    private void ApplyTypeBonus() // 应用类型加成
    {
        // 根据敌人类型激活对应的类型系统
        switch (enemyType)
        {
            case EnemyType.Normal:
                ActivateTypeSystem(normalTypeSystem);
                break;
            case EnemyType.Elite:
                ActivateTypeSystem(eliteTypeSystem);
                break;
            case EnemyType.Boss:
                ActivateTypeSystem(bossTypeSystem);
                break;
            default:
                Debug.LogWarning($"未知的敌人类型：{enemyType}，使用默认类型系统");
                ActivateTypeSystem(normalTypeSystem);
                break;
        }
    }

    private void ActivateTypeSystem(EnemyTypeSystem targetSystem) // 激活指定的类型系统
    {
        if (targetSystem == null)
        {
            Debug.LogWarning($"{enemyName} 的{enemyType}类型系统未找到，请确保在Inspector中挂载对应组件");
            return;
        }

        activeTypeSystem = targetSystem;
        activeTypeSystem.Initialize(this);
        
        Debug.Log($"{enemyName} 已激活{enemyType}类型系统");
    }

    public void TryStun(float extraDuration = 0f)
    {
        if (!canBeStunned || stateMachine.currentState == stunnedState)
            return;
        stunnedDuration = Mathf.Max(1f, stunnedDuration + extraDuration);
        stateMachine.ChangeState(stunnedState);
    }

    public RaycastHit2D PlayerDetected()//检测玩家
    {
        RaycastHit2D hit =
            Physics2D.Raycast(playerCheck.position, Vector2.right * facingDir, checkPlayer_Distance, whatIsPlayer | whatIsGround);

        if (hit.collider == null || hit.collider.gameObject.layer != LayerMask.NameToLayer("Player"))
            return default;
        //如果没有检测到玩家或者只检测到地面时返回默认值，确保只有检测到玩家且碰到玩家时才返回检测到的结果
        return hit;
    }
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = Color.yellow;//绘制检测玩家的射线
        Gizmos.DrawLine(playerCheck.position, new Vector3(playerCheck.position.x + (facingDir * checkPlayer_Distance),playerCheck.position.y));
        Gizmos.color = Color.blue;//绘制攻击范围的射线
        Gizmos.DrawLine(playerCheck.position, new Vector3(playerCheck.position.x + (facingDir * attackDistance), playerCheck.position.y));
        Gizmos.color = Color.green;//绘制应该后退的最小距离射线
        Gizmos.DrawLine(playerCheck.position, new Vector3(playerCheck.position.x + (facingDir * minRetreatDistance), playerCheck.position.y));
    }
}