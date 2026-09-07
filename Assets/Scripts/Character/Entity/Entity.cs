using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public class Entity : MonoBehaviour, IHitStopable
{

    public event Action OnFlipped;
    public event Action OnEntityDead; // 实体死亡事件
    public bool IsDead { get; set; } // 是否已死亡
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public Entity_Stats stats { get; private set; }
    protected StateMachine stateMachine;


    private bool facingRight = true;
    public int facingDir { get; private set; } = 1;


    [Header("碰撞检测")]
    public LayerMask whatIsGround;
    [SerializeField] public float groundCheckDistance;
    [SerializeField] private float wallCheckDistance;
    [SerializeField] public Transform groundCheck;
    public Transform GroundCheck => groundCheck;
    public float GroundCheckDistance => groundCheckDistance;
    [SerializeField] private Transform primaryWallCheck;
    [SerializeField] private Transform secondaryWallCheck;
    public bool isOnGround { get; private set; }
    public bool isOnWall { get; private set; }


    private bool isKnocked;
    private Coroutine knockbackCo;
    private Coroutine slowDownCo;

    [Header("顿帧设置")]
    [SerializeField] private bool enableHitStop = true;
    [SerializeField] private float hitStopDuration = 0.08f;
    [SerializeField] private float hitStopTimeScale = 0.0f;
    public bool IsHitStopActive => isHitStopActive;
    public bool HitStopEnabled { get => enableHitStop; set => enableHitStop = value; } // 顿帧开关（追击等状态可临时禁用）
    private bool isHitStopActive;
    private float originalAnimSpeed;
    private Vector2 savedVelocity;
    private RigidbodyConstraints2D savedConstraints;
    private Coroutine hitStopCo;


protected virtual void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponentInChildren<Rigidbody2D>();
        stats = GetComponentInChildren<Entity_Stats>();
        stateMachine = new StateMachine();
    }

    protected virtual void Start()
    {

    }

    protected virtual void Update()
    {
        if (stateMachine == null) return;
        stateMachine.UpdateActiveState();

        HandleCollisionDetection();
    }


    public void CurrentState_AnimationTriggers()//触发动画事件
    {
        stateMachine.currentState.AnimationTriggers();
    }

    public virtual void EntityDead()
    {
        IsDead = true; // 标记为已死亡
        OnEntityDead?.Invoke(); // 触发死亡事件
    }

    public virtual void SlowDownEntity(float duration, float slowMultiplier,bool canOverrideSlowEffect = false)//���ٳ���
    {
        if(slowDownCo != null)
        {
            if(canOverrideSlowEffect)//������Ը��Ǽ���Ч��
                StopCoroutine(slowDownCo); //ֹͣ��ǰ����Эͬ����
            else
                return;
        }

       slowDownCo =  StartCoroutine(SlowDownEntityCo(duration,slowMultiplier));
    }

    protected virtual IEnumerator SlowDownEntityCo(float duration, float slowMultiplier)
    {
        yield return null;
    }

    public virtual void StopSlowDown()//���ü���
    {
        slowDownCo = null;
    }

    public void ReciveKnockback(Vector2 knockback, float duration)//���ܻ���
    {
        if (knockbackCo != null)
            StopCoroutine(knockbackCo);

        knockbackCo = StartCoroutine(KnockbackCo(knockback,duration));//��������Эͬ����
    }


    private IEnumerator KnockbackCo(Vector2 knockback,float duration)//ö�ٻ���Эͬ����
    {
        isKnocked = true;
        rb.linearVelocity = knockback;//���豻���˵��ٶ�

        yield return new WaitForSeconds(duration);//�ȴ����˳���ʱ��

        rb.linearVelocity = Vector2.zero;//�����ٶ�
        isKnocked = false;
    }

    public void SetVelocity(float xVelocity, float yVelocity)
    {
        if(isKnocked || isHitStopActive)
            return;

        rb.linearVelocity = new Vector2(xVelocity, yVelocity);
        HandleFlip(xVelocity);
    }

    public void Flip()//��ת
    {
        facingRight = !facingRight;
        transform.Rotate(0, 180, 0);
        facingDir *= -1;

        OnFlipped?.Invoke();//Ѫ������ת,ʹ�ñ�������
    }

    public void HandleFlip(float xVelocity)//��ת������
    {
        if (xVelocity > 0 && !facingRight)
            Flip();
        else if (xVelocity < 0 && facingRight)
            Flip();
    }


    private void HandleCollisionDetection()
    {
        isOnGround = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, whatIsGround);
        if (secondaryWallCheck != null)
        {
            isOnWall = Physics2D.Raycast(primaryWallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround)
                     && Physics2D.Raycast(secondaryWallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround);
            
        }
        else
            isOnWall = Physics2D.Raycast(primaryWallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround);
    }

    public virtual void StartHitStop(float duration)
    {
        if (!enableHitStop || isHitStopActive)
            return;

        if (hitStopCo != null)
            StopCoroutine(hitStopCo);

        hitStopCo = StartCoroutine(HitStopCo(duration));
    }

    public virtual void EndHitStop()
    {
        if (!isHitStopActive)
            return;

        if (hitStopCo != null)
        {
            StopCoroutine(hitStopCo);
            hitStopCo = null;
        }

        anim.speed = originalAnimSpeed;
        rb.constraints = savedConstraints;
        rb.linearVelocity = savedVelocity;
        isHitStopActive = false;
    }

    public void SetAnimationSpeed(float speed)
    {
        if (anim != null)
        {
            anim.speed = speed;
        }

        if (isHitStopActive && rb != null)
        {
            if (speed <= 0)
            {
                rb.linearVelocity = Vector2.zero;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            else
            {
                if (rb.constraints == RigidbodyConstraints2D.FreezeAll)
                    rb.constraints = savedConstraints;

                rb.linearVelocity = savedVelocity * Mathf.Clamp01(speed);
            }
        }
    }

    private IEnumerator HitStopCo(float duration)
    {
        isHitStopActive = true;
        originalAnimSpeed = anim.speed;
        anim.speed = hitStopTimeScale;

        savedVelocity = rb.linearVelocity;
        savedConstraints = rb.constraints;
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;

        yield return new WaitForSecondsRealtime(duration);

        rb.constraints = savedConstraints;
        rb.linearVelocity = savedVelocity;
        anim.speed = originalAnimSpeed;
        isHitStopActive = false;
        hitStopCo = null;
    }

    public void TriggerHitStop(float duration = -1)
    {
        float actualDuration = duration > 0 ? duration : hitStopDuration;
        HitStopManager.Instance.TriggerLocalHitStop(this, actualDuration);
    }

    protected virtual void OnDrawGizmos()
    {
        Gizmos.DrawLine(groundCheck.position, groundCheck.position + new Vector3(0, -groundCheckDistance));
        Gizmos.DrawLine(primaryWallCheck.position, primaryWallCheck.position + new Vector3(wallCheckDistance * facingDir, 0));
        if (secondaryWallCheck != null)
            Gizmos.DrawLine(secondaryWallCheck.position, secondaryWallCheck.position + new Vector3(wallCheckDistance * facingDir, 0));
    }
}
