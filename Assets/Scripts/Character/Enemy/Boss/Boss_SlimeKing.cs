using System.Collections;
using UnityEngine;

// 史莱姆王——动作池调度器 Boss AI
// 出生即冻结 FSM（inert），BeginFight() 才启动动作循环；EntityDead() 停调度器+解冻播死亡
public class Boss_SlimeKing : Enemy
{
    [Header("跳跃砸击")]
    [SerializeField] private float chargeTime = 0.5f;      // 蓄力时长（前摇）
    [SerializeField] private float jumpHeightCap = 6f;     // 跳高上限（保证屏内）
    [SerializeField] private float landingRadius = 2f;     // 落点 AoE 半径
    [SerializeField] private float landingDamagePercent = 0.2f; // 落点伤害（%MaxHP）
    [SerializeField] private float landingRecovery = 1f;   // 落地后摇（惩罚窗口，期间关接触伤害）
    [SerializeField] private GameObject telegraphPrefab;   // 落点预告（投影阴影）预制体，可空

    [Header("召唤")]
    [SerializeField] private GameObject minionPrefab;      // 迷你王预制体
    [SerializeField] private float summonCooldown = 10f;   // 召唤 CD（秒）
    [SerializeField] private int minionBaseCount = 2;      // 每次召唤数量（基础）
    [SerializeField] private int minionMaxAlive = 3;       // 场上迷你王上限

    [Header("传送")]
    [SerializeField] private float teleportCooldown = 6f;  // 传送冷却（秒）
    [SerializeField] private float stuckThreshold = 3f;    // 被卡判定时长（秒）
    [SerializeField] private float stuckSpeedThreshold = 0.5f; // 被卡位移阈值（m/s）
    [SerializeField] private float tooFarDistance = 15f;   // 距玩家超此值强制传送
    [SerializeField] private float teleportOffsetMin = 3f; // 传送落点距玩家最小偏移
    [SerializeField] private float teleportOffsetMax = 6f; // 传送落点距玩家最大偏移
    [SerializeField] private Transform arenaBounds;        // 竞技场边界（落点 clamp，可为空）

    [Header("狂暴")]
    [SerializeField] private float rageThreshold = 0.3f;   // 狂暴血量阈值

    [Header("组件")]
    [SerializeField] private ContactDamageArea contactArea;   // 身体接触（史莱姆专属）
    [SerializeField] private BossMinionRegistry minionRegistry; // 迷你王登记处

    public event System.Action OnLanded; // 落地事件（BossEncounter 订阅 → 震屏）

    private Coroutine schedulerCo;      // 调度器协程
    private Coroutine currentActionCo;  // 当前动作协程
    private bool isFighting;            // 战斗标志
    private bool isRaging;              // 狂暴标志
    private float stuckClock;           // 被卡计时
    private float lastSummonTime;       // 上次召唤时间
    private float lastTeleportTime;     // 上次传送时间
    private Vector2 lastStuckPos;       // 被卡检测参考位置
    private Transform lockedLandingTarget; // 落点快照目标
    private float enrageFactor = 1f;    // 狂暴节奏倍率（1=正常，0.7=狂暴）

    protected override void Awake()
    {
        base.Awake();
        idleState = new Enemy_IdleState(this, stateMachine, "idle");
        moveState = new Enemy_MoveState(this, stateMachine, "move");
        attackState = new Enemy_AttackState(this, stateMachine, "attack");
        battleState = new Enemy_BattleState(this, stateMachine, "battle");
        deadState = new Enemy_DeadState(this, stateMachine, "dead");
        stunnedState = new Enemy_StunnedState(this, stateMachine, "stunned");
        lastStuckPos = transform.position;
    }

    protected override void Start()
    {
        base.Start();
        // inert：冻结 FSM + 给空状态，杜绝 idleState 自动进战斗/每帧写速度
        stateMachine.Initialize(idleState);
        stateMachine.SwitchOffStateMachine();
    }

    // BossEncounter 在 Intro→Fighting 调用：激活战斗
    public void BeginFight()
    {
        if (isFighting) return;
        isFighting = true;
        lastSummonTime = Time.time; // 入场先给一段召唤 CD
        schedulerCo = StartCoroutine(SchedulerLoop());
    }

    // 死亡优先：停调度器+动作 → 解冻 FSM → base（播死亡动画/掉落）
    public override void EntityDead()
    {
        isFighting = false;
        if (currentActionCo != null) StopCoroutine(currentActionCo);
        if (schedulerCo != null) StopCoroutine(schedulerCo);
        stateMachine.canChangeSate = true; // 解冻，否则 ChangeState(deadState) 被吞
        base.EntityDead();
    }

    // 防 Enemy_Health 命中时强抢控制权（Boss 只由 BeginFight 激活）
    public override void TryEnterBattleState(Transform player) { }

    public int GetMinionCount() => minionRegistry != null ? minionRegistry.Count : 0;

    // ==================== 调度器 ====================

    private IEnumerator SchedulerLoop()
    {
        while (!IsDead && isFighting)
        {
            UpdateRage();
            UpdateStuckClock();

            // 传送优先级：被卡或超距
            if (IsStuck() || TooFarFromPlayer())
            {
                yield return RunAction(TeleportCo());
                stuckClock = 0f;
                continue;
            }

            // 召唤超时保底（狂暴 CD 减半）
            float cd = summonCooldown * (isRaging ? 0.5f : 1f);
            if (Time.time - lastSummonTime > cd && GetMinionCount() < minionMaxAlive)
            {
                yield return RunAction(SummonCo());
                lastSummonTime = Time.time;
                continue;
            }

            // 主攻：跳跃砸击
            yield return RunAction(JumpSmashCo());
            stuckClock = 0f;
        }
    }

