# 史莱姆王 Boss（King Slime）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现第一个 Boss「史莱姆王」+ 可复用的公共 Boss 系统（BossEncounter 编排 / 顶部血条 / BGM 切换 / 相机出场 / 门锁竞技场）。

**Architecture:** 通用层（玩家受击无敌帧、AudioManager BGM push/pop、GameInput 控制锁、UI_BossHealthBar、BossEncounter 编排）与 Boss 专属层（Boss_SlimeKing 动作池调度器 + Boss_SlimeMinion + ContactDamageArea）分层。Boss 继承 `Enemy`，出生即冻结 FSM，`BeginFight()` 激活动作池协程调度器。

**Tech Stack:** Unity 6000.4.8f1 / C# / Cinemachine / DOTween / UGUI / TMP

## Global Constraints

- 代码注释：`//` 行内注释（不用 `/// <summary>`），私有字段 camelCase、公开 PascalCase
- `if` 条件后必须换行（即使单行）
- 战斗表演（调度器协程、Boss 位移/缩放 tween、落点预告）**全用 scaled 时间，一律不 `SetUpdate(true)`**；`SetUpdate(true)` 只留给纯 UI（血条光泽、横幅）
- 落点预告窗口用 `Time.time` 手动计时（`while (Time.time < t)`），不用 `WaitForSeconds` 墙钟
- Boss `canKnockbacked=false`、`canBeStunned=false`（预制体防御性确认）
- 所有伤害走 `Entity_Health.TakeDamage`；"一次失误最多吃一下"由玩家受击无敌帧 0.7s + 每源冷却 1s 保证
- 设计文档：`design/gdd/boss-slime-king.md`（v2.1），实现前通读

---

### Task 1: 玩家受击无敌帧（Entity_Health + Player）

**Files:**
- Modify: `Assets/Scripts/Character/Entity/Entity_Health.cs`
- Modify: `Assets/Scripts/Character/Player/Player.cs`（Start 内设 0.7s）

**Interfaces:**
- Produces: `Entity_Health.SetInvincibleDuration(float)` + 受击无敌帧自动生效

- [ ] **Step 1: Entity_Health 加字段与检查**

在 `Entity_Health.cs` 的 `[Header("击退设置")]` 之后（`canKnockbacked` 附近）加：

```csharp
    [Header("受击无敌帧")]
    [SerializeField] protected float invincibleDuration = 0f; // 受击后无敌时长（0=关闭；玩家设 0.7s）
    protected float invincibleUntil; // 无敌结束时间（Time.time）

    // 代码设置受击无敌帧时长（玩家在 Start 调用）
    public void SetInvincibleDuration(float duration) => invincibleDuration = duration;
```

- [ ] **Step 2: TakeDamage 加无敌帧门**

`TakeDamage` 方法内，`AttackEvaded()` 检查之后、取攻击者之前插入：

```csharp
        if (AttackEvaded())
        {
            Debug.Log($"{gameObject.name} 躲避了攻击");
            return false;
        }

        // 受击无敌帧：期间免疫所有伤害（"一次失误最多吃一下"的兜底）
        if (invincibleDuration > 0f && Time.time < invincibleUntil)
            return false;
```

`ReduceHP` 调用之后、`lastDamageTaken` 赋值之前插入：

```csharp
        TakeKnockBack(damageDealer, physicalDamageTaken);
        ReduceHP(physicalDamageTaken, elementalDamageTaken, element, isCrit);

        // 命中后进入受击无敌（防止多源同帧叠加）
        if (invincibleDuration > 0f)
            invincibleUntil = Time.time + invincibleDuration;

        lastDamageTaken = physicalDamageTaken + elementalDamageTaken;
```

- [ ] **Step 3: Player.Start 设置 0.7s**

`Player.cs` 的 `protected override void Start()` 内 `base.Start()` 之后加：

```csharp
        base.Start();
        // 受击无敌帧 0.7s（通用层，全游戏受益；敌人 invincibleDuration 保持 0 不启用）
        health?.SetInvincibleDuration(0.7f);
```

- [ ] **Step 4: 编辑器验证**

