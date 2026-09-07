using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Skill_Shard : Skill_Base
{
    private SkillObject_Shard currentShard;
    private Entity_Health playerHealth;

    [SerializeField] private GameObject shardPrefab;//��Ƭ��Ԥ�����ȡ
    [SerializeField] private float detonateTime = 2;

    [Header("����׷��")]
    [SerializeField] private float shardSpeed = 7;

    [Header("����׷��")]
    [SerializeField] private int maxCharges = 3;//��󴢴���
    [SerializeField] private int currentCharges;//��ǰ����
    [SerializeField] private bool isRecharging;//���ڳ���

    [Header("���崫��")]
    [SerializeField] private float shardExistDuration = 10;//�������ʱ��

    [Header("�����ָ�")]
    [SerializeField] private float HealthRecoveryPercent = 0.3f;//�����ָ��ı���

    protected override void Awake()
    {
        base.Awake();
        currentCharges = maxCharges;
        playerHealth = GetComponentInParent<Entity_Health>();
    }


    public override void TryUseSkill()
    {
        if (CanUseSkill() == false)
            return;

        if(Unlocked(SkillUpgradeType.Shard))
            HandleShardRegular();

        if (Unlocked(SkillUpgradeType.Shard_MoveToEnemy))
            HandleShardMoving();

        if (Unlocked(SkillUpgradeType.Shard_Multicast))
            HandleShardMulticast();

        if (Unlocked(SkillUpgradeType.Shard_Teleport))
            HandleShardTeleport();

        if (Unlocked(SkillUpgradeType.Shard_TeleportHpRewind))
            HandleShardHealthRewind();
    }

    private void HandleShardRegular()//��ֻͨ���쾧��
    {
        CreateShard();
        StartSkillCooldown();
    }
    public void CreateShard()//���쾧��(���ڱ����Ľ���)
    {
        float detonationTime = GetDetonateTime();

        GameObject shard = Instantiate(shardPrefab,transform.position,Quaternion.identity);
        currentShard = shard.GetComponent<SkillObject_Shard>();
        currentShard.SetupShard(this);

        if (Unlocked(SkillUpgradeType.Shard_Teleport) || Unlocked(SkillUpgradeType.Shard_TeleportHpRewind))
            currentShard.OnExplode += ForceCooldown;//�����ը�ˣ��¼�ת��ForceCooldown
    }

    public void CreateRawShard(Transform target = null,bool isDomainShard = false)//�������ľ��壨���ڳ�̼��ܵķ�֧����/��������еķ�֧���ף�
    {
        bool canMove = isDomainShard != false ? isDomainShard : //����������壬�Ϳ��Ƿ����������ƶ�����
                    Unlocked(SkillUpgradeType.Shard_MoveToEnemy) || Unlocked(SkillUpgradeType.Shard_Multicast);//������������壬�Ϳ��Ƿ�����׷�ٻ���������

        GameObject shard = Instantiate(shardPrefab, transform.position, Quaternion.identity);
        shard.GetComponent<SkillObject_Shard>().SetupShard(this, detonateTime, canMove, shardSpeed,target);
    }

    public void CreateDomainShard(Transform target)
    {
        if (target == null) return;

        GameObject shard = Instantiate(shardPrefab, transform.position, Quaternion.identity);
        SkillObject_Shard shardComponent = shard.GetComponent<SkillObject_Shard>();
        shardComponent.SetupShard(this, detonateTime, true, shardSpeed, target);
    }

    public float GetDetonateTime()//��������ʱ��ĸ�ֵ
    {
        if (Unlocked(SkillUpgradeType.Shard_Teleport) || Unlocked(SkillUpgradeType.Shard_TeleportHpRewind))
            return shardExistDuration;

        return detonateTime;
    }

    private void HandleShardMoving()//׷�ٹ���
    {
        CreateShard();
        currentShard.MoveTowardsClosestTarget(shardSpeed);
        StartSkillCooldown();
    }

    private void HandleShardMulticast()//��������
    {
        if (currentCharges <= 0)
            return;

        CreateShard();
        currentShard.MoveTowardsClosestTarget(shardSpeed);
        currentCharges--;

        if (isRecharging == false)
            StartCoroutine(ShardRechargeCo());
    }

    private IEnumerator ShardRechargeCo()//��������Эͬ����
    {
        isRecharging = true;//���ڳ���

        while (currentCharges < maxCharges)//ѭ������ǰ����洢��<�����ʱ
        {
            yield return new WaitForSeconds(cooldown);//�ȴ���ȴ
            currentCharges++;//�洢��+1
        }

        isRecharging = false;//�رճ���
    }


    private void HandleShardTeleport()//���崫�͹���
    {
        if (currentShard == null)
        {
            CreateShard();
        }
        else
        {
            SwapPlayerAndShard();
            StartSkillCooldown();
        }
    }

    private void SwapPlayerAndShard()
    {
        Vector3 shardPosition = currentShard.transform.position;//���澧��λ��
        Vector3 playerPosition = player.transform.position;//�������λ��

        currentShard.transform.position = playerPosition;//��������ҽ���λ��
        currentShard.Explode();

        player.TeleportPlayer(shardPosition);//����뾧�彻��λ��
    }

    private void ForceCooldown()//�������ȴǰ���屬ը�ˣ�ǿ�����ã���ʼ��ȴ
    {
        if (IsOnCooldown() == false)
        {
            StartSkillCooldown();
            currentShard.OnExplode -= ForceCooldown;
        }
    }


    private void HandleShardHealthRewind()//Ѫ�����ݹ���
    {
        if (currentShard == null)
        {
            CreateShard();
            //HealthRecoveryPercent = playerHealth.GetHealthPercent();//�������
        }
        else
        {
            SwapPlayerAndShard();
            playerHealth.SetHealthToPercent(HealthRecoveryPercent);//���ݱ����ָ�Ѫ������ָ�0.3maxHP
            StartSkillCooldown();
        }
    }


}
