using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI管理器 - 负责管理游戏中所有UI面板的显示和切换
/// </summary>
public class UIManager : MonoBehaviour
{
    #region 主面板组件
    [Header("主面板组件")]
    public GameObject panelBackground;  // UI面板背景，可选
    public GameObject characterPanel;   // 角色主面板
    public GameObject skillPanel;       // 技能主面板
    public GameObject settingPanel;     // 设置主面板
    public GameObject questPanel;       // 任务面板
    public GameObject questTrackerPanel; // 小任务面板
    public GameObject skillSlotsPanel;  // 技能槽面板
    #endregion

    #region 技能提示框组件
    public GameObject skillTipObject;        // 技能反馈提示框GameObject
    public GameObject skillToolTipObject;    // 技能数据提示框GameObject
    #endregion

    #region 其他提示框组件
    public GameObject itemToolTipObject;     // 物品属性提示框GameObject
    public GameObject statToolTipObject;     // 角色属性提示框GameObject
    #endregion

    #region 主面板切换按钮
    [Header("主面板切换按钮")]
    public Button characterBtn;         // 角色按钮
    public Button skillBtn;             // 技能按钮
    public Button settingBtn;           // 设置按钮
    public Button questBtn;             // 任务按钮
    #endregion

    #region 子面板切换器
    [Header("子面板切换器")]
    [SerializeField] private PanelSwitcher skillPanelSwitcher;
    [SerializeField] private PanelSwitcher settingPanelSwitcher;
    #endregion

    #region 按钮状态颜色设置

    public Color selectColor = new Color(0.7f, 0.7f, 0.7f);  // 选中时的颜色
    public Color normalColor = Color.white;                  // 未选中时的颜色
    #endregion

    /// <summary>
    /// 组件初始化 - 添加按钮点击事件监听
    /// </summary>
    private void Awake()
    {
        characterBtn.onClick.AddListener(ShowCharacterPanel);
        skillBtn.onClick.AddListener(ShowSkillPanel);
        settingBtn.onClick.AddListener(ShowSettingPanel);
        if (questBtn != null)
            questBtn.onClick.AddListener(ShowQuestPanel);

        HideAllPanels();
        ResetAllButtonStatus();
    }

    private void Start()
    {
        // 默认隐藏所有面板
        HideAllPanels();
        ResetAllButtonStatus();
    }

    #region 主面板显示方法
    public void ShowCharacterPanel()
    {
        ShowPanel(characterPanel, characterBtn);
    }

    public void ShowSkillPanel()
    {
        ShowPanel(skillPanel, skillBtn);
        skillPanelSwitcher?.ShowPanel(0);
    }

    public void ShowSettingPanel()
    {
        ShowPanel(settingPanel, settingBtn);
        settingPanelSwitcher?.ShowPanel(0);
    }

    public void ShowQuestPanel()
    {
        ShowPanel(questPanel, questBtn);
    }

    private void ShowPanel(GameObject panel, Button button)
    {
        HideAllPanels();
        panel.SetActive(true);
        if (panelBackground != null)
            panelBackground.SetActive(true);
        UpdateMainBtnStatus(button);
        UpdateSkillSlotsVisibility(panel);
        HideAllToolTips();
        if (questTrackerPanel != null)
            questTrackerPanel.SetActive(false);
    }
    #endregion

    #region 通用工具方法
    /// <summary>
    /// 隐藏所有面板（主面板+子面板）
    /// </summary>
    private void HideAllPanels()
    {
        characterPanel.SetActive(false);
        skillPanel.SetActive(false);
        settingPanel.SetActive(false);
        if (questPanel != null)
            questPanel.SetActive(false);
        if (panelBackground != null)
            panelBackground.SetActive(false);
        ShowSkillSlots();
        HideAllToolTips();
        if (questTrackerPanel != null)
            questTrackerPanel.SetActive(true);
    }

    private void ResetAllButtonStatus()
    {
        SetButtonColor(characterBtn, normalColor);
        SetButtonColor(skillBtn, normalColor);
        SetButtonColor(settingBtn, normalColor);
        if (questBtn != null)
            SetButtonColor(questBtn, normalColor);
    }

    /// <summary>
    /// 统一修改按钮的颜色设置
    /// </summary>
    /// <param name="targetBtn">目标按钮</param>
    /// <param name="targetColor">目标颜色</param>
    private void SetButtonColor(Button targetBtn, Color targetColor)
    {
        ColorBlock colorBlock = targetBtn.colors;
        colorBlock.normalColor = targetColor;       // 常态颜色
        colorBlock.highlightedColor = targetColor;  // 高亮颜色
        colorBlock.pressedColor = targetColor;      // 按下颜色
        colorBlock.selectedColor = targetColor;     // 按钮选中颜色
        targetBtn.colors = colorBlock;
    }