进 Unity → Play。进入场景，让一只普通史莱姆攻击玩家：受击后约 0.7s 内再次受击不掉血；连续受击只掉一次。确认敌人（非玩家）连续受击仍正常掉血（无敌帧未生效）。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Character/Entity/Entity_Health.cs Assets/Scripts/Character/Player/Player.cs
git commit -m "feat: 玩家受击无敌帧 0.7s"
```

---

### Task 2: AudioManager BGM 切换 API

**Files:**
- Modify: `Assets/Scripts/AudioSystem/AudioManager.cs`

**Interfaces:**
- Produces: `PlayBgm(AudioClip, crossfade)` / `PopBgm(crossfade)` / `GetCurrentBgmClip()`（BossEncounter 用）

- [ ] **Step 1: 加第二 BGM 源 + 栈字段**

字段区（`private AudioSource bgmSource;` 附近）加：

```csharp
    private AudioSource bgmSourceB;          // 第二 BGM 源（crossfade ping-pong）
    private AudioSource activeBgmSource;     // 当前实际播放的 BGM 源
    private readonly Stack<AudioClip> bgmStack = new(); // BGM 上下文栈（push/pop 恢复，防与区域音乐冲突）
```

`CreateSources()` 内加 `bgmSourceB = AddSource();`，`Awake()` 的 `PlayBgm()` 调用后加 `activeBgmSource = bgmSource;`。

- [ ] **Step 2: 加公开 BGM API**

`UpdateBgmVolume()` 之后加：

```csharp
    // 当前正在播放的 BGM（BossEncounter 缓存/断言用）
    public AudioClip GetCurrentBgmClip() => activeBgmSource != null ? activeBgmSource.clip : BGM.bgmClip;

    // 推入 Boss 曲（缓存上一曲到栈，供 PopBgm 恢复）
    public void PushBgm(AudioClip clip, float crossfade = 1f)
    {
        if (clip == null) return;
        AudioClip prev = GetCurrentBgmClip();
        if (prev != null && prev != clip)
            bgmStack.Push(prev);
        StartCoroutine(CrossfadeTo(clip, crossfade));
    }

    // 弹出恢复上一曲（Boss 战结束/玩家死亡）
    public void PopBgm(float crossfade = 1f)
    {
        if (bgmStack.Count == 0) return;
        StartCoroutine(CrossfadeTo(bgmStack.Pop(), crossfade));
    }

    // 双源交叉淡入：新曲强拍落在旧曲淡出的同帧（落地冲击作遮罩）
    private IEnumerator CrossfadeTo(AudioClip clip, float crossfade)
    {
        AudioSource oldSource = activeBgmSource;
        AudioSource newSource = (oldSource == bgmSource) ? bgmSourceB : bgmSource;

        newSource.clip = clip;
        newSource.loop = true;
        newSource.volume = 0f;
        newSource.timeSamples = 0;
        newSource.Play();

        float t = 0f;
        while (t < crossfade)
        {
            t += Time.unscaledDeltaTime; // 音频不受 timeScale 影响，渐变用真实时间
            float k = Mathf.Clamp01(t / crossfade);
            if (oldSource != null) oldSource.volume = masterVolume * BGM.bgmVolume * (1f - k);
            newSource.volume = masterVolume * BGM.bgmVolume * k;
            yield return null;
        }
        if (oldSource != null) oldSource.Stop();
        activeBgmSource = newSource;
    }
```

`UpdateBgmVolume()` 改为写 `activeBgmSource`：

```csharp
    private void UpdateBgmVolume()
    {
        if (activeBgmSource != null) activeBgmSource.volume = masterVolume * BGM.bgmVolume;
    }
```

- [ ] **Step 3: 编辑器验证**

Play → 在 Inspector 找一个 AudioManager 的 `GetCurrentBgmClip` 返回值；调用 `PushBgm(某clip)` 听淡入新曲，`PopBgm()` 淡回原曲。无爆音、无死音缝隙。

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/AudioSystem/AudioManager.cs
git commit -m "feat: AudioManager BGM 双源 crossfade + push/pop 栈"
```

---

### Task 3: GameInput 玩家控制锁 + CinemaScreenShake 通用震屏

**Files:**
- Modify: `Assets/Scripts/InputSystem/GameInput.cs`
- Modify: `Assets/Scripts/VFX/CinemaScreenShake.cs`

**Interfaces:**
- Produces: `GameInput.IsPlayerControlBlocked`（BossEncounter Intro 置位）、`CinemaScreenShake.ShakeWith(Vector3, float)`

- [ ] **Step 1: GameInput 加控制锁**

