using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI管理器 - 面板切换委托给 PanelSwitcher，自身专注额外逻辑。
/// </summary>
public class UIManager : MonoBehaviour
{
    public static bool IsAnyPanelOpen { get; private set; }
    #region 面板切换器
    [Header("面板切换器")]
    [SerializeField] private PanelSwitcher mainPanelSwitcher;       // 主面板（角色/技能/设置/任务）
    [SerializeField] private PanelSwitcher skillPanelSwitcher;      // 技能子面板
    [SerializeField] private PanelSwitcher settingPanelSwitcher;     // 设置子面板
    #endregion

    #region 面板关联组件
    [Header("面板关联组件")]
    public GameObject panelBackground;   // UI面板背景
    public GameObject questTrackerPanel; // 小任务面板
    public GameObject skillSlotsPanel;   // 技能槽面板
    #endregion

    #region 提示框组件
    [Header("提示框组件")]
    public GameObject skillTipObject;        // 技能反馈提示框
    public GameObject skillToolTipObject;    // 技能数据提示框
    public GameObject itemToolTipObject;     // 物品属性提示框
    public GameObject statToolTipObject;     // 角色属性提示框
    #endregion

    #region 按钮状态颜色设置
    [Header("按钮颜色")]
    public Color selectColor = Color.white;
    public Color normalColor = new Color(0.5f, 0.5f, 0.5f);
    #endregion

    /// <summary>
    /// 主面板索引映射（需与 mainPanelSwitcher 的 entries 顺序一致）
    /// </summary>
    public const int IDX_CHARACTER = 0;
    public const int IDX_SKILL = 1;
    public const int IDX_SETTING = 2;
    public const int IDX_QUEST = 3;

    // ==================== 初始化 ====================

    private void Awake()
    {
        // 监听主面板切换事件
        mainPanelSwitcher.OnPanelShown += OnMainPanelShown;
        mainPanelSwitcher.OnAllHidden += OnMainPanelHidden;

        // 开局关闭所有面板
        mainPanelSwitcher.HideAll();
    }

    private void Start()
    {
        UI_ButtonEffect.HookAll();

        // 确保开局全关（覆盖 PanelSwitcher.Start 可能触发的自动显示）
        mainPanelSwitcher.HideAll();
    }

    // ==================== 主面板事件处理 ====================

    /// <summary>主面板显示时：背景/技能槽/提示框/小任务/子面板复位</summary>
    private void OnMainPanelShown(int index)
    {
        IsAnyPanelOpen = true;

        // 背景
        if (panelBackground != null)
            panelBackground.SetActive(true);

        // 技能槽显隐
        if (index == IDX_SKILL)
            ShowSkillSlots();
        else
            HideSkillSlots();

        // 隐藏小任务面板
        if (questTrackerPanel != null)
            questTrackerPanel.SetActive(false);

        // 隐藏所有提示框
        HideAllToolTips();

        // 子面板切换器复位
        if (index == IDX_SKILL)
            skillPanelSwitcher?.ShowPanel(0);
        else if (index == IDX_SETTING)
            settingPanelSwitcher?.ShowPanel(0);
    }

    /// <summary>主面板全部关闭时：恢复背景/技能槽/小任务</summary>
    private void OnMainPanelHidden()
    {
        IsAnyPanelOpen = false;

        if (panelBackground != null)
            panelBackground.SetActive(false);

        ShowSkillSlots();
        HideAllToolTips();

        if (questTrackerPanel != null)
            questTrackerPanel.SetActive(true);
    }

    // ==================== 公开方法（按钮调用） ====================

    public void ShowCharacterPanel() => mainPanelSwitcher.ShowPanel(IDX_CHARACTER);
    public void ShowSkillPanel()     => mainPanelSwitcher.ShowPanel(IDX_SKILL);
    public void ShowSettingPanel()   => mainPanelSwitcher.ShowPanel(IDX_SETTING);
    public void ShowQuestPanel()     => mainPanelSwitcher.ShowPanel(IDX_QUEST);

    // ==================== 按键控制 ====================

    private void Update()
    {
        if (GameInput.GetKeyDown(GameInput.Action.ToggleCharacterPanel))
            TogglePanelWithKey(IDX_CHARACTER);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleSkillPanel))
            TogglePanelWithKey(IDX_SKILL);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleSettingsPanel))
            TogglePanelWithKey(IDX_SETTING);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleQuestPanel))
            TogglePanelWithKey(IDX_QUEST);
    }

    /// <summary>按键切换：同一面板再按关闭，不同面板切换，无面板时打开</summary>
    private void TogglePanelWithKey(int index)
    {
        if (mainPanelSwitcher.CurrentIndex == index)
            mainPanelSwitcher.HideAll();
        else
            mainPanelSwitcher.ShowPanel(index);
    }

    // ==================== 技能槽 ====================

    private void ShowSkillSlots()
    {
        if (skillSlotsPanel != null)
            skillSlotsPanel.SetActive(true);
    }

    private void HideSkillSlots()
    {
        if (skillSlotsPanel != null)
            skillSlotsPanel.SetActive(false);
    }

    // ==================== 隐藏提示框 ====================

    private void HideAllToolTips()
    {
        if (skillTipObject != null)
            skillTipObject.GetComponent<UI_SkillTip>()?.ForceHideTip();

        if (skillToolTipObject != null)
            skillToolTipObject.GetComponent<UI_SkillToolTip>()?.ShowToolTip(false, null, null);

        if (itemToolTipObject != null)
            itemToolTipObject.GetComponent<UI_ItemToolTip>()?.ShowToolTip(false, null, null);

        if (statToolTipObject != null)
            statToolTipObject.GetComponent<UI_StatToolTip>()?.ShowToolTip(false, null, StatType.MaxHP);
    }
}
