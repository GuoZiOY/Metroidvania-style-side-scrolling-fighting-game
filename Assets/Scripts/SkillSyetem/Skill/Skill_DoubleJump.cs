using UnityEngine;

public class Skill_DoubleJump : Skill_Base
{
    [SerializeField] private float doubleJumpForce = 10f;

    protected override void Awake()
    {
        base.Awake();
    }

    public bool CanDoubleJump()
    {
        return Unlocked(SkillUpgradeType.DoubleJump);
    }

    public float GetDoubleJumpForce()
    {
        return doubleJumpForce;
    }
}