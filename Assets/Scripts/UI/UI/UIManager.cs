using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Networking;   // UI_Chat 在 Networking 命名空间

// UI管理器——唯一单例，统一管理所有面板的显隐/切换/背景/Escape。
// 状态唯一事实源 = ModalStack：各面板 Open 时 Push、Close 时 Pop，Escape 按栈顶分发。
public class UIManager : MonoBehaviour
{
    // 单例（UIManager 常驻 active，Awake 设置）
    public static UIManager Instance { get; private set; }

    // 兼容旧代码：外部仍可读 UIManager.IsAnyPanelOpen
    public static bool IsAnyPanelOpen => ModalStack.IsAnyModalOpen;

    #region 面板切换器
    [Header("面板切换器")]
    [SerializeField] private PanelSwitcher mainPanelSwitcher;       // 主面板（角色/技能/设置/任务）
    [SerializeField] private PanelSwitcher skillPanelSwitcher;      // 技能子面板
    [SerializeField] private PanelSwitcher settingPanelSwitcher;    // 设置子面板
    #endregion

    #region 面板关联组件
    [Header("面板关联组件")]
    [SerializeField] private GameObject panelBackground;   // UI面板背景
    [SerializeField] private GameObject questTrackerPanel; // 小任务面板
    [SerializeField] private GameObject skillSlotsPanel;   // 技能槽面板

    [Header("收编面板（统一入口）")]
    [SerializeField] private GameObject tabMainPanel;     // Tab主面板（角色/技能/设置/任务 容器 + 切换按钮）
    [SerializeField] private GameObject shopPanel;        // 商店面板
    [SerializeField] private GameObject blacksmithPanel;  // 铁匠面板
    [SerializeField] private GameObject npcMenu;          // NPC 对话菜单
    [SerializeField] private GameObject questDialogue;    // 任务对话面板
    [SerializeField] private GameObject deathScreen;      // 死亡界面
    [SerializeField] private GameObject chatPanel;        // 聊天面板
    [SerializeField] private GameObject pauseMenu;        // 暂停菜单（HUD 菜单按钮呼出）
    #endregion

    #region 提示框组件
    [Header("提示框组件")]
    [SerializeField] private GameObject skillTipObject;        // 技能反馈提示框
    [SerializeField] private GameObject skillToolTipObject;    // 技能数据提示框
    [SerializeField] private GameObject itemToolTipObject;     // 物品属性提示框
    [SerializeField] private GameObject statToolTipObject;     // 角色属性提示框
    [SerializeField] private GameObject eventTipObject;        // 事件提示框
    #endregion

    #region 按钮状态颜色设置
    [Header("按钮颜色")]
    [SerializeField] private Color selectColor = Color.white;
    [SerializeField] private Color normalColor = new Color(0.5f, 0.5f, 0.5f);
    #endregion

    // 主面板索引映射（需与 mainPanelSwitcher 的 entries 顺序一致）
    public const int IDX_CHARACTER = 0;
    public const int IDX_SKILL = 1;
    public const int IDX_SETTING = 2;
    public const int IDX_QUEST = 3;

    // 模态栈 id（与各面板 Push/Pop 一致，Escape 按栈顶分发）
    private const string MODAL_SHOP = "shop";
    private const string MODAL_BLACKSMITH = "blacksmith";
    private const string MODAL_NPC_MENU = "npc_menu";
    private const string MODAL_QUEST_DIALOGUE = "quest_dialogue";
    private const string MODAL_MAIN_PANEL = "panel";
    private const string MODAL_PAUSE = "pause";

    // ==================== 公开状态查询/访问器（供外部面板逻辑使用） ====================

    // 商店是否打开（NPCBehaviour 交互判断）
    public bool IsShopOpen => shopPanel != null && shopPanel.activeInHierarchy;

    // 关闭商店（Escape/商店关闭按钮/NPC 交互统一入口）：内容清理 + 出栈 + 恢复 + 背景 + 重开NPC菜单
    public void CloseShop()
    {
        if (shopPanel == null)
            return;
        shopPanel.GetComponent<UI_ShopPanel>()?.Close();
        ModalStack.Pop(MODAL_SHOP);   // 出栈
        Time.timeScale = 1f;          // 恢复游戏
        SetPanelBackground(false);    // 隐藏统一背景
        UI_NpcMenu.TryReopen();       // 商店关闭后若无其他面板，重开 NPC 菜单
    }

