using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows;

public class Player_BasicAttackState : PlayerState
{
    public Player_BasicAttackState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    private float attack_PlayerVelocity_Timer;//攻击时的角色速度变化计时器
    private int firstComboIndex = 1;//第一连击的索引

    private bool comboAttackQueued;//连击攻击队列
    private int comboIndex = 1;//连击索引
    private int comboLimit = 3;//连击的连击数
    private float lastAttackTime;//上次攻击的游戏时间

    private int attackDir;//攻击方向
    private bool isFromChase;
    private bool isLastAttackHit;

    public bool IsLastAttackHit => isLastAttackHit;
    public int ComboIndex => comboIndex;

    public void SetComboIndex(int index)
    {
        comboIndex = index;
        isFromChase = true;
    }

    public override void Enter()
    {
        base.Enter();
        comboAttackQueued = false;
        ResetComboIndexIfNeed();
        isLastAttackHit = false;
        SyncAttackSpeed();
        InputAttackDir();

        anim.SetInteger("basicAttackindex", comboIndex);
        AttackingDisplace();

        player.combat.OnAttackHitResult += HandleAttackHit;
        player.combat.OnAttackHitWithIndex += HandleAttackHitWithIndex;

        if (comboIndex == comboLimit)
        {
            if (isFromChase)
                player.combat.SetSpecialAttackType(Player_Combat.SpecialAttackType.ChaseAttack);
            else
                player.combat.SetSpecialAttackType(Player_Combat.SpecialAttackType.ThirdComboAttack);
        }

        isFromChase = false;
    }

    public override void Exit()
    {
        base.Exit();
        player.combat.OnAttackHitResult -= HandleAttackHit;
        player.combat.OnAttackHitWithIndex -= HandleAttackHitWithIndex;
        comboIndex++;
        lastAttackTime = Time.time;
    }

    private void HandleAttackHit(ElementType element, bool hitResult)
    {
        if (hitResult && comboIndex == comboLimit)
        {
            isLastAttackHit = true;
        }
    }

    private void HandleAttackHitWithIndex(int attackIndex)
    {
        if (player.VFX != null)
            player.VFX.ShakeScreenForAttack(attackIndex);
    }

    public override void Update()
    {
        base.Update();
        HandleAttack_Input_PlayerVelocity();

        if (UnityEngine.Input.GetKeyDown(KeyCode.Mouse0))
            QueueNextAttack();

        if (triggerCalled)
            HandleStateExit();
    }
    private void InputAttackDir()//设置攻击方向
    {
        if (player.xInput != 0)
            attackDir = ((int)player.xInput);
        else
            attackDir = player.facingDir;
    }

    private void HandleStateExit()
    {
        if (comboAttackQueued)//通过延迟判断是否退出连击状态，否则为同帧判断的basicAttack状态切换到idle状态，回到可操作
        {
            anim.SetBool(animBoolName, false);//延迟期间关闭动画
            player.EnterAttackStateWithDelay();//player进入 攻击状态 延迟
        }
        else
            stateMachine.ChangeState(player.idleState);
    }


    private void QueueNextAttack()//下一连击队列，使用 连击队列 连击延迟，使连击延迟，使用不连续攻击，使用连续攻击
    {
        if (comboIndex < comboLimit)//如果 连击索引 小于 连击限制
            comboAttackQueued = true; //连击攻击队列 为真 
    }

    private void HandleAttack_Input_PlayerVelocity()//
    {
        attack_PlayerVelocity_Timer -= Time.deltaTime;//攻击时的角色速度变化的计时器
        if (attack_PlayerVelocity_Timer < 0)//计时器时间到，角色可以根据输入移动
            player.SetVelocity(0,rb.linearVelocity.y);
    }

    private void AttackingDisplace()//攻击时位移，指定优化
    {
        Vector2 attack_PlayerVelocity = player.attack_PlayerVelocity[ comboIndex - 1 ];//获取Player中的attack_PlayerVelocity

        attack_PlayerVelocity_Timer = player.attack_PlayerVelocity_Duration;//获取Player中的值attack_PlayerVelocity_Duration
        player.SetVelocity(attack_PlayerVelocity.x * attackDir, attack_PlayerVelocity.y);//使用移动
    }

    private void ResetComboIndexIfNeed()//如果需要重置连击索引
    {   
        if (isFromChase)
            return;

        if (Time.time > lastAttackTime + player.comboResetTime)//如果连击的时间过期
            comboIndex = firstComboIndex;

        if (comboIndex > comboLimit)//如果连击索引超过连击限制
            comboIndex = firstComboIndex;
    }

}