`GameInput.cs` 的 `IsGameBlocked` 处改为：

```csharp
    public static bool IsPlayerControlBlocked; // Boss 出场等锁定玩家操作（BossEncounter 置位）

    public static bool IsGameBlocked => Networking.UI_Chat.IsChatFocused || UIManager.IsAnyPanelOpen || IsPlayerControlBlocked;
```

- [ ] **Step 2: CinemaScreenShake 加通用震屏**

`ShakeScreenForJumpAttack()` 之后加：

```csharp
    // 通用震屏：Boss 落地等任意来源（不依赖玩家朝向；Boss 身上挂 CinemaScreenShake 或单独 ImpulseSource）
    public void ShakeWith(Vector3 velocity, float multiplier = 1f)
    {
        if (screenShake == null) return;
        screenShake.m_DefaultVelocity = velocity * multiplier;
        screenShake.GenerateImpulse();
    }
```

- [ ] **Step 3: 编辑器验证**

Play → 调 `ShakeWith(new Vector3(3,-4,0))` 相机震一下；设 `IsPlayerControlBlocked=true` 后玩家无法移动/跳跃（重力仍在），置 false 恢复。

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/InputSystem/GameInput.cs Assets/Scripts/VFX/CinemaScreenShake.cs
git commit -m "feat: GameInput 玩家控制锁 + CinemaScreenShake 通用震屏"
```

---

### Task 4: PlayerSpawner 双 vcam 兼容

**Files:**
- Modify: `Assets/Scripts/Character/Player/PlayerSpawner.cs`（`BindCameraToPlayer`）

**Interfaces:**
- Consumes: follow vcam 命名含 "Follow"（场景约定）；Intro vcam 命名含 "Intro" 且 priority=0

- [ ] **Step 1: 按命名约定绑定 follow vcam**

`BindCameraToPlayer()` 内 `var vcam = FindAnyObjectByType<CinemachineVirtualCamera>();` 替换为：

```csharp
        // 双 vcam 兼容：按命名约定选 follow vcam（Intro vcam 名含 "Intro" 且 priority=0，不绑定）
        var vcams = FindObjectsByType<CinemachineVirtualCamera>();
        var vcam = System.Array.Find(vcams, v => v != null && v.name.Contains("Follow"));
        if (vcam == null)
            vcam = System.Array.Find(vcams, v => v != null && v.priority > 0);
```

- [ ] **Step 2: 编辑器验证**

场景加一个命名含 "Intro"、priority=0 的 vcam 后重进场景，确认 follow vcam 仍正确跟随玩家、Intro vcam 不跟随。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Character/Player/PlayerSpawner.cs
git commit -m "feat: PlayerSpawner 双 vcam 兼容"
```

---

### Task 5: ContactDamageArea（史莱姆专属接触伤害）

**Files:**
- Create: `Assets/Scripts/Character/Entity/ContactDamageArea.cs`

**Interfaces:**
- Produces: `ContactDamageArea`（挂到身体触发碰撞体；`contactDamage`/`hitCooldown` Inspector 配）
- Consumes: `Entity_Health.TakeDamage`（Task 1 后可用无敌帧）

- [ ] **Step 1: 写组件**

```csharp
using UnityEngine;

// 身体接触伤害（史莱姆专属）——重叠检测 + 每源冷却，防"站进身体逐帧秒杀"
// 与玩家受击无敌帧互补：每源冷却管"同源反复刷"，无敌帧管"多源叠加"
[RequireComponent(typeof(Collider2D))]
public class ContactDamageArea : MonoBehaviour
{
    [SerializeField] private float contactDamage = 0.12f;   // 接触伤害（目标 MaxHP 的百分比，0.12=12%）
    [SerializeField] private float hitCooldown = 1f;        // 每源冷却（秒）
    [SerializeField] private bool damagePercentOfMaxHp = true; // true=按目标 MaxHP 百分比

    private float lastHitTime; // 上次造成伤害的时间（每源冷却计时）

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time - lastHitTime < hitCooldown)
            return; // 冷却内不重复触发

        if (other.TryGetComponent<Entity_Health>(out var health) == false)
            return;

        float dmg = damagePercentOfMaxHp ? health.GetMaxHP() * contactDamage : contactDamage;
        health.TakeDamage(dmg, 0f, ElementType.None, transform);
        lastHitTime = Time.time;
    }
}
```

- [ ] **Step 2: 编辑器验证**