    // 任务对话面板组件（UI_NpcMenu 等经 UIManager 访问，不再依赖面板单例）
    public UI_QuestDialogue QuestDialogueComponent =>
        questDialogue != null ? questDialogue.GetComponent<UI_QuestDialogue>() : null;

    // NPC 对话菜单组件（TryReopen 等经 UIManager 访问）
    public UI_NpcMenu NpcMenuComponent =>
        npcMenu != null ? npcMenu.GetComponent<UI_NpcMenu>() : null;

    // ==================== 初始化 ====================

    // 绑定 Tab主面板 内的面板切换按钮（Tab主面板 inactive 时按钮 Awake 不执行，由 UIManager 常驻绑定）
    private void BindTabButtons()
    {
        if (tabMainPanel == null)
            return;

        var buttons = tabMainPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var b in buttons)
        {
            if (b.name.Contains("角色")) b.onClick.AddListener(ShowCharacterPanel);
            else if (b.name.Contains("技能")) b.onClick.AddListener(ShowSkillPanel);
            else if (b.name.Contains("设置")) b.onClick.AddListener(ShowSettingPanel);
            else if (b.name.Contains("任务")) b.onClick.AddListener(ShowQuestPanel);
        }
    }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject); // 跨场景持久（方案A）：HUD + 常用面板随 UI系统 整体保留
        SceneManager.sceneLoaded += OnSceneLoadedForPersist;

        BindTabButtons(); // 绑定 Tab主面板 内面板切换按钮

        // 监听主面板切换事件
        mainPanelSwitcher.OnPanelShown += OnMainPanelShown;
        mainPanelSwitcher.OnAllHidden += OnMainPanelHidden;

        // 开局关闭所有面板
        mainPanelSwitcher.HideAll();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedForPersist;
    }

    // UI 系统显隐保证机制：菜单场景隐藏整个 UI，游戏场景强制显示（覆盖默认隐藏/被误关的兜底）
    private void ApplySceneVisibility(Scene scene)
    {
        gameObject.SetActive(scene.name != "主菜单");
    }

    // 主菜单场景隐藏整个 UI，进入游戏场景恢复（避免 HUD 盖在菜单上）
    private void OnSceneLoadedForPersist(Scene scene, LoadSceneMode mode)
    {
        ApplySceneVisibility(scene);
    }

    private void Start()
    {
        UI_ButtonEffect.HookAll();

        // 确保开局全关（覆盖 PanelSwitcher.Start 可能触发的自动显示）
        mainPanelSwitcher.HideAll();
        CloseAllPanelsAtStart();

        // 暂停菜单自发现：未在 Inspector 拖入时自动找场景中的 UI_PauseMenu（简化接线，无需手动拖引用）
        if (pauseMenu == null)
        {
            var pm = FindAnyObjectByType<UI_PauseMenu>(FindObjectsInactive.Include);
            if (pm != null)
                pauseMenu = pm.gameObject;
        }

        // 世界时间统一由 UIManager 管理：启动强制复位，避免残留 timeScale=0 导致游戏冻结
        Time.timeScale = 1f;

        // 保证机制：启动即按当前场景修正 UI 显隐。
        // Start 可能被延后到首次激活时（菜单场景被隐藏后、进入游戏场景才首次运行），
        // 此时仍按当前场景强制保证显示，兜底 sceneLoaded 事件漏触发的场景
        ApplySceneVisibility(SceneManager.GetActiveScene());
    }

    // 开局统一关闭所有收编面板（各面板不再在自身 Awake 里关闭，避免首次 Open 被 Awake 抵消）
    // 不关聊天根——聊天根需保持激活以跑 Update，聊天面板子对象由 UI_Chat 自管显隐
    private void CloseAllPanelsAtStart()
    {
        if (tabMainPanel != null) tabMainPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (blacksmithPanel != null) blacksmithPanel.SetActive(false);
        if (npcMenu != null) npcMenu.SetActive(false);
        if (questDialogue != null) questDialogue.SetActive(false);
        if (deathScreen != null) deathScreen.SetActive(false);
        if (pauseMenu != null) pauseMenu.SetActive(false);
    }

    // ==================== 主面板事件处理 ====================

    // 主面板显示时：背景/技能槽/提示框/小任务/子面板复位
    private void OnMainPanelShown(int index)
    {
        // 面板切换时 HideAllPanels 不触发 OnAllHidden，先清理旧 panel 条码
        ModalStack.PopAll("panel");
        ModalStack.Push("panel");

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

    // 主面板全部关闭时：恢复背景/技能槽/小任务
    private void OnMainPanelHidden()
    {
        ModalStack.Pop("panel");

        if (panelBackground != null)
            panelBackground.SetActive(false);

        ShowSkillSlots();
        HideAllToolTips();

        if (questTrackerPanel != null)
            questTrackerPanel.SetActive(true);
    }

    // ==================== 公开方法（按钮调用） ====================

    public void ShowCharacterPanel() => OpenMainPanel(IDX_CHARACTER);
    public void ShowSkillPanel()     => OpenMainPanel(IDX_SKILL);
    public void ShowSettingPanel()   => OpenMainPanel(IDX_SETTING);
    public void ShowQuestPanel()     => OpenMainPanel(IDX_QUEST);

    // 关闭所有主面板（含 Tab主面板 容器）
    public void HideAllPanels()
    {
        mainPanelSwitcher.HideAll();
        if (tabMainPanel != null)
            tabMainPanel.SetActive(false);
    }

    // 打开主面板：先激活 Tab主面板 容器，再切换子面板
    private void OpenMainPanel(int index)
    {
        if (tabMainPanel != null)
            tabMainPanel.SetActive(true);
        mainPanelSwitcher.ShowPanel(index);
    }

    // ==================== 收编面板统一入口（NPC 等调用） ====================

    // 打开商店（商人/铁匠 NPC 调用）：隐藏主面板 + 激活根 + 背景 + 面板内容 + 入栈 + 暂停
    public void ShowShop(ShopSO shopData, string npcName = "")
    {
        if (shopData == null)
            return;
        HideAllPanels();
        if (shopPanel != null)
        {
            EnsurePanelRootActive(shopPanel);
            SetPanelBackground(true); // 统一背景
            shopPanel.GetComponent<UI_ShopPanel>()?.Open(shopData, npcName);
            ModalStack.Push(MODAL_SHOP); // 入栈：阻塞游戏输入
            Time.timeScale = 0f;         // 暂停游戏
        }
    }

    // 打开铁匠工作台（制作/分解/合成）：隐藏主面板 + 激活根 + 背景 + 面板内容 + 入栈 + 暂停
    public void ShowBlacksmith()
    {
        HideAllPanels();
        if (blacksmithPanel != null)
        {
            EnsurePanelRootActive(blacksmithPanel);
            SetPanelBackground(true); // 统一背景
            blacksmithPanel.GetComponent<UI_BlacksmithPanel>()?.Open();
            ModalStack.Push(MODAL_BLACKSMITH); // 入栈：阻塞游戏输入
            Time.timeScale = 0f;              // 暂停游戏
        }
    }

    // 关闭铁匠工作台（Escape/铁匠关闭按钮统一入口）：内容清理 + 出栈 + 恢复 + 背景
    public void CloseBlacksmith()
    {
        if (blacksmithPanel == null)
            return;
        blacksmithPanel.GetComponent<UI_BlacksmithPanel>()?.Close();
        ModalStack.Pop(MODAL_BLACKSMITH); // 出栈
        Time.timeScale = 1f;              // 恢复游戏
        SetPanelBackground(false);        // 隐藏统一背景
    }

    // 打开暂停菜单（HUD 菜单按钮/Escape 呼出）：隐藏主面板 + 背景 + 入栈 + 暂停
    public void ShowPauseMenu()
    {
        if (pauseMenu == null)
            return;
        HideAllPanels(); // 先关主面板/子面板，避免与暂停菜单叠放
        SetPanelBackground(true); // 统一背景
        pauseMenu.GetComponent<UI_PauseMenu>()?.Open();
        ModalStack.Push(MODAL_PAUSE); // 入栈：阻塞游戏输入（GameInput.IsGameBlocked）
        Time.timeScale = 0f;          // 暂停游戏
    }

    // 关闭暂停菜单（Escape/继续游戏按钮统一入口）
    public void ClosePauseMenu()
    {
        if (pauseMenu == null)
            return;
        pauseMenu.GetComponent<UI_PauseMenu>()?.Close();
        ModalStack.Pop(MODAL_PAUSE); // 出栈
        Time.timeScale = 1f;         // 恢复游戏
        SetPanelBackground(false);   // 隐藏统一背景
    }

    // 暂停菜单是否打开（供 HUD 按钮判断/防重复打开）
    public bool IsPauseMenuOpen => ModalStack.Top == MODAL_PAUSE;

    // 激活面板根对象（面板可能挂在默认 inactive 的 Canvas 下，打开前确保根激活）
    private void EnsurePanelRootActive(GameObject panel)
    {
        if (panel != null)
            panel.transform.root.gameObject.SetActive(true);
    }

    // 打开 NPC 对话菜单（按 F 交互）
    public void ShowNpcMenu(NPCBehaviour npc)
    {
        if (npc == null || npcMenu == null)
            return;
        npcMenu.GetComponent<UI_NpcMenu>()?.Open(npc);
    }

    // 打开死亡界面（玩家死亡时调用）——先激活根（开局已统一关闭），再显示内容
    public void ShowDeathScreen()
    {
        if (deathScreen != null)
        {
            deathScreen.SetActive(true);
            var death = deathScreen.GetComponent<UI_DeathScreen>();
            Debug.Log($"[UIManager] ShowDeathScreen: deathScreen={(deathScreen != null)} ui={death != null}");
            death?.Show();
        }
        else
        {
            Debug.LogWarning("[UIManager] ShowDeathScreen: deathScreen 引用为 null！");
        }
    }

    // 聊天面板显隐（收编统一入口）
    public void ShowChat(bool show)
    {
        if (chatPanel != null)
            chatPanel.GetComponent<UI_Chat>()?.ShowChat(show);
    }

    // 统一背景遮罩显隐
    private void SetPanelBackground(bool show)
    {
        if (panelBackground != null)
            panelBackground.SetActive(show);
    }

    // ==================== 按键控制 ====================

    private void Update()
    {
        // Escape 统一关闭：聊天 → 模态栈顶 → 主面板
        if (GameInput.GetKeyDown(GameInput.Action.Escape))
            HandleEscape();

        // 商店/铁匠打开时禁止切换主面板
        string top = ModalStack.Top;
        if (top == MODAL_SHOP || top == MODAL_BLACKSMITH)
            return;

        if (GameInput.GetKeyDown(GameInput.Action.ToggleCharacterPanel))
            TogglePanelWithKey(IDX_CHARACTER);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleSkillPanel))
            TogglePanelWithKey(IDX_SKILL);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleSettingsPanel))
            TogglePanelWithKey(IDX_SETTING);

        if (GameInput.GetKeyDown(GameInput.Action.ToggleQuestPanel))
            TogglePanelWithKey(IDX_QUEST);
    }

    // Escape 按优先级关闭：聊天聚焦 → 模态栈顶（商店/铁匠/NPC菜单/任务对话）→ 主面板
    private void HandleEscape()
    {
        // 聊天输入框聚焦时 Esc 只关聊天，避免连带关下面板
        if (Networking.UI_Chat.IsChatFocused)
        {
            ShowChat(false);
            return;
        }

        switch (ModalStack.Top)
        {
            case MODAL_SHOP:
                CloseShop();
                break;
            case MODAL_BLACKSMITH:
                CloseBlacksmith();
                break;
            case MODAL_NPC_MENU:
                npcMenu?.GetComponent<UI_NpcMenu>()?.Close();
                break;
            case MODAL_QUEST_DIALOGUE:
                questDialogue?.GetComponent<UI_QuestDialogue>()?.Close();
                break;
            case MODAL_MAIN_PANEL:
                mainPanelSwitcher.HideAll();
                break;
            case MODAL_PAUSE:
                ClosePauseMenu();
                break;
        }
    }

    // 按键切换：同一面板再按关闭（含容器），不同面板切换，无面板时打开。
    // 必须走 OpenMainPanel 激活 tabMainPanel 容器，否则容器 inactive 时子面板即使激活也不显示。
    private void TogglePanelWithKey(int index)
    {
        if (mainPanelSwitcher.CurrentIndex == index)
        {
            mainPanelSwitcher.HideAll();
            if (tabMainPanel != null)
                tabMainPanel.SetActive(false); // 关闭时收起容器
            return;
        }
        OpenMainPanel(index); // 打开/切换：激活容器 + 切换子面板
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

        if (eventTipObject != null)
            eventTipObject.GetComponent<UI_EventTip>()?.ForceHideTip();
    }
}
