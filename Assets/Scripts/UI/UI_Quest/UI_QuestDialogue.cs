using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 任务对话面板——NPC 交互时的轻量弹窗。
// 五种模式：接取 / 进行中 / 阶段完成 / 最终领奖 / 问候
public class UI_QuestDialogue : MonoBehaviour
{
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

    private NPCBehaviour currentNpc;
    private QuestData currentQuest;
    private DialogueMode currentMode;

    // StageComplete 是否已处理（领奖或已领过）
    private bool stageReadyToAdvance;

    private enum DialogueMode { Accept, InProgress, StageComplete, FinalClaim }

    private void Awake()
    {
        // 注意：不在 Awake 里 SetActive(false)——面板收编后初始 inactive，
        // 首次 Open 的 SetActive(true) 会触发 Awake，若这里再关闭会抵消激活。开局关闭由 UIManager 统一处理。
        if (primaryButton != null)
            primaryButton.onClick.AddListener(OnPrimaryClicked);
        if (secondaryButton != null)
            secondaryButton.onClick.AddListener(OnSecondaryClicked);
    }

    // ═══════════════════════════════════════════════
    //  公开入口
    // ═══════════════════════════════════════════════

    public void ShowForAccept(NPCBehaviour npc, QuestData quest)
    {
        currentNpc = npc;
        currentQuest = quest;
        currentMode = DialogueMode.Accept;

        SetupUI(
            npcName: npc?.npcName ?? "",
            dialogue: "",
            questInfo: BuildQuestInfoText(quest, quest.GetFirstStage()),
            progress: BuildObjectiveText(quest, 0),
            reward: BuildRewardText(quest, 0),
            primaryText: "接受任务",
            secondaryText: "下次再说"
        );
        Open();
    }

    public void ShowForInProgress(NPCBehaviour npc, QuestData quest)
    {
        currentNpc = npc;
        currentQuest = quest;
        currentMode = DialogueMode.InProgress;

        var stage = QuestManager.Instance?.GetCurrentStage(quest.questId);
        var progress = QuestManager.Instance?.GetStageProgress(quest.questId);

        SetupUI(
            npcName: npc?.npcName ?? "",
            dialogue: "",
            questInfo: BuildQuestInfoText(quest, stage),
            progress: progress != null && stage != null
                ? BuildObjectiveText(stage.objectives, progress) : "",
            reward: BuildRewardText(quest),
            primaryText: "知道了",
            secondaryText: "关闭"
        );
        Open();
    }

    public void ShowForStageComplete(NPCBehaviour npc, QuestData quest)
    {
        currentNpc = npc;
        currentQuest = quest;
        currentMode = DialogueMode.StageComplete;

        var stage = QuestManager.Instance?.GetCurrentStage(quest.questId);
        int stageIdx = stage != null && quest.stages != null ? quest.stages.IndexOf(stage) : -1;
        bool rewardAlreadyClaimed = stageIdx >= 0 && QuestManager.Instance != null &&
            QuestManager.Instance.IsStageRewardClaimed(quest.questId, stageIdx);
        stageReadyToAdvance = rewardAlreadyClaimed;

        string primary = rewardAlreadyClaimed
            ? GetAdvanceButtonText(quest, stage)
            : GetClaimButtonText(stage);

        SetupUI(
            npcName: npc?.npcName ?? "",
            dialogue: "",
            questInfo: $"<color=#FFD700>✦ {quest.questName} — 阶段完成!</color>",
            progress: BuildObjectiveText(quest, stageIdx),
            reward: BuildRewardText(quest, stageIdx),
            primaryText: primary,
            secondaryText: "关闭"
        );
        Open();
    }

    public void ShowForFinalClaim(NPCBehaviour npc, QuestData quest)
    {
        currentNpc = npc;
        currentQuest = quest;
        currentMode = DialogueMode.FinalClaim;

        SetupUI(
            npcName: npc?.npcName ?? "",
            dialogue: "",
            questInfo: $"<color=#FFD700>✦ {quest.questName} — 全部完成!</color>",
            progress: "",
            reward: BuildFinalRewardText(quest),
            primaryText: "领取奖励",
            secondaryText: "离开"
        );
        Open();
    }

	    public void Close()
    {
        currentNpc = null;
        currentQuest = null;
        gameObject.SetActive(false);
        ModalStack.Pop("quest_dialogue");
        UI_NpcMenu.TryReopen();
    }

