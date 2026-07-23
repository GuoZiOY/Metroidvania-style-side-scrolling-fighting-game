using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CinemaScreenShake : MonoBehaviour
{
    private Player player;

    [Header("相机震动")]
    private CinemachineImpulseSource screenShake;

    [Header("暴击震动")]
    [SerializeField] private bool enableCritShake = true;
    [SerializeField] private float critShakeMultiplier = 1f;
    [SerializeField] private Vector3 critShakePower = new Vector3(2f, 1f, 0f);

    [Header("反击震动")]
    [SerializeField] private bool enableCounterShake = true;
    [SerializeField] private float counterShakeMultiplier = 1.5f;
    [SerializeField] private Vector3 counterShakePower = new Vector3(3f, 1.5f, 0f);

    [Header("普通攻击震动")]
    [SerializeField] private bool enableAttack1Shake = true;
    [SerializeField] private float attack1ShakeMultiplier = 0.5f;
    [SerializeField] private Vector3 attack1ShakePower = new Vector3(1f, 0.5f, 0f);
    [SerializeField] private bool enableAttack2Shake = true;
    [SerializeField] private float attack2ShakeMultiplier = 0.7f;
    [SerializeField] private Vector3 attack2ShakePower = new Vector3(1.5f, 0.7f, 0f);
    [SerializeField] private bool enableAttack3Shake = true;
    [SerializeField] private float attack3ShakeMultiplier = 1f;
    [SerializeField] private Vector3 attack3ShakePower = new Vector3(2f, 1f, 0f);

    [Header("跳跃攻击震动")]
    [SerializeField] private bool enableJumpAttackShake = true;
    [SerializeField] private float jumpAttackShakeMultiplier = 1.2f;
    [SerializeField] private Vector3 jumpAttackShakePower = new Vector3(2.5f, 1.2f, 0f);

    private void Start()
    {
        player = FindAnyObjectByType<Player>();
        screenShake = GetComponent<CinemachineImpulseSource>();
    }
    
    public void ShakeScreen()
    {
        if (!enableCritShake)
            return;
        
        screenShake.m_DefaultVelocity = new Vector3(critShakePower.x * player.facingDir, critShakePower.y) * critShakeMultiplier;
        screenShake.GenerateImpulse();
    }

    public void ShakeScreenForCounter()
    {
        if (!enableCounterShake)
            return;
        
        screenShake.m_DefaultVelocity = new Vector3(counterShakePower.x * player.facingDir, counterShakePower.y) * counterShakeMultiplier;
        screenShake.GenerateImpulse();
    }

    public void ShakeScreenForAttack(int attackIndex)
    {
        switch (attackIndex)
        {
            case 1:
                if (!enableAttack1Shake)
                    return;
                screenShake.m_DefaultVelocity = new Vector3(attack1ShakePower.x * player.facingDir, attack1ShakePower.y) * attack1ShakeMultiplier;
                break;
            case 2:
                if (!enableAttack2Shake)
                    return;
                screenShake.m_DefaultVelocity = new Vector3(attack2ShakePower.x * player.facingDir, attack2ShakePower.y) * attack2ShakeMultiplier;
                break;
            case 3:
                if (!enableAttack3Shake)
                    return;
                screenShake.m_DefaultVelocity = new Vector3(attack3ShakePower.x * player.facingDir, attack3ShakePower.y) * attack3ShakeMultiplier;
                break;
            default:
                return;
        }
        
        screenShake.GenerateImpulse();
    }

    public void ShakeScreenForJumpAttack()
    {
        if (!enableJumpAttackShake)
            return;
        
        screenShake.m_DefaultVelocity = new Vector3(jumpAttackShakePower.x * player.facingDir, -jumpAttackShakePower.y) * jumpAttackShakeMultiplier;
        screenShake.GenerateImpulse();
    }

}
