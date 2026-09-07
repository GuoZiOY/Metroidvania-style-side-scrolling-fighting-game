using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_StunnedState : EnemyState
{
    private Enemy_VFX vfx;

    public Enemy_StunnedState(Enemy enemy, StateMachine stateMachine, string animBoolName) : base(enemy, stateMachine, animBoolName)
    {
        vfx = enemy.GetComponent<Enemy_VFX>();
    }

    public override void Enter()
    {
        base.Enter();

        vfx.EnableAttackAlert(false);//�رչ���Ԥ�����ڣ��Ա��´�ʹ��
        enemy.EnableCounterTime(false);//�رտ�����ѣ���ڣ��Ա��´�ʹ��

        stateTimer = enemy.stunnedDuration;//������ѣʱ��
    }

    public override void Update()
    {
        base.Update();
        if(stateTimer <= 0)//��ѣʱ��������л�״̬��idle
            stateMachine.ChangeState(enemy.battleState);

    }
}
