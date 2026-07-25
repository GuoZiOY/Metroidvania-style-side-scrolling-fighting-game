using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 任务对话面板。轻量弹窗，和 NPC 交互时使用。
// 挂在预制体上，单例。
public class UI_QuestDialogue : MonoBehaviour
{
    private static UI_QuestDialogue instance;
    public static UI_QuestDialogue Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<UI_QuestDialogue>(FindObjectsInactive.Include);
            return instance;
        }
    }

    [Header("UI 组件")]
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private RectTransform questInfoRoot;
    [SerializeField] private TextMeshProUGUI questInfoText;
    [SerializeField] private TextMeshProUGUI questProgressText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TextMeshProUGUI primaryButtonText;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TextMeshProUGUI secondaryButtonText;

    private NPCQuestGiver currentGiver;
    private QuestData currentQuest;
    private DialogueMode currentMode;

    private enum DialogueMode
    {
        Accept,         // 接取
        InProgress,     // 进行中
        StageComplete,  // 阶段完成
        FinalClaim,     // 最终领奖
        Greeting,       // 问候
    }

    private void Awake()
    {
        instance = this;
        gameObject.SetActive(false);

        if (primaryButton != null)
            primaryButton.onClick.AddListener(OnPrimaryClicked);
        if (secondaryButton != null)
            secondaryButton.onClick.AddListener(OnSecondaryClicked);
    }

    // ─── 公开方法 ───

    public void ShowForAccept(NPCQuestGiver giver, QuestData quest)
    {
        currentGiver = giver;
        currentQuest = quest;
        currentMode = DialogueMode.Accept;

        SetupUI(
            npcName: GetNpcName(giver),
            dialogue: GetStageDialogue(quest, 0, d => d.startDialogue),
            questInfo: $"{quest.questName}  [{GetQuestTypeName(quest.questType)}]",
            progress: BuildObjectiveText(quest, 0),
            reward: BuildRewardText(quest.GetFirstStage()?.stageReward, quest.finalReward),
            primaryText: "接受",
            secondaryText: "下次再说"
        );

        Open();
    }

    public void ShowForInProgress(NPCQuestGiver giver, QuestData quest)
    {
        currentGiver = giver;
        currentQuest = quest;
        currentMode = DialogueMode.InProgress;

        var stage = QuestManager.Instance?.GetCurrentStage(quest.questId);
        var progress = QuestManager.Instance?.GetStageProgress(quest.questId);

        SetupUI(
            npcName: GetNpcName(giver),
            dialogue: stage?.inProgressDialogue ?? "……",
            questInfo: $"{quest.questName}  [{GetQuestTypeName(quest.questType)}]",
            progress: progress != null && stage != null
                ? BuildObjectiveText(stage.objectives, progress) : "",
            reward: "",
            primaryText: "继续",
            secondaryText: ""
        );

        Open();
    }

    public void ShowForStageComplete(NPCQuestGiver giver, QuestData quest)
    {
        currentGiver = giver;
        currentQuest = quest;
        currentMode = DialogueMode.StageComplete;

        var stage = QuestManager.Instance?.GetCurrentStage(quest.questId);

        SetupUI(
            npcName: GetNpcName(giver),
            dialogue: stage?.completeDialogue ?? "完成了。",
            questInfo: $"{quest.questName}  — 阶段完成!",
            progress: BuildObjectiveText(quest, GetStageIndex(quest, stage)),
            reward: stage != null ? BuildRewardText(stage.stageReward, null) : "",
            primaryText: "继续",
            secondaryText: ""
        );

        Open();
    }

    public void ShowForFinalClaim(NPCQuestGiver giver, QuestData quest)
    {
        currentGiver = giver;
        currentQuest = quest;
        currentMode = DialogueMode.FinalClaim;

        var stage = QuestManager.Instance?.GetCurrentStage(quest.questId);

        SetupUI(
            npcName: GetNpcName(giver),
            dialogue: stage?.completeDialogue ?? "这是你的报酬。",
            questInfo: $"{quest.questName}  — 全部完成!",
            progress: "",
            reward: BuildRewardText(null, quest.finalReward),
            primaryText: "领取奖励",
            secondaryText: "离开"
        );

        Open();
    }

    public void ShowGreeting(NPCQuestGiver giver)
    {
        currentGiver = giver;
        currentQuest = null;
        currentMode = DialogueMode.Greeting;

        string line = (giver.greetingLines != null && giver.greetingLines.Length > 0)
            ? giver.greetingLines[Random.Range(0, giver.greetingLines.Length)]
            : "……";

        SetupUI(
            npcName: GetNpcName(giver),
            dialogue: line,
            questInfo: "",
            progress: "",
            reward: "",
            primaryText: "离开",
            secondaryText: ""
        );

        Open();
    }

    public void Close()
    {
        currentGiver = null;
        currentQuest = null;
        gameObject.SetActive(false);
        ModalStack.Pop("quest_dialogue");
    }

    // ─── 按钮事件 ───

    private void OnPrimaryClicked()
    {
        switch (currentMode)
        {
            case DialogueMode.Accept:
                if (currentQuest != null)
                    QuestManager.Instance?.AcceptQuest(currentQuest.questId);
                Close();
                break;

            case DialogueMode.InProgress:
                Close();
                break;

            case DialogueMode.StageComplete:
                if (currentQuest != null)
                    QuestManager.Instance?.AdvanceToNextStage(currentQuest.questId);
                Close();
                break;

            case DialogueMode.FinalClaim:
                if (currentQuest != null)
                    QuestManager.Instance?.ClaimFinalReward(currentQuest.questId);
                Close();
                break;

            case DialogueMode.Greeting:
                Close();
                break;
        }
    }

    private void OnSecondaryClicked()
    {
        Close();
    }

    // ─── UI 辅助 ───

    private void SetupUI(string npcName, string dialogue, string questInfo,
                         string progress, string reward,
                         string primaryText, string secondaryText)
    {
        if (npcNameText != null) npcNameText.text = npcName;
        if (dialogueText != null) dialogueText.text = dialogue;

        if (questInfoRoot != null)
            questInfoRoot.gameObject.SetActive(!string.IsNullOrEmpty(questInfo));
        if (questInfoText != null) questInfoText.text = questInfo;
        if (questProgressText != null) questProgressText.text = progress;
        if (rewardText != null) rewardText.text = reward;

        if (primaryButtonText != null) primaryButtonText.text = primaryText;
        if (secondaryButton != null)
            secondaryButton.gameObject.SetActive(!string.IsNullOrEmpty(secondaryText));
        if (secondaryButtonText != null) secondaryButtonText.text = secondaryText;
    }

    private void Open()
    {
        gameObject.SetActive(true);
        ModalStack.Push("quest_dialogue");
    }

    // ─── 工具方法 ───

    private string GetNpcName(NPCQuestGiver giver)
    {
        return giver != null ? giver.npcId : "";
    }

    private string GetQuestTypeName(QuestType type) => type switch
    {
        QuestType.Main => "主线",
        QuestType.Side => "支线",
        QuestType.Temporary => "临时",
        _ => "",
    };

    private string GetStageDialogue(QuestData quest, int stageIndex, System.Func<QuestStage, string> getter)
    {
        if (quest.stages != null && stageIndex < quest.stages.Count)
            return getter(quest.stages[stageIndex]) ?? "";
        return "";
    }

    private int GetStageIndex(QuestData quest, QuestStage stage)
    {
        if (quest.stages == null || stage == null) return -1;
        return quest.stages.IndexOf(stage);
    }

    private string BuildObjectiveText(QuestData quest, int stageIndex)
    {
        if (stageIndex < 0 || quest.stages == null || stageIndex >= quest.stages.Count)
            return "";

        var stage = quest.stages[stageIndex];
        var progress = QuestManager.Instance?.GetStageProgress(quest.questId);
        return BuildObjectiveText(stage.objectives, progress);
    }

    private string BuildObjectiveText(System.Collections.Generic.List<ObjectiveConfig> objectives, int[] progress)
    {
        if (objectives == null) return "";
        string text = "";
        for (int i = 0; i < objectives.Count; i++)
        {
            var obj = objectives[i];
            int cur = (progress != null && i < progress.Length) ? progress[i] : 0;
            string action = obj.type switch
            {
                ObjectiveType.Kill => "击杀",
                ObjectiveType.Collect => "收集",
                ObjectiveType.TalkToNPC => "对话",
                _ => "完成",
            };
            string name = TargetNameResolver.Resolve(obj.type, obj.targetId);
            text += $"{action} {name}  {cur}/{obj.requiredCount}\n";
        }
        return text.TrimEnd('\n');
    }

    private string BuildRewardText(QuestReward stageReward, QuestReward finalReward)
    {
        string text = "";
        if (stageReward != null)
        {
            text += "<color=#FFD700>阶段奖励:</color>\n";
            text += FormatReward(stageReward);
        }
        if (finalReward != null)
        {
            text += "<color=#FF6B35>最终奖励:</color>\n";
            text += FormatReward(finalReward);
        }
        return text.TrimEnd('\n');
    }

    private string FormatReward(QuestReward reward)
    {
        if (reward == null) return "";
        string str = "";
        if (reward.expAmount > 0) str += $"  经验 x{reward.expAmount}\n";
        if (reward.skillPoints > 0) str += $"  技能点 x{reward.skillPoints}\n";
        if (reward.goldAmount > 0)
        {
            var amt = CurrencyFormatter.Split(reward.goldAmount);
            if (amt.gold > 0) str += $"  {amt.gold}金";
            if (amt.silver > 0) str += $" {amt.silver}银";
            if (amt.copper > 0) str += $" {amt.copper}铜";
            str += "\n";
        }
        if (reward.items != null)
        {
            foreach (var item in reward.items)
            {
                if (item?.itemData != null)
                    str += $"  {item.itemData.itemName} x{item.amount}\n";
            }
        }
        return str;
    }
}
