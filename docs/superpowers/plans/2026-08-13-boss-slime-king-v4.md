# 史莱姆王 v4 重设计实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把史莱姆王重做成 v4：取消入场演出（最简流程）、重写 Boss 机制（常态/分裂狂暴/濒死隐身 三阶段 + 4 动作）、框架精简（删 ContactDamageArea/Boss_SlimeMinion）。

**Architecture:** BossEncounter 极简化（门/BGM/血条/开战/击杀/死亡）；Boss_SlimeKing 内置身体接触伤害 + 动作池（大跳/冲刺/传送攻击/召唤史莱姆）+ 阶段状态机（分裂 2 实例/存活者晋升/濒死隐身）；召唤物复用现有 Enemy_Slime。

**Tech Stack:** Unity 6000.4.8f1 / C# / DOTween / UGUI / TMP / Cinemachine(仅血条/场景)

## Global Constraints

- 注释 `//` 行内（不用 XML）；私有字段 camelCase；`if` 条件后必须换行
- 战斗表演用 scaled 时间；`SetUpdate(true)` 只留给纯 UI
- 玩家受击无敌 0.7s（已有）；"一次失误最多吃一下"
- Boss `canBeStunned=false`、`canKnockbacked=false`（预制体）
- 设计文档：`design/gdd/boss-slime-king.md`（v4）
- 无自动化测试基建；验证 = Unity Play 手动（项目惯例）

---

### Task 1: 清理废弃文件（ContactDamageArea / Boss_SlimeMinion）

**Files:**
- Delete: `Assets/Scripts/Character/Entity/ContactDamageArea.cs`（+ .meta）
- Delete: `Assets/Scripts/Character/Enemy/Boss/Boss_SlimeMinion.cs`（+ .meta）
- Delete: `Assets/Scripts/Character/Enemy/Boss/BossMinionRegistry.cs`（+ .meta；新设计召唤普通史莱姆，不再登记迷你王）
- Delete: `Assets/Prefab/实体/BOSS_SlimeMinion.prefab`（+ .meta）

**Interfaces:**
- Consumes: 无（纯删除）
- Produces: 干净的脚本目录；后续 Boss 不再引用这两个类型

- [ ] **Step 1: 删除文件**

用 Git 删除（保留历史）：
```bash
git rm "Assets/Scripts/Character/Entity/ContactDamageArea.cs" "Assets/Scripts/Character/Entity/ContactDamageArea.cs.meta"
git rm "Assets/Scripts/Character/Enemy/Boss/Boss_SlimeMinion.cs" "Assets/Scripts/Character/Enemy/Boss/Boss_SlimeMinion.cs.meta"
git rm "Assets/Prefab/实体/BOSS_SlimeMinion.prefab" "Assets/Prefab/实体/BOSS_SlimeMinion.prefab.meta"
```

- [ ] **Step 2: 清除引用**

grep 确认没有代码引用 `ContactDamageArea` / `Boss_SlimeMinion`：
```bash
grep -rn "ContactDamageArea\|Boss_SlimeMinion" Assets/Scripts --include="*.cs"
```
若 `Boss_SlimeKing.cs` 引用了它们（`minionPrefab`/`minionRegistry`），在 Task 3/4 一并移除。此时若有残留引用，先在本任务删干净（Boss_SlimeKing 的 `minionPrefab`→Boss_SlimeMinion 引用改 `Enemy_Slime.prefab`，`minionRegistry` 相关逻辑删掉）。

- [ ] **Step 3: 提交**

```bash
git commit -m "refactor: 删除 ContactDamageArea/Boss_SlimeMinion（v4 重设计）"
```

---

### Task 2: BossEncounter 极简化（删开场演出）

**Files:**
- Modify: `Assets/Scripts/Character/Enemy/Boss/BossEncounter.cs`（重写为极简版）