    private void UpdateMainBtnStatus(Button selectBtn)
    {
        ResetAllButtonStatus();
        SetButtonColor(selectBtn, selectColor);
    }

    /// <summary>
    /// 根据面板类型更新技能槽显示状态
    /// </summary>
    /// <param name="panel">当前显示的面板</param>
    private void UpdateSkillSlotsVisibility(GameObject panel)
    {
        if (panel == skillPanel)
        {
            // 打开技能面板时显示技能槽
            ShowSkillSlots();
        }
        else if (panel == characterPanel || panel == settingPanel)
        {
            // 打开角色面板或设置面板时隐藏技能槽
            HideSkillSlots();
        }
    }

    /// <summary>
    /// 显示技能槽
    /// </summary>
    private void ShowSkillSlots()
    {
        if (skillSlotsPanel != null)
        {
            skillSlotsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 隐藏技能槽
    /// </summary>
    private void HideSkillSlots()
    {
        if (skillSlotsPanel != null)
        {
            skillSlotsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 隐藏所有提示框
    /// </summary>
    private void HideAllToolTips()
    {
        // 关闭技能反馈提示框
        if (skillTipObject != null)
        {
            UI_SkillTip skillTip = skillTipObject.GetComponent<UI_SkillTip>();
            if (skillTip != null)
            {
                skillTip.ForceHideTip();
            }
        }
        // 关闭技能数据提示框
        if (skillToolTipObject != null)
        {
            UI_SkillToolTip skillToolTip = skillToolTipObject.GetComponent<UI_SkillToolTip>();
            if (skillToolTip != null)
            {
                skillToolTip.ShowToolTip(false, null, null);
            }
        }
        // 关闭物品属性提示框
        if (itemToolTipObject != null)
        {
            UI_ItemToolTip itemToolTip = itemToolTipObject.GetComponent<UI_ItemToolTip>();
            if (itemToolTip != null)
            {
                itemToolTip.ShowToolTip(false, null, null);
            }
        }
        // 关闭角色属性提示框
        if (statToolTipObject != null)
        {
            UI_StatToolTip statToolTip = statToolTipObject.GetComponent<UI_StatToolTip>();
            if (statToolTip != null)
            {
                statToolTip.ShowToolTip(false, null, StatType.MaxHP);
            }
        }
    }
    #endregion

    #region 按键控制UI显示/隐藏
    /// <summary>
    /// 检测按键输入，控制UI显示
    /// </summary>
    private void Update()
    {
        // Tab键 - 切换主面板显示/隐藏，默认显示角色面板
        if (GameInput.GetKeyDown(GameInput.Action.ToggleCharacterPanel))
            TogglePanelWithKey(ShowCharacterPanel);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleSkillPanel))
            TogglePanelWithKey(ShowSkillPanel);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleSettingsPanel))
            TogglePanelWithKey(ShowSettingPanel);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleQuestPanel))
            TogglePanelWithKey(ShowQuestPanel);
    }

    /// <summary>
    /// 通用按键面板切换逻辑
    /// </summary>
    /// <param name="showTargetPanel">要显示的目标面板方法</param>
    private void TogglePanelWithKey(System.Action showTargetPanel)
    {
        // 检查当前是否有主面板可见
        bool isAnyMainPanelVisible = characterPanel.activeSelf || skillPanel.activeSelf || settingPanel.activeSelf;

        if (isAnyMainPanelVisible)
        {
            // 检查当前是否是目标面板
            bool isCurrentPanelTarget = IsCurrentPanelTarget(showTargetPanel);
            
            if (isCurrentPanelTarget)
            {
                // 如果当前是目标面板，则关闭所有面板
                HideAllPanels();
                ResetAllButtonStatus();
            }
            else
            {
                // 如果当前不是目标面板，则切换到目标面板
                showTargetPanel?.Invoke();
            }
        }
        else
        {
            // 如果主面板未打开，则显示目标面板
            showTargetPanel?.Invoke();
        }
    }

    /// <summary>
    /// 检查当前显示的面板是否是目标面板
    /// </summary>
    /// <param name="showTargetPanel">目标面板的显示方法</param>
    /// <returns>是否是目标面板</returns>
    private bool IsCurrentPanelTarget(System.Action showTargetPanel)
    {
        // 根据传递的显示方法判断目标面板
        if (showTargetPanel == ShowCharacterPanel)
        {
            return characterPanel.activeSelf;
        }
        else if (showTargetPanel == ShowSkillPanel)
        {
            return skillPanel.activeSelf;
        }
        else if (showTargetPanel == ShowSettingPanel)
        {
            return settingPanel.activeSelf;
        }
        else if (showTargetPanel == ShowQuestPanel)
        {
            return questPanel != null && questPanel.activeSelf;
        }
        return false;
    }
    #endregion
}