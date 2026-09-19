using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using static Entity_Stats;

public class SkillObject_Base : MonoBehaviour
{
    [SerializeField] private GameObject onHitVfx;

    [SerializeField] protected LayerMask whatIsEnemy;//���֣�˭�ǵ���
    [SerializeField] protected Transform targetCheck;//����Ŀ��
    [SerializeField] protected float checkRadius = 1;//���뾶

    protected Rigidbody2D rb;
    protected Animator anim;

    protected Entity_Stats playerStats;
    protected DamageScaleData damageScaleData;
    protected ElementType usedElement;
    protected bool targetGotHit;
    protected Transform lastTarget;
    
    protected virtual void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    protected void DamageEnemiesInRadius(Transform t, float radius)//�Լ�⵽�ĵ�������˺�
    {
        foreach (var target in EnemiesAround(t, radius))//��t�뾶�ڼ�⵽�ĵ���
        {
            IDamgable damgable = target.GetComponent<IDamgable>();//�ӿ� => ����˺�����ȡ����Ŀ��

            if (damgable == null)
                continue;

            AttackData attackData = playerStats.GetAttackData(damageScaleData);

            float physDamage = attackData.phyiscalDamage;
            float elemDamage = attackData.elementalDamage;
            ElementType element = attackData.element;

            targetGotHit = damgable.TakeDamage(physDamage, elemDamage, element, transform, attackData.isCrit);

            if (element != ElementType.None)
            {
                Entity_StatusHandler statusHandler = target.GetComponent<Entity_StatusHandler>();
                if (statusHandler != null)
                    statusHandler.ApplyStatusEffect(element, attackData.effectData);
            }

            if (targetGotHit)
            {
                lastTarget = target.transform;
                if(onHitVfx == null) return;
                else Instantiate(onHitVfx, target.transform.position, Quaternion.identity);//ʵ����������Ч
            }

            usedElement = element;
        }
    }

    protected Transform FindClosestTarget()//Ѱ�������Ŀ��
    {
        Transform target = null;
        float closestDistance = Mathf.Infinity;

        foreach (var enemy in EnemiesAround(transform, 10))
        {
            float distance = Vector2.Distance(transform.position, enemy.transform.position);

            if (distance < closestDistance)
            {
                target = enemy.transform;
                closestDistance = distance;
            }
        }

        return target;
    }


    protected Collider2D[] EnemiesAround(Transform t, float radius)//����Բ�η�Χ��,������
    {
        return Physics2D.OverlapCircleAll(t.position, radius, whatIsEnemy);
    }

    protected virtual void OnDrawGizmos()//����Բ��
    {
        if (targetCheck == null)
            targetCheck = transform;

        Gizmos.DrawWireSphere(targetCheck.position, checkRadius);//��ը���Բ
        Gizmos.DrawWireSphere(targetCheck.position, 10);//׷�ټ�⸨��Բ
    }
}
