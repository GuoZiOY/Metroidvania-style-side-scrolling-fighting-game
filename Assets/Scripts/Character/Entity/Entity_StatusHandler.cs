
using System.Collections;
using UnityEngine;

using static Entity_Stats;

public class Entity_StatusHandler : MonoBehaviour
{
    private Entity_Stats entityStats;
    private Entity entity;
    private Entity_VFX entityVFX;
    private Entity_Health entityHealth;
    private Transform selfTransform; // �������


    private ElementType currentEffect = ElementType.None;

    [Header("���buff����")]
    [SerializeField] private GameObject lighingStrikeVfx;//���VFX
    [SerializeField] private float currentCharge;//��ǰ����
    [SerializeField] private float maximumCharge = 1;//����ĳ�����ֵ���൱��100%
    private Coroutine shockCo;//���Э��


private void Awake()
    {
        entity = GetComponentInParent<Entity>();
        entityStats = GetComponentInParent<Entity_Stats>();
        entityVFX = GetComponentInParent<Entity_VFX>();
        entityHealth = GetComponentInParent<Entity_Health>();
        selfTransform = transform;
    }


    public void RemoveAllNegativeEffects()//�Ƴ����и���Ч��
    {
        StopAllCoroutines();
        currentEffect = ElementType.None;
        entityVFX.StopAllVFX();
    }

    public void ApplyStatusEffect(ElementType element, ElementalEffectData effectData)
    {
        if (element == ElementType.Ice && CanBeApplied(ElementType.Ice))
            ApplyChillEffect(effectData.chillDuration, effectData.chillSlowMultiplier);

        if (element == ElementType.Fire && CanBeApplied(ElementType.Fire))
            ApplyBurnEffect(effectData.burnDuratoin, effectData.totalBurnDamage);

        if (element == ElementType.Lightning && CanBeApplied(ElementType.Lightning))
            ApplyShockEffect(effectData.shockDuration, effectData.shockDamage, effectData.shockCharge);
    }



    public bool CanBeApplied(ElementType element)
    {
        if (element == ElementType.Lightning && currentEffect == ElementType.Lightning) 
            return true;//����buff���ۼƣ�����һ������

        return currentEffect == ElementType.None;
    }
 
    public void ApplyBurnEffect(float duration, float fireDamage)//����buffӦ��
    {
        StartCoroutine(BurnEffectCo(duration, fireDamage));
    }

    private IEnumerator BurnEffectCo(float duration, float totalDamage)//����buff��Эͬ����
    {
        currentEffect = ElementType.Fire;
        entityVFX.PlayStatusVFX(duration, ElementType.Fire);//���
        
        int ticksPerSecond = 2;//ÿ�봥������
        int tickCount = Mathf.RoundToInt(ticksPerSecond * duration);//�ܵĴ�������(����ȡ��)

        float damagePerTick = totalDamage / tickCount;//ÿ�δ������˺�
        float tickInterval = 1f / ticksPerSecond;//�������

        for (int i = 0; i < tickCount; i++)//ѭ�����ܴ��������ڣ�ÿ�ȼ�����һ������
        {
            entityHealth.TakeDamage(0, damagePerTick, ElementType.Fire, selfTransform);//���ռ�ȥѪ��
            yield return new WaitForSeconds(tickInterval);//��һ֡����ȴ��������
        }
        currentEffect = ElementType.None;
    }

    public void ApplyChillEffect(float duration, float slowMultiplier)//����buffӦ��
    {
        float iceResistance = entityStats.GetElementalResistance(ElementType.Ice);//��Ԫ�ؿ��Ի�ȡ
        float finalDuration = duration * (1 - iceResistance);//����buff���ճ���ʱ��

        StartCoroutine(ChillCoEffecteCo(duration, slowMultiplier));
    }

    private IEnumerator ChillCoEffecteCo(float duration,float slowMultiplier)//����buff��Эͬ����
    {
        currentEffect = ElementType.Ice;
        entity.SlowDownEntity(duration, slowMultiplier);
        entityVFX.PlayStatusVFX(duration,ElementType.Ice);//���

        yield return new WaitForSeconds(duration);
        currentEffect = ElementType.None;
    }
  
    public void ApplyShockEffect(float duration, float damage, float charge)//Ӧ�õ��buff
    {
        currentCharge = currentCharge + charge;//�ۼƵ������ֵ

        if (currentCharge >= maximumCharge)//�����ܵ�����ֵ
        {
            DoLightningStrike(damage);//����˺�
            StopShockEffect();//��ֹͣ���buff
            return;
        }

        if (shockCo != null)
            StopCoroutine(shockCo);

        shockCo = StartCoroutine(ShockEffectCo(duration));
    }
    private void DoLightningStrike(float lightingDamage)//ִ�е��buff
    {
        Instantiate(lighingStrikeVfx, transform.position, Quaternion.identity);
        entityHealth.TakeDamage(0, lightingDamage, ElementType.Lightning, selfTransform);
    }

    private void StopShockEffect()//ֹͣ���buff
    {
        currentEffect = ElementType.None;
        currentCharge = 0;
        entityVFX.StopAllVFX();
    }


    private IEnumerator ShockEffectCo(float duration)//���buffЭͬ����
    {
        currentEffect = ElementType.Lightning;
        entityVFX.PlayStatusVFX(duration, ElementType.Lightning);

        yield return new WaitForSeconds(duration);
        StopShockEffect();
    }
}
