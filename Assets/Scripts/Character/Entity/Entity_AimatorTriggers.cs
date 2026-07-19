using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Entity_AimatorTriggers : MonoBehaviour
{

    private Entity entity;
    private Entity_Combat entityCombat;

    protected virtual void Awake()
    {
        entity = GetComponentInParent <Entity>();
        entityCombat = GetComponentInParent <Entity_Combat>();
    }
    public void CurrentStateTrigger()
    {
        entity.CurrentState_AnimationTriggers();
    }

    public void AttackTrigger()
    {
        entityCombat.PerformAttack();
    }

    public void FootstepTrigger()
    {
        AudioManager.Instance?.PlayFootstepSfx();
    }
}
