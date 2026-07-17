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
        anim.SetFloat("battleAnimSpeedMultiplier", battleAnimSpeedMultiplier);//赋予敌人战斗时动画的播放速度
        anim.SetFloat("moveAnimSpeedMultiplier", enemy.moveAnimSpeedMultiplier);//赋予敌人移动动画的播放速度
        anim.SetFloat("xVelocity",rb.velocity.x);//战斗混合树的x坐标赋予

    }
}
