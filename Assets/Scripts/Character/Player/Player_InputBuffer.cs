using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_InputBuffer : MonoBehaviour
{
    [Header("缓冲时间参数")]
    public float jumpBufferTime = 0.1f;
    public float doubleJumpBufferTime = 0.1f; 
    public float dashBufferTime = 0.1f;
    public float attackBufferTime = 0.1f;
    public float counterAttackBufferTime = 0.1f;

    private float jumpBufferTimer;
    private float attackBufferTimer;
    private float dashBufferTimer;
    private float counterAttackBufferTimer;
    private float doubleJumpBufferTimer;

    private void Update()
    {
        UpdateBufferTimers();
    }

    private void UpdateBufferTimers()
    {
        if (jumpBufferTimer > 0)
            jumpBufferTimer -= Time.deltaTime;

        if (attackBufferTimer > 0)
            attackBufferTimer -= Time.deltaTime;

        if (dashBufferTimer > 0)
            dashBufferTimer -= Time.deltaTime;

        if (counterAttackBufferTimer > 0)
            counterAttackBufferTimer -= Time.deltaTime;

        if (doubleJumpBufferTimer > 0)
            doubleJumpBufferTimer -= Time.deltaTime;
    }

    public void AddJumpBuffer()
    {
        jumpBufferTimer = jumpBufferTime;
    }

    public void AddAttackBuffer()
    {
        attackBufferTimer = attackBufferTime;
    }

    public void AddDashBuffer()
    {
        dashBufferTimer = dashBufferTime;
    }

    public void AddCounterAttackBuffer()
    {
        counterAttackBufferTimer = counterAttackBufferTime;
    }

    public void AddDoubleJumpBuffer()
    {
        doubleJumpBufferTimer = doubleJumpBufferTime;
    }

    public bool HasJumpBuffer()
    {
        return jumpBufferTimer > 0;
    }

    public bool HasAttackBuffer()
    {
        return attackBufferTimer > 0;
    }

    public bool HasDashBuffer()
    {
        return dashBufferTimer > 0;
    }

    public bool HasCounterAttackBuffer()
    {
        return counterAttackBufferTimer > 0;
    }

    public bool HasDoubleJumpBuffer()
    {
        return doubleJumpBufferTimer > 0;
    }

    public void ClearJumpBuffer()
    {
        jumpBufferTimer = 0;
    }

    public void ClearAttackBuffer()
    {
        attackBufferTimer = 0;
    }

    public void ClearDashBuffer()
    {
        dashBufferTimer = 0;
    }

    public void ClearCounterAttackBuffer()
    {
        counterAttackBufferTimer = 0;
    }

    public void ClearDoubleJumpBuffer()
    {
        doubleJumpBufferTimer = 0;
    }

    public void ClearAllBuffers()
    {
        jumpBufferTimer = 0;
        attackBufferTimer = 0;
        dashBufferTimer = 0;
        counterAttackBufferTimer = 0;
        doubleJumpBufferTimer = 0;
    }
}
