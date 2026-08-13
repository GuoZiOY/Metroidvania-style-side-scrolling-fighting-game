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
    [SerializeField] private float landingRecovery = 1f;   // 落地后摇（惩罚窗口，玩家可安全输出）

    [Header("召唤")]
    [SerializeField] private float summonCooldown = 10f;   // 召唤 CD（秒）
    [SerializeField] private int minionBaseCount = 2;      // 每次召唤数量（基础）
    [SerializeField] private int minionMaxAlive = 3;       // 场上召唤物上限

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

    public event System.Action OnLanded; // 落地事件（BossEncounter 订阅 → 震屏）

    private Coroutine schedulerCo;      // 调度器协程
    private Coroutine currentActionCo;  // 当前动作协程
    private bool isFighting;            // 战斗标志
    public bool entryJumpDone;          // 开场大跳是否完成（BossEncounter 看门狗等待用）
    private bool isRaging;              // 狂暴标志
    private float stuckClock;           // 被卡计时
    private float lastTeleportTime;     // 上次传送时间
    private Vector2 lastStuckPos;       // 被卡检测参考位置

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
        entryJumpDone = false;
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
        schedulerCo = StartCoroutine(SchedulerLoop());
    }

    // 开场大跳进场：从当前远点弧线跳向落地位置（Intro 相机跟随），落地触发 OnLanded。
    // 由 BossEncounter 以嵌套协程等待其完成（yield return boss.EntryJump(...)）
    public IEnumerator EntryJump(Vector3 landPoint)
    {
        isFighting = false; // 入场非战斗，调度器不启动
        entryJumpDone = false; // 看门狗等待标志复位
        stateMachine.SwitchOffStateMachine(); // 冻结 FSM，防自动进战斗

        // 弧线初速度：垂直起跳 + 水平按"实际空中时长"飞向落点（保持重力成抛物线，避免飞过）
        float distX = landPoint.x - transform.position.x;
        float gravity = rb.gravityScale; // 重力（决定空中时长）
        float airTime = gravity > 0.01f ? 2f * jumpHeightCap / gravity : 1.5f; // 起跳-落地时长 ≈ 2v/g
        float horizVel = Mathf.Clamp(distX / airTime, -14f, 14f);
        rb.linearVelocity = new Vector2(horizVel, jumpHeightCap);

        // 等待落地（带超时防御）
        bool leftGround = false;
        float timeout = 3f;
        while (timeout > 0f && IsDead == false)
        {
            if (isOnGround == false)
                leftGround = true;
            else if (leftGround)
                break;
            yield return null;
            timeout -= Time.deltaTime;
        }

        rb.linearVelocity = Vector2.zero; // 落地清零速度
        entryJumpDone = true; // 标记完成（BossEncounter 看门狗据此放行）
        OnLanded?.Invoke(); // BossEncounter 订阅 → 震屏
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

    // ==================== 调度器 ====================

    private IEnumerator SchedulerLoop()
    {
        while (!IsDead && isFighting)
        {
            UpdateRage();
            UpdateStuckClock();

            // 每轮面向玩家（Boss 一直锁定玩家；GetPlayerReference 已兜底按 tag 找）
            Transform face = player != null ? player : GetPlayerReference();
            if (face != null)
                HandleFlip(face.position.x > transform.position.x ? 1 : -1);

            // 传送优先级：被卡或超距
            if (IsStuck() || TooFarFromPlayer())
            {
                yield return RunAction(TeleportCo());
                stuckClock = 0f;
                continue;
            }

            // 主攻：跳跃砸击
            yield return RunAction(JumpSmashCo());
            stuckClock = 0f;
        }
    }

    // 统一包装：等待动作完成（Unity 嵌套协程语义）+ 结束后复位物理；动作内部自带超时不致永久卡死
    private IEnumerator RunAction(IEnumerator action)
    {
        currentActionCo = StartCoroutine(action);
        yield return currentActionCo; // 等待动作完成
        currentActionCo = null;
        rb.linearVelocity = Vector2.zero; // 复位物理（防遗留速度）
    }

    // ==================== 动作 ① 跳跃砸击 ====================

    private IEnumerator JumpSmashCo()
    {
        // 蓄力（夸张 squash + 前摇；狂暴不缩短前摇，≥0.45s 可读下限）
        yield return new WaitForSeconds(chargeTime);

        // 最高点快照玩家位置，落点固定（禁止持续追踪=不可躲）
        Transform target = player != null ? player : GetPlayerReference();
        Vector2 landingPoint = target != null ? (Vector2)target.position : (Vector2)transform.position;

        // 升空：垂直起跳 + 水平飞向落点（保持重力形成弧线；落点已最高点快照）
        float distX = landingPoint.x - transform.position.x;
        float horizVel = Mathf.Clamp(distX / 1.5f, -8f, 8f); // 1.5s 估算空中时长，限制最大水平速度
        rb.linearVelocity = new Vector2(horizVel, jumpHeightCap);

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
            Transform p = player != null ? player : GetPlayerReference();
            var health = p != null ? p.GetComponent<Entity_Health>() : null;
            if (health != null)
                health.TakeDamage(health.GetMaxHP() * landingDamagePercent, 0f, ElementType.None, transform);
        }
        // 注意：跳砸落地不触发 OnLanded 震屏——相机震动只在 Boss 入场那次（EntryJump）震一次

        // 落地后摇（惩罚窗口）：玩家可安全输出（接触伤害并入 Boss 脚本，Task 4 处理）
        yield return new WaitForSeconds(landingRecovery * (isRaging ? 0.7f : 1f));
    }

    // ==================== 动作 ② 传送 ====================

    // 隐藏/显示：禁用/启用渲染+碰撞（保持 GameObject active，让协程继续运行——SetActive(false) 会杀死协程）
    private void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            c.enabled = visible;
    }

    private IEnumerator TeleportCo()
    {
        if (Time.time - lastTeleportTime < teleportCooldown)
            yield break;
        lastTeleportTime = Time.time;

        // 原地发光预告 0.3s → 消失
        yield return new WaitForSeconds(0.3f);
        SetVisible(false);

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
        SetVisible(true);

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
        }
    }

    private void UpdateStuckClock()
    {
        Vector2 pos = transform.position;
        float moved = Vector2.Distance(pos, lastStuckPos);
        lastStuckPos = pos;
        // 位移低于阈值持续累加（排除跳跃/传送等主动位移）
        if (moved < stuckSpeedThreshold * Time.deltaTime && isOnGround)
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
