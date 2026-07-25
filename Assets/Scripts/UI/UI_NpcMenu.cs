using UnityEngine;
using UnityEngine.UI;

// NPC 对话菜单。
// 挂在 NPC 菜单 UI 上，按 F 时由 NPCBehaviour 打开。
public class UI_NpcMenu : MonoBehaviour
{
    [SerializeField] private Button questButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button dialogueButton;
    [SerializeField] private Button leaveButton;

    [SerializeField] private GameObject questButtonRoot;
    [SerializeField] private GameObject shopButtonRoot;

    private NPCBehaviour currentNpc;
    private NPCQuestGiver currentGiver;

    private void Awake()
    {
        questButton.onClick.AddListener(OnQuestClicked);
        shopButton.onClick.AddListener(OnShopClicked);
        dialogueButton.onClick.AddListener(OnDialogueClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    public void Open(NPCBehaviour npc, NPCQuestGiver giver)
    {
        currentNpc = npc;
        currentGiver = giver;

        // 任务按钮：有任务交互才显示
        if (questButtonRoot != null)
        {
            bool hasQuest = giver != null && (
                giver.HasAvailableQuest() ||
                giver.HasStageToSubmit() ||
                giver.HasFinalRewardToClaim());
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
        currentGiver = null;
        gameObject.SetActive(false);
        ModalStack.Pop("npc_menu");
    }

    private void OnQuestClicked()
    {
        Close();
        if (currentGiver == null) return;

        // 按优先级处理
        if (currentGiver.HasFinalRewardToClaim())
        {
            var quest = currentGiver.GetFirstReadyToClaimQuest();
            if (quest != null)
                UI_QuestDialogue.Instance?.ShowForFinalClaim(currentGiver, quest);
            return;
        }

        if (currentGiver.HasStageToSubmit())
        {
            var quest = currentGiver.GetFirstStageToSubmit();
            if (quest != null)
                UI_QuestDialogue.Instance?.ShowForStageComplete(currentGiver, quest);
            return;
        }

        if (currentGiver.HasAvailableQuest())
        {
            var quest = currentGiver.GetFirstAvailableQuest();
            if (quest != null)
                UI_QuestDialogue.Instance?.ShowForAccept(currentGiver, quest);
            return;
        }

        if (currentGiver.HasActiveQuest())
        {
            var quest = currentGiver.GetFirstActiveQuest();
            if (quest != null)
                UI_QuestDialogue.Instance?.ShowForInProgress(currentGiver, quest);
            return;
        }
    }

    private void OnShopClicked()
    {
        Close();
        if (currentNpc != null && currentNpc.shopData != null)
            UI_ShopPanel.Instance?.Open(currentNpc.shopData, currentGiver?.npcId ?? "");
    }

    private void OnDialogueClicked()
    {
        Close();
        if (currentGiver != null)
            UI_QuestDialogue.Instance?.ShowGreeting(currentGiver);
    }

    private void OnLeaveClicked()
    {
        Close();
    }
}