给一个临时史莱姆身体加 Trigger Collider2D + ContactDamageArea（12%、1s）。玩家站进身体：每秒掉 12%，不会逐帧秒杀；配合 Task 1 无敌帧，落地+接触同帧只结算一次。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Character/Entity/ContactDamageArea.cs
git commit -m "feat: ContactDamageArea 身体接触伤害（每源冷却）"
```

---

### Task 6: BossMinionRegistry（迷你王登记处）

**Files:**
- Create: `Assets/Scripts/Character/Enemy/Boss/BossMinionRegistry.cs`

**Interfaces:**
- Produces: `Register(Boss_SlimeMinion)` / `Unregister(Boss_SlimeMinion)` / `Count` / `Clear()`
- Consumed by: Boss_SlimeKing（召唤登记）、BossEncounter（Victory/Reset 清理）

- [ ] **Step 1: 写注册表**

```csharp
using System.Collections.Generic;
using UnityEngine;

// Boss 迷你王登记处——生成登记/死亡注销/批量销毁；Victory/场景卸载时 Clear()
// 注：用基类 Enemy 而非 Boss_SlimeMinion（迷你王是 Enemy 子类，Task 8 才创建，解耦保证本文件可独立编译）
public class BossMinionRegistry : MonoBehaviour
{
    private readonly List<Enemy> minions = new();

    public int Count => minions.Count; // 场上存活迷你王数（召唤上限判定用）

    // Boss 召唤时登记
    public void Register(Enemy minion)
    {
        if (minion != null && minions.Contains(minion) == false)
            minions.Add(minion);
    }

    // 迷你王死亡时注销
    public void Unregister(Enemy minion)
    {
        minions.Remove(minion);
    }

    // 批量销毁全部（Victory/Reset 调用，防卡死个体占满召唤上限）
    public void Clear()
    {
        foreach (var m in minions)
            if (m != null)
                Destroy(m.gameObject);
        minions.Clear();
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add Assets/Scripts/Character/Enemy/Boss/BossMinionRegistry.cs
git commit -m "feat: BossMinionRegistry 迷你王统一管理"
```

---

### Task 7: UI_BossHealthBar（顶部大血条）

**Files:**
- Create: `Assets/Scripts/UI/UI_Boss/UI_BossHealthBar.cs`

**Interfaces:**
- Produces: `BindBoss(Entity_Health, string)` / `Unbind()`
- Consumed by: BossEncounter（Fighting 绑定 / Victory 解绑）

- [ ] **Step 1: 写血条组件**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boss 顶部大血条（公共）——绑定任意 Boss 的 Entity_Health，显示名称+血量填充
// Canvas 结构由 UI 手动搭建（名称 Text + Fill Image[type=Filled] + CanvasGroup），挂在持久 HUD 层级
public class UI_BossHealthBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;   // Boss 名称文本
    [SerializeField] private Image fillImage;             // 血条填充（Image.type = Filled, fillMethod = Horizontal）
    [SerializeField] private CanvasGroup canvasGroup;     // 整条显隐/淡入控制

    private Entity_Health boundHealth; // 当前绑定的 Boss 生命

    // 绑定 Boss：订阅生命更新，立即刷新到初始值
    public void BindBoss(Entity_Health health, string bossName)
    {
        Unbind();
        boundHealth = health;
        if (boundHealth != null)
            boundHealth.OnHealthUpdate += OnHealthUpdated;
        if (nameText != null)
            nameText.text = bossName;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        OnHealthUpdated(); // 立即刷新（100%）
    }

    // 解绑：Boss 死亡（OnEntityDead）时调用，防销毁后迟到回调 MissingReferenceException
    public void Unbind()
    {
        if (boundHealth != null)
            boundHealth.OnHealthUpdate -= OnHealthUpdated;
        boundHealth = null;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnHealthUpdated()
    {
        if (boundHealth == null || fillImage == null)
            return;
        fillImage.fillAmount = boundHealth.GetHealthPercent();
    }
}
```

- [ ] **Step 2: 编辑器验证**

手动搭 Canvas（名称 + Filled Image + CanvasGroup，`raycastTarget=false`）→ 挂组件拖引用 → 脚本调 `BindBoss(某Boss的Entity_Health, "史莱姆王")` → 血条满格、名称显示；扣血后填充下降；`Unbind()` 隐藏。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/UI/UI_Boss/UI_BossHealthBar.cs
git commit -m "feat: UI_BossHealthBar 顶部大血条"
```

---

### Task 8: Boss_SlimeMinion（迷你王）

**Files:**
- Create: `Assets/Scripts/Character/Enemy/Boss/Boss_SlimeMinion.cs`

**Interfaces:**
- Produces: `InitMinion(BossMinionRegistry)`；`OnMinionDestroyed`（BossEncounter Reset 判断用可选）
- Consumes: `Enemy`、`ContactDamageArea`（Task 5）、`BossMinionRegistry`（Task 6）

- [ ] **Step 1: 写迷你王**

```csharp
using UnityEngine;

// 迷你史莱姆王——简单跳向玩家 + 接触伤害；不分裂不召唤（独立小怪，不复用 Enemy_Slime）
public class Boss_SlimeMinion : Enemy
{
    [SerializeField] private float hopInterval = 1.2f;   // 跳跃间隔（秒）
    [SerializeField] private float hopForce = 6f;        // 跳跃力度（y 初速）
    [SerializeField] private float hopHorizontalRatio = 0.5f; // 横向速度 = hopForce × 此值

    private BossMinionRegistry registry; // 所属登记处（Boss 召唤时注入）
    private float nextHopTime;           // 下次跳跃时间
    private bool hopAIEnabled;           // 是否激活跳跃 AI

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
        stateMachine.Initialize(idleState);
        stateMachine.SwitchOffStateMachine(); // 冻结 FSM（迷你王用自定义跳跃 AI，不用 FSM 追击）
        hopAIEnabled = true;
    }

    // Boss 召唤时注入登记处；登记后死亡注销
    public void InitMinion(BossMinionRegistry reg)
    {
        registry = reg;
        registry?.Register(this);
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead || hopAIEnabled == false)
            return;
        if (Time.time < nextHopTime)
            return;

        // 每 hopInterval 朝玩家方向跳一下
        Transform target = player;
        if (target == null)
            target = GetPlayerReference();
        if (target != null)
        {
            int dir = target.position.x > transform.position.x ? 1 : -1;
            HandleFlip(dir);
            rb.linearVelocity = new Vector2(dir * hopForce * hopHorizontalRatio, hopForce);
        }
        nextHopTime = Time.time + hopInterval;
    }

