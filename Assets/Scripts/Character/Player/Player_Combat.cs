using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Entity_Stats;

public class Player_Combat : Entity_Combat
{
    public enum SpecialAttackType
    {
        None,
        JumpAttack,
        ChaseAttack,
        ThirdComboAttack
    }

    private Player player;
    private int currentAttackIndex = 1;
    private SpecialAttackType currentSpecialAttack = SpecialAttackType.None;
    private bool isChaseAttack;

    public event Action<int> OnAttackHitWithIndex;

    [Header("反击设置")]
    [SerializeField] private float counterRecovery = 0.2f;
    [SerializeField] private float counterCooldownDuration = 1f;
    [SerializeField] private float counterKnockbackMultiplier = 1.5f;

    [Header("追击设置")]
    [SerializeField] private float chaseTimeDuration = 0.5f;
    [SerializeField] private float chaseSpeedMultiplier = 1.2f;
    [SerializeField] private float chaseStopDistance = 0.8f;

    [Header("顿帧设置")]
    [SerializeField] private bool enableHitStop = true;
    [SerializeField] private bool enableHitStopOnCrit = true;
    [SerializeField] private float critHitStopDuration = 0.15f;
    [SerializeField] private bool enableCounterHitStop = true;
    [SerializeField] private float counterHitStopDuration = 0.12f;
    [SerializeField] private bool enableChaseHitStop = false;
    [SerializeField] private float chaseHitStopDuration = 0.1f;
    [SerializeField] private bool enableLastComboHitStop = true;
    [SerializeField] private float lastComboHitStopDuration = 0.06f;
    [SerializeField] private bool enableJumpAttackHitStop = true;
    [SerializeField] private float jumpAttackHitStopDuration = 0.15f;

    [Header("特殊攻击加成")]
    [SerializeField] private float jumpAttackDamageMultiplier = 1.2f;
    [SerializeField] private float jumpAttackCritChanceBonus = 5f;
    [SerializeField] private float chaseAttackDamageMultiplier = 1.2f;
    [SerializeField] private float chaseAttackCritChanceBonus = 5f;
    [SerializeField] private float thirdComboAttackDamageMultiplier = 1.2f;
    [SerializeField] private float thirdComboAttackCritChanceBonus = 5f;

    private Transform chaseTarget;
    private bool chaseTimeActive;
    private float currentChaseTimer;
    private bool counterCooldownActive;
    private float currentCounterCooldown;

    protected override void Awake()
    {
        base.Awake();
        player = GetComponent<Player>();
    }

    public override void PerformAttack()
    {
        UpdateCurrentAttackIndex();

        if (GetDectectedCollders().Length == 0)
            AudioManager.Instance?.PlaySwingSfx(currentAttackIndex);

        base.PerformAttack();
        TriggerLastComboHitStop();
        ResetSpecialAttackType();
    }

    private void UpdateCurrentAttackIndex()//更新当前攻击索引
    {
        if (player.basicAttackState != null)
        {
            currentAttackIndex = player.basicAttackState.ComboIndex;
        }
    }

    private void ResetSpecialAttackType()//重置特殊攻击类型
    {
        currentSpecialAttack = SpecialAttackType.None;
    }

    protected override AttackData CalculateAttackData()
    {
        float damageMultiplier = GetDamageMultiplier();
        float critChanceBonus = GetCritChanceBonus();

        float physDamage = stats.GetPhyiscalDamage(basicAttacksData.phyiscal);
        float elemDamage = stats.GetElementalDamage(stats.InputElement, out ElementType element, basicAttacksData.elemental);

        bool isCrit = stats.CalculateCritStatus(critChanceBonus);

        if (isCrit)
        {
            float critPower = stats.GetCritPower();
            physDamage *= critPower;
            elemDamage *= critPower;
        }

        physDamage *= damageMultiplier;
        elemDamage *= damageMultiplier;

        return new AttackData
        {
            phyiscalDamage = physDamage,
            elementalDamage = elemDamage,
            isCrit = isCrit,
            element = element,
            effectData = new ElementalEffectData(stats, basicAttacksData)
        };
    }

