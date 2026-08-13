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
    [Header("追击（常态）")]
    [SerializeField] private float chaseSpeed = 3.5f;       // 追击速度（比玩家慢，接触伤害=威胁）
    [SerializeField] private float chaseDuration = 1f;      // 每次追击持续时长（秒）

    [Header("普通攻击·原地起跳落下")]
    [SerializeField] private float normalJumpChargeTime = 0.4f; // 前摇
    [SerializeField] private float normalJumpHeight = 14f;      // 起跳速度（原地小跳，顶点≈2.9单位/滞空≈0.8s）
    [SerializeField] private float closeJumpRange = 4f;         // 贴身触发距离（<此值原地跳逼开）
    [SerializeField] private float closeJumpCooldown = 3f;      // 原地跳冷却

    [Header("大跳·从远处扑过来")]
    [SerializeField] private float bigJumpChargeTime = 0.6f;    // 前摇（更长，可读）
    [SerializeField] private float bigJumpHeight = 26f;         // 起跳速度（高弧线≈顶点9.6单位/滞空≈1.5s，给反应时间）
    [SerializeField] private float jumpHorizSpeedCap = 12f;     // 大跳水平起跳速度上限（落地不会太远）
    [SerializeField] private float bigJumpCooldown = 8f;        // 大跳冷却（降低频率）
    [SerializeField] private float bigJumpMinRange = 7f;        // 距玩家超此值才大跳（中远距离）

    [Header("落地")]
    [SerializeField] private float landingRadius = 2f;      // 落点 AoE 半径
    [SerializeField] private float landingDamagePercent = 0.2f; // 落点伤害
    [SerializeField] private float landingRecovery = 1f;    // 落地后摇（惩罚窗口）
    [SerializeField] private float telegraphTime = 0.8f;    // 落点预告停留时长

    [Header("冲刺")]
    [SerializeField] private float dashSpeed = 26f;         // 冲刺速度（待手动调）
    [SerializeField] private float dashDistance = 20f;      // 冲刺距离（待手动调）
    [SerializeField] private float dashDamagePercent = 0.18f; // 1.5x
    [SerializeField] private float dashRangeMin = 3f;       // 冲刺触发最近距离
    [SerializeField] private float dashRangeMax = 10f;      // 冲刺触发最远距离
    [SerializeField] private float dashCooldown = 5f;       // 冲刺冷却

    [Header("传送攻击")]
    [SerializeField] private float teleportRange = 15f;     // 超距触发
    [SerializeField] private float farTeleportDelay = 4f;   // 超距持续多久才传送（先追击逼近，最后一招）
    [SerializeField] private float teleportTelegraph = 1f;  // 原地闪光预告（加长，可读）
    [SerializeField] private float teleportLandMark = 0.8f; // 头顶落点标记预告（砸落前）
    [SerializeField] private float teleportHeight = 6f;     // 头顶传送高度（砸落起点）
    [SerializeField] private float teleportSlamDamagePercent = 0.24f; // 2x

    [Header("召唤")]
    [SerializeField] private GameObject slimePrefab;        // Enemy_Slime.prefab
    [SerializeField] private float summonCooldown = 10f;    // 召唤间隔

    [Header("预告")]
    [SerializeField] private GameObject telegraphPrefab;    // 落点/传送预告标记（可空）

    private Coroutine schedulerCo;  // 调度器协程
    private bool isFighting;        // 战斗标志
    private Transform playerTarget; // 锁定的玩家
    private float lastBigJumpTime;  // 上次大跳时间（冷却控制）
    private float lastDashTime;     // 上次冲刺时间（冷却控制）
    private float lastCloseJumpTime;// 上次原地跳时间（冷却控制）
    private float lastCloseTime;    // 玩家最近一次近距离时间（超距计时基准）

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

    [Header("濒死隐身·一次性保命")]
    [SerializeField] private float stealthThreshold = 0.01f;     // 致命伤后强制保留血量（1%）
    [SerializeField] private float stealthHealRate = 0.02f;      // 隐身回血速度（%/s）
    [SerializeField] private float stealthHealCap = 0.29f;       // 回血封顶（<分裂阈值）
    [SerializeField] private float stealthSummonInterval = 3f;   // 隐身召唤间隔（频繁）
    [SerializeField] private int stealthSummonCount = 1;         // 每次召唤数量
    [SerializeField] private int stealthMaxSummons = 5;          // 隐身总召唤次数上限（防无限刷）
    [SerializeField] private int stealthMaxAlive = 4;            // 隐身同时存活史莱姆上限

    private Entity_Health health;                       // 自身生命缓存
    private Coroutine stealthCo;                        // 隐身协程
    private Coroutine currentActionCo;                  // 当前动作协程（隐身/死亡时停掉，防残留移动）
    private readonly List<GameObject> stealthSlimes = new(); // 隐身期间召唤的史莱姆
    private bool stealthUsed;                           // 濒死保命是否已用（一次性）

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

    // 死亡处理：濒死保命（一次性）→ 停调度器/隐身/动作 → 解冻 FSM → 分裂体存亡分流
    public override void EntityDead()
    {
        // 濒死保命：受致命伤害时（health 已置 isDead）强制保留 1% 血进入隐身回血+召唤，不死
        if (stealthUsed == false)
        {
            stealthUsed = true;
            health.Revive(); // 清除 isDead，否则后续无法受伤/回血
            health.SetCurrentHP(health.GetMaxHP() * stealthThreshold); // 强制保留 1% 血
            StartStealth();
            return;
        }

        isFighting = false;
        if (schedulerCo != null)
            StopCoroutine(schedulerCo);
        if (stealthCo != null)
            StopCoroutine(stealthCo);
        if (currentActionCo != null)
        {
            StopCoroutine(currentActionCo); // 停掉被打断时正在跑的动作（防残留移动）
            currentActionCo = null;
        }
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

    // 每轮阶段检查：分裂狂暴（<30% 未分裂）。濒死隐身改由 EntityDead 受致命伤害时触发（一次性保命）
    private void UpdatePhase()
    {
        if (IsDead || phase == BossPhase.Stealth)
            return;

        // 分裂狂暴（<30%，未分裂）
        if (health.GetHealthPercent() < splitThreshold && hasSplit == false)
        {
            Split();
        }
    }

    // 分裂：生成第 2 实例，各半血，克隆也开打
    private void Split()
    {
        hasSplit = true;
        phase = BossPhase.Split;
        // 分裂瞬间可能处于受击闪光中（sr.material 被换成白色 onDamageMaterial），
        // 若直接 Instantiate，克隆会把白色材质拷过去且克隆 Awake 把白色记为 originalMaterial → 永远白。
        // 先还原原实例材质再克隆，保证克隆拿到正常材质。
        var splitVfx = GetComponentInChildren<Entity_VFX>();
        if (splitVfx != null)
            splitVfx.StopAllVFX();

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
            yield return RunAction(DashCo());
        }
        phase = BossPhase.Normal; // 恢复常态（不再分裂）
    }

    // 濒死隐身：半透明、无法移动攻击、频繁召唤史莱姆、缓慢回血；一次性保命
    private void StartStealth()
    {
        phase = BossPhase.Stealth;
        hasSplit = true; // 隐身封顶 29% < 分裂阈值，杜绝退出后重新分裂
        if (schedulerCo != null)
            StopCoroutine(schedulerCo);
        if (currentActionCo != null)
        {
            StopCoroutine(currentActionCo); // 停掉被打断时正在跑的动作（防隐身期间残留移动）
            currentActionCo = null;
        }
        stealthCo = StartCoroutine(StealthCo());
    }

    // 隐身协程：半透明、缓慢回血、频繁召唤（总次数+存活上限）；
    // 退出条件：召唤的史莱姆全部死亡（玩家清场） 或 回血封顶 29%
    private IEnumerator StealthCo()
    {
        // 半透明
        var sr = GetComponentInChildren<SpriteRenderer>();
        Color c = sr.color;
        sr.color = new Color(c.r, c.g, c.b, 0.3f);
        contactEnabled = false; // 隐身不攻击
        stealthSlimes.Clear();  // 清空上一轮隐身残留
        int summonsUsed = 0;    // 已召唤次数（预算）
        float summonTimer = 0f;

        while (!IsDead)
        {
            // 缓慢回血（封顶 29%）
            health.IncreaseHP(health.GetMaxHP() * stealthHealRate * Time.deltaTime);

            // 频繁召唤：未达总次数上限 且 未达存活上限 → 召唤
            summonTimer += Time.deltaTime;
            if (summonTimer >= stealthSummonInterval)
            {
                summonTimer = 0f;
                stealthSlimes.RemoveAll(s => s == null); // 清掉已死史莱姆引用
                if (summonsUsed < stealthMaxSummons && stealthSlimes.Count < stealthMaxAlive && slimePrefab != null)
                {
                    summonsUsed++;
                    for (int i = 0; i < stealthSummonCount; i++)
                    {
                        Vector2 pos = (Vector2)transform.position + Random.insideUnitCircle * 1.5f;
                        stealthSlimes.Add(Instantiate(slimePrefab, pos, Quaternion.identity));
                    }
                }
            }
            stealthSlimes.RemoveAll(s => s == null); // 掉出被销毁的史莱姆引用

            // 退出①：召唤预算已尽 且 召唤的史莱姆全部死亡 → 玩家清场，退出隐身
            if (summonsUsed >= stealthMaxSummons && stealthSlimes.Count == 0)
                break;
            // 退出②：回血封顶 → 退出隐身（防玩家不清场导致战斗卡住）
            if (health.GetHealthPercent() >= stealthHealCap)
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

    // 每轮按优先级选行为：超距传送 / 召唤 / 大跳 / 冲刺 / 原地跳 / 追击（默认常态）
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

            // 超距计时（真实时间）：持续超距才传送（最后一招），中途给追击/大跳逼近机会
            float distToPlayer = t != null ? Vector2.Distance(transform.position, t.position) : 0f;
            if (distToPlayer > teleportRange)
            {
                if (lastCloseTime <= 0f)
                    lastCloseTime = Time.time; // 首次超距开始计时
                if (Time.time - lastCloseTime > farTeleportDelay)
                {
                    lastCloseTime = Time.time; // 传送后重置计时
                    yield return RunAction(TeleportAttackCo());
                    continue;
                }
            }
            else
            {
                lastCloseTime = Time.time; // 近距离：刷新计时基准
            }

            // 召唤（冷却，周期性干扰）
            if (Time.time - lastSummonTime > summonCooldown)
            {
                lastSummonTime = Time.time;
                yield return RunAction(SummonCo());
                continue;
            }

            // 大跳：中远距离扑过来（冷却）
            if (distToPlayer > bigJumpMinRange && Time.time - lastBigJumpTime > bigJumpCooldown)
            {
                lastBigJumpTime = Time.time;
                yield return RunAction(BigJumpCo());
                continue;
            }

            // 冲刺：中距离带横冲（冷却）
            if (distToPlayer > dashRangeMin && distToPlayer < dashRangeMax && Time.time - lastDashTime > dashCooldown)
            {
                lastDashTime = Time.time;
                yield return RunAction(DashCo());
                continue;
            }

            // 原地跳：贴身逼开（冷却）
            if (distToPlayer < closeJumpRange && Time.time - lastCloseJumpTime > closeJumpCooldown)
            {
                lastCloseJumpTime = Time.time;
                yield return RunAction(JumpInPlaceCo());
                continue;
            }

            // 默认：追击（接触伤害=威胁）——没招可放就往玩家脸上走
            yield return RunAction(ChaseCo());
        }
    }

    // 统一包装：记录当前动作协程（隐身/死亡时停掉，防残留移动）+ 等待动作完成
    private IEnumerator RunAction(IEnumerator action)
    {
        currentActionCo = StartCoroutine(action);
        yield return currentActionCo;
        currentActionCo = null;
    }

    // ==================== 追击（常态） ====================

    // 朝玩家方向移动（接触伤害常开 → 追上即威胁）；每帧刷新朝向
    private IEnumerator ChaseCo()
    {
        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        if (t == null)
            yield break;

        int dir = t.position.x > transform.position.x ? 1 : -1;
        float timer = 0f;
        while (timer < chaseDuration && !IsDead)
        {
            // 玩家位移时跟着转
            if (t != null)
                dir = t.position.x > transform.position.x ? 1 : -1;
            rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
            yield return null;
            timer += Time.deltaTime;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    // ==================== 动作 ① 普通攻击·原地起跳落下 ====================

    // 前摇 → 原地小跳 → 落点 AoE + 后摇（惩罚窗口）；近距离高频主攻
    private IEnumerator JumpInPlaceCo()
    {
        // 前摇（收缩）
        yield return new WaitForSeconds(normalJumpChargeTime);

        // 落点 = 原地（起跳落下，不追击）
        Vector2 landing = (Vector2)transform.position;

        // 落地预告（原地投影，提前可见）
        if (telegraphPrefab != null)
        {
            var tel = Instantiate(telegraphPrefab, landing, Quaternion.identity);
            Destroy(tel, telegraphTime);
        }

        // 原地小跳：垂直起跳，无水平位移
        float gravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale; // 实际重力加速度
        rb.linearVelocity = new Vector2(0f, normalJumpHeight);

        // 等落地（超时防御）
        bool leftGround = false;
        float timeout = 2f;
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

        // 落点 AoE 伤害（原地范围，可躲）
        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        if (t != null && Vector2.Distance(transform.position, t.position) <= landingRadius)
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

    // ==================== 动作 ② 大跳·从远处扑过来 ====================

    // 前摇 → 高弧线长滞空（给反应）→ 落点 AoE → 落地后摇
    private IEnumerator BigJumpCo()
    {
        // 前摇（更长，可读）
        yield return new WaitForSeconds(bigJumpChargeTime);

        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        Vector2 landing = t != null ? (Vector2)t.position : (Vector2)transform.position;

        // 落地预告（投影阴影，停留到落地，给足反应时间）
        if (telegraphPrefab != null)
        {
            var tel = Instantiate(telegraphPrefab, landing, Quaternion.identity);
            Destroy(tel, 2f);
        }

        // 高弧线：垂直起跳高 + 水平速度被上限限制（落地不会太远）
        float distX = landing.x - transform.position.x;
        float gravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale; // 实际重力加速度
        float airTime = gravity > 0.01f ? 2f * bigJumpHeight / gravity : 1.5f;
        rb.linearVelocity = new Vector2(Mathf.Clamp(distX / airTime, -jumpHorizSpeedCap, jumpHorizSpeedCap), bigJumpHeight);

        // 等落地（超时防御，滞空≈1.5s）
        bool leftGround = false;
        float timeout = 3.5f;
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

        // 落地后摇（惩罚窗口）：关接触伤害，玩家可安全输出
        contactEnabled = false;
        yield return new WaitForSeconds(landingRecovery);
        contactEnabled = true;
    }

    // ==================== 动作 ③ 冲刺 ====================

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

    // ==================== 动作 ④ 传送攻击 ====================

    // 超距 → 加长预告三阶段：原地闪光(可读) → 消失 → 头顶落点标记 → 砸落触碰
    private IEnumerator TeleportAttackCo()
    {
        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        if (t == null)
            yield break;

        // ① 原地闪光预告（明显可读，玩家看到它要传送）
        if (telegraphPrefab != null)
            Instantiate(telegraphPrefab, transform.position, Quaternion.identity);
        yield return new WaitForSeconds(teleportTelegraph);

        // ② 消失（隐藏渲染+碰撞，GameObject 保持 active 让协程继续）
        SetVisible(false);

        // ③ 玩家头顶落点标记（预告砸落位置，给反应时间）
        Vector2 overhead = (Vector2)t.position + Vector2.up * teleportHeight;
        if (telegraphPrefab != null)
            Instantiate(telegraphPrefab, overhead, Quaternion.identity);
        yield return new WaitForSeconds(teleportLandMark);

        // ④ 传送到头顶 + 自由落体砸落
        transform.position = overhead;
        SetVisible(true);
        bool fell = false;
        float fallTimeout = 1.2f;
        while (fallTimeout > 0f && !IsDead)
        {
            if (isOnGround == false)
                fell = true;
            else if (fell)
                break;
            yield return null;
            fallTimeout -= Time.deltaTime;
        }
        rb.linearVelocity = Vector2.zero;

        // 砸落触碰（头顶近身判定）
        if (t != null && Vector2.Distance(transform.position, t.position) < 2f)
        {
            var h = t.GetComponent<Entity_Health>();
            if (h != null)
                h.TakeDamage(h.GetMaxHP() * teleportSlamDamagePercent, 0f, ElementType.None, transform);
        }
        yield return new WaitForSeconds(0.5f); // 落地停顿
    }

    // 隐藏/显示：禁用/启用渲染+碰撞（保持 GameObject active，让协程继续运行）
    private void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            c.enabled = visible;
    }

    // ==================== 动作 ⑤ 召唤普通史莱姆 ====================

    private float lastSummonTime; // 上次召唤时间（冷却由调度器维护）

    private IEnumerator SummonCo()
    {
        if (slimePrefab == null)
            yield break;

        for (int i = 0; i < 2; i++)
        {
            Vector2 pos = (Vector2)transform.position + Random.insideUnitCircle * 1.5f;
            Instantiate(slimePrefab, pos, Quaternion.identity);
            yield return new WaitForSeconds(0.2f);
        }
    }
}
