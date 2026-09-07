using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy_DeadState : EnemyState
{
    public Enemy_DeadState(Enemy enemy, StateMachine stateMachine, string animBoolName) : base(enemy, stateMachine, animBoolName)
    {
    }

    private Entity_VFX entityVFX;
    public override void Enter()
    {
        base.Enter();

        stateMachine.SwitchOffStateMachine();//关闭状态机防止bug

        DropLoot(); //掉落战利品
    }

    private void DropLoot() //掉落战利品
    {
        if (enemy.lootDropper != null)
        {
            enemy.lootDropper.OnDrop();
        }
    }

    public override void Update()
    {
        if (triggerCalled)
        {
            enemy.DestroyEntity();
        }
    }
  


}
