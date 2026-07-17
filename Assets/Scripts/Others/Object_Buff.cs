using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Buff
{
     public StatType type;
     public float value;

}

public class Object_Buff : MonoBehaviour
{
    private SpriteRenderer sr;
    private Entity_Stats statsToModify;

    [Header("Buffϸ��")]
    [SerializeField] private Buff[] buffs;
    [SerializeField] private string buffName;
    [SerializeField] private float buffDuration = 4;
    [SerializeField] private bool canBeUsed = true;

    [Header("����")]
    [SerializeField] private float floatSpeed = 1f;//�����ٶ�
    [SerializeField] private float floatRange = .1f;//������Χ
    private Vector3 startPosition;//������ά
    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        startPosition = transform.position;
        statsToModify = GetComponent<Entity_Stats>();
    }

    private void Update()
    {
        float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatRange;//y���������
        transform.position = startPosition + new Vector3(0, yOffset);//�������¸���
    }

    private void OnTriggerEnter2D(Collider2D collision)//������������
    {
        if (canBeUsed == false)
            return;
        statsToModify = collision.GetComponent<Entity_Stats>();//���
        StartCoroutine(BuffCo(buffDuration));//����Эͬ����buff��ʰȡ
    }

    private IEnumerator BuffCo(float duration)//buff��ʰȡ��Эͬ����
    {
        canBeUsed = false;
        sr.color = Color.clear;
        ApplyBuff(true);

        yield return new WaitForSeconds(duration);

        ApplyBuff(false);
        Destroy(gameObject);
    }

    private void ApplyBuff(bool apply)//Ӧ��buff
    {
        foreach (var buff in buffs)
        {
            if (apply)
                statsToModify.GetStatByType(buff.type).AddModifier(buff.value, buffName);//����buff
            else
                statsToModify.GetStatByType(buff.type).RemoveModifier(buffName);//�Ƴ�buff
        }
    }
}