    public override void EntityDead()
    {
        registry?.Unregister(this); // 死亡注销，释放召唤上限
        base.EntityDead();
    }
}
```

- [ ] **Step 2: 编辑器验证**

手动放一只迷你王 + ContactDamageArea（5~8%）。它会每 1.2s 跳向玩家；接触掉血 5~8%；死亡后从 Registry 注销。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Character/Enemy/Boss/Boss_SlimeMinion.cs
git commit -m "feat: Boss_SlimeMinion 迷你王（跳向玩家+接触伤害）"
```

---

### Task 9: Boss_SlimeKing（动作池调度器）

**Files:**
- Create: `Assets/Scripts/Character/Enemy/Boss/Boss_SlimeKing.cs`

**Interfaces:**
- Produces: `BeginFight()`（BossEncounter Intro→Fighting 调用）、`GetMinionCount()`、`OnLanded` 事件（震屏由 BossEncounter 订阅）
- Consumes: `Enemy`、`ContactDamageArea`、`BossMinionRegistry`、`Entity_Health`（血条绑定）

- [ ] **Step 1: 写史莱姆王核心**

```csharp
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
```

- [ ] **Step 2: 编辑器验证（临时场景）**

放一只 Boss_SlimeKing + 迷你王预制体 + 身体 ContactDamageArea。手动调 `BeginFight()`：开始跳砸（有蓄力前摇、落点有预告可选）、召唤迷你王（2 只、上限 3）、拉远玩家触发传送、HP<30% 进狂暴（召唤变 3 只、落地后摇变短）。击杀后正常播死亡动画（调度器不卡死）。

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Character/Enemy/Boss/Boss_SlimeKing.cs
git commit -m "feat: Boss_SlimeKing 动作池调度器（跳砸/召唤/传送/狂暴）"
```

---

### Task 10: BossEncounter（公共编排，独立 Boss 房场景版）+ Portal.SetLocked

**Files:**
- Create: `Assets/Scripts/Character/Enemy/Boss/BossEncounter.cs`
- Modify: `Assets/Scripts/Others/Area/Portal.cs`（加 `SetLocked(bool)` 锁定方法）

**Interfaces:**
- Produces: `BossEncounter.State` 可读、`VictoryEvent`（奖励挂载点，探针可订阅）、`BossEncounter` 场景加载自动开场
- Consumes: `Boss_SlimeKing.BeginFight()/OnLanded/enemyName`、`Entity_Health`、`UI_BossHealthBar`、`BossMinionRegistry`、`Portal.SetLocked`、`AudioManager.PushBgm/PopBgm`、`GameInput.IsPlayerControlBlocked`、`CinemachineVirtualCamera`、`CinemaScreenShake.ShakeWith`

**Step 1 — Portal 加 SetLocked（Modify）**

`Portal.cs` 加字段与方法（`playerInRange` 字段附近 + Update 加锁判断）：

```csharp
    private bool isLocked; // 锁定后禁止交互（Boss 战期间到达传送门锁定用）

    // 锁定/解锁（BossEncounter 调用；锁定后禁交互并隐藏提示）
    public void SetLocked(bool value)
    {
        isLocked = value;
        if (value)
            ShowPrompt(false);
    }