**Interfaces:**
- Consumes: `Boss_SlimeKing.BeginFight()`、`UI_BossHealthBar.BindBoss/Unbind`、`AudioManager.PushBgm/PopBgm`、`Portal.SetLocked`、`BossMinionRegistry`（可删）
- Produces: 极简开场——锁门 → BGM → 血条 → 开战 → 击杀/死亡

- [ ] **Step 1: 重写 BossEncounter**

整文件替换为：

```csharp
using System;
using System.Collections;
using UnityEngine;

// 公共 Boss 编排（极简版，v4）——锁门→BGM→血条→开战→击杀/死亡
// 无任何入场演出；玩家进房立刻能动、Boss 直接开打
public class BossEncounter : MonoBehaviour
{
    public enum EncounterState { Idle, Fighting, Victory }

    [Header("引用（场景接线）")]
    [SerializeField] private GameObject bossPrefab;             // Boss 预制体
    [SerializeField] private Transform spawnPoint;              // Boss 生成位置
    [SerializeField] private Portal arrivalPortal;              // 到达传送门（锁定）
    [SerializeField] private GameObject exitPortal;             // 出口传送门（胜利激活）
    [SerializeField] private AudioClip bossBgm;                 // Boss 曲
    [SerializeField] private UI_BossHealthBar healthBar;        // 顶部血条

    public EncounterState State { get; private set; } = EncounterState.Idle;
    public event Action VictoryEvent; // 奖励挂载点

    private Boss_SlimeKing boss;
    private Entity_Health bossHealth;
    private bool defeatedFlag;

    private void Awake()
    {
        if (spawnPoint == null) spawnPoint = transform;
    }

    private void Start()
    {
        if (healthBar == null)
            healthBar = FindAnyObjectByType<UI_BossHealthBar>(FindObjectsInactive.Include);
        StartCoroutine(StartFight());
    }

    private void OnDestroy()
    {
        AudioManager.Instance?.PopBgm(0f); // 防 BGM 跨场景残留
    }

    private IEnumerator StartFight()
    {
        State = EncounterState.Fighting;

        // 锁到达传送门（不能回头）
        if (arrivalPortal != null)
            arrivalPortal.SetLocked(true);

        // 切 Boss BGM
        AudioManager.Instance?.PushBgm(bossBgm, 0.5f);

        // 生成 Boss + 绑血条 + 开战
        boss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation).GetComponent<Boss_SlimeKing>();
        bossHealth = boss.GetComponent<Entity_Health>();
        if (healthBar != null)
            healthBar.BindBoss(bossHealth, boss.enemyName);
        boss.BeginFight();

        // 监听 Boss 死亡（最后实例死亡→胜利；玩家死亡由现有死亡系统处理）
        while (State == EncounterState.Fighting)
        {
            if (FindAnyObjectByType<Boss_SlimeKing>() == null)
            {
                OnBossDead();
                break;
            }
            yield return null;
        }
    }

    private void OnBossDead()
    {
        State = EncounterState.Victory;
        if (healthBar != null) healthBar.Unbind();
        AudioManager.Instance?.PopBgm(0.8f);
        if (exitPortal != null) exitPortal.SetActive(true); // 出口传送门出现
        defeatedFlag = true;
        VictoryEvent?.Invoke();
    }
}
```

> 注：分裂后有多实例时，`FindAnyObjectByType<Boss_SlimeKing>() == null` 即"最后实例死亡"→胜利，天然支持多体。

- [ ] **Step 2: 编辑器验证（半场景）**

