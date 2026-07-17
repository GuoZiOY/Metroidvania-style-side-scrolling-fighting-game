using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillObject_DomainExpansion : SkillObject_Base
{
    private Skill_DomainExpansion domainManager;

    private float expandSpeed = 2;
    private float slowDownPercent = 0.9f;
    private float duration;

    private Vector3 targetScale;
    private bool isShrinking;

    public void SetupDomain(Skill_DomainExpansion domainManager)//设置领域扩展
    {
        this.domainManager = domainManager;

        duration = domainManager.GetDomainDuration();
        slowDownPercent = domainManager.GetDomainSlowDownPercent();
        expandSpeed = domainManager.expandSpeed;
        float maxSize = domainManager.maxDomainSize;

        targetScale = Vector3.one * maxSize; 
        Invoke(nameof(ShrinkDomain), duration);
    } 

    private void Update()//更新
    {
        HandleScaling();//处理缩放
    }

    private void HandleScaling()//处理缩放
    {
        float sizeDiffrence = Mathf.Abs(transform.localScale.x - targetScale.x);//计算当前缩放与目标缩放的差值
        bool shouldChanegeScale = sizeDiffrence > .1f;//如果差值大于.1f，则需要改变缩放

        if(shouldChanegeScale)//如果需要改变缩放
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, expandSpeed * Time.deltaTime);//插值改变缩放 

        if(isShrinking && sizeDiffrence < .1f)//如果正在缩小且差值小于.1f
        {
            domainManager.ClearTargets();//清除所有目标
            Destroy(gameObject);//销毁领域扩展对象      
        }    
    }

    private void ShrinkDomain()
    {
        targetScale = Vector3.zero;
        isShrinking = true;
    }

    private void OnTriggerEnter2D(Collider2D collection)
    {
        Enemy enemy = collection.GetComponent<Enemy>();//获取敌人组件

        if(enemy == null)//如果敌人组件不存在
            return;//返回

        domainManager.AddTarget(enemy);//添加敌人到领域内敌人列表
        enemy.SlowDownEntity(duration, slowDownPercent,true);//设置敌人减速百分比
    }

    private void OnTriggerExit2D(Collider2D collection)
    {
        Enemy enemy = collection.GetComponent<Enemy>();//获取敌人组件

        if(enemy == null)//如果敌人组件不存在
            return;//返回
        
        enemy.StopSlowDown();//重置敌人减速系数和动画播放速度
        
    }


}
