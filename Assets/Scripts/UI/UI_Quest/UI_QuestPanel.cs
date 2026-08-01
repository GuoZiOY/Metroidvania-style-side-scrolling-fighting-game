using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 任务日志面板——只读。
// 按类型分页签显示任务列表，点击查看详情和进度。
// 不包含接受/提交/领奖操作，全部统一走 NPC 对话。
public class UI_QuestPanel : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject panelRoot;

    [Header("任务列表")]
    [SerializeField] private RectTransform questListContent;
    [SerializeField] private GameObject questEntryPrefab;

    [Header("分类页签")]
    [SerializeField] private Button tabMain;
    [SerializeField] private Button tabSide;
    [SerializeField] private Button tabTemporary;

    [Header("详情")]
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescText;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button trackButton;

    private string selectedQuestId;
    private QuestType currentTab = QuestType.Main;
    private Button selectedEntryButton; // 当前选中的任务条目（点击放大保持，切换/刷新时恢复）

    private void Awake()
    {
        if (trackButton != null)
            trackButton.onClick.AddListener(OnTrackClicked);

        if (tabMain != null)
            tabMain.onClick.AddListener(() => SwitchTab(QuestType.Main));
        if (tabSide != null)
            tabSide.onClick.AddListener(() => SwitchTab(QuestType.Side));
        if (tabTemporary != null)
            tabTemporary.onClick.AddListener(() => SwitchTab(QuestType.Temporary));
    }

    private void OnEnable()
    {
        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        qm.OnQuestAccepted += OnQuestEvent;
        qm.OnQuestStageChanged += OnQuestEvent;
        qm.OnQuestReadyToClaim += OnQuestEvent;
        qm.OnQuestClaimed += OnQuestEvent;
        qm.OnQuestFailed += OnQuestEvent;
        qm.OnObjectiveUpdated += OnObjectiveUpdated;
        qm.OnTrackChanged += OnQuestEvent;

        Refresh();
    }

    private void OnDisable()
    {
        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        qm.OnQuestAccepted -= OnQuestEvent;
        qm.OnQuestStageChanged -= OnQuestEvent;
        qm.OnQuestReadyToClaim -= OnQuestEvent;
        qm.OnQuestClaimed -= OnQuestEvent;
        qm.OnQuestFailed -= OnQuestEvent;
        qm.OnObjectiveUpdated -= OnObjectiveUpdated;
        qm.OnTrackChanged -= OnQuestEvent;
    }

    private void OnQuestEvent(string _)
    {
        Refresh();
    }

    private void OnObjectiveUpdated(string questId, int index, int current)
    {
        if (questId == selectedQuestId)
            UpdateDetailPanel();
    }

    private void SwitchTab(QuestType type)
    {
        currentTab = type;
        Refresh();
    }

    // ─── 公开方法 ───

    public void Open()
    {
        panelRoot.SetActive(true);
        Refresh();
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

    // ─── 刷新 ───

    private void Refresh()
    {
        selectedQuestId = null;
        RebuildList();

        if (questListContent.childCount > 0)
        {
            var first = questListContent.GetChild(0).GetComponent<Button>();
            if (first != null)
                first.onClick.Invoke();
        }

        UpdateDetailPanel();
    }

    private void RebuildList()
    {
        foreach (Transform child in questListContent)
            Destroy(child.gameObject);
        selectedEntryButton = null; // 旧条目销毁，清选中引用防残留

        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        var quests = GetQuestListByType(qm, currentTab);

        foreach (var quest in quests)
        {
            if (quest == null)
                continue;

            var entry = Instantiate(questEntryPrefab, questListContent);
            AudioManager.Instance?.RegisterButton(entry.GetComponent<Button>());

            var text = entry.GetComponentInChildren<TMP_Text>();
            string status = GetQuestStatus(quest.questId);
            if (text != null)
                text.text = $"{quest.questName}  {status}";

            string capturedId = quest.questId;
            var btn = entry.GetComponent<Button>();
            btn.onClick.AddListener(() => OnEntryClicked(capturedId, btn));
        }
    }

    private List<QuestData> GetQuestListByType(QuestManager qm, QuestType type)
    {
        var result = new List<QuestData>();
        foreach (var quest in qm.GetAllQuestData())
        {
            if (quest.questType != type)
                continue;
            if (!qm.IsActive(quest.questId) && !qm.IsReadyToClaim(quest.questId) &&
                !qm.IsCompleted(quest.questId) && !qm.IsFailed(quest.questId))
                continue;
            result.Add(quest);
        }
        return result;
    }

    private static string GetQuestStatus(string questId)
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
        return "";
    }

    // ─── 条目点击 ───

    // 选中视觉：点击后当前条目保持放大，切换条目时恢复上一个（关闭/刷新由 RebuildList 清引用防残留）
    private void OnEntryClicked(string questId, Button btn)
    {
        if (selectedEntryButton != null && selectedEntryButton != btn)
            SetEntrySelected(selectedEntryButton, false);
        selectedEntryButton = btn;
        SetEntrySelected(btn, true);

        selectedQuestId = questId;
        UpdateDetailPanel();
    }

    // 条目选中视觉：走 UI_ButtonEffect（hover 感知选中态），无 effect 时直接缩放
    private void SetEntrySelected(Button btn, bool selected)
    {
        if (btn == null)
            return;
        var effect = btn.GetComponent<UI_ButtonEffect>();
        if (effect != null)
        {
            effect.SetSelected(selected);
            return;
        }
        btn.transform.DOKill();
        if (selected)
            btn.transform.DOScale(Vector3.one * 1.05f, 0.15f).SetEase(Ease.OutQuad);
        else
            btn.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad);
    }

    // ─── 详情面板 ───

    private void UpdateDetailPanel()
    {
        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        if (string.IsNullOrEmpty(selectedQuestId) || qm.GetQuestData(selectedQuestId) == null)
        {
            ClearDetail();
            return;
        }

        var quest = qm.GetQuestData(selectedQuestId);
        var stage = qm.GetCurrentStage(selectedQuestId);
        var progress = qm.GetStageProgress(selectedQuestId);

        UpdateQuestHeader(quest);
        UpdateStageDesc(stage);
        UpdateObjectiveDisplay(stage, progress);
        UpdateRewardDisplay(quest, stage);
        UpdateTrackButton();
    }

    private void UpdateQuestHeader(QuestData quest)
    {
        if (detailNameText == null)
            return;

        string typeColor = QuestUIUtility.GetQuestTypeColor(quest.questType);
        string typeName = QuestUIUtility.GetQuestTypeName(quest.questType);
        detailNameText.text =
            $"<color={typeColor}>[{typeName}]</color> {quest.questName}  {GetQuestStatus(selectedQuestId)}";
    }

    private void UpdateStageDesc(QuestStage stage)
    {
        if (detailDescText != null)
            detailDescText.text = stage != null ? stage.description : "";
    }

    private void UpdateObjectiveDisplay(QuestStage stage, int[] progress)
    {
        if (objectiveText == null)
            return;

        string text = "";
        if (stage?.objectives != null)
        {
            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var obj = stage.objectives[i];
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
        }
        objectiveText.text = text.TrimEnd('\n');
    }

    private void UpdateRewardDisplay(QuestData quest, QuestStage stage)
    {
        if (rewardText == null)
            return;

        string str = "";
        int stageIdx = stage != null && quest.stages != null ? quest.stages.IndexOf(stage) : -1;

        if (stage != null && stageIdx >= 0 && stage.stageReward != null)
        {
            string f = QuestUIUtility.FormatReward(stage.stageReward);
            if (!string.IsNullOrEmpty(f))
            {
                string cn = QuestUIUtility.GetStageCnx(stageIdx);
                str += $"<color=white>阶段{cn}奖励:</color>\n<color=#FFD700>{f}</color>\n";
            }
        }
        if (quest.finalReward != null)
        {
            string f = QuestUIUtility.FormatReward(quest.finalReward);
            if (!string.IsNullOrEmpty(f))
                str += $"<color=white>最终奖励:</color>\n<color=#FF6B35>{f}</color>\n";
        }

        rewardText.text = str.TrimEnd('\n');
        rewardText.gameObject.SetActive(!string.IsNullOrEmpty(str));
    }

    private void ClearDetail()
    {
        if (detailNameText != null)
            detailNameText.text = "";
        if (detailDescText != null)
            detailDescText.text = "";
        if (objectiveText != null)
            objectiveText.text = "";
        if (rewardText != null)
            rewardText.text = "";
        SetTrackButtonState("", false, false);
    }

    // ─── 追踪 ───

    private void OnTrackClicked()
    {
        if (string.IsNullOrEmpty(selectedQuestId))
            return;

        QuestManager.Instance?.ToggleTrack(selectedQuestId);
    }

    private void UpdateTrackButton()
    {
        var qm = QuestManager.Instance;
        if (qm == null || string.IsNullOrEmpty(selectedQuestId))
        {
            SetTrackButtonState("", false, false);
            return;
        }

        bool tracked = qm.IsTracked(selectedQuestId);
        bool active = qm.IsActive(selectedQuestId) || qm.IsReadyToClaim(selectedQuestId);
        SetTrackButtonState("追踪", active, tracked);
    }

    private void SetTrackButtonState(string text, bool interactable, bool tracked)
    {
        if (trackButton == null)
            return;

        trackButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        trackButton.interactable = interactable;

        var btnText = trackButton.GetComponentInChildren<TMP_Text>();
        if (btnText != null)
            btnText.text = tracked ? "追踪中" : text;

        var img = trackButton.GetComponent<Image>();
        if (img != null)
            img.color = tracked ? Color.yellow : Color.white;
    }
}
