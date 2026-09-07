using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillObject_Health : Entity_Health
{
    protected override void Die()
    {
        SkillObject_TimeEcho timeEcho = GetComponent<SkillObject_TimeEcho>();
        Debug.Log(gameObject.name + " À¿Õˆ");
        timeEcho.HandleDeath();
    }
}
