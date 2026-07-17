using UnityEngine;

public class UI : MonoBehaviour
{
    
    public UI_SkillToolTip skillToolTip;
    public UI_StatToolTip statToolTip;

    public UI_ItemToolTip itemToolTip;
    public UI_SkillTree skillTree;
    public UI_SkillTip skillTip;

    [Header("设置UI")]
    public UI_Setting Setting;

    private void Awake()
    {
        skillToolTip = GetComponentInChildren<UI_SkillToolTip>(true);
        statToolTip = GetComponentInChildren<UI_StatToolTip>(true);
        itemToolTip = GetComponentInChildren<UI_ItemToolTip>(true);
        skillTree = GetComponentInChildren<UI_SkillTree>(true);
        skillTip = GetComponentInChildren<UI_SkillTip>(true);
        Setting = GetComponentInChildren<UI_Setting>();
    }

    // 移除ToggleSkillTreeUI方法，该功能已集成到UIManager.cs中

    // 移除SettingUI方法，该功能将通过UIManager统一管理
}