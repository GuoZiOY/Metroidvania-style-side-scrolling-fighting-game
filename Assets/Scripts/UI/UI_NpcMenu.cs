using UnityEngine;
using UnityEngine.UI;

// NPC 对话菜单。场景中公用一份，所有 NPC 共享。
// 放在 UI 系统 Canvas 下，开局隐藏。
public class UI_NpcMenu : MonoBehaviour
{
    public static UI_NpcMenu Instance { get; private set; }

    /// <summary>最后一个交互的 NPC，供子面板关闭后重开菜单使用</summary>
    public static NPCBehaviour LastNpc { get; private set; }

    /// <summary>子面板关闭时调用，若没有其他面板打开则自动重开 NPC 菜单</summary>
    public static void TryReopen()
    {
        if (Instance == null || LastNpc == null) return;
        if (Instance.gameObject.activeSelf) return;
        bool anyOpen = UI_ShopPanel.IsShopOpen ||
            (UI_QuestDialogue.Instance != null && UI_QuestDialogue.Instance.gameObject.activeInHierarchy);
        if (!anyOpen)
        {
            Instance.Open(LastNpc);
        }
    }

    [SerializeField] private Button questButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button dialogueButton;
    [SerializeField] private Button leaveButton;

    [SerializeField] private GameObject questButtonRoot;
    [SerializeField] private GameObject shopButtonRoot;

    private NPCBehaviour currentNpc;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);

        questButton.onClick.AddListener(OnQuestClicked);
        shopButton.onClick.AddListener(OnShopClicked);
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
            if (quest != null) UI_QuestDialogue.Instance?.ShowForStageComplete(npc, quest);
            return;
        }

        if (npc.HasFinalRewardToClaim())
        {
            var quest = npc.GetFirstReadyToClaimQuest();
            if (quest != null) UI_QuestDialogue.Instance?.ShowForFinalClaim(npc, quest);
            return;
        }

        if (npc.HasAvailableQuest())
        {
            var quest = npc.GetFirstAvailableQuest();
            if (quest != null) UI_QuestDialogue.Instance?.ShowForAccept(npc, quest);
            return;
        }

        if (npc.HasActiveQuest())
        {
            var quest = npc.GetFirstActiveQuest();
            if (quest != null) UI_QuestDialogue.Instance?.ShowForInProgress(npc, quest);
            return;
        }
    }

    private void OnShopClicked()
    {
        var npc = currentNpc;
        if (npc == null || npc.shopData == null) return;
        gameObject.SetActive(false);
        ModalStack.Pop("npc_menu");

        UI_ShopPanel.Instance?.Open(npc.shopData, npc.npcName ?? "");
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