    // 统一包装：记录当前动作 + 看门狗超时，任何动作都不可能永久阻塞循环
    private IEnumerator RunAction(IEnumerator action)
    {
        currentActionCo = StartCoroutine(action);
        float watchdog = 8f; // 动作最大时长（防卡死）
        while (currentActionCo != null && watchdog > 0f)
        {
            if (IsDead || isFighting == false) yield break;
            yield return null;
            watchdog -= Time.deltaTime;
        }
        if (currentActionCo != null) StopCoroutine(currentActionCo);
        currentActionCo = null;
        rb.linearVelocity = Vector2.zero; // 中断后复位物理
    }

    // ==================== 动作 ① 跳跃砸击 ====================

    private IEnumerator JumpSmashCo()
    {
        // 蓄力（夸张 squash + 前摇；狂暴不缩短前摇，≥0.45s 可读下限）
        yield return new WaitForSeconds(chargeTime);

        // 最高点快照玩家位置，落点固定（禁止持续追踪=不可躲）
        Transform target = player != null ? player : GetPlayerReference();
        Vector2 landingPoint = target != null ? (Vector2)target.position : (Vector2)transform.position;

        // 升空（限高防出屏）
        float jumpVel = Mathf.Min(jumpHeightCap * 2f, jumpHeightCap);
        rb.linearVelocity = new Vector2(0f, jumpVel);

        // 空中等待落地（带超时防御）
        bool leftGround = false;
        float airTimeout = 2f;
        while (airTimeout > 0f)
        {
            if (IsDead) yield break;
            if (isOnGround == false) leftGround = true;
            else if (leftGround) break;
            yield return null;
            airTimeout -= Time.deltaTime;
        }

        // 落地：AoE 伤害 + 震屏事件 + 后摇
        rb.linearVelocity = Vector2.zero;
        float distToPoint = Vector2.Distance(transform.position, landingPoint);
        if (distToPoint <= landingRadius)
        {
            var health = FindAnyObjectByType<Player>()?.health;
            if (health != null)
                health.TakeDamage(health.GetMaxHP() * landingDamagePercent, 0f, ElementType.None, transform);
        }
        OnLanded?.Invoke(); // BossEncounter 订阅 → 震屏（事件驱动，非定时器）

        // 落地后摇（惩罚窗口）：接触伤害关闭（contactArea 禁用）+ 玩家可安全输出
        if (contactArea != null) contactArea.enabled = false;
        yield return new WaitForSeconds(landingRecovery * (isRaging ? 0.7f : 1f));
        if (contactArea != null) contactArea.enabled = true;
    }

    // ==================== 动作 ② 召唤 ====================

    private IEnumerator SummonCo()
    {
        int count = isRaging ? 3 : minionBaseCount;
        for (int i = 0; i < count && GetMinionCount() < minionMaxAlive; i++)
        {
            if (minionPrefab == null) yield break;
            Vector2 spawnPos = (Vector2)transform.position + Random.insideUnitCircle * 1.2f;
            var go = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<Boss_SlimeMinion>(out var minion))
                minion.InitMinion(minionRegistry);
            yield return new WaitForSeconds(0.2f); // 错峰生成
        }
    }

    // ==================== 动作 ③ 传送 ====================

    private IEnumerator TeleportCo()
    {
        if (Time.time - lastTeleportTime < teleportCooldown)
            yield break;
        lastTeleportTime = Time.time;

        // 原地发光预告 0.3s → 消失
        yield return new WaitForSeconds(0.3f);
        gameObject.SetActive(false);

        // 目标点：距玩家 3~6m 偏移 + 地面射线校验 + 竞技场内 clamp
        Transform target = player != null ? player : GetPlayerReference();
        Vector2 desired = target != null ? (Vector2)target.position : (Vector2)transform.position;
        desired += Random.insideUnitCircle.normalized * Random.Range(teleportOffsetMin, teleportOffsetMax);
        if (arenaBounds != null)
        {
            Bounds b = arenaBounds.GetComponent<Collider2D>() != null
                ? arenaBounds.GetComponent<Collider2D>().bounds
                : new Bounds(arenaBounds.position, Vector3.one * 10f);
            desired = new Vector2(Mathf.Clamp(desired.x, b.min.x, b.max.x), desired.y);
        }
        // 地面校验：目标点下方无地面则回落
        var hit = Physics2D.Raycast(desired, Vector2.down, 2f);
        if (hit.collider != null)
            desired.y = hit.point.y;

        transform.position = desired;
        gameObject.SetActive(true);

        // 落地停顿 ≥0.5s（不做贴脸惩罚）
        yield return new WaitForSeconds(0.5f);
    }

    // ==================== 狂暴 / 被卡检测 ====================

    private void UpdateRage()
    {
        if (isRaging || IsDead)
            return;
        if (GetComponent<Entity_Health>() != null && GetComponent<Entity_Health>().GetHealthPercent() < rageThreshold)
        {
            isRaging = true;
            enrageFactor = 0.7f; // 节奏提档（召唤 CD 减半在前，前摇不缩短）
        }
    }

    private void UpdateStuckClock()
    {
        Vector2 pos = transform.position;
        float moved = Vector2.Distance(pos, lastStuckPos);
        lastStuckPos = pos;
        // 位移低于阈值持续累加（排除跳跃/传送等主动位移）
        if (moved < stuckSpeedThreshold * Time.deltaTime * 60f && isOnGround)
            stuckClock += Time.deltaTime;
        else
            stuckClock = 0f;
    }

    private bool IsStuck() => stuckClock > stuckThreshold;

    private bool TooFarFromPlayer()
    {
        Transform target = player != null ? player : GetPlayerReference();
        if (target == null) return false;
        return Vector2.Distance(transform.position, target.position) > tooFarDistance;
    }
}