```

`OnTriggerEnter2D` 开头加 `if (isLocked) return;`（锁定不显示提示）；`Update()` 开头加 `if (isLocked) return;`（锁定不可交互）。

**Step 2 — BossEncounter（Create，跨场景版）**

```csharp
using System;
using System.Collections;
using UnityEngine;
using Cinemachine;
using DG.Tweening;

// 公共 Boss 编排（独立 Boss 房场景版）——场景加载自动开场，不关心 Boss 具体招式
// 流程：传送门进入 → 锁到达传送门 → 相机特写+BGM+血条 → 战斗（相机拉远）→ 击杀激活出口传送门
// 死亡走现有死亡面板+读档（场景卸载重置 Boss），本组件不处理；OnDestroy 恢复 BGM 防残留
public class BossEncounter : MonoBehaviour
{
    public enum EncounterState { Idle, Intro, Fighting, Victory }

    [Header("引用（场景接线）")]
    [SerializeField] private GameObject bossPrefab;             // Boss 预制体
    [SerializeField] private Transform spawnPoint;              // Boss 出场点
    [SerializeField] private Portal arrivalPortal;              // 到达传送门（Intro 锁定）
    [SerializeField] private GameObject exitPortal;             // 出口传送门（胜利激活，指向城镇）
    [SerializeField] private CinemachineVirtualCamera followCam; // 场景 follow vcam（战斗拉远）
    [SerializeField] private CinemachineVirtualCamera introCam;  // Intro vcam（出场特写）
    [SerializeField] private AudioClip bossBgm;                 // Boss 曲（calm 段）
    [SerializeField] private AudioClip bossBgmRage;             // 狂暴段（可空，HP<30% 切）
    [SerializeField] private UI_BossHealthBar healthBar;        // 顶部大血条
    [SerializeField] private BossMinionRegistry minionRegistry; // 迷你王登记处
    [SerializeField] private CinemaScreenShake screenShake;     // 震屏

    private const int INTRO_PRIORITY = 20;   // Intro vcam 拉高优先级（follow 默认 9）
    private const float FIGHT_ZOOM_SIZE = 8f; // 战斗拉远 orthographic size（放大视野）

    public EncounterState State { get; private set; } = EncounterState.Idle;
    public event Action VictoryEvent; // 击杀事件（奖励挂载点，探针可订阅验证）

    private Boss_SlimeKing boss;
    private Entity_Health bossHealth;
    private float originalOrthoSize = 5f;
    private bool defeatedFlag;

    private void Awake()
    {
        if (minionRegistry == null) minionRegistry = GetComponentInChildren<BossMinionRegistry>();
        if (followCam != null) originalOrthoSize = followCam.m_Lens.OrthographicSize;
    }

    private void Start()
    {
        // 场景加载自动开场（玩家经传送门到达、PlayerSpawner 定位后）
        StartCoroutine(RunIntro());
    }

    private void OnDestroy()
    {
        // 防 Boss 曲跨场景残留（死亡读档/离开场景）
        AudioManager.Instance?.PopBgm(0f);
    }

    // ==================== Intro ====================

