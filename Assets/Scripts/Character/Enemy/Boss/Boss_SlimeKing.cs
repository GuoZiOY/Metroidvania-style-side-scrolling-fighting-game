using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random; // 消除 System.Random / UnityEngine.Random 二义性

// 史莱姆王——动作池调度器 Boss AI
// 出生即冻结 FSM（inert），BeginFight() 才启动动作循环；EntityDead() 停调度器+解冻播死亡
public class Boss_SlimeKing : Enemy
{
    // === 动作参数 ===
    [Header("大跳")]
    [SerializeField] private float jumpChargeTime = 0.5f;   // 前摇
    [SerializeField] private float jumpHeight = 9f;         // 起跳速度（越高弧线越夸张/滞空越久）
    [SerializeField] private float jumpHorizSpeedCap = 18f; // 水平起跳速度上限（远距离追击可达距离）
    [SerializeField] private float landingRadius = 2f;      // 落点 AoE 半径
    [SerializeField] private float landingDamagePercent = 0.2f; // 落点伤害
    [SerializeField] private float landingRecovery = 1f;    // 落地后摇（惩罚窗口）
    [SerializeField] private float telegraphTime = 0.8f;    // 落点预告提前量

    [Header("冲刺")]
    [SerializeField] private float dashSpeed = 26f;         // 冲刺速度（待手动调）
    [SerializeField] private float dashDistance = 20f;      // 冲刺距离（待手动调）
    [SerializeField] private float dashDamagePercent = 0.18f; // 1.5x

    [Header("传送攻击")]
    [SerializeField] private float teleportRange = 15f;     // 超距触发
    [SerializeField] private float farTeleportDelay = 4f;   // 超距持续多久才传送（先跳跃逼近，最后一招）
    [SerializeField] private float teleportSlamDamagePercent = 0.24f; // 2x

    [Header("召唤")]
    [SerializeField] private GameObject slimePrefab;        // Enemy_Slime.prefab
    [SerializeField] private float summonCooldown = 10f;    // 召唤间隔

    [Header("预告")]
    [SerializeField] private GameObject telegraphPrefab;    // 落点/传送预告标记（可空）