进 BOSS 场景 Play：到达门锁定、BGM 切、血条满格、Boss 生成即开打（无黑边/名字/输入锁）。击杀后出口门激活。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Character/Enemy/Boss/BossEncounter.cs
git commit -m "feat: BossEncounter 极简化（删开场演出）"
```

---

### Task 3: Boss_SlimeKing 身体接触伤害（内置）+ 基础

**Files:**
- Modify: `Assets/Scripts/Character/Enemy/Boss/Boss_SlimeKing.cs`

**Interfaces:**
- Consumes: `Entity_Health.TakeDamage`、身体 Trigger Collider2D（子物体 ContactArea）
- Produces: `contactEnabled`（落地后摇可关）、`OnTriggerStay2D` 接触伤害

- [ ] **Step 1: 加接触伤害字段与方法**

在类内加：

```csharp
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
```

- [ ] **Step 2: 编辑器验证**

Play：玩家碰到 Boss 身体 → 每 1s 掉 12%，不秒杀；落地后摇期间（contactEnabled=false）不触发。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Character/Enemy/Boss/Boss_SlimeKing.cs
git commit -m "feat: Boss 身体接触伤害内置（每源冷却）"
```

---

### Task 4: Boss_SlimeKing 动作池（大跳/冲刺/传送攻击/召唤史莱姆）

**Files:**
- Modify: `Assets/Scripts/Character/Enemy/Boss/Boss_SlimeKing.cs`

**Interfaces:**
- Consumes: `GetPlayerReference()`（Enemy 基类，已兜底找玩家）、`Enemy_Slime.prefab`（召唤物）
- Produces: `BeginFight()` 启动动作池；四个动作

- [ ] **Step 1: 动作池调度器 + 四动作**

替换/重构动作池。核心调度器（每轮按状态选动作）：

```csharp
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

    private Coroutine schedulerCo;
    private bool isFighting;
    private Transform playerTarget; // 锁定的玩家

    // BossEncounter 调用：开战
    public void BeginFight()
    {
        if (isFighting) return;
        isFighting = true;
        stateMachine.SwitchOffStateMachine(); // 冻结 FSM
        schedulerCo = StartCoroutine(SchedulerLoop());
    }

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
```

**大跳触碰**（落地预告 + 落点 AoE + 后摇）：

```csharp
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
            if (isOnGround == false) leftGround = true;
            else if (leftGround) break;
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
```

**冲刺**（无提示，锁定方向横冲）：

```csharp
    private IEnumerator DashCo()
    {
        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        if (t == null) yield break;

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
```

**传送攻击**（超距 → 头顶落下）：

```csharp
    private IEnumerator TeleportAttackCo()
    {
        Transform t = playerTarget != null ? playerTarget : GetPlayerReference();
        if (t == null) yield break;

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
```

**召唤普通史莱姆**：

```csharp
    private float lastSummonTime;

    private IEnumerator SummonCo()
    {
        if (Time.time - lastSummonTime < summonCooldown)
            yield break;
        lastSummonTime = Time.time;

        if (slimePrefab != null)
            for (int i = 0; i < 2; i++)
            {
                Vector2 pos = (Vector2)transform.position + Random.insideUnitCircle * 1.5f;
                Instantiate(slimePrefab, pos, Quaternion.identity);
                yield return new WaitForSeconds(0.2f);
            }
    }
```

- [ ] **Step 2: 编辑器验证**

