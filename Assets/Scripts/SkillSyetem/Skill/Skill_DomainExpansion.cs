using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_DomainExpansion : Skill_Base
{
    [SerializeField] private GameObject domainPrefab;

    [Header("领域扩展基础值")]
    [SerializeField] private float baseMaxDomainSize = 10;//基础领域最大范围
    [SerializeField] private float baseExpandSpeed = 3;//基础领域扩展速度
    [SerializeField] private float baseDuration = 5;//基础领域持续时间
    [SerializeField] private float baseSpellsToCast = 10;//基础领域每秒法术数量

    public float maxDomainSize => baseMaxDomainSize + totalLevel * 1f;//领域最大范围
    public float expandSpeed => baseExpandSpeed + totalLevel * 0.5f;//领域扩展速度
    public float slowDownDomainDuration => baseDuration + totalLevel * 0.5f;//减速领域持续时间
    public float spellCastingDomainDuration => baseDuration + totalLevel * 0.5f;//法术领域持续时间
    public float spellsToCast => baseSpellsToCast + totalLevel * 2f;//领域每秒法术数量

    [Header("减速领域效果详情")]
    [SerializeField] private float slowDownPercent = 0.8f;

    [Header("法术领域效果详情")]
    [SerializeField] private float spellCastingDomainSlowDown = 1;

    private float spellCastTimer;
    private float spellsPerSecond;

    private List<Enemy> trappedTargets = new List<Enemy>();
    private Transform currentTarget;

    public void CreatedDomain()
    {
        spellsPerSecond = spellsToCast / GetDomainDuration();

        GameObject domain = Instantiate(domainPrefab, transform.position, Quaternion.identity);
        domain.GetComponent<SkillObject_DomainExpansion>().SetupDomain(this);
        Debug.Log("创建领域");
    }


    public void DoSpellCasting()
    {
        spellCastTimer -= Time.deltaTime;

        if(currentTarget == null)
            currentTarget = FindTargetInDomain();

        if(currentTarget != null && spellCastTimer <= 0)
        {
            CastSpell(currentTarget);
            spellCastTimer = 1f / spellsPerSecond;
            currentTarget = null;
        }
    }

    private void CastSpell(Transform target)
    {
        if(upgradeType == SkillUpgradeType.Domain_EchoSpam)
        {
            Vector3 offset = Random.value < 0.5f ? new Vector2(-1,0) : new Vector2(1,0);
            skillManager.timeEcho.CreateTimeEcho(target.position + offset);
        }

        if(upgradeType == SkillUpgradeType.Domain_ShardSpam)
        {
            skillManager.shard.CreateRawShard(target,true);
        }
    }

    private Transform FindTargetInDomain()
    {
        if(trappedTargets.Count == 0)
            return null;

        int randomIndex = Random.Range(0, trappedTargets.Count);
        Transform target = trappedTargets[randomIndex].transform;

        if(target == null)
        {
            trappedTargets.RemoveAt(randomIndex);
            return null;
        }

        return target;
    }

    public float GetDomainDuration()
    {
        if(upgradeType == SkillUpgradeType.Domain_SlowDown)
            return slowDownDomainDuration;
        else
            return spellCastingDomainDuration;
    }

    public float GetDomainSlowDownPercent()
    {
        if(upgradeType == SkillUpgradeType.Domain_SlowDown)
            return slowDownPercent;
        else
            return spellCastingDomainSlowDown;
    }

    public bool InstantDomain()
    {
        return upgradeType != SkillUpgradeType.Domain_EchoSpam 
            && upgradeType != SkillUpgradeType.Domain_ShardSpam;
    }

   

    public void AddTarget(Enemy targetToAdd)
    {
        trappedTargets.Add(targetToAdd);
    }

    public void ClearTargets()
    {
        foreach(var enemy in trappedTargets)
            enemy.StopSlowDown();

        trappedTargets = new List<Enemy>();
    }

    public override void SetSkillLevelData(SkillUpgradeType upgradeType, LevelData levelData, int level, bool updateUpgradeType = true)
    {
        base.SetSkillLevelData(upgradeType, levelData, level, updateUpgradeType);
        Debug.Log($"{GetType().Name} 升级到 {level} 级，totalLevel: {totalLevel}，maxDomainSize: {maxDomainSize:F1}, expandSpeed: {expandSpeed:F1}, 持续时间: {GetDomainDuration():F1}, 法术数量: {spellsToCast:F1}");
    }
}