    private Coroutine schedulerCo;  // 调度器协程
    private bool isFighting;        // 战斗标志
    private Transform playerTarget; // 锁定的玩家
    private float farTimer;         // 超距持续计时（智能传送：先逼近，持续超距才传送）

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
        var playerHealth = other.GetComponent<Entity_Health>();
        if (playerHealth == null)
            return;
        playerHealth.TakeDamage(playerHealth.GetMaxHP() * contactDamagePercent, 0f, ElementType.None, transform);
        lastContactHitTime = Time.time;
    }

    // === 阶段状态机（常态/分裂狂暴/濒死隐身）===
    public enum BossPhase { Normal, Split, Stagger, Stealth } // 三阶段枚举

    public BossPhase phase = BossPhase.Normal;               // 当前阶段
    public bool hasSplit;                                    // 是否已分裂（防重复）
    public Boss_SlimeKing sibling;                           // 分裂的另一只
    public bool isPrimary = true;                            // 主实例（血条绑定/存活者晋升）
    public event Action<Boss_SlimeKing> OnPrimaryChanged;    // 血条重绑事件

    [Header("分裂")]
    [SerializeField] private float splitThreshold = 0.3f;       // 分裂阈值
    [SerializeField] private float splitScale = 0.7f;           // 分裂体缩放
    [SerializeField] private float survivorHealPercent = 0.29f; // 存活者回血值
    [SerializeField] private float staggerDuration = 1.5f;      // 僵直时长

    [Header("濒死隐身")]
    [SerializeField] private float stealthThreshold = 0.01f;     // 濒死阈值
    [SerializeField] private float stealthHealRate = 0.02f;      // 隐身回血速度（%/s）
    [SerializeField] private float stealthHealCap = 0.29f;       // 回血封顶（<分裂阈值）
    [SerializeField] private float stealthSummonInterval = 4f;   // 隐身召唤间隔
    [SerializeField] private int stealthSummonCount = 1;         // 每次召唤数量

    private Entity_Health health;                       // 自身生命缓存
    private Coroutine stealthCo;                        // 隐身协程
    private readonly List<GameObject> stealthSlimes = new(); // 隐身期间召唤的史莱姆
    private bool stealthSpawnedAny;                     // 隐身是否召唤过史莱姆

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<Entity_Health>(); // 自身生命缓存
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

    // 死亡优先：停调度器/隐身 → 解冻 FSM → 分裂体存亡分流（存活者晋升 vs 最后实例胜利）
    public override void EntityDead()
    {
        isFighting = false;
        if (schedulerCo != null)
            StopCoroutine(schedulerCo);
        if (stealthCo != null)
            StopCoroutine(stealthCo);
        stateMachine.canChangeSate = true; // 解冻，否则 ChangeState(deadState) 被吞

        // 兄弟存活 → 存活者晋升（回血/僵直/3段冲刺）；本实例正常死亡（播死亡+销毁，不留尸体）
        if (sibling != null && sibling.IsDead == false)
        {
            OnPrimaryChanged?.Invoke(sibling); // 血条重绑到存活者（事件在已订阅的实例上触发）
            sibling.OnSiblingDied();
            base.EntityDead(); // 分裂体自身播死亡+销毁，不留尸体
        }
        else
        {
            base.EntityDead(); // 最后实例死亡 → 胜利
        }
    }

    // ==================== 阶段状态机 ====================

    // 每轮阶段检查：濒死隐身优先 → 分裂狂暴（<30% 未分裂）
    private void UpdatePhase()
    {
        if (IsDead || phase == BossPhase.Stealth)
            return;

        float pct = health.GetHealthPercent();

        // 濒死隐身（<1%）
        if (pct < stealthThreshold)
        {
            StartStealth();
            return;
        }
        // 分裂狂暴（<30%，未分裂）
        if (pct < splitThreshold && hasSplit == false)
        {
            Split();
        }
    }

    // 分裂：生成第 2 实例，各半血，克隆也开打
    private void Split()
    {
        hasSplit = true;
        phase = BossPhase.Split;
        var clone = Instantiate(gameObject, transform.position, Quaternion.identity).GetComponent<Boss_SlimeKing>();
        clone.isPrimary = false;
        clone.hasSplit = true;
        clone.sibling = this;
        sibling = clone;
        clone.transform.localScale = transform.localScale * splitScale;
        // 各半血（当前血量 / 2）
        float half = health.GetCurrentHP() / 2f;
        health.SetCurrentHP(half);
        clone.health.SetCurrentHP(half);
        clone.BeginFight(); // 克隆也开打
    }

    // 兄弟死亡 → 存活者：晋升为主 + 回血 29% → 僵直 → 3 段冲刺 → 恢复（不再分裂）
    private void OnSiblingDied()
    {
        if (isPrimary == false)
        {
            isPrimary = true; // 晋升主实例（血条重绑已由死亡实例的 OnPrimaryChanged 事件完成）
        }
        // 回血到 29%
        health.SetCurrentHP(health.GetMaxHP() * survivorHealPercent);
        // 僵直（可被打）
        phase = BossPhase.Stagger;
        StartCoroutine(StaggerThenDashCo());
    }

    private IEnumerator StaggerThenDashCo()
    {
        yield return new WaitForSeconds(staggerDuration); // 僵直
        // 连续 3 段快速冲刺（DashCo 死亡自终止）
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(DashCo());
        }
        phase = BossPhase.Normal; // 恢复常态（不再分裂）
    }

    // 濒死隐身：半透明、无法移动攻击、召唤史莱姆、缓慢回血
    private void StartStealth()
    {
        phase = BossPhase.Stealth;
        hasSplit = true; // 隐身封顶 29% < 分裂阈值，杜绝退出后重新分裂
        if (schedulerCo != null)
            StopCoroutine(schedulerCo);
        stealthCo = StartCoroutine(StealthCo());
    }

    private IEnumerator StealthCo()
    {
        // 半透明
        var sr = GetComponentInChildren<SpriteRenderer>();
        Color c = sr.color;
        sr.color = new Color(c.r, c.g, c.b, 0.3f);
        contactEnabled = false; // 隐身不攻击
        stealthSlimes.Clear();  // 清空上一轮隐身残留
        stealthSpawnedAny = false;
        float summonTimer = 0f;

        // 回血（封顶 29%）+ 周期召唤史莱姆；杀光史莱姆或回满提前退出
        while (!IsDead)
        {
            // 缓慢回血（按 %MaxHP/s）
            health.IncreaseHP(health.GetMaxHP() * stealthHealRate * Time.deltaTime);

            // 周期召唤史莱姆（slimePrefab 为空则跳过召唤，仅回血）
            summonTimer += Time.deltaTime;
            if (summonTimer >= stealthSummonInterval && slimePrefab != null)
            {
                summonTimer = 0f;
                stealthSpawnedAny = true;
                for (int i = 0; i < stealthSummonCount; i++)
                {
                    Vector2 pos = (Vector2)transform.position + Random.insideUnitCircle * 1.5f;
                    stealthSlimes.Add(Instantiate(slimePrefab, pos, Quaternion.identity));
                }
            }
            // 掉出被销毁的史莱姆引用
            stealthSlimes.RemoveAll(s => s == null);

            // 回血封顶 → 退出隐身
            if (health.GetHealthPercent() >= stealthHealCap)
                break;
            // 史莱姆杀光 → 提前退出隐身
            if (stealthSpawnedAny && stealthSlimes.Count == 0)
                break;

            yield return null;
        }

        // 退出隐身：恢复不透明 + 接触伤害 + 重启调度器
        sr.color = new Color(c.r, c.g, c.b, 1f);
        contactEnabled = true;
        phase = BossPhase.Normal;
        isFighting = true;
        schedulerCo = StartCoroutine(SchedulerLoop());
    }

    // 防 Enemy_Health 命中时强抢控制权（Boss 只由 BeginFight 激活）
    public override void TryEnterBattleState(Transform player) { }

    // ==================== 调度器 ====================

    // 每轮按状态选动作：超距传送 / 随机（大跳主攻/冲刺/召唤）
    private IEnumerator SchedulerLoop()
    {
        while (!IsDead && isFighting)
        {
            // 每轮先查阶段（分裂/濒死隐身），StartStealth 已停本协程
            UpdatePhase();
            // 隐身期间不执行动作（防残留一帧出招）
            if (phase == BossPhase.Stealth)
                break;
            // 僵直：调度器挂起不选动作（可被打），StaggerThenDashCo 单独驱动 3 段冲刺
            if (phase == BossPhase.Stagger)
            {
                yield return null;
                continue;
            }

            // 每轮面向玩家
            Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
            if (t != null)
            {
                playerTarget = t;
                HandleFlip(t.position.x > transform.position.x ? 1 : -1);
            }

            // 超距计时：先尝试大跳逼近，持续超距才传送（最后一招，不立刻传）
            float distToPlayer = t != null ? Vector2.Distance(transform.position, t.position) : 0f;
            if (distToPlayer > teleportRange)
                farTimer += Time.deltaTime;
            else
                farTimer = 0f;

            if (farTimer > farTeleportDelay)
            {
                farTimer = 0f;
                yield return StartCoroutine(TeleportAttackCo());
                continue;
            }

            // 动作选择：远距离优先大跳逼近（大跳水平可达 jumpHorizSpeedCap*滞空）；近距离随机
            float roll = Random.value;
            if (distToPlayer > teleportRange * 0.7f)
                yield return StartCoroutine(JumpTouchCo()); // 远：跳跃逼近
            else if (roll < 0.5f)
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
        rb.linearVelocity = new Vector2(Mathf.Clamp(distX / airTime, -jumpHorizSpeedCap, jumpHorizSpeedCap), jumpHeight);

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
