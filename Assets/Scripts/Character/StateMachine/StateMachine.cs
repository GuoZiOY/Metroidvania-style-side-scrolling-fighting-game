using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class StateMachine//״̬��
{
    public EntityState currentState {  get; private set; }//ȡ�û���EntityState �ĵ�ǰ״̬
    public bool canChangeSate;

    public void Initialize(EntityState startState)//��ʼ��״̬
    {
        canChangeSate = true;
        currentState = startState;//����ǰ״̬��Ϊ����ĳ�ʼ״̬
        currentState.Enter();//�����ʼ״̬
    }
    
    public void ChangeState(EntityState newState)//�ı�״̬
    {
        if (canChangeSate == false) return;
        currentState.Exit();//�˳�֮ǰ��״̬
        currentState = newState;//����ǰ״̬��Ϊ��״̬
        currentState.Enter();//������״̬
    }

    public void UpdateActiveState()//����״̬
    {
        currentState.Update();
    }

    public void SwitchOffStateMachine() => canChangeSate = false;
}
