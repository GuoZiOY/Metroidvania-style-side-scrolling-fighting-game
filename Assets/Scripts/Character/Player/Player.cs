using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Player : Entity
{
    private UI ui;
    public SpriteRenderer sr { get; private set; }
    public Player_SkillManager skillManager {  get; private set; }
    public Player_VFX VFX { get; private set; }
    public Entity_Health health { get; private set; }
    public Entity_StatusHandler statusHandler { get; private set; }
    public Player_InputBuffer inputBuffer { get; private set; }
    public Player_Combat combat { get; private set; }


    #region 玩家状态
    public Player_IdleState idleState { get; private set; }
    public Player_MoveState moveState { get; private set; }
    public Player_JumpState jumpState { get; private set; }
    public Player_DoubleJumpState doubleJumpState { get; private set; }
    public Player_FallState fallState { get; private set; }
    public Player_WallSlideState wallSlideState { get; private set; }
    public Player_WallJumpState wallJumpState { get; private set; }
    public Player_DashState dashState { get; private set; }
    public Player_BasicAttackState basicAttackState { get; private set; }
    public Player_JumpAttackState jumpAttackState { get; private set; }
    public Player_DeadState deadState { get; private set; }
    public Player_CounterAttackState counterAttackState { get; private set; }
    public Player_DomainExpansionState domainExpansionState { get; private set; }
    public Player_CounterChaseState counterChaseState { get; private set; }
    #endregion

    [Header("移动参数")]
    [Range(0, 1)] public float inAirMoveMuliplier;//在空中移动的系数
    public float jumpForce;//跳跃力
    public Vector2 wallJumpForce;//爬墙时的跳跃力
    [Range(0, 1)] public float wallSlideSlowMoveMuliplier;//爬墙时移动系数
    public float xInput { get; private set; }//水平移动输入
    public float yInput { get; private set; }//垂直移动输入
    public float moveSpeed;//移动速度
    public int jumpCount { get; private set; }//跳跃次数计数器
    [Space]
    public float dashDuration = 0.25f;//冲刺时间
    public float dashSpeed = 20;//冲刺速度
    [Space]// 领域扩展参数
    public float riseSpeed = 25f;//上升速度
    public float riseMaxDistance = 3f;//最大上升距离

    public bool disableDynamicGravity { get; set; }

    [Header("土狼时间参数")]
    public float coyoteTime = 0.1f;//土狼时间：离开平台后仍可起跳的时间
    private float lastGroundedTime;//最后一次在地面的时间

    [Header("动态重力缩放参数")]
    public float riseGravityScale = 0.9f;
    public float fallGravityScale = 1.3f;
    public float doubleJumpRiseGravityScale = 1f;
    public float doubleJumpFallGravityScale = 1.6f;
    private float originalGravityScale;
    public bool isDoubleJumping { get; private set; }
    private bool wasOnGround;
    public bool justLanded { get; private set; }

    public void SetDoubleJumping(bool value)
    {
        isDoubleJumping = value;
    }

    [Header("跳跃变形参数")]
    public float jumpSquashScale = 0.9f;
    public float jumpStretchScale = 1.1f;
    public float landSquashScale = 0.8f;
    public float jumpSquashDuration = 0.1f;
    public float jumpStretchDuration = 0.1f;
    public float landSquashDuration = 0.1f;
    private Tween squashStretchTween;


    [Header("攻击参数")]
    public Vector2[] attack_PlayerVelocity;//攻击时玩家速度
    public float attack_PlayerVelocity_Duration = .1f;//攻击时玩家速度的持续时间
    public Vector2 jumpAttackVelocity;
    public float comboResetTime = 1;//连击重置时间
    private Coroutine queuedAttackCo;//延迟攻击协程


protected override void Awake()
    {
        base.Awake();

        ui = FindAnyObjectByType<UI>();
        sr = GetComponentInChildren<SpriteRenderer>();
        skillManager = GetComponent<Player_SkillManager>();
        VFX = GetComponent<Player_VFX>();
        statusHandler = GetComponent<Entity_StatusHandler>();
        health = GetComponentInChildren<Entity_Health>();
        inputBuffer = GetComponent<Player_InputBuffer>();
        combat = GetComponent<Player_Combat>();

        originalGravityScale = rb.gravityScale;

        #region 状态类赋值初始化
        idleState = new Player_IdleState(this, stateMachine, "idle");
        moveState = new Player_MoveState(this, stateMachine, "move");
        jumpState = new Player_JumpState(this, stateMachine, "jumpFall");
        doubleJumpState = new Player_DoubleJumpState(this, stateMachine, "jumpFall");
        fallState = new Player_FallState(this, stateMachine, "jumpFall");
        wallSlideState = new Player_WallSlideState(this, stateMachine, "wallSlide");
        wallJumpState = new Player_WallJumpState(this, stateMachine, "jumpFall");
        dashState = new Player_DashState(this, stateMachine, "dash");
        basicAttackState = new Player_BasicAttackState(this, stateMachine, "basicAttack");
        jumpAttackState = new Player_JumpAttackState(this, stateMachine, "jumpAttack");
        deadState = new Player_DeadState(this, stateMachine, "dead");
        counterAttackState = new Player_CounterAttackState(this,stateMachine,"counterAttack");
        domainExpansionState = new Player_DomainExpansionState(this,stateMachine,"jumpFall");
        counterChaseState = new Player_CounterChaseState(this, stateMachine, "dash");
        #endregion

    }

    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState); 
    }

    protected override void Update()
    {
        base.Update();
        Input();
        UpdateDynamicGravity();
        UpdateGroundedState();
    }

    private void UpdateGroundedState()
    {
        justLanded = !wasOnGround && isOnGround;
        wasOnGround = isOnGround;
    }

    public void TeleportPlayer(Vector3 position) => transform.position = position;
    public void ResetJumpCount() => jumpCount = 0;
    public void IncrementJumpCount() => jumpCount++;
    public void UpdateLastGroundedTime() => lastGroundedTime = Time.time;
    public bool CanUseCoyoteTime() => Time.time - lastGroundedTime <= coyoteTime;

    private void UpdateDynamicGravity()
    {
        if (disableDynamicGravity)
            return;

        if (isOnGround)
        {
            rb.gravityScale = originalGravityScale;
            return;
        }

        if (rb.linearVelocity.y > 0)
        {
            if (isDoubleJumping)
                rb.gravityScale = originalGravityScale * doubleJumpRiseGravityScale;
            else
                rb.gravityScale = originalGravityScale * riseGravityScale;
        }
        else
        {
            if (isDoubleJumping)
                rb.gravityScale = originalGravityScale * doubleJumpFallGravityScale;
            else
                rb.gravityScale = originalGravityScale * fallGravityScale;
        }
    }

    public void JumpSquashAndStretch()
    {
        squashStretchTween?.Kill();

        var seq = DOTween.Sequence();
        seq.Append(sr.transform.DOScaleY(jumpSquashScale, jumpSquashDuration).SetEase(Ease.OutQuad));
        seq.Join(sr.transform.DOScaleX(1f / jumpSquashScale, jumpSquashDuration).SetEase(Ease.OutQuad));
        seq.Append(sr.transform.DOScaleY(jumpStretchScale, jumpStretchDuration).SetEase(Ease.OutQuad));
        seq.Join(sr.transform.DOScaleX(1f / jumpStretchScale, jumpStretchDuration).SetEase(Ease.OutQuad));
        seq.Append(sr.transform.DOScale(Vector3.one, 0.05f));
        squashStretchTween = seq;
    }

    public void LandSquash()
    {
        squashStretchTween?.Kill();

        // 落地压扁 + 弹性弹回，比硬切更自然
        sr.transform.localScale = Vector3.one;
        squashStretchTween = sr.transform.DOPunchScale(
            new Vector3(1f / landSquashScale - 1f, landSquashScale - 1f, 0),
            landSquashDuration * 1.5f, 5, 0.5f);
    }

    public void Input()
    {
        xInput = GameInput.Horizontal;
        yInput = GameInput.Vertical;

        HandleInputBuffer();
        HandleSkillInput();
    }

    private void HandleInputBuffer()
    {
        if (GameInput.GetKeyDown(GameInput.Action.Jump))
        {
            if (isOnGround)
                inputBuffer.AddJumpBuffer();
            else
                inputBuffer.AddDoubleJumpBuffer();
        }

        if (GameInput.GetKeyDown(GameInput.Action.Attack))
            inputBuffer.AddAttackBuffer();

        if (GameInput.GetKeyDown(GameInput.Action.Dash))
            inputBuffer.AddDashBuffer();

        if (GameInput.GetKeyDown(GameInput.Action.CounterAttack))
            inputBuffer.AddCounterAttackBuffer();
    }

    private void HandleSkillInput()
    {
        // 技能槽位系统已接管H/Y/U/I/O按键的技能释放
        // 这里的逻辑已移除，由SkillSlotManager统一管理
    }


    protected override IEnumerator SlowDownEntityCo(float duration, float slowMultiplier)//实体减速协程
    {
        float originalMoveSpeed = moveSpeed;
        float originalJumpForce = jumpForce;
        float originalAnimSpeed = anim.speed;
        Vector2 originalWallJump = wallJumpForce;
        Vector2 originalJumpAttack = jumpAttackVelocity;
        Vector2[] originalAttackVelocity = attack_PlayerVelocity;

        float speedMultiplier = 1 - slowMultiplier;

        moveSpeed = moveSpeed * speedMultiplier;
        jumpForce = jumpForce * speedMultiplier;
        anim.speed = anim.speed * speedMultiplier;
        wallJumpForce = wallJumpForce * speedMultiplier;
        jumpAttackVelocity = jumpAttackVelocity * speedMultiplier;

        for (int i = 0; i < attack_PlayerVelocity.Length; i++)
        {
            attack_PlayerVelocity[i] = attack_PlayerVelocity[i] * speedMultiplier;
        }

        yield return new WaitForSeconds(duration);

        moveSpeed = originalMoveSpeed;
        jumpForce = originalJumpForce;
        anim.speed = originalAnimSpeed;
        wallJumpForce = originalWallJump;
        jumpAttackVelocity = originalJumpAttack;

        for (int i = 0; i < attack_PlayerVelocity.Length; i++)
        {
            attack_PlayerVelocity[i] = originalAttackVelocity[i];
        }
    }

