using UnityEngine;
using UnityEngine.UI;

// NPC 对话菜单。场景中公用一份，所有 NPC 共享。
// 放在 UI 系统 Canvas 下，开局隐藏。
public class UI_NpcMenu : MonoBehaviour
{
    // 最后一个交互的 NPC，供子面板关闭后重开菜单使用
    public static NPCBehaviour LastNpc { get; private set; }

    // 子面板关闭时调用，若没有其他面板打开则自动重开 NPC 菜单（经 UIManager 访问，不依赖面板单例）
    public static void TryReopen()
    {
        var mgr = UIManager.Instance;
        if (mgr == null || mgr.NpcMenuComponent == null || LastNpc == null)
            return;
        if (mgr.NpcMenuComponent.gameObject.activeSelf)
            return;
        bool anyOpen = mgr.IsShopOpen ||
            (mgr.QuestDialogueComponent != null && mgr.QuestDialogueComponent.gameObject.activeInHierarchy);
        if (!anyOpen)
            mgr.NpcMenuComponent.Open(LastNpc);
    }

    [SerializeField] private Button questButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button workbenchButton;
    [SerializeField] private Button dialogueButton;
    [SerializeField] private Button leaveButton;

    [SerializeField] private GameObject questButtonRoot;
    [SerializeField] private GameObject shopButtonRoot;
    [SerializeField] private GameObject workbenchButtonRoot;

    private NPCBehaviour currentNpc;

    private void Awake()
    {
        // 注意：不在 Awake 里 SetActive(false)——面板收编后初始 inactive，
        // 首次 Open 的 SetActive(true) 会触发 Awake，若这里再关闭会抵消激活。开局关闭由 UIManager 统一处理。
        questButton.onClick.AddListener(OnQuestClicked);
        shopButton.onClick.AddListener(OnShopClicked);
        workbenchButton.onClick.AddListener(OnWorkbenchClicked);
        dialogueButton.onClick.AddListener(OnDialogueClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    public void Open(NPCBehaviour npc)
    {
        if (gameObject.activeSelf) return; // 防止重复按F导致栈错乱
        LastNpc = npc;
        currentNpc = npc;

        // 任务按钮：有任意任务状态就显示
        if (questButtonRoot != null)
        {
            bool hasQuest = npc != null && (
                npc.HasActiveQuest() ||
                npc.HasAvailableQuest() ||
                npc.HasStageToSubmit() ||
                npc.HasFinalRewardToClaim());
            questButtonRoot.SetActive(hasQuest);
        }

        // 商店按钮：有商店数据才显示
        if (shopButtonRoot != null)
            shopButtonRoot.SetActive(npc != null && npc.shopData != null);

        // 工作台按钮：铁匠（hasWorkbench）才显示
        if (workbenchButtonRoot != null)
            workbenchButtonRoot.SetActive(npc != null && npc.hasWorkbench);

        gameObject.SetActive(true);
        ModalStack.Push("npc_menu");
    }

    public void Close()
    {
        currentNpc = null;
        gameObject.SetActive(false);
        ModalStack.PopAll("npc_menu");
        // 安全复位：防止商店等面板遗留 timeScale=0
        Time.timeScale = 1f;
    }

    private void OnQuestClicked()
    {
        var npc = currentNpc;
        if (npc == null) return;
        gameObject.SetActive(false);
        ModalStack.Pop("npc_menu");

        if (npc.HasStageToSubmit())
        {
            var quest = npc.GetFirstStageToSubmit();
            if (quest != null) UIManager.Instance?.QuestDialogueComponent?.ShowForStageComplete(npc, quest);
            return;
        }

        if (npc.HasFinalRewardToClaim())
        {
            var quest = npc.GetFirstReadyToClaimQuest();
            if (quest != null) UIManager.Instance?.QuestDialogueComponent?.ShowForFinalClaim(npc, quest);
            return;
        }

        if (npc.HasAvailableQuest())
        {
            var quest = npc.GetFirstAvailableQuest();
            if (quest != null) UIManager.Instance?.QuestDialogueComponent?.ShowForAccept(npc, quest);
            return;
        }

        if (npc.HasActiveQuest())
        {
            var quest = npc.GetFirstActiveQuest();
            if (quest != null) UIManager.Instance?.QuestDialogueComponent?.ShowForInProgress(npc, quest);
            return;
        }
    }

    private void OnShopClicked()
    {
        var npc = currentNpc;
        if (npc == null || npc.shopData == null) return;
        gameObject.SetActive(false);
        ModalStack.Pop("npc_menu");

        // 统一走 UIManager 打开商店
        UIManager.Instance?.ShowShop(npc.shopData, npc.npcName ?? "");
    }

    private void OnWorkbenchClicked()
    {
        var npc = currentNpc;
        if (npc == null) return;
        gameObject.SetActive(false);
        ModalStack.Pop("npc_menu");

        // 统一走 UIManager 打开铁匠工作台
        UIManager.Instance?.ShowBlacksmith();
    }

    private void OnDialogueClicked()
    {
        // 对话系统暂未实现，仅关闭菜单（按钮保留占位）
        Close();
    }

    private void OnLeaveClicked()
    {
        Close();
    }
}
