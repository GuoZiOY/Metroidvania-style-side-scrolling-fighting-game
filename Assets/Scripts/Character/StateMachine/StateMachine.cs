using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class StateMachine//状态机
{
    public EntityState currentState {  get; private set; }//取得基类EntityState 的当前状态
    public bool canChangeSate;

    public void Initialize(EntityState startState)//初始化状态
    {
        canChangeSate = true;
        currentState = startState;//将当前状态设为传入的初始状态
        currentState.Enter();//进入初始状态
    }
    
    public void ChangeState(EntityState newState)//改变状态
    {
        if (canChangeSate == false) return;
        currentState.Exit();//退出之前的状态
        currentState = newState;//将当前状态设为新状态
        currentState.Enter();//进入新状态
    }

    public void UpdateActiveState()//更新状态
    {
        currentState.Update();
    }

    public void SwitchOffStateMachine() => canChangeSate = false;
}
