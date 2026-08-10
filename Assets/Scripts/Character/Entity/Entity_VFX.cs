using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Linq;
using UnityEngine;
using static Entity_Stats;

public class Entity_VFX : MonoBehaviour//实体视觉特效
{
    protected SpriteRenderer sr;//����
    protected Entity entity;

    [SerializeField] private GameObject popUpTextPrefab;//�����ı�Ԥ����

    [Header("������Ч���")]
    [SerializeField] private Material onDamageMaterial;//�ܻ���Ч����
    public float onDamageVfxDuration = .2f;
    private Material originalMaterial;//��ʼ����
    private Coroutine onDamageVfxCoroutine;//�ܻ�����Эͬ���򣨱����ʱ������������ܻ���Ч��

    [Header("伤害VFX")]
    [SerializeField] private Color hitVFXColor = Color.yellow;
    [SerializeField] private GameObject hitVFX;
    [SerializeField] private GameObject critHitVFX;
    
    [Header("暴击粒子系统")]
    [SerializeField] private CritParticleEffect critParticleEffect;

    [Header("相机震动")]
    private CinemaScreenShake screenShake; // 相机震动（场景对象，预制体无法序列化，运行时查找）

    [Header("元素颜色-击中特效")]
    public bool closeHitVFXColor = true;//������Ч������Ԫ����ɫ��Ĭ�Ͽ���
    [SerializeField] private Color chillVFX = Color.cyan;//����Ч����vfx��ɫ
    [SerializeField] private Color burnVFX = Color.red;//����Ч����vfx��ɫ
    [SerializeField] private Color ChockVFX = Color.yellow;
    private Color originalHitVFXColor;//��ʼ��������Ч��ɫ

    [Header("Ԫ����ɫ-�ܻ����")]
    [SerializeField] private Color inChillVFX = Color.cyan;//����Ч����vfx��ɫ
    [SerializeField] private Color inBurnVFX = Color.red;//����Ч����vfx��ɫ
    [SerializeField] private Color inChockVFX = Color.yellow;
    private Color originalInHitVFXColor;//��ʼ��������Ч��ɫ

    private void Awake()
    {
        entity = GetComponent<Entity>();
        sr = GetComponentInChildren<SpriteRenderer>();//�������<����ͼ��>��
        originalMaterial = sr.material;//��ó�ʼ����
        originalHitVFXColor = hitVFXColor;
    }

    public void CreatePopUpText(string text)//创建弹出文本
    {
        GameObject newPopUpText = Instantiate(popUpTextPrefab, transform.position, Quaternion.identity);
        PopUpText popUpTextComponent = newPopUpText.GetComponent<PopUpText>();
        
        if (popUpTextComponent != null)
        {
            popUpTextComponent.SetText(text);
            popUpTextComponent.SetRandomOffsetPosition();
        }
    }

    public void CreatePopUpText(float physicalDamage, float elementalDamage, ElementType element, bool isCrit = false)//创建伤害弹出文本
    {
        GameObject newPopUpText = Instantiate(popUpTextPrefab, transform.position, Quaternion.identity);
        PopUpText popUpTextComponent = newPopUpText.GetComponent<PopUpText>();
        
        if (popUpTextComponent != null)
        {
            popUpTextComponent.SetDamageText(physicalDamage, elementalDamage, element, isCrit);
        }
    }

    public void PlayStatusVFX(float duration, ElementType element)//�ܻ�������Ԫ��buff��Ч��������
    {
        if (element == ElementType.Ice)
            StartCoroutine(PlayStatusVFXco(duration, inChillVFX));
        if (element == ElementType.Fire)
            StartCoroutine(PlayStatusVFXco(duration, inBurnVFX));
        if (element == ElementType.Lightning)
            StartCoroutine(PlayStatusVFXco(duration, inChockVFX));
    }

    public void StopAllVFX()
    {
        StopAllCoroutines();
        sr.color = Color.white;
        sr.material = originalMaterial;
    }

    private  IEnumerator PlayStatusVFXco(float duration, Color effectColor)//����Ԫ��buff��ЧЭͬ����
    {
        float tickInterval = 0.25f;//��˸Ƶ��
        float timerHasPassed = 0;

        Color lightColor = effectColor * 1.2f;
        Color darkColor = effectColor * 0.8f;

        bool toggle = false;
        while (timerHasPassed < duration)//��ɫ��˸
        {
            sr.color = toggle ? lightColor:darkColor;
            toggle = !toggle;//ȡ������ֵ��ʹ��color�����仯

            yield return new WaitForSeconds(tickInterval);//�ȴ���˸Ƶ�ʺ����ѭ��
            timerHasPassed = timerHasPassed + tickInterval;//��ʱ�ۼ�
        }
        sr.color = Color.white;//�ָ�ԭ������ɫ
    }



    public Color UpdateOnHitColor(ElementType element)
    {
      if(element == ElementType.Ice)
            hitVFXColor = chillVFX;
      if(element == ElementType.Fire)
            hitVFXColor = burnVFX;
      if(element == ElementType.Lightning)
            hitVFXColor = ChockVFX;
      if (element == ElementType.None)
            hitVFXColor = originalHitVFXColor;

      return hitVFXColor;
    }

    public void CreateOnHitVFX(Transform target,bool isCrit, ElementType element)
    {
        if (isCrit && critParticleEffect != null)
        {
            Color particleColor = UpdateOnHitColor(element);
            critParticleEffect.CreateCritParticles(target.position, particleColor);
            UnityEngine.Debug.Log("CreateCritParticles");
            
            if (screenShake != null)
                screenShake.ShakeScreen();
        }
        else
        {
            GameObject hitPrefab = isCrit ? critHitVFX : hitVFX;
            GameObject vfx = Instantiate(hitPrefab, target.position, Quaternion.identity);
            
            if(closeHitVFXColor == false)
                vfx.GetComponentInChildren<SpriteRenderer>().color = UpdateOnHitColor(element);

            if (entity.facingDir == -1 && isCrit)
                vfx.transform.Rotate(0, 180, 0);
        }
    }

    public void ShakeScreenForCounter()
    {
        GetScreenShake()?.ShakeScreenForCounter();
    }

    public void ShakeScreenForAttack(int attackIndex)
    {
        GetScreenShake()?.ShakeScreenForAttack(attackIndex);
    }

    public void ShakeScreenForJumpAttack()
    {
        GetScreenShake()?.ShakeScreenForJumpAttack();
    }

    // 相机震动延迟解析：场景对象引用在预制体中无效，运行时查找
    private CinemaScreenShake GetScreenShake()
    {
        if (screenShake == null)
            screenShake = FindAnyObjectByType<CinemaScreenShake>();
        return screenShake;
    }

    public void PlayOnDamageVfx()
    {
        if (onDamageVfxCoroutine != null)
            StopCoroutine(onDamageVfxCoroutine);
        onDamageVfxCoroutine = StartCoroutine(OnDamageVfxCo());
    }

    private IEnumerator OnDamageVfxCo()
    {
        sr.material = onDamageMaterial;//�����ܻ���Ч����
        yield return new WaitForSeconds(onDamageVfxDuration);//�ȴ��ܻ���Ч����ʱ��
        sr.material = originalMaterial;//��ԭ�ɳ�ʼ����
    }


}