    private float GetDamageMultiplier()//获取伤害乘法器
    {
        return currentSpecialAttack switch
        {
            SpecialAttackType.JumpAttack => jumpAttackDamageMultiplier,
            SpecialAttackType.ChaseAttack => chaseAttackDamageMultiplier,
            SpecialAttackType.ThirdComboAttack => thirdComboAttackDamageMultiplier,
            _ => 1f
        };
    }

    private float GetCritChanceBonus()//获取暴击率加成
    {
        return currentSpecialAttack switch
        {
            SpecialAttackType.JumpAttack => jumpAttackCritChanceBonus,
            SpecialAttackType.ChaseAttack => chaseAttackCritChanceBonus,
            SpecialAttackType.ThirdComboAttack => thirdComboAttackCritChanceBonus,
            _ => 0f
        };
    }

    protected override void OnTargetHit(Collider2D target, AttackData attackData)
    {
        base.OnTargetHit(target, attackData);
        AudioManager.Instance?.PlayHitSfx(currentAttackIndex);
        AudioManager.Instance?.PlayExtraSfx(currentAttackIndex);
        if (attackData.isCrit)
            AudioManager.Instance?.PlayCritSfx(currentAttackIndex);

        TryApplyChaseStun(target);

        TriggerHitStopOnHit(attackData.isCrit, target.transform);
        TriggerOnAttackHitWithIndex(currentAttackIndex);
    }

    private void TryApplyChaseStun(Collider2D target)
    {
        if (!isChaseAttack)
            return;
        isChaseAttack = false;

        Skill_PowerCounterChase skill = player?.skillManager?.powerCounterChase;
        if (skill == null) return;

        float stunChance = skill.GetStunChance();
        if (stunChance <= 0 || UnityEngine.Random.value > stunChance)
            return;

        Enemy enemy = target.GetComponent<Enemy>();
        enemy?.TryStun(skill.GetExtraStunDuration());
    }

    protected virtual void TriggerHitStopOnHit(bool isCrit, Transform targetTransform)//触发命中的顿帧
    {
        if (!enableHitStop)
            return;

        if (enableHitStopOnCrit && isCrit)
        {
            HitStopManager.Instance.TriggerLocalHitStop(gameObject, targetTransform.gameObject, critHitStopDuration);
        }
    }

    protected virtual void TriggerOnAttackHitWithIndex(int attackIndex)//触发攻击命中事件
    {
        OnAttackHitWithIndex?.Invoke(attackIndex);
    }

    private void TriggerLastComboHitStop()//触发最后一次攻击的顿帧
    {
        if (!enableLastComboHitStop)
            return;

        if (player.basicAttackState == null)
            return;

        if (player.basicAttackState.IsLastAttackHit)
        {
            Collider2D[] targets = GetDectectedCollders();
            if (targets.Length > 0)
            {
                foreach (var target in targets)
                {
                    IDamgable damgable = target.GetComponent<IDamgable>();
                    if (damgable != null)
                    {
                        HitStopManager.Instance.TriggerLocalHitStop(gameObject, target.gameObject, lastComboHitStopDuration);
                        break;
                    }
                }
            }
        }
    }

    private IEnumerator DelayedCounterHitStop(GameObject target, float knockbackMultiplier, bool canBeChased)
    {
        yield return null; // 1帧，等待粒子生成
        HitStopManager.Instance.TriggerLocalHitStop(gameObject, target, counterHitStopDuration);
        yield return new WaitForSecondsRealtime(counterHitStopDuration);
        
        ICounterable counterable = target.GetComponent<ICounterable>();
        if (counterable != null)
        {
            counterable.HandleCounter(knockbackMultiplier);
            
            Debug.Log($"反击成功: {target.name}, CanBeChased: {canBeChased}");
            
            if (canBeChased)
            {
                chaseTarget = target.transform;
                Debug.Log($"设置追击目标: {chaseTarget.name}");
            }
        }
    }

