using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_BattleState : EnemyState
{
    public Enemy_BattleState(Enemy enemy, StateMachine stateMachine, string animBoolName) : base(enemy, stateMachine, animBoolName)
    {
    }

    private Transform player;
    private Transform lastTarget;
    private float lastTimeWasInBattle;//�ϴ�ս����ʱ��

    public override void Enter()
    {
        base.Enter();
        UpdateBattleTimer();

        if (player == null)
            player = enemy.GetPlayerReference();//��ȡ��⵽����ҵ�transform

        if (ShouldRetreat() == true)//��
        {
            rb.linearVelocity = new Vector2((enemy.reteatVelocity.x * enemy.activeSlowMultiplier) * -enemy.DirctionToPlayer(),enemy.reteatVelocity.y);
            enemy.HandleFlip(enemy.DirctionToPlayer());
        }   
 
    }

    public override void Update()
    {
        base.Update();

        RaycastHit2D playerHit = enemy.PlayerDetected();
        bool hasDetectedPlayer = playerHit;

        if (hasDetectedPlayer)
        {
            UpdateTarget(playerHit.transform);
            UpdateBattleTimer();
        }

        if (BattleTimeIsOver())
            stateMachine.ChangeState(enemy.idleState);

        if (WithInAttackRange() && hasDetectedPlayer)
            stateMachine.ChangeState(enemy.attackState);
        else
            enemy.SetVelocity(enemy.GetBattleMoveSpeed() * enemy.DirctionToPlayer(), rb.linearVelocity.y);
    }

    private void UpdateTarget(Transform detectedPlayer)
    {
        if (detectedPlayer != lastTarget)
        {
            lastTarget = detectedPlayer;
            player = detectedPlayer;
        }
    }

    private void UpdateBattleTimer() => lastTimeWasInBattle = Time.time;//��¼�ϴ�ս����ʱ��
    private bool BattleTimeIsOver() => Time.time > lastTimeWasInBattle + enemy.battleTimeDuration;//ս������ʱ���ж�
    private bool WithInAttackRange() => DistanceToPlayer() < enemy.attackDistance;//�Ƿ���빥����Χ,�����Ҿ���С�ڹ������룬������
    private bool ShouldRetreat() => DistanceToPlayer() < enemy.minRetreatDistance;//Ӧ�ú�ʱ

    private float DistanceToPlayer()//����ҵľ���
    {
        if (player == null)
            return float.MaxValue;
        return Mathf.Abs(player.position.x - enemy.transform.position.x);//������������������ľ���ֵ
    }

 


}