    private IEnumerator RunIntro()
    {
        State = EncounterState.Intro;
        GameInput.IsPlayerControlBlocked = true; // 锁玩家输入

        // ① 锁到达传送门（禁交互）
        if (arrivalPortal != null)
            arrivalPortal.SetLocked(true);

        // ② 相机特写：Intro vcam 拉高
        if (introCam != null)
            introCam.Priority = INTRO_PRIORITY;

        // ③ 实例化 Boss（先隐藏，出场再显）
        boss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation).GetComponent<Boss_SlimeKing>();
        bossHealth = boss.GetComponent<Entity_Health>();
        boss.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.5f);

        // ④ Boss 出场 + 名称横幅 + 切 Boss BGM
        boss.gameObject.SetActive(true);
        AudioManager.Instance?.PushBgm(bossBgm, 0.3f);

        // ⑤ 落地冲击演出（坠入 + 震屏）
        boss.transform.position += Vector3.up * 4f;
        yield return new WaitForSeconds(0.6f);
        screenShake?.ShakeWith(new Vector3(3f, -4f, 0f));

        // ⑥ 进入战斗：血条 + 激活 Boss + 相机拉远 + 解锁输入
        if (healthBar != null) healthBar.BindBoss(bossHealth, boss.enemyName);
        boss.BeginFight();
        if (introCam != null) introCam.Priority = 0; // 镜头回玩家
        ZoomCameraOut();
        GameInput.IsPlayerControlBlocked = false;
        State = EncounterState.Fighting;

        boss.OnLanded += OnBossLanded;
        StartCoroutine(WatchForRage());

        // 监听 Boss 死亡（玩家死亡由现有死亡系统处理，不在此监听）
        while (State == EncounterState.Fighting)
        {
            if (bossHealth != null && bossHealth.GetCurrentHP() <= 0f)
            {
                OnBossDead();
                break;
            }
            yield return null;
        }
    }

    private IEnumerator WatchForRage()
    {
        while (State == EncounterState.Fighting && bossHealth != null && bossBgmRage != null)
        {
            if (bossHealth.GetHealthPercent() < 0.3f)
            {
                AudioManager.Instance?.PushBgm(bossBgmRage, 0.3f);
                yield break;
            }
            yield return null;
        }
    }

    private void OnBossLanded() => screenShake?.ShakeWith(new Vector3(3f, -4f, 0f));

    // 战斗相机拉远：放大视野看清落点预告（scaled 时间，暂停冻结）
    private void ZoomCameraOut()
    {
        if (followCam == null) return;
        DOTween.To(() => followCam.m_Lens.OrthographicSize,
            v => followCam.m_Lens.OrthographicSize = v,
            FIGHT_ZOOM_SIZE, 0.5f);
    }

    private void RestoreCamera()
    {
        if (followCam == null) return;
        DOTween.To(() => followCam.m_Lens.OrthographicSize,
            v => followCam.m_Lens.OrthographicSize = v,
            originalOrthoSize, 0.5f);
    }

    // ==================== Victory ====================

    private void OnBossDead()
    {
        State = EncounterState.Victory;
        if (boss != null) boss.OnLanded -= OnBossLanded;
        if (healthBar != null) healthBar.Unbind(); // 血条隐藏（OnEntityDead 时序内）
        if (minionRegistry != null) minionRegistry.Clear(); // 清迷你王
        RestoreCamera();
        AudioManager.Instance?.PopBgm(0.8f);
        if (exitPortal != null) exitPortal.SetActive(true); // 出口传送门出现（指向城镇）
        defeatedFlag = true;
        VictoryEvent?.Invoke(); // 奖励挂载点（探针可订阅验证）
    }
}
```

- [ ] **Step 3: 编辑器验证（半场景）**

在 Boss 房场景摆 BossEncounter（引用齐：bossPrefab/spawnPoint/arrivalPortal/exitPortal/followCam/introCam/bossBgm/bossBgmRage/healthBar/minionRegistry/screenShake）。进场景 → 到达传送门锁定、Boss 出场特写+切 BGM、血条满格、落地震屏；战斗开始相机拉远；击杀 → 血条隐藏、迷你王清、BGM 恢复、出口传送门激活；死亡 → 读档回存档点、BGM 恢复无残留。

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/Character/Enemy/Boss/BossEncounter.cs Assets/Scripts/Others/Area/Portal.cs
git commit -m "feat: BossEncounter 公共编排（跨场景版）+ Portal.SetLocked"
```
### Task 11: 场景接线（用户手动）+ 集成验证

