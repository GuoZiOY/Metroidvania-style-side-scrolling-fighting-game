using System.Collections;
using UnityEngine;

// 史莱姆王——动作池调度器 Boss AI
// 出生即冻结 FSM（inert），BeginFight() 才启动动作循环；EntityDead() 停调度器+解冻播死亡
public class Boss_SlimeKing : Enemy
{
    // === 动作参数 ===
    [Header("大跳")]
    [SerializeField] private float jumpChargeTime = 0.5f;   // 前摇
    [SerializeField] private float jumpHeight = 6f;         // 起跳速度
    [SerializeField] private float landingRadius = 2f;      // 落点 AoE 半径
    [SerializeField] private float landingDamagePercent = 0.2f; // 落点伤害
    [SerializeField] private float landingRecovery = 1f;    // 落地后摇（惩罚窗口）
    [SerializeField] private float telegraphTime = 0.8f;    // 落点预告提前量

    [Header("冲刺")]
    [SerializeField] private float dashSpeed = 20f;         // 待手动调
    [SerializeField] private float dashDistance = 15f;      // 待手动调
    [SerializeField] private float dashDamagePercent = 0.18f; // 1.5x

    [Header("传送攻击")]
    [SerializeField] private float teleportRange = 15f;     // 超距触发
    [SerializeField] private float teleportSlamDamagePercent = 0.24f; // 2x

    [Header("召唤")]
    [SerializeField] private GameObject slimePrefab;        // Enemy_Slime.prefab
    [SerializeField] private float summonCooldown = 10f;    // 召唤间隔

    [Header("预告")]
    [SerializeField] private GameObject telegraphPrefab;    // 落点/传送预告标记（可空）

    private Coroutine schedulerCo;  // 调度器协程
    private bool isFighting;        // 战斗标志
    private Transform playerTarget; // 锁定的玩家

    // === 身体接触伤害（内置，v4：不单独组件）===
    [Header("身体接触伤害")]
    [SerializeField] private float contactDamagePercent = 0.12f; // 接触伤害（%MaxHP）
    [SerializeField] private float contactCooldown = 1f;         // 每源冷却（秒）
    private float lastContactHitTime; // 上次接触伤害时间
    private bool contactEnabled = true; // 落地后摇期间关闭

    // 身体 Trigger 接触玩家 → 每源冷却内造成一次接触伤害
    private void OnTriggerStay2D(Collider2D other)
    {
        if (contactEnabled == false)
            return;
        if (Time.time - lastContactHitTime < contactCooldown)
            return; // 冷却内不重复触发
        if (other.CompareTag("Player") == false)
            return; // 只伤玩家
        var health = other.GetComponent<Entity_Health>();
        if (health == null)
            return;
        health.TakeDamage(health.GetMaxHP() * contactDamagePercent, 0f, ElementType.None, transform);
        lastContactHitTime = Time.time;
    }

    protected override void Awake()
    {
        base.Awake();
        idleState = new Enemy_IdleState(this, stateMachine, "idle");
        moveState = new Enemy_MoveState(this, stateMachine, "move");
        attackState = new Enemy_AttackState(this, stateMachine, "attack");
        battleState = new Enemy_BattleState(this, stateMachine, "battle");
        deadState = new Enemy_DeadState(this, stateMachine, "dead");
        stunnedState = new Enemy_StunnedState(this, stateMachine, "stunned");
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
        if (isFighting)
            return;
        isFighting = true;
        stateMachine.SwitchOffStateMachine(); // 冻结 FSM
        schedulerCo = StartCoroutine(SchedulerLoop());
    }

    // 死亡优先：停调度器 → 解冻 FSM → base（播死亡动画/掉落）
    public override void EntityDead()
    {
        isFighting = false;
        if (schedulerCo != null)
            StopCoroutine(schedulerCo);
        stateMachine.canChangeSate = true; // 解冻，否则 ChangeState(deadState) 被吞
        base.EntityDead();
    }

    // 防 Enemy_Health 命中时强抢控制权（Boss 只由 BeginFight 激活）
    public override void TryEnterBattleState(Transform player) { }

    // ==================== 调度器 ====================

    // 每轮按状态选动作：超距传送 / 随机（大跳主攻/冲刺/召唤）
    private IEnumerator SchedulerLoop()
    {
        while (!IsDead && isFighting)
        {
            // 每轮面向玩家
            Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
            if (t != null)
            {
                playerTarget = t;
                HandleFlip(t.position.x > transform.position.x ? 1 : -1);
            }

            // 超距 → 传送攻击
            if (t != null && Vector2.Distance(transform.position, t.position) > teleportRange)
            {
                yield return StartCoroutine(TeleportAttackCo());
                continue;
            }

            // 随机选：大跳(主) / 冲刺 / 召唤
            float roll = Random.value;
            if (roll < 0.5f)
                yield return StartCoroutine(JumpTouchCo());
            else if (roll < 0.75f)
                yield return StartCoroutine(DashCo());
            else
                yield return StartCoroutine(SummonCo());
        }
    }

