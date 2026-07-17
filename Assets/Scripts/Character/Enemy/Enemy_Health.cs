using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Entity_Stats;

public class Enemy_Health : Entity_Health
{
    private Enemy enemy => GetComponent<Enemy>();//��õ���

    public override bool TakeDamage(float damage, float elementalDamage, ElementType element,Transform damageDealer, bool isCrit = false)
    {
        bool wasHit = base.TakeDamage(damage, elementalDamage, element, damageDealer, isCrit);//�ֲ���������ֵ���ں������࣬�Ϳ�������ʹ��

        if (wasHit == false)
            return false;//����������߹��������ܣ�����false
       
        if (damageDealer.GetComponent<Player>() != null)//����˺���Դ����ң�����
            enemy.TryEnterBattleState(damageDealer);//���Խ���ս��״̬������ڱ��󹥻�����ʱ��

        return true;
    }
        

    public override void ReduceHP(float physicalDamageTaken, float elementalDamageTaken, ElementType element, bool isCrit = false)//��������ֵ
    {
        entityVFX?.CreatePopUpText(physicalDamageTaken, elementalDamageTaken, element, isCrit);//�����˺������ı�
        base.ReduceHP(physicalDamageTaken, elementalDamageTaken, element, isCrit);//���û����������ֵ����
    }
}
