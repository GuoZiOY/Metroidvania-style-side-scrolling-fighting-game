using System.Collections;
using System.Collections.Generic;
using System.Drawing.Printing;
using UnityEngine;

public class SkillObject_TimeEcho : SkillObject_Base
{
    [SerializeField] private float wispMoveSpeed = 15f;//幽灵移动速度
    [SerializeField] private GameObject onDeathVfx;
    [SerializeField] private LayerMask whatIsGround;
    private bool shouldMoveToPlayer;

    private Transform playerTransform;
    private Skill_TimeEcho echoManager;
    private TrailRenderer wispTrail;
    private Entity_Health playerhealth;
    private SkillObject_Health echoHealth;
    private Player_SkillManager skillManager;
    private Entity_StatusHandler statusHandler;
    public int maxAttacks {  get; private set; }

    public void SetupEcho(Skill_TimeEcho echoManager)
    {
        this.echoManager = echoManager;
        playerStats = echoManager.player.stats;
        damageScaleData = echoManager.damageScaleData;
        maxAttacks = echoManager.GetMaxAttacks();
      
        playerTransform = echoManager.player.transform;
        playerhealth = echoManager.player.health;
        skillManager = echoManager.skillManager;
        statusHandler = echoManager.player.statusHandler;

    
        Invoke(nameof(HandleDeath), echoManager.GetEchoDuration());
        
        FlipToTarget();//使复制体始终面相目标

        echoHealth = GetComponent<SkillObject_Health>();
        wispTrail = GetComponentInChildren<TrailRenderer>();
        wispTrail.gameObject.SetActive(false);

        anim.SetBool("canAttack", maxAttacks > 0);
    }

    private void Update()
    {

        if (shouldMoveToPlayer)
            HandleWispMovement();
        else
        {
            anim.SetFloat("yVelocity", rb.linearVelocity.y);
            StopHorizontalMovement();
        }
    }

    private void FlipToTarget()//翻转后面相目标
    {
        Transform target = FindClosestTarget();
        if (target != null && target.position.x < transform.position.x)//（默认面相右）如果目标在复制体的左边
            transform.Rotate(0, 180, 0);//则翻转
    }

    public void PerformAttack()//攻击
    {
        DamageEnemiesInRadius(targetCheck, 1);//执行伤害

        if(targetGotHit == false)
            return;

        bool canDuplicate = Random.value < echoManager.GetDuplicateChance();//可以复制的判断
        float xoffset = transform.position.x < lastTarget.position.x ? 1 : -1;

        if (canDuplicate)
            echoManager.CreateTimeEcho(lastTarget.position + new Vector3(xoffset, 0));
    }

    public void HandleDeath()//死亡方法
    {
        Instantiate(onDeathVfx, transform.position, Quaternion.identity);//死亡特效实例化

        if(echoManager.ShouldBeWisp())
            TurnIntoWisp();
        else
            Destroy(gameObject);
    }


    private void TurnIntoWisp()
    {
        shouldMoveToPlayer = true;
        anim.gameObject.SetActive(false);
        wispTrail.gameObject.SetActive(true);
        rb.simulated = false;
    }

    private void HandlePlayerTouch()
    {
        float healAmount = echoHealth.lastDamageTaken * echoManager.GetPercentOfDamageHealed();
        playerhealth.IncreaseHP(healAmount);
        

        float amountInSeconds = echoManager.GetCooldownReduceInSeconds();
        skillManager.ReduceAllSkillCooldownBy(amountInSeconds);

        if (echoManager.CanRemoveNegativeEffects())
            statusHandler.RemoveAllNegativeEffects();
    }


    private void HandleWispMovement()
    {
        transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, wispMoveSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, playerTransform.position) < .5f)
        {
            HandlePlayerTouch();
            Destroy(gameObject);
        }

    }

    private void StopHorizontalMovement()//在地面上时停止x轴移动
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.5f, whatIsGround);

        if (hit.collider != null)
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }
}