    // ==================== 动作 ① 大跳触碰 ====================

    // 前摇 → 落点预告 → 弧线起跳 → 落地 AoE + 后摇（惩罚窗口）
    private IEnumerator JumpTouchCo()
    {
        // 前摇（收缩）
        yield return new WaitForSeconds(jumpChargeTime);

        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        Vector2 landing = t != null ? (Vector2)t.position : (Vector2)transform.position;

        // 落点预告（投影阴影，提前可见）
        if (telegraphPrefab != null)
        {
            var tel = Instantiate(telegraphPrefab, landing, Quaternion.identity);
            Destroy(tel, telegraphTime);
        }

        // 起跳弧线（水平朝落点 + 垂直起跳）
        float distX = landing.x - transform.position.x;
        float gravity = rb.gravityScale;
        float airTime = gravity > 0.01f ? 2f * jumpHeight / gravity : 1.5f;
        rb.linearVelocity = new Vector2(Mathf.Clamp(distX / airTime, -14f, 14f), jumpHeight);

        // 等落地（超时防御）
        bool leftGround = false;
        float timeout = 3f;
        while (timeout > 0f && !IsDead)
        {
            if (isOnGround == false)
                leftGround = true;
            else if (leftGround)
                break;
            yield return null;
            timeout -= Time.deltaTime;
        }
        rb.linearVelocity = Vector2.zero;

        // 落点 AoE 伤害（可躲，靠预告）
        if (Vector2.Distance(transform.position, landing) <= landingRadius && t != null)
        {
            var h = t.GetComponent<Entity_Health>();
            if (h != null)
                h.TakeDamage(h.GetMaxHP() * landingDamagePercent, 0f, ElementType.None, transform);
        }

        // 落地后摇（惩罚窗口）：关接触伤害，玩家可输出
        contactEnabled = false;
        yield return new WaitForSeconds(landingRecovery);
        contactEnabled = true;
    }

    // ==================== 动作 ② 冲刺 ====================

    // 无提示，锁定方向横冲；接触伤害关闭改由冲刺自身判定
    private IEnumerator DashCo()
    {
        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        if (t == null)
            yield break;

        // 锁定方向（朝玩家）+ 极短 0.2s 定向前摇
        int dir = t.position.x > transform.position.x ? 1 : -1;
        yield return new WaitForSeconds(0.2f);

        // 快速横冲
        float traveled = 0f;
        float speed = dashSpeed;
        while (traveled < dashDistance && !IsDead)
        {
            rb.linearVelocity = new Vector2(dir * speed, 0f);
            traveled += speed * Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;

        // 冲刺碰撞伤害（触碰）
        contactEnabled = false; // 冲刺期间接触伤害关闭，改由冲刺自身判定
        // 冲刺路径上的玩家判定（简化：终点近身判定）
        if (t != null && Vector2.Distance(transform.position, t.position) < 2f)
        {
            var h = t.GetComponent<Entity_Health>();
            if (h != null)
                h.TakeDamage(h.GetMaxHP() * dashDamagePercent, 0f, ElementType.None, transform);
        }
        contactEnabled = true;
        yield return new WaitForSeconds(0.4f); // 冲刺后停顿
    }

    // ==================== 动作 ③ 传送攻击 ====================

    // 超距 → 头顶落下（原地闪光预告 + 传送到玩家头顶下落触碰）
    private IEnumerator TeleportAttackCo()
    {
        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        if (t == null)
            yield break;

        // 传送闪光预告
        if (telegraphPrefab != null)
            Instantiate(telegraphPrefab, transform.position, Quaternion.identity);
        yield return new WaitForSeconds(0.3f);

        // 传送到玩家头顶，落下触碰
        transform.position = (Vector2)t.position + Vector2.up * 6f;
        yield return new WaitForSeconds(0.6f); // 落下

        if (t != null && Vector2.Distance(transform.position, t.position) < 2f)
        {
            var h = t.GetComponent<Entity_Health>();
            if (h != null)
                h.TakeDamage(h.GetMaxHP() * teleportSlamDamagePercent, 0f, ElementType.None, transform);
        }
        yield return new WaitForSeconds(0.5f); // 落地停顿
    }

    // ==================== 动作 ④ 召唤普通史莱姆 ====================

    private float lastSummonTime; // 上次召唤时间

    private IEnumerator SummonCo()
    {
        if (Time.time - lastSummonTime < summonCooldown)
            yield break;
        lastSummonTime = Time.time;

        if (slimePrefab != null)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 pos = (Vector2)transform.position + Random.insideUnitCircle * 1.5f;
                Instantiate(slimePrefab, pos, Quaternion.identity);
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}
