using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 任务面板 UI。左侧列表 + 右侧详情。
// 显示所有任务的状态、进度、奖励，以及接受/提交/领取操作。
public class UI_QuestPanel : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject panelRoot;

    [Header("任务列表")]
    [SerializeField] private ScrollRect questScrollView;
    [SerializeField] private GameObject questEntryPrefab;

    [Header("详情")]
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescText;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button claimButton;    // 领取奖励
    [SerializeField] private Button acceptButton;    // 接受/提交/放弃
    [SerializeField] private Button trackButton;     // 标记/取消标记
    [SerializeField] private RectTransform detailContent;  // 详情内容容器（用于滑动静画）

    private string selectedQuestId;
    private int selectedQuestIndex = -1;
    private bool isProcessingAction;
    private Sequence detailSeq;
    private Vector2 detailOriginalPos;
    private bool _detailPosRecorded;
    private List<string> questOrder = new List<string>();

    private void Awake()
    {
        claimButton.onClick.AddListener(OnClaimClicked);
        if (acceptButton != null)
            acceptButton.onClick.AddListener(OnAcceptClicked);
        if (trackButton != null)
            trackButton.onClick.AddListener(OnTrackClicked);
    }

    private void OnEnable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestAccepted += OnQuestEvent;
            QuestManager.Instance.OnQuestReadyToClaim += OnQuestEvent;
            QuestManager.Instance.OnQuestClaimed += OnQuestEvent;
            QuestManager.Instance.OnQuestFailed += OnQuestEvent;
            QuestManager.Instance.OnObjectiveUpdated += OnObjectiveUpdated;
            QuestManager.Instance.OnTrackChanged += OnQuestEvent;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestAccepted -= OnQuestEvent;
            QuestManager.Instance.OnQuestReadyToClaim -= OnQuestEvent;
            QuestManager.Instance.OnQuestClaimed -= OnQuestEvent;
            QuestManager.Instance.OnQuestFailed -= OnQuestEvent;
            QuestManager.Instance.OnObjectiveUpdated -= OnObjectiveUpdated;
            QuestManager.Instance.OnTrackChanged -= OnQuestEvent;
        }
    }

    private void OnQuestEvent(string _)
    {
        if (!isProcessingAction)
            Refresh();
    }

    private void OnObjectiveUpdated(string questId, int index, int current)
    {
        if (questId == selectedQuestId)
            UpdateDetailPanel();
    }

    public void Open()
    {
        panelRoot.SetActive(true);
    }

    public void Close()
    {
        panelRoot.SetActive(false);
        selectedQuestId = null;
    }

    public void Toggle()
    {
        if (panelRoot.activeSelf)
            Close();
        else
            Open();
    }

    private void Refresh(bool keepSelection = false)
    {
        string pendingId = keepSelection ? selectedQuestId : null;
        selectedQuestId = null;
        RebuildList();

        if (pendingId != null && questScrollView.content.childCount > 0)
            selectedQuestId = pendingId;

        if (selectedQuestId == null && questScrollView.content.childCount > 0)
        {
            var firstEntry = questScrollView.content.GetChild(0).GetComponent<Button>();
            if (firstEntry != null)
                firstEntry.onClick.Invoke();
        }

        UpdateDetailPanel();
    }

    private void RebuildList()
    {
        foreach (Transform child in questScrollView.content)
            Destroy(child.gameObject);

        var qm = QuestManager.Instance;
        if (qm == null) return;

        questOrder.Clear();
        foreach (var quest in qm.GetAllQuestData())
        {
            questOrder.Add(quest.questId);
            var entry = Instantiate(questEntryPrefab, questScrollView.content);
            AudioManager.Instance?.RegisterButton(entry.GetComponent<Button>());
            var text = entry.GetComponentInChildren<TMP_Text>();
            string status = GetQuestStatus(quest.questId);
            if (text != null)
                text.text = $"{quest.questName}  {status}";

            string capturedId = quest.questId;
            entry.GetComponent<Button>().onClick.AddListener(() => OnEntryClicked(capturedId));
        }
    }

    private string GetQuestStatus(string questId)
    {
        var qm = QuestManager.Instance;
        if (qm == null)
            return "";

        if (qm.IsFailed(questId))
            return "<color=red>[失败]</color>";
        if (qm.IsReadyToClaim(questId))
            return "<color=#FFD700>[可领取]</color>";
        if (qm.IsActive(questId))
            return "<color=#A5D6A7>[进行中]</color>";
        if (qm.IsCompleted(questId))
            return "<color=yellow>[已完成]</color>";

        return "<color=grey>[可接取]</color>";
    }

    private void OnEntryClicked(string questId)
    {
        selectedQuestId = questId;
        int newIndex = questOrder.IndexOf(questId);
        // 确保 CanvasGroup 存在（DOFade 需要）
        if (detailContent != null && detailContent.GetComponent<CanvasGroup>() == null)
            detailContent.gameObject.AddComponent<CanvasGroup>();
        if (selectedQuestIndex < 0 || newIndex == selectedQuestIndex)
        {
            selectedQuestIndex = newIndex;
            UpdateDetailPanel();
            return;
        }
        bool slideDown = newIndex > selectedQuestIndex;
        selectedQuestIndex = newIndex;
        AnimateDetailTransition(slideDown);
    }

    private void AnimateDetailTransition(bool slideDown)
    {
        if (detailContent == null) { UpdateDetailPanel(); return; }

        // 只记录一次真实原点，防止连续点击累积偏移
        if (!_detailPosRecorded)
        {
            detailOriginalPos = detailContent.anchoredPosition;
            _detailPosRecorded = true;
        }

        // 终止旧动画，回到真实原点
        detailSeq?.Kill();
        detailContent.anchoredPosition = detailOriginalPos;

        float slideH = Mathf.Max(detailContent.rect.height * 0.75f, 50f);
        float fromY = slideDown ? slideH : -slideH;

        detailSeq = DOTween.Sequence();

        // 滑出（下一任务向上滑，上一任务向下滑）
        detailSeq.Append(detailContent.DOAnchorPosY(detailOriginalPos.y - fromY, 0.15f));
        detailSeq.Join(detailContent.GetComponent<CanvasGroup>().DOFade(0, 0.12f));

        // 更新文本，复位到滑入起始位置
        detailSeq.AppendCallback(() =>
        {
            UpdateDetailPanel();
            detailContent.anchoredPosition = detailOriginalPos + new Vector2(0, fromY);
        });

        // 滑入
        detailSeq.Append(detailContent.DOAnchorPosY(detailOriginalPos.y, 0.2f).SetEase(Ease.OutCubic));
        detailSeq.Join(detailContent.GetComponent<CanvasGroup>().DOFade(1, 0.18f));
    }

    private void UpdateDetailPanel()
    {
        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        if (HasNoSelection(qm))
        {
            ClearDetailPanel();
            return;
        }

        var quest = qm.GetQuestData(selectedQuestId);
        var progress = qm.GetProgress(selectedQuestId);

        SetQuestHeader(quest);
        UpdateObjectiveDisplay(quest, progress);
        UpdateRewardDisplay(quest.reward);
        UpdateButtonStates(quest, qm);
    }

    private bool HasNoSelection(QuestManager qm)
    {
        return string.IsNullOrEmpty(selectedQuestId) || qm.GetQuestData(selectedQuestId) == null;
    }

    private void ClearDetailPanel()
    {
        if (detailNameText != null)  detailNameText.text = "";
        if (detailDescText != null)  detailDescText.text = "";
        if (objectiveText != null)   objectiveText.text = "";
        if (rewardText != null)      rewardText.text = "";
        SetButtonState(claimButton, "", false);
        SetButtonState(acceptButton, "", false);
        SetTrackButtonState("", false, false);
    }

    private void SetQuestHeader(QuestData quest)
    {
        if (detailNameText != null)
            detailNameText.text = $"{quest.questName}  {GetQuestStatus(selectedQuestId)}";
        if (detailDescText != null)
            detailDescText.text = quest.description;
    }

    private void UpdateObjectiveDisplay(QuestData quest, QuestManager.QuestProgress progress)
    {
        string objText = "";
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var obj = quest.objectives[i];
            int current = progress?.objectiveProgress[i] ?? 0;
            string action = obj.type == ObjectiveType.Kill ? "击杀" : "提交";
            string name = TargetNameResolver.Resolve(obj.type, obj.targetId);
            objText += $"{action} {name} x{obj.requiredCount} (<color=#FFD700>{current}</color>/{obj.requiredCount})";
        }
        if (objectiveText != null)
            objectiveText.text = objText.TrimEnd('\n');
    }

    private void UpdateRewardDisplay(QuestReward reward)
    {
        if (rewardText == null)
            return;

        string str = "奖励：\n";
        if (reward.expAmount > 0)
            str += $"<color=#4FC3F7>经验</color> x<b>{reward.expAmount}</b>\n";
        if (reward.skillPoints > 0)
            str += $"<color=#81C784>技能点</color> x<b>{reward.skillPoints}</b>\n";
        if (reward.items != null)
        {
            foreach (var rewardItem in reward.items)
            {
                if (rewardItem != null && rewardItem.itemData != null)
                    str += $"{rewardItem.itemData.itemName} x<b>{rewardItem.amount}</b>\n";
            }
        }
        rewardText.text = str.TrimEnd('\n');
    }

    private void UpdateButtonStates(QuestData quest, QuestManager qm)
    {
        string claimText, acceptText;
        bool claimInteractable, acceptInteractable;

        if (qm.IsReadyToClaim(selectedQuestId))
        {
            claimText = "领取奖励"; claimInteractable = true;
            acceptText = "已完成"; acceptInteractable = false;
        }
        else if (qm.IsActive(selectedQuestId))
        {
            claimText = "进行中"; claimInteractable = false;
            bool allDone = AllObjectivesComplete(quest, qm.GetProgress(selectedQuestId));
            acceptText = allDone ? "提交任务" : "放弃任务"; acceptInteractable = true;
        }
        else if (qm.IsCompleted(selectedQuestId) || qm.IsFailed(selectedQuestId))
        {
            string status = qm.IsCompleted(selectedQuestId) ? "已完成" : "已失败";
            claimText = status; claimInteractable = false;
            acceptText = status; acceptInteractable = false;
        }
        else
        {
            claimText = "待接取"; claimInteractable = false;
            acceptText = "接受任务"; acceptInteractable = true;
        }

        SetButtonState(claimButton, claimText, claimInteractable);
        SetButtonState(acceptButton, acceptText, acceptInteractable);

        // 标记按钮
        UpdateTrackButton();
    }

    private void UpdateTrackButton()
    {
        var qm = QuestManager.Instance;
        if (qm == null || string.IsNullOrEmpty(selectedQuestId) || qm.GetQuestData(selectedQuestId) == null)
        {
            SetTrackButtonState("", false, false);
            return;
        }

        bool tracked = qm.IsTracked(selectedQuestId);
        SetTrackButtonState("追踪", true, tracked);
    }

    private void SetTrackButtonState(string text, bool interactable, bool tracked)
    {
        if (trackButton == null) return;

        trackButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        trackButton.interactable = interactable;
        TMP_Text btnText = trackButton.GetComponentInChildren<TMP_Text>();
        if (btnText != null)
            btnText.text = text;

        Image img = trackButton.GetComponent<Image>();
        if (img != null)
            img.color = tracked ? Color.yellow : Color.white;
    }

    private void OnClaimClicked()
    {
        if (string.IsNullOrEmpty(selectedQuestId))
            return;

        isProcessingAction = true;
        QuestManager.Instance?.ClaimQuest(selectedQuestId);
        isProcessingAction = false;
        Refresh(true);
    }

    private void OnAcceptClicked()
    {
        if (string.IsNullOrEmpty(selectedQuestId))
            return;

        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        isProcessingAction = true;

        if (qm.IsActive(selectedQuestId))
        {
            var quest = qm.GetQuestData(selectedQuestId);
            if (quest != null && AllObjectivesComplete(quest, qm.GetProgress(selectedQuestId)))
                qm.SubmitQuest(selectedQuestId);
            else
                qm.MarkQuestFailed(selectedQuestId);
        }
        else
        {
            qm.AcceptQuest(selectedQuestId);
        }

        isProcessingAction = false;
        Refresh(true);
    }

    private void OnTrackClicked()
    {
        if (string.IsNullOrEmpty(selectedQuestId))
            return;

        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        isProcessingAction = true;
        qm.ToggleTrack(selectedQuestId);
        isProcessingAction = false;
        Refresh(true);
    }

    private bool AllObjectivesComplete(QuestData quest, QuestManager.QuestProgress progress)
    {
        if (quest == null || progress == null)
            return false;
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            if (progress.objectiveProgress[i] < quest.objectives[i].requiredCount)
                return false;
        }
        return true;
    }

    private void SetButtonState(Button btn, string text, bool interactable)
    {
        if (btn == null)
            return;

        btn.gameObject.SetActive(true);
        btn.interactable = interactable;
        TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>();

        if (btnText != null)
            btnText.text = text;
    }
}