    public bool CounterAttackPerformed()
    {
        bool hasPerformedCounter = false;
        chaseTarget = null;

        foreach (var target in GetDectectedCollders())
        {
            ICounterable counterable = target.GetComponent<ICounterable>();
            if (counterable == null)
                continue;

            Debug.Log($"检测到敌人: {target.name}, IsInCounterTime: {counterable.IsInCounterTime}");

            if (counterable.IsInCounterTime)
            {
                hasPerformedCounter = true;

                bool targetCanBeChased = counterable.CanBeChased;

                AudioManager.Instance?.PlayCounterWithDelay();

                if (player.VFX != null)
                {
                    player.VFX.ShakeScreenForCounter();
                    player.VFX.DoCounterVisuals(target.transform.position);
                }
                
                if (enableCounterHitStop)
                {
                    if (targetCanBeChased)
                    {
                        chaseTarget = target.transform;
                        Debug.Log($"立即设置追击目标: {chaseTarget.name}");
                    }
                    StartCoroutine(DelayedCounterHitStop(target.gameObject, counterKnockbackMultiplier, targetCanBeChased));
                }
                else
                {
                    counterable.HandleCounter(counterKnockbackMultiplier);
                    
                    Debug.Log($"反击成功: {target.name}, CanBeChased: {targetCanBeChased}");
                    
                    if (targetCanBeChased)
                    {
                        chaseTarget = target.transform;
                        Debug.Log($"设置追击目标: {chaseTarget.name}");
                    }
                }
            }
        }
        
        if (!hasPerformedCounter)
        {
            ActivateCounterCooldown();
        }
        else
        {
            counterCooldownActive = false;
            Debug.Log("反击成功，冷却已取消，可立即进行下一次反击");
        }
        
        return hasPerformedCounter;
    }

    private void Update()
    {
        UpdateChaseTime();
        UpdateCounterCooldown();
    }

    private void UpdateChaseTime()
    {
        if (chaseTimeActive)
        {
            currentChaseTimer -= Time.deltaTime;
            if (currentChaseTimer <= 0)
            {
                DeactivateChaseTime();
            }
        }
    }

    private void UpdateCounterCooldown()
    {
        if (counterCooldownActive)
        {
            currentCounterCooldown -= Time.deltaTime;
            if (currentCounterCooldown <= 0)
            {
                counterCooldownActive = false;
                Debug.Log("反击冷却已结束");
            }
        }
    }

    private void ActivateCounterCooldown()
    {
        counterCooldownActive = true;
        currentCounterCooldown = counterCooldownDuration;
        Debug.Log($"反击冷却已激活，持续时间: {counterCooldownDuration}秒");
    }

    public void DeactivateChaseTime()
    {
        Debug.Log($"追击时间已关闭");
        chaseTimeActive = false;
        chaseTarget = null;
    }

    public void ActivateChaseTime(Transform target)
    {
        chaseTarget = target;
        chaseTimeActive = true;
        currentChaseTimer = chaseTimeDuration;
        Debug.Log($"追击时间已激活，目标: {target.name}");
    }

    public Transform ChaseTarget => chaseTarget;
    public float CounterRecoveryDuration => counterRecovery;
    public float ChaseTimeDuration => chaseTimeDuration;
    public float ChaseSpeedMultiplier => chaseSpeedMultiplier;
    public float CounterKnockbackMultiplier => counterKnockbackMultiplier;
    public float ChaseStopDistance => chaseStopDistance;
    public bool IsChaseTimeActive => chaseTimeActive;
    public bool IsCounterCooldownActive => counterCooldownActive;
    public float CurrentCounterCooldown => currentCounterCooldown;
    public bool EnableHitStop => enableHitStop;
    public bool EnableHitStopOnCrit => enableHitStopOnCrit;
    public float CritHitStopDuration => critHitStopDuration;
    public bool EnableChaseHitStop => enableChaseHitStop;
    public float ChaseHitStopDuration => chaseHitStopDuration;
    public bool EnableLastComboHitStop => enableLastComboHitStop;
    public float LastComboHitStopDuration => lastComboHitStopDuration;
    public bool EnableJumpAttackHitStop => enableJumpAttackHitStop;
    public float JumpAttackHitStopDuration => jumpAttackHitStopDuration;

    public void SetSpecialAttackType(SpecialAttackType type)
    {
        currentSpecialAttack = type;
        if (type == SpecialAttackType.ChaseAttack)
            isChaseAttack = true;
    }
}