Play：Boss 大跳（落点预告+后摇窗口）、冲刺（无提示横冲）、超距传送头顶落下、周期性召唤普通史莱姆（会分裂干扰）。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Character/Enemy/Boss/Boss_SlimeKing.cs
git commit -m "feat: Boss 动作池（大跳/冲刺/传送攻击/召唤史莱姆）"
```

---

### Task 5: Boss_SlimeKing 阶段状态机（分裂狂暴 / 濒死隐身）

**Files:**
- Modify: `Assets/Scripts/Character/Enemy/Boss/Boss_SlimeKing.cs`

**Interfaces:**
- Consumes: Task 4 的动作池、`Entity_Health`（血量/死亡）
- Produces: 三阶段；分裂出第 2 实例；存活者晋升 + 僵直 + 3 段冲刺；濒死隐身回血

- [ ] **Step 1: 阶段字段与分裂/隐身逻辑**

```csharp
    // === 阶段 ===
    public enum BossPhase { Normal, Split, Stagger, Stealth }
    public BossPhase phase = BossPhase.Normal;
    public bool hasSplit;             // 是否已分裂（防重复）
    public Boss_SlimeKing sibling;    // 分裂的另一只
    public bool isPrimary = true;     // 主实例（血条绑定/存活者晋升）
    public event Action<Boss_SlimeKing> OnPrimaryChanged; // 血条重绑

    [Header("分裂")]
    [SerializeField] private float splitThreshold = 0.3f;   // 分裂阈值
    [SerializeField] private float splitScale = 0.7f;       // 分裂体缩放
    [SerializeField] private float survivorHealPercent = 0.29f; // 存活者回血值
    [SerializeField] private float staggerDuration = 1.5f;  // 僵直时长

    [Header("濒死隐身")]
    [SerializeField] private float stealthThreshold = 0.01f; // 濒死阈值
    [SerializeField] private float stealthHealRate = 0.02f; // 隐身回血速度（%/s）
    [SerializeField] private float stealthHealCap = 0.29f;  // 回血封顶（<分裂阈值）
    [SerializeField] private GameObject slimePrefab;         // 召唤物（隐身召唤）

    private Entity_Health health; // 自身生命
    private Coroutine stealthCo;

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<Entity_Health>();
        // FSM 状态构造（同 Enemy_Slime 模式）
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState);
        stateMachine.SwitchOffStateMachine();
    }

    // 每帧检查阶段切换（在 BeginFight 调度器循环里每轮调）
    private void UpdatePhase()
    {
        if (IsDead || phase == BossPhase.Stealth) return;

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

    // 分裂：生成第 2 实例，各半血
    private void Split()
    {
        hasSplit = true;
        phase = BossPhase.Split;
        var clone = Instantiate(gameObject, transform.position, transform.identity).GetComponent<Boss_SlimeKing>();
        clone.isPrimary = false;
        clone.hasSplit = true;
        clone.sibling = this;
        sibling = clone;
        clone.transform.localScale = transform.localScale * splitScale;
        // 各半血
        float half = health.GetCurrentHP() / 2f;
        health.SetCurrentHP(half);
        clone.health.SetCurrentHP(half);
        clone.BeginFight(); // 克隆也开打
    }

    // 死亡处理：若兄弟存活 → 存活者晋升（回血/僵直/3段冲刺）
    public override void EntityDead()
    {
        isFighting = false;
        if (schedulerCo != null) StopCoroutine(schedulerCo);
        if (stealthCo != null) StopCoroutine(stealthCo);
        stateMachine.canChangeSate = true;

        if (sibling != null && sibling != null && sibling.IsDead == false)
            sibling.OnSiblingDied(); // 存活者处理
        else
            base.EntityDead(); // 最后实例死亡 → 胜利
    }

    // 兄弟死亡 → 存活者：回血 29% → 僵直 → 3段冲刺 → 恢复
    private void OnSiblingDied()
    {
        if (isPrimary == false)
        {
            isPrimary = true;
            OnPrimaryChanged?.Invoke(this); // BossEncounter 重绑血条
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
        // 连续 3 段快速冲刺
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(DashCo());
        }
        phase = BossPhase.Normal; // 恢复常态（不再分裂）
    }

    // 濒死隐身：半透明、无法移动攻击、召唤史莱姆、回血
    private void StartStealth()
    {
        phase = BossPhase.Stealth;
        if (schedulerCo != null) StopCoroutine(schedulerCo);
        stealthCo = StartCoroutine(StealthCo());
    }

    private IEnumerator StealthCo()
    {
        // 半透明
        var sr = GetComponentInChildren<SpriteRenderer>();
        Color c = sr.color;
        sr.color = new Color(c.r, c.g, c.b, 0.3f);
        contactEnabled = false; // 隐身不攻击

        // 回血（封顶 29%）+ 召唤史莱姆
        while (health.GetHealthPercent() < stealthHealCap && !IsDead)
        {
            health.IncreaseHP(health.GetMaxHP() * stealthHealRate * Time.deltaTime);
            yield return null;
        }

        // 退出隐身（回血到封顶 or 所有史莱姆被杀——由外部判定杀光）
        sr.color = new Color(c.r, c.g, c.b, 1f);
        contactEnabled = true;
        phase = BossPhase.Normal;
        isFighting = true;
        schedulerCo = StartCoroutine(SchedulerLoop());
    }
```

> 注：`OnPrimaryChanged` 事件让 BossEncounter 在存活者晋升时重绑血条（`BossEncounter.StartFight` 里订阅）。

- [ ] **Step 2: 编辑器验证**

Play：HP<30% 分裂成 2 个 → 杀一只 → 存活者回血 29% + 僵直 + 3 段冲刺 → 不再分裂；HP<1% 隐身回血召唤 → 杀光史莱姆退出。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Character/Enemy/Boss/Boss_SlimeKing.cs
git commit -m "feat: Boss 阶段状态机（分裂狂暴/濒死隐身）"
```

---

### Task 6: 场景清理 + 接线（删开场 UI / 调整点 / 删迷你王引用）

**Files:**
- Modify: `Assets/Scenes/BOSS.unity`

**Interfaces:**
- Consumes: Task 1-5 的代码
- Produces: 干净场景——无黑边/Boss名/IntroCamera；BossEncounter 极简接线

- [ ] **Step 1: 场景清理（编辑器/MCP）**
  - 删除 `BossHUD/BlackBars`、`BossHUD/BossName`（开场 UI 不再用）
  - 删除残留 `IntroCamera`（若有）
  - BossEncounter 引用更新：删 `blackScreen/bossNameText/entryPoint`（新脚本无这些字段）；确认 `spawnPoint` 指向地面战斗位、`bossPrefab`→BOSS_Slime
  - 确认场景无 `Boss_SlimeMinion` 引用（删预制体后）
  - 存档点、到达/出口传送门保留

- [ ] **Step 2: 验证**

从 level1 传送门进 BOSS 场景 Play：无开场演出，Boss 直接打；击杀出口门激活；死亡读档。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scenes/BOSS.unity
git commit -m "feat: Boss 房场景清理（删开场UI/迷你王，极简接线）"
```

---

### Task 7: 集成验证（对照 spec AC）

**Files:**
- 设计文档 `design/gdd/boss-slime-king.md` §Acceptance Criteria

- [ ] **Step 1: 跑 AC**
  - 进入 → 锁门/BGM/血条/Boss 直接打（玩家立刻能动）
  - 常态四动作（大跳有预告/冲刺无提示/超距传送/召唤史莱姆可分裂）
  - HP<30% 分裂 2 个各半血 → 杀一存活者回 29% → 僵直 → 3 段冲刺 → 不再分裂
  - HP<1% 隐身回血（最多 29%）→ 杀光史莱姆退出
  - 击杀 → 血条隐藏/BGM 恢复/出口门
  - 死亡 → 读档回存档点 → 重进重置

- [ ] **Step 2: 记录未达标项 → 下个迭代

## Self-Review

- **Spec coverage**: 机制（三阶段+四动作）→ Task 4/5；流程（极简）→ Task 2；框架（删两个文件+接触内置）→ Task 1/3；场景清理 → Task 6；AC → Task 7。无缺口。
- **Placeholder scan**: 无 TBD/TODO；冲刺速度/距离为 Inspector 调参（spec 明确留空手动调）。
- **Type consistency**: `BeginFight()`/`SchedulerLoop()`/`UpdatePhase()`/`Split()`/`EntityDead()`/`OnSiblingDied()`/`StartStealth()` 一致；`OnPrimaryChanged` 事件供 BossEncounter 重绑血条；`contactEnabled` 接触开关 Task 3 定义、Task 4/5 使用。
