using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VerticalPlatform : MonoBehaviour
{
    private PlatformEffector2D effector;
    [SerializeField] private float waitTime = 0.3f;
    [SerializeField] private LayerMask playerLayer; // 玩家层
    private bool isPlayerOnPlatform; // 玩家是否在平台上

    private void Start()
    {
        effector = GetComponent<PlatformEffector2D>();
    }

    private void Update()
    {
        PlatformChange();
    }

    private void PlatformChange()
    {
        if (GameInput.GetKey(GameInput.Action.PlatformDrop))
        {
            if (waitTime <= 0)
                effector.rotationalOffset = 180;
            else
                waitTime -= Time.deltaTime;
        }
        else
        {
            effector.rotationalOffset = 0;
            waitTime = 0.3f;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 检测碰撞对象是否是玩家
        if (collision.gameObject.layer == playerLayer)
        {
            isPlayerOnPlatform = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // 检测离开的对象是否是玩家
        if (collision.gameObject.layer == playerLayer)
        {
            isPlayerOnPlatform = false;
            // 玩家离开后重置平台碰撞状态
            effector.rotationalOffset = 0;
        }
    }

    public bool IsPlayerOnPlatform()
    {
        // 获取玩家是否在平台上
        return isPlayerOnPlatform;
    }

}