    // ═══════════════════════════════════════════════
    //  按钮点击
    // ═══════════════════════════════════════════════

    private void OnPrimaryClicked()
    {
        switch (currentMode)
        {
            case DialogueMode.Accept:        OnAcceptClicked(); break;
            case DialogueMode.InProgress:    Close(); break;
            case DialogueMode.StageComplete: OnStageCompleteClicked(); break;
            case DialogueMode.FinalClaim:    OnFinalClaimClicked(); break;
        }
    }

    private void OnSecondaryClicked()
    {
        Close();
    }

    private void OnAcceptClicked()
    {
        if (currentQuest != null)
            QuestManager.Instance?.AcceptQuest(currentQuest.questId);
        Close();
    }

    private void OnFinalClaimClicked()
    {
        if (currentQuest != null)
            QuestManager.Instance?.ClaimFinalReward(currentQuest.questId);
        Close();
    }

    // ─── 阶段完成：一次点击完成提交+领奖，自动过渡 ───

    private void OnStageCompleteClicked()
    {
        if (currentQuest == null)
            return;

        if (!stageReadyToAdvance)
        {
            // 收集类先扣物品
            var stage = QuestManager.Instance?.GetCurrentStage(currentQuest.questId);
            if (stage != null && stage.HasCollectObjective)
                QuestManager.Instance?.RemoveCollectItemsForQuest(currentQuest.questId);

            QuestManager.Instance?.ClaimCurrentStageReward(currentQuest.questId);
            stageReadyToAdvance = true;

            // 自动过渡到下一状态
            TransitionAfterClaim();
        }
        else
        {
            AdvanceOrFinish();
        }
    }

    private void TransitionAfterClaim()
    {
        var stage = QuestManager.Instance?.GetCurrentStage(currentQuest.questId);
        bool hasNext = stage != null && currentQuest.stages != null &&
                       currentQuest.stages.IndexOf(stage) < currentQuest.stages.Count - 1;

        if (hasNext && QuestManager.Instance?.AdvanceToNextStage(currentQuest.questId) == true)
            SwitchToInProgress();
        else if (HasFinalReward(currentQuest))
            SwitchToFinalClaim();
        else
            Close();
    }

    private void AdvanceOrFinish()
    {
        if (QuestManager.Instance?.AdvanceToNextStage(currentQuest.questId) == true)
            SwitchToInProgress();
        else if (HasFinalReward(currentQuest))
            SwitchToFinalClaim();
        else
            Close();
    }

    // ═══════════════════════════════════════════════
    //  状态切换
    // ═══════════════════════════════════════════════

    private void SwitchToInProgress()
    {
        var quest = currentQuest;
        currentMode = DialogueMode.InProgress;
        var newStage = QuestManager.Instance?.GetCurrentStage(quest.questId);
        var newProgress = QuestManager.Instance?.GetStageProgress(quest.questId);

        SetupUI(
            npcName: GetNpcName(currentNpc),
            dialogue: "",
            questInfo: BuildQuestInfoText(quest, newStage),
            progress: newProgress != null && newStage != null
                ? BuildObjectiveText(newStage.objectives, newProgress) : "",
            reward: BuildRewardText(quest),
            primaryText: "知道了",
            secondaryText: "关闭"
        );
    }