**Files:**
- 场景：竞技场（level0 或新场景）、Boss 预制体、迷你王预制体、Intro vcam、血条 Canvas、Boss 曲资源

**Interfaces:**
- 依赖 Task 1-10 全部代码就位

- [ ] **Step 1: 场景摆放**
  - 竞技场：长方形碰撞体边界；入口门（BossEncounter 引用数组）
  - 触发区：Trigger Collider2D（玩家进入触发）
  - follow vcam 命名含 "Follow"；Intro vcam 命名含 "Intro"、priority=0、对准出场点
  - `BossEncounter` 对象挂脚本，拖引用（bossPrefab/spawnPoint/triggerArea/arenaDoors/introCam/bossBgm/bossBgmRage/healthBar/minionRegistry/screenShake）
  - `BossMinionRegistry` 场景摆一个
  - 血条 Canvas 挂持久 HUD 层级（UIManager 根下），`UI_BossHealthBar` 拖引用
  - Boss 预制体：`Boss_SlimeKing` + Enemy/Entity_Stats/Entity_Health + 身体 Trigger Collider + `ContactDamageArea`（12%、1s）+ 迷你王预制体引用 + 动画（idle/move/attack/dead 通用）+ `canKnockbacked=false`
  - 迷你王预制体：`Boss_SlimeMinion` + `ContactDamageArea`（5~8%）
  - Boss 曲两段 AudioClip（calm/rage）

- [ ] **Step 2: 全流程集成验证（Play）**
  - 进入竞技场 → 门锁 + 相机引导 + 横幅 + BGM + 落地震屏
  - 跳跃砸击（蓄力前摇 + 落点预告 + 落地后摇窗口）+ 召唤迷你王（2 只）+ 传送（拉远触发）+ 狂暴（HP<30%）
  - 玩家死亡 → 完整重演（每次死亡完整 Intro）
  - 击杀 → 血条隐藏 + 门开 + BGM 恢复 + 迷你王清场
  - 二次进入 → 不触发（不可重打）
  - 玩家受击无敌帧 0.7s：AoE+接触+迷你王不同帧只结算一次

- [ ] **Step 3: 提交（场景/预制体，按你的 git 惯例）**

---

### Task 12: 验收标准核对（对照设计文档 AC）

**Files:**
- 设计文档 `design/gdd/boss-slime-king.md` §Acceptance Criteria

- [ ] **Step 1: 逐条跑 AC**
  - 入场：门逐项锁 / vcam 0→20 / Boss 实例化 / 横幅 `[BOSS] 史莱姆王` / `CurrentBgm == bossBgm` / 落地冲击事件恰好一次 / Intro ≤5s / 血条 100%
  - 战斗：30s 窗口跳砸 ≥1 次 + 预告 0.6~1.0s 出现 / 召唤 2 只（狂暴 3 只）场上 ≤3 且死亡不分裂 / 被卡 3s 或超 15m → 3s 内传送且落点在竞技场内 / HP<30% 同帧切狂暴档
  - 死亡：零 Boss/迷你王、BGM 恢复、门解锁、重进完整重演
  - 击杀：血条解绑、迷你王清、门解锁、BGM 恢复、击杀事件恰好一次（探针）
  - 不可重打：胜利后 5s 内无 Intro
  - 公平：单无敌帧窗口内玩家最多结算一次

- [ ] **Step 2: 记录未达标项 → 回设计文档 Open Questions / 下个迭代**

---

## Self-Review

- **Spec coverage:** 设计文档 §A1（BossEncounter）→ Task 10；§A2（血条）→ Task 7；§A3（BGM）→ Task 2；§A4（相机）→ Task 4+10；§B0（生命周期/时间源）→ Task 9；§B1 四招 → Task 9；§B2（调度器）→ Task 9；§B3（迷你王）→ Task 6+8；Formulas（无敌帧/接触）→ Task 1+5；AC → Task 12。无缺口。
- **Placeholder scan:** 无 TBD/TODO；每任务含完整代码与验证。
- **Type consistency:** `BeginFight()`/`GetMinionCount()`/`OnLanded`（Boss_SlimeKing）↔ Task 10 调用一致；`Register/Unregister/Count/Clear`（Registry）↔ Task 8/10 一致；`BindBoss/Unbind`（血条）↔ Task 10 一致。