public override void StartHitStop(float duration)
    {
        base.StartHitStop(duration);
        if (VFX != null) VFX.ToggleShockwavePause(true);
    }

    public override void EndHitStop()
    {
        base.EndHitStop();
        if (VFX != null) VFX.ToggleShockwavePause(false);
    }

    public override void EntityDead()//实体死亡方法
    {
        base.EntityDead();
        stateMachine.ChangeState(deadState);

        // 启动死亡序列（慢动作 → 相机聚焦 → 停时间 → 死亡面板）
        if (gameObject.activeInHierarchy)
            deathSequenceCoroutine = StartCoroutine(DeathSequence());
    }

    private Tween timeScaleTween;
    private Tween cameraZoomTween;
    private Tween cameraMoveTween;
    private Coroutine deathSequenceCoroutine;

    [Header("死亡效果")]
    [SerializeField] private float slomoDuration = 2f;
    [SerializeField] private float slomoTimeScale = 0.05f;
    [SerializeField] private float cameraZoom = 1.5f;
    [SerializeField] private float focusDuration = 2f;

    private IEnumerator DeathSequence()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            float originalSize = cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
            Vector3 targetPos = new Vector3(transform.position.x, transform.position.y, cam.transform.position.z);

            // 慢动作
            timeScaleTween = DOTween.To(() => Time.timeScale, v => Time.timeScale = v, slomoTimeScale, slomoDuration)
                .SetUpdate(true);

            // 相机聚焦
            if (cam.orthographic)
                cameraZoomTween = DOTween.To(() => cam.orthographicSize, v => cam.orthographicSize = v, originalSize / cameraZoom, focusDuration).SetUpdate(true);
            else
                cameraZoomTween = DOTween.To(() => cam.fieldOfView, v => cam.fieldOfView = v, originalSize / cameraZoom, focusDuration).SetUpdate(true);

            cameraMoveTween = cam.transform.DOMove(targetPos, focusDuration).SetUpdate(true);
        }

        // 等慢动作播完
        yield return new WaitForSecondsRealtime(slomoDuration);

        // 停止 → 通知死亡面板
        timeScaleTween?.Kill();
        cameraZoomTween?.Kill();
        cameraMoveTween?.Kill();
        DOTween.Kill(Camera.main?.transform);
        Time.timeScale = 0;

        UIManager.Instance?.ShowDeathScreen();
    }

    public void Revive()
    {
        // 清理死亡序列（防止 DOTween 泄漏）
        if (deathSequenceCoroutine != null)
        {
            StopCoroutine(deathSequenceCoroutine);
            deathSequenceCoroutine = null;
        }
        timeScaleTween?.Kill();
        cameraZoomTween?.Kill();
        cameraMoveTween?.Kill();
        Time.timeScale = 1f;

        IsDead = false;
        health?.Revive();
        stateMachine.ChangeState(idleState);
    }

    public void EnterAttackStateWithDelay()//延迟进入攻击状态的方法，可以从Player_BasicAttackState调用
    {
        if (queuedAttackCo != null)//如果延迟攻击协程不为空
            StopCoroutine(queuedAttackCo); //停止延迟攻击协程

        queuedAttackCo = StartCoroutine(EnterAttackStateWithDelayCo());//启动协程，延迟进入攻击状态
    }

    private IEnumerator EnterAttackStateWithDelayCo()//延迟进入攻击状态
    {
        yield return new WaitForEndOfFrame();//等待一帧结束
        stateMachine.ChangeState(basicAttackState);//切换到攻击状态
    }
}

