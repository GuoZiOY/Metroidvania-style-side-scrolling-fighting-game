using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyState : EntityState
{
    protected Enemy enemy;

    public EnemyState(Enemy enemy, StateMachine stateMachine, string animBoolName) : base(stateMachine, animBoolName)
    {
        this.enemy = enemy;
        rb = enemy.rb;
        anim = enemy.anim;
        stats = enemy.stats;
    }



    public override void Update()
    {
        base.Update();

    }

    public override void UpdateAnimationParaeters()
    {
        base.UpdateAnimationParaeters();
        float battleAnimSpeedMultiplier = enemy.battleMoveSpeed / enemy.moveAnimSpeedMultiplier;
        anim.SetFloat("battleAnimSpeedMultiplier", battleAnimSpeedMultiplier);//�������ս��ʱ�����Ĳ����ٶ�
        anim.SetFloat("moveAnimSpeedMultiplier", enemy.moveAnimSpeedMultiplier);//��������ƶ������Ĳ����ٶ�
        anim.SetFloat("xVelocity",rb.linearVelocity.x);//ս���������x���긳��

    }
}
