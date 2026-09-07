using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaDetectorBase : MonoBehaviour
{
    [Header("区域设置")]
    [SerializeField] private Collider2D areaCollider; // 区域碰撞体
    [SerializeField] protected string areaName = "未知区域"; // 区域名称

    [Header("UI引用")]
    [SerializeField] protected UI_EventTip eventTip; // 事件提示引用

    protected bool isPlayerInArea; // 玩家是否在区域内

    public event Action<GameObject> OnPlayerEnterEvent; // 玩家进入事件
    public event Action<GameObject> OnPlayerExitEvent; // 玩家离开事件
    public event Action<bool> OnPlayerStateChangedEvent; // 玩家状态改变事件
    
    protected virtual void Awake()
    {
        // 确保碰撞体是触发器
        if (areaCollider != null)
        {
            areaCollider.isTrigger = true;
        }

        // 自动查找UI_EventTip引用
        if (eventTip == null)
        {
            eventTip = FindAnyObjectByType<UI_EventTip>();
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 检测到玩家进入区域
        if (other.CompareTag("Player"))
        {
            isPlayerInArea = true;
            
            
            // 触发玩家进入事件
            OnPlayerEnterEvent?.Invoke(other.gameObject);
            
            // 触发状态改变事件
            OnPlayerStateChangedEvent?.Invoke(true);
            
            OnPlayerEnter(other.gameObject);
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        // 检测到玩家离开区域
        if (other.CompareTag("Player"))
        {
            isPlayerInArea = false;
            
            // 触发玩家离开事件
            OnPlayerExitEvent?.Invoke(other.gameObject);
            
            // 触发状态改变事件
            OnPlayerStateChangedEvent?.Invoke(false);
            
            OnPlayerExit(other.gameObject);
        }
    }
    
    protected virtual void OnPlayerEnter(GameObject player)
    {
        Debug.Log($"玩家进入区域: {areaName}");
    }

    protected virtual void OnPlayerExit(GameObject player)
    {
        Debug.Log($"玩家离开区域: {areaName}");
    }
    
    public bool IsPlayerInArea()
    {
        // 获取玩家是否在区域内
        return isPlayerInArea;
    }
}