    private void SwitchToFinalClaim()
    {
        currentMode = DialogueMode.FinalClaim;
        SetupUI(
            npcName: GetNpcName(currentNpc),
            dialogue: "",
            questInfo: $"<color=#FFD700>✦ {currentQuest.questName} — 全部完成!</color>",
            progress: "",
            reward: BuildFinalRewardText(currentQuest),
            primaryText: "领取奖励",
            secondaryText: "离开"
        );
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    private void SetupUI(string npcName, string dialogue, string questInfo,
                         string progress, string reward,
                         string primaryText, string secondaryText)
    {
        if (npcNameText != null)
            npcNameText.text = npcName;
        if (dialogueText != null)
            dialogueText.text = dialogue;
        if (questInfoRoot != null)
            questInfoRoot.gameObject.SetActive(!string.IsNullOrEmpty(questInfo));
        if (questInfoText != null)
            questInfoText.text = questInfo;
        if (questProgressText != null)
            questProgressText.text = progress;
        if (rewardText != null)
            rewardText.text = reward;
        if (primaryButtonText != null)
            primaryButtonText.text = primaryText;
        if (secondaryButton != null)
            secondaryButton.gameObject.SetActive(!string.IsNullOrEmpty(secondaryText));
        if (secondaryButtonText != null)
            secondaryButtonText.text = secondaryText;
    }

    private void Open()
    {
        gameObject.SetActive(true);
        ModalStack.Push("quest_dialogue");
    }

    private static string GetNpcName(NPCBehaviour npc)
    {
        return npc != null ? (npc.npcName ?? "") : "";
    }


    private static string GetClaimButtonText(QuestStage stage)
    {
        return stage != null && stage.HasCollectObjective ? "提交并领取奖励" : "领取奖励";
    }

    private static string GetAdvanceButtonText(QuestData quest, QuestStage stage)
    {
        bool hasNext = stage != null && quest.stages != null &&
                       quest.stages.IndexOf(stage) < quest.stages.Count - 1;
        if (hasNext)
            return "继续推进";
        if (HasFinalReward(quest))
            return "领取最终奖励";
        return "离开";
    }

    private static bool HasFinalReward(QuestData quest)
    {
        return quest.finalReward != null && quest.finalReward.HasReward;
    }

    // ═══════════════════════════════════════════════
    //  文本构建
    // ═══════════════════════════════════════════════

    private static string BuildQuestInfoText(QuestData quest, QuestStage stage)
    {
        string color = QuestUIUtility.GetQuestTypeColor(quest.questType);
        string typeName = QuestUIUtility.GetQuestTypeName(quest.questType);
        string text = $"<color={color}>[{typeName}]</color> {quest.questName}";
        if (stage != null && !string.IsNullOrEmpty(stage.description))
            text += $"\n<color=#AAAAAA>- {stage.description}</color>";
        return text;
    }

    // ─── 目标文本 ───

    private static string BuildObjectiveText(QuestData quest, int stageIndex)
    {
        if (stageIndex < 0 || quest.stages == null || stageIndex >= quest.stages.Count)
            return "";
        var stage = quest.stages[stageIndex];
        var progress = QuestManager.Instance?.GetStageProgress(quest.questId);
        return BuildObjectiveText(stage.objectives, progress);
    }

    private static string BuildObjectiveText(
        System.Collections.Generic.List<ObjectiveConfig> objectives, int[] progress)
    {
        if (objectives == null)
            return "";
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
            text += $"<color=#CCCCCC>{action}</color> {name}  <color=#FFD700>{cur}</color>/{obj.requiredCount}\n";
        }
        return text.TrimEnd('\n');
    }

    // ─── 奖励文本（三种场景，清晰独立） ───

    /// <summary>显示指定阶段的奖励 + 最终奖励</summary>
    private static string BuildRewardText(QuestData quest, int stageIndex)
    {
        string str = "";
        var stage = quest.GetStage(stageIndex);
        if (stage?.stageReward != null)
        {
            string f = QuestUIUtility.FormatReward(stage.stageReward);
            if (!string.IsNullOrEmpty(f))
                str += $"<color=white>阶段{QuestUIUtility.GetStageCnx(stageIndex)}奖励:</color>\n<color=#FFD700>{f}</color>\n";
        }
        str += BuildFinalRewardText(quest);
        return str.TrimEnd('\n');
    }

    /// <summary>显示当前活跃阶段的奖励 + 最终奖励（InProgress 模式）</summary>
    private static string BuildRewardText(QuestData quest)
    {
        string str = "";
        var curStage = QuestManager.Instance?.GetCurrentStage(quest.questId);
        if (curStage?.stageReward != null)
        {
            string f = QuestUIUtility.FormatReward(curStage.stageReward);
            if (!string.IsNullOrEmpty(f))
                str += $"<color=white>阶段奖励:</color>\n<color=#FFD700>{f}</color>\n";
        }
        str += BuildFinalRewardText(quest);
        return str.TrimEnd('\n');
    }

    /// <summary>仅最终奖励（FinalClaim 模式）</summary>
    private static string BuildFinalRewardText(QuestData quest)
    {
        if (quest.finalReward == null)
            return "";
        string f = QuestUIUtility.FormatReward(quest.finalReward);
        return !string.IsNullOrEmpty(f) ? $"<color=white>最终奖励:</color>\n<color=#FF6B35>{f}</color>\n" : "";
    }
}
